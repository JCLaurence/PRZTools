using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

        private CancellationTokenSource _cts = null;
        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""
        private bool _operation_Cmd_IsEnabled;
        private bool _operationIsUnderway = false;
        private Cursor _proWindowCursor;

        private bool _pu_exists = false;
        private bool _blt_exists = false;

        #region COMMANDS

        private ICommand _cmdBuildBoundaryTable;
        private ICommand _cmdCancel;
        private ICommand _cmdClearLog;

        #endregion

        #region COMPONENT STATUS INDICATORS

        // Planning Unit Dataset
        private string _compStat_Img_PlanningUnits_Path;
        private string _compStat_Txt_PlanningUnits_Label;

        // Boundary Lengths Table
        private string _compStat_Img_BoundaryLengths_Path;
        private string _compStat_Txt_BoundaryLengths_Label;

        #endregion

        #region OPERATION STATUS INDICATORS

        private Visibility _opStat_Img_Visibility;
        private string _opStat_Txt_Label;

        #endregion

        #endregion

        #region PROPERTIES

        public ProgressManager PM
        {
            get => _pm; set => SetProperty(ref _pm, value, () => PM);
        }

        public bool Operation_Cmd_IsEnabled
        {
            get => _operation_Cmd_IsEnabled;
            set => SetProperty(ref _operation_Cmd_IsEnabled, value, () => Operation_Cmd_IsEnabled);
        }

        public bool OperationIsUnderway
        {
            get => _operationIsUnderway;
        }

        public Cursor ProWindowCursor
        {
            get => _proWindowCursor;
            set => SetProperty(ref _proWindowCursor, value, () => ProWindowCursor);
        }

        #region COMPONENT STATUS INDICATORS

        // Planning Units Dataset
        public string CompStat_Img_PlanningUnits_Path
        {
            get => _compStat_Img_PlanningUnits_Path;
            set => SetProperty(ref _compStat_Img_PlanningUnits_Path, value, () => CompStat_Img_PlanningUnits_Path);
        }

        public string CompStat_Txt_PlanningUnits_Label
        {
            get => _compStat_Txt_PlanningUnits_Label;
            set => SetProperty(ref _compStat_Txt_PlanningUnits_Label, value, () => CompStat_Txt_PlanningUnits_Label);
        }

        // Boundary Lengths Table
        public string CompStat_Img_BoundaryLengths_Path
        {
            get => _compStat_Img_BoundaryLengths_Path;
            set => SetProperty(ref _compStat_Img_BoundaryLengths_Path, value, () => CompStat_Img_BoundaryLengths_Path);
        }

        public string CompStat_Txt_BoundaryLengths_Label
        {
            get => _compStat_Txt_BoundaryLengths_Label;
            set => SetProperty(ref _compStat_Txt_BoundaryLengths_Label, value, () => CompStat_Txt_BoundaryLengths_Label);
        }

        #endregion

        #region OPERATION STATUS INDICATORS

        public Visibility OpStat_Img_Visibility
        {
            get => _opStat_Img_Visibility;
            set => SetProperty(ref _opStat_Img_Visibility, value, () => OpStat_Img_Visibility);
        }

        public string OpStat_Txt_Label
        {
            get => _opStat_Txt_Label;
            set => SetProperty(ref _opStat_Txt_Label, value, () => OpStat_Txt_Label);
        }

        #endregion

        #endregion

        #region COMMANDS

        public ICommand CmdBuildBoundaryTable => _cmdBuildBoundaryTable ?? (_cmdBuildBoundaryTable = new RelayCommand(async () =>
        {
            // Change UI to Underway
            StartOpUI();

            // Start the operation
            using (_cts = new CancellationTokenSource())
            {
                await BuildBoundaryTable(_cts.Token);
            }

            // Set source to null (it's already disposed)
            _cts = null;

            // Validate controls
            await ValidateControls();

            // Reset UI to Idle
            ResetOpUI();

        }, () => true, true, false));

        public ICommand CmdCancel => _cmdCancel ?? (_cmdCancel = new RelayCommand(() =>
        {
            if (_cts != null)
            {
                // Optionally notify the user or prompt the user here

                // Cancel the operation
                _cts.Cancel();
            }
        }, () => _cts != null, true, false));

        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true, true, false));

        #endregion

        #region METHODS

        public async Task OnProWinLoaded()
        {
            try
            {
                // Initialize the Progress Bar & Log
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                // Configure a few controls
                await ValidateControls();

                // Reset the UI
                ResetOpUI();
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task BuildBoundaryTable(CancellationToken token)
        {
            bool edits_are_disabled = !Project.Current.IsEditingEnabled;
            int val = 0;
            int max = 50;

            try
            {
                #region INITIALIZATION

                #region EDITING CHECK

                // Check for currently unsaved edits in the project
                if (Project.Current.HasEdits)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro Project has unsaved edits.  Please save all edits before proceeding.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("This ArcGIS Pro Project has some unsaved edits.  Please save all edits before proceeding.");
                    return;
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
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to enable editing for this ArcGIS Pro Project.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Unable to enable editing for this ArcGIS Pro Project.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing enabled."), true, ++val);
                    }
                }

                #endregion

                // Initialize a few objects and names
                string temp_table = "boundtemp";
                string temp_fc = "polytemp";

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags_GPRefresh = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread;
                string toolOutput;

                // Initialize ProgressBar and Progress Log
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Boundary Lengths Table Generator..."), false, max, ++val);

                // Ensure the Project Geodatabase Exists
                string gdbpath = PRZH.GetPath_ProjectGDB();
                var try_gdbexists = await PRZH.GDBExists_Project();

                if (!try_gdbexists.exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Project Geodatabase not found: {gdbpath}", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Project Geodatabase not found at {gdbpath}.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Project Geodatabase found at {gdbpath}."), true, ++val);
                }

                // Validate Existence/Type of Planning Unit Spatial Data, capture infos
                var pu_result = await PRZH.PUExists();
                string pu_path = "";            // path to data
                SpatialReference PU_SR = null;  // PU spatial reference
                double full_perim = 0;          // full perimeter of planning unit (for raster pu, this is a constant)

                if (!pu_result.exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit layer not found in project geodatabase.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Planning Unit layer not found in project geodatabase.");
                    return;
                }
                else if (pu_result.puLayerType == PlanningUnitLayerType.UNKNOWN)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit layer format unknown.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Planning Unit layer format unknown.");
                    return;
                }
                else if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                {
                    // Ensure data present
                    if (!(await PRZH.FCExists_Project(PRZC.c_FC_PLANNING_UNITS)).exists)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Planning Unit feature class not found.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Planning Unit feature class not found.  Have you built it yet?");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit feature class found."), true, ++val);
                    }

                    // Get path
                    pu_path = PRZH.GetPath_Project(PRZC.c_FC_PLANNING_UNITS).path;

                    // Get Spatial Reference
                    await QueuedTask.Run(() =>
                    {
                        var tryget = PRZH.GetFC_Project(PRZC.c_FC_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            throw new Exception("Error retrieving feature class");
                        }

                        using (FeatureClass FC = tryget.featureclass)
                        using (FeatureClassDefinition fcDef = FC.GetDefinition())
                        {
                            PU_SR = fcDef.GetSpatialReference();
                        }
                    });
                }
                else if (pu_result.puLayerType == PlanningUnitLayerType.RASTER)
                {
                    // Ensure data present
                    if (!(await PRZH.RasterExists_Project(PRZC.c_RAS_PLANNING_UNITS)).exists)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Planning Unit raster dataset not found.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Planning Unit raster dataset not found.  Have you built it yet?");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit raster dataset found."), true, ++val);
                    }

                    // Get path
                    pu_path = PRZH.GetPath_Project(PRZC.c_RAS_PLANNING_UNITS).path;

                    // Get Spatial Reference & other stuff
                    await QueuedTask.Run(() =>
                    {
                        var tryget = PRZH.GetRaster_Project(PRZC.c_RAS_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            throw new Exception("Unable to retrieve planning unit raster dataset.");
                        }

                        using (RasterDataset RD = tryget.rasterDataset)
                        using (Raster raster = RD.CreateFullRaster())
                        {
                            PU_SR = raster.GetSpatialReference();
                            var cell_size = raster.GetMeanCellSize();
                            double side_length = Math.Round(cell_size.Item1, 2, MidpointRounding.AwayFromZero);
                            full_perim = side_length * 4.0;
                        }
                    });
                }

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
                    return;
                }

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                #endregion

                PRZH.CheckForCancellation(token);

                #region DELETE OBJECTS

                // Delete the Boundary table if present
                if ((await PRZH.TableExists_Project(PRZC.c_TABLE_PUBOUNDARY)).exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting the {PRZC.c_TABLE_PUBOUNDARY} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_TABLE_PUBOUNDARY);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {PRZC.c_TABLE_PUBOUNDARY} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the {PRZC.c_TABLE_PUBOUNDARY} table.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Table deleted successfully."), true, ++val);
                    }
                }

                PRZH.CheckForCancellation(token);

                // Delete the temp table if present
                if ((await PRZH.TableExists_Project(temp_table)).exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting the {temp_table} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(temp_table);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {temp_table} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the {temp_table} table.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Table deleted successfully."), true, ++val);
                    }
                }

                PRZH.CheckForCancellation(token);

                // Delete the temp fc if present
                if ((await PRZH.FCExists_Project(temp_fc)).exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {temp_fc} feature class..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(temp_fc);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting the {temp_fc} feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting the {temp_fc} feature class.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Feature class deleted successfully."), true, ++val);
                    }
                }

                #endregion

                PRZH.CheckForCancellation(token);

                #region GENERATE THE POLYGON NEIGHBOURS DATA

                // For Raster PU, first convert Raster to Poly
                if (pu_result.puLayerType == PlanningUnitLayerType.RASTER)
                {
                    // Raster to Poly tool
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Converting planning unit raster dataset to polygon feature class..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(pu_path, temp_fc, "NO_SIMPLIFY", "VALUE", "SINGLE_OUTER_PART", "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true, outputCoordinateSystem: PU_SR);
                    toolOutput = await PRZH.RunGPTool("RasterToPolygon_conversion", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error executing Raster To Polygon tool.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Error executing Raster To Polygon tool.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Conversion successful."), true, ++val);
                    }
                }

                PRZH.CheckForCancellation(token);

                // Common values
                string source_fc = "";
                string source_field = "";
                if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                {
                    source_fc = PRZC.c_FC_PLANNING_UNITS;   // the planning unit fc
                    source_field = PRZC.c_FLD_FC_PU_ID;
                }
                else
                {
                    source_fc = temp_fc;                    // we just generated this from Raster to Polygon
                    source_field = "gridcode";
                }

                // Generate boundary length data using the Polygon Neighbours tool
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Executing Polygon Neighbors geoprocessing tool..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(source_fc, temp_table, source_field, "NO_AREA_OVERLAP", "NO_BOTH_SIDES", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("PolygonNeighbors_analysis", toolParams, toolEnvs, toolFlags_GPRefresh);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error executing the Polygon Neighbors geoprocessing tool.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error executing the Polygon Neighbors tool.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully executed the Polygon Neighbors tool."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Delete point records from temp table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting point records from {temp_table} table..."), true, ++val);

                (bool success, string message) try_edit = await QueuedTask.Run(async () =>
                {
                    try
                    {
                        // Get the operation object
                        EditOperation editOp = PRZH.GetEditOperation("Point Record Deletion");

                        // set up the callback
                        var tryget = PRZH.GetTable_Project(temp_table);
                        if (!tryget.success)
                        {
                            throw new Exception("Error retrieving table.");
                        }

                        using (Table tab = tryget.table)
                        {
                            editOp.Callback((context) =>
                            {
                                using (Table table = PRZH.GetTable_Project(temp_table).table)
                                {
                                    context.Invalidate(table);
                                    table.DeleteRows(new QueryFilter { WhereClause = "LENGTH = 0" });
                                }
                            }, tab);
                        }

                        // execute the callback
                        bool worked = editOp.Execute();

                        if (worked)
                        {
                            // save edits if callback execution successful
                            bool saved = await Project.Current.SaveEditsAsync();
                            return (saved, saved ? "Operation succeeded, changes saved." : "Error saving changes.");
                        }
                        else
                            return (false, $"Error executing operation: {editOp.ErrorMessage}");
                    }
                    catch (Exception ex)
                    {
                        return (false, ex.Message);
                    }
                });

                if (!try_edit.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting point records: {try_edit.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error deleting point records: {try_edit.message}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Point records deleted."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Index both id fields
                string fldSource = "";
                string fldNeighbour = "";

                if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                {
                    fldSource = "src_" + PRZC.c_FLD_FC_PU_ID;
                    fldNeighbour = "nbr_" + PRZC.c_FLD_FC_PU_ID;
                }
                else
                {
                    fldSource = "src_gridcode";
                    fldNeighbour = "nbr_gridcode";
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Indexing both id fields..."), true, ++val);
                List<string> index_fields = new List<string>() { fldSource, fldNeighbour };
                toolParams = Geoprocessing.MakeValueArray(temp_table, index_fields, "ixTemp", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error indexing fields.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields indexed successfully."), true, ++val);
                }

                #endregion

                PRZH.CheckForCancellation(token);

                #region TABULATE PUIDS, PERIMETERS, AND SHARED EDGES

                // Get Planning Unit IDs
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Getting Planning Unit IDs..."), true, ++val);
                var outcome = await PRZH.GetPUIDHashset();
                if (!outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving Planning Unit IDs\n{outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving Planning Unit IDs\n{outcome.message}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved {outcome.puids.Count} Planning Unit IDs"), true, ++val);
                }

                HashSet<int> PUIDs = outcome.puids;

                PRZH.CheckForCancellation(token);

                // Get full perimeters of each planning unit
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Getting planning unit perimeters..."), true, ++val);
                Dictionary<int, double> PUIDs_and_perimeters = new Dictionary<int, double>();        // Key = PUID     Value = perimeter (rounded to 2 decimal places)

                if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                {
                    // get perimeter of each feature
                    if (!await QueuedTask.Run(() =>
                    {
                        try
                        {
                            var tryget = PRZH.GetFC_Project(PRZC.c_FC_PLANNING_UNITS);
                            if (!tryget.success)
                            {
                                throw new Exception("Error retrieving feature class.");
                            }

                            using (FeatureClass featureClass = tryget.featureclass)
                            using (FeatureClassDefinition fcDef = featureClass.GetDefinition())
                            {
                                string length_field = fcDef.GetLengthField();

                                QueryFilter queryFilter = new QueryFilter { SubFields = PRZC.c_FLD_FC_PU_ID + "," + length_field };

                                using (RowCursor rowCursor = featureClass.Search(queryFilter))
                                {
                                    while (rowCursor.MoveNext())
                                    {
                                        using (Row row = rowCursor.Current)
                                        {
                                            int puid = Convert.ToInt32(row[PRZC.c_FLD_FC_PU_ID]);
                                            double perim = Math.Round(Convert.ToDouble(row[length_field]), 2, MidpointRounding.AwayFromZero);

                                            if (puid > 0)
                                            {
                                                PUIDs_and_perimeters.Add(puid, perim);
                                            }
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
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error populating dictionary.", LogMessageType.ERROR), true, max, val++);
                        ProMsgBox.Show($"Error populating dictionary.");
                        return;
                    }
                }
                else
                {
                    // get perimeter of each raster cell (a constant)
                    foreach (int id in PUIDs)
                    {
                        PUIDs_and_perimeters.Add(id, full_perim);
                    }
                }
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Dictionary populated: {PUIDs_and_perimeters.Count} entries."), true, ++val);

                PRZH.CheckForCancellation(token);

                // Get shared edges of each planning unit (from temp boundary table)
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Getting shared edges..."), true, ++val);
                Dictionary<int, double> PUIDs_and_shared_edges = new Dictionary<int, double>();

                if (!await QueuedTask.Run(() =>
                {
                    try
                    {
                        var tryget = PRZH.GetTable_Project(temp_table);
                        if (!tryget.success)
                        {
                            throw new Exception("Error retrieving table.");
                        }

                        using (Table table = tryget.table)
                        using (RowCursor rowCursor = table.Search())
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int src_puid = Convert.ToInt32(row[fldSource]);
                                    int nbr_puid = Convert.ToInt32(row[fldNeighbour]);
                                    double shared_perim = Math.Round(Convert.ToDouble(row["LENGTH"]), 2, MidpointRounding.AwayFromZero);

                                    // source
                                    if (PUIDs_and_shared_edges.ContainsKey(src_puid))
                                    {
                                        PUIDs_and_shared_edges[src_puid] += shared_perim;
                                    }
                                    else
                                    {
                                        PUIDs_and_shared_edges.Add(src_puid, shared_perim);
                                    }

                                    // neighbour
                                    if (PUIDs_and_shared_edges.ContainsKey(nbr_puid))
                                    {
                                        PUIDs_and_shared_edges[nbr_puid] += shared_perim;
                                    }
                                    else
                                    {
                                        PUIDs_and_shared_edges.Add(nbr_puid, shared_perim);
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error getting shared edges.", LogMessageType.ERROR), true, max, val++);
                    ProMsgBox.Show($"Error getting shared edges");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Shared edges retrieved: {PUIDs_and_shared_edges.Count} entries."), true, max, val++);
                }

                PRZH.CheckForCancellation(token);

                // Get "Self-Intersecting" edge lengths by planning unit
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Calculating Self-Intersection Edge Lengths..."), true, max, val++);
                Dictionary<int, double> PUIDs_and_self_intersecting_edges = new Dictionary<int, double>();
                foreach (int puid in PUIDs)
                {
                    // Add puid
                    PUIDs_and_self_intersecting_edges.Add(puid, 0);

                    double total_length = 0;
                    double shared_length = 0;

                    // Get the perimeter (must have one or quit!)
                    if (PUIDs_and_perimeters.ContainsKey(puid))
                    {
                        total_length = PUIDs_and_perimeters[puid];
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"PU ID {puid} not found in perimeter dictionary.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"PU ID {puid} not found in perimeter dictionary.");
                        return;
                    }

                    // Get the shared edge length (absence of KVP means shared length = 0)
                    if (PUIDs_and_shared_edges.ContainsKey(puid))
                    {
                        shared_length = PUIDs_and_shared_edges[puid];
                    }

                    // Store the non-shared (the "self-intersecting") length for each puid
                    PUIDs_and_self_intersecting_edges[puid] = total_length - shared_length;
                }
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Self-Intersections calculated: {PUIDs_and_self_intersecting_edges.Count} entries."), true, max, val++);

                #endregion

                PRZH.CheckForCancellation(token);

                #region BUILD AND FILL THE BOUNDARY TABLE

                // Now create the new table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating {PRZC.c_TABLE_PUBOUNDARY} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_TABLE_PUBOUNDARY, "", "", "Boundary Lengths");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateTable_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {PRZC.c_TABLE_PUBOUNDARY} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error creating the {PRZC.c_TABLE_PUBOUNDARY} table.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Table created."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Add fields to the table
                string fldPUID1 = PRZC.c_FLD_TAB_BOUND_ID1 + " LONG 'Planning Unit ID 1' # # #;";
                string fldPUID2 = PRZC.c_FLD_TAB_BOUND_ID2 + " LONG 'Planning Unit ID 2' # # #;";
                string fldBoundary = PRZC.c_FLD_TAB_BOUND_BOUNDARY + " DOUBLE 'Boundary Length' # # #;";

                string flds = fldPUID1 + fldPUID2 + fldBoundary;

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to {PRZC.c_TABLE_PUBOUNDARY} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_TABLE_PUBOUNDARY, flds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to {PRZC.c_TABLE_PUBOUNDARY} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error adding fields to {PRZC.c_TABLE_PUBOUNDARY} table");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully added fields."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Populate the boundary table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Populating the {PRZC.c_TABLE_PUBOUNDARY} table.."), true, ++val);

                (bool success, string message) result_bort2 = await QueuedTask.Run(async () =>
                {
                    try
                    {
                        // Get the operation object
                        EditOperation editOp = PRZH.GetEditOperation("Boundary Table Population");

                        // set up the callback
                        var tryget = PRZH.GetTable_Project(PRZC.c_TABLE_PUBOUNDARY);
                        if (!tryget.success)
                        {
                            throw new Exception("Unable to retrieve table.");
                        }

                        using (Table tab = tryget.table)
                        {
                            editOp.Callback((context) =>
                            {
                                int flusher = 0;

                                using (Table table = PRZH.GetTable_Project(PRZC.c_TABLE_PUBOUNDARY).table)
                                using (InsertCursor insertCursor = table.CreateInsertCursor())
                                using (RowBuffer rowBuffer = table.CreateRowBuffer())
                                using (Table searchTable = PRZH.GetTable_Project(temp_table).table)
                                {
                                    QueryFilter queryFilter = new QueryFilter();

                                    foreach (int puid in PUIDs)
                                    {
                                        queryFilter.WhereClause = fldSource + " = " + puid.ToString();

                                        using (RowCursor searchCursor = searchTable.Search(queryFilter, false))
                                        {
                                            while (searchCursor.MoveNext())
                                            {
                                                using (Row searchRow = searchCursor.Current)
                                                {
                                                    int id1 = Convert.ToInt32(searchRow[fldSource]);
                                                    int id2 = Convert.ToInt32(searchRow[fldNeighbour]);
                                                    double edge = Convert.ToDouble(searchRow["LENGTH"]);

                                                    // Fill the row buffer
                                                    rowBuffer[PRZC.c_FLD_TAB_BOUND_ID1] = id1;
                                                    rowBuffer[PRZC.c_FLD_TAB_BOUND_ID2] = id2;
                                                    rowBuffer[PRZC.c_FLD_TAB_BOUND_BOUNDARY] = edge;

                                                    // Insert new record
                                                    insertCursor.Insert(rowBuffer);
                                                }
                                            }
                                        }

                                        // Add one extra row for this puid if it has "self-intersecting" edge > 0
                                        double self_int_edge = PUIDs_and_self_intersecting_edges[puid];

                                        if (self_int_edge > 0)
                                        {
                                            rowBuffer[PRZC.c_FLD_TAB_BOUND_ID1] = puid;
                                            rowBuffer[PRZC.c_FLD_TAB_BOUND_ID2] = puid;
                                            rowBuffer[PRZC.c_FLD_TAB_BOUND_BOUNDARY] = self_int_edge;
                                            insertCursor.Insert(rowBuffer);
                                        }

                                        flusher++;
                                        if (flusher == 10000)
                                        {
                                            insertCursor.Flush();
                                            flusher = 0;
                                        }
                                    }

                                    insertCursor.Flush();
                                }
                            }, tab);
                        }

                        // execute the callback
                        bool worked = editOp.Execute();

                        if (worked)
                        {
                            // save edits if callback execution successful
                            bool saved = await Project.Current.SaveEditsAsync();
                            return (saved, saved ? "Operation succeeded, changes saved." : "Error saving changes.");
                        }
                        else
                            return (false, $"Error executing operation: {editOp.ErrorMessage}");
                    }
                    catch (Exception ex)
                    {
                        return (false, ex.Message);
                    }
                });

                if (!result_bort2.success)

                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error populating {PRZC.c_TABLE_PUBOUNDARY} table.", LogMessageType.ERROR), true, max, val++);
                    ProMsgBox.Show($"Error populating {PRZC.c_TABLE_PUBOUNDARY} table.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_TABLE_PUBOUNDARY} table populated."), true, max, val++);
                }

                // Index both id fields
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing {PRZC.c_TABLE_PUBOUNDARY} table id fields..."), true, ++val);
                List<string> LIST_ix = new List<string>() { PRZC.c_FLD_TAB_BOUND_ID1, PRZC.c_FLD_TAB_BOUND_ID2 };
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_TABLE_PUBOUNDARY, LIST_ix, "ix" + PRZC.c_FLD_FC_PU_ID, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error indexing {PRZC.c_TABLE_PUBOUNDARY} table id fields.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields indexed successfully."), true, ++val);
                }

                #endregion

                PRZH.CheckForCancellation(token);

                #region WRAP THINGS UP

                // Delete temp table
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {temp_table} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(temp_table);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting {temp_table} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error deleting {temp_table} table.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Table deleted."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Delete temp feature class (if applicable)
                if (pu_result.puLayerType == PlanningUnitLayerType.RASTER)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting {temp_fc} feature class..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(temp_fc);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting {temp_fc} feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting {temp_fc} feature class.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{temp_fc} feature class deleted."), true, ++val);
                    }
                }

                PRZH.CheckForCancellation(token);

                // Compact the Geodatabase
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacting the Geodatabase..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("Compact_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error compacting the geodatabase. GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error compacting geodatabase");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Geodatabase compacted."), true, ++val);
                }

                // End timer
                stopwatch.Stop();
                string message = PRZH.GetElapsedTimeMessage(stopwatch.Elapsed);
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Construction completed successfully!"), true, 1, 1);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(message), true, 1, 1);
                ProMsgBox.Show("Construction Completed Successfully!" + Environment.NewLine + Environment.NewLine + message);

                #endregion
            }
            catch (OperationCanceledException)
            {
                // Cancelled by user
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Operation cancelled by user.", LogMessageType.CANCELLATION), true, ++val);
                ProMsgBox.Show($"Operation cancelled by user.");
            }
            catch (Exception ex)
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.CANCELLATION), true, ++val);
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
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

        private async Task ValidateControls()
        {
            try
            {
                // Establish Geodatabase Object Existence:
                // 1. Planning Unit Dataset
                var try_exists = await PRZH.PUExists();
                _pu_exists = try_exists.exists;

                // 2. Boundary Lengths Table
                _blt_exists = (await PRZH.TableExists_Project(PRZC.c_TABLE_PUBOUNDARY)).exists;

                // Configure Labels:
                // 1. Planning Unit Dataset Label
                if (!_pu_exists || try_exists.puLayerType == PlanningUnitLayerType.UNKNOWN)
                {
                    CompStat_Txt_PlanningUnits_Label = "Planning Unit Dataset does not exist.  Build it.";
                }
                else if (try_exists.puLayerType == PlanningUnitLayerType.FEATURE)
                {
                    CompStat_Txt_PlanningUnits_Label = "Planning Unit Dataset exists (Feature Class).";
                }
                else if (try_exists.puLayerType == PlanningUnitLayerType.RASTER)
                {
                    CompStat_Txt_PlanningUnits_Label = "Planning Unit Dataset exists (Raster Dataset).";
                }
                else
                {
                    CompStat_Txt_PlanningUnits_Label = "Planning Unit Dataset does not exist.  Build it.";
                }

                // 2. Boundary Lengths Table Label
                if (_blt_exists)
                {
                    CompStat_Txt_BoundaryLengths_Label = "Boundary Lengths Table exists.";
                }
                else
                {
                    CompStat_Txt_BoundaryLengths_Label = "Boundary Lengths Table does not exist.";
                }

                // Configure Images:
                // 1. Planning Units
                if (_pu_exists)
                {
                    CompStat_Img_PlanningUnits_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_Img_PlanningUnits_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
                }

                // 2. Boundary Lengths Table
                if (_blt_exists)
                {
                    CompStat_Img_BoundaryLengths_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_Img_BoundaryLengths_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Warn16.png";
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private void StartOpUI()
        {
            _operationIsUnderway = true;
            Operation_Cmd_IsEnabled = false;
            OpStat_Img_Visibility = Visibility.Visible;
            OpStat_Txt_Label = "Processing...";
            ProWindowCursor = Cursors.Wait;
        }

        private void ResetOpUI()
        {
            ProWindowCursor = Cursors.Arrow;
            Operation_Cmd_IsEnabled = true;
            OpStat_Img_Visibility = Visibility.Hidden;
            OpStat_Txt_Label = "Idle.";
            _operationIsUnderway = false;
        }

        #endregion

    }
}