//#define ARCMAP

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
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
using System.Data;
using System.Data.OleDb;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
//using System.Windows.Forms;
//using Excel = Microsoft.Office.Interop.Excel;
using PRZC = NCC.PRZTools.PRZConstants;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace NCC.PRZTools
{

    internal static class PRZHelper
    {

        #region LOGGING

        internal static string WriteLog(string message, LogMessageType type = LogMessageType.INFO)
        {
            try
            {
                // Make sure we have a valid log file
                if (!ProjectWSExists())
                {
                    return "";
                }

                string logpath = GetProjectLogPath();
                if (!ProjectLogExists())
                {
                    using (FileStream fs = File.Create(logpath)) { }
                }

                // Create the message lines
                string lines = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.ff tt") + " :: " + type.ToString() 
                                + Environment.NewLine + message 
                                + Environment.NewLine;

                using (StreamWriter w = File.AppendText(logpath))
                {
                    w.WriteLine(lines);
                    w.Flush();
                    w.Close();
                }

                return lines;
            }
            catch (Exception)
            {
                return "Unable to log message...";
            }
        }

        internal static string ReadLog()
        {
            try
            {
                if (!ProjectLogExists())
                {
                    return "";
                }

                string logpath = GetProjectLogPath();

                return File.ReadAllText(logpath);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

        }

        #endregion LOGGING

        #region RETRIEVING PATHS AND OBJECTS

        // *** Paths
        internal static string GetProjectWSPath()
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

        internal static string GetProjectGDBPath()
        {
            try
            {
                string wspath = GetProjectWSPath();
                string gdbpath = Path.Combine(wspath, PRZC.c_PRZ_PROJECT_FGDB);

                return gdbpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        internal static string GetProjectLogPath()
        {
            try
            {
                string ws = GetProjectWSPath();
                string logpath = Path.Combine(ws, PRZC.c_PRZ_LOGFILE);

                return logpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        internal static string GetPlanningUnitFCPath()
        {
            try
            {
                string gdbpath = GetProjectGDBPath();
                string pufcpath = Path.Combine(gdbpath, PRZC.c_FC_PLANNING_UNITS);

                return pufcpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        internal static string GetStudyAreaFCPath()
        {
            try
            {
                string gdbpath = GetProjectGDBPath();
                string fcpath = Path.Combine(gdbpath, PRZC.c_FC_STUDY_AREA_MAIN);

                return fcpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        internal static string GetStudyAreaMultiFCPath()
        {
            try
            {
                string gdbpath = GetProjectGDBPath();
                string fcpath = Path.Combine(gdbpath, PRZC.c_FC_STUDY_AREA_MULTI);

                return fcpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        internal static string GetStudyAreaBufferFCPath()
        {
            try
            {
                string gdbpath = GetProjectGDBPath();
                string fcpath = Path.Combine(gdbpath, PRZC.c_FC_STUDY_AREA_MAIN_BUFFERED);

                return fcpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        internal static string GetStudyAreaBufferMultiFCPath()
        {
            try
            {
                string gdbpath = GetProjectGDBPath();
                string fcpath = Path.Combine(gdbpath, PRZC.c_FC_STUDY_AREA_MULTI_BUFFERED);

                return fcpath;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        // *** Path + Object Existence
        internal static bool ProjectWSExists()
        {
            try
            {
                string path = GetProjectWSPath();
                return Directory.Exists(path);
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        internal static async Task<bool> ProjectGDBExists()
        {
            try
            {
                string path = GetProjectGDBPath();

                if (path == null)
                {
                    return false;
                }

                Uri uri = new Uri(path);
                FileGeodatabaseConnectionPath pathConn = new FileGeodatabaseConnectionPath(uri);

                try
                {
                    await QueuedTask.Run(() =>
                    {
                        var gdb = new Geodatabase(pathConn);
                    });

                }
                catch (GeodatabaseNotFoundOrOpenedException)
                {
                    return false;
                }

                // If I get to this point, the file gdb exists and was successfully opened
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool ProjectLogExists()
        {
            try
            {
                string path = GetProjectLogPath();
                return File.Exists(path);
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        internal static async Task<bool> PlanningUnitFCExists()
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

        internal static async Task<bool> StudyAreaFCExists()
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

        internal static async Task<bool> StudyAreaBufferFCExists()
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

        // *** Objects

        internal static async Task<Geodatabase> GetProjectGDB()
        {
            try
            {
                string gdbpath = GetProjectGDBPath();

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

        internal static async Task<FeatureClass> GetPlanningUnitFC()
        {
            try
            {
                using (Geodatabase gdb = await GetProjectGDB())
                {
                    if (gdb == null) return null;

                    try
                    {
                        FeatureClass fc = gdb.OpenDataset<FeatureClass>(PRZC.c_FC_PLANNING_UNITS);
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









        #endregion

        #region GENERIC DATA METHODS

        internal static async Task<Geodatabase> GetFileGDB(string path)
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

        internal static async Task<bool> FCExists(Geodatabase geodatabase, string FCName)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    FeatureClassDefinition fcdef = geodatabase.GetDefinition<FeatureClassDefinition>(FCName);
                    fcdef.Dispose();
                });

                return true;
            }
            catch
            {
                // Exception thrown if definition doesn't exist, meaning a FC of that name doesn't exist in GDB
                return false;
            }
        }

        internal static async Task<bool> TableExists(Geodatabase geodatabase, string TabName)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    TableDefinition tabdef = geodatabase.GetDefinition<TableDefinition>(TabName);
                    tabdef.Dispose();
                });

                return true;
            }
            catch
            {
                // Exception thrown if definition doesn't exist, meaning a Table of that name doesn't exist in GDB
                return false;
            }
        }

        /// <summary>
        /// Delete all Feature Datasets, Tables, and Feature Classes from the Project GDB
        /// </summary>
        /// <returns>boolean</returns>
        internal static async Task<bool> ClearProjectGDB()
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

        internal static async Task<string> RunGPTool(string toolName, IReadOnlyList<string> toolParams, IReadOnlyList<KeyValuePair<string, string>> toolEnvs, GPExecuteToolFlags flags)
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

        internal static string GetElapsedTimeMessage(TimeSpan span)
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

        public static bool ApplyLegend_PU_Simple(FeatureLayer puFL)
        {
            try
            {
                // Colors
                CIMColor outlineColor = GetNamedColor(Color.BlueViolet);
                CIMColor fillColor = GetNamedColor(Color.PaleGreen);

                CIMStroke outlineSym = SymbolFactory.Instance.ConstructStroke(outlineColor, 1.5, SimpleLineStyle.Solid);
                CIMPolygonSymbol fillSym = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor, SimpleFillStyle.Solid, outlineSym);
                CIMSimpleRenderer rend = puFL.GetRenderer() as CIMSimpleRenderer;
                rend.Symbol = fillSym.MakeSymbolReference();
                rend.Label = "A lowly planning unit";
                puFL.SetRenderer(rend);
                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool ApplyLegend_SAB_Simple(FeatureLayer sabFL)
        {
            try
            {
                // Colors
                CIMColor outlineColor = GetNamedColor(Color.Black);
                CIMColor fillColor = CIMColor.NoColor();

                CIMStroke outlineSym = SymbolFactory.Instance.ConstructStroke(outlineColor, 1, SimpleLineStyle.Solid);
                CIMPolygonSymbol fillSym = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor, SimpleFillStyle.Solid, outlineSym);
                CIMSimpleRenderer rend = sabFL.GetRenderer() as CIMSimpleRenderer;
                rend.Symbol = fillSym.MakeSymbolReference();
                //rend.Label = "";
                sabFL.SetRenderer(rend);
                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static bool ApplyLegend_SA_Simple(FeatureLayer saFL)
        {
            try
            {
                // Colors
                CIMColor outlineColor = GetNamedColor(Color.Black);
                CIMColor fillColor = CIMColor.NoColor();

                CIMStroke outlineSym = SymbolFactory.Instance.ConstructStroke(outlineColor, 2, SimpleLineStyle.Solid);
                CIMPolygonSymbol fillSym = SymbolFactory.Instance.ConstructPolygonSymbol(fillColor, SimpleFillStyle.Solid, outlineSym);
                CIMSimpleRenderer rend = saFL.GetRenderer() as CIMSimpleRenderer;
                rend.Symbol = fillSym.MakeSymbolReference();
                //rend.Label = "";
                saFL.SetRenderer(rend);
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

        internal static CIMColor GetRGBColor(byte r, byte g, byte b, byte a = 100)
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

        internal static CIMColor GetNamedColor(Color color)
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



        internal static string GetUserWSPath()
        {
            try
            {
                string local_app_path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string parent_path = Path.Combine(local_app_path, PRZC.c_USER_PROFILE_WORKDIR);

                if (!Directory.Exists(parent_path))
                {
                    Directory.CreateDirectory(parent_path);
                }

                return parent_path;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }


#if ARCMAP

        #region RETRIEVE ARCMAP OBJECTS

        private static IApplication _theAppRef()
        {
            Type t = Type.GetTypeFromProgID("esriFramework.AppRef");
            System.Object obj = Activator.CreateInstance(t);
            IApplication app = obj as ESRI.ArcGIS.Framework.IApplication;
            return app;
        }

        internal static IApplication IApp
        {
            get { return _theAppRef(); }
        }

        internal static IMxApplication IMxApp
        {
            get
            {
                if (IApp == null)
                {
                    return null;
                }
                else
                {
                    return (IMxApplication)IApp;
                }
            }
        }

        internal static IDocument IDoc
        {
            get
            {
                if (IApp == null)
                {
                    return null;
                }
                else
                {
                    return IApp.Document;
                }
            }
        }

        internal static IMxDocument IMxDoc
        {
            get
            {
                if (IDoc == null)
                {
                    return null;
                }
                else
                {
                    return (IMxDocument)IDoc;
                }
            }
        }

        internal static IMap FocusMap
        {
            get
            {
                if (IMxDoc == null)
                {
                    return null;
                }
                else
                {
                    return IMxDoc.FocusMap;
                }
            }
        }

        internal static IActiveView IAV
        {
            get
            {
                if (IMxDoc == null)
                {
                    return null;
                }
                else
                {
                    return IMxDoc.ActiveView;
                }
            }
        }

        internal static IActiveView ActiveView_FocusMap
        {
            get
            {
                if (IMxDoc == null)
                {
                    return null;
                }
                else
                {
                    return (IActiveView)FocusMap;
                }
            }
        }

        internal static IActiveView ActiveView_PageLayout
        {
            get
            {
                if (IMxDoc == null)
                {
                    return null;
                }
                else
                {
                    return (IActiveView)IMxDoc.PageLayout;
                }
            }
        }

        internal static WindowWrapper Wrap
        {
            get
            {
                WindowWrapper wrap = new WindowWrapper((IntPtr)IApp.hWnd);
                return wrap;
            }
        }

        #endregion

        #region GEOPROCESSING STUFF

        internal static IProgressDialog2 CreateProgressDialog(string Title, int MaxRange, int hwnd, esriProgressAnimationTypes animtype)
        {
            //INITIALIZE PROGRESS DIALOG
            IProgressDialogFactory ProgFact = new ProgressDialogFactoryClass();
            ITrackCancel TrackCancel = new CancelTrackerClass();
            IProgressDialog2 ProgressDialog = (IProgressDialog2)ProgFact.Create(TrackCancel, hwnd);
            ProgressDialog.Title = Title;
            ProgressDialog.CancelEnabled = false;
            ProgressDialog.Animation = animtype;
            IStepProgressor Stepper = (IStepProgressor)ProgressDialog;
            Stepper.MaxRange = MaxRange;
            Stepper.MinRange = 0;
            Stepper.Position = 0;
            Stepper.StepValue = 1;

            return ProgressDialog;
        }

        internal static void RunGPTool(Geoprocessor GP, IGPProcess tool, bool DisplayMessages)
        {
            // Execute the Geoprocessing Tool
            try
            {
                GP.Execute(tool, null);
            }
            catch
            {
            }

            ProcessGPMessages(GP, DisplayMessages);
        }

        private static void ProcessGPMessages(Geoprocessor GP, bool display_messages)
        {
            try
            {
                if (GP.MessageCount > 0)
                {
                    string GPMessage = "";

                    for (int Count = 0; Count <= GP.MessageCount - 1; Count++)
                    {
                        GPMessage = (Count == 0)
                                    ? GP.GetMessage(Count)
                                    : GPMessage + Environment.NewLine + GP.GetMessage(Count);
                    }

                    if (Properties.Settings.Default.LogGPMessages)
                    {
                        if (!LogGPMessage(GPMessage))
                        {
                            GPMessage += Environment.NewLine + ">>> UNABLE TO LOG MESSAGE!" + Environment.NewLine;
                        }
                    }

                    if (Properties.Settings.Default.DisplayGPMessages | display_messages)
                    {
                        MessageBox.Show(GPMessage);
                    }
                }
            }
            catch
            {
            }
        }

        private static bool LogGPMessage(string GPMessage)
        {
            try
            {
                string parentpath = GetUserWorkspaceDirectory();

                using (StreamWriter w = File.AppendText(System.IO.Path.Combine(parentpath, SC.c_GP_LOGFILE)))
                {
                    w.WriteLine("***************************************");
                    w.WriteLine("");
                    w.WriteLine(GPMessage);
                    w.WriteLine("");
                    w.Flush();
                    w.Close();
                }

                return true;
            }
            catch
            {
                MessageBox.Show("Unable to log GP message");
                return false;
            }
        }

        #endregion

        internal static string GetUserWorkspaceDirectory()
        {
            try
            {
                string local_app_path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string parent_path = SysPath.Combine(local_app_path, SC.c_USER_PROFILE_WORKDIR);

                if (!Directory.Exists(parent_path))
                {
                    Directory.CreateDirectory(parent_path);
                }

                return parent_path;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return string.Empty;
            }
        }

        internal static IGroupLayer ReturnGroupLayer(string GLName, IMap map, bool MoveToTop, bool CreateLayerIfNotFound)
        {
            try
            {
                bool LayerPresent = false;

                IGroupLayer GL = null;

                UID id = new UIDClass();
                id.Value = "{EDAD6644-1810-11D1-86AE-0000F8751720}";    // group layer

                IEnumLayer enumGroupLayers = map.get_Layers(id, true);
                ILayer gl = enumGroupLayers.Next();

                while (gl != null)
                {
                    if (gl.Name == GLName)
                    {
                        if (LayerPresent)
                        {
                            map.MoveLayer(gl, 0);
                            map.DeleteLayer(gl);
                        }
                        else
                        {
                            LayerPresent = true;
                            GL = (IGroupLayer)gl;
                        }
                    }

                    gl = enumGroupLayers.Next();
                }

                if (!LayerPresent & CreateLayerIfNotFound)
                {
                    GL = new GroupLayerClass();
                    GL.Name = GLName;
                    GL.Visible = true;
                    GL.Expanded = false;
                    map.AddLayer((ILayer)GL);
                }

                if (GL != null & MoveToTop)
                {
                    ILayer lyr = map.get_Layer(0);
                    if (!(lyr is IGroupLayer & lyr.Name == GL.Name))
                    {
                        map.MoveLayer(GL, 0);
                    }
                }

                return GL;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        #region SPATIAL REFERENCE STUFF

        internal static ISpatialReference GetMNRLambertProjection()
        {
            try
            {
                ISpatialReference SR;
                int bytesread;
                string lambertPRJString = @"PROJCS[""MNR_Lambert_Conformal_Conic"",GEOGCS[""GCS_North_American_1983"",DATUM[""D_North_American_1983"",SPHEROID[""GRS_1980"",6378137,298.257222101]],PRIMEM[""Greenwich"",0],UNIT[""Degree"",0.017453292519943295]],PROJECTION[""Lambert_Conformal_Conic""],PARAMETER[""False_Easting"",930000],PARAMETER[""False_Northing"",6430000],PARAMETER[""Central_Meridian"",-85],PARAMETER[""Standard_Parallel_1"",44.5],PARAMETER[""Standard_Parallel_2"",53.5],PARAMETER[""Latitude_Of_Origin"",0],UNIT[""Meter"",1]]";

                ISpatialReferenceFactory2 fact = new SpatialReferenceEnvironmentClass();
                fact.CreateESRISpatialReference(lambertPRJString, out SR, out bytesread);

                ISpatialReferenceResolution srr = (ISpatialReferenceResolution)SR;
                srr.ConstructFromHorizon();

                return SR;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        internal enum enumSpatialReferenceType
        {
            GeogCS,
            ProjCS,
            UnknownCS,
            NullCS,
        }

        internal static bool GetSpatialReferenceFromWKT(string WKT, out ISpatialReference SR, out string message)
        {
            SR = null;
            message = "";

            try
            {
                ISpatialReferenceFactory3 SRF3 = new SpatialReferenceEnvironmentClass();
                int bytred;

                try
                {
                    SRF3.CreateESRISpatialReference(WKT, out SR, out bytred);
                }
                catch
                {
                    message = "Error converting WKT to ISpatialReference..." + Environment.NewLine + Environment.NewLine +
                              "WKT: " + WKT;
                    return false;
                }

                if (SR == null)
                {
                    message = "Following conversion from WKT, SR is null";
                    return false;
                }

                if (SR is IUnknownCoordinateSystem)
                {
                    message = "Unknown CS";
                }
                else if (SR is IGeographicCoordinateSystem)
                {
                    message = "Geographic CS";
                }
                else if (SR is IProjectedCoordinateSystem)
                {
                    message = "Projected CS";
                }
                else
                {
                    message = "Unspecified CS";
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        internal static bool GetSpatialReferenceWKTFromSR(ISpatialReference SR, out string WKT, out string message)
        {
            WKT = "";
            message = "";

            try
            {
                if (SR == null)
                {
                    message = "Null Spatial Reference";
                    return false;
                }
                else if (SR is IUnknownCoordinateSystem)
                {
                    message = "Unknown Coordinate System";
                    return false;
                }
                else if (!(SR is IProjectedCoordinateSystem) & !(SR is IGeographicCoordinateSystem))
                {
                    message = "Spatial Reference is not null, not unknown, not geographic, and not projected";
                    return false;
                }

                IESRISpatialReferenceGEN2 SRGEN = (IESRISpatialReferenceGEN2)SR;
                int bytes_written;
                SRGEN.ExportToESRISpatialReference2(out WKT, out bytes_written);
                if (WKT.Length > 0)
                {
                    return true;
                }
                else
                {
                    message = "Zero-length WKT returned by ExportToESRISpatialReference2 method";
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        internal static bool GetSpatialReferenceFromMap(IMap map, out ISpatialReference SR, out enumSpatialReferenceType SRType)
        {
            SR = null;

            try
            {
                SR = map.SpatialReference;

                if (SR == null)
                {
                    //MessageBox.Show("Your ArcMap DataFrame (the main map window) has no spatial reference!" + Environment.NewLine +
                    //                "Please set the spatial reference to a defined coordinate system please!" + Environment.NewLine + Environment.NewLine +
                    //                "(Hint: Use the dropdown list on the District Toolbar)");
                    SRType = enumSpatialReferenceType.NullCS;
                    return false;
                }
                else if (SR is IGeographicCoordinateSystem)
                {
                    SRType = enumSpatialReferenceType.GeogCS;
                    return true;
                }
                else if (SR is IProjectedCoordinateSystem)
                {
                    SRType = enumSpatialReferenceType.ProjCS;
                    return true;
                }
                else
                {
                    SRType = enumSpatialReferenceType.UnknownCS;
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                SRType = enumSpatialReferenceType.NullCS;
                return false;
            }
        }

        #endregion

        #region COLORS AND SYMBOLS

        internal static IColor ReturnESRIColorFromRGB(byte r, byte g, byte b)
        {
            try
            {
                IColor color = new RgbColorClass();
                color.RGB = ColorTranslator.ToWin32(Color.FromArgb(r, g, b));
                return color;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
        }

        internal static IColor ReturnESRIColorFromNETColor(Color NamedColor)
        {
            try
            {
                IColor color = new RgbColorClass();
                color.RGB = ColorTranslator.ToWin32(NamedColor);
                return color;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
        }

        internal static ISimpleFillSymbol ReturnSimpleFillSymbol(IColor FillColor, IColor OutlineColor, double OutlineWidth, esriSimpleFillStyle FillStyle)
        {
            try
            {
                ISimpleLineSymbol Outline = new SimpleLineSymbolClass();
                Outline.Color = OutlineColor;
                Outline.Style = esriSimpleLineStyle.esriSLSSolid;
                Outline.Width = OutlineWidth;

                ISimpleFillSymbol FillSymbol = new SimpleFillSymbolClass();
                FillSymbol.Color = FillColor;
                FillSymbol.Outline = Outline;
                FillSymbol.Style = FillStyle;

                return FillSymbol;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
        }

        internal static IMarkerSymbol ReturnMarkerSymbol(string SymbolName, string StyleName, IColor SymbolColor, int SymbolSize)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("esriFramework.StyleGallery");
                System.Object obj = Activator.CreateInstance(t);
                IStyleGallery Gallery = obj as IStyleGallery;
                IStyleGalleryStorage GalleryStorage = (IStyleGalleryStorage)Gallery;
                string StylePath = GalleryStorage.DefaultStylePath + StyleName;

                bool StyleFound = false;

                for (int i = 0; i < GalleryStorage.FileCount; i++)
                {
                    if (GalleryStorage.get_File(i).ToUpper() == StyleName.ToUpper())
                    {
                        StyleFound = true;
                        break;
                    }
                }

                if (!StyleFound)
                    GalleryStorage.AddFile(StylePath);

                IEnumStyleGalleryItem EnumGalleryItem = Gallery.get_Items("Marker Symbols", StyleName, "DEFAULT");
                IStyleGalleryItem GalleryItem = EnumGalleryItem.Next();

                while (GalleryItem != null)
                {
                    if (GalleryItem.Name == SymbolName)
                    {
                        IClone SourceClone = (IClone)GalleryItem.Item;
                        IClone DestClone = SourceClone.Clone();
                        IMarkerSymbol MarkerSymbol = (IMarkerSymbol)DestClone;
                        MarkerSymbol.Color = SymbolColor;
                        MarkerSymbol.Size = SymbolSize;
                        return MarkerSymbol;
                    }
                    GalleryItem = EnumGalleryItem.Next();
                }

                MessageBox.Show("Unable to locate Marker Symbol '" + SymbolName + "' in style '" + StyleName + "'.");
                return null;

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
        }

        internal static ISimpleMarkerSymbol ReturnSimpleMarkerSymbol(IColor MarkerColor, IColor OutlineColor, esriSimpleMarkerStyle MarkerStyle, double MarkerSize, double OutlineSize)
        {
            try
            {
                ISimpleMarkerSymbol MarkerSymbol = new SimpleMarkerSymbolClass();
                MarkerSymbol.Color = MarkerColor;
                MarkerSymbol.Style = MarkerStyle;
                MarkerSymbol.Size = MarkerSize;
                MarkerSymbol.Outline = true;
                MarkerSymbol.OutlineSize = OutlineSize;
                MarkerSymbol.OutlineColor = OutlineColor;

                return MarkerSymbol;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
        }

        internal static IPictureMarkerSymbol ReturnPictureMarkerSymbol(Bitmap SourceBitmap, IColor TransparentColor, double MarkerSize)
        {
            try
            {
                IPictureMarkerSymbol PictureSymbol = new PictureMarkerSymbolClass();
                PictureSymbol.Picture = (IPictureDisp)OLE.GetIPictureDispFromBitmap(SourceBitmap);
                PictureSymbol.Size = MarkerSize;
                PictureSymbol.BitmapTransparencyColor = TransparentColor;
                return PictureSymbol;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }

        }

        internal static object missing = Type.Missing;

        #endregion

#endif

        internal static string GetUser()
        {
            string[] fulluser = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split(new Char[] { '\\' });
            return fulluser[fulluser.Length - 1];
        }
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
