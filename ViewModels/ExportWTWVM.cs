using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZC = NCC.PRZTools.PRZConstants;
using PRZH = NCC.PRZTools.PRZHelper;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.Converters;
using CsvHelper;
using CsvHelper.Configuration;

namespace NCC.PRZTools
{
    public class ExportWTWVM : PropertyChangedBase
    {
        public ExportWTWVM()
        {
        }

        #region FIELDS

        private CancellationTokenSource _cts = null;

        private bool _boundaryTableExists = false;
        private bool _puDataExists = false;

        private bool _settings_Rad_SpatialFormat_Vector_IsChecked;
        private bool _settings_Rad_SpatialFormat_Raster_IsChecked;

        private string _txt_PlanningUnitLabel;
        private string _txt_BoundaryLengthsLabel;

        private readonly SpatialReference Export_SR = SpatialReferences.WGS84;

        private bool _exportIsEnabled;

        private string _compStat_Img_PU = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
        private string _compStat_Img_BoundaryLengths = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";

        private ICommand _cmdExport;
        private ICommand _cmdClearLog;
        private ICommand _cmdCancel;

        private Visibility _operationStatus_Img_Visibility = Visibility.Visible;
        private string _operationStatus_Txt_Label;

        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""

        #endregion

        #region PROPERTIES

        public Visibility OperationStatus_Img_Visibility
        {
            get => _operationStatus_Img_Visibility;
            set => SetProperty(ref _operationStatus_Img_Visibility, value, () => OperationStatus_Img_Visibility);
        }

        public string OperationStatus_Txt_Label
        {
            get => _operationStatus_Txt_Label;
            set => SetProperty(ref _operationStatus_Txt_Label, value, () => OperationStatus_Txt_Label);
        }

        public bool Settings_Rad_SpatialFormat_Vector_IsChecked
        {
            get => _settings_Rad_SpatialFormat_Vector_IsChecked;
            set
            {
                SetProperty(ref _settings_Rad_SpatialFormat_Vector_IsChecked, value, () => Settings_Rad_SpatialFormat_Vector_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.WTW_SPATIAL_FORMAT = "VECTOR";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool Settings_Rad_SpatialFormat_Raster_IsChecked
        {
            get => _settings_Rad_SpatialFormat_Raster_IsChecked;
            set
            {
                SetProperty(ref _settings_Rad_SpatialFormat_Raster_IsChecked, value, () => Settings_Rad_SpatialFormat_Raster_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.WTW_SPATIAL_FORMAT = "RASTER";
                    Properties.Settings.Default.Save();
                }
            }
        }

        public string Txt_PlanningUnitLabel
        {
            get => _txt_PlanningUnitLabel;
            set => SetProperty(ref _txt_PlanningUnitLabel, value, () => Txt_PlanningUnitLabel);
        }

        public string Txt_BoundaryLengthsLabel
        {
            get => _txt_BoundaryLengthsLabel;
            set => SetProperty(ref _txt_BoundaryLengthsLabel, value, () => Txt_BoundaryLengthsLabel);
        }

        public bool ExportIsEnabled
        {
            get => _exportIsEnabled;
            set => SetProperty(ref _exportIsEnabled, value, () => ExportIsEnabled);
        }

        public string CompStat_Img_PU
        {
            get => _compStat_Img_PU;
            set => SetProperty(ref _compStat_Img_PU, value, () => CompStat_Img_PU);
        }

        public string CompStat_Img_BoundaryLengths
        {
            get => _compStat_Img_BoundaryLengths;
            set => SetProperty(ref _compStat_Img_BoundaryLengths, value, () => CompStat_Img_BoundaryLengths);
        }

        public ProgressManager PM
        {
            get => _pm;
            set => SetProperty(ref _pm, value, () => PM);
        }

        #endregion

        #region COMMANDS

        public ICommand CmdExport => _cmdExport ?? (_cmdExport = new RelayCommand(async () =>
        {
            // Operation Status indicators
            OperationStatus_Img_Visibility = Visibility.Visible;
            OperationStatus_Txt_Label = "Processing...";

            // Start the operation
            using (_cts = new CancellationTokenSource())
            {
                await ExportWTWPackage(_cts.Token);
            }

            // Set back to null (already disposed)
            _cts = null;

            // Idle indicators
            OperationStatus_Img_Visibility = Visibility.Hidden;
            OperationStatus_Txt_Label = "Idle...";

        }, () => true));

        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true));

        public ICommand CmdCancel => _cmdCancel ?? (_cmdCancel = new RelayCommand(() =>
        {
            if (_cts != null)
            {
                // Optionally notify the user or prompt the user here

                // Cancel the operation
                _cts.Cancel();
            }

        }, () => _cts != null, true, false));

        #endregion

        #region METHODS

        public async Task OnProWinLoaded()
        {
            try
            {
                // Clear the Progress Bar
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                // Spatial Format Radio Buttons
                string spatial_format = Properties.Settings.Default.WTW_SPATIAL_FORMAT;
                if (string.IsNullOrEmpty(spatial_format) || spatial_format == "RASTER")
                {
                    Settings_Rad_SpatialFormat_Raster_IsChecked = true;
                }
                else if (spatial_format == "VECTOR")
                {
                    Settings_Rad_SpatialFormat_Vector_IsChecked = true;
                }
                else
                {
                    Settings_Rad_SpatialFormat_Raster_IsChecked = true;
                }

                // Configure a few controls
                await ValidateControls();
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task ExportWTWPackage(CancellationToken token)
        {
            bool edits_are_disabled = !Project.Current.IsEditingEnabled;
            int val = 0;
            int max = 50;

            try
            {
                #region INITIALIZATION

                #region EDITING CHECK

                // Check for currently unsaved edits in the project
                if (Project.Current.HasEdits)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro Project has unsaved edits.  Please save all edits before proceeding.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("This ArcGIS Pro Project has some unsaved edits.  Please save all edits before proceeding.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro Project has no unsaved edits.  Proceeding..."), true, ++val);
                }

                // If editing is disabled, enable it temporarily (and disable again in the finally block)
                if (edits_are_disabled)
                {
                    if (!await Project.Current.SetIsEditingEnabledAsync(true))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to enabled editing for this ArcGIS Pro Project.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Unable to enabled editing for this ArcGIS Pro Project.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing enabled."), true, ++val);
                    }
                }

                #endregion

                // Initialize a few objects and names
                Map map = MapView.Active.Map;
                string gdbpath = PRZH.GetPath_ProjectGDB();
                string export_folder_path = PRZH.GetPath_ExportWTWFolder();

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags_GPRefresh = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread;
                string toolOutput;

                // Initialize ProgressBar and Progress Log
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Where to Work Exporter..."), false, max, ++val);

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Ensure the Project Geodatabase Exists
                var try_gdbexists = await PRZH.GDBExists_Project();
                if (!try_gdbexists.exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Project Geodatabase not found: {gdbpath}", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Project Geodatabase not found at {gdbpath}.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Project Geodatabase found at {gdbpath}."), true, ++val);
                }

                // Ensure the ExportWTW folder exists
                if (!PRZH.FolderExists_ExportWTW().exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_DIR_EXPORT_WTW} folder not found in project workspace.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"{PRZC.c_DIR_EXPORT_WTW} folder not found in project workspace.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_DIR_EXPORT_WTW} folder found."), true, ++val);
                }

                // Validate Existence/Type of Planning Unit Spatial Data, capture infos
                var pu_result = await PRZH.PUExists();
                string pu_path = "";            // path to data
                SpatialReference PU_SR = null;  // PU spatial reference
                double cell_size = 0;           // Only applies to raster PU

                if (!pu_result.exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit layer not found in project geodatabase.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Planning Unit layer not found in project geodatabase.");
                    return;
                }
                else if (pu_result.puLayerType == PlanningUnitLayerType.UNKNOWN)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit layer format unknown.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Planning Unit layer format unknown.");
                    return;
                }
                else if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                {
                    // Ensure data present
                    if (!(await PRZH.FCExists_Project(PRZC.c_FC_PLANNING_UNITS)).exists)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Planning Unit feature class not found.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Planning Unit feature class not found.  Have you built it yet?");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit feature class found."), true, ++val);
                    }

                    // Get path
                    pu_path = PRZH.GetPath_Project(PRZC.c_FC_PLANNING_UNITS).path;

                    // Get Spatial Reference
                    await QueuedTask.Run(() =>
                    {
                        var tryget = PRZH.GetFC_Project(PRZC.c_FC_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            throw new Exception("Error retrieving feature class.");
                        }

                        using (FeatureClass FC = tryget.featureclass)
                        using (FeatureClassDefinition fcDef = FC.GetDefinition())
                        {
                            PU_SR = fcDef.GetSpatialReference();
                        }
                    });
                }
                else if (pu_result.puLayerType == PlanningUnitLayerType.RASTER)
                {
                    // Ensure data present
                    if (!(await PRZH.RasterExists_Project(PRZC.c_RAS_PLANNING_UNITS)).exists)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Planning Unit raster dataset not found.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Planning Unit raster dataset not found.  Have you built it yet?");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit raster dataset found."), true, ++val);
                    }

                    // Get path
                    pu_path = PRZH.GetPath_Project(PRZC.c_RAS_PLANNING_UNITS).path;

                    // Get Spatial Reference and cell size
                    await QueuedTask.Run(() =>
                    {
                        var tryget = PRZH.GetRaster_Project(PRZC.c_RAS_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            throw new Exception("Unable to retrieve planning unit raster dataset.");
                        }

                        using (RasterDataset RD = tryget.rasterDataset)
                        using (Raster raster = RD.CreateFullRaster())
                        {
                            PU_SR = raster.GetSpatialReference();
                            var msc = raster.GetMeanCellSize();
                            cell_size = msc.Item1;
                        }
                    });
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("There was a resounding KABOOM.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("KABOOM?");
                    return;
                }

                // Prompt users for permission to proceed
                if (ProMsgBox.Show("If you proceed, all files in the following folder will be deleted and/or overwritten:" + Environment.NewLine +
                    export_folder_path + Environment.NewLine + Environment.NewLine +
                   "Do you wish to proceed?" +
                   Environment.NewLine + Environment.NewLine +
                   "Choose wisely...",
                   "FILE OVERWRITE WARNING",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out."), true, ++val);
                    return;
                }

                #endregion

                PRZH.CheckForCancellation(token);

                #region DELETE OBJECTS

                // Delete all existing files within export dir
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting existing files..."), true, ++val);
                DirectoryInfo di = new DirectoryInfo(export_folder_path);

                try
                {
                    foreach (FileInfo fi in di.GetFiles())
                    {
                        fi.Delete();
                    }

                    foreach (DirectoryInfo sdi in di.GetDirectories())
                    {
                        sdi.Delete(true);
                    }
                }
                catch (Exception ex)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting files and folder from {export_folder_path}.\n{ex.Message}", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Unable to delete files & subfolders in the {export_folder_path} folder.\n{ex.Message}");
                    return;
                }
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Existing files deleted."), true, ++val);

                #endregion

                PRZH.CheckForCancellation(token);

                #region EXPORT SPATIAL DATA

                if (Settings_Rad_SpatialFormat_Vector_IsChecked)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Export Format: Feature"), true, ++val);

                    if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Existing Format: Feature"), true, ++val);
                        var result = await ExportFeaturesToShapefile(token);

                        if (!result.success)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error writing {PRZC.c_FC_PLANNING_UNITS} feature class to shapefile.\n{result.message}", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error writing {PRZC.c_FC_PLANNING_UNITS} feature class to shapefile.\n{result.message}");
                            return;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Shapefile created."), true, ++val);
                        }
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Existing Format: Raster"), true, ++val);
                        var result = await ExportRasterToShapefile(token);

                        if (!result.success)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error converting {PRZC.c_RAS_PLANNING_UNITS} raster dataset to shapefile.\n{result.message}", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error converting {PRZC.c_RAS_PLANNING_UNITS} raster dataset to shapefile.\n{result.message}");
                            return;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Shapefile created."), true, ++val);
                        }
                    }
                }
                else   // Raster Output is specified
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Raster output is not supported at this time.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Raster output is not supported at this time.");
                    return;

                }

                #endregion

                PRZH.CheckForCancellation(token);

                #region GET PUID AND NATGRID CELLNUMBER LISTS AND DICTIONARIES

                // Get the Planning Unit IDs
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Getting Planning Unit IDs..."), true, ++val);
                var puid_outcome = await PRZH.GetPUIDList();
                if (!puid_outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving Planning Unit IDs.\n{puid_outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving Planning Unit IDs\n{puid_outcome.message}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{puid_outcome.puids.Count} Planning Unit IDs retrieved."), true, ++val);
                }
                List<int> PUIDs = puid_outcome.puids;

                // Get the National Grid Cell Numbers
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Getting Cell Numbers..."), true, ++val);
                var cellnum_outcome = await PRZH.GetCellNumberList();
                if (!cellnum_outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving Cell Numbers.\n{cellnum_outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving Cell Numbers.\n{cellnum_outcome.message}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{cellnum_outcome.cell_numbers.Count} Cell Numbers retrieved."), true, ++val);
                }
                List<long> CellNumbers = cellnum_outcome.cell_numbers;

                // Get the (PUID, Cell Number) Dictionary
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Getting the (PUID, Cell Number) dictionary."), true, ++val);
                var puidcell = await PRZH.GetPUIDsAndCellNumbers();
                if (!puidcell.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving the (PUID, Cell Number) dictionary.\n{puidcell.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving the (PUID, Cell Number) dictionary.\n{puidcell.message}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved the dictionary ({puidcell.dict.Count} entries)."), true, ++val);
                }

                // store the dictionary
                var DICT_PUID_and_CN = puidcell.dict;

                // Get the (Cell Number, PUID) Dictionary
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Getting the (Cell Number, PUID) dictionary."), true, ++val);
                var cellpuid = await PRZH.GetCellNumbersAndPUIDs();
                if (!cellpuid.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving the (Cell Number, PUID) dictionary.\n{cellpuid.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving the (Cell Number, PUID) dictionary.\n{cellpuid.message}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved the dictionary ({cellpuid.dict.Count} entries)."), true, ++val);
                }

                // store the dictionary
                var DICT_CN_and_puids = cellpuid.dict;

                #endregion

                PRZH.CheckForCancellation(token);

                #region GET NATIONAL TABLE CONTENTS

                // Get the National Themes
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving national themes..."), true, ++val);
                var theme_outcome = await PRZH.GetNationalThemes(NationalThemePresence.Present);
                if (!theme_outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving national themes.\n{theme_outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving national themes.\n{theme_outcome.message}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved {theme_outcome.themes.Count} national themes."), true, ++val);
                }
                List<NatTheme> themes = theme_outcome.themes;

                // Get the goals
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving national {NationalElementType.Goal} elements..."), true, ++val);
                var goal_outcome = await PRZH.GetNationalElements(NationalElementType.Goal, NationalElementStatus.Active, NationalElementPresence.Present);
                if (!goal_outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving national {NationalElementType.Goal} elements.\n{goal_outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving national {NationalElementType.Goal} elements.\n{goal_outcome.message}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved {goal_outcome.elements.Count} national {NationalElementType.Goal} elements."), true, ++val);
                }
                List<NatElement> goals = goal_outcome.elements;

                // Get the weights
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving national {NationalElementType.Weight} elements..."), true, ++val);
                var weight_outcome = await PRZH.GetNationalElements(NationalElementType.Weight, NationalElementStatus.Active, NationalElementPresence.Present);
                if (!weight_outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving national {NationalElementType.Weight} elements.\n{weight_outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving national {NationalElementType.Weight} elements.\n{weight_outcome.message}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved {weight_outcome.elements.Count} national {NationalElementType.Weight} elements."), true, ++val);
                }
                List<NatElement> weights = weight_outcome.elements;

                // Get the includes
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving national {NationalElementType.Include} elements..."), true, ++val);
                var include_outcome = await PRZH.GetNationalElements(NationalElementType.Include, NationalElementStatus.Active, NationalElementPresence.Present);
                if (!include_outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving national {NationalElementType.Include} elements.\n{include_outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving national {NationalElementType.Include} elements.\n{include_outcome.message}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved {include_outcome.elements.Count} national {NationalElementType.Include} elements."), true, ++val);
                }
                List<NatElement> includes = include_outcome.elements;

                // Get the excludes
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving national {NationalElementType.Exclude} elements..."), true, ++val);
                var exclude_outcome = await PRZH.GetNationalElements(NationalElementType.Exclude, NationalElementStatus.Active, NationalElementPresence.Present);
                if (!exclude_outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving national {NationalElementType.Exclude} elements.\n{exclude_outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving national {NationalElementType.Exclude} elements.\n{exclude_outcome.message}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved {exclude_outcome.elements.Count} national {NationalElementType.Exclude} elements."), true, ++val);
                }
                List<NatElement> excludes = exclude_outcome.elements;

                #endregion

                PRZH.CheckForCancellation(token);

                #region ASSEMBLE ELEMENT VALUE DICTIONARIES

                // Populate a unique list of active themes

                // Get the Goal Value Dictionary of Dictionaries:  Key = element ID, Value = Dictionary of PUID + Values
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Populating the Goals dictionary ({goals.Count} goals)..."), true, ++val);
                Dictionary<int, Dictionary<int, double>> DICT_Goals = new Dictionary<int, Dictionary<int, double>>();
                for (int i = 0; i < goals.Count; i++)
                {
                    // Get the goal
                    NatElement goal = goals[i];

                    // Get the values dictionary for this goal
                    var getvals_outcome = await PRZH.GetValuesFromElementTable_PUID(goal.ElementID);
                    if (getvals_outcome.success)
                    {
                        // Store dictionary in Goals dictionary
                        DICT_Goals.Add(goal.ElementID, getvals_outcome.dict);
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Goal {i} (element ID {goal.ElementID}): {getvals_outcome.dict.Count} values"), true, ++val);
                    }
                    else
                    {
                        // Store null value in Goals dictionary
                        DICT_Goals.Add(goal.ElementID, null);
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Goal {i} (element ID {goal.ElementID}): Null dictionary (no values retrieved)"), true, ++val);
                    }
                }

                // Get the Weight Value Dictionary of Dictionaries:  Key = element ID, Value = Dictionary of PUID + Values
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Populating the Weights dictionary ({weights.Count} weights)..."), true, ++val);
                Dictionary<int, Dictionary<int, double>> DICT_Weights = new Dictionary<int, Dictionary<int, double>>();
                for (int i = 0; i < weights.Count; i++)
                {
                    // Get the weight
                    NatElement weight = weights[i];

                    // Get the values dictionary for this weight
                    var getvals_outcome = await PRZH.GetValuesFromElementTable_PUID(weight.ElementID);
                    if (getvals_outcome.success)
                    {
                        // Store dictionary in Weights dictionary
                        DICT_Weights.Add(weight.ElementID, getvals_outcome.dict);
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Weight {i} (element ID {weight.ElementID}): {getvals_outcome.dict.Count} values"), true, ++val);
                    }
                    else
                    {
                        // Store null value in Weights dictionary
                        DICT_Weights.Add(weight.ElementID, null);
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Weight {i} (element ID {weight.ElementID}): Null dictionary (no values retrieved)"), true, ++val);
                    }
                }

                // Get the Includes Value Dictionary of Dictionaries:  Key = element ID, Value = Dictionary of PUID + Values
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Populating the Includes dictionary ({includes.Count} includes)..."), true, ++val);
                Dictionary<int, Dictionary<int, double>> DICT_Includes = new Dictionary<int, Dictionary<int, double>>();
                for (int i = 0; i < includes.Count; i++)
                {
                    // Get the include
                    NatElement include = includes[i];

                    // Get the values dictionary for this include
                    var getvals_outcome = await PRZH.GetValuesFromElementTable_PUID(include.ElementID);
                    if (getvals_outcome.success)
                    {
                        // Store dictionary in Includes dictionary
                        DICT_Includes.Add(include.ElementID, getvals_outcome.dict);
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Includes {i} (element ID {include.ElementID}): {getvals_outcome.dict.Count} values"), true, ++val);
                    }
                    else
                    {
                        // Store null value in Includes dictionary
                        DICT_Includes.Add(include.ElementID, null);
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Includes {i} (element ID {include.ElementID}): Null dictionary (no values retrieved)"), true, ++val);
                    }
                }

                // Get the Excludes Value Dictionary of Dictionaries:  Key = element ID, Value = Dictionary of PUID + Values
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Populating the Excludes dictionary ({excludes.Count} excludes)..."), true, ++val);
                Dictionary<int, Dictionary<int, double>> DICT_Excludes = new Dictionary<int, Dictionary<int, double>>();
                for (int i = 0; i < excludes.Count; i++)
                {
                    // Get the exclude
                    NatElement exclude = excludes[i];

                    // Get the values dictionary for this exclude
                    var getvals_outcome = await PRZH.GetValuesFromElementTable_PUID(exclude.ElementID);
                    if (getvals_outcome.success)
                    {
                        // Store dictionary in Excludes dictionary
                        DICT_Excludes.Add(exclude.ElementID, getvals_outcome.dict);
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Excludes {i} (element ID {exclude.ElementID}): {getvals_outcome.dict.Count} values"), true, ++val);
                    }
                    else
                    {
                        // Store null value in Excludes dictionary
                        DICT_Excludes.Add(exclude.ElementID, null);
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Excludes {i} (element ID {exclude.ElementID}): Null dictionary (no values retrieved)"), true, ++val);
                    }
                }

                #endregion

                PRZH.CheckForCancellation(token);

                #region GENERATE THE ATTRIBUTE CSV

                string attributepath = Path.Combine(export_folder_path, PRZC.c_FILE_WTW_EXPORT_ATTR);

                var csvConfig_Attr = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = false, // this is default
                    NewLine = Environment.NewLine
                };

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating attribute CSV..."), true, ++val);
                using (var writer = new StreamWriter(attributepath))
                using (var csv = new CsvWriter(writer, csvConfig_Attr))
                {
                    #region ADD COLUMN HEADERS (ROW 1)

                    // First, the goals
                    for (int i = 0; i < goals.Count; i++)
                    {
                        csv.WriteField(goals[i].ElementTable);
                    }

                    // Next, the weights
                    for (int i = 0; i < weights.Count; i++)
                    {
                        csv.WriteField(weights[i].ElementTable);
                    }

                    // Next, the includes
                    for (int i = 0; i < includes.Count; i++)
                    {
                        csv.WriteField(includes[i].ElementTable);
                    }

                    // Next, the excludes
                    for (int i = 0; i < excludes.Count; i++)
                    {
                        csv.WriteField(excludes[i].ElementTable);
                    }

                    // Finally include the Planning Unit ID column
                    csv.WriteField("_index");
                    csv.NextRecord();   // First line is done!

                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Headers added."), true, ++val);

                    #endregion

                    #region ADD DATA ROWS (ROWS 2 -> N)

                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Writing values..."), true, ++val);
                    for (int i = 0; i < PUIDs.Count; i++)
                    {
                        int puid = PUIDs[i];

                        // Goals
                        for (int j = 0; j < goals.Count; j++)
                        {
                            // Get the goal
                            NatElement goal = goals[j];

                            if (DICT_Goals.ContainsKey(goal.ElementID))
                            {
                                var d = DICT_Goals[goal.ElementID]; // this is the dictionary of puid > value for this element id

                                if (d.ContainsKey(puid))
                                {
                                    csv.WriteField(d[puid]);    // write the value
                                }
                                else
                                {
                                    // no puid in dictionary, just write a zero for this PUI + goal
                                    csv.WriteField(0);
                                }
                            }
                            else
                            {
                                // No dictionary, just write a zero for this PUID + goal
                                csv.WriteField(0);
                            }
                        }


                        // Weights
                        for (int j = 0; j < weights.Count; j++)
                        {
                            // Get the weight
                            NatElement weight = weights[j];

                            if (DICT_Weights.ContainsKey(weight.ElementID))
                            {
                                var d = DICT_Weights[weight.ElementID]; // this is the dictionary of puid > value for this element id

                                if (d.ContainsKey(puid))
                                {
                                    csv.WriteField(d[puid]);    // write the value
                                }
                                else
                                {
                                    // no puid in dictionary, just write a zero for this PUI + weight
                                    csv.WriteField(0);
                                }
                            }
                            else
                            {
                                // No dictionary, just write a zero for this PUID + weight
                                csv.WriteField(0);
                            }
                        }

                        // Includes
                        for (int j = 0; j < includes.Count; j++)
                        {
                            // Get the include
                            NatElement include = includes[j];

                            if (DICT_Includes.ContainsKey(include.ElementID))
                            {
                                var d = DICT_Includes[include.ElementID]; // this is the dictionary of puid > value for this element id

                                if (d.ContainsKey(puid))
                                {
                                    csv.WriteField(d[puid]);    // write the value
                                }
                                else
                                {
                                    // no puid in dictionary, just write a zero for this PUI + include
                                    csv.WriteField(0);
                                }
                            }
                            else
                            {
                                // No dictionary, just write a zero for this PUID + include
                                csv.WriteField(0);
                            }
                        }

                        // Excludes
                        for (int j = 0; j < excludes.Count; j++)
                        {
                            // Get the exclude
                            NatElement exclude = excludes[j];

                            if (DICT_Excludes.ContainsKey(exclude.ElementID))
                            {
                                var d = DICT_Excludes[exclude.ElementID]; // this is the dictionary of puid > value for this element id

                                if (d.ContainsKey(puid))
                                {
                                    csv.WriteField(d[puid]);    // write the value
                                }
                                else
                                {
                                    // no puid in dictionary, just write a zero for this PUI + exclude
                                    csv.WriteField(0);
                                }
                            }
                            else
                            {
                                // No dictionary, just write a zero for this PUID + exclude
                                csv.WriteField(0);
                            }
                        }

                        // Finally, write the Planning Unit ID and end the row
                        csv.WriteField(puid);
                        csv.NextRecord();
                    }
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PUIDs.Count} data rows written to CSV."), true, ++val);

                    #endregion
                }

                // Compress Attribute CSV to gzip format
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Zipping attribute CSV."), true, ++val);
                FileInfo attribfi = new FileInfo(attributepath);
                FileInfo attribzgipfi = new FileInfo(string.Concat(attribfi.FullName, ".gz"));

                using (FileStream fileToBeZippedAsStream = attribfi.OpenRead())
                using (FileStream gzipTargetAsStream = attribzgipfi.Create())
                using (GZipStream gzipStream = new GZipStream(gzipTargetAsStream, CompressionMode.Compress))
                {
                    try
                    {
                        fileToBeZippedAsStream.CopyTo(gzipStream);
                    }
                    catch (Exception ex)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error zipping attribute CSV.\n{ex.Message}", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error zipping attribute CSV.\n{ex.Message}");
                        return;
                    }
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Attribute CSV zipped."), true, ++val);

                #endregion

                PRZH.CheckForCancellation(token);

                #region GENERATE AND ZIP THE BOUNDARY CSV

                string bndpath = Path.Combine(export_folder_path, PRZC.c_FILE_WTW_EXPORT_BND);

                var csvConfig_Bnd = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = false, // this is default
                    NewLine = Environment.NewLine
                };

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating boundary CSV..."), true, ++val);
                using (var writer = new StreamWriter(bndpath))
                using (var csv = new CsvWriter(writer, csvConfig_Bnd))
                {
                    // *** ROW 1 => COLUMN NAMES

                    // PU ID Columns
                    csv.WriteField(PRZC.c_FLD_TAB_BOUND_ID1);
                    csv.WriteField(PRZC.c_FLD_TAB_BOUND_ID2);
                    csv.WriteField(PRZC.c_FLD_TAB_BOUND_BOUNDARY);

                    csv.NextRecord();

                    // *** ROWS 2 TO N => Boundary Records
                    if (!await QueuedTask.Run(() =>
                    {
                        try
                        {
                            var tryget = PRZH.GetTable_Project(PRZC.c_TABLE_PUBOUNDARY);
                            if (!tryget.success)
                            {
                                throw new Exception("Unable to retrieve table.");
                            }

                            using (Table table = tryget.table)
                            using (RowCursor rowCursor = table.Search())
                            {
                                while (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        int id1 = Convert.ToInt32(row[PRZC.c_FLD_TAB_BOUND_ID1]);
                                        int id2 = Convert.ToInt32(row[PRZC.c_FLD_TAB_BOUND_ID2]);
                                        double bnd = Convert.ToDouble(row[PRZC.c_FLD_TAB_BOUND_BOUNDARY]);

                                        csv.WriteField(id1);
                                        csv.WriteField(id2);
                                        csv.WriteField(bnd);

                                        csv.NextRecord();
                                    }
                                }
                            }

                            return true;
                        }
                        catch (Exception ex)
                        {
                            ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                            return false;
                        }
                    }))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating boundary CSV.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error creating boundary CSV.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Boundary CSV created."), true, ++val);
                    }
                }

                // Compress Boundary CSV to gzip format
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Zipping boundary CSV."), true, ++val);
                FileInfo bndfi = new FileInfo(bndpath);
                FileInfo bndzgipfi = new FileInfo(string.Concat(bndfi.FullName, ".gz"));

                using (FileStream fileToBeZippedAsStream = bndfi.OpenRead())
                using (FileStream gzipTargetAsStream = bndzgipfi.Create())
                using (GZipStream gzipStream = new GZipStream(gzipTargetAsStream, CompressionMode.Compress))
                {
                    try
                    {
                        fileToBeZippedAsStream.CopyTo(gzipStream);
                    }
                    catch (Exception ex)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error zipping boundary CSV.\n{ex.Message}", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error zipping boundary CSV.\n{ex.Message}");
                        return;
                    }
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Boundary CSV zipped."), true, ++val);

                #endregion

                PRZH.CheckForCancellation(token);

                #region GENERATE THE YAML FILE

                #region THEMES & GOALS

                // Create the yamlTheme list
                List<YamlTheme> yamlThemes = new List<YamlTheme>();

                for (int i = 0; i < themes.Count; i++)
                {
                    NatTheme theme = themes[i];

                    // Get all goals belonging to this theme
                    List<NatElement> theme_goals = goals.Where(g => g.ThemeID == theme.ThemeID).OrderBy(g => g.ElementID).ToList();

                    // Skip this theme if (through some weird alternate universe) it has no associated goals
                    if (theme_goals.Count == 0)
                    {
                        continue;
                    }

                    // Assemble the yaml Goal list
                    List<YamlFeature> yamlGoals = new List<YamlFeature>();

                    // Populate the yaml Goal list
                    for (int j = 0; j < theme_goals.Count; j++)
                    {
                        // Get the goal element
                        NatElement goal = theme_goals[j];

                        // Build the Yaml Legend
                        YamlLegend yamlLegend = new YamlLegend();

                        List<Color> colors = new List<Color>()
                        {
                            Color.Transparent,
                            Color.DarkSeaGreen
                        };

                        yamlLegend.SetCategoricalColors(colors);

                        // Build the Yaml Variable
                        YamlVariable yamlVariable = new YamlVariable();
                        yamlVariable.index = goal.ElementTable;
                        yamlVariable.units = goal.ElementUnit;
                        yamlVariable.provenance = WTWProvenanceType.national.ToString();
                        yamlVariable.legend = yamlLegend;

                        // Build the Yaml Goal
                        YamlFeature yamlGoal = new YamlFeature();
                        yamlGoal.name = goal.ElementName;
                        yamlGoal.status = true; // enabled or disabled
                        yamlGoal.visible = true;
                        yamlGoal.hidden = false;
                        yamlGoal.goal = 0.5;        // needs to be retrieved from somewhere, or just left to 0.5
                        yamlGoal.variable = yamlVariable;

                        // Add to list
                        yamlGoals.Add(yamlGoal);
                    }

                    // Create the Yaml Theme
                    YamlTheme yamlTheme = new YamlTheme();

                    yamlTheme.name = theme.ThemeName;
                    yamlTheme.feature = yamlGoals.ToArray();

                    // Add to list
                    yamlThemes.Add(yamlTheme);
                }

                #endregion

                #region WEIGHTS

                // Create the yaml Weights list
                List<YamlWeight> yamlWeights = new List<YamlWeight>();

                for (int i = 0; i < weights.Count; i++)
                {
                    // Get the weight
                    NatElement weight = weights[i];

                    // Build the Yaml Legend
                    YamlLegend yamlLegend = new YamlLegend();
                    List<Color> colors = new List<Color>()
                    {
                        Color.White,
                        Color.DarkOrchid
                    };

                    yamlLegend.SetContinuousColors(colors);

                    // Build the Yaml Variable
                    YamlVariable yamlVariable = new YamlVariable();
                    yamlVariable.index = weight.ElementTable;
                    yamlVariable.units = weight.ElementUnit;
                    yamlVariable.provenance = WTWProvenanceType.national.ToString();
                    yamlVariable.legend = yamlLegend;

                    // Build the Yaml Weight
                    YamlWeight yamlWeight = new YamlWeight();
                    yamlWeight.name = weight.ElementName;
                    yamlWeight.status = true; // enabled or disabled
                    yamlWeight.visible = true;
                    yamlWeight.hidden = false;
                    yamlWeight.factor = 0;                  // what's this?
                    yamlWeight.variable = yamlVariable;

                    // Add to list
                    yamlWeights.Add(yamlWeight);
                }

                #endregion

                #region INCLUDES

                // Create the yaml Includes list
                List<YamlInclude> yamlIncludes = new List<YamlInclude>();

                for (int i = 0; i < includes.Count; i++)
                {
                    // Get the include
                    NatElement include = includes[i];

                    // Build the Yaml Legend
                    YamlLegend yamlLegend = new YamlLegend();
                    List<(Color color, string label)> values = new List<(Color color, string label)>()
                        {
                            (Color.Transparent, "Do not include"),
                            (Color.Green, "Include")
                        };

                    yamlLegend.SetManualColors(values);

                    // Build the Yaml Variable
                    YamlVariable yamlVariable = new YamlVariable();
                    yamlVariable.index = include.ElementTable;
                    yamlVariable.units = "";//include.ElementUnit;
                    yamlVariable.provenance = WTWProvenanceType.national.ToString();
                    yamlVariable.legend = yamlLegend;

                    // Build the Yaml Include
                    YamlInclude yamlInclude = new YamlInclude();
                    yamlInclude.name = include.ElementName;
                    yamlInclude.mandatory = false;      // what's this
                    yamlInclude.status = true; // enabled or disabled
                    yamlInclude.visible = true;
                    yamlInclude.hidden = false;
                    yamlInclude.variable = yamlVariable;

                    // Add to list
                    yamlIncludes.Add(yamlInclude);
                }

                #endregion

                #region EXCLUDES

                // Create the yaml Excludes list
                List<YamlExclude> yamlExcludes = new List<YamlExclude>();

                for (int i = 0; i < excludes.Count; i++)
                {
                    // Get the exclude
                    NatElement exclude = excludes[i];

                    // Build the Yaml Legend
                    YamlLegend yamlLegend = new YamlLegend();       // default legend

                    // Build the Yaml Variable
                    YamlVariable yamlVariable = new YamlVariable();
                    yamlVariable.index = exclude.ElementTable;
                    yamlVariable.units = exclude.ElementUnit;
                    yamlVariable.provenance = WTWProvenanceType.national.ToString();
                    yamlVariable.legend = yamlLegend;

                    // Build the Yaml Exclude
                    YamlExclude yamlExclude = new YamlExclude();
                    yamlExclude.name = exclude.ElementName;
                    yamlExclude.mandatory = false;      // what's this
                    yamlExclude.status = true; // enabled or disabled
                    yamlExclude.visible = true;
                    yamlExclude.hidden = false;
                    yamlExclude.variable = yamlVariable;

                    // Add to list
                    yamlExcludes.Add(yamlExclude);
                }

                #endregion

                #region YAML PACKAGE

                YamlPackage yamlPackage = new YamlPackage();
                yamlPackage.name = "TEMP PROJECT NAME";                 // Could be customized later, or derived from the ArcGIS Pro project name
                yamlPackage.mode = WTWModeType.advanced.ToString();     // Which of these should I use?
                yamlPackage.themes = yamlThemes.ToArray();
                yamlPackage.weights = yamlWeights.ToArray();
                yamlPackage.includes = yamlIncludes.ToArray();
                // yamlPackage.excludes = yamlExcludes.ToArray();       // Excludes are not part of the yaml schema (yet)

                ISerializer builder = new SerializerBuilder().DisableAliases().Build();
                string the_yaml = builder.Serialize(yamlPackage);

                string yamlpath = Path.Combine(export_folder_path, PRZC.c_FILE_WTW_EXPORT_YAML);
                try
                {
                    File.WriteAllText(yamlpath, the_yaml);
                }
                catch (Exception ex)
                {
                    ProMsgBox.Show("Unable to write the Yaml Config File..." + Environment.NewLine + Environment.NewLine + ex.Message);
                    return;
                }

                #endregion

                #endregion

                ProMsgBox.Show("Export Complete.");

                return;
            }
            catch (OperationCanceledException)
            {
                // Cancelled by user
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"ExportWTWPackage: cancelled by user.", LogMessageType.CANCELLATION), true, ++val);
                ProMsgBox.Show($"ExportWTWPackage: Cancelled by user.");
            }
            catch (Exception ex)
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return;
            }
            finally
            {
                // reset disabled editing status
                if (edits_are_disabled)
                {
                    await Project.Current.SetIsEditingEnabledAsync(false);
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing disabled."), true, max, ++val);
                }

                // redraw controls
                await ValidateControls();
            }
        }

        private async Task<(bool success, string message)> ExportRasterToShapefile(CancellationToken token)
        {
            int val = 0;

            try
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating shapefile..."), true, 30, ++val);

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                // Filenames and Paths
                string gdbpath = PRZH.GetPath_ProjectGDB();

                string export_folder_path = PRZH.GetPath_ExportWTWFolder();
                string export_shp_name = PRZC.c_FILE_WTW_EXPORT_SPATIAL + ".shp";
                string export_shp_path = Path.Combine(export_folder_path, export_shp_name);

                // Confirm that source raster is present
                if (!(await PRZH.RasterExists_Project(PRZC.c_RAS_PLANNING_UNITS)).exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_PLANNING_UNITS} raster dataset not found.", LogMessageType.ERROR), true, ++val);
                    return (false, $"{PRZC.c_RAS_PLANNING_UNITS} raster dataset not found");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_PLANNING_UNITS} raster found."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Convert source raster to temp polygon feature class
                string fldPUID_Temp = "gridcode";

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Converting {PRZC.c_RAS_PLANNING_UNITS} raster dataset to polygon feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS, PRZC.c_FC_TEMP_WTW_FC1, "NO_SIMPLIFY", "VALUE", "SINGLE_OUTER_PART", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("RasterToPolygon_conversion", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error executing Raster To Polygon tool.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return (false, "Error executing Raster to Polygon tool.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Conversion successful."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Project temp polygon feature class
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Projecting {PRZC.c_FC_TEMP_WTW_FC1} feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_TEMP_WTW_FC1, PRZC.c_FC_TEMP_WTW_FC2, Export_SR, "", "", "NO_PRESERVE_SHAPE", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("Project_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error projecting feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return (false, "Error projecting feature class.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Projection successful."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Repair Geometry
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Repairing geometry..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_TEMP_WTW_FC2);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("RepairGeometry_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error repairing geometry.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return (false, "Error repairing geometry.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Geometry repaired."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Delete the unnecessary fields
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting extra fields..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_TEMP_WTW_FC2, fldPUID_Temp, "KEEP_FIELDS");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return (false, "Error deleting fields.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields deleted."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Calculate field
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Calculating {PRZC.c_FLD_FC_PU_ID} field..."), true, ++val);
                string expression = "!" + fldPUID_Temp + "!";
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_TEMP_WTW_FC2, PRZC.c_FLD_FC_PU_ID, expression, "PYTHON3", "", "LONG", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CalculateField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error Calculating {PRZC.c_FLD_FC_PU_ID} field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return (false, $"Error calculating the new {PRZC.c_FLD_FC_PU_ID} field.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field calculated successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Export to Shapefile
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Export the {PRZC.c_FILE_WTW_EXPORT_SPATIAL} shapefile..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_TEMP_WTW_FC2, export_shp_path);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CopyFeatures_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error exporting the {PRZC.c_FILE_WTW_EXPORT_SPATIAL} shapefile.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return (false, $"Error exporting the {PRZC.c_FILE_WTW_EXPORT_SPATIAL} shapefile.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Shapefile exported."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Index the new id field
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing {PRZC.c_FLD_FC_PU_ID} field..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FILE_WTW_EXPORT_SPATIAL, new List<string>() { PRZC.c_FLD_FC_PU_ID }, "ix" + PRZC.c_FLD_FC_PU_ID, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: export_folder_path);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return (false, "Error indexing field.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field indexed."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Delete the unnecessary fields
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting extra fields (again)..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FILE_WTW_EXPORT_SPATIAL, PRZC.c_FLD_FC_PU_ID, "KEEP_FIELDS");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: export_folder_path);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return (false, "Error deleting fields.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields deleted."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Delete temp feature classes
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {PRZC.c_FC_TEMP_WTW_FC1} and {PRZC.c_FC_TEMP_WTW_FC2} feature classes..."), true, ++val);

                if ((await PRZH.FCExists_Project(PRZC.c_FC_TEMP_WTW_FC1)).exists)
                {
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_TEMP_WTW_FC1);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting {PRZC.c_FC_TEMP_WTW_FC1} feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        return (false, $"Error deleting {PRZC.c_FC_TEMP_WTW_FC1} feature class.");
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Feature class deleted."), true, ++val);
                    }
                }

                PRZH.CheckForCancellation(token);

                if ((await PRZH.FCExists_Project(PRZC.c_FC_TEMP_WTW_FC2)).exists)
                {
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_TEMP_WTW_FC2);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting {PRZC.c_FC_TEMP_WTW_FC2} feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        return (false, $"Error deleting {PRZC.c_FC_TEMP_WTW_FC2} feature class.");
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Feature class deleted."), true, ++val);
                    }
                }

                return (true, "success");
            }
            catch (OperationCanceledException cancelex)
            {
                // Cancelled by user
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"ExportRasterToShapefile: cancelled by user.", LogMessageType.CANCELLATION), true, ++val);

                // Throw the cancellation error to the parent
                throw cancelex;
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<(bool success, string message)> ExportFeaturesToShapefile(CancellationToken token)
        {
            int val = 0;

            try
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating shapefile..."), true, ++val);

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                // Filenames and Paths
                string gdbpath = PRZH.GetPath_ProjectGDB();

                string export_folder_path = PRZH.GetPath_ExportWTWFolder();
                string export_shp_name = PRZC.c_FILE_WTW_EXPORT_SPATIAL + ".shp";
                string export_shp_path = Path.Combine(export_folder_path, export_shp_name);

                // Confirm that source feature class is present
                if (!(await PRZH.FCExists_Project(PRZC.c_FC_PLANNING_UNITS)).exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_FC_PLANNING_UNITS} feature class not found.", LogMessageType.ERROR), true, ++val);
                    return (false, $"{PRZC.c_FC_PLANNING_UNITS} feature class not found");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_FC_PLANNING_UNITS} feature class found."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Project feature class
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Projecting {PRZC.c_FC_PLANNING_UNITS} feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_PLANNING_UNITS, PRZC.c_FC_TEMP_WTW_FC2, Export_SR, "", "", "NO_PRESERVE_SHAPE", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("Project_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error projecting feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return (false, "Error projecting feature class.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Projection successful."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Repair Geometry
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Repairing geometry..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_TEMP_WTW_FC2);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("RepairGeometry_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error repairing geometry.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return (false, "Error repairing geometry.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Geometry repaired."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Delete the unnecessary fields
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting extra fields..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_TEMP_WTW_FC2, PRZC.c_FLD_FC_PU_ID, "KEEP_FIELDS");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return (false, "Error deleting fields.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields deleted."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Export to Shapefile
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Export the {PRZC.c_FILE_WTW_EXPORT_SPATIAL} shapefile..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_TEMP_WTW_FC2, export_shp_path);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CopyFeatures_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error exporting the {PRZC.c_FILE_WTW_EXPORT_SPATIAL} shapefile.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return (false, $"Error exporting the {PRZC.c_FILE_WTW_EXPORT_SPATIAL} shapefile.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Shapefile exported."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Index the new id field
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing {PRZC.c_FLD_FC_PU_ID} field..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FILE_WTW_EXPORT_SPATIAL, new List<string>() { PRZC.c_FLD_FC_PU_ID }, "ix" + PRZC.c_FLD_FC_PU_ID, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: export_folder_path);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return (false, "Error indexing field.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field indexed."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Delete the unnecessary fields
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting extra fields (again)..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FILE_WTW_EXPORT_SPATIAL, PRZC.c_FLD_FC_PU_ID, "KEEP_FIELDS");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: export_folder_path);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return (false, "Error deleting fields.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields deleted."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Delete temp feature class
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {PRZC.c_FC_TEMP_WTW_FC2} feature class..."), true, ++val);
                if ((await PRZH.FCExists_Project(PRZC.c_FC_TEMP_WTW_FC2)).exists)
                {
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_TEMP_WTW_FC2);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting {PRZC.c_FC_TEMP_WTW_FC2} feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        return (false, $"Error deleting {PRZC.c_FC_TEMP_WTW_FC2} feature class.");
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Feature class deleted."), true, ++val);
                    }
                }

                return (true, "success");
            }
            catch (OperationCanceledException cancelex)
            {
                // Cancelled by user
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"ExportFeaturesToShapefile: cancelled by user.", LogMessageType.CANCELLATION), true, ++val);

                // Throw the cancellation error to the parent
                throw cancelex;
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task ValidateControls()
        {
            try
            {
                // Establish Geodatabase Object Existence:
                // 1. Planning Unit Dataset
                var try_exists = await PRZH.PUExists();
                _puDataExists = try_exists.exists;

                // 2. Boundary Lengths Table
                _boundaryTableExists = (await PRZH.TableExists_Project(PRZC.c_TABLE_PUBOUNDARY)).exists;

                // Configure Labels:
                // 1. Planning Unit Dataset Label
                if (!_puDataExists || try_exists.puLayerType == PlanningUnitLayerType.UNKNOWN)
                {
                    Txt_PlanningUnitLabel = "Planning Units do not exist.  Build them.";
                }
                else if (try_exists.puLayerType == PlanningUnitLayerType.FEATURE)
                {
                    Txt_PlanningUnitLabel = "Planning Units exist (Feature Class).";
                }
                else if (try_exists.puLayerType == PlanningUnitLayerType.RASTER)
                {
                    Txt_PlanningUnitLabel = "Planning Units exist (Raster Dataset).";
                }
                else
                {
                    Txt_PlanningUnitLabel = "Planning Units do not exist.  Build them.";
                }

                // 2. Boundary Lengths Table Label
                if (_boundaryTableExists)
                {
                    Txt_BoundaryLengthsLabel = "Boundary Lengths Table exists.";
                }
                else
                {
                    Txt_BoundaryLengthsLabel = "Boundary Lengths Table does not exist.  Build it.";
                }

                // Configure Images:
                // 1. Planning Units
                if (_puDataExists)
                {
                    CompStat_Img_PU = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_Img_PU = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
                }

                // 2. Boundary Lengths Table
                if (_boundaryTableExists)
                {
                    CompStat_Img_BoundaryLengths = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_Img_BoundaryLengths = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
                }

                // Enable/Disable Export Button as required
                ExportIsEnabled = _puDataExists && _boundaryTableExists;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        #endregion
    }
}


//PRZH.UpdateProgress(PM, PRZH.WriteLog($"Export Format: Raster"), true, ++val);

//if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
//{
//    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Existing Format: Feature (conversion required)"), true, ++val);
//    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Feature to Raster conversion is not allowed yet.", LogMessageType.VALIDATION_ERROR), true, ++val);
//    ProMsgBox.Show("Feature to Raster conversion is not allowed yet.");
//    return false;
//}
//else
//{
//    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Existing Format: Raster"), true, ++val);

//    // Filenames and Paths
//    string source_raster_path = PRZH.GetPath_Raster_PU();
//    string temp_raster_file = "export_temp.tif";
//    string output_raster_file = PRZC.c_FILE_WTW_EXPORT_SPATIAL + ".tif";
//    string temp_raster_path = Path.Combine(export_folder_path, temp_raster_file);
//    string output_raster_path = Path.Combine(export_folder_path, output_raster_file);

//    // Confirm that raster is present
//    if (!await PRZH.RasterExists_PU())
//    {
//        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_PLANNING_UNITS} raster not found.", LogMessageType.ERROR), true, ++val);
//        ProMsgBox.Show($"{PRZC.c_RAS_PLANNING_UNITS} raster not found.");
//        return false;
//    }
//    else
//    {
//        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_PLANNING_UNITS} raster found."), true, ++val);
//    }

//    // Copy raster to temp raster
//    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Copying {PRZC.c_RAS_PLANNING_UNITS} raster dataset..."), true, ++val);
//    toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS, temp_raster_path, "", "", "", "", "", "32_BIT_UNSIGNED", "", "", "", "", "", "");
//    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
//    toolOutput = await PRZH.RunGPTool("CopyRaster_management", toolParams, toolEnvs, toolFlags);
//    if (toolOutput == null)
//    {
//        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying the {PRZC.c_RAS_PLANNING_UNITS} raster dataset.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
//        ProMsgBox.Show($"Error copying the {PRZC.c_RAS_PLANNING_UNITS} raster dataset.");
//        return false;
//    }
//    else
//    {
//        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_PLANNING_UNITS} raster dataset copied successfully..."), true, ++val);
//    }

//    // Project temp raster to final raster
//    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Projecting raster dataset to Web Mercator..."), true, ++val);
//    toolParams = Geoprocessing.MakeValueArray(temp_raster_path, output_raster_path, Export_SR, "NEAREST", cell_size, "", "", "", "");
//    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: export_folder_path, overwriteoutput: true);
//    toolOutput = await PRZH.RunGPTool("ProjectRaster_management", toolParams, toolEnvs, toolFlags);
//    if (toolOutput == null)
//    {
//        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error projecting the raster dataset.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
//        ProMsgBox.Show($"Error projecting the raster dataset.");
//        return false;
//    }
//    else
//    {
//        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Raster dataset projected successfully..."), true, ++val);
//    }

//    // Delete the temp raster
//    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting temp raster dataset..."), true, ++val);
//    toolParams = Geoprocessing.MakeValueArray(temp_raster_path, "");
//    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: export_folder_path);
//    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
//    if (toolOutput == null)
//    {
//        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting temp raster.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
//        ProMsgBox.Show($"Error deleting temp raster.");
//        return false;
//    }
//    else
//    {
//        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Temp raster deleted."), true, ++val);
//    }

//    // Delete the final raster attribute table (if it exists)
//    if (await QueuedTask.Run(async () =>
//    {
//        try
//        {
//            using (RasterDataset rasterDataset = await PRZH.GetRaster_PU())
//            using (Raster raster = rasterDataset.CreateFullRaster())
//            {
//                var t = raster.GetAttributeTable();
//                return t != null;
//            }
//        }
//        catch (Exception ex)
//        {
//            ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
//            return false;
//        }
//    }))
//    {
//        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting raster attribute table..."), true, ++val);
//        toolParams = Geoprocessing.MakeValueArray(output_raster_path);
//        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: export_folder_path);
//        toolOutput = await PRZH.RunGPTool("DeleteRasterAttributeTable_management", toolParams, toolEnvs, toolFlags);
//        if (toolOutput == null)
//        {
//            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting raster attribute table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
//            ProMsgBox.Show($"Error deleting raster attribute table.");
//            return false;
//        }
//        else
//        {
//            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Raster attribute table deleted."), true, ++val);
//        }
//    }
//}
