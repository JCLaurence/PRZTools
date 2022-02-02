using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZC = NCC.PRZTools.PRZConstants;
using PRZH = NCC.PRZTools.PRZHelper;

namespace NCC.PRZTools
{
    public class DataLoad_RegionalVM : PropertyChangedBase
    {
        public DataLoad_RegionalVM()
        {
        }

        #region FIELDS

        private CancellationTokenSource _cts = null;
        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);
        private bool _operation_Cmd_IsEnabled;
        private bool _operationIsUnderway = false;
        private Cursor _proWindowCursor;

        private bool _pu_exists = false;
        private bool _dir_exists = false;

        #region COMMANDS

        private ICommand _cmdLoadRegionalData;
        private ICommand _cmdCancel;
        private ICommand _cmdClearLog;
        private ICommand _cmdTest;

        #endregion

        #region COMPONENT STATUS INDICATORS

        // Planning Unit Dataset
        private string _compStat_Img_PlanningUnits_Path;
        private string _compStat_Txt_PlanningUnits_Label;

        // Regional Data Folder
        private string _compStat_Img_RegionalData_Path;
        private string _compStat_Txt_RegionalData_Label;

        #endregion

        #region OPERATION STATUS INDICATORS

        private Visibility _opStat_Img_Visibility = Visibility.Collapsed;
        private string _opStat_Txt_Label;

        #endregion

        #endregion

        #region PROPERTIES

        public ProgressManager PM
        {
            get => _pm; set => SetProperty(ref _pm, value, () => PM);
        }

        public bool Operation_Cmd_IsEnabled
        {
            get => _operation_Cmd_IsEnabled;
            set => SetProperty(ref _operation_Cmd_IsEnabled, value, () => Operation_Cmd_IsEnabled);
        }

        public bool OperationIsUnderway
        {
            get => _operationIsUnderway;
        }

        public Cursor ProWindowCursor
        {
            get => _proWindowCursor;
            set => SetProperty(ref _proWindowCursor, value, () => ProWindowCursor);
        }

        #region COMPONENT STATUS INDICATORS

        // Planning Units Dataset
        public string CompStat_Img_PlanningUnits_Path
        {
            get => _compStat_Img_PlanningUnits_Path;
            set => SetProperty(ref _compStat_Img_PlanningUnits_Path, value, () => CompStat_Img_PlanningUnits_Path);
        }

        public string CompStat_Txt_PlanningUnits_Label
        {
            get => _compStat_Txt_PlanningUnits_Label;
            set => SetProperty(ref _compStat_Txt_PlanningUnits_Label, value, () => CompStat_Txt_PlanningUnits_Label);
        }

        // Regional Data Folder
        public string CompStat_Img_RegionalData_Path
        {
            get => _compStat_Img_RegionalData_Path;
            set => SetProperty(ref _compStat_Img_RegionalData_Path, value, () => CompStat_Img_RegionalData_Path);
        }

        public string CompStat_Txt_RegionalData_Label
        {
            get => _compStat_Txt_RegionalData_Label;
            set => SetProperty(ref _compStat_Txt_RegionalData_Label, value, () => CompStat_Txt_RegionalData_Label);
        }


        #endregion

        #region OPERATION STATUS INDICATORS

        public Visibility OpStat_Img_Visibility
        {
            get => _opStat_Img_Visibility;
            set => SetProperty(ref _opStat_Img_Visibility, value, () => OpStat_Img_Visibility);
        }

        public string OpStat_Txt_Label
        {
            get => _opStat_Txt_Label;
            set => SetProperty(ref _opStat_Txt_Label, value, () => OpStat_Txt_Label);
        }

        #endregion

        #region COMMANDS

        public ICommand CmdLoadRegionalData => _cmdLoadRegionalData ?? (_cmdLoadRegionalData = new RelayCommand(async () =>
        {
            // Change UI to Underway
            StartOpUI();

            // Start the operation
            using (_cts = new CancellationTokenSource())
            {
                await LoadRegionalData(_cts.Token);
            }

            // Set source to null (it's already disposed)
            _cts = null;

            // Validate controls
            await ValidateControls();

            // Reset UI to Idle
            ResetOpUI();

        }, () => true, true, false));

        public ICommand CmdCancel => _cmdCancel ?? (_cmdCancel = new RelayCommand(() =>
        {
            if (_cts != null)
            {
                // Optionally notify the user or prompt the user here

                // Cancel the operation
                _cts.Cancel();
            }
        }, () => _cts != null, true, false));

        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true, true, false));

        public ICommand CmdTest => _cmdTest ?? (_cmdTest = new RelayCommand(async () =>
        {
            await Test();
        }, () => true, true, false));

        #endregion

        #endregion

        #region METHODS

        public async Task OnProWinLoaded()
        {
            try
            {
                // Initialize the Progress Bar & Log
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                // Configure a few controls
                await ValidateControls();

                // Reset the UI
                ResetOpUI();
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task LoadRegionalData(CancellationToken token)
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
                string regpath = PRZH.GetPath_RegionalDataFolder();
                string reggoalpath = PRZH.GetPath_RegionalDataSubfolder(RegionalDataSubfolder.GOALS);
                string regweightpath = PRZH.GetPath_RegionalDataSubfolder(RegionalDataSubfolder.WEIGHTS);
                string regincludepath = PRZH.GetPath_RegionalDataSubfolder(RegionalDataSubfolder.INCLUDES);
                string regexcludepath = PRZH.GetPath_RegionalDataSubfolder(RegionalDataSubfolder.EXCLUDES);

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags_GP = GPExecuteToolFlags.GPThread;
                GPExecuteToolFlags toolFlags_GPRefresh = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread;
                string toolOutput;

                // Initialize ProgressBar and Progress Log
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Regional Data Loader..."), false, max, ++val);

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

                // Ensure the Regional Data Folder exists
                if (!PRZH.FolderExists_RegionalData().exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Regional Data Folder not found: {regpath}", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Regional Data Folder not found at {regpath}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Regional Data Folder exists at {regpath}."), true, ++val);
                }

                // Determine the existence of the 4 possible regional data subfolders
                bool direxists_goals = PRZH.FolderExists_RegionalDataSubfolder(RegionalDataSubfolder.GOALS).exists;
                bool direxists_weights = PRZH.FolderExists_RegionalDataSubfolder(RegionalDataSubfolder.WEIGHTS).exists;
                bool direxists_includes = PRZH.FolderExists_RegionalDataSubfolder(RegionalDataSubfolder.INCLUDES).exists;
                bool direxists_excludes = PRZH.FolderExists_RegionalDataSubfolder(RegionalDataSubfolder.EXCLUDES).exists;

                // Ensure there's at least one subfolder
                if (!direxists_goals & !direxists_weights & !direxists_includes & !direxists_excludes)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Regional Data Folder must contain at least one of the following subfolders: {PRZC.c_DIR_REGDATA_GOALS}, {PRZC.c_DIR_REGDATA_WEIGHTS}, {PRZC.c_DIR_REGDATA_INCLUDES}, {PRZC.c_DIR_REGDATA_EXCLUDES}", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Regional Data Folder must contain at least one of the following subfolders: { PRZC.c_DIR_REGDATA_GOALS}, { PRZC.c_DIR_REGDATA_WEIGHTS}, { PRZC.c_DIR_REGDATA_INCLUDES}, { PRZC.c_DIR_REGDATA_EXCLUDES}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Regional Data Subfolders found: {(direxists_goals ? PRZC.c_DIR_REGDATA_GOALS : "")}  {(direxists_weights ? PRZC.c_DIR_REGDATA_WEIGHTS : "")}  {(direxists_includes ? PRZC.c_DIR_REGDATA_INCLUDES : "")}  {(direxists_excludes ? PRZC.c_DIR_REGDATA_EXCLUDES : "")}"), true, ++val);
                }

                // Ensure the Planning Unit dataset exists
                var pu_result = await PRZH.PUExists();
                if (!pu_result.exists || pu_result.puLayerType == PlanningUnitLayerType.UNKNOWN)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit dataset not found.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Planning Unit dataset not found.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit feature class found."), true, ++val);
                }

                // Prompt users for permission to proceed
                if (ProMsgBox.Show("If you proceed, all regional data tables will be deleted and/or overwritten:\n\n" +
                   "Do you wish to proceed?\n\n" +
                   "Choose wisely...",
                   "FILE OVERWRITE WARNING",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out."), true, ++val);
                    return;
                }

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                #endregion

                #region DICTIONARIES

                // Prepare dictionary of puids > cellnumbers (this data is optional)
                Dictionary<int, long> DICT_PUID_and_cellnumbers = new Dictionary<int, long>();

                var tryget_isnat = await PRZH.PUIsNational();
                if (tryget_isnat.is_national)
                {
                    (bool success, Dictionary<int, long> dict, string message) tryget_cellnumbers = await PRZH.GetPUIDsAndCellNumbers();    // this dictionary could have no entries, if the PU dataset has no populated cell_numbers
                    if (!tryget_cellnumbers.success)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving puid dictionary.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error retrieving puid dictionary.");
                        return;
                    }
                    DICT_PUID_and_cellnumbers = tryget_cellnumbers.dict;
                }
                bool pu_is_national = tryget_isnat.is_national;

                #endregion

                #region PREPARE PLANNING UNIT RASTER INFORMATION

                // Some pu infos
                SpatialReference pu_sr = null;
                double pu_raster_cellsize = 0;
                string pu_raster_path = "";
                Envelope extent_pu = null;

                // Gather infos from existing planning unit dataset
                if (pu_result.puLayerType == PlanningUnitLayerType.RASTER)
                {
                    // Get Spatial Reference and cell size
                    await QueuedTask.Run(() =>
                    {
                        var tryget = PRZH.GetRaster_Project(PRZC.c_RAS_PLANNING_UNITS);

                        using (RasterDataset RD = tryget.rasterDataset)
                        using (Raster raster = RD.CreateFullRaster())
                        {
                            pu_sr = raster.GetSpatialReference();
                            var msc = raster.GetMeanCellSize();
                            pu_raster_cellsize = msc.Item1;
                            extent_pu = raster.GetExtent();
                        }
                    });

                    // Get path to original pu raster
                    pu_raster_path = PRZH.GetPath_Project(PRZC.c_RAS_PLANNING_UNITS).path;
                }
                else if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                {
                    // Gather cell size and spatial reference
                    await QueuedTask.Run(() =>
                    {
                        var tryget_fc = PRZH.GetFC_Project(PRZC.c_FC_PLANNING_UNITS);

                        using (FeatureClass fc = tryget_fc.featureclass)
                        using (FeatureClassDefinition fcDef = fc.GetDefinition())
                        using (RowCursor rowCursor = fc.Search())
                        {
                            // get cell size
                            // TODO: Ensure that cell size is derived differently when source pu fc is not a grid.
                            if (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    double l = Convert.ToDouble(row[fcDef.GetLengthField()]);
                                    pu_raster_cellsize = l / 4.0;
                                }
                            }

                            // get spatial reference
                            pu_sr = fcDef.GetSpatialReference();
                        }
                    });

                    // Convert FC to raster
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Converting {PRZC.c_FC_PLANNING_UNITS} featureclass to raster..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_PLANNING_UNITS, PRZC.c_FLD_FC_PU_ID, PRZC.c_RAS_PLANNING_UNITS_TEMP, "CELL_CENTER", "NONE", pu_raster_cellsize, "BUILD");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(
                        workspace: gdbpath,
                        overwriteoutput: true,
                        outputCoordinateSystem: pu_sr);
                    toolOutput = await PRZH.RunGPTool("PolygonToRaster_conversion", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error converting polygons to raster.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error converting polygons to raster.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Polygons converted to raster."), true, ++val);
                    }

                    // Build Pyramids for pu raster
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Building pyramids..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS_TEMP);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("BuildPyramids_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error building pyramids.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error building pyramids.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"pyramids built successfully."), true, ++val);
                    }

                    // Build Statistics
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Calculating Statistics..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS_TEMP, 1, 1, "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("CalculateStatistics_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error calculating statistics.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error calculating statistics.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Statistics calculated."), true, ++val);
                    }

                    // Build the Raster Attribute table...
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Building raster attribute table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS_TEMP, "OVERWRITE");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("BuildRasterAttributeTable_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error Building the raster attribute table .  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error Building the raster attribute table.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Raster attribute table built successfully."), true, ++val);
                    }

                    // Add fields to Raster Attribute Table
                    string fldPUID = PRZC.c_FLD_RAS_PU_ID + " LONG 'Planning Unit ID' # # #;";
                    string fldNatGridCellNum = PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER + " LONG 'National Grid Cell Number' # 0 #;";

                    string flds = fldPUID + fldNatGridCellNum;

                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to raster attribute table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS_TEMP, flds);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to raster attribute table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error adding fields to raster attribute table.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully."), true, ++val);
                    }

                    // Populate the table (pu id and optionally cell numbers)
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Populating the raster attribute table.."), true, ++val);
                    await QueuedTask.Run(() =>
                    {
                        var tryget_gdb = PRZH.GetGDB_Project();

                        using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                        using (RasterDataset rasterDataset = geodatabase.OpenDataset<RasterDataset>(PRZC.c_RAS_PLANNING_UNITS_TEMP))
                        using (Raster raster = rasterDataset.CreateFullRaster())
                        using (Table table = raster.GetAttributeTable())
                        using (RowCursor rowCursor = table.Search(null, false))
                        {
                            geodatabase.ApplyEdits(() =>
                            {
                                while (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        int value = Convert.ToInt32(row[PRZC.c_FLD_RAS_PU_VALUE]);

                                        // copy value to id field
                                        row[PRZC.c_FLD_RAS_PU_ID] = value;

                                        // retrieve cell_number if it exists
                                        if (DICT_PUID_and_cellnumbers.ContainsKey(value))
                                        {
                                            row[PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER] = DICT_PUID_and_cellnumbers[value];
                                        }

                                        row.Store();
                                    }
                                }
                            });

                            // Get the extent
                            extent_pu = raster.GetExtent();
                        }
                    });

                    // get path to new temp pu raster
                    pu_raster_path = PRZH.GetPath_Project(PRZC.c_RAS_PLANNING_UNITS_TEMP).path;
                }

                // Calculate some pu raster-related values
                double poly_to_ras_cellsize = pu_raster_cellsize / 10.0;    // TODO: Is this the best way to determine cell size?
                double poly_to_ras_cellarea = poly_to_ras_cellsize * poly_to_ras_cellsize;  // square meters



                #endregion

                PRZH.CheckForCancellation(token);

                #region DELETE EXISTING GEODATABASE OBJECTS

                // Delete the Regional Element table if present
                if ((await PRZH.TableExists_Project(PRZC.c_TABLE_REG_ELEMENTS)).exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting the {PRZC.c_TABLE_REG_ELEMENTS} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_TABLE_REG_ELEMENTS);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_REG_ELEMENTS} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_REG_ELEMENTS} table.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Table deleted successfully."), true, ++val);
                    }
                }

                PRZH.CheckForCancellation(token);

                // Delete any regional element tables
                var tryget_tables = await PRZH.GetRegionalElementTables();

                if (!tryget_tables.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving list of regional element tables.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving list of regional element tables.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{tryget_tables.tables.Count} regional element tables found."), true, ++val);
                }

                if (tryget_tables.tables.Count > 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {tryget_tables.tables.Count} regional element tables..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(string.Join(";", tryget_tables.tables));
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the regional element tables ({string.Join(";", tryget_tables.tables)}.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the regional element tables.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Regional element tables deleted successfully."), true, ++val);
                    }
                }

                // Delete any regional rasters
                var tryget_regras = await PRZH.GetRegionalElementRasters();
                if (!tryget_regras.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving list of regional element rasters.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving list of regional element rasters.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{tryget_regras.rasters.Count} regional element rasters found."), true, ++val);
                }

                if (tryget_regras.rasters.Count > 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {tryget_regras.rasters.Count} regional element rasters..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(string.Join(";", tryget_regras.rasters));
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting regional element rasters.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the regional element rasters.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Regional element rasters deleted successfully."), true, ++val);
                    }
                }

                // Delete any temp pu reclass rasters if found
                var tryget_pureclass = await PRZH.GetRegionalPUReclassRasters();
                if (!tryget_pureclass.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving list of regional pu reclass rasters.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving list of regional pu reclass rasters.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{tryget_pureclass.rasters.Count} regional pu reclass rasters found."), true, ++val);
                }

                if (tryget_pureclass.rasters.Count > 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {tryget_pureclass.rasters.Count} regional pu reclass rasters..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(string.Join(";", tryget_pureclass.rasters));
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting regional pu reclass rasters.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the regional pu reclass rasters.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Regional pu reclass rasters deleted successfully."), true, ++val);
                    }
                }

                // Delete and rebuild Regional FDS

                // delete...
                var tryex_regfds = await PRZH.FDSExists_Project(PRZC.c_FDS_REGIONAL_ELEMENTS);
                if (tryex_regfds.exists)
                {
                    // delete it
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {PRZC.c_FDS_REGIONAL_ELEMENTS} FDS..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_FDS_REGIONAL_ELEMENTS);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting {PRZC.c_FDS_REGIONAL_ELEMENTS} FDS.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting {PRZC.c_FDS_REGIONAL_ELEMENTS} FDS.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"FDS deleted successfully."), true, ++val);
                    }
                }

                // (re)build!
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating {PRZC.c_FDS_REGIONAL_ELEMENTS} feature dataset..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FDS_REGIONAL_ELEMENTS, pu_sr);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(
                    workspace: gdbpath,
                    overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureDataset_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating feature dataset.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error creating feature dataset.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Feature dataset created."), true, ++val);
                }

                #endregion

                PRZH.CheckForCancellation(token);

                #region CREATE REGIONAL ELEMENT TABLE

                // Create the table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating the {PRZC.c_TABLE_REG_ELEMENTS} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_TABLE_REG_ELEMENTS, "", "", "Regional Elements");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateTable_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {PRZC.c_TABLE_REG_ELEMENTS} table.  GP Tool failed or was cancelled by user.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error creating the {PRZC.c_TABLE_REG_ELEMENTS} table.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Created the {PRZC.c_TABLE_REG_ELEMENTS} table."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Add fields to the table
                string fldElementID = PRZC.c_FLD_TAB_REGELEMENT_ELEMENT_ID + " LONG 'Element ID' # 0 #;";
                string fldElementName = PRZC.c_FLD_TAB_REGELEMENT_NAME + " TEXT 'Element Name' 100 # #;";
                string fldElementType = PRZC.c_FLD_TAB_REGELEMENT_TYPE + $" SHORT 'Element Type' # # '{PRZC.c_DOMAIN_REG_TYPE}';";
                string fldElementStatus = PRZC.c_FLD_TAB_REGELEMENT_STATUS + $" SHORT 'Element Status' # # '{PRZC.c_DOMAIN_REG_STATUS}';";
                string fldThemeID = PRZC.c_FLD_TAB_REGELEMENT_THEME_ID + $" SHORT 'Theme ID' # # '{PRZC.c_DOMAIN_REG_THEME}';";
                string fldElementPresence = PRZC.c_FLD_TAB_REGELEMENT_PRESENCE + $" SHORT 'Element Presence' # # '{PRZC.c_DOMAIN_PRESENCE}';";
                string fldLyrxPath = PRZC.c_FLD_TAB_REGELEMENT_LYRXPATH + " TEXT 'Lyrx Path' 250 # #;";
                string fldLayerName = PRZC.c_FLD_TAB_REGELEMENT_LAYERNAME + " TEXT 'Layer Name' 100 # #;";
                string fldLayerType = PRZC.c_FLD_TAB_REGELEMENT_LAYERTYPE + " TEXT 'Layer Type' 20 # #;";
                string fldLayerJson = PRZC.c_FLD_TAB_REGELEMENT_LAYERJSON + " TEXT 'Layer JSON' 100000 # #;";
                string fldLayerWhereClause = PRZC.c_FLD_TAB_REGELEMENT_WHERECLAUSE + " TEXT 'WHERE Clause' 1000 # #;";
                string fldLegendGroup = PRZC.c_FLD_TAB_REGELEMENT_LEGENDGROUP + " TEXT 'Legend Group' 100 # #;";
                string fldLegendClass = PRZC.c_FLD_TAB_REGELEMENT_LEGENDCLASS + " TEXT 'Legend Class' 100 # #;";

                string fields = fldElementID + 
                                fldElementName + 
                                fldElementType + 
                                fldElementStatus + 
                                fldThemeID + 
                                fldElementPresence + 
                                fldLyrxPath +
                                fldLayerName + 
                                fldLayerType +
                                fldLayerJson +
                                fldLayerWhereClause +
                                fldLegendGroup +
                                fldLegendClass;

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to the {PRZC.c_TABLE_REG_ELEMENTS} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_TABLE_REG_ELEMENTS, fields);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to the {PRZC.c_TABLE_REG_ELEMENTS} table.  GP Tool failed or was cancelled by user.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error adding fields to the {PRZC.c_TABLE_REG_ELEMENTS} table.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Fields added successfully."), true, ++val);
                }

                #endregion

                PRZH.CheckForCancellation(token);

                #region PROCESS EACH REGIONAL SUBDIR

                if (direxists_goals)
                {
                    var subdir = RegionalDataSubfolder.GOALS;
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Processing the regional {subdir} directory..."), true, ++val);
                    var tryprocess = await ProcessRegionalFolder(subdir, pu_raster_cellsize, pu_sr, pu_raster_path, DICT_PUID_and_cellnumbers, extent_pu, token);

                    if (!tryprocess.success)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error processing the regional {subdir} directory.\n{tryprocess.message}"), true, ++val);
                        return;
                    }
                }

                if (direxists_weights)
                {
                    var subdir = RegionalDataSubfolder.WEIGHTS;
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Processing the regional {subdir} directory..."), true, ++val);
                    var tryprocess = await ProcessRegionalFolder(subdir, pu_raster_cellsize, pu_sr, pu_raster_path, DICT_PUID_and_cellnumbers, extent_pu, token);

                    if (!tryprocess.success)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error processing the regional {subdir} directory.\n{tryprocess.message}"), true, ++val);
                        return;
                    }
                }

                if (direxists_includes)
                {
                    var subdir = RegionalDataSubfolder.INCLUDES;
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Processing the regional {subdir} directory..."), true, ++val);
                    var tryprocess = await ProcessRegionalFolder(subdir, pu_raster_cellsize, pu_sr, pu_raster_path, DICT_PUID_and_cellnumbers, extent_pu, token);

                    if (!tryprocess.success)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error processing the regional {subdir} directory.\n{tryprocess.message}"), true, ++val);
                        return;
                    }
                }

                if (direxists_excludes)
                {
                    var subdir = RegionalDataSubfolder.EXCLUDES;
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Processing the regional {subdir} directory..."), true, ++val);
                    var tryprocess = await ProcessRegionalFolder(subdir, pu_raster_cellsize, pu_sr, pu_raster_path, DICT_PUID_and_cellnumbers, extent_pu, token);

                    if (!tryprocess.success)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error processing the regional {subdir} directory.\n{tryprocess.message}"), true, ++val);
                        return;
                    }
                }

                #endregion

                #region GENERATE REGIONAL SPATIAL DATASETS

                // Generate the Regional Element spatial datasets
                var tryspat = await GenerateSpatialDatasets(token);
                if (!tryspat.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error generating regional spatial datasets.\n{tryspat.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error generating regional spatial datasets.\n{tryspat.message}.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Regional spatial datasets generated successfully."), true, ++val);
                }

                #endregion

                // Compact the Geodatabase
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacting the Geodatabase..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath);
                toolOutput = await PRZH.RunGPTool("Compact_management", toolParams, null, toolFlags_GPRefresh);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error compacting the geodatabase. GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error compacting the geodatabase.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Geodatabase compactetd."), true, ++val);
                }

                // End timer
                stopwatch.Stop();
                string message = PRZH.GetElapsedTimeMessage(stopwatch.Elapsed);
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Regional Data loaded successfully."), true, 1, 1);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(message), true, 1, 1);
                ProMsgBox.Show("Regional Data loaded successfully." + Environment.NewLine + Environment.NewLine + message);
            }
            catch (OperationCanceledException)
            {
                // Cancelled by user
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Operation cancelled by user.", LogMessageType.CANCELLATION), true, ++val);
                ProMsgBox.Show($"Operation cancelled by user.");
            }
            catch (Exception ex)
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.CANCELLATION), true, ++val);
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
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

        private async Task<(bool success, string message)> ProcessRegionalFolder(
            RegionalDataSubfolder subFolderType,
            double pu_cell_size,
            SpatialReference pu_sr,
            string pu_raster_path,
            Dictionary<int, long> DICT_PUID_and_cellnumbers,
            Envelope pu_extent,
            CancellationToken token)
        {
            int val = PM.Current;
            int max = PM.Max;

            try
            {
                #region INITIALIZE

                // Initialize a few objects and names
                Map map = MapView.Active.Map;
                string gdbpath = PRZH.GetPath_ProjectGDB();
                string regpath = PRZH.GetPath_RegionalDataFolder();
                string regpath_subdir = PRZH.GetPath_RegionalDataSubfolder(subFolderType);

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags_GP = GPExecuteToolFlags.GPThread;
                GPExecuteToolFlags toolFlags_GPRefresh = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread;
                string toolOutput;

                // Set some values based on subdir type
                string gl_name = "";
                ElementType elementType = ElementType.Unknown;
                ElementTheme elementTheme = ElementTheme.Unknown;

                switch (subFolderType)
                {
                    case RegionalDataSubfolder.GOALS:
                        gl_name = PRZC.c_GROUPLAYER_REG_GOALS;
                        elementType = ElementType.Goal;
                        elementTheme = ElementTheme.RegionalGoal;
                        break;

                    case RegionalDataSubfolder.WEIGHTS:
                        gl_name = PRZC.c_GROUPLAYER_REG_WEIGHTS;
                        elementType = ElementType.Weight;
                        elementTheme = ElementTheme.RegionalWeight;
                        break;

                    case RegionalDataSubfolder.INCLUDES:
                        gl_name = PRZC.c_GROUPLAYER_REG_INCLUDES;
                        elementType = ElementType.Include;
                        elementTheme = ElementTheme.RegionalInclude;
                        break;

                    case RegionalDataSubfolder.EXCLUDES:
                        gl_name = PRZC.c_GROUPLAYER_REG_EXCLUDES;
                        elementType = ElementType.Exclude;
                        elementTheme = ElementTheme.RegionalExclude;
                        break;
                }

                // Calculate some pu raster-related values
                double poly_to_ras_cellsize = pu_cell_size / 10.0;    // TODO: Is this the best way to determine cell size?
                double poly_to_ras_cellarea = poly_to_ras_cellsize * poly_to_ras_cellsize;  // square meters

                #endregion

                #region PREPARE GROUP LAYER

                // Determine if the group layer exists already - if so, delete it
                GroupLayer GL = map.GetLayersAsFlattenedList().OfType<GroupLayer>().FirstOrDefault(gl => string.Equals(gl.Name, gl_name, StringComparison.OrdinalIgnoreCase));
                if (GL != null)
                {
                    await QueuedTask.Run(() => { map.RemoveLayer(GL); });
                }

                // Create the new group layer
                GL = await QueuedTask.Run(() => { return LayerFactory.Instance.CreateGroupLayer(map, map.Layers.Count, gl_name); });

                // Create and load the raster layer
                Uri uri = new Uri(pu_raster_path);
                LayerCreationParams creationParams = new LayerCreationParams(uri)
                {
                    Name = "pu",
                    IsVisible = false
                };

                Layer pu_layer = null;
                await QueuedTask.Run(() =>
                {
                    pu_layer = LayerFactory.Instance.CreateLayer<RasterLayer>(creationParams, GL, LayerPosition.AddToBottom);
                });
                RasterLayer pu_rl = (RasterLayer)pu_layer;
                await MapView.Active.RedrawAsync(false);

                #endregion

                #region ASSEMBLE LAYERS

                // find all lyrx files
                var layer_files = Directory.EnumerateFiles(regpath_subdir, "*.lyrx", SearchOption.TopDirectoryOnly);

                // Lists of individual FL and RL CIM Documents.  I can turn these into layers later.  OPTION A
                List<CIMLayerDocument> cimDocuments_FL = new List<CIMLayerDocument>();
                List<CIMLayerDocument> cimDocuments_RL = new List<CIMLayerDocument>();

                // Lists of lyrx paths and associated lists of FL or RL CIMLayerDocuments.  OPTION B
                List<(string lyrx_path, List<CIMLayerDocument> cimLyrDocs)> FL_CIMDocs = new List<(string lyrx_path, List<CIMLayerDocument> cimLyrDocs)>();
                List<(string lyrx_path, List<CIMLayerDocument> cimLyrDocs)> RL_CIMDocs = new List<(string lyrx_path, List<CIMLayerDocument> cimLyrDocs)>();

                await QueuedTask.Run(() =>
                {
                    foreach (var layer_file in layer_files)
                    {
                        // Get the lyrx file's underlying CIMLayerDocument and all FL and RL cimdefinitions in it
                        LayerDocument layerDocument = new LayerDocument(layer_file);
                        CIMLayerDocument cimLayerDocument = layerDocument.GetCIMLayerDocument();

                        // Get a list of any feature or raster layer definitions in the CIMLayerDocument
                        List<CIMFeatureLayer> cimFLDefs = cimLayerDocument.LayerDefinitions.OfType<CIMFeatureLayer>().ToList();
                        List<CIMRasterLayer> cimRLDefs = cimLayerDocument.LayerDefinitions.OfType<CIMRasterLayer>().ToList();

                        // Move on if this lyrx file contains no feature or raster layers
                        if (cimFLDefs.Count == 0 & cimRLDefs.Count == 0)
                        {
                            continue;
                        }

                        // Process the FL CIMDefinitions
                        if (cimFLDefs.Count > 0)
                        {
                            List<CIMLayerDocument> tempCIMs = new List<CIMLayerDocument>();

                            foreach (var cimFLDef in cimFLDefs)
                            {
                                // Create a new CIMLayerDocument
                                CIMLayerDocument tempCIM = new CIMLayerDocument();

                                // Set properties
                                tempCIM.Layers = new string[] { cimFLDef.URI };
                                tempCIM.LayerDefinitions = new CIMDefinition[] { cimFLDef };

                                // Store the CIM Doc
                                cimDocuments_FL.Add(tempCIM);
                                tempCIMs.Add(tempCIM);
                            }

                            FL_CIMDocs.Add((layer_file, tempCIMs));
                        }

                        // Process the RL CIMDefinitions
                        if (cimRLDefs.Count > 0)
                        {
                            List<CIMLayerDocument> tempCIMs = new List<CIMLayerDocument>();

                            foreach (var cimRLDef in cimRLDefs)
                            {
                                // Create a new CIMLayerDocument
                                CIMLayerDocument tempCIM = new CIMLayerDocument();

                                // Set properties
                                tempCIM.Layers = new string[] { cimRLDef.URI };
                                tempCIM.LayerDefinitions = new CIMDefinition[] { cimRLDef };

                                // Store the CIM Doc
                                cimDocuments_RL.Add(tempCIM);
                                tempCIMs.Add(tempCIM);
                            }

                            RL_CIMDocs.Add((layer_file, tempCIMs));
                        }
                    }
                });

                // Build lists of layers
                List<(string lyrx_path, List<FeatureLayer> FLs)> lyrx_FLs = new List<(string lyrx_path, List<FeatureLayer> FLs)>();
                List<(string lyrx_path, List<RasterLayer> RLs)> lyrx_RLs = new List<(string lyrx_path, List<RasterLayer> RLs)>();

                // Load the feature layers here
                foreach (var o in FL_CIMDocs)
                {
                    List<FeatureLayer> fls = new List<FeatureLayer>();

                    foreach (var a in o.cimLyrDocs)
                    {
                        await QueuedTask.Run(async () =>
                        {
                            // Create and add the layer
                            LayerCreationParams lcparams = new LayerCreationParams(a)
                            {
                                IsVisible = false
                            };

                            var l = LayerFactory.Instance.CreateLayer<FeatureLayer>(lcparams, GL, LayerPosition.AddToTop);

                            // Check for valid source Feature Class
                            bool bad_fc = false;

                            try
                            {
                                using (FeatureClass featureClass = l.GetFeatureClass())
                                {
                                    if (featureClass == null)
                                    {
                                        bad_fc = true;
                                    }
                                }
                            }
                            catch
                            {
                                bad_fc = true;
                            }

                            // If FC is invalid, leave
                            if (bad_fc)
                            {
                                GL.RemoveLayer(l);
                            }

                            // If FL is not a polygon feature layer, leave
                            else if (l.ShapeType != esriGeometryType.esriGeometryPolygon)
                            {
                                GL.RemoveLayer(l);
                            }

                            // If FL has null or unknown spatial reference, leave
                            else
                            {
                                var sr = l.GetSpatialReference();

                                if (sr == null || sr.IsUnknown)
                                {
                                    GL.RemoveLayer(l);
                                }
                                else
                                {
                                    // Finally, we have an OK feature layer!
                                    fls.Add(l);
                                }
                            }

                            await MapView.Active.RedrawAsync(false);
                        });
                    }

                    lyrx_FLs.Add((o.lyrx_path, fls));
                }

                // Load the raster layers here
                foreach (var o in RL_CIMDocs)
                {
                    List<RasterLayer> rls = new List<RasterLayer>();

                    foreach (var a in o.cimLyrDocs)
                    {
                        await QueuedTask.Run(async () =>
                        {
                            // Create and add the layer
                            LayerCreationParams lcparams = new LayerCreationParams(a)
                            {
                                IsVisible = false
                            };

                            var l = LayerFactory.Instance.CreateLayer<RasterLayer>(lcparams, GL, LayerPosition.AddToTop);

                            // Check for a valid Raster source
                            bool bad_raster = false;
                            int band_count = 0;

                            try
                            {
                                using (Raster raster = l.GetRaster())
                                {
                                    if (raster == null)
                                    {
                                        bad_raster = true;
                                    }

                                    band_count = raster.GetBandCount();
                                }
                            }
                            catch
                            {
                                bad_raster = true;
                            }

                            // If Raster is invalid, leave
                            if (bad_raster)
                            {
                                GL.RemoveLayer(l);
                            }

                            // If raster has more than 1 band, leave
                            else if (band_count > 1)
                            {
                                GL.RemoveLayer(l);
                            }

                            // If RL has null or unknown spatial reference, leave
                            else
                            {
                                var sr = l.GetSpatialReference();

                                if (sr == null || sr.IsUnknown)
                                {
                                    GL.RemoveLayer(l);
                                }
                                else
                                {
                                    // Finally, we have an OK raster layer!
                                    rls.Add(l);
                                }
                            }

                            await MapView.Active.RedrawAsync(false);
                        });
                    }

                    lyrx_RLs.Add((o.lyrx_path, rls));
                }

                // ensure at least one layer is available
                bool fls_present = false;
                bool rls_present = false;

                foreach (var u in lyrx_FLs)
                {
                    if (u.FLs.Count > 0)
                    {
                        fls_present = true;
                        break;
                    }
                }

                foreach (var u in lyrx_RLs)
                {
                    if (u.RLs.Count > 0)
                    {
                        rls_present = true;
                        break;
                    }
                }

                if (!fls_present & !rls_present)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"No valid layers found within the regional {subFolderType} directory"), true, ++val);
                    return (true, "success");
                }

                #endregion

                #region PROCESS LAYERS TO EXTRACT ELEMENTS

                // Prepare list of Elements
                List<RegElement> regElements = new List<RegElement>();
                int id = 1;

                // Populate list with Feature Elements
                if (fls_present)
                {
                    for (int i = 0; i < lyrx_FLs.Count; i++)
                    {
                        (string lyrx_path, List<FeatureLayer> FLs) FLInfo = lyrx_FLs[i];

                        foreach (FeatureLayer FL in FLInfo.FLs)
                        {
                            // validate this feature layer for renderer stuff
                            await QueuedTask.Run(async () =>
                            {
                                // Get the renderer
                                var rend = FL.GetRenderer();

                                if (rend is CIMUniqueValueRenderer UVRend)
                                {
                                    // Get the field index plus the field type, for each of the 1, 2, or 3 fields in the UV Renderer
                                    Dictionary<int, FieldCategory> DICT_FieldIndex_and_category = new Dictionary<int, FieldCategory>();

                                    for (int b = 0; b < UVRend.Fields.Length; b++)
                                    {
                                        string uvrend_fieldname = UVRend.Fields[b];

                                        foreach (FieldDescription fieldDescription in FL.GetFieldDescriptions())
                                        {
                                            if (uvrend_fieldname == fieldDescription.Name)
                                            {
                                                FieldCategory fcat = PRZH.GetFieldCategory(fieldDescription);

                                                if (fcat == FieldCategory.DATE)
                                                {
                                                    throw new Exception($"Layer: {FL.Name} >> Date Fields in UV Legends not supported.");
                                                }

                                                DICT_FieldIndex_and_category.Add(b, fcat);
                                            }
                                        }
                                    }

                                    // Make sure we picked up a DICT entry for each UVRend field name
                                    if (UVRend.Fields.Length != DICT_FieldIndex_and_category.Count)
                                    {
                                        throw new Exception($"Layer: {FL.Name} >> Not all renderer fields found within layer table.");
                                    }

                                    // Cycle through each Legend Group, retrieve Group heading...
                                    CIMUniqueValueGroup[] UVGroups = UVRend.Groups;

                                    if (UVGroups is null)
                                    {
                                        throw new Exception($"Layer: {FL.Name} >> UV Renderer has no groups.");
                                    }

                                    foreach (CIMUniqueValueGroup UVGroup in UVGroups)
                                    {
                                        // Get the group heading
                                        string group_heading = UVGroup.Heading;

                                        // Retrieve the Classes in this Group
                                        CIMUniqueValueClass[] UVClasses = UVGroup.Classes;

                                        // Each UVClass will become its own unique regional element
                                        foreach (CIMUniqueValueClass UVClass in UVClasses)
                                        {
                                            // Get the class label
                                            string class_label = UVClass.Label;

                                            // Retrieve the "Tuples" associated with this UVClass
                                            // A "Tuple" is a collection of 1, 2, or 3 specific field values (from the 1, 2 or 3 fields in the UV Renderer)
                                            // A UVClass can consist of 1 or more "Tuples".  By default, 1, but if the user groups together 2 or more classes into a single class,
                                            // the UVClass will consist of those 2 or more "Tuples".
                                            CIMUniqueValue[] UVClassTuples = UVClass.Values;

                                            string classClause = "";

                                            // For Each Tuple (could be 1 to many many)
                                            for (int tupIx = 0; tupIx < UVClassTuples.Length; tupIx++)
                                            {
                                                CIMUniqueValue tuple = UVClassTuples[tupIx];

                                                string tupleClause = "";

                                                // For each field value in the tuple (could be 1, 2, or 3)
                                                for (int fldIx = 0; fldIx < tuple.FieldValues.Length; fldIx++)
                                                {
                                                    string fieldValue = tuple.FieldValues[fldIx];
                                                    bool IsNull = fieldValue == "<Null>";

                                                    string Expression = "";

                                                    switch (DICT_FieldIndex_and_category[fldIx])
                                                    {
                                                        case FieldCategory.STRING:
                                                            Expression = (IsNull) ? "IS NULL" : "= '" + fieldValue.Replace("'", "''") + "'";
                                                            break;

                                                        case FieldCategory.NUMERIC:
                                                            Expression = (IsNull) ? "IS NULL" : "= " + fieldValue;
                                                            break;

                                                        default:
                                                            break;
                                                    }

                                                    // Assemble the unit clause - the building block of the main where clause
                                                    string unitClause = "(" + UVRend.Fields[fldIx] + " " + Expression + ")";

                                                    // Assemble the tuple clause - the where clause of this particular tuple
                                                    if (fldIx == 0)
                                                    {
                                                        tupleClause = unitClause;
                                                    }
                                                    else
                                                    {
                                                        tupleClause += " And " + unitClause;
                                                    }
                                                }

                                                tupleClause = "(" + tupleClause + ")";

                                                if (tupIx == 0)
                                                {
                                                    classClause = tupleClause;
                                                }
                                                else
                                                {
                                                    classClause += " Or " + tupleClause;
                                                }
                                            }

                                            classClause = "(" + classClause + ")";  // this is the where clause

                                            // Assemble the new element
                                            RegElement regElement = new RegElement();

                                            regElement.ElementID = id++;
                                            regElement.ElementName = FL.Name + " - " + group_heading + " - " + class_label;
                                            regElement.ElementType = (int)elementType;
                                            regElement.LayerObject = FL;
                                            regElement.LayerName = FL.Name;
                                            regElement.LayerType = (int)LayerType.FEATURE;
                                            regElement.LayerJson = await QueuedTask.Run(() => { return (FL.GetDefinition()).ToJson(); });
                                            regElement.LyrxPath = FLInfo.lyrx_path;
                                            regElement.WhereClause = classClause;
                                            regElement.LegendGroup = group_heading;
                                            regElement.LegendClass = class_label;
                                            regElement.ThemeID = (int)elementTheme;

                                            regElements.Add(regElement);
                                        }
                                    }
                                }
                                else
                                {
                                    RegElement regElement = new RegElement();

                                    regElement.ElementID = id++;
                                    regElement.ElementName = FL.Name;
                                    regElement.ElementType = (int)elementType;
                                    regElement.LayerObject = FL;
                                    regElement.LayerName = FL.Name;
                                    regElement.LayerType = (int)LayerType.FEATURE;
                                    regElement.LayerJson = await QueuedTask.Run(() => { return (FL.GetDefinition()).ToJson(); });
                                    regElement.LyrxPath = FLInfo.lyrx_path;
                                    regElement.ThemeID = (int)elementTheme;

                                    regElements.Add(regElement);
                                }
                            });
                        }
                    }
                }

                // Populate list with Raster Elements
                if (rls_present)
                {
                    for (int i = 0; i < lyrx_RLs.Count; i++)
                    {
                        (string lyrx_path, List<RasterLayer> RLs) RLInfo = lyrx_RLs[i];

                        foreach (RasterLayer RL in RLInfo.RLs)
                        {
                            RegElement regElement = new RegElement();

                            regElement.ElementID = id++;
                            regElement.ElementName = RL.Name;
                            regElement.ElementType = (int)elementType;
                            regElement.LayerObject = RL;
                            regElement.LayerName = RL.Name;
                            regElement.LayerType = (int)LayerType.RASTER;
                            regElement.LayerJson = await QueuedTask.Run(() => { return (RL.GetDefinition()).ToJson(); });
                            regElement.LyrxPath = RLInfo.lyrx_path;
                            regElement.ThemeID = (int)elementTheme;

                            regElements.Add(regElement);
                        }
                    }
                }

                #endregion

                #region WRITE ELEMENTS TO REG ELEMENT TABLE

                // Sort by element ID (just in case).  I think the objects got added to the list already ordered correctly...
                regElements.Sort((x, y) => x.ElementID.CompareTo(y.ElementID));

                // Ensure the table is present
                var tryex_regelem = await PRZH.TableExists_Project(PRZC.c_TABLE_REG_ELEMENTS);
                if (!tryex_regelem.exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to find the {PRZC.c_TABLE_REG_ELEMENTS} table.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Unable to find the {PRZC.c_TABLE_REG_ELEMENTS} table.");
                    return (false, "error finding table.");
                }

                // Populate the table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Populating the {PRZC.c_TABLE_REG_ELEMENTS} table.."), true, ++val);
                await QueuedTask.Run(() =>
                {
                    var tryget_gdb = PRZH.GetGDB_Project();

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    using (Table table = geodatabase.OpenDataset<Table>(PRZC.c_TABLE_REG_ELEMENTS))
                    using (RowBuffer rowBuffer = table.CreateRowBuffer())
                    {
                        geodatabase.ApplyEdits(() =>
                        {
                            foreach (var elem in regElements)
                            {
                                rowBuffer[PRZC.c_FLD_TAB_REGELEMENT_ELEMENT_ID] = elem.ElementID;
                                rowBuffer[PRZC.c_FLD_TAB_REGELEMENT_NAME] = elem.ElementName;
                                rowBuffer[PRZC.c_FLD_TAB_REGELEMENT_TYPE] = elem.ElementType;
                                rowBuffer[PRZC.c_FLD_TAB_REGELEMENT_STATUS] = (int)ElementStatus.Active;
                                rowBuffer[PRZC.c_FLD_TAB_REGELEMENT_PRESENCE] = (int)ElementPresence.Absent;
                                rowBuffer[PRZC.c_FLD_TAB_REGELEMENT_LYRXPATH] = elem.LyrxPath;
                                rowBuffer[PRZC.c_FLD_TAB_REGELEMENT_LAYERNAME] = elem.LayerName;
                                rowBuffer[PRZC.c_FLD_TAB_REGELEMENT_LAYERTYPE] = ((LayerType)elem.LayerType).ToString();
                                rowBuffer[PRZC.c_FLD_TAB_REGELEMENT_LAYERJSON] = elem.LayerJson;
                                rowBuffer[PRZC.c_FLD_TAB_REGELEMENT_WHERECLAUSE] = elem.WhereClause;
                                rowBuffer[PRZC.c_FLD_TAB_REGELEMENT_LEGENDGROUP] = elem.LegendGroup;
                                rowBuffer[PRZC.c_FLD_TAB_REGELEMENT_LEGENDCLASS] = elem.LegendClass;
                                rowBuffer[PRZC.c_FLD_TAB_REGELEMENT_THEME_ID] = elem.ThemeID;

                                table.CreateRow(rowBuffer);
                            }
                        });
                    }
                });

                #endregion

                #region OVERLAY ELEMENTS WITH PLANNING UNITS

                // pu layer extent (in map SR)
                Envelope extent_pu_query = await QueuedTask.Run(() => { return pu_layer.QueryExtent(); });

                // Process each element
                foreach (var regElement in regElements)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Processing regional element {regElement.ElementID}: {regElement.ElementName}..."), true, ++val);

                    // Get the layer
                    Layer lyr = regElement.LayerObject;

                    // Set up geodatabase object names
                    string reg_raster_init = PRZC.c_RAS_REG_ELEM_PREFIX + regElement.ElementID.ToString() + PRZC.c_RAS_REG_ELEM_SUFFIX_ORIG;
                    string reg_raster_2 = PRZC.c_RAS_REG_ELEM_PREFIX + regElement.ElementID.ToString() + PRZC.c_RAS_REG_ELEM_SUFFIX_RECLASS;
                    string reg_raster_3 = PRZC.c_RAS_REG_ELEM_PREFIX + regElement.ElementID.ToString() + PRZC.c_RAS_REG_ELEM_SUFFIX_ZONAL;

                    string zonal_stats_table = PRZC.c_TABLE_REG_ZONALSTATS_PREFIX + regElement.ElementID.ToString() + PRZC.c_TABLE_REG_ZONALSTATS_SUFFIX;

                    List<string> gdb_objects_delete = new List<string>();

                    #region PROJECTION AND GEOTRANSFORMATION INFORMATION

                    // layer extent (in map SR)
                    Envelope extent_lyr_query = await QueuedTask.Run(() => { return lyr.QueryExtent(); });

                    // intersection of pu and layer extents in map sr
                    EnvelopeBuilderEx builderEx = new EnvelopeBuilderEx(extent_pu_query);
                    bool hasoverlap = builderEx.Intersects(extent_lyr_query);
                    builderEx.Intersection(extent_lyr_query);
                    Envelope extent_intersection_query = (Envelope)builderEx.ToGeometry();

                    if (!hasoverlap)
                    {
                        // extents don't overlap, so no point continuing with this one.
                        continue;
                    }

                    // layer spatial reference
                    SpatialReference lyr_sr = await QueuedTask.Run(() => { return lyr.GetSpatialReference(); });
                    SpatialReference map_sr = map.SpatialReference;

                    // Projection Transformations
                    // Forwards
                    ProjectionTransformation projTrans_pu_to_lyr = ProjectionTransformation.Create(pu_sr, lyr_sr);
                    ProjectionTransformation projTrans_pu_to_map = ProjectionTransformation.Create(pu_sr, map_sr);
                    ProjectionTransformation projTrans_lyr_to_map = ProjectionTransformation.Create(lyr_sr, map_sr);

                    // Inverse
                    ProjectionTransformation projTrans_lyr_to_pu = projTrans_pu_to_lyr.GetInverse();
                    ProjectionTransformation projTrans_map_to_pu = projTrans_pu_to_map.GetInverse();
                    ProjectionTransformation projTrans_map_to_lyr = projTrans_lyr_to_map.GetInverse();

                    // Geographic Transformations (may be null)
                    CompositeGeographicTransformation compGeoTrans_pu_to_lyr = (CompositeGeographicTransformation)projTrans_pu_to_lyr.Transformation;
                    CompositeGeographicTransformation compGeoTrans_lyr_to_pu = (CompositeGeographicTransformation)projTrans_lyr_to_pu.Transformation;
                    CompositeGeographicTransformation compGeoTrans_pu_to_map = (CompositeGeographicTransformation)projTrans_pu_to_map.Transformation;
                    CompositeGeographicTransformation compGeoTrans_map_to_pu = (CompositeGeographicTransformation)projTrans_map_to_pu.Transformation;
                    CompositeGeographicTransformation compGeoTrans_lyr_to_map = (CompositeGeographicTransformation)projTrans_lyr_to_map.Transformation;
                    CompositeGeographicTransformation compGeoTrans_map_to_lyr = (CompositeGeographicTransformation)projTrans_map_to_lyr.Transformation;

                    // Transformation Existences
                    bool geoTransExists_pu_to_lyr = false;
                    bool geoTransExists_lyr_to_pu = false;
                    bool geoTransExists_pu_to_map = false;
                    bool geoTransExists_map_to_pu = false;
                    bool geoTransExists_lyr_to_map = false;
                    bool geoTransExists_map_to_lyr = false;

                    // Transformations as semi-colon delimited strings
                    string geoTransNames_pu_to_lyr = "";
                    string geoTransNames_lyr_to_pu = "";
                    string geoTransNames_pu_to_map = "";
                    string geoTransNames_map_to_pu = "";
                    string geoTransNames_lyr_to_map = "";
                    string geoTransNames_map_to_lyr = "";

                    // Get the transformation strings

                    // PU to LYR
                    if (compGeoTrans_pu_to_lyr != null)
                    {
                        geoTransExists_pu_to_lyr = true;
                        var names = compGeoTrans_pu_to_lyr.Transformations.Select(t => t.Name).ToList();
                        geoTransNames_pu_to_lyr = string.Join(";", names);
                    }

                    // LYR to PU
                    if (compGeoTrans_lyr_to_pu != null)
                    {
                        geoTransExists_lyr_to_pu = true;
                        var names = compGeoTrans_lyr_to_pu.Transformations.Select(t => t.Name).ToList();
                        geoTransNames_lyr_to_pu = string.Join(";", names);
                    }

                    // PU to MAP
                    if (compGeoTrans_pu_to_map != null)
                    {
                        geoTransExists_pu_to_map = true;
                        var names = compGeoTrans_pu_to_map.Transformations.Select(t => t.Name).ToList();
                        geoTransNames_pu_to_map = string.Join(";", names);
                    }

                    // MAP to PU
                    if (compGeoTrans_map_to_pu != null)
                    {
                        geoTransExists_map_to_pu = true;
                        var names = compGeoTrans_map_to_pu.Transformations.Select(t => t.Name).ToList();
                        geoTransNames_map_to_pu = string.Join(";", names);
                    }

                    // LYR to MAP
                    if (compGeoTrans_lyr_to_map != null)
                    {
                        geoTransExists_lyr_to_map = true;
                        var names = compGeoTrans_lyr_to_map.Transformations.Select(t => t.Name).ToList();
                        geoTransNames_lyr_to_map = string.Join(";", names);
                    }

                    // MAP to LYR
                    if (compGeoTrans_map_to_lyr != null)
                    {
                        geoTransExists_map_to_lyr = true;
                        var names = compGeoTrans_map_to_lyr.Transformations.Select(t => t.Name).ToList();
                        geoTransNames_map_to_lyr = string.Join(";", names);
                    }

                    // Prepare the pu extent envelopes
                    Envelope pu_extent_in_lyr_sr = (Envelope)GeometryEngine.Instance.ProjectEx(pu_extent, projTrans_pu_to_lyr);
                    Envelope pu_extent_in_map_sr = (Envelope)GeometryEngine.Instance.ProjectEx(pu_extent, projTrans_pu_to_map);     // should be the same as pu_query_extent

                    #endregion

                    // Process varies based on element layer type
                    LayerType layerType = (LayerType)regElement.LayerType;
                    if (layerType == LayerType.FEATURE)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Source layer is of type: {layerType}"), true, ++val);

                        // Get the element feature layer
                        FeatureLayer regFL = (FeatureLayer)lyr;

                        // Apply selection if where clause was stored
                        if (!string.IsNullOrEmpty(regElement.WhereClause))
                        {
                            await QueuedTask.Run(() =>
                            {
                                QueryFilter queryFilter = new QueryFilter()
                                {
                                    WhereClause = regElement.WhereClause
                                };

                                regFL.Select(queryFilter, SelectionCombinationMethod.New);
                            });

                            if (regFL.SelectionCount == 0)
                            {
                                // no matching features - I can skip this element
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"No feature selection found.  Skipping layer."), true, ++val);
                                continue;
                            }
                        }
                        else
                        {
                            await QueuedTask.Run(() =>
                            {
                                regFL.ClearSelection();
                            });
                        }

                        // Get the object id field
                        string oidfield = await QueuedTask.Run(() =>
                        {
                            using (FeatureClass featureClass = regFL.GetFeatureClass())
                            using (FeatureClassDefinition fcDef = featureClass.GetDefinition())
                            {
                                return fcDef.GetObjectIDField();
                            }
                        });

                        // Convert feature layer to raster
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Converting {regElement.ElementName} to raster..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(regFL, oidfield, reg_raster_init, "CELL_CENTER", "NONE", poly_to_ras_cellsize, "BUILD");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(
                            workspace: gdbpath,
                            overwriteoutput: true,
                            extent: extent_pu_query);
                        toolOutput = await PRZH.RunGPTool("PolygonToRaster_conversion", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error converting polygons to raster.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error converting polygons to raster.");
                            return (false, "error converting polygons to raster.");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Polygons converted to raster."), true, ++val);
                        }

                        // Build Pyramids
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Building pyramids..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(reg_raster_init);
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("BuildPyramids_management", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error building pyramids.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error building pyramids.");
                            return (false, "error building pyramids.");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"pyramids built successfully."), true, ++val);
                        }

                        // Build Statistics
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Calculating Statistics..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(reg_raster_init, 1, 1, "");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("CalculateStatistics_management", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error calculating statistics.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error calculating statistics.");
                            return (false, "error calculating stats.");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Statistics calculated."), true, ++val);
                        }

                        // Reclass the new raster so that all non-null values are now equal to cell area
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Reclassing raster values..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(reg_raster_init, poly_to_ras_cellarea, reg_raster_2, "", "Value IS NOT NULL");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(
                            workspace: gdbpath,
                            overwriteoutput: true,
                            cellSize: poly_to_ras_cellsize);
                        toolOutput = await PRZH.RunGPTool("Con_sa", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error reclassing values.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error reclassing values.");
                            return (false, "error reclassing values.");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Values reclassed."), true, ++val);
                        }

                        // Build Pyramids
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Building pyramids..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(reg_raster_2);
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("BuildPyramids_management", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error building pyramids.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error building pyramids.");
                            return (false, "error building pyramids.");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"pyramids built successfully."), true, ++val);
                        }

                        // Build Statistics
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Calculating Statistics..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(reg_raster_2, 1, 1, "");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("CalculateStatistics_management", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error calculating statistics.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error calculating statistics.");
                            return (false, "error calculating stats.");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Statistics calculated."), true, ++val);
                        }

                        // resize pu raster to match cell size of layer raster
                        string pu_raster_reclass = PRZC.c_RAS_PLANNING_UNITS_RECLASS + regElement.ElementID.ToString();
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Copying PU Raster..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(pu_layer, pu_raster_reclass);
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(
                            workspace: gdbpath,
                            overwriteoutput: true,
                            cellSize: poly_to_ras_cellsize,
                            resamplingMethod: "NEAREST");
                        toolOutput = await PRZH.RunGPTool("CopyRaster_management", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying raster.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error copying raster.");
                            return (false, "error copying raster");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"raster copied."), true, ++val);
                        }
                        gdb_objects_delete.Add(pu_raster_reclass);

                        // Snap raster for both zonal stats tools
                        string snapraster = PRZH.GetPath_Project(pu_raster_reclass).path;

                        // Zonal Statistics (Sum)
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Zonal Statistics..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(pu_raster_reclass, PRZC.c_FLD_RAS_PU_ID, reg_raster_2, reg_raster_3, "SUM", "DATA");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(
                            workspace: gdbpath,
                            overwriteoutput: true,
                            cellSize: poly_to_ras_cellsize,
                            snapRaster: snapraster,
                            outputCoordinateSystem: pu_sr,
                            geographicTransformations: geoTransNames_lyr_to_pu);
                        toolOutput = await PRZH.RunGPTool("ZonalStatistics_sa", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error calculating zonal statistics.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error calculating zonal statistics.");
                            return (false, "error calculating zonal statistics");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Zonal statistics calculated."), true, ++val);
                        }

                        // Zonal Statistics as Table
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Zonal Statistics as Table..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(pu_raster_reclass, PRZC.c_FLD_RAS_PU_ID, reg_raster_2, zonal_stats_table, "DATA", "ALL");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(
                            workspace: gdbpath,
                            overwriteoutput: true,
                            cellSize: poly_to_ras_cellsize,
                            snapRaster: snapraster,
                            outputCoordinateSystem: pu_sr,
                            geographicTransformations: geoTransNames_lyr_to_pu);
                        toolOutput = await PRZH.RunGPTool("ZonalStatisticsAsTable_sa", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error calculating zonal statistics as table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error calculating zonal statistics as table.");
                            return (false, "error calculating zonal statistics as table");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Zonal statistics calculated."), true, ++val);
                        }
                    }
                    else if (layerType == LayerType.RASTER)
                    {
                        // Get the element raster layer
                        RasterLayer regRL = (RasterLayer)regElement.LayerObject;

                        // Get some raster details
                        int band_count = 0;
                        bool has_nodata_value = false;
                        List<double> nodata_values = new List<double>();
                        double celsiz = 0;
                        string interpolation_method = "";

                        await QueuedTask.Run(() =>
                        {
                            using (Raster raster = regRL.GetRaster())
                            {
                                // Determine interpolation method
                                interpolation_method = raster.IsInteger() ? "NEAREST" : "BILINEAR";

                                // Get Band Count
                                band_count = raster.GetBandCount();

                                // Get cell size
                                celsiz = raster.GetMeanCellSize().Item1;

                                // Get nodata values if they exist
                                var nodata_object = raster.GetNoDataValue();

                                if (nodata_object != null)
                                {
                                    // there is a nodata value
                                    has_nodata_value = true;

                                    // cast to ienumerable
                                    IEnumerable enumerable = nodata_object as IEnumerable;

                                    if (enumerable != null)
                                    {
                                        foreach (object o in enumerable)
                                        {
                                            try
                                            {
                                                double d = Convert.ToDouble(o);
                                                nodata_values.Add(d);
                                            }
                                            catch
                                            {
                                                nodata_values.Add(-9999);
                                            }
                                        }
                                    }
                                }
                            }
                        });

                        // Raster can only have a single band
                        if (band_count > 1)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Raster has more than 1 band.\n{regRL.Name}", LogMessageType.VALIDATION_ERROR), true, ++val);
                            ProMsgBox.Show($"Raster has more than 1 band.\n{regRL.Name}");
                            return (false, "error - raster has more than 1 band.");
                        }

                        // Prepare nodata string
                        string nodataval = "";
                        if (has_nodata_value)
                        {
                            nodataval = nodata_values[0].ToString();
                        }

                        if (celsiz > poly_to_ras_cellsize)
                        {
                            celsiz = poly_to_ras_cellsize;
                        }

                        // Copy Raster to temp raster
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Copy raster..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(regRL, reg_raster_init, "", "", "");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(
                            workspace: gdbpath,
                            overwriteoutput: true,
                            extent: extent_pu_query,
                            cellSize: celsiz,
                            rasterStatistics: "STATISTICS 1 1",
                            pyramid: "PYRAMIDS -1",
                            resamplingMethod: interpolation_method);
                        toolOutput = await PRZH.RunGPTool("CopyRaster_management", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying raster.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error copying raster.");
                            return (false, "error copying raster.");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Raster copied."), true, ++val);
                        }

                        // Reclass the new raster so that all zero values are set to NODATA
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Reclassing raster values..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(reg_raster_init, reg_raster_init, reg_raster_2, "Value = 0");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(
                            workspace: gdbpath,
                            overwriteoutput: true,
                            cellSize: celsiz);
                        toolOutput = await PRZH.RunGPTool("SetNull_sa", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error reclassing values.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error reclassing values.");
                            return (false, "error reclassing values.");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Values reclassed."), true, ++val);
                        }

                        // Build Pyramids
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Building pyramids..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(reg_raster_2);
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("BuildPyramids_management", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error building pyramids.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error building pyramids.");
                            return (false, "error building pyramids.");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"pyramids built successfully."), true, ++val);
                        }

                        // Build Statistics
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Calculating Statistics..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(reg_raster_2, 1, 1, "");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("CalculateStatistics_management", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error calculating statistics.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error calculating statistics.");
                            return (false, "error calculating stats.");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Statistics calculated."), true, ++val);
                        }

                        // resize pu raster to match cell size of layer raster
                        string pu_raster_reclass = PRZC.c_RAS_PLANNING_UNITS_RECLASS + regElement.ElementID.ToString();
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Copying PU Raster..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(pu_layer, pu_raster_reclass);
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(
                            workspace: gdbpath,
                            overwriteoutput: true,
                            cellSize: poly_to_ras_cellsize,
                            resamplingMethod: "NEAREST");
                        toolOutput = await PRZH.RunGPTool("CopyRaster_management", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying raster.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error copying raster.");
                            return (false, "error copying raster");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"raster copied."), true, ++val);
                        }
                        gdb_objects_delete.Add(pu_raster_reclass);

                        // Snap raster for both zonal stats tools
                        string snapraster = PRZH.GetPath_Project(pu_raster_reclass).path;

                        // Zonal Statistics (Sum)
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Zonal Statistics..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(pu_raster_reclass, PRZC.c_FLD_RAS_PU_ID, reg_raster_2, reg_raster_3, "SUM", "DATA");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(
                            workspace: gdbpath,
                            overwriteoutput: true,
                            cellSize: poly_to_ras_cellsize,
                            snapRaster: snapraster,
                            outputCoordinateSystem: pu_sr,
                            geographicTransformations: geoTransNames_lyr_to_pu,
                            resamplingMethod: interpolation_method);
                        toolOutput = await PRZH.RunGPTool("ZonalStatistics_sa", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error calculating zonal statistics.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error calculating zonal statistics.");
                            return (false, "error calculating zonal statistics");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Zonal statistics calculated."), true, ++val);
                        }

                        // Zonal Statistics as Table
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Zonal Statistics as Table..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(pu_raster_reclass, PRZC.c_FLD_RAS_PU_ID, reg_raster_2, zonal_stats_table, "DATA", "ALL");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(
                            workspace: gdbpath,
                            overwriteoutput: true,
                            cellSize: poly_to_ras_cellsize,
                            snapRaster: snapraster,
                            outputCoordinateSystem: pu_sr,
                            geographicTransformations: geoTransNames_lyr_to_pu,
                            resamplingMethod: interpolation_method);
                        toolOutput = await PRZH.RunGPTool("ZonalStatisticsAsTable_sa", toolParams, toolEnvs, toolFlags_GP);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error calculating zonal statistics as table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error calculating zonal statistics as table.");
                            return (false, "error calculating zonal statistics as table");
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Zonal statistics calculated."), true, ++val);
                        }
                    }

                    // Retrieve dictionary from zonal stats table:   puid, sum
                    Dictionary<int, double> DICT_PUID_and_value_sum = new Dictionary<int, double>();

                    await QueuedTask.Run(() =>
                    {
                        var tryget_table = PRZH.GetTable_Project(zonal_stats_table);
                        if (!tryget_table.success)
                        {
                            throw new Exception($"Unable to retrieve the temp zonal stats table {zonal_stats_table}");
                        }

                        using (Table table = tryget_table.table)
                        using (RowCursor rowCursor = table.Search())
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int puid = Convert.ToInt32(row[PRZC.c_FLD_RAS_PU_ID]);
                                    double sumval = Convert.ToDouble(row[PRZC.c_FLD_ZONALSTATS_SUM]);

                                    //if (puid > 0 && sumval > 0)
                                    if (puid > 0)
                                    {
                                        DICT_PUID_and_value_sum.Add(puid, sumval);
                                    }
                                }
                            }
                        }
                    });

                    // Delete temp objects
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting temp objects..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(string.Join(";", gdb_objects_delete));
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting temp objects.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting temp objects.");
                        return (false, "error deleting temp objects.");
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Temp objects deleted successfully."), true, ++val);
                    }

                    // if there are no entries in the zonal stats dict, move on to next regElement
                    if (DICT_PUID_and_value_sum.Count == 0)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"No data to store for regional element {regElement.ElementName}"), true, ++val);
                        continue;
                    }

                    // Create the regional element table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating the {regElement.ElementTable} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, regElement.ElementTable, "", "", "Element " + regElement.ElementID.ToString("D5"));
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("CreateTable_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {regElement.ElementTable} table.  GP Tool failed or was cancelled by user.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error creating the {regElement.ElementTable} table.");
                        return (false, "error creating table.");
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Created the {regElement.ElementTable} table."), true, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Add fields to the table
                    string fldPUID = PRZC.c_FLD_TAB_REG_ELEMVAL_PU_ID + " LONG 'Planning Unit ID' # 0 #;";
                    string fldCellNum = PRZC.c_FLD_TAB_REG_ELEMVAL_CELL_NUMBER + " LONG 'Cell Number' # 0 #;";
                    string fldCellVal = PRZC.c_FLD_TAB_REG_ELEMVAL_CELL_VALUE + " DOUBLE 'Cell Value' # 0 #;";

                    string flds = fldPUID + fldCellNum + fldCellVal;

                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to the {regElement.ElementTable} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(regElement.ElementTable, flds);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to the {regElement.ElementTable} table.  GP Tool failed or was cancelled by user.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error adding fields to the {regElement.ElementTable} table.");
                        return (false, "error adding fields.");
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Fields added successfully."), true, ++val);
                    }

                    // Populate the table...
                    await QueuedTask.Run(() =>
                    {
                        var tryget_gdb = PRZH.GetGDB_Project();

                        using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                        using (Table table = geodatabase.OpenDataset<Table>(regElement.ElementTable))
                        using (RowBuffer rowBuffer = table.CreateRowBuffer())
                        {
                            geodatabase.ApplyEdits(() =>
                            {
                                foreach (int puid in DICT_PUID_and_value_sum.Keys) // PUIDs showing up in this regElement's zonal stats table
                                {
                                    // store the puid
                                    rowBuffer[PRZC.c_FLD_TAB_REG_ELEMVAL_PU_ID] = puid;

                                    // store the value
                                    rowBuffer[PRZC.c_FLD_TAB_REG_ELEMVAL_CELL_VALUE] = DICT_PUID_and_value_sum[puid];

                                    // store the cell number (if present)
                                    if (DICT_PUID_and_cellnumbers.ContainsKey(puid))
                                    {
                                        rowBuffer[PRZC.c_FLD_TAB_REG_ELEMVAL_CELL_NUMBER] = DICT_PUID_and_cellnumbers[puid];
                                    }

                                    table.CreateRow(rowBuffer);
                                }
                            });
                        }
                    });

                    // index the PUID field
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing {PRZC.c_FLD_TAB_REG_ELEMVAL_PU_ID} field in the {regElement.ElementTable} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(regElement.ElementTable, PRZC.c_FLD_TAB_REG_ELEMVAL_PU_ID, "ix" + PRZC.c_FLD_TAB_REG_ELEMVAL_PU_ID, "", "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Error indexing field.");
                        return (false, "error indexing field.");
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Field indexed successfully."), true, ++val);
                    }

                    // index the Cell Number field
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing {PRZC.c_FLD_TAB_REG_ELEMVAL_CELL_NUMBER} field in the {regElement.ElementTable} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(regElement.ElementTable, PRZC.c_FLD_TAB_REG_ELEMVAL_CELL_NUMBER, "ix" + PRZC.c_FLD_TAB_REG_ELEMVAL_CELL_NUMBER, "", "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Error indexing field.");
                        return (false, "error indexing field.");
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Field indexed successfully."), true, ++val);
                    }

                    // Update the regElements table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Updating the {PRZC.c_TABLE_REG_ELEMENTS} table.."), true, ++val);
                    await QueuedTask.Run(() =>
                    {
                        var tryget_gdb = PRZH.GetGDB_Project();

                        using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                        using (Table table = geodatabase.OpenDataset<Table>(PRZC.c_TABLE_REG_ELEMENTS))
                        {
                            QueryFilter queryFilter = new QueryFilter()
                            {
                                WhereClause = $"{PRZC.c_FLD_TAB_REGELEMENT_ELEMENT_ID} = {regElement.ElementID}"
                            };

                            using (RowCursor rowCursor = table.Search(queryFilter, false))
                            {
                                if (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        geodatabase.ApplyEdits(() =>
                                        {
                                            row[PRZC.c_FLD_TAB_REGELEMENT_PRESENCE] = (int)ElementPresence.Present;
                                            row.Store();
                                        });
                                    }
                                }
                            }
                        }
                    });
                }

                #endregion

                return (true, "success");
            }
            catch (OperationCanceledException cancelex)
            {
                throw cancelex;
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<(bool success, string message)> GenerateSpatialDatasets(CancellationToken token)
        {
            int val = PM.Current;
            int max = PM.Max;

            try
            {

                // we're done here
                return (true, "success");
            }
            catch (OperationCanceledException cancelex)
            {
                throw cancelex;
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task ValidateControls()
        {
            try
            {
                // Establish Geodatabase Object Existence:
                // 1. Planning Unit Dataset
                var try_exists = await PRZH.PUExists();
                _pu_exists = try_exists.exists;

                // 2. Boundary Lengths Table
                var try_existsdir = PRZH.FolderExists_RegionalData();
                _dir_exists = try_existsdir.exists;

                // Configure Labels:
                // 1. Planning Unit Dataset Label
                if (!_pu_exists || try_exists.puLayerType == PlanningUnitLayerType.UNKNOWN)
                {
                    CompStat_Txt_PlanningUnits_Label = "Planning Unit Dataset does not exist.  Build it.";
                }
                else if (try_exists.puLayerType == PlanningUnitLayerType.FEATURE)
                {
                    CompStat_Txt_PlanningUnits_Label = "Planning Unit Dataset exists (Feature Class).";
                }
                else if (try_exists.puLayerType == PlanningUnitLayerType.RASTER)
                {
                    CompStat_Txt_PlanningUnits_Label = "Planning Unit Dataset exists (Raster Dataset).";
                }
                else
                {
                    CompStat_Txt_PlanningUnits_Label = "Planning Unit Dataset does not exist.  Build it.";
                }

                // 2. Regional Data Folder
                if (_dir_exists)
                {
                    CompStat_Txt_RegionalData_Label = $"Regional Data Folder exists ({PRZH.GetPath_RegionalDataFolder()}).";
                }
                else
                {
                    CompStat_Txt_RegionalData_Label = $"Regional Data Folder not found at path: {PRZH.GetPath_RegionalDataFolder()}";
                }


                // Configure Images:
                // 1. Planning Units
                if (_pu_exists)
                {
                    CompStat_Img_PlanningUnits_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_Img_PlanningUnits_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
                }

                // 2. Regional Data Folder
                if (_dir_exists)
                {
                    CompStat_Img_RegionalData_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_Img_RegionalData_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private void StartOpUI()
        {
            _operationIsUnderway = true;
            Operation_Cmd_IsEnabled = false;
            OpStat_Img_Visibility = Visibility.Visible;
            OpStat_Txt_Label = "Processing...";
            ProWindowCursor = Cursors.Wait;
        }

        private void ResetOpUI()
        {
            ProWindowCursor = Cursors.Arrow;
            Operation_Cmd_IsEnabled = _pu_exists && _dir_exists;
            OpStat_Img_Visibility = Visibility.Hidden;
            OpStat_Txt_Label = "Idle";
            _operationIsUnderway = false;
        }

        private async Task Test()
        {
            int val = PM.Current;
            int max = PM.Max;

            try
            {

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags_GP = GPExecuteToolFlags.GPThread;
                string toolOutput;

                SpatialReference sr = await QueuedTask.Run(() =>
                {
                    var a = PRZH.GetFC_Project(PRZC.c_FC_PLANNING_UNITS);

                    using (FeatureClass fc = a.featureclass)
                    using (FeatureClassDefinition fcDef = fc.GetDefinition())
                    {
                        SpatialReference sr2 = fcDef.GetSpatialReference();
                        return sr2;
                    }
                });

                string gdbpath = PRZH.GetPath_ProjectGDB();
                string outputpath = PRZH.GetPath_Project("test", PRZC.c_FDS_NATIONAL_ELEMENTS).path;

                // Copy pufc to fds
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_PLANNING_UNITS, outputpath);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(
                    workspace: gdbpath,
                    overwriteoutput: true,
                    outputCoordinateSystem: sr);
                toolOutput = await PRZH.RunGPTool("CopyFeatures_management", toolParams, toolEnvs, toolFlags_GP | GPExecuteToolFlags.AddToHistory);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying features.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error copying features.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"features copied."), true, ++val);
                }




                // copy to fds
                // join field
                // delete by query
                // remove fields

                // OR...


                // add join
                // copy features
                // remove fields
                // remove layer



                //// Make feature layer
                //toolParams = Geoprocessing.MakeValueArray(fcpath, "test_layer");
                //toolEnvs = Geoprocessing.MakeEnvironmentArray();
                //toolOutput = await PRZH.RunGPTool("MakeFeatureLayer_management", toolParams, toolEnvs, toolFlags_GP | GPExecuteToolFlags.AddOutputsToMap);
                //if (toolOutput == null)
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error making layer.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                //    ProMsgBox.Show($"Error making layer.");
                //    return;
                //}
                //else
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"layer created."), true, ++val);
                //    ProMsgBox.Show(toolOutput);
                //}

                //string layer_name = toolOutput;
                //string outputfcpath = PRZH.GetPath_Project("bloard").path;
                //ProMsgBox.Show(outputfcpath);

                //// Copy layer to disk
                //toolParams = Geoprocessing.MakeValueArray(layer_name, outputfcpath);
                //toolEnvs = Geoprocessing.MakeEnvironmentArray();
                //toolOutput = await PRZH.RunGPTool("CopyFeatures_management", toolParams, toolEnvs, toolFlags_GP | GPExecuteToolFlags.AddToHistory);
                //if (toolOutput == null)
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying features.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                //    ProMsgBox.Show($"Error copying features.");
                //    return;
                //}
                //else
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"features copied."), true, ++val);
                //}

                //// Add Join
                //toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_PLANNING_UNITS, PRZC.c_FLD_FC_PU_ID, "n00010", PRZC.c_FLD_TAB_NAT_ELEMVAL_PU_ID, "KEEP_COMMON");
                //toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                //toolOutput = await PRZH.RunGPTool("AddJoin_management", toolParams, toolEnvs, toolFlags_GP | GPExecuteToolFlags.AddToHistory);
                //if (toolOutput == null)
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding join.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                //    ProMsgBox.Show($"Error adding join.");
                //    return;
                //}
                //else
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"join added."), true, ++val);
                //}

                //string lyr = toolOutput;
                //ProMsgBox.Show($"Layer Name: {lyr}");

                //string outputfcpath = PRZH.GetPath_Project("bloard").path;
                //ProMsgBox.Show(outputfcpath);

                //// Copy layer to disk
                //toolParams = Geoprocessing.MakeValueArray(lyr, outputfcpath);
                //toolEnvs = Geoprocessing.MakeEnvironmentArray(
                //    qualifiedFieldNames: false);
                //toolOutput = await PRZH.RunGPTool("CopyFeatures_management", toolParams, toolEnvs, toolFlags_GP | GPExecuteToolFlags.AddToHistory);
                //if (toolOutput == null)
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying features.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                //    ProMsgBox.Show($"Error copying features.");
                //    return;
                //}
                //else
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"features copied."), true, ++val);
                //}

                //ProMsgBox.Show("About to delete...");

                //toolParams = Geoprocessing.MakeValueArray(lyr);
                //toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                //toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP | GPExecuteToolFlags.AddToHistory);
                //if (toolOutput == null)
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting layer.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                //    ProMsgBox.Show($"Error deleting layer.");
                //    return;
                //}
                //else
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"layer deleted."), true, ++val);
                //}

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message);
            }
        }

        #endregion



    }
}

/*
                await QueuedTask.Run(() =>
                {
                    foreach (var layer_file in layer_files)
                    {
                        // Get the layer file LayerDocument and CIMLayerDocument
                        LayerDocument layerDocument = new LayerDocument(layer_file);

                        // Get the individual CIMDefinitions for each layer in the lyrx file
                        CIMDefinition[] layerdefs = layerDocument.GetCIMLayerDocument().LayerDefinitions;

                        foreach (CIMDefinition def in layerdefs)
                        {
                            if (def is CIMFeatureLayer cimFL)
                            {
                                CIMLayerDocument cimLayerDoc = layerDocument.GetCIMLayerDocument();

                                ProMsgBox.Show($"Feature Layer Name: {def.Name}");

                                var defs = cimLayerDoc.LayerDefinitions;

                                defs = new CIMDefinition[] { def };

                                cimLayerDoc.LayerDefinitions = defs;

                                LayerDocument newLD = new LayerDocument(cimLayerDoc);
                                newLD.Save($@"c:\temp\{def.Name}.lyrx");
                            }
                            else if (def is CIMRasterLayer cimRL)
                            {
                                ProMsgBox.Show($"Name: {def.Name}\nURI: {def.URI}\nRaster Layer!");
                            }
                            else
                            {
                                ProMsgBox.Show($"Name: {def.Name}\nURI: {def.URI}\nSome other layer type");
                            }
                        }
                    }
                });

 * 
 */ 