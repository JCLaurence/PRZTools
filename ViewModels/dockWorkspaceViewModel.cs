using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.GeoProcessing;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using PRZH = NCC.PRZTools.PRZHelper;
using PRZC = NCC.PRZTools.PRZConstants;

namespace NCC.PRZTools
{
    internal class dockWorkspaceViewModel : DockPane
    {
        private const string _dockPaneID = "NCC_PRZTools_dockWorkspace";
        private string _projectContentType = Properties.Settings.Default.WORKSPACE_DISPLAY_MODE;

        protected dockWorkspaceViewModel()
        {

        }

        #region COMMANDS

        private ICommand _testCommand;
        private ICommand _openDialogCommand;
        private ICommand _initializeCommand;
        private ICommand _resetCommand;
        private ICommand _openFolderCommand;

        public ICommand TestCommand {get { return _testCommand ?? (_testCommand = new RelayCommand(() => CommandMethod(), () => true)); }}

        public ICommand OpenDialogCommand { get { return _openDialogCommand ?? (_openDialogCommand = new RelayCommand(() => OpenDialogger(), () => true)); } }

        public ICommand InitializeCommand { get { return _initializeCommand ?? (_initializeCommand = new RelayCommand(async () => await InitializeFolder(), () => true)); } }

        public ICommand ResetCommand { get { return _resetCommand ?? (_resetCommand = new RelayCommand(async () => await ResetFolder(), () => true)); } }

        public ICommand OpenFolderCommand { get { return _openFolderCommand ?? (_openFolderCommand = new RelayCommand(() => OpenProjectFolder(), () => true)); } }

        #endregion COMMANDS

        #region PROPERTIES

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

        private string _folderPath = Properties.Settings.Default.WORKSPACE_PATH;
        public string FolderPath
        {
            get { return _folderPath; }
            set
            {
                SetProperty(ref _folderPath, value, () => FolderPath);
                Properties.Settings.Default.WORKSPACE_PATH = _folderPath;
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

        private bool _resetEnabled;
        public bool ResetEnabled
        {
            get { return _resetEnabled; }
            set
            {
                SetProperty(ref _resetEnabled, value, () => ResetEnabled);
            }
        }

        #endregion PROPERTIES

        private async void DrawContent()
        {
            try
            {
                string pct = "";
                string fp = _folderPath;

                StringBuilder message = new StringBuilder();

                if (this.ShowFolderContent)
                {
                    pct = "DIR";

                    // Validate the Project Folder

                    if (!Directory.Exists(fp))
                        return;

                    // Enter top-level info
                    message.AppendLine("PRZ PROJECT FOLDER");
                    message.AppendLine("*********************");
                    message.AppendLine("PATH: " + fp);
                    message.AppendLine();

                    // List Directories
                    string[] subdirs = Directory.GetDirectories(fp);

                    message.AppendLine("SUBFOLDERS");
                    message.AppendLine("**********");

                    if (subdirs.Length == 0)
                    {
                        message.AppendLine(" > No Subfolders Found");
                        message.AppendLine();
                    }
                    else
                    {
                        foreach (string dir in subdirs)
                        {
                            message.AppendLine(" > " + dir);
                        }

                        message.AppendLine();
                    }

                    // List Files
                    string[] files = Directory.GetFiles(fp);

                    message.AppendLine("FILES");
                    message.AppendLine("*****");

                    if (files.Length == 0)
                    {
                        message.AppendLine(" > No Files Found");
                        message.AppendLine();
                    }
                    else
                    {
                        foreach (string file in files)
                        {
                            message.AppendLine(" > " + file);
                        }

                        message.AppendLine();
                    }

                    // INPUT Folder Contents, if present
                    string inputdir = System.IO.Path.Combine(fp, "INPUT");

                    if (Directory.Exists(inputdir))
                    {
                        message.AppendLine("INPUT FOLDER FILES");
                        message.AppendLine("******************");
                        string[] inputfiles = Directory.GetFiles(inputdir);

                        if (inputfiles.Length == 0)
                        {
                            message.AppendLine(" > No Files Found");
                            message.AppendLine();
                        }
                        else
                        {
                            foreach (string f in inputfiles)
                            {
                                message.AppendLine(" > " + f);
                            }

                            message.AppendLine();
                        }
                    }

                    // OUTPUT Folder Contents, if present
                    string outputdir = System.IO.Path.Combine(fp, "OUTPUT");

                    if (Directory.Exists(outputdir))
                    {
                        message.AppendLine("OUTPUT FOLDER FILES");
                        message.AppendLine("*******************");

                        string[] outputfiles = Directory.GetFiles(outputdir);

                        if (outputfiles.Length == 0)
                        {
                            message.AppendLine(" > No Files Found");
                            message.AppendLine();
                        }
                        else
                        {
                            foreach (string f in outputfiles)
                            {
                                message.AppendLine(" > " + f);
                            }

                            message.AppendLine();
                        }
                    }
                }
                else if (this.ShowGDBContent)
                {
                    pct = "GDB";

                    string gdbpath = Path.Combine(fp, "PRZ.gdb");

                    Geodatabase fgdb = null;
                    Uri uri = new Uri(gdbpath);
                    FileGeodatabaseConnectionPath cp = new FileGeodatabaseConnectionPath(uri);

                    bool worked = false;
                    try
                    {
                        await QueuedTask.Run(() =>
                        {
                            fgdb = new Geodatabase(cp);
                            worked = true;
                        });

                    }
                    catch (GeodatabaseNotFoundOrOpenedException ex)
                    {
                        message.AppendLine(ex.Message);
                        return;
                    }

                    // Enter top-level info
                    message.AppendLine("PRZ FILE GEODATABASE WORKSPACE");
                    message.AppendLine("*********************************");
                    message.AppendLine("PATH:    " + gdbpath);

                    List<string> LIST_FC = new List<string>();
                    List<string> LIST_TAB = new List<string>();

                    await QueuedTask.Run(() =>
                    {
                        IReadOnlyList<FeatureClassDefinition> fc = fgdb.GetDefinitions<FeatureClassDefinition>();
                        foreach (var def in fc)
                        {
                            LIST_FC.Add(def.GetName());
                        }

                        IReadOnlyList<TableDefinition> tab = fgdb.GetDefinitions<TableDefinition>();
                        foreach (var def in tab)
                        {
                            LIST_TAB.Add(def.GetName());
                        }

                        fgdb.Dispose();
                    });

                    // List Feature Classes
                    message.AppendLine("FEATURE CLASSES");
                    message.AppendLine("***************");
                    LIST_FC.Sort();
                    foreach (var fc in LIST_FC)
                    {
                        message.AppendLine(" > " + fc);
                    }
                    message.AppendLine();

                    // List Tables
                    message.AppendLine("TABLES");
                    message.AppendLine("******");
                    LIST_TAB.Sort();
                    foreach (var tab in LIST_TAB)
                    {
                        message.AppendLine(" > " + tab);
                    }
                    message.AppendLine();

                }
                else if (this.ShowLogContent)
                {
                    pct = "LOG";
                    message.AppendLine("loggy");
                }
                else
                {
                    message.AppendLine("what?");
                }

                this.ContentListing = message.ToString();
                Properties.Settings.Default.WORKSPACE_DISPLAY_MODE = pct;
                Properties.Settings.Default.Save();

            }
            catch (Exception ex)
            {
                this.ContentListing = ex.Message;
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
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
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
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
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
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

                string pct = Properties.Settings.Default.WORKSPACE_DISPLAY_MODE;
                switch (pct)
                {
                    case "DIR":
                        this.ShowFolderContent = true;
                        break;
                    case "GDB":
                        this.ShowGDBContent = true;
                        break;
                    case "LOG":
                        this.ShowLogContent = true;
                        break;
                }

                string pf = Properties.Settings.Default.WORKSPACE_PATH;
                bool ok = Directory.Exists(pf);

                this.ResetEnabled = ok;
                this.InitializeEnabled = ok;

                this.FolderStatus = await GetFolderStatus(pf);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> ResetFolder()
        {
            try
            {
                // This method does the following tasks:
                // 1. If INPUT folder not found, add it.  Else, empty it
                // 2. If OUTPUT folder not found, add it.  Else, empty it
                // 3. If PRZ.log file not found, add it.  Else, do nothing
                // 4. If PRZ.gdb geodatabase not found, add it.  Else, do nothing
                // 5. Add line in PRZ.log file saying we just reset the folder
                // 6. somehow trigger the ContentListing value recalculation

                // This method will use the value currently in _folderPath field

                // Ensure that main Folder Path exists
                if (!Directory.Exists(_folderPath))
                {
                    MessageBox.Show("Please select a valid Project Folder" + Environment.NewLine + Environment.NewLine + _folderPath + " does not exist");
                    return false;
                }

                string pathInputFolder = Path.Combine(_folderPath, "INPUT");
                string pathOutputFolder = Path.Combine(_folderPath, "OUTPUT");
                string pathLogFile = Path.Combine(_folderPath, "PRZ.log");

                if (Directory.Exists(pathInputFolder))
                {
                    // delete the directory and all its contents
                    Directory.Delete(pathInputFolder, true);
                }

                // create a new directory
                Directory.CreateDirectory(pathInputFolder);

                // Next, remove and replace the OUTPUT directory
                if (Directory.Exists(pathOutputFolder))
                {
                    // delete the directory and all its contents
                    Directory.Delete(pathOutputFolder, true);
                }

                // attempt to create a new directory
                Directory.CreateDirectory(pathOutputFolder);

                if (!File.Exists(pathLogFile))
                {
                    PRZH.WriteLog("Log File Created");
                    PRZH.WriteLog("Project Folder Reset");
                }
                else
                {
                    PRZH.WriteLog("Project Folder Reset");
                }

                // Create the File Geodatabase if missing
                string pathGDB = Path.Combine(_folderPath, "PRZ.gdb");

                Geodatabase fgdb;
                Uri uri = new Uri(pathGDB);
                FileGeodatabaseConnectionPath gdbpath = new FileGeodatabaseConnectionPath(uri);

                bool worked = false;
                try
                {
                    await QueuedTask.Run(() =>
                    {
                        fgdb = new Geodatabase(gdbpath);
                        worked = true;
                        fgdb.Dispose();
                    });

                }
                catch (GeodatabaseNotFoundOrOpenedException) { }

                if (!worked)
                {
                    await QueuedTask.Run(() =>
                    {
                        fgdb = SchemaBuilder.CreateGeodatabase(gdbpath);
                        fgdb.Dispose();
                    });
                }

                MessageBox.Show("Refresh Succeeded");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private async Task<bool> InitializeFolder()
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

                // Ensure that main Folder Path exists
                if (!Directory.Exists(_folderPath))
                {
                    MessageBox.Show("Please select a valid Project Folder" + Environment.NewLine + Environment.NewLine + _folderPath + " does not exist");
                    return false;
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
                {
                    PRZH.WriteLog("Log File Created");
                    PRZH.WriteLog("Project Folder Initialized");
                }
                else
                {
                    PRZH.WriteLog("Project Folder Initialized");
                }

                // Create the File Geodatabase if missing
                string pathGDB = Path.Combine(_folderPath, "PRZ.gdb");

                Geodatabase fgdb;
                Uri uri = new Uri(pathGDB);
                FileGeodatabaseConnectionPath gdbpath = new FileGeodatabaseConnectionPath(uri);

                bool worked = false;
                try
                {
                    await QueuedTask.Run(() =>
                    {
                        fgdb = new Geodatabase(gdbpath);
                        worked = true;
                        fgdb.Dispose();
                    });

                }
                catch (GeodatabaseNotFoundOrOpenedException){}

                if (!worked)
                {
                    await QueuedTask.Run(() =>
                    {
                        fgdb = SchemaBuilder.CreateGeodatabase(gdbpath);
                        fgdb.Dispose();
                    });
                }

                MessageBox.Show("Initialize Succeeded");
                return true;

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private void OpenProjectFolder()
        {
            try
            {
                string fp = _folderPath;

                if (!Directory.Exists(fp))
                {
                    MessageBox.Show("Please select a valid Project Folder" + Environment.NewLine + Environment.NewLine + fp + " does not exist");
                    return;
                }

                if (!fp.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    fp = fp + Path.DirectorySeparatorChar.ToString();
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = fp;
                psi.UseShellExecute = true;
                psi.Verb = "open";
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
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
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
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


    }

}



















