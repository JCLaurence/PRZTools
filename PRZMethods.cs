//#define ARCMAP

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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using PRZC = NCC.PRZTools.PRZConstants;
using PRZH = NCC.PRZTools.PRZHelper;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace NCC.PRZTools
{

    public enum IntersectType
    {
        GraphicPoly,
        FeaturePoly
    }

    internal static class PRZMethods
    {

        internal static async Task<bool> ValidatePRZGroupLayers()
        {
            try
            {
                // Verify that the Project Workspace exists
                string wspath = PRZH.GetProjectWSPath();
                if (!PRZH.ProjectWSExists())
                {
                    ProMsgBox.Show("Project Workspace does not exist at this path:" +
                                     Environment.NewLine + Environment.NewLine +
                                     wspath + Environment.NewLine + Environment.NewLine +
                                     "Please verify Workspace Settings");
                    return false;
                }

                // Verify that Project GDB exists
                string gdbpath = PRZH.GetProjectGDBPath();
                if (!await PRZH.ProjectGDBExists())
                {
                    ProMsgBox.Show("Project Workspace File Geodatabase does not exist at this path:" +
                                     Environment.NewLine + Environment.NewLine +
                                     gdbpath + Environment.NewLine + Environment.NewLine +
                                     "Please verify Workspace Settings");
                    return false;
                }

                // Get the MapView information
                MapView mv = MapView.Active;
                if (mv is null) return false;   // no active map
                if (!mv.IsReady) return false;  // active map is busy
                if (mv.ViewingMode != MapViewingMode.Map) return false; // we want regular old 2D map, no 3D stuff or 2D stereo stuff

                // Get the Map owning the active mapview
                Map map = mv.Map;

                if (map.MapType.ToString() != "Map") return false;  // has to be a plain old Map

                // Remove any instances of PRZ Geodatabase layers in the map
                if (!await RemovePRZLayersAndTables())
                {
                    return false;
                }

                // *** PRZ GROUP LAYER *******************************************

                // Insert or Retrieve Main PRZ group layer at top-level in map
                GroupLayer GL_PRZ = null;

                var glyrs = map.FindLayers(PRZC.c_GROUPLAYER_PRZ, false).OfType<GroupLayer>().ToList();
                if (glyrs.Count == 0)
                {
                    // Main GL not found at top level - add it
                    await QueuedTask.Run(() =>
                    {
                        GL_PRZ = LayerFactory.Instance.CreateGroupLayer(map, 0, PRZC.c_GROUPLAYER_PRZ);
                    });
                }
                else if (glyrs.Count > 1)
                {
                    MessageBox.Show(PRZC.c_GROUPLAYER_PRZ + " Group Layer appears more than once.  There can be only one.");
                    return false;
                }
                else
                {
                    GL_PRZ = glyrs[0];
                    await QueuedTask.Run(() =>
                    {
                        map.MoveLayer(GL_PRZ, 0);
                    });
                }

                // Insert or retrieve Status Group Layer in PRZ Group Layer
                GroupLayer GL_STATUS = null;

                glyrs = GL_PRZ.FindLayers(PRZC.c_GROUPLAYER_STATUS, false).OfType<GroupLayer>().ToList();
                if (glyrs.Count == 0)
                {
                    // Status GL not found in PRZ GL - add it
                    await QueuedTask.Run(() =>
                    {
                        GL_STATUS = LayerFactory.Instance.CreateGroupLayer(GL_PRZ, 0, PRZC.c_GROUPLAYER_STATUS);
                    });
                }
                else if (glyrs.Count > 1)
                {
                    MessageBox.Show(PRZC.c_GROUPLAYER_STATUS + " Group Layer appears more than once.  There can be only one.");
                    return false;
                }
                else
                {
                    GL_STATUS = glyrs[0];
                    await QueuedTask.Run(() =>
                    {
                        GL_PRZ.MoveLayer(GL_STATUS, 0);
                    });
                }

                // Insert or retrieve Cost Group Layer in PRZ Group Layer
                GroupLayer GL_COST = null;

                glyrs = GL_PRZ.FindLayers(PRZC.c_GROUPLAYER_COST, false).OfType<GroupLayer>().ToList();
                if (glyrs.Count == 0)
                {
                    // Cost GL not found in PRZ GL - add it
                    await QueuedTask.Run(() =>
                    {
                        GL_COST = LayerFactory.Instance.CreateGroupLayer(GL_PRZ, 1, PRZC.c_GROUPLAYER_COST);
                    });
                }
                else if (glyrs.Count > 1)
                {
                    MessageBox.Show(PRZC.c_GROUPLAYER_COST + " Group Layer appears more than once.  There can be only one.");
                    return false;
                }
                else
                {
                    GL_COST = glyrs[0];
                    await QueuedTask.Run(() =>
                    {
                        GL_PRZ.MoveLayer(GL_COST, 1);
                    });
                }

                // Insert or retrieve CF Group Layer in PRZ Group Layer
                GroupLayer GL_CF = null;

                glyrs = GL_PRZ.FindLayers(PRZC.c_GROUPLAYER_CF, false).OfType<GroupLayer>().ToList();
                if (glyrs.Count == 0)
                {
                    // CF GL not found in PRZ GL - add it
                    await QueuedTask.Run(() =>
                    {
                        GL_CF = LayerFactory.Instance.CreateGroupLayer(GL_PRZ, 2, PRZC.c_GROUPLAYER_CF);
                    });
                }
                else if (glyrs.Count > 1)
                {
                    MessageBox.Show(PRZC.c_GROUPLAYER_CF + " Group Layer appears more than once.  There can be only one.");
                    return false;
                }
                else
                {
                    GL_CF = glyrs[0];
                    await QueuedTask.Run(() =>
                    {
                        GL_PRZ.MoveLayer(GL_CF, 2);
                    });
                }

                // Finally (at top level), remove all layers from PRZ Group Layer that are not one of the 3 child group layers
                List<Layer> LayersToDelete = new List<Layer>();
                var lyrs = GL_PRZ.Layers;
                foreach (var lyr in lyrs)
                {
                    if (lyr is GroupLayer)
                    {
                        if (lyr.Name != PRZC.c_GROUPLAYER_STATUS && lyr.Name != PRZC.c_GROUPLAYER_COST && lyr.Name != PRZC.c_GROUPLAYER_CF)
                        {
                            LayersToDelete.Add(lyr);
                        }
                    }
                    else
                    {
                        LayersToDelete.Add(lyr);
                    }
                }

                await QueuedTask.Run(() =>
                {
                    GL_PRZ.RemoveLayers(LayersToDelete);
                });

                // Add the 2 study area layers and the planning unit layer
                bool pufcexists = await PRZH.PlanningUnitFCExists();
                bool safcexists = await PRZH.StudyAreaFCExists();
                bool sabfcexists = await PRZH.StudyAreaBufferFCExists();

                int i = 3;

                if (sabfcexists)
                {
                    string sabfcpath = PRZH.GetStudyAreaBufferFCPath();

                    Uri uri = new Uri(sabfcpath);
                    await QueuedTask.Run(() =>
                    {
                        var sabFL = LayerFactory.Instance.CreateFeatureLayer(uri, GL_PRZ, i++, PRZC.c_LAYER_STUDY_AREA_BUFFER);
                        PRZH.ApplyLegend_SAB_Simple(sabFL);
                    });
                }

                if (safcexists)
                {
                    string safcpath = PRZH.GetStudyAreaFCPath();

                    Uri uri = new Uri(safcpath);
                    await QueuedTask.Run(() =>
                    {
                        var saFL = LayerFactory.Instance.CreateFeatureLayer(uri, GL_PRZ, i++, PRZC.c_LAYER_STUDY_AREA);
                        PRZH.ApplyLegend_SA_Simple(saFL);
                    });
                }

                if (pufcexists)
                {
                    string pufcpath = PRZH.GetPlanningUnitFCPath();

                    Uri uri = new Uri(pufcpath);
                    await QueuedTask.Run(() =>
                    {
                        var puFL = LayerFactory.Instance.CreateFeatureLayer(uri, GL_PRZ, i++, PRZC.c_LAYER_PLANNING_UNITS);
                        PRZH.ApplyLegend_PU_Simple(puFL);
                    });
                }

                // ***************************************************************

                // *** STATUS GROUP LAYER ****************************************

                // Status Exclude Group Layer
                GroupLayer GL_STATUS_EXCLUDE = null;

                glyrs = GL_STATUS.FindLayers(PRZC.c_GROUPLAYER_STATUS_EXCLUDE, false).OfType<GroupLayer>().ToList();
                if (glyrs.Count == 0)
                {
                    // Status Exclude GL not found in Status GL - add it
                    await QueuedTask.Run(() =>
                    {
                        GL_STATUS_EXCLUDE = LayerFactory.Instance.CreateGroupLayer(GL_STATUS, 0, PRZC.c_GROUPLAYER_STATUS_EXCLUDE);
                    });
                }
                else if (glyrs.Count > 1)
                {
                    MessageBox.Show(PRZC.c_GROUPLAYER_STATUS_EXCLUDE + " Group Layer appears more than once.  There can be only one.");
                    return false;
                }
                else
                {
                    GL_STATUS_EXCLUDE = glyrs[0];
                    await QueuedTask.Run(() =>
                    {
                        GL_STATUS.MoveLayer(GL_STATUS_EXCLUDE, 0);
                    });
                }

                // Status Include Group Layer
                GroupLayer GL_STATUS_INCLUDE = null;

                glyrs = GL_STATUS.FindLayers(PRZC.c_GROUPLAYER_STATUS_INCLUDE, false).OfType<GroupLayer>().ToList();
                if (glyrs.Count == 0)
                {
                    // Status Include GL not found in Status GL - add it
                    await QueuedTask.Run(() =>
                    {
                        GL_STATUS_INCLUDE = LayerFactory.Instance.CreateGroupLayer(GL_STATUS, 1, PRZC.c_GROUPLAYER_STATUS_INCLUDE);
                    });
                }
                else if (glyrs.Count > 1)
                {
                    MessageBox.Show(PRZC.c_GROUPLAYER_STATUS_INCLUDE + " Group Layer appears more than once.  There can be only one.");
                    return false;
                }
                else
                {
                    GL_STATUS_INCLUDE = glyrs[0];
                    await QueuedTask.Run(() =>
                    {
                        GL_STATUS.MoveLayer(GL_STATUS_INCLUDE, 1);
                    });
                }

                // Finally, remove all layers from Status Group Layer that are not one of the 2 child group layers
                LayersToDelete.Clear();
                lyrs = GL_STATUS.Layers;

                foreach (var lyr in lyrs)
                {
                    if (lyr is GroupLayer)
                    {
                        if (lyr.Name != PRZC.c_GROUPLAYER_STATUS_EXCLUDE && lyr.Name != PRZC.c_GROUPLAYER_STATUS_INCLUDE)
                        {
                            LayersToDelete.Add(lyr);
                        }
                    }
                    else
                    {
                        LayersToDelete.Add(lyr);
                    }
                }

                await QueuedTask.Run(() =>
                {
                    GL_STATUS.RemoveLayers(LayersToDelete);
                });

                // ***************************************************************

                // *** STATUS EXCLUDE GROUP LAYER ********************************

                // Remove all non-eligible layers from Status Exclude Group Layer
                LayersToDelete.Clear();
                lyrs = GL_STATUS_EXCLUDE.Layers;

                foreach (var lyr in lyrs)
                {
                    if (!(lyr is FeatureLayer) && !(lyr is RasterLayer))
                    {
                        LayersToDelete.Add(lyr);                        
                    }
                }

                await QueuedTask.Run(() =>
                {
                    GL_STATUS_EXCLUDE.RemoveLayers(LayersToDelete);
                });

                // ***************************************************************

                // *** STATUS INCLUDE GROUP LAYER ********************************

                // Remove all non-eligible layers from Status Include Group Layer
                LayersToDelete.Clear();
                lyrs = GL_STATUS_INCLUDE.Layers;

                foreach (var lyr in lyrs)
                {
                    if (!(lyr is FeatureLayer) && !(lyr is RasterLayer))
                    {
                        LayersToDelete.Add(lyr);
                    }
                }

                await QueuedTask.Run(() =>
                {
                    GL_STATUS_INCLUDE.RemoveLayers(LayersToDelete);
                });

                // ***************************************************************

                // *** COST GROUP LAYER ******************************************

                // Remove all non-eligible layers from Cost Group Layer
                LayersToDelete.Clear();
                lyrs = GL_COST.Layers;

                foreach (var lyr in lyrs)
                {
                    if (!(lyr is FeatureLayer) && !(lyr is RasterLayer))
                    {
                        LayersToDelete.Add(lyr);
                    }
                }

                await QueuedTask.Run(() =>
                {
                    GL_COST.RemoveLayers(LayersToDelete);
                });

                // ***************************************************************

                // *** CF GROUP LAYER ******************************************

                // Remove all non-eligible layers from CF Group Layer
                LayersToDelete.Clear();
                lyrs = GL_CF.Layers;

                foreach (var lyr in lyrs)
                {
                    if (!(lyr is FeatureLayer) && !(lyr is RasterLayer))
                    {
                        LayersToDelete.Add(lyr);
                    }
                }

                await QueuedTask.Run(() =>
                {
                    GL_CF.RemoveLayers(LayersToDelete);
                });

                // ***************************************************************


                // *** Set Contents to Drawing Order Mode
                IPlugInWrapper wrapper = FrameworkApplication.GetPlugInWrapper("esri_mapping_showDrawingOrderTOC");
                var command = wrapper as ICommand; // tool and command(Button) supports this

                if ((command != null) && command.CanExecute(null))
                {
                    command.Execute(null);
                    
                }

                // *** I still need to somehow enable/active/refresh the buttons on the Contents Pane
                //     since the above command execution doesn't refresh the pane UI.

                //var pane = FrameworkApplication.DockPaneManager.Find("esri_core_contentsDockPane");

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
            finally
            {
                // Any clean-up regardless of errors caught above goes here
            }
        }

        internal static async Task<bool> RemovePRZLayersAndTables()
        {
            try
            {
                var map = MapView.Active.Map;

                List<StandaloneTable> tables_to_delete = new List<StandaloneTable>();
                List<Layer> layers_to_delete = new List<Layer>();

                await QueuedTask.Run(async () =>
                {
                    using (var geodatabase = await PRZH.GetProjectGDB())
                    {
                        string gdbpath = geodatabase.GetPath().AbsolutePath;

                        // Standalone Tables
                        var standalone_tables = map.StandaloneTables.ToList();
                        foreach (var standalone_table in standalone_tables)
                        {
                            using (Table table = standalone_table.GetTable())
                            {
                                if (table != null)
                                {
                                    try
                                    {
                                        using (var store = table.GetDatastore())
                                        {
                                            if (store != null && store is Geodatabase)
                                            {
                                                var uri = store.GetPath();
                                                if (uri != null)
                                                {
                                                    string newpath = uri.AbsolutePath;

                                                    if (gdbpath == newpath)
                                                    {
                                                        tables_to_delete.Add(standalone_table);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        continue;
                                    }
                                }
                            }
                        }

                        // Layers
                        var layers = map.GetLayersAsFlattenedList().ToList();
                        foreach (var layer in layers)
                        {
                            var uri = layer.GetPath();
                            if (uri != null)
                            {
                                string layer_path = uri.AbsolutePath;

                                if (layer_path.StartsWith(gdbpath))
                                {
                                    layers_to_delete.Add(layer);
                                }
                            }
                        }
                    }

                    map.RemoveStandaloneTables(tables_to_delete);
                    map.RemoveLayers(layers_to_delete);
                    await MapView.Active.RedrawAsync(false);
                });

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }





#if ARCMAP

        public static string GetWorkingFileGDBPath()
        {
            try
            {
                return Properties.Settings.Default.MARXAN_FILEGDB;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return string.Empty;
            }
        }

        public static string GetMarxanProjectFolder(bool CheckForPresence)
        {
            try
            {
                string MarxanProjectFolder = Properties.Settings.Default.MARXAN_FOLDER;

                if (CheckForPresence)
                {
                    if (!System.IO.Directory.Exists(MarxanProjectFolder) | !System.IO.Path.IsPathRooted(MarxanProjectFolder))
                        MarxanProjectFolder = "";
                }

                return MarxanProjectFolder;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return "";
            }
        }

        public static string GetTempFileGDBPath()
        {
            try
            {
                string TempDirPath = GetDLLPath();
                string TempFGDBPath = System.IO.Path.Combine(TempDirPath, SC.c_TEMP_FILEGDB_NAME);
                return TempFGDBPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return string.Empty;
            }
        }

        public static string GetDLLPath()
        {
            try
            {
                string thePath = Assembly.GetExecutingAssembly().Location;
                string theDir = System.IO.Path.GetDirectoryName(thePath);
                return theDir;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return string.Empty;
            }
        }

        public static IFeatureLayer GetGridFL(IMap FocusMap)
        {
            try
            {
                IGroupLayer GL_Shmarxan = RetrieveGroupLayer(FocusMap, SC.c_GROUPLAYER_SHMARXAN);

                if (GL_Shmarxan == null)
                {
                    MessageBox.Show("Unable to retrieve the '" + SC.c_GROUPLAYER_SHMARXAN + "' Group Layer");
                    return null;
                }

                ICompositeLayer CL = (ICompositeLayer)GL_Shmarxan;

                for (int i = 0; i < CL.Count; i++)
                {
                    ILayer lyr = CL.get_Layer(i);
                    if (lyr.Valid && lyr is IFeatureLayer & lyr.Name == SC.c_LAYER_PLANNING_UNIT_GRID)
                    {
                        return (IFeatureLayer)lyr;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static IFeatureLayer GetFeatureLayer(IMap FocusMap, string LayerName)
        {
            IFeatureLayer FL = null;

            try
            {
                if (FocusMap.LayerCount > 0)
                {
                    IEnumLayer EnumLayers = null;
                    UID id = new UIDClass();
                    id.Value = "{40A9E885-5533-11d0-98BE-00805F7CED21}";

                    try
                    {
                        EnumLayers = FocusMap.get_Layers(id, true);
                    }
                    catch
                    {
                        //Marshal.FinalReleaseComObject(EnumLayers);
                        return null;
                    }

                    FL = (IFeatureLayer)EnumLayers.Next();
                    while (FL != null)
                    {
                        if (FL.Valid == true)
                        {
                            if (FL.Name == LayerName)
                            {
                                break;
                            }
                        }
                        FL = (IFeatureLayer)EnumLayers.Next();
                    }
                    return FL;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        /// <summary>
        /// Join an ITable to a Feature Layer or a Standalone Table
        /// </summary>
        /// <param name="SourceTable">FeatureLayer or StandaloneTable object</param>
        /// <param name="SourceField">Field in base table</param>
        /// <param name="JoinTable">ITable to be joined to base table</param>
        /// <param name="JoinField">Field in join table</param>        
        public static void JoinTables(object SourceTable, string SourceField, ITable JoinTable, string JoinField)
        {
            try
            {
                IDisplayTable DT = (IDisplayTable)SourceTable;
                ITable RelQueryTab = DT.DisplayTable;   //returns the relquerytable from the featurelayer or standalone table

                IMemoryRelationshipClassFactory MemClassFact = new MemoryRelationshipClassFactoryClass();
                IRelationshipClass RelClass = MemClassFact.Open("TableToTable", (IObjectClass)JoinTable, JoinField, (IObjectClass)RelQueryTab,
                                                                SourceField, "forward", "backward", esriRelCardinality.esriRelCardinalityOneToMany);

                IDisplayRelationshipClass DispRelClass = (IDisplayRelationshipClass)SourceTable;
                DispRelClass.DisplayRelationshipClass(RelClass, esriJoinType.esriLeftOuterJoin);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Remove all joins from a featurelayer or standalone table
        /// </summary>
        /// <param name="BaseTable">This object is either a FeatureLayer or a StandaloneTable object</param>        
        public static void RemoveJoins(object BaseTable)
        {
            try
            {
                IDisplayTable DT = (IDisplayTable)BaseTable;
                ITable RelQueryTab = DT.DisplayTable;		//retrieves a RelQueryTable
                if (RelQueryTab is IRelQueryTable)
                {
                    IDisplayRelationshipClass RelClass = (IDisplayRelationshipClass)BaseTable;
                    RelClass.DisplayRelationshipClass(null, esriJoinType.esriLeftOuterJoin);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        public static IFeatureLayer RetrievePlanningUnitLayer(IMap FocusMap)
        {
            try
            {
                //First, make sure layer exists
                IFeatureLayer GridFL = GetGridFL(FocusMap);
                if (GridFL == null)
                {
                    MessageBox.Show("Unable to find the Planning Unit Grid layer!" + Environment.NewLine + Environment.NewLine +
                                    "Please ensure that the current data frame includes a polygon feature layer containing your " +
                                    "planning units.  It must have the name '" + SC.c_LAYER_PLANNING_UNIT_GRID + "'.  Don't include the single quotes...");
                    return null;
                }

                IFeatureClass GridFC = GridFL.FeatureClass;

                //Next, make sure the key fields are present
                int ix_id = GridFC.FindField(SC.c_FLD_PUFC_ID);
                int ix_status = GridFC.FindField(SC.c_FLD_PUFC_STATUS);
                int ix_cost = GridFC.FindField(SC.c_FLD_PUFC_COST);

                //id field
                if (ix_id == -1)    //can't find the id field
                {
                    MessageBox.Show("INVALID Planning Unit Grid layer!!" + Environment.NewLine + Environment.NewLine +
                                    "This layer must include a long integer field named '" + SC.c_FLD_PUFC_ID + "'.");
                    return null;
                }
                else    //check that the field is an integer field
                {
                    IField fld = GridFC.Fields.get_Field(ix_id);
                    if (fld.Type != esriFieldType.esriFieldTypeInteger)
                    {
                        MessageBox.Show("INVALID Planning Unit Grid layer!!" + Environment.NewLine + Environment.NewLine +
                                        "'" + SC.c_FLD_PUFC_ID + "' field is not a long integer field.");
                        return null;
                    }
                }

                //status field
                if (ix_status == -1)    //can't find the status field
                {
                    MessageBox.Show("INVALID Planning Unit Grid layer!!" + Environment.NewLine + Environment.NewLine +
                                    "This layer must include a long integer field named '" + SC.c_FLD_PUFC_STATUS + "'.");
                    return null;
                }
                else    //check that status field actually is an integer field
                {
                    IField fld = GridFC.Fields.get_Field(ix_status);
                    if (fld.Type != esriFieldType.esriFieldTypeInteger)
                    {
                        MessageBox.Show("INVALID Planning Unit Grid layer!!" + Environment.NewLine + Environment.NewLine +
                                        "'" + SC.c_FLD_PUFC_STATUS + "' field is not a long integer field.");
                        return null;
                    }
                }

                //cost field
                if (ix_cost == -1)  //can't find the cost field
                {
                    MessageBox.Show("INVALID Planning Unit Grid layer!!" + Environment.NewLine + Environment.NewLine +
                                    "This layer must include a double field named '" + SC.c_FLD_PUFC_COST + "'.");
                    return null;
                }
                else    //check that the cost field actually is a double field
                {
                    IField fld = GridFC.Fields.get_Field(ix_cost);
                    if (fld.Type != esriFieldType.esriFieldTypeDouble)
                    {
                        MessageBox.Show("INVALID Planning Unit Grid layer!!" + Environment.NewLine + Environment.NewLine +
                                        "'" + SC.c_FLD_PUFC_COST + "' field is not a numeric (double) field.");
                        return null;
                    }
                }

                //Make sure the planning unit grid layer contains at least one feature
                if (GridFC.FeatureCount(null) == 0)
                {
                    MessageBox.Show("INVALID Planning Unit Grid layer!!" + Environment.NewLine + Environment.NewLine +
                                    "This layer must contain at least one feature.  Come on, folks. Try a little harder.");
                    return null;
                }

                //Check for Multi-part features and duplicate id values
                List<string> LIST_ID = new List<string>();

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    IFeatureCursor searchFeatureCursor = GridFC.Search(null, true);
                    comReleaser.ManageLifetime(searchFeatureCursor);

                    IFeature searchFeature = searchFeatureCursor.NextFeature();

                    while (searchFeature != null)
                    {
                        // check for multipart features
                        IPolygon poly = (IPolygon)searchFeature.ShapeCopy;
                        if (poly.ExteriorRingCount > 1)
                        {
                            MessageBox.Show("Feature " + searchFeature.OID.ToString() + " in the Planning Unit Grid layer is a multipart feature.  It consists of " + poly.ExteriorRingCount.ToString() +
                                            " exterior rings.  Please ensure that the Planning Unit Grid layer has no multipart features.");
                            return null;
                        }

                        //check for null id field values
                        string ID = searchFeature.get_Value(ix_id).ToString();
                        if (ID.Length == 0)
                        {
                            MessageBox.Show("Feature " + searchFeature.OID.ToString() + " in the Planning Unit Grid layer seems to have no value in the 'id' field...  This is a concern!  Check it out!");
                            return null;
                        }

                        //check for non-numeric id field values
                        int res;
                        bool outcome = Int32.TryParse(ID, out res);
                        if (!outcome)
                        {
                            MessageBox.Show("Feature " + searchFeature.OID.ToString() + " seems to have a non-numeric value in the 'id' field...  This is a concern!  Check it out!");
                            return null;
                        }

                        //check for non-unique id field values
                        if (LIST_ID.Contains(ID))
                        {
                            MessageBox.Show("The 'id' field in the planning unit grid layer is not unique...  The value " + ID + " appears more than once in the 'id' field.");
                            return null;
                        }
                        else
                        {
                            LIST_ID.Add(ID);
                        }

                        searchFeature = searchFeatureCursor.NextFeature();
                    }
                }

                return GridFL;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return null;
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public static ISpatialReference RetrieveFocusMapSpatialReference(IMap FocusMap)
        {
            try
            {
                ISpatialReference SR = FocusMap.SpatialReference;

                if (SR == null)
                {
                    MessageBox.Show("Please set the Data Frame's spatial reference to any PROJECTED COORDINATE SYSTEM whose units are METERS.  This is to keep JC's head from exploding." + Environment.NewLine +
                                    "It is currently not set to anything");
                    return null;
                }
                else if (SR is IUnknownCoordinateSystem)
                {
                    MessageBox.Show("Please set the Data Frame's spatial reference to any PROJECTED COORDINATE SYSTEM whose units are METERS.  This is to keep JC's head from exploding." + Environment.NewLine +
                                    "It is currently an Unknown Coordinate System");
                    return null;
                }
                else if (SR is IGeographicCoordinateSystem)
                {
                    IGeographicCoordinateSystem GCS = (IGeographicCoordinateSystem)SR;
                    MessageBox.Show("Please set the Data Frame's spatial reference to any PROJECTED COORDINATE SYSTEM whose units are METERS.  This is to keep JC's head from exploding." + Environment.NewLine +
                                    "It is currently set to a Geographic Coordinate System (" + GCS.Name + ").");
                    return null;
                }
                else if (SR is IProjectedCoordinateSystem)
                {
                    IProjectedCoordinateSystem PCS = (IProjectedCoordinateSystem)SR;
                    ILinearUnit unit = PCS.CoordinateUnit;
                    if (unit.MetersPerUnit != 1)
                    {
                        MessageBox.Show("Please set the Data Frame's spatial reference to any PROJECTED COORDINATE SYSTEM whose units are METERS.  This is to keep JC's head from exploding" + Environment.NewLine +
                                        "It is currently set to a projected coordinate system with non-meter units (" + PCS.Name + "  units=" + unit.Name + ")");
                        return null;
                    }
                }
                return SR;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static IFeatureWorkspace RetrieveTempWorkspace()
        {
            try
            {
                string TempFileGDBPath = GetTempFileGDBPath();
                string DLLPath = GetDLLPath();

                IFeatureWorkspace FWS = null;
                IWorkspaceFactory Fact = new FileGDBWorkspaceFactoryClass();

                if (Fact.IsWorkspace(TempFileGDBPath))  //path is a valid workspace
                    FWS = (IFeatureWorkspace)Fact.OpenFromFile(TempFileGDBPath, SH.IApp.hWnd);
                else
                {
                    IWorkspaceName WSName = Fact.Create(DLLPath, SC.c_TEMP_FILEGDB_NAME, null, SH.IApp.hWnd);
                    FWS = (IFeatureWorkspace)((IName)WSName).Open();
                }

                if (FWS == null)
                    MessageBox.Show("Unable to access Temp Workspace at: " + Environment.NewLine + TempFileGDBPath);
                return FWS;         // might be null or an actual reference
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static IFeatureWorkspace RetrieveWorkingWorkspace()
        {
            try
            {
                string WorkingPath = GetWorkingFileGDBPath();

                IWorkspaceFactory2 Fact = new FileGDBWorkspaceFactoryClass();
                IFeatureWorkspace FWS = null;

                if (Fact.IsWorkspace(WorkingPath))	//path is valid workspace
                {
                    FWS = (IFeatureWorkspace)Fact.OpenFromFile(WorkingPath, SH.IApp.hWnd);
                }
                else
                {
                    MessageBox.Show("Working File Geodatabase setting is not a valid workspace - please use the settings form to choose a valid file geodatabase workspace");
                }

                return FWS;     //might be null - that's OK
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        public static IGroupLayer RetrieveGroupLayer(IMap FocusMap, string LayerName)
        {
            try
            {
                IEnumLayer EnumLayers = null;
                UID id = new UIDClass();
                id.Value = "{EDAD6644-1810-11D1-86AE-0000F8751720}";            //VALUE FOR GROUP LAYERS

                try
                {
                    EnumLayers = FocusMap.get_Layers(id, true);
                }
                catch
                {
                }

                if (EnumLayers == null)	//no group layers at all in the focus map - stop here
                {
                    MessageBox.Show("The Data Frame does not contain a group layer called '" + LayerName + "'.");
                    return null;
                }

                IGroupLayer GL = null;
                IGroupLayer GL_temp = (IGroupLayer)EnumLayers.Next();
                int LayerCount = 0;

                while (GL_temp != null)
                {
                    if (GL_temp.Name == LayerName)
                    {
                        GL = GL_temp;
                        LayerCount++;
                    }
                    GL_temp = (IGroupLayer)EnumLayers.Next();
                }

                //NOT FOUND? - QUIT
                if (GL == null) //no group layer with correct name
                {
                    MessageBox.Show("The Data Frame does not contain a group layer called '" + LayerName + "'.");
                    return null;
                }

                //MORE THAN ONE FOUND? - QUIT
                if (LayerCount > 1) //multiple group layers were found with name = LayerName
                {
                    MessageBox.Show(LayerCount.ToString() + " group layers called '" + LayerName +
                                    "' were found.  Please ensure that only one group layer has that name.");
                    return null;
                }

                return GL;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        /// <summary>
        /// Determines if a five character string contains an integer from 000 to 100 within two brackets
        /// </summary>
        /// <param name="TargetString">Five-character string to be validated, of format (nnn) or [nnn]</param>
        /// <param name="BracketType">string indicating bracket type by storing the left-hand bracket character:  "(" or "["</param>
        /// <param name="IntVal">receives integer value between 0 and 100 inclusive, if format is correct.  Returns -1 if incorrect</param>
        /// <returns>true if string format is valid</returns>
        public static bool ValidateNumberString(string TargetString, string BracketType, out int IntVal)
        {
            IntVal = -1;

            string LeftBracket = "";
            string RightBracket = "";

            if (BracketType == "(")
            {
                LeftBracket = "(";
                RightBracket = ")";
            }
            else if (BracketType == "[")
            {
                LeftBracket = "[";
                RightBracket = "]";
            }

            try
            {
                //validate bracket part of string
                if (TargetString[0].ToString() != LeftBracket | TargetString[4].ToString() != RightBracket)     //bracket part of format is wrong, quit.
                    return false;

                //add three chars together
                string TextNum = TargetString[1].ToString().Trim() + TargetString[2].ToString().Trim() + TargetString[3].ToString().Trim();

                //make sure there are three visibls chars
                if (TextNum.Length != 3)    //must be at least three numbers here, no whitespace characters
                    return false;

                //try to convert three concatenated chars into an integer
                if (Int32.TryParse(TextNum, out IntVal) && (IntVal >= 0 & IntVal <= 100))   // a valid integer and within acceptable range (0 to 100)
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public static double GetTileArea(IFeatureLayer FL, out double SideLength, out int SideCount)
        {
            SideLength = 0;
            SideCount = 0;

            //get featureclass
            IFeatureClass FC = FL.FeatureClass;
            if (FC.FeatureCount(null) == 0)
            {
                MessageBox.Show("Planning Unit Grid is empty.  It has no tiles.  Huh?");
                return 0;
            }

            //field indexes
            int ix_length = FC.FindField(FC.LengthField.Name);
            int ix_area = FC.FindField(FC.AreaField.Name);

            try
            {
                using (ComReleaser comReleaser = new ComReleaser())
                {
                    IFeatureCursor searchFeatureCursor = FC.Search(null, false);
                    comReleaser.ManageLifetime(searchFeatureCursor);

                    IFeature searchFeature = searchFeatureCursor.NextFeature();

                    if (searchFeature == null)
                    {
                        MessageBox.Show("First Feature found in Planning Unit Grid is null... There may be no features in this dataset.");
                        return 0;
                    }

                    double tilearea = (double)searchFeature.get_Value(ix_area);				//Area of Tile	- this is the return value of this method
                    double tileperim = (double)searchFeature.get_Value(ix_length);			//Perimeter of Tile

                    IGeometry TileGeom = searchFeature.ShapeCopy;	// a polygon
                    IPointCollection PointColl = (IPointCollection)TileGeom;
                    int PointCount = PointColl.PointCount;

                    if (PointCount == 5)	//square
                    {
                        SideCount = 4;
                    }
                    else if (PointCount == 7)	//hexagon
                    {
                        SideCount = 6;
                    }

                    SideLength = tileperim / SideCount;		//assumption here is that tiles have lengths of same size
                    return tilearea;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return 0;
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public static bool ValidateShmarxanGroupLayers()
        {
            try
            {
                IMxDocument doc = SH.IMxDoc;
                IMap FocusMap = doc.FocusMap;

                //Get Workspace
                IFeatureWorkspace FWS = RetrieveWorkingWorkspace();
                if (FWS == null)
                {
                    MessageBox.Show("Unable to retrieve Workspace.  Please use the Settings Window to set a workspace.");
                    return false;
                }
                IWorkspace WS = (IWorkspace)FWS;

                //Get GridFC and GridFL
                IFeatureClass GridFC = (IFeatureClass)GetDatasetFromWorkspace(SC.c_FCNAME_PLANNING_UNIT_GRID, esriDatasetType.esriDTFeatureClass, WS);
                IFeatureLayer GridFL = null;

                // Switch the Table of Contents over to 'Display' view
                if (doc.CurrentContentsView.Name != "Display")
                {
                    IContentsView cv = null;

                    for (int i = 0; i < doc.ContentsViewCount; i++)
                    {
                        cv = doc.ContentsView[i];
                        if (cv.Name == "Display")
                        {
                            doc.CurrentContentsView = cv;
                            break;
                        }
                    }
                }

                if (GridFC == null)
                {
                    MessageBox.Show("No planning unit grid was found in the Marxan File Geodatabase workspace:" +
                                    Environment.NewLine + Environment.NewLine +
                                    WS.PathName + Environment.NewLine + Environment.NewLine +
                                    "Don't think you've built it yet...");
                }
                else
                {
                    GridFL = new FeatureLayerClass();
                    GridFL.Name = SC.c_LAYER_PLANNING_UNIT_GRID;
                    GridFL.FeatureClass = GridFC;
                    ApplySimpleGridLegend(GridFL);
                }

                // Remove any Grid feature layer from the focus map
                RemoveGridFL();

                //Prepare empty group layers
                IGroupLayer GL_CF = new GroupLayerClass();
                GL_CF.Name = SC.c_GROUPLAYER_CF;
                GL_CF.Visible = true;

                IGroupLayer GL_COST = new GroupLayerClass();
                GL_COST.Name = SC.c_GROUPLAYER_COST;
                GL_COST.Visible = true;

                IGroupLayer GL_STATUS_INCLUDE = new GroupLayerClass();
                GL_STATUS_INCLUDE.Name = SC.c_GROUPLAYER_STATUS_INCLUDE;
                GL_STATUS_INCLUDE.Visible = true;

                IGroupLayer GL_STATUS_EXCLUDE = new GroupLayerClass();
                GL_STATUS_EXCLUDE.Name = SC.c_GROUPLAYER_STATUS_EXCLUDE;
                GL_STATUS_EXCLUDE.Visible = true;

                IGroupLayer GL_STATUS_PREFER = new GroupLayerClass();
                GL_STATUS_PREFER.Name = SC.c_GROUPLAYER_STATUS_PREFER;
                GL_STATUS_PREFER.Visible = true;

                IGroupLayer GL_STATUS_MAIN = new GroupLayerClass();
                GL_STATUS_MAIN.Name = SC.c_GROUPLAYER_STATUS;
                GL_STATUS_MAIN.Visible = true;
                GL_STATUS_MAIN.Add((ILayer)GL_STATUS_INCLUDE);
                GL_STATUS_MAIN.Add((ILayer)GL_STATUS_EXCLUDE);
                GL_STATUS_MAIN.Add((ILayer)GL_STATUS_PREFER);

                IGroupLayer GL_SHMARXAN = new GroupLayerClass();
                GL_SHMARXAN.Name = SC.c_GROUPLAYER_SHMARXAN;
                GL_SHMARXAN.Visible = true;
                GL_SHMARXAN.Add((ILayer)GL_COST);
                GL_SHMARXAN.Add((ILayer)GL_STATUS_MAIN);
                GL_SHMARXAN.Add((ILayer)GL_CF);
                if (GridFL != null)
                    GL_SHMARXAN.Add((ILayer)GridFL);

        #region FIRST LOOK FOR THE OVERALL SHMARXAN GROUP LAYER
                IEnumLayer enumLayers = null;
                UID id = new UIDClass();
                id.Value = "{EDAD6644-1810-11D1-86AE-0000F8751720}";			//GROUP LAYER

                enumLayers = FocusMap.get_Layers(id, false);

                //If no group layers, add the shmarxan group layer and quit
                if (enumLayers == null)	//no group layers at all - add them
                {
                    FocusMap.AddLayer((ILayer)GL_SHMARXAN);
                    doc.UpdateContents();
                    return true;
                }

                //Search top-level group layers for Shmarxan Group Layer
                enumLayers.Reset();
                int ShmarxanCount = 0;
                IGroupLayer shmarxan_gl_finder = null;
                IGroupLayer gl = (IGroupLayer)enumLayers.Next();
                while (gl != null)
                {
                    if (gl.Name == SC.c_GROUPLAYER_SHMARXAN)
                    {
                        shmarxan_gl_finder = gl;
                        ShmarxanCount++;
                    }

                    gl = (IGroupLayer)enumLayers.Next();
                }

                //Add Shmarxan GL if not already present
                if (shmarxan_gl_finder == null)	//Of all top-level group layers, none is called c_GROUPLAYER_SHMARXAN
                {
                    FocusMap.AddLayer((ILayer)GL_SHMARXAN);
                    doc.UpdateContents();
                    return true;
                }
                else if (ShmarxanCount > 1)
                {
                    MessageBox.Show("The Top-Level Group Layer '" + SC.c_GROUPLAYER_SHMARXAN + "' was found " +
                                    ShmarxanCount.ToString() + " times.  Please ensure there is only one group layer with this name.");
                    return false;
                }
                else
                {
                    GL_SHMARXAN = shmarxan_gl_finder;
                }

        #endregion

        #region SEARCH THE SHMARXAN GROUP LAYER FOR COST, STATUS AND CF SUBGROUP LAYERS AND GRID FL
                // At this point, GL_SHMARXAN exists but its contents have not been validated
                // I must search for the Status group layer and the CF group layer
                ICompositeLayer CL = (ICompositeLayer)GL_SHMARXAN;
                IGroupLayer glcost = null;
                IGroupLayer glstatusmain = null;
                IGroupLayer glcf = null;
                IFeatureLayer fl = null;

                int glcfCount = 0;
                int glstatusCount = 0;
                int glcostCount = 0;
                int gridflCount = 0;
                bool CostIsHere = false;
                bool StatusIsHere = false;
                bool CFIsHere = false;
                bool GridFLIsHere = false;

                for (int i = CL.Count - 1; i >= 0; i--)
                {
                    ILayer lyr = CL.get_Layer(i);

                    if (lyr.Name == SC.c_GROUPLAYER_COST & lyr is IGroupLayer)	//a group layer named c_grouplayer_cost
                    {
                        glcost = (IGroupLayer)lyr;
                        CostIsHere = true;
                        glcostCount++;
                    }
                    else if (lyr.Name == SC.c_GROUPLAYER_STATUS & lyr is IGroupLayer)   //a group layer named c_grouplayer_status
                    {
                        glstatusmain = (IGroupLayer)lyr;
                        StatusIsHere = true;
                        glstatusCount++;
                    }
                    else if (lyr.Name == SC.c_GROUPLAYER_CF & lyr is IGroupLayer)   //a group layer named c_grouplayer_cf
                    {
                        glcf = (IGroupLayer)lyr;
                        CFIsHere = true;
                        glcfCount++;
                    }
                    else if (lyr.Name == SC.c_LAYER_PLANNING_UNIT_GRID & lyr is IFeatureLayer)
                    {
                        GridFLIsHere = true;
                        fl = (IFeatureLayer)lyr;
                        gridflCount++;
                    }
                    else if (lyr is IGroupLayer)
                    {
                        GL_SHMARXAN.Delete(lyr);
                    }
                    else if (lyr is IFeatureLayer)
                    {
                        GL_SHMARXAN.Delete(lyr);
                    }
                }

                //cost gl
                if (!CostIsHere)
                {
                    GL_SHMARXAN.Add(GL_COST);
                }
                else if (glcostCount > 1)
                {
                    MessageBox.Show("Multiple '" + SC.c_GROUPLAYER_COST + "' group layers are present within the '" +
                                   SC.c_GROUPLAYER_SHMARXAN + "' group layer.  There should only be one.");
                    return false;
                }
                else
                {
                    GL_COST = glcost;
                }

                //status gl
                if (!StatusIsHere)
                {
                    GL_SHMARXAN.Add(GL_STATUS_MAIN);
                }
                else if (glstatusCount > 1)
                {
                    MessageBox.Show("Multiple '" + SC.c_GROUPLAYER_STATUS + "' group layers are present within the '" +
                                   SC.c_GROUPLAYER_SHMARXAN + "' group layer.  There should only be one.");
                    return false;
                }
                else
                {
                    GL_STATUS_MAIN = glstatusmain;
                }

                //cf gl
                if (!CFIsHere)
                {
                    GL_SHMARXAN.Add(GL_CF);
                }
                else if (glcfCount > 1)
                {
                    MessageBox.Show("Multiple '" + SC.c_GROUPLAYER_CF + "' group layers are present within the '" +
                                   SC.c_GROUPLAYER_SHMARXAN + "' group layer.  There should only be one.");
                    return false;
                }
                else
                {
                    GL_CF = glcf;
                }

                //gridfl
                if (!GridFLIsHere & GridFL != null)
                {
                    GL_SHMARXAN.Add(GridFL);
                }

                //Finally, reorder the Group Layer contents
                IMapLayers maplayers = (IMapLayers)FocusMap;

                maplayers.MoveLayerEx(GL_SHMARXAN, GL_SHMARXAN, GL_COST, 0);
                maplayers.MoveLayerEx(GL_SHMARXAN, GL_SHMARXAN, GL_STATUS_MAIN, 1);
                maplayers.MoveLayerEx(GL_SHMARXAN, GL_SHMARXAN, GL_CF, 2);

        #endregion

        #region VALIDATE THE STATUS GROUP LAYER
                //At this point, GL_STATUS_MAIN still needs to be validated for the three subgroup layers
                CL = (ICompositeLayer)GL_STATUS_MAIN;
                IGroupLayer glinclude = null;
                IGroupLayer glexclude = null;
                IGroupLayer glprefer = null;
                int glincludeCount = 0;
                int glexcludeCount = 0;
                int glpreferCount = 0;
                bool IncludeIsHere = false;
                bool ExcludeIsHere = false;
                bool PreferIsHere = false;

                for (int i = CL.Count - 1; i >= 0; i--)
                {
                    ILayer lyr = CL.get_Layer(i);

                    if (lyr.Name == SC.c_GROUPLAYER_STATUS_INCLUDE & lyr is IGroupLayer)    //the include group layer
                    {
                        glinclude = (IGroupLayer)lyr;
                        IncludeIsHere = true;
                        glincludeCount++;
                    }
                    else if (lyr.Name == SC.c_GROUPLAYER_STATUS_EXCLUDE & lyr is IGroupLayer)   //the exclude group layer
                    {
                        glexclude = (IGroupLayer)lyr;
                        ExcludeIsHere = true;
                        glexcludeCount++;
                    }
                    else if (lyr.Name == SC.c_GROUPLAYER_STATUS_PREFER & lyr is IGroupLayer)    //the prefer group layer
                    {
                        glprefer = (IGroupLayer)lyr;
                        PreferIsHere = true;
                        glpreferCount++;
                    }
                    else if (lyr is IGroupLayer)
                    {
                        GL_STATUS_MAIN.Delete(lyr);
                    }
                    else if (lyr is IFeatureLayer)
                    {
                        GL_STATUS_MAIN.Delete(lyr);
                    }
                }

                //include
                if (!IncludeIsHere)
                {
                    GL_STATUS_MAIN.Add(GL_STATUS_INCLUDE);
                }
                else if (glincludeCount > 1)
                {
                    MessageBox.Show("Multiple '" + SC.c_GROUPLAYER_STATUS_INCLUDE + "' group layers are present within the '" +
                                   SC.c_GROUPLAYER_STATUS + "' group layer.  There should only be one.");
                    return false;
                }
                else
                {
                    GL_STATUS_INCLUDE = glinclude;
                }

                //exclude
                if (!ExcludeIsHere)
                {
                    GL_STATUS_MAIN.Add(GL_STATUS_EXCLUDE);
                }
                else if (glexcludeCount > 1)
                {
                    MessageBox.Show("Multiple '" + SC.c_GROUPLAYER_STATUS_EXCLUDE + "' group layers are present within the '" +
                                   SC.c_GROUPLAYER_STATUS + "' group layer.  There should only be one.");
                    return false;
                }
                else
                {
                    GL_STATUS_EXCLUDE = glexclude;
                }

                //prefer
                if (!PreferIsHere)
                {
                    GL_STATUS_MAIN.Add(GL_STATUS_PREFER);
                }
                else if (glpreferCount > 1)
                {
                    MessageBox.Show("Multiple '" + SC.c_GROUPLAYER_STATUS_PREFER + "' group layers are present within the '" +
                                   SC.c_GROUPLAYER_STATUS + "' group layer.  There should only be one.");
                    return false;
                }
                else
                {
                    GL_STATUS_PREFER = glprefer;
                }

                //Finally, reorder the Group Layer contents
                maplayers.MoveLayerEx(GL_STATUS_MAIN, GL_STATUS_MAIN, GL_STATUS_INCLUDE, 0);
                maplayers.MoveLayerEx(GL_STATUS_MAIN, GL_STATUS_MAIN, GL_STATUS_EXCLUDE, 1);
                maplayers.MoveLayerEx(GL_STATUS_MAIN, GL_STATUS_MAIN, GL_STATUS_PREFER, 2);

        #endregion

                // Ensure that certain group layers don't contain any group layers
                CL = (ICompositeLayer)GL_COST;
                for (int i = CL.Count - 1; i >= 0; i--)
                {
                    ILayer lyr = CL.get_Layer(i);

                    if (lyr is IGroupLayer)
                    {
                        GL_COST.Delete(lyr);
                    }
                }

                CL = (ICompositeLayer)GL_STATUS_INCLUDE;
                for (int i = CL.Count - 1; i >= 0; i--)
                {
                    ILayer lyr = CL.get_Layer(i);

                    if (lyr is IGroupLayer)
                    {
                        GL_STATUS_INCLUDE.Delete(lyr);
                    }
                }

                CL = (ICompositeLayer)GL_STATUS_EXCLUDE;
                for (int i = CL.Count - 1; i >= 0; i--)
                {
                    ILayer lyr = CL.get_Layer(i);

                    if (lyr is IGroupLayer)
                    {
                        GL_STATUS_EXCLUDE.Delete(lyr);
                    }
                }

                CL = (ICompositeLayer)GL_STATUS_PREFER;
                for (int i = CL.Count - 1; i >= 0; i--)
                {
                    ILayer lyr = CL.get_Layer(i);

                    if (lyr is IGroupLayer)
                    {
                        GL_STATUS_PREFER.Delete(lyr);
                    }
                }

                CL = (ICompositeLayer)GL_CF;
                for (int i = CL.Count - 1; i >= 0; i--)
                {
                    ILayer lyr = CL.get_Layer(i);

                    if (lyr is IGroupLayer)
                    {
                        GL_CF.Delete(lyr);
                    }
                }

                doc.UpdateContents();
                IActiveView av = (IActiveView)FocusMap;
                av.Refresh();

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        internal static void RemoveGridFL()
        {
            try
            {
                IFeatureWorkspace FWS = RetrieveWorkingWorkspace();
                if (FWS == null)
                    return;
                IWorkspace WS = (IWorkspace)FWS;
                string ws_path = GetWorkingFileGDBPath();

                // retrieve all feature layers
                IMap map = SH.FocusMap;
                UID theUID = new UIDClass();
                theUID.Value = "{40A9E885-5533-11d0-98BE-00805F7CED21}";    // featurelayers
                IEnumLayer enumLayers = map.get_Layers(theUID, true);
                ILayer lyr = enumLayers.Next();

                while (lyr != null)
                {
                    IFeatureLayer FL = (IFeatureLayer)lyr;
                    IDataset DS = (IDataset)FL.FeatureClass;

                    if (FL.Valid)
                    {
                        if (!(FL is IGdbRasterCatalogLayer))
                        {
                            if (DS.Workspace.PathName == ws_path && DS.Name == SC.c_FCNAME_PLANNING_UNIT_GRID)
                            {
                                map.DeleteLayer(lyr);
                            }
                        }
                    }
                    else if (!FL.Valid & FL.Name == SC.c_LAYER_PLANNING_UNIT_GRID)
                    {
                        map.DeleteLayer(lyr);
                    }

                    lyr = enumLayers.Next();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        internal static IDataset GetDatasetFromWorkspace(string DatasetName, esriDatasetType DatasetType, IWorkspace WS)
        {
            try
            {
                IEnumDatasetName enumDatasetName = WS.get_DatasetNames(DatasetType);
                IDatasetName DSName = enumDatasetName.Next();

                while (DSName != null)
                {
                    if (DSName.Name == DatasetName)
                    {
                        IName name = (IName)DSName;
                        return (IDataset)name.Open();
                    }

                    DSName = enumDatasetName.Next();
                }

                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        #region LEGENDS

        public static void ApplySimpleGridLegend(IFeatureLayer GridFL)
        {
            try
            {
                IColor FillColor = SH.ReturnESRIColorFromNETColor(Color.Black);
                IColor OutlineColor = SH.ReturnESRIColorFromNETColor(Color.Black);

                //line symbol
                ISimpleLineSymbol LineOutline = new SimpleLineSymbolClass();
                LineOutline.Color = OutlineColor;
                LineOutline.Width = 0.1;

                //fill symbol
                ISimpleFillSymbol FillSym = new SimpleFillSymbolClass();

                FillSym.Color = FillColor;
                FillSym.Style = esriSimpleFillStyle.esriSFSNull;
                FillSym.Outline = LineOutline;

                //apply legend
                IGeoFeatureLayer GFL = (IGeoFeatureLayer)GridFL;
                ISimpleRenderer rend = new SimpleRendererClass();
                rend.Symbol = (ISymbol)FillSym;
                rend.Label = "A lowly tile.";
                GFL.Renderer = (IFeatureRenderer)rend;

                IMxDocument doc = SH.IMxDoc;
                doc.UpdateContents();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        public static void ApplyStatusLegend(IFeatureLayer GridFL)
        {
            try
            {
                IMap FocusMap = SH.FocusMap;
                IGeoFeatureLayer GeoFL = (IGeoFeatureLayer)GridFL;

                //FIRST, BUILD A UNIQUEVALUES RENDERER
                //colors
                IColor ColorStatusAvailable = SH.ReturnESRIColorFromRGB(231, 245, 203);
                IColor ColorStatusPreferred = SH.ReturnESRIColorFromRGB(197, 232, 0);
                IColor ColorStatusIncluded = SH.ReturnESRIColorFromRGB(168, 168, 0);
                IColor ColorStatusExcluded = SH.ReturnESRIColorFromRGB(255, 115, 223);
                IColor ColorStatusOutline = SH.ReturnESRIColorFromRGB(78, 78, 78);
                IColor ColorStatusUnknown = SH.ReturnESRIColorFromRGB(10, 10, 10);

                //Outline Symbol
                ISimpleLineSymbol LineStatusOutline = new SimpleLineSymbolClass();
                LineStatusOutline.Color = ColorStatusOutline;
                LineStatusOutline.Width = 0.1;

                //Fill Symbols        		
                ISimpleFillSymbol FillStatus_Available = new SimpleFillSymbolClass();
                ISimpleFillSymbol FillStatus_Preferred = new SimpleFillSymbolClass();
                ISimpleFillSymbol FillStatus_Included = new SimpleFillSymbolClass();
                ISimpleFillSymbol FillStatus_Excluded = new SimpleFillSymbolClass();
                ISimpleFillSymbol FillStatus_Unknown = new SimpleFillSymbolClass();

                FillStatus_Available.Color = ColorStatusAvailable;
                FillStatus_Preferred.Color = ColorStatusPreferred;
                FillStatus_Included.Color = ColorStatusIncluded;
                FillStatus_Excluded.Color = ColorStatusExcluded;
                FillStatus_Unknown.Color = ColorStatusUnknown;

                FillStatus_Available.Outline = LineStatusOutline;
                FillStatus_Preferred.Outline = LineStatusOutline;
                FillStatus_Included.Outline = LineStatusOutline;
                FillStatus_Excluded.Outline = LineStatusOutline;
                FillStatus_Unknown.Outline = LineStatusOutline;

                //build the unique values renderer
                string UVField = SC.c_TABLENAME_STATUSINFO + "." + SC.c_FLD_STATUSINFO_QUICKSTATUS;
                IUniqueValueRenderer UVRend = new UniqueValueRendererClass();
                UVRend.FieldCount = 1;
                UVRend.set_Field(0, UVField);

                UVRend.AddValue("0", "ASSIGNED STATUS", (ISymbol)FillStatus_Available);
                UVRend.AddValue("1", "ASSIGNED STATUS", (ISymbol)FillStatus_Preferred);
                UVRend.AddValue("2", "ASSIGNED STATUS", (ISymbol)FillStatus_Included);
                UVRend.AddValue("3", "ASSIGNED STATUS", (ISymbol)FillStatus_Excluded);

                UVRend.set_Label("0", "Available");
                UVRend.set_Label("1", "Preferred");
                UVRend.set_Label("2", "Locked In");
                UVRend.set_Label("3", "Locked Out");

                UVRend.DefaultSymbol = (ISymbol)FillStatus_Unknown;
                UVRend.DefaultLabel = "<other>";
                UVRend.UseDefaultSymbol = false;

                //NEXT BUILD A CLASSBREAKS RENDERER TO SYMBOLIZE THE POLYGONS USING MARKER SYMBOLS
                //colors
                IColor ColorConflict_None = new RgbColorClass();
                ColorConflict_None.NullColor = true;
                IColor ColorConflict_Minor = SH.ReturnESRIColorFromNETColor(Color.Purple);
                IColor ColorConflict_Major = SH.ReturnESRIColorFromNETColor(Color.MidnightBlue);

                //markers
                IMarkerSymbol MarkerConflict_None = SH.ReturnMarkerSymbol("Circle 1", "ESRI.Style", ColorConflict_None, 2);
                IMarkerSymbol MarkerConflict_Minor = SH.ReturnMarkerSymbol("Circle 1", "ESRI.Style", ColorConflict_Minor, 5);
                IMarkerSymbol MarkerConflict_Major = SH.ReturnMarkerSymbol("Circle 1", "ESRI.Style", ColorConflict_Major, 9);

                //build the class breaks renderer
                string CBField = SC.c_TABLENAME_STATUSINFO + "." + SC.c_FLD_STATUSINFO_CONFLICT;

                IClassBreaksRenderer CBRend = new ClassBreaksRendererClass();
                CBRend.BreakCount = 3;
                CBRend.MinimumBreak = 0;

                CBRend.set_Break(0, 0);
                CBRend.set_Label(0, "None");
                CBRend.set_Symbol(0, (ISymbol)MarkerConflict_None);

                CBRend.set_Break(1, 1);
                CBRend.set_Label(1, "Minor (Preferred vs Locked Out)");
                CBRend.set_Symbol(1, (ISymbol)MarkerConflict_Minor);

                CBRend.set_Break(2, 2);
                CBRend.set_Label(2, "Major (Locked In vs Locked Out)");
                CBRend.set_Symbol(2, (ISymbol)MarkerConflict_Major);

                CBRend.Field = CBField;
                CBRend.BackgroundSymbol = FillStatus_Preferred;
                ILegendInfo CBLI = (ILegendInfo)CBRend;
                CBLI.SymbolsAreGraduated = true;

                //FINALLY ASSIGN BOTH RENDERERS TO BVREND THEN CALL BVREND.CREATELEGEND
                IBivariateRenderer BVRend = new BiUniqueValueRendererClass();
                BVRend.MainRenderer = (IFeatureRenderer)UVRend;
                BVRend.VariationRenderer = (IFeatureRenderer)CBRend;
                BVRend.CreateLegend();

                ILegendInfo VBLI = (ILegendInfo)BVRend;
                ILegendGroup LG = VBLI.get_LegendGroup(0);
                LG.Heading = "STATUS CONFLICTS";

                GeoFL.Renderer = (IFeatureRenderer)BVRend;
                IMxDocument doc = SH.IMxDoc;
                doc.UpdateContents();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        public static void ApplyCFCountLegend(IFeatureLayer GridFL)
        {
            try
            {
                //*** GENERATE A LIST OF UNIQUE FREQUENCY VALUES
                IDisplayTable DT = (IDisplayTable)GridFL;
                ITable FullTable = DT.DisplayTable;
                ICursor Cur = FullTable.Search(null, false);

                IDataStatistics DataStat = new DataStatisticsClass();
                DataStat.Field = SC.c_TABLENAME_PUVCF + "." + SC.c_FLD_PUVCF_CFCOUNT;
                DataStat.Cursor = Cur;

                IEnumerator EnumVal = DataStat.UniqueValues;
                EnumVal.Reset();

                List<int> LIST_UniqueValues = new List<int>();
                int num;

                while (EnumVal.MoveNext())
                {
                    if (Int32.TryParse(EnumVal.Current.ToString(), out num))
                        LIST_UniqueValues.Add(num);
                }

                LIST_UniqueValues.Sort();
                int MinVal = LIST_UniqueValues[0];
                int MaxVal = LIST_UniqueValues[LIST_UniqueValues.Count - 1];
                int ValCount = LIST_UniqueValues.Count;

                //*** BUILD THE COLOR RAMP
                //Colors
                IColor ColorFrom = SH.ReturnESRIColorFromRGB(230, 230, 230);
                IColor ColorTo = SH.ReturnESRIColorFromNETColor(Color.Green);
                IColor OutlineColor = SH.ReturnESRIColorFromNETColor(Color.Black);
                IColor ColorOther = SH.ReturnESRIColorFromNETColor(Color.LightPink);

                ISimpleLineSymbol LineOutline = new SimpleLineSymbolClass();
                LineOutline.Color = OutlineColor;
                LineOutline.Width = 0.1;

                //default symbol
                ISimpleFillSymbol FillOther = new SimpleFillSymbolClass();
                FillOther.Color = ColorOther;
                FillOther.Outline = LineOutline;

                IAlgorithmicColorRamp ramp = new AlgorithmicColorRampClass();
                ramp.FromColor = ColorFrom;
                ramp.ToColor = ColorTo;

                ramp.Algorithm = esriColorRampAlgorithm.esriCIELabAlgorithm;    //two other kinds to try out
                ramp.Size = MaxVal + 1;
                bool boolOK;
                ramp.CreateRamp(out boolOK);    //THIS CREATES THE RAMP COLORS

                IEnumColors Colors = ramp.Colors;   //HAS ALL THE COLORS I NEED

                //*** NOW CREATE THE RENDERER
                IGeoFeatureLayer GFL = (IGeoFeatureLayer)GridFL;
                string LegendField = SC.c_TABLENAME_PUVCF + "." + SC.c_FLD_PUVCF_CFCOUNT;
                IUniqueValueRenderer UVRend = new UniqueValueRendererClass();
                UVRend.FieldCount = 1;
                UVRend.set_Field(0, LegendField);
                UVRend.DefaultSymbol = (ISymbol)FillOther;
                UVRend.DefaultLabel = "<other value>";
                UVRend.UseDefaultSymbol = true;

                for (int i = 0; i <= MaxVal; i++)
                {
                    ISimpleFillSymbol fill = new SimpleFillSymbolClass();
                    fill.Color = Colors.Next();
                    fill.Outline = LineOutline;

                    UVRend.AddValue(i.ToString(), "CONSERVATION FEATURE COUNT", (ISymbol)fill);
                    UVRend.set_Label(i.ToString(), i.ToString());
                }

                GFL.Renderer = (IFeatureRenderer)UVRend;

                IMxDocument doc = SH.IMxDoc;
                doc.UpdateContents();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        public static void ApplyExternalTileLegend(IFeatureLayer GridFL)
        {
            try
            {
                //colors
                IColor ColorYes = SH.ReturnESRIColorFromNETColor(Color.Orange);
                IColor ColorNull = SH.ReturnESRIColorFromRGB(220, 220, 220);
                IColor OutlineColor = SH.ReturnESRIColorFromNETColor(Color.Black);

                //line symbol
                ISimpleLineSymbol LineOutline = new SimpleLineSymbolClass();
                LineOutline.Color = OutlineColor;
                LineOutline.Width = 0.1;

                //fill symbols
                ISimpleFillSymbol FillYes = new SimpleFillSymbolClass();
                ISimpleFillSymbol FillNull = new SimpleFillSymbolClass();

                FillYes.Color = ColorYes;
                FillNull.Color = ColorNull;

                FillYes.Outline = LineOutline;
                FillNull.Outline = LineOutline;

                //apply legend
                IGeoFeatureLayer GFL = (IGeoFeatureLayer)GridFL;

                string LegendField = SC.c_TABLENAME_EXTERIORTILES + "." + SC.c_FLD_EXTILE_EXTERIOR;
                IUniqueValueRenderer UVRend = new UniqueValueRendererClass();
                UVRend.FieldCount = 1;
                UVRend.set_Field(0, LegendField);

                UVRend.AddValue("YES", "ADJACENCY", (ISymbol)FillYes);
                UVRend.AddValue("<Null>", "ADJACENCY", (ISymbol)FillNull);
                UVRend.set_Label("YES", "Exterior Tile");
                UVRend.set_Label("<Null>", "Interior Tile");
                UVRend.DefaultSymbol = null;
                UVRend.DefaultLabel = "NULL Record";
                UVRend.UseDefaultSymbol = false;

                GFL.Renderer = (IFeatureRenderer)UVRend;

                IMxDocument doc = SH.IMxDoc;
                doc.UpdateContents();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        public static void ApplyExternalTileOpenSidesLegend(IFeatureLayer GridFL)
        {
            try
            {
                //colors
                IColor ColorYes = SH.ReturnESRIColorFromNETColor(Color.Orange);
                IColor ColorNull = SH.ReturnESRIColorFromRGB(220, 220, 220);
                IColor OutlineColor = SH.ReturnESRIColorFromNETColor(Color.Black);

                //line symbol
                ISimpleLineSymbol LineOutline = new SimpleLineSymbolClass();
                LineOutline.Color = OutlineColor;
                LineOutline.Width = 0.1;

                //fill symbols
                ISimpleFillSymbol FillYes = new SimpleFillSymbolClass();
                ISimpleFillSymbol FillNull = new SimpleFillSymbolClass();

                FillYes.Color = ColorYes;
                FillNull.Color = ColorNull;

                FillYes.Outline = LineOutline;
                FillNull.Outline = LineOutline;

                //apply legend
                IGeoFeatureLayer GFL = (IGeoFeatureLayer)GridFL;

                string LegendField = SC.c_TABLENAME_EXTERIORTILES + "." + SC.c_FLD_EXTILE_EXTERIOR;
                IUniqueValueRenderer UVRend = new UniqueValueRendererClass();
                UVRend.FieldCount = 1;
                UVRend.set_Field(0, LegendField);

                UVRend.AddValue("YES", "ADJACENCY", (ISymbol)FillYes);
                UVRend.AddValue("<Null>", "ADJACENCY", (ISymbol)FillNull);
                UVRend.set_Label("YES", "Exterior Tile");
                UVRend.set_Label("<Null>", "Interior Tile");
                UVRend.DefaultSymbol = null;
                UVRend.DefaultLabel = "NULL Record";
                UVRend.UseDefaultSymbol = false;

                GFL.Renderer = (IFeatureRenderer)UVRend;

                IMxDocument doc = SH.IMxDoc;
                doc.UpdateContents();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        public static void ApplySolutionLegend(IFeatureLayer GridFL, string JoinTable)
        {
            try
            {
                //colors
                IColor ColorIN = SH.ReturnESRIColorFromNETColor(Color.YellowGreen);
                IColor ColorOUT = SH.ReturnESRIColorFromRGB(230, 230, 230);
                IColor ColorOther = SH.ReturnESRIColorFromNETColor(Color.LightPink);
                IColor OutlineColor = SH.ReturnESRIColorFromNETColor(Color.Black);

                //line symbol
                ISimpleLineSymbol LineOutline = new SimpleLineSymbolClass();
                LineOutline.Color = OutlineColor;
                LineOutline.Width = 0.1;

                //fill symbols
                ISimpleFillSymbol FillIN = new SimpleFillSymbolClass();
                ISimpleFillSymbol FillOUT = new SimpleFillSymbolClass();
                ISimpleFillSymbol FillOther = new SimpleFillSymbolClass();

                FillIN.Color = ColorIN;
                FillOUT.Color = ColorOUT;
                FillOther.Color = ColorOther;

                FillIN.Outline = LineOutline;
                FillOUT.Outline = LineOutline;
                FillOther.Outline = LineOutline;

                //apply legend
                IGeoFeatureLayer GFL = (IGeoFeatureLayer)GridFL;

                string LegendField = JoinTable + "." + SC.c_FLD_SOLN_SELECTED;
                IUniqueValueRenderer UVRend = new UniqueValueRendererClass();
                UVRend.FieldCount = 1;
                UVRend.set_Field(0, LegendField);

                string numberstring = JoinTable.Substring(13);
                int num = 0;
                if (!Int32.TryParse(numberstring, out num))
                    numberstring = "?";
                else
                    numberstring = num.ToString();

                UVRend.AddValue("YES", "SOLUTION " + numberstring, (ISymbol)FillIN);
                UVRend.AddValue("NO", "SOLUTION " + numberstring, (ISymbol)FillOUT);
                UVRend.set_Label("YES", "Tile, you have been chosen");
                UVRend.set_Label("NO", "Tile, you have failed me for the last time");
                UVRend.DefaultSymbol = (ISymbol)FillOther;
                UVRend.DefaultLabel = "<other value>";
                UVRend.UseDefaultSymbol = true;

                GFL.Renderer = (IFeatureRenderer)UVRend;

                IMxDocument doc = SH.IMxDoc;
                doc.UpdateContents();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        public static void ApplyBestSolutionLegend(IFeatureLayer GridFL)
        {
            try
            {
                //colors
                IColor ColorIN = SH.ReturnESRIColorFromNETColor(Color.YellowGreen);
                IColor ColorOUT = SH.ReturnESRIColorFromRGB(230, 230, 230);
                IColor ColorOther = SH.ReturnESRIColorFromNETColor(Color.LightPink);
                IColor OutlineColor = SH.ReturnESRIColorFromNETColor(Color.Black);

                //line symbol
                ISimpleLineSymbol LineOutline = new SimpleLineSymbolClass();
                LineOutline.Color = OutlineColor;
                LineOutline.Width = 0.1;

                //fill symbols
                ISimpleFillSymbol FillIN = new SimpleFillSymbolClass();
                ISimpleFillSymbol FillOUT = new SimpleFillSymbolClass();
                ISimpleFillSymbol FillOther = new SimpleFillSymbolClass();

                FillIN.Color = ColorIN;
                FillOUT.Color = ColorOUT;
                FillOther.Color = ColorOther;

                FillIN.Outline = LineOutline;
                FillOUT.Outline = LineOutline;
                FillOther.Outline = LineOutline;

                //apply legend
                IGeoFeatureLayer GFL = (IGeoFeatureLayer)GridFL;

                string LegendField = SC.c_TABLENAME_BESTSOLUTION + "." + SC.c_FLD_BESTSOLN_SELECTED;
                IUniqueValueRenderer UVRend = new UniqueValueRendererClass();
                UVRend.FieldCount = 1;
                UVRend.set_Field(0, LegendField);

                UVRend.AddValue("YES", "BEST SOLUTION", (ISymbol)FillIN);
                UVRend.AddValue("NO", "BEST SOLUTION", (ISymbol)FillOUT);
                UVRend.set_Label("YES", "Tile, you have been chosen");
                UVRend.set_Label("NO", "Tile, you have failed me for the last time");
                UVRend.DefaultSymbol = (ISymbol)FillOther;
                UVRend.DefaultLabel = "<other value>";
                UVRend.UseDefaultSymbol = true;

                GFL.Renderer = (IFeatureRenderer)UVRend;

                IMxDocument doc = SH.IMxDoc;
                doc.UpdateContents();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        public static void ApplySummedSolutionLegend(IFeatureLayer GridFL)
        {
            try
            {
                //*** GENERATE A LIST OF UNIQUE FREQUENCY VALUES
                IDisplayTable DT = (IDisplayTable)GridFL;
                ITable FullTable = DT.DisplayTable;
                ICursor Cur = FullTable.Search(null, false);

                IDataStatistics DataStat = new DataStatisticsClass();
                DataStat.Field = SC.c_TABLENAME_SUMSOLUTION + "." + SC.c_FLD_SUMSOLN_FREQUENCY;
                DataStat.Cursor = Cur;

                IEnumerator EnumVal = DataStat.UniqueValues;
                EnumVal.Reset();

                List<int> LIST_UniqueValues = new List<int>();
                int num;

                while (EnumVal.MoveNext())
                {
                    if (Int32.TryParse(EnumVal.Current.ToString(), out num))
                        LIST_UniqueValues.Add(num);
                }

                LIST_UniqueValues.Sort();
                int MinVal = LIST_UniqueValues[0];
                int MaxVal = LIST_UniqueValues[LIST_UniqueValues.Count - 1];
                int ValCount = LIST_UniqueValues.Count;

                //*** BUILD THE COLOR RAMP
                //Colors
                IColor ColorFrom = SH.ReturnESRIColorFromRGB(230, 230, 230);
                IColor ColorTo = SH.ReturnESRIColorFromNETColor(Color.Red);
                IColor OutlineColor = SH.ReturnESRIColorFromNETColor(Color.Black);
                IColor ColorOther = SH.ReturnESRIColorFromNETColor(Color.LightPink);

                ISimpleLineSymbol LineOutline = new SimpleLineSymbolClass();
                LineOutline.Color = OutlineColor;
                LineOutline.Width = 0.1;

                //default symbol
                ISimpleFillSymbol FillOther = new SimpleFillSymbolClass();
                FillOther.Color = ColorOther;
                FillOther.Outline = LineOutline;

                IAlgorithmicColorRamp ramp = new AlgorithmicColorRampClass();
                ramp.FromColor = ColorFrom;
                ramp.ToColor = ColorTo;

                ramp.Algorithm = esriColorRampAlgorithm.esriCIELabAlgorithm;    //two other kinds to try out
                ramp.Size = MaxVal + 1;
                bool boolOK;
                ramp.CreateRamp(out boolOK);    //THIS CREATES THE RAMP COLORS

                IEnumColors Colors = ramp.Colors;   //HAS ALL THE COLORS I NEED

                //*** NOW CREATE THE RENDERER
                IGeoFeatureLayer GFL = (IGeoFeatureLayer)GridFL;
                string LegendField = SC.c_TABLENAME_SUMSOLUTION + "." + SC.c_FLD_SUMSOLN_FREQUENCY;
                IUniqueValueRenderer UVRend = new UniqueValueRendererClass();
                UVRend.FieldCount = 1;
                UVRend.set_Field(0, LegendField);
                UVRend.DefaultSymbol = (ISymbol)FillOther;
                UVRend.DefaultLabel = "<other value>";
                UVRend.UseDefaultSymbol = true;

                for (int i = 0; i <= MaxVal; i++)
                {
                    ISimpleFillSymbol fill = new SimpleFillSymbolClass();
                    fill.Color = Colors.Next();
                    fill.Outline = LineOutline;

                    UVRend.AddValue(i.ToString(), "TILE SELECTION FREQUENCY", (ISymbol)fill);
                    UVRend.set_Label(i.ToString(), i.ToString());
                }

                GFL.Renderer = (IFeatureRenderer)UVRend;

                IMxDocument doc = SH.IMxDoc;
                doc.UpdateContents();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        #endregion

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
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return "<error calculating duration>";
            }
        }
#endif
    }

}
