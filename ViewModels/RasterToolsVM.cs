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
