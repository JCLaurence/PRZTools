using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZC = NCC.PRZTools.PRZConstants;
using PRZH = NCC.PRZTools.PRZHelper;

namespace NCC.PRZTools
{
    public class RasterToolsVM : PropertyChangedBase
    {
        public RasterToolsVM()
        {
        }

        #region FIELDS

        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""
        private string _settings_Txt_ScratchFGDBPath;

        private ICommand _cmdSelectScratchFGDB;
        private ICommand _cmdRasterToTable;
        private ICommand _cmdTest;
        private ICommand _cmdNationalRaster_Zero;
        private ICommand _cmdNationalRaster_CellNum;

        #endregion

        #region PROPERTIES

        public string Settings_Txt_ScratchFGDBPath
        {
            get => _settings_Txt_ScratchFGDBPath;
            set
            {
                SetProperty(ref _settings_Txt_ScratchFGDBPath, value, () => Settings_Txt_ScratchFGDBPath);
                Properties.Settings.Default.RT_SCRATCH_FGDB = value;
                Properties.Settings.Default.Save();
            }
        }


        public ProgressManager PM
        {
            get => _pm; set => SetProperty(ref _pm, value, () => PM);
        }


        #endregion

        #region COMMANDS

        public ICommand CmdSelectScratchFGDB => _cmdSelectScratchFGDB ?? (_cmdSelectScratchFGDB = new RelayCommand(() => SelectScratchFGDB(), () => true));

        public ICommand CmdRasterToTable => _cmdRasterToTable ?? (_cmdRasterToTable = new RelayCommand(() => RasterToTable(), () => true));

        public ICommand CmdNationalRaster_Zero => _cmdNationalRaster_Zero ?? (_cmdNationalRaster_Zero = new RelayCommand(() => NationalRaster_Zero(), () => true));

        public ICommand CmdNationalRaster_CellNum => _cmdNationalRaster_CellNum ?? (_cmdNationalRaster_CellNum = new RelayCommand(() => NationalRaster_CellNum(), () => true));

        public ICommand CmdTest => _cmdTest ?? (_cmdTest = new RelayCommand(() => Test(), () => true));

        #endregion

        #region METHODS

        public async Task OnProWinLoaded()
        {
            try
            {
                // Clear the Progress Bar
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                // Scratch FGDB
                string scratch_path = Properties.Settings.Default.RT_SCRATCH_FGDB;
                if (string.IsNullOrEmpty(scratch_path) || string.IsNullOrWhiteSpace(scratch_path))
                {
                    Settings_Txt_ScratchFGDBPath = "";
                }
                else
                {
                    Settings_Txt_ScratchFGDBPath = scratch_path;
                }

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private void SelectScratchFGDB()
        {
            try
            {
                string initDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (Directory.Exists(Settings_Txt_ScratchFGDBPath))
                {
                    DirectoryInfo di = new DirectoryInfo(Settings_Txt_ScratchFGDBPath);
                    DirectoryInfo dip = di.Parent;

                    if (dip != null)
                    {
                        initDir = dip.FullName;
                    }
                }

                // Configure the Browse Filter
                BrowseProjectFilter bf = new BrowseProjectFilter("esri_browseDialogFilters_geodatabases_file")
                {
                    Name = "File Geodatabases"
                };

                OpenItemDialog dlg = new OpenItemDialog
                {
                    Title = "Specify a Scratch File Geodatabase",
                    InitialLocation = initDir,
                    MultiSelect = false,
                    AlwaysUseInitialLocation = true,
                    BrowseFilter = bf
                };

                bool? result = dlg.ShowDialog();

                if ((dlg.Items == null) || (dlg.Items.Count() < 1))
                {
                    return;
                }

                Item item = dlg.Items.FirstOrDefault();

                if (item == null)
                {
                    return;
                }

                Settings_Txt_ScratchFGDBPath = item.Path;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> RasterToTable()
        {
            try
            {
                // Ensure I have a valid scratch workspace
                if (string.IsNullOrEmpty(Settings_Txt_ScratchFGDBPath) || string.IsNullOrWhiteSpace(Settings_Txt_ScratchFGDBPath))
                {
                    ProMsgBox.Show("Please specify a Scratch Workspace.");
                    return false;
                }

                #region Show the Raster to Table dialog

                RasterToTable dlg = new RasterToTable
                {
                    Owner = Application.Current.MainWindow
                };                                                                  // View

                RasterToTableVM vm = (RasterToTableVM)dlg.DataContext;              // View Model
                vm.ParentDlg = this;

                // set other dialog field values here
                vm.FgdbPath = Settings_Txt_ScratchFGDBPath;

                // Closed Event Handler
                dlg.Closed += (o, e) =>
                {
                    // Event Handler for Dialog close in case I need to do things...
                    // System.Diagnostics.Debug.WriteLine("Pro Window Dialog Closed";)
                };

                // Loaded Event Handler
                dlg.Loaded += (sender, e) =>
                {
                    if (vm != null)
                    {
                        vm.OnProWinLoaded();
                    }
                };

                bool? result = dlg.ShowDialog();

                // Take whatever action required here once the dialog is close (true or false)
                // do stuff here!

                if (!result.HasValue || result.Value == false)
                {
                    // Cancelled by user
                    return false;
                }

                #endregion



                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private async Task<bool> NationalRaster_Zero()
        {
            int val = 0;
            int max = 50;

            try
            {
                // Get paths
                string gdbpath = PRZH.GetPath_RTScratchGDB();

                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags_All = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                GPExecuteToolFlags toolFlags_GPRefresh = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                GPExecuteToolFlags toolFlags_GP = GPExecuteToolFlags.GPThread;
                string toolOutput;

                // Initialize ProgressBar and Progress Log
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Raster Tools logger..."), false, max, ++val);

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Ensure the Scratch GDB exists
                var outcome = await PRZH.GDBExists_RTScratch();
                if (!outcome.exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Raster tools scratch geodatabase not found: {gdbpath}", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Raster tool scratch geodatabase not found at {gdbpath}.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Geodatabase found."), true, ++val);
                }

                // Prepare the Output Spatial Reference
                SpatialReference SR = PRZH.GetSR_PRZCanadaAlbers();
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Output Spatial Reference: {SR.Name}"), true, ++val);

                // Get the National Grid Extent envelope
                Envelope env = NationalGrid.GetNatGridEnvelope();

                // Build Constant Raster
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating the {PRZC.c_RAS_NATGRID_ZERO} raster dataset (constant 1_BIT integer raster, value = 0)..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_NATGRID_ZERO, 0, "INTEGER", 1000, env);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: SR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateConstantRaster_sa", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {PRZC.c_RAS_NATGRID_ZERO} raster dataset.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error creating the {PRZC.c_RAS_NATGRID_ZERO} raster dataset.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_NATGRID_ZERO} raster dataset created successfully."), true, ++val);
                }

                // Compact the Geodatabase
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacting the Geodatabase..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath);
                toolOutput = await PRZH.RunGPTool("Compact_management", toolParams, null, toolFlags_GPRefresh);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error compacting the geodatabase. GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error compacting the geodatabase.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Geodatabase compacted."), true, ++val);
                }

                // Wrap things up
                stopwatch.Stop();
                string message = PRZH.GetElapsedTimeMessage(stopwatch.Elapsed);
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Raster created successfully!"), true, 1, 1);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(message), true, 1, 1);

                ProMsgBox.Show("Raster created successfully!" + Environment.NewLine + Environment.NewLine + message);

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                return false;
            }
        }

        private async Task<bool> NationalRaster_CellNum()
        {
            int val = 0;
            int max = 50;

            try
            {
                // Get paths
                string gdbpath = PRZH.GetPath_RTScratchGDB();

                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags_All = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                GPExecuteToolFlags toolFlags_GPRefresh = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                GPExecuteToolFlags toolFlags_GP = GPExecuteToolFlags.GPThread;
                string toolOutput;

                // Initialize ProgressBar and Progress Log
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Raster Tools logger..."), false, max, ++val);

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Ensure the Scratch GDB exists
                var gdb_outcome = await PRZH.GDBExists_RTScratch();
                if (!gdb_outcome.exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Raster tools scratch geodatabase not found: {gdbpath}", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Raster tool scratch geodatabase not found at {gdbpath}.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Geodatabase found."), true, ++val);
                }

                // Prepare the Output Spatial Reference
                SpatialReference SR = PRZH.GetSR_PRZCanadaAlbers();
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Output Spatial Reference: {SR.Name}"), true, ++val);

                // Get the National Grid Extent envelope
                Envelope env = NationalGrid.GetNatGridEnvelope();

                // Build Constant Raster
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating {PRZC.c_RAS_TEMP_1} raster dataset (constant 1_BIT integer raster, value = 0)..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_TEMP_1, 0, "INTEGER", 1000, env);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: SR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateConstantRaster_sa", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {PRZC.c_RAS_TEMP_1} raster dataset.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error creating the {PRZC.c_RAS_TEMP_1} raster dataset.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_TEMP_1} raster dataset created successfully."), true, ++val);
                }

                // Copy the raster to the correct bitdepth of int32 (to allow storage of humongous ID numbers
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Copying {PRZC.c_RAS_TEMP_1} to {PRZC.c_RAS_TEMP_2} for 32_BIT_UNSIGNED bit depth..."), true, ++val);

                toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_TEMP_1, PRZC.c_RAS_TEMP_2, "", "", "1", "", "", "32_BIT_UNSIGNED", "", "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: SR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CopyRaster_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying {PRZC.c_RAS_TEMP_1} to {PRZC.c_RAS_TEMP_2}.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error copying {PRZC.c_RAS_TEMP_1} to {PRZC.c_RAS_TEMP_2}.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Raster dataset copied successfully."), true, ++val);
                }

                // Delete the final raster if it is present
                if (await PRZH.RasterExists_RTScratch(PRZC.c_RAS_NATGRID_CELLNUMS))
                {
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_NATGRID_CELLNUMS, "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting {PRZC.c_RAS_NATGRID_CELLNUMS} raster.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting {PRZC.c_RAS_NATGRID_CELLNUMS} rasters");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Raster deleted."), true, ++val);
                    }
                }

                // Assign Cell Numbers
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Assigning national grid cell numbers..."), true, ++val);

                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        // Ensure the raster is present
                        if (!await PRZH.RasterExists_RTScratch(PRZC.c_RAS_TEMP_2))
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to find {PRZC.c_RAS_TEMP_2} raster dataset."), true, ++val);
                            ProMsgBox.Show($"Unable to find {PRZC.c_RAS_TEMP_2} raster dataset.");
                            return false;
                        }

                        // try to get raster dataset
                        var getras_outcome = await PRZH.GetRaster_RTScratch(PRZC.c_RAS_TEMP_2);

                        if (!getras_outcome.success)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve {PRZC.c_RAS_TEMP_2} raster dataset."), true, ++val);
                            ProMsgBox.Show($"Unable to retrieve {PRZC.c_RAS_TEMP_2} raster dataset.");
                            return false;
                        }

                        // Update cell values from zeros to cell numbers
                        using (RasterDataset rasterDataset = getras_outcome.rasterDataset)
                        {
                            // Get the virtual Raster from Band 1 of the Raster Dataset
                            Raster raster = rasterDataset.CreateFullRaster();

                            // Get the row and column counts
                            int rowcount = raster.GetHeight();
                            int colcount = raster.GetWidth();

                            int block_height = 1000;
                            int block_width = colcount;
                            int block_count = rowcount / block_height;

                            int r = 0;
                            // Create a Raster Cursor to iterate over blocks of raster cells
                            using (RasterCursor rasterCursor = raster.CreateCursor(block_width, block_height))
                            {
                                do
                                {
                                    using (PixelBlock pixelBlock = rasterCursor.Current)
                                    {
                                        r++;

                                        ProMsgBox.Show($"Iteration {r}: Height: {pixelBlock.GetHeight()}   Width: {pixelBlock.GetWidth()}");
                                    }
                                } while (rasterCursor.MoveNext());
                            }

                            ProMsgBox.Show($"Iterations: {r}");
                            return false;

                            #region THIS WORKS

                            //// Get the row and column counts
                            //int rowcount = raster.GetHeight();
                            //int colcount = raster.GetWidth();

                            //// Create a PixelBlock 1 row high, stretching across all columns
                            //PixelBlock pixelBlock = raster.CreatePixelBlock(colcount, 1);

                            //// Iterate row by row
                            //for (int row = 1; row <= 1000; row++)
                            //{
                            //    // Initialize the pixel block
                            //    raster.Read(0, row - 1, pixelBlock);

                            //    // write the pixel block contents to an array
                            //    int[,] array = (int[,])pixelBlock.GetPixelData(0, true);

                            //    // Loop through each cell (left to right) within the parent row
                            //    for (int col = 1; col <= colcount; col++)
                            //    {
                            //        array[col - 1, 0] = 30;
                            //    }

                            //    // write the array values back to the pixel block, even if unchanged
                            //    pixelBlock.SetPixelData(0, array);

                            //    // write the pixel block data back to the raster
                            //    raster.Write(0, row - 1, pixelBlock);

                            //    PRZH.UpdateProgress(PM, null, true, 1000, row - 1);
                            //}

                            #endregion

                            // Save the raster
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Saving updated raster to {PRZC.c_RAS_NATGRID_CELLNUMS}."), true, max, ++val);
                            raster.SaveAs(PRZC.c_RAS_NATGRID_CELLNUMS, rasterDataset.GetDatastore(), "GRID");
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_NATGRID_CELLNUMS} raster dataset saved successfully."), true, max, ++val);
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{ex.Message}", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                        return false;
                    }
                }))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error assigning cell numbers to pixels.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error assigning cell numbers to pixels.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Cell numbers assigned."), true, ++val);
                }

                // Build Pyramids
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Building pyramids..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_NATGRID_CELLNUMS, -1, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("BuildPyramids_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error building pyramids.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error building pyramids.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"pyramids built successfully."), true, ++val);
                }

                // Calculate Statistics
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Calculating Statistics..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_NATGRID_CELLNUMS);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CalculateStatistics_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error calculating statistics.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error calculating statistics.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Statistics calculated successfully."), true, ++val);
                }

                // Delete the temp rasters
                string rasters_to_delete = PRZC.c_RAS_TEMP_1 + ";" + PRZC.c_RAS_TEMP_2;
                toolParams = Geoprocessing.MakeValueArray(rasters_to_delete, "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting temp rasters.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error deleting temp rasters.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Temp rasters deleted."), true, ++val);
                }

                // Compact the Geodatabase
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacting the Geodatabase..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath);
                toolOutput = await PRZH.RunGPTool("Compact_management", toolParams, null, toolFlags_GPRefresh);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error compacting the geodatabase. GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error compacting the geodatabase.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Geodatabase compacted."), true, ++val);
                }

                // Wrap things up
                stopwatch.Stop();
                string message = PRZH.GetElapsedTimeMessage(stopwatch.Elapsed);
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Raster created successfully!"), true, 1, 1);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(message), true, 1, 1);

                ProMsgBox.Show("Raster created successfully!" + Environment.NewLine + Environment.NewLine + message);

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                return false;
            }
        }

        private void SelectRaster()
        {
            try
            {
                string RasterPath = "c:\temp";
                string initDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (Directory.Exists(RasterPath) && Path.IsPathRooted(RasterPath))
                {
                    DirectoryInfo di = new DirectoryInfo(RasterPath);
                    if (di.Parent != null)
                    {
                        initDir = di.Parent.FullName;
                    }
                }
                else if (File.Exists(RasterPath) && Path.IsPathRooted(RasterPath))
                {
                    FileInfo fi = new FileInfo(RasterPath);
                    initDir = fi.DirectoryName;
                }
                else if (!string.IsNullOrEmpty(RasterPath))
                {
                    if (RasterPath.Contains(".gdb"))
                    {
                        // remove the part after .gdb
                        string gdbpath = RasterPath.Substring(0, RasterPath.IndexOf(".gdb") + 4);

                        if (Directory.Exists(gdbpath) && Path.IsPathRooted(gdbpath))
                        {
                            DirectoryInfo di = new DirectoryInfo(gdbpath);
                            initDir = di.Parent.FullName;
                        }
                    }
                    else if (RasterPath.Contains(".sde"))
                    {
                        // remove the part after .sde
                        string sdepath = RasterPath.Substring(0, RasterPath.IndexOf(".sde") + 4);

                        if (File.Exists(sdepath) && Path.IsPathRooted(sdepath))
                        {
                            FileInfo fi = new FileInfo(sdepath);
                            initDir = fi.DirectoryName;
                        }
                    }
                }

                // Configure the Browse Filter
                BrowseProjectFilter bf = BrowseProjectFilter.GetFilter(ItemFilters.rasters);

                SaveItemDialog dlg = new SaveItemDialog
                {
                    Title = "Specify Output Raster",
                    InitialLocation = initDir,
                    AlwaysUseInitialLocation = true,
                    BrowseFilter = bf,
                    DefaultExt = ".tif",
                    OverwritePrompt = true
                };

                bool? result = dlg.ShowDialog();

                // stop if user didn't specify anything
                if (!result.HasValue || !result.Value)
                {
                    return;
                }

                ProMsgBox.Show($"True\nFilePath: {dlg.FilePath}");



                //OpenItemDialog dlg = new OpenItemDialog
                //{
                //    Title = "Specify an output National Grid Raster",
                //    InitialLocation = initDir,
                //    MultiSelect = false,
                //    AlwaysUseInitialLocation = true,
                //    BrowseFilter = bf
                //};

                //bool? result = dlg.ShowDialog();

                //if ((dlg.Items == null) || (dlg.Items.Count() < 1))
                //{
                //    return;
                //}

                //Item item = dlg.Items.FirstOrDefault();

                //if (item == null)
                //{
                //    return;
                //}

                //string thePath = item.Path;

                //ProMsgBox.Show(thePath);

                //RasterPath = thePath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }


        private async Task<bool> Test()
        {
            try
            {
                int rowcount = 4500;
                int colcount = 5500;

                // Create array of cell numbers for this row
                var a = Array.CreateInstance(typeof(int), colcount, 1);

                ProMsgBox.Show($"Lower Bound (dim 0): {a.GetLowerBound(0)}\nLower Bound (dim 1): {a.GetLowerBound(1)}");
                ProMsgBox.Show($"Upper Bound (dim 0): {a.GetUpperBound(0)}\nUpper Bound (dim 1): {a.GetUpperBound(1)}");




                ProMsgBox.Show("Bort");
                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message);
                return false;
            }
        }

        #endregion


    }
}
