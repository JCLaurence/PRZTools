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

        private Map _map = MapView.Active.Map;
        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""
        private string _settings_Txt_ScratchFGDBPath;

        private ICommand _cmdSelectScratchFGDB;
        private ICommand _cmdRasterToTable;
        private ICommand _cmdTest;

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

        public ICommand CmdTest => _cmdTest ?? (_cmdTest = new RelayCommand(() => Test(), () => true));

        #endregion

        #region METHODS

        public async Task OnProWinLoaded()
        {
            try
            {
                _map = MapView.Active.Map;

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
                #region Show the Raster to Table dialog

                RasterToTable dlg = new RasterToTable
                {
                    Owner = Application.Current.MainWindow
                };                                                                  // View

                RasterToTableVM vm = (RasterToTableVM)dlg.DataContext;              // View Model
                vm.ParentDlg = this;

                // set other dialog field values here
                //vm.NumericFields = LIST_NumericFieldNames;
                //vm.IntFields = LIST_IntFieldNames;
                //vm.CostParent = this;
                //vm.DSName = DSName;
                //vm.DSPath = DSPath;
                //vm.DSType = DSType;

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

                //MapView mv = MapView.Active;
                //Layer lyr = mv.GetSelectedLayers().FirstOrDefault();

                //if (lyr == null)
                //{
                //    ProMsgBox.Show("No Layer Selected");
                //    return false;
                //}
                //else if (!(lyr is RasterLayer))
                //{
                //    ProMsgBox.Show("Layer is not raster layer");
                //    return false;
                //}

                //RasterLayer RL = (RasterLayer)lyr;
                //Dictionary<string, double> DICT_Pixels = new Dictionary<string, double>();
                //Dictionary<long, double> DICT_Pixels2 = new Dictionary<long, double>();

                //await QueuedTask.Run(() =>
                //{
                //    using (Raster raster = RL.GetRaster())
                //    {
                //        Envelope env = raster.GetExtent();
                //        int rows = raster.GetHeight();
                //        int cols = raster.GetWidth();

                //        using (PixelBlock pixelBlock = raster.CreatePixelBlock(cols, 1))
                //        {
                //            for (int row = 1; row <= rows; row++)
                //            {
                //                // fill a pixel block
                //                raster.Read(0, row - 1, pixelBlock);

                //                for (int col = 1; col <= cols; col++)
                //                {
                //                    if (Convert.ToByte(pixelBlock.GetNoDataMaskValue(0, col - 1, 0)) == 1)
                //                    {
                //                        var val = pixelBlock.GetValue(0, col - 1, 0);
                //                        double d = Convert.ToDouble(val);

                //                        var result = NationalGridInfo.GetIdentifierFromRowColumn(row, col, 3);
                //                        var result2 = NationalGridInfo.GetCellNumberFromRowColumn(row, col, 3);
                //                        if (result.success && result2.success)
                //                        {
                //                            DICT_Pixels.Add(result.identifier, d);
                //                            DICT_Pixels2.Add(result2.cell_number, d);
                //                        }
                //                    }
                //                }
                //            }
                //        }
                //    }
                //});

                //// Some GP variables
                //IReadOnlyList<string> toolParams;
                //IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                //GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                //string toolOutput;

                //// Build the empty table
                //string gdbpath = PRZH.GetPath_ProjectGDB();
                //toolParams = Geoprocessing.MakeValueArray(gdbpath, "test1", "", "", "");
                //toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                //toolOutput = await PRZH.RunGPTool("CreateTable_management", toolParams, toolEnvs, toolFlags);
                //if (toolOutput == null)
                //{
                //    ProMsgBox.Show($"Error creating the test1 table.");
                //    return false;
                //}

                //// Add Fields
                //string fldCellNumber = "cellnumber LONG 'Cell Number' # 0 #;";
                //string fldIdentifier = "identifier TEXT 'Cell Identifier' 20 # #;";
                //string fldValue = "cellvalue DOUBLE 'Cell Value' # 0 #";

                //string flds = fldCellNumber + fldIdentifier + fldValue;
                //string testpath = Path.Combine(gdbpath, "test1");

                //toolParams = Geoprocessing.MakeValueArray(testpath, flds);
                //toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                //toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                //if (toolOutput == null)
                //{
                //    ProMsgBox.Show($"Error adding fields");
                //    return false;
                //}

                //// Insert values from dictionaries
                //if (!await QueuedTask.Run(async () =>
                //{
                //    bool success = false;

                //    try
                //    {
                //        var loader = new EditOperation();
                //        loader.Name = "record creator";
                //        loader.ShowProgressor = false;
                //        loader.ShowModalMessageAfterFailure = false;
                //        loader.SelectNewFeatures = false;
                //        loader.SelectModifiedFeatures = false;

                //        if (!await PRZH.TableExists("test1"))
                //        {
                //            return false;
                //        }

                //        using (Table tab = await PRZH.GetTable("test1"))
                //        {
                //            loader.Callback(async (context) =>
                //            {
                //                using (Table table = await PRZH.GetTable("test1"))
                //                using (InsertCursor insertCursor = table.CreateInsertCursor())
                //                using (RowBuffer rowBuffer = table.CreateRowBuffer())
                //                {
                //                    long flusher = 0;

                //                    var cellNumbers = DICT_Pixels2.Keys.ToList();
                //                    cellNumbers.Sort();

                //                    foreach (var num in cellNumbers)
                //                    {
                //                        var val = DICT_Pixels2[num];

                //                        rowBuffer["cellnumber"] = num;
                //                        rowBuffer["cellvalue"] = val;
                //                        insertCursor.Insert(rowBuffer);

                //                        flusher++;

                //                        if (flusher == 10000)
                //                        {
                //                            insertCursor.Flush();
                //                            flusher = 0;
                //                        }
                //                    }

                //                    insertCursor.Flush();
                //                }
                //            }, tab);
                //        }

                //        // Execute all the queued "creates"
                //        success = loader.Execute();

                //        if (success)
                //        {
                //            if (!await Project.Current.SaveEditsAsync())
                //            {
                //                ProMsgBox.Show($"Error saving edits.");
                //                return false;
                //            }
                //        }
                //        else
                //        {
                //            ProMsgBox.Show($"Edit Operation error: unable to create tiles: {loader.ErrorMessage}");
                //        }

                //        return success;
                //    }
                //    catch (Exception ex)
                //    {
                //        ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                //        return false;
                //    }
                //}))
                //{
                //    ProMsgBox.Show($"Error loading the table :(");
                //    return false;
                //}

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private async Task<bool> Test()
        {
            try
            {
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion


    }
}
