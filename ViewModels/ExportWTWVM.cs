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

        #region Properties

        private bool _exportIsEnabled = false;
        public bool ExportIsEnabled
        {
            get => _exportIsEnabled; set => SetProperty(ref _exportIsEnabled, value, () => ExportIsEnabled);
        }

        private string _compStat_PUFC = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
        public string CompStat_PUFC
        {
            get => _compStat_PUFC; set => SetProperty(ref _compStat_PUFC, value, () => CompStat_PUFC);
        }

        private string _compStat_CF = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
        public string CompStat_CF
        {
            get => _compStat_CF; set => SetProperty(ref _compStat_CF, value, () => CompStat_CF);
        }

        private string _compStat_Bounds = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
        public string CompStat_Bounds
        {
            get => _compStat_Bounds; set => SetProperty(ref _compStat_Bounds, value, () => CompStat_Bounds);
        }

        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""
        public ProgressManager PM
        {
            get => _pm; set => SetProperty(ref _pm, value, () => PM);
        }

        #endregion

        #region Commands

        private ICommand _cmdExport;
        public ICommand CmdExport => _cmdExport ?? (_cmdExport = new RelayCommand(() => ExportWTWPackage(), () => true));

        private ICommand _cmdClearLog;
        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true));

        #endregion

        #region Methods

        public async void OnProWinLoaded()
        {
            try
            {
                // Initialize the Progress Bar & Log
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                // Initialize the indicator images
                bool PUFC_OK = await PRZH.FCExists_PU();
                bool CF_OK = await PRZH.TableExists_Features();
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

                if (CF_OK)
                {
                    CompStat_CF = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_CF = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
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
                ExportIsEnabled = PUFC_OK & CF_OK & Bounds_OK;

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
                if (!PRZH.FolderExists_ExportWTW())
                {
                    ProMsgBox.Show($"The {PRZC.c_DIR_EXPORT_WTW} folder does not exist in your project workspace." + Environment.NewLine + Environment.NewLine +
                                    "Please Initialize or Reset your workspace");
                    return false;
                }

                // Prompt the user for permission to proceed
                if (ProMsgBox.Show("Suitable prompt goes here" +
                   Environment.NewLine + Environment.NewLine +
                   "Do you wish to proceed?" +
                   Environment.NewLine + Environment.NewLine +
                   "Choose wisely...",
                   "File Overwrite Warning",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out."), true, ++val);
                    return false;
                }

                #endregion

                #region PREPARATION

                // Delete all existing files within export dir
                string exportpath = PRZH.GetPath_ExportWTWFolder();
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

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Making a temp copy of the Planning Unit FC..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pufcpath, temppufc, "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CopyFeatures_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error copying the Planning Unit FC.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Planning Unit FC copied successfully..."), true, ++val);
                }

                // Delete all unnecessary fields from the temp fc
                List<string> LIST_DeleteFields = new List<string>();

                if (!await QueuedTask.Run(async () =>
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
                }))
                {
                    ProMsgBox.Show("Unable to assemble the list of deletable fields");
                    return false;
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Removing unnecessary fields from the temp Planning Unit FC..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(temppufc, LIST_DeleteFields);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting fields from temp PU FC.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Temp PU FC fields successfully deleted"), true, ++val);
                }

                // Repair geometry
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Repairing geometry for temp Planning Unit FC..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(temppufc);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("RepairGeometry_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error repairing geometry.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Geometry successfully repaired"), true, ++val);
                }

                // Export to Shapefile in EXPORT_WTW folder
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Exporting PU FC to Shapefile..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(temppufc, exportfcpath);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CopyFeatures_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error creating the shapefile.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Shapefile created successfully..."), true, ++val);
                }

                // Finally, delete the temp feature class
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting temporary feature class..."), true);

                toolParams = Geoprocessing.MakeValueArray(temppufc, "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting temp feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Temp Feature Class deleted successfully."), true);
                }


                // Delete the two no-longer-required area and length fields from the shapefile
                LIST_DeleteFields = new List<string>();

                if (!await QueuedTask.Run(() =>
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
                }))
                {
                    ProMsgBox.Show("Unable to assemble the list of deletable fields");
                    return false;
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Removing unnecessary fields from shapefile..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(exportfcpath, LIST_DeleteFields);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: exportdirpath);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting fields from shapefile.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Shapefile fields deleted successfully"), true, ++val);
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Shapefile Export Complete!"), true, ++val);

                #endregion

                #region GENERATE AND ZIP THE ATTRIBUTE CSV

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

                // Retrieve Info from the CF Table
                var DICT_CF = new Dictionary<int, (string name, string varname, double area, bool inuse, int goal, int thresh)>();

                await QueuedTask.Run(async () =>
                {
                    using (Table table = await PRZH.GetTable_Features())
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                int cf_id = (int)row[PRZC.c_FLD_TAB_CF_ID];
                                string cf_name = row[PRZC.c_FLD_TAB_CF_NAME].ToString();
                                double area = (double)row[PRZC.c_FLD_TAB_CF_AREA_KM];          // total area, this may not really be important
                                bool used = true; //row[PRZC.c_FLD_CF_IN_USE];
                                int goal = (int)row[PRZC.c_FLD_TAB_CF_TARGET_PCT];
                                int thresh = (int)row[PRZC.c_FLD_TAB_CF_MIN_THRESHOLD_PCT];
                                string cf_varname = "CF_" + cf_id.ToString("D3");   // id 5 will look like '005'

                                DICT_CF.Add(cf_id, (cf_name, cf_varname, area, used, goal, thresh));
                            }
                        }
                    }
                });


                var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = false, // this is default
                    NewLine = Environment.NewLine
                };

                using (var writer = new StreamWriter(attributepath))
                using (var csv = new CsvWriter(writer, csvConfig))
                {
                    // *** ROW 1 => COLUMN NAMES

                    // CF Variable Name Columns
                    foreach(int cfid in DICT_CF.Keys)
                    {
                        csv.WriteField(DICT_CF[cfid].varname);
                    }

                    // PU ID Column
                    //csv.WriteField(PRZC.c_FLD_PUFC_ID);
                    csv.WriteField("_index");               // PUID field must be called "_index" and must be the final column.

                    csv.NextRecord();

                    // *** ROWS 2 TO N => PLANNING UNIT RECORDS
                    await QueuedTask.Run(async () =>
                    {
                        using (Table table = await PRZH.GetTable_PUFeatures())
                        using (RowCursor rowCursor = table.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    foreach (int cfid in DICT_CF.Keys)
                                    {
                                        // Get the PUVCF area field for this CF
                                        string fldname = PRZC.c_FLD_TAB_PUCF_PREFIX_CF + cfid.ToString() + PRZC.c_FLD_TAB_PUCF_SUFFIX_AREA;

                                        double area = (double)row[fldname]; // area in square meters

                                        // If I want to report "amount" of each CF in a way different from Area (m2), this is where I should do it!

                                        // *****

                                        double area_ha = Math.Round((area * PRZC.c_CONVERT_M2_TO_HA), 2, MidpointRounding.AwayFromZero);
                                        double area_km2 = Math.Round((area * PRZC.c_CONVERT_M2_TO_KM2), 3, MidpointRounding.AwayFromZero);

                                        csv.WriteField(area_km2);
                                        // *****

                                    }

                                    int puid = (int)row[PRZC.c_FLD_TAB_PUCF_ID];
                                    csv.WriteField(puid);

                                    csv.NextRecord();
                                }
                            }    
                        }
                    });
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

                foreach (var KVP in DICT_CF)
                {
                    int cfid = KVP.Key;


                    // Set goal between 0 and 1 inclusive
                    int g = KVP.Value.goal; // g is between 0 and 100 inclusive
                    double dg = Math.Round(g / 100.0, 2, MidpointRounding.AwayFromZero); // I need dg to be between 0 and 1 inclusive

                    YamlLegend yamlLegend = new YamlLegend();
                    yamlLegend.type = WTWLegendType.continuous.ToString();

                    YamlVariable yamlVariable = new YamlVariable();
                    yamlVariable.index = KVP.Value.varname;
                    yamlVariable.units = "km\xB2";
                    yamlVariable.provenance = (cfid % 2 == 0) ? WTWProvenanceType.national.ToString() : WTWProvenanceType.regional.ToString();
                    yamlVariable.legend = yamlLegend;

                    YamlFeature yamlFeature = new YamlFeature();
                    yamlFeature.name = KVP.Value.name;
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

                ProMsgBox.Show("Done");

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
