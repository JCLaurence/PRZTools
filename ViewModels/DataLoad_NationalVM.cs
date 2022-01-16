using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    public class DataLoad_NationalVM : PropertyChangedBase
    {
        public DataLoad_NationalVM()
        {
        }

        #region FIELDS

        private CancellationTokenSource _cts = null;
        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);
        private bool _operation_Cmd_IsEnabled;
        private bool _operationIsUnderway = false;
        private Cursor _proWindowCursor;

        private bool _pu_exists = false;
        private bool _pu_isnat = false;
        private bool _natdb_exists = false;
        private bool _blt_exists = false;

        #region COMMANDS

        private ICommand _cmdLoadNationalData;
        private ICommand _cmdCancel;
        private ICommand _cmdClearLog;

        #endregion

        #region COMPONENT STATUS INDICATORS

        // Planning Unit Dataset
        private string _compStat_Img_PlanningUnits_Path;
        private string _compStat_Txt_PlanningUnits_Label;

        // National DB
        private string _compStat_Img_NatDB_Path;
        private string _compStat_Txt_NatDB_Label;

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

        // National Database
        public string CompStat_Img_NatDB_Path
        {
            get => _compStat_Img_NatDB_Path;
            set => SetProperty(ref _compStat_Img_NatDB_Path, value, () => CompStat_Img_NatDB_Path);
        }

        public string CompStat_Txt_NatDB_Label
        {
            get => _compStat_Txt_NatDB_Label;
            set => SetProperty(ref _compStat_Txt_NatDB_Label, value, () => CompStat_Txt_NatDB_Label);
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

        public ICommand CmdLoadNationalData => _cmdLoadNationalData ?? (_cmdLoadNationalData = new RelayCommand(async () =>
        {
            // Change UI to Underway
            StartOpUI();

            // Start the operation
            using (_cts = new CancellationTokenSource())
            {
                await LoadNationalData(_cts.Token);
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

        private async Task LoadNationalData(CancellationToken token)
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
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to enable editing for this ArcGIS Pro Project.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Unable to enable editing for this ArcGIS Pro Project.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing enabled."), true, ++val);
                    }
                }

                #endregion

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags_GP = GPExecuteToolFlags.GPThread;
                GPExecuteToolFlags toolFlags_GPRefresh = GPExecuteToolFlags.GPThread | GPExecuteToolFlags.RefreshProjectItems;
                string toolOutput;

                // Initialize ProgressBar and Progress Log
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the National Data Loader..."), false, max, ++val);

                // Ensure the Project Geodatabase Exists
                string gdbpath = PRZH.GetPath_ProjectGDB();
                var tryexists_gdb = await PRZH.GDBExists_Project();

                if (!tryexists_gdb.exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Project Geodatabase not found: {gdbpath}", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Project Geodatabase not found at {gdbpath}.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Project Geodatabase found at {gdbpath}."), true, ++val);
                }

                // Ensure the Planning Units dataset exists
                var tryexists_pu = await PRZH.PUExists();
                if (!tryexists_pu.exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Units dataset not found.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Planning Units dataset not found.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Units dataset exists."), true, ++val);
                }

                // Ensure the National db exists
                string natpath = PRZH.GetPath_NatGDB();
                var tryexists_nat = await PRZH.GDBExists_Nat();
                if (!tryexists_nat.exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Valid National Geodatabase not found: {natpath}", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Valid National Geodatabase not found at {natpath}.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"National Geodatabase is OK: {natpath}"), true, ++val);
                }

                // Notify users what will happen if they proceed
                if (ProMsgBox.Show($"If you proceed, any existing National Theme and Element tables in the project geodatabase WILL BE DELETED!!\n\n" +
                                   $"Do you wish to proceed?\n\nChoose wisely...",
                                   "GEODATABASE OVERWRITE WARNING",
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

                #region DELETE EXISTING TABLES

                // Delete the National Theme Table if present
                if ((await PRZH.TableExists_Project(PRZC.c_TABLE_NAT_THEMES)).exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting the {PRZC.c_TABLE_NAT_THEMES} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_TABLE_NAT_THEMES);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_NAT_THEMES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_NAT_THEMES} table.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Table deleted successfully."), true, ++val);
                    }
                }

                PRZH.CheckForCancellation(token);

                // Delete the National Element table if present
                if ((await PRZH.TableExists_Project(PRZC.c_TABLE_NAT_ELEMENTS)).exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting the {PRZC.c_TABLE_NAT_ELEMENTS} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_TABLE_NAT_ELEMENTS);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_NAT_ELEMENTS} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_NAT_ELEMENTS} table.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Table deleted successfully."), true, ++val);
                    }
                }

                PRZH.CheckForCancellation(token);

                // Delete any national element tables
                var tryget_tables = await PRZH.GetNationalElementTables();

                if (!tryget_tables.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving list of national element tables.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving list of national element tables.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{tryget_tables.tables.Count} national element tables found.", LogMessageType.ERROR), true, ++val);
                }

                if (tryget_tables.tables.Count > 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {tryget_tables.tables.Count} national element tables..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(string.Join(";", tryget_tables.tables));
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the national element tables ({string.Join(";", tryget_tables.tables)}.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the national element tables.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"National element tables deleted successfully."), true, ++val);
                    }
                }

                #endregion

                // Process the national tables
                var trynatdb = await ProcessNationalDbTables(tryexists_pu.puLayerType, token);

                if (!trynatdb.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error processing National DB tables.\n{trynatdb.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error processing National DB tables.\n{trynatdb.message}.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"National data loaded successfully."), true, ++val);
                }

                #region WRAP UP

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

                PRZH.CheckForCancellation(token);

                // Final message
                stopwatch.Stop();
                string message = PRZH.GetElapsedTimeMessage(stopwatch.Elapsed);
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Construction completed successfully!"), true, 1, 1);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(message), true, 1, 1);

                ProMsgBox.Show("Construction Completed Successfully!" + Environment.NewLine + Environment.NewLine + message);

                #endregion
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

        private async Task<(bool success, string message)> ProcessNationalDbTables(PlanningUnitLayerType puLayerType, CancellationToken token)
        {
            int val = PM.Current;
            int max = PM.Max;

            try
            {
                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags_GP = GPExecuteToolFlags.GPThread;
                GPExecuteToolFlags toolFlags_GPRefresh = GPExecuteToolFlags.GPThread | GPExecuteToolFlags.RefreshProjectItems;
                string toolOutput;

                #region RETRIEVE AND PREPARE INFO FROM NATIONAL DATABASE

                // COPY THE ELEMENT TABLE
                string gdbpath = PRZH.GetPath_ProjectGDB();
                string natdbpath = PRZH.GetPath_NatGDB();

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Copying the {PRZC.c_TABLE_NAT_ELEMENTS} table..."), true, ++val);
                var q_elem = await PRZH.GetNatDBQualifiedName(PRZC.c_TABLE_NAT_ELEMENTS);
                string inputelempath = Path.Combine(natdbpath, q_elem.qualified_name);
                toolParams = Geoprocessing.MakeValueArray(inputelempath, PRZC.c_TABLE_NAT_ELEMENTS, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("Copy_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying the {PRZC.c_TABLE_NAT_ELEMENTS} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error copying the {PRZC.c_TABLE_NAT_ELEMENTS} table.");
                    return (false, "table copy error.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Table copied successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // INSERT EXTRA FIELDS INTO ELEMENT TABLE
                string fldElemPresence = PRZC.c_FLD_TAB_NATELEMENT_PRESENCE + $" SHORT 'Presence' # {(int)ElementPresence.Absent} '" + PRZC.c_DOMAIN_PRESENCE + "';";
                string flds = fldElemPresence;

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to the copied {PRZC.c_TABLE_NAT_ELEMENTS} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_TABLE_NAT_ELEMENTS, flds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to the copied {PRZC.c_TABLE_NAT_ELEMENTS} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error adding fields to the copied {PRZC.c_TABLE_NAT_ELEMENTS} table.");
                    return (false, "field addition error.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // COPY THE THEMES TABLE
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Copying the {PRZC.c_TABLE_NAT_THEMES} table..."), true, ++val);
                var q_theme = await PRZH.GetNatDBQualifiedName(PRZC.c_TABLE_NAT_THEMES);
                string inputthemepath = Path.Combine(natdbpath, q_theme.qualified_name);
                toolParams = Geoprocessing.MakeValueArray(inputthemepath, PRZC.c_TABLE_NAT_THEMES, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("Copy_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying the {PRZC.c_TABLE_NAT_THEMES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error copying the {PRZC.c_TABLE_NAT_THEMES} table.");
                    return (false, "table copy error.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Table copied successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // INSERT EXTRA FIELDS INTO THEME TABLE
                string fldThemePresence = PRZC.c_FLD_TAB_NATTHEME_PRESENCE + $" SHORT 'Presence' # {(int)ElementPresence.Absent} '" + PRZC.c_DOMAIN_PRESENCE + "';";
                flds = fldThemePresence;

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to the copied {PRZC.c_TABLE_NAT_THEMES} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_TABLE_NAT_THEMES, flds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to the copied {PRZC.c_TABLE_NAT_THEMES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error adding fields to the copied {PRZC.c_TABLE_NAT_THEMES} table.");
                    return (false, "field addition error.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Get the National Themes from the copied national themes table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving national themes..."), true, ++val);
                var theme_outcome = await PRZH.GetNationalThemes();
                if (!theme_outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving national themes.\n{theme_outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving national themes.\n{theme_outcome.message}");
                    return (false, "error retrieving themes.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved {theme_outcome.themes.Count} national themes."), true, ++val);
                }
                List<NatTheme> themes = theme_outcome.themes;

                PRZH.CheckForCancellation(token);

                // Get the National Elements from the copied national elements table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving national elements..."), true, ++val);
                var elem_outcome = await PRZH.GetNationalElements();
                if (!elem_outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving national elements.\n{elem_outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving national elements.\n{elem_outcome.message}");
                    return (false, "error retrieving elements.");
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved {elem_outcome.elements.Count} national elements."), true, ++val);
                }
                List<NatElement> elements = elem_outcome.elements;

                PRZH.CheckForCancellation(token);

                #endregion

                PRZH.CheckForCancellation(token);

                #region RETRIEVE INTERSECTING ELEMENTS

                // Get the cell_numbers and associated puids
                var tryget_cnpuid = await PRZH.GetCellNumbersAndPUIDs();
                if (!tryget_cnpuid.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve cell number and puid dictionary", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Unable to retrieve cell number and puid dictionary");
                    return (false, "error retrieving dictionary.");
                }

                var DICT_CN_and_PUIDs = tryget_cnpuid.dict;
                var puCellNumbers = new HashSet<long>(DICT_CN_and_PUIDs.Keys);

                PRZH.CheckForCancellation(token);

                List<int> elements_with_intersection = new List<int>();
                HashSet<int> themes_with_intersection = new HashSet<int>();

                foreach (var element in elements)
                {
                    // Attempt to retrieve intersection dictionary
                    var tryget_elemintersect = await PRZH.GetElementIntersection(element.ElementID, puCellNumbers);

                    if (!tryget_elemintersect.success)
                    {
                        // Failed, exit
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve values from the {element.ElementTable} table.\n{tryget_elemintersect.message}", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Unable to retrieve values from the {element.ElementTable} table.\n{tryget_elemintersect.message}");
                        return (false, "error getting element intersections.");
                    }
                    else if (tryget_elemintersect.dict.Count == 0)
                    {
                        // No intersection, continue
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{element.ElementTable} table: no intersection."), true, ++val);
                        continue;
                    }
                    else
                    {
                        // Intersection found, deal with it
                        elements_with_intersection.Add(element.ElementID);

                        if (element.ThemeID > 0)
                        {
                            themes_with_intersection.Add(element.ThemeID);
                        }

                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{element.ElementTable} table: intersection with {tryget_elemintersect.dict.Count} planning units."), true, ++val);
                    }
                    var DICT_ElemIntersect = tryget_elemintersect.dict;

                    PRZH.CheckForCancellation(token);

                    // Create the table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating the {element.ElementTable} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, element.ElementTable, "", "", "Element " + element.ElementID.ToString("D5"));
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("CreateTable_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {element.ElementTable} table.  GP Tool failed or was cancelled by user.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error creating the {element.ElementTable} table.");
                        return (false, "error writing element table.");
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Created the {element.ElementTable} table."), true, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Add fields to the table
                    string fldPUID = (puLayerType == PlanningUnitLayerType.FEATURE ? PRZC.c_FLD_FC_PU_ID : PRZC.c_FLD_RAS_PU_ID) + " LONG 'Planning Unit ID' # 0 #;";
                    string fldCellNum = PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER + " LONG 'Cell Number' # 0 #;";
                    string fldCellVal = PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_VALUE + " DOUBLE 'Cell Value' # 0 #;";

                    string fields = fldPUID + fldCellNum + fldCellVal;

                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to the {element.ElementTable} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(element.ElementTable, fields);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to the {element.ElementTable} table.  GP Tool failed or was cancelled by user.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error adding fields to the {element.ElementTable} table.");
                        return (false, "error adding fields.");
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Fields added successfully."), true, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Populate the table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Populating the {element.ElementTable} table.."), true, ++val);
                    await QueuedTask.Run(() =>
                    {
                        var tryget_gdb = PRZH.GetGDB_Project();

                        using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                        using (Table table = geodatabase.OpenDataset<Table>(element.ElementTable))
                        using (InsertCursor insertCursor = table.CreateInsertCursor())
                        using (RowBuffer rowBuffer = table.CreateRowBuffer())
                        {
                            geodatabase.ApplyEdits(() =>
                            {
                                int flusher = 0;

                                foreach (var kvp in DICT_ElemIntersect)
                                {
                                    rowBuffer[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER] = kvp.Key;
                                    rowBuffer[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_VALUE] = kvp.Value;

                                    if (DICT_CN_and_PUIDs.ContainsKey(kvp.Key))
                                    {
                                        rowBuffer[PRZC.c_FLD_TAB_NAT_ELEMVAL_PU_ID] = DICT_CN_and_PUIDs[kvp.Key];
                                    }

                                    insertCursor.Insert(rowBuffer);

                                    flusher++;

                                    if (flusher == 10000)
                                    {
                                        insertCursor.Flush();
                                        flusher = 0;
                                    }
                                }

                                insertCursor.Flush();
                            });
                        }
                    });

                    PRZH.CheckForCancellation(token);

                    // index the cell number field
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing {PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER} field in the {element.ElementTable} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(element.ElementTable, PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER, "ix" + PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER, "", "");
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

                    // index the puid field
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing {PRZC.c_FLD_TAB_NAT_ELEMVAL_PU_ID} field in the {element.ElementTable} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(element.ElementTable, PRZC.c_FLD_TAB_NAT_ELEMVAL_PU_ID, "ix" + PRZC.c_FLD_TAB_NAT_ELEMVAL_PU_ID, "", "");
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
                }

                #endregion

                PRZH.CheckForCancellation(token);

                #region UPDATE THE LOCAL ELEMENT TABLE PRESENCE FIELD

                // Update the table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Updating the {PRZC.c_TABLE_NAT_ELEMENTS} table {PRZC.c_FLD_TAB_NATELEMENT_PRESENCE} field..."), true, ++val);
                await QueuedTask.Run(() =>
                {
                    var tryget_gdb = PRZH.GetGDB_Project();

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    using (Table table = geodatabase.OpenDataset<Table>(PRZC.c_TABLE_NAT_ELEMENTS))
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        geodatabase.ApplyEdits(() =>
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int element_id = Convert.ToInt32(row[PRZC.c_FLD_TAB_NATELEMENT_ELEMENT_ID]);

                                    if (elements_with_intersection.Contains(element_id))
                                    {
                                        row[PRZC.c_FLD_TAB_NATELEMENT_PRESENCE] = (int)ElementPresence.Present;
                                    }
                                    else
                                    {
                                        row[PRZC.c_FLD_TAB_NATELEMENT_PRESENCE] = (int)ElementPresence.Absent;
                                    }

                                    row.Store();
                                }
                            }
                        });
                    }
                });

                #endregion

                PRZH.CheckForCancellation(token);

                #region UPDATE THE LOCAL THEME TABLE PRESENCE FIELD

                // Update the table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Updating the {PRZC.c_TABLE_NAT_THEMES} table {PRZC.c_FLD_TAB_NATTHEME_PRESENCE} field..."), true, ++val);
                await QueuedTask.Run(() =>
                {
                    var tryget_gdb = PRZH.GetGDB_Project();

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    using (Table table = geodatabase.OpenDataset<Table>(PRZC.c_TABLE_NAT_THEMES))
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        geodatabase.ApplyEdits(() =>
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int theme_id = Convert.ToInt32(row[PRZC.c_FLD_TAB_NATTHEME_THEME_ID]);

                                    if (themes_with_intersection.Contains(theme_id))
                                    {
                                        row[PRZC.c_FLD_TAB_NATTHEME_PRESENCE] = (int)ElementPresence.Present;
                                    }
                                    else
                                    {
                                        row[PRZC.c_FLD_TAB_NATTHEME_PRESENCE] = (int)ElementPresence.Absent;
                                    }

                                    row.Store();
                                }
                            }
                        });
                    }
                });

                // we're done here
                return (true, "success");

                #endregion
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
                // Existence of Planning Unit dataset
                var try_exists = await PRZH.PUExists();
                _pu_exists = try_exists.exists;

                // National status of Planning Unit dataset
                var try_isnat = await PRZH.PUIsNational();
                _pu_isnat = try_isnat.is_national;

                // Existence of National Database
                var a = await PRZH.GDBExists_Nat();
                _natdb_exists = a.exists;

                // Planning Unit Dataset type
                string ds_type = "";
                if (try_exists.puLayerType == PlanningUnitLayerType.FEATURE)
                {
                    ds_type = "Feature Class";
                }
                else if (try_exists.puLayerType == PlanningUnitLayerType.RASTER)
                {
                    ds_type = "Raster Dataset";
                }

                // Planning Unit Label
                if (_pu_exists & _pu_isnat)
                {
                    CompStat_Txt_PlanningUnits_Label = $"Planning Units exist ({ds_type}) and are configured for National data.";
                }
                else if (_pu_exists & !_pu_isnat)
                {
                    CompStat_Txt_PlanningUnits_Label = $"Planning Units exist ({ds_type}) but are NOT configured for National data.";
                }
                else
                {
                    CompStat_Txt_PlanningUnits_Label = "Planning Units DO NOT exist. Build them.";
                }

                // Planning Unit Image
                if (_pu_exists & _pu_isnat)
                {
                    CompStat_Img_PlanningUnits_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_Img_PlanningUnits_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
                }

                // National Database Label
                if (_natdb_exists)
                {
                    CompStat_Txt_NatDB_Label = "National Database exists.";
                }
                else
                {
                    CompStat_Txt_NatDB_Label = "National Database does not exist or is invalid.";
                }

                // National Database Image
                if (_natdb_exists)
                {
                    CompStat_Img_NatDB_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_Img_NatDB_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
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
            Operation_Cmd_IsEnabled = _pu_exists & _pu_isnat & _natdb_exists;
            OpStat_Img_Visibility = Visibility.Hidden;
            OpStat_Txt_Label = "Idle";
            _operationIsUnderway = false;
        }


        #endregion


    }
}
