using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
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

namespace NCC.PRZTools
{
    public class BoundaryLengthsVM : PropertyChangedBase
    {
        public BoundaryLengthsVM()
        {
        }

        #region FIELDS

        private bool _boundaryTableExists = false;
        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""

        private ICommand _cmdClearLog;
        private ICommand _cmdBuildBoundaryTable;
        private ICommand _cmdTest;


        #endregion

        #region PROPERTIES

        public bool BoundaryTableExists
        {
            get => _boundaryTableExists;
            set => SetProperty(ref _boundaryTableExists, value, () => BoundaryTableExists);
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

        public ICommand CmdBuildBoundaryTable => _cmdBuildBoundaryTable ?? (_cmdBuildBoundaryTable = new RelayCommand(async () => await BuildBoundaryTable(), () => true));

        public ICommand CmdTest => _cmdTest ?? (_cmdTest = new RelayCommand(async () => ProMsgBox.Show($"{await Test()}"), () => true));

        #endregion

        #region METHODS

        public async Task OnProWinLoaded()
        {
            try
            {
                // Initialize the Progress Bar & Log
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                // Table presence
                BoundaryTableExists = await PRZH.TableExists_Boundary();

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        public async Task<bool> BuildBoundaryTable()
        {
            bool edits_are_disabled = !Project.Current.IsEditingEnabled;
            int val = 0;
            int max = 50;

            try
            {
                #region INITIALIZATION AND VALIDATION

                // Check for currently unsaved edits in the project
                if (Project.Current.HasEdits)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro Project has unsaved edits.  Please save all edits before proceeding.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("This ArcGIS Pro Project has some unsaved edits.  Please save all edits before proceeding.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro Project has no unsaved edits.  Proceeding..."), true, ++val);
                }

                // If editing is disabled, enable it temporarily (and disable again in the finally block)
                if (edits_are_disabled)
                {
                    if (!await Project.Current.SetIsEditingEnabledAsync(true))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to enabled editing for this ArcGIS Pro Project.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Unable to enabled editing for this ArcGIS Pro Project.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing enabled."), true, ++val);
                    }
                }

                // Initialize a few thingies
                Map map = MapView.Active.Map;

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                // Initialize ProgressBar and Progress Log
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Boundary Table generator..."), false, max, ++val);

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Validation: Ensure the Project Geodatabase Exists
                string gdbpath = PRZH.GetPath_ProjectGDB();
                if (!await PRZH.GDBExists_Project())
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Project Geodatabase is OK: " + gdbpath), true, ++val);
                }

                // Validation: Ensure that the Planning Unit data exists
                var puresult = await PRZH.PUExists();
                if (!puresult.exists)
                {
                    ProMsgBox.Show("Planning Unit Feature Class or Raster Dataset not found in the project geodatabase.");
                    return false;
                }

                #endregion

                #region CONSTRUCT TABLE

                if (puresult.puLayerType == PlanningUnitLayerType.FEATURE)
                {
                    // Make sure FC is there
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

                    // Ensure the Planning Unit Layer is present in the map
                    if (!PRZH.PRZLayerExists(map, PRZLayerNames.PU))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Layer not found in the map.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Planning Units feature layer not found in the map.  Please reload the PRZ Layers and try again.", "Validation");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Layer found."), true, ++val);
                    }

                    // Get the PU Feature Layer and its Spatial Reference
                    FeatureLayer PUFL = null;
                    SpatialReference PUFL_SR = null;
                    await QueuedTask.Run(() =>
                    {
                        PUFL = (FeatureLayer)PRZH.GetPRZLayer(map, PRZLayerNames.PU);
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
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_PUBOUNDARY} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_PUBOUNDARY} table.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_PUBOUNDARY} table deleted successfully."), true, ++val);
                        }
                    }

                    // Construct Temp Table from Polygon Neighbors tool
                    string boundtemp = "boundtemp";

                    // Polygon Neighbors tool
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Executing Polygon Neighbors geoprocessing tool..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PUFL, boundtemp, PRZC.c_FLD_FC_PU_ID, "NO_AREA_OVERLAP", "NO_BOTH_SIDES", "", "", "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("PolygonNeighbors_analysis", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error executing the Polygon Neighbors geoprocessing tool.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Error executing the Polygon Neighbors tool.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully executed the Polygon Neighbors tool."), true, ++val);
                    }

                    // Delete all zero length records from temp table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting node connections..."), true, ++val);
                    if (!await QueuedTask.Run(async () =>
                    {
                        bool success = false;

                        try
                        {
                            var deletor = new EditOperation();
                            deletor.Name = "Boundary Records Deletion";
                            deletor.ShowProgressor = false;
                            deletor.ShowModalMessageAfterFailure = false;
                            deletor.SelectNewFeatures = false;
                            deletor.SelectModifiedFeatures = false;

                            using (Table tab = await PRZH.GetTable(boundtemp))
                            {
                                deletor.Callback(async (context) =>
                                {
                                    QueryFilter queryFilter = new QueryFilter
                                    {
                                        WhereClause = "LENGTH = 0"
                                    };

                                    using (Table table = await PRZH.GetTable(boundtemp))
                                    {
                                        context.Invalidate(table);
                                        table.DeleteRows(queryFilter);
                                    }
                                }, tab);
                            }

                            // Execute all the queued "deletions"
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Executing record deletions!  This one might take a while..."), true, max, ++val);
                            success = deletor.Execute();

                            if (success)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Saving record deletions..."), true, max, ++val);
                                if (!await Project.Current.SaveEditsAsync())
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error saving record deletions.", LogMessageType.ERROR), true, max, ++val);
                                    ProMsgBox.Show($"Error saving record deletions.");
                                    return false;
                                }
                                else
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Record deletions saved."), true, max, ++val);
                                }
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Edit Operation error: unable to delete records: {deletor.ErrorMessage}", LogMessageType.ERROR), true, max, ++val);
                                ProMsgBox.Show($"Edit Operation error: unable to delete records: {deletor.ErrorMessage}");
                            }

                            return success;
                        }
                        catch (Exception ex)
                        {
                            ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                            return false;
                        }
                    }))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to delete node connection records from {boundtemp} table.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Unable to delete node connection records from {boundtemp} table.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Node connection records deleted."), true, ++val);
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
                    string fldSource = "src_" + PRZC.c_FLD_FC_PU_ID;
                    string fldNeighbour = "nbr_" + PRZC.c_FLD_FC_PU_ID;

                    await QueuedTask.Run(async () =>
                    {
                        using (Table table = await PRZH.GetTable(boundtemp))
                        {
                            foreach (int puid in LIST_PUID)
                            {
                                double lengthsum = 0;

                                QueryFilter QF = new QueryFilter
                                {
                                    WhereClause = fldSource + " = " + puid.ToString() + " Or " + fldNeighbour + " = " + puid.ToString()         // BOUNDTEMP ID COLUMN NAMES
                                };

                                using (RowCursor rowCursor = table.Search(QF, false))
                                {
                                    while (rowCursor.MoveNext())
                                    {
                                        using (Row row = rowCursor.Current)
                                        {
                                            lengthsum += Convert.ToDouble(row["LENGTH"]);
                                        }
                                    }
                                }

                                DICT_PUID_and_shared_perim.Add(puid, Math.Round(lengthsum, 2, MidpointRounding.AwayFromZero));
                            }
                        }
                    });

                    // Populate a Dictionary of puids and self-intersecting perimeter lengths
                    Dictionary<int, double> DICT_PUID_and_selfint_perim = new Dictionary<int, double>();    // will have an entry for EVERY PUID
                    foreach (int puid in LIST_PUID)
                    {
                        double total = DICT_PUID_and_perimeter[puid];       // total edge length (rounded to 2 decimal places)
                        double shared = DICT_PUID_and_shared_perim[puid];    // shared edge length (rounded to 2 decimal places)

                        // selfintperim will be zero if entire perim is shared
                        double selfintperim = total - shared;// total_rounded - shared_rounded;

                        DICT_PUID_and_selfint_perim.Add(puid, selfintperim);
                    }

                    // Now create the new table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating Boundary Length Table..."), true, ++val);
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
                    if (!await QueuedTask.Run(async () =>
                    {
                        bool success = false;

                        try
                        {
                            var loader = new EditOperation();
                            loader.Name = "Boundary Length record loader";
                            loader.ShowProgressor = false;
                            loader.ShowModalMessageAfterFailure = false;
                            loader.SelectNewFeatures = false;
                            loader.SelectModifiedFeatures = false;

                            int flusher = 0;

                            using (Table tab = await PRZH.GetTable_Boundary())
                            {
                                loader.Callback(async (context) =>
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
                                                WhereClause = fldSource + " = " + puid.ToString()
                                            };

                                            // get the rows for this puid from the temp table
                                            using (Table temptab = await PRZH.GetTable(boundtemp))
                                            using (RowCursor searchCursor = temptab.Search(QF))  // recycling cursor!
                                            {
                                                while (searchCursor.MoveNext())
                                                {
                                                    using (Row row = searchCursor.Current)
                                                    {
                                                        int srcid1 = Convert.ToInt32(row[fldSource]);
                                                        int srcid2 = Convert.ToInt32(row[fldNeighbour]);
                                                        double perim = Convert.ToDouble(row["LENGTH"]);

                                                        // Fill the row buffer
                                                        rowBuffer[PRZC.c_FLD_TAB_BOUND_ID1] = srcid1;
                                                        rowBuffer[PRZC.c_FLD_TAB_BOUND_ID2] = srcid2;
                                                        rowBuffer[PRZC.c_FLD_TAB_BOUND_BOUNDARY] = perim;

                                                        // Insert new record
                                                        insertCursor.Insert(rowBuffer);
                                                        flusher++;
                                                        if (flusher == 1000)
                                                        {
                                                            insertCursor.Flush();
                                                            flusher = 0;
                                                        }
                                                    }
                                                }
                                            }

                                            // Add one extra row for this puid, IF that puid has unshared perim
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

                                        insertCursor.Flush();
                                    }
                                }, tab);
                            }

                            // Execute all the queued "creates"
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Executing Edit Operation!  This one might take a while..."), true, max, ++val);
                            success = loader.Execute();

                            if (success)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Saving new records..."), true, max, ++val);
                                if (!await Project.Current.SaveEditsAsync())
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error saving records.", LogMessageType.ERROR), true, max, ++val);
                                    ProMsgBox.Show($"Error saving records.");
                                    return false;
                                }
                                else
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Records saved."), true, max, ++val);
                                }
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Edit Operation error: unable to create records: {loader.ErrorMessage}", LogMessageType.ERROR), true, max, ++val);
                                ProMsgBox.Show($"Edit Operation error: unable to create records: {loader.ErrorMessage}");
                            }

                            return success;
                        }
                        catch (Exception ex)
                        {
                            ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                            return false;
                        }
                    }))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating boundary records.", LogMessageType.ERROR), true, max, val++);
                        ProMsgBox.Show($"Error creating boundary records.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Boundary records created."), true, max, val++);
                    }

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
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting {boundtemp} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting {boundtemp} table");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully deleted the temp table."), true, ++val);
                    }

                    //// Update the boundary columns in PUFC
                    ///// TODO: Do I even need to do this?
                    //PRZH.UpdateProgress(PM, PRZH.WriteLog("Updating boundary columns in PU FC..."), true, ++val);

                    //await QueuedTask.Run(async () =>
                    //{
                    //    using (Table table = await PRZH.GetFC_PU())
                    //    using (RowCursor rowCursor = table.Search(null, false))
                    //    {
                    //        while (rowCursor.MoveNext())
                    //        {
                    //            using (Row row = rowCursor.Current)
                    //            {
                    //                int puid = Convert.ToInt32(row[PRZC.c_FLD_FC_PU_ID]);

                    //                double sharedperim = DICT_PUID_and_shared_perim[puid];
                    //                double selfintperim = DICT_PUID_and_selfint_perim[puid];

                    //                // set the indicator field
                    //                row[PRZC.c_FLD_FC_PU_HAS_UNSHARED_PERIM] = (selfintperim > 0) ? 1 : 0;

                    //                // set the two perim fields
                    //                row[PRZC.c_FLD_FC_PU_SHARED_PERIM] = sharedperim;
                    //                row[PRZC.c_FLD_FC_PU_UNSHARED_PERIM] = selfintperim;

                    //                row.Store();
                    //            }
                    //        }
                    //    }
                    //});

                    #endregion
                }
                else if (puresult.puLayerType == PlanningUnitLayerType.RASTER)
                {
                    // Make sure Raster is there
                    string puraspath = PRZH.GetPath_Raster_PU();
                    if (!await PRZH.RasterExists_PU())
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Raster Dataset not found in the Project Geodatabase.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Planning Unit Raster Dataset not present in the project geodatabase.  Have you built it yet?");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Planning Unit Raster Dataset is OK: {puraspath}"), true, ++val);
                    }

                    // Ensure the Planning Unit Layer is present in the map
                    if (!PRZH.RasterLayerExists_PU(map))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Raster Layer not found in the map.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Planning Unit raster layer not found in the map.  Please reload the PRZ Layers and try again.", "Validation");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Raster Layer found."), true, ++val);
                    }

                    // Get the PU Raster Layer and some associated info
                    RasterLayer PURL = null;
                    SpatialReference PURL_SR = null;
                    double side_length = 0;
                    await QueuedTask.Run(() =>
                    {
                        PURL = PRZH.GetRasterLayer_PU(map);
                        PURL_SR = PURL.GetSpatialReference();
                        var cell_size = PURL.GetRaster().GetMeanCellSize();
                        side_length = Math.Round(cell_size.Item1, 2, MidpointRounding.AwayFromZero);
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
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_PUBOUNDARY} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_PUBOUNDARY} table.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_PUBOUNDARY} table deleted successfully."), true, ++val);
                        }
                    }

                    // Delete the temp objects if present
                    string polytemp = "polytemp";
                    string boundtemp = "boundtemp";

                    if (await PRZH.FCExists(polytemp))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {polytemp} feature class..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(polytemp);
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                        toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {polytemp} feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error deleting the {polytemp} feature class.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"{polytemp} feature class deleted successfully."), true, ++val);
                        }
                    }
                    if (await PRZH.TableExists(boundtemp))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {boundtemp} table..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(boundtemp);
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                        toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {boundtemp} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error deleting the {boundtemp} table.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"{boundtemp} table deleted successfully."), true, ++val);
                        }
                    }

                    // Convert Raster to Polygon FC so I can use the Neighbours tool

                    // Raster to Poly tool
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Converting Raster to Polygon..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PURL, polytemp, "NO_SIMPLIFY", "VALUE", "SINGLE_OUTER_PART", "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true, outputCoordinateSystem: PURL_SR);
                    toolOutput = await PRZH.RunGPTool("RasterToPolygon_conversion", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error converting Raster to Polygon.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Error converting Raster to Polygon.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully converted Raster to Polygon."), true, ++val);
                    }
                    // At this point I have polygon with a "GridCode" column containing PU ID values from the raster

                    // Construct Temp Table from Polygon Neighbors tool
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Executing Polygon Neighbors geoprocessing tool..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(polytemp, boundtemp, "gridcode", "NO_AREA_OVERLAP", "NO_BOTH_SIDES", "", "", "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("PolygonNeighbors_analysis", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error executing the Polygon Neighbors geoprocessing tool.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Error executing the Polygon Neighbors tool.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully executed the Polygon Neighbors tool."), true, ++val);
                    }

                    // Delete all zero length records from temp table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting node connections..."), true, ++val);
                    if (!await QueuedTask.Run(async () =>
                    {
                        bool success = false;

                        try
                        {
                            var deletor = new EditOperation();
                            deletor.Name = "Boundary Records Deletion";
                            deletor.ShowProgressor = false;
                            deletor.ShowModalMessageAfterFailure = false;
                            deletor.SelectNewFeatures = false;
                            deletor.SelectModifiedFeatures = false;

                            using (Table tab = await PRZH.GetTable(boundtemp))
                            {
                                deletor.Callback(async (context) =>
                                {
                                    QueryFilter queryFilter = new QueryFilter
                                    {
                                        WhereClause = "LENGTH = 0"
                                    };

                                    using (Table table = await PRZH.GetTable(boundtemp))
                                    {
                                        context.Invalidate(table);
                                        table.DeleteRows(queryFilter);
                                    }
                                }, tab);
                            }

                            // Execute all the queued "deletions"
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Executing record deletions!  This one might take a while..."), true, max, ++val);
                            success = deletor.Execute();

                            if (success)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Saving record deletions..."), true, max, ++val);
                                if (!await Project.Current.SaveEditsAsync())
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error saving record deletions.", LogMessageType.ERROR), true, max, ++val);
                                    ProMsgBox.Show($"Error saving record deletions.");
                                    return false;
                                }
                                else
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Record deletions saved."), true, max, ++val);
                                }
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Edit Operation error: unable to delete records: {deletor.ErrorMessage}", LogMessageType.ERROR), true, max, ++val);
                                ProMsgBox.Show($"Edit Operation error: unable to delete records: {deletor.ErrorMessage}");
                            }

                            return success;
                        }
                        catch (Exception ex)
                        {
                            ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                            return false;
                        }
                    }))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to delete node connection records from {boundtemp} table.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Unable to delete node connection records from {boundtemp} table.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Node connection records deleted."), true, ++val);
                    }

                    // Index both id fields
                    string fldSource = "src_gridcode";
                    string fldNeighbour = "nbr_gridcode";

                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Indexing both id fields..."), true, ++val);
                    List<string> index_fields = new List<string>() { fldSource, fldNeighbour };
                    toolParams = Geoprocessing.MakeValueArray(boundtemp, index_fields, "ixTemp", "", "");
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

                    #endregion

                    #region CALCULATE EXTERIOR PLANNING UNITS AND CREATE FINAL TABLE

                    // First, get the perimeter of each planning unit
                    List<int> LIST_PUID = new List<int>();                                                  // ALL PLANNING UNIT IDS
                    Dictionary<int, double> DICT_PUID_and_perimeter = new Dictionary<int, double>();        // ALL PLANNING UNIT IDS AND THEIR PERIMETERS

                    await QueuedTask.Run(async () =>
                    {
                        using (RasterDataset rasterDataset = await PRZH.GetRaster_PU())
                        using (Raster raster = rasterDataset.CreateFullRaster())
                        using (Table table = raster.GetAttributeTable())
                        using (RowCursor rowCursor = table.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int puid = Convert.ToInt32(row[PRZC.c_FLD_RAS_PU_ID]);
                                    LIST_PUID.Add(puid);

                                    if (puid > 0)
                                    {
                                        DICT_PUID_and_perimeter.Add(puid, side_length * 4.0);
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

                    await QueuedTask.Run(async () =>
                    {
                        using (Table table = await PRZH.GetTable(boundtemp))
                        {
                            foreach (int puid in LIST_PUID)
                            {
                                double lengthsum = 0;

                                QueryFilter QF = new QueryFilter
                                {
                                    WhereClause = fldSource + " = " + puid.ToString() + " Or " + fldNeighbour + " = " + puid.ToString()         // BOUNDTEMP ID COLUMN NAMES
                                };

                                using (RowCursor rowCursor = table.Search(QF, false))
                                {
                                    while (rowCursor.MoveNext())
                                    {
                                        using (Row row = rowCursor.Current)
                                        {
                                            lengthsum += Convert.ToDouble(row["LENGTH"]);
                                        }
                                    }
                                }

                                DICT_PUID_and_shared_perim.Add(puid, Math.Round(lengthsum, 2, MidpointRounding.AwayFromZero));
                            }
                        }
                    });

                    // Populate a Dictionary of puids and self-intersecting perimeter lengths
                    Dictionary<int, double> DICT_PUID_and_selfint_perim = new Dictionary<int, double>();    // will have an entry for EVERY PUID
                    foreach (int puid in LIST_PUID)
                    {
                        double total = DICT_PUID_and_perimeter[puid];       // total edge length (rounded to 2 decimal places)
                        double shared = DICT_PUID_and_shared_perim[puid];    // shared edge length (rounded to 2 decimal places)

                        // selfintperim will be zero if entire perim is shared
                        double selfintperim = total - shared;// total_rounded - shared_rounded;

                        DICT_PUID_and_selfint_perim.Add(puid, selfintperim);
                    }

                    // Now create the new table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating {PRZC.c_TABLE_PUBOUNDARY} Table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_TABLE_PUBOUNDARY, "", "", "Boundary Lengths");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("CreateTable_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {PRZC.c_TABLE_PUBOUNDARY} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Created the {PRZC.c_TABLE_PUBOUNDARY} table."), true, ++val);
                    }

                    // Add fields to the table
                    string fldPUID1 = PRZC.c_FLD_TAB_BOUND_ID1 + " LONG 'Planning Unit ID 1' # # #;";
                    string fldPUID2 = PRZC.c_FLD_TAB_BOUND_ID2 + " LONG 'Planning Unit ID 2' # # #;";
                    string fldBoundary = PRZC.c_FLD_TAB_BOUND_BOUNDARY + " DOUBLE 'Boundary Length' # # #;";

                    string flds = fldPUID1 + fldPUID2 + fldBoundary;

                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to {PRZC.c_TABLE_PUBOUNDARY} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(boundpath, flds);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to {PRZC.c_TABLE_PUBOUNDARY} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Added fields to {PRZC.c_TABLE_PUBOUNDARY} table."), true, ++val);
                    }

                    // Populate Boundary Table from temp bounds table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Writing records to final Boundary Lengths table.."), true, ++val);
                    if (!await QueuedTask.Run(async () =>
                    {
                        bool success = false;

                        try
                        {
                            var loader = new EditOperation();
                            loader.Name = "Boundary Length record loader";
                            loader.ShowProgressor = false;
                            loader.ShowModalMessageAfterFailure = false;
                            loader.SelectNewFeatures = false;
                            loader.SelectModifiedFeatures = false;

                            int flusher = 0;

                            using (Table tab = await PRZH.GetTable_Boundary())
                            {
                                loader.Callback(async (context) =>
                                {
                                    using (Table table = await PRZH.GetTable_Boundary())
                                    using (InsertCursor insertCursor = table.CreateInsertCursor())
                                    using (RowBuffer rowBuffer = table.CreateRowBuffer())
                                    {
                                        // Go through the temp table
                                        using (Table tempTable = await PRZH.GetTable(boundtemp))
                                        {
                                            foreach (int puid in LIST_PUID)
                                            {
                                                QueryFilter QF = new QueryFilter
                                                {
                                                    WhereClause = fldSource + " = " + puid.ToString()
                                                };

                                                using (RowCursor searchCursor = tempTable.Search(QF, false))
                                                {
                                                    while (searchCursor.MoveNext())
                                                    {
                                                        using (Row row = searchCursor.Current)
                                                        {
                                                            int srcid1 = Convert.ToInt32(row[fldSource]);
                                                            int srcid2 = Convert.ToInt32(row[fldNeighbour]);
                                                            double perim = Convert.ToDouble(row["LENGTH"]);

                                                            // Fill the row buffer
                                                            rowBuffer[PRZC.c_FLD_TAB_BOUND_ID1] = srcid1;
                                                            rowBuffer[PRZC.c_FLD_TAB_BOUND_ID2] = srcid2;
                                                            rowBuffer[PRZC.c_FLD_TAB_BOUND_BOUNDARY] = perim;

                                                            // Insert new record
                                                            insertCursor.Insert(rowBuffer);
                                                            flusher++;
                                                            if (flusher == 1000)
                                                            {
                                                                insertCursor.Flush();
                                                                flusher = 0;
                                                            }
                                                        }
                                                    }
                                                }

                                                // Add one extra row for this puid, IF that puid has unshared perim
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

                                        insertCursor.Flush();
                                    }
                                }, tab);
                            }

                            // Execute all the queued "creates"
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Executing Edit Operation!  Adding records..."), true, max, ++val);
                            success = loader.Execute();

                            if (success)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Saving new records..."), true, max, ++val);
                                if (!await Project.Current.SaveEditsAsync())
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error saving records.", LogMessageType.ERROR), true, max, ++val);
                                    ProMsgBox.Show($"Error saving records.");
                                    return false;
                                }
                                else
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Records saved."), true, max, ++val);
                                }
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Edit Operation error: unable to create records: {loader.ErrorMessage}", LogMessageType.ERROR), true, max, ++val);
                                ProMsgBox.Show($"Edit Operation error: unable to create records: {loader.ErrorMessage}");
                            }

                            return success;
                        }
                        catch (Exception ex)
                        {
                            ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                            return false;
                        }
                    }))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating boundary records.", LogMessageType.ERROR), true, max, val++);
                        ProMsgBox.Show($"Error creating boundary records.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Boundary records created."), true, max, val++);
                    }

                    // Index both id fields
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Indexing both id fields..."), true, ++val);
                    List<string> LIST_ix = new List<string>() { PRZC.c_FLD_TAB_BOUND_ID1, PRZC.c_FLD_TAB_BOUND_ID2 };
                    toolParams = Geoprocessing.MakeValueArray(boundpath, LIST_ix, "ix" + PRZC.c_FLD_RAS_PU_ID, "", "");
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
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting {boundtemp} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting {boundtemp} table");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully deleted the temp table."), true, ++val);
                    }

                    // Delete temp poly fc
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting temp polygon fc..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(polytemp);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting {polytemp} fc.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting {polytemp} fc");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully deleted the temp fc."), true, ++val);
                    }

                    #endregion
                }
                else
                {
                    // huh? should be unreachable
                    ProMsgBox.Show("Planning Units not found in geodatabase.");
                    return false;
                }

                #endregion

                #region WRAP THINGS UP

                // Compact the Geodatabase
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacting the Geodatabase..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath);
                toolOutput = await PRZH.RunGPTool("Compact_management", toolParams, null, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error compacting the geodatabase. GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error compacting geodatabase");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Geodatabase compacted."), true, ++val);
                }

                // Wrap things up
                stopwatch.Stop();
                string message = PRZH.GetElapsedTimeMessage(stopwatch.Elapsed);
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Construction completed successfully!"), true, 1, 1);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(message), true, 1, 1);

                ProMsgBox.Show("Construction Completed Successfully!" + Environment.NewLine + Environment.NewLine + message);
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
                // reset disabled editing status
                if (edits_are_disabled)
                {
                    await Project.Current.SetIsEditingEnabledAsync(false);
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing disabled."), true, max, ++val);
                }
            }
        }

        public async Task<bool> Test()
        {
            int val = 0;
            bool edits_are_disabled = !Project.Current.IsEditingEnabled;

            try
            {
                // Check for currently unsaved edits in the project
                if (Project.Current.HasEdits)
                {
                    ProMsgBox.Show("This ArcGIS Pro Project has some unsaved edits.  Please save all edits before proceeding");
                    return true;
                }

                // Enable editing temporarily (reset this in 'finally')
                if (edits_are_disabled)
                {
                    await Project.Current.SetIsEditingEnabledAsync(true);
                }

                // Initialize ProgressBar and Progress Log
                int max = 50;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the tester..."), false, max, ++val);

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                int counter = 0;
                int hundreds = 0;

                if (!await QueuedTask.Run(async () =>
                {
                    bool success = false;

                    try
                    {
                        using (Table table = await PRZH.GetTable_PUFeatures())
                        {
                            // EditOperation approach
                            var editOp = new EditOperation();
                            editOp.Name = "Tester I";
                            editOp.ShowProgressor = false;
                            editOp.ShowModalMessageAfterFailure = false;
                            
                            editOp.Callback((context) =>
                            {
                                // callback editing function
                                try
                                {
                                    using (RowCursor rowCursor = table.Search(null, false))
                                    {
                                        while (rowCursor.MoveNext())
                                        {
                                            using (Row row = rowCursor.Current)
                                            {
                                                context.Invalidate(row);
                                                row[PRZC.c_FLD_TAB_PUCF_FEATURECOUNT] = 3;
                                                row.Store();
                                                context.Invalidate(row);
                                            }
                                        }
                                    }

                                    throw new Exception("whaaah happened?");

                                }
                                catch (Exception ex)
                                {
                                    context.Abort("Abortorama: " + ex.Message);
                                }

                            }, table);

                            success = await editOp.ExecuteAsync();

                            if (success)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Saving edits..."), true, max, ++val);
                                await Project.Current.SaveEditsAsync();
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Error... Error...", LogMessageType.ERROR), true, max, ++val);
                                ProMsgBox.Show("Error Message: " + editOp.ErrorMessage);
                            }
                        }

                        return success;
                    }
                    catch (Exception ex)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error happened.", LogMessageType.ERROR), true, max, ++val);
                        ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                        return false;
                    }
                }))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"test error.", LogMessageType.ERROR), true);
                    ProMsgBox.Show($"test error.");
                    return false;
                }

                stopwatch.Stop();

                string message = PRZH.GetElapsedTimeInSeconds(stopwatch.Elapsed);
                ProMsgBox.Show(message);

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message);
                return false;
            }
            finally
            {
                if (edits_are_disabled)
                {
                    await Project.Current.SetIsEditingEnabledAsync(false);
                }
            }
        }

        #endregion

    }
}