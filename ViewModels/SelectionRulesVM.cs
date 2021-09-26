using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZC = NCC.PRZTools.PRZConstants;
using PRZH = NCC.PRZTools.PRZHelper;
using PRZM = NCC.PRZTools.PRZMethods;

namespace NCC.PRZTools
{
    public class SelectionRulesVM : PropertyChangedBase
    {
        public SelectionRulesVM()
        {
        }

        #region FIELDS

        private ObservableCollection<SelectionRule> _selectionRules = new ObservableCollection<SelectionRule>();
        private SelectionRule _selectedSelectionRule;
        private ObservableCollection<SelectionRuleConflict> _selectionRuleConflicts = new ObservableCollection<SelectionRuleConflict>();
        private SelectionRuleConflict _selectedConflict;
        private List<string> _overrideOptions = new List<string> { SelectionRuleType.INCLUDE.ToString(), SelectionRuleType.EXCLUDE.ToString()};
        private string _selectedOverrideOption;
        private string _defaultThreshold = Properties.Settings.Default.DEFAULT_SELRULE_MIN_THRESHOLD;
        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""
        private bool _selRulesExist = false;
        private bool _selRuleTableExists = false;
        private bool _puSelRuleTableExists = false;

        private ICommand _cmdClearLog;
        private ICommand _cmdConflictDblClick;
        private ICommand _cmdRuleDblClick;
        private ICommand _cmdClearSelRules;
        private ICommand _cmdGenerateSelRules;

        #endregion

        #region PROPERTIES

        public ObservableCollection<SelectionRule> SelectionRules
        {
            get => _selectionRules;
            set => SetProperty(ref _selectionRules, value, () => SelectionRules);
        }

        public SelectionRule SelectedSelectionRule
        {
            get => _selectedSelectionRule; 
            set => SetProperty(ref _selectedSelectionRule, value, () => SelectedSelectionRule);
        }

        public ObservableCollection<SelectionRuleConflict> SelectionRuleConflicts
        {
            get => _selectionRuleConflicts;
            set => SetProperty(ref _selectionRuleConflicts, value, () => SelectionRuleConflicts);
        }

        public SelectionRuleConflict SelectedConflict
        {
            get => _selectedConflict; set => SetProperty(ref _selectedConflict, value, () => SelectedConflict);
        }

        public List<string> OverrideOptions
        {
            get => _overrideOptions; set => SetProperty(ref _overrideOptions, value, () => OverrideOptions);
        }

        public string SelectedOverrideOption
        {
            get => _selectedOverrideOption; set => SetProperty(ref _selectedOverrideOption, value, () => SelectedOverrideOption);
        }

        public string DefaultThreshold
        {
            get => _defaultThreshold;

            set
            {
                SetProperty(ref _defaultThreshold, value, () => DefaultThreshold);
                Properties.Settings.Default.DEFAULT_SELRULE_MIN_THRESHOLD = value;
                Properties.Settings.Default.Save();
            }
        }

        public ProgressManager PM
        {
            get => _pm;

            set => SetProperty(ref _pm, value, () => PM);
        }

        public bool SelRulesExist
        {
            get => _selRulesExist;
            set => SetProperty(ref _selRulesExist, value, () => SelRulesExist);
        }

        public bool SelRuleTableExists
        {
            get => _selRuleTableExists;
            set => SetProperty(ref _selRuleTableExists, value, () => SelRuleTableExists);
        }

        public bool PUSelRuleTableExists
        {
            get => _puSelRuleTableExists;
            set => SetProperty(ref _puSelRuleTableExists, value, () => PUSelRuleTableExists);
        }

        #endregion

        #region COMMANDS

        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true));

        public ICommand CmdConflictDblClick => _cmdConflictDblClick ?? (_cmdConflictDblClick = new RelayCommand(() => ConflictDblClick(), () => true));

        public ICommand CmdRuleDblClick => _cmdRuleDblClick ?? (_cmdRuleDblClick = new RelayCommand(() => RuleDblClick(), () => true));

        public ICommand CmdClearSelRules => _cmdClearSelRules ?? (_cmdClearSelRules = new RelayCommand(() => ClearSelRules(), () => true));

        public ICommand CmdGenerateSelRules => _cmdGenerateSelRules ?? (_cmdGenerateSelRules = new RelayCommand(() => GenerateSelRules(), () => true));

        #endregion

        #region METHODS

        public async Task OnProWinLoaded()
        {
            try
            {
                // Initialize the Progress Bar & Log
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                // Set the Conflict Override value default
                SelectedOverrideOption = SelectionRuleType.INCLUDE.ToString();

                // Determine the presence of 2 tables, and enable/disable the main button accordingly
                SelRuleTableExists = await PRZH.TableExists_SelRules();
                PUSelRuleTableExists = await PRZH.TableExists_PUSelRules();
                SelRulesExist = SelRuleTableExists || PUSelRuleTableExists;

                // Populate the grids
                if (!await PopulateGrid_Rules())
                {
                    ProMsgBox.Show("Error loading the Selection Rules grid");
                }
                if (!await PopulateGrid_Conflicts())
                {
                    ProMsgBox.Show("Error loading the Conflicts grid");
                }

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> GenerateSelRules()
        {
            int val = 0;

            try
            {
                #region INITIALIZATION AND USER INPUT VALIDATION

                // Initialize a few thingies
                Map map = MapView.Active.Map;

                // Initialize ProgressBar and Progress Log
                int max = 50;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Selection Rules Generator..."), false, max, ++val);

                // Validation: Ensure the Project Geodatabase Exists
                string gdbpath = PRZH.GetPath_ProjectGDB();
                if (!await PRZH.ProjectGDBExists())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Project Geodatabase not found: {gdbpath}", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Project Geodatabase not found at this path:" +
                                   Environment.NewLine +
                                   gdbpath +
                                   Environment.NewLine + Environment.NewLine +
                                   "Please specify a valid Project Workspace.", "Validation");

                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Project Geodatabase is OK: {gdbpath}"), true, ++val);
                }

                // Validation: Ensure that the Planning Unit FC exists
                string pufcpath = PRZH.GetPath_FC_PU();
                if (!await PRZH.FCExists_PU())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Class not found in the Project Geodatabase.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Planning Unit Feature Class not present in the project geodatabase.  Have you built it yet?");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Planning Unit Feature Class is OK: {pufcpath}"), true, ++val);
                }

                // Validation: Ensure three required Layers are present
                if (!PRZH.PRZLayerExists(map, PRZLayerNames.STATUS_INCLUDE) || !PRZH.PRZLayerExists(map, PRZLayerNames.STATUS_EXCLUDE) || !PRZH.PRZLayerExists(map, PRZLayerNames.PU))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Layers are missing.  Please reload PRZ layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("PRZ Layers are missing.  Please reload the PRZ Layers and try again.", "Validation");
                    return false;
                }

                // Validation: Ensure that at least one Feature Layer is present in either of the two group layers
                var include_layers = PRZH.GetPRZLayers(map, PRZLayerNames.STATUS_INCLUDE, PRZLayerRetrievalType.BOTH);
                var exclude_layers = PRZH.GetPRZLayers(map, PRZLayerNames.STATUS_EXCLUDE, PRZLayerRetrievalType.BOTH);

                if (include_layers == null || exclude_layers == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Unable to retrieve contents of {PRZC.c_GROUPLAYER_SELRULES_INCLUDE} or {PRZC.c_GROUPLAYER_SELRULES_EXCLUDE} Group Layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Unable to retrieve contents of {PRZC.c_GROUPLAYER_SELRULES_INCLUDE} or {PRZC.c_GROUPLAYER_SELRULES_EXCLUDE} Group Layers.", "Validation");
                    return false;
                }

                if (include_layers.Count == 0 && exclude_layers.Count == 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> No Raster or Feature Layers found within {PRZC.c_GROUPLAYER_SELRULES_INCLUDE} or {PRZC.c_GROUPLAYER_SELRULES_EXCLUDE} group layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"There must be at least one Raster or Feature Layer within either the {PRZC.c_GROUPLAYER_SELRULES_INCLUDE} or {PRZC.c_GROUPLAYER_SELRULES_EXCLUDE} group layers.", "Validation");
                    return false;
                }

                // Validation: Ensure the Default Minimum Threshold is valid
                string threshold_text = string.IsNullOrEmpty(DefaultThreshold) ? "0" : ((DefaultThreshold.Trim() == "") ? "0" : DefaultThreshold.Trim());

                if (!double.TryParse(threshold_text, out double threshold_double))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Threshold value", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Threshold value.  Value must be a number between 0 and 100 (inclusive)", "Validation");
                    return false;
                }
                else if (threshold_double < 0 | threshold_double > 100)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Threshold value", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Threshold value.  Value must be a number between 0 and 100 (inclusive)", "Validation");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Default Threshold = {threshold_text}"), true, ++val);
                }

                // Validation: Prompt User for permission to proceed
                if (ProMsgBox.Show($"If you proceed, the {PRZC.c_TABLE_SELRULES} and {PRZC.c_TABLE_PUSELRULES} tables will be overwritten if they exist in the Project Geodatabase." +
                   Environment.NewLine + Environment.NewLine +
                   $"Additionally, the contents of the {PRZC.c_FLD_FC_PU_EFFECTIVE_RULE} field in the {PRZC.c_FC_PLANNING_UNITS} Feature Class will be updated." +
                   Environment.NewLine + Environment.NewLine +
                   "Do you wish to proceed?" +
                   Environment.NewLine + Environment.NewLine +
                   "Choose wisely...",
                   "TABLE OVERWRITE WARNING",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out."), true, ++val);
                    return false;
                }

                #endregion

                #region COMPILE LIST OF SELECTION RULES

                // Retrieve the Selection Rules
                var rule_getter = await GetRulesFromLayers();

                if (!rule_getter.success || rule_getter.rules == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error retrieving Selection Rules from Layers", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Unable to construct the Selection Rules listing");
                    return false;
                }
                else if (rule_getter.rules.Count == 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("No valid Selection Rules found", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("No valid Selection Rules found", "Validation");
                    return false;
                }

                List<SelectionRule> LIST_Rules = rule_getter.rules;

                #endregion

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                #region BUILD THE SELECTION RULES TABLE

                string srpath = PRZH.GetPath_Table_SelRules();

                // Delete the existing SelRules table, if it exists
                if (await PRZH.TableExists_SelRules())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {PRZC.c_TABLE_SELRULES} Table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(srpath, "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_SELRULES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleted the existing {PRZC.c_TABLE_SELRULES} Table..."), true, ++val);
                    }
                }

                // Create the table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating the {PRZC.c_TABLE_SELRULES} Table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_TABLE_SELRULES, "", "", "Selection Rules");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CreateTable_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {PRZC.c_TABLE_SELRULES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_SELRULES} table created successfully..."), true, ++val);
                }

                // Add fields to the table
                string fldSRID = PRZC.c_FLD_TAB_SELRULES_ID + " LONG 'Rule ID' # # #;";
                string fldSRName = PRZC.c_FLD_TAB_SELRULES_NAME + " TEXT 'Rule Name' 255 # #;";
                string fldSRRuleType = PRZC.c_FLD_TAB_SELRULES_RULETYPE + " TEXT 'Rule Type' 50 # #;";
                string fldSRLayerType = PRZC.c_FLD_TAB_SELRULES_LAYERTYPE + " TEXT 'Source Layer Type' 50 # #;";
                string fldSRLayerJson = PRZC.c_FLD_TAB_SELRULES_LAYERJSON + " TEXT 'Source Layer JSON' 100000 # #;";
                string fldSRMinThreshold = PRZC.c_FLD_TAB_SELRULES_MIN_THRESHOLD + " LONG 'Min Threshold (%)' # 0 #;";
                string fldSREnabled = PRZC.c_FLD_TAB_SELRULES_ENABLED + " LONG 'Enabled' # 1 #;";
                string fldSRHidden = PRZC.c_FLD_TAB_SELRULES_HIDDEN + " LONG 'Hidden' # 0 #;";
                string fldSRArea_m2 = PRZC.c_FLD_TAB_SELRULES_AREA_M + " DOUBLE 'Total Area (m2)' # 0, #;";
                string fldSRArea_ac = PRZC.c_FLD_TAB_SELRULES_AREA_AC + " DOUBLE 'Total Area (ac)' # 0, #;";
                string fldSRArea_ha = PRZC.c_FLD_TAB_SELRULES_AREA_HA + " DOUBLE 'Total Area (ha)' # 0, #;";
                string fldSRArea_km2 = PRZC.c_FLD_TAB_SELRULES_AREA_KM + " DOUBLE 'Total Area (km2)' # 0, #;";
                string fldSRPUCount = PRZC.c_FLD_TAB_SELRULES_PUCOUNT + " LONG 'Planning Unit Count' # 0 #;";

                string flds = fldSRID +
                              fldSRName +
                              fldSRRuleType +
                              fldSRLayerType +
                              fldSRLayerJson +
                              fldSRMinThreshold +
                              fldSREnabled +
                              fldSRHidden +
                              fldSRArea_m2 +
                              fldSRArea_ac +
                              fldSRArea_ha +
                              fldSRArea_km2 +
                              fldSRPUCount;

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to {PRZC.c_TABLE_SELRULES} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(srpath, flds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to {PRZC.c_TABLE_SELRULES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_SELRULES} table fields added successfully..."), true, ++val);
                }

                #endregion

                #region POPULATE THE SELECTION RULES TABLE

                // Populate Table from LIST
                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (Table table = await PRZH.GetTable_SelRules())
                        using (InsertCursor insertCursor = table.CreateInsertCursor())
                        using (RowBuffer rowBuffer = table.CreateRowBuffer())
                        {
                            // Iterate through each selection rule
                            foreach (SelectionRule SR in LIST_Rules)
                            {
                                rowBuffer[PRZC.c_FLD_TAB_SELRULES_ID] = SR.sr_id;
                                rowBuffer[PRZC.c_FLD_TAB_SELRULES_NAME] = SR.sr_name;
                                rowBuffer[PRZC.c_FLD_TAB_SELRULES_RULETYPE] = SR.sr_rule_type.ToString();
                                rowBuffer[PRZC.c_FLD_TAB_SELRULES_LAYERTYPE] = SR.sr_layer_type.ToString();
                                rowBuffer[PRZC.c_FLD_TAB_SELRULES_LAYERJSON] = SR.sr_layer_json;
                                rowBuffer[PRZC.c_FLD_TAB_SELRULES_MIN_THRESHOLD] = SR.sr_min_threshold;
                                rowBuffer[PRZC.c_FLD_TAB_SELRULES_AREA_M] = 0;
                                rowBuffer[PRZC.c_FLD_TAB_SELRULES_AREA_AC] = 0;
                                rowBuffer[PRZC.c_FLD_TAB_SELRULES_AREA_HA] = 0;
                                rowBuffer[PRZC.c_FLD_TAB_SELRULES_AREA_KM] = 0;
                                rowBuffer[PRZC.c_FLD_TAB_SELRULES_ENABLED] = 1;
                                rowBuffer[PRZC.c_FLD_TAB_SELRULES_HIDDEN] = 0;

                                // Insert the row
                                insertCursor.Insert(rowBuffer);
                                insertCursor.Flush();
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error populating the {PRZC.c_TABLE_SELRULES} table.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error populating the {PRZC.c_TABLE_SELRULES} table.");
                    return false;

                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_SELRULES} table populated successfully."), true, ++val);
                }

                #endregion

                #region BUILD THE PU + SELRULES TABLE - PART I

                string pusrpath = PRZH.GetPath_Table_PUSelRules();

                // Delete the existing PUSR table, if it exists
                if (await PRZH.TableExists_PUSelRules())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {PRZC.c_TABLE_PUSELRULES} Table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(pusrpath, "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_PUSELRULES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_PUSELRULES} table.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Table deleted successfully."), true, ++val);
                    }
                }

                // Copy PU FC rows into a new PUSR table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Copying Planning Unit FC Attributes..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pufcpath, pusrpath, "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CopyRows_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying planning unit attributes to {PRZC.c_TABLE_PUSELRULES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error executing the CopyRows tool.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Attributes copied successfully."), true, ++val);
                }

                // Delete all fields but OID and PUID from PUVCF table
                List<string> LIST_DeleteFields = new List<string>();

                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (Table tab = await PRZH.GetTable_PUSelRules())
                        {
                            if (tab == null)
                            {
                                ProMsgBox.Show($"Unable to retrieve the {PRZC.c_TABLE_PUSELRULES} table");
                                return false;
                            }

                            using (TableDefinition tDef = tab.GetDefinition())
                            {
                                List<Field> fields = tDef.GetFields().Where(f => f.Name != tDef.GetObjectIDField() && f.Name != PRZC.c_FLD_TAB_PUSELRULES_ID).ToList();

                                foreach (Field field in fields)
                                {
                                    LIST_DeleteFields.Add(field.Name);
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving fields to delete.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving fields to delete.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Field list retrieved."), true, ++val);
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Removing unnecessary fields from the {PRZC.c_TABLE_PUSELRULES} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pusrpath, LIST_DeleteFields);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting fields from {PRZC.c_TABLE_PUSELRULES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error deleting fields.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Fields deleted successfully."), true, ++val);
                }

                // Now index the PUID field
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing {PRZC.c_FLD_TAB_PUSELRULES_ID} field..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pusrpath, PRZC.c_FLD_TAB_PUSELRULES_ID, "ix" + PRZC.c_FLD_TAB_PUSELRULES_ID, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error indexing field.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field indexed successfully."), true, ++val);
                }

                // Add 2 additional fields 
                string fldEffectiveRule = PRZC.c_FLD_TAB_PUSELRULES_EFFECTIVE_RULE + " TEXT 'Effective Rule' 50 # #;";
                string fldConflict = PRZC.c_FLD_TAB_PUSELRULES_CONFLICT + " LONG 'Rule Conflict Exists' # 0 #;";

                flds = fldEffectiveRule + fldConflict;

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding extra fields to {PRZC.c_TABLE_PUSELRULES} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pusrpath, flds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding extra fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error adding lovely fields.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Extra fields added successfully."), true, ++val);
                }

                #endregion

                #region BUILD THE PU + SELRULES TABLE - PART II

                // Cycle through each Selection Rule
                foreach (SelectionRule SR in LIST_Rules)
                {
                    // Get the Selection Rule ID
                    int srid = SR.sr_id;

                    // Get the Selection Rule Name (limited to 75 characters)
                    string rule_name = (SR.sr_name.Length > 75) ? SR.sr_name.Substring(0, 75) : SR.sr_name;

                    // Get the Selection Rule Type
                    SelectionRuleType rule_type = SR.sr_rule_type;
                    string prefix = "";
                    string alias_prefix = "";

                    if (rule_type == SelectionRuleType.INCLUDE)
                    {
                        prefix = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_INCLUDE;
                        alias_prefix = "Include ";
                    }
                    else if (rule_type == SelectionRuleType.EXCLUDE)
                    {
                        prefix = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_EXCLUDE;
                        alias_prefix = "Exclude ";
                    }

                    // Add 5 Fields: id, Name, Area, Coverage, and Status

                    // ID field
                    string fId = prefix + srid.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_SELRULEID;
                    string fIdAlias = alias_prefix + srid.ToString() + " ID";
                    string f1 = fId + " LONG '" + fIdAlias + "' # 0 #;";

                    // Name field 
                    string fName = prefix + srid.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_NAME;
                    string fNameAlias = alias_prefix + srid.ToString() + " Name";
                    string f2 = fName + " TEXT '" + fNameAlias + "' 200 # #;";

                    // Area field
                    string fArea = prefix + srid.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_AREA;
                    string fAreaAlias = alias_prefix + srid.ToString() + " Area (m2)";
                    string f3 = fArea + " DOUBLE '" + fAreaAlias + "' # 0 #;";

                    // Coverage field
                    string fCov = prefix + srid.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_COVERAGE;
                    string fCovAlias = alias_prefix + srid.ToString() + " Coverage (%)";
                    string f4 = fCov + " DOUBLE '" + fCovAlias + "' # 0 #;";

                    // State field
                    string fStat = prefix + srid.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_STATE;
                    string fStatAlias = alias_prefix + srid.ToString() + " State";
                    string f5 = fStat + " LONG '" + fStatAlias + "' # 0 #;";

                    flds = f1 + f2 + f3 + f4 + f5;

                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields for selection rule {srid}..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(pusrpath, flds);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Error adding fields...");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully..."), true, ++val);
                    }

                    // Populate the new fields
                    if (!await QueuedTask.Run(async () =>
                    {
                        try
                        {
                            using (Table table = await PRZH.GetTable_PUSelRules())
                            using (RowCursor rowCursor = table.Search(null, false))
                            {
                                while (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        row[fId] = srid;
                                        row[fName] = rule_name;
                                        row[fArea] = 0;
                                        row[fCov] = 0;
                                        row[fStat] = 0;

                                        row.Store();
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
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error updating records in the {PRZC.c_TABLE_PUSELRULES} table...", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error updating records in the {PRZC.c_TABLE_PUSELRULES} table.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_PUSELRULES} table updated successfully."), true, ++val);
                    }
                }

                #endregion

                #region INTERSECT SELRULE LAYERS WITH PLANNING UNITS

                if (!await IntersectRuleLayers(LIST_Rules, val))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error intersecting the Selection Rule layers.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error intersecting the Selection Rule layers.", "");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Selection Rule layers intersected successfully."), true, ++val);
                }

                #endregion

                #region UPDATE EFFECTIVE RULE AND CONFLICT FIELDS IN PUSELRULES TABLE

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Determining conflicts and effective selection rules"), true, ++val);

                Dictionary<int, (string rule, int conflict)> DICT_PUID_and_rule_conflict = new Dictionary<int, (string rule, int conflict)>();

                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (Table table = await PRZH.GetTable_PUSelRules())
                        using (TableDefinition tDef = table.GetDefinition())
                        {
                            // Get list of INCLUDE layer State fields
                            List<Field> INStateFields = tDef.GetFields().Where(f => f.Name.StartsWith(PRZC.c_FLD_TAB_PUSELRULES_PREFIX_INCLUDE) && f.Name.EndsWith(PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_STATE)).ToList();

                            // Get list of EXCLUDE layer State fields
                            List<Field> EXStateFields = tDef.GetFields().Where(f => f.Name.StartsWith(PRZC.c_FLD_TAB_PUSELRULES_PREFIX_EXCLUDE) && f.Name.EndsWith(PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_STATE)).ToList();

                            using (RowCursor rowCursor = table.Search(null, false))
                            {
                                while (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        int puid = Convert.ToInt32(row[PRZC.c_FLD_TAB_PUSELRULES_ID]);

                                        bool hasIN = false;
                                        bool hasEX = false;

                                        // Determine if there are any INCLUDE State fields having a value of 1
                                        foreach (Field fld in INStateFields)
                                        {
                                            int test = Convert.ToInt32(row[fld.Name]);

                                            if (test == 1)
                                            {
                                                hasIN = true;
                                            }
                                        }

                                        // Determine if there are any EXCLUDE area fields having values > 0 for this PU ID
                                        foreach (Field fld in EXStateFields)
                                        {
                                            int test = Convert.ToInt32(row[fld.Name]);

                                            if (test == 1)
                                            {
                                                hasEX = true;
                                            }
                                        }

                                        // Update the effective rule and the conflict information

                                        int conflict = 0;
                                        string effective_rule = "";

                                        if (hasIN && hasEX) // Planning Unit has both Include(s) and Exclude(s), = a conflict
                                        {
                                            conflict = 1;
                                            effective_rule = SelectedOverrideOption;    // user-specified 'tie-breaker' in the event of a conflict
                                        }
                                        else if (hasIN)     // Planning Unit has only Include(s)
                                        {
                                            conflict = 0;
                                            effective_rule = SelectionRuleType.INCLUDE.ToString();
                                        }
                                        else if (hasEX)     // Planning Unit has only Exclude(s)
                                        {
                                            conflict = 0;
                                            effective_rule = SelectionRuleType.EXCLUDE.ToString();
                                        }
                                        else                // Planning Unit has neither Includes nor Excludes
                                        {
                                            conflict = 0;
                                            effective_rule = "";
                                        }

                                        // update the row
                                        row[PRZC.c_FLD_TAB_PUSELRULES_CONFLICT] = conflict;
                                        row[PRZC.c_FLD_TAB_PUSELRULES_EFFECTIVE_RULE] = effective_rule;
                                        row.Store();

                                        // Add to dictionary
                                        DICT_PUID_and_rule_conflict.Add(puid, (effective_rule, conflict));
                                    }
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error updating the {PRZC.c_FLD_TAB_PUSELRULES_EFFECTIVE_RULE} and {PRZC.c_FLD_TAB_PUSELRULES_CONFLICT} fields in the {PRZC.c_TABLE_PUSELRULES} table...", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error updating the {PRZC.c_FLD_TAB_PUSELRULES_EFFECTIVE_RULE} and {PRZC.c_FLD_TAB_PUSELRULES_CONFLICT} fields in the {PRZC.c_TABLE_PUSELRULES} table...");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_FLD_TAB_PUSELRULES_EFFECTIVE_RULE} and {PRZC.c_FLD_TAB_PUSELRULES_CONFLICT} fields updated."), true, ++val);
                }

                #endregion

                #region UPDATE EFFECTIVE RULE AND CONFLICT FIELDS IN PLANNING UNIT FC

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Updating {PRZC.c_FLD_FC_PU_EFFECTIVE_RULE} and {PRZC.c_FLD_FC_PU_CONFLICT} fields in the {PRZC.c_FC_PLANNING_UNITS} feature class..."), true, ++val);

                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (Table table = await PRZH.GetFC_PU())
                        using (RowCursor rowCursor = table.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int puid = Convert.ToInt32(row[PRZC.c_FLD_FC_PU_ID]);
                                    int conflict = 0;
                                    object obj_rule = DBNull.Value;

                                    if (DICT_PUID_and_rule_conflict.ContainsKey(puid))
                                    {
                                        conflict = DICT_PUID_and_rule_conflict[puid].conflict;

                                        if (DICT_PUID_and_rule_conflict[puid].rule != "")
                                        {
                                            obj_rule = DICT_PUID_and_rule_conflict[puid].rule;
                                        }
                                    }

                                    row[PRZC.c_FLD_FC_PU_CONFLICT] = conflict;
                                    row[PRZC.c_FLD_FC_PU_EFFECTIVE_RULE] = obj_rule;

                                    row.Store();
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error updating the {PRZC.c_FLD_FC_PU_EFFECTIVE_RULE} and {PRZC.c_FLD_FC_PU_CONFLICT} fields in the {PRZC.c_FC_PLANNING_UNITS} feature class...", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error updating the {PRZC.c_FLD_FC_PU_EFFECTIVE_RULE} and {PRZC.c_FLD_FC_PU_CONFLICT} fields in the {PRZC.c_FC_PLANNING_UNITS} feature class...");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_FLD_FC_PU_EFFECTIVE_RULE} and {PRZC.c_FLD_FC_PU_CONFLICT} fields updated."), true, ++val);
                }

                #endregion

                #region UPDATE SUMMARY FIELDS IN SELRULES TABLE

                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (Table table = await PRZH.GetTable_SelRules())
                        using (RowCursor rowCursor = table.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int sr_id = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_ID]);
                                    
                                    string AreaField = "";
                                    string StateField = "";

                                    string sr_rule_type = row[PRZC.c_FLD_TAB_SELRULES_RULETYPE] == null | row[PRZC.c_FLD_TAB_SELRULES_RULETYPE] == DBNull.Value ? "" : row[PRZC.c_FLD_TAB_SELRULES_RULETYPE].ToString();
                                    if (sr_rule_type == SelectionRuleType.INCLUDE.ToString())
                                    {
                                        AreaField = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_INCLUDE + sr_id.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_AREA;
                                        StateField = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_INCLUDE + sr_id.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_STATE;
                                    }
                                    else if (sr_rule_type == SelectionRuleType.EXCLUDE.ToString())
                                    {
                                        AreaField = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_EXCLUDE + sr_id.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_AREA;
                                        StateField = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_EXCLUDE + sr_id.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_STATE;
                                    }
                                    else
                                    {
                                        return false;
                                    }

                                    int pucount = 0;
                                    double area_m2 = 0;

                                    QueryFilter QF = new QueryFilter();
                                    QF.SubFields = AreaField;
                                    QF.WhereClause = StateField + @" = 1";

                                    using (Table table2 = await PRZH.GetTable_PUSelRules())
                                    using (RowCursor rowCursor2 = table2.Search(QF, false))
                                    {
                                        while (rowCursor2.MoveNext())
                                        {
                                            using (Row row2 = rowCursor2.Current)
                                            {
                                                double d = Convert.ToDouble(row2[AreaField]);

                                                area_m2 += d;
                                                pucount++;
                                            }
                                        }
                                    }

                                    // Update the SelRules Table with this summary info

                                    double m2_round = Math.Round(area_m2, 2, MidpointRounding.AwayFromZero);
                                    double ac_round = Math.Round(area_m2 * PRZC.c_CONVERT_M2_TO_AC, 2, MidpointRounding.AwayFromZero);
                                    double ha_round = Math.Round(area_m2 * PRZC.c_CONVERT_M2_TO_HA, 2, MidpointRounding.AwayFromZero);
                                    double km2_round = Math.Round(area_m2 * PRZC.c_CONVERT_M2_TO_KM2, 2, MidpointRounding.AwayFromZero);

                                    row[PRZC.c_FLD_TAB_SELRULES_AREA_M] = m2_round;
                                    row[PRZC.c_FLD_TAB_SELRULES_AREA_AC] = ac_round;
                                    row[PRZC.c_FLD_TAB_SELRULES_AREA_HA] = ha_round;
                                    row[PRZC.c_FLD_TAB_SELRULES_AREA_KM] = km2_round;
                                    row[PRZC.c_FLD_TAB_SELRULES_PUCOUNT] = pucount;

                                    row.Store();
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error updating area fields in the {PRZC.c_TABLE_SELRULES} table.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error updating area fields in the {PRZC.c_TABLE_SELRULES} table.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_SELRULES} table summary fields updated."), true, ++val);
                }

                #endregion

                #region WRAP THINGS UP

                // Populate the grids
                if (!await PopulateGrid_Rules())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error populating the Selection Rules grid", LogMessageType.ERROR), true, ++val);
                }
                if (!await PopulateGrid_Conflicts())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error populating the Conflicts grid", LogMessageType.ERROR), true, ++val);
                }

                // Compact the Geodatabase
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacting the geodatabase..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath);
                toolOutput = await PRZH.RunGPTool("Compact_management", toolParams, null, GPExecuteToolFlags.None);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error compacting the geodatabase. GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error compacting the geodatabase...");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacted successfully..."), true, ++val);
                }

                // Wrap things up
                stopwatch.Stop();
                string message = PRZH.GetElapsedTimeMessage(stopwatch.Elapsed);
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Construction completed successfully!"), true, 1, 1);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(message), true, 1, 1);

                ProMsgBox.Show("Success!" + Environment.NewLine + Environment.NewLine + message);

                return true;

                #endregion
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                return false;
            }
        }

        private async Task<bool> PopulateGrid_Rules()
        {
            try
            {
                // Clear the contents
                SelectionRules = new ObservableCollection<SelectionRule>(); // triggers the xaml refresh

                // If Selection Rules table doesn't exist, exit
                if (!await PRZH.TableExists_SelRules())
                {
                    return true;
                }

                List<SelectionRule> rules = new List<SelectionRule>();

                await QueuedTask.Run(async () =>
                {

                    using (Table table = await PRZH.GetTable_SelRules())
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                SelectionRule SR = new SelectionRule();

                                SR.sr_id = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_ID]);
                                SR.sr_name = row[PRZC.c_FLD_TAB_SELRULES_NAME].ToString() ?? "";

                                string rt = row[PRZC.c_FLD_TAB_SELRULES_RULETYPE].ToString() ?? "";
                                if (rt == SelectionRuleType.INCLUDE.ToString())
                                {
                                    SR.sr_rule_type = SelectionRuleType.INCLUDE;
                                }
                                else if (rt == SelectionRuleType.EXCLUDE.ToString())
                                {
                                    SR.sr_rule_type = SelectionRuleType.EXCLUDE;
                                }

                                string lt = row[PRZC.c_FLD_TAB_SELRULES_LAYERTYPE].ToString() ?? "";
                                if (lt == SelectionRuleLayerType.RASTER.ToString())
                                {
                                    SR.sr_layer_type = SelectionRuleLayerType.RASTER;
                                }
                                else if (lt == SelectionRuleLayerType.VECTOR.ToString())
                                {
                                    SR.sr_layer_type = SelectionRuleLayerType.VECTOR;
                                }

                                SR.sr_min_threshold = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_MIN_THRESHOLD]);
                                SR.sr_pucount = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_PUCOUNT]);
                                SR.sr_enabled = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_ENABLED]);
                                SR.sr_hidden = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_HIDDEN]);
                                SR.sr_area_m2 = Convert.ToDouble(row[PRZC.c_FLD_TAB_SELRULES_AREA_M]);
                                SR.sr_area_ac = Convert.ToDouble(row[PRZC.c_FLD_TAB_SELRULES_AREA_AC]);
                                SR.sr_area_ha = Convert.ToDouble(row[PRZC.c_FLD_TAB_SELRULES_AREA_HA]);
                                SR.sr_area_km2 = Convert.ToDouble(row[PRZC.c_FLD_TAB_SELRULES_AREA_KM]);

                                rules.Add(SR);
                            }
                        }

                        int c = table.GetCount();
                    }
                });

                // Sort them
                rules.Sort((x, y) => x.sr_id.CompareTo(y.sr_id));

                SelectionRules = new ObservableCollection<SelectionRule>(rules);

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private async Task<bool> PopulateGrid_Conflicts()
        {
            try
            {
                // Clear the contents
                SelectionRuleConflicts = new ObservableCollection<SelectionRuleConflict>(); // triggers the xaml refresh

                // If either table doesn't exist, exit
                if (!await PRZH.TableExists_SelRules() | !await PRZH.TableExists_PUSelRules())
                {
                    return true;
                }

                // Retrieve Selection Rule info
                Dictionary<int, (string name, string statefield)> DICT_Includes = new Dictionary<int, (string name, string statefield)>();
                Dictionary<int, (string name, string statefield)> DICT_Excludes = new Dictionary<int, (string name, string statefield)>();

                if (!await QueuedTask.Run(async() =>
                {
                    try
                    {
                        using (Table table = await PRZH.GetTable_SelRules())
                        using (TableDefinition tDef = table.GetDefinition())
                        using (RowCursor rowCursor = table.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int sr_id = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_ID]);
                                    string sr_name = row[PRZC.c_FLD_TAB_SELRULES_NAME].ToString() ?? "";
                                    string sr_type = row[PRZC.c_FLD_TAB_SELRULES_RULETYPE].ToString() ?? "";

                                    string StateField = "";

                                    if (sr_type == SelectionRuleType.INCLUDE.ToString())
                                    {
                                        StateField = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_INCLUDE + sr_id.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_STATE;
                                        DICT_Includes.Add(sr_id, (sr_name, StateField));
                                    }
                                    else if (sr_type == SelectionRuleType.EXCLUDE.ToString())
                                    {
                                        StateField = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_EXCLUDE + sr_id.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_STATE;
                                        DICT_Excludes.Add(sr_id, (sr_name, StateField));
                                    }
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
                    ProMsgBox.Show("Error retrieving Selection Rule details...");
                    return false;
                }

                // Exit if either dictionary is empty (can't have a conflict)
                if (DICT_Includes.Count == 0 | DICT_Excludes.Count == 0)
                {
                    return true;
                }

                // For each combination of Include & Exclude rules, search the PUSelRules table for records having STATE = 1
                // If found, add a new Conflict object to the list

                List<SelectionRuleConflict> conflicts = new List<SelectionRuleConflict>();

                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        int conflict_id = 1;

                        foreach (KeyValuePair<int, (string name, string statefield)> include in DICT_Includes)
                        {
                            foreach (KeyValuePair<int, (string name, string statefield)> exclude in DICT_Excludes)
                            {
                                QueryFilter QF = new QueryFilter
                                {
                                    SubFields = include.Value.statefield + "," + exclude.Value.statefield,
                                    WhereClause = include.Value.statefield + @" = 1 And " + exclude.Value.statefield + @" = 1"
                                };

                                int rowcount = 0;

                                using (Table table = await PRZH.GetTable_PUSelRules())
                                {
                                    rowcount = table.GetCount(QF);
                                }

                                if (rowcount > 0)
                                {
                                    // a conflict exists between include and exclude

                                    SelectionRuleConflict con = new SelectionRuleConflict();

                                    con.conflict_id = conflict_id++;
                                    con.pu_count = rowcount;
                                    con.include_rule_id = include.Key;
                                    con.include_rule_name = include.Value.name;
                                    con.include_rule_statefield = include.Value.statefield;
                                    con.exclude_rule_id = exclude.Key;
                                    con.exclude_rule_name = exclude.Value.name;
                                    con.exclude_rule_statefield = exclude.Value.statefield;

                                    conflicts.Add(con);
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
                    ProMsgBox.Show("Error retrieving Selection Rule details...");
                    return false;
                }

                if (conflicts.Count > 0)
                {
                    // Sort the conflicts
                    conflicts.Sort((x, y) => x.conflict_id.CompareTo(y.conflict_id));

                    // Update the collection (causes refresh)
                    SelectionRuleConflicts = new ObservableCollection<SelectionRuleConflict>(conflicts);
                }

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private async Task<(bool success, List<SelectionRule> rules)> GetRulesFromLayers()
        {
            (bool, List<SelectionRule>) fail = (false, null);

            try
            {

                #region INITIALIZE STUFF

                // Set some initial variables
                Map map = MapView.Active.Map;
                int srid = 1;
                int default_threshold = int.Parse(Properties.Settings.Default.DEFAULT_SELRULE_MIN_THRESHOLD);

                // Set up the master list of selection rules
                List<SelectionRule> rules = new List<SelectionRule>();

                // Get the Layer Lists
                var include_layers = PRZH.GetPRZLayers(map, PRZLayerNames.STATUS_INCLUDE, PRZLayerRetrievalType.BOTH);
                var exclude_layers = PRZH.GetPRZLayers(map, PRZLayerNames.STATUS_EXCLUDE, PRZLayerRetrievalType.BOTH);

                // Exit if errors obtaining the lists
                if (include_layers == null)
                {
                    ProMsgBox.Show("Unable to retrieve 'INCLUDE' layers...");
                    return fail;
                }

                if (exclude_layers == null)
                {
                    ProMsgBox.Show("Unable to retrieve 'EXCLUDE' layers...");
                    return fail;
                }

                // Exit if no layers actually returned
                if (include_layers.Count == 0 && exclude_layers.Count == 0)
                {
                    ProMsgBox.Show("No 'INCLUDE' or 'EXCLUDE' layers found...");
                    return fail;
                }

                #endregion

                // Process the INCLUDE layers first
                for (int i = 0; i < include_layers.Count; i++)
                {
                    Layer L = include_layers[i];

                    #region EXTRACT MINIMUM THRESHOLD FROM LAYER NAME

                    string layer_name = "";

                    // Inspect the Layer Name for a Minimum Threshold number
                    (bool ThresholdFound, int layer_threshold, string layer_name_thresh_removed) = PRZH.ExtractValueFromString(L.Name, PRZC.c_REGEX_THRESHOLD_PERCENT_PATTERN_ANY);

                    // If the Layer Name contains a Threshold number...
                    if (ThresholdFound)
                    {
                        // ensure threshold is 0 to 100 inclusive
                        if (layer_threshold < 0 | layer_threshold > 100)
                        {
                            string message = "An invalid threshold of " + layer_threshold.ToString() + " has been specified for:" +
                                             Environment.NewLine + Environment.NewLine +
                                             "Layer: " + L.Name + Environment.NewLine +
                                             "Threshold must be in the range 0 to 100." + Environment.NewLine + Environment.NewLine +
                                             "Click OK to skip this layer and continue, or click CANCEL to quit";

                            if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
                                                System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return fail;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // My new layer name (threshold excised)
                        layer_name = layer_name_thresh_removed;
                    }

                    // Layer Name does not contain a number
                    else
                    {
                        // My layer name should remain unchanged
                        layer_name = L.Name;

                        // get the default threshold for this layer
                        layer_threshold = default_threshold;   // use default value
                    }

                    #endregion

                    string layerJson = "";
                    await QueuedTask.Run(() =>
                    {
                        CIMBaseLayer cimbl = L.GetDefinition();
                        layerJson = cimbl.ToJson();
                    });

                    // Process layer based on type
                    if (L is FeatureLayer FL)
                    {
                        #region BASIC FL VALIDATION

                        // Ensure that FL is valid (i.e. has valid source data)
                        if (!await QueuedTask.Run(() =>
                        {
                            using (FeatureClass FC = FL.GetFeatureClass())
                            {
                                return FC != null;
                            }
                        }))
                        {
                            if (ProMsgBox.Show($"The Feature Layer '{FL.Name}' has no Data Source.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return fail;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // Ensure that FL has a valid spatial reference
                        if (!await QueuedTask.Run(() =>
                        {
                            SpatialReference SR = FL.GetSpatialReference();
                            return SR != null && !SR.IsUnknown;
                        }))
                        {
                            if (ProMsgBox.Show($"The Feature Layer '{FL.Name}' has a NULL or UNKNOWN Spatial Reference.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return fail;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // Ensure that FL is Polygon layer
                        if (FL.ShapeType != esriGeometryType.esriGeometryPolygon)
                        {
                            if (ProMsgBox.Show($"The Feature Layer '{FL.Name}' is NOT a Polygon Feature Layer.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return fail;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        #endregion

                        #region CREATE THE RULE

                        SelectionRule rule = new SelectionRule();

                        rule.sr_id = srid++;
                        rule.sr_name = layer_name;
                        rule.sr_layer_object = L;
                        rule.sr_layer_type = SelectionRuleLayerType.VECTOR;
                        rule.sr_layer_json = layerJson;
                        rule.sr_rule_type = SelectionRuleType.INCLUDE;
                        rule.sr_min_threshold = layer_threshold;

                        rules.Add(rule);

                        #endregion
                    }
                    else if (L is RasterLayer RL)
                    {
                        #region BASIC RL VALIDATION

                        // Ensure that RL is valid (i.e. has valid source data)
                        if (!await QueuedTask.Run(() =>
                        {
                            using (Raster R = RL.GetRaster())
                            {
                                return R != null;
                            }
                        }))
                        {
                            if (ProMsgBox.Show($"The Raster Layer '{RL.Name}' has no Data Source.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return fail;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // Ensure that RL has a valid spatial reference
                        if (!await QueuedTask.Run(() =>
                        {
                            SpatialReference SR = RL.GetSpatialReference();
                            return SR != null && !SR.IsUnknown;
                        }))
                        {
                            if (ProMsgBox.Show($"The Raster Layer '{RL.Name}' has a NULL or UNKNOWN Spatial Reference.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return fail;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        #endregion

                        #region CREATE THE RULE

                        SelectionRule rule = new SelectionRule();

                        rule.sr_id = srid++;
                        rule.sr_name = layer_name;
                        rule.sr_layer_object = L;
                        rule.sr_layer_type = SelectionRuleLayerType.RASTER;
                        rule.sr_layer_json = layerJson;
                        rule.sr_rule_type = SelectionRuleType.INCLUDE;
                        rule.sr_min_threshold = layer_threshold;

                        rules.Add(rule);

                        #endregion
                    }
                }

                // Process the EXCLUDE layers next
                for (int i = 0; i < exclude_layers.Count; i++)
                {
                    Layer L = exclude_layers[i];

                    #region EXTRACT MINIMUM THRESHOLD FROM LAYER NAME

                    string layer_name = "";

                    // Inspect the Layer Name for a Minimum Threshold number
                    (bool ThresholdFound, int layer_threshold, string layer_name_thresh_removed) = PRZH.ExtractValueFromString(L.Name, PRZC.c_REGEX_THRESHOLD_PERCENT_PATTERN_ANY);

                    // If the Layer Name contains a Threshold number...
                    if (ThresholdFound)
                    {
                        // ensure threshold is 0 to 100 inclusive
                        if (layer_threshold < 0 | layer_threshold > 100)
                        {
                            string message = "An invalid threshold of " + layer_threshold.ToString() + " has been specified for:" +
                                             Environment.NewLine + Environment.NewLine +
                                             "Layer: " + L.Name + Environment.NewLine +
                                             "Threshold must be in the range 0 to 100." + Environment.NewLine + Environment.NewLine +
                                             "Click OK to skip this layer and continue, or click CANCEL to quit";

                            if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
                                                System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return fail;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // My new layer name (threshold excised)
                        layer_name = layer_name_thresh_removed;
                    }

                    // Layer Name does not contain a number
                    else
                    {
                        // My layer name should remain unchanged
                        layer_name = L.Name;

                        // get the default threshold for this layer
                        layer_threshold = default_threshold;   // use default value
                    }

                    #endregion

                    string layerJson = "";
                    await QueuedTask.Run(() =>
                    {
                        CIMBaseLayer cimbl = L.GetDefinition();
                        layerJson = cimbl.ToJson();
                    });

                    // Process layer based on type
                    if (L is FeatureLayer FL)
                    {
                        #region BASIC FL VALIDATION

                        // Ensure that FL is valid (i.e. has valid source data)
                        if (!await QueuedTask.Run(() =>
                        {
                            using (FeatureClass FC = FL.GetFeatureClass())
                            {
                                return FC != null;
                            }
                        }))
                        {
                            if (ProMsgBox.Show($"The Feature Layer '{FL.Name}' has no Data Source.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return fail;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // Ensure that FL has a valid spatial reference
                        if (!await QueuedTask.Run(() =>
                        {
                            SpatialReference SR = FL.GetSpatialReference();
                            return SR != null && !SR.IsUnknown;
                        }))
                        {
                            if (ProMsgBox.Show($"The Feature Layer '{FL.Name}' has a NULL or UNKNOWN Spatial Reference.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return fail;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // Ensure that FL is Polygon layer
                        if (FL.ShapeType != esriGeometryType.esriGeometryPolygon)
                        {
                            if (ProMsgBox.Show($"The Feature Layer '{FL.Name}' is NOT a Polygon Feature Layer.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return fail;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        #endregion

                        #region CREATE THE RULE

                        SelectionRule rule = new SelectionRule();

                        rule.sr_id = srid++;
                        rule.sr_name = layer_name;
                        rule.sr_layer_object = L;
                        rule.sr_layer_type = SelectionRuleLayerType.VECTOR;
                        rule.sr_layer_json = layerJson;
                        rule.sr_rule_type = SelectionRuleType.EXCLUDE;
                        rule.sr_min_threshold = layer_threshold;

                        rules.Add(rule);

                        #endregion
                    }
                    else if (L is RasterLayer RL)
                    {
                        #region BASIC RL VALIDATION

                        // Ensure that RL is valid (i.e. has valid source data)
                        if (!await QueuedTask.Run(() =>
                        {
                            using (Raster R = RL.GetRaster())
                            {
                                return R != null;
                            }
                        }))
                        {
                            if (ProMsgBox.Show($"The Raster Layer '{RL.Name}' has no Data Source.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return fail;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // Ensure that RL has a valid spatial reference
                        if (!await QueuedTask.Run(() =>
                        {
                            SpatialReference SR = RL.GetSpatialReference();
                            return SR != null && !SR.IsUnknown;
                        }))
                        {
                            if (ProMsgBox.Show($"The Raster Layer '{RL.Name}' has a NULL or UNKNOWN Spatial Reference.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return fail;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        #endregion

                        #region CREATE THE RULE

                        SelectionRule rule = new SelectionRule();

                        rule.sr_id = srid++;
                        rule.sr_name = layer_name;
                        rule.sr_layer_object = L;
                        rule.sr_layer_type = SelectionRuleLayerType.RASTER;
                        rule.sr_layer_json = layerJson;
                        rule.sr_rule_type = SelectionRuleType.EXCLUDE;
                        rule.sr_min_threshold = layer_threshold;

                        rules.Add(rule);

                        #endregion
                    }

                }

                return (true, rules);
            }
            catch (Exception)
            {
                return fail;
            }
        }

        private async Task<bool> IntersectRuleLayers(List<SelectionRule> rules, int val)
        {
            try
            {
                #region BUILD PUID AND TOTAL PU AREA DICTIONARY

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Constructing PUID and Area dictionary..."), true, ++val);
                Dictionary<int, double> DICT_PUID_Area_Total = await PRZH.GetPlanningUnitIDsAndArea();

                if (DICT_PUID_Area_Total == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error constructing PUID and Area dictionary...", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error constructing PUID and Area dictionary.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Dictionary constructed..."), true, ++val);
                }

                #endregion

                Map map = MapView.Active.Map;

                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                // some paths
                string gdbpath = PRZH.GetPath_ProjectGDB();
                string pufcpath = PRZH.GetPath_FC_PU();
                string pusrpath = PRZH.GetPath_Table_PUSelRules();
                string srpath = PRZH.GetPath_Table_SelRules();

                // some planning unit elements
                FeatureLayer PUFL = (FeatureLayer)PRZH.GetPRZLayer(map, PRZLayerNames.PU);
                SpatialReference PUFC_SR = null;
                Envelope PUFC_Extent = null;

                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        PUFL.ClearSelection();

                        using (FeatureClass PUFC = await PRZH.GetFC_PU())
                        using (FeatureClassDefinition fcDef = PUFC.GetDefinition())
                        {
                            PUFC_SR = fcDef.GetSpatialReference();
                            PUFC_Extent = PUFC.GetExtent();
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving Spatial Reference and Extent...", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving Spatial Reference and Extent.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved spatial reference and extent."), true, ++val);
                }

                foreach (SelectionRule SR in rules)
                {
                    // Get some Rule info
                    int srid = SR.sr_id;
                    string name = SR.sr_name;
                    SelectionRuleType ruletype = SR.sr_rule_type;
                    SelectionRuleLayerType layertype = SR.sr_layer_type;
                    int threshold = SR.sr_min_threshold;

                    string prefix = "";

                    if (ruletype == SelectionRuleType.INCLUDE)
                    {
                        prefix = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_INCLUDE;
                    }
                    else if (ruletype == SelectionRuleType.EXCLUDE)
                    {
                        prefix = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_EXCLUDE;
                    }
                    else
                    { 
                        return false; 
                    }

                    // Process each rule layer
                    if (SR.sr_layer_object is FeatureLayer FL)
                    {
                        // Clear selection on rule layer
                        await QueuedTask.Run(() =>
                        {
                            FL.ClearSelection();
                        });

                        // Prepare for Intersection Prelim FCs
                        string intersect_fc_name = PRZC.c_FC_TEMP_PUSELRULES_PREFIX + srid.ToString() + PRZC.c_FC_TEMP_PUSELRULES_SUFFIX_INT;
                        string intersect_fc_path = Path.Combine(gdbpath, intersect_fc_name);

                        // Construct the inputs value array
                        object[] a = { PUFL, 1 };   // prelim array -> combine the layer object and the Rank (PU layer)
                        object[] b = { FL, 2 };     // prelim array -> combine the layer object and the Rank (Rule layer)

                        IReadOnlyList<string> a2 = Geoprocessing.MakeValueArray(a);   // Let this method figure out how best to quote the layer info
                        IReadOnlyList<string> b2 = Geoprocessing.MakeValueArray(b);   // Let this method figure out how best to quote the layer info

                        string inputs_string = string.Join(" ", a2) + ";" + string.Join(" ", b2);   // my final inputs string

                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Intersecting selection rule {srid} layer ({name})."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(inputs_string, intersect_fc_path, "ALL", "", "INPUT");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true, outputCoordinateSystem: PUFC_SR);
                        toolOutput = await PRZH.RunGPTool("Intersect_analysis", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error intersecting selection rule {srid} layer.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error intersecting selection rule {srid} layer.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Intersection successful for selection rule {srid} layer."), true, ++val);
                        }

                        // Now dissolve the temp intersect layer on PUID
                        string dissolve_fc_name = PRZC.c_FC_TEMP_PUSELRULES_PREFIX + srid.ToString() + PRZC.c_FC_TEMP_PUSELRULES_SUFFIX_DSLV;
                        string dissolve_fc_path = Path.Combine(gdbpath, dissolve_fc_name);

                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Dissolving {intersect_fc_name} on {PRZC.c_FLD_FC_PU_ID}..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(intersect_fc_path, dissolve_fc_path, PRZC.c_FLD_FC_PU_ID, "", "MULTI_PART", "");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true, outputCoordinateSystem: PUFC_SR);
                        toolOutput = await PRZH.RunGPTool("Dissolve_management", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error dissolving {intersect_fc_name}.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error dissolving {intersect_fc_name}.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"{intersect_fc_name} was dissolved successfully."), true, ++val);
                        }

                        // Extract the dissolved area for each puid into a second dictionary
                        Dictionary<int, double> DICT_PUID_Area_Dissolved = new Dictionary<int, double>();

                        if (!await QueuedTask.Run(async () =>
                        {
                            try
                            {
                                using (Geodatabase gdb = await PRZH.GetProjectGDB())
                                using (FeatureClass fc = await PRZH.GetFeatureClass(gdb, dissolve_fc_name))
                                {
                                    if (fc == null)
                                    {
                                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to locate dissolve output: " + dissolve_fc_name, LogMessageType.ERROR), true, ++val);
                                        return false;
                                    }

                                    using (RowCursor rowCursor = fc.Search(null, false))
                                    {
                                        while (rowCursor.MoveNext())
                                        {
                                            using (Feature feature = (Feature)rowCursor.Current)
                                            {
                                                int puid = Convert.ToInt32(feature[PRZC.c_FLD_FC_PU_ID]);

                                                Polygon poly = (Polygon)feature.GetShape();
                                                double area_m = poly.Area;

                                                DICT_PUID_Area_Dissolved.Add(puid, area_m);
                                            }
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
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error constructing PUID and Dissolved Area dictionary...", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error constructing PUID and Dissolved Area dictionary.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Dictionary constructed..."), true, ++val);
                        }

                        // Write this information to the PU SelRules table
                        if (!await QueuedTask.Run(async () =>
                        {
                            try
                            {
                                // Get the Area and Coverage fields for this Selection Rule
                                string AreaField = prefix + srid.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_AREA;
                                string CoverageField = prefix + srid.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_COVERAGE;
                                string StateField = prefix + srid.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_STATE;

                                // Iterate through each PUID returned from the intersection (this dictionary might only have a few, or even no entries)
                                foreach (KeyValuePair<int, double> KVP in DICT_PUID_Area_Dissolved)
                                {
                                    int PUID = KVP.Key;
                                    double area_dslv = KVP.Value;
                                    double area_total = DICT_PUID_Area_Total[PUID];
                                    double coverage = area_dslv / area_total;    // write this to table later

                                    double coverage_pct = (coverage > 1) ? 100 : coverage * 100.0;

                                    coverage_pct = Math.Round(coverage_pct, 1, MidpointRounding.AwayFromZero);

                                    QueryFilter QF = new QueryFilter
                                    {
                                        WhereClause = PRZC.c_FLD_TAB_PUSELRULES_ID + " = " + PUID.ToString()
                                    };

                                    using (Table table = await PRZH.GetTable_PUSelRules())
                                    using (RowCursor rowCursor = table.Search(QF, false))
                                    {
                                        while (rowCursor.MoveNext())
                                        {
                                            using (Row row = rowCursor.Current)
                                            {
                                                row[AreaField] = area_dslv;
                                                row[CoverageField] = coverage_pct;
                                                row[StateField] = coverage_pct >= threshold ? 1 : 0;

                                                row.Store();
                                            }
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
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error writing dissolve info to the {PRZC.c_TABLE_PUSELRULES} table...", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error writing dissolve info to the {PRZC.c_TABLE_PUSELRULES} table.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Dissolve info written to {PRZC.c_TABLE_PUSELRULES}..."), true, ++val);
                        }

                        // Finally, delete the two temp feature classes
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting temporary feature classes..."), true, ++val);

                        object[] e = { intersect_fc_path, dissolve_fc_path };
                        var e2 = Geoprocessing.MakeValueArray(e);   // Let this method figure out how best to quote the paths
                        string inputs2 = String.Join(";", e2);
                        toolParams = Geoprocessing.MakeValueArray(inputs2, "");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting temp feature classes.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show("Error deleting temp feature classes...");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Temp feature classes deleted successfully."), true, ++val);
                        }
                    }
                    else if (SR.sr_layer_object is RasterLayer RL)
                    {
                        // Get the Raster Layer and its SRs
                        SpatialReference RL_SR = null;
                        SpatialReference R_SR = null;

                        if (!await QueuedTask.Run(() =>
                        {
                            try
                            {
                                RL_SR = RL.GetSpatialReference();               // do I need this one...

                                using (Raster costRaster = RL.GetRaster())
                                {
                                    R_SR = costRaster.GetSpatialReference();    // or this one... ?
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
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving Spatial Reference and Extent...", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error retrieving Spatial Reference and Extent.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved spatial reference and extent."), true, ++val);
                        }

                        // prepare the temporary zonal stats table
                        string tabname = "sr_zonal_temp";

                        // Calculate Zonal Statistics as Table
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Executing Zonal Statistics as Table for rule {srid} table..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(PUFL, PRZC.c_FLD_FC_PU_ID, RL, tabname);  // TODO: Ensure I'm using the correct object: FL or FC?  Which one?
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: PUFC_SR, overwriteoutput: true, extent: PUFC_Extent);
                        toolOutput = await PRZH.RunGPTool("ZonalStatisticsAsTable_sa", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Error executing the Zonal Statistics as Table tool.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show("Error executing zonal statistics as table tool...");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Zonal Statistics as Table tool completed successfully."), true, ++val);
                        }

                        // Retrieve info from the zonal stats table.
                        // Each record in the zonal stats table represents a single PU ID

                        // for each PU ID, I need the following:
                        //  > COUNT field value     -- this is the number of raster cells found within the zone (the PU)
                        //  > AREA field value      -- this is the total area of all cells within zone (cell area * count)

                        // *** COUNT is based on all cells having a non-NODATA value ***
                        // *** This is a business rule that PRZ Tools users will need to be aware of when supplying CF rasters

                        Dictionary<int, Tuple<int, double>> DICT_PUID_and_count_area = new Dictionary<int, Tuple<int, double>>();

                        if (!await QueuedTask.Run(async () =>
                        {
                            try
                            {
                                using (Geodatabase gdb = await PRZH.GetProjectGDB())
                                using (Table table = await PRZH.GetTable(gdb, tabname))
                                using (RowCursor rowCursor = table.Search(null, false))
                                {
                                    while (rowCursor.MoveNext())
                                    {
                                        using (Row row = rowCursor.Current)
                                        {
                                            int puid = Convert.ToInt32(row[PRZC.c_FLD_ZONALSTATS_ID]);
                                            int count = Convert.ToInt32(row[PRZC.c_FLD_ZONALSTATS_COUNT]);
                                            double area = Convert.ToDouble(row[PRZC.c_FLD_ZONALSTATS_AREA]);

                                            if (puid > 0)
                                            {
                                                DICT_PUID_and_count_area.Add(puid, Tuple.Create(count, area));
                                            }
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
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving Zonal Stats...", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error retrieving Zonal Stats.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Zonal stats retrieved."), true, ++val);
                        }

                        // Delete the temp zonal stats table (I no longer need it, I think...)
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {tabname} Table..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(tabname, "");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                        toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {tabname} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error deleting the {tabname} table.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"{tabname} table deleted."), true, ++val);
                        }

                        // Get the Area and Coverage fields for this Selection Rule
                        string AreaField = prefix + srid.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_AREA;
                        string CoverageField = prefix + srid.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_COVERAGE;
                        string StateField = prefix + srid.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_STATE;

                        foreach (KeyValuePair<int, Tuple<int, double>> KVP in DICT_PUID_and_count_area)
                        {
                            int PUID = KVP.Key;
                            Tuple<int, double> tuple = KVP.Value;

                            int count_ras = tuple.Item1;
                            double area_ras = tuple.Item2;
                            double area_total = DICT_PUID_Area_Total[PUID];
                            double coverage = area_ras / area_total;
                            double coverage_pct = (coverage > 1) ? 100 : coverage * 100.0;

                            coverage_pct = Math.Round(coverage_pct, 1, MidpointRounding.AwayFromZero);

                            QueryFilter QF = new QueryFilter
                            {
                                WhereClause = PRZC.c_FLD_TAB_PUSELRULES_ID + " = " + PUID.ToString()
                            };

                            if (!await QueuedTask.Run(async () =>
                            {
                                try
                                {
                                    using (Table table = await PRZH.GetTable_PUSelRules())
                                    using (RowCursor rowCursor = table.Search(QF, false))
                                    {
                                        while (rowCursor.MoveNext())
                                        {
                                            using (Row row = rowCursor.Current)
                                            {
                                                row[AreaField] = area_ras;
                                                row[CoverageField] = coverage_pct;
                                                row[StateField] = coverage_pct >= threshold ? 1 : 0;

                                                row.Store();
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
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error writing rule {srid} info to the {PRZC.c_TABLE_PUSELRULES} table...", LogMessageType.ERROR), true, ++val);
                                ProMsgBox.Show($"Error writing rule {srid} info to the {PRZC.c_TABLE_PUSELRULES} table.");
                                return false;
                            }
                        }
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Selection Rule {srid} layer is neither a FeatureLayer or a RasterLayer", LogMessageType.ERROR), true, ++val);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true);
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private async Task<bool> ConflictDblClick()
        {
            try
            {
                if (SelectedConflict == null)
                {
                    return true;
                }

                // Get the conflicted Planning Unit IDs
                List<int> LIST_PUIDs = new List<int>();

                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        string whereclause = SelectedConflict.include_rule_statefield + @" = 1 And " + SelectedConflict.exclude_rule_statefield + @" = 1";

                        QueryFilter QF = new QueryFilter
                        {
                            SubFields = PRZC.c_FLD_TAB_PUSELRULES_ID + "," + SelectedConflict.include_rule_statefield + "," + SelectedConflict.exclude_rule_statefield,
                            WhereClause = whereclause
                        };

                        using (Table table = await PRZH.GetTable_PUSelRules())
                        using (RowCursor rowCursor = table.Search(QF, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int id = Convert.ToInt32(row[PRZC.c_FLD_TAB_PUSELRULES_ID]);
                                    LIST_PUIDs.Add(id);
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
                    ProMsgBox.Show("Error retrieving conflicted planning unit IDs...");
                    return false;
                }

                // Exit if list is empty
                if (LIST_PUIDs.Count == 0)
                {
                    return true;
                }

                // Select the planning units
                Map map = MapView.Active.Map;

                if (!await QueuedTask.Run(() =>
                {
                    try
                    {
                        FeatureLayer FL = (FeatureLayer)PRZH.GetPRZLayer(map, PRZLayerNames.PU);

                        string puids = string.Join(",", LIST_PUIDs);

                        QueryFilter QF = new QueryFilter
                        {
                            WhereClause = PRZC.c_FLD_FC_PU_ID + " In (" + puids + ")"
                        };

                        using (Selection selection = FL.Select(QF, SelectionCombinationMethod.New))
                        {
                            int counter = selection.GetCount();
                            string label = (counter == 1) ? "" : "s";
                            ProMsgBox.Show($"{counter} Planning Unit{label} Selected");
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
                    ProMsgBox.Show("Error selecting conflicted planning units.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private async Task<bool> RuleDblClick()
        {
            try
            {
                if (SelectedSelectionRule == null)
                {
                    return true;
                }

                // Exit if the Selection Rule has no associated planning units
                if (SelectedSelectionRule.sr_pucount == 0)
                {
                    return true;
                }

                // Get the Selection Rule ID
                int sr_id = SelectedSelectionRule.sr_id;

                // Get the Area Field name
                string statefield = "";

                if (SelectedSelectionRule.sr_rule_type == SelectionRuleType.INCLUDE)
                {
                    statefield = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_INCLUDE + sr_id.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_STATE;
                }
                else if (SelectedSelectionRule.sr_rule_type == SelectionRuleType.EXCLUDE)
                {
                    statefield = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_EXCLUDE + sr_id.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_STATE;
                }
                else
                {
                    return false;
                }

                // Query the PUSelRules table for associated planning unit records
                List<int> LIST_PUIDs = new List<int>();

                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        string whereclause = statefield + @" = 1";

                        QueryFilter QF = new QueryFilter
                        {
                            SubFields = PRZC.c_FLD_TAB_PUSELRULES_ID + "," + statefield,
                            WhereClause = whereclause
                        };

                        using (Table table = await PRZH.GetTable_PUSelRules())
                        using (RowCursor rowCursor = table.Search(QF, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int id = Convert.ToInt32(row[PRZC.c_FLD_TAB_PUSELRULES_ID]);
                                    LIST_PUIDs.Add(id);
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
                    ProMsgBox.Show("Error retrieving associated planning unit IDs...");
                    return false;
                }

                // Exit if list is empty
                if (LIST_PUIDs.Count == 0)
                {
                    return true;
                }

                // Select the planning units
                Map map = MapView.Active.Map;

                if (!await QueuedTask.Run(() =>
                {
                    try
                    {
                        FeatureLayer FL = (FeatureLayer)PRZH.GetPRZLayer(map, PRZLayerNames.PU);

                        string puids = string.Join(",", LIST_PUIDs);

                        QueryFilter QF = new QueryFilter
                        {
                            WhereClause = PRZC.c_FLD_FC_PU_ID + " In (" + puids + ")"
                        };

                        using (Selection selection = FL.Select(QF, SelectionCombinationMethod.New))
                        {
                            int counter = selection.GetCount();
                            string label = (counter == 1) ? "" : "s";
                            ProMsgBox.Show($"{counter} Planning Unit{label} Selected");
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
                    ProMsgBox.Show("Error selecting associated planning units.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private async Task<bool> ClearSelRules()
        {
            int val = 0;

            try
            {
                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                // Some paths
                string gdbpath = PRZH.GetPath_ProjectGDB();
                string srpath = PRZH.GetPath_Table_SelRules();
                string pusrpath = PRZH.GetPath_Table_PUSelRules();

                // Initialize ProgressBar and Progress Log
                int max = 20;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing..."), false, max, ++val);

                // Validation: Prompt User for permission to proceed
                if (ProMsgBox.Show("If you proceed, the following will happen:" +
                   Environment.NewLine + 
                   $"1. The {PRZC.c_TABLE_SELRULES} and {PRZC.c_TABLE_PUSELRULES} tables will be deleted from the project geodatabase (if they exist)" + Environment.NewLine +
                   $"2. All values in the {PRZC.c_FLD_FC_PU_EFFECTIVE_RULE} field in the {PRZC.c_FC_PLANNING_UNITS} feature class will be set to 1" + Environment.NewLine +
                   $"3. All values in the {PRZC.c_FLD_FC_PU_CONFLICT} field in the {PRZC.c_FC_PLANNING_UNITS} feature class will be set to 0" +
                   Environment.NewLine + Environment.NewLine +
                   "Do you wish to proceed?" +
                   Environment.NewLine + Environment.NewLine +
                   "Choose wisely...",
                   "TABLE DELETE WARNING",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out."), true, ++val);
                    return false;
                }

                // Delete the SelRules table
                if (await PRZH.TableExists_SelRules())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting the {PRZC.c_TABLE_SELRULES} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(srpath, "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_SELRULES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_SELRULES} table.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_SELRULES} table deleted successfully..."), true, ++val);
                    }
                }

                // Delete the PUSelRules table
                if (await PRZH.TableExists_PUSelRules())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting the {PRZC.c_TABLE_PUSELRULES} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(pusrpath, "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_PUSELRULES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_PUSELRULES} table.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_PUSELRULES} table deleted successfully..."), true, ++val);
                    }
                }

                // Update PUFC, set effective rule and conflict fields to default
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Resetting {PRZC.c_FLD_FC_PU_EFFECTIVE_RULE} and {PRZC.c_FLD_FC_PU_CONFLICT} fields in {PRZC.c_FC_PLANNING_UNITS} Feature Class..."), true, ++val);
                
                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (Table table = await PRZH.GetFC_PU())
                        using (RowCursor rowCursor = table.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    row[PRZC.c_FLD_FC_PU_EFFECTIVE_RULE] = DBNull.Value;
                                    row[PRZC.c_FLD_FC_PU_CONFLICT] = 0;

                                    row.Store();
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error resetting the {PRZC.c_FLD_FC_PU_EFFECTIVE_RULE} and {PRZC.c_FLD_FC_PU_CONFLICT} fields in {PRZC.c_FC_PLANNING_UNITS} Feature Class.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error resetting {PRZC.c_FLD_FC_PU_EFFECTIVE_RULE} and {PRZC.c_FLD_FC_PU_CONFLICT} fields in {PRZC.c_FC_PLANNING_UNITS} Feature Class.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields updated successfully."), true, ++val);
                }


                // Determine the presence of 2 tables, and enable/disable the main button accordingly
                SelRuleTableExists = await PRZH.TableExists_SelRules();
                PUSelRuleTableExists = await PRZH.TableExists_PUSelRules();
                SelRulesExist = SelRuleTableExists || PUSelRuleTableExists;

                // Repopulate the grids
                if (!await PopulateGrid_Rules())
                {
                    ProMsgBox.Show("Error loading the Selection Rules grid");
                }
                if (!await PopulateGrid_Conflicts())
                {
                    ProMsgBox.Show("Error loading the Conflicts grid");
                }

                ProMsgBox.Show("Selection Rules Removed.");

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                return false;
            }
        }

        #endregion


    }
}











