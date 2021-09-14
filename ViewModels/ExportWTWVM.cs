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
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.Converters;

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
                bool PUFC_OK = await PRZH.PlanningUnitFCExists();
                bool CF_OK = await PRZH.CFTableExists();
                bool Bounds_OK = await PRZH.BoundaryTableExists();

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
                if (!PRZH.ExportWTWFolderExists())
                {
                    ProMsgBox.Show($"The {PRZC.c_WS_EXPORT_WTW} folder does not exist in your project workspace." + Environment.NewLine + Environment.NewLine +
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
                string exportpath = PRZH.GetExportWTWFolderPath();
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

                #region SHAPEFILE EXPORT

                // STEP 1: Generate and format the shapefile
                //          - Copy FC to a temp fgdb fc - Project as part of this to web mercator!
                //          - Repair geometry (just in case)
                //          - MakeFeatureLayer, remove all but a few fields
                //          - CopyFeatures, set output location to folder (hence shapefile output)
                //                  - set output location to a filedirectoryworkspace, which should make a shapefile (?)

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

                string gdbpath = PRZH.GetProjectGDBPath();
                string pufcpath = PRZH.GetPlanningUnitFCPath();
                string exportdirpath = PRZH.GetExportWTWFolderPath();
                string exportfcpath = Path.Combine(exportdirpath, PRZC.c_EXPORTWTW_SHAPEFILE);

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
                                                                        && f.Name != PRZC.c_FLD_PUFC_ID
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
                    using (FeatureClass shpFC = fsDS.OpenDataset<FeatureClass>(PRZC.c_EXPORTWTW_SHAPEFILE))
                    {
                        if (shpFC == null)
                        {
                            return false;
                        }

                        FeatureClassDefinition fcDef = shpFC.GetDefinition();
                        List<Field> fields = fcDef.GetFields().Where(f => f.Name != fcDef.GetObjectIDField()
                                                                        && f.Name != PRZC.c_FLD_PUFC_ID
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

                // STEP 2: Generate the Attribute file

                // The Attribute CSV file contains information for 3 components of a Prioritizr Project: Conservation Features, Includes, and Weights (costs)
                // Initially I will only insert Conservation Features
                // Column 1 is PUID (name is 'id')
                // Column 2-n are Conservation Features
                // Rows = PUs
                // Column Header is the CF Name
                // Column values are the amount of CF in the PUs (rows)

                // a)  Create Table with Column 1 = PUID, and one row per planning unit
                // b)  From the PUVCF table, copy & paste (not really) each AREA column into new table




                // STEP 3: Generate the Boundary file




                //YamlLegend l = new YamlLegend();
                //l.colors = new string[] { "a", "b", "c" };
                //l.labels = new string[] { "label a", "label b", "label c" };
                //l.type = "manual";

                //ISerializer builder = new SerializerBuilder().DisableAliases().Build();
                //string s = builder.Serialize(l);

                //ProMsgBox.Show(s, "YAML!");

                //var builder2 = new SerializerBuilder().DisableAliases().JsonCompatible().Build();
                //string t = builder2.Serialize(l);

                //ProMsgBox.Show(t, "JSON!");

                //var builder3 = new SerializerBuilder().DisableAliases().ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build();
                //string u = builder3.Serialize(l);

                //ProMsgBox.Show(u, "Yaml No Nulls!");

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
