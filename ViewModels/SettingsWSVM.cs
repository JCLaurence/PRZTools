using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.Raster;
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
using System.Windows;
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

        #region FIELDS

        // Commands
        private ICommand _cmdSelectFolder;
        private ICommand _cmdSelectNationalDb;
        private ICommand _cmdValidateNationalDb;
        private ICommand _cmdInitializeWorkspace;
        private ICommand _cmdResetWorkspace;
        private ICommand _cmdExploreWorkspace;
        private ICommand _cmdViewLogFile;
        private ICommand _cmdClearLogFile;

        // Other Fields
        private string _prjSettings_Txt_ProjectFolderPath;
        private string _prjSettings_Txt_NationalDbPath;
        private bool _prjSettings_Rad_WSViewer_Dir_IsChecked;
        private bool _prjSettings_Rad_WSViewer_Gdb_IsChecked;
        private string _prjSettings_Txt_WorkspaceContents;

        private Visibility _natDbInfo_Vis_DockPanel = Visibility.Collapsed;
        private string _natDbInfo_Txt_DbName;
        private List<string> _natDbInfo_Cmb_SchemaNames;
        private string _natDbInfo_Cmb_SelectedSchemaName;

        private string _natDBInfo_Img_Status = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";


        #endregion

        #region PROPERTIES

        public string NatDBInfo_Img_Status
        {
            get => _natDBInfo_Img_Status;
            set => SetProperty(ref _natDBInfo_Img_Status, value, () => NatDBInfo_Img_Status);
        }

        public string NatDbInfo_Txt_DbName
        {
            get => _natDbInfo_Txt_DbName;
            set
            {
                SetProperty(ref _natDbInfo_Txt_DbName, value, () => NatDbInfo_Txt_DbName);
                Properties.Settings.Default.NATDB_DBNAME = string.IsNullOrEmpty(value) ? "" : value;
                Properties.Settings.Default.Save();
            }
        }

        public Visibility NatDbInfo_Vis_DockPanel
        {
            get => _natDbInfo_Vis_DockPanel;
            set => SetProperty(ref _natDbInfo_Vis_DockPanel, value, () => NatDbInfo_Vis_DockPanel);
        }

        public List<string> NatDbInfo_Cmb_SchemaNames
        {
            get => _natDbInfo_Cmb_SchemaNames;
            set => SetProperty(ref _natDbInfo_Cmb_SchemaNames, value, () => NatDbInfo_Cmb_SchemaNames);
        }

        public string NatDbInfo_Cmb_SelectedSchemaName
        {
            get => _natDbInfo_Cmb_SelectedSchemaName;
            set
            {
                SetProperty(ref _natDbInfo_Cmb_SelectedSchemaName, value, () => NatDbInfo_Cmb_SelectedSchemaName);
                Properties.Settings.Default.NATDB_SCHEMANAME = string.IsNullOrEmpty(value) ? "" : value;
                Properties.Settings.Default.Save();
            }
        }


        public string PrjSettings_Txt_ProjectFolderPath
        {
            get => _prjSettings_Txt_ProjectFolderPath;
            set
            {
                SetProperty(ref _prjSettings_Txt_ProjectFolderPath, value, () => PrjSettings_Txt_ProjectFolderPath);
                Properties.Settings.Default.WORKSPACE_PATH = value;
                Properties.Settings.Default.Save();
            }
        }

        public string PrjSettings_Txt_NationalDbPath
        {
            get => _prjSettings_Txt_NationalDbPath;
            set
            {
                SetProperty(ref _prjSettings_Txt_NationalDbPath, value, () => PrjSettings_Txt_NationalDbPath);
                Properties.Settings.Default.NATDB_DBPATH = value;
                Properties.Settings.Default.Save();
            }
        }

        public string PrjSettings_Txt_WorkspaceContents
        {
            get => _prjSettings_Txt_WorkspaceContents;
            set => SetProperty(ref _prjSettings_Txt_WorkspaceContents, value, () => PrjSettings_Txt_WorkspaceContents);
        }

        public bool PrjSettings_Rad_WSViewer_Dir_IsChecked
        {
            get => _prjSettings_Rad_WSViewer_Dir_IsChecked;
            set
            {
                SetProperty(ref _prjSettings_Rad_WSViewer_Dir_IsChecked, value, () => PrjSettings_Rad_WSViewer_Dir_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.WORKSPACE_DISPLAY_MODE = (int)WorkspaceDisplayMode.DIR;
                    Properties.Settings.Default.Save();
                    DisplayContent(WorkspaceDisplayMode.DIR);
                }
            }
        }

        public bool PrjSettings_Rad_WSViewer_Gdb_IsChecked
        {
            get => _prjSettings_Rad_WSViewer_Gdb_IsChecked;
            set
            {
                SetProperty(ref _prjSettings_Rad_WSViewer_Gdb_IsChecked, value, () => PrjSettings_Rad_WSViewer_Gdb_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.WORKSPACE_DISPLAY_MODE = (int)WorkspaceDisplayMode.GDB;
                    Properties.Settings.Default.Save();
                    DisplayContent(WorkspaceDisplayMode.GDB);
                }
            }
        }

        #endregion

        #region COMMANDS

        public ICommand CmdViewLogFile => _cmdViewLogFile ?? (_cmdViewLogFile = new RelayCommand(() => ViewLogFile(), () => true));

        public ICommand CmdClearLogFile => _cmdClearLogFile ?? (_cmdClearLogFile = new RelayCommand(() => ClearLogFile(), () => true));

        public ICommand CmdSelectFolder => _cmdSelectFolder ?? (_cmdSelectFolder = new RelayCommand(() => SelectFolder(), () => true));

        public ICommand CmdSelectNationalDb => _cmdSelectNationalDb ?? (_cmdSelectNationalDb = new RelayCommand(() => SelectNationalDb(), () => true));

        public ICommand CmdValidateNationalDb => _cmdValidateNationalDb ?? (_cmdValidateNationalDb = new RelayCommand(() => ValidateNationalDb(), () => true));

        public ICommand CmdInitializeWorkspace => _cmdInitializeWorkspace ?? (_cmdInitializeWorkspace = new RelayCommand(async () => await InitializeWorkspace(), () => true));

        public ICommand CmdResetWorkspace => _cmdResetWorkspace ?? (_cmdResetWorkspace = new RelayCommand(async () => await ResetWorkspace(), () => true));

        public ICommand CmdExploreWorkspace => _cmdExploreWorkspace ?? (_cmdExploreWorkspace = new RelayCommand(() => ExploreWorkspace(), () => true));

        #endregion

        #region METHODS

        public async Task OnProWinLoaded()
        {
            try
            {
                // Project Folder
                string wspath = Properties.Settings.Default.WORKSPACE_PATH;
                if (string.IsNullOrEmpty(wspath) || string.IsNullOrWhiteSpace(wspath))
                {
                    PrjSettings_Txt_ProjectFolderPath = "";
                }
                else
                {
                    PrjSettings_Txt_ProjectFolderPath = wspath;
                }

                // National DB
                string natpath = Properties.Settings.Default.NATDB_DBPATH;
                if (string.IsNullOrEmpty(natpath) || string.IsNullOrWhiteSpace(natpath))
                {
                    PrjSettings_Txt_NationalDbPath = "";
                }
                else
                {
                    PrjSettings_Txt_NationalDbPath = natpath;
                }

                // Validate National Db
                var tryvalidate = await ValidateNationalDb();

                if (tryvalidate.success && tryvalidate.valid)
                {
                    NatDBInfo_Img_Status = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    NatDBInfo_Img_Status = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
                }

                // Workspace Viewer
                WorkspaceDisplayMode mode = (WorkspaceDisplayMode)Properties.Settings.Default.WORKSPACE_DISPLAY_MODE;
                if (mode == WorkspaceDisplayMode.DIR)
                {
                    PrjSettings_Rad_WSViewer_Dir_IsChecked = true;
                }
                else if (mode == WorkspaceDisplayMode.GDB)
                {
                    PrjSettings_Rad_WSViewer_Gdb_IsChecked = true;
                }
                else
                {
                    PrjSettings_Rad_WSViewer_Dir_IsChecked = true;
                }



            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task DisplayContent(WorkspaceDisplayMode mode)
        {
            StringBuilder contents = new StringBuilder("");

            try
            {
                string wspath = PrjSettings_Txt_ProjectFolderPath;

                if (mode == WorkspaceDisplayMode.DIR)
                {
                    // Validate the Project Folder
                    if (!Directory.Exists(wspath))
                    {
                        contents.AppendLine($"Directory does not exist: {wspath}");
                        this.PrjSettings_Txt_WorkspaceContents = contents.ToString();
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
                else if (mode == WorkspaceDisplayMode.GDB)
                {
                    string gdbpath = PRZH.GetPath_ProjectGDB();

                    // ensure project gdb exists
                    var try_exists = await PRZH.GDBExists_Project();
                    
                    if (!try_exists.exists)
                    {
                        contents.AppendLine($"Project Geodatabase does not exist at path: {gdbpath}");
                        this.PrjSettings_Txt_WorkspaceContents = contents.ToString();
                        return;
                    }

                    // Create the lists of gdb object names
                    List<string> FCNames = new List<string>();
                    List<string> RasterNames = new List<string>();
                    List<string> TableNames = new List<string>();

                    if (!await QueuedTask.Run(() =>
                    {
                        // Get the project geodatabase
                        var try_gdb = PRZH.GetGDB_Project();

                        if (!try_gdb.success)
                        {
                            return false;
                        }

                        // Populate the geodatabase object name lists
                        using (Geodatabase geodatabase = try_gdb.geodatabase)
                        {
                            IReadOnlyList<FeatureClassDefinition> fcDefs = geodatabase.GetDefinitions<FeatureClassDefinition>();

                            foreach (var fcDef in fcDefs)
                            {
                                FCNames.Add(fcDef.GetName());
                            }

                            IReadOnlyList<RasterDatasetDefinition> rasDefs = geodatabase.GetDefinitions<RasterDatasetDefinition>();

                            foreach (var rasDef in rasDefs)
                            {
                                RasterNames.Add(rasDef.GetName());
                            }

                            IReadOnlyList<TableDefinition> tabDefs = geodatabase.GetDefinitions<TableDefinition>();

                            foreach (var tabDef in tabDefs)
                            {
                                TableNames.Add(tabDef.GetName());
                            }
                        }

                        return true;
                    }))
                    {
                        contents.AppendLine($"Unable to retrieve project geodatabase.");
                        this.PrjSettings_Txt_WorkspaceContents = contents.ToString();
                        return;
                    }

                    // Enter top-level info
                    contents.AppendLine("PROJECT GEODATABASE");
                    contents.AppendLine("*******************");
                    contents.AppendLine("PATH: " + gdbpath);
                    contents.AppendLine();

                    // List Feature Classes
                    contents.AppendLine("FEATURE CLASSES");
                    contents.AppendLine("***************");

                    FCNames.Sort();

                    foreach (string name in FCNames)
                    {
                        contents.AppendLine($" > {name}");
                    }

                    contents.AppendLine();

                    // List Feature Classes
                    contents.AppendLine("RASTER DATASETS");
                    contents.AppendLine("***************");

                    RasterNames.Sort();

                    foreach (string name in RasterNames)
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

                this.PrjSettings_Txt_WorkspaceContents = contents.ToString();
            }
            catch (Exception ex)
            {
                contents.AppendLine(ex.Message);
                this.PrjSettings_Txt_WorkspaceContents = contents.ToString();
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private void SelectFolder()
        {
            try
            {
                string initDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (Directory.Exists(PrjSettings_Txt_ProjectFolderPath))
                {
                    DirectoryInfo di = new DirectoryInfo(PrjSettings_Txt_ProjectFolderPath);
                    DirectoryInfo dip = di.Parent;

                    if (dip != null)
                    {
                        initDir = dip.FullName;
                    }
                }

                OpenItemDialog dlg = new OpenItemDialog
                {
                    Title = "Specify a Project Folder",
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

                PrjSettings_Txt_ProjectFolderPath = item.Path;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> InitializeWorkspace()
        {
            try
            {
                // Validate the current Project Workspace
                if (!Directory.Exists(PrjSettings_Txt_ProjectFolderPath))
                {
                    ProMsgBox.Show("Please select a valid Project Folder" + Environment.NewLine + Environment.NewLine + PrjSettings_Txt_ProjectFolderPath + " does not exist");
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
                var try_gdbexists = await PRZH.GDBExists_Project();
                bool gdbfolderexists = Directory.Exists(gdbpath);

                if (!try_gdbexists.exists)
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
                if (PrjSettings_Rad_WSViewer_Dir_IsChecked)
                {
                    await DisplayContent(WorkspaceDisplayMode.DIR);
                }
                else if (PrjSettings_Rad_WSViewer_Gdb_IsChecked)
                {
                    await DisplayContent(WorkspaceDisplayMode.GDB);
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

        private async Task<bool> ResetWorkspace()
        {
            try
            {
                // Validate the current Project Workspace
                if (!Directory.Exists(PrjSettings_Txt_ProjectFolderPath))
                {
                    ProMsgBox.Show("Please select a valid Project Folder" + Environment.NewLine + Environment.NewLine + PrjSettings_Txt_ProjectFolderPath + " does not exist");
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
                bool gdbfolderexists = Directory.Exists(gdbpath);
                var try_gdbexists = await PRZH.GDBExists_Project();

                // Create the Geodatabase
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread;
                string toolOutput;

                if (try_gdbexists.exists)  // geodatabase exists
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
                if (PrjSettings_Rad_WSViewer_Dir_IsChecked)
                {
                    await DisplayContent(WorkspaceDisplayMode.DIR);
                }
                else if (PrjSettings_Rad_WSViewer_Gdb_IsChecked)
                {
                    await DisplayContent(WorkspaceDisplayMode.GDB);
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

        private void ExploreWorkspace()
        {
            try
            {
                string fp = PrjSettings_Txt_ProjectFolderPath;

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

        private void ViewLogFile()
        {
            try
            {
                string wspath = PrjSettings_Txt_ProjectFolderPath;

                if (!Directory.Exists(wspath))
                {
                    ProMsgBox.Show("Invalid Project Folder Path..." + Environment.NewLine + Environment.NewLine + wspath + " does not exist");
                    return;
                }

                string logpath = PRZH.GetPath_ProjectLog();

                if (!File.Exists(logpath))
                {
                    ProMsgBox.Show($"Log file does not exist: {logpath}\nPlease initialize the project workspace.");
                    return;
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = logpath,
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

        private void ClearLogFile()
        {
            try
            {
                // Prompt User for permission to proceed
                if (ProMsgBox.Show("This will remove all entries from the project log file." + Environment.NewLine + Environment.NewLine +
                   "Do you wish to proceed?" +
                   Environment.NewLine + Environment.NewLine +
                   "Choose wisely...",
                   "Clear Log File",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    return;
                }

                // Clear the log file
                PRZH.WriteLog("Log File Cleared.", LogMessageType.INFO, false);

                ProMsgBox.Show("Log File Cleared.");
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task SelectNationalDb()
        {
            try
            {
                string initDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                // File Geodatabase
                if (Directory.Exists(PrjSettings_Txt_NationalDbPath) && Path.IsPathRooted(PrjSettings_Txt_NationalDbPath) && PrjSettings_Txt_NationalDbPath.EndsWith(".gdb"))
                {
                    DirectoryInfo di = new DirectoryInfo(PrjSettings_Txt_NationalDbPath);
                    if (di != null && di.Parent != null)
                    {
                        initDir = di.Parent.FullName;
                    }
                }

                // Database Connection File (.sde)
                else if (File.Exists(PrjSettings_Txt_NationalDbPath) && Path.IsPathRooted(PrjSettings_Txt_NationalDbPath) && PrjSettings_Txt_NationalDbPath.EndsWith(".sde"))
                {
                    FileInfo fi = new FileInfo(PrjSettings_Txt_NationalDbPath);
                    if (fi != null && fi.Directory != null)
                    {
                        initDir = fi.DirectoryName;
                    }
                }

                // Configure the Browse Filter
                BrowseProjectFilter bf = new BrowseProjectFilter("esri_browseDialogFilters_geodatabases")
                {
                    Name = "Geodatabases"
                };

                OpenItemDialog dlg = new OpenItemDialog
                {
                    Title = "Specify a National Database",
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
                PrjSettings_Txt_NationalDbPath = thePath;

                // Finally, validate the database.
                var tryvalidate = await ValidateNationalDb();
                if (tryvalidate.success && tryvalidate.valid)
                {
                    NatDBInfo_Img_Status = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    NatDBInfo_Img_Status = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<(bool success, bool valid, string message)> ValidateNationalDb()
        {
            try
            {
                // Ensure national database exists
                var tryexists = await PRZH.GDBExists_Nat();
                if (!tryexists.exists)
                {
                    NatDbInfo_Txt_DbName = "";
                    NatDbInfo_Cmb_SchemaNames = new List<string>();
                    NatDbInfo_Cmb_SelectedSchemaName = "";

                    return (false, false, tryexists.message);
                }
                else if (tryexists.gdbType == GeoDBType.EnterpriseGDB)
                {
                    // do nothing here yet...
                }
                else if (tryexists.gdbType == GeoDBType.FileGDB)
                {
                    NatDbInfo_Txt_DbName = "";
                    NatDbInfo_Cmb_SchemaNames = new List<string>();
                    NatDbInfo_Cmb_SelectedSchemaName = "";
                }
                else
                {
                    return (false, false, tryexists.message);
                }

                HashSet<string> full_names = new HashSet<string>();
                HashSet<string> parsed_names = new HashSet<string>();
                HashSet<string> schemas = new HashSet<string>();

                // Retrieve and validate the table list
                (bool success, string message) outcome = await QueuedTask.Run(() =>
                {
                    var tryget = PRZH.GetGDB_Nat();
                    
                    // If I'm unable to retrieve the geodatabase, exit.
                    if (!tryget.success)
                    {
                        return (false, tryget.message);
                    }

                    using (Geodatabase geodatabase = tryget.geodatabase)
                    {
                        // Get the database name (if applicable)
                        if (tryget.gdbType == GeoDBType.EnterpriseGDB)
                        {
                            var conn = (DatabaseConnectionProperties)geodatabase.GetConnector();
                            NatDbInfo_Txt_DbName = ((DatabaseConnectionProperties)geodatabase.GetConnector()).Database;
                        }
                        else
                        {
                            NatDbInfo_Txt_DbName = "";
                        }

                        // Get the table names
                        var table_names = geodatabase.GetDefinitions<TableDefinition>().Select(d => d.GetName());
                        SQLSyntax syntax = geodatabase.GetSQLSyntax();

                        // process names
                        foreach (var name in table_names)
                        {
                            // store full name
                            full_names.Add(name);

                            // parse full name
                            var parsed = syntax.ParseTableName(name);

                            // get schema
                            if (!string.IsNullOrEmpty(parsed.Item2))
                            {
                                schemas.Add(parsed.Item2);
                            }

                            // get name
                            if (!string.IsNullOrEmpty(parsed.Item3))
                            {
                                parsed_names.Add(parsed.Item3);
                            }
                        }

                        HashSet<string> all_schemas = new HashSet<string>();
                        // I am now looking for presence of Element and Theme tables
                        foreach (var name in table_names)
                        {
                            // get all schemas
                            var p = syntax.ParseTableName(name);
                            all_schemas.Add(p.Item2);
                        }

                        List<string> valid_schemas = new List<string>();

                        foreach (string schema in all_schemas)
                        {
                            bool element_found = false;
                            bool theme_found = false;

                            foreach (var name in table_names)
                            {
                                var p2 = syntax.ParseTableName(name);

                                if (p2.Item2 == schema)
                                {
                                    if (p2.Item3 == PRZC.c_TABLE_NAT_ELEMENTS)
                                    {
                                        element_found = true;
                                    }

                                    if (p2.Item3 == PRZC.c_TABLE_NAT_THEMES)
                                    {
                                        theme_found = true;
                                    }
                                }
                            }

                            if (element_found && theme_found)
                            {
                                valid_schemas.Add(schema);
                            }
                        }
                    }
                    // I'm here!!!

                    return (true, "success");
                });

                if (!outcome.success)
                {
                    return (false, false, outcome.message);
                }

                // Populate the list of schemas
                if (schemas.Count > 0)
                {
                    List<string> l = schemas.ToList();
                    l.Sort();
                    NatDbInfo_Cmb_SchemaNames = l;

                    string saved_schema = Properties.Settings.Default.NATDB_SCHEMANAME;
                    if (!string.IsNullOrEmpty(saved_schema) && l.Contains(saved_schema))
                    {
                        NatDbInfo_Cmb_SelectedSchemaName = saved_schema;
                    }
                    else
                    {
                        NatDbInfo_Cmb_SelectedSchemaName = l[0];    // first element in the list.
                    }
                }
                else
                {
                    NatDbInfo_Cmb_SchemaNames = new List<string>();
                    NatDbInfo_Cmb_SelectedSchemaName = "";
                }

                // I need to confirm the presence of an Element and a Theme table within the same schema (if schemas are applicable


                // Finally, trigger visibility of schema combo control
                if (tryexists.gdbType == GeoDBType.EnterpriseGDB)
                {
                    NatDbInfo_Vis_DockPanel = Visibility.Visible;
                }
                else
                {
                    NatDbInfo_Vis_DockPanel = Visibility.Collapsed;
                }

                return (true, true, "success");
            }
            catch (Exception ex)
            {
                NatDbInfo_Vis_DockPanel = Visibility.Collapsed;
                NatDbInfo_Txt_DbName = "";
                NatDbInfo_Cmb_SchemaNames = new List<string>();
                NatDbInfo_Cmb_SelectedSchemaName = "";

                return (false, false, ex.Message);
            }
        }

        #endregion


    }
}