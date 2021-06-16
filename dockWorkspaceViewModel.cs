using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NCC.PRZTools
{
    internal class dockWorkspaceViewModel : DockPane
    {
        private const string _dockPaneID = "NCC_PRZTools_dockWorkspace";
        private string _projectContentType = Properties.Settings.Default.PROJECT_CONTENT_TYPE;

        protected dockWorkspaceViewModel()
        {
            _testCommand = new RelayCommand(() => CommandMethod(), () => true);
            _openDialogCommand = new RelayCommand(() => OpenDialogger(), () => true);
        }

        #region Properties

        private bool _showFolderContent;
        public bool ShowFolderContent
        {
            get { return _showFolderContent; }
            set
            {
                SetProperty(ref _showFolderContent, value, () => ShowFolderContent);
                DrawContent();
            }
        }
        private bool _showGDBContent;
        public bool ShowGDBContent
        {
            get { return _showGDBContent; }
            set
            {
                SetProperty(ref _showGDBContent, value, () => ShowGDBContent);
                DrawContent();
            }
        }
        private bool _showLogContent;
        public bool ShowLogContent
        {
            get { return _showLogContent; }
            set
            {
                SetProperty(ref _showLogContent, value, () => ShowLogContent);
                DrawContent();
            }
        }

        private string _contentListing;
        public string ContentListing
        {
            get { return _contentListing; }
            set
            {
                SetProperty(ref _contentListing, value, () => ContentListing);
            }
        }


        private string _folderPath = Properties.Settings.Default.PROJECT_FOLDER_PATH;
        public string FolderPath
        {
            get { return _folderPath; }
            set
            {
                SetProperty(ref _folderPath, value, () => FolderPath);
                Properties.Settings.Default.PROJECT_FOLDER_PATH = _folderPath;
                Properties.Settings.Default.Save();
            }
        }

        private bool _initializeEnabled;
        public bool InitializeEnabled
        {
            get { return _initializeEnabled; }
            set
            {
                SetProperty(ref _initializeEnabled, value, () => InitializeEnabled);
            }
        }

        private bool _refreshEnabled;
        public bool RefreshEnabled
        {
            get { return _refreshEnabled; }
            set
            {
                SetProperty(ref _refreshEnabled, value, () => RefreshEnabled);
            }
        }

        private bool _resetEnabled;
        public bool ResetEnabled
        {
            get { return _resetEnabled; }
            set
            {
                SetProperty(ref _resetEnabled, value, () => ResetEnabled);
            }
        }


        #endregion Properties

        private void DrawContent()
        {
            try
            {
                string pct = "";

                if (this.ShowFolderContent)
                {
                    // Assemble the Folder Content Listing here...
                    this.ContentListing = "Folder Content! " + DateTime.Now.ToString();
                    pct = "FOLDER";
                }
                else if (this.ShowGDBContent)
                {
                    // Assemble the GDB Content Listing here...
                    this.ContentListing = "GDB Content! " + DateTime.Now.ToString();
                    pct = "GDB";
                }
                else if (this.ShowLogContent)
                {
                    // Assemble the Log Content Listing here...
                    this.ContentListing = "Log Content! " + DateTime.Now.ToString();
                    pct = "LOG";
                }

                Properties.Settings.Default.PROJECT_CONTENT_TYPE = pct;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        private void CommandMethod()
        {
            try
            {
                string nameo = "";

                var projMapItems = Project.Current.GetItems<MapProjectItem>();
                if (projMapItems == null)
                {
                    nameo = "no maps";
                }
                else
                {
                    List<MapProjectItem> l = projMapItems.ToList();
                    if (l.Count > 0) nameo = l.First().Name;
                    else nameo = "bad";
                }

                MessageBox.Show("Name of Map: " + nameo);

                this.FolderPath = "gotcha";

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void OpenDialogger()
        {
            try
            {
                string initDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (Directory.Exists(this.FolderPath))
                {
                    DirectoryInfo di = new DirectoryInfo(this.FolderPath);
                    DirectoryInfo dip = di.Parent;

                    if (dip != null)
                        initDir = dip.FullName;
                }

                OpenItemDialog dlg = new OpenItemDialog();
                dlg.Title = "Specify a Project Folder";
                dlg.InitialLocation = initDir;
                dlg.MultiSelect = false;
                dlg.AlwaysUseInitialLocation = true;
                dlg.Filter = ItemFilters.folders;

                bool? ok = dlg.ShowDialog();

                if (ok == true)
                {
                    IEnumerable<Item> selitems = dlg.Items;
                    Item i = selitems.First();

                    if (i != null)
                        this.FolderPath = i.Path;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }

        protected override Task InitializeAsync()
        {
            try
            {
                // Put one-time initialization code here that can be done asynchronously

                string pct = Properties.Settings.Default.PROJECT_CONTENT_TYPE;

                switch (pct)
                {
                    case "FOLDER":
                        this.ShowFolderContent = true;
                        break;
                    case "GDB":
                        this.ShowGDBContent = true;
                        break;
                    case "LOG":
                        this.ShowLogContent = true;
                        break;
                }

                string pf = Properties.Settings.Default.PROJECT_FOLDER_PATH;
                bool ok = Directory.Exists(pf);

                this.RefreshEnabled = ok;
                this.ResetEnabled = ok;

                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return Task.FromResult(1);
            }
        }

        protected override void OnShow(bool isVisible)
        {
            // fires whenever the dialog changes visibility (gets opened, closed, hidden, etc)
        }

        /// <summary>
        /// Override the default behaviour when dockpane's help icon is clicked or F1 key is pressed
        /// </summary>
        protected override void OnHelpRequested()
        {
            MessageBox.Show("Help");
        }

        private readonly ICommand _testCommand;
        public ICommand TestCommand => _testCommand;

        private readonly ICommand _openDialogCommand;
        public ICommand OpenDialogCommand => _openDialogCommand;


        private readonly ICommand _initializeCommand;
        public ICommand InitializeCommand => _initializeCommand;

        private readonly ICommand _refreshCommand;
        public ICommand RefreshCommand => _refreshCommand;

        private readonly ICommand _resetCommand;
        public ICommand ResetCommand => _resetCommand;

    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class dockWorkspace_ShowButton : Button
    {
        protected override void OnClick()
        {
            dockWorkspaceViewModel.Show();
        }
    }
}



















