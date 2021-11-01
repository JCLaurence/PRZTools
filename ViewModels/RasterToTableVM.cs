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

        #endregion

        #region PROPERTIES

        public RasterToolsVM ParentDlg
        {
            get => _parentDlg;
            set => SetProperty(ref _parentDlg, value, () => ParentDlg);
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

        public ICommand CmdOK => new RelayCommand((paramProWin) =>
        {
            // set parent property values
            CostParent.ImportFieldPUID = SelectedPUIDField;
            CostParent.ImportFieldCost = SelectedCostField;

            (paramProWin as ProWindow).DialogResult = true;
            (paramProWin as ProWindow).Close();
        }, () => true);

        public ICommand CmdCancel => new RelayCommand((paramProWin) =>
        {
            // set parent property values (not necessary, unlike CmdOK)
            CostParent.ImportFieldPUID = SelectedPUIDField;
            CostParent.ImportFieldCost = SelectedCostField;

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


        #endregion

    }
}
