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
    public class BoundaryLengthsVM : PropertyChangedBase
    {
        public BoundaryLengthsVM()
        {
        }

        #region Properties

        private bool _boundaryTableExists;
        public bool BoundaryTableExists
        {
            get => _boundaryTableExists;
            set => SetProperty(ref _boundaryTableExists, value, () => BoundaryTableExists);
        }

        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""
        public ProgressManager PM
        {
            get => _pm; set => SetProperty(ref _pm, value, () => PM);
        }

        #endregion

        #region Commands

        private ICommand _cmdClearLog;
        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true));

        private ICommand _cmdBuildBoundaryTable;
        public ICommand CmdBuildBoundaryTable => _cmdBuildBoundaryTable ?? (_cmdBuildBoundaryTable = new RelayCommand(async () => await BuildBoundaryTable(), () => true));

        #endregion

        #region METHODS

        public async Task OnProWinLoaded()
        {
            try
            {
                //// Initialize the Progress Bar & Log
                //PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                //// Set the Conflict Override value default
                //SelectedOverrideOption = c_OVERRIDE_INCLUDE;

                //// Populate the Grid
                //bool Populated = await PopulateConflictGrid();

                BoundaryTableExists = await PRZH.BoundaryTableExists();

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        public async Task<bool> BuildBoundaryTable()
        {
            int val = 0;

            try
            {
                #region INITIALIZATION AND VALIDATION

                Map map = MapView.Active.Map;

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Initialize ProgressBar and Progress Log
                int max = 50;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Boundary Table generator..."), false, max, ++val);

                // Validation: Project Geodatabase
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

                if (!PRZH.FeatureLayerExists_PU(map))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Layer not found in the map.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    return false;
                }

                // Get the PU Feature Layer and its Spatial Reference
                FeatureLayer PUFL = null;
                SpatialReference PUFL_SR = null;
                await QueuedTask.Run(() =>
                {
                    PUFL = PRZH.GetFeatureLayer_PU(map);
                    PUFL_SR = PUFL.GetSpatialReference();

                    // clear selection as well
                    PUFL.ClearSelection();
                });

                // Notify users what will happen if they proceed
                if (ProMsgBox.Show("If you proceed, the Boundary Length table will be overwritten (if it exists)." +
                                   Environment.NewLine + Environment.NewLine +
                                   "Do you wish to proceed?" +
                                   Environment.NewLine + Environment.NewLine +
                                   "Choose wisely...",
                                   "Table Overwrite Warning",
                                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out."), true, ++val);
                    return false;
                }

                #endregion

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                #region GEOPROCESSING STEPS

                // Delete the Boundary table if present
                string boundpath = PRZH.GetBoundaryTablePath();

                if (await PRZH.BoundaryTableExists())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting Boundary Length table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(boundpath);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting the Boundary Length table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully deleted the Boundary Length table."), true, ++val);
                    }
                }

                // Polygon Neighbors tool
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Executing Polygon Neighbors geoprocessing tool..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PUFL, boundpath, PRZC.c_FLD_PUFC_ID, "NO_AREA_OVERLAP", "NO_BOTH_SIDES", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("PolygonNeighbors_analysis", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error executing the Polygon Neighbors geoprocessing tool.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully executed the Polygon Neighbors tool."), true, ++val);
                }

                // Delete all zero length records
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting node connections from Boundary Length table..."), true, ++val);
                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        QueryFilter QF = new QueryFilter
                        {
                            WhereClause = "LENGTH = 0"
                        };

                        using (Table table = await PRZH.GetBoundaryTable())
                        {
                            table.DeleteRows(QF);
                        }

                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to delete node connection records from Boundary Length table...", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Unable to delete all node connection records from Boundary Length table...");
                    return false;
                }

                #endregion

                #region CALCULATE EXTERIOR PLANNING UNITS AND FINALIZE TABLE

                // Get perimeter of planning units
                Dictionary<int, double> DICT_PUID_and_perimeter = new Dictionary<int, double>();
                List<int> LIST_PUID = new List<int>();

                await QueuedTask.Run(async () =>
                {
                    using (FeatureClass featureClass = await PRZH.GetPlanningUnitFC())
                    using (RowCursor rowCursor = featureClass.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Feature feature = (Feature)rowCursor.Current)
                            {
                                int puid = Convert.ToInt32(feature[PRZC.c_FLD_PUFC_ID]);
                                Polygon polygon = (Polygon)feature.GetShape();
                                double perim = polygon.Length;

                                LIST_PUID.Add(puid);

                                if (puid > 0)
                                {
                                    DICT_PUID_and_perimeter.Add(puid, perim);
                                }
                            }
                        }
                    }
                });

                // Add Fields to Table
                string fldPUID1 = PRZC.c_FLD_BL_ID1 + " LONG 'Planning Unit ID 1' # # #;";
                string fldPUID2 = PRZC.c_FLD_BL_ID2 + " LONG 'Planning Unit ID 2' # # #;";
                string fldBoundary = PRZC.c_FLD_BL_BOUNDARY + " DOUBLE 'Boundary Length' # # #;";

                string flds = fldPUID1 + fldPUID2 + fldBoundary;

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Adding fields to Boundary Length table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(boundpath, flds);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, null, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding fields to Boundary Length table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully added fields."), true, ++val);
                }

                // Calculate the two id fields
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Calculating PUID 1 field..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(boundpath, PRZC.c_FLD_BL_ID1, $"!src_id!");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CalculateField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error Calculating {PRZC.c_FLD_BL_ID1} Field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field calculated successfully."), true, ++val);
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Calculating PUID 2 field..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(boundpath, PRZC.c_FLD_BL_ID2, $"!nbr_id!");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CalculateField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error Calculating {PRZC.c_FLD_BL_ID2} Field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field calculated successfully."), true, ++val);
                }

                // Calculate the Boundary length field
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Calculating boundary length field..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(boundpath, PRZC.c_FLD_BL_BOUNDARY, $"!LENGTH!");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CalculateField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error Calculating {PRZC.c_FLD_BL_BOUNDARY} Field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field calculated successfully."), true, ++val);
                }

                // Delete the unnecessary fields
                string[] delfld = { "src_id", "nbr_id", "LENGTH", "NODE_COUNT" };

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting unnecessary fields..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(boundpath, delfld);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting unnecessary fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields deleted successfully."), true, ++val);
                }

                // Index both id fields
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Indexing both id fields..."), true, ++val);
                List<string> LIST_ix = new List<string>() { PRZC.c_FLD_BL_ID1, PRZC.c_FLD_BL_ID2 };
                toolParams = Geoprocessing.MakeValueArray(boundpath, LIST_ix, "ix" + PRZC.c_FLD_PUFC_ID, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields indexed successfully."), true, ++val);
                }

                // Now, for each PUID, sum the boundary lengths from the table
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Summing the shared perimeter values per planning unit..."), true, ++val);
                Dictionary<int, double> DICT_PUID_and_sum_lengths = new Dictionary<int, double>();
                //foreach (KeyValuePair<int, double> KVP in DICT_PUID_and_perimeter)
                foreach (int puid in LIST_PUID)
                {
                    QueryFilter QF = new QueryFilter
                    {
                        WhereClause = PRZC.c_FLD_BL_ID1 + " = " + puid.ToString() + " Or " + PRZC.c_FLD_BL_ID2 + " = " + puid.ToString()
                    };

                    double lengthsum = 0;

                    await QueuedTask.Run(async () =>
                    {
                        using (Table table = await PRZH.GetBoundaryTable())
                        using (RowCursor rowCursor = table.Search(QF, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    lengthsum += Convert.ToDouble(row[PRZC.c_FLD_BL_BOUNDARY]);
                                }
                            }
                        }
                    });

                    DICT_PUID_and_sum_lengths.Add(puid, lengthsum);
                }

                // Update the shared perimeter and unshared edge columns in PUFC
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Updating shared perimeter field in PU FC..."), true, ++val);
                await QueuedTask.Run(async () =>
                {
                    using (Table table = await PRZH.GetPlanningUnitFC())
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                int puid = Convert.ToInt32(row[PRZC.c_FLD_PUFC_ID]);

                                if (DICT_PUID_and_sum_lengths.ContainsKey(puid))
                                {
                                    double shared = DICT_PUID_and_sum_lengths[puid];    // shared edge length
                                    double total = DICT_PUID_and_perimeter[puid];       // total edge length

                                    double shared_rounded = Math.Round(shared, 2, MidpointRounding.AwayFromZero);
                                    double total_rounded = Math.Round(total, 2, MidpointRounding.AwayFromZero);

                                    row[PRZC.c_FLD_PUFC_SHARED_PERIM] = shared;

                                    if (shared_rounded == total_rounded)
                                    {
                                        row[PRZC.c_FLD_PUFC_HAS_UNSHARED_EDGE] = 0;
                                    }
                                    else
                                    {
                                        row[PRZC.c_FLD_PUFC_HAS_UNSHARED_EDGE] = 1;
                                    }

                                    row.Store();
                                }
                            }
                        }
                    }
                });

                #endregion

                // Compact the Geodatabase
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacting the Geodatabase..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath);
                toolOutput = await PRZH.RunGPTool("Compact_management", toolParams, null, GPExecuteToolFlags.None);
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

                BoundaryTableExists = await PRZH.BoundaryTableExists();
                ProMsgBox.Show("Construction Completed Sucessfully!" + Environment.NewLine + Environment.NewLine + message);
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