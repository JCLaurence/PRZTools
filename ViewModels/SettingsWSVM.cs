using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
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
        public ICommand CmdOpenCurrentWorkspace => _cmdOpenCurrentWorkspace ?? (_cmdOpenCurrentWorkspace = new RelayCommand(() => OpenCurrentWorkspaceInExplorer(), () => true));



        private ICommand _cmdDisplayFolderContent;
        public ICommand CmdDisplayFolderContent => _cmdDisplayFolderContent ?? (_cmdDisplayFolderContent = new RelayCommand(async () => await DisplayContent(WorkspaceDisplayMode.DIR), () => true));

        private ICommand _cmdDisplayGDBContent;
        public ICommand CmdDisplayGDBContent => _cmdDisplayGDBContent ?? (_cmdDisplayGDBContent = new RelayCommand(async () => await DisplayContent(WorkspaceDisplayMode.GDB), () => true));

        private ICommand _cmdDisplayLogContent;
        public ICommand CmdDisplayLogContent => _cmdDisplayLogContent ?? (_cmdDisplayLogContent = new RelayCommand(async () => await DisplayContent(WorkspaceDisplayMode.LOG), () => true));

        #endregion

        #region Methods

        public async Task OnProWinLoaded()
        {
            try
            {
                string wdm = Properties.Settings.Default.WORKSPACE_DISPLAY_MODE;

                // enable the correct radio button
                if (wdm == WorkspaceDisplayMode.DIR.ToString())
                {
                    FolderChecked = true;
                    await DisplayContent(WorkspaceDisplayMode.DIR);
                }
                else if (wdm == WorkspaceDisplayMode.GDB.ToString())
                {
                    GeodatabaseChecked = true;
                    await DisplayContent(WorkspaceDisplayMode.GDB);
                }
                else if (wdm == WorkspaceDisplayMode.LOG.ToString())
                {
                    LogChecked = true;
                    await DisplayContent(WorkspaceDisplayMode.LOG);
                }
                else
                {
                    FolderChecked = true;
                    await DisplayContent(WorkspaceDisplayMode.DIR);
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

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
                // Validate the current Project Workspace
                if (!Directory.Exists(CurrentWorkspacePath))
                {
                    ProMsgBox.Show("Please select a valid Project Folder" + Environment.NewLine + Environment.NewLine + CurrentWorkspacePath + " does not exist");
                    return false;
                }

                // Prompt User for permission to proceed
                if (ProMsgBox.Show("Proper Prompt Here" + Environment.NewLine + Environment.NewLine +
                   "Do you wish to proceed?" +
                   Environment.NewLine + Environment.NewLine +
                   "Choose wisely...",
                   "INITIALIZE PROJECT WORKSPACE",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    return false;
                }

                #region SUBDIRECTORIES

                // INPUT
                string input_folder = PRZH.GetPath_InputFolder();
                if (!Directory.Exists(input_folder))
                {
                    Directory.CreateDirectory(input_folder);
                }

                // OUTPUT
                string output_folder = PRZH.GetPath_OutputFolder();
                if (!Directory.Exists(output_folder))
                {
                    Directory.CreateDirectory(output_folder);
                }

                // EXPORT WTW
                string wtw_folder = PRZH.GetPath_ExportWTWFolder();
                if (!Directory.Exists(wtw_folder))
                {
                    Directory.CreateDirectory(wtw_folder);
                }

                #endregion

                #region LOG FILE

                string logpath = PRZH.GetPath_ProjectLog();

                if (!File.Exists(logpath))
                {
                    PRZH.WriteLog("Log File Created");
                }
                else
                {
                    PRZH.WriteLog("Project Folder Initialized");
                }

                #endregion

                #region GEODATABASE

                string gdbpath = PRZH.GetPath_ProjectGDB();
                bool gdbexists = await PRZH.ProjectGDBExists();
                bool gdbfolderexists = Directory.Exists(gdbpath);

                if (!gdbexists)
                {
                    if (gdbfolderexists)
                    {
                        try
                        {
                            Directory.Delete(gdbpath, true);
                        }
                        catch (Exception ex)
                        {
                            ProMsgBox.Show($"Unable to delete the geodatabase folder at {gdbpath}" + Environment.NewLine + Environment.NewLine + ex.Message);
                            return false;
                        }
                    }

                    // create the geodatabase
                    Uri uri = new Uri(gdbpath);
                    FileGeodatabaseConnectionPath fgcp = new FileGeodatabaseConnectionPath(uri);
                    try
                    {
                        using (Geodatabase gdb = SchemaBuilder.CreateGeodatabase(fgcp)) { }
                    }
                    catch (Exception ex)
                    {
                        ProMsgBox.Show("Unable to create the geodatabase..." + Environment.NewLine + Environment.NewLine + ex.Message);
                        return false;
                    }
                }

                #endregion

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

                ProMsgBox.Show("Workspace has been initialized");

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
                // Validate the current Project Workspace
                if (!Directory.Exists(CurrentWorkspacePath))
                {
                    ProMsgBox.Show("Please select a valid Project Folder" + Environment.NewLine + Environment.NewLine + CurrentWorkspacePath + " does not exist");
                    return false;
                }

                // Prompt User for permission to proceed
                if (ProMsgBox.Show("Suitable Prompt goes here" + Environment.NewLine + Environment.NewLine +
                   "Do you wish to proceed?" +
                   Environment.NewLine + Environment.NewLine +
                   "Choose wisely...",
                   "RESET PROJECT WORKSPACE",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    return false;
                }

                #region SUBDIRECTORIES

                // INPUT
                string input_folder = PRZH.GetPath_InputFolder();
                if (Directory.Exists(input_folder))
                {
                    Directory.Delete(input_folder, true);
                }

                Directory.CreateDirectory(input_folder);


                // OUTPUT
                string output_folder = PRZH.GetPath_OutputFolder();
                if (Directory.Exists(output_folder))
                {
                    Directory.Delete(output_folder, true);
                }

                Directory.CreateDirectory(output_folder);

                // EXPORT WTW
                string wtw_folder = PRZH.GetPath_ExportWTWFolder();
                if (Directory.Exists(wtw_folder))
                {
                    Directory.Delete(wtw_folder, true);
                }

                Directory.CreateDirectory(wtw_folder);

                #endregion

                #region LOG FILE

                // Reset Log File
                PRZH.WriteLog("Log File Created.", LogMessageType.INFO, false);

                #endregion

                #region GEODATABASE

                string gdbpath = PRZH.GetPath_ProjectGDB();
                bool gdbexists = await PRZH.ProjectGDBExists();
                bool gdbfolderexists = Directory.Exists(gdbpath);

                // Create the Geodatabase
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                if (gdbexists)  // geodatabase exists
                {
                    // delete the geodatabase
                    toolParams = Geoprocessing.MakeValueArray(gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, null, toolFlags);
                    if (toolOutput == null)
                    {
                        ProMsgBox.Show("Unable to delete the PRZ geodatabase...");
                        return false;
                    }
                }
                else if (gdbfolderexists)   // folder exists but it is not a valid geodatabase
                {
                    try
                    {
                        Directory.Delete(gdbpath, true);
                    }
                    catch (Exception ex)
                    {
                        ProMsgBox.Show($"Unable to delete the {gdbpath} directory..." + Environment.NewLine + Environment.NewLine + ex.Message);
                        return false;
                    }
                }

                // Create the Geodatabase
                Uri uri = new Uri(gdbpath);
                FileGeodatabaseConnectionPath fgcp = new FileGeodatabaseConnectionPath(uri);
                try
                {
                    using (Geodatabase gdb = SchemaBuilder.CreateGeodatabase(fgcp)) { }
                }
                catch (Exception ex)
                {
                    ProMsgBox.Show("Unable to create the geodatabase..." + Environment.NewLine + Environment.NewLine + ex.Message);
                    return false;
                }

                #endregion

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

                ProMsgBox.Show("Workspace has been reset");

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private void OpenCurrentWorkspaceInExplorer()
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
                    string inputdir = PRZH.GetPath_InputFolder();

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
                    string outputdir = PRZH.GetPath_OutputFolder();

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

                    // EXPORT WTW Folder Contents, if present
                    string wtwdir = PRZH.GetPath_ExportWTWFolder();

                    if (Directory.Exists(wtwdir))
                    {
                        contents.AppendLine("EXPORT_WTW FOLDER FILES");
                        contents.AppendLine("***********************");

                        string[] wtwfiles = Directory.GetFiles(wtwdir);

                        if (wtwfiles.Length == 0)
                        {
                            contents.AppendLine(" > No Files Found");
                            contents.AppendLine();
                        }
                        else
                        {
                            foreach (string f in wtwfiles)
                            {
                                contents.AppendLine(" > " + f);
                            }

                            contents.AppendLine();
                        }
                    }
                }
                else if (displayMode == WorkspaceDisplayMode.GDB)
                {
                    string gdbpath = PRZH.GetPath_ProjectGDB();
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
                        contents.AppendLine("Project Log File not found at path: " + PRZH.GetPath_ProjectLog());
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


        #endregion


    }
}