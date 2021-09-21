using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
//using System.Windows.Forms;
//using Excel = Microsoft.Office.Interop.Excel;
using PRZC = NCC.PRZTools.PRZConstants;

namespace NCC.PRZTools
{

    public static class PRZHelper
    {
        #region LOGGING AND NOTIFICATIONS

        // Write to log
        public static string WriteLog(string message, LogMessageType type = LogMessageType.INFO, bool Append = true)
        {
            try
            {
                // Make sure we have a valid log file
                if (!FolderExists_Project())
                {
                    return "";
                }

                string logpath = GetPath_ProjectLog();
                if (!ProjectLogExists())
                {
                    using (FileStream fs = File.Create(logpath)) { }
                }

                // Create the message lines
                string lines = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.ff tt") + " :: " + type.ToString() 
                                + Environment.NewLine + message 
                                + Environment.NewLine;

                if (Append)
                {
                    using (StreamWriter w = File.AppendText(logpath))
                    {
                        w.WriteLine(lines);
                        w.Flush();
                        w.Close();
                    }
                }
                else
                {
                    using (StreamWriter w = File.CreateText(logpath))
                    {
                        w.WriteLine(lines);
                        w.Flush();
                        w.Close();
                    }
                }

                return lines;
            }
            catch (Exception)
            {
                return "Unable to log message...";
            }
        }

        // Read from the log
        public static string ReadLog()
        {
            try
            {
                if (!ProjectLogExists())
                {
                    return "";
                }

                string logpath = GetPath_ProjectLog();

                return File.ReadAllText(logpath);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

        }

        // User notifications
        public static void UpdateProgress(ProgressManager pm, string message, bool append)
        {
            try
            {
                DispatchProgress(pm, message, append, null, null, null);
            }
            catch (Exception)
            {
            }
        }

        public static void UpdateProgress(ProgressManager pm, string message, bool append, int current)
        {
            try
            {
                DispatchProgress(pm, message, append, null, null, current);
            }
            catch (Exception)
            {
            }
        }

        public static void UpdateProgress(ProgressManager pm, string message, bool append, int max, int current)
        {
            try
            {
                DispatchProgress(pm, message, append, null, max, current);
            }
            catch (Exception)
            {
            }
        }

        public static void UpdateProgress(ProgressManager pm, string message, bool append, int min, int max, int current)
        {
            try
            {
                DispatchProgress(pm, message, append, min, max, current);
            }
            catch (Exception)
            {
            }
        }

        private static void DispatchProgress(ProgressManager pm, string message, bool append, int? min, int? max, int? current)
        {
            try
            {
                if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    ManageProgress(pm, message, append, min, max, current);
                }
                else
                {
                    ProApp.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
                    {
                        ManageProgress(pm, message, append, min, max, current);
                    }));
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private static void ManageProgress(ProgressManager pm, string message, bool append, int? min, int? max, int? current)
        {
            try
            {
                // Update the Message
                if (string.IsNullOrEmpty(message))
                {
                    pm.Message = append ? (pm.Message + Environment.NewLine + "") : "";
                }
                else
                {
                    pm.Message = append ? (pm.Message + Environment.NewLine + message) : message;
                }

                // Update the Min property
                if (min != null)
                {
                    pm.Min = (int)min;
                }

                // Update the Max property
                if (max != null)
                {
                    pm.Max = (int)max;
                }

                // Update the Value property
                if (current != null)
                {
                    pm.Current = (int)current;
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }


        #endregion LOGGING

        #region PATHS

        #region FILE AND FOLDER PATHS

        // Project Folder
        public static string GetPath_ProjectFolder()
        {
            try
            {
                return Properties.Settings.Default.WORKSPACE_PATH;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // Project GDB
        public static string GetPath_ProjectGDB()
        {
            try
            {
                string wspath = GetPath_ProjectFolder();
                string gdbpath = Path.Combine(wspath, PRZC.c_FILE_PRZ_FGDB);

                return gdbpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // Project Log File
        public static string GetPath_ProjectLog()
        {
            try
            {
                string ws = GetPath_ProjectFolder();
                string logpath = Path.Combine(ws, PRZC.c_FILE_PRZ_LOG);

                return logpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // Input Subfolder
        public static string GetPath_InputFolder()
        {
            try
            {
                string wspath = GetPath_ProjectFolder();
                string inputfolderpath = Path.Combine(wspath, PRZC.c_DIR_INPUT);

                return inputfolderpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // Output Subfolder
        public static string GetPath_OutputFolder()
        {
            try
            {
                string wspath = GetPath_ProjectFolder();
                string outputfolderpath = Path.Combine(wspath, PRZC.c_DIR_OUTPUT);

                return outputfolderpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // Export WTW Subfolder
        public static string GetPath_ExportWTWFolder()
        {
            try
            {
                string wspath = GetPath_ProjectFolder();
                string exportwtwpath = Path.Combine(wspath, PRZC.c_DIR_EXPORT_WTW);

                return exportwtwpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        #endregion

        #region GEODATABASE OBJECT PATHS

        // Planning Unit FC Path
        public static string GetPath_FC_PU()
        {
            try
            {
                string gdbpath = GetPath_ProjectGDB();
                string pufcpath = Path.Combine(gdbpath, PRZC.c_FC_PLANNING_UNITS);

                return pufcpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // Study Area FC Path
        public static string GetPath_FC_StudyArea()
        {
            try
            {
                string gdbpath = GetPath_ProjectGDB();
                string fcpath = Path.Combine(gdbpath, PRZC.c_FC_STUDY_AREA_MAIN);

                return fcpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // Study Area Buffer FC Path
        public static string GetPath_FC_StudyAreaBuffer()
        {
            try
            {
                string gdbpath = GetPath_ProjectGDB();
                string fcpath = Path.Combine(gdbpath, PRZC.c_FC_STUDY_AREA_MAIN_BUFFERED);

                return fcpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // Sel Rules Table Path
        public static string GetPath_Table_SelRules()
        {
            try
            {
                string gdbpath = GetPath_ProjectGDB();
                string path = Path.Combine(gdbpath, PRZC.c_TABLE_SELRULES);

                return path;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // PU Sel Rules Table Path
        public static string GetPath_Table_PUSelRules()
        {
            try
            {
                string gdbpath = GetPath_ProjectGDB();
                string path = Path.Combine(gdbpath, PRZC.c_TABLE_PUSELRULES);

                return path;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // PU Cost Table Path
        public static string GetPath_Table_PUCost()
        {
            try
            {
                string gdbpath = GetPath_ProjectGDB();
                string path = Path.Combine(gdbpath, PRZC.c_TABLE_COSTSTATS);

                return path;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // Features Table Path
        public static string GetPath_Table_Features()
        {
            try
            {
                string gdbpath = GetPath_ProjectGDB();
                string path = Path.Combine(gdbpath, PRZC.c_TABLE_FEATURES);

                return path;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // PU Features Table Path
        public static string GetPath_Table_PUFeatures()
        {
            try
            {
                string gdbpath = GetPath_ProjectGDB();
                string path = Path.Combine(gdbpath, PRZC.c_TABLE_PUFEATURES);

                return path;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // PU Boundary Table Path
        public static string GetPath_Table_Boundary()
        {
            try
            {
                string gdbpath = GetPath_ProjectGDB();
                string path = Path.Combine(gdbpath, PRZC.c_TABLE_PUBOUNDARY);

                return path;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        #endregion

        #endregion

        #region OBJECT EXISTENCE

        public static bool FolderExists_Project()
        {
            try
            {
                string path = GetPath_ProjectFolder();
                return Directory.Exists(path);
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static async Task<bool> ProjectGDBExists()
        {
            try
            {
                bool result = await QueuedTask.Run(() =>
                {
                    string gdbpath = GetPath_ProjectGDB();

                    if (gdbpath == null)
                    {
                        return false;
                    }

                    Uri u = new Uri(gdbpath);
                    FileGeodatabaseConnectionPath fgcpath = new FileGeodatabaseConnectionPath(u);

                    try
                    {
                        using (Geodatabase gdb = new Geodatabase(fgcpath)) { }
                    }
                    catch (GeodatabaseNotFoundOrOpenedException)
                    {
                        return false;
                    }

                    // If I get to this point, the file gdb exists and was successfully opened
                    return true;
                });

                return result;

            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool ProjectLogExists()
        {
            try
            {
                string path = GetPath_ProjectLog();
                return File.Exists(path);
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool FolderExists_Input()
        {
            try
            {
                string path = GetPath_InputFolder();
                return Directory.Exists(path);
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool FolderExists_Output()
        {
            try
            {
                string path = GetPath_OutputFolder();
                return Directory.Exists(path);
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool FolderExists_ExportWTW()
        {
            try
            {
                string path = GetPath_ExportWTWFolder();
                return Directory.Exists(path);
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static async Task<bool> FCExists_PU()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null)
                    {
                        return false;
                    }

                    return await FCExists(gdb, PRZC.c_FC_PLANNING_UNITS);
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static async Task<bool> FCExists_StudyArea()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null)
                    {
                        return false;
                    }

                    return await FCExists(gdb, PRZC.c_FC_STUDY_AREA_MAIN);
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static async Task<bool> FCExists_StudyAreaBuffer()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null)
                    {
                        return false;
                    }

                    return await FCExists(gdb, PRZC.c_FC_STUDY_AREA_MAIN_BUFFERED);
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static async Task<bool> TableExists_PUSelRules()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null)
                    {
                        return false;
                    }

                    return await TableExists(gdb, PRZC.c_TABLE_PUSELRULES);
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static async Task<bool> TableExists_PUCost()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null)
                    {
                        return false;
                    }

                    return await TableExists(gdb, PRZC.c_TABLE_COSTSTATS);
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static async Task<bool> TableExists_Features()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null)
                    {
                        return false;
                    }

                    return await TableExists(gdb, PRZC.c_TABLE_FEATURES);
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static async Task<bool> TableExists_SelRules()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null)
                    {
                        return false;
                    }

                    return await TableExists(gdb, PRZC.c_TABLE_SELRULES);
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static async Task<bool> TableExists_PUFeatures()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null)
                    {
                        return false;
                    }

                    return await TableExists(gdb, PRZC.c_TABLE_PUFEATURES);
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static async Task<bool> TableExists_Boundary()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null)
                    {
                        return false;
                    }

                    return await TableExists(gdb, PRZC.c_TABLE_PUBOUNDARY);
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        #endregion

        #region OBJECT RETRIEVAL

        public static async Task<Geodatabase> GetProjectGDB()
        {
            try
            {
                string gdbpath = GetPath_ProjectGDB();

                Uri uri = new Uri(gdbpath);
                FileGeodatabaseConnectionPath connpath = new FileGeodatabaseConnectionPath(uri);
                Geodatabase gdb = null;

                try
                {
                    await QueuedTask.Run(() =>
                    {
                        gdb = new Geodatabase(connpath);
                    });

                }
                catch (GeodatabaseNotFoundOrOpenedException)
                {
                    return null;
                }

                // If I get to this point, the file gdb exists and was successfully opened
                return gdb;
            }
            catch (Exception)
            {
                //MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static async Task<FeatureClass> GetFC_PU()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null) return null;

                    try
                    {
                        FeatureClass fc = await QueuedTask.Run(() =>
                        {
                            return gdb.OpenDataset<FeatureClass>(PRZC.c_FC_PLANNING_UNITS);
                        });

                        return fc;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static async Task<FeatureClass> GetFC_StudyArea()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null) return null;

                    try
                    {
                        FeatureClass fc = await QueuedTask.Run(() =>
                        {
                            return gdb.OpenDataset<FeatureClass>(PRZC.c_FC_STUDY_AREA_MAIN);
                        });

                        return fc;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static async Task<FeatureClass> GetFC_StudyAreaBuffer()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null) return null;

                    try
                    {
                        FeatureClass fc = await QueuedTask.Run(() =>
                        {
                            return gdb.OpenDataset<FeatureClass>(PRZC.c_FC_STUDY_AREA_MAIN_BUFFERED);
                        });

                        return fc;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static async Task<Table> GetTable_PUSelRules()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null) return null;

                    try
                    {
                        Table tab = await QueuedTask.Run(() =>
                        {
                            return gdb.OpenDataset<Table>(PRZC.c_TABLE_PUSELRULES);
                        });

                        return tab;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static async Task<Table> GetTable_PUCost()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null) return null;

                    try
                    {
                        Table tab = await QueuedTask.Run(() =>
                        {
                            return gdb.OpenDataset<Table>(PRZC.c_TABLE_COSTSTATS);
                        });

                        return tab;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static async Task<Table> GetTable_Features()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null) return null;

                    try
                    {
                        Table tab = await QueuedTask.Run(() =>
                        {
                            return gdb.OpenDataset<Table>(PRZC.c_TABLE_FEATURES);
                        });

                        return tab;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static async Task<Table> GetTable_PUFeatures()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null) return null;

                    try
                    {
                        Table tab = await QueuedTask.Run(() =>
                        {
                            return gdb.OpenDataset<Table>(PRZC.c_TABLE_PUFEATURES);
                        });

                        return tab;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static async Task<Table> GetTable_SelRules()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null) return null;

                    try
                    {
                        Table tab = await QueuedTask.Run(() =>
                        {
                            return gdb.OpenDataset<Table>(PRZC.c_TABLE_SELRULES);
                        });

                        return tab;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static async Task<Table> GetTable_Boundary()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null) return null;

                    try
                    {
                        Table tab = await QueuedTask.Run(() =>
                        {
                            return gdb.OpenDataset<Table>(PRZC.c_TABLE_PUBOUNDARY);
                        });

                        return tab;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static async Task<Table> GetTable(Geodatabase gdb, string table_name)
        {
            try
            {
                if (gdb == null) return null;

                try
                {
                    Table tab = await QueuedTask.Run(() =>
                    {
                        return gdb.OpenDataset<Table>(table_name);
                    });

                    return tab;
                }
                catch
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static async Task<FeatureClass> GetFeatureClass(Geodatabase gdb, string fc_name)
        {
            try
            {
                if (gdb == null) return null;

                try
                {
                    FeatureClass fc = await QueuedTask.Run(() =>
                    {
                        return gdb.OpenDataset<FeatureClass>(fc_name);
                    });

                    return fc;
                }
                catch (Exception ex)
                {
                    ProMsgBox.Show(ex.Message);
                    return null;
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        #endregion

        #region LAYER EXISTENCE

        public static bool PRZLayerExists(Map map, PRZLayerNames layer_name)
        {
            try
            {
                switch (layer_name)
                {
                    case PRZLayerNames.MAIN:
                        return GroupLayerExists_MAIN(map);

                    case PRZLayerNames.STATUS:
                        return GroupLayerExists_STATUS(map);

                    case PRZLayerNames.STATUS_INCLUDE:
                        return GroupLayerExists_STATUS_INCLUDE(map);

                    case PRZLayerNames.STATUS_EXCLUDE:
                        return GroupLayerExists_STATUS_EXCLUDE(map);

                    case PRZLayerNames.COST:
                        return GroupLayerExists_COST(map);

                    case PRZLayerNames.FEATURE:
                        return GroupLayerExists_FEATURE(map);

                    case PRZLayerNames.PU:
                        return FeatureLayerExists_PU(map);

                    case PRZLayerNames.SA:
                        return FeatureLayerExists_SA(map);

                    case PRZLayerNames.SAB:
                        return FeatureLayerExists_SAB(map);

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool GroupLayerExists_MAIN(Map map)
        {
            try
            {
                // map can't be null
                if (map == null)
                {
                    return false;
                }

                // Get list of map-level group layers having matching name
                List<Layer> LIST_layers = map.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_PRZ && (l is GroupLayer)).ToList();

                // If at least one match is found, return true.  Otherwise, false.
                return LIST_layers.Count > 0;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool GroupLayerExists_STATUS(Map map)
        {
            try
            {
                if (map == null)
                {
                    return false;
                }

                if (!PRZLayerExists(map, PRZLayerNames.MAIN))
                {
                    return false;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_STATUS && (l is GroupLayer)).ToList();

                return LIST_layers.Count > 0;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool GroupLayerExists_STATUS_INCLUDE(Map map)
        {
            try
            {
                if (map == null)
                {
                    return false;
                }

                if (!PRZLayerExists(map, PRZLayerNames.STATUS))
                {
                    return false;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.STATUS);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_STATUS_INCLUDE && (l is GroupLayer)).ToList();

                return LIST_layers.Count > 0;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool GroupLayerExists_STATUS_EXCLUDE(Map map)
        {
            try
            {
                if (map == null)
                {
                    return false;
                }

                if (!PRZLayerExists(map , PRZLayerNames.STATUS))
                {
                    return false;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.STATUS);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_STATUS_EXCLUDE && (l is GroupLayer)).ToList();

                return LIST_layers.Count > 0;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool GroupLayerExists_COST(Map map)
        {
            try
            {
                if (map == null)
                {
                    return false;
                }

                if (!PRZLayerExists(map, PRZLayerNames.MAIN))
                {
                    return false;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_COST && (l is GroupLayer)).ToList();

                return LIST_layers.Count > 0;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool GroupLayerExists_FEATURE(Map map)
        {
            try
            {
                if (map == null)
                {
                    return false;
                }

                if (!PRZLayerExists(map, PRZLayerNames.MAIN))
                {
                    return false;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_FEATURE && (l is GroupLayer)).ToList();

                return LIST_layers.Count > 0;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool FeatureLayerExists_PU(Map map)
        {
            try
            {
                if (map == null)
                {
                    return false;
                }

                if (!PRZLayerExists(map, PRZLayerNames.MAIN))
                {
                    return false;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_LAYER_PLANNING_UNITS && (l is FeatureLayer)).ToList();

                return LIST_layers.Count > 0;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool FeatureLayerExists_SA(Map map)
        {
            try
            {
                if (map == null)
                {
                    return false;
                }

                if (!PRZLayerExists(map, PRZLayerNames.MAIN))
                {
                    return false;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_LAYER_STUDY_AREA && (l is FeatureLayer)).ToList();

                return LIST_layers.Count > 0;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool FeatureLayerExists_SAB(Map map)
        {
            try
            {
                if (map == null)
                {
                    return false;
                }

                if (!PRZLayerExists(map, PRZLayerNames.MAIN))
                {
                    return false;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_LAYER_STUDY_AREA_BUFFER && (l is FeatureLayer)).ToList();

                return LIST_layers.Count > 0;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        #endregion

        #region SINGLE LAYER RETRIEVAL

        public static Layer GetPRZLayer(Map map, PRZLayerNames layer_name)
        {
            try
            {
                switch (layer_name)
                {
                    case PRZLayerNames.MAIN:
                        return GetGroupLayer_MAIN(map);

                    case PRZLayerNames.STATUS:
                        return GetGroupLayer_STATUS(map);

                    case PRZLayerNames.STATUS_INCLUDE:
                        return GetGroupLayer_STATUS_INCLUDE(map);

                    case PRZLayerNames.STATUS_EXCLUDE:
                        return GetGroupLayer_STATUS_EXCLUDE(map);

                    case PRZLayerNames.COST:
                        return GetGroupLayer_COST(map);

                    case PRZLayerNames.FEATURE:
                        return GetGroupLayer_FEATURE(map);

                    case PRZLayerNames.PU:
                        return GetFeatureLayer_PU(map);

                    case PRZLayerNames.SA:
                        return GetFeatureLayer_SA(map);

                    case PRZLayerNames.SAB:
                        return GetFeatureLayer_SAB(map);

                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static GroupLayer GetGroupLayer_MAIN(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.MAIN))
                {
                    return null;
                }

                List<Layer> LIST_layers = map.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_PRZ && (l is GroupLayer)).ToList();

                if (LIST_layers.Count == 0)
                {
                    return null;
                }
                else
                {
                    return LIST_layers[0] as GroupLayer;
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static GroupLayer GetGroupLayer_STATUS(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.STATUS))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_STATUS && (l is GroupLayer)).ToList();

                if (LIST_layers.Count == 0)
                {
                    return null;
                }
                else
                {
                    return LIST_layers[0] as GroupLayer;
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static GroupLayer GetGroupLayer_STATUS_INCLUDE(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.STATUS_INCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.STATUS);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_STATUS_INCLUDE && (l is GroupLayer)).ToList();

                if (LIST_layers.Count == 0)
                {
                    return null;
                }
                else
                {
                    return LIST_layers[0] as GroupLayer;
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static GroupLayer GetGroupLayer_STATUS_EXCLUDE(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.STATUS_EXCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.STATUS);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_STATUS_EXCLUDE && (l is GroupLayer)).ToList();

                if (LIST_layers.Count == 0)
                {
                    return null;
                }
                else
                {
                    return LIST_layers[0] as GroupLayer;
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static GroupLayer GetGroupLayer_COST(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.COST))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_COST && (l is GroupLayer)).ToList();

                if (LIST_layers.Count == 0)
                {
                    return null;
                }
                else
                {
                    return LIST_layers[0] as GroupLayer;
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static GroupLayer GetGroupLayer_FEATURE(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.FEATURE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_FEATURE && (l is GroupLayer)).ToList();

                if (LIST_layers.Count == 0)
                {
                    return null;
                }
                else
                {
                    return LIST_layers[0] as GroupLayer;
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static FeatureLayer GetFeatureLayer_PU(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.PU))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_LAYER_PLANNING_UNITS && (l is FeatureLayer)).ToList();

                if (LIST_layers.Count == 0)
                {
                    return null;
                }
                else
                {
                    return LIST_layers[0] as FeatureLayer;
                }

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static FeatureLayer GetFeatureLayer_SA(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.SA))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_LAYER_STUDY_AREA && (l is FeatureLayer)).ToList();

                if (LIST_layers.Count == 0)
                {
                    return null;
                }
                else
                {
                    return LIST_layers[0] as FeatureLayer;
                }

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static FeatureLayer GetFeatureLayer_SAB(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.SAB))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_LAYER_STUDY_AREA_BUFFER && (l is FeatureLayer)).ToList();

                if (LIST_layers.Count == 0)
                {
                    return null;
                }
                else
                {
                    return LIST_layers[0] as FeatureLayer;
                }

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        #endregion

        #region LAYER COLLECTION RETRIEVAL

        public static List<Layer> GetPRZLayers(Map map, PRZLayerNames container, PRZLayerRetrievalType type)
        {
            try
            {
                // Proceed only for specific containers
                GroupLayer GL = null;

                if (container == PRZLayerNames.COST |
                    container == PRZLayerNames.FEATURE |
                    container == PRZLayerNames.STATUS_INCLUDE |
                    container == PRZLayerNames.STATUS_EXCLUDE)
                {
                    GL = (GroupLayer)GetPRZLayer(map, container);
                }
                else
                {
                    return null;
                }

                // If unable to retrieve container, leave
                if (GL == null)
                {
                    return null;
                }

                // Retrieve the layers in the container
                List<Layer> layers = new List<Layer>();

                switch (type)
                {
                    case PRZLayerRetrievalType.FEATURE:
                        layers = GL.Layers.Where(lyr => lyr is FeatureLayer).ToList();
                        break;
                    case PRZLayerRetrievalType.RASTER:
                        layers = GL.Layers.Where(lyr => lyr is RasterLayer).ToList();
                        break;
                    case PRZLayerRetrievalType.BOTH:
                        layers = GL.Layers.Where(lyr => lyr is FeatureLayer | lyr is RasterLayer).ToList();
                        break;


                    default:
                        return null;
                }

                return layers;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // STATUS INCLUDE
        public static List<FeatureLayer> GetFeatureLayers_STATUS_INCLUDE(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.STATUS_INCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.STATUS_INCLUDE);
                List<FeatureLayer> LIST_layers = GL.Layers.Where(l => l is FeatureLayer).Cast<FeatureLayer>().ToList();

                return LIST_layers;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static List<RasterLayer> GetRasterLayers_STATUS_INCLUDE(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.STATUS_INCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.STATUS_INCLUDE);
                List<RasterLayer> LIST_layers = GL.Layers.Where(l => l is RasterLayer).Cast<RasterLayer>().ToList();

                return LIST_layers;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static List<Layer> GetLayers_STATUS_INCLUDE(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.STATUS_INCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.STATUS_INCLUDE);
                List<Layer> LIST_layers = GL.Layers.Where(l => l is RasterLayer | l is FeatureLayer).ToList();

                return LIST_layers;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // STATUS EXCLUDE
        public static List<FeatureLayer> GetFeatureLayers_STATUS_EXCLUDE(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.STATUS_EXCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.STATUS_EXCLUDE);
                List<FeatureLayer> LIST_layers = GL.Layers.Where(l => l is FeatureLayer).Cast<FeatureLayer>().ToList();

                return LIST_layers;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static List<RasterLayer> GetRasterLayers_STATUS_EXCLUDE(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.STATUS_EXCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.STATUS_EXCLUDE);
                List<RasterLayer> LIST_layers = GL.Layers.Where(l => l is RasterLayer).Cast<RasterLayer>().ToList();

                return LIST_layers;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static List<Layer> GetLayers_STATUS_EXCLUDE(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.STATUS_EXCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.STATUS_EXCLUDE);
                List<Layer> LIST_layers = GL.Layers.Where(l => l is RasterLayer | l is FeatureLayer).ToList();

                return LIST_layers;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // COST
        public static List<FeatureLayer> GetFeatureLayers_COST(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.COST))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.COST);
                List<FeatureLayer> LIST_layers = GL.Layers.Where(l => l is FeatureLayer).Cast<FeatureLayer>().ToList();

                return LIST_layers;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static List<RasterLayer> GetRasterLayers_COST(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.COST))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.COST);
                List<RasterLayer> LIST_layers = GL.Layers.Where(l => l is RasterLayer).Cast<RasterLayer>().ToList();

                return LIST_layers;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static List<Layer> GetLayers_COST(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.COST))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.COST);
                List<Layer> LIST_layers = GL.Layers.Where(l => l is RasterLayer | l is FeatureLayer).ToList();

                return LIST_layers;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // FEATURE
        public static List<FeatureLayer> GetFeatureLayers_FEATURE(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.FEATURE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.FEATURE);
                List<FeatureLayer> LIST_layers = GL.Layers.Where(l => l is FeatureLayer).Cast<FeatureLayer>().ToList();

                return LIST_layers;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static List<RasterLayer> GetRasterLayers_FEATURE(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.FEATURE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.FEATURE);
                List<RasterLayer> LIST_layers = GL.Layers.Where(l => l is RasterLayer).Cast<RasterLayer>().ToList();

                return LIST_layers;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static List<Layer> GetLayers_FEATURE(Map map)
        {
            try
            {
                if (!PRZLayerExists(map, PRZLayerNames.FEATURE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.FEATURE);
                List<Layer> LIST_layers = GL.Layers.Where(l => l is RasterLayer | l is FeatureLayer).ToList();

                return LIST_layers;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        #endregion

        #region GENERIC DATA METHODS

        public static async Task<Geodatabase> GetFileGDB(string path)
        {
            try
            {
                // Ensure the path is an existing directory
                if (!Directory.Exists(path))
                {
                    return null;
                }

                // Ensure the Uri is a valid Uri

                Uri uri = new Uri(path);
                FileGeodatabaseConnectionPath pathConn = new FileGeodatabaseConnectionPath(uri);
                Geodatabase gdb = null;

                try
                {
                    await QueuedTask.Run(() =>
                    {
                        gdb = new Geodatabase(pathConn);
                    });

                }
                catch (GeodatabaseNotFoundOrOpenedException)
                {
                    return null;
                }

                // If I get to this point, the file gdb exists and was successfully opened
                return gdb;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static async Task<bool> FCExists(Geodatabase geodatabase, string FCName)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    using (FeatureClassDefinition fcDef = geodatabase.GetDefinition<FeatureClassDefinition>(FCName))
                    {
                        // Error will be thrown by using statement above if FC of the supplied name doesn't exist in GDB
                    }
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> TableExists(Geodatabase geodatabase, string TabName)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    using (TableDefinition tabdef = geodatabase.GetDefinition<TableDefinition>(TabName))
                    {
                        // Error will be thrown by using statement above if table of the supplied name doesn't exist in GDB
                    }
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> ClearProjectGDB()
        {
            try
            {
                await QueuedTask.Run(async () =>
                {
                    using (Geodatabase gdb = await GetProjectGDB())
                    {
                        SchemaBuilder schemaBuilder = new SchemaBuilder(gdb);

                        // First, delete all feature datasets
                        var fdsDefs = gdb.GetDefinitions<FeatureDatasetDefinition>().ToList();
                        if (fdsDefs.Count > 0)
                        {
                            foreach (var fdsDef in fdsDefs)
                            {
                                schemaBuilder.Delete(new FeatureDatasetDescription(fdsDef));
                            }
                            schemaBuilder.Build();
                        }

                        // Next, delete all remaining standalone feature classes
                        var fcDefs = gdb.GetDefinitions<FeatureClassDefinition>().ToList();
                        if (fcDefs.Count > 0)
                        {
                            foreach (var fcDef in fcDefs)
                            {
                                schemaBuilder.Delete(new FeatureClassDescription(fcDef));
                            }
                            schemaBuilder.Build();
                        }

                        // Finally, delete all remaining tables
                        var tabDefs = gdb.GetDefinitions<TableDefinition>().ToList();
                        if (tabDefs.Count > 0)
                        {
                            foreach (var tabDef in tabDefs)
                            {
                                schemaBuilder.Delete(new TableDescription(tabDef));
                            }
                            schemaBuilder.Build();
                        }
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        #endregion GENERIC DATA METHODS

        #region GEOPROCESSING

        public static async Task<string> RunGPTool(string toolName, IReadOnlyList<string> toolParams, IReadOnlyList<KeyValuePair<string, string>> toolEnvs, GPExecuteToolFlags flags)
        {
            IGPResult gp_result = null;
            using (CancelableProgressorSource cps = new CancelableProgressorSource("Executing GP Tool: " + toolName, "Tool cancelled by user", false))
            {
                // Execute the Geoprocessing Tool
                try
                {
                    gp_result = await Geoprocessing.ExecuteToolAsync(toolName, toolParams, toolEnvs, cps.Progressor, flags);
                }
                catch (Exception ex)
                {
                    // handle error and leave
                    WriteLog("Error Executing GP Tool: " + toolName + " >>> " + ex.Message, LogMessageType.ERROR);
                    return null;
                }
            }

            // At this point, GP Tool has executed and either succeeded, failed, or been cancelled.  There's also a chance that the output IGpResult is null.
            ProcessGPMessages(gp_result, toolName);

            // Configure return value
            if (gp_result == null || gp_result.ReturnValue == null)
            {
                return null;
            }
            else
            {
                return gp_result.ReturnValue;
            }
        }

        private static void ProcessGPMessages(IGPResult gp_result, string toolName)
        {
            try
            {
                StringBuilder messageBuilder = new StringBuilder();
                messageBuilder.AppendLine("Executing GP Tool: " + toolName);

                // If GPTool execution (i.e. ExecuteToolAsync) didn't even run, we have a null IGpResult
                if (gp_result == null)
                {
                    messageBuilder.AppendLine(" > Failure Executing Tool. IGpResult is null...  Something fishy going on here...");
                    WriteLog(messageBuilder.ToString(), LogMessageType.ERROR);
                    return;
                }

                // I now have an existing IGpResult.

                // Assemble the IGpResult Messages into a single string
                if (gp_result.Messages.Count() > 0)
                {
                    foreach (var gp_message in gp_result.Messages)
                    {
                        string gpm = " > " + gp_message.Type.ToString() + " " + gp_message.ErrorCode.ToString() + ": " + gp_message.Text;
                        messageBuilder.AppendLine(gpm);
                    }
                }
                else
                {
                    // if no messages present, add my own message
                    messageBuilder.AppendLine(" > No messages generated...  Something fishy going on here... User might have cancelled");
                }

                // Now, provide some execution result info
                messageBuilder.AppendLine(" > Result Code (0 means success): " + gp_result.ErrorCode.ToString() + "   Execution Status: " + (gp_result.IsFailed ? "Failed or Cancelled" : "Succeeded"));
                messageBuilder.Append(" > Return Value: " + (gp_result.ReturnValue == null ? "null   --> definitely something fishy going on" : gp_result.ReturnValue));

                // Finally, log the message info and return
                if (gp_result.IsFailed)
                {
                    WriteLog(messageBuilder.ToString(), LogMessageType.ERROR);
                }
                else
                {
                    WriteLog(messageBuilder.ToString());
                }
            }
            catch
            {
            }
        }

        public static string GetElapsedTimeMessage(TimeSpan span)
        {
            try
            {
                int inthours = span.Hours;
                int intminutes = span.Minutes;
                int intseconds = span.Seconds;

                string hours = inthours.ToString() + ((inthours == 1) ? " hour" : " hours");
                string minutes = intminutes.ToString() + ((intminutes == 1) ? " minute" : " minutes");
                string seconds = intseconds.ToString() + ((intseconds == 1) ? " second" : " seconds");

                string elapsedmessage = "";

                if (inthours == 0 & intminutes == 0)
                {
                    elapsedmessage = seconds;
                }
                else if (inthours == 0)
                {
                    elapsedmessage = minutes + " and " + seconds;
                }
                else
                {
                    elapsedmessage = hours + ", " + minutes + ", " + seconds;
                }

                return "Elapsed Time: " + elapsedmessage;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return "<error calculating duration>";
            }
        }

        #endregion

        #region RENDERERS

        public static async Task<bool> ApplyLegend_PU_Basic(FeatureLayer FL)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    // Colors
                    CIMColor outlineColor = GetRGBColor(0, 112, 255); // Blue-ish
                    CIMColor fillColor = CIMColor.NoColor();

                    // Symbols
                    CIMStroke outlineSym = SymbolFactory.Instance.ConstructStroke(outlineColor, 1, SimpleLineStyle.Solid);
                    CIMPolygonSymbol fillSym = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor, SimpleFillStyle.Solid, outlineSym);

                    // Create a new Renderer Definition
                    SimpleRendererDefinition rendDef = new SimpleRendererDefinition
                    {
                        SymbolTemplate = fillSym.MakeSymbolReference()
                    };

                    CIMSimpleRenderer rend = (CIMSimpleRenderer)FL.CreateRenderer(rendDef);
                    rend.Patch = PatchShape.AreaSquare;
                    FL.SetRenderer(rend);
                });

                MapView.Active.Redraw(false);

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static async Task<bool> ApplyLegend_PU_SelRules(FeatureLayer FL)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    // COLORS
                    CIMColor outlineColor = GetNamedColor(Color.Gray); // outline color for all 3 poly symbols
                    CIMColor fillColor_Available = GetNamedColor(Color.Bisque);
                    CIMColor fillColor_Include = GetNamedColor(Color.GreenYellow);
                    CIMColor fillColor_Exclude = GetNamedColor(Color.OrangeRed);

                    // SYMBOLS
                    CIMStroke outlineSym = SymbolFactory.Instance.ConstructStroke(outlineColor, 1, SimpleLineStyle.Solid);
                    CIMPolygonSymbol fillSym_Available = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor_Available, SimpleFillStyle.Solid, outlineSym);
                    CIMPolygonSymbol fillSym_Include = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor_Include, SimpleFillStyle.Solid, outlineSym);
                    CIMPolygonSymbol fillSym_Exclude = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor_Exclude, SimpleFillStyle.Solid, outlineSym);

                    // CIM UNIQUE VALUES
                    CIMUniqueValue uv_Available = new CIMUniqueValue { FieldValues = new string[] { "0" } };
                    CIMUniqueValue uv_Include = new CIMUniqueValue { FieldValues = new string[] { "2" } };
                    CIMUniqueValue uv_Exclude = new CIMUniqueValue { FieldValues = new string[] { "3" } };

                    // CIM UNIQUE VALUE CLASSES
                    CIMUniqueValueClass uvcAvailable = new CIMUniqueValueClass
                    {
                        Editable = true,
                        Label = "Available",
                        Symbol = fillSym_Available.MakeSymbolReference(),
                        Description = "",
                        Visible = true,
                        Values = new CIMUniqueValue[] { uv_Available }
                    };
                    CIMUniqueValueClass uvcInclude = new CIMUniqueValueClass
                    {
                        Editable = true,
                        Label = "Included (Locked In)",
                        Symbol = fillSym_Include.MakeSymbolReference(),
                        Description = "",
                        Visible = true,
                        Values = new CIMUniqueValue[] { uv_Include }
                    };
                    CIMUniqueValueClass uvcExclude = new CIMUniqueValueClass
                    {
                        Editable = true,
                        Label = "Excluded (Locked Out)",
                        Symbol = fillSym_Exclude.MakeSymbolReference(),
                        Description = "",
                        Visible = true,
                        Values = new CIMUniqueValue[] { uv_Exclude }
                    };

                    // CIM UNIQUE VALUE GROUP
                    CIMUniqueValueGroup uvgMain = new CIMUniqueValueGroup
                    {
                        Classes = new CIMUniqueValueClass[] { uvcInclude, uvcExclude, uvcAvailable },
                        Heading = "Status"
                    };

                    // UV RENDERER
                    CIMUniqueValueRenderer UVRend = new CIMUniqueValueRenderer
                    {
                        UseDefaultSymbol = false,
                        Fields = new string[] { PRZC.c_FLD_FC_PU_EFFECTIVE_RULE },
                        Groups = new CIMUniqueValueGroup[] { uvgMain },
                        DefaultSymbolPatch = PatchShape.AreaRoundedRectangle
                    };

                    FL.SetRenderer(UVRend);
                });

                MapView.Active.Redraw(false);

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static async Task<bool> ApplyLegend_PU_Cost(FeatureLayer FL)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    // get the lowest and highest cost values in PUCF
                    double minCost = 0;
                    double maxCost = 0;
                    bool seeded = false;

                    using (Table table = FL.GetFeatureClass())
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                double cost = Convert.ToDouble(row[PRZC.c_FLD_FC_PU_COST]);

                                if (!seeded)
                                {
                                    minCost = cost;
                                    maxCost = cost;

                                    seeded = true;
                                }
                                else
                                {
                                    if (cost > maxCost)
                                    {
                                        maxCost = cost;
                                    }

                                    if (cost < minCost)
                                    {
                                        minCost = cost;
                                    }
                                }

                            }
                        }
                    }

                    // Create the polygon fill template
                    CIMStroke outline = SymbolFactory.Instance.ConstructStroke(GetNamedColor(Color.Gray), 0, SimpleLineStyle.Solid);
                    CIMPolygonSymbol fillWithOutline = SymbolFactory.Instance.ConstructPolygonSymbol(GetNamedColor(Color.White), SimpleFillStyle.Solid, outline);

                    // Create the color ramp
                    CIMLinearContinuousColorRamp ramp = new CIMLinearContinuousColorRamp
                    {
                        FromColor = GetNamedColor(Color.LightGray),
                        ToColor = GetNamedColor(Color.Red)
                    };

                    // Create the Unclassed Renderer
                    UnclassedColorsRendererDefinition ucDef = new UnclassedColorsRendererDefinition();

                    ucDef.Field = PRZC.c_FLD_FC_PU_COST;
                    ucDef.ColorRamp = ramp;
                    ucDef.LowerColorStop = minCost;
                    ucDef.LowerLabel = minCost.ToString();
                    ucDef.UpperColorStop = maxCost;
                    ucDef.UpperLabel = maxCost.ToString();
                    ucDef.SymbolTemplate = fillWithOutline.MakeSymbolReference();

                    CIMClassBreaksRenderer rend = (CIMClassBreaksRenderer)FL.CreateRenderer(ucDef);
                    FL.SetRenderer(rend);
                });

                await MapView.Active.RedrawAsync(false);

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static async Task<bool> ApplyLegend_PU_CFCount(FeatureLayer FL)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    // What's the highest number of CF within a single planning unit?
                    int maxCF = 0;

                    using (Table table = FL.GetFeatureClass())
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                int max = Convert.ToInt32(row[PRZC.c_FLD_FC_PU_CFCOUNT]);

                                if (max > maxCF)
                                {
                                    maxCF = max;
                                }
                            }
                        }
                    }

                    // Create the polygon fill template
                    CIMStroke outline = SymbolFactory.Instance.ConstructStroke(GetNamedColor(Color.Gray), 0, SimpleLineStyle.Solid);
                    CIMPolygonSymbol fillWithOutline = SymbolFactory.Instance.ConstructPolygonSymbol(GetNamedColor(Color.White), SimpleFillStyle.Solid, outline);

                    // Create the color ramp
                    CIMLinearContinuousColorRamp ramp = new CIMLinearContinuousColorRamp
                    {
                        FromColor = GetNamedColor(Color.LightGray),
                        ToColor = GetNamedColor(Color.ForestGreen)
                    };

                    // Create the Unclassed Renderer
                    UnclassedColorsRendererDefinition ucDef = new UnclassedColorsRendererDefinition();

                    ucDef.Field = PRZC.c_FLD_FC_PU_CFCOUNT;
                    ucDef.ColorRamp = ramp;
                    ucDef.LowerColorStop = 0;
                    ucDef.LowerLabel = "0";
                    ucDef.UpperColorStop = maxCF;
                    ucDef.UpperLabel = maxCF.ToString();
                    ucDef.SymbolTemplate = fillWithOutline.MakeSymbolReference();

                    CIMClassBreaksRenderer rend = (CIMClassBreaksRenderer)FL.CreateRenderer(ucDef);
                    FL.SetRenderer(rend);
                });

                await MapView.Active.RedrawAsync(false);

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static async Task<bool> ApplyLegend_PU_Boundary(FeatureLayer FL)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    // Colors
                    CIMColor colorOutline = GetNamedColor(Color.Gray);
                    CIMColor colorEdge = GetNamedColor(Color.Magenta);
                    CIMColor colorNonEdge = GetNamedColor(Color.LightGray);

                    // Symbols
                    CIMStroke outlineSym = SymbolFactory.Instance.ConstructStroke(colorOutline, 1, SimpleLineStyle.Solid);
                    CIMPolygonSymbol fillEdge = SymbolFactory.Instance.ConstructPolygonSymbol(colorEdge, SimpleFillStyle.Solid, outlineSym);
                    CIMPolygonSymbol fillNonEdge = SymbolFactory.Instance.ConstructPolygonSymbol(colorNonEdge, SimpleFillStyle.Solid, outlineSym);

                    // fields array
                    string[] fields = new string[] { PRZC.c_FLD_FC_PU_HAS_UNSHARED_PERIM };

                    // CIM Unique Values
                    CIMUniqueValue uvEdge = new CIMUniqueValue { FieldValues = new string[] { "1" } };
                    CIMUniqueValue uvNonEdge = new CIMUniqueValue { FieldValues = new string[] { "0" } };

                    // CIM Unique Value Classes
                    CIMUniqueValueClass uvcEdge = new CIMUniqueValueClass
                    {
                        Editable = true,
                        Label = "Edge",
                        Symbol = fillEdge.MakeSymbolReference(),
                        Description = "",
                        Visible = true,
                        Values = new CIMUniqueValue[] { uvEdge }
                    };

                    CIMUniqueValueClass uvcNonEdge = new CIMUniqueValueClass
                    {
                        Editable = true,
                        Label = "Non Edge",
                        Symbol = fillNonEdge.MakeSymbolReference(),
                        Description = "",
                        Visible = true,
                        Values = new CIMUniqueValue[] { uvNonEdge }
                    };

                    // CIM Unique Value Group
                    CIMUniqueValueGroup uvgMain = new CIMUniqueValueGroup
                    {
                        Classes = new CIMUniqueValueClass[] { uvcEdge, uvcNonEdge },
                        Heading = "Has Unshared Perimeter"                        
                    };


                    // Unique Values Renderer
                    CIMUniqueValueRenderer UVRend = new CIMUniqueValueRenderer
                    {
                        UseDefaultSymbol = false,
                        Fields = fields,
                        Groups = new CIMUniqueValueGroup[] { uvgMain },
                        DefaultSymbolPatch = PatchShape.AreaSquare
                    };

                    FL.SetRenderer(UVRend);
                });

                await MapView.Active.RedrawAsync(false);

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }


        public static bool ApplyLegend_SAB_Simple(FeatureLayer FL)
        {
            try
            {
                // Colors
                CIMColor outlineColor = GetNamedColor(Color.Black);
                CIMColor fillColor = CIMColor.NoColor();

                CIMStroke outlineSym = SymbolFactory.Instance.ConstructStroke(outlineColor, 1, SimpleLineStyle.Solid);
                CIMPolygonSymbol fillSym = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor, SimpleFillStyle.Solid, outlineSym);
                CIMSimpleRenderer rend = FL.GetRenderer() as CIMSimpleRenderer;
                rend.Symbol = fillSym.MakeSymbolReference();
                //rend.Label = "";
                FL.SetRenderer(rend);
                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool ApplyLegend_SA_Simple(FeatureLayer FL)
        {
            try
            {
                // Colors
                CIMColor outlineColor = GetNamedColor(Color.Black);
                CIMColor fillColor = CIMColor.NoColor();

                CIMStroke outlineSym = SymbolFactory.Instance.ConstructStroke(outlineColor, 2, SimpleLineStyle.Solid);
                CIMPolygonSymbol fillSym = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor, SimpleFillStyle.Solid, outlineSym);
                CIMSimpleRenderer rend = FL.GetRenderer() as CIMSimpleRenderer;
                rend.Symbol = fillSym.MakeSymbolReference();
                //rend.Label = "";
                FL.SetRenderer(rend);
                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        #endregion

        #region COLORS AND SYMBOLS

        public static CIMColor GetRGBColor(byte r, byte g, byte b, byte a = 100)
        {
            try
            {
                return ColorFactory.Instance.CreateRGBColor(r, g, b, a);
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static CIMColor GetNamedColor(Color color)
        {
            try
            {
                return ColorFactory.Instance.CreateRGBColor(color.R, color.G, color.B, color.A);
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        //internal static ISimpleFillSymbol ReturnSimpleFillSymbol(IColor FillColor, IColor OutlineColor, double OutlineWidth, esriSimpleFillStyle FillStyle)
        //{
        //    try
        //    {
        //        ISimpleLineSymbol Outline = new SimpleLineSymbolClass();
        //        Outline.Color = OutlineColor;
        //        Outline.Style = esriSimpleLineStyle.esriSLSSolid;
        //        Outline.Width = OutlineWidth;

        //        ISimpleFillSymbol FillSymbol = new SimpleFillSymbolClass();
        //        FillSymbol.Color = FillColor;
        //        FillSymbol.Outline = Outline;
        //        FillSymbol.Style = FillStyle;

        //        return FillSymbol;
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(ex.Message);
        //        return null;
        //    }
        //}

        //internal static IMarkerSymbol ReturnMarkerSymbol(string SymbolName, string StyleName, IColor SymbolColor, int SymbolSize)
        //{
        //    try
        //    {
        //        Type t = Type.GetTypeFromProgID("esriFramework.StyleGallery");
        //        System.Object obj = Activator.CreateInstance(t);
        //        IStyleGallery Gallery = obj as IStyleGallery;
        //        IStyleGalleryStorage GalleryStorage = (IStyleGalleryStorage)Gallery;
        //        string StylePath = GalleryStorage.DefaultStylePath + StyleName;

        //        bool StyleFound = false;

        //        for (int i = 0; i < GalleryStorage.FileCount; i++)
        //        {
        //            if (GalleryStorage.get_File(i).ToUpper() == StyleName.ToUpper())
        //            {
        //                StyleFound = true;
        //                break;
        //            }
        //        }

        //        if (!StyleFound)
        //            GalleryStorage.AddFile(StylePath);

        //        IEnumStyleGalleryItem EnumGalleryItem = Gallery.get_Items("Marker Symbols", StyleName, "DEFAULT");
        //        IStyleGalleryItem GalleryItem = EnumGalleryItem.Next();

        //        while (GalleryItem != null)
        //        {
        //            if (GalleryItem.Name == SymbolName)
        //            {
        //                IClone SourceClone = (IClone)GalleryItem.Item;
        //                IClone DestClone = SourceClone.Clone();
        //                IMarkerSymbol MarkerSymbol = (IMarkerSymbol)DestClone;
        //                MarkerSymbol.Color = SymbolColor;
        //                MarkerSymbol.Size = SymbolSize;
        //                return MarkerSymbol;
        //            }
        //            GalleryItem = EnumGalleryItem.Next();
        //        }

        //        MessageBox.Show("Unable to locate Marker Symbol '" + SymbolName + "' in style '" + StyleName + "'.");
        //        return null;

        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(ex.Message);
        //        return null;
        //    }
        //}

        //internal static ISimpleMarkerSymbol ReturnSimpleMarkerSymbol(IColor MarkerColor, IColor OutlineColor, esriSimpleMarkerStyle MarkerStyle, double MarkerSize, double OutlineSize)
        //{
        //    try
        //    {
        //        ISimpleMarkerSymbol MarkerSymbol = new SimpleMarkerSymbolClass();
        //        MarkerSymbol.Color = MarkerColor;
        //        MarkerSymbol.Style = MarkerStyle;
        //        MarkerSymbol.Size = MarkerSize;
        //        MarkerSymbol.Outline = true;
        //        MarkerSymbol.OutlineSize = OutlineSize;
        //        MarkerSymbol.OutlineColor = OutlineColor;

        //        return MarkerSymbol;
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(ex.Message);
        //        return null;
        //    }
        //}

        //internal static IPictureMarkerSymbol ReturnPictureMarkerSymbol(Bitmap SourceBitmap, IColor TransparentColor, double MarkerSize)
        //{
        //    try
        //    {
        //        IPictureMarkerSymbol PictureSymbol = new PictureMarkerSymbolClass();
        //        PictureSymbol.Picture = (IPictureDisp)OLE.GetIPictureDispFromBitmap(SourceBitmap);
        //        PictureSymbol.Size = MarkerSize;
        //        PictureSymbol.BitmapTransparencyColor = TransparentColor;
        //        return PictureSymbol;
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(ex.Message);
        //        return null;
        //    }

        //}


        #endregion

        #region TABLES AND FIELDS

        public static FieldCategory GetFieldCategory(ArcGIS.Desktop.Mapping.FieldDescription fieldDescription)
        {
            try
            {
                FieldCategory fc;

                switch (fieldDescription.Type)
                {
                    // Field values require single quotes
                    case FieldType.String:
                    case FieldType.GUID:
                    case FieldType.GlobalID:
                        fc = FieldCategory.STRING;
                        break;

                    // Field values require datestamp ''
                    case FieldType.Date:
                        fc = FieldCategory.DATE;
                        break;

                    // Field values require nothing, just the value
                    case FieldType.Double:
                    case FieldType.Integer:
                    case FieldType.OID:
                    case FieldType.Single:
                    case FieldType.SmallInteger:
                        fc = FieldCategory.NUMERIC;
                        break;

                    // Everything else...
                    default:
                        fc = FieldCategory.OTHER;
                        break;
                }

                return fc;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return FieldCategory.UNKNOWN;
            }
        }

        #endregion

        #region MISCELLANEOUS

        public static (bool ValueFound, int Value, string AdjustedString) ExtractValueFromString(string string_to_search, string regex_pattern)
        {
            (bool, int, string) errTuple = (false, 0, "");

            try
            {
                Regex regex = new Regex(regex_pattern);
                Match match = regex.Match(string_to_search);

                if (match.Success)
                {
                    string matched_pattern = match.Value;                                                   // match.Value is the [n], [nn], or [nnn] substring includng the square brackets
                    string string_adjusted = string_to_search.Replace(matched_pattern, "").Trim();          // string to search minus the [n], [nn], or [nnn] substring, then trim
                    string value_string = matched_pattern.Replace("[", "").Replace("]", "").Replace("{", "").Replace("}", "");    // leaves just the 1, 2, or 3 numeric digits, no more brackets

                    // convert text value to int
                    if (!int.TryParse(value_string, out int value_int))
                    {
                        return errTuple;
                    }

                    return (true, value_int, string_adjusted);
                }
                else
                {
                    return errTuple;
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return errTuple;
            }
        }

        public static string GetUser()
        {
            string[] fulluser = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split(new Char[] { '\\' });
            return fulluser[fulluser.Length - 1];
        }

        #endregion

    }
}

/*

        internal static void ExportDataViewToExcel(DataView DV, string Title, string SheetName)
        {
            IProgressDialog2 PD = null;
            Excel.Application xlApp = null;
            Excel.Workbook xlWb = null;
            Excel.Worksheet xlWs = null;
            Excel.Range TempRange = null;

            try
            {
                if (DV == null)
                {
                    return;
                }
                else if (DV.Count == 0)
                {
                    return;
                }

                //create progress dialog
                PD = CreateProgressDialog("EXPORT TO EXCEL", 10, IApp.hWnd, esriProgressAnimationTypes.esriProgressGlobe);
                IStepProgressor Stepper = (IStepProgressor)PD;
                Stepper.Message = "Creating Excel Document...";
                Stepper.Step();

                //Make Sure Excel is present and will start
                xlApp = new Excel.ApplicationClass();
                if (xlApp == null)
                {
                    MessageBox.Show("EXCEL could not be started.  Verify your MS Office Installation and/or project references...");
                    return;
                }

                xlWb = xlApp.Workbooks.Add(Missing.Value);
                xlWs = (Excel.Worksheet)xlWb.Worksheets.get_Item(1);
                if (xlWs == null)
                {
                    MessageBox.Show("Worksheet could not be created.  Verify your MS Office Installation and/or project references...");
                    return;
                }
                xlWs.Name = SheetName;

                //Insert Title Information
                xlWs.Cells[1, 1] = Title;
                TempRange = (Excel.Range)xlWs.Cells[1, 1];
                TempRange.Font.Bold = false;
                TempRange.Font.Size = 15;

                xlWs.Cells[2, 1] = "Exported on " + DateTime.Now.ToLongDateString() + "  by " + GetUser();
                TempRange = (Excel.Range)xlWs.Cells[2, 1];
                TempRange.Font.Bold = false;
                TempRange.Font.Italic = true;
                TempRange.Font.Size = 10;

                TempRange = xlWs.get_Range("A1", "W2");
                TempRange.Borders.Color = ColorTranslator.ToOle(Color.LightYellow);
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeBottom).LineStyle = Excel.XlLineStyle.xlContinuous;
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeBottom).Weight = Excel.XlBorderWeight.xlThin;
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeBottom).Color = ColorTranslator.ToOle(Color.Black);
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeRight).LineStyle = Excel.XlLineStyle.xlContinuous;
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeRight).Weight = Excel.XlBorderWeight.xlThin;
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeRight).Color = ColorTranslator.ToOle(Color.Black);

                //Add Column Headers
                DataTable DT = DV.ToTable("sptab");
                int ColCount = DT.Columns.Count;
                int RowCount = DV.Count;

                for (int i = 1; i <= ColCount; i++)
                {
                    xlWs.Cells[3, i] = DT.Columns[i - 1].ColumnName;
                }

                TempRange = (Excel.Range)xlWs.Rows[3, Missing.Value];
                TempRange.Font.Size = 10;
                TempRange.Font.Bold = true;
                TempRange.Interior.Color = ColorTranslator.ToOle(Color.Gold);
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeTop).LineStyle = Excel.XlLineStyle.xlContinuous;
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeTop).Weight = Excel.XlBorderWeight.xlThin;
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeTop).Color = ColorTranslator.ToOle(Color.Black);
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeBottom).LineStyle = Excel.XlLineStyle.xlContinuous;
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeBottom).Weight = Excel.XlBorderWeight.xlThin;
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeBottom).Color = ColorTranslator.ToOle(Color.Black);
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeRight).LineStyle = Excel.XlLineStyle.xlContinuous;
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeRight).Weight = Excel.XlBorderWeight.xlMedium;
                TempRange.Borders.get_Item(Excel.XlBordersIndex.xlEdgeRight).Color = ColorTranslator.ToOle(Color.Black);

                //Now Add DataView information
                Stepper.Message = "Loading Excel Sheet...";
                Stepper.MaxRange = RowCount;

                object[,] rowdata = new object[RowCount, ColCount];

                for (int i = 0; i < RowCount; i++)
                {
                    for (int j = 0; j < ColCount; j++)
                    {
                        if (DT.Rows[i].ItemArray.GetValue(j) is string)
                        {
                            string fullvalue = DT.Rows[i].ItemArray.GetValue(j).ToString();
                            int stringlen = fullvalue.Length;
                            if (stringlen > 911)
                            {
                                rowdata[i, j] = fullvalue.Substring(0, 911);
                            }
                            else
                            {
                                rowdata[i, j] = fullvalue;
                            }
                        }
                        else
                            rowdata[i, j] = DT.Rows[i].ItemArray.GetValue(j);
                    }
                    Stepper.Step();
                }

                TempRange = xlWs.get_Range("A4", Missing.Value);
                TempRange = TempRange.get_Resize(RowCount, ColCount);
                TempRange.set_Value(Missing.Value, rowdata);

                //autofit columns
                TempRange = xlWs.get_Range("A3", Missing.Value);
                TempRange = TempRange.get_Resize(RowCount + 1, ColCount);
                TempRange.Columns.AutoFit();

                xlApp.Visible = true;
                xlApp.UserControl = true;


            }

            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                if (xlApp != null)
                {
                    xlApp.DisplayAlerts = false;
                    xlApp.Quit();
                    Marshal.FinalReleaseComObject(xlApp);
                }
            }

            finally
            {
                PD.HideDialog();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
 
 
 
 */
