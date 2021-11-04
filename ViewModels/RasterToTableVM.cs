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
    public class RasterToTableVM : PropertyChangedBase
    {
        public RasterToTableVM()
        {
        }

        #region FIELDS

        private Map _map;

        private string _fgdbPath;

        private bool _ok_is_enabled;
        private RasterToolsVM _parentDlg;

        private string _rasterPath;
        private string _tableName;
        private ICommand _cmdSelectRaster;
        private ICommand _cmdValidateRaster;
        private ICommand _cmdRasterToTable;

        private bool _zeroIsNoData_IsChecked;

        #endregion

        #region PROPERTIES

        public RasterToolsVM ParentDlg
        {
            get => _parentDlg;
            set => SetProperty(ref _parentDlg, value, () => ParentDlg);
        }

        public bool ZeroIsNoData_IsChecked
        {
            get => _zeroIsNoData_IsChecked;
            set
            {
                SetProperty(ref _zeroIsNoData_IsChecked, value, () => ZeroIsNoData_IsChecked);
                Properties.Settings.Default.RT_ZERO_IS_NODATA = value;
                Properties.Settings.Default.Save();
            }
        }

        public string FgdbPath
        {
            get => _fgdbPath;
            set
            {
                SetProperty(ref _fgdbPath, value, () => FgdbPath);
            }
        }

        public string RasterPath
        {
            get => _rasterPath;
            set
            {
                SetProperty(ref _rasterPath, value, () => RasterPath);
                Properties.Settings.Default.RT_RASTER_PATH = value;
                Properties.Settings.Default.Save();
            }
        }

        public string TableName
        {
            get => _tableName;
            set
            {
                SetProperty(ref _tableName, value, () => TableName);
                //Properties.Settings.Default.RT_RASTER_PATH = value;
                //Properties.Settings.Default.Save();
            }
        }

        #endregion

        #region COMMANDS

        public ICommand CmdSelectRaster => _cmdSelectRaster ?? (_cmdSelectRaster = new RelayCommand(() => SelectRaster(), () => true));

        public ICommand CmdValidateRaster => _cmdValidateRaster ?? (_cmdValidateRaster = new RelayCommand(async () =>
        {
            if (await ValidateRaster())
            {
                ProMsgBox.Show("Raster is valid!");
            }
        }, () => true));

        public ICommand CmdRasterToTable => _cmdRasterToTable ?? (_cmdRasterToTable = new RelayCommand(() => RasterToTable(), () => true));

        //public ICommand CmdOK => new RelayCommand((paramProWin) =>
        //{
        //    //// set parent property values
        //    //CostParent.ImportFieldPUID = SelectedPUIDField;
        //    //CostParent.ImportFieldCost = SelectedCostField;

        //    (paramProWin as ProWindow).DialogResult = true;
        //    (paramProWin as ProWindow).Close();
        //}, () => true);

        //public ICommand CmdCancel => new RelayCommand((paramProWin) =>
        //{
        //    //// set parent property values (not necessary, unlike CmdOK)
        //    //CostParent.ImportFieldPUID = SelectedPUIDField;
        //    //CostParent.ImportFieldCost = SelectedCostField;

        //    (paramProWin as ProWindow).DialogResult = false;
        //    (paramProWin as ProWindow).Close();
        //}, () => true);



        #endregion

        #region METHODS

        public async Task OnProWinLoaded()
        {
            try
            {
                _map = MapView.Active.Map;

                // Raster Path
                string rasterpath = Properties.Settings.Default.RT_RASTER_PATH;
                if (string.IsNullOrEmpty(rasterpath) || string.IsNullOrWhiteSpace(rasterpath))
                {
                    RasterPath = "";
                }
                else
                {
                    RasterPath = rasterpath;
                }

                // zero is nodata
                ZeroIsNoData_IsChecked = Properties.Settings.Default.RT_ZERO_IS_NODATA;

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private void SelectRaster()
        {
            try
            {
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
                BrowseProjectFilter bf = new BrowseProjectFilter("esri_browseDialogFilters_rasters")
                {
                    Name = "Rasters"
                };

                OpenItemDialog dlg = new OpenItemDialog
                {
                    Title = "Specify a National Grid Raster",
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

                string thePath = item.Path;

                RasterPath = thePath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> ValidateRaster()
        {
            RasterDataset rasterDataset = null;

            try
            {

                #region CREATE THE RASTER DATASET

                if (!await QueuedTask.Run(() =>
                {
                    // Determine the type of path in RasterPath
                    if (string.IsNullOrEmpty(RasterPath) || string.IsNullOrWhiteSpace(RasterPath))
                    {
                        // Quit if no path specified
                        ProMsgBox.Show($"Raster path is null or empty.  Please specify a raster.");
                        return false;
                    }
                    else if (File.Exists(RasterPath) && Path.IsPathRooted(RasterPath))
                    {
                        // Probably a file-based raster
                        FileInfo fi = new FileInfo(RasterPath);

                        // Get the file
                        if (fi == null)
                        {
                            ProMsgBox.Show($"Unable to retrieve file path of raster.\nPath: {RasterPath}");
                            return false;
                        }

                        // Get the raster dataset
                        FileSystemConnectionPath connPath = new FileSystemConnectionPath(new Uri(fi.DirectoryName), FileSystemDatastoreType.Raster);
                        using (FileSystemDatastore dataStore = new FileSystemDatastore(connPath))
                        {
                            rasterDataset = dataStore.OpenDataset<RasterDataset>(fi.Name);
                        }
                    }
                    else if (RasterPath.Contains(".gdb") && RasterPath.Length >= RasterPath.IndexOf(".gdb") + 6)
                    {
                        // Probably a file geodatabase raster
                        string gdbdir = RasterPath.Substring(0, RasterPath.IndexOf(".gdb") + 4);
                        string rastername = RasterPath.Substring(RasterPath.IndexOf(".gdb") + 5);

                        // Get raster dataset
                        FileGeodatabaseConnectionPath connPath = new FileGeodatabaseConnectionPath(new Uri(gdbdir));

                        using (Geodatabase geodatabase = new Geodatabase(connPath))
                        {
                            rasterDataset = geodatabase.OpenDataset<RasterDataset>(rastername);
                        }
                    }
                    else if (RasterPath.Contains(".sde") && RasterPath.Length >= RasterPath.IndexOf(".sde") + 6)
                    {
                        // probably an enterprise geodatabase raster
                        string sdedir = RasterPath.Substring(0, RasterPath.IndexOf(".sde") + 4);
                        string rastername = RasterPath.Substring(RasterPath.IndexOf(".sde") + 5);

                        // Get raster dataset
                        DatabaseConnectionFile connPath = new DatabaseConnectionFile(new Uri(sdedir));

                        using (Geodatabase geodatabase = new Geodatabase(connPath))
                        {
                            rasterDataset = geodatabase.OpenDataset<RasterDataset>(rastername);
                        }
                    }
                    else
                    {
                        ProMsgBox.Show($"Unable to retrieve raster from path.\n{RasterPath}");
                        return false;
                    }

                    return true;
                }))
                {
                    return false;
                }

                #endregion

                #region VALIDATE RASTER 

                if (!await QueuedTask.Run(() =>
                {
                    using (Raster raster = rasterDataset.CreateFullRaster())
                    {
                        // Spatial Reference
                        SpatialReference rasterSR = raster.GetSpatialReference();

                        if (rasterSR == null || rasterSR.IsUnknown)
                        {
                            ProMsgBox.Show("Raster has null or unknown spatial reference.");
                            return false;
                        }

                        SpatialReference NatGridSR = NationalGridInfo.CANADA_ALBERS_SR;

                        string NatGridSR_wkt = NatGridSR.Wkt.Replace(NatGridSR.Name, "");
                        string rasterSR_wkt = rasterSR.Wkt.Replace(rasterSR.Name, "");

                        if (!string.Equals(NatGridSR_wkt, rasterSR_wkt, StringComparison.OrdinalIgnoreCase))
                        {
                            ProMsgBox.Show("Raster spatial reference does not equal the required National Grid Albers projection.");
                            return false;
                        }

                        // Row and Column Count
                        int rows = raster.GetHeight();
                        int cols = raster.GetWidth();

                        if (rows != NationalGridInfo.ROWCOUNT_3 || cols != NationalGridInfo.COLUMNCOUNT_3)
                        {
                            ProMsgBox.Show($"Incorrect raster size.  Raster has {rows} rows and {cols} columns.\nRaster must have {NationalGridInfo.ROWCOUNT_3} rows and {NationalGridInfo.COLUMNCOUNT_3} columns.");
                            return false;
                        }

                        // cell size
                        var size = raster.GetMeanCellSize();
                        double rasterDimX = Math.Round(size.Item1, 3, MidpointRounding.AwayFromZero);
                        double rasterDimY = Math.Round(size.Item2, 3, MidpointRounding.AwayFromZero);

                        if (rasterDimX != 1000 | rasterDimY != 1000)
                        {
                            ProMsgBox.Show($"Raster cells are {rasterDimX} by {rasterDimY} m. Cell size must be 1000 m x 1000 m.");
                            return false;
                        }

                        // Extent
                        Envelope rasterEnv = raster.GetExtent();
                        double rasterMinX = Math.Round(rasterEnv.XMin, 3, MidpointRounding.AwayFromZero);
                        double rasterMinY = Math.Round(rasterEnv.YMin, 3, MidpointRounding.AwayFromZero);
                        double rasterMaxX = Math.Round(rasterEnv.XMax, 3, MidpointRounding.AwayFromZero);
                        double rasterMaxY = Math.Round(rasterEnv.YMax, 3, MidpointRounding.AwayFromZero);

                        if (Convert.ToDouble(NationalGridInfo.MIN_X_COORDINATE) != rasterMinX |
                            Convert.ToDouble(NationalGridInfo.MIN_Y_COORDINATE) != rasterMinY |
                            Convert.ToDouble(NationalGridInfo.MAX_X_COORDINATE) != rasterMaxX |
                            Convert.ToDouble(NationalGridInfo.MAX_Y_COORDINATE) != rasterMaxY)
                        {
                            ProMsgBox.Show("Raster envelope does not match National Grid envelope.\n" +
                                            $"Raster envelope XMin: {rasterMinX} YMin: {rasterMinY} XMax: {rasterMaxX} YMax: {rasterMaxY}\n" +
                                            $"National Grid envelope XMin: {NationalGridInfo.MIN_X_COORDINATE} YMin: {NationalGridInfo.MIN_Y_COORDINATE} XMax: {NationalGridInfo.MAX_X_COORDINATE} YMax: {NationalGridInfo.MAX_Y_COORDINATE}");
                            return false;
                        }

                        // Band Count
                        if (raster.GetBandCount() != 1)
                        {
                            ProMsgBox.Show("Raster has more than one band.  There should only be a single band.");
                            return false;
                        }
                    }

                    return true;
                }))
                {
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
            finally
            {
                if (rasterDataset != null)
                {
                    rasterDataset.Dispose();
                }
            }
        }

        private async Task<bool> RasterToTable()
        {
            try
            {
                // Validate the table name
                string tableName = TableName;
                if (string.IsNullOrEmpty(tableName) || string.IsNullOrWhiteSpace(tableName))
                {
                    ProMsgBox.Show("No table name specified");
                    return false;
                }

                // Validate the Raster
                if (!await ValidateRaster())
                {
                    return false;
                }

                // Validate the Scratch Workspace path
                if (string.IsNullOrEmpty(FgdbPath) || string.IsNullOrWhiteSpace(FgdbPath))
                {
                    ProMsgBox.Show("Invalid workspace.");
                    return false;
                }
                // TODO: More validation here for output workspace

                // Storage for Cell Numbers + Cell Values
                Dictionary<long, double> DICT_Pixels = new Dictionary<long, double>();  // key = cell number    value = cell value

                // Create and Process the Raster Dataset
                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (RasterDataset rasterDataset = await GetRasterDataset(RasterPath))
                        {
                            if (rasterDataset == null)
                            {
                                ProMsgBox.Show($"Unable to retrieve raster dataset from path\n{RasterPath}");
                                return false;
                            }

                            using (Raster raster = rasterDataset.CreateFullRaster())
                            {
                                if (raster == null)
                                {
                                    ProMsgBox.Show("Unable to retrieve Raster object from raster dataset");
                                    return false;
                                }

                                Envelope env = raster.GetExtent();
                                int rows = raster.GetHeight();
                                int cols = raster.GetWidth();

                                using (PixelBlock pixelBlock = raster.CreatePixelBlock(cols, 1))
                                {
                                    for (int row = 1; row <= rows; row++)
                                    {
                                        // fill a pixel block
                                        raster.Read(0, row - 1, pixelBlock);

                                        for (int col = 1; col <= cols; col++)
                                        {
                                            if (Convert.ToByte(pixelBlock.GetNoDataMaskValue(0, col - 1, 0)) == 1)
                                            {
                                                var val = pixelBlock.GetValue(0, col - 1, 0);
                                                double d = Convert.ToDouble(val);

                                                if (ZeroIsNoData_IsChecked)
                                                {
                                                    if (d != 0)
                                                    {
                                                        var result = NationalGridInfo.GetCellNumberFromRowColumn(row, col, 3);
                                                        if (result.success)
                                                        {
                                                            DICT_Pixels.Add(result.cell_number, d);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    var result = NationalGridInfo.GetCellNumberFromRowColumn(row, col, 3);
                                                    if (result.success)
                                                    {
                                                        DICT_Pixels.Add(result.cell_number, d);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }))
                {
                    ProMsgBox.Show("Unable to process the raster");
                    return false;
                }

                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                // Build the empty table
                toolParams = Geoprocessing.MakeValueArray(FgdbPath, TableName, "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: FgdbPath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateTable_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    ProMsgBox.Show($"Error creating the {TableName} table.");
                    return false;
                }

                // Add Fields
                string fldCellNumber = "cellnumber LONG 'Cell Number' # 0 #;";
                string fldValue = "cellvalue DOUBLE 'Cell Value' # 0 #";

                string flds = fldCellNumber + fldValue;
                string testpath = Path.Combine(FgdbPath, TableName);

                toolParams = Geoprocessing.MakeValueArray(testpath, flds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: FgdbPath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    ProMsgBox.Show($"Error adding fields to {TableName}");
                    return false;
                }

                // Insert values from dictionaries
                if (!await QueuedTask.Run(async () =>
                {
                    bool success = false;

                    try
                    {
                        var loader = new EditOperation();
                        loader.Name = "record creator";
                        loader.ShowProgressor = false;
                        loader.ShowModalMessageAfterFailure = false;
                        loader.SelectNewFeatures = false;
                        loader.SelectModifiedFeatures = false;

                        using (Geodatabase gdb = await PRZH.GetFileGDB(FgdbPath))
                        using (Table tab = await PRZH.GetTable(gdb, TableName))
                        {
                            loader.Callback(async (context) =>
                            {
                                using (Geodatabase fgdb = await PRZH.GetFileGDB(FgdbPath))
                                using (Table table = await PRZH.GetTable(fgdb, TableName))
                                using (InsertCursor insertCursor = table.CreateInsertCursor())
                                using (RowBuffer rowBuffer = table.CreateRowBuffer())
                                {
                                    long flusher = 0;

                                    var cellNumbers = DICT_Pixels.Keys.ToList();
                                    cellNumbers.Sort();

                                    foreach (var num in cellNumbers)
                                    {
                                        var val = DICT_Pixels[num];

                                        rowBuffer["cellnumber"] = num;
                                        rowBuffer["cellvalue"] = val;
                                        insertCursor.Insert(rowBuffer);

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

                        // Execute all the queued "creates"
                        success = loader.Execute();

                        if (success)
                        {
                            if (!await Project.Current.SaveEditsAsync())
                            {
                                ProMsgBox.Show($"Error saving edits.");
                                return false;
                            }
                        }
                        else
                        {
                            ProMsgBox.Show($"Edit Operation error: unable to insert table records: {loader.ErrorMessage}");
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
                    ProMsgBox.Show($"Error loading the table :(");
                    return false;
                }

                // Add Attribute Index
                List<string> LIST_ix = new List<string>() { "cellnumber" };
                toolParams = Geoprocessing.MakeValueArray(testpath, LIST_ix, "ix_cellnumber", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: FgdbPath);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    ProMsgBox.Show("Error adding index to cellnumber field");
                    return false;
                }

                ProMsgBox.Show("Raster converted to table successfully.");
                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private async Task<RasterDataset> GetRasterDataset(string rasterPath)
        {
            try
            {
                RasterDataset rasterDataset = null;

                await QueuedTask.Run(() =>
                {
                    // Determine the type of path in RasterPath
                    if (string.IsNullOrEmpty(RasterPath) || string.IsNullOrWhiteSpace(RasterPath))
                    {
                        // Quit if no path specified
                        return;
                    }
                    else if (File.Exists(RasterPath) && Path.IsPathRooted(RasterPath))
                    {
                        // Probably a file-based raster
                        FileInfo fi = new FileInfo(RasterPath);

                        // Get the file
                        if (fi == null)
                        {
                            return;
                        }

                        // Get the raster dataset
                        FileSystemConnectionPath connPath = new FileSystemConnectionPath(new Uri(fi.DirectoryName), FileSystemDatastoreType.Raster);
                        using (FileSystemDatastore dataStore = new FileSystemDatastore(connPath))
                        {
                            rasterDataset = dataStore.OpenDataset<RasterDataset>(fi.Name);
                        }
                    }
                    else if (RasterPath.Contains(".gdb") && RasterPath.Length >= RasterPath.IndexOf(".gdb") + 6)
                    {
                        // Probably a file geodatabase raster
                        string gdbdir = RasterPath.Substring(0, RasterPath.IndexOf(".gdb") + 4);
                        string rastername = RasterPath.Substring(RasterPath.IndexOf(".gdb") + 5);

                        // Get raster dataset
                        FileGeodatabaseConnectionPath connPath = new FileGeodatabaseConnectionPath(new Uri(gdbdir));

                        using (Geodatabase geodatabase = new Geodatabase(connPath))
                        {
                            rasterDataset = geodatabase.OpenDataset<RasterDataset>(rastername);
                        }
                    }
                    else if (RasterPath.Contains(".sde") && RasterPath.Length >= RasterPath.IndexOf(".sde") + 6)
                    {
                        // probably an enterprise geodatabase raster
                        string sdedir = RasterPath.Substring(0, RasterPath.IndexOf(".sde") + 4);
                        string rastername = RasterPath.Substring(RasterPath.IndexOf(".sde") + 5);

                        // Get raster dataset
                        DatabaseConnectionFile connPath = new DatabaseConnectionFile(new Uri(sdedir));

                        using (Geodatabase geodatabase = new Geodatabase(connPath))
                        {
                            rasterDataset = geodatabase.OpenDataset<RasterDataset>(rastername);
                        }
                    }
                });

                return rasterDataset;   // might be null
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion

    }
}
