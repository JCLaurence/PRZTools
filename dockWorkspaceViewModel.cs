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
            _initializeCommand = new RelayCommand(() => InitializeFolder(), () => true);
            _refreshCommand = new RelayCommand(() => MessageBox.Show("Refresh Command"), () => true);
            _resetCommand = new RelayCommand(() => MessageBox.Show("Reset Command"), () => true);
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

        private string _folderStatus;
        public string FolderStatus
        {
            get { return _folderStatus; }
            set
            {
                SetProperty(ref _folderStatus, value, () => FolderStatus);
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

        private async void OpenDialogger()
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
                    {
                        this.FolderPath = i.Path;
                        this.FolderStatus = await GetFolderStatus(this.FolderPath);


                    }
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

        protected override async Task InitializeAsync()
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
                this.InitializeEnabled = ok;

                this.FolderStatus = await GetFolderStatus(pf);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task InitializeFolder()
        {
            try
            {
                // This method does the following tasks:
                // 1. If INPUT folder not found, add it.  Else, do nothing
                // 2. If OUTPUT folder not found, add it.  Else, do nothing
                // 3. If PRZ.log file not found, add it.  Else, do nothing
                // 4. If PRZ.gdb geodatabase not found, add it.  Else, do nothing
                // 5. Add line in PRZ.log file saying we just initialized the folder
                // 6. somehow trigger the ContentListing value recalculation

                // This method will use the value currently in _folderPath field

                string fp = _folderPath;

                // Ensure that main Folder Path exists
                if (!Directory.Exists(_folderPath))
                {
                    MessageBox.Show("Please select a valid Project Folder" + Environment.NewLine + Environment.NewLine + _folderPath + " does not exist");
                    return;
                }

                // Create the INPUT and OUTPUT directories and the PRZ.log file if missing.
                string pathInputFolder = Path.Combine(_folderPath, "INPUT");
                string pathOutputFolder = Path.Combine(_folderPath, "OUTPUT");
                string pathLogFile = Path.Combine(_folderPath, "PRZ.log");

                if (!Directory.Exists(pathInputFolder))
                    Directory.CreateDirectory(pathInputFolder);

                if (!Directory.Exists(pathOutputFolder))
                    Directory.CreateDirectory(pathOutputFolder);

                if (!File.Exists(pathLogFile))
                    File.Create(pathLogFile);

                // Create the File Geodatabase if missing
                string pathGDB = Path.Combine(_folderPath, "PRZ.gdb");

                // I'm here!!!

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async Task<string> GetFolderStatus(string folderPath)
        {
            try
            {
                // Project Folder should have the following elements:
                // INPUT folder
                // OUTPUT folder
                // PRZ.gdb geodatabase
                // PRZ.log log file (?)

                if (!Directory.Exists(folderPath))
                {
                    return "Directory does not exist!";
                }

                // look for INPUT and OUTPUT subdirectories
                bool hasInputFolder = false;
                bool hasOutputFolder = false;
                bool hasLogFile = false;

                string response = "";

                string inputDir = Path.Combine(folderPath, "INPUT");
                string outputDir = Path.Combine(folderPath, "OUTPUT");
                string logFile = Path.Combine(folderPath, "PRZ.log");

                if (Directory.Exists(inputDir))
                {
                    hasInputFolder = true;
                    response += " Has Input Folder ";
                }

                if (Directory.Exists(outputDir))
                {
                    hasOutputFolder = true;
                    response += " Has Output Folder ";
                }

                if (File.Exists(logFile))
                {
                    hasLogFile = true;
                    response += " Has Log File ";
                }

                // Check for existence of geodatabase here...
                string gdb = Path.Combine(folderPath, "PRZ.gdb");

                Geodatabase fgdb;
                Uri uri = new Uri(gdb);
                FileGeodatabaseConnectionPath gdbpath = new FileGeodatabaseConnectionPath(uri);

                try
                {
                    await QueuedTask.Run(() =>
                    {
                        fgdb = new Geodatabase(gdbpath);
                        response += " Has PRZ.gdb ";
                        fgdb.Dispose();
                    });

                }
                catch (GeodatabaseNotFoundOrOpenedException ex2)
                {
                    response += " NO PRZ.gdb ";
                }

                return response;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return "";
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



















