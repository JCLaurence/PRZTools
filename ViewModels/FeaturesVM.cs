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
                var try_gdbexists = await PRZH.GDBExists_Project();

                if (!try_gdbexists.exists)
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

                // Validation: Ensure that the required layers are present
                if (!PRZH.PRZLayerExists(map, PRZLayerNames.PU) | !PRZH.PRZLayerExists(map, PRZLayerNames.FEATURES))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Layers are missing.  Please reload PRZ layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("PRZ Layers are missing.  Please reload the PRZ Layers and try again.", "Validation");
                    return false;
                }

                // Validation: Ensure that at least one Feature Layer is present in the Feature Group Layer
                var CF_Layers = PRZH.GetPRZLayers(map, PRZLayerNames.FEATURES, PRZLayerRetrievalType.BOTH);

                if (CF_Layers == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Unable to retrieve contents of {PRZC.c_GROUPLAYER_FEATURES} Group Layer.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Unable to retrieve contents of {PRZC.c_GROUPLAYER_FEATURES} Group Layer.", "Validation");
                    return false;
                }

                if (CF_Layers.Count == 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> No Raster or Feature Layers found within the {PRZC.c_GROUPLAYER_FEATURES} group layer.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"There must be at least one Raster or Feature Layer within either the {PRZC.c_GROUPLAYER_FEATURES} group layer.", "Validation");
                    return false;
                }

                // Validation: Ensure the Default Minimum Threshold is valid
                string threshold_text = string.IsNullOrEmpty(DefaultMinThreshold) ? "0" : ((DefaultMinThreshold.Trim() == "") ? "0" : DefaultMinThreshold.Trim());

                if (!double.TryParse(threshold_text, out double threshold_double))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Default Minimum Threshold", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Default Minimum Threshold value.  Value must be a number between 0 and 100 (inclusive)", "Validation");
                    return false;
                }
                else if (threshold_double < 0 | threshold_double > 100)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Default Minimum Threshold", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Default Minimum Threshold value.  Value must be a number between 0 and 100 (inclusive)", "Validation");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Default Minimum Threshold = {threshold_text}"), true, ++val);
                }

                // Validation: Ensure the Default Target is valid
                string goal_text = string.IsNullOrEmpty(DefaultGoal) ? "0" : ((DefaultGoal.Trim() == "") ? "0" : DefaultGoal.Trim());

                if (!double.TryParse(goal_text, out double goal_double))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Default Goal", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Default Goal value.  Value must be a number between 0 and 100 (inclusive)", "Validation");
                    return false;
                }
                else if (goal_double < 0 | goal_double > 100)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Default Goal", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Default Goal value.  Value must be a number between 0 and 100 (inclusive)", "Validation");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Default Goal = " + goal_text), true, ++val);
                }

                // Validation: Prompt User for permission to proceed
                if (ProMsgBox.Show($"If you proceed, the {PRZC.c_TABLE_FEATURES} and {PRZC.c_TABLE_PUFEATURES} tables will be overwritten if they exist in the Project Geodatabase." +
                   Environment.NewLine + Environment.NewLine +
                   $"Additionally, the contents of the {PRZC.c_FLD_FC_PU_FEATURECOUNT} field in the {PRZC.c_FC_PLANNING_UNITS} Feature Class will be updated." +
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

                #region COMPILE LIST OF FEATURES

                // Retrieve the Features
                var feature_getter = await GetFeaturesFromLayers();

                if (!feature_getter.success || feature_getter.features == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error retrieving Features from Layers", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Unable to construct the Features listing");
                    return false;
                }
                else if (feature_getter.features.Count == 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("No valid Features found", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("No valid Features found", "Validation");
                    return false;
                }

                List<FeatureElement> LIST_Features = feature_getter.features;

                #endregion

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                #region BUILD THE FEATURES TABLE

                string cfpath = PRZH.GetPath_Table_Features();

                // Delete the existing Features table, if it exists
                if (await PRZH.TableExists_Features())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {PRZC.c_TABLE_FEATURES} Table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(cfpath, "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_FEATURES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_FEATURES} table.  GP Tool failed or was cancelled by user");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleted the existing {PRZC.c_TABLE_FEATURES} Table..."), true, ++val);
                    }
                }

                // Create the table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating the {PRZC.c_TABLE_FEATURES} Table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_TABLE_FEATURES, "", "", "Features");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CreateTable_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {PRZC.c_TABLE_FEATURES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error creating the {PRZC.c_TABLE_FEATURES} table.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Created the {PRZC.c_TABLE_FEATURES} table..."), true, ++val);
                }

                // Add fields to the table
                string fldCFID = PRZC.c_FLD_TAB_CF_ID + " LONG 'Feature ID' # # #;";
                string fldCFName = PRZC.c_FLD_TAB_CF_NAME + " TEXT 'Feature Name' 255 # #;";

                string fldCFThreshold = PRZC.c_FLD_TAB_CF_MIN_THRESHOLD + " LONG 'Min Threshold (%)' # 0 #;";
                string fldCFGoal = PRZC.c_FLD_TAB_CF_GOAL + " LONG 'Goal (%)' # 0 #;";
                string fldCFEnabled = PRZC.c_FLD_TAB_CF_ENABLED + " LONG 'Enabled' # 1 #;";
                string fldCFHidden = PRZC.c_FLD_TAB_CF_HIDDEN + " LONG 'Hidden' # 0 #;";

                string fldCFArea_m2 = PRZC.c_FLD_TAB_CF_AREA_M2 + " DOUBLE 'Total Area (m2)' # 0 #;";
                string fldCFArea_ac = PRZC.c_FLD_TAB_CF_AREA_AC + " DOUBLE 'Total Area (ac)' # 0 #;";
                string fldCFArea_ha = PRZC.c_FLD_TAB_CF_AREA_HA + " DOUBLE 'Total Area (ha)' # 0 #;";
                string fldCFArea_km2 = PRZC.c_FLD_TAB_CF_AREA_KM2 + " DOUBLE 'Total Area (km2)' # 0 #;";
                string fldCFPUCount = PRZC.c_FLD_TAB_CF_PUCOUNT + " LONG 'Planning Unit Count' # 0 #;";

                string fldCFLayerName = PRZC.c_FLD_TAB_CF_LYR_NAME + " TEXT 'Source Layer Name' 300 # #;";
                string fldCFLayerType = PRZC.c_FLD_TAB_CF_LYR_TYPE + " TEXT 'Source Layer Type' 50 # #;";
                string fldCFWhereClause = PRZC.c_FLD_TAB_CF_WHERECLAUSE + " TEXT 'WHERE Clause' 1000 # #;";
                string fldCFLayerJSON = PRZC.c_FLD_TAB_CF_LYR_JSON + " TEXT 'Source Layer JSON' 100000 # #;";

                string flds = fldCFID +
                              fldCFName +
                              fldCFThreshold +
                              fldCFGoal +
                              fldCFEnabled +
                              fldCFHidden +
                              fldCFArea_m2 +
                              fldCFArea_ac +
                              fldCFArea_ha +
                              fldCFArea_km2 +
                              fldCFPUCount +
                              fldCFLayerName +
                              fldCFLayerType +
                              fldCFWhereClause +
                              fldCFLayerJSON;

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to {PRZC.c_TABLE_FEATURES} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(cfpath, flds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to {PRZC.c_TABLE_FEATURES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error adding fields to the {PRZC.c_TABLE_FEATURES} table.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Fields added successfully."), true, ++val);
                }

                #endregion

                #region POPULATE THE FEATURES TABLE

                // Populate Table from LIST
                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (Table table = await PRZH.GetTable_Features())
                        using (InsertCursor insertCursor = table.CreateInsertCursor())
                        using (RowBuffer rowBuffer = table.CreateRowBuffer())
                        {
                            // Iterate through each feature
                            foreach (FeatureElement CF in LIST_Features)
                            {
                                rowBuffer[PRZC.c_FLD_TAB_CF_ID] = CF.CF_ID;
                                rowBuffer[PRZC.c_FLD_TAB_CF_NAME] = CF.CF_Name;
                                rowBuffer[PRZC.c_FLD_TAB_CF_MIN_THRESHOLD] = CF.CF_MinThreshold;
                                rowBuffer[PRZC.c_FLD_TAB_CF_GOAL] = CF.CF_Goal;
                                rowBuffer[PRZC.c_FLD_TAB_CF_WHERECLAUSE] = CF.CF_WhereClause;
                                rowBuffer[PRZC.c_FLD_TAB_CF_ENABLED] = CF.CF_Enabled;
                                rowBuffer[PRZC.c_FLD_TAB_CF_HIDDEN] = CF.CF_Hidden;
                                rowBuffer[PRZC.c_FLD_TAB_CF_AREA_M2] = CF.CF_Area_M2;
                                rowBuffer[PRZC.c_FLD_TAB_CF_AREA_AC] = CF.CF_Area_Ac;
                                rowBuffer[PRZC.c_FLD_TAB_CF_AREA_HA] = CF.CF_Area_Ha;
                                rowBuffer[PRZC.c_FLD_TAB_CF_AREA_KM2] = CF.CF_Area_Km2;
                                rowBuffer[PRZC.c_FLD_TAB_CF_PUCOUNT] = CF.CF_PUCount;
                                rowBuffer[PRZC.c_FLD_TAB_CF_LYR_NAME] = CF.Layer_Name;
                                rowBuffer[PRZC.c_FLD_TAB_CF_LYR_TYPE] = CF.Layer_Type.ToString();
                                rowBuffer[PRZC.c_FLD_TAB_CF_LYR_JSON] = CF.Layer_Json;

                                // Finally, insert the row
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error populating the {PRZC.c_TABLE_FEATURES} table.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error populating the {PRZC.c_TABLE_FEATURES} table.");
                    return false;

                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_FEATURES} table populated successfully."), true, ++val);
                }

                #endregion

                #region BUILD THE PUFEATURES TABLE - PART I

                string pucfpath = PRZH.GetPath_Table_PUFeatures();

                // Delete the existing PUFeatures table, if it exists
                if (await PRZH.TableExists_PUFeatures())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting the {PRZC.c_TABLE_PUFEATURES} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(pucfpath, "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_PUFEATURES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_PUFEATURES} table.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Table deleted successfully."), true, ++val);
                    }
                }

                // Copy PU FC rows into a new PUFeatures table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Copying attributes from the {PRZC.c_FC_PLANNING_UNITS} feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pufcpath, pucfpath, "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CopyRows_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying planning unit attributes to {PRZC.c_TABLE_PUFEATURES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error copying planning unit attributes to {PRZC.c_TABLE_PUFEATURES} table.");
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
                        using (Table tab = await PRZH.GetTable_PUFeatures())
                        {
                            if (tab == null)
                            {
                                ProMsgBox.Show($"Unable to retrieve the {PRZC.c_TABLE_PUFEATURES} table");
                                return false;
                            }

                            using (TableDefinition tDef = tab.GetDefinition())
                            {
                                List<Field> fields = tDef.GetFields().Where(f => f.Name != tDef.GetObjectIDField() && f.Name != PRZC.c_FLD_TAB_PUCF_ID).ToList();

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

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Removing unnecessary fields from the {PRZC.c_TABLE_PUFEATURES} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pucfpath, LIST_DeleteFields);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting fields from {PRZC.c_TABLE_PUFEATURES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error deleting fields.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Fields deleted successfully."), true, ++val);
                }

                // Now index the PUID field
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing {PRZC.c_FLD_TAB_PUCF_ID} field..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pucfpath, PRZC.c_FLD_TAB_PUCF_ID, "ix" + PRZC.c_FLD_TAB_PUCF_ID, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error indexing the {PRZC.c_FLD_TAB_PUCF_ID} field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error indexing the {PRZC.c_FLD_TAB_PUCF_ID} field.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field indexed successfully."), true, ++val);
                }

                // Add a Feature Count field
                string fldCFSum = PRZC.c_FLD_TAB_PUCF_FEATURECOUNT + " LONG 'Feature Count' # 0 #;";

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding {PRZC.c_FLD_TAB_PUCF_FEATURECOUNT} field to {PRZC.c_TABLE_PUFEATURES} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pucfpath, fldCFSum);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding {PRZC.c_FLD_TAB_PUCF_FEATURECOUNT} field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error adding {PRZC.c_FLD_TAB_PUCF_FEATURECOUNT} field.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field added successfully."), true, ++val);
                }

                // Populate the CF Count field with zeros
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Setting {PRZC.c_FLD_TAB_PUCF_FEATURECOUNT} field to 0..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pucfpath, PRZC.c_FLD_TAB_PUCF_FEATURECOUNT, 0);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CalculateField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error Calculating {PRZC.c_FLD_TAB_PUCF_FEATURECOUNT} field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error Calculating {PRZC.c_FLD_TAB_PUCF_FEATURECOUNT} field.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field calculated successfully."), true, ++val);
                }

                #endregion

                #region BUILD THE PUFEATURES TABLE - PART II

                // Cycle through each Feature
                foreach (FeatureElement CF in LIST_Features)
                {
                    // Get the Feature ID
                    int cfid = CF.CF_ID;

                    // Get the Feature Name (limited to 75 characters)
                    string feature_name = (CF.CF_Name.Length > 75) ? CF.CF_Name.Substring(0, 75) : CF.CF_Name;

                    string prefix = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + cfid.ToString();
                    string aliasprefix = PRZC.c_FLD_TAB_PUCF_PREFIX_CF.Replace("_", " ") + cfid.ToString();

                    // Add Fields: CFID, Name, Area, Coverage, and Status

                    //// CF ID field
                    //string fId = prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_CFID;
                    //string fIdAlias = aliasprefix + " ID";
                    //string f1 = fId + " LONG '" + fIdAlias + "' # 0 #;";

                    //// Name field 
                    //string fName = prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_NAME;
                    //string fNameAlias = aliasprefix + " Name";
                    //string f2 = fName + " TEXT '" + fNameAlias + "' 200 # #;";

                    // Area field
                    string fArea = prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA;
                    string fAreaAlias = aliasprefix + " Area (m2)";
                    string f3 = fArea + " DOUBLE '" + fAreaAlias + "' # 0 #;";

                    // Coverage field
                    string fCov = prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_COVERAGE;
                    string fCovAlias = aliasprefix + " Coverage (%)";
                    string f4 = fCov + " DOUBLE '" + fCovAlias + "' # 0 #;";

                    // State field
                    string fStat = prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_STATE;
                    string fStatAlias = aliasprefix + " State";
                    string f5 = fStat + " LONG '" + fStatAlias + "' # 0 #;";

                    flds = f3 + f4 + f5;

                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields for feature {cfid}..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(pucfpath, flds);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields for feature {cfid}.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error adding fields for feature {cfid}.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully..."), true, ++val);
                    }
                }

                // Update values in the new fields
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Updating new field values..."), true, ++val);

                // Get lists of field names
                //Dictionary<string, int> IDFields = new Dictionary<string, int>();
                //Dictionary<string, string> NameFields = new Dictionary<string, string>();
                List<string> AreFields = new List<string>();
                List<string> CovFields = new List<string>();
                List<string> StatFields = new List<string>();

                foreach (FeatureElement CF in LIST_Features)
                {
                    int cfid = CF.CF_ID;

                    string prefix = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + cfid.ToString();

                    //IDFields.Add(prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_CFID, cfid);
                    //NameFields.Add(prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_NAME, CF.CF_Name);
                    AreFields.Add(prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA);
                    CovFields.Add(prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_COVERAGE);
                    StatFields.Add(prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_STATE);
                }

                // Populate the new fields
                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (Table table = await PRZH.GetTable_PUFeatures())
                        using (RowCursor rowCursor = table.Search())
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    //foreach (var kvp in IDFields)
                                    //{
                                    //    string fldname = kvp.Key;
                                    //    int id = kvp.Value;
                                    //    row[fldname] = id;
                                    //}

                                    //foreach (var kvp in NameFields)
                                    //{
                                    //    string fldname = kvp.Key;
                                    //    string name = kvp.Value;
                                    //    row[fldname] = name;
                                    //}

                                    foreach (var fldname in AreFields)
                                    {
                                        row[fldname] = 0;
                                    }

                                    foreach (var fldname in CovFields)
                                    {
                                        row[fldname] = 0;
                                    }

                                    foreach (var fldname in StatFields)
                                    {
                                        row[fldname] = 0;
                                    }

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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error updating records in the {PRZC.c_TABLE_PUFEATURES} table...", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error updating records in the {PRZC.c_TABLE_PUFEATURES} table.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_PUFEATURES} table updated successfully."), true, ++val);
                }


                #endregion

                #region INTERSECT THE FEATURES WITH PLANNING UNITS

                if (!await IntersectFeatures(LIST_Features, val))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error intersecting the Features.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error intersecting the Features.", "");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Features intersected successfully."), true, ++val);
                }

                #endregion

                #region UPDATE FEATURECOUNT FIELD IN PUFEATURES TABLE

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Calculating {PRZC.c_FLD_TAB_PUCF_FEATURECOUNT} field in {PRZC.c_TABLE_PUFEATURES} table..."), true, ++val);
                Dictionary<int, int> DICT_PUID_and_featurecount = new Dictionary<int, int>();
                List<string> LIST_AreaFieldNames = new List<string>();

                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (Table table = await PRZH.GetTable_PUFeatures())
                        using (TableDefinition tDef = table.GetDefinition())
                        {
                            // Get list of State fields
                            List<Field> StateFields = tDef.GetFields().Where(f => f.Name.StartsWith(PRZC.c_FLD_TAB_PUCF_PREFIX_CF) && f.Name.EndsWith(PRZC.c_FLD_TAB_PUCF_SUFFIX_STATE)).ToList();
                            List<Field> AreaFields = tDef.GetFields().Where(f => f.Name.StartsWith(PRZC.c_FLD_TAB_PUCF_PREFIX_CF) && f.Name.EndsWith(PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA)).ToList();

                            // Store the list of Area Fields
                            foreach (Field fd in AreaFields)
                            {
                                using (fd)
                                {
                                    LIST_AreaFieldNames.Add(fd.Name);
                                }
                            }

                            // Iterate through each planning unit, then iterate through each State field.  Each field having value = 1 means the associated Feature has a presence in this planning unit.
                            using (RowCursor rowCursor = table.Search())
                            {
                                while (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        int puid = Convert.ToInt32(row[PRZC.c_FLD_TAB_PUCF_ID]);
                                        int cfcount = 0;

                                        foreach (Field fld in StateFields)
                                        {
                                            if (Convert.ToInt32(row[fld.Name]) == 1)
                                            {
                                                cfcount++;
                                            }
                                        }

                                        // update the row
                                        row[PRZC.c_FLD_TAB_PUCF_FEATURECOUNT] = cfcount;
                                        row.Store();

                                        // Add to dictionary
                                        DICT_PUID_and_featurecount.Add(puid, cfcount);
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error updating the {PRZC.c_FLD_TAB_PUCF_FEATURECOUNT} field in the {PRZC.c_TABLE_PUFEATURES} table.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error updating the {PRZC.c_FLD_TAB_PUCF_FEATURECOUNT} field in the {PRZC.c_TABLE_PUFEATURES} table.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_FLD_TAB_PUCF_FEATURECOUNT} field updated."), true, ++val);
                }

                #endregion

                #region UPDATE SUMMARY FIELDS IN CF TABLE

                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (Table table = await PRZH.GetTable_Features())
                        using (RowCursor rowCursor = table.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int cfid = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_ID]);

                                    string AreaField = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + cfid.ToString() + PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA;
                                    string StateField = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + cfid.ToString() + PRZC.c_FLD_TAB_PUCF_SUFFIX_STATE;

                                    int pucount = 0;
                                    double area_m2 = 0;

                                    QueryFilter QF = new QueryFilter
                                    {
                                        SubFields = AreaField,
                                        WhereClause = StateField + @" = 1"
                                    };

                                    using (Table table2 = await PRZH.GetTable_PUFeatures())
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

                                    // Update the Features Table with this summary info

                                    double m2_round = Math.Round(area_m2, 2, MidpointRounding.AwayFromZero);
                                    double ac_round = Math.Round(area_m2 * PRZC.c_CONVERT_M2_TO_AC, 2, MidpointRounding.AwayFromZero);
                                    double ha_round = Math.Round(area_m2 * PRZC.c_CONVERT_M2_TO_HA, 2, MidpointRounding.AwayFromZero);
                                    double km2_round = Math.Round(area_m2 * PRZC.c_CONVERT_M2_TO_KM2, 2, MidpointRounding.AwayFromZero);

                                    row[PRZC.c_FLD_TAB_CF_AREA_M2] = m2_round;
                                    row[PRZC.c_FLD_TAB_CF_AREA_AC] = ac_round;
                                    row[PRZC.c_FLD_TAB_CF_AREA_HA] = ha_round;
                                    row[PRZC.c_FLD_TAB_CF_AREA_KM2] = km2_round;
                                    row[PRZC.c_FLD_TAB_CF_PUCOUNT] = pucount;

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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error updating summary fields in the {PRZC.c_TABLE_FEATURES} table.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error updating summary fields in the {PRZC.c_TABLE_FEATURES} table.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Summary fields updated."), true, ++val);
                }

                #endregion

                #region UPDATE FEATURECOUNT FIELD IN PU FEATURE CLASS

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
                                    row[PRZC.c_FLD_FC_PU_FEATURECOUNT] = DICT_PUID_and_featurecount[puid];
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error updating the {PRZC.c_FLD_FC_PU_FEATURECOUNT} field in the {PRZC.c_FC_PLANNING_UNITS} feature class.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error updating the {PRZC.c_FLD_FC_PU_FEATURECOUNT} field in the {PRZC.c_FC_PLANNING_UNITS} feature class.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_FLD_FC_PU_FEATURECOUNT} field updated."), true, ++val);
                }

                #endregion

                #region WRAP THINGS UP

                // Populate the grids
                if (!await PopulateGrid())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error populating the Features grid.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error populating the Features grid.");
                    return false;
                }

                // Compact the Geodatabase
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacting the geodatabase..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath);
                toolOutput = await PRZH.RunGPTool("Compact_management", toolParams, null, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error compacting the geodatabase. GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error compacting the geodatabase.");
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

        private async Task<(bool success, List<FeatureElement> features)> GetFeaturesFromLayers()
        {
            (bool, List<FeatureElement>) fail = (false, null);

            try
            {

                #region INITIALIZE STUFF

                // Set some initial variables
                Map map = MapView.Active.Map;
                int cfid = 1;
                int default_threshold = int.Parse(Properties.Settings.Default.DEFAULT_CF_MIN_THRESHOLD);
                int default_goal = int.Parse(Properties.Settings.Default.DEFAULT_CF_GOAL);

                // Set up the master list of features
                List<FeatureElement> features = new List<FeatureElement>();

                // Get the Layer Lists
                var LIST_Layers = PRZH.GetPRZLayers(map, PRZLayerNames.FEATURES, PRZLayerRetrievalType.BOTH);

                // Exit if errors obtaining the lists
                if (LIST_Layers == null)
                {
                    ProMsgBox.Show($"Unable to retrieve layers from the {PRZC.c_GROUPLAYER_FEATURES} group layer...");
                    return fail;
                }

                // Exit if no layers actually returned
                if (LIST_Layers.Count == 0)
                {
                    ProMsgBox.Show($"No valid layers found within the {PRZC.c_GROUPLAYER_FEATURES} group layer...");
                    return fail;
                }

                #endregion

                // Process the FEATURE layers
                for (int i = 0; i < LIST_Layers.Count; i++)
                {
                    Layer L = LIST_Layers[i];

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
                            string message = "An invalid minimum threshold of " + layer_threshold.ToString() + " has been specified for:" +
                                             Environment.NewLine + Environment.NewLine +
                                             "Layer: " + L.Name + Environment.NewLine +
                                             "Minimum Threshold must be in the range 0 to 100 (inclusive)." + Environment.NewLine + Environment.NewLine +
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

                    #region EXTRACT GOAL FROM LAYER NAME

                    // Inspect the Layer Name for a Minimum Threshold number
                    (bool GoalFound, int layer_goal, string layer_name_goal_removed) = PRZH.ExtractValueFromString(layer_name, PRZC.c_REGEX_GOAL_PERCENT_PATTERN_ANY);

                    // If the Layer Name contains a Goal number...
                    if (GoalFound)
                    {
                        // ensure goal is 0 to 100 inclusive
                        if (layer_goal < 0 | layer_goal > 100)
                        {
                            string message = "An invalid goal of " + layer_goal.ToString() + " has been specified for:" +
                                             Environment.NewLine + Environment.NewLine +
                                             "Layer: " + L.Name + Environment.NewLine +
                                             "Goal must be in the range 0 to 100 (inclusive)." + Environment.NewLine + Environment.NewLine +
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
                        layer_name = layer_name_goal_removed;
                    }

                    // Layer Name does not contain a number
                    else
                    {
                        // My layer name should remain unchanged
                        layer_name = L.Name;

                        // get the default goal for this layer
                        layer_goal = default_goal;   // use default value
                    }

                    #endregion

                    string layer_json = "";
                    await QueuedTask.Run(() =>
                    {
                        CIMBaseLayer cimbl = L.GetDefinition();
                        layer_json = cimbl.ToJson();
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

                        #region CREATE THE FEATURE

                        // Examine the FL Renderer to identify specific classes, each of which will be its own CF
                        List<FeatureUV> LIST_UV = new List<FeatureUV>();

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
                                    // Process the Group Heading looking for Group-level threshold and goal
                                    string group_heading = ""; // group heading, optionally with thresholds and/or goal excised

                                    #region GROUP-LEVEL MINIMUM THRESHOLD

                                    // Inspect the Group Heading for a Minimum Threshold number
                                    (bool GroupThresholdFound, int group_threshold, string group_heading_threshold_removed) = PRZH.ExtractValueFromString(UVGroup.Heading, PRZC.c_REGEX_THRESHOLD_PERCENT_PATTERN_ANY);

                                    // If the group heading contains a Threshold number...
                                    if (GroupThresholdFound)
                                    {
                                        // ensure threshold is 0 to 100 inclusive
                                        if (group_threshold < 0 | group_threshold > 100)
                                        {
                                            string message = "An invalid threshold of " + group_threshold.ToString() + " has been specified for:" +
                                                             Environment.NewLine + Environment.NewLine +
                                                             "Legend Group: " + UVGroup.Heading + " of " + Environment.NewLine + Environment.NewLine +
                                                             "Layer: " + FL.Name + Environment.NewLine +
                                                             "Threshold must be in the range 0 to 100 (inclusive)." + Environment.NewLine + Environment.NewLine +
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

                                        // My new group heading (threshold excised)
                                        group_heading = group_heading_threshold_removed;
                                    }
                                    // Group heading does not contain a number
                                    else
                                    {
                                        // My layer name should remain unchanged
                                        group_heading = UVGroup.Heading;

                                        // get the default threshold for this layer
                                        group_threshold = layer_threshold;   // use layer or default value
                                    }

                                    #endregion

                                    #region GROUP-LEVEL GOAL

                                    // Inspect the Group Heading for a Goal number
                                    (bool GroupGoalFound, int group_goal, string group_heading_goal_removed) = PRZH.ExtractValueFromString(group_heading, PRZC.c_REGEX_GOAL_PERCENT_PATTERN_ANY);

                                    // If the group heading contains a Goal number...
                                    if (GroupGoalFound)
                                    {
                                        // ensure goal is 0 to 100 inclusive
                                        if (group_goal < 0 | group_goal > 100)
                                        {
                                            string message = "An invalid goal of " + group_goal.ToString() + " has been specified for:" +
                                                             Environment.NewLine + Environment.NewLine +
                                                             "Legend Group: " + UVGroup.Heading + " of " + Environment.NewLine + Environment.NewLine +
                                                             "Layer: " + layer_name + Environment.NewLine +
                                                             "Goal must be in the range 0 to 100." + Environment.NewLine + Environment.NewLine +
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

                                        // My new group heading (goal excised)
                                        group_heading = group_heading_goal_removed;
                                    }
                                    // Group heading does not contain a number
                                    else
                                    {
                                        // get the default target for this group heading
                                        group_goal = layer_goal;   // use default value
                                    }

                                    #endregion

                                    // Retrieve the Classes in this Group
                                    CIMUniqueValueClass[] UVClasses = UVGroup.Classes;

                                    // Each UVClass will become a Conservation Feature
                                    foreach (CIMUniqueValueClass UVClass in UVClasses)
                                    {
                                        // Process the Class Label looking for Class-level threshold and target
                                        string class_label = "";

                                        #region CLASS-LEVEL MINIMUM THRESHOLD

                                        // Inspect the Class Label for a Minimum Threshold number
                                        (bool ClassThresholdFound, int class_threshold, string class_label_threshold_removed) = PRZH.ExtractValueFromString(UVClass.Label, PRZC.c_REGEX_THRESHOLD_PERCENT_PATTERN_ANY);

                                        // If the class label contains a Threshold number...
                                        if (ClassThresholdFound)
                                        {
                                            // ensure threshold is 0 to 100 inclusive
                                            if (class_threshold < 0 | class_threshold > 100)
                                            {
                                                string message = "An invalid threshold of " + class_threshold.ToString() + " has been specified for:" +
                                                                 Environment.NewLine + Environment.NewLine +
                                                                 "Legend Class: " + UVClass.Label + " of " + Environment.NewLine + Environment.NewLine +
                                                                 "Layer: " + FL.Name + Environment.NewLine +
                                                                 "Threshold must be in the range 0 to 100 (inclusive)." + Environment.NewLine + Environment.NewLine +
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

                                            // My new class label (threshold excised)
                                            class_label = class_label_threshold_removed;
                                        }
                                        // Class label does not contain a number
                                        else
                                        {
                                            // My class label should remain unchanged
                                            class_label = UVClass.Label;

                                            // get the default threshold for this layer
                                            class_threshold = group_threshold;   // use layer or default value
                                        }

                                        #endregion

                                        #region CLASS-LEVEL GOAL

                                        // Inspect the Class Label for a Goal number
                                        (bool ClassGoalFound, int class_goal, string class_label_goal_removed) = PRZH.ExtractValueFromString(class_label, PRZC.c_REGEX_GOAL_PERCENT_PATTERN_ANY);

                                        // If the class label contains a goal number...
                                        if (ClassGoalFound)
                                        {
                                            // ensure goal is 0 to 100 inclusive
                                            if (class_goal < 0 | class_goal > 100)
                                            {
                                                string message = "An invalid goal of " + class_goal.ToString() + " has been specified for:" +
                                                                 Environment.NewLine + Environment.NewLine +
                                                                 "Legend Class: " + UVClass.Label + " of " + Environment.NewLine + Environment.NewLine +
                                                                 "Layer: " + layer_name + Environment.NewLine +
                                                                 "Goal must be in the range 0 to 100 (inclusive)." + Environment.NewLine + Environment.NewLine +
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

                                            // My new class label (goal excised)
                                            class_label = class_label_goal_removed;
                                        }
                                        // Class label does not contain a number
                                        else
                                        {
                                            // get the default goal for this group heading
                                            class_goal = group_goal;   // use default value
                                        }

                                        #endregion

                                        // Create and populate the UV CF object
                                        FeatureUV cf = new FeatureUV();
                                        cf.GroupHeading = group_heading;
                                        cf.ClassLabel = class_label;
                                        cf.GroupThreshold = group_threshold;
                                        cf.GroupGoal = group_goal;
                                        cf.ClassThreshold = class_threshold;
                                        cf.ClassGoal = class_goal;

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

                                        LIST_UV.Add(cf);
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
                            string message = "Error validating the renderer for layer '" + L.Name + "'." +
                                             Environment.NewLine + Environment.NewLine +
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

                        // If List has items, get the features from this list
                        if (LIST_UV.Count > 0)
                        {
                            foreach (FeatureUV fuv in LIST_UV)
                            {
                                FeatureElement consFeat = new FeatureElement();

                                consFeat.CF_ID = cfid++;
                                consFeat.CF_Name = layer_name + " - " + fuv.GroupHeading + " - " + fuv.ClassLabel;
                                consFeat.CF_WhereClause = fuv.WhereClause;
                                consFeat.Layer_MinThreshold = layer_threshold;
                                consFeat.CF_MinThreshold = fuv.ClassThreshold;
                                consFeat.Layer_Object = L;
                                consFeat.Layer_Type = FeatureLayerType.FEATURE;
                                consFeat.Layer_Name = L.Name;
                                consFeat.Layer_Json = layer_json;
                                consFeat.Layer_Goal = layer_goal;
                                consFeat.CF_Goal = fuv.ClassGoal;

                                features.Add(consFeat);
                            }
                        }
                        // Otherwise, get the CF from the FL
                        else
                        {
                            FeatureElement consFeat = new FeatureElement();

                            consFeat.CF_ID = cfid++;
                            consFeat.CF_Name = layer_name;
                            consFeat.CF_WhereClause = "";
                            consFeat.Layer_MinThreshold = layer_threshold; ;
                            consFeat.CF_MinThreshold = layer_threshold;
                            consFeat.Layer_Object = L;
                            consFeat.Layer_Type = FeatureLayerType.FEATURE;
                            consFeat.Layer_Name = L.Name;
                            consFeat.Layer_Json = layer_json;
                            consFeat.Layer_Goal = layer_goal;
                            consFeat.CF_Goal = layer_goal;

                            features.Add(consFeat);
                        }

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

                        #region CREATE THE FEATURE

                        // Create a new CF for this raster layer
                        FeatureElement consFeat = new FeatureElement();

                        consFeat.CF_ID = cfid++;
                        consFeat.CF_Name = layer_name;
                        consFeat.CF_WhereClause = "";
                        consFeat.Layer_MinThreshold = layer_threshold;
                        consFeat.CF_MinThreshold = layer_threshold;
                        consFeat.Layer_Object = L;
                        consFeat.Layer_Type = FeatureLayerType.RASTER;
                        consFeat.Layer_Name = RL.Name;
                        consFeat.Layer_Json = layer_json;
                        consFeat.Layer_Goal = layer_goal;
                        consFeat.CF_Goal = layer_goal;

                        features.Add(consFeat);

                        #endregion
                    }
                }

                return (true, features);
            }
            catch (Exception)
            {
                return fail;
            }
        }

        private async Task<bool> IntersectFeatures(List<FeatureElement> features, int val)
        {
            try
            {
                #region BUILD PUID AND TOTAL PU AREA DICTIONARY

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Constructing Planning Unit ID and Area dictionary..."), true, ++val);
                Dictionary<int, double> DICT_PUID_Area_Total = await PRZH.GetPlanningUnitIDsAndArea();

                if (DICT_PUID_Area_Total == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error constructing Planning Unit ID and Area dictionary...", LogMessageType.ERROR), true, ++val);
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
                string pucfpath = PRZH.GetPath_Table_PUFeatures();
                string cfpath = PRZH.GetPath_Table_Features();

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

                foreach (FeatureElement CF in features)
                {
                    // Get some Feature info
                    int cfid = CF.CF_ID;
                    string name = CF.CF_Name;
                    FeatureLayerType layertype = CF.Layer_Type;
                    int threshold = CF.CF_MinThreshold;
                    int goal = CF.CF_Goal;
                    string whereclause = CF.CF_WhereClause;
                    string prefix = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + cfid.ToString();

                    // Process each feature layer
                    if (CF.Layer_Object is FeatureLayer FL)
                    {
                        // Clear selection on cf layer, apply new selection as per WhereClause (if applicable)
                        await QueuedTask.Run(() =>
                        {
                            FL.ClearSelection();

                            if (whereclause.Length > 0)
                            {
                                QueryFilter QF = new QueryFilter();
                                QF.WhereClause = whereclause;
                                FL.Select(QF, SelectionCombinationMethod.New);
                            }
                        });

                        // Prepare for Intersection Prelim FCs
                        string intersect_fc_name = PRZC.c_FC_TEMP_PUCF_PREFIX + cfid.ToString() + PRZC.c_FC_TEMP_PUCF_SUFFIX_INT;
                        string intersect_fc_path = Path.Combine(gdbpath, intersect_fc_name);

                        // Construct the inputs value array
                        object[] a = { PUFL, 1 };   // prelim array -> combine the layer object and the Rank (PU layer)
                        object[] b = { FL, 2 };     // prelim array -> combine the layer object and the Rank (Feature layer)

                        IReadOnlyList<string> a2 = Geoprocessing.MakeValueArray(a);   // Let this method figure out how best to quote the layer info
                        IReadOnlyList<string> b2 = Geoprocessing.MakeValueArray(b);   // Let this method figure out how best to quote the layer info

                        string inputs_string = string.Join(" ", a2) + ";" + string.Join(" ", b2);   // my final inputs string

                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Intersecting feature {cfid} layer ({name})."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(inputs_string, intersect_fc_path, "ALL", "", "INPUT");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true, outputCoordinateSystem: PUFC_SR);
                        toolOutput = await PRZH.RunGPTool("Intersect_analysis", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error intersecting feature {cfid} layer.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error intersecting feature {cfid} layer.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Intersection successful for feature {cfid} layer."), true, ++val);
                        }

                        // Now dissolve the temp intersect layer on PUID
                        string dissolve_fc_name = PRZC.c_FC_TEMP_PUCF_PREFIX + cfid.ToString() + PRZC.c_FC_TEMP_PUCF_SUFFIX_DSLV;
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
                                var tryget_gdb = await PRZH.GetGDB_Project();
                                if (!tryget_gdb.success)
                                {
                                    return false;
                                }

                                using (Geodatabase gdb = tryget_gdb.geodatabase)
                                using (FeatureClass fc = await PRZH.GetFeatureClass(gdb, dissolve_fc_name))
                                {
                                    if (fc == null)
                                    {
                                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to locate dissolve output: " + dissolve_fc_name, LogMessageType.ERROR), true, ++val);
                                        return false;
                                    }

                                    using (RowCursor rowCursor = fc.Search())
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

                        // Write this information to the PU Features table
                        if (!await QueuedTask.Run(async () =>
                        {
                            try
                            {
                                // Get the Area and Coverage fields for this feature
                                string AreaField = prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA;
                                string CoverageField = prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_COVERAGE;
                                string StateField = prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_STATE;

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
                                        WhereClause = $"{PRZC.c_FLD_TAB_PUCF_ID} = {PUID}"
                                    };

                                    using (Table table = await PRZH.GetTable_PUFeatures())
                                    using (RowCursor rowCursor = table.Search(QF))
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
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error writing dissolve info to the {PRZC.c_TABLE_PUFEATURES} table...", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error writing dissolve info to the {PRZC.c_TABLE_PUFEATURES} table.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Dissolve info written to {PRZC.c_TABLE_PUFEATURES}..."), true, ++val);
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
                    else if (CF.Layer_Object is RasterLayer RL)
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
                        string tabname = "cf_zonal_temp";

                        // Calculate Zonal Statistics as Table
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Executing Zonal Statistics as Table for feature {cfid}..."), true, ++val);
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
                                var tryget_gdb = await PRZH.GetGDB_Project();
                                if (!tryget_gdb.success)
                                {
                                    return false;
                                }

                                using (Geodatabase gdb = tryget_gdb.geodatabase)
                                using (Table table = await PRZH.GetTable(gdb, tabname))
                                using (RowCursor rowCursor = table.Search())
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
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {tabname} table..."), true, ++val);
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

                        // Get the Area, Coverage, and State fields for this Feature
                        string AreaField = prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA;
                        string CoverageField = prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_COVERAGE;
                        string StateField = prefix + PRZC.c_FLD_TAB_PUCF_SUFFIX_STATE;

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
                                WhereClause = PRZC.c_FLD_TAB_PUCF_ID + " = " + PUID.ToString()
                            };

                            if (!await QueuedTask.Run(async () =>
                            {
                                try
                                {
                                    using (Table table = await PRZH.GetTable_PUFeatures())
                                    using (RowCursor rowCursor = table.Search(QF))
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
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error writing feature {cfid} info to the {PRZC.c_TABLE_PUFEATURES} table...", LogMessageType.ERROR), true, ++val);
                                ProMsgBox.Show($"Error writing feature {cfid} info to the {PRZC.c_TABLE_PUFEATURES} table.");
                                return false;
                            }
                        }
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Feature {cfid} layer is neither a FeatureLayer or a RasterLayer", LogMessageType.ERROR), true, ++val);
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
                                    FeatureElement feature = new FeatureElement();

                                    feature.CF_ID = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_ID]);
                                    feature.CF_Name = (row[PRZC.c_FLD_TAB_CF_NAME] == null) ? "" : row[PRZC.c_FLD_TAB_CF_NAME].ToString();
                                    feature.CF_MinThreshold = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_MIN_THRESHOLD]);
                                    feature.CF_Goal = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_GOAL]);
                                    feature.CF_Enabled = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_ENABLED]);
                                    feature.CF_Hidden = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_HIDDEN]);
                                    feature.CF_Area_M2 = Convert.ToDouble(row[PRZC.c_FLD_TAB_CF_AREA_M2]);
                                    feature.CF_Area_Ac = Convert.ToDouble(row[PRZC.c_FLD_TAB_CF_AREA_AC]);
                                    feature.CF_Area_Ha = Convert.ToDouble(row[PRZC.c_FLD_TAB_CF_AREA_HA]);
                                    feature.CF_Area_Km2 = Convert.ToDouble(row[PRZC.c_FLD_TAB_CF_AREA_KM2]);
                                    feature.CF_PUCount = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_PUCOUNT]);
                                    feature.CF_WhereClause = (row[PRZC.c_FLD_TAB_CF_WHERECLAUSE] == null) ? "" : row[PRZC.c_FLD_TAB_CF_WHERECLAUSE].ToString();
                                    feature.Layer_Name = (row[PRZC.c_FLD_TAB_CF_LYR_NAME] == null) ? "" : row[PRZC.c_FLD_TAB_CF_LYR_NAME].ToString();
                                    feature.Layer_Json = (row[PRZC.c_FLD_TAB_CF_LYR_JSON] == null) ? "" : row[PRZC.c_FLD_TAB_CF_LYR_JSON].ToString();

                                    string lt = (row[PRZC.c_FLD_TAB_CF_LYR_TYPE] == null) ? "" : row[PRZC.c_FLD_TAB_CF_LYR_TYPE].ToString();
                                    if (lt == FeatureLayerType.RASTER.ToString())
                                    {
                                        feature.Layer_Type = FeatureLayerType.RASTER;
                                    }
                                    else if (lt == FeatureLayerType.FEATURE.ToString())
                                    {
                                        feature.Layer_Type = FeatureLayerType.FEATURE;
                                    }

                                    LIST_Features.Add(feature);
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

                // Sort them
                LIST_Features.Sort((x, y) => x.CF_ID.CompareTo(y.CF_ID));

                Features = new ObservableCollection<FeatureElement>(LIST_Features);

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
                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                // Some paths
                string gdbpath = PRZH.GetPath_ProjectGDB();
                string cfpath = PRZH.GetPath_Table_Features();
                string pucfpath = PRZH.GetPath_Table_PUFeatures();

                // Initialize ProgressBar and Progress Log
                int max = 20;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing..."), false, max, ++val);

                // Validation: Prompt User for permission to proceed
                if (ProMsgBox.Show("If you proceed, the following will happen:" +
                   Environment.NewLine +
                   $"1. The {PRZC.c_TABLE_FEATURES} and {PRZC.c_TABLE_PUFEATURES} tables will be deleted from the project geodatabase (if they exist)" + Environment.NewLine +
                   $"2. All values in the {PRZC.c_FLD_FC_PU_FEATURECOUNT} field in the {PRZC.c_FC_PLANNING_UNITS} feature class will be set to 0" +
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

                // Delete the Features table
                if (await PRZH.TableExists_Features())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting the {PRZC.c_TABLE_FEATURES} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(cfpath, "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_FEATURES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_FEATURES} table.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_FEATURES} table deleted successfully..."), true, ++val);
                    }
                }

                // Delete the PUFeatures table
                if (await PRZH.TableExists_PUFeatures())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting the {PRZC.c_TABLE_PUFEATURES} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(pucfpath, "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_PUFEATURES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_PUFEATURES} table.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_PUFEATURES} table deleted successfully..."), true, ++val);
                    }
                }

                // Update PUFC, set cfcount field to 0
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Resetting {PRZC.c_FLD_FC_PU_FEATURECOUNT} field in {PRZC.c_FC_PLANNING_UNITS} Feature Class..."), true, ++val);

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
                                    row[PRZC.c_FLD_FC_PU_FEATURECOUNT] = 0;

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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error resetting the {PRZC.c_FLD_FC_PU_FEATURECOUNT} field in {PRZC.c_FC_PLANNING_UNITS} Feature Class.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error resetting the {PRZC.c_FLD_FC_PU_FEATURECOUNT} field in {PRZC.c_FC_PLANNING_UNITS} Feature Class.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field updated successfully."), true, ++val);
                }

                // Determine the presence of 2 tables, and enable/disable the clear button accordingly
                FeaturesTableExists = await PRZH.TableExists_Features();
                PUFeaturesTableExists = await PRZH.TableExists_PUFeatures();
                FeaturesExist = FeaturesTableExists || PUFeaturesTableExists;

                // Populate the grid
                if (!await PopulateGrid())
                {
                    ProMsgBox.Show("Error loading the Features grid");
                }

                ProMsgBox.Show("Features Removed.");

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