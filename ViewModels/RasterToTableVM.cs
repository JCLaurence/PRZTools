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

        private bool _ok_is_enabled;
        private RasterToolsVM _parentDlg;

        private string _rasterPath;
        private ICommand _cmdSelectRaster;
        private ICommand _cmdValidateRaster;

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

        #endregion

        #region COMMANDS

        public ICommand CmdSelectRaster => _cmdSelectRaster ?? (_cmdSelectRaster = new RelayCommand(() => SelectRaster(), () => true));

        public ICommand CmdValidateRaster => _cmdValidateRaster ?? (_cmdValidateRaster = new RelayCommand(() => ValidateRaster(), () => true));

        public ICommand CmdOK => new RelayCommand((paramProWin) =>
        {
            //// set parent property values
            //CostParent.ImportFieldPUID = SelectedPUIDField;
            //CostParent.ImportFieldCost = SelectedCostField;

            (paramProWin as ProWindow).DialogResult = true;
            (paramProWin as ProWindow).Close();
        }, () => true);

        public ICommand CmdCancel => new RelayCommand((paramProWin) =>
        {
            //// set parent property values (not necessary, unlike CmdOK)
            //CostParent.ImportFieldPUID = SelectedPUIDField;
            //CostParent.ImportFieldCost = SelectedCostField;

            (paramProWin as ProWindow).DialogResult = false;
            (paramProWin as ProWindow).Close();
        }, () => true);



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

                if (Directory.Exists(RasterPath))
                {
                    DirectoryInfo di = new DirectoryInfo(RasterPath);
                    DirectoryInfo dip = di.Parent;

                    if (dip != null)
                    {
                        initDir = dip.FullName;
                    }
                }
                else if (File.Exists(RasterPath))
                {
                    FileInfo fi = new FileInfo(RasterPath);
                    initDir = fi.DirectoryName;
                }
                else if (!string.IsNullOrEmpty(RasterPath))
                {
                    if (RasterPath.Contains(".gdb"))
                    {
                        // remove the part after .gdb
                        string gdbdir = RasterPath.Substring(0, RasterPath.IndexOf(".gdb") + 4);

                        if (Directory.Exists(gdbdir))
                        {
                            DirectoryInfo di = new DirectoryInfo(gdbdir);
                            initDir = di.Parent.FullName;
                        }
                    }
                    else if (RasterPath.Contains(".sde"))
                    {
                        // remove the part after .sde
                        string sdepath = RasterPath.Substring(0, RasterPath.IndexOf(".sde") + 4);

                        if (File.Exists(sdepath))
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
                /*
                > Raster must meet these requirements
                    - n rows, n columns
                    - coordinate system is NCC albers
                    - envelope is correct
                    - Any raster type is OK here (various types of integer and floating point raster)
                 */

                #region CREATE THE RASTER DATASET

                await QueuedTask.Run(() =>
                {
                    // Determine the type of path in RasterPath
                    if (string.IsNullOrEmpty(RasterPath) || string.IsNullOrWhiteSpace(RasterPath))
                    {
                        // Quit if no path specified
                        ProMsgBox.Show($"Raster path is null or empty.  Please specify a raster.");
                        return;
                    }
                    else if (File.Exists(RasterPath) && Path.IsPathRooted(RasterPath))
                    {
                        // Probably a file-based raster
                        FileInfo fi = new FileInfo(RasterPath);

                        // Get the file
                        if (fi == null)
                        {
                            ProMsgBox.Show($"Unable to retrieve file path of raster.\nPath: {RasterPath}");
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
                    else
                    {
                        ProMsgBox.Show($"Unable to retrieve raster from path.\n{RasterPath}");
                        return;
                    }

                    ProMsgBox.Show(rasterDataset.GetName());

                });



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

        #endregion

    }
}
