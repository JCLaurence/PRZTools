using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
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
using MsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZH = NCC.PRZTools.PRZHelper;
using PRZC = NCC.PRZTools.PRZConstants;

namespace NCC.PRZTools
{
    public class WorkspaceSettingsVM : PropertyChangedBase
    {

        public WorkspaceSettingsVM()
        {
        }

        #region Properties

        private string _currentWorkspacePath = Properties.Settings.Default.WORKSPACE_PATH;
        public string CurrentWorkspacePath
        {
            get { return _currentWorkspacePath; }
            set
            {
                SetProperty(ref _currentWorkspacePath, value, () => CurrentWorkspacePath);
                Properties.Settings.Default.WORKSPACE_PATH = _currentWorkspacePath;
                Properties.Settings.Default.Save();
            }
        }

        private string _workspaceContents;
        public string WorkspaceContents
        {
            get { return _workspaceContents; }
            set
            {
                SetProperty(ref _workspaceContents, value, () => WorkspaceContents);
            }
        }

        private bool _folderChecked;
        public bool FolderChecked
        {
            get { return _folderChecked; }
            set
            {
                SetProperty(ref _folderChecked, value, () => FolderChecked);
            }
        }

        private bool _geodatabaseChecked;
        public bool GeodatabaseChecked
        {
            get { return _geodatabaseChecked; }
            set
            {
                SetProperty(ref _geodatabaseChecked, value, () => GeodatabaseChecked);
            }
        }

        private bool _logChecked;
        public bool LogChecked
        {
            get { return _logChecked; }
            set
            {
                SetProperty(ref _logChecked, value, () => LogChecked);
            }
        }

        #endregion

        #region Commands

        public ICommand CmdOK => new RelayCommand((paramProWin) =>
        {
            // TODO: set dialog result and close the window
            (paramProWin as ProWindow).DialogResult = true;
            (paramProWin as ProWindow).Close();
        }, () => true);

        public ICommand CmdCancel => new RelayCommand((paramProWin) =>
        {
            // TODO: set dialog result and close the window
            (paramProWin as ProWindow).DialogResult = false;
            (paramProWin as ProWindow).Close();
        }, () => true);


        private ICommand _cmdSelectWorkspaceFolder;
        public ICommand CmdSelectWorkspaceFolder { get { return _cmdSelectWorkspaceFolder ?? (_cmdSelectWorkspaceFolder = new RelayCommand(() => SelectWorkspaceFolder(), () => true)); } }


        private ICommand _cmdInitializeCurrentWorkspace;
        public ICommand CmdInitializeCurrentWorkspace { get { return _cmdInitializeCurrentWorkspace ?? (_cmdInitializeCurrentWorkspace = new RelayCommand(async () => await InitializeCurrentWorkspace(), () => true)); } }


        private ICommand _cmdResetCurrentWorkspace;
        public ICommand CmdResetCurrentWorkspace { get { return _cmdResetCurrentWorkspace ?? (_cmdResetCurrentWorkspace = new RelayCommand(async () => await ResetCurrentWorkspace(), () => true)); } }


        private ICommand _cmdOpenCurrentWorkspace;
        public ICommand CmdOpenCurrentWorkspace { get { return _cmdOpenCurrentWorkspace ?? (_cmdOpenCurrentWorkspace = new RelayCommand(() => OpenCurrentWorkspace(), () => true)); } }


        private ICommand _cmdDisplayContent;
        public ICommand CmdDisplayContent { get { return _cmdDisplayContent ?? (_cmdDisplayContent = new RelayCommand(async (paramType) => await PopulateWorkspaceContents(paramType.ToString()), () => true)); } }

        #endregion

        #region Methods

        private void SelectWorkspaceFolder()
        {
            try
            {
                string initDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (Directory.Exists(this.CurrentWorkspacePath))
                {
                    DirectoryInfo di = new DirectoryInfo(this.CurrentWorkspacePath);
                    DirectoryInfo dip = di.Parent;

                    if (dip != null)
                    {
                        initDir = dip.FullName;
                    }
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
                        this.CurrentWorkspacePath = i.Path;
                    }
                }
            }
            catch (Exception ex)
            {
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> InitializeCurrentWorkspace()
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
                if (!Directory.Exists(_currentWorkspacePath))
                {
                    MsgBox.Show("Please select a valid Workspace Folder" + Environment.NewLine + Environment.NewLine + _currentWorkspacePath + " does not exist");
                    return false;
                }

                // Create the INPUT and OUTPUT directories and the PRZ.log file if missing.
                string pathInputFolder = Path.Combine(_currentWorkspacePath, "INPUT");
                string pathOutputFolder = Path.Combine(_currentWorkspacePath, "OUTPUT");
                string pathLogFile = Path.Combine(_currentWorkspacePath, PRZC.c_PRZ_LOGFILE);

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
                string pathGDB = Path.Combine(_currentWorkspacePath, PRZC.c_PRZ_PROJECT_FGDB);

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

                MsgBox.Show("Initialize Succeeded");
                return true;

            }
            catch (Exception ex)
            {
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private async Task<bool> ResetCurrentWorkspace()
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
                if (!Directory.Exists(_currentWorkspacePath))
                {
                    MsgBox.Show("Please select a valid Project Folder" + Environment.NewLine + Environment.NewLine + _currentWorkspacePath + " does not exist");
                    return false;
                }

                string pathInputFolder = Path.Combine(_currentWorkspacePath, "INPUT");
                string pathOutputFolder = Path.Combine(_currentWorkspacePath, "OUTPUT");
                string pathLogFile = Path.Combine(_currentWorkspacePath, PRZC.c_PRZ_LOGFILE);

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
                string pathGDB = Path.Combine(_currentWorkspacePath, PRZC.c_PRZ_PROJECT_FGDB);

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

                MsgBox.Show("Refresh Succeeded");
                return true;
            }
            catch (Exception ex)
            {
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private void OpenCurrentWorkspace()
        {
            try
            {
                string fp = _currentWorkspacePath;

                if (!Directory.Exists(fp))
                {
                    MsgBox.Show("Invalid Workspace Path" + Environment.NewLine + Environment.NewLine + fp + " does not exist");
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
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task PopulateWorkspaceContents(string WSType)
        {
            try
            {
                string fp = _currentWorkspacePath;

                StringBuilder contents = new StringBuilder();

                if (WSType == WorkspaceDisplayMode.DIR.ToString())
                {
                    // Validate the Project Folder

                    if (!Directory.Exists(fp))
                        return;

                    // Enter top-level info
                    contents.AppendLine("PRZ PROJECT FOLDER");
                    contents.AppendLine("*********************");
                    contents.AppendLine("PATH: " + fp);
                    contents.AppendLine();

                    // List Directories
                    string[] subdirs = Directory.GetDirectories(fp);

                    contents.AppendLine("SUBFOLDERS");
                    contents.AppendLine("**********");

                    if (subdirs.Length == 0)
                    {
                        contents.AppendLine(" > No Subfolders Found");
                        contents.AppendLine();
                    }
                    else
                    {
                        foreach (string dir in subdirs)
                        {
                            contents.AppendLine(" > " + dir);
                        }

                        contents.AppendLine();
                    }

                    // List Files
                    string[] files = Directory.GetFiles(fp);

                    contents.AppendLine("FILES");
                    contents.AppendLine("*****");

                    if (files.Length == 0)
                    {
                        contents.AppendLine(" > No Files Found");
                        contents.AppendLine();
                    }
                    else
                    {
                        foreach (string file in files)
                        {
                            contents.AppendLine(" > " + file);
                        }

                        contents.AppendLine();
                    }

                    // INPUT Folder Contents, if present
                    string inputdir = System.IO.Path.Combine(fp, "INPUT");

                    if (Directory.Exists(inputdir))
                    {
                        contents.AppendLine("INPUT FOLDER FILES");
                        contents.AppendLine("******************");
                        string[] inputfiles = Directory.GetFiles(inputdir);

                        if (inputfiles.Length == 0)
                        {
                            contents.AppendLine(" > No Files Found");
                            contents.AppendLine();
                        }
                        else
                        {
                            foreach (string f in inputfiles)
                            {
                                contents.AppendLine(" > " + f);
                            }

                            contents.AppendLine();
                        }
                    }

                    // OUTPUT Folder Contents, if present
                    string outputdir = System.IO.Path.Combine(fp, "OUTPUT");

                    if (Directory.Exists(outputdir))
                    {
                        contents.AppendLine("OUTPUT FOLDER FILES");
                        contents.AppendLine("*******************");

                        string[] outputfiles = Directory.GetFiles(outputdir);

                        if (outputfiles.Length == 0)
                        {
                            contents.AppendLine(" > No Files Found");
                            contents.AppendLine();
                        }
                        else
                        {
                            foreach (string f in outputfiles)
                            {
                                contents.AppendLine(" > " + f);
                            }

                            contents.AppendLine();
                        }
                    }
                }
                else if (WSType == WorkspaceDisplayMode.GDB.ToString())
                {
                    string gdbpath = Path.Combine(fp, PRZC.c_PRZ_PROJECT_FGDB);

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
                        contents.AppendLine(ex.Message);
                        return;
                    }

                    // Enter top-level info
                    contents.AppendLine("PRZ FILE GEODATABASE WORKSPACE");
                    contents.AppendLine("*********************************");
                    contents.AppendLine("PATH:    " + gdbpath);

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
                    contents.AppendLine("FEATURE CLASSES");
                    contents.AppendLine("***************");
                    LIST_FC.Sort();
                    foreach (var fc in LIST_FC)
                    {
                        contents.AppendLine(" > " + fc);
                    }
                    contents.AppendLine();

                    // List Tables
                    contents.AppendLine("TABLES");
                    contents.AppendLine("******");
                    LIST_TAB.Sort();
                    foreach (var tab in LIST_TAB)
                    {
                        contents.AppendLine(" > " + tab);
                    }
                    contents.AppendLine();

                }
                else if (WSType == WorkspaceDisplayMode.LOG.ToString())
                {
                    contents.AppendLine(PRZH.ReadLog());
                }
                else
                {
                    contents.AppendLine("what?");
                }

                this.WorkspaceContents = contents.ToString();
                Properties.Settings.Default.WORKSPACE_DISPLAY_MODE = WSType;
                Properties.Settings.Default.Save();

            }
            catch (Exception ex)
            {
                this.WorkspaceContents = ex.Message;
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        /// <summary>
        /// This method runs when the ProWindow Loaded event occurs.
        /// </summary>
        internal async void OnProWinLoaded()
        {
            try
            {
                string wdm = Properties.Settings.Default.WORKSPACE_DISPLAY_MODE;

                // enable the correct radio button
                if (wdm == WorkspaceDisplayMode.DIR.ToString())
                {
                    this.FolderChecked = true;
                }
                else if (wdm == WorkspaceDisplayMode.GDB.ToString())
                {
                    this.GeodatabaseChecked = true;
                }
                else if (wdm == WorkspaceDisplayMode.LOG.ToString())
                {
                    this.LogChecked = true;
                }
                else
                {

                }

                await PopulateWorkspaceContents(wdm);
            }
            catch (Exception ex)
            {
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        #endregion


    }
}