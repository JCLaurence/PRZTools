using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Data.Topology;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
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
using System.Windows.Input;
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
                if (!FolderExists_Project().exists)
                {
                    return "";
                }

                string logpath = GetPath_ProjectLog();
                if (!ProjectLogExists().exists)
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
                if (!ProjectLogExists().exists)
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
                if (message == null)
                {
                    if (append == false)
                    {
                        pm.Message = "";
                    }
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

        // National GDB
        public static string GetPath_NatGDB()
        {
            try
            {
                return Properties.Settings.Default.NATDB_DBPATH;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // Raster Tools Scratch Geodatabase
        public static string GetPath_RTScratchGDB()
        {
            try
            {
                return Properties.Settings.Default.RT_SCRATCH_FGDB;
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

        /// <summary>
        /// Retrieve the path to the project geodatabase.  Silent errors.
        /// </summary>
        /// <param name="gdb_obj_name"></param>
        /// <returns></returns>
        public static (bool success, string path, string message) GetPath_Project(string gdb_obj_name)
        {
            try
            {
                // Get the GDB Path
                string gdbpath = GetPath_ProjectGDB();

                if (string.IsNullOrEmpty(gdbpath))
                {
                    return (false, "", "geodatabase path is null");
                }

                return (true, Path.Combine(gdbpath, gdb_obj_name), "success");
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }

        /// <summary>
        /// Retrieve the path to the national geodatabase (file or enterprise).  Silent errors.
        /// </summary>
        /// <param name="gdb_obj_name"></param>
        /// <returns></returns>
        public static (bool success, string path, string message) GetPath_Nat(string gdb_obj_name)
        {
            try
            {
                // Get the GDB Path
                string gdbpath = GetPath_NatGDB();

                if (string.IsNullOrEmpty(gdbpath))
                {
                    return (false, "", "geodatabase path is null");
                }

                return (true, Path.Combine(gdbpath, gdb_obj_name), "success");
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }

        /// <summary>
        /// Retrieve the path to the RT Scratch file geodatabase.  Silent errors.
        /// </summary>
        /// <param name="gdb_obj_name"></param>
        /// <returns></returns>
        public static (bool success, string path, string message) GetPath_RTScratch(string gdb_obj_name)
        {
            try
            {
                // Get the GDB Path
                string gdbpath = GetPath_RTScratchGDB();

                if (string.IsNullOrEmpty(gdbpath))
                {
                    return (false, "", "geodatabase path is null");
                }

                return (true, Path.Combine(gdbpath, gdb_obj_name), "success");
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }

        #endregion

        #endregion

        #region OBJECT EXISTENCE

        #region FOLDER EXISTENCE

        /// <summary>
        /// Establish the existence of the project folder.  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static (bool exists, string message) FolderExists_Project()
        {
            try
            {
                string path = GetPath_ProjectFolder();
                bool exists = Directory.Exists(path);

                return (exists, "success");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Establish the existence of the export where-to-work tool folder.  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static (bool exists, string message) FolderExists_ExportWTW()
        {
            try
            {
                string path = GetPath_ExportWTWFolder();
                bool exists = Directory.Exists(path);

                return (exists, "success");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        #endregion

        #region GDB EXISTENCE

        /// <summary>
        /// Establish the existence of a geodatabase (file or enterprise) from a path.  Silent errors.
        /// </summary>
        /// <param name="gdbpath"></param>
        /// <returns></returns>
        public static async Task<(bool exists, GeoDBType gdbType, string message)> GDBExists(string gdbpath)
        {
            try
            {
                // Run this on the worker thread
                return await QueuedTask.Run(() => 
                {
                    // Ensure a non-null and non-empty path
                    if (string.IsNullOrEmpty(gdbpath))
                    {
                        return (false, GeoDBType.Unknown, "Geodatabase path is null or empty.");
                    }

                    // Ensure a rooted path
                    if (!Path.IsPathRooted(gdbpath))
                    {
                        return (false, GeoDBType.Unknown, $"Path is not rooted: {gdbpath}");
                    }

                    // Create the Uri object
                    Uri uri = null;

                    try
                    {
                        uri = new Uri(gdbpath);
                    }
                    catch
                    {
                        return (false, GeoDBType.Unknown, $"Unable to create Uri from path: {gdbpath}");
                    }

                    // Determine if path is file geodatabase (.gdb) or database connection file (.sde)
                    if (Directory.Exists(gdbpath) && gdbpath.EndsWith(".gdb"))  // File Geodatabase (possibly)
                    {
                        // Create the Connection Path object
                        FileGeodatabaseConnectionPath conn = null;

                        try
                        {
                            conn = new FileGeodatabaseConnectionPath(uri);
                        }
                        catch
                        {
                            return (false, GeoDBType.Unknown, $"Unable to create file geodatabase connection path from path: {gdbpath}");
                        }

                        // Try to open the connection
                        try
                        {
                            using (Geodatabase gdb = new Geodatabase(conn)) { }
                        }
                        catch
                        {
                            return (false, GeoDBType.Unknown, $"File geodatabase could not be opened from path: {gdbpath}");
                        }

                        // If I get to this point, the file gdb exists and was successfully opened
                        return (true, GeoDBType.FileGDB, "success");
                    }
                    else if (File.Exists(gdbpath) && gdbpath.EndsWith(".sde"))    // It's a connection file (.sde)
                    {
                        // Create the Connection File object
                        DatabaseConnectionFile conn = null;

                        try
                        {
                            conn = new DatabaseConnectionFile(uri);
                        }
                        catch
                        {
                            return (false, GeoDBType.Unknown, $"Unable to create database connection file from path: {gdbpath}");
                        }

                        // try to open the connection
                        try
                        {
                            using (Geodatabase gdb = new Geodatabase(conn)) { }
                        }
                        catch
                        {
                            return (false, GeoDBType.Unknown, $"Enterprise geodatabase could not be opened from path: {gdbpath}");
                        }

                        // If I get to this point, the enterprise geodatabase exists and was successfully opened
                        return (true, GeoDBType.EnterpriseGDB, "success");
                    }
                    else
                    {
                        // something else, weird!
                        return (false, GeoDBType.Unknown, $"unable to process database path: {gdbpath}");
                    }
                });

            }
            catch (Exception ex)
            {
                return (false, GeoDBType.Unknown, ex.Message);
            }
        }

        /// <summary>
        /// Establish the existence of the project geodatabase.  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static async Task<(bool exists, string message)> GDBExists_Project()
        {
            try
            {
                var tryexists = await GDBExists(GetPath_ProjectGDB());

                return (tryexists.exists, tryexists.message);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Establish the existence of the RT Scratch file geodatabase.  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static async Task<(bool exists, string message)> GDBExists_RTScratch()
        {
            try
            {
                var tryexists = await GDBExists(GetPath_RTScratchGDB());

                return (tryexists.exists, tryexists.message);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Establish the existence of the national geodatabase (file or enterprise).
        /// Geodatabase must exist and be valid (have the required tables).  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static async Task<(bool exists, GeoDBType gdbType, string message)> GDBExists_Nat()
        {
            try
            {
                var tryex = await GDBExists(GetPath_NatGDB());

                if (!tryex.exists)
                {
                    return tryex;
                }

                // Ensure that national geodatabase is valid
                if (!Properties.Settings.Default.NATDB_DBVALID)
                {
                    return (false, GeoDBType.Unknown, "National geodatabase exists but is invalid.");
                }
                else
                {
                    return tryex;
                }
            }
            catch (Exception ex)
            {
                return (false, GeoDBType.Unknown, ex.Message);
            }
        }

        #endregion

        #region FC/TABLE/RASTER ANY GDB

        /// <summary>
        /// Establish the existence of a feature class by name within a specified geodatabase.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="geodatabase"></param>
        /// <param name="fc_name"></param>
        /// <returns></returns>
        public static (bool exists, string message) FCExists(Geodatabase geodatabase, string fc_name)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Attempt to retrieve definition based on name
                using (FeatureClassDefinition fcDef = geodatabase.GetDefinition<FeatureClassDefinition>(fc_name))
                {
                    // Error will be thrown by using statement above if FC of the supplied name doesn't exist in GDB
                }

                return (true, "success");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Establish the existence of a table by name within a specified geodatabase.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="geodatabase"></param>
        /// <param name="table_name"></param>
        /// <returns></returns>
        public static (bool exists, string message) TableExists(Geodatabase geodatabase, string table_name)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Attempt to retrieve definition based on name
                using (TableDefinition tabDef = geodatabase.GetDefinition<TableDefinition>(table_name))
                {
                    // Error will be thrown by using statement above if table of the supplied name doesn't exist in GDB
                }

                return (true, "success");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Establish the existence of a raster dataset by name within a specified geodatabase.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="geodatabase"></param>
        /// <param name="raster_name"></param>
        /// <returns></returns>
        public static (bool exists, string message) RasterExists(Geodatabase geodatabase, string raster_name)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Attempt to retrieve definition based on name
                using (RasterDatasetDefinition rasDef = geodatabase.GetDefinition<RasterDatasetDefinition>(raster_name))
                {
                    // Error will be thrown by using statement above if rasterdataset of the supplied name doesn't exist in GDB
                }

                return (true, "success");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        #endregion

        #region FC/TABLE/RASTER IN PROJECT GDB

        /// <summary>
        /// Establish the existence of a feature class by name from the project file geodatabase.  Silent errors.
        /// </summary>
        /// <param name="fc_name"></param>
        /// <returns></returns>
        public static async Task<(bool exists, string message)> FCExists_Project(string fc_name)
        {
            try
            {
                // Run this code on the worker thread
                return await QueuedTask.Run(() =>
                {
                    // Get geodatabase
                    var tryget_gdb = GetGDB_Project();

                    if (!tryget_gdb.success)
                    {
                        return (false, tryget_gdb.message);
                    }

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    {
                        if (geodatabase == null)
                        {
                            return (false, "unable to access the geodatabase.");
                        }

                        return FCExists(geodatabase, fc_name);
                    }
                });
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Establish the existence of a table by name from the project file geodatabase.  Silent errors.
        /// </summary>
        /// <param name="table_name"></param>
        /// <returns></returns>
        public static async Task<(bool exists, string message)> TableExists_Project(string table_name)
        {
            try
            {
                // Run this code on the worker thread
                return await QueuedTask.Run(() =>
                {
                    // Get geodatabase
                    var tryget_gdb = GetGDB_Project();

                    if (!tryget_gdb.success)
                    {
                        return (false, tryget_gdb.message);
                    }

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    {
                        if (geodatabase == null)
                        {
                            return (false, "unable to access the geodatabase.");
                        }

                        return TableExists(geodatabase, table_name);
                    }
                });
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Establish the existence of a raster dataset by name from the project file geodatabase.  Silent errors.
        /// </summary>
        /// <param name="raster_name"></param>
        /// <returns></returns>
        public static async Task<(bool exists, string message)> RasterExists_Project(string raster_name)
        {
            try
            {
                // Run this code on the worker thread
                return await QueuedTask.Run(() =>
                {
                    // Get geodatabase
                    var tryget_gdb = GetGDB_Project();

                    if (!tryget_gdb.success)
                    {
                        return (false, tryget_gdb.message);
                    }

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    {
                        if (geodatabase == null)
                        {
                            return (false, "unable to access the geodatabase.");
                        }

                        return RasterExists(geodatabase, raster_name);
                    }
                });
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        #endregion

        #region FC/TABLE/RASTER IN RT SCRATCH GDB

        /// <summary>
        /// Establish the existence of a feature class by name from the rt scratch file geodatabase.  Silent errors.
        /// </summary>
        /// <param name="fc_name"></param>
        /// <returns></returns>
        public static async Task<(bool exists, string message)> FCExists_RTScratch(string fc_name)
        {
            try
            {
                // Run this code on the worker thread
                return await QueuedTask.Run(() =>
                {
                    // Get geodatabase
                    var tryget_gdb = GetGDB_RTScratch();

                    if (!tryget_gdb.success)
                    {
                        return (false, tryget_gdb.message);
                    }

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    {
                        if (geodatabase == null)
                        {
                            return (false, "unable to access the geodatabase.");
                        }

                        return FCExists(geodatabase, fc_name);
                    }
                });
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Establish the existence of a table by name from the rt scratch file geodatabase.  Silent errors.
        /// </summary>
        /// <param name="table_name"></param>
        /// <returns></returns>
        public static async Task<(bool exists, string message)> TableExists_RTScratch(string table_name)
        {
            try
            {
                // Run this code on the worker thread
                return await QueuedTask.Run(() =>
                {
                    // Get geodatabase
                    var tryget_gdb = GetGDB_RTScratch();

                    if (!tryget_gdb.success)
                    {
                        return (false, tryget_gdb.message);
                    }

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    {
                        if (geodatabase == null)
                        {
                            return (false, "unable to access the geodatabase.");
                        }

                        return TableExists(geodatabase, table_name);
                    }
                });
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Establish the existence of a raster dataset by name from the rt scratch file geodatabase.  Silent errors.
        /// </summary>
        /// <param name="raster_name"></param>
        /// <returns></returns>
        public static async Task<(bool exists, string message)> RasterExists_RTScratch(string raster_name)
        {
            try
            {
                // Run this code on the worker thread
                return await QueuedTask.Run(() =>
                {
                    // Get geodatabase
                    var tryget_gdb = GetGDB_RTScratch();

                    if (!tryget_gdb.success)
                    {
                        return (false, tryget_gdb.message);
                    }

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    {
                        if (geodatabase == null)
                        {
                            return (false, "unable to access the geodatabase.");
                        }

                        return RasterExists(geodatabase, raster_name);
                    }
                });
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        #endregion

        #region FC/TABLE/RASTER IN NATIONAL GDB

        /// <summary>
        /// Establish the existence of a table by name from the national geodatabase.  Silent errors.
        /// </summary>
        /// <param name="table_name"></param>
        /// <returns></returns>
        public static async Task<(bool exists, string message)> TableExists_Nat(string table_name)
        {
            try
            {
                // Run this code on the worker thread
                return await QueuedTask.Run(() =>
                {
                    // Get geodatabase
                    var tryget_gdb = GetGDB_Nat();

                    if (!tryget_gdb.success)
                    {
                        return (false, tryget_gdb.message);
                    }

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    {
                        if (geodatabase == null)
                        {
                            return (false, "unable to access the geodatabase.");
                        }

                        // fully qualified table name as required
                        SQLSyntax syntax = geodatabase.GetSQLSyntax();

                        string db = Properties.Settings.Default.NATDB_DBNAME;
                        string schema = Properties.Settings.Default.NATDB_SCHEMANAME;

                        string qualified_table_name = syntax.QualifyTableName(db, schema, table_name);

                        return TableExists(geodatabase, qualified_table_name);
                    }
                });
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        #endregion

        #region MISCELLANEOUS

        /// <summary>
        /// Establish the existence of the Planning Unit dataset (feature class or raster dataset)
        /// in the project file geodatabase.  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static async Task<(bool exists, PlanningUnitLayerType puLayerType, string message)> PUExists()
        {
            try
            {
                // Run entire method on the worker thread
                return await QueuedTask.Run(() =>
                {
                    // Try to retrieve the project geodatabase
                    var tryget_gdb = GetGDB_Project();

                    if (!tryget_gdb.success)
                    {
                        return (false, PlanningUnitLayerType.UNKNOWN, tryget_gdb.message);
                    }

                    // Search the geodatabase for a feature/raster planning unit dataset
                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    {
                        if (geodatabase == null)
                        {
                            return (false, PlanningUnitLayerType.UNKNOWN, "Unable to retrieve project gdb.");
                        }

                        // Look for a Planning Unit dataset
                        if (FCExists(geodatabase, PRZC.c_FC_PLANNING_UNITS).exists)
                        {
                            // Found a FC!
                            return (true, PlanningUnitLayerType.FEATURE, "success");
                        }
                        else if (RasterExists(geodatabase, PRZC.c_RAS_PLANNING_UNITS).exists)
                        {
                            // Found a RD!
                            return (true, PlanningUnitLayerType.RASTER, "success");
                        }
                        else
                        {
                            // Found nothing.
                            return (false, PlanningUnitLayerType.UNKNOWN, "No Planning Unit dataset found.");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return (false, PlanningUnitLayerType.UNKNOWN, ex.Message);
            }
        }

        /// <summary>
        /// Establish the existence of a project log file.  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static (bool exists, string message) ProjectLogExists()
        {
            try
            {
                string path = GetPath_ProjectLog();

                bool exists = File.Exists(path);

                return (exists, "success");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        #endregion

        #endregion

        #region GEODATABASE OBJECT RETRIEVAL

        #region GEODATABASES

        /// <summary>
        /// Retrieve a file geodatabase from a path.  Must be run on MCT. Silent errors.
        /// </summary>
        /// <param name="gdbpath"></param>
        /// <returns></returns>
        public static (bool success, Geodatabase geodatabase, string message) GetFileGDB(string gdbpath)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Ensure a non-null and non-empty path
                if (string.IsNullOrEmpty(gdbpath))
                {
                    return (false, null, $"Path is null or empty.");
                }

                // Ensure a rooted path
                if (!Path.IsPathRooted(gdbpath))
                {
                    return (false, null, $"Path is not rooted: {gdbpath}");
                }

                // Ensure the path is an existing directory
                if (!Directory.Exists(gdbpath))
                {
                    return (false, null, $"Path is not a valid folder path.\n{gdbpath}");
                }

                // Create the Uri object
                Uri uri = null;

                try
                {
                    uri = new Uri(gdbpath);
                }
                catch
                {
                    return (false, null, $"Unable to create Uri from path: {gdbpath}");
                }

                // Create the Connection Path object
                FileGeodatabaseConnectionPath connpath = null;

                try
                {
                    connpath = new FileGeodatabaseConnectionPath(uri);
                }
                catch
                {
                    return (false, null, $"Unable to create file geodatabase connection path from path: {gdbpath}");
                }

                // Create the Geodatabase object
                Geodatabase geodatabase = null;

                // Try to open the geodatabase from the connection path
                try
                {
                    geodatabase = new Geodatabase(connpath);
                }
                catch (Exception ex)
                {
                    return (false, null, $"Error opening the geodatabase from the connection path.\n{ex.Message}");
                }

                // If we get to here, the geodatabase has been opened successfully!  Return it!
                return (true, geodatabase, "success");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve an enterprise geodatabase from database connection file path.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="sdepath"></param>
        /// <returns></returns>
        public static (bool success, Geodatabase geodatabase, string message) GetEnterpriseGDB(string sdepath)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Ensure a non-null and non-empty path
                if (string.IsNullOrEmpty(sdepath))
                {
                    return (false, null, "Geodatabase path is null or empty.");
                }

                // Ensure a rooted path
                if (!Path.IsPathRooted(sdepath))
                {
                    return (false, null, $"Path is not rooted: {sdepath}");
                }

                // Ensure path is a valid file
                if (!File.Exists(sdepath))
                {
                    return (false, null, $"Path is not a valid file: {sdepath}");
                }

                // Create the Uri object
                Uri uri = null;

                try
                {
                    uri = new Uri(sdepath);
                }
                catch
                {
                    return (false, null, $"Unable to create Uri from path: {sdepath}");
                }

                // Ensure the path is an existing sde connection file
                if (sdepath.EndsWith(".sde"))
                {
                    // Create the Connection File object
                    DatabaseConnectionFile conn = null;

                    try
                    {
                        conn = new DatabaseConnectionFile(uri);
                    }
                    catch
                    {
                        return (false, null, $"Unable to create database connection file from path: {sdepath}");
                    }

                    // try to open the connection
                    Geodatabase geodatabase = null;

                    try
                    {
                        geodatabase = new Geodatabase(conn);
                    }
                    catch
                    {
                        return (false, null, $"Enterprise geodatabase could not be opened from path: {sdepath}");
                    }

                    // If I get to this point, the enterprise geodatabase exists and was successfully opened
                    return (true, geodatabase, "success");
                }
                else
                {
                    return (false, null, "Unable to process database connection file.");
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a file or enterprise geodatabase from a path.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static (bool success, Geodatabase geodatabase, GeoDBType gdbType, string message) GetGDB(string path)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Ensure a non-null and non-empty path
                if (string.IsNullOrEmpty(path))
                {
                    return (false, null, GeoDBType.Unknown, "Geodatabase path is null or empty.");
                }

                // Ensure a rooted path
                if (!Path.IsPathRooted(path))
                {
                    return (false, null, GeoDBType.Unknown, $"Path is not rooted: {path}");
                }

                if (path.EndsWith(".gdb"))
                {
                    var tryget = GetFileGDB(path);
                    return (tryget.success, tryget.geodatabase, GeoDBType.FileGDB, tryget.message);
                }
                else if (path.EndsWith(".sde"))
                {
                    var tryget = GetEnterpriseGDB(path);
                    return (tryget.success, tryget.geodatabase, GeoDBType.EnterpriseGDB, tryget.message);
                }
                else
                {
                    return (false, null, GeoDBType.Unknown, "Invalid geodatabase path (not *.gdb or *.sde)");
                }
            }
            catch (Exception ex)
            {
                return (false, null, GeoDBType.Unknown, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve the project file geodatabase.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static (bool success, Geodatabase geodatabase, string message) GetGDB_Project()
        {
            try
            {
                // Ensure this is called on the worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Get the Project GDB Path
                string gdbpath = GetPath_ProjectGDB();

                // Retrieve the File Geodatabase
                return GetFileGDB(gdbpath);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve the RT scratch file geodatabase.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static (bool success, Geodatabase geodatabase, string message) GetGDB_RTScratch()
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Get the RT Scratch Geodatabase Path
                string gdbpath = GetPath_RTScratchGDB();

                // Retrieve the File Geodatabase
                return GetFileGDB(gdbpath);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve the national geodatabase (file or enterprise).  Geodatabase must be
        /// valid (e.g. have the required tables).  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static (bool success, Geodatabase geodatabase, GeoDBType gdbType, string message) GetGDB_Nat()
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Get the Nat Geodatabase Path
                string gdbpath = GetPath_NatGDB();

                // Get the Geodatabase
                var tryget = GetGDB(gdbpath);

                if (!tryget.success)
                {
                    return tryget;
                }

                // Ensure geodatabase is valid
                if (!Properties.Settings.Default.NATDB_DBVALID)
                {
                    return (false, null, GeoDBType.Unknown, "Geodatabase exists but is invalid.");
                }
                else
                {
                    return tryget;
                }
            }
            catch (Exception ex)
            {
                return (false, null, GeoDBType.Unknown, ex.Message);
            }
        }

        #endregion

        #region GENERIC GDB OBJECTS

        /// <summary>
        /// Retrieve a feature class by name from a specified geodatabase.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="geodatabase"></param>
        /// <param name="fc_name"></param>
        /// <returns></returns>
        public static (bool success, FeatureClass featureclass, string message) GetFC(Geodatabase geodatabase, string fc_name)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Ensure geodatabase is not null
                if (geodatabase == null)
                {
                    return (false, null, "Null or invalid geodatabase.");
                }

                // Ensure feature class exists
                if (!FCExists(geodatabase, fc_name).exists)
                {
                    return (false, null, "Feature class not found in geodatabase");
                }

                // retrieve feature class
                FeatureClass featureClass = null;

                try
                {
                    featureClass = geodatabase.OpenDataset<FeatureClass>(fc_name);

                    return (true, featureClass, "success");
                }
                catch (Exception ex)
                {
                    return (false, null, $"Error opening {fc_name} feature class.\n{ex.Message}");
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a table by name from a specified geodatabase.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="geodatabase"></param>
        /// <param name="table_name"></param>
        /// <returns></returns>
        public static (bool success, Table table, string message) GetTable(Geodatabase geodatabase, string table_name)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Ensure geodatabase is not null
                if (geodatabase == null)
                {
                    return (false, null, "Null or invalid geodatabase.");
                }

                // Ensure feature class exists
                if (!TableExists(geodatabase, table_name).exists)
                {
                    return (false, null, "Table not found in geodatabase");
                }

                // Retrieve table
                Table table = null;

                try
                {
                    table = geodatabase.OpenDataset<Table>(table_name);

                    return (true, table, "success");
                }
                catch (Exception ex)
                {
                    return (false, null, $"Error opening {table_name} feature class.\n{ex.Message}");
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a raster dataset by name from a specified geodatabase.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="geodatabase"></param>
        /// <param name="raster_name"></param>
        /// <returns></returns>
        public static (bool success, RasterDataset rasterDataset, string message) GetRaster(Geodatabase geodatabase, string raster_name)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Ensure geodatabase is not null
                if (geodatabase == null)
                {
                    return (false, null, "Null or invalid geodatabase.");
                }

                // Ensure feature class exists
                if (!RasterExists(geodatabase, raster_name).exists)
                {
                    return (false, null, "Raster dataset not found in geodatabase");
                }

                // Retrieve raster dataset
                RasterDataset rasterDataset = null;

                try
                {
                    rasterDataset = geodatabase.OpenDataset<RasterDataset>(raster_name);

                    return (true, rasterDataset, "success");
                }
                catch (Exception ex)
                {
                    return (false, null, $"Error opening {raster_name} raster dataset.\n{ex.Message}");
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        #endregion

        #region PROJECT GDB OBJECTS

        /// <summary>
        /// Retrieve a feature class by name from the project file geodatabase.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="fc_name"></param>
        /// <returns></returns>
        public static (bool success, FeatureClass featureclass, string message) GetFC_Project(string fc_name)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Retrieve geodatabase
                var tryget = GetGDB_Project();
                if (!tryget.success)
                {
                    return (false, null, tryget.message);
                }

                using (Geodatabase geodatabase = tryget.geodatabase)
                {
                    // ensure feature class exists
                    if (!FCExists(geodatabase, fc_name).exists)
                    {
                        return (false, null, "Feature class not found in geodatabase");
                    }

                    // get the feature class
                    FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(fc_name);

                    return (true, featureClass, "success");
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a table by name from the project file geodatabase.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="table_name"></param>
        /// <returns></returns>
        public static (bool success, Table table, string message) GetTable_Project(string table_name)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Retrieve geodatabase
                var tryget = GetGDB_Project();
                if (!tryget.success)
                {
                    return (false, null, tryget.message);
                }

                using (Geodatabase geodatabase = tryget.geodatabase)
                {
                    // ensure table exists
                    if (!TableExists(geodatabase, table_name).exists)
                    {
                        return (false, null, "Table not found in geodatabase");
                    }

                    // get the table
                    Table table = geodatabase.OpenDataset<Table>(table_name);

                    return (true, table, "success");
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a raster dataset by name from the project file geodatabase.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="raster_name"></param>
        /// <returns></returns>
        public static (bool success, RasterDataset rasterDataset, string message) GetRaster_Project(string raster_name)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Retrieve geodatabase
                var tryget = GetGDB_Project();
                if (!tryget.success)
                {
                    return (false, null, tryget.message);
                }

                using (Geodatabase geodatabase = tryget.geodatabase)
                {
                    // ensure raster dataset exists
                    if (!RasterExists(geodatabase, raster_name).exists)
                    {
                        return (false, null, "Raster dataset not found in geodatabase");
                    }

                    // get the raster dataset
                    RasterDataset rasterDataset = geodatabase.OpenDataset<RasterDataset>(raster_name);

                    return (true, rasterDataset, "success");
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        #endregion

        #region RT SCRATCH GDB OBJECTS

        /// <summary>
        /// Retrieve a feature class by name from the RT Scratch file geodatabase.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="fc_name"></param>
        /// <returns></returns>
        public static (bool success, FeatureClass featureclass, string message) GetFC_RTScratch(string fc_name)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Retrieve geodatabase
                var tryget = GetGDB_RTScratch();
                if (!tryget.success)
                {
                    return (false, null, tryget.message);
                }

                using (Geodatabase geodatabase = tryget.geodatabase)
                {
                    // ensure feature class exists
                    if (!FCExists(geodatabase, fc_name).exists)
                    {
                        return (false, null, "Feature class not found in geodatabase");
                    }

                    // get the feature class
                    FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(fc_name);

                    return (true, featureClass, "success");
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a table by name from the RT Scratch file geodatabase.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="table_name"></param>
        /// <returns></returns>
        public static (bool success, Table table, string message) GetTable_RTScratch(string table_name)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Retrieve geodatabase
                var tryget = GetGDB_RTScratch();
                if (!tryget.success)
                {
                    return (false, null, tryget.message);
                }

                using (Geodatabase geodatabase = tryget.geodatabase)
                {
                    // ensure table exists
                    if (!TableExists(geodatabase, table_name).exists)
                    {
                        return (false, null, "Table not found in geodatabase");
                    }

                    // get the table
                    Table table = geodatabase.OpenDataset<Table>(table_name);

                    return (true, table, "success");
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a raster dataset by name from the RT Scratch file geodatabase.  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="raster_name"></param>
        /// <returns></returns>
        public static (bool success, RasterDataset rasterDataset, string message) GetRaster_RTScratch(string raster_name)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Retrieve geodatabase
                var tryget = GetGDB_RTScratch();
                if (!tryget.success)
                {
                    return (false, null, tryget.message);
                }

                using (Geodatabase geodatabase = tryget.geodatabase)
                {
                    // ensure raster dataset exists
                    if (!RasterExists(geodatabase, raster_name).exists)
                    {
                        return (false, null, "Raster dataset not found in geodatabase");
                    }

                    // get the raster dataset
                    RasterDataset rasterDataset = geodatabase.OpenDataset<RasterDataset>(raster_name);

                    return (true, rasterDataset, "success");
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        #endregion

        #region NAT GDB OBJECTS

        /// <summary>
        /// Retrieve a table by name from the national geodatabase (file or enterprise).  Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="table_name"></param>
        /// <returns></returns>
        public static (bool success, Table table, string message) GetTable_Nat(string table_name)
        {
            try
            {
                // Ensure this is called on worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Retrieve geodatabase
                var tryget = GetGDB_Nat();
                if (!tryget.success)
                {
                    return (false, null, tryget.message);
                }

                using (Geodatabase geodatabase = tryget.geodatabase)
                {
                    // fully qualified table name as required
                    SQLSyntax syntax = geodatabase.GetSQLSyntax();

                    string db = Properties.Settings.Default.NATDB_DBNAME;
                    string schema = Properties.Settings.Default.NATDB_SCHEMANAME;

                    string qualified_table_name = syntax.QualifyTableName(db, schema, table_name);

                    // ensure table exists
                    if (!TableExists(geodatabase, qualified_table_name).exists)
                    {
                        return (false, null, "Table not found in geodatabase");
                    }

                    // get the table
                    Table table = geodatabase.OpenDataset<Table>(qualified_table_name);

                    return (true, table, "success");
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        #endregion

        #region MISCELLANEOUS

        public static async Task<(bool success, string qualified_name, string message)> GetNatDBQualifiedName(string name)
        {
            try
            {
                // Ensure the national database is valid
                bool valid = Properties.Settings.Default.NATDB_DBVALID;

                if (!valid)
                {
                    return (false, "", "invalid national database");
                }

                // Construct the qualified name
                string db = Properties.Settings.Default.NATDB_DBNAME;
                string schema = Properties.Settings.Default.NATDB_SCHEMANAME;

                string qualified_name = "";

                await QueuedTask.Run(() =>
                {
                    // Get the nat gdb
                    var tryget = GetGDB_Nat();
                    if (!tryget.success)
                    {
                        throw new Exception("Error retrieving valid national geodatabase.");
                    }

                    using (Geodatabase geodatabase = tryget.geodatabase)
                    {
                        SQLSyntax syntax = geodatabase.GetSQLSyntax();
                        qualified_name = syntax.QualifyTableName(db, schema, name);
                    }
                });

                return (true, qualified_name, "success");
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }

        #endregion

        #endregion

        #region PRZ LISTS AND DICTIONARIES

        #region NATIONAL TABLES

        /// <summary>
        /// Returns the national element table name for the supplied element id.  Silent errors.
        /// </summary>
        /// <param name="element_id"></param>
        /// <returns></returns>
        public static (bool success, string table_name, string message) GetElementTableName(int element_id)
        {
            try
            {
                if (element_id > 99999 || element_id < 1)
                {
                    throw new Exception($"Element ID {element_id} is out of range (1 to 99999)");
                }
                else
                {
                    return (true, PRZC.c_TABLE_NAT_PREFIX_ELEMENT + element_id.ToString("D5"), "success");
                }
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a list of NatTheme objects from the project geodatabase.  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static async Task<(bool success, List<NatTheme> themes, string message)> GetNationalThemes()
        {
            try
            {
                // Check for Project GDB
                var try_gdbexists = await GDBExists_Project();
                if (!try_gdbexists.exists)
                {
                    return (false, null, try_gdbexists.message);
                }

                // Check for existence of Theme table
                if (!(await TableExists_Project(PRZC.c_TABLE_NAT_THEMES)).exists)
                {
                    return (false, null, $"{PRZC.c_TABLE_NAT_THEMES} table not found in project geodatabase");
                }

                // Create the list
                List<NatTheme> themes = new List<NatTheme>();

                // Populate the list
                (bool success, string message) outcome = await QueuedTask.Run(() =>
                {
                    var tryget = GetTable_Project(PRZC.c_TABLE_NAT_THEMES);
                    if (!tryget.success)
                    {
                        throw new Exception("Error retrieving table.");
                    }

                    using (Table table = tryget.table)
                    using (RowCursor rowCursor = table.Search())
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                int id = Convert.ToInt32(row[PRZC.c_FLD_TAB_THEME_THEME_ID]);
                                string name = (string)row[PRZC.c_FLD_TAB_THEME_NAME];
                                string code = (string)row[PRZC.c_FLD_TAB_THEME_CODE];
                                int theme_presence = Convert.ToInt32(row[PRZC.c_FLD_TAB_THEME_PRESENCE]);

                                if (id > 0 && !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(code))
                                {
                                    NatTheme theme = new NatTheme()
                                    {
                                        ThemeID = id,
                                        ThemeName = name,
                                        ThemeCode = code,
                                        ThemePresence = theme_presence
                                    };

                                    themes.Add(theme);
                                }
                            }
                        }
                    }

                    return (true, "success");
                });

                if (outcome.success)
                {
                    // Sort the list by theme id
                    themes.Sort((a, b) => a.ThemeID.CompareTo(b.ThemeID));

                    return (true, themes, "success");
                }
                else
                {
                    return (false, null, outcome.message);
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a list of NatTheme objects from the project geodatabase, optionally filtered
        /// by the presence indicator.  Silent errors.
        /// </summary>
        /// <param name="presence"></param>
        /// <returns></returns>
        public static async Task<(bool success, List<NatTheme> themes, string message)> GetNationalThemes(NationalThemePresence? presence)
        {
            try
            {
                // Get the full Theme list
                var tryget = await GetNationalThemes();

                if (!tryget.success)
                {
                    return (false, null, tryget.message);
                }

                List<NatTheme> themes = tryget.themes;

                // Filter the list based on filter criteria:

                // By Presence
                IEnumerable<NatTheme> v = (presence != null) ? themes.Where(t => t.ThemePresence == ((int)presence)) : themes;

                // Sort by Theme ID
                v.OrderBy(t => t.ThemeID);

                return (true, v.ToList(), "success");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a list of NatElement objects from the project geodatabase.  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static async Task<(bool success, List<NatElement> elements, string message)> GetNationalElements()
        {
            try
            {
                // Check for Project GDB
                var try_gdbexists = await GDBExists_Project();
                if (!try_gdbexists.exists)
                {
                    return (false, null, try_gdbexists.message);
                }

                // Check for existence of Element table
                if (!(await TableExists_Project(PRZC.c_TABLE_NAT_ELEMENTS)).exists)
                {
                    return (false, null, $"{PRZC.c_TABLE_NAT_ELEMENTS} table not found in project geodatabase");
                }

                // Create list
                List<NatElement> elements = new List<NatElement>();

                // Populate the list
                (bool success, string message) element_outcome = await QueuedTask.Run(() =>
                {
                    var tryget = GetTable_Project(PRZC.c_TABLE_NAT_ELEMENTS);
                    if (!tryget.success)
                    {
                        throw new Exception("Error retrieving table.");
                    }

                    using (Table table = tryget.table)
                    using (RowCursor rowCursor = table.Search())
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                int id = Convert.ToInt32(row[PRZC.c_FLD_TAB_ELEMENT_ELEMENT_ID]);
                                string name = (string)row[PRZC.c_FLD_TAB_ELEMENT_NAME];
                                int elem_type = Convert.ToInt32(row[PRZC.c_FLD_TAB_ELEMENT_TYPE]);
                                int elem_status = Convert.ToInt32(row[PRZC.c_FLD_TAB_ELEMENT_STATUS]);
                                string data_path = (string)row[PRZC.c_FLD_TAB_ELEMENT_DATAPATH];
                                int theme_id = Convert.ToInt32(row[PRZC.c_FLD_TAB_ELEMENT_THEME_ID]);
                                int elem_presence = Convert.ToInt32(row[PRZC.c_FLD_TAB_ELEMENT_PRESENCE]);

                                if (id > 0 && elem_type > 0 && elem_status > 0 && theme_id > 0 && !string.IsNullOrEmpty(name))
                                {
                                    NatElement element = new NatElement()
                                    {
                                        ElementID = id,
                                        ElementName = name,
                                        ElementType = elem_type,
                                        ElementStatus = elem_status,
                                        ElementDataPath = data_path,
                                        ThemeID = theme_id,
                                        ElementPresence = elem_presence
                                    };

                                    elements.Add(element);
                                }
                            }
                        }
                    }

                    return (true, "success");
                });

                if (!element_outcome.success)
                {
                    return (false, null, element_outcome.message);
                }

                // Populate the Theme Information
                var theme_outcome = await GetNationalThemes();
                if (!theme_outcome.success)
                {
                    return (false, null, theme_outcome.message);
                }

                List<NatTheme> themes = theme_outcome.themes;

                foreach (NatElement element in elements)
                {
                    int theme_id = element.ThemeID;

                    if (theme_id < 1)
                    {
                        element.ThemeName = "INVALID THEME ID";
                        element.ThemeCode = "---";
                    }
                    else
                    {
                        NatTheme theme = themes.FirstOrDefault(t => t.ThemeID == theme_id);

                        if (theme != null)
                        {
                            element.ThemeName = theme.ThemeName;
                            element.ThemeCode = theme.ThemeCode;
                        }
                        else
                        {
                            element.ThemeName = "NO CORRESPONDING THEME";
                            element.ThemeCode = "???";
                        }
                    }
                }

                // Sort the list
                elements.Sort((a, b) => a.ElementID.CompareTo(b.ElementID));

                return (true, elements, "success");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a list of NatElement objects from the project geodatabase, optionally filtered
        /// by type, status, or presence indicators.  Silent errors.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="status"></param>
        /// <param name="presence"></param>
        /// <returns></returns>
        public static async Task<(bool success, List<NatElement> elements, string message)> GetNationalElements(NationalElementType? type, NationalElementStatus? status, NationalElementPresence? presence)
        {
            try
            {
                // Get the full Elements list
                var tryget = await GetNationalElements();

                if (!tryget.success)
                {
                    return (false, null, tryget.message);
                }

                List<NatElement> elements = tryget.elements;

                // Filter the list based on filter criteria:

                // By Type
                IEnumerable<NatElement> v = (type != null) ? elements.Where(e => e.ElementType == ((int)type)) : elements;

                // By Status
                v = (status != null) ? v.Where(e => e.ElementStatus == ((int)status)) : v;

                // By Presence
                v = (presence != null) ? v.Where(e => e.ElementPresence == ((int)presence)) : v;

                // Sort by Element ID
                v.OrderBy(e => e.ElementID);

                return (true, v.ToList(), "success");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        #endregion

        #region ELEMENT VALUES

        /// <summary>
        /// Retrieve a national grid value for a specified element and cell number.  Silent errors.
        /// </summary>
        /// <param name="element_id"></param>
        /// <param name="cell_number"></param>
        /// <returns></returns>
        public static async Task<(bool success, double value, string message)> GetValueFromElementTable_CellNum(int element_id, long cell_number)
        {
            double value = -9999;

            try
            {
                // Ensure valid element id
                if (element_id < 1 || element_id > 99999)
                {
                    return (false, value, "Element ID out of range (1 - 99999)");
                }

                // Get element table name
                var trygettab = GetElementTableName(element_id);
                if (!trygettab.success)
                {
                    return (false, value, "Unable to retrieve element table name");
                }

                string table_name = trygettab.table_name;

                // Check for Project GDB
                if (!(await GDBExists_Project()).exists)
                {
                    return (false, value, "Project GDB not found.");
                }

                // Verify that table exists in project GDB
                if (!(await TableExists_Project(table_name)).exists)
                {
                    return (false, value, $"Element table {table_name} not found in project geodatabase");
                }

                // retrieve the value for the cell number in the element table
                (bool success, string message) outcome = await QueuedTask.Run(() =>
                {
                    QueryFilter queryFilter = new QueryFilter
                    {
                        WhereClause = PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER + " = " + cell_number
                    };

                    var tryget = GetTable_Project(table_name);
                    if (!tryget.success)
                    {
                        throw new Exception("Error retrieving table.");
                    }

                    using (Table table = tryget.table)
                    {
                        // Row Count
                        int rows = table.GetCount(queryFilter);

                        if (rows == 1)
                        {
                            using (RowCursor rowCursor = table.Search(queryFilter))
                            {
                                if (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        value = Convert.ToDouble(row[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_VALUE]);
                                        return (true, "success");
                                    }
                                }
                                else
                                {
                                    return (false, "no match found");
                                }
                            }
                        }
                        else if (rows == 0)
                        {
                            return (false, "no match found");
                        }
                        else if (rows > 1)
                        {
                            return (false, "more than one matching cell number found");
                        }
                        else
                        {
                            return (false, "there was a resounding kaboom");
                        }
                    }
                });

                if (outcome.success)
                {
                    return (true, value, "success");
                }
                else
                {
                    return (false, value, outcome.message);
                }
            }
            catch (Exception ex)
            {
                return (false, value, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a national grid value for a specified element and planning unit id.  Silent errors.
        /// </summary>
        /// <param name="element_id"></param>
        /// <param name="puid"></param>
        /// <returns></returns>
        public static async Task<(bool success, double value, string message)> GetValueFromElementTable_PUID(int element_id, int puid)
        {
            double value = -9999;

            try
            {
                // Ensure valid element id
                if (element_id < 1 || element_id > 99999)
                {
                    return (false, value, "Element ID out of range (1 - 99999)");
                }

                // Get element table name
                var trygetname = GetElementTableName(element_id);

                if (!trygetname.success)
                {
                    return (false, value, "Unable to retrieve element table name");
                }

                string table_name = trygetname.table_name;

                // Check for Project GDB
                var try_gdbexists = await GDBExists_Project();
                if (!try_gdbexists.exists)
                {
                    return (false, value, "Project GDB not found.");
                }

                // Verify that table exists in project GDB
                if (!(await TableExists_Project(table_name)).exists)
                {
                    return (false, value, $"Element table {table_name} not found in project geodatabase");
                }

                // Retrieve the cell number for the provided puid
                var result = await GetCellNumberFromPUID(puid);
                if (!result.success)
                {
                    return (false, value, result.message);
                }

                long cell_number = result.cell_number;

                // retrieve the value for the cell number in the element table
                (bool success, string message) outcome = await QueuedTask.Run(() =>
                {
                    QueryFilter queryFilter = new QueryFilter
                    {
                        WhereClause = PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER + " = " + cell_number
                    };

                    var tryget = GetTable_Project(table_name);
                    if (!tryget.success)
                    {
                        throw new Exception("Error retrieving the table.");
                    }

                    using (Table table = tryget.table)
                    {
                        // Row Count
                        int rows = table.GetCount(queryFilter);

                        if (rows == 1)
                        {
                            using (RowCursor rowCursor = table.Search(queryFilter))
                            {
                                if (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        value = Convert.ToDouble(row[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_VALUE]);
                                        return (true, "success");
                                    }
                                }
                                else
                                {
                                    return (false, "no match found");
                                }
                            }
                        }
                        else if (rows == 0)
                        {
                            return (false, "no match found");
                        }
                        else if (rows > 1)
                        {
                            return (false, "more than one matching cell number found");
                        }
                        else
                        {
                            return (false, "there was a resounding kaboom");
                        }
                    }
                });

                if (outcome.success)
                {
                    return (true, value, "success");
                }
                else
                {
                    return (false, value, outcome.message);
                }
            }
            catch (Exception ex)
            {
                return (false, value, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a dictionary of cell numbers and associated element values from the
        /// project geodatabase, for the specified element.  Silent errors.
        /// </summary>
        /// <param name="element_id"></param>
        /// <returns></returns>
        public static async Task<(bool success, Dictionary<long, double> dict, string message)> GetValuesFromElementTable_CellNum(int element_id)
        {
            try
            {
                // Ensure valid element id
                if (element_id < 1 || element_id > 99999)
                {
                    return (false, null, "Element ID out of range (1 - 99999)");
                }

                // Get element table name
                var trygetname = GetElementTableName(element_id);

                if (!trygetname.success)
                {
                    return (false, null, "Unable to retrieve element table name");
                }

                string table_name = trygetname.table_name;

                // Check for Project GDB
                var try_gdbexists = await GDBExists_Project();
                if (!try_gdbexists.exists)
                {
                    return (false, null, "Project GDB not found.");
                }

                // Verify that table exists in project GDB
                if (!(await TableExists_Project(table_name)).exists)
                {
                    return (false, null, $"Element table {table_name} not found in project geodatabase");
                }

                // Create the dictionary
                Dictionary<long, double> dict = new Dictionary<long, double>();

                // Populate the dictionary
                await QueuedTask.Run(() =>
                {
                    var tryget = GetTable_Project(table_name);
                    if (!tryget.success)
                    {
                        throw new Exception("Error retrieving table.");
                    }

                    using (Table table = tryget.table)
                    using (RowCursor rowCursor = table.Search())
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                long cellnum = Convert.ToInt64(row[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER]);
                                double cellval = Convert.ToDouble(row[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_VALUE]);

                                if (cellnum > 0 && !dict.ContainsKey(cellnum))
                                {
                                    dict.Add(cellnum, cellval);
                                }
                            }
                        }
                    }
                });

                return (true, dict, "success");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a dictionary of planning unit ids and associated element values from the 
        /// project geodatabase, for the specified element id.  Silent errors.
        /// </summary>
        /// <param name="element_id"></param>
        /// <returns></returns>
        public static async Task<(bool success, Dictionary<int, double> dict, string message)> GetValuesFromElementTable_PUID(int element_id)
        {
            try
            {
                // Ensure valid element id
                if (element_id < 1 || element_id > 99999)
                {
                    return (false, null, "Element ID out of range (1 - 99999)");
                }

                // Get element table name
                var trygetname = GetElementTableName(element_id);

                if (!trygetname.success)
                {
                    return (false, null, "Unable to retrieve element table name");
                }

                string table_name = trygetname.table_name;

                // Check for Project GDB
                var try_gdbexists = await GDBExists_Project();
                if (!try_gdbexists.exists)
                {
                    return (false, null, "Project GDB not found.");
                }

                // Verify that table exists in project GDB
                if (!(await TableExists_Project(table_name)).exists)
                {
                    return (false, null, $"Element table {table_name} not found in project geodatabase");
                }

                // Get the dictionary of Cell Numbers > PUIDs
                var outcome = await GetCellNumbersAndPUIDs();
                if (!outcome.success)
                {
                    return (false, null, $"Unable to retrieve Cell Number dictionary\n{outcome.message}");
                }
                Dictionary<long, int> cellnumdict = outcome.dict;

                // Create the dictionary
                Dictionary<int, double> dict = new Dictionary<int, double>();

                // Populate the dictionary
                (bool success, string message) result = await QueuedTask.Run(() =>
                {
                    var tryget = GetTable_Project(table_name);
                    if (!tryget.success)
                    {
                        throw new Exception("Error retrieving table.");
                    }

                    using (Table table = tryget.table)
                    using (RowCursor rowCursor = table.Search())
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                long cellnum = Convert.ToInt64(row[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER]);
                                double cellval = Convert.ToDouble(row[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_VALUE]);

                                if (cellnum > 0)
                                {
                                    if (cellnumdict.ContainsKey(cellnum))
                                    {
                                        int puid = cellnumdict[cellnum];

                                        if (puid > 0 && !dict.ContainsKey(puid))
                                        {
                                            dict.Add(puid, cellval);
                                        }
                                    }
                                    else
                                    {
                                        return (false, $"No matching puid for cell number {cellnum}");
                                    }
                                }
                            }
                        }
                    }

                    return (true, "success");
                });

                if (result.success)
                {
                    return (true, dict, "success");
                }
                else
                {
                    return (false, null, result.message);
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a dictionary of cell numbers and associated element values from the
        /// national database, for the specified element and list of cell numbers.  Silent errors.
        /// </summary>
        /// <param name="element_id"></param>
        /// <param name="cell_numbers"></param>
        /// <returns></returns>
        public static async Task<(bool success, Dictionary<long, double> dict, string message)> GetElementIntersectionOld(int element_id, HashSet<long> cell_numbers)
        {
            try
            {
                // Ensure valid element id
                if (element_id < 1 || element_id > 99999)
                {
                    return (false, null, "Element ID out of range (1 - 99999)");
                }

                // Get element table name
                var trygetname = GetElementTableName(element_id);
                if (!trygetname.success)
                {
                    return (false, null, "Unable to retrieve element table name");
                }
                string table_name = trygetname.table_name;  // unqualified table name

                // Create the dictionary
                Dictionary<long, double> dict = new Dictionary<long, double>();

                // Populate dictionary
                await QueuedTask.Run(() =>
                {
                    // try getting the e0000n table
                    var trygettab = GetTable_Nat(table_name);

                    if (!trygettab.success)
                    {
                        throw new Exception("Unable to retrieve table.");
                    }

                    // I'm here - can I do this more efficiently?

                    // iterate
                    using (Table table = trygettab.table)
                    using (RowCursor rowCursor = table.Search())
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                var cell_number = Convert.ToInt64(row[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER]);
                                var cell_value = Convert.ToDouble(row[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_VALUE]);

                                // check for presence of cell_number in the hashset
                                if (cell_numbers.Contains(cell_number))
                                {
                                    // save the KVP
                                    dict.Add(cell_number, cell_value);
                                }
                            }
                        }
                    }
                });

                return (true, dict, "success");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a dictionary of cell numbers and associated element values from the
        /// national database, for the specified element and list of cell numbers.  Silent errors.
        /// </summary>
        /// <param name="element_id"></param>
        /// <param name="cell_numbers"></param>
        /// <returns></returns>
        public static async Task<(bool success, Dictionary<long, double> dict, string message)> GetElementIntersectionOld2(int element_id, HashSet<long> cell_numbers)
        {
            try
            {
                // Ensure valid element id
                if (element_id < 1 || element_id > 99999)
                {
                    return (false, null, "Element ID out of range (1 - 99999)");
                }

                // Get element table name
                var trygetname = GetElementTableName(element_id);
                if (!trygetname.success)
                {
                    return (false, null, "Unable to retrieve element table name");
                }
                string table_name = trygetname.table_name;  // unqualified table name

                // Create the dictionary
                Dictionary<long, double> dict = new Dictionary<long, double>();

                // Get the min and max cell numbers.  I can ignore all cell numbers outside this range
                long min_cell_number = cell_numbers.Min();
                long max_cell_number = cell_numbers.Max();

                // Populate dictionary
                await QueuedTask.Run(() =>
                {
                    // try getting the e0000n table
                    var trygettab = GetTable_Nat(table_name);

                    if (!trygettab.success)
                    {
                        throw new Exception("Unable to retrieve table.");
                    }

                    // iterate
                    using (Table table = trygettab.table)
                    {
                        foreach(long cell_num in cell_numbers)
                        {
                            QueryFilter queryFilter = new QueryFilter();

                            queryFilter.SubFields = PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER + "," + PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_VALUE;
                            //                            queryFilter.WhereClause = $"{PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER} = {cell_num} And {} >= {} And {} <= {}"; 
                            queryFilter.WhereClause = $"{PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER} = {cell_num}"; 

                            using (RowCursor rowCursor = table.Search(queryFilter))
                            {
                                if (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        var cell_value = Convert.ToDouble(row[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_VALUE]);
                                        dict.Add(cell_num, cell_value);
                                    }
                                }
                            }
                        }
                    }
                });

                return (true, dict, "success");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a dictionary of cell numbers and associated element values from the
        /// national database, for the specified element and list of cell numbers.  Silent errors.
        /// </summary>
        /// <param name="element_id"></param>
        /// <param name="cell_numbers"></param>
        /// <returns></returns>
        public static async Task<(bool success, Dictionary<long, double> dict, string message)> GetElementIntersection(int element_id, HashSet<long> cell_numbers)
        {
            try
            {
                // Ensure valid element id
                if (element_id < 1 || element_id > 99999)
                {
                    return (false, null, "Element ID out of range (1 - 99999)");
                }

                // Get element table name
                var trygetname = GetElementTableName(element_id);
                if (!trygetname.success)
                {
                    return (false, null, "Unable to retrieve element table name");
                }
                string table_name = trygetname.table_name;  // unqualified table name

                // Create the dictionaries
                Dictionary<long, double> dict_final = new Dictionary<long, double>();
                Dictionary<long, double> dict_test = new Dictionary<long, double>();

                // Get the min and max cell numbers.  I can ignore all cell numbers outside this range
                long min_cell_number = cell_numbers.Min();
                long max_cell_number = cell_numbers.Max();

                // Populate dictionary
                await QueuedTask.Run(() =>
                {
                    // try getting the e0000n table
                    var trygettab = GetTable_Nat(table_name);

                    if (!trygettab.success)
                    {
                        throw new Exception("Unable to retrieve table.");
                    }

                    // Retrieve all element table KVPs within the min max range
                    using (Table table = trygettab.table)
                    {
                        QueryFilter queryFilter = new QueryFilter();
                        queryFilter.SubFields = PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER + "," + PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_VALUE;
                        queryFilter.WhereClause = $"{PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER} BETWEEN {min_cell_number} AND {max_cell_number}";

                        using (RowCursor rowCursor = table.Search(queryFilter))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    var cn = Convert.ToInt64(row[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER]);
                                    var cv = Convert.ToDouble(row[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_VALUE]);

                                    dict_test.Add(cn, cv);
                                }
                            }
                        }
                    }
                });

                // Populate the final dictionary
                foreach (long cellnum in cell_numbers)
                {
                    if (dict_test.ContainsKey(cellnum))
                    {
                        dict_final.Add(cellnum, dict_test[cellnum]);
                    }
                }

                return (true, dict_final, "success");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        #endregion

        #region PUID AND CELL NUMBERS

        #region SINGLE VALUES

        /// <summary>
        /// Retrieve the national grid cell number associated with the specified planning unit id.  Silent errors.
        /// </summary>
        /// <param name="puid"></param>
        /// <returns></returns>
        public static async Task<(bool success, long cell_number, string message)> GetCellNumberFromPUID(int puid)
        {
            long cell_number = -9999;

            try
            {
                // Check for Project GDB
                var try_gdbexists = await GDBExists_Project();
                if (!try_gdbexists.exists)
                {
                    return (false, cell_number, "Project GDB not found.");
                }

                // Check for PU
                var pu_result = await PUExists();
                if (!pu_result.exists)
                {
                    return (false, cell_number, "Planning Unit dataset not found.");
                }

                // Get the Cell Number
                (bool success, string message) outcome = await QueuedTask.Run(async () =>
                {
                    if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                    {
                        if (!(await FCExists_Project(PRZC.c_FC_PLANNING_UNITS)).exists)
                        {
                            return (false, "PU feature class not found");
                        }

                        QueryFilter queryFilter = new QueryFilter()
                        {
                            SubFields = PRZC.c_FLD_FC_PU_ID + "," + PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER,
                            WhereClause = $"{PRZC.c_FLD_FC_PU_ID} = {puid}"
                        };

                        var tryget = GetFC_Project(PRZC.c_FC_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            throw new Exception("Error retrieving feature class.");
                        }

                        using (Table table = tryget.featureclass)
                        {
                            // Row Count
                            int rows = table.GetCount(queryFilter);

                            if (rows == 1)
                            {
                                using (RowCursor rowCursor = table.Search(queryFilter))
                                {
                                    if (rowCursor.MoveNext())
                                    {
                                        using (Row row = rowCursor.Current)
                                        {
                                            cell_number = Convert.ToInt64(row[PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER]);

                                            if (cell_number > 0)
                                            {
                                                return (true, "success");
                                            }
                                            else
                                            {
                                                return (false, "cell number value is zero or lower.");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        return (false, "no matching puid found");
                                    }
                                }
                            }
                            else if (rows == 0)
                            {
                                return (false, "no matching puid found");
                            }
                            else if (rows > 1)
                            {
                                return (false, "more than one matching puid found");
                            }
                            else
                            {
                                return (false, "there was a resounding kaboom");
                            }
                        }
                    }
                    else if (pu_result.puLayerType == PlanningUnitLayerType.RASTER)
                    {
                        if (!(await RasterExists_Project(PRZC.c_RAS_PLANNING_UNITS)).exists)
                        {
                            return (false, "PU raster dataset not found");
                        }

                        QueryFilter queryFilter = new QueryFilter()
                        {
                            SubFields = PRZC.c_FLD_RAS_PU_ID + "," + PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER,
                            WhereClause = $"{PRZC.c_FLD_RAS_PU_ID} = {puid}"
                        };

                        var tryget = GetRaster_Project(PRZC.c_RAS_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            return (false, "Error retrieving raster planning units");
                        }

                        using (RasterDataset rasterDataset = tryget.rasterDataset)
                        using (Raster raster = rasterDataset.CreateFullRaster())
                        using (Table table = raster.GetAttributeTable())
                        {
                            // Row Count
                            int rows = table.GetCount(queryFilter);

                            if (rows == 1)
                            {
                                using (RowCursor rowCursor = table.Search(queryFilter))
                                {
                                    if (rowCursor.MoveNext())
                                    {
                                        using (Row row = rowCursor.Current)
                                        {
                                            cell_number = Convert.ToInt64(row[PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER]);

                                            if (cell_number > 0)
                                            {
                                                return (true, "success");
                                            }
                                            else
                                            {
                                                return (false, "cell number value is zero or lower.");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        return (false, "no matching puid found");
                                    }
                                }
                            }
                            else if (rows == 0)
                            {
                                return (false, "no matching puid found");
                            }
                            else if (rows > 1)
                            {
                                return (false, "more than one matching puid found");
                            }
                            else
                            {
                                return (false, "there was a resounding kaboom");
                            }
                        }
                    }
                    else
                    {
                        return (false, "there was a resounding kaboom");
                    }
                });

                if (outcome.success)
                {
                    return (true, cell_number, "success");
                }
                else
                {
                    return (false, cell_number, outcome.message);
                }
            }
            catch (Exception ex)
            {
                return (false, cell_number, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve the planning unit id associated with the specified national grid cell number.  Silent errors.
        /// </summary>
        /// <param name="cell_number"></param>
        /// <returns></returns>
        public static async Task<(bool success, int puid, string message)> GetPUIDFromCellNumber(long cell_number)
        {
            int puid = -9999;

            try
            {
                // Check for Project GDB
                var try_gdbexists = await GDBExists_Project();
                if (!try_gdbexists.exists)
                {
                    return (false, puid, try_gdbexists.message);
                }

                // Check for PU
                var pu_result = await PUExists();
                if (!pu_result.exists)
                {
                    return (false, puid, "Planning Unit dataset not found.");
                }

                // Get the PUID
                (bool success, string message) outcome = await QueuedTask.Run(async () =>
                {
                    if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                    {
                        if (!(await FCExists_Project(PRZC.c_FC_PLANNING_UNITS)).exists)
                        {
                            return (false, "PU feature class not found");
                        }

                        QueryFilter queryFilter = new QueryFilter()
                        {
                            SubFields = PRZC.c_FLD_FC_PU_ID + "," + PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER,
                            WhereClause = $"{PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER} = {cell_number}"
                        };

                        var tryget = GetFC_Project(PRZC.c_FC_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            throw new Exception("Error retrieving feature class.");
                        }

                        using (Table table = tryget.featureclass)
                        {
                            // Row Count
                            int rows = table.GetCount(queryFilter);

                            if (rows == 1)
                            {
                                using (RowCursor rowCursor = table.Search(queryFilter))
                                {
                                    if (rowCursor.MoveNext())
                                    {
                                        using (Row row = rowCursor.Current)
                                        {
                                            puid = Convert.ToInt32(row[PRZC.c_FLD_FC_PU_ID]);

                                            if (puid > 0)
                                            {
                                                return (true, "success");
                                            }
                                            else
                                            {
                                                return (false, "PUID value is zero or lower.");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        return (false, "no matching cell number found");
                                    }
                                }
                            }
                            else if (rows == 0)
                            {
                                return (false, "no matching cell number found");
                            }
                            else if (rows > 1)
                            {
                                return (false, "more than one matching cell number found");
                            }
                            else
                            {
                                return (false, "there was a resounding kaboom");
                            }
                        }
                    }
                    else if (pu_result.puLayerType == PlanningUnitLayerType.RASTER)
                    {
                        if (!(await RasterExists_Project(PRZC.c_RAS_PLANNING_UNITS)).exists)
                        {
                            return (false, "PU raster dataset not found");
                        }

                        QueryFilter queryFilter = new QueryFilter()
                        {
                            SubFields = PRZC.c_FLD_RAS_PU_ID + "," + PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER,
                            WhereClause = $"{PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER} = {cell_number}"
                        };

                        var tryget = GetRaster_Project(PRZC.c_RAS_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            return (false, "Error retrieving raster planning units");
                        }

                        using (RasterDataset rasterDataset = tryget.rasterDataset)
                        using (Raster raster = rasterDataset.CreateFullRaster())
                        using (Table table = raster.GetAttributeTable())
                        {
                            // Row Count
                            int rows = table.GetCount(queryFilter);

                            if (rows == 1)
                            {
                                using (RowCursor rowCursor = table.Search(queryFilter))
                                {
                                    if (rowCursor.MoveNext())
                                    {
                                        using (Row row = rowCursor.Current)
                                        {
                                            puid = Convert.ToInt32(row[PRZC.c_FLD_RAS_PU_ID]);

                                            if (puid > 0)
                                            {
                                                return (true, "success");
                                            }
                                            else
                                            {
                                                return (false, "puid value is zero or lower.");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        return (false, "no matching cell number found");
                                    }
                                }
                            }
                            else if (rows == 0)
                            {
                                return (false, "no matching cell number found");
                            }
                            else if (rows > 1)
                            {
                                return (false, "more than one matching cell number found");
                            }
                            else
                            {
                                return (false, "there was a resounding kaboom");
                            }
                        }
                    }
                    else
                    {
                        return (false, "there was a resounding kaboom");
                    }
                });

                if (outcome.success)
                {
                    return (true, puid, "success");
                }
                else
                {
                    return (false, puid, outcome.message);
                }
            }
            catch (Exception ex)
            {
                return (false, puid, ex.Message);
            }
        }

        #endregion

        #region LISTS AND HASHSETS

        /// <summary>
        /// Retrieves a Hashset of Planning Unit IDs from the existing Planning Unit dataset (either Feature or Raster).
        /// Return value follows the success (bool), object (hashset), string (message) pattern.
        /// success == false means the hashset was not retrieved, and the message string explains why.
        /// </summary>
        /// <returns></returns>
        public static async Task<(bool success, HashSet<int> puids, string message)> GetPUIDHashset()
        {
            try
            {
                // Check for Project GDB
                var try_gdbexists = await GDBExists_Project();
                if (!try_gdbexists.exists)
                {
                    return (false, null, "Project GDB not found.");
                }

                // Check for PU
                var pu_result = await PUExists();
                if (!pu_result.exists)
                {
                    return (false, null, "Planning Unit dataset not found.");
                }

                // Create the hashset
                HashSet<int> puids = new HashSet<int>();

                // Populate the hashset
                (bool success, string message) outcome = await QueuedTask.Run(async () =>
                {
                    if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                    {
                        if (!(await FCExists_Project(PRZC.c_FC_PLANNING_UNITS)).exists)
                        {
                            return (false, "PU feature class not found");
                        }

                        QueryFilter queryFilter = new QueryFilter()
                        {
                            SubFields = PRZC.c_FLD_FC_PU_ID
                        };

                        var tryget = GetFC_Project(PRZC.c_FC_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            throw new Exception("Error retrieving feature class.");
                        }

                        using (Table table = tryget.featureclass)
                        using (RowCursor rowCursor = table.Search(queryFilter))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int puid = Convert.ToInt32(row[PRZC.c_FLD_FC_PU_ID]);

                                    if (puid > 0)
                                    {
                                        puids.Add(puid);
                                    }
                                }
                            }
                        }

                        return (true, "success");
                    }
                    else if (pu_result.puLayerType == PlanningUnitLayerType.RASTER)
                    {
                        if (!(await RasterExists_Project(PRZC.c_RAS_PLANNING_UNITS)).exists)
                        {
                            return (false, "PU raster dataset not found");
                        }

                        QueryFilter queryFilter = new QueryFilter()
                        {
                            SubFields = PRZC.c_FLD_RAS_PU_ID
                        };

                        var tryget = GetRaster_Project(PRZC.c_RAS_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            return (false, "Error retrieving raster planning units");
                        }

                        using (RasterDataset rasterDataset = tryget.rasterDataset)
                        using (Raster raster = rasterDataset.CreateFullRaster())
                        using (Table table = raster.GetAttributeTable())
                        using (RowCursor rowCursor = table.Search(queryFilter))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int puid = Convert.ToInt32(row[PRZC.c_FLD_FC_PU_ID]);

                                    if (puid > 0)
                                    {
                                        puids.Add(puid);
                                    }
                                }
                            }
                        }

                        return (true, "success");
                    }
                    else
                    {
                        return (false, "there was a resounding kaboom");
                    }
                });

                if (outcome.success)
                {
                    return (true, puids, "success");
                }
                else
                {
                    return (false, null, outcome.message);
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Returns a sorted list of Planning Unit IDs from the existing Planning Unit dataset (raster or feature).
        /// Return value follows the success (bool), object (list), string (message) pattern.
        /// success == false means the list was not retrieved, and the message string explains why.
        /// </summary>
        /// <returns></returns>
        public static async Task<(bool success, List<int> puids, string message)> GetPUIDList()
        {
            try
            {
                var outcome = await GetPUIDHashset();

                if (outcome.success)
                {
                    List<int> puids = outcome.puids.ToList();
                    puids.Sort();

                    return (true, puids, "success");
                }
                else
                {
                    return (false, null, outcome.message);
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieves a Hashset of National Grid Cell Numbers from the existing Planning Unit dataset (either Feature or Raster).
        /// Return value follows the success (bool), object (hashset), string (message) pattern.
        /// success == false means the hashset was not retrieved, and the message string explains why.
        /// </summary>
        /// <returns></returns>
        public static async Task<(bool success, HashSet<long> cell_numbers, string message)> GetCellNumberHashset()
        {
            try
            {
                // Check for Project GDB
                var try_gdbexists = await GDBExists_Project();
                if (!try_gdbexists.exists)
                {
                    return (false, null, "Project GDB not found.");
                }

                // Check for PU
                var pu_result = await PUExists();
                if (!pu_result.exists)
                {
                    return (false, null, "Planning Unit dataset not found.");
                }

                // Create the hashset
                HashSet<long> cellnums = new HashSet<long>();

                // Populate the hashset
                (bool success, string message) outcome = await QueuedTask.Run(async () =>
                {
                    if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                    {
                        if (!(await FCExists_Project(PRZC.c_FC_PLANNING_UNITS)).exists)
                        {
                            return (false, "PU feature class not found");
                        }

                        QueryFilter queryFilter = new QueryFilter()
                        {
                            SubFields = PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER
                        };

                        var tryget = GetFC_Project(PRZC.c_FC_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            throw new Exception("Error retrieving feature class.");
                        }

                        using (Table table = tryget.featureclass)
                        using (RowCursor rowCursor = table.Search(queryFilter))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    long cellnum = Convert.ToInt64(row[PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER]);

                                    if (cellnum > 0)
                                    {
                                        cellnums.Add(cellnum);
                                    }
                                }
                            }
                        }

                        return (true, "success");
                    }
                    else if (pu_result.puLayerType == PlanningUnitLayerType.RASTER)
                    {
                        if (!(await RasterExists_Project(PRZC.c_RAS_PLANNING_UNITS)).exists)
                        {
                            return (false, "PU raster dataset not found");
                        }

                        QueryFilter queryFilter = new QueryFilter()
                        {
                            SubFields = PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER
                        };

                        var tryget = GetRaster_Project(PRZC.c_RAS_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            return (false, "Error retrieving planning unit raster");
                        }

                        using (RasterDataset rasterDataset = tryget.rasterDataset)
                        using (Raster raster = rasterDataset.CreateFullRaster())
                        using (Table table = raster.GetAttributeTable())
                        using (RowCursor rowCursor = table.Search(queryFilter))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    long cellnum = Convert.ToInt64(row[PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER]);

                                    if (cellnum > 0)
                                    {
                                        cellnums.Add(cellnum);
                                    }
                                }
                            }
                        }

                        return (true, "success");
                    }
                    else
                    {
                        return (false, "there was a resounding kaboom");
                    }
                });

                if (outcome.success)
                {
                    return (true, cellnums, "success");
                }
                else
                {
                    return (false, null, outcome.message);
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Returns a sorted list of National Grid Cell Numbers from the existing Planning Unit dataset (raster or feature).
        /// Return value follows the success (bool), object (list), string (message) pattern.
        /// success == false means the list was not retrieved, and the message string explains why.
        /// </summary>
        /// <returns></returns>
        public static async Task<(bool success, List<long> cell_numbers, string message)> GetCellNumberList()
        {
            try
            {
                var outcome = await GetCellNumberHashset();

                if (outcome.success)
                {
                    List<long> cellnums = outcome.cell_numbers.ToList();
                    cellnums.Sort();

                    return (true, cellnums, "success");
                }
                else
                {
                    return (false, null, outcome.message);
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        #endregion

        #region DICTIONARIES

        /// <summary>
        /// Retrieve a dictionary of cell numbers and associated planning unit ids from the
        /// project geodatabase.  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static async Task<(bool success, Dictionary<long, int> dict, string message)> GetCellNumbersAndPUIDs()
        {
            try
            {
                // Check for Project GDB
                var try_gdbexists = await GDBExists_Project();
                if (!try_gdbexists.exists)
                {
                    return (false, null, "Project GDB not found.");
                }

                // Check for PU
                var pu_result = await PUExists();
                if (!pu_result.exists)
                {
                    return (false, null, "Planning Unit dataset not found.");
                }

                // Create the dictionary
                Dictionary<long, int> dict = new Dictionary<long, int>();

                // Populate the dictionary
                (bool success, string message) outcome = await QueuedTask.Run(async () =>
                {
                    if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                    {
                        if (!(await FCExists_Project(PRZC.c_FC_PLANNING_UNITS)).exists)
                        {
                            return (false, "PU feature class not found");
                        }

                        QueryFilter queryFilter = new QueryFilter()
                        {
                            SubFields = PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER + "," + PRZC.c_FLD_FC_PU_ID
                        };

                        var tryget = GetFC_Project(PRZC.c_FC_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            throw new Exception("Error retrieving feature class.");
                        }

                        using (Table table = tryget.featureclass)
                        using (RowCursor rowCursor = table.Search(queryFilter))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    long cellnum = Convert.ToInt64(row[PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER]);
                                    int puid = Convert.ToInt32(row[PRZC.c_FLD_FC_PU_ID]);

                                    if (cellnum > 0 && puid > 0 && !dict.ContainsKey(cellnum))
                                    {
                                        dict.Add(cellnum, puid);
                                    }
                                }
                            }
                        }

                        return (true, "success");
                    }
                    else if (pu_result.puLayerType == PlanningUnitLayerType.RASTER)
                    {
                        if (!(await RasterExists_Project(PRZC.c_RAS_PLANNING_UNITS)).exists)
                        {
                            return (false, "PU raster dataset not found");
                        }

                        QueryFilter queryFilter = new QueryFilter()
                        {
                            SubFields = PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER + "," + PRZC.c_FLD_RAS_PU_ID
                        };

                        var tryget = GetRaster_Project(PRZC.c_RAS_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            return (false, "Error retrieving planning unit raster");
                        }

                        using (RasterDataset rasterDataset = tryget.rasterDataset)
                        using (Raster raster = rasterDataset.CreateFullRaster())
                        using (Table table = raster.GetAttributeTable())
                        using (RowCursor rowCursor = table.Search(queryFilter))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    long cellnum = Convert.ToInt64(row[PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER]);
                                    int puid = Convert.ToInt32(row[PRZC.c_FLD_RAS_PU_ID]);

                                    if (cellnum > 0 && puid > 0 && !dict.ContainsKey(cellnum))
                                    {
                                        dict.Add(cellnum, puid);
                                    }
                                }
                            }
                        }

                        return (true, "success");
                    }
                    else
                    {
                        return (false, "there was a resounding kaboom");
                    }
                });

                if (outcome.success)
                {
                    return (true, dict, "success");
                }
                else
                {
                    return (false, null, outcome.message);
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Retrieve a dictionary of planning unit ids and associated cell numbers from the
        /// Project geodatabase.  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static async Task<(bool success, Dictionary<int, long> dict, string message)> GetPUIDsAndCellNumbers()
        {
            try
            {
                // Check for Project GDB
                var try_gdbexists = await GDBExists_Project();
                if (!try_gdbexists.exists)
                {
                    return (false, null, "Project GDB not found.");
                }

                // Check for PU
                var pu_result = await PUExists();
                if (!pu_result.exists)
                {
                    return (false, null, "Planning Unit dataset not found.");
                }

                // Create the dictionary
                Dictionary<int, long> dict = new Dictionary<int, long>();

                // Populate the dictionary
                (bool success, string message) outcome = await QueuedTask.Run(async () =>
                {
                    if (pu_result.puLayerType == PlanningUnitLayerType.FEATURE)
                    {
                        if (!(await FCExists_Project(PRZC.c_FC_PLANNING_UNITS)).exists)
                        {
                            return (false, "PU feature class not found");
                        }

                        QueryFilter queryFilter = new QueryFilter()
                        {
                            SubFields = PRZC.c_FLD_FC_PU_ID + "," + PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER
                        };

                        var tryget = GetFC_Project(PRZC.c_FC_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            throw new Exception("Error retrieving feature class.");
                        }

                        using (Table table = tryget.featureclass)
                        using (RowCursor rowCursor = table.Search(queryFilter))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int puid = Convert.ToInt32(row[PRZC.c_FLD_FC_PU_ID]);
                                    long cellnum = Convert.ToInt64(row[PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER]);

                                    if (puid > 0 && cellnum > 0 && !dict.ContainsKey(puid))
                                    {
                                        dict.Add(puid, cellnum);
                                    }
                                }
                            }
                        }

                        return (true, "success");
                    }
                    else if (pu_result.puLayerType == PlanningUnitLayerType.RASTER)
                    {
                        if (!(await RasterExists_Project(PRZC.c_RAS_PLANNING_UNITS)).exists)
                        {
                            return (false, "PU raster dataset not found");
                        }

                        QueryFilter queryFilter = new QueryFilter()
                        {
                            SubFields = PRZC.c_FLD_RAS_PU_ID + "," + PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER
                        };

                        var tryget = GetRaster_Project(PRZC.c_RAS_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            return (false, "Error retrieving raster planning units");
                        }

                        using (RasterDataset rasterDataset = tryget.rasterDataset)
                        using (Raster raster = rasterDataset.CreateFullRaster())
                        using (Table table = raster.GetAttributeTable())
                        using (RowCursor rowCursor = table.Search(queryFilter))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int puid = Convert.ToInt32(row[PRZC.c_FLD_RAS_PU_ID]);
                                    long cellnum = Convert.ToInt64(row[PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER]);

                                    if (puid > 0 && cellnum > 0 && !dict.ContainsKey(puid))
                                    {
                                        dict.Add(puid, cellnum);
                                    }
                                }
                            }
                        }

                        return (true, "success");
                    }
                    else
                    {
                        return (false, "there was a resounding kaboom");
                    }
                });

                if (outcome.success)
                {
                    return (true, dict, "success");
                }
                else
                {
                    return (false, null, outcome.message);
                }

            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        #endregion

        #endregion

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

                    case PRZLayerNames.SELRULES:
                        return GroupLayerExists_STATUS(map);

                    case PRZLayerNames.SELRULES_INCLUDE:
                        return GroupLayerExists_STATUS_INCLUDE(map);

                    case PRZLayerNames.SELRULES_EXCLUDE:
                        return GroupLayerExists_STATUS_EXCLUDE(map);

                    case PRZLayerNames.COST:
                        return GroupLayerExists_COST(map);

                    case PRZLayerNames.FEATURES:
                        return GroupLayerExists_FEATURE(map);

                    case PRZLayerNames.PU:
                        bool fle = FeatureLayerExists_PU(map);
                        bool rle = RasterLayerExists_PU(map);
                        return (fle | rle);

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
                List<Layer> LIST_layers = map.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_MAIN && (l is GroupLayer)).ToList();

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
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_SELRULES && (l is GroupLayer)).ToList();

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

                if (!PRZLayerExists(map, PRZLayerNames.SELRULES))
                {
                    return false;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.SELRULES);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_SELRULES_INCLUDE && (l is GroupLayer)).ToList();

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

                if (!PRZLayerExists(map , PRZLayerNames.SELRULES))
                {
                    return false;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.SELRULES);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_SELRULES_EXCLUDE && (l is GroupLayer)).ToList();

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
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_FEATURES && (l is GroupLayer)).ToList();

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

        public static bool RasterLayerExists_PU(Map map)
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
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_LAYER_PLANNING_UNITS && (l is RasterLayer)).ToList();

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

                    case PRZLayerNames.SELRULES:
                        return GetGroupLayer_STATUS(map);

                    case PRZLayerNames.SELRULES_INCLUDE:
                        return GetGroupLayer_STATUS_INCLUDE(map);

                    case PRZLayerNames.SELRULES_EXCLUDE:
                        return GetGroupLayer_STATUS_EXCLUDE(map);

                    case PRZLayerNames.COST:
                        return GetGroupLayer_COST(map);

                    case PRZLayerNames.FEATURES:
                        return GetGroupLayer_FEATURE(map);

                    case PRZLayerNames.PU:
                        Layer fl = GetFeatureLayer_PU(map);
                        Layer rl = GetRasterLayer_PU(map);
                        return fl ?? rl;

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

                List<Layer> LIST_layers = map.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_MAIN && (l is GroupLayer)).ToList();

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
                if (!PRZLayerExists(map, PRZLayerNames.SELRULES))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_SELRULES && (l is GroupLayer)).ToList();

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
                if (!PRZLayerExists(map, PRZLayerNames.SELRULES_INCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.SELRULES);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_SELRULES_INCLUDE && (l is GroupLayer)).ToList();

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
                if (!PRZLayerExists(map, PRZLayerNames.SELRULES_EXCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.SELRULES);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_SELRULES_EXCLUDE && (l is GroupLayer)).ToList();

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
                if (!PRZLayerExists(map, PRZLayerNames.FEATURES))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_GROUPLAYER_FEATURES && (l is GroupLayer)).ToList();

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
                if (!FeatureLayerExists_PU(map))
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

        public static RasterLayer GetRasterLayer_PU(Map map)
        {
            try
            {
                if (!RasterLayerExists_PU(map))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                List<Layer> LIST_layers = GL.Layers.Where(l => l.Name == PRZC.c_LAYER_PLANNING_UNITS && (l is RasterLayer)).ToList();

                if (LIST_layers.Count == 0)
                {
                    return null;
                }
                else
                {
                    return LIST_layers[0] as RasterLayer;
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
                    container == PRZLayerNames.FEATURES |
                    container == PRZLayerNames.SELRULES_INCLUDE |
                    container == PRZLayerNames.SELRULES_EXCLUDE)
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
                if (!PRZLayerExists(map, PRZLayerNames.SELRULES_INCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.SELRULES_INCLUDE);
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
                if (!PRZLayerExists(map, PRZLayerNames.SELRULES_INCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.SELRULES_INCLUDE);
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
                if (!PRZLayerExists(map, PRZLayerNames.SELRULES_INCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.SELRULES_INCLUDE);
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
                if (!PRZLayerExists(map, PRZLayerNames.SELRULES_EXCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.SELRULES_EXCLUDE);
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
                if (!PRZLayerExists(map, PRZLayerNames.SELRULES_EXCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.SELRULES_EXCLUDE);
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
                if (!PRZLayerExists(map, PRZLayerNames.SELRULES_EXCLUDE))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.SELRULES_EXCLUDE);
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
                if (!PRZLayerExists(map, PRZLayerNames.FEATURES))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.FEATURES);
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
                if (!PRZLayerExists(map, PRZLayerNames.FEATURES))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.FEATURES);
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
                if (!PRZLayerExists(map, PRZLayerNames.FEATURES))
                {
                    return null;
                }

                GroupLayer GL = (GroupLayer)GetPRZLayer(map, PRZLayerNames.FEATURES);
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

        /// <summary>
        /// Delete all contents of the project file geodatabase.  
        /// Must be run on MCT.  Silent errors.
        /// </summary>
        /// <returns></returns>
        public static async Task<(bool success, string message)> DeleteProjectGDBContents()
        {
            try
            {
                // Ensure this method is called on the worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags_GPRefresh = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread;
                string toolOutput;

                // geodatabase path
                string gdbpath = GetPath_ProjectGDB();

                // Get the project gdb
                var tryget_gdb = GetGDB_Project();
                if (!tryget_gdb.success)
                {
                    return (false, "Unable to retrieve the project geodatabase.");
                }

                // Create the lists of object definitions
                List<string> relDefs = null;
                List<string> fdsDefs = null;
                List<string> rdsDefs = null;
                List<string> fcDefs = null;
                List<string> tabDefs = null;
                List<string> domainNames = null;

                // Populate the lists of existing objects
                using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                {
                    // Get list of Relationship Classes
                    relDefs = geodatabase.GetDefinitions<RelationshipClassDefinition>().Select(o => o.GetName()).ToList();
                    WriteLog($"{relDefs.Count} Relationship Class(es) found in {gdbpath}...");

                    // Get list of Feature Dataset names
                    fdsDefs = geodatabase.GetDefinitions<FeatureDatasetDefinition>().Select(o => o.GetName()).ToList();
                    WriteLog($"{fdsDefs.Count} Feature Dataset(s) found in {gdbpath}...");

                    // Get list of Raster Dataset names
                    rdsDefs = geodatabase.GetDefinitions<RasterDatasetDefinition>().Select(o => o.GetName()).ToList();
                    WriteLog($"{rdsDefs.Count} Raster Dataset(s) found in {gdbpath}...");

                    // Get list of top-level Feature Classes
                    fcDefs = geodatabase.GetDefinitions<FeatureClassDefinition>().Select(o => o.GetName()).ToList();
                    WriteLog($"{fcDefs.Count} Feature Class(es) found in {gdbpath}...");

                    // Get list of tables
                    tabDefs = geodatabase.GetDefinitions<TableDefinition>().Select(o => o.GetName()).ToList();
                    WriteLog($"{tabDefs.Count} Table(s) found in {gdbpath}...");

                    // Get list of domains
                    domainNames = geodatabase.GetDomains().Select(o => o.GetName()).ToList();
                    WriteLog($"{domainNames.Count} domain(s) found in {gdbpath}...");
                }

                // Delete those objects using geoprocessing tools
                // Relationship Classes
                if (relDefs != null && relDefs.Count > 0)
                {
                    WriteLog($"Deleting {relDefs.Count} relationship class(es)...");
                    toolParams = Geoprocessing.MakeValueArray(string.Join(";", relDefs));
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        WriteLog($"Error deleting relationship class(es). GP Tool failed or was cancelled by user", LogMessageType.ERROR);
                        return (false, "Error deleting relationship class(es).");
                    }
                    else
                    {
                        WriteLog($"Relationship class(es) deleted.");
                    }
                }

                // Feature Datasets
                if (fdsDefs != null && fdsDefs.Count > 0)
                {
                    WriteLog($"Deleting {fdsDefs.Count} feature dataset(s)...");
                    toolParams = Geoprocessing.MakeValueArray(string.Join(";", fdsDefs));
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        WriteLog($"Error deleting feature dataset(s). GP Tool failed or was cancelled by user", LogMessageType.ERROR);
                        return (false, "Error deleting feature dataset(s).");
                    }
                    else
                    {
                        WriteLog($"Feature dataset(s) deleted.");
                    }
                }

                // Raster Datasets
                if (rdsDefs != null && rdsDefs.Count > 0)
                {
                    WriteLog($"Deleting {rdsDefs.Count} raster dataset(s)...");
                    toolParams = Geoprocessing.MakeValueArray(string.Join(";", rdsDefs));
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        WriteLog($"Error deleting raster dataset(s). GP Tool failed or was cancelled by user", LogMessageType.ERROR);
                        return (false, "Error deleting raster dataset(s).");
                    }
                    else
                    {
                        WriteLog($"Raster dataset(s) deleted.");
                    }
                }

                // Feature Classes
                if (fcDefs != null && fcDefs.Count > 0)
                {
                    WriteLog($"Deleting {fcDefs.Count} feature class(es)...");
                    toolParams = Geoprocessing.MakeValueArray(string.Join(";", fcDefs));
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        WriteLog($"Error deleting feature class(es). GP Tool failed or was cancelled by user", LogMessageType.ERROR);
                        return (false, "Error deleting feature class(es).");
                    }
                    else
                    {
                        WriteLog($"Feature class(es) deleted.");
                    }
                }

                // Tables
                if (tabDefs != null && tabDefs.Count > 0)
                {
                    WriteLog($"Deleting {tabDefs.Count} table(s)...");
                    toolParams = Geoprocessing.MakeValueArray(string.Join(";", tabDefs));
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                    if (toolOutput == null)
                    {
                        WriteLog($"Error deleting table(s). GP Tool failed or was cancelled by user", LogMessageType.ERROR);
                        return (false, "Error deleting table(s).");
                    }
                    else
                    {
                        WriteLog($"Table(s) deleted.");
                    }
                }

                // Domains
                if (domainNames != null && domainNames.Count > 0)
                {
                    WriteLog($"Deleting {domainNames.Count} domain(s)...");
                    foreach (string domainName in domainNames)
                    {
                        WriteLog($"Deleting {domainName} domain...");
                        toolParams = Geoprocessing.MakeValueArray(gdbpath, domainName);
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                        toolOutput = await RunGPTool("DeleteDomain_management", toolParams, toolEnvs, toolFlags_GPRefresh);
                        if (toolOutput == null)
                        {
                            WriteLog($"Error deleting {domainName} domain. GP Tool failed or was cancelled by user", LogMessageType.ERROR);
                            return (false, $"Error deleting {domainName} domain.");
                        }
                        else
                        {
                            WriteLog($"Domain deleted.");
                        }
                    }
                }

                // I've deleted everything.
                return (true, "success");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Remove all core layers from the active mapview's Prioritization group layer.
        /// Must be run on MCT.  Silent errors.
        /// </summary>
        /// <param name="map"></param>
        /// <returns></returns>
        public static async Task<(bool success, string message)> RemovePRZItemsFromMap(Map map)
        {
            try
            {
                // Ensure this method is called on the worker thread
                if (!QueuedTask.OnWorker)
                {
                    throw new ArcGIS.Core.CalledOnWrongThreadException();
                }

                // Relevant map contents
                var standalone_tables = map.StandaloneTables;
                var layers = map.GetLayersAsFlattenedList().Where(l => (l is FeatureLayer | l is RasterLayer));

                // Lists of items to remove
                List<StandaloneTable> tables_to_remove = new List<StandaloneTable>();
                List<Layer> layers_to_remove = new List<Layer>();

                // geodatabase path
                string gdbpath = GetPath_ProjectGDB();

                // Get the project gdb
                var tryget_gdb = GetGDB_Project();
                if (!tryget_gdb.success)
                {
                    return (false, "Unable to retrieve the project geodatabase.");
                }

                using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                {
                    if (geodatabase == null)
                    {
                        return (false, "Unable to retrieve the geodatabase.");
                    }

                    // Get the Geodatabase Info
                    var gdbUri = geodatabase.GetPath();
                    string gdbPath = gdbUri.AbsolutePath;

                    // Process the Standalone Tables
                    foreach (var standalone_table in standalone_tables)
                    {
                        using (Table table = standalone_table.GetTable())
                        {
                            // Ensure table actually exists...
                            if (table != null)
                            {
                                // Table's Datastore
                                using (Datastore datastore = table.GetDatastore())
                                {
                                    if (datastore != null)
                                    {
                                        Uri datastoreUri = datastore.GetPath();
                                        if (datastoreUri != null && datastoreUri.IsAbsoluteUri)
                                        {
                                            if (gdbPath == datastoreUri.AbsolutePath)
                                            {
                                                tables_to_remove.Add(standalone_table);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // this standalone table has no source table.  Remove it.
                                tables_to_remove.Add(standalone_table);
                            }
                        }
                    }

                    // Process the Layers
                    foreach (var layer in layers)
                    {
                        if (layer is FeatureLayer FL)
                        {
                            using (FeatureClass featureClass = FL.GetFeatureClass())
                            {
                                if (featureClass != null)
                                {
                                    // Feature Class's Datastore
                                    using (Datastore datastore = featureClass.GetDatastore())
                                    {
                                        if (datastore != null)
                                        {
                                            Uri datastoreUri = datastore.GetPath();
                                            if (datastoreUri != null && datastoreUri.IsAbsoluteUri)
                                            {
                                                if (gdbPath == datastoreUri.AbsolutePath)
                                                {
                                                    layers_to_remove.Add(layer);
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // this feature layer has no source feature class.  Remove it.
                                    layers_to_remove.Add(layer);
                                }
                            }
                        }
                        else if (layer is RasterLayer RL)
                        {
                            using (Raster raster = RL.GetRaster())
                            {
                                var rasterDataset = raster.GetRasterDataset();

                                if (rasterDataset != null)
                                {
                                    // Raster Dataset's Datastore
                                    using (Datastore datastore = rasterDataset.GetDatastore())
                                    {
                                        if (datastore != null)
                                        {
                                            Uri datastoreUri = datastore.GetPath();
                                            if (datastoreUri != null && datastoreUri.IsAbsoluteUri)
                                            {
                                                if (gdbPath == datastoreUri.AbsolutePath)
                                                {
                                                    layers_to_remove.Add(layer);
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // this raster layer has no source raster dataset.  Remove it.
                                    layers_to_remove.Add(layer);
                                }
                            }
                        }
                    }
                }

                // Remove the items
                bool removed = false;

                if (tables_to_remove.Count > 0)
                {
                    map.RemoveStandaloneTables(tables_to_remove);
                    removed = true;
                }

                if (layers_to_remove.Count > 0)
                {
                    map.RemoveLayers(layers_to_remove);
                    removed = true;
                }

                // redraw
                if (removed)
                {
                    await MapView.Active.RedrawAsync(false);
                }

                return (true, "success");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Rebuild the active mapview's Prioritization group layer.  Silent errors.
        /// </summary>
        /// <param name="map"></param>
        /// <returns></returns>
        internal static async Task<(bool success, string message)> RedrawPRZLayers(Map map)
        {
            try
            {

                #region VALIDATION

                // Ensure that Project Workspace exists
                string project_path = GetPath_ProjectFolder();
                var trydirexists = FolderExists_Project();
                if (!trydirexists.exists)
                {
                    return (false, trydirexists.message);
                }

                // Check for Project GDB
                string gdb_path = GetPath_ProjectGDB();
                var trygdbexists = await GDBExists_Project();
                if (!trygdbexists.exists)
                {
                    return (false, trygdbexists.message);
                }

                // Remove any PRZ items from the map
                var try_rem = await QueuedTask.Run(async () => { return await RemovePRZItemsFromMap(map); });
                if (!try_rem.success)
                {
                    return (false, try_rem.message);
                }

                #endregion

                (bool success, string message) tryprocess = await QueuedTask.Run(async () =>
                {
                    try
                    {

                        #region TOP-LEVEL LAYERS

                        // Main PRZ Group Layer
                        GroupLayer GL_MAIN = null;
                        if (!PRZLayerExists(map, PRZLayerNames.MAIN))
                        {
                            GL_MAIN = LayerFactory.Instance.CreateGroupLayer(map, 0, PRZC.c_GROUPLAYER_MAIN);
                        }
                        else
                        {
                            GL_MAIN = (GroupLayer)GetPRZLayer(map, PRZLayerNames.MAIN);
                            map.MoveLayer(GL_MAIN, 0);
                        }

                        GL_MAIN.SetVisibility(true);

                        // Selection Rules Group Layer
                        GroupLayer GL_SELRULES = null;
                        if (!PRZLayerExists(map, PRZLayerNames.SELRULES))
                        {
                            GL_SELRULES = LayerFactory.Instance.CreateGroupLayer(GL_MAIN, 0, PRZC.c_GROUPLAYER_SELRULES);
                        }
                        else
                        {
                            GL_SELRULES = (GroupLayer)GetPRZLayer(map, PRZLayerNames.SELRULES);
                            GL_MAIN.MoveLayer(GL_SELRULES, 0);
                        }

                        GL_SELRULES.SetVisibility(true);

                        // Cost Group Layer
                        GroupLayer GL_COST = null;
                        if (!PRZLayerExists(map, PRZLayerNames.COST))
                        {
                            GL_COST = LayerFactory.Instance.CreateGroupLayer(GL_MAIN, 1, PRZC.c_GROUPLAYER_COST);
                        }
                        else
                        {
                            GL_COST = (GroupLayer)GetPRZLayer(map, PRZLayerNames.COST);
                            GL_MAIN.MoveLayer(GL_COST, 1);
                        }

                        GL_COST.SetVisibility(true);

                        // Features Group Layer
                        GroupLayer GL_FEATURES = null;
                        if (!PRZLayerExists(map, PRZLayerNames.FEATURES))
                        {
                            GL_FEATURES = LayerFactory.Instance.CreateGroupLayer(GL_MAIN, 2, PRZC.c_GROUPLAYER_FEATURES);
                        }
                        else
                        {
                            GL_FEATURES = (GroupLayer)GetPRZLayer(map, PRZLayerNames.FEATURES);
                            GL_MAIN.MoveLayer(GL_FEATURES, 2);
                        }

                        GL_FEATURES.SetVisibility(true);

                        // Remove all top-level layers from GL_MAIN that are not supposed to be there
                        List<Layer> layers_to_delete = new List<Layer>();

                        var all_layers = GL_MAIN.Layers;
                        foreach (var lyr in all_layers)
                        {
                            if (lyr is GroupLayer)
                            {
                                if (lyr.Name != PRZC.c_GROUPLAYER_SELRULES && lyr.Name != PRZC.c_GROUPLAYER_COST && lyr.Name != PRZC.c_GROUPLAYER_FEATURES)
                                {
                                    layers_to_delete.Add(lyr);
                                }
                            }
                            else
                            {
                                layers_to_delete.Add(lyr);
                            }
                        }

                        GL_MAIN.RemoveLayers(layers_to_delete);

                        int w = 0;

                        // Add the Planning Unit Layer (could be VECTOR or RASTER) (MIGHT NOT EXIST YET)
                        if ((await FCExists_Project(PRZC.c_FC_PLANNING_UNITS)).exists)
                        {
                            string fc_path = GetPath_Project(PRZC.c_FC_PLANNING_UNITS).path;
                            Uri uri = new Uri(fc_path);
                            FeatureLayer featureLayer = LayerFactory.Instance.CreateFeatureLayer(uri, GL_MAIN, w++, PRZC.c_LAYER_PLANNING_UNITS);
                            await ApplyLegend_PU_Basic(featureLayer);
                            featureLayer.SetVisibility(true);
                        }
                        else if ((await RasterExists_Project(PRZC.c_RAS_PLANNING_UNITS)).exists)
                        {
                            string ras_path = GetPath_Project(PRZC.c_RAS_PLANNING_UNITS).path;
                            Uri uri = new Uri(ras_path);
                            RasterLayer rasterLayer = (RasterLayer)LayerFactory.Instance.CreateRasterLayer(uri, GL_MAIN, w++, PRZC.c_LAYER_PLANNING_UNITS);
                            // TODO: Renderer for this raster layer
                            rasterLayer.SetVisibility(true);
                        }

                        // Add the Study Area Layer (MIGHT NOT EXIST YET)
                        if ((await FCExists_Project(PRZC.c_FC_STUDY_AREA_MAIN)).exists)
                        {
                            string fc_path = GetPath_Project(PRZC.c_FC_STUDY_AREA_MAIN).path;
                            Uri uri = new Uri(fc_path);
                            FeatureLayer featureLayer = LayerFactory.Instance.CreateFeatureLayer(uri, GL_MAIN, w++, PRZC.c_LAYER_STUDY_AREA);
                            ApplyLegend_SA_Simple(featureLayer);
                            featureLayer.SetVisibility(true);
                        }

                        // Add the Study Area Buffer Layer (MIGHT NOT EXIST YET)
                        if ((await FCExists_Project(PRZC.c_FC_STUDY_AREA_MAIN_BUFFERED)).exists)
                        {
                            string fc_path = GetPath_Project(PRZC.c_FC_STUDY_AREA_MAIN_BUFFERED).path;
                            Uri uri = new Uri(fc_path);
                            FeatureLayer featureLayer = LayerFactory.Instance.CreateFeatureLayer(uri, GL_MAIN, w++, PRZC.c_LAYER_STUDY_AREA_BUFFER);
                            ApplyLegend_SAB_Simple(featureLayer);
                            featureLayer.SetVisibility(true);
                        }

                        #endregion

                        #region SELECTION RULE LAYERS

                        // Selection Rules - Include Layers
                        GroupLayer GL_SELRULES_INCLUDE = null;
                        if (!PRZLayerExists(map, PRZLayerNames.SELRULES_INCLUDE))
                        {
                            GL_SELRULES_INCLUDE = LayerFactory.Instance.CreateGroupLayer(GL_SELRULES, 0, PRZC.c_GROUPLAYER_SELRULES_INCLUDE);
                        }
                        else
                        {
                            GL_SELRULES_INCLUDE = (GroupLayer)GetPRZLayer(map, PRZLayerNames.SELRULES_INCLUDE);
                            GL_SELRULES.MoveLayer(GL_SELRULES_INCLUDE, 0);
                        }

                        GL_SELRULES_INCLUDE.SetVisibility(true);

                        // Selection Rules - Exclude Layers
                        GroupLayer GL_SELRULES_EXCLUDE = null;
                        if (!PRZLayerExists(map, PRZLayerNames.SELRULES_EXCLUDE))
                        {
                            GL_SELRULES_EXCLUDE = LayerFactory.Instance.CreateGroupLayer(GL_SELRULES, 1, PRZC.c_GROUPLAYER_SELRULES_EXCLUDE);
                        }
                        else
                        {
                            GL_SELRULES_EXCLUDE = (GroupLayer)GetPRZLayer(map, PRZLayerNames.SELRULES_EXCLUDE);
                            GL_SELRULES.MoveLayer(GL_SELRULES_EXCLUDE, 1);
                        }

                        GL_SELRULES_EXCLUDE.SetVisibility(true);

                        // Remove all layers from GL_SELRULES that are not supposed to be there
                        layers_to_delete = new List<Layer>();

                        all_layers = GL_SELRULES.Layers;
                        foreach (var lyr in all_layers)
                        {
                            if (lyr is GroupLayer)
                            {
                                if (lyr.Name != PRZC.c_GROUPLAYER_SELRULES_INCLUDE && lyr.Name != PRZC.c_GROUPLAYER_SELRULES_EXCLUDE)
                                {
                                    layers_to_delete.Add(lyr);
                                }
                            }
                            else
                            {
                                layers_to_delete.Add(lyr);
                            }
                        }

                        GL_SELRULES.RemoveLayers(layers_to_delete);

                        // Remove all layers from GL_SELRULES_INCLUDE that are not supposed to be there
                        layers_to_delete = new List<Layer>();

                        all_layers = GL_SELRULES_INCLUDE.Layers;
                        foreach (var lyr in all_layers)
                        {
                            if (!(lyr is FeatureLayer) && !(lyr is RasterLayer))
                            {
                                layers_to_delete.Add(lyr);
                            }
                            else
                            {
                                lyr.SetVisibility(true);
                            }
                        }

                        GL_SELRULES_INCLUDE.RemoveLayers(layers_to_delete);

                        // Remove all layers from GL_SELRULES_EXCLUDE that are not supposed to be there
                        layers_to_delete = new List<Layer>();

                        all_layers = GL_SELRULES_EXCLUDE.Layers;
                        foreach (var lyr in all_layers)
                        {
                            if (!(lyr is FeatureLayer) && !(lyr is RasterLayer))
                            {
                                layers_to_delete.Add(lyr);
                            }
                            else
                            {
                                lyr.SetVisibility(true);
                            }
                        }

                        GL_SELRULES_EXCLUDE.RemoveLayers(layers_to_delete);

                        #endregion

                        #region COST LAYERS

                        // Remove all layers from GL_COST that are not supposed to be there
                        layers_to_delete = new List<Layer>();

                        all_layers = GL_COST.Layers;
                        foreach (var lyr in all_layers)
                        {
                            if (!(lyr is FeatureLayer) && !(lyr is RasterLayer))
                            {
                                layers_to_delete.Add(lyr);
                            }
                            else
                            {
                                lyr.SetVisibility(true);
                            }
                        }

                        GL_COST.RemoveLayers(layers_to_delete);

                        #endregion

                        #region FEATURES LAYERS

                        // Remove all layers from GL_FEATURES that are not supposed to be there
                        layers_to_delete = new List<Layer>();

                        all_layers = GL_FEATURES.Layers;
                        foreach (var lyr in all_layers)
                        {
                            if (!(lyr is FeatureLayer) && !(lyr is RasterLayer))
                            {
                                layers_to_delete.Add(lyr);
                            }
                            else
                            {
                                lyr.SetVisibility(true);
                            }
                        }

                        GL_FEATURES.RemoveLayers(layers_to_delete);

                        #endregion

                        return (true, "success");
                    }
                    catch (Exception ex)
                    {
                        return (false, ex.Message);
                    }
                });

                if (tryprocess.success)
                {
                    return (true, "success");
                }
                else
                {
                    return (false, tryprocess.message);
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        #endregion GENERIC DATA METHODS

        #region EDIT OPERATIONS

        /// <summary>
        /// Retrieve a generic EditOperation object for editing tasks.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static EditOperation GetEditOperation(string name)
        {
            try
            {
                EditOperation editOp = new EditOperation();

                editOp.Name = name ?? "unnamed edit operation";
                editOp.ShowProgressor = false;
                editOp.ShowModalMessageAfterFailure = false;
                editOp.SelectNewFeatures = false;
                editOp.SelectModifiedFeatures = false;

                return editOp;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        #endregion

        #region SPATIAL REFERENCES

        /// <summary>
        /// Retrieve the custom prioritization Canada Albers projection.
        /// </summary>
        /// <returns></returns>
        public static SpatialReference GetSR_PRZCanadaAlbers()
        {
            try
            {
                string wkt = PRZC.c_SR_WKT_WGS84_CanadaAlbers;  // Special PRZ WGS84 Canada Albers projection
                SpatialReference sr = SpatialReferenceBuilder.CreateSpatialReference(wkt);

                return sr;  // this might be null if WKT is no good.  Or, the CreateSpatialReference method might throw an error.
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        /// <summary>
        /// Establishes whether a specified spatial reference is equivalent to the custom
        /// prioritization Canada Albers projection.  The comparison ignores the PCS and GCS
        /// names since they are user-defined and might differ.  Silent errors.
        /// </summary>
        /// <param name="TestSR"></param>
        /// <returns></returns>
        public static (bool match, string message) SpatialReferenceIsPRZCanadaAlbers(SpatialReference TestSR)
        {
            try
            {
                if (TestSR == null)
                {
                    return (false, "Spatial Reference is null");
                }
                else if (TestSR.IsUnknown)
                {
                    return (false, "Spatial Reference is null");
                }

                SpatialReference AlbersSR = GetSR_PRZCanadaAlbers();

                if (AlbersSR == null)
                {
                    return (false, "Unable to retrieve PRZ Albers projection");
                }

                // Get the WKT, eliminate the names + GCS names to eliminate name differences where otherwise equal
                string TestSR_WKT = TestSR.Wkt.Replace(TestSR.Name, "").Replace(TestSR.Gcs.Name, "");
                string AlbersSR_WKT = AlbersSR.Wkt.Replace(AlbersSR.Name, "").Replace(AlbersSR.Gcs.Name, "");

                bool result = string.Equals(AlbersSR_WKT, TestSR_WKT, StringComparison.OrdinalIgnoreCase);

                return result ? (true, "Spatial References are equivalent") : (false, "Spatial References are not equivalent");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        #endregion




        #region *** MAY NOT BE NECESSARY ANY MORE!!! ***

        public static async Task<Dictionary<int, double>> GetPlanningUnitIDsAndArea()
        {
            try
            {
                Dictionary<int, double> dict = new Dictionary<int, double>();

                if (!await QueuedTask.Run(() =>
                {
                    try
                    {
                        var tryget = GetFC_Project(PRZC.c_FC_PLANNING_UNITS);
                        if (!tryget.success)
                        {
                            throw new Exception("Error retrieving feature class.");
                        }

                        using (Table table = tryget.featureclass)
                        using (RowCursor rowCursor = table.Search())
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int id = Convert.ToInt32(row[PRZC.c_FLD_FC_PU_ID]);
                                    double area_m2 = Convert.ToDouble(row[PRZC.c_FLD_FC_PU_AREA_M2]);

                                    dict.Add(id, area_m2);
                                }
                            }
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                        return false;
                    }
                }))
                {
                    ProMsgBox.Show("Error retrieving dictionary of planning unit ids + area");
                    return null;
                }
                else
                {
                    return dict;
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }


        #endregion

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
                int intmilliseconds = span.Milliseconds;

                string hours = inthours.ToString() + ((inthours == 1) ? " hour" : " hours");
                string minutes = intminutes.ToString() + ((intminutes == 1) ? " minute" : " minutes");
                string seconds = intseconds.ToString() + ((intseconds == 1) ? " second" : " seconds");
                string milliseconds = intmilliseconds.ToString() + ((intmilliseconds == 1) ? " millisecond" : " milliseconds");

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

        public static string GetElapsedTimeInSeconds(TimeSpan span)
        {
            try
            {
                double sec = span.TotalSeconds;

                string message = $"Elapsed Time: {sec:N3}";

                return message;
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
                    CIMStroke outlineSym = SymbolFactory.Instance.ConstructStroke(outlineColor, 0.3, SimpleLineStyle.Solid);
                    CIMPolygonSymbol fillSym_Available = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor_Available, SimpleFillStyle.Solid, outlineSym);
                    CIMPolygonSymbol fillSym_Include = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor_Include, SimpleFillStyle.Solid, outlineSym);
                    CIMPolygonSymbol fillSym_Exclude = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor_Exclude, SimpleFillStyle.Solid, outlineSym);

                    // CIM UNIQUE VALUES
                    CIMUniqueValue uv_Available = new CIMUniqueValue { FieldValues = new string[] { "<Null>" } };
                    CIMUniqueValue uv_Include = new CIMUniqueValue { FieldValues = new string[] { SelectionRuleType.INCLUDE.ToString() } };
                    CIMUniqueValue uv_Exclude = new CIMUniqueValue { FieldValues = new string[] { SelectionRuleType.EXCLUDE.ToString() } };

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
                        Label = "Included",
                        Symbol = fillSym_Include.MakeSymbolReference(),
                        Description = "",
                        Visible = true,
                        Values = new CIMUniqueValue[] { uv_Include }
                    };
                    CIMUniqueValueClass uvcExclude = new CIMUniqueValueClass
                    {
                        Editable = true,
                        Label = "Excluded",
                        Symbol = fillSym_Exclude.MakeSymbolReference(),
                        Description = "",
                        Visible = true,
                        Values = new CIMUniqueValue[] { uv_Exclude }
                    };

                    // CIM UNIQUE VALUE GROUP
                    CIMUniqueValueGroup uvgMain = new CIMUniqueValueGroup
                    {
                        Classes = new CIMUniqueValueClass[] { uvcInclude, uvcExclude, uvcAvailable },
                        Heading = "Effective Selection Rule"
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

        public static async Task<bool> ApplyLegend_PU_SelRuleConflicts(FeatureLayer FL)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    // COLORS
                    CIMColor outlineColor = GetNamedColor(Color.Gray); // outline color for all 3 poly symbols
                    CIMColor fillColor_Conflict = GetNamedColor(Color.Magenta);
                    CIMColor fillColor_NoConflict = GetNamedColor(Color.LightGray);

                    // SYMBOLS
                    CIMStroke outlineSym = SymbolFactory.Instance.ConstructStroke(outlineColor, 0.1, SimpleLineStyle.Solid);
                    CIMPolygonSymbol fillSym_Conflict = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor_Conflict, SimpleFillStyle.Solid, outlineSym);
                    CIMPolygonSymbol fillSym_NoConflict = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor_NoConflict, SimpleFillStyle.Solid, outlineSym);

                    // CIM UNIQUE VALUES
                    CIMUniqueValue uv_Conflict = new CIMUniqueValue { FieldValues = new string[] { "1" } };
                    CIMUniqueValue uv_NoConflict = new CIMUniqueValue { FieldValues = new string[] { "0" } };

                    // CIM UNIQUE VALUE CLASSES
                    CIMUniqueValueClass uvcConflict = new CIMUniqueValueClass
                    {
                        Editable = true,
                        Label = "Conflict",
                        Symbol = fillSym_Conflict.MakeSymbolReference(),
                        Description = "",
                        Visible = true,
                        Values = new CIMUniqueValue[] { uv_Conflict }
                    };
                    CIMUniqueValueClass uvcNoConflict = new CIMUniqueValueClass
                    {
                        Editable = true,
                        Label = "OK",
                        Symbol = fillSym_NoConflict.MakeSymbolReference(),
                        Description = "",
                        Visible = true,
                        Values = new CIMUniqueValue[] { uv_NoConflict }
                    };

                    // CIM UNIQUE VALUE GROUP
                    CIMUniqueValueGroup uvgMain = new CIMUniqueValueGroup
                    {
                        Classes = new CIMUniqueValueClass[] { uvcConflict, uvcNoConflict },
                        Heading = "Selection Rule Conflicts"
                    };

                    // UV RENDERER
                    CIMUniqueValueRenderer UVRend = new CIMUniqueValueRenderer
                    {
                        UseDefaultSymbol = false,
                        Fields = new string[] { PRZC.c_FLD_FC_PU_CONFLICT },
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
                                int max = Convert.ToInt32(row[PRZC.c_FLD_FC_PU_FEATURECOUNT]);

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

                    ucDef.Field = PRZC.c_FLD_FC_PU_FEATURECOUNT;
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

        #region GEOMETRIES

        public static async Task<string> GetNationalGridInfo(Polygon cellPolygon)
        {
            try
            {

                return "hi";
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return "hi";
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
