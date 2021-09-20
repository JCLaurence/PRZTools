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

                BoundaryTableExists = await PRZH.TableExists_Boundary();

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
                string gdbpath = PRZH.GetPath_ProjectGDB();
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
                string pufcpath = PRZH.GetPath_FC_PU();
                if (!await PRZH.FCExists_PU())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Class not found in the Project Geodatabase.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Class is OK: " + pufcpath), true, ++val);
                }

                if (!PRZH.PRZLayerExists(map, PRZLayerNames.PU))
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
                string boundpath = PRZH.GetPath_Table_Boundary();

                if (await PRZH.TableExists_Boundary())
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

                // temporary table
                string boundtemp = "boundtemp";

                // Polygon Neighbors tool
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Executing Polygon Neighbors geoprocessing tool..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PUFL, boundtemp, PRZC.c_FLD_FC_PU_ID, "NO_AREA_OVERLAP", "NO_BOTH_SIDES", "", "", "");
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

                        using (Geodatabase geodatabase = await PRZH.GetProjectGDB())
                        using (Table table = await PRZH.GetTable(geodatabase, boundtemp))
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to delete node connection records from temp Boundary Length table...", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Unable to delete all node connection records from temp Boundary Length table...");
                    return false;
                }

                #endregion

                #region CALCULATE EXTERIOR PLANNING UNITS AND CREATE FINAL TABLE

                // First, get the perimeter of each planning unit
                List<int> LIST_PUID = new List<int>();                                                  // ALL PLANNING UNIT IDS
                Dictionary<int, double> DICT_PUID_and_perimeter = new Dictionary<int, double>();        // ALL PLANNING UNIT IDS AND THEIR PERIMETERS

                await QueuedTask.Run(async () =>
                {
                    using (FeatureClass featureClass = await PRZH.GetFC_PU())
                    using (RowCursor rowCursor = featureClass.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Feature feature = (Feature)rowCursor.Current)
                            {
                                int puid = Convert.ToInt32(feature[PRZC.c_FLD_FC_PU_ID]);
                                Polygon polygon = (Polygon)feature.GetShape();
                                double perim = polygon.Length;

                                LIST_PUID.Add(puid);

                                if (puid > 0)
                                {
                                    DICT_PUID_and_perimeter.Add(puid, Math.Round(perim, 2, MidpointRounding.AwayFromZero));
                                }
                            }
                        }
                    }
                });

                LIST_PUID.Sort();   // sort the list (not required I don't think...)

                // Now, for each PUID, sum the boundary lengths from the temp bounds table
                // this gives me the total SHARED perimeter for each planning unit
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Summing the shared perimeters per planning unit..."), true, ++val);
                Dictionary<int, double> DICT_PUID_and_shared_perim = new Dictionary<int, double>();                         // ALL PLANNING UNIT IDS AND THEIR SHARED PERIMETERS
                foreach (int puid in LIST_PUID)
                {
                    QueryFilter QF = new QueryFilter
                    {
                        WhereClause = "src_id = " + puid.ToString() + " Or nbr_id = " + puid.ToString()         // BOUNDTEMP ID COLUMN NAMES
                    };

                    double lengthsum = 0;

                    await QueuedTask.Run(async () =>
                    {
                        using (Geodatabase geodatabase = await PRZH.GetProjectGDB())
                        using (Table table = await PRZH.GetTable(geodatabase, boundtemp))
                        using (RowCursor rowCursor = table.Search(QF))  // recycling cursor!
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    lengthsum += Convert.ToDouble(row["LENGTH"]);   // BOUNDTEMP LENGTH COLUMN NAME
                                }
                            }
                        }
                    });

                    DICT_PUID_and_shared_perim.Add(puid, Math.Round(lengthsum, 2, MidpointRounding.AwayFromZero));
                }

                // Populate a Dictionary of puids and self-intersecting perimeter lengths
                Dictionary<int, double> DICT_PUID_and_selfint_perim = new Dictionary<int, double>();    // will have an entry for EVERY PUID
                foreach(int puid in LIST_PUID)
                {
                    double total = DICT_PUID_and_perimeter[puid];       // total edge length (rounded to 2 decimal places)
                    double shared = DICT_PUID_and_shared_perim[puid];    // shared edge length (rounded to 2 decimal places)

                    //double total_rounded = Math.Round(total, 2, MidpointRounding.AwayFromZero);         // make these floating point numbers a bit easier to equate
                    //double shared_rounded = Math.Round(shared, 2, MidpointRounding.AwayFromZero);       // make these floating point numbers a bit easier to equate

                    // selfintperim will be zero if entire perim is shared
                    double selfintperim = total - shared;// total_rounded - shared_rounded;

                    DICT_PUID_and_selfint_perim.Add(puid, selfintperim);
                }

                // Now create the new table
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating Bounary Length Table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_TABLE_PUBOUNDARY, "", "", "Boundary Lengths");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CreateTable_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error creating the Boundary Lengths table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Created the Boundary Lengths Table..."), true, ++val);
                }

                // Add fields to the table
                string fldPUID1 = PRZC.c_FLD_TAB_BOUND_ID1 + " LONG 'Planning Unit ID 1' # # #;";
                string fldPUID2 = PRZC.c_FLD_TAB_BOUND_ID2 + " LONG 'Planning Unit ID 2' # # #;";
                string fldBoundary = PRZC.c_FLD_TAB_BOUND_BOUNDARY + " DOUBLE 'Boundary Length' # # #;";

                string flds = fldPUID1 + fldPUID2 + fldBoundary;

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Adding fields to Boundary Lengths table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(boundpath, flds);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, null, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding fields to Boundary Lengths table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully added fields."), true, ++val);
                }

                // Populate Boundary Table from temp bounds table
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Writing records to final Boundary Lengths table.."), true, ++val);
                await QueuedTask.Run(async () =>
                {
                    using (Table table = await PRZH.GetTable_Boundary())
                    using (InsertCursor insertCursor = table.CreateInsertCursor())
                    using (RowBuffer rowBuffer = table.CreateRowBuffer())
                    {
                        // Iterate through each PUID
                        foreach (int puid in LIST_PUID)
                        {
                            QueryFilter QF = new QueryFilter
                            {
                                WhereClause = "src_id = " + puid.ToString()
                            };

                            int reccount = 0;   // Track how many rows I write for current puid (do I need this?)

                            // get the rows for this puid from the temp table
                            using (Geodatabase geodatabase = await PRZH.GetProjectGDB())
                            using (Table temptab = await PRZH.GetTable(geodatabase, boundtemp))
                            using (RowCursor searchCursor = temptab.Search(QF))  // recycling cursor!
                            {
                                while (searchCursor.MoveNext())
                                {
                                    using (Row row = searchCursor.Current)
                                    {
                                        int srcid1 = Convert.ToInt32(row["src_id"]);
                                        int srcid2 = Convert.ToInt32(row["nbr_id"]);
                                        double perim = Convert.ToDouble(row["LENGTH"]);

                                        // Create the new record
                                        rowBuffer[PRZC.c_FLD_TAB_BOUND_ID1] = srcid1;
                                        rowBuffer[PRZC.c_FLD_TAB_BOUND_ID2] = srcid2;
                                        rowBuffer[PRZC.c_FLD_TAB_BOUND_BOUNDARY] = perim;

                                        // Insert it
                                        insertCursor.Insert(rowBuffer);
                                        insertCursor.Flush();

                                        reccount++;
                                    }
                                }
                            }

                            // Add an extra row for this puid, IF that puid has unshared perim
                            double selfperim = DICT_PUID_and_selfint_perim[puid];

                            if (selfperim > 0)
                            {
                                rowBuffer[PRZC.c_FLD_TAB_BOUND_ID1] = puid;
                                rowBuffer[PRZC.c_FLD_TAB_BOUND_ID2] = puid;
                                rowBuffer[PRZC.c_FLD_TAB_BOUND_BOUNDARY] = selfperim;
                                insertCursor.Insert(rowBuffer);
                                insertCursor.Flush();
                            }
                        }
                    }
                });

                // Index both id fields
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Indexing both id fields..."), true, ++val);
                List<string> LIST_ix = new List<string>() { PRZC.c_FLD_TAB_BOUND_ID1, PRZC.c_FLD_TAB_BOUND_ID2 };
                toolParams = Geoprocessing.MakeValueArray(boundpath, LIST_ix, "ix" + PRZC.c_FLD_FC_PU_ID, "", "");
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

                // Delete temp table
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting temp table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(boundtemp);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting the temp table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully deleted the temp table."), true, ++val);
                }

                // Update the boundary columns in PUFC
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Updating boundary columns in PU FC..."), true, ++val);
                await QueuedTask.Run(async () =>
                {
                    using (Table table = await PRZH.GetFC_PU())
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                int puid = Convert.ToInt32(row[PRZC.c_FLD_FC_PU_ID]);

                                double sharedperim = DICT_PUID_and_shared_perim[puid];
                                double selfintperim = DICT_PUID_and_selfint_perim[puid];

                                // set the indicator field
                                row[PRZC.c_FLD_FC_PU_HAS_UNSHARED_PERIM] = (selfintperim > 0) ? 1 : 0;

                                // set the two perim fields
                                row[PRZC.c_FLD_FC_PU_SHARED_PERIM] = sharedperim;
                                row[PRZC.c_FLD_FC_PU_UNSHARED_PERIM] = selfintperim;

                                row.Store();
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

                BoundaryTableExists = await PRZH.TableExists_Boundary();
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