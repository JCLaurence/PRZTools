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
using PRZM = NCC.PRZTools.PRZMethods;
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

        private bool _exportIsEnabled = false;
        private string _compStat_PUFC = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
        private string _compStat_SelRules = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Warn16.png";
        private string _compStat_Weights = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Warn16.png";
        private string _compStat_Features = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
        private string _compStat_Bounds = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""
        private ICommand _cmdExport;
        private ICommand _cmdClearLog;

        #endregion

        #region PROPERTIES

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

        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true));

        #endregion

        #region METHODS

        public async void OnProWinLoaded()
        {
            try
            {
                // Initialize the Progress Bar & Log
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                // Initialize the indicator images
                bool PUFC_OK = await PRZH.FCExists_PU();
                bool SelRules_OK = await PRZH.TableExists_SelRules();
                bool Weights_OK = false;    // TODO: ADD THIS LATER
                bool Features_OK = await PRZH.TableExists_Features();
                bool Bounds_OK = await PRZH.TableExists_Boundary();

                // Set the Component Status Images
                if (PUFC_OK)
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
                ExportIsEnabled = PUFC_OK & Features_OK & Bounds_OK;

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> ExportWTWPackage()
        {
            int val = 0;

            try
            {
                // Initialize a few thingies
                Map map = MapView.Active.Map;

                // Initialize ProgressBar and Progress Log
                int max = 50;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the WTW Exporter..."), false, max, ++val);

                #region VALIDATION

                // Ensure the ExportWTW folder exists
                string exportpath = PRZH.GetPath_ExportWTWFolder();
                if (!PRZH.FolderExists_ExportWTW())
                {
                    ProMsgBox.Show($"The {PRZC.c_DIR_EXPORT_WTW} folder does not exist in your project workspace." + Environment.NewLine + Environment.NewLine +
                                    "Please Initialize or Reset your workspace");
                    return false;
                }

                // Prompt the user for permission to proceed
                if (ProMsgBox.Show("If you proceed, all files in the following folder will be deleted and/or overwritten:" + Environment.NewLine +
                    exportpath + Environment.NewLine + Environment.NewLine +
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

                #region PREPARATION

                // Delete all existing files within export dir
                DirectoryInfo di = new DirectoryInfo(exportpath);

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
                    ProMsgBox.Show("Unable to delete files & subfolders within " + exportpath + Environment.NewLine + Environment.NewLine + ex.Message);
                    return false;
                }

                #endregion

                #region GENERATE THE SHAPEFILE

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Prepare the necessary output coordinate system
                SpatialReference OutputSR = SpatialReferenceBuilder.CreateSpatialReference(4326);   // (WGS84 (GCS)

                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                string gdbpath = PRZH.GetPath_ProjectGDB();
                string pufcpath = PRZH.GetPath_FC_PU();
                string exportdirpath = PRZH.GetPath_ExportWTWFolder();
                string exportfcpath = Path.Combine(exportdirpath, PRZC.c_FILE_WTW_EXPORT_SHP);

                // Copy PUFC, project at the same time
                string temppufc = "temppu_wtw";

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Making a temp copy of the {PRZC.c_FC_PLANNING_UNITS} feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pufcpath, temppufc, "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CopyFeatures_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying the {PRZC.c_FC_PLANNING_UNITS} feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error copying the {PRZC.c_FC_PLANNING_UNITS} feature class.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_FC_PLANNING_UNITS} feature class copied successfully..."), true, ++val);
                }

                // Delete all unnecessary fields from the temp fc
                List<string> LIST_DeleteFields = new List<string>();

                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (Geodatabase geodatabase = await PRZH.GetProjectGDB())
                        using (FeatureClass fc = await PRZH.GetFeatureClass(geodatabase, temppufc))
                        {
                            if (fc == null)
                            {
                                return false;
                            }

                            FeatureClassDefinition fcDef = fc.GetDefinition();
                            List<Field> fields = fcDef.GetFields().Where(f => f.Name != fcDef.GetObjectIDField()
                                                                            && f.Name != PRZC.c_FLD_FC_PU_ID
                                                                            && f.Name != fcDef.GetShapeField()
                                                                            && f.Name != fcDef.GetAreaField()
                                                                            && f.Name != fcDef.GetLengthField()
                                                                            ).ToList();

                            foreach (Field field in fields)
                            {
                                LIST_DeleteFields.Add(field.Name);
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying the {PRZC.c_FC_PLANNING_UNITS} feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Unable to assemble the list of deletable fields");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Feature class copied."), true, ++val);
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Removing unnecessary fields from the temporary feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(temppufc, LIST_DeleteFields);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting fields from temporary feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error deleting fields from temporary feature class.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields deleted."), true, ++val);
                }

                // Repair geometry
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Repairing geometry..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(temppufc);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("RepairGeometry_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error repairing geometry.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error repairing geometry.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Geometry successfully repaired"), true, ++val);
                }

                // Export to Shapefile in EXPORT_WTW folder
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Exporting Shapefile..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(temppufc, exportfcpath);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CopyFeatures_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error exporting shapefile.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error exporting shapefile.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Shapefile exported."), true, ++val);
                }

                // Finally, delete the temp feature class
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting temporary feature class..."), true);

                toolParams = Geoprocessing.MakeValueArray(temppufc, "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting temporary feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true);
                    ProMsgBox.Show("Error deleting temporary feature class.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Temporary feature class deleted."), true);
                }

                // Delete the two no-longer-required area and length fields from the shapefile
                LIST_DeleteFields = new List<string>();

                if (!await QueuedTask.Run(() =>
                {
                    try
                    {
                        FileSystemConnectionPath fsConn = new FileSystemConnectionPath(new Uri(exportdirpath), FileSystemDatastoreType.Shapefile);

                        using (FileSystemDatastore fsDS = new FileSystemDatastore(fsConn))
                        using (FeatureClass shpFC = fsDS.OpenDataset<FeatureClass>(PRZC.c_FILE_WTW_EXPORT_SHP))
                        {
                            if (shpFC == null)
                            {
                                return false;
                            }

                            FeatureClassDefinition fcDef = shpFC.GetDefinition();
                            List<Field> fields = fcDef.GetFields().Where(f => f.Name != fcDef.GetObjectIDField()
                                                                            && f.Name != PRZC.c_FLD_FC_PU_ID
                                                                            && f.Name != fcDef.GetShapeField()
                                                                            ).ToList();

                            foreach (Field field in fields)
                            {
                                LIST_DeleteFields.Add(field.Name);
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving the list of deletable shapefile fields.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error retrieving the list of deletable shapefile fields.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Fields retrieved."), true, ++val);
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting unnecessary fields from shapefile..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(exportfcpath, LIST_DeleteFields);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: exportdirpath);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting fields from shapefile.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error deleting fields from shapefile.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields deleted."), true, ++val);
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Shapefile Export Complete!"), true, ++val);

                #endregion

                #region GET MASTER PLANNING UNIT ID LIST

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving master list of planning unit ids..."), true, ++val);

                List<int> LIST_PUIDs = await PRZH.GetPlanningUnitIDs();
                if (LIST_PUIDs == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving master list of planning unit ids", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving master list of planning unit ids");
                    return false;
                }

                LIST_PUIDs.Sort();    // THIS IS THE MASTER LIST OF PLANNING UNIT IDS.

                #endregion

                #region COMPILE FEATURE INFORMATION

                // Retrieve key info from the Feature Table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving records from the {PRZC.c_TABLE_FEATURES} table..."), true, ++val);
                var LIST_FeatureIDs = new List<int>();
                var DICT_Features = new Dictionary<int, (string feature_name, string variable_name, bool enabled, int goal)>();

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
                                    // Feature ID
                                    int cfid = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_ID]);

                                    // Names
                                    string name = (row[PRZC.c_FLD_TAB_CF_NAME] == null | row[PRZC.c_FLD_TAB_CF_NAME] == DBNull.Value) ? "" : row[PRZC.c_FLD_TAB_CF_NAME].ToString();
                                    string varname = "CF_" + cfid.ToString("D3");   // Example:  for id 5, we get CF_005

                                    // Enabled
                                    bool enabled = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_ENABLED]) == 1;

                                    // Goal
                                    int goal = Convert.ToInt32(row[PRZC.c_FLD_TAB_CF_TARGET_PCT]);

                                    LIST_FeatureIDs.Add(cfid);
                                    DICT_Features.Add(cfid, (name, varname, enabled, goal));
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving information from the {PRZC.c_TABLE_FEATURES} table.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving records from the {PRZC.c_TABLE_FEATURES} table.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Records retrieved."), true, ++val);
                }

                LIST_FeatureIDs.Sort();

                #endregion

                #region COMPILE SELECTION RULE INFORMATION

                // Retrieve key information from the Selection Rule table

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving records from the {PRZC.c_TABLE_SELRULES} table..."), true, ++val);
                var LIST_IncludeIDs = new List<int>();
                var LIST_ExcludeIDs = new List<int>();
                var DICT_Includes = new Dictionary<int, (string include_name, string variable_name, bool enabled)>();
                var DICT_Excludes = new Dictionary<int, (string exclude_name, string variable_name, bool enabled)>();

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
                                    // Selection Rule ID
                                    int srid = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_ID]);

                                    // Enabled
                                    bool enabled = Convert.ToInt32(row[PRZC.c_FLD_TAB_SELRULES_ENABLED]) == 1;

                                    // Rule Name
                                    string name = (row[PRZC.c_FLD_TAB_SELRULES_NAME] == null | row[PRZC.c_FLD_TAB_SELRULES_NAME] == DBNull.Value) ? "" : row[PRZC.c_FLD_TAB_SELRULES_NAME].ToString();

                                    string ruletype = (row[PRZC.c_FLD_TAB_SELRULES_RULETYPE] == null | row[PRZC.c_FLD_TAB_SELRULES_RULETYPE] == DBNull.Value) ? "" : row[PRZC.c_FLD_TAB_SELRULES_RULETYPE].ToString();
                                    if (ruletype == SelectionRuleType.INCLUDE.ToString())
                                    {
                                        string varname = "SR_" + srid.ToString("D3") + "_IN";   // Example:  for id 5, we get SR_005_IN
                                        LIST_IncludeIDs.Add(srid);
                                        DICT_Includes.Add(srid, (name, varname, enabled));
                                    }
                                    else if (ruletype == SelectionRuleType.EXCLUDE.ToString())
                                    {
                                        string varname = "SR_" + srid.ToString("D3") + "_EX";   // Example:  for id 5, we get SR_005_EX
                                        LIST_ExcludeIDs.Add(srid);
                                        DICT_Excludes.Add(srid, (name, varname, enabled));
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving information from the {PRZC.c_TABLE_SELRULES} table.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving records from the {PRZC.c_TABLE_SELRULES} table.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Records retrieved."), true, ++val);
                }

                LIST_IncludeIDs.Sort();
                LIST_ExcludeIDs.Sort();

                #endregion

                #region COMPILE WEIGHTS INFORMATION

                // TODO: Add this

                #endregion

                #region GENERATE THE ATTRIBUTE CSV

                string attributepath = Path.Combine(exportdirpath, PRZC.c_FILE_WTW_EXPORT_ATTR);

                // If file exists, delete it
                try
                {
                    if (File.Exists(attributepath))
                    {
                        File.Delete(attributepath);
                    }
                }
                catch (Exception ex)
                {
                    ProMsgBox.Show("Unable to delete the existing Export WTW Attribute file..." +
                        Environment.NewLine + Environment.NewLine + ex.Message);
                    return false;
                }

                // Get the CSV file started
                var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = false, // this is default
                    NewLine = Environment.NewLine
                };

                using (var writer = new StreamWriter(attributepath))
                using (var csv = new CsvWriter(writer, csvConfig))
                {
                    // *** WRITE ROW #1 = COLUMN NAMES

                    // First, add the Feature Variable Name Columns
                    for (int i = 0; i < LIST_FeatureIDs.Count; i++)
                    {
                        int cf_id = LIST_FeatureIDs[i];
                        csv.WriteField(DICT_Features[cf_id].variable_name);
                    }

                    // Now the Include columns
                    for (int i = 0; i < LIST_IncludeIDs.Count; i++)
                    {
                        int sr_id = LIST_IncludeIDs[i];
                        csv.WriteField(DICT_Includes[sr_id].variable_name);
                    }

                    // Now the Exclude columns
                    for (int i = 0; i < LIST_ExcludeIDs.Count; i++)
                    {
                        int sr_id = LIST_ExcludeIDs[i];
                        csv.WriteField(DICT_Excludes[sr_id].variable_name);
                    }

                    // Insert the PU ID Column => must the "_index" and must be the final column in the attributes CSV
                    csv.WriteField("_index");
                    csv.NextRecord();   // First line is done!

                    // *** WRITE REGULAR ROWS - ONE PER PLANNING UNIT

                    for (int i = 0; i < LIST_PUIDs.Count; i++)  // Each iteration represents a single CSV row
                    {
                        int puid = LIST_PUIDs[i];   // ID of planning unit = this row

                        // WRITE THE FEATURE COLUMN VALUES
                        QueryFilter QF_F = new QueryFilter();
                        QF_F.WhereClause = PRZC.c_FLD_TAB_PUCF_ID + " = " + puid.ToString();

                        using (Table table = await PRZH.GetTable_PUFeatures())
                        using (RowCursor rowCursor = table.Search(QF_F, true))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    for (int j = 0; j < LIST_FeatureIDs.Count; j++)
                                    {
                                        int cfid = LIST_FeatureIDs[j];
                                        string areafield = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + cfid.ToString() + PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA;

                                        double area_m2 = Convert.ToDouble(row[areafield]);  // this is the "amount" of feature in this planning unit (area in m2)

                                        // *** THESE ARE OPTIONAL *********************************
                                        double area_ac = Math.Round((area_m2 * PRZC.c_CONVERT_M2_TO_HA), 2, MidpointRounding.AwayFromZero);
                                        double area_ha = Math.Round((area_m2 * PRZC.c_CONVERT_M2_TO_HA), 2, MidpointRounding.AwayFromZero);
                                        double area_km2 = Math.Round((area_m2 * PRZC.c_CONVERT_M2_TO_KM2), 2, MidpointRounding.AwayFromZero);
                                        // ********************************************************

                                        csv.WriteField(area_km2);       // this could be a user-specified option
                                    }
                                }
                            }
                        }

                        // WRITE THE INCLUDES COLUMN VALUES
                        QueryFilter QF_IN = new QueryFilter();
                        QF_IN.WhereClause = PRZC.c_FLD_TAB_PUSELRULES_ID + " = " + puid.ToString();

                        using (Table table = await PRZH.GetTable_PUSelRules())
                        using (RowCursor rowCursor = table.Search(QF_IN, true))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    for (int j = 0; j < LIST_FeatureIDs.Count; j++)
                                    {
                                        int srid = LIST_IncludeIDs[j];
                                        string areafield = PRZC.c_FLD_TAB_PUSELRULES_PREFIX_INCLUDE + srid.ToString() + PRZC.c_FLD_TAB_PUSELRULES_SUFFIX_AREA;

                                        double area_m2 = Convert.ToDouble(row[areafield]);  // this is the "amount" of include in this planning unit (area in m2)

                                        // *** THESE ARE OPTIONAL *********************************
                                        double area_ac = Math.Round((area_m2 * PRZC.c_CONVERT_M2_TO_HA), 2, MidpointRounding.AwayFromZero);
                                        double area_ha = Math.Round((area_m2 * PRZC.c_CONVERT_M2_TO_HA), 2, MidpointRounding.AwayFromZero);
                                        double area_km2 = Math.Round((area_m2 * PRZC.c_CONVERT_M2_TO_KM2), 2, MidpointRounding.AwayFromZero);
                                        // ********************************************************



                                        csv.WriteField(area_km2);       // this could be a user-specified option
                                    }
                                }
                            }
                        }
                    }


                }

                // Compress Attribute CSV to gzip format
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
                        ProMsgBox.Show("Unable to compress the Attribute CSV file to GZIP..." + Environment.NewLine + Environment.NewLine + ex.Message);
                        return false;
                    }
                }

                #endregion

                #region GENERATE AND ZIP THE BOUNDARY CSV

                string bndpath = Path.Combine(exportdirpath, PRZC.c_FILE_WTW_EXPORT_BND);

                // If file exists, delete it
                try
                {
                    if (File.Exists(bndpath))
                    {
                        File.Delete(bndpath);
                    }
                }
                catch (Exception ex)
                {
                    ProMsgBox.Show("Unable to delete the existing Export WTW Boundary file..." +
                        Environment.NewLine + Environment.NewLine + ex.Message);
                    return false;
                }

                using (var writer = new StreamWriter(bndpath))
                using (var csv = new CsvWriter(writer, csvConfig))
                {
                    // *** ROW 1 => COLUMN NAMES

                    // PU ID Columns
                    csv.WriteField(PRZC.c_FLD_TAB_BOUND_ID1);
                    csv.WriteField(PRZC.c_FLD_TAB_BOUND_ID2);
                    csv.WriteField(PRZC.c_FLD_TAB_BOUND_BOUNDARY);

                    csv.NextRecord();

                    // *** ROWS 2 TO N => Boundary Records
                    await QueuedTask.Run(async () =>
                    {
                        using (Table table = await PRZH.GetTable_Boundary())
                        using (RowCursor rowCursor = table.Search(null, true))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int id1 = (int)row[PRZC.c_FLD_TAB_BOUND_ID1];
                                    int id2 = (int)row[PRZC.c_FLD_TAB_BOUND_ID2];
                                    double bnd = (double)row[PRZC.c_FLD_TAB_BOUND_BOUNDARY];

                                    csv.WriteField(id1);
                                    csv.WriteField(id2);
                                    csv.WriteField(bnd);

                                    csv.NextRecord();
                                }
                            }
                        }
                    });
                }

                // Compress Boundary CSV to gzip format
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
                        ProMsgBox.Show("Unable to compress the Boundary CSV file to GZIP..." + Environment.NewLine + Environment.NewLine + ex.Message);
                        return false;
                    }
                }

                #endregion

                #region GENERATE THE YAML CONFIG FILE

                List<YamlTheme> LIST_YamlThemes = new List<YamlTheme>();

                foreach (var KVP in DICT_Features)
                {
                    int cfid = KVP.Key;


                    // Set goal between 0 and 1 inclusive
                    int g = KVP.Value.goal; // g is between 0 and 100 inclusive
                    double dg = Math.Round(g / 100.0, 2, MidpointRounding.AwayFromZero); // I need dg to be between 0 and 1 inclusive

                    YamlLegend yamlLegend = new YamlLegend();
                    yamlLegend.type = WTWLegendType.continuous.ToString();

                    YamlVariable yamlVariable = new YamlVariable();
                    yamlVariable.index = KVP.Value.variable_name;
                    yamlVariable.units = "km\xB2";
                    yamlVariable.provenance = (cfid % 2 == 0) ? WTWProvenanceType.national.ToString() : WTWProvenanceType.regional.ToString();
                    yamlVariable.legend = yamlLegend;

                    YamlFeature yamlFeature = new YamlFeature();
                    yamlFeature.name = KVP.Value.feature_name;
                    yamlFeature.status = true;
                    yamlFeature.visible = true;
                    yamlFeature.goal = dg;
                    yamlFeature.variable = yamlVariable;

                    YamlTheme yamlTheme = new YamlTheme();
                    yamlTheme.name = yamlFeature.name.Substring(0, 5).Trim();   // this is silly, come up with something better
                    yamlTheme.feature = new YamlFeature[] { yamlFeature };
                    LIST_YamlThemes.Add(yamlTheme);
                }


                // INCLUDES



                YamlPackage yamlPackage = new YamlPackage();
                yamlPackage.name = "TEMP PROJECT NAME";
                yamlPackage.mode = WTWModeType.advanced.ToString();
                yamlPackage.themes = LIST_YamlThemes.ToArray();
                yamlPackage.includes = new YamlInclude[] { };
                yamlPackage.weights = new YamlWeight[] { };

                ISerializer builder = new SerializerBuilder().DisableAliases().Build();
                string the_yaml = builder.Serialize(yamlPackage);

                string yamlpath = Path.Combine(exportdirpath, PRZC.c_FILE_WTW_EXPORT_YAML);
                try
                {
                    File.WriteAllText(yamlpath, the_yaml);
                }
                catch (Exception ex)
                {
                    ProMsgBox.Show("Unable to write the Yaml Config File..." + Environment.NewLine + Environment.NewLine + ex.Message);
                    return false;
                }

                #endregion

                ProMsgBox.Show("Export of WTW Files Complete :)");

                return true;
            }
            catch (Exception ex)
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        #endregion

    }
}
