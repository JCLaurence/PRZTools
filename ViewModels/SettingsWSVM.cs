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
using System.Windows.Input;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZC = NCC.PRZTools.PRZConstants;
using PRZH = NCC.PRZTools.PRZHelper;

namespace NCC.PRZTools
{
    public class SettingsWSVM : PropertyChangedBase
    {
        public SettingsWSVM()
        {
        }

        #region Fields

        #endregion

        #region Properties

        private string _currentWorkspacePath = Properties.Settings.Default.WORKSPACE_PATH;
        public string CurrentWorkspacePath
        {
            get => _currentWorkspacePath;
            set
            {
                SetProperty(ref _currentWorkspacePath, value, () => CurrentWorkspacePath);
                Properties.Settings.Default.WORKSPACE_PATH = value;
                Properties.Settings.Default.Save();
            }
        }

        private string _workspaceContents;
        public string WorkspaceContents
        {
            get => _workspaceContents;
            set => SetProperty(ref _workspaceContents, value, () => WorkspaceContents);
        }

        private bool _folderChecked;
        public bool FolderChecked
        {
            get => _folderChecked;
            set => SetProperty(ref _folderChecked, value, () => FolderChecked);
        }

        private bool _geodatabaseChecked;
        public bool GeodatabaseChecked
        {
            get => _geodatabaseChecked;
            set => SetProperty(ref _geodatabaseChecked, value, () => GeodatabaseChecked);
        }

        private bool _logChecked;
        public bool LogChecked
        {
            get => _logChecked;
            set => SetProperty(ref _logChecked, value, () => LogChecked);
        }

        #endregion

        #region Commands

        private ICommand _cmdSelectWorkspaceFolder;
        public ICommand CmdSelectWorkspaceFolder => _cmdSelectWorkspaceFolder ?? (_cmdSelectWorkspaceFolder = new RelayCommand(() => SelectWorkspaceFolder(), () => true));


        private ICommand _cmdInitializeCurrentWorkspace;
        public ICommand CmdInitializeCurrentWorkspace => _cmdInitializeCurrentWorkspace ?? (_cmdInitializeCurrentWorkspace = new RelayCommand(async () => await InitializeCurrentWorkspace(), () => true));


        private ICommand _cmdResetCurrentWorkspace;
        public ICommand CmdResetCurrentWorkspace => _cmdResetCurrentWorkspace ?? (_cmdResetCurrentWorkspace = new RelayCommand(async () => await ResetCurrentWorkspace(), () => true));


        private ICommand _cmdOpenCurrentWorkspace;
        public ICommand CmdOpenCurrentWorkspace => _cmdOpenCurrentWorkspace ?? (_cmdOpenCurrentWorkspace = new RelayCommand(() => OpenCurrentWorkspace(), () => true));



        private ICommand _cmdDisplayFolderContent;
        public ICommand CmdDisplayFolderContent => _cmdDisplayFolderContent ?? (_cmdDisplayFolderContent = new RelayCommand(async () => await DisplayContent(WorkspaceDisplayMode.DIR), () => true));

        private ICommand _cmdDisplayGDBContent;
        public ICommand CmdDisplayGDBContent => _cmdDisplayGDBContent ?? (_cmdDisplayGDBContent = new RelayCommand(async () => await DisplayContent(WorkspaceDisplayMode.GDB), () => true));

        private ICommand _cmdDisplayLogContent;
        public ICommand CmdDisplayLogContent => _cmdDisplayLogContent ?? (_cmdDisplayLogContent = new RelayCommand(async () => await DisplayContent(WorkspaceDisplayMode.LOG), () => true));

        #endregion

        #region Methods

        private void SelectWorkspaceFolder()
        {
            try
            {
                string initDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (Directory.Exists(this.CurrentWorkspacePath))
                {
                    DirectoryInfo di = new DirectoryInfo(CurrentWorkspacePath);
                    DirectoryInfo dip = di.Parent;

                    if (dip != null)
                    {
                        initDir = dip.FullName;
                    }
                }

                OpenItemDialog dlg = new OpenItemDialog
                {
                    Title = "Specify a Project Workspace folder",
                    InitialLocation = initDir,
                    MultiSelect = false,
                    AlwaysUseInitialLocation = true,
                    Filter = ItemFilters.folders
                };

                bool? result = dlg.ShowDialog();

                if (!result.HasValue || !result.Value || dlg.Items.Count() == 0)
                {
                    return;
                }

                Item item = dlg.Items.First();

                if (item == null)
                {
                    return;
                }

                CurrentWorkspacePath = item.Path;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
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
                // 6. Trigger the ContentListing value recalculation

                // Ensure that main Folder Path exists
                if (!Directory.Exists(CurrentWorkspacePath))
                {
                    ProMsgBox.Show("Please select a valid Workspace Folder" + Environment.NewLine + Environment.NewLine + CurrentWorkspacePath + " does not exist");
                    return false;
                }

                // Validation: Prompt User for permission to proceed
                if (ProMsgBox.Show("If you proceed, bla bla bla" + Environment.NewLine + Environment.NewLine +
                   "Do you wish to proceed?" +
                   Environment.NewLine + Environment.NewLine +
                   "Choose wisely...",
                   "INITIALIZE PROJECT WORKSPACE",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    return false;
                }

                // Create the INPUT and OUTPUT directories and the PRZ.log file if missing.
                string pathInputFolder = Path.Combine(CurrentWorkspacePath, "INPUT");
                string pathOutputFolder = Path.Combine(CurrentWorkspacePath, "OUTPUT");
                string pathLogFile = Path.Combine(CurrentWorkspacePath, PRZC.c_PRZ_LOGFILE);

                if (!Directory.Exists(pathInputFolder))
                {
                    Directory.CreateDirectory(pathInputFolder);
                }

                if (!Directory.Exists(pathOutputFolder))
                {
                    Directory.CreateDirectory(pathOutputFolder);
                }

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
                string pathGDB = Path.Combine(CurrentWorkspacePath, PRZC.c_PRZ_PROJECT_FGDB);
                Uri uri = new Uri(pathGDB);
                FileGeodatabaseConnectionPath gdbpath = new FileGeodatabaseConnectionPath(uri);

                bool gdbExists = false;

                try
                {
                    await QueuedTask.Run(() =>
                    {
                        using (Geodatabase gdb = new Geodatabase(gdbpath))
                        {
                            gdbExists = true;
                        }
                    });

                }
                catch (GeodatabaseNotFoundOrOpenedException) { }

                if (!gdbExists)
                {
                    await QueuedTask.Run(() =>
                    {
                        using (Geodatabase gdb = SchemaBuilder.CreateGeodatabase(gdbpath)) {}
                    });
                }

                // Refresh the Display
                if (FolderChecked)
                {
                    await DisplayContent(WorkspaceDisplayMode.DIR);
                }
                else if (GeodatabaseChecked)
                {
                    await DisplayContent(WorkspaceDisplayMode.GDB);
                }
                else if (LogChecked)
                {
                    await DisplayContent(WorkspaceDisplayMode.LOG);
                }

                ProMsgBox.Show("Workspace Initialization Succeeded");

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
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

                if (!Directory.Exists(CurrentWorkspacePath))
                {
                    ProMsgBox.Show("Please select a valid Project Folder" + Environment.NewLine + Environment.NewLine + CurrentWorkspacePath + " does not exist");
                    return false;
                }

                // Validation: Prompt User for permission to proceed
                if (ProMsgBox.Show("If you proceed, bla bla bla" + Environment.NewLine + Environment.NewLine +
                   "Do you wish to proceed?" +
                   Environment.NewLine + Environment.NewLine +
                   "Choose wisely...",
                   "RESET PROJECT WORKSPACE",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    return false;
                }

                string pathInputFolder = Path.Combine(CurrentWorkspacePath, "INPUT");
                string pathOutputFolder = Path.Combine(CurrentWorkspacePath, "OUTPUT");
                string pathLogFile = Path.Combine(CurrentWorkspacePath, PRZC.c_PRZ_LOGFILE);

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
                string pathGDB = Path.Combine(CurrentWorkspacePath, PRZC.c_PRZ_PROJECT_FGDB);
                Uri uri = new Uri(pathGDB);
                FileGeodatabaseConnectionPath gdbpath = new FileGeodatabaseConnectionPath(uri);

                bool gdbExists = false;

                try
                {
                    await QueuedTask.Run(() =>
                    {
                        using (Geodatabase gdb = new Geodatabase(gdbpath))
                        {
                            gdbExists = true;
                        }
                    });

                }
                catch (GeodatabaseNotFoundOrOpenedException) { }

                if (!gdbExists)
                {
                    await QueuedTask.Run(() =>
                    {
                        using (Geodatabase gdb = SchemaBuilder.CreateGeodatabase(gdbpath)) { }
                    });
                }

                // Refresh the Display
                if (FolderChecked)
                {
                    await DisplayContent(WorkspaceDisplayMode.DIR);
                }
                else if (GeodatabaseChecked)
                {
                    await DisplayContent(WorkspaceDisplayMode.GDB);
                }
                else if (LogChecked)
                {
                    await DisplayContent(WorkspaceDisplayMode.LOG);
                }

                ProMsgBox.Show("Workspace Reset Succeeded");

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private void OpenCurrentWorkspace()
        {
            try
            {
                string fp = CurrentWorkspacePath;

                if (!Directory.Exists(fp))
                {
                    ProMsgBox.Show("Invalid Workspace Path" + Environment.NewLine + Environment.NewLine + fp + " does not exist");
                    return;
                }

                if (!fp.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    fp += Path.DirectorySeparatorChar.ToString();
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = fp,
                    UseShellExecute = true,
                    Verb = "open"
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task DisplayContent(WorkspaceDisplayMode displayMode)
        {
            StringBuilder contents = new StringBuilder("");

            try
            {
                // Save the latest display type
                Properties.Settings.Default.WORKSPACE_DISPLAY_MODE = displayMode.ToString();
                Properties.Settings.Default.Save();

                string wspath = CurrentWorkspacePath;

                if (displayMode == WorkspaceDisplayMode.DIR)
                {
                    // Validate the Project Folder
                    if (!Directory.Exists(wspath))
                    {
                        contents.AppendLine($"Directory does not exist: {wspath}");
                        this.WorkspaceContents = contents.ToString();
                        return;
                    }

                    // Enter top-level info
                    contents.AppendLine("PRZ PROJECT FOLDER");
                    contents.AppendLine("******************");
                    contents.AppendLine("PATH: " + wspath);
                    contents.AppendLine();

                    // List Directories
                    string[] subdirs = Directory.GetDirectories(wspath);

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
                    string[] files = Directory.GetFiles(wspath);

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
                    string inputdir = System.IO.Path.Combine(wspath, "INPUT");

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
                    string outputdir = System.IO.Path.Combine(wspath, "OUTPUT");

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
                else if (displayMode == WorkspaceDisplayMode.GDB)
                {
                    string gdbpath = PRZH.GetProjectGDBPath();
                    bool gdbExists = await PRZH.ProjectGDBExists();

                    if (!gdbExists)
                    {
                        contents.AppendLine($"Workspace Geodatabase does not exist at path: {gdbpath}");
                        this.WorkspaceContents = contents.ToString();
                        return;
                    }

                    // Enter top-level info
                    contents.AppendLine("PRZ FILE GEODATABASE WORKSPACE");
                    contents.AppendLine("*********************************");
                    contents.AppendLine("PATH:    " + gdbpath);
                    contents.AppendLine();

                    List<string> FCNames = new List<string>();
                    List<string> TableNames = new List<string>();

                    try
                    {
                        await QueuedTask.Run(async () =>
                        {
                            using (Geodatabase gdb = await PRZH.GetProjectGDB())
                            {
                                IReadOnlyList<FeatureClassDefinition> fcDefs = gdb.GetDefinitions<FeatureClassDefinition>();

                                foreach (var fcDef in fcDefs)
                                {
                                    FCNames.Add(fcDef.GetName());
                                }

                                IReadOnlyList<TableDefinition> tabDefs = gdb.GetDefinitions<TableDefinition>();

                                foreach (var tabDef in tabDefs)
                                {
                                    TableNames.Add(tabDef.GetName());
                                }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        contents.AppendLine(ex.Message);
                        this.WorkspaceContents = contents.ToString();
                        return;
                    }

                    // List Feature Classes
                    contents.AppendLine("FEATURE CLASSES");
                    contents.AppendLine("***************");

                    FCNames.Sort();

                    foreach (string name in FCNames)
                    {
                        contents.AppendLine($" > {name}");
                    }

                    contents.AppendLine();

                    // List Tables
                    contents.AppendLine("TABLES");
                    contents.AppendLine("******");
                    
                    TableNames.Sort();
                    
                    foreach (string name in TableNames)
                    {
                        contents.AppendLine($" > {name}");
                    }

                    contents.AppendLine();
                }
                else if (displayMode == WorkspaceDisplayMode.LOG)
                {
                    if (PRZH.ProjectLogExists())
                    {
                        contents.AppendLine(PRZH.ReadLog());
                    }
                    else
                    {
                        contents.AppendLine("Project Log File not found at path: " + PRZH.GetProjectLogPath());
                    }
                }
                else
                {
                    contents.AppendLine("what?");
                }

                this.WorkspaceContents = contents.ToString();
            }
            catch (Exception ex)
            {
                contents.AppendLine(ex.Message);
                this.WorkspaceContents = contents.ToString();
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        public async Task OnProWinLoaded()
        {
            try
            {
                string wdm = Properties.Settings.Default.WORKSPACE_DISPLAY_MODE;

                // enable the correct radio button
                if (wdm == WorkspaceDisplayMode.DIR.ToString())
                {
                    this.FolderChecked = true;
                    await DisplayContent(WorkspaceDisplayMode.DIR);
                }
                else if (wdm == WorkspaceDisplayMode.GDB.ToString())
                {
                    this.GeodatabaseChecked = true;
                    await DisplayContent(WorkspaceDisplayMode.GDB);
                }
                else if (wdm == WorkspaceDisplayMode.LOG.ToString())
                {
                    this.LogChecked = true;
                    await DisplayContent(WorkspaceDisplayMode.LOG);
                }
                else
                {
                    this.FolderChecked = true;
                    await DisplayContent(WorkspaceDisplayMode.DIR);
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        #endregion


    }
}