//#define TEST

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
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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

        private bool _settings_Rad_SpatialFormat_Vector_IsChecked;
        private bool _settings_Rad_SpatialFormat_Raster_IsChecked;
        private string _txt_PlanningUnitLabel;

        private SpatialReference Export_SR = SpatialReferences.WebMercator;



        private bool _exportIsEnabled = false;
        private string _compStat_PUFC = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
        private string _compStat_SelRules = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Warn16.png";
        private string _compStat_Weights = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Warn16.png";
        private string _compStat_Features = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
        private string _compStat_Bounds = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""
        private ICommand _cmdExport;
        private ICommand _cmdClearLog;
        private ICommand _cmdTest;

        #endregion

        #region PROPERTIES

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





        public bool ExportIsEnabled
        {
            get => _exportIsEnabled;
            set => SetProperty(ref _exportIsEnabled, value, () => ExportIsEnabled);
        }

        public string CompStat_PUFC
        {
            get => _compStat_PUFC;
            set => SetProperty(ref _compStat_PUFC, value, () => CompStat_PUFC);
        }

        public string CompStat_SelRules
        {
            get => _compStat_SelRules;
            set => SetProperty(ref _compStat_SelRules, value, () => CompStat_SelRules);
        }

        public string CompStat_Weights
        {
            get => _compStat_Weights;
            set => SetProperty(ref _compStat_Weights, value, () => CompStat_Weights);
        }

        public string CompStat_Features
        {
            get => _compStat_Features;
            set => SetProperty(ref _compStat_Features, value, () => CompStat_Features);
        }

        public string CompStat_Bounds
        {
            get => _compStat_Bounds;
            set => SetProperty(ref _compStat_Bounds, value, () => CompStat_Bounds);
        }

        public ProgressManager PM
        {
            get => _pm;
            set => SetProperty(ref _pm, value, () => PM);
        }

        #endregion

        #region COMMANDS

        public ICommand CmdExport => _cmdExport ?? (_cmdExport = new RelayCommand(() => ExportWTWPackage(), () => true));

        public ICommand CmdTest => _cmdTest ?? (_cmdTest = new RelayCommand(() => Test(), () => true));

        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true));

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

                // Component Presence
                var pu_result = await PRZH.PUExists();
                if (!pu_result.exists)
                {
                    Txt_PlanningUnitLabel = "Planning Unit Dataset";
                }
                else if (pu_result.puLayerType == PlanningUnitLayerType.UNKNOWN)
                {
                    Txt_PlanningUnitLabel = "Planning Unit Dataset";
                }
                else if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                {
                    Txt_PlanningUnitLabel = "Planning Unit Feature Class";
                    // Ensure data present
                    if (!await PRZH.FCExists_PU())
                    {
                        // PU FC NOT FOUND
                    }
                    else
                    {
                        // PU FC FOUND
                    }
                }
                else if (pu_result.puLayerType == PlanningUnitLayerType.RASTER)
                {
                    Txt_PlanningUnitLabel = "Planning Unit Raster Dataset";
                    // Ensure data present
                    if (!await PRZH.RasterExists_PU())
                    {
                        // RASTER NOT FOUND
                    }
                    else
                    {
                        // RASTER FOUND
                    }
                }
                else
                {
                    Txt_PlanningUnitLabel = "Planning Unit Dataset";
                }

                // Initialize the indicator images
                bool PlanningUnits_OK = pu_result.exists;
                bool SelRules_OK = await PRZH.TableExists_SelRules();
                bool Weights_OK = false;    // TODO: ADD THIS LATER
                bool Features_OK = await PRZH.TableExists_Features();
                bool Bounds_OK = await PRZH.TableExists_Boundary();

                // Set the Component Status Images
                if (PlanningUnits_OK)
                {
                    CompStat_PUFC = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_PUFC = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
                }

                if (SelRules_OK)
                {
                    CompStat_SelRules = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_SelRules = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Warn16.png";
                }

                if (Weights_OK)
                {
                    CompStat_Weights = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_Weights = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Warn16.png";
                }

                if (Features_OK)
                {
                    CompStat_Features = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_Features = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
                }

                if (Bounds_OK)
                {
                    CompStat_Bounds = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_Bounds = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
                }

                // Set Enabled Status on export button
                //                ExportIsEnabled = PUFC_OK & Features_OK & Bounds_OK;
                ExportIsEnabled = true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> ExportWTWPackage()
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
                    return false;
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
                        return false;
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
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                // Initialize ProgressBar and Progress Log
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Where to Work Exporter..."), false, max, ++val);

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Ensure the Project Geodatabase Exists
                if (!await PRZH.GDBExists_Project())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Project Geodatabase not found: {gdbpath}", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Project Geodatabase not found at {gdbpath}.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Project Geodatabase found at {gdbpath}."), true, ++val);
                }

                // Ensure the ExportWTW folder exists
                if (!PRZH.FolderExists_ExportWTW())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_DIR_EXPORT_WTW} folder not found in project workspace.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"{PRZC.c_DIR_EXPORT_WTW} folder not found in project workspace.");
                    return false;
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
                    return false;
                }
                else if (pu_result.puLayerType == PlanningUnitLayerType.UNKNOWN)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit layer format unknown.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Planning Unit layer format unknown.");
                    return false;
                }
                else if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                {
                    // Ensure data present
                    if (!await PRZH.FCExists_PU())
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Planning Unit feature class not found.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Planning Unit feature class not found.  Have you built it yet?");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit feature class found."), true, ++val);
                    }

                    // Get path
                    pu_path = PRZH.GetPath_FC_PU();

                    // Get Spatial Reference
                    await QueuedTask.Run(async () =>
                    {
                        using (FeatureClass FC = await PRZH.GetFC_PU())
                        using (FeatureClassDefinition fcDef = FC.GetDefinition())
                        {
                            PU_SR = fcDef.GetSpatialReference();
                        }
                    });
                }
                else if (pu_result.puLayerType == PlanningUnitLayerType.RASTER)
                {
                    // Ensure data present
                    if (!await PRZH.RasterExists_PU())
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Planning Unit raster dataset not found.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Planning Unit raster dataset not found.  Have you built it yet?");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit raster dataset found."), true, ++val);
                    }

                    // Get path
                    pu_path = PRZH.GetPath_Raster_PU();

                    // Get Spatial Reference and cell size
                    await QueuedTask.Run(async () =>
                    {
                        using (RasterDataset RD = await PRZH.GetRaster_PU())
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
                    return false;
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
                    return false;
                }

                #endregion

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
                    return false;
                }
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Existing files deleted."), true, ++val);

                #endregion

#if TEST
                #region EXPORT SPATIAL DATA

                if (Settings_Rad_SpatialFormat_Vector_IsChecked)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Export Format: Feature"), true, ++val);

                    if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Existing Format: Feature"), true, ++val);
                        var result = await ExportFeaturesToShapefile();

                        if (!result.success)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error writing {PRZC.c_FC_PLANNING_UNITS} feature class to shapefile.\n{result.message}", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error writing {PRZC.c_FC_PLANNING_UNITS} feature class to shapefile.\n{result.message}");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Shapefile created."), true, ++val);
                        }
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Existing Format: Raster"), true, ++val);
                        var result = await ExportRasterToShapefile();

                        if (!result.success)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error converting {PRZC.c_RAS_PLANNING_UNITS} raster dataset to shapefile.\n{result.message}", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error converting {PRZC.c_RAS_PLANNING_UNITS} raster dataset to shapefile.\n{result.message}");
                            return false;
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
                    return false;

                }

                #endregion

#endif

                #region GET PUID AND NATGRID CELLNUMBER LISTS AND DICTIONARIES

                // Get the Planning Unit IDs
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Getting Planning Unit IDs..."), true, ++val);
                var outcome = await PRZH.GetPUIDs();
                if (!outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving Planning Unit IDs.\n{outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving Planning Unit IDs\n{outcome.message}");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{outcome.puids.Count} Planning Unit IDs retrieved."), true, ++val);
                }

                // Convert to list and sort
                List<int> PUIDs = outcome.puids.ToList();
                PUIDs.Sort();

                // Get the National Grid Cell Numbers
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Getting Cell Numbers..."), true, ++val);
                var outcome2 = await PRZH.GetCellNumbers();
                if (!outcome2.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving Cell Numbers.\n{outcome2.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving Cell Numbers.\n{outcome2.message}");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{outcome2.cell_numbers.Count} Cell Numbers retrieved."), true, ++val);
                }

                // Convert to list and sort
                List<long> CellNumbers = outcome2.cell_numbers.ToList();
                CellNumbers.Sort();

                // Get the (PUID, Cell Number) Dictionary
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Getting the (PUID, Cell Number) dictionary."), true, ++val);
                var puidcell = await PRZH.GetPUIDsAndCellNumbers();
                if (!puidcell.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving the (PUID, Cell Number) dictionary.\n{puidcell.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving the (PUID, Cell Number) dictionary.\n{puidcell.message}");
                    return false;
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
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved the dictionary ({cellpuid.dict.Count} entries)."), true, ++val);
                }

                // store the dictionary
                var DICT_CN_and_puids = cellpuid.dict;

                #endregion

                #region GET NATIONAL TABLE CONTENTS

                // Get the National Themes
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving national themes..."), true, ++val);
                var theme_outcome = await PRZH.GetNationalThemes();
                if (!theme_outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving national themes.\n{theme_outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving national themes.\n{theme_outcome.message}");
                    return false;
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
                    return false;
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
                    return false;
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
                    return false;
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
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved {exclude_outcome.elements.Count} national {NationalElementType.Exclude} elements."), true, ++val);
                }
                List<NatElement> excludes = exclude_outcome.elements;

                #endregion

                #region ASSEMBLE ELEMENT LISTS



                #endregion

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

                    #endregion

                    #region ADD DATA ROWS (ROWS 2 -> N)

                    for (int i = 0; i < PUIDs.Count; i++)
                    {
                        int puid = PUIDs[i];
                        long cellnum = DICT_PUID_and_CN[puid];

                        // Goal Values
                        for (int j = 0; j < goals.Count; j++)
                        {
                            NatElement goal = goals[j];
                            string tablename = goal.ElementTable;


                        }


                        csv.WriteField(cellnum);
                        csv.WriteField(puid);
                        csv.NextRecord();
                    }

                    #endregion



                    //    #region ADD REMAINING ROWS

                    //    for (int i = 0; i < LIST_PUIDs.Count; i++)  // each iteration = single planning unit record = single CSV row
                    //    {
                    //        int puid = LIST_PUIDs[i];

                    //        #region FEATURE COLUMN VALUES

                    //        if (!await QueuedTask.Run(async () =>
                    //        {
                    //            try
                    //            {
                    //                QueryFilter featureQF = new QueryFilter
                    //                {
                    //                    WhereClause = $"{PRZC.c_FLD_TAB_PUCF_ID} = {puid}",
                    //                    SubFields = string.Join(",", AreaFieldNames_Features)
                    //                };

                    //                using (Table table = await PRZH.GetTable_PUFeatures())
                    //                using (RowCursor rowCursor = table.Search(featureQF, true))
                    //                {
                    //                    while (rowCursor.MoveNext())
                    //                    {
                    //                        using (Row row = rowCursor.Current)
                    //                        {
                    //                            for (int n = 0; n < AreaFieldNames_Features.Count; n++)
                    //                            {
                    //                                double area_m2 = Math.Round(Convert.ToDouble(row[AreaFieldNames_Features[n]]), 2, MidpointRounding.AwayFromZero);

                    //                                // *** THESE ARE OPTIONAL *********************************
                    //                                double area_ac = Math.Round((area_m2 * PRZC.c_CONVERT_M2_TO_HA), 2, MidpointRounding.AwayFromZero);
                    //                                double area_ha = Math.Round((area_m2 * PRZC.c_CONVERT_M2_TO_HA), 2, MidpointRounding.AwayFromZero);
                    //                                double area_km2 = Math.Round((area_m2 * PRZC.c_CONVERT_M2_TO_KM2), 2, MidpointRounding.AwayFromZero);
                    //                                // ********************************************************

                    //                                csv.WriteField(area_m2);    // make this user-specifiable (e.g. user picks an output unit)
                    //                            }
                    //                        }
                    //                    }
                    //                }

                    //                return true;
                    //            }
                    //            catch (Exception ex)
                    //            {
                    //                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                    //                return false;
                    //            }
                    //        }))
                    //        {
                    //            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error writing features values to CSV.", LogMessageType.ERROR), true, ++val);
                    //            ProMsgBox.Show($"Error writing Features values to CSV.");
                    //            return false;
                    //        }

                    //        #endregion

                    //        #region INCLUDE AND EXCLUDE SELECTION RULES COLUMN VALUES

                    //        if (!await QueuedTask.Run(async () =>
                    //        {
                    //            try
                    //            {
                    //                // Merge the Include and Exclude State Field names
                    //                List<string> StateFieldNames = StateFieldNames_Includes.Concat(StateFieldNames_Excludes).ToList();

                    //                if (StateFieldNames.Count == 0)
                    //                {
                    //                    return true;    // no point proceeding with selection rules, there aren't any
                    //                }

                    //                QueryFilter selruleQF = new QueryFilter
                    //                {
                    //                    WhereClause = $"{PRZC.c_FLD_TAB_PUSELRULES_ID} = {puid}",
                    //                    SubFields = string.Join(",", StateFieldNames)
                    //                };

                    //                using (Table table = await PRZH.GetTable_PUSelRules())
                    //                using (RowCursor rowCursor = table.Search(selruleQF, true))
                    //                {
                    //                    while (rowCursor.MoveNext())
                    //                    {
                    //                        using (Row row = rowCursor.Current)
                    //                        {
                    //                            // First write the includes
                    //                            for (int n = 0; n < StateFieldNames_Includes.Count; n++)
                    //                            {
                    //                                int state = Convert.ToInt32(row[StateFieldNames_Includes[n]]);  // will be a zero or 1
                    //                                csv.WriteField(state);
                    //                            }

                    //                            // Next write the excludes
                    //                            for (int n = 0; n < StateFieldNames_Excludes.Count; n++)
                    //                            {
                    //                                int state = Convert.ToInt32(row[StateFieldNames_Excludes[n]]);  // will be a zero or 1
                    //                                csv.WriteField(state);
                    //                            }
                    //                        }
                    //                    }
                    //                }

                    //                return true;
                    //            }
                    //            catch (Exception ex)
                    //            {
                    //                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                    //                return false;
                    //            }
                    //        }))
                    //        {
                    //            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error writing selection rule values to CSV.", LogMessageType.ERROR), true, ++val);
                    //            ProMsgBox.Show($"Error writing selection rule values to CSV.");
                    //            return false;
                    //        }

                    //        #endregion

                    //        #region WEIGHTS COLUMN VALUES



                    //        #endregion

                    //        // Write the Planning Unit ID to the final column
                    //        csv.WriteField(puid);

                    //        // Finish the line
                    //        csv.NextRecord();
                    //    }

                    //    #endregion
                    //}

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
                        return false;
                    }
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Attribute CSV zipped."), true, ++val);

                #endregion

#if TEST

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
                    if (!await QueuedTask.Run(async () =>
                    {
                        try
                        {
                            using (Table table = await PRZH.GetTable_Boundary())
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
                        return false;
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
                        return false;
                    }
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Boundary CSV zipped."), true, ++val);

#endregion

#endif

                ProMsgBox.Show("Export of WTW Files Complete :)");

                return true;
            }
            catch (Exception ex)
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
            finally
            {
                // reset disabled editing status
                if (edits_are_disabled)
                {
                    await Project.Current.SetIsEditingEnabledAsync(false);
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing disabled."), true, max, ++val);
                }
            }
        }

        private async Task<(bool success, string message)> ExportRasterToShapefile()
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
                if (!await PRZH.RasterExists_PU())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_PLANNING_UNITS} raster dataset not found.", LogMessageType.ERROR), true, ++val);
                    return (false, $"{PRZC.c_RAS_PLANNING_UNITS} raster dataset not found");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_PLANNING_UNITS} raster found."), true, ++val);
                }

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

                // Delete temp feature classes
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {PRZC.c_FC_TEMP_WTW_FC1} and {PRZC.c_FC_TEMP_WTW_FC2} feature classes..."), true, ++val);

                if (await PRZH.FCExists(PRZC.c_FC_TEMP_WTW_FC1))
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

                if (await PRZH.FCExists(PRZC.c_FC_TEMP_WTW_FC2))
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
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<(bool success, string message)> ExportFeaturesToShapefile()
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
                if (!await PRZH.FCExists_PU())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_FC_PLANNING_UNITS} feature class not found.", LogMessageType.ERROR), true, ++val);
                    return (false, $"{PRZC.c_FC_PLANNING_UNITS} feature class not found");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_FC_PLANNING_UNITS} feature class found."), true, ++val);
                }

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

                // Delete temp feature class
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {PRZC.c_FC_TEMP_WTW_FC2} feature class..."), true, ++val);
                if (await PRZH.FCExists(PRZC.c_FC_TEMP_WTW_FC2))
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
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }



        private async Task<bool> Test()
        {
            try
            {

                object o = null;

                string t = (string)o;

                ProMsgBox.Show($"t isnullorempty: {string.IsNullOrEmpty(t)}");


                ProMsgBox.Show("Bort");
                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private void Placeholder()
        {

#region COMPILE FEATURE INFORMATION

            //// First, retrieve key info from the Feature Table
            //PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving records from the {PRZC.c_TABLE_FEATURES} table..."), true, ++val);
            //List<int> LIST_FeatureIDs = new List<int>();
            //var DICT_Features = new Dictionary<int, (string feature_name, string variable_name, string area_field_name, bool enabled, int goal)>();

            //if (!await QueuedTask.Run(async () =>
            //{
            //    try
            //    {
            //        using (Table table = await PRZH.GetTable_Features())
            //        using (RowCursor rowCursor = table.Search(null, false))
            //        {
            //            while (rowCursor.MoveNext())
            //            {
            //                using (Row row = rowCursor.Current)
            //                {
            //                    // Feature ID
            //                    int cfid = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_ID]);

            //                    // Names
            //                    string name = (row[PRZC.c_FLD_TAB_CF_NAME] == null | row[PRZC.c_FLD_TAB_CF_NAME] == DBNull.Value) ? "" : row[PRZC.c_FLD_TAB_CF_NAME].ToString();
            //                    string varname = "CF_" + cfid.ToString("D3");   // Example:  for id 5, we get CF_005
            //                    string areafieldname = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + cfid.ToString() + PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA;

            //                    // Enabled
            //                    bool enabled = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_ENABLED]) == 1;

            //                    // Goal
            //                    int goal = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_GOAL]);

            //                    LIST_FeatureIDs.Add(cfid);
            //                    DICT_Features.Add(cfid, (name, varname, areafieldname, enabled, goal));
            //                }
            //            }
            //        }

            //        return true;
            //    }
            //    catch (Exception ex)
            //    {
            //        ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            //        return false;
            //    }
            //}))
            //{
            //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving information from the {PRZC.c_TABLE_FEATURES} table.", LogMessageType.ERROR), true, ++val);
            //    ProMsgBox.Show($"Error retrieving records from the {PRZC.c_TABLE_FEATURES} table.");
            //    return false;
            //}
            //else
            //{
            //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Records retrieved."), true, ++val);
            //}

            //LIST_FeatureIDs.Sort();

            //// List of Area Fields (in order by cf_id) from PU Features table
            //List<string> AreaFieldNames_Features = new List<string>();
            //for (int k = 0; k < LIST_FeatureIDs.Count; k++)
            //{
            //    AreaFieldNames_Features.Add(DICT_Features[LIST_FeatureIDs[k]].area_field_name);
            //}

#endregion

#region COMPILE SELECTION RULE INFORMATION

            //// Retrieve key information from the Selection Rule table

            //PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving records from the {PRZC.c_TABLE_SELRULES} table..."), true, ++val);
            //List<int> LIST_IncludeIDs = new List<int>();
            //List<int> LIST_ExcludeIDs = new List<int>();
            //var DICT_Includes = new Dictionary<int, (string include_name, string variable_name, string state_field_name, bool enabled)>();
            //var DICT_Excludes = new Dictionary<int, (string exclude_name, string variable_name, string state_field_name, bool enabled)>();

            //if (!await QueuedTask.Run(async () =>
            //{
            //    try
            //    {
            //        using (Table table = await PRZH.GetTable_SelRules())
            //        using (RowCursor rowCursor = table.Search(null, false))
            //        {
            //            while (rowCursor.MoveNext())
            //            {
            //                using (Row row = rowCursor.Current)
            //                {
            //                    // Selection Rule ID
            //                    int srid = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_ID]);

            //                    // Enabled
            //                    bool enabled = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_ENABLED]) == 1;

            //                    // Name
            //                    string name = (row[PRZC.c_FLD_TAB_SELRULES_NAME] == null | row[PRZC.c_FLD_TAB_SELRULES_NAME] == DBNull.Value) ? "" : row[PRZC.c_FLD_TAB_SELRULES_NAME].ToString();

            //                    // Rest depend on the type
            //                    string ruletype = (row[PRZC.c_FLD_TAB_SELRULES_RULETYPE] == null | row[PRZC.c_FLD_TAB_SELRULES_RULETYPE] == DBNull.Value) ? "" : row[PRZC.c_FLD_TAB_SELRULES_RULETYPE].ToString();
            //                    if (ruletype == SelectionRuleType.INCLUDE.ToString())
            //                    {
            //                        string varname = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_INCLUDE + srid.ToString("D3"); // Example:  for id 5, we get IN_005
            //                        string statename = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_INCLUDE + srid.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_STATE;
            //                        LIST_IncludeIDs.Add(srid);
            //                        DICT_Includes.Add(srid, (name, varname, statename, enabled));
            //                    }
            //                    else if (ruletype == SelectionRuleType.EXCLUDE.ToString())
            //                    {
            //                        string varname = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_EXCLUDE + srid.ToString("D3"); // Example:  for id 8, we get EX_008
            //                        string statename = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_EXCLUDE + srid.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_STATE;
            //                        LIST_ExcludeIDs.Add(srid);
            //                        DICT_Excludes.Add(srid, (name, varname, statename, enabled));
            //                    }
            //                }
            //            }
            //        }

            //        return true;
            //    }
            //    catch (Exception ex)
            //    {
            //        ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            //        return false;
            //    }
            //}))
            //{
            //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving information from the {PRZC.c_TABLE_SELRULES} table.", LogMessageType.ERROR), true, ++val);
            //    ProMsgBox.Show($"Error retrieving records from the {PRZC.c_TABLE_SELRULES} table.");
            //    return false;
            //}
            //else
            //{
            //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Records retrieved."), true, ++val);
            //}

            //LIST_IncludeIDs.Sort();
            //LIST_ExcludeIDs.Sort();

            //// INCLUDES: List of State Fields (in order by sr_id) from PU Sel Rules table
            //List<string> StateFieldNames_Includes = new List<string>();
            //for (int k = 0; k < LIST_IncludeIDs.Count; k++)
            //{
            //    StateFieldNames_Includes.Add(DICT_Includes[LIST_IncludeIDs[k]].state_field_name);
            //}

            //// EXCLUDES: List of State Fields (in order by sr_id) from PU Sel Rules table
            //List<string> StateFieldNames_Excludes = new List<string>();
            //for (int k = 0; k < LIST_ExcludeIDs.Count; k++)
            //{
            //    StateFieldNames_Excludes.Add(DICT_Excludes[LIST_ExcludeIDs[k]].state_field_name);
            //}

#endregion

#region COMPILE WEIGHTS INFORMATION

            // TODO: Add this

#endregion

#region GENERATE THE ATTRIBUTE CSV

            //string attributepath = Path.Combine(export_folder_path, PRZC.c_FILE_WTW_EXPORT_ATTR);

            //// If file exists, delete it
            //try
            //{
            //    if (File.Exists(attributepath))
            //    {
            //        File.Delete(attributepath);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    ProMsgBox.Show("Unable to delete the existing Export WTW Attribute file..." +
            //        Environment.NewLine + Environment.NewLine + ex.Message);
            //    return false;
            //}

            //// Get the CSV file started
            //var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            //{
            //    HasHeaderRecord = false, // this is default
            //    NewLine = Environment.NewLine
            //};

            //using (var writer = new StreamWriter(attributepath))
            //using (var csv = new CsvWriter(writer, csvConfig))
            //{
            //    #region ADD COLUMN HEADERS (ROW 1)

            //    // First, add the Feature Variable Name Columns
            //    for (int i = 0; i < LIST_FeatureIDs.Count; i++)
            //    {
            //        csv.WriteField(DICT_Features[LIST_FeatureIDs[i]].variable_name);
            //    }

            //    // Now the Include columns
            //    for (int i = 0; i < LIST_IncludeIDs.Count; i++)
            //    {
            //        csv.WriteField(DICT_Includes[LIST_IncludeIDs[i]].variable_name);
            //    }

            //    // Now the Exclude columns
            //    for (int i = 0; i < LIST_ExcludeIDs.Count; i++)
            //    {
            //        csv.WriteField(DICT_Excludes[LIST_ExcludeIDs[i]].variable_name);
            //    }

            //    // Insert the PU ID Column => must the "_index" and must be the final column in the attributes CSV
            //    csv.WriteField("_index");
            //    csv.NextRecord();   // First line is done!

            //    #endregion

            //    #region ADD REMAINING ROWS

            //    for (int i = 0; i < LIST_PUIDs.Count; i++)  // each iteration = single planning unit record = single CSV row
            //    {
            //        int puid = LIST_PUIDs[i];

            //        #region FEATURE COLUMN VALUES

            //        if (!await QueuedTask.Run(async () =>
            //        {
            //            try
            //            {
            //                QueryFilter featureQF = new QueryFilter
            //                {
            //                    WhereClause = $"{PRZC.c_FLD_TAB_PUCF_ID} = {puid}",
            //                    SubFields = string.Join(",", AreaFieldNames_Features)
            //                };

            //                using (Table table = await PRZH.GetTable_PUFeatures())
            //                using (RowCursor rowCursor = table.Search(featureQF, true))
            //                {
            //                    while (rowCursor.MoveNext())
            //                    {
            //                        using (Row row = rowCursor.Current)
            //                        {
            //                            for (int n = 0; n < AreaFieldNames_Features.Count; n++)
            //                            {
            //                                double area_m2 = Math.Round(Convert.ToDouble(row[AreaFieldNames_Features[n]]), 2, MidpointRounding.AwayFromZero);

            //                                // *** THESE ARE OPTIONAL *********************************
            //                                double area_ac = Math.Round((area_m2 * PRZC.c_CONVERT_M2_TO_HA), 2, MidpointRounding.AwayFromZero);
            //                                double area_ha = Math.Round((area_m2 * PRZC.c_CONVERT_M2_TO_HA), 2, MidpointRounding.AwayFromZero);
            //                                double area_km2 = Math.Round((area_m2 * PRZC.c_CONVERT_M2_TO_KM2), 2, MidpointRounding.AwayFromZero);
            //                                // ********************************************************

            //                                csv.WriteField(area_m2);    // make this user-specifiable (e.g. user picks an output unit)
            //                            }
            //                        }
            //                    }
            //                }

            //                return true;
            //            }
            //            catch (Exception ex)
            //            {
            //                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            //                return false;
            //            }
            //        }))
            //        {
            //            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error writing features values to CSV.", LogMessageType.ERROR), true, ++val);
            //            ProMsgBox.Show($"Error writing Features values to CSV.");
            //            return false;
            //        }

            //        #endregion

            //        #region INCLUDE AND EXCLUDE SELECTION RULES COLUMN VALUES

            //        if (!await QueuedTask.Run(async () =>
            //        {
            //            try
            //            {
            //                // Merge the Include and Exclude State Field names
            //                List<string> StateFieldNames = StateFieldNames_Includes.Concat(StateFieldNames_Excludes).ToList();

            //                if (StateFieldNames.Count == 0)
            //                {
            //                    return true;    // no point proceeding with selection rules, there aren't any
            //                }

            //                QueryFilter selruleQF = new QueryFilter
            //                {
            //                    WhereClause = $"{PRZC.c_FLD_TAB_PUSELRULES_ID} = {puid}",
            //                    SubFields = string.Join(",", StateFieldNames)
            //                };

            //                using (Table table = await PRZH.GetTable_PUSelRules())
            //                using (RowCursor rowCursor = table.Search(selruleQF, true))
            //                {
            //                    while (rowCursor.MoveNext())
            //                    {
            //                        using (Row row = rowCursor.Current)
            //                        {
            //                            // First write the includes
            //                            for (int n = 0; n < StateFieldNames_Includes.Count; n++)
            //                            {
            //                                int state = Convert.ToInt32(row[StateFieldNames_Includes[n]]);  // will be a zero or 1
            //                                csv.WriteField(state);
            //                            }

            //                            // Next write the excludes
            //                            for (int n = 0; n < StateFieldNames_Excludes.Count; n++)
            //                            {
            //                                int state = Convert.ToInt32(row[StateFieldNames_Excludes[n]]);  // will be a zero or 1
            //                                csv.WriteField(state);
            //                            }
            //                        }
            //                    }
            //                }

            //                return true;
            //            }
            //            catch (Exception ex)
            //            {
            //                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            //                return false;
            //            }
            //        }))
            //        {
            //            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error writing selection rule values to CSV.", LogMessageType.ERROR), true, ++val);
            //            ProMsgBox.Show($"Error writing selection rule values to CSV.");
            //            return false;
            //        }

            //        #endregion

            //        #region WEIGHTS COLUMN VALUES



            //        #endregion

            //        // Write the Planning Unit ID to the final column
            //        csv.WriteField(puid);

            //        // Finish the line
            //        csv.NextRecord();
            //    }

            //    #endregion
            //}

            //// Compress Attribute CSV to gzip format
            //FileInfo attribfi = new FileInfo(attributepath);
            //FileInfo attribzgipfi = new FileInfo(string.Concat(attribfi.FullName, ".gz"));

            //using (FileStream fileToBeZippedAsStream = attribfi.OpenRead())
            //using (FileStream gzipTargetAsStream = attribzgipfi.Create())
            //using (GZipStream gzipStream = new GZipStream(gzipTargetAsStream, CompressionMode.Compress))
            //{
            //    try
            //    {
            //        fileToBeZippedAsStream.CopyTo(gzipStream);
            //    }
            //    catch (Exception ex)
            //    {
            //        ProMsgBox.Show("Unable to compress the Attribute CSV file to GZIP..." + Environment.NewLine + Environment.NewLine + ex.Message);
            //        return false;
            //    }
            //}

#endregion

#region GENERATE THE YAML CONFIG FILE

            //#region FEATURES

            //List<YamlTheme> LIST_YamlThemes = new List<YamlTheme>();

            //foreach (int cfid in LIST_FeatureIDs)
            //{
            //    var feature = DICT_Features[cfid];

            //    // Set goal between 0 and 1 inclusive
            //    int g = feature.goal; // g is between 0 and 100 inclusive
            //    double dg = Math.Round(g / 100.0, 2, MidpointRounding.AwayFromZero); // I need dg to be between 0 and 1 inclusive

            //    YamlLegend yamlLegend = new YamlLegend();

            //    YamlVariable yamlVariable = new YamlVariable();
            //    yamlVariable.index = feature.variable_name;
            //    yamlVariable.units = "m\xB2";
            //    yamlVariable.provenance = (cfid % 2 == 0) ? WTWProvenanceType.national.ToString() : WTWProvenanceType.regional.ToString();
            //    yamlVariable.legend = yamlLegend;

            //    YamlFeature yamlFeature = new YamlFeature();
            //    yamlFeature.name = feature.feature_name;
            //    yamlFeature.status = feature.enabled;
            //    yamlFeature.visible = true;
            //    yamlFeature.hidden = false;
            //    yamlFeature.goal = dg;
            //    yamlFeature.variable = yamlVariable;

            //    YamlTheme yamlTheme = new YamlTheme();
            //    yamlTheme.name = $"Theme_{cfid}";
            //    yamlTheme.feature = new YamlFeature[] { yamlFeature };
            //    LIST_YamlThemes.Add(yamlTheme);
            //}

            //#endregion

            //#region INCLUDES

            //List<YamlInclude> LIST_YamlIncludes = new List<YamlInclude>();

            //foreach (var srid in LIST_IncludeIDs)
            //{
            //    var include = DICT_Includes[srid];

            //    // Legend
            //    YamlLegend yamlLegend = new YamlLegend();
            //    yamlLegend.type = WTWLegendType.manual.ToString();
            //    yamlLegend.colors = new string[] { "#ffffff", "#4ce30b" };
            //    yamlLegend.labels = new string[] { "Available", "Included" };

            //    // Variable
            //    YamlVariable yamlVariable = new YamlVariable();
            //    yamlVariable.index = include.variable_name;
            //    yamlVariable.legend = yamlLegend;
            //    yamlVariable.units = "";
            //    yamlVariable.provenance = WTWProvenanceType.national.ToString();

            //    // Include
            //    YamlInclude yamlInclude = new YamlInclude();
            //    yamlInclude.name = include.include_name;
            //    yamlInclude.variable = yamlVariable;
            //    yamlInclude.mandatory = false;
            //    yamlInclude.hidden = false;
            //    yamlInclude.status = include.enabled;
            //    yamlInclude.visible = true;

            //    LIST_YamlIncludes.Add(yamlInclude);
            //}

            //#endregion

            //#region EXCLUDES



            //#endregion

            //#region WEIGHTS



            //#endregion

            //#region FINAL

            //YamlPackage yamlPackage = new YamlPackage();
            //yamlPackage.name = "TEMP PROJECT NAME";
            //yamlPackage.mode = WTWModeType.advanced.ToString();
            //yamlPackage.themes = LIST_YamlThemes.ToArray();
            //yamlPackage.includes = LIST_YamlIncludes.ToArray();
            //yamlPackage.weights = new YamlWeight[] { };

            //ISerializer builder = new SerializerBuilder().DisableAliases().Build();
            //string the_yaml = builder.Serialize(yamlPackage);

            //string yamlpath = Path.Combine(export_folder_path, PRZC.c_FILE_WTW_EXPORT_YAML);
            //try
            //{
            //    File.WriteAllText(yamlpath, the_yaml);
            //}
            //catch (Exception ex)
            //{
            //    ProMsgBox.Show("Unable to write the Yaml Config File..." + Environment.NewLine + Environment.NewLine + ex.Message);
            //    return false;
            //}

            //#endregion

#endregion

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
