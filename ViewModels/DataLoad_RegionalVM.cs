//#define blarg

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
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

#if blarg
                #region DELETE EXISTING TABLES

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags_GPRefresh = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread;
                string toolOutput;

                // Delete the Regional Theme Table if present
                if ((await PRZH.TableExists_Project(PRZC.c_TABLE_REG_THEMES)).exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting the {PRZC.c_TABLE_REG_THEMES} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_TABLE_REG_THEMES);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GPRefresh);
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
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GPRefresh);
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
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the regional element tables ({string.Join(";", tryget_tables.tables)}.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the regional element tables.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Table deleted successfully."), true, ++val);
                    }
                }

                #endregion

                PRZH.CheckForCancellation(token);

                #region DOMAIN MANAGEMENT

                // Check for the existence of the national element/theme domains.  If not found, add them.
                // Check for the existence of a national theme domain (id > name).  If not found, create (if theme table present)

                bool domElemPresenceExists = false;
                bool domElemStatusExists = false;
                bool domElemTypeExists = false;

                // retrieve domain descriptions
                await QueuedTask.Run(() =>
                {
                    var tryget_gdb = PRZH.GetGDB_Project();
                    if (!tryget_gdb.success)
                    {
                        throw new Exception("Error opening the project geodatabase.");
                    }

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    {
                        var domains = geodatabase.GetDomains();

                        foreach (var domain in domains)
                        {
                            using (domain)
                            {
                                if (domain is CodedValueDomain cvd)
                                {
                                    string domname = cvd.GetName();
                                    if (domname == PRZC.c_DOMAIN_ELEMENT_PRESENCE)
                                    {
                                        domElemPresenceExists = true;
                                    }
                                    else if (domname == PRZC.c_DOMAIN_ELEMENT_STATUS)
                                    {
                                        domElemStatusExists = true;
                                    }
                                    else if (domname == PRZC.c_DOMAIN_ELEMENT_TYPE)
                                    {
                                        domElemTypeExists = true;
                                    }
                                }
                            }
                        }
                    }
                });

                // Create the Element Presence domain if it is missing
                if (!domElemPresenceExists)
                {
                    // create domain
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating the {PRZC.c_DOMAIN_ELEMENT_PRESENCE} coded value domain..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_DOMAIN_ELEMENT_PRESENCE, "", "SHORT", "CODED", "DEFAULT", "DEFAULT");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("CreateDomain_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating {PRZC.c_DOMAIN_ELEMENT_PRESENCE} domain.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error creating {PRZC.c_DOMAIN_ELEMENT_PRESENCE} domain.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Domain created."), true, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Add coded value #1
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding coded value 1 to the {PRZC.c_DOMAIN_ELEMENT_PRESENCE} domain..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_DOMAIN_ELEMENT_PRESENCE, (int)NationalElementPresence.Present, NationalElementPresence.Present.ToString());
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddCodedValueToDomain_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding coded value to domain.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error adding coded value to domain.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Coded value added."), true, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Add coded value #2
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding coded value 2 to the {PRZC.c_DOMAIN_ELEMENT_PRESENCE} domain..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_DOMAIN_ELEMENT_PRESENCE, (int)NationalElementPresence.Absent, NationalElementPresence.Absent.ToString());
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddCodedValueToDomain_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding coded value to domain.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error adding coded value to domain.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Coded value added."), true, ++val);
                    }
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_DOMAIN_ELEMENT_PRESENCE} coded value domain found."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Create the Element Status domain if it is missing
                if (!domElemStatusExists)
                {
                    // create domain
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating the {PRZC.c_DOMAIN_ELEMENT_STATUS} coded value domain..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_DOMAIN_ELEMENT_STATUS, "", "SHORT", "CODED", "DEFAULT", "DEFAULT");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("CreateDomain_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating {PRZC.c_DOMAIN_ELEMENT_STATUS} domain.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error creating {PRZC.c_DOMAIN_ELEMENT_STATUS} domain.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Domain created."), true, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Add coded value #1
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding coded value 1 to the {PRZC.c_DOMAIN_ELEMENT_STATUS} domain..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_DOMAIN_ELEMENT_STATUS, (int)NationalElementStatus.Active, NationalElementStatus.Active.ToString());
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddCodedValueToDomain_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding coded value to domain.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error adding coded value to domain.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Coded value added."), true, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Add coded value #2
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding coded value 2 to the {PRZC.c_DOMAIN_ELEMENT_STATUS} domain..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_DOMAIN_ELEMENT_STATUS, (int)NationalElementStatus.Inactive, NationalElementStatus.Inactive.ToString());
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddCodedValueToDomain_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding coded value to domain.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error adding coded value to domain.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Coded value added."), true, ++val);
                    }
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_DOMAIN_ELEMENT_STATUS} coded value domain found."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Create the Element Type domain if it is missing
                if (!domElemTypeExists)
                {
                    // create domain
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating the {PRZC.c_DOMAIN_ELEMENT_TYPE} coded value domain..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_DOMAIN_ELEMENT_TYPE, "", "SHORT", "CODED", "DEFAULT", "DEFAULT");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("CreateDomain_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating {PRZC.c_DOMAIN_ELEMENT_TYPE} domain.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error creating {PRZC.c_DOMAIN_ELEMENT_TYPE} domain.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Domain created."), true, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Add coded value #1
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding coded value 1 to the {PRZC.c_DOMAIN_ELEMENT_TYPE} domain..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_DOMAIN_ELEMENT_TYPE, (int)NationalElementType.Goal, NationalElementType.Goal.ToString());
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddCodedValueToDomain_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding coded value to domain.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error adding coded value to domain.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Coded value added."), true, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Add coded value #2
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding coded value 2 to the {PRZC.c_DOMAIN_ELEMENT_TYPE} domain..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_DOMAIN_ELEMENT_TYPE, (int)NationalElementType.Weight, NationalElementType.Weight.ToString());
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddCodedValueToDomain_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding coded value to domain.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error adding coded value to domain.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Coded value added."), true, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Add coded value #3
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding coded value 1 to the {PRZC.c_DOMAIN_ELEMENT_TYPE} domain..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_DOMAIN_ELEMENT_TYPE, (int)NationalElementType.Include, NationalElementType.Include.ToString());
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddCodedValueToDomain_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding coded value to domain.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error adding coded value to domain.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Coded value added."), true, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Add coded value #4
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding coded value 2 to the {PRZC.c_DOMAIN_ELEMENT_TYPE} domain..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_DOMAIN_ELEMENT_TYPE, (int)NationalElementType.Exclude, NationalElementType.Exclude.ToString());
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddCodedValueToDomain_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding coded value to domain.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error adding coded value to domain.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Coded value added."), true, ++val);
                    }
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_DOMAIN_ELEMENT_TYPE} coded value domain found."), true, ++val);
                }

                // TODO: Create a new Theme domain from the national theme table, or from scratch.

                #endregion

                PRZH.CheckForCancellation(token);

                #region CREATE REGIONAL ELEMENT TABLE

                // Create the table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating the {PRZC.c_TABLE_REG_ELEMENTS} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_TABLE_REG_ELEMENTS, "", "", "Regional Elements");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateTable_management", toolParams, toolEnvs, toolFlags_GPRefresh);
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
                string fldElementType = PRZC.c_FLD_TAB_REGELEMENT_TYPE + $" SHORT 'Element Type' # 0 '{PRZC.c_DOMAIN_ELEMENT_TYPE}';";
                string fldElementStatus = PRZC.c_FLD_TAB_REGELEMENT_STATUS + $" SHORT 'Element Status' # 0 '{PRZC.c_DOMAIN_ELEMENT_STATUS}';";
                //string fldThemeID = PRZC.c_FLD_TAB_REGELEMENT_THEME_ID + $" SHORT 'Theme ID' # 0 '{PRZC.c_DOMAIN_THEME_NAMES}';";
                string fldThemeID = PRZC.c_FLD_TAB_REGELEMENT_THEME_ID + $" SHORT 'Theme ID' # 0 #;";
                string fldElementPresence = PRZC.c_FLD_TAB_REGELEMENT_PRESENCE + $" SHORT 'Element Presence' # 0 '{PRZC.c_DOMAIN_ELEMENT_PRESENCE}'";


                string fields = fldElementID + fldElementName + fldElementType + fldElementStatus + fldThemeID + fldElementPresence;

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to the {PRZC.c_TABLE_REG_ELEMENTS} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_TABLE_REG_ELEMENTS, fields);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags_GPRefresh);
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

#endif
                #region PROCESS THE GOALS FOLDER

                if (goaldirexists)
                {
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
                            LayerCreationParams lcparams = new LayerCreationParams(a);
                            await QueuedTask.Run(async () =>
                            {
                                // Create and add the layer
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

                                // Remove layer if invalid FC or if shapetype is not polygon
                                if (bad_fc)
                                {
                                    GL_GOALS.RemoveLayer(l);
                                }
                                else if (l.ShapeType == esriGeometryType.esriGeometryPolygon)
                                {
                                    fls.Add(l);
                                }
                                else
                                {
                                    GL_GOALS.RemoveLayer(l);
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
                            LayerCreationParams lcparams = new LayerCreationParams(a);
                            await QueuedTask.Run(async () =>
                            {
                                // Create and add the layer
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

                                if (bad_raster)
                                {
                                    GL_GOALS.RemoveLayer(l);
                                }
                                else
                                {
                                    rls.Add(l);
                                }

                                await MapView.Active.RedrawAsync(false);
                            });
                        }

                        lyrx_RLs.Add((o.lyrx_path, rls));
                    }

                    // my two lists now contain Raster Layers and Polygon Feature Layers







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