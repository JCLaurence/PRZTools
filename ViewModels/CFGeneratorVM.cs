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
using System.IO;
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
using PRZM = NCC.PRZTools.PRZMethods;

namespace NCC.PRZTools
{
    public class CFGeneratorVM : PropertyChangedBase
    {
        public CFGeneratorVM()
        {
        }


        #region Properties

        private bool _cfTableExists;
        public bool CFTableExists
        {
            get => _cfTableExists;
            set => SetProperty(ref _cfTableExists, value, () => CFTableExists);
        }

        private bool _puvcfTableExists;
        public bool PUVCFTableExists
        {
            get => _puvcfTableExists;
            set => SetProperty(ref _puvcfTableExists, value, () => PUVCFTableExists);
        }

        private string _gridCaption;
        public string GridCaption
        {
            get => _gridCaption; set => SetProperty(ref _gridCaption, value, () => GridCaption);
        }

        private string _defaultThreshold = Properties.Settings.Default.DEFAULT_CF_THRESHOLD;
        public string DefaultThreshold
        {
            get => _defaultThreshold;

            set
            {
                SetProperty(ref _defaultThreshold, value, () => DefaultThreshold);
                Properties.Settings.Default.DEFAULT_CF_THRESHOLD = value;
                Properties.Settings.Default.Save();
            }
        }

        private string _defaultTarget = Properties.Settings.Default.DEFAULT_CF_TARGET;
        public string DefaultTarget
        {
            get => _defaultTarget;

            set
            {
                SetProperty(ref _defaultTarget, value, () => DefaultTarget);
                Properties.Settings.Default.DEFAULT_CF_TARGET = value;
                Properties.Settings.Default.Save();
            }
        }

        private ObservableCollection<ConservationFeature> _conservationFeatures = new ObservableCollection<ConservationFeature>();
        public ObservableCollection<ConservationFeature> ConservationFeatures
        {
            get { return _conservationFeatures; }
            set
            {
                _conservationFeatures = value;
                SetProperty(ref _conservationFeatures, value, () => ConservationFeatures);
            }
        }

        private ConservationFeature _selectedConservationFeature;
        public ConservationFeature SelectedConservationFeature
        {
            get => _selectedConservationFeature; set => SetProperty(ref _selectedConservationFeature, value, () => SelectedConservationFeature);
        }



        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""
        public ProgressManager PM
        {
            get => _pm; set => SetProperty(ref _pm, value, () => PM);
        }

        #endregion

        #region Commands

        private ICommand _cmdConstraintDoubleClick;
        public ICommand CmdGridDoubleClick => _cmdConstraintDoubleClick ?? (_cmdConstraintDoubleClick = new RelayCommand(() => GridDoubleClick(), () => true));

        private ICommand _cmdGenerateCF;
        public ICommand CmdGenerateCF => _cmdGenerateCF ?? (_cmdGenerateCF = new RelayCommand(() => GenerateCF(), () => true));

        private ICommand _cmdClearLog;
        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true));


        #endregion

        #region Methods

        public async Task OnProWinLoaded()
        {
            try
            {
                // Initialize the Progress Bar & Log
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                // checkboxes
                CFTableExists = await PRZH.CFTableExists();
                PUVCFTableExists = await PRZH.PUVCFTableExists();

                // Populate the Grid
                bool Populated = await PopulateGrid();

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> PopulateGrid()
        {
            try
            {
                // Clear the contents of the Conflicts observable collection
                ConservationFeatures.Clear();

                string caption = "";
                List<ConservationFeature> LIST_CF = new List<ConservationFeature>();

                await QueuedTask.Run(async() =>
                {
                    if (!await PRZH.CFTableExists())
                    {
                        caption = "Conservation Features Table not yet built...";
                        return;
                    }

                    using (Table table = await PRZH.GetCFTable())
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                ConservationFeature CF = new ConservationFeature();

                                CF.cf_id = (row[PRZC.c_FLD_CF_ID] == null) ? -1 : (int)row[PRZC.c_FLD_CF_ID];
                                CF.cf_name = (row[PRZC.c_FLD_CF_NAME] == null) ? "" : row[PRZC.c_FLD_CF_NAME].ToString();
                                CF.cf_whereclause = (row[PRZC.c_FLD_CF_WHERECLAUSE] == null) ? "" : row[PRZC.c_FLD_CF_WHERECLAUSE].ToString();
                                CF.cf_min_threshold_pct = (row[PRZC.c_FLD_CF_MIN_THRESHOLD_PCT] == null) ? -1 : (int)row[PRZC.c_FLD_CF_MIN_THRESHOLD_PCT];
                                CF.cf_target_pct = (row[PRZC.c_FLD_CF_TARGET_PCT] == null) ? -1 : (int)row[PRZC.c_FLD_CF_TARGET_PCT];

                                CF.lyr_name = (row[PRZC.c_FLD_CF_LYR_NAME] == null) ? "" : row[PRZC.c_FLD_CF_LYR_NAME].ToString();
                                CF.lyr_type = (row[PRZC.c_FLD_CF_LYR_TYPE] == null) ? "" : row[PRZC.c_FLD_CF_LYR_TYPE].ToString();
                                CF.lyr_json = (row[PRZC.c_FLD_CF_LYR_JSON] == null) ? "" : row[PRZC.c_FLD_CF_LYR_JSON].ToString();

                                object o = row[PRZC.c_FLD_CF_IN_USE];
                                bool? b;

                                if (o == null)
                                    b = null;
                                else if (o.ToString() == "Yes")
                                    b = true;
                                else
                                    b = false;

                                CF.cf_in_use = b;

                                LIST_CF.Add(CF);
                            }
                        }

                        int c = table.GetCount();
                        caption = "Conservation Features Table: (" + ((c == 1) ? "1 Record)" : c.ToString() + " Records)");
                    }
                });

                GridCaption = caption;

                // Sort them
                LIST_CF.Sort((x, y) => x.cf_id.CompareTo(y.cf_id));

                foreach (ConservationFeature cf in LIST_CF)
                {
                    ConservationFeatures.Add(cf);
                }
                NotifyPropertyChanged(() => ConservationFeatures);



                // CF Table Exists - retrieve data from it

                //Dictionary<int, string> DICT_IN = new Dictionary<int, string>();    // Dictionary where key = Area Column Indexes, value = IN Constraint Layer to which it applies
                //Dictionary<int, string> DICT_EX = new Dictionary<int, string>();    // Dictionary where key = Area Column Indexes, value = EX Constraint Layer to which it applies

                //List<Field> fields = null;  // List of Planning Unit Status table fields

                //// Populate the Dictionaries
                //await QueuedTask.Run(async () =>
                //{
                //    using (Table table = await PRZH.GetStatusInfoTable())
                //    using (RowCursor rowCursor = table.Search(null, false))
                //    {
                //        TableDefinition tDef = table.GetDefinition();
                //        fields = tDef.GetFields().ToList();

                //        // Get the first row (I only need one row)
                //        if (rowCursor.MoveNext())
                //        {
                //            using (Row row = rowCursor.Current)
                //            {
                //                // Now, get field info and row value info that's present in all rows exactly the same (that's why I only need one row)
                //                for (int i = 0; i < fields.Count; i++)
                //                {
                //                    Field field = fields[i];

                //                    if (field.Name.EndsWith("_Name"))
                //                    {
                //                        string constraint_name = row[i].ToString();         // this is the name of the constraint layer to which columns i to i+2 apply

                //                        if (field.Name.StartsWith("IN"))
                //                        {
                //                            DICT_IN.Add(i + 2, constraint_name);    // i + 2 is the Area field, two columns to the right of the Name field
                //                        }
                //                        else if (field.Name.StartsWith("EX"))
                //                        {
                //                            DICT_EX.Add(i + 2, constraint_name);    // i + 2 is the Area field, two columns to the right of the Name field
                //                        }
                //                    }
                //                }
                //            }
                //        }
                //    }
                //});

                //// Build a List of Unique Combinations of IN and EX Layers
                //string c_ConflictNumber = "CONFLICT";
                //string c_LayerName_Include = "'INCLUDE' Constraint";
                //string c_LayerName_Exclude = "'EXCLUDE' Constraint";
                //string c_PUCount = "PLANNING UNIT COUNT";
                //string c_AreaFieldIndex_Include = "IndexIN";
                //string c_AreaFieldIndex_Exclude = "IndexEX";
                //string c_ConflictExists = "Exists";

                //DataTable DT = new DataTable();
                //DT.Columns.Add(c_ConflictNumber, Type.GetType("System.Int32"));
                //DT.Columns.Add(c_LayerName_Include, Type.GetType("System.String"));
                //DT.Columns.Add(c_LayerName_Exclude, Type.GetType("System.String"));
                //DT.Columns.Add(c_PUCount, Type.GetType("System.Int32"));
                //DT.Columns.Add(c_AreaFieldIndex_Include, Type.GetType("System.Int32"));
                //DT.Columns.Add(c_AreaFieldIndex_Exclude, Type.GetType("System.Int32"));
                //DT.Columns.Add(c_ConflictExists, Type.GetType("System.Boolean"));

                //foreach (int IN_AreaFieldIndex in DICT_IN.Keys)
                //{
                //    string IN_LayerName = DICT_IN[IN_AreaFieldIndex];

                //    foreach (int EX_AreaFieldIndex in DICT_EX.Keys)
                //    {
                //        string EX_LayerName = DICT_EX[EX_AreaFieldIndex];

                //        DataRow DR = DT.NewRow();

                //        DR[c_LayerName_Include] = IN_LayerName;
                //        DR[c_LayerName_Exclude] = EX_LayerName;
                //        DR[c_PUCount] = 0;
                //        DR[c_AreaFieldIndex_Include] = IN_AreaFieldIndex;
                //        DR[c_AreaFieldIndex_Exclude] = EX_AreaFieldIndex;
                //        DR[c_ConflictExists] = false;
                //        DT.Rows.Add(DR);
                //    }
                //}

                //// For each row in DataTable, query PU Status for pairs having area>0 in both IN and EX
                //int conflict_number = 1;
                //int IN_AreaField_Index;
                //int EX_AreaField_Index;
                //string IN_AreaField_Name = "";
                //string EX_AreaField_Name = "";

                //foreach (DataRow DR in DT.Rows)
                //{
                //    IN_AreaField_Index = (int)DR[c_AreaFieldIndex_Include];
                //    EX_AreaField_Index = (int)DR[c_AreaFieldIndex_Exclude];

                //    IN_AreaField_Name = fields[IN_AreaField_Index].Name;
                //    EX_AreaField_Name = fields[EX_AreaField_Index].Name;

                //    string where_clause = IN_AreaField_Name + @" > 0 And " + EX_AreaField_Name + @" > 0";

                //    QueryFilter QF = new QueryFilter();
                //    QF.SubFields = IN_AreaField_Name + "," + EX_AreaField_Name;
                //    QF.WhereClause = where_clause;

                //    int row_count = 0;

                //    await QueuedTask.Run(async () =>
                //    {
                //        using (Table table = await PRZH.GetStatusInfoTable())
                //        {
                //            row_count = table.GetCount(QF);
                //        }
                //    });

                //    if (row_count > 0)
                //    {
                //        DR[c_ConflictNumber] = conflict_number++;
                //        DR[c_PUCount] = row_count;
                //        DR[c_ConflictExists] = true;
                //    }
                //}


                //// Filter out only those DataRows where conflict exists
                //DataView DV = DT.DefaultView;
                //DV.RowFilter = c_ConflictExists + " = true";

                //// Finally, populate the Observable Collection

                //List<StatusConflict> l = new List<StatusConflict>();
                //foreach (DataRowView DRV in DV)
                //{
                //    StatusConflict sc = new StatusConflict();

                //    sc.include_layer_name = DRV[c_LayerName_Include].ToString();
                //    sc.include_area_field_index = (int)DRV[c_AreaFieldIndex_Include];
                //    sc.exclude_layer_name = DRV[c_LayerName_Exclude].ToString();
                //    sc.exclude_area_field_index = (int)DRV[c_AreaFieldIndex_Exclude];
                //    sc.conflict_num = (int)DRV[c_ConflictNumber];
                //    sc.pu_count = (int)DRV[c_PUCount];

                //    l.Add(sc);
                //}

                //// Sort them
                //l.Sort((x, y) => x.conflict_num.CompareTo(y.conflict_num));

                //// Set the property
                //_conflicts = new ObservableCollection<StatusConflict>(l);
                //NotifyPropertyChanged(() => Conflicts);

                //int count = DV.Count;

                //ConflictGridCaption = "Planning Unit Status Conflict Listing (" + ((count == 1) ? "1 conflict)" : count.ToString() + " conflicts)");


                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true);
                return false;
            }
        }

        private async Task<bool> GenerateCF()
        {
            int val = 0;

            try
            {
                #region INITIALIZATION AND USER INPUT VALIDATION

                // Initialize a few thingies
                Map map = MapView.Active.Map;

                // Initialize ProgressBar and Progress Log
                int max = 50;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the CF Table Generator..."), false, max, ++val);

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

                // Validation: Ensure that the Planning Unit Feature Layer exists
                if (!PRZH.FeatureLayerExists_PU(map))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Layer not found in the map.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Planning Unit Feature Layer not present in the map.  Please reload PRZ layers");
                    return false;
                }

                // Validation: Ensure the Default Threshold is valid
                string threshold_text = string.IsNullOrEmpty(DefaultThreshold) ? "0" : ((DefaultThreshold.Trim() == "") ? "0" : DefaultThreshold.Trim());

                if (!double.TryParse(threshold_text, out double threshold_double))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Default Threshold value", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Default Threshold value.  Value must be a number between 0 and 100 (inclusive)", "Validation");
                    return false;
                }
                else if (threshold_double < 0 | threshold_double > 100)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Default Threshold value", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Default Threshold value.  Value must be a number between 0 and 100 (inclusive)", "Validation");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Default Threshold = " + threshold_text), true, ++val);
                }

                // Validation: Ensure the Default Target is valid
                string target_text = string.IsNullOrEmpty(DefaultTarget) ? "0" : ((DefaultTarget.Trim() == "") ? "0" : DefaultTarget.Trim());

                if (!double.TryParse(target_text, out double target_double))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Default Target value", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Default Target value.  Value must be a number between 0 and 100 (inclusive)", "Validation");
                    return false;
                }
                else if (target_double < 0 | target_double > 100)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Default Target value", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Default Target value.  Value must be a number between 0 and 100 (inclusive)", "Validation");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Default Target = " + target_text), true, ++val);
                }

                // Validation: Ensure the Conservation Features group layer is present
                if (!PRZH.PRZLayerExists(map, PRZLayerNames.CF))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> CF Group Layer is missing.  Please reload PRZ layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("CF Group Layer is missing.  Please reload the PRZ Layers and try again.", "Validation");
                    return false;
                }

                // Validation: Ensure that at least 1 FL or RL is present within the CF Group Layer
                var FLs = PRZH.GetFeatureLayers_CF(map);
                var RLs = PRZH.GetRasterLayers_CF(map);

                if (FLs is null || RLs is null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Unable to retrieve contents of Conservation Features Group Layer.  Please reload PRZ layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Unable to retrieve contents of Conservation Features Group Layer.  Please reload the PRZ Layers and try again.", "Validation");
                    return false;
                }

                if (FLs.Count == 0 && RLs.Count == 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> No Feature or Raster Layers found within Conservation Feature Group Layer.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("There must be at least one Feature Layer or Raster Layer within the Conservation Features Group Layer.", "Validation");
                    return false;
                }

                // Validation: Prompt User for permission to proceed
                if (ProMsgBox.Show("If you proceed, the CF and PUvCF tables will be overwritten if they exist in the Project Geodatabase." +
                   Environment.NewLine + Environment.NewLine +
                   "Do you wish to proceed?" +
                   Environment.NewLine + Environment.NewLine +
                   "Choose wisely...",
                   "TABLE OVERWRITE WARNING",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out of Conservation Feature Generation."), true, ++val);
                    return false;
                }

                #endregion

                #region RETRIEVE CONSERVATION FEATURES FROM THE CF GROUPLAYER

                // Create the empty list of future Conservation Features
                List<ConservationFeature> LIST_CF = new List<ConservationFeature>();

                if (!await GetConservationFeaturesFromLayers(LIST_CF))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error retrieving Conservation Features from Layers", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Unable to construct the Conservation Features DataTable");
                    return false;
                }
                else if (LIST_CF.Count == 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("No valid Conservation Features layers found", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("No valid Conservation Feature layers found", "Validation");
                    return false;
                }

                #endregion

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                #region BUILD THE CONSERVATION FEATURES TABLE

                string cfpath = PRZH.GetCFTablePath();

                // Delete the existing CF table, if it exists

                if (await PRZH.CFTableExists())
                {
                    // Delete the existing CF table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting Conservation Features Table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(cfpath, "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting the CF table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleted the existing CF Table..."), true, ++val);
                    }
                }

                // Create the table
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating Conservation Features Table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_TABLE_CF, "", "", "Conservation Features");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CreateTable_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error creating the CF table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Created the CF Table..."), true, ++val);
                }

                // Add fields to the table
                string fldCFID = PRZC.c_FLD_CF_ID + " LONG 'Conservation Feature ID' # # #;";
                string fldCFName = PRZC.c_FLD_CF_NAME + " TEXT 'Conservation Feature Name' 255 # #;";
                string fldCFThresholdPct = PRZC.c_FLD_CF_MIN_THRESHOLD_PCT + " LONG 'Min Threshold (%)' # 0 #;";
                string fldCFTargetPct = PRZC.c_FLD_CF_TARGET_PCT + " LONG 'Target (%)' # 0 #;";
                string fldCFWhereClause = PRZC.c_FLD_CF_WHERECLAUSE + " TEXT 'WHERE Clause' 1000 # #;";
                string fldCFInUse = PRZC.c_FLD_CF_IN_USE + " TEXT 'In Use' 3 'Yes' #;";
                string fldCFArea_m2 = PRZC.c_FLD_CF_AREA_M2 + " DOUBLE 'Total Area (m2)' # 0, #;";
                string fldCFArea_ac = PRZC.c_FLD_CF_AREA_AC + " DOUBLE 'Total Area (ac)' # 0, #;";
                string fldCFArea_ha = PRZC.c_FLD_CF_AREA_HA + " DOUBLE 'Total Area (ha)' # 0, #;";
                string fldCFArea_km2 = PRZC.c_FLD_CF_AREA_KM2 + " DOUBLE 'Total Area (km2)' # 0, #;";
                string fldCFPUCount = PRZC.c_FLD_CF_PUCOUNT + " LONG 'Planning Unit Count' # 0 #;";
                string fldCFLayerName = PRZC.c_FLD_CF_LYR_NAME + " TEXT 'Source Layer Name' 300 # #;";
                string fldCFLayerType = PRZC.c_FLD_CF_LYR_TYPE + " TEXT 'Source Layer Type' 50 # #;";
                string fldCFLayerJSON = PRZC.c_FLD_CF_LYR_JSON + " TEXT 'Source Layer JSON' 100000 # #;";

                string flds = fldCFID +
                              fldCFName +
                              fldCFThresholdPct +
                              fldCFTargetPct +
                              fldCFWhereClause +
                              fldCFInUse +
                              fldCFArea_m2 +
                              fldCFArea_ac +
                              fldCFArea_ha +
                              fldCFArea_km2 +
                              fldCFPUCount +
                              fldCFLayerName +
                              fldCFLayerType +
                              fldCFLayerJSON;

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Adding fields to CF table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(cfpath, flds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding fields to CF table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("CF table fields added successfully..."), true, ++val);
                }

                #endregion

                #region POPULATE THE CONSERVATION FEATURES TABLE

                // Populate CF Table from LIST_CF
                await QueuedTask.Run(async () =>
                {
                    using (Table table = await PRZH.GetCFTable())
                    using (InsertCursor insertCursor = table.CreateInsertCursor())
                    using (RowBuffer rowBuffer = table.CreateRowBuffer())
                    {
                        // Iterate through each CF
                        foreach (ConservationFeature CF in LIST_CF)
                        {
                            // Set the row values from the CF object
                            rowBuffer[PRZC.c_FLD_CF_ID] = CF.cf_id;
                            rowBuffer[PRZC.c_FLD_CF_NAME] = CF.cf_name;

                            // min threshold
                            rowBuffer[PRZC.c_FLD_CF_MIN_THRESHOLD_PCT] = (CF.cf_min_threshold_pct == -1) ? Convert.ToInt32(threshold_double) : CF.cf_min_threshold_pct;

                            // target
                            rowBuffer[PRZC.c_FLD_CF_TARGET_PCT] = (CF.cf_target_pct == -1) ? Convert.ToInt32(target_double) : CF.cf_target_pct;

                            rowBuffer[PRZC.c_FLD_CF_WHERECLAUSE] = CF.cf_whereclause;

                            // In use
                            if (!CF.cf_in_use.HasValue)
                                rowBuffer[PRZC.c_FLD_CF_IN_USE] = "";
                            else if (CF.cf_in_use == true)
                                rowBuffer[PRZC.c_FLD_CF_IN_USE] = "Yes";
                            else
                                rowBuffer[PRZC.c_FLD_CF_IN_USE] = "No";
                            
                            
                            //rowBuffer[PRZC.c_FLD_CF_AREA_M2] = CF.cf_area_m2;                       // maybe not yet
                            //rowBuffer[PRZC.c_FLD_CF_AREA_AC] = CF.cf_area_ac;                       // maybe not yet
                            //rowBuffer[PRZC.c_FLD_CF_AREA_HA] = CF.cf_area_ha;                       // maybe not yet
                            //rowBuffer[PRZC.c_FLD_CF_AREA_KM2] = CF.cf_area_km2;                     // maybe not yet
                            //rowBuffer[PRZC.c_FLD_CF_PUCOUNT] = CF.cf_pucount;                       // maybe not yet

                            rowBuffer[PRZC.c_FLD_CF_LYR_NAME] = CF.lyr_name;
                            rowBuffer[PRZC.c_FLD_CF_LYR_TYPE] = CF.lyr_type;
                            rowBuffer[PRZC.c_FLD_CF_LYR_JSON] = CF.lyr_json;

                            // Finally, insert the row
                            insertCursor.Insert(rowBuffer);
                            insertCursor.Flush();
                        }
                    }
                });

                #endregion



                //#region INTERSECT THE VARIOUS LAYERS

                //if (!await IntersectConstraintLayers(PRZLayerNames.STATUS_INCLUDE, DT_IncludeLayers, DICT_PUID_and_assoc_area_m2))
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error intersecting the INCLUDE layers.", LogMessageType.ERROR), true, ++val);
                //    ProMsgBox.Show("Error intersecting the INCLUDE layers.", "");
                //    return false;
                //}
                //else
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully intersected all INCLUDE layers (if any were present)."), true, ++val);
                //}

                //if (!await IntersectConstraintLayers(PRZLayerNames.STATUS_EXCLUDE, DT_ExcludeLayers, DICT_PUID_and_assoc_area_m2))
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error intersecting the EXCLUDE layers.", LogMessageType.ERROR), true, ++val);
                //    ProMsgBox.Show("Error intersecting the EXCLUDE layers.", "");
                //    return false;
                //}
                //else
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully disoolved all EXCLUDE layers (if any were present)."), true, ++val);
                //}

                //#endregion

                //#region UPDATE QUICKSTATUS AND CONFLICT FIELDS

                //Dictionary<int, int> DICT_PUID_and_QuickStatus = new Dictionary<int, int>();
                //Dictionary<int, int> DICT_PUID_and_Conflict = new Dictionary<int, int>();

                //PRZH.UpdateProgress(PM, PRZH.WriteLog("Calculating Status Conflicts and QuickStatus"), true, ++val);

                //try
                //{
                //    await QueuedTask.Run(async () =>
                //    {
                //        using (Table table = await PRZH.GetStatusInfoTable())
                //        {
                //            TableDefinition tDef = table.GetDefinition();

                //            // Get list of INCLUDE layer Area fields
                //            var INAreaFields = tDef.GetFields().Where(f => f.Name.StartsWith("IN") && f.Name.EndsWith("_Area")).ToList();

                //            // Get list of EXCLUDE layer Area fields
                //            var EXAreaFields = tDef.GetFields().Where(f => f.Name.StartsWith("EX") && f.Name.EndsWith("_Area")).ToList();

                //            using (RowCursor rowCursor = table.Search(null, false))
                //            {
                //                while (rowCursor.MoveNext())
                //                {
                //                    using (Row row = rowCursor.Current)
                //                    {
                //                        int puid = (int)row[PRZC.c_FLD_PUFC_ID];

                //                        bool hasIN = false;
                //                        bool hasEX = false;

                //                        // Determine if there are any INCLUDE area fields having values > 0 for this PU ID
                //                        foreach (Field fld in INAreaFields)
                //                        {
                //                            double test = (double)row[fld.Name];
                //                            if (test > 0)
                //                            {
                //                                hasIN = true;
                //                            }
                //                        }

                //                        // Determine if there are any EXCLUDE area fields having values > 0 for this PU ID
                //                        foreach (Field fld in EXAreaFields)
                //                        {
                //                            double test = (double)row[fld.Name];
                //                            if (test > 0)
                //                            {
                //                                hasEX = true;
                //                            }
                //                        }

                //                        // Update the QuickStatus and Conflict information

                //                        // Both INCLUDE and EXCLUDE occur in PU:
                //                        if (hasIN && hasEX)
                //                        {
                //                            // Flag as a conflicted planning unit
                //                            row[PRZC.c_FLD_STATUSINFO_CONFLICT] = 1;
                //                            DICT_PUID_and_Conflict.Add(puid, 1);

                //                            // Set the 'winning' QuickStatus based on user-specified setting
                //                            if (SelectedOverrideOption == c_OVERRIDE_INCLUDE)
                //                            {
                //                                row[PRZC.c_FLD_STATUSINFO_QUICKSTATUS] = 2;
                //                                DICT_PUID_and_QuickStatus.Add(puid, 2);
                //                            }
                //                            else if (SelectedOverrideOption == c_OVERRIDE_EXCLUDE)
                //                            {
                //                                row[PRZC.c_FLD_STATUSINFO_QUICKSTATUS] = 3;
                //                                DICT_PUID_and_QuickStatus.Add(puid, 3);
                //                            }

                //                        }

                //                        // INCLUDE only:
                //                        else if (hasIN)
                //                        {
                //                            // Flag as a no-conflict planning unit
                //                            row[PRZC.c_FLD_STATUSINFO_CONFLICT] = 0;
                //                            DICT_PUID_and_Conflict.Add(puid, 0);

                //                            // Set the Status
                //                            row[PRZC.c_FLD_STATUSINFO_QUICKSTATUS] = 2;
                //                            DICT_PUID_and_QuickStatus.Add(puid, 2);
                //                        }

                //                        // EXCLUDE only:
                //                        else if (hasEX)
                //                        {
                //                            // Flag as a no-conflict planning unit
                //                            row[PRZC.c_FLD_STATUSINFO_CONFLICT] = 0;
                //                            DICT_PUID_and_Conflict.Add(puid, 0);

                //                            // Set the Status
                //                            row[PRZC.c_FLD_STATUSINFO_QUICKSTATUS] = 3;
                //                            DICT_PUID_and_QuickStatus.Add(puid, 3);
                //                        }

                //                        // Neither:
                //                        else
                //                        {
                //                            // Flag as a no-conflict planning unit
                //                            row[PRZC.c_FLD_STATUSINFO_CONFLICT] = 0;
                //                            DICT_PUID_and_Conflict.Add(puid, 0);

                //                            // Set the Status
                //                            row[PRZC.c_FLD_STATUSINFO_QUICKSTATUS] = 0;
                //                            DICT_PUID_and_QuickStatus.Add(puid, 0);
                //                        }

                //                        // update the row
                //                        row.Store();
                //                    }
                //                }
                //            }
                //        }

                //    });
                //}
                //catch (Exception ex)
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error updating the Status Info Quickstatus and Conflict fields.", LogMessageType.ERROR), true, ++val);
                //    ProMsgBox.Show("Error updating Status Info Table quickstatus and conflict fields" + Environment.NewLine + Environment.NewLine + ex.Message, "");
                //    return false;
                //}

                //#endregion

                //#region UPDATE PLANNING UNIT FC QUICKSTATUS AND CONFLICT COLUMNS

                //PRZH.UpdateProgress(PM, PRZH.WriteLog("Updating Planning Unit FC Status Column"), true, ++val);

                //try
                //{
                //    await QueuedTask.Run(async () =>
                //    {
                //        using (Table table = await PRZH.GetPlanningUnitFC())    // Get the Planning Unit FC attribute table
                //        using (RowCursor rowCursor = table.Search(null, false))
                //        {
                //            while (rowCursor.MoveNext())
                //            {
                //                using (Row row = rowCursor.Current)
                //                {
                //                    int puid = (int)row[PRZC.c_FLD_PUFC_ID];

                //                    if (DICT_PUID_and_QuickStatus.ContainsKey(puid))
                //                    {
                //                        row[PRZC.c_FLD_PUFC_STATUS] = DICT_PUID_and_QuickStatus[puid];
                //                    }
                //                    else
                //                    {
                //                        row[PRZC.c_FLD_PUFC_STATUS] = -1;
                //                    }

                //                    if (DICT_PUID_and_Conflict.ContainsKey(puid))
                //                    {
                //                        row[PRZC.c_FLD_PUFC_CONFLICT] = DICT_PUID_and_Conflict[puid];
                //                    }
                //                    else
                //                    {
                //                        row[PRZC.c_FLD_PUFC_CONFLICT] = -1;
                //                    }

                //                    row.Store();
                //                }
                //            }
                //        }

                //    });
                //}
                //catch (Exception ex)
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error updating the Status Info Quickstatus and Conflict fields.", LogMessageType.ERROR), true, ++val);
                //    ProMsgBox.Show("Error updating Status Info Table quickstatus and conflict fields" + Environment.NewLine + Environment.NewLine + ex.Message, "");
                //    return false;
                //}

                //#endregion


                #region WRAP THINGS UP

                // Populate the Grid
                bool Populated = await PopulateGrid();

                // Compact the Geodatabase
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacting the geodatabase..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath);
                toolOutput = await PRZH.RunGPTool("Compact_management", toolParams, null, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error compacting the geodatabase. GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
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
        }

        private async Task<bool> GetConservationFeaturesFromLayers(List<ConservationFeature> LIST_CF)
        {
            try
            {
                Map map = MapView.Active.Map;

                int cfid = 1;
                int default_threshold_int = int.Parse(Properties.Settings.Default.DEFAULT_CF_THRESHOLD);     // retrieve default threshold value
                int default_target_int = int.Parse(Properties.Settings.Default.DEFAULT_CF_TARGET);           // retrieve default target value

                List<Layer> LIST_L = PRZH.GetLayers_CF(map);

                for (int i = 0; i < LIST_L.Count; i++)
                {
                    Layer L = LIST_L[i];

                    // Process the Layer if it is a FeatureLayer
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
                            if (ProMsgBox.Show("The Feature Layer '" + FL.Name + "' has no Data Source.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return false;
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
                            if (ProMsgBox.Show("The Feature Layer '" + FL.Name + "' has a NULL or UNKNOWN Spatial Reference.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return false;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // Ensure that FL is Polygon layer
                        if (FL.ShapeType != esriGeometryType.esriGeometryPolygon)
                        {
                            if (ProMsgBox.Show("The Feature Layer '" + FL.Name + "' is NOT a Polygon Feature Layer.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return false;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        #endregion

                        string layer_name = ""; // layer name, optionally with thresholds and/or targets excised

                        #region Layer-Level Minimum Threshold

                        // Inspect the Layer Name for a Minimum Threshold number
                        (bool ThresholdFound, int lyr_threshold_int, string layer_name_no_thresh) = PRZH.ExtractValueFromString(FL.Name, PRZC.c_REGEX_THRESHOLD_PERCENT_PATTERN_ANY);

                        // If the Layer Name contains a Threshold number...
                        if (ThresholdFound)
                        {
                            // ensure threshold is 0 to 100 inclusive
                            if (lyr_threshold_int < 0 | lyr_threshold_int > 100)
                            {
                                string message = "An invalid threshold of " + lyr_threshold_int.ToString() + " has been specified for:" +
                                                 Environment.NewLine + Environment.NewLine +
                                                 "Layer: " + FL.Name + Environment.NewLine +
                                                 "Threshold must be in the range 0 to 100." + Environment.NewLine + Environment.NewLine +
                                                 "Click OK to skip this layer and continue, or click CANCEL to quit";

                                if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
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

                            // ensure adjusted name length is not zero
                            if (layer_name_no_thresh.Length == 0)
                            {
                                string message = "Layer '" + FL.Name + "' has a zero-length name once the threshold value is removed." +
                                                 Environment.NewLine + Environment.NewLine +
                                                 "Click OK to skip this layer and continue, or click CANCEL to quit";

                                if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
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

                            // My new layer name (threshold excised)
                            layer_name = layer_name_no_thresh;
                        }

                        // Layer Name does not contain a number
                        else
                        {
                            // My layer name should remain unchanged
                            layer_name = FL.Name;

                            // check the name length
                            if (layer_name.Length == 0)
                            {
                                string message = "Layer '" + layer_name + "' has a zero-length name." +
                                                 Environment.NewLine + Environment.NewLine +
                                                 "Click OK to skip this layer and continue, or click CANCEL to quit";

                                if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
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
                            lyr_threshold_int = default_threshold_int;   // use default value
                        }

                        #endregion

                        #region Layer-Level Target

                        // Inspect the Layer Name for a Target number
                        (bool TargetFound, int lyr_target_int, string layer_name_no_tgt) = PRZH.ExtractValueFromString(layer_name, PRZC.c_REGEX_TARGET_PERCENT_PATTERN_ANY);

                        // If the Layer Name contains a Target number...
                        if (TargetFound)
                        {
                            // ensure target is 0 to 100 inclusive
                            if (lyr_target_int < 0 | lyr_target_int > 100)
                            {
                                string message = "An invalid target of " + lyr_target_int.ToString() + " has been specified for:" +
                                                 Environment.NewLine + Environment.NewLine +
                                                 "Layer: " + layer_name + Environment.NewLine +
                                                 "Target must be in the range 0 to 100." + Environment.NewLine + Environment.NewLine +
                                                 "Click OK to skip this layer and continue, or click CANCEL to quit";

                                if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
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

                            // ensure adjusted name length is not zero
                            if (layer_name_no_tgt.Length == 0)
                            {
                                string message = "Layer '" + layer_name + "' has a zero-length name once the target value is removed." +
                                                 Environment.NewLine + Environment.NewLine +
                                                 "Click OK to skip this layer and continue, or click CANCEL to quit";

                                if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
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

                            // My new layer name (target excised)
                            layer_name = layer_name_no_tgt;
                        }

                        // Layer Name does not contain a number
                        else
                        {
                            // check the name length
                            if (layer_name.Length == 0)
                            {
                                string message = "Layer '" + layer_name + "' has a zero-length name." +
                                                 Environment.NewLine + Environment.NewLine +
                                                 "Click OK to skip this layer and continue, or click CANCEL to quit";

                                if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
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

                            // get the default target for this layer
                            lyr_target_int = default_target_int;   // use default value
                        }

                        #endregion

                        // Get the JSON from the Feature Layer
                        string flJson = "";
                        await QueuedTask.Run(() =>
                        {
                            CIMBaseLayer cimbl = FL.GetDefinition();
                            flJson = cimbl.ToJson();
                        });

                        // Examine the FL Renderer to identify specific classes, each of which will be its own CF
                        // Create the List of Conservation Features to extract from UVRenderer (if applicable)
                        List<UVConservationFeature> LIST_ConservationFeaturesUV = new List<UVConservationFeature>();

                        if (!await QueuedTask.Run(() =>
                        {
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
                                                ProMsgBox.Show("PRZ Tools does not support Date fields in Unique Values Legends (yet)", "Validation Error");
                                                return false;
                                            }

                                            DICT_FieldIndex_and_category.Add(b, fcat);
                                        }
                                    }
                                }

                                // Make sure we picked up a DICT entry for each UVRend field name
                                if (UVRend.Fields.Length != DICT_FieldIndex_and_category.Count)
                                {
                                    ProMsgBox.Show($"Not all renderer fields were found within the {FL.Name} feature layer.");
                                    return false;
                                }

                                // Cycle through each Legend Group, retrieve Group heading...
                                CIMUniqueValueGroup[] UVGroups = UVRend.Groups;

                                if (UVGroups is null)
                                {
                                    ProMsgBox.Show($"Feature Layer {FL.Name} has a Unique Values Legend with no groups in it.");
                                    return false;
                                }

                                foreach (CIMUniqueValueGroup UVGroup in UVGroups)
                                {
                                    // Process the Group Heading looking for Group-level threshold and target
                                    string group_heading = ""; // group heading, optionally with thresholds and/or targets excised

                                    #region Group-Level Minimum Threshold

                                    // Inspect the Group Heading for a Minimum Threshold number
                                    (bool GroupThresholdFound, int group_threshold_int, string group_heading_no_thresh) = PRZH.ExtractValueFromString(UVGroup.Heading, PRZC.c_REGEX_THRESHOLD_PERCENT_PATTERN_ANY);

                                    // If the group heading contains a Threshold number...
                                    if (GroupThresholdFound)
                                    {
                                        // ensure threshold is 0 to 100 inclusive
                                        if (group_threshold_int < 0 | group_threshold_int > 100)
                                        {
                                            string message = "An invalid threshold of " + group_threshold_int.ToString() + " has been specified for:" +
                                                             Environment.NewLine + Environment.NewLine +
                                                             "Legend Group: " + UVGroup.Heading + " of " + Environment.NewLine + Environment.NewLine +
                                                             "Layer: " + FL.Name + Environment.NewLine +
                                                             "Threshold must be in the range 0 to 100." + Environment.NewLine + Environment.NewLine +
                                                             "Click OK to skip this group and continue, or click CANCEL to quit";

                                            if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
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

                                        // ensure adjusted heading length is not zero
                                        if (group_heading_no_thresh.Length == 0)
                                        {
                                            string message = "Legend Group '" + UVGroup.Heading + "' has a zero-length heading once the threshold value is removed." +
                                                             Environment.NewLine + Environment.NewLine +
                                                             "Click OK to skip this Legend Group and continue, or click CANCEL to quit";

                                            if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
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

                                        // My new group heading (threshold excised)
                                        group_heading = group_heading_no_thresh;
                                    }

                                    // Group heading does not contain a number
                                    else
                                    {
                                        // My layer name should remain unchanged
                                        group_heading = UVGroup.Heading;

                                        // check the name length
                                        if (group_heading.Length == 0)
                                        {
                                            string message = "Legend Group '" + group_heading + "' has a zero-length name." +
                                                             Environment.NewLine + Environment.NewLine +
                                                             "Click OK to skip this Legend Group and continue, or click CANCEL to quit";

                                            if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
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
                                        group_threshold_int = lyr_threshold_int;   // use layer or default value
                                    }

                                    #endregion

                                    #region Group-Level Target

                                    // Inspect the Group Heading for a Target number
                                    (bool GroupTargetFound, int group_target_int, string group_heading_no_tgt) = PRZH.ExtractValueFromString(group_heading, PRZC.c_REGEX_TARGET_PERCENT_PATTERN_ANY);

                                    // If the group heading contains a Target number...
                                    if (GroupTargetFound)
                                    {
                                        // ensure target is 0 to 100 inclusive
                                        if (group_target_int < 0 | group_target_int > 100)
                                        {
                                            string message = "An invalid target of " + group_target_int.ToString() + " has been specified for:" +
                                                             Environment.NewLine + Environment.NewLine +
                                                             "Legend Group: " + UVGroup.Heading + " of " + Environment.NewLine + Environment.NewLine +
                                                             "Layer: " + layer_name + Environment.NewLine +
                                                             "Target must be in the range 0 to 100." + Environment.NewLine + Environment.NewLine +
                                                             "Click OK to skip this layer and continue, or click CANCEL to quit";

                                            if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
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

                                        // ensure adjusted heading length is not zero
                                        if (group_heading_no_tgt.Length == 0)
                                        {
                                            string message = "Legend Group '" + UVGroup.Heading + "' has a zero-length heading once the target value is removed." +
                                                             Environment.NewLine + Environment.NewLine +
                                                             "Click OK to skip this Legend Group and continue, or click CANCEL to quit";

                                            if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
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

                                        // My new group heading (target excised)
                                        group_heading = group_heading_no_tgt;
                                    }

                                    // Group heading does not contain a number
                                    else
                                    {
                                        // check the name length
                                        if (group_heading.Length == 0)
                                        {
                                            string message = "Legend Group '" + group_heading + "' has a zero-length name." +
                                                             Environment.NewLine + Environment.NewLine +
                                                             "Click OK to skip this Legend Group and continue, or click CANCEL to quit";

                                            if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
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

                                        // get the default target for this group heading
                                        group_target_int = lyr_target_int;   // use default value
                                    }

                                    #endregion

                                    // Retrieve the Classes in this Group
                                    CIMUniqueValueClass[] UVClasses = UVGroup.Classes;
 
                                    // Each UVClass will become a Conservation Feature
                                    foreach (CIMUniqueValueClass UVClass in UVClasses)
                                    {
                                        // Create and populate the UV CF object
                                        UVConservationFeature cf = new UVConservationFeature();
                                        cf.GroupHeading = UVGroup.Heading;
                                        cf.ClassLabel = UVClass.Label;

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

                                                    case FieldCategory.DATE:
                                                        //TODO: Do this date stuff correctly or eliminate dates as an option entirely
                                                        Expression = (IsNull) ? "IS NULL" : "= date '1973-02-20'";
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

                                        classClause = "(" + classClause + ")";
                                        cf.WhereClause = classClause;

                                        LIST_ConservationFeaturesUV.Add(cf);
                                    }
                                }
                            }

                            else
                            {
                                // do I need to check for other types of renderer?  Maybe later on
                            }

                            return true;
                        }))
                        {
                            string message = "Error validating the renderer for layer '" + FL.Name + "'." +
                                             Environment.NewLine + Environment.NewLine +
                                             "Click OK to skip this layer and continue, or click CANCEL to quit";

                            if (ProMsgBox.Show(message, "Layer Validation", System.Windows.MessageBoxButton.OKCancel,
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

                        // *** GENERATE THE FL-BASED CONSERVATION FEATURES AND ADD TO DATATABLE ***

                        // If CF List has items, get the CFs from this list
                        if (LIST_ConservationFeaturesUV.Count > 0)
                        {
                            foreach (UVConservationFeature CF in LIST_ConservationFeaturesUV)
                            {
                                ConservationFeature consFeat = new ConservationFeature();

                                consFeat.cf_id = cfid++;
                                consFeat.cf_name = layer_name + " - " + CF.GroupHeading + " - " + CF.ClassLabel;
                                consFeat.cf_whereclause = CF.WhereClause;
                                consFeat.lyr_min_threshold_pct = lyr_threshold_int;
                                consFeat.cf_min_threshold_pct = lyr_threshold_int;  // TODO: cf_threshold_int
                                consFeat.lyr_object = FL;
                                consFeat.lyr_type = "FeatureLayer";
                                consFeat.lyr_name = FL.Name;
                                consFeat.lyr_json = flJson;
                                consFeat.lyr_target_pct = lyr_target_int;
                                consFeat.cf_target_pct = lyr_target_int;    // TODO: cf_target_int
                                consFeat.cf_in_use = true;

                                LIST_CF.Add(consFeat);
                            }
                        }

                        // Otherwise, get the CF from the FL
                        else
                        {
                            ConservationFeature consFeat = new ConservationFeature();

                            consFeat.cf_id = cfid++;
                            consFeat.cf_name = layer_name;
                            consFeat.cf_whereclause = "";
                            consFeat.lyr_min_threshold_pct = lyr_threshold_int;
                            consFeat.cf_min_threshold_pct = lyr_threshold_int;
                            consFeat.lyr_object = FL;
                            consFeat.lyr_type = "FeatureLayer";
                            consFeat.lyr_name = FL.Name;
                            consFeat.lyr_json = flJson;
                            consFeat.lyr_target_pct = lyr_target_int;
                            consFeat.cf_target_pct = lyr_target_int;
                            consFeat.cf_in_use = true;

                            LIST_CF.Add(consFeat);
                        }
                    }

                    // Process the layer if it is a RasterLayer
                    else if (L is RasterLayer RL)
                    {
                        // Ensure that RL is valid (i.e. has valid source data)
                        if (!await QueuedTask.Run(() =>
                        {
                            using (Raster R = RL.GetRaster())
                            {
                                return R != null;
                            }
                        }))
                        {
                            if (ProMsgBox.Show("The Raster Layer '" + RL.Name + "' has no Data Source.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return false;
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
                            if (ProMsgBox.Show("The Raster Layer '" + RL.Name + "' has a NULL or UNKNOWN Spatial Reference.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                                "Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
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
                }

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private async Task<bool> GridDoubleClick()
        {
            try
            {
                ProMsgBox.Show("click");

                //if (SelectedConflict != null)
                //{
                //    string LayerName_IN = SelectedConflict.include_layer_name;
                //    string LayerName_EX = SelectedConflict.exclude_layer_name;

                //    int AreaFieldIndex_IN = SelectedConflict.include_area_field_index;
                //    int AreaFieldIndex_EX = SelectedConflict.exclude_area_field_index;

                //    ProMsgBox.Show("Include Index: " + AreaFieldIndex_IN.ToString() + "   Layer: " + LayerName_IN);
                //    ProMsgBox.Show("Exclude Index: " + AreaFieldIndex_EX.ToString() + "   Layer: " + LayerName_EX);

                //    // Query the Status Info table for all records (i.e. PUs) where field IN ix > 0 and field EX ix > 0
                //    // Save the PUIDs in a list

                //    List<int> PlanningUnitIDs = new List<int>();

                //    await QueuedTask.Run(async () =>
                //    {
                //        using (Table table = await PRZH.GetStatusInfoTable())
                //        using (TableDefinition tDef = table.GetDefinition())
                //        {
                //            // Get the field names
                //            var fields = tDef.GetFields();
                //            string area_field_IN = fields[AreaFieldIndex_IN].Name;
                //            string area_field_EX = fields[AreaFieldIndex_EX].Name;

                //            ProMsgBox.Show("IN Field Name: " + area_field_IN + "    EX Field Name: " + area_field_EX);

                //            QueryFilter QF = new QueryFilter();
                //            QF.SubFields = PRZC.c_FLD_PUFC_ID + "," + area_field_IN + "," + area_field_EX;
                //            QF.WhereClause = area_field_IN + @" > 0 And " + area_field_EX + @" > 0";

                //            using (RowCursor rowCursor = table.Search(QF, false))
                //            {
                //                while (rowCursor.MoveNext())
                //                {
                //                    using (Row row = rowCursor.Current)
                //                    {
                //                        int puid = (int)row[PRZC.c_FLD_PUFC_ID];
                //                        PlanningUnitIDs.Add(puid);
                //                    }
                //                }
                //            }
                //        }
                //    });

                //    // validate the number of PUIDs returned
                //    if (PlanningUnitIDs.Count == 0)
                //    {
                //        ProMsgBox.Show("No Planning Unit IDs retrieved.  That's very strange, there should be at least 1.  Ask JC about this.");
                //        return true;
                //    }

                //    // I now have a list of PUIDs.  I need to select the associated features in the PUFC
                //    Map map = MapView.Active.Map;

                //    await QueuedTask.Run(() =>
                //    {
                //        // Get the Planning Unit Feature Layer
                //        FeatureLayer featureLayer = PRZH.GetFeatureLayer_PU(map);

                //        // Clear Selection
                //        featureLayer.ClearSelection();

                //        // Build QueryFilter
                //        QueryFilter QF = new QueryFilter();
                //        string puid_list = string.Join(",", PlanningUnitIDs);
                //        QF.WhereClause = PRZC.c_FLD_PUFC_ID + " In (" + puid_list + ")";

                //        // Do the actual selection
                //        using (Selection selection = featureLayer.Select(QF, SelectionCombinationMethod.New))   // selection happens here
                //        {
                //            // do nothing?
                //        }
                //    });
                //}

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }



        #endregion

        #region Event Handlers


        #endregion

    }
}