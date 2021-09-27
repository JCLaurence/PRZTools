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
    public class FeaturesVM : PropertyChangedBase
    {
        public FeaturesVM()
        {
        }

        #region FIELDS

        private ObservableCollection<FeatureElement> _features = new ObservableCollection<FeatureElement>();
        private FeatureElement _selectedFeature;
        private string _defaultMinThreshold = Properties.Settings.Default.DEFAULT_CF_MIN_THRESHOLD;
        private string _defaultGoal = Properties.Settings.Default.DEFAULT_CF_GOAL;
        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""
        private bool _featuresTableExists = false;
        private bool _puFeaturesTableExists = false;
        private bool _featuresExist = false;

        private ICommand _cmdFeatureDblClick;
        private ICommand _cmdGenerateFeatures;
        private ICommand _cmdClearFeatures;
        private ICommand _cmdClearLog;

        #endregion

        #region PROPERTIES

        public bool FeaturesTableExists
        {
            get => _featuresTableExists;
            set => SetProperty(ref _featuresTableExists, value, () => FeaturesTableExists);
        }

        public bool PUFeaturesTableExists
        {
            get => _puFeaturesTableExists;
            set => SetProperty(ref _puFeaturesTableExists, value, () => PUFeaturesTableExists);
        }

        public bool FeaturesExist
        {
            get => _featuresExist;
            set => SetProperty(ref _featuresExist, value, () => FeaturesExist);
        }

        public string DefaultMinThreshold
        {
            get => _defaultMinThreshold;
            set
            {
                SetProperty(ref _defaultMinThreshold, value, () => DefaultMinThreshold);
                Properties.Settings.Default.DEFAULT_CF_MIN_THRESHOLD = value;
                Properties.Settings.Default.Save();
            }
        }

        public string DefaultGoal
        {
            get => _defaultGoal;
            set
            {
                SetProperty(ref _defaultGoal, value, () => DefaultGoal);
                Properties.Settings.Default.DEFAULT_CF_GOAL = value;
                Properties.Settings.Default.Save();
            }
        }

        public ObservableCollection<FeatureElement> Features
        {
            get => _features;
            set => SetProperty(ref _features, value, () => Features);
        }

        public FeatureElement SelectedFeature
        {
            get => _selectedFeature;
            set => SetProperty(ref _selectedFeature, value, () => SelectedFeature);
        }

        public ProgressManager PM
        {
            get => _pm; set => SetProperty(ref _pm, value, () => PM);
        }

        #endregion

        #region COMMANDS

        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true));

        public ICommand CmdFeatureDblClick => _cmdFeatureDblClick ?? (_cmdFeatureDblClick = new RelayCommand(() => FeatureDblClick(), () => true));

        public ICommand CmdClearFeatures => _cmdClearFeatures ?? (_cmdClearFeatures = new RelayCommand(() => ClearFeatures(), () => true));

        public ICommand CmdGenerateFeatures => _cmdGenerateFeatures ?? (_cmdGenerateFeatures = new RelayCommand(() => GenerateFeatures(), () => true));

        #endregion

        #region METHODS

        public async Task OnProWinLoaded()
        {
            try
            {
                // Initialize the Progress Bar & Log
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                // Determine the presence of 2 tables, and enable/disable the clear button accordingly
                FeaturesTableExists = await PRZH.TableExists_Features();
                PUFeaturesTableExists = await PRZH.TableExists_PUFeatures();
                FeaturesExist = FeaturesTableExists || PUFeaturesTableExists;

                // Populate the grid
                if (!await PopulateGrid())
                {
                    ProMsgBox.Show("Error loading the Features grid");
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> GenerateFeatures()
        {
            int val = 0;

            try
            {
                #region INITIALIZATION AND USER INPUT VALIDATION

                // Initialize a few thingies
                Map map = MapView.Active.Map;

                // Initialize ProgressBar and Progress Log
                int max = 50;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Features Generator..."), false, max, ++val);

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

                // Validation: Ensure the Planning Unit FC exists
                string pufcpath = PRZH.GetPath_FC_PU();
                if (!await PRZH.FCExists_PU())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Class not found in the Project Geodatabase.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Planning Unit Feature Class is OK: {pufcpath}"), true, ++val);
                }

                // Validation: Ensure that the Planning Unit Feature Layer exists
                if (!PRZH.PRZLayerExists(map, PRZLayerNames.PU))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Layer not found in the map.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Planning Unit Feature Layer not present in the map.  Please reload PRZ layers");
                    return false;
                }

                // Validation: Ensure the Default Threshold is valid
                string threshold_text = string.IsNullOrEmpty(DefaultMinThreshold) ? "0" : ((DefaultMinThreshold.Trim() == "") ? "0" : DefaultMinThreshold.Trim());

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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Default Threshold = {threshold_text}"), true, ++val);
                }

                // Validation: Ensure the Default Target is valid
                string target_text = string.IsNullOrEmpty(DefaultGoal) ? "0" : ((DefaultGoal.Trim() == "") ? "0" : DefaultGoal.Trim());

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

                // Validation: Ensure the Features group layer is present
                if (!PRZH.PRZLayerExists(map, PRZLayerNames.FEATURE))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Features Group Layer is missing.  Please reload PRZ layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Features Group Layer is missing.  Please reload the PRZ Layers and try again.", "Validation");
                    return false;
                }

                // Validation: Ensure that at least 1 FL or RL is present within the CF Group Layer
                var FLs = PRZH.GetFeatureLayers_FEATURE(map);
                var RLs = PRZH.GetRasterLayers_FEATURE(map);

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
                if (ProMsgBox.Show($"If you proceed, the {PRZC.c_TABLE_FEATURES} and {PRZC.c_TABLE_PUFEATURES} tables will be overwritten if they exist in the Project Geodatabase." +
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

                #region RETRIEVE CONSERVATION FEATURES FROM THE CF GROUPLAYER

                // Create the empty list of future Conservation Features
                List<FeatureElement> LIST_CF = new List<FeatureElement>();

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

                string cfpath = PRZH.GetPath_Table_Features();

                // Delete the existing CF table, if it exists

                if (await PRZH.TableExists_Features())
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
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_TABLE_FEATURES, "", "", "Conservation Features");
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
                string fldCFID = PRZC.c_FLD_TAB_CF_ID + " LONG 'Conservation Feature ID' # # #;";
                string fldCFName = PRZC.c_FLD_TAB_CF_NAME + " TEXT 'Conservation Feature Name' 255 # #;";
                string fldCFThresholdPct = PRZC.c_FLD_TAB_CF_MIN_THRESHOLD_PCT + " LONG 'Min Threshold (%)' # 0 #;";
                string fldCFTargetPct = PRZC.c_FLD_TAB_CF_TARGET_PCT + " LONG 'Target (%)' # 0 #;";
                string fldCFWhereClause = PRZC.c_FLD_TAB_CF_WHERECLAUSE + " TEXT 'WHERE Clause' 1000 # #;";
                string fldCFEnabled = PRZC.c_FLD_TAB_CF_ENABLED + " LONG 'Enabled' # 1 #;";
                string fldCFHidden = PRZC.c_FLD_TAB_CF_HIDDEN + " LONG 'Hidden' # 0 #;";
                string fldCFArea_m2 = PRZC.c_FLD_TAB_CF_AREA_M + " DOUBLE 'Total Area (m2)' # 0, #;";
                string fldCFArea_ac = PRZC.c_FLD_TAB_CF_AREA_AC + " DOUBLE 'Total Area (ac)' # 0, #;";
                string fldCFArea_ha = PRZC.c_FLD_TAB_CF_AREA_HA + " DOUBLE 'Total Area (ha)' # 0, #;";
                string fldCFArea_km2 = PRZC.c_FLD_TAB_CF_AREA_KM + " DOUBLE 'Total Area (km2)' # 0, #;";
                string fldCFPUCount = PRZC.c_FLD_TAB_CF_PUCOUNT + " LONG 'Planning Unit Count' # 0 #;";
                string fldCFLayerName = PRZC.c_FLD_TAB_CF_LYR_NAME + " TEXT 'Source Layer Name' 300 # #;";
                string fldCFLayerType = PRZC.c_FLD_TAB_CF_LYR_TYPE + " TEXT 'Source Layer Type' 50 # #;";
                string fldCFLayerJSON = PRZC.c_FLD_TAB_CF_LYR_JSON + " TEXT 'Source Layer JSON' 100000 # #;";

                string flds = fldCFID +
                              fldCFName +
                              fldCFThresholdPct +
                              fldCFTargetPct +
                              fldCFWhereClause +
                              fldCFEnabled +
                              fldCFHidden +
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
                    using (Table table = await PRZH.GetTable_Features())
                    using (InsertCursor insertCursor = table.CreateInsertCursor())
                    using (RowBuffer rowBuffer = table.CreateRowBuffer())
                    {
                        // Iterate through each CF
                        foreach (FeatureElement CF in LIST_CF)
                        {
                            // Set the row values from the CF object
                            rowBuffer[PRZC.c_FLD_TAB_CF_ID] = CF.CF_ID;
                            rowBuffer[PRZC.c_FLD_TAB_CF_NAME] = CF.CF_Name;

                            // min threshold
                            rowBuffer[PRZC.c_FLD_TAB_CF_MIN_THRESHOLD_PCT] = (CF.CF_MinThreshold == -1) ? Convert.ToInt32(threshold_double) : CF.CF_MinThreshold;

                            // target
                            rowBuffer[PRZC.c_FLD_TAB_CF_TARGET_PCT] = (CF.CF_Goal == -1) ? Convert.ToInt32(target_double) : CF.CF_Goal;

                            rowBuffer[PRZC.c_FLD_TAB_CF_WHERECLAUSE] = CF.CF_WhereClause;

                            // enabled
                            rowBuffer[PRZC.c_FLD_TAB_CF_ENABLED] = CF.CF_Enabled;

                            rowBuffer[PRZC.c_FLD_TAB_CF_LYR_NAME] = CF.Layer_Name;
                            rowBuffer[PRZC.c_FLD_TAB_CF_LYR_TYPE] = CF.Layer_Type;
                            rowBuffer[PRZC.c_FLD_TAB_CF_LYR_JSON] = CF.Layer_Json;

                            // Finally, insert the row
                            insertCursor.Insert(rowBuffer);
                            insertCursor.Flush();
                        }
                    }
                });

                #endregion

                #region BUILD THE PUVCF TABLE

                string puvcfpath = PRZH.GetPath_Table_PUFeatures();

                // Delete the existing PUVCF table, if it exists

                if (await PRZH.TableExists_PUFeatures())
                {
                    // Delete the existing PUVCF table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting PUVCF Table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(puvcfpath, "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting the PUVCF table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleted the existing PUVCF Table..."), true, ++val);
                    }
                }

                // Copy PU FC rows into a new PUVCF table
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Copying Planning Unit FC Attributes into new PUVCF table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pufcpath, puvcfpath, "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CopyRows_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error copying PU FC rows to PUVCF table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("PUVCF Table successfully created and populated..."), true, ++val);
                }

                // Delete all fields but OID and PUID from PUVCF table
                List<string> LIST_DeleteFields = new List<string>();

                using (Table tab = await PRZH.GetTable_PUFeatures())
                {
                    if (tab == null)
                    {
                        ProMsgBox.Show("Error getting PUVCF Table :(");
                        return false;
                    }

                    await QueuedTask.Run(() =>
                    {
                        TableDefinition tDef = tab.GetDefinition();
                        List<Field> fields = tDef.GetFields().Where(f => f.FieldType != FieldType.OID && f.Name != PRZC.c_FLD_TAB_PUCF_ID).ToList();

                        foreach (Field field in fields)
                        {
                            LIST_DeleteFields.Add(field.Name);
                        }
                    });
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Removing unnecessary fields from the PUVCF table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(puvcfpath, LIST_DeleteFields);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting fields from PUVCF table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("PUVCF Table fields successfully deleted"), true, ++val);
                }

                // Now index the PUID field in the PUVCF table
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Indexing Planning Unit ID field in the PUVCF table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(puvcfpath, PRZC.c_FLD_TAB_PUCF_ID, "ix" + PRZC.c_FLD_TAB_PUCF_ID, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field indexed successfully."), true, ++val);
                }

                // Add a CF Count field
                string fldCFSum = PRZC.c_FLD_TAB_PUCF_CFCOUNT + " LONG 'CF Count' # 0 #;";

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Adding Count field to PUVCF Table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(puvcfpath, fldCFSum);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding field to PUVCF Table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field added successfully."), true, ++val);
                }

                // Populate the CF Count field with zeros
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Setting counts to 0..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(puvcfpath, PRZC.c_FLD_TAB_PUCF_CFCOUNT, 0);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CalculateField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error Calculating {PRZC.c_FLD_TAB_PUCF_CFCOUNT} Field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field calculated successfully."), true, ++val);
                }

                // Add CF-specific fields to PUVCF Table
                foreach (FeatureElement CF in LIST_CF)
                {
                    int cf_id = CF.CF_ID;

                    // CF Name field
                    string fldCFNAME_name = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + cf_id.ToString() + PRZC.c_FLD_TAB_PUCF_SUFFIX_NAME;
                    string fldCFNAME_alias = "CF " + cf_id.ToString() + " Name";
                    string fCFName = fldCFNAME_name + " TEXT '" + fldCFNAME_alias + "' 200 # #;";

                    // CF Area field
                    string fldCFAREA_name = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + cf_id.ToString() + PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA;
                    string fldCFAREA_alias = "CF " + cf_id.ToString() + " Area (m2)";
                    string fCFArea = fldCFAREA_name + " DOUBLE '" + fldCFAREA_alias + "' # 0 #;";

                    // PU Proportion field
                    string fldCFPUPROP_name = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + cf_id.ToString() + PRZC.c_FLD_TAB_PUCF_SUFFIX_PROP;
                    string fldCFPUPROP_alias = "CF " + cf_id.ToString() + " PU %";
                    string fCFPUProp = fldCFPUPROP_name + " DOUBLE '" + fldCFPUPROP_alias + "' # 0 #;";

                    flds = fCFName + fCFArea + fCFPUProp;

                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding CF{cf_id} fields to CF table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(puvcfpath, flds);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully..."), true, ++val);
                    }

                    // Populate the new fields
                    await QueuedTask.Run(async () =>
                    {
                        using (Table table = await PRZH.GetTable_PUFeatures())
                        using (RowCursor rowCursor = table.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    string cf_name = (CF.CF_Name.Length > 100) ? CF.CF_Name.Substring(0, 75) : CF.CF_Name;
                                    row[fldCFNAME_name] = cf_name;

                                    row[fldCFAREA_name] = 0;
                                    row[fldCFPUPROP_name] = 0;

                                    row.Store();
                                }
                            }
                        }
                    });
                }

                #endregion

                #region INTERSECT THE CF LAYERS WITH PUFC

                // Retrieve the area (m2) of each planning unit
                Dictionary<int, double> DICT_PUID_and_area_m2 = new Dictionary<int, double>();
                await QueuedTask.Run(async () =>
                {
                    using (FeatureClass featureClass = await PRZH.GetFC_PU())
                    using (RowCursor rowCursor1 = featureClass.Search(null, false))
                    {
                        while (rowCursor1.MoveNext())
                        {
                            using (Row row1 = rowCursor1.Current)
                            {
                                int pu_id = (int)row1[PRZC.c_FLD_FC_PU_ID];
                                double a = (double)row1[PRZC.c_FLD_FC_PU_AREA_M];

                                DICT_PUID_and_area_m2.Add(pu_id, a);
                            }
                        }
                    }
                });

                if (!await IntersectConservationFeatures(LIST_CF, DICT_PUID_and_area_m2, val))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error intersecting the Conservation Feature layers.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error intersecting the CF layers.", "");
                    return false;
                }

                #endregion

                #region UPDATE PUVCF CFCOUNT FIELD

                List<string> LIST_AreaFieldNames = new List<string>();      // All the PUVCF area field names
                Dictionary<int, int> DICT_PUID_and_cf_count = new Dictionary<int, int>();

                await QueuedTask.Run(async () =>
                {
                    using (Table table = await PRZH.GetTable_PUFeatures())
                    using (TableDefinition tDef = table.GetDefinition())
                    {
                        // Get list of CF Area fields
                        List<Field> areaFields = tDef.GetFields().Where(f => f.Name.StartsWith(PRZC.c_FLD_TAB_PUCF_PREFIX_CF) && f.Name.EndsWith(PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA)).ToList();

                        foreach (var fld in tDef.GetFields())
                        {
                            string name = fld.Name;

                            if (name.StartsWith(PRZC.c_FLD_TAB_PUCF_PREFIX_CF) && name.EndsWith(PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA))
                            {
                                LIST_AreaFieldNames.Add(fld.Name);
                            }
                        }

                        using (RowCursor rowCursor = table.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int CFCount = 0;
                                    int puid = (int)row[PRZC.c_FLD_TAB_PUCF_ID];

                                    foreach (string fldname in LIST_AreaFieldNames)
                                    {
                                        double cfarea = (row[fldname] == null) ? 0 : (double)row[fldname];

                                        if (cfarea > 0)
                                        {
                                            CFCount++;
                                        }
                                    }

                                    row[PRZC.c_FLD_TAB_PUCF_CFCOUNT] = CFCount;
                                    row.Store();

                                    DICT_PUID_and_cf_count.Add(puid, CFCount);
                                }
                            }
                        }

                    }
                });

                #endregion

                #region UPDATE SUMMARY FIELDS IN CF TABLE

                await QueuedTask.Run(async () =>
                {

                    using (Table table = await PRZH.GetTable_Features())
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        while (rowCursor.MoveNext())        // iterate through each Conservation Feature
                        {
                            using (Row row = rowCursor.Current)             // each row here is a single Conservation Feature
                            {
                                int cf_id = (int)row[PRZC.c_FLD_TAB_CF_ID];

                                string PUVCFAreaFieldName = "";

                                foreach (string f in LIST_AreaFieldNames)
                                {

                                    string idstring = f.Replace(PRZC.c_FLD_TAB_PUCF_PREFIX_CF, "").Replace(PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA, "");

                                    if (idstring == cf_id.ToString())
                                    {
                                        PUVCFAreaFieldName = f;
                                        break;
                                    }
                                }

                                if (PUVCFAreaFieldName == "")
                                {
                                    continue;
                                }

                                // Store summary values from PUVCF (values from all PU in PUVCF having some overlap with this CF)
                                int pucount = 0;        // Planning unit count for the CF
                                double area_m2 = 0;     // Total area (m2) of CF

                                using (Table table2 = await PRZH.GetTable_PUFeatures())
                                using (RowCursor rowCursor2 = table2.Search(null, false))
                                {
                                    while (rowCursor2.MoveNext())               // iterate through each record in PUVCF (each record is a planning unit)
                                    {
                                        using (Row row2 = rowCursor2.Current)
                                        {
                                            // get the area from the specific area field for this CF
                                            double d = (row2[PUVCFAreaFieldName] == null) ? 0 : (double)row2[PUVCFAreaFieldName];

                                            if (d > 0)
                                            {
                                                area_m2 += d;
                                                pucount++;
                                            }

                                        }
                                    }
                                }

                                // Update the CF Table with this summary info
                                row[PRZC.c_FLD_TAB_CF_AREA_M] = area_m2;
                                row[PRZC.c_FLD_TAB_CF_AREA_AC] = area_m2 * PRZC.c_CONVERT_M2_TO_AC;
                                row[PRZC.c_FLD_TAB_CF_AREA_HA] = area_m2 * PRZC.c_CONVERT_M2_TO_HA;
                                row[PRZC.c_FLD_TAB_CF_AREA_KM] = area_m2 * PRZC.c_CONVERT_M2_TO_KM2;
                                row[PRZC.c_FLD_TAB_CF_PUCOUNT] = pucount;

                                row.Store();
                            }
                        }
                    }
                });

                #endregion

                #region UPDATE PUFC CFCOUNT FIELD

                await QueuedTask.Run(async () =>
                {
                    using (Table table = await PRZH.GetFC_PU())
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                int puid = (int)row[PRZC.c_FLD_FC_PU_ID];
                                int cfcount = DICT_PUID_and_cf_count[puid];

                                row[PRZC.c_FLD_FC_PU_CFCOUNT] = cfcount;
                                row.Store();
                            }
                        }

                    }
                });


                #endregion


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

                // update checkboxes
                FeaturesTableExists = await PRZH.TableExists_Features();
                PUFeaturesTableExists = await PRZH.TableExists_PUFeatures();

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

        private async Task<bool> GetConservationFeaturesFromLayers(List<FeatureElement> LIST_CF)
        {
            try
            {
                Map map = MapView.Active.Map;

                int cfid = 1;
                int default_threshold_int = int.Parse(Properties.Settings.Default.DEFAULT_CF_MIN_THRESHOLD);     // retrieve default threshold value
                int default_target_int = int.Parse(Properties.Settings.Default.DEFAULT_CF_GOAL);           // retrieve default target value

                List<Layer> LIST_L = PRZH.GetLayers_FEATURE(map);

                for (int i = 0; i < LIST_L.Count; i++)
                {
                    Layer L = LIST_L[i];

                    // Process the Layer based on type
                    if (L is FeatureLayer FL)                   // Layer is FeatureLayer
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
                                        // Process the Class Label looking for Class-level threshold and target
                                        string class_label = "";

                                        #region Class-Level Minimum Threshold

                                        // Inspect the Class Label for a Minimum Threshold number
                                        (bool ClassThresholdFound, int class_threshold_int, string class_label_no_thresh) = PRZH.ExtractValueFromString(UVClass.Label, PRZC.c_REGEX_THRESHOLD_PERCENT_PATTERN_ANY);

                                        // If the class label contains a Threshold number...
                                        if (ClassThresholdFound)
                                        {
                                            // ensure threshold is 0 to 100 inclusive
                                            if (class_threshold_int < 0 | class_threshold_int > 100)
                                            {
                                                string message = "An invalid threshold of " + class_threshold_int.ToString() + " has been specified for:" +
                                                                 Environment.NewLine + Environment.NewLine +
                                                                 "Legend Class: " + UVClass.Label + " of " + Environment.NewLine + Environment.NewLine +
                                                                 "Layer: " + FL.Name + Environment.NewLine +
                                                                 "Threshold must be in the range 0 to 100." + Environment.NewLine + Environment.NewLine +
                                                                 "Click OK to skip this class and continue, or click CANCEL to quit";

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

                                            // ensure adjusted label length is not zero
                                            if (class_label_no_thresh.Length == 0)
                                            {
                                                string message = "Legend Class '" + UVClass.Label + "' has a zero-length label once the threshold value is removed." +
                                                                 Environment.NewLine + Environment.NewLine +
                                                                 "Click OK to skip this Legend Class and continue, or click CANCEL to quit";

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

                                            // My new class label (threshold excised)
                                            class_label = class_label_no_thresh;
                                        }

                                        // Class label does not contain a number
                                        else
                                        {
                                            // My class label should remain unchanged
                                            class_label = UVClass.Label;

                                            // check the name length
                                            if (class_label.Length == 0)
                                            {
                                                string message = "Legend Class '" + UVClass.Label + "' has a zero-length name." +
                                                                 Environment.NewLine + Environment.NewLine +
                                                                 "Click OK to skip this Legend Class and continue, or click CANCEL to quit";

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
                                            class_threshold_int = group_threshold_int;   // use layer or default value
                                        }

                                        #endregion

                                        #region Class-Level Target

                                        // Inspect the Class Label for a Target number
                                        (bool ClassTargetFound, int class_target_int, string class_label_no_tgt) = PRZH.ExtractValueFromString(class_label, PRZC.c_REGEX_TARGET_PERCENT_PATTERN_ANY);

                                        // If the class label contains a Target number...
                                        if (ClassTargetFound)
                                        {
                                            // ensure target is 0 to 100 inclusive
                                            if (class_target_int < 0 | class_target_int > 100)
                                            {
                                                string message = "An invalid target of " + class_target_int.ToString() + " has been specified for:" +
                                                                 Environment.NewLine + Environment.NewLine +
                                                                 "Legend Class: " + UVClass.Label + " of " + Environment.NewLine + Environment.NewLine +
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

                                            // ensure adjusted label length is not zero
                                            if (class_label_no_tgt.Length == 0)
                                            {
                                                string message = "Legend Class '" + UVClass.Label + "' has a zero-length label once the target value is removed." +
                                                                 Environment.NewLine + Environment.NewLine +
                                                                 "Click OK to skip this Legend Class and continue, or click CANCEL to quit";

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

                                            // My new class label (target excised)
                                            class_label = class_label_no_tgt;
                                        }

                                        // Class label does not contain a number
                                        else
                                        {
                                            // check the name length
                                            if (class_label.Length == 0)
                                            {
                                                string message = "Legend Class '" + class_label + "' has a zero-length name." +
                                                                 Environment.NewLine + Environment.NewLine +
                                                                 "Click OK to skip this Legend Class and continue, or click CANCEL to quit";

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
                                            class_target_int = group_target_int;   // use default value
                                        }

                                        #endregion

                                        // Create and populate the UV CF object
                                        UVConservationFeature cf = new UVConservationFeature();
                                        cf.GroupHeading = group_heading;
                                        cf.ClassLabel = class_label;
                                        cf.GroupThreshold = group_threshold_int;
                                        cf.GroupTarget = group_target_int;
                                        cf.ClassThreshold = class_threshold_int;
                                        cf.ClassTarget = class_target_int;

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
                                FeatureElement consFeat = new FeatureElement();

                                consFeat.CF_ID = cfid++;
                                consFeat.CF_Name = layer_name + " - " + CF.GroupHeading + " - " + CF.ClassLabel;
                                consFeat.CF_WhereClause = CF.WhereClause;
                                consFeat.Layer_MinThreshold = lyr_threshold_int;
                                consFeat.CF_MinThreshold = CF.ClassThreshold;
                                consFeat.Layer_Object = FL;
                                consFeat.Layer_Type = "Feature";
                                consFeat.Layer_Name = FL.Name;
                                consFeat.Layer_Json = flJson;
                                consFeat.Layer_Goal = lyr_target_int;
                                consFeat.CF_Goal = CF.ClassTarget;
                                consFeat.CF_Enabled = 1;

                                LIST_CF.Add(consFeat);
                            }
                        }

                        // Otherwise, get the CF from the FL
                        else
                        {
                            FeatureElement consFeat = new FeatureElement();

                            consFeat.CF_ID = cfid++;
                            consFeat.CF_Name = layer_name;
                            consFeat.CF_WhereClause = "";
                            consFeat.Layer_MinThreshold = lyr_threshold_int;
                            consFeat.CF_MinThreshold = lyr_threshold_int;
                            consFeat.Layer_Object = FL;
                            consFeat.Layer_Type = "Feature";
                            consFeat.Layer_Name = FL.Name;
                            consFeat.Layer_Json = flJson;
                            consFeat.Layer_Goal = lyr_target_int;
                            consFeat.CF_Goal = lyr_target_int;
                            consFeat.CF_Enabled = 1;

                            LIST_CF.Add(consFeat);
                        }
                    }

                    // Process the layer if it is a RasterLayer
                    else if (L is RasterLayer RL)           // Layer is RasterLayer
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

                        string layer_name = ""; // layer name, optionally with thresholds and/or targets excised

                        #region Layer-Level Minimum Threshold

                        // Inspect the Layer Name for a Minimum Threshold number
                        (bool ThresholdFound, int lyr_threshold_int, string layer_name_no_thresh) = PRZH.ExtractValueFromString(RL.Name, PRZC.c_REGEX_THRESHOLD_PERCENT_PATTERN_ANY);

                        // If the Layer Name contains a Threshold number...
                        if (ThresholdFound)
                        {
                            // ensure threshold is 0 to 100 inclusive
                            if (lyr_threshold_int < 0 | lyr_threshold_int > 100)
                            {
                                string message = "An invalid threshold of " + lyr_threshold_int.ToString() + " has been specified for:" +
                                                 Environment.NewLine + Environment.NewLine +
                                                 "Layer: " + RL.Name + Environment.NewLine +
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
                                string message = "Layer '" + RL.Name + "' has a zero-length name once the threshold value is removed." +
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
                            layer_name = RL.Name;

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

                        // Get the JSON from the Raster Layer
                        string rlJson = "";
                        await QueuedTask.Run(() =>
                        {
                            CIMBaseLayer cimbl = RL.GetDefinition();
                            rlJson = cimbl.ToJson();
                        });

                        // Create a new CF for this raster layer
                        FeatureElement consFeat = new FeatureElement();

                        consFeat.CF_ID = cfid++;
                        consFeat.CF_Name = layer_name;
                        consFeat.CF_WhereClause = "";
                        consFeat.Layer_MinThreshold = lyr_threshold_int;
                        consFeat.CF_MinThreshold = lyr_threshold_int;
                        consFeat.Layer_Object = RL;
                        consFeat.Layer_Type = "Raster";
                        consFeat.Layer_Name = RL.Name;
                        consFeat.Layer_Json = rlJson;
                        consFeat.Layer_Goal = lyr_target_int;
                        consFeat.CF_Goal = lyr_target_int;
                        consFeat.CF_Enabled = 1;

                        LIST_CF.Add(consFeat);
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

        private async Task<bool> IntersectConservationFeatures(List<FeatureElement> LIST_CF, Dictionary<int, double> DICT_PUID_area_total, int val)
        {
            try
            {
                return await QueuedTask.Run(async () =>
                {
                    Map map = MapView.Active.Map;

                    // Some GP variables
                    IReadOnlyList<string> toolParams;
                    IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                    GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                    string toolOutput;

                    // some paths
                    string gdbpath = PRZH.GetPath_ProjectGDB();
                    string pufcpath = PRZH.GetPath_FC_PU();
                    string puvcfpath = PRZH.GetPath_Table_PUFeatures();
                    string cfpath = PRZH.GetPath_Table_Features();

                    // Get the PU FL ready (no selection)
                    FeatureLayer PUFL = PRZH.GetFeatureLayer_PU(map);
                    PUFL.ClearSelection();  // we don't want selected features only, we want all of them

                    foreach (FeatureElement CF in LIST_CF)
                    {

                        // CALCULATIONS DEPEND ON LAYER TYPE!!!!

                        // If FeatureLayer...
                        if (CF.Layer_Object is FeatureLayer FL)
                        {
                            // Selection
                            await QueuedTask.Run(() =>
                            {
                                FL.ClearSelection();

                                if (CF.CF_WhereClause.Length > 0)
                                {
                                    // Select appropriate features
                                    QueryFilter QF = new QueryFilter();
                                    QF.WhereClause = CF.CF_WhereClause;
                                    FL.Select(QF, SelectionCombinationMethod.New);
                                }
                            });

                            // Prepare for Intersection Prelim FCs
                            string intersect_fc_name = PRZC.c_FC_TEMP_PUVCF_PREFIX + CF.CF_ID.ToString() + PRZC.c_FC_TEMP_PUVCF_SUFFIX_INT;
                            string intersect_fc_path = Path.Combine(gdbpath, intersect_fc_name);

                            // Construct the inputs value array
                            object[] a = { PUFL, 1 };   // prelim array -> combine the layer object and the Rank (PU layer)
                            object[] b = { FL, 2 };     // prelim array -> combine the layer object and the Rank (CF layer)

                            IReadOnlyList<string> a2 = Geoprocessing.MakeValueArray(a);   // Let this method figure out how best to quote the layer info
                            IReadOnlyList<string> b2 = Geoprocessing.MakeValueArray(b);   // Let this method figure out how best to quote the layer info

                            string inputs_string = string.Join(" ", a2) + ";" + string.Join(" ", b2);   // my final inputs string

                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Intersecting CF {CF.CF_ID}: {CF.CF_Name}"), true);
                            toolParams = Geoprocessing.MakeValueArray(inputs_string, intersect_fc_path, "ALL", "", "INPUT");
                            toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                            toolOutput = await PRZH.RunGPTool("Intersect_analysis", toolParams, toolEnvs, toolFlags);
                            if (toolOutput == null)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error Intersecting CF {CF.CF_ID}.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true);
                                return false;
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Intersect was successful for CF {CF.CF_ID}."), true);
                            }

                            // Now dissolve the temp intersect layer on PUID
                            string dissolve_fc_name = PRZC.c_FC_TEMP_PUVCF_PREFIX + CF.CF_ID.ToString() + PRZC.c_FC_TEMP_PUVCF_SUFFIX_DSLV;
                            string dissolve_fc_path = Path.Combine(gdbpath, dissolve_fc_name);

                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Dissolving {intersect_fc_name} on Planning Unit ID..."), true);
                            toolParams = Geoprocessing.MakeValueArray(intersect_fc_path, dissolve_fc_path, PRZC.c_FLD_FC_PU_ID, "", "MULTI_PART", "");
                            toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                            toolOutput = await PRZH.RunGPTool("Dissolve_management", toolParams, toolEnvs, toolFlags);
                            if (toolOutput == null)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error dissolving {intersect_fc_name}.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true);
                                return false;
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"{intersect_fc_name} was dissolved successfully."), true);
                            }

                            // Extract the dissolved area for each puid into a dictionary
                            Dictionary<int, double> DICT_PUID_area_dslv = new Dictionary<int, double>();

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
                                            int puid;
                                            double area_m;

                                            // get the planning unit id
                                            puid = (int)feature[PRZC.c_FLD_FC_PU_ID];

                                            // get the area (m2)
                                            Polygon poly = (Polygon)feature.GetShape();
                                            area_m = poly.Area;

                                            DICT_PUID_area_dslv.Add(puid, area_m);
                                        }
                                    }
                                }
                            }

                            // Finally, write this information to PUVCF table
                            int cf_min_thresh = CF.CF_MinThreshold;
                            int cf_tgt = CF.CF_Goal;

                            string fnArea = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + CF.CF_ID.ToString() + PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA;
                            string fnProp = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + CF.CF_ID.ToString() + PRZC.c_FLD_TAB_PUCF_SUFFIX_PROP;

                            foreach (KeyValuePair<int, double> KVP in DICT_PUID_area_dslv)
                            {
                                int PUID = KVP.Key;
                                double area_dslv = KVP.Value;
                                double area_total = DICT_PUID_area_total[PUID];
                                double percent_cf_coverage = area_dslv / area_total;

                                QueryFilter QF = new QueryFilter
                                {
                                    WhereClause = PRZC.c_FLD_TAB_PUCF_ID + " = " + PUID.ToString()
                                };

                                using (Table table = await PRZH.GetTable_PUFeatures())
                                using (RowCursor rowCursor = table.Search(QF, false))
                                {
                                    while (rowCursor.MoveNext())
                                    {
                                        using (Row row = rowCursor.Current)
                                        {
                                            row[fnArea] = area_dslv;
                                            row[fnProp] = percent_cf_coverage * 100.0;

                                            row.Store();
                                        }
                                    }
                                }
                            }

                            // Finally, delete the two temp feature classes
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting temporary feature classes..."), true);

                            object[] e = { intersect_fc_path, dissolve_fc_path };
                            var e2 = Geoprocessing.MakeValueArray(e);   // Let this method figure out how best to quote the paths
                            string inputs2 = String.Join(";", e2);
                            toolParams = Geoprocessing.MakeValueArray(inputs2, "");
                            toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                            toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                            if (toolOutput == null)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting temp feature classes.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true);
                                return false;
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Temp Feature Classes deleted successfully."), true);
                            }

                            FL.ClearSelection();
                        }

                        // If RasterLayer...
                        else if (CF.Layer_Object is RasterLayer RL)
                        {
                            // Get the PUFC SR and Extent
                            SpatialReference PUFC_SR = null;
                            Envelope PUFC_Extent = null;
                            await QueuedTask.Run(async () =>
                            {
                                using (FeatureClass fc = await PRZH.GetFC_PU())
                                using (FeatureClassDefinition fcDef = fc.GetDefinition())
                                {
                                    PUFC_SR = fcDef.GetSpatialReference();
                                    PUFC_Extent = fc.GetExtent();
                                }
                            });

                            // Get the Cost Raster Layer and its SRs
                            SpatialReference CostRL_SR = null;
                            SpatialReference CostR_SR = null;

                            await QueuedTask.Run(() =>
                            {
                                CostRL_SR = RL.GetSpatialReference();               // do I need this one...

                                using (Raster costRaster = RL.GetRaster())
                                {
                                    CostR_SR = costRaster.GetSpatialReference();    // or this one... ?
                                }
                            });

                            // prepare the temporary zonal stats table
                            string tabname = "cf_zonal_temp";

                            // Calculate Zonal Statistics as Table
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Executing Zonal Statistics as Table for CF {CF.CF_ID}..."), true, ++val);
                            toolParams = Geoprocessing.MakeValueArray(PUFL, PRZC.c_FLD_FC_PU_ID, RL, tabname);
                            toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: PUFC_SR, overwriteoutput: true, extent: PUFC_Extent);
                            toolOutput = await PRZH.RunGPTool("ZonalStatisticsAsTable_sa", toolParams, toolEnvs, toolFlags);
                            if (toolOutput == null)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Error executing the Zonal Statistics as Table tool.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
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

                            await QueuedTask.Run(async () =>
                            {
                                using (Geodatabase geodatabase = await PRZH.GetProjectGDB())
                                using (Table table = await PRZH.GetTable(geodatabase, tabname))
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
                            });

                            // Delete the temp zonal stats table (I no longer need it, I think...)
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {tabname} Table..."), true, ++val);
                            toolParams = Geoprocessing.MakeValueArray(tabname, "");
                            toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                            toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                            if (toolOutput == null)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {tabname} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                                return false;
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleted the {tabname} Table..."), true, ++val);
                            }

                            // Finally, write the dictionary info to PUVCF table
                            string fnArea = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + CF.CF_ID.ToString() + PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA;
                            string fnProp = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + CF.CF_ID.ToString() + PRZC.c_FLD_TAB_PUCF_SUFFIX_PROP;

                            foreach (KeyValuePair<int, Tuple<int, double>> KVP in DICT_PUID_and_count_area)
                            {
                                int PUID = KVP.Key;
                                Tuple<int, double> tuple = KVP.Value;

                                int count_ras = tuple.Item1;
                                double area_ras = tuple.Item2;

                                double area_total = DICT_PUID_area_total[PUID];
                                double percent_cf_coverage = area_ras / area_total;

                                QueryFilter QF = new QueryFilter
                                {
                                    WhereClause = PRZC.c_FLD_TAB_PUCF_ID + " = " + PUID.ToString()
                                };

                                using (Table table = await PRZH.GetTable_PUFeatures())
                                using (RowCursor rowCursor = table.Search(QF, false))
                                {
                                    while (rowCursor.MoveNext())
                                    {
                                        using (Row row = rowCursor.Current)
                                        {
                                            row[fnArea] = area_ras;
                                            row[fnProp] = percent_cf_coverage * 100.0;

                                            row.Store();
                                        }
                                    }
                                }
                            }

                        }
                        else
                        {
                            continue;
                        }
                    }

                    return true;
                });
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true);
                return false;
            }
        }

        private async Task<bool> PopulateGrid()
        {
            try
            {
                // Clear the contents
                Features = new ObservableCollection<FeatureElement>(); // triggers the xaml refresh

                // If Features table doesn't exist, exit
                if (!await PRZH.TableExists_Features())
                {
                    return true;
                }

                List<FeatureElement> LIST_Features = new List<FeatureElement>();

                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (Table table = await PRZH.GetTable_Features())
                        using (RowCursor rowCursor = table.Search())
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {

                                    // i'm here!!!
                                    FeatureElement feature = new FeatureElement();

                                    feature.CF_ID = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_ID]);
                                    feature.CF_Name = (row[PRZC.c_FLD_TAB_CF_NAME] == null | row[PRZC.c_FLD_TAB_CF_NAME] == DBNull.Value) ? "" : row[PRZC.c_FLD_TAB_SELRULES_NAME].ToString();


                                    feature.CF_Name = (row[PRZC.c_FLD_TAB_CF_NAME] == null) ? "" : row[PRZC.c_FLD_TAB_CF_NAME].ToString();
                                    feature.CF_WhereClause = (row[PRZC.c_FLD_TAB_CF_WHERECLAUSE] == null) ? "" : row[PRZC.c_FLD_TAB_CF_WHERECLAUSE].ToString();
                                    feature.CF_MinThreshold = (row[PRZC.c_FLD_TAB_CF_MIN_THRESHOLD_PCT] == null) ? -1 : (int)row[PRZC.c_FLD_TAB_CF_MIN_THRESHOLD_PCT];
                                    feature.CF_Goal = (row[PRZC.c_FLD_TAB_CF_TARGET_PCT] == null) ? -1 : (int)row[PRZC.c_FLD_TAB_CF_TARGET_PCT];

                                    feature.CF_Area_M2 = (row[PRZC.c_FLD_TAB_CF_AREA_M] == null) ? -1 : (double)row[PRZC.c_FLD_TAB_CF_AREA_M];
                                    feature.CF_Area_Ac = (row[PRZC.c_FLD_TAB_CF_AREA_AC] == null) ? -1 : (double)row[PRZC.c_FLD_TAB_CF_AREA_AC];
                                    feature.CF_Area_Ha = (row[PRZC.c_FLD_TAB_CF_AREA_HA] == null) ? -1 : (double)row[PRZC.c_FLD_TAB_CF_AREA_HA];
                                    feature.CF_Area_Km2 = (row[PRZC.c_FLD_TAB_CF_AREA_KM] == null) ? -1 : (double)row[PRZC.c_FLD_TAB_CF_AREA_KM];
                                    feature.CF_PUCount = (row[PRZC.c_FLD_TAB_CF_PUCOUNT] == null) ? -1 : (int)row[PRZC.c_FLD_TAB_CF_PUCOUNT];

                                    feature.Layer_Name = (row[PRZC.c_FLD_TAB_CF_LYR_NAME] == null) ? "" : row[PRZC.c_FLD_TAB_CF_LYR_NAME].ToString();
                                    feature.Layer_Type = (row[PRZC.c_FLD_TAB_CF_LYR_TYPE] == null) ? "" : row[PRZC.c_FLD_TAB_CF_LYR_TYPE].ToString();
                                    feature.Layer_Json = (row[PRZC.c_FLD_TAB_CF_LYR_JSON] == null) ? "" : row[PRZC.c_FLD_TAB_CF_LYR_JSON].ToString();

                                    feature.CF_Enabled = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_ENABLED]);

                                    LIST_Features.Add(feature);



                                    SelectionRule SR = new SelectionRule();

                                    SR.SR_Name = row[PRZC.c_FLD_TAB_SELRULES_NAME].ToString() ?? "";

                                    string rt = row[PRZC.c_FLD_TAB_SELRULES_RULETYPE].ToString() ?? "";
                                    if (rt == SelectionRuleType.INCLUDE.ToString())
                                    {
                                        SR.SR_RuleType = SelectionRuleType.INCLUDE;
                                    }
                                    else if (rt == SelectionRuleType.EXCLUDE.ToString())
                                    {
                                        SR.SR_RuleType = SelectionRuleType.EXCLUDE;
                                    }

                                    string lt = row[PRZC.c_FLD_TAB_SELRULES_LAYERTYPE].ToString() ?? "";
                                    if (lt == SelectionRuleLayerType.RASTER.ToString())
                                    {
                                        SR.SR_LayerType = SelectionRuleLayerType.RASTER;
                                    }
                                    else if (lt == SelectionRuleLayerType.VECTOR.ToString())
                                    {
                                        SR.SR_LayerType = SelectionRuleLayerType.VECTOR;
                                    }

                                    SR.SR_MinThreshold = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_MIN_THRESHOLD]);
                                    SR.SR_PUCount = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_PUCOUNT]);
                                    SR.SR_Enabled = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_ENABLED]);
                                    SR.SR_Hidden = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_HIDDEN]);
                                    SR.SR_Area_M2 = Convert.ToDouble(row[PRZC.c_FLD_TAB_SELRULES_AREA_M]);
                                    SR.SR_Area_Ac = Convert.ToDouble(row[PRZC.c_FLD_TAB_SELRULES_AREA_AC]);
                                    SR.SR_Area_Ha = Convert.ToDouble(row[PRZC.c_FLD_TAB_SELRULES_AREA_HA]);
                                    SR.SR_Area_Km2 = Convert.ToDouble(row[PRZC.c_FLD_TAB_SELRULES_AREA_KM]);

                                    rules.Add(SR);
                                }
                            }

                            int c = table.GetCount();
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
                    ProMsgBox.Show($"Error retrieving Feature information from the {PRZC.c_TABLE_FEATURES} table.");
                    return false;
                }







                await QueuedTask.Run(async() =>
                {
                    if (!await PRZH.TableExists_Features())
                    {
                        return;
                    }

                    using (Table table = await PRZH.GetTable_Features())
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                FeatureElement CF = new FeatureElement();

                                CF.CF_ID = (row[PRZC.c_FLD_TAB_CF_ID] == null) ? -1 : (int)row[PRZC.c_FLD_TAB_CF_ID];
                                CF.CF_Name = (row[PRZC.c_FLD_TAB_CF_NAME] == null) ? "" : row[PRZC.c_FLD_TAB_CF_NAME].ToString();
                                CF.CF_WhereClause = (row[PRZC.c_FLD_TAB_CF_WHERECLAUSE] == null) ? "" : row[PRZC.c_FLD_TAB_CF_WHERECLAUSE].ToString();
                                CF.CF_MinThreshold = (row[PRZC.c_FLD_TAB_CF_MIN_THRESHOLD_PCT] == null) ? -1 : (int)row[PRZC.c_FLD_TAB_CF_MIN_THRESHOLD_PCT];
                                CF.CF_Goal = (row[PRZC.c_FLD_TAB_CF_TARGET_PCT] == null) ? -1 : (int)row[PRZC.c_FLD_TAB_CF_TARGET_PCT];

                                CF.CF_Area_M2 = (row[PRZC.c_FLD_TAB_CF_AREA_M] == null) ? -1 : (double)row[PRZC.c_FLD_TAB_CF_AREA_M];
                                CF.CF_Area_Ac = (row[PRZC.c_FLD_TAB_CF_AREA_AC] == null) ? -1 : (double)row[PRZC.c_FLD_TAB_CF_AREA_AC];
                                CF.CF_Area_Ha = (row[PRZC.c_FLD_TAB_CF_AREA_HA] == null) ? -1 : (double)row[PRZC.c_FLD_TAB_CF_AREA_HA];
                                CF.CF_Area_Km2 = (row[PRZC.c_FLD_TAB_CF_AREA_KM] == null) ? -1 : (double)row[PRZC.c_FLD_TAB_CF_AREA_KM];
                                CF.CF_PUCount = (row[PRZC.c_FLD_TAB_CF_PUCOUNT] == null) ? -1 : (int)row[PRZC.c_FLD_TAB_CF_PUCOUNT];

                                CF.Layer_Name = (row[PRZC.c_FLD_TAB_CF_LYR_NAME] == null) ? "" : row[PRZC.c_FLD_TAB_CF_LYR_NAME].ToString();
                                CF.Layer_Type = (row[PRZC.c_FLD_TAB_CF_LYR_TYPE] == null) ? "" : row[PRZC.c_FLD_TAB_CF_LYR_TYPE].ToString();
                                CF.Layer_Json = (row[PRZC.c_FLD_TAB_CF_LYR_JSON] == null) ? "" : row[PRZC.c_FLD_TAB_CF_LYR_JSON].ToString();

                                CF.CF_Enabled = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_ENABLED]);

                                LIST_Features.Add(CF);
                            }
                        }
                    }
                });

                // Sort them
                LIST_Features.Sort((x, y) => x.CF_ID.CompareTo(y.CF_ID));

                foreach (FeatureElement cf in LIST_Features)
                {
                    Features.Add(cf);
                }
                NotifyPropertyChanged(() => Features);



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

        private async Task<bool> ClearFeatures()
        {
            int val = 0;

            try
            {
                //// Some GP variables
                //IReadOnlyList<string> toolParams;
                //IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                //GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                //string toolOutput;

                //// Some paths
                //string gdbpath = PRZH.GetPath_ProjectGDB();
                //string srpath = PRZH.GetPath_Table_SelRules();
                //string pusrpath = PRZH.GetPath_Table_PUSelRules();

                //// Initialize ProgressBar and Progress Log
                //int max = 20;
                //PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing..."), false, max, ++val);

                //// Validation: Prompt User for permission to proceed
                //if (ProMsgBox.Show("If you proceed, the following will happen:" +
                //   Environment.NewLine +
                //   $"1. The {PRZC.c_TABLE_SELRULES} and {PRZC.c_TABLE_PUSELRULES} tables will be deleted from the project geodatabase (if they exist)" + Environment.NewLine +
                //   $"2. All values in the {PRZC.c_FLD_FC_PU_EFFECTIVE_RULE} field in the {PRZC.c_FC_PLANNING_UNITS} feature class will be set to 1" + Environment.NewLine +
                //   $"3. All values in the {PRZC.c_FLD_FC_PU_CONFLICT} field in the {PRZC.c_FC_PLANNING_UNITS} feature class will be set to 0" +
                //   Environment.NewLine + Environment.NewLine +
                //   "Do you wish to proceed?" +
                //   Environment.NewLine + Environment.NewLine +
                //   "Choose wisely...",
                //   "TABLE DELETE WARNING",
                //   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                //   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out."), true, ++val);
                //    return false;
                //}

                //// Delete the SelRules table
                //if (await PRZH.TableExists_SelRules())
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting the {PRZC.c_TABLE_SELRULES} table..."), true, ++val);
                //    toolParams = Geoprocessing.MakeValueArray(srpath, "");
                //    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                //    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                //    if (toolOutput == null)
                //    {
                //        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_SELRULES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                //        ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_SELRULES} table.");
                //        return false;
                //    }
                //    else
                //    {
                //        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_SELRULES} table deleted successfully..."), true, ++val);
                //    }
                //}

                //// Delete the PUSelRules table
                //if (await PRZH.TableExists_PUSelRules())
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting the {PRZC.c_TABLE_PUSELRULES} table..."), true, ++val);
                //    toolParams = Geoprocessing.MakeValueArray(pusrpath, "");
                //    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                //    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                //    if (toolOutput == null)
                //    {
                //        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_PUSELRULES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                //        ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_PUSELRULES} table.");
                //        return false;
                //    }
                //    else
                //    {
                //        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_PUSELRULES} table deleted successfully..."), true, ++val);
                //    }
                //}

                //// Update PUFC, set effective rule and conflict fields to default
                //PRZH.UpdateProgress(PM, PRZH.WriteLog($"Resetting {PRZC.c_FLD_FC_PU_EFFECTIVE_RULE} and {PRZC.c_FLD_FC_PU_CONFLICT} fields in {PRZC.c_FC_PLANNING_UNITS} Feature Class..."), true, ++val);

                //if (!await QueuedTask.Run(async () =>
                //{
                //    try
                //    {
                //        using (Table table = await PRZH.GetFC_PU())
                //        using (RowCursor rowCursor = table.Search(null, false))
                //        {
                //            while (rowCursor.MoveNext())
                //            {
                //                using (Row row = rowCursor.Current)
                //                {
                //                    row[PRZC.c_FLD_FC_PU_EFFECTIVE_RULE] = DBNull.Value;
                //                    row[PRZC.c_FLD_FC_PU_CONFLICT] = 0;

                //                    row.Store();
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
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error resetting the {PRZC.c_FLD_FC_PU_EFFECTIVE_RULE} and {PRZC.c_FLD_FC_PU_CONFLICT} fields in {PRZC.c_FC_PLANNING_UNITS} Feature Class.", LogMessageType.ERROR), true, ++val);
                //    ProMsgBox.Show($"Error resetting {PRZC.c_FLD_FC_PU_EFFECTIVE_RULE} and {PRZC.c_FLD_FC_PU_CONFLICT} fields in {PRZC.c_FC_PLANNING_UNITS} Feature Class.");
                //    return false;
                //}
                //else
                //{
                //    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields updated successfully."), true, ++val);
                //}


                //// Determine the presence of 2 tables, and enable/disable the main button accordingly
                //SelRuleTableExists = await PRZH.TableExists_SelRules();
                //PUSelRuleTableExists = await PRZH.TableExists_PUSelRules();
                //SelRulesExist = SelRuleTableExists || PUSelRuleTableExists;

                //// Repopulate the grids
                //if (!await PopulateGrid_Rules())
                //{
                //    ProMsgBox.Show("Error loading the Selection Rules grid");
                //}
                //if (!await PopulateGrid_Conflicts())
                //{
                //    ProMsgBox.Show("Error loading the Conflicts grid");
                //}

                //ProMsgBox.Show("Selection Rules Removed.");

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                return false;
            }
        }

        private async Task<bool> FeatureDblClick()
        {
            try
            {
                if (SelectedFeature == null)
                {
                    return true;
                }

                // Exit if the Feature has no associated planning units
                if (SelectedFeature.CF_PUCount == 0)
                {
                    return true;
                }

                // Get the Feature ID
                int cf_id = SelectedFeature.CF_ID;

                // Get the associated area field name
                string areafieldname = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + cf_id.ToString() + PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA;

                // Query the PUFeatures table for associated planning unit records
                List<int> LIST_PUIDs = new List<int>();

                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        string whereclause = areafieldname + @" > 0";

                        QueryFilter QF = new QueryFilter
                        {
                            SubFields = PRZC.c_FLD_TAB_PUCF_ID + "," + areafieldname,
                            WhereClause = whereclause
                        };

                        using (Table table = await PRZH.GetTable_PUFeatures())
                        using (RowCursor rowCursor = table.Search(QF))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int id = Convert.ToInt32(row[PRZC.c_FLD_TAB_PUCF_ID]);
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

        #endregion

    }
}