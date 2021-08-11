using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
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
    public class PUStatusVM : PropertyChangedBase
    {
        public PUStatusVM()
        {
        }

        #region Fields

        private const string c_OVERRIDE_INCLUDE = "LOCKED IN";
        private const string c_OVERRIDE_EXCLUDE = "LOCKED OUT";

        #endregion

        #region Properties

        private ObservableCollection<StatusConflict> _conflicts = new ObservableCollection<StatusConflict>();
        public ObservableCollection<StatusConflict> Conflicts
        {
            get { return _conflicts; }
            set
            {
                _conflicts = value;
                SetProperty(ref _conflicts, value, () => Conflicts);
            }
        }

        private StatusConflict _selectedConflict;
        public StatusConflict SelectedConflict
        {
            get => _selectedConflict; set => SetProperty(ref _selectedConflict, value, () => SelectedConflict);
        }

        private List<string> _overrideOptions = new List<string> { c_OVERRIDE_INCLUDE, c_OVERRIDE_EXCLUDE };
        public List<string> OverrideOptions
        {
            get => _overrideOptions; set => SetProperty(ref _overrideOptions, value, () => OverrideOptions);
        }

        private string _selectedOverrideOption = c_OVERRIDE_INCLUDE;
        public string SelectedOverrideOption
        {
            get => _selectedOverrideOption; set => SetProperty(ref _selectedOverrideOption, value, () => SelectedOverrideOption);
        }


        private int _barMax;
        public int BarMax
        {
            get => _barMax; set => SetProperty(ref _barMax, value, () => BarMax);
        }

        private int _barMin;
        public int BarMin
        {
            get => _barMin; set => SetProperty(ref _barMin, value, () => BarMin);
        }

        private int _barValue;
        public int BarValue
        {
            get => _barValue; set => SetProperty(ref _barValue, value, () => BarValue);
        }

        private string _barMessage;
        public string BarMessage
        {
            get => _barMessage; set => SetProperty(ref _barMessage, value, () => BarMessage);
        }

        private string _conflictGridCaption;
        public string ConflictGridCaption
        {
            get => _conflictGridCaption; set => SetProperty(ref _conflictGridCaption, value, () => ConflictGridCaption);
        }

        private string _defaultThreshold = Properties.Settings.Default.DEFAULT_STATUS_THRESHOLD;
        public string DefaultThreshold
        {
            get => _defaultThreshold;

            set
            {
                SetProperty(ref _defaultThreshold, value, () => DefaultThreshold);
                Properties.Settings.Default.DEFAULT_STATUS_THRESHOLD = value;
                Properties.Settings.Default.Save();
            }
        }


        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""
        public ProgressManager PM
        {
            get => _pm;

            set => SetProperty(ref _pm, value, () => PM);
        }

        #endregion

        #region Commands

        private ICommand _cmdClearLog;
        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true));

        //CmdDeleteStatusInfoTable
        private ICommand _cmdDeleteStatusInfoTable;
        public ICommand CmdDeleteStatusInfoTable => _cmdDeleteStatusInfoTable ?? (_cmdDeleteStatusInfoTable = new RelayCommand(() => DeleteStatusInfoTable(), () => true));

        private ICommand _cmdConstraintDoubleClick;
        public ICommand CmdConstraintDoubleClick => _cmdConstraintDoubleClick ?? (_cmdConstraintDoubleClick = new RelayCommand(() => ConstraintDoubleClick(), () => true));


        private ICommand _calculateStatus;
        public ICommand CmdCalculateStatus => _calculateStatus ?? (_calculateStatus = new RelayCommand(() => CalculateStatus(), () => true));

        public ICommand cmdOK => new RelayCommand((paramProWin) =>
        {
            (paramProWin as ProWindow).DialogResult = true;
            (paramProWin as ProWindow).Close();
        }, () => true);
        public ICommand cmdCancel => new RelayCommand((paramProWin) =>
        {
            (paramProWin as ProWindow).DialogResult = false;
            (paramProWin as ProWindow).Close();
        }, () => true);

        #endregion

        #region Methods

        public async void OnProWinLoaded()
        {
            try
            {
                // Initialize the Progress Bar & Log
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                // Set the Conflict Override value default
                SelectedOverrideOption = c_OVERRIDE_INCLUDE;

                // Populate the Grid
                bool Populated = await PopulateConflictGrid();

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> PopulateConflictGrid()
        {
            try
            {
                // Read records from the Status Info table
                // convert each record to a Status Conflict object
                // add to ObservableCollection
                // call notifypropertychanged method

                // Clear the contents of the Conflicts observable collection
                Conflicts.Clear();

                if (!await PRZH.StatusInfoTableExists())
                {
                    // format stuff appropriately if no table exists
                    ConflictGridCaption = "Planning Unit Status Conflict Listing (no Status Info Table)";

                    return true;
                }

                // PUStatus Table exists, retrieve the data
                Dictionary<int, string> DICT_IN = new Dictionary<int, string>();    // Dictionary where key = Area Column Indexes, value = IN Constraint Layer to which it applies
                Dictionary<int, string> DICT_EX = new Dictionary<int, string>();    // Dictionary where key = Area Column Indexes, value = EX Constraint Layer to which it applies

                List<Field> fields = null;  // List of Planning Unit Status table fields

                // Populate the Dictionaries
                await QueuedTask.Run(async () =>
                {
                    using (Table table = await PRZH.GetStatusInfoTable())
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        TableDefinition tDef = table.GetDefinition();
                        fields = tDef.GetFields().ToList();

                        // Get the first row (I only need one row)
                        if (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                // Now, get field info and row value info that's present in all rows exactly the same (that's why I only need one row)
                                for (int i = 0; i < fields.Count; i++)
                                {
                                    Field field = fields[i];

                                    if (field.Name.EndsWith("_Name"))
                                    {
                                        string constraint_name = row[i].ToString();         // this is the name of the constraint layer to which columns i to i+2 apply

                                        if (field.Name.StartsWith("IN"))
                                        {
                                            DICT_IN.Add(i + 2, constraint_name);    // i + 2 is the Area field, two columns to the right of the Name field
                                        }
                                        else if (field.Name.StartsWith("EX"))
                                        {
                                            DICT_EX.Add(i + 2, constraint_name);    // i + 2 is the Area field, two columns to the right of the Name field
                                        }
                                    }
                                }
                            }
                        }
                    }
                });

                // Build a List of Unique Combinations of IN and EX Layers
                string c_ConflictNumber = "CONFLICT";
                string c_NameInclude = "'INCLUDE' Constraint";
                string c_NameExclude = "'EXCLUDE' Constraint";
                string c_TileCount = "TILE COUNT";
                string c_IndexInclude = "IndexIN";
                string c_IndexExclude = "IndexEX";
                string c_ConflictExists = "Exists";

                DataTable DT = new DataTable();
                DT.Columns.Add(c_ConflictNumber, Type.GetType("System.Int32"));
                DT.Columns.Add(c_NameInclude, Type.GetType("System.String"));
                DT.Columns.Add(c_NameExclude, Type.GetType("System.String"));
                DT.Columns.Add(c_TileCount, Type.GetType("System.Int32"));
                DT.Columns.Add(c_IndexInclude, Type.GetType("System.Int32"));
                DT.Columns.Add(c_IndexExclude, Type.GetType("System.Int32"));
                DT.Columns.Add(c_ConflictExists, Type.GetType("System.Boolean"));

                foreach (int in_ix in DICT_IN.Keys)
                {
                    string in_name = DICT_IN[in_ix];

                    foreach (int ex_ix in DICT_EX.Keys)
                    {
                        string ex_name = DICT_EX[ex_ix];

                        DataRow DR = DT.NewRow();

                        DR[c_NameInclude] = in_name;
                        DR[c_NameExclude] = ex_name;
                        DR[c_TileCount] = 0;
                        DR[c_IndexInclude] = in_ix;
                        DR[c_IndexExclude] = ex_ix;
                        DR[c_ConflictExists] = false;
                        DT.Rows.Add(DR);
                    }
                }

                // For each row in DataTable, query PU Status for pairs having area>0 in both IN and EX
                int conflict_number = 1;
                int IN_AreaField_Index;
                int EX_AreaField_Index;
                string IN_AreaField_Name = "";
                string EX_AreaField_Name = "";

                foreach (DataRow DR in DT.Rows)
                {
                    IN_AreaField_Index = (int)DR[c_IndexInclude];
                    EX_AreaField_Index = (int)DR[c_IndexExclude];

                    IN_AreaField_Name = fields[IN_AreaField_Index].Name;
                    EX_AreaField_Name = fields[EX_AreaField_Index].Name;

                    string where_clause = IN_AreaField_Name + @" > 0 And " + EX_AreaField_Name + @" > 0";

                    QueryFilter QF = new QueryFilter();
                    QF.SubFields = IN_AreaField_Name + "," + EX_AreaField_Name;
                    QF.WhereClause = where_clause;

                    int row_count = 0;

                    await QueuedTask.Run(async () =>
                    {
                        using (Table table = await PRZH.GetStatusInfoTable())
                        {
                            row_count = table.GetCount(QF);
                        }
                    });

                    if (row_count > 0)
                    {
                        DR[c_ConflictNumber] = conflict_number++;
                        DR[c_TileCount] = row_count;
                        DR[c_ConflictExists] = true;
                    }
                }


                // Filter out only those DataRows where conflict exists
                DataView DV = DT.DefaultView;
                DV.RowFilter = c_ConflictExists + " = true";

                // Finally, populate the Observable Collection

                List<StatusConflict> l = new List<StatusConflict>();
                foreach (DataRowView DRV in DV)
                {
                    StatusConflict sc = new StatusConflict();

                    sc.include_layer_name = DRV[c_NameInclude].ToString();
                    sc.include_layer_index = (int)DRV[c_IndexInclude];
                    sc.exclude_layer_name = DRV[c_NameExclude].ToString();
                    sc.exclude_layer_index = (int)DRV[c_IndexExclude];
                    sc.conflict_num = (int)DRV[c_ConflictNumber];
                    sc.pu_count = (int)DRV[c_TileCount];

                    l.Add(sc);
                }

                // Sort them
                l.Sort((x, y) => x.conflict_num.CompareTo(y.conflict_num));

                // Set the property
                _conflicts = new ObservableCollection<StatusConflict>(l);
                NotifyPropertyChanged(() => Conflicts);

                int count = DV.Count;

                ConflictGridCaption = "Planning Unit Status Conflict Listing (" + ((count == 1) ? "1 conflict)" : count.ToString() + " conflicts)");

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true);
                return false;
            }
        }

        private async Task<bool> CalculateStatus()
        {
            CancelableProgressorSource cps = null;  // use this for QueuedTask.Run tasks that take a while.  Otherwise, just use the progressbar on the window
            int val = 0;

            try
            {
                #region INITIALIZATION AND USER INPUT VALIDATION

                // Initialize a few thingies
                Map map = MapView.Active.Map;

                // Initialize ProgressBar and Progress Log
                int max = 50;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Status Calculator..."), false, max, ++val);

                // Validation: Ensure the Project Geodatabase Exists
                string gdbpath = PRZH.GetProjectGDBPath();
                if (!await PRZH.ProjectGDBExists())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Project Geodatabase not found: " + gdbpath, LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Project Geodatabase not found at this path:" +
                                   Environment.NewLine +
                                   gdbpath +
                                   Environment.NewLine + Environment.NewLine +
                                   "Please specify a valid Project Workspace.", "Validation");

                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Project Geodatabase is OK: " + gdbpath), true, ++val);
                }

                // Validation: Ensure the Planning Unit FC exists
                string pufcpath = PRZH.GetPlanningUnitFCPath();
                if (!await PRZH.PlanningUnitFCExists())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Class not found in the Project Geodatabase.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Class is OK: " + pufcpath), true, ++val);
                }

                // Validation: Ensure the Default Status Threshold is valid
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Default Threshold = " + threshold_text), true, ++val);
                }

                // Validation: Ensure three required Layers are present
                if (!PRZH.PRZLayerExists(map, PRZLayerNames.STATUS_INCLUDE) || !PRZH.PRZLayerExists(map, PRZLayerNames.STATUS_EXCLUDE) || !PRZH.PRZLayerExists(map, PRZLayerNames.PU))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Layers are missing.  Please reload PRZ layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("PRZ Layers are missing.  Please reload the PRZ Layers and try again.", "Validation");
                    return false;
                }

                // Validation: Ensure that at least one Feature Layer is present in either of the two group layers
                var LIST_IncludeFL = PRZH.GetFeatureLayers_STATUS_INCLUDE(map);
                var LIST_ExcludeFL = PRZH.GetFeatureLayers_STATUS_EXCLUDE(map);

                if (LIST_IncludeFL == null || LIST_ExcludeFL == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Unable to retrieve contents of Status Include or Status Exclude Group Layers.  Please reload PRZ layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Unable to retrieve contents of Status Include or Status Exclude Group Layers.  Please reload the PRZ Layers and try again.", "Validation");
                    return false;
                }

                if (LIST_IncludeFL.Count == 0 && LIST_ExcludeFL.Count == 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> No Feature Layers found within Status Include or Status Exclude group layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("There must be at least one Feature Layer within either the Status INCLUDE or the Status EXCLUDE group layers.", "Validation");
                    return false;
                }

                // Validation: Prompt User for permission to proceed
                if (ProMsgBox.Show("If you proceed, the Planning Unit Status table will be overwritten if it exists in the Project Geodatabase." +
                   Environment.NewLine + Environment.NewLine +
                   "Additionally, the contents of the 'status' field in the Planning Unit Feature Class will be updated." +
                   Environment.NewLine + Environment.NewLine +
                   "Do you wish to proceed?" +
                   Environment.NewLine + Environment.NewLine +
                   "Choose wisely...",
                   "TABLE OVERWRITE WARNING",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out of Status Calculation."), true, ++val);
                    return false;
                }

                #endregion

                #region PREPARE THE LAYER DATATABLES

                // Create Include and Exclude Data Tables
                // Each DataTable will contain one row per layer in that category (e.g. Include DT might contain 3 rows, one row per layer within the INclude group layer)
                DataTable DT_IncludeLayers = new DataTable("INCLUDE");    // doesn't really need a name
                DT_IncludeLayers.Columns.Add(PRZC.c_FLD_DATATABLE_STATUS_LAYER, typeof(Layer));
                DT_IncludeLayers.Columns.Add(PRZC.c_FLD_DATATABLE_STATUS_INDEX, Type.GetType("System.Int32"));
                DT_IncludeLayers.Columns.Add(PRZC.c_FLD_DATATABLE_STATUS_NAME, Type.GetType("System.String"));
                DT_IncludeLayers.Columns.Add(PRZC.c_FLD_DATATABLE_STATUS_THRESHOLD, Type.GetType("System.Double"));
                DT_IncludeLayers.Columns.Add(PRZC.c_FLD_DATATABLE_STATUS_STATUS, Type.GetType("System.Int32"));

                DataTable DT_ExcludeLayers = DT_IncludeLayers.Clone();

                if (!await PopulateLayerTable(PRZLayerNames.STATUS_INCLUDE, DT_IncludeLayers))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("INCLUDE Layers >> Unable to populate Data Table.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Unable to populate the INCLUDE Data Table.", "INCLUDE Layers");
                    return false;
                }

                if (!await PopulateLayerTable(PRZLayerNames.STATUS_EXCLUDE, DT_ExcludeLayers))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("EXCLUDE Layers >> Unable to populate Data Table.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Unable to populate the EXCLUDE Data Table.", "EXCLUDE Layers");
                    return false;
                }

                // Ensure that at least one row exists in one of the 2 DataTables.  Otherwise, quit

                if (DT_IncludeLayers.Rows.Count == 0 && DT_ExcludeLayers.Rows.Count == 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Status Calculator >> No valid Polygon Feature Layers found from INCLUDE or EXCLUDE group layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("There must be at least one valid Polygon Feature Layer in either the INCLUDE or EXCLUDE group layers.", "Status Calculator");
                    return false;
                }

                #endregion

                #region POPULATE 2 DICTIONARIES:  PUID -> AREA_M, and PUID -> STATUS

                Dictionary<int, double> DICT_PUID_and_assoc_area_m2 = new Dictionary<int, double>();
                Dictionary<int, int> DICT_PUID_and_assoc_status = new Dictionary<int, int>();

                await QueuedTask.Run(async () =>
                {
                    using (Geodatabase gdb = await PRZH.GetProjectGDB())
                    using (FeatureClass puFC = gdb.OpenDataset<FeatureClass>(PRZC.c_FC_PLANNING_UNITS))
                    using (RowCursor rowCursor = puFC.Search(null, false))
                    {
                        // Get the Definition
                        FeatureClassDefinition fcDef = puFC.GetDefinition();

                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                int puid = (int)row[PRZC.c_FLD_PUFC_ID];
                                double a = (double)row[PRZC.c_FLD_PUFC_AREA_M];
                                int status = (int)row[PRZC.c_FLD_PUFC_STATUS];

                                // store this id -> area KVP in the 1st dictionary
                                DICT_PUID_and_assoc_area_m2.Add(puid, a);

                                // store this id -> status KVP in the 2nd dictionary
                                DICT_PUID_and_assoc_status.Add(puid, status);
                            }
                        }
                    }
                });

                #endregion

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                string toolOutput;

                #region BUILD THE STATUS INFO TABLE

                string sipath = PRZH.GetStatusInfoTablePath();
                
                // Delete the existing Status Info table, if it exists

                if (await PRZH.StatusInfoTableExists())
                {
                    // Delete the existing Status Info table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting Status Info Table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(sipath, "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting the Status Info table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleted the existing Status Info Table..."), true, ++val);
                    }
                }

                // Copy PU FC rows into new Status Info table
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Copying Planning Unit FC Attributes into new Status Info table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pufcpath, sipath, "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CopyRows_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error copying PU FC rows to Status Info table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Status Info Table successfully created and populated..."), true, ++val);
                }

                // Delete all fields but OID and PUID from Status Info table
                List<string> LIST_DeleteFields = new List<string>();

                using (Table tab = await PRZH.GetStatusInfoTable())
                {
                    if (tab == null)
                    {
                        ProMsgBox.Show("Error getting Status Info Table :(");
                        return false;
                    }

                    await QueuedTask.Run(() =>
                    {
                        TableDefinition tDef = tab.GetDefinition();
                        List<Field> fields = tDef.GetFields().Where(f => f.FieldType != FieldType.OID && f.Name != PRZC.c_FLD_PUFC_ID).ToList();

                        foreach (Field field in fields)
                        {
                            LIST_DeleteFields.Add(field.Name);
                        }
                    });
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Removing unnecessary fields from the Status Info table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(sipath, LIST_DeleteFields);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting fields from Status Info table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Status Info Table fields successfully deleted"), true, ++val);
                }

                // Now index the PUID field in the Status Info table
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Indexing Planning Unit ID field in the Status Info table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(sipath, PRZC.c_FLD_PUFC_ID, "ix" + PRZC.c_FLD_PUFC_ID, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Add 2 additional fields to Status Info
                string fldQuickStatus = PRZC.c_FLD_STATUSINFO_QUICKSTATUS + " LONG 'Quick Status' # # #;";
                string fldConflict = PRZC.c_FLD_STATUSINFO_CONFLICT + " LONG 'Conflict' # # #;";

                string flds = fldQuickStatus + fldConflict;

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Adding fields to Status Info Table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(sipath, flds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding fields to Status Info Table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Add INCLUDE & EXCLUDE layer-based columns to Status Info table
                if (!await AddLayerFields(PRZLayerNames.STATUS_INCLUDE, DT_IncludeLayers))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding INCLUDE layer fields to Status Info Table.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error adding the INCLUDE layer fields to the Status Info table.", "");
                    return false;
                }

                if (!await AddLayerFields(PRZLayerNames.STATUS_EXCLUDE, DT_ExcludeLayers))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding EXCLUDE layer fields to Status Info Table.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error adding the EXCLUDE layer fields to the Status Info table.", "");
                    return false;
                }

                #endregion

                #region INTERSECT THE VARIOUS LAYERS

                if (!await IntersectConstraintLayers(PRZLayerNames.STATUS_INCLUDE, DT_IncludeLayers, DICT_PUID_and_assoc_area_m2))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error intersecting the INCLUDE layers.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error intersecting the INCLUDE layers.", "");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully intersected all INCLUDE layers (if any were present)."), true, ++val);
                }

                if (!await IntersectConstraintLayers(PRZLayerNames.STATUS_EXCLUDE, DT_ExcludeLayers, DICT_PUID_and_assoc_area_m2))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error intersecting the EXCLUDE layers.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error intersecting the EXCLUDE layers.", "");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully disoolved all EXCLUDE layers (if any were present)."), true, ++val);
                }

                #endregion

                #region UPDATE QUICKSTATUS AND CONFLICT FIELDS

                Dictionary<int, int> DICT_PUID_and_QuickStatus = new Dictionary<int, int>();
                Dictionary<int, int> DICT_PUID_and_Conflict = new Dictionary<int, int>();

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Calculating Status Conflicts and QuickStatus"), true, ++val);

                try
                {
                    await QueuedTask.Run(async () =>
                    {
                        using (Table table = await PRZH.GetStatusInfoTable())
                        {
                            TableDefinition tDef = table.GetDefinition();

                            // Get list of INCLUDE layer Area fields
                            var INAreaFields = tDef.GetFields().Where(f => f.Name.StartsWith("IN") && f.Name.EndsWith("_Area")).ToList();

                            // Get list of EXCLUDE layer Area fields
                            var EXAreaFields = tDef.GetFields().Where(f => f.Name.StartsWith("EX") && f.Name.EndsWith("_Area")).ToList();

                            using (RowCursor rowCursor = table.Search(null, false))
                            {
                                while (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        int puid = (int)row[PRZC.c_FLD_PUFC_ID];

                                        bool hasIN = false;
                                        bool hasEX = false;

                                        // Determine if there are any INCLUDE area fields having values > 0 for this PU ID
                                        foreach (Field fld in INAreaFields)
                                        {
                                            double test = (double)row[fld.Name];
                                            if (test > 0)
                                            {
                                                hasIN = true;
                                            }
                                        }

                                        // Determine if there are any EXCLUDE area fields having values > 0 for this PU ID
                                        foreach (Field fld in EXAreaFields)
                                        {
                                            double test = (double)row[fld.Name];
                                            if (test > 0)
                                            {
                                                hasEX = true;
                                            }
                                        }

                                        // Update the QuickStatus and Conflict information

                                        // Both INCLUDE and EXCLUDE occur in PU:
                                        if (hasIN && hasEX)
                                        {
                                            // Flag as a conflicted planning unit
                                            row[PRZC.c_FLD_STATUSINFO_CONFLICT] = 1;
                                            DICT_PUID_and_Conflict.Add(puid, 1);

                                            // Set the 'winning' QuickStatus based on user-specified setting
                                            if (SelectedOverrideOption == c_OVERRIDE_INCLUDE)
                                            {
                                                row[PRZC.c_FLD_STATUSINFO_QUICKSTATUS] = 2;
                                                DICT_PUID_and_QuickStatus.Add(puid, 2);
                                            }
                                            else if (SelectedOverrideOption == c_OVERRIDE_EXCLUDE)
                                            {
                                                row[PRZC.c_FLD_STATUSINFO_QUICKSTATUS] = 3;
                                                DICT_PUID_and_QuickStatus.Add(puid, 3);
                                            }

                                        }

                                        // INCLUDE only:
                                        else if (hasIN)
                                        {
                                            // Flag as a no-conflict planning unit
                                            row[PRZC.c_FLD_STATUSINFO_CONFLICT] = 0;
                                            DICT_PUID_and_Conflict.Add(puid, 0);

                                            // Set the Status
                                            row[PRZC.c_FLD_STATUSINFO_QUICKSTATUS] = 2;
                                            DICT_PUID_and_QuickStatus.Add(puid, 2);
                                        }

                                        // EXCLUDE only:
                                        else if (hasEX)
                                        {
                                            // Flag as a no-conflict planning unit
                                            row[PRZC.c_FLD_STATUSINFO_CONFLICT] = 0;
                                            DICT_PUID_and_Conflict.Add(puid, 0);

                                            // Set the Status
                                            row[PRZC.c_FLD_STATUSINFO_QUICKSTATUS] = 3;
                                            DICT_PUID_and_QuickStatus.Add(puid, 3);
                                        }

                                        // Neither:
                                        else
                                        {
                                            // Flag as a no-conflict planning unit
                                            row[PRZC.c_FLD_STATUSINFO_CONFLICT] = 0;
                                            DICT_PUID_and_Conflict.Add(puid, 0);

                                            // Set the Status
                                            row[PRZC.c_FLD_STATUSINFO_QUICKSTATUS] = 0;
                                            DICT_PUID_and_QuickStatus.Add(puid, 0);
                                        }

                                        // update the row
                                        row.Store();
                                    }
                                }
                            }
                        }

                    });
                }
                catch (Exception ex)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error updating the Status Info Quickstatus and Conflict fields.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error updating Status Info Table quickstatus and conflict fields" + Environment.NewLine + Environment.NewLine + ex.Message, "");
                    return false;
                }

                #endregion

                #region UPDATE PLANNING UNIT FC QUICKSTATUS AND CONFLICT COLUMNS

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Updating Planning Unit FC Status Column"), true, ++val);

                try
                {
                    await QueuedTask.Run(async () =>
                    {
                        using (Table table = await PRZH.GetPlanningUnitFC())    // Get the Planning Unit FC attribute table
                        using (RowCursor rowCursor = table.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int puid = (int)row[PRZC.c_FLD_PUFC_ID];

                                    if (DICT_PUID_and_QuickStatus.ContainsKey(puid))
                                    {
                                        row[PRZC.c_FLD_PUFC_STATUS] = DICT_PUID_and_QuickStatus[puid];
                                    }
                                    else
                                    {
                                        row[PRZC.c_FLD_PUFC_STATUS] = -1;
                                    }

                                    if (DICT_PUID_and_Conflict.ContainsKey(puid))
                                    {
                                        row[PRZC.c_FLD_PUFC_CONFLICT] = DICT_PUID_and_Conflict[puid];
                                    }
                                    else
                                    {
                                        row[PRZC.c_FLD_PUFC_CONFLICT] = -1;
                                    }

                                    row.Store();
                                }
                            }
                        }

                    });
                }
                catch (Exception ex)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error updating the Status Info Quickstatus and Conflict fields.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error updating Status Info Table quickstatus and conflict fields" + Environment.NewLine + Environment.NewLine + ex.Message, "");
                    return false;
                }

                #endregion

                #region WRAP THINGS UP

                // Populate the Grid
                bool Populated = await PopulateConflictGrid();

                // Compact the Geodatabase
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacting the geodatabase..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath);
                toolOutput = await PRZH.RunGPTool("Compact_management", toolParams, null, GPExecuteToolFlags.None);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error compacting the geodatabase. GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Refresh the Map & TOC
                if (!await PRZM.ValidatePRZGroupLayers())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error validating PRZ layers...", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Wrap things up
                stopwatch.Stop();
                string message = PRZH.GetElapsedTimeMessage(stopwatch.Elapsed);
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Construction completed successfully!"), true, 1, 1);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(message), true, 1, 1);

                ProMsgBox.Show("Construction Completed Sucessfully!" + Environment.NewLine + Environment.NewLine + message);

                return true;

                #endregion
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                return false;
            }
            finally
            {
                if (cps != null)
                    cps.Dispose();
            }
        }

        private async Task<bool> IntersectConstraintLayers(PRZLayerNames layer, DataTable DT, Dictionary<int, double> DICT_PUID_area)
        {
            try
            {
                var success = await QueuedTask.Run(async () =>
                {
                    Map map = MapView.Active.Map;

                    // Some GP variables
                    IReadOnlyList<string> toolParams;
                    IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                    GPExecuteToolFlags flags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                    string toolOutput;

                    // some paths
                    string gdbpath = PRZH.GetProjectGDBPath();
                    string pufcpath = PRZH.GetPlanningUnitFCPath();
                    string sipath = PRZH.GetStatusInfoTablePath();

                    // some other stuff
                    FeatureLayer PUFL = PRZH.GetFeatureLayer_PU(map);
                    PUFL.ClearSelection();  // we don't want selected features only, we want all of them

                    List<FeatureLayer> LIST_FL = null;
                    string group = "";
                    string prefix = "";

                    switch (layer)
                    {
                        case PRZLayerNames.STATUS_INCLUDE:
                            LIST_FL = PRZH.GetFeatureLayers_STATUS_INCLUDE(map);
                            group = "INCLUDE";
                            prefix = "IN";
                            break;
                        case PRZLayerNames.STATUS_EXCLUDE:
                            LIST_FL = PRZH.GetFeatureLayers_STATUS_EXCLUDE(map);
                            group = "EXCLUDE";
                            prefix = "EX";
                            break;
                        default:
                            return false;
                    }

                    foreach (DataRow DR in DT.Rows)
                    {
                        FeatureLayer FL = (FeatureLayer)DR[PRZC.c_FLD_DATATABLE_STATUS_LAYER];
                        FL.ClearSelection();    // get rid of any selection on this layer

                        int layer_index = (int)DR[PRZC.c_FLD_DATATABLE_STATUS_INDEX];
                        string layer_name = DR[PRZC.c_FLD_DATATABLE_STATUS_NAME].ToString();
                        int layer_status = (int)DR[PRZC.c_FLD_DATATABLE_STATUS_STATUS];
                        double threshold_double = (double)DR[PRZC.c_FLD_DATATABLE_STATUS_THRESHOLD];
                        int layer_number = layer_index + 1;

                        string intersect_fc_name = prefix + layer_number.ToString() + "_Prelim1_Int";
                        string intersect_fc_path = Path.Combine(gdbpath, intersect_fc_name);

                        // Construct the inputs value array
                        object[] a = { PUFL, 1 };   // prelim array -> combine the layer object and the Rank (PU layer)
                        object[] b = { FL, 2 };     // prelim array -> combine the layer object and the Rank (layer from DT)

                        var a2 = Geoprocessing.MakeValueArray(a);   // Let this method figure out how best to quote the layer info
                        var b2 = Geoprocessing.MakeValueArray(b);   // Let this method figure out how best to quote the layer info

                        string inputs_string = String.Join(" ", a2) + ";" + String.Join(" ", b2);   // my final inputs string

                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Intersecting " + group + " layer " + layer_number.ToString() + ": " + layer_name), true);
                        toolParams = Geoprocessing.MakeValueArray(inputs_string, intersect_fc_path, "ALL", "", "INPUT");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);                        
                        toolOutput = await PRZH.RunGPTool("Intersect_analysis", toolParams, toolEnvs, flags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Error intersecting " + group + " layer " + layer_number.ToString() + ".  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true);
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Intersect was successful for " + group + " layer " + layer_number.ToString() + "."), true);
                        }

                        // Now dissolve the temp intersect layer on PUID
                        string dissolve_fc_name = prefix + layer_number.ToString() + "_Prelim2_Dslv";
                        string dissolve_fc_path = Path.Combine(gdbpath, dissolve_fc_name);

                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Dissolving on Planning Unit ID..."), true);
                        toolParams = Geoprocessing.MakeValueArray(intersect_fc_path, dissolve_fc_path, PRZC.c_FLD_PUFC_ID, "", "MULTI_PART", "");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("Dissolve_management", toolParams, toolEnvs, flags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Error dissolving " + intersect_fc_name + ".  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true);
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog(intersect_fc_name + " was dissolved successfully."), true);
                        }

                        // Pass through the Dissolve Layer, and retrieve the PUID and area for each feature (store in DICT)
                        Dictionary<int, double> DICT_PUID_and_dissolved_area = new Dictionary<int, double>();

                        // get the puids and areas from the dissolved features first
                        using (Geodatabase gdb = await PRZH.GetProjectGDB())
                        using (FeatureClass fc = await PRZH.GetFeatureClass(gdb, dissolve_fc_name))
                        {
                            if (fc == null)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to locate dissolve output: " + dissolve_fc_name, LogMessageType.ERROR), true);
                                return false;
                            }

                            using (RowCursor rowCursor = fc.Search(null, false))
                            {
                                while (rowCursor.MoveNext())
                                {
                                    using (Feature feature = (Feature)rowCursor.Current)
                                    {
                                        // set up variables
                                        int puid;
                                        double area_m;

                                        // get the planning unit id
                                        puid = (int)feature[PRZC.c_FLD_PUFC_ID];

                                        // get the area
                                        Polygon poly = (Polygon)feature.GetShape();
                                        area_m = poly.Area;

                                        DICT_PUID_and_dissolved_area.Add(puid, area_m);
                                    }
                                }
                            }
                        }

                        // Now write this puid and area information into the Status Info table
                        using (Table table = await PRZH.GetStatusInfoTable())
                        {
                            QueryFilter QF = new QueryFilter();
                            QF.SubFields = "*";

                            foreach (KeyValuePair<int, double> kvp in DICT_PUID_and_dissolved_area)
                            {
                                int puid = kvp.Key;
                                double area_constraint_m = kvp.Value;
                                double area_planning_unit_m = DICT_PUID_area[puid];
                                double percent_constrained = area_constraint_m / area_planning_unit_m;
                                string area_field = prefix + layer_number.ToString() + "_Area";

                                QF.WhereClause = PRZC.c_FLD_PUFC_ID + " = " + kvp.Key;

                                if (percent_constrained >= threshold_double)
                                {
                                    using (RowCursor rowCursor = table.Search(QF, false))
                                    {
                                        while (rowCursor.MoveNext())
                                        {
                                            using (Row row = rowCursor.Current)
                                            {
                                                row[area_field] = area_constraint_m;
                                                row.Store();
                                            }
                                        }

                                    }
                                }
                            }
                        }

                        // Finally, delete the two temp feature classes
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting temporary feature classes..."), true);

                        object[] e = { intersect_fc_path, dissolve_fc_path };
                        var e2 = Geoprocessing.MakeValueArray(e);   // Let this method figure out how best to quote the paths
                        string inputs2= String.Join(";", e2);
                        toolParams = Geoprocessing.MakeValueArray(inputs2, "");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, flags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting temp feature classes.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true);
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Temp Feature Classes deleted successfully."), true);
                        }
                    }

                    return true;

                });

                return success;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true);
                return false;
            }
        }

        private async Task<bool> AddLayerFields(PRZLayerNames layer, DataTable DT)
        {
            try
            {
                Map map = MapView.Active.Map;

                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                string toolOutput;

                string gdbpath = PRZH.GetProjectGDBPath();
                string sipath = PRZH.GetStatusInfoTablePath();

                List<FeatureLayer> LIST_FL = null;
                string group = "";
                string prefix = "";

                switch (layer)
                {
                    case PRZLayerNames.STATUS_INCLUDE:
                        LIST_FL = PRZH.GetFeatureLayers_STATUS_INCLUDE(map);
                        group = "INCLUDE";
                        prefix = "IN";
                        break;
                    case PRZLayerNames.STATUS_EXCLUDE:
                        LIST_FL = PRZH.GetFeatureLayers_STATUS_EXCLUDE(map);
                        group = "EXCLUDE";
                        prefix = "EX";
                        break;
                    default:
                        return false;
                }

                foreach (DataRow DR in DT.Rows)
                {
                    int layer_index = (int)DR[PRZC.c_FLD_DATATABLE_STATUS_INDEX];
                    string layer_name = DR[PRZC.c_FLD_DATATABLE_STATUS_NAME].ToString();
                    //int layer_status = (int)DR[PRZC.c_FLD_DATATABLE_STATUS_STATUS];
                    double threshold_double = (double)DR[PRZC.c_FLD_DATATABLE_STATUS_THRESHOLD];
                    int layer_number = layer_index + 1; // not sure why I need this?  maybe zeros aren't cool as the first layer?
                    string layer_name_75 = (layer_name.Length > 75) ? layer_name.Substring(0, 75) : layer_name;

                    // Add all the fields for this layer
                    string fldName = prefix + layer_number.ToString() + "_Name";
                    string fldNameAlias = prefix + " " + layer_number.ToString() + " Name";
                    string fld1 = fldName + " TEXT '" + fldNameAlias + "'  75 # #;";

                    string fldStatus = prefix + layer_number.ToString() + "_Status";
                    string fldStatusAlias = prefix + " " + layer_number.ToString() + " Status";
                    string fld2 = fldStatus + " LONG '" + fldStatusAlias + "' # # #;";

                    string fldArea = prefix + layer_number.ToString() + "_Area";
                    string fldAreaAlias = prefix + " " + layer_number.ToString() + " Area (m2)";
                    string fld3 = fldArea + " DOUBLE '" + fldAreaAlias + "' # # #;";

                    string fldThreshold = prefix + layer_number.ToString() + "_Threshold";
                    string fldThresholdAlias = prefix + " " + layer_number.ToString() + " Threshold";
                    string fld4 = fldThreshold + " DOUBLE '" + fldThresholdAlias + "' # # #;";

                    string flds = fld1 + fld2 + fld3 + fld4;

                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Adding " + group + " Layer fields to Status Info Table..."), true);
                    toolParams = Geoprocessing.MakeValueArray(sipath, flds);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding Layer fields to Status Info Table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true);
                        return false;
                    }

                    // Now Calculate these fields
                    await QueuedTask.Run(async () =>
                    {
                        using (Geodatabase gdb = await PRZH.GetProjectGDB())
                        using (Table table = await PRZH.GetStatusInfoTable())
                        using (RowCursor rowCursor = table.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    // set the name
                                    row[fldName] = layer_name_75;

                                    // set the status
                                    if (prefix == "IN")
                                    {
                                        row[fldStatus] = 2;
                                    }
                                    else if (prefix == "EX")
                                    {
                                        row[fldStatus] = 3;
                                    }
                                    else
                                    {
                                        row[fldStatus] = 0;
                                    }

                                    // set the area to 0
                                    row[fldArea] = 0;

                                    // set the layer threshold
                                    row[fldThreshold] = threshold_double;

                                    row.Store();
                                }
                            }
                        }
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private async Task<bool> PopulateLayerTable(PRZLayerNames layer, DataTable DT)
        {
            try
            {
                Map map = MapView.Active.Map;

                List<FeatureLayer> LIST_FL = null;
                string group = "";
                int status_val;

                switch (layer)
                {
                    case PRZLayerNames.STATUS_INCLUDE:
                        LIST_FL = PRZH.GetFeatureLayers_STATUS_INCLUDE(map);
                        status_val = 2;
                        group = "INCLUDE";
                        break;
                    case PRZLayerNames.STATUS_EXCLUDE:
                        LIST_FL = PRZH.GetFeatureLayers_STATUS_EXCLUDE(map);
                        status_val = 3;
                        group = "EXCLUDE";
                        break;
                    default:
                        return false;
                }

                for (int i = 0; i < LIST_FL.Count; i++) // if the list has no members, this whole for loop will be skipped and we'll return true, which is good.
                {
                    // VALIDATE THE FEATURE LAYER
                    FeatureLayer FL = LIST_FL[i];

                    // Make sure the layer has source data and is not an invalid layer
                    if (!await QueuedTask.Run(() =>
                    {
                        FeatureClass FC = FL.GetFeatureClass();
                        bool exists = FC != null;       // if the FL has a valid source, FC will not be null.  If the FL doesn't, FC will be null
                        return exists;                  // return true = FC exists, false = FC doesn't exist.
                    }))
                    {
                        if (ProMsgBox.Show("The Feature Layer '" + FL.Name + "' has no Data Source.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                            group + " Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                            == System.Windows.MessageBoxResult.Cancel)
                        {
                            return false;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    // Make sure the layer has a valid spatial reference
                    if (await QueuedTask.Run(() =>
                    {
                        SpatialReference SR = FL.GetSpatialReference();
                        return (SR == null || SR.IsUnknown);        // return true = invalid SR, or false = valid SR
                    }))
                    {
                        if (ProMsgBox.Show("The Feature Layer '" + FL.Name + "' has a NULL or UNKNOWN Spatial Reference.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                            group + " Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                            == System.Windows.MessageBoxResult.Cancel)
                        {
                            return false;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    // Make sure the layer is a polygon layer
                    if (FL.ShapeType != esriGeometryType.esriGeometryPolygon)
                    {
                        if (ProMsgBox.Show("The Feature Layer '" + FL.Name + "' is NOT a Polygon Feature Layer.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                            group + " Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                            == System.Windows.MessageBoxResult.Cancel)
                        {
                            return false;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    // NOW CHECK THE LAYER NAME FOR A USER-SUPPLIED THRESHOLD
                    string original_layer_name = FL.Name;
                    string layer_name;
                    int threshold_int;

                    //string pattern_start = @"^\[\d{1,3}\]"; // start of string
                    //string pattern_end = @"$\[\d{1,3}\]";   // end of string
                    string pattern = @"\[\d{1,3}\]";        // anywhere in string

                    Regex regex = new Regex(pattern);
                    Match match = regex.Match(original_layer_name);

                    if (match.Success)
                    {
                        string matched_pattern = match.Value;   // match.Value is the [n], [nn], or [nnn] substring includng the square brackets
                        //layer_name = original_layer_name.Substring(matched_pattern.Length).Trim();  // layer name minus the [n], [nn], or [nnn] substring
                        layer_name = original_layer_name.Replace(matched_pattern, "");  // layer name minus the [n], [nn], or [nnn] substring
                        string threshold_text = matched_pattern.Replace("[", "").Replace("]", "");  // leaves just the 1, 2, or 3 numeric digits, no more brackets

                        threshold_int = int.Parse(threshold_text);  // integer value

                        if (threshold_int < 0 | threshold_int > 100)
                        {
                            string message = "An invalid threshold of " + threshold_int.ToString() + " has been specified for:" +
                                             Environment.NewLine + Environment.NewLine +
                                             "Layer: " + original_layer_name + Environment.NewLine +
                                             "Group Layer: " + group + Environment.NewLine + Environment.NewLine +
                                             "Threshold must be in the range 0 to 100." + Environment.NewLine + Environment.NewLine +
                                             "Click OK to skip this layer and continue, or click CANCEL to quit";
                            
                            if (ProMsgBox.Show(message, group + " Layer Validation", System.Windows.MessageBoxButton.OKCancel,
                                                System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK) 
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return false;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // check the name length
                        if (layer_name.Length == 0)
                        {
                            string message = "Layer '" + original_layer_name + "' has a zero-length name once the threshold value is removed." +
                                             Environment.NewLine + Environment.NewLine +
                                             "Click OK to skip this layer and continue, or click CANCEL to quit";

                            if (ProMsgBox.Show(message, group + " Layer Validation", System.Windows.MessageBoxButton.OKCancel,
                                                System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return false;
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                    else
                    {
                        layer_name = original_layer_name;

                        // check the name length
                        if (layer_name.Length == 0)
                        {
                            string message = "Layer '" + original_layer_name + "' has a zero-length name." +
                                             Environment.NewLine + Environment.NewLine +
                                             "Click OK to skip this layer and continue, or click CANCEL to quit";

                            if (ProMsgBox.Show(message, group + " Layer Validation", System.Windows.MessageBoxButton.OKCancel,
                                                System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return false;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // get the default threshold for this layer
                        threshold_int = int.Parse(Properties.Settings.Default.DEFAULT_STATUS_THRESHOLD);   // use default value
                    }

                    double threshold_double = threshold_int / 100.0;    // convert threshold to a double between 0 and 1 inclusive

                    // ADD ROW TO DATATABLE
                    DataRow DR = DT.NewRow();
                    DR[PRZC.c_FLD_DATATABLE_STATUS_LAYER] = FL;
                    DR[PRZC.c_FLD_DATATABLE_STATUS_INDEX] = i;
                    DR[PRZC.c_FLD_DATATABLE_STATUS_NAME] = layer_name;
                    DR[PRZC.c_FLD_DATATABLE_STATUS_THRESHOLD] = threshold_double;
                    DR[PRZC.c_FLD_DATATABLE_STATUS_STATUS] = status_val;

                    DT.Rows.Add(DR);
                }

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private async Task<bool> ConstraintDoubleClick()
        {
            try
            {

                if (SelectedConflict != null)
                {
                    ProMsgBox.Show("TODO: Select the planning units associated with this conflict");

                    string IncludeLayer = SelectedConflict.include_layer_name;
                    string ExcludeLayer = SelectedConflict.exclude_layer_name;
                    

                    ProMsgBox.Show("Included: " + IncludeLayer + Environment.NewLine + "Excluded: " + ExcludeLayer);

                    // select all rows from the Status Info table where these fields have >0 areas
                    // TODO: Finish this!
                }



                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private async Task<bool> DeleteStatusInfoTable()
        {
            int val = 0;

            try
            {
                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                string toolOutput;

                // Initialize ProgressBar and Progress Log
                int max = 10;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing..."), false, max, ++val);

                // Quit if table doesn't exist
                if (!await PRZH.StatusInfoTableExists())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Status Table does not exist in the Project Geodatabase."), true, ++val);
                    ProMsgBox.Show("Status Info table does not exist in the Project Geodatabase.  There's nothing to delete.");
                    return true;
                }

                // Validation: Prompt User for permission to proceed
                if (ProMsgBox.Show("If you proceed, the Planning Unit Status table will be DELETED if it exists in the Project Geodatabase." +
                   Environment.NewLine + Environment.NewLine +
                   "Additionally, the contents of the 'status' field in the Planning Unit Feature Class will be reset to 0." +
                   Environment.NewLine + Environment.NewLine +
                   "Do you wish to proceed?" +
                   Environment.NewLine + Environment.NewLine +
                   "Choose wisely...",
                   "TABLE DELETE WARNING",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out of Status Calculation."), true, ++val);
                    return false;
                }

                // Delete the Status Info Table
                string gdbpath = PRZH.GetProjectGDBPath();
                string sipath = PRZH.GetStatusInfoTablePath();

                if (await PRZH.StatusInfoTableExists())
                {
                    // Delete the existing Status Info table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting Status Info Table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(sipath, "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting the Status Info table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Status Info Table deleted successfully..."), true, ++val);
                    }
                }

                // Update the PUFC Status and Conflict fields to zero (0).
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Updating Planning Unit Feature Class, setting Status and Conflict fields to 0."), true, ++val);
                await QueuedTask.Run(async () =>
                {
                    using (FeatureClass PUFC = await PRZH.GetPlanningUnitFC())
                    using (RowCursor rowCursor = PUFC.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                row[PRZC.c_FLD_PUFC_STATUS] = 0;
                                row[PRZC.c_FLD_STATUSINFO_CONFLICT] = 0;

                                row.Store();
                            }
                        }
                    }
                });

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Status and Conflict fields updated successfully."), true, ++val);

                // Rebuild the Conflict Grid
                if (!await PopulateConflictGrid())
                {
                    ProMsgBox.Show("Error populating the Conflict Grid...");
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error populating the Conflict Grid.", LogMessageType.ERROR), true, ++val);
                    return false;
                }

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

        #region Event Handlers


        #endregion

    }
}











