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

        private Visibility _opStat_Img_Visibility;
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
                bool goaldirexists = PRZH.FolderExists_RegionalDataSubfolder(RegionalDataSubfolder.GOALS).exists;
                bool weightdirexists = PRZH.FolderExists_RegionalDataSubfolder(RegionalDataSubfolder.WEIGHTS).exists;
                bool includedirexists = PRZH.FolderExists_RegionalDataSubfolder(RegionalDataSubfolder.INCLUDES).exists;
                bool excludedirexists = PRZH.FolderExists_RegionalDataSubfolder(RegionalDataSubfolder.EXCLUDES).exists;

                // Ensure there's at least one subfolder
                if (!goaldirexists & !weightdirexists & !includedirexists & !excludedirexists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Regional Data Folder must contain at least one of the following subfolders: {PRZC.c_DIR_REGDATA_GOALS}, {PRZC.c_DIR_REGDATA_WEIGHTS}, {PRZC.c_DIR_REGDATA_INCLUDES}, {PRZC.c_DIR_REGDATA_EXCLUDES}", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Regional Data Folder must contain at least one of the following subfolders: { PRZC.c_DIR_REGDATA_GOALS}, { PRZC.c_DIR_REGDATA_WEIGHTS}, { PRZC.c_DIR_REGDATA_INCLUDES}, { PRZC.c_DIR_REGDATA_EXCLUDES}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Regional Data Subfolders found: {(goaldirexists ? PRZC.c_DIR_REGDATA_GOALS : "")}  {(weightdirexists ? PRZC.c_DIR_REGDATA_WEIGHTS : "")}  {(includedirexists ? PRZC.c_DIR_REGDATA_INCLUDES : "")}  {(excludedirexists ? PRZC.c_DIR_REGDATA_EXCLUDES : "")}"), true, ++val);
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

                PRZH.CheckForCancellation(token);

                #region DELETE EXISTING TABLES

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags_GP = GPExecuteToolFlags.GPThread;
                GPExecuteToolFlags toolFlags_GPRefresh = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread;
                string toolOutput;

                // Delete the Regional Theme Table if present
                if ((await PRZH.TableExists_Project(PRZC.c_TABLE_REG_THEMES)).exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting the {PRZC.c_TABLE_REG_THEMES} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_TABLE_REG_THEMES);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_REG_THEMES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_REG_THEMES} table.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Table deleted successfully."), true, ++val);
                    }
                }

                PRZH.CheckForCancellation(token);

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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{tryget_tables.tables.Count} regional element tables found.", LogMessageType.ERROR), true, ++val);
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

                // TODO: Delete any previously-created temp datasets (TABLES, RASTERS, FEATURECLASSES)


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
                string fldElementType = PRZC.c_FLD_TAB_REGELEMENT_TYPE + $" SHORT 'Element Type' # 0 '{PRZC.c_DOMAIN_REG_TYPE}';";
                string fldElementStatus = PRZC.c_FLD_TAB_REGELEMENT_STATUS + $" SHORT 'Element Status' # 0 '{PRZC.c_DOMAIN_REG_STATUS}';";
                //string fldThemeID = PRZC.c_FLD_TAB_REGELEMENT_THEME_ID + $" SHORT 'Theme ID' # 0 '{PRZC.c_DOMAIN_THEME_NAMES}';";
                string fldThemeID = PRZC.c_FLD_TAB_REGELEMENT_THEME_ID + $" SHORT 'Theme ID' # 0 #;";
                string fldElementPresence = PRZC.c_FLD_TAB_REGELEMENT_PRESENCE + $" SHORT 'Element Presence' # 0 '{PRZC.c_DOMAIN_PRESENCE}';";
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

                #region PROCESS THE GOALS FOLDER

                if (goaldirexists)
                {
                    #region ASSEMBLE LAYERS

                    // find all lyrx files
                    var layer_files = Directory.EnumerateFiles(reggoalpath, "*.lyrx", SearchOption.TopDirectoryOnly);

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

                    // Create temp group layer (delete first if already exists)
                    GroupLayer GL_GOALS = map.GetLayersAsFlattenedList().OfType<GroupLayer>().FirstOrDefault(gl => gl.Name == "REGGOALS");

                    if (GL_GOALS != null)
                    {
                        await QueuedTask.Run(() => { map.RemoveLayer(GL_GOALS); });
                    }

                    GL_GOALS = await QueuedTask.Run(() => { return LayerFactory.Instance.CreateGroupLayer(map, map.Layers.Count, "REGGOALS"); });

                    // Build lists of layers
                    List<(string lyrx_path, List<FeatureLayer> FLs)> lyrx_FLs = new List<(string lyrx_path, List<FeatureLayer> FLs)>();
                    List<(string lyrx_path, List<RasterLayer> RLs)> lyrx_RLs = new List<(string lyrx_path, List<RasterLayer> RLs)>();

                    // Load the feature layers here
                    foreach (var o in FL_CIMDocs)
                    {
                        List<FeatureLayer> fls = new List<FeatureLayer>();

                        foreach(var a in o.cimLyrDocs)
                        {
                            await QueuedTask.Run(async () =>
                            {
                                // Create and add the layer
                                LayerCreationParams lcparams = new LayerCreationParams(a)
                                {
                                    IsVisible = false
                                };

                                var l = LayerFactory.Instance.CreateLayer<FeatureLayer>(lcparams, GL_GOALS, LayerPosition.AddToTop);

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
                                    GL_GOALS.RemoveLayer(l);
                                }

                                // If FL is not a polygon feature layer, leave
                                else if (l.ShapeType != esriGeometryType.esriGeometryPolygon)
                                {
                                    GL_GOALS.RemoveLayer(l);
                                }

                                // If FL has null or unknown spatial reference, leave
                                else
                                {
                                    var sr = l.GetSpatialReference();

                                    if (sr == null || sr.IsUnknown)
                                    {
                                        GL_GOALS.RemoveLayer(l);
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

                                var l = LayerFactory.Instance.CreateLayer<RasterLayer>(lcparams, GL_GOALS, LayerPosition.AddToTop);

                                // Check for a valid Raster source
                                bool bad_raster = false;

                                try
                                {
                                    using (Raster raster = l.GetRaster())
                                    {
                                        if (raster == null)
                                        {
                                            bad_raster = true;
                                        }
                                    }
                                }
                                catch
                                {
                                    bad_raster = true;
                                }

                                // If Raster is invalid, leave
                                if (bad_raster)
                                {
                                    GL_GOALS.RemoveLayer(l);
                                }

                                // If RL has null or unknown spatial reference, leave
                                else
                                {
                                    var sr = l.GetSpatialReference();

                                    if (sr == null || sr.IsUnknown)
                                    {
                                        GL_GOALS.RemoveLayer(l);
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
                        ProMsgBox.Show("No available layers for processing!");
                        return;
                    }

                    #endregion

                    #region PROCESS LAYERS TO EXTRACT ELEMENTS

                    // I will be populating a list of RegElement objects

                    List<RegElement> regElements = new List<RegElement>();
                    int id = 1;

                    // First, the Feature Layers
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
                                                regElement.ElementType = (int)ElementType.Goal;
                                                regElement.LayerObject = FL;
                                                regElement.LayerName = FL.Name;
                                                regElement.LayerType = (int)LayerType.FEATURE;
                                                regElement.LayerJson = await QueuedTask.Run(() => { return ((CIMBaseLayer)FL.GetDefinition()).ToJson(); });
                                                regElement.LyrxPath = FLInfo.lyrx_path;
                                                regElement.WhereClause = classClause;
                                                regElement.LegendGroup = group_heading;
                                                regElement.LegendClass = class_label;

                                                regElements.Add(regElement);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        RegElement regElement = new RegElement();

                                        regElement.ElementID = id++;
                                        regElement.ElementName = FL.Name;
                                        regElement.ElementType = (int)ElementType.Goal;
                                        regElement.LayerObject = FL;
                                        regElement.LayerName = FL.Name;
                                        regElement.LayerType = (int)LayerType.FEATURE;
                                        regElement.LayerJson = await QueuedTask.Run(() => { return ((CIMBaseLayer)FL.GetDefinition()).ToJson(); });
                                        regElement.LyrxPath = FLInfo.lyrx_path;

                                        regElements.Add(regElement);
                                    }
                                });
                            }                            
                        }
                    }
                    
                    // Next, the Raster Layers
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
                                regElement.ElementType = (int)ElementType.Goal;
                                regElement.LayerObject = RL;
                                regElement.LayerName = RL.Name;
                                regElement.LayerType = (int)LayerType.RASTER;
                                regElement.LayerJson = await QueuedTask.Run(() => { return ((CIMBaseLayer)RL.GetDefinition()).ToJson(); });
                                regElement.LyrxPath = RLInfo.lyrx_path;

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
                        return;
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

                                    table.CreateRow(rowBuffer);
                                }
                            });
                        }
                    });

                    #endregion

                    #region LOAD PLANNING UNIT LAYER

                    Layer pu_layer = null;

                    Uri uri = new Uri(pu_path);
                    LayerCreationParams pa = new LayerCreationParams(uri)
                    {
                        Name = "pu",
                        IsVisible = false
                    };

                    if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                    {
                        await QueuedTask.Run(() =>
                        {
                            pu_layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(pa, GL_GOALS, LayerPosition.AddToBottom);                        
                        });
                    }
                    else
                    {
                        await QueuedTask.Run(() =>
                        {
                            pu_layer = LayerFactory.Instance.CreateLayer<RasterLayer>(pa, GL_GOALS, LayerPosition.AddToBottom);
                        });
                    }

                    await MapView.Active.RedrawAsync(false);

                    #endregion

                    #region OVERLAY ELEMENTS WITH PLANNING UNITS

                    // Process depends on Planning Unit dataset type
                    if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                    {
                        // Process each element
                        foreach (var regElement in regElements)
                        {
                            // Process varies based on element layer type
                            LayerType layerType = (LayerType)regElement.LayerType;

                            if (layerType == LayerType.FEATURE)
                            {


                            }
                            else if (layerType == LayerType.RASTER)
                            {


                            }
                            else
                            {
                                // huh?
                            }
                        }
                    }
                    else if (pu_result.puLayerType == PlanningUnitLayerType.RASTER)
                    {
                        // Get some pu raster properties
                        RasterLayer pu_rl = (RasterLayer)pu_layer;
                        double pu_ras_cellsize = await QueuedTask.Run(() =>
                        {
                            using (Raster raster = pu_rl.GetRaster())
                            {
                                return raster.GetMeanCellSize().Item1;
                            }
                        });
                        double poly_to_ras_cellsize = pu_ras_cellsize / 10.0;
                        double poly_to_ras_cellarea = poly_to_ras_cellsize * poly_to_ras_cellsize;  // square meters

                        // Process each element
                        foreach (var regElement in regElements)
                        {
                            // Process varies based on element layer type
                            LayerType layerType = (LayerType)regElement.LayerType;

                            if (layerType == LayerType.FEATURE)
                            {
                                // Get the element feature layer
                                FeatureLayer regFL = (FeatureLayer)regElement.LayerObject;

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
                                        continue;
                                    }
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

                                // Temp Raste Dataset names
                                string temp_raster_a = PRZC.c_RAS_TEMP_A + regElement.ElementID.ToString();
                                string temp_raster_b = PRZC.c_RAS_TEMP_B + regElement.ElementID.ToString();
                                string temp_table_a = PRZC.c_TABLE_TEMP_A + regElement.ElementID.ToString();

                                // Convert feature layer to raster
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Converting {regElement.ElementName} to raster..."), true, ++val);
                                toolParams = Geoprocessing.MakeValueArray(regFL, oidfield, temp_raster_a, "CELL_CENTER", "NONE", "", "BUILD");
                                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true, snapRaster: pu_rl, cellSize: poly_to_ras_cellsize, outputCoordinateSystem: PU_SR);
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

                                // Reclass the new raster so that all non-null values are now equal to cell area
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Reclassing raster values..."), true, ++val);
                                toolParams = Geoprocessing.MakeValueArray(temp_raster_a, poly_to_ras_cellarea, temp_raster_b, "", "Value IS NOT NULL");
                                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true, snapRaster: pu_rl, cellSize: poly_to_ras_cellsize, outputCoordinateSystem: PU_SR);
                                toolOutput = await PRZH.RunGPTool("Con_sa", toolParams, toolEnvs, toolFlags_GP);
                                if (toolOutput == null)
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error reclassing values.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                                    ProMsgBox.Show($"Error reclassing values.");
                                    return;
                                }
                                else
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Values reclassed."), true, ++val);
                                }

                                // Zonal Statistics as Table
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Zonal Statistics..."), true, ++val);
                                toolParams = Geoprocessing.MakeValueArray(pu_layer, PRZC.c_FLD_RAS_PU_ID, temp_raster_b, temp_table_a, "DATA", "ALL");
                                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true, snapRaster: pu_rl, cellSize: poly_to_ras_cellsize, outputCoordinateSystem: PU_SR);
                                toolOutput = await PRZH.RunGPTool("ZonalStatisticsAsTable_sa", toolParams, toolEnvs, toolFlags_GP);
                                if (toolOutput == null)
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error calculating zonal statistics.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                                    ProMsgBox.Show($"Error calculating zonal statistics.");
                                    return;
                                }
                                else
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Zonal statistics calculated."), true, ++val);
                                }

                                // Retrieve dictionary from zonal stats table:   puid, sum
                                Dictionary<int, double> DICT_PUID_and_value_sum = new Dictionary<int, double>();

                                await QueuedTask.Run(() =>
                                {
                                    var tryget_table = PRZH.GetTable_Project(temp_table_a);
                                    if (!tryget_table.success)
                                    {
                                        throw new Exception($"Unable to retrieve the temp zonal stats table {temp_table_a}");
                                    }

                                    using (Table table = tryget_table.table)
                                    using (RowCursor rowCursor = table.Search())
                                    {
                                        while (rowCursor.MoveNext())
                                        {
                                            using (Row row = rowCursor.Current)
                                            {
                                                int puid = Convert.ToInt32(row[PRZC.c_FLD_TAB_REG_ELEMVAL_PU_ID]);
                                                double sumval = Convert.ToDouble(row[PRZC.c_FLD_ZONALSTATS_SUM]);

                                                if (puid > 0 && sumval > 0)
                                                {
                                                    DICT_PUID_and_value_sum.Add(puid, sumval);
                                                }
                                            }
                                        }
                                    }
                                });

                                // Delete temp objects
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting temp objects..."), true, ++val);
                                string dellist = temp_raster_a + ";" + temp_raster_b + ";" + temp_table_a;
                                toolParams = Geoprocessing.MakeValueArray(dellist);
                                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                                toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP);
                                if (toolOutput == null)
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting temp objects.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                                    ProMsgBox.Show($"Error deleting temp objects.");
                                    return;
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

                                // Retrieve dictionary of puids, cellnumbers
                                var tryget_cellnumbers = await PRZH.GetPUIDsAndCellNumbers();    // this dictionary could have no entries, if the PU dataset has no populated cell_numbers
                                if (!tryget_cellnumbers.success)
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving puid dictionary.", LogMessageType.ERROR), true, ++val);
                                    ProMsgBox.Show($"Error retrieving puid dictionary.");
                                    return;
                                }

                                var DICT_PUID_and_cellnumbers = tryget_cellnumbers.dict;

                                // Create the regional element table
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating the {regElement.ElementTable} table..."), true, ++val);
                                toolParams = Geoprocessing.MakeValueArray(gdbpath, regElement.ElementTable, "", "", "Element " + regElement.ElementID.ToString("D5"));
                                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                                toolOutput = await PRZH.RunGPTool("CreateTable_management", toolParams, toolEnvs, toolFlags_GP);
                                if (toolOutput == null)
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {regElement.ElementTable} table.  GP Tool failed or was cancelled by user.", LogMessageType.ERROR), true, ++val);
                                    ProMsgBox.Show($"Error creating the {regElement.ElementTable} table.");
                                    return;
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
                                    return;
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
                                    return;
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
                                    return;
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
                            else if (layerType == LayerType.RASTER)
                            {


                            }
                            else
                            {
                                // huh?
                            }
                        }
                    }
                    else
                    {
                        // huh?
                    }

                    #endregion
                }
                else
                {
                    ProMsgBox.Show("No goaldir");
                }
                #endregion

                #region PROCESS THE WEIGHTS FOLDER

                if (weightdirexists)
                {

                }

                #endregion

                #region PROCESS THE INCLUDES FOLDER

                if (includedirexists)
                {

                }

                #endregion

                #region PROCESS THE EXCLUDES FOLDER

                if (excludedirexists)
                {

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