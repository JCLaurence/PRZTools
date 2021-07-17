using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
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
using PRZH = NCC.PRZTools.PRZHelper;
using PRZC = NCC.PRZTools.PRZConstants;
using PRZM = NCC.PRZTools.PRZMethods;

namespace NCC.PRZTools
{
    public class PlanningUnitsVM : PropertyChangedBase 
    {
        public PlanningUnitsVM()
        {
        }

        #region Fields

        private SpatialReference _outputSR;
        private SpatialReference _mapSR;
        private SpatialReference _userSR;

        #endregion


        #region Properties

        private bool _srMapIsChecked;
        public bool SRMapIsChecked
        {
            get { return _srMapIsChecked; }
            set
            {
                SetProperty(ref _srMapIsChecked, value, () => SRMapIsChecked);
            }
        }

        private bool _srLayerIsChecked;
        public bool SRLayerIsChecked
        {
            get { return _srLayerIsChecked; }
            set
            {
                SetProperty(ref _srLayerIsChecked, value, () => SRLayerIsChecked);
            }
        }

        private bool _srUserIsChecked;
        public bool SRUserIsChecked
        {
            get { return _srUserIsChecked; }
            set
            {
                SetProperty(ref _srUserIsChecked, value, () => SRUserIsChecked);
            }
        }

        private string _mapSRName;
        public string MapSRName
        {
            get { return _mapSRName; }
            set
            {
                SetProperty(ref _mapSRName, value, () => MapSRName);
            }
        }

        private string _userSRName;
        public string UserSRName
        {
            get { return _userSRName; }
            set
            {
                SetProperty(ref _userSRName, value, () => UserSRName);
            }
        }

        private List<SpatialReference> _layerSRList;
        public List<SpatialReference> LayerSRList
        {
            get { return _layerSRList; }
            set
            {
                SetProperty(ref _layerSRList, value, () => LayerSRList);
            }
        }

        private bool _srMapIsEnabled;
        public bool SRMapIsEnabled
        {
            get { return _srMapIsEnabled; }
            set
            {
                SetProperty(ref _srMapIsEnabled, value, () => SRMapIsEnabled);
            }
        }

        private bool _srLayerIsEnabled;
        public bool SRLayerIsEnabled
        {
            get { return _srLayerIsEnabled; }
            set
            {
                SetProperty(ref _srLayerIsEnabled, value, () => SRLayerIsEnabled);
            }
        }

        private bool _srUserIsEnabled;
        public bool SRUserIsEnabled
        {
            get { return _srUserIsEnabled; }
            set
            {
                SetProperty(ref _srUserIsEnabled, value, () => SRUserIsEnabled);
            }
        }

        private SpatialReference _selectedLayerSR;
        public SpatialReference SelectedLayerSR
        {
            get { return _selectedLayerSR; }
            set
            {
                SetProperty(ref _selectedLayerSR, value, () => SelectedLayerSR);
            }
        }


        private List<GraphicsLayer> _graphicsLayerList;
        public List<GraphicsLayer> GraphicsLayerList
        {
            get { return _graphicsLayerList; }
            set
            {
                SetProperty(ref _graphicsLayerList, value, () => GraphicsLayerList);
            }
        }

        private GraphicsLayer _selectedGraphicsLayer;
        public GraphicsLayer SelectedGraphicsLayer
        {
            get { return _selectedGraphicsLayer; }
            set
            {
                SetProperty(ref _selectedGraphicsLayer, value, () => SelectedGraphicsLayer);
            }
        }


        private List<FeatureLayer> _featureLayerList;
        public List<FeatureLayer> FeatureLayerList
        {
            get { return _featureLayerList; }
            set
            {
                SetProperty(ref _featureLayerList, value, () => FeatureLayerList);
            }
        }

        private FeatureLayer _selectedFeatureLayer;
        public FeatureLayer SelectedFeatureLayer
        {
            get { return _selectedFeatureLayer; }
            set
            {
                SetProperty(ref _selectedFeatureLayer, value, () => SelectedFeatureLayer);
            }
        }


        private string _bufferValue;
        public string BufferValue
        {
            get { return _bufferValue; }
            set
            {
                SetProperty(ref _bufferValue, value, () => BufferValue);
            }
        }


        private string _gridAlign_X;
        public string GridAlign_X
        {
            get { return _gridAlign_X; }
            set
            {
                SetProperty(ref _gridAlign_X, value, () => GridAlign_X);
            }
        }

        private string _gridAlign_Y;
        public string GridAlign_Y
        {
            get { return _gridAlign_Y; }
            set
            {
                SetProperty(ref _gridAlign_Y, value, () => GridAlign_Y);
            }
        }


        private bool _bufferUnitMetersIsChecked;
        public bool BufferUnitMetersIsChecked
        {
            get { return _bufferUnitMetersIsChecked; }
            set
            {
                SetProperty(ref _bufferUnitMetersIsChecked, value, () => BufferUnitMetersIsChecked);
            }
        }

        private bool _bufferUnitKilometersIsChecked;
        public bool BufferUnitKilometersIsChecked
        {
            get { return _bufferUnitKilometersIsChecked; }
            set
            {
                SetProperty(ref _bufferUnitKilometersIsChecked, value, () => BufferUnitKilometersIsChecked);
            }
        }


        private List<string> _gridTypeList;
        public List<string> GridTypeList
        {
            get { return _gridTypeList; }
            set
            {
                SetProperty(ref _gridTypeList, value, () => GridTypeList);
            }
        }

        private string _selectedGridType;
        public string SelectedGridType
        {
            get { return _selectedGridType; }
            set
            {
                SetProperty(ref _selectedGridType, value, () => SelectedGridType);
            }
        }

        private string _tileArea;
        public string TileArea
        {
            get { return _tileArea; }
            set
            {
                SetProperty(ref _tileArea, value, () => TileArea);
            }
        }

        private bool _tileAreaMIsSelected;
        public bool TileAreaMIsSelected
        {
            get { return _tileAreaMIsSelected; }
            set
            {
                SetProperty(ref _tileAreaMIsSelected, value, () => TileAreaMIsSelected);
            }
        }

        private bool _tileAreaAcIsSelected;
        public bool TileAreaAcIsSelected
        {
            get { return _tileAreaAcIsSelected; }
            set
            {
                SetProperty(ref _tileAreaAcIsSelected, value, () => TileAreaAcIsSelected);
            }
        }

        private bool _tileAreaHaIsSelected;
        public bool TileAreaHaIsSelected
        {
            get { return _tileAreaHaIsSelected; }
            set
            {
                SetProperty(ref _tileAreaHaIsSelected, value, () => TileAreaHaIsSelected);
            }
        }

        private bool _tileAreaKmIsSelected;
        public bool TileAreaKmIsSelected
        {
            get { return _tileAreaKmIsSelected; }
            set
            {
                SetProperty(ref _tileAreaKmIsSelected, value, () => TileAreaKmIsSelected);
            }
        }

        private bool _buildIsEnabled;
        public bool BuildIsEnabled
        {
            get { return _buildIsEnabled; }
            set
            {
                SetProperty(ref _buildIsEnabled, value, () => BuildIsEnabled);
            }
        }


        private bool _graphicsLayerIsEnabled;
        public bool GraphicsLayerIsEnabled
        {
            get { return _graphicsLayerIsEnabled; }
            set
            {
                SetProperty(ref _graphicsLayerIsEnabled, value, () => GraphicsLayerIsEnabled);
            }
        }

        private bool _graphicsLayerIsChecked;
        public bool GraphicsLayerIsChecked
        {
            get { return _graphicsLayerIsChecked; }
            set
            {
                SetProperty(ref _graphicsLayerIsChecked, value, () => GraphicsLayerIsChecked);
            }
        }

        private bool _featureLayerIsEnabled;
        public bool FeatureLayerIsEnabled
        {
            get { return _featureLayerIsEnabled; }
            set
            {
                SetProperty(ref _featureLayerIsEnabled, value, () => FeatureLayerIsEnabled);
            }
        }

        private bool _featureLayerIsChecked;
        public bool FeatureLayerIsChecked
        {
            get { return _featureLayerIsChecked; }
            set
            {
                SetProperty(ref _featureLayerIsChecked, value, () => FeatureLayerIsChecked);
            }
        }

        private string _testInfo;
        public string TestInfo
        {
            get { return _testInfo; }
            set
            {
                SetProperty(ref _testInfo, value, () => TestInfo);
            }
        }

        private int _barMax;
        public int BarMax
        {
            get { return _barMax; }
            set
            {
                SetProperty(ref _barMax, value, () => BarMax);
            }
        }

        private int _barMin;
        public int BarMin
        {
            get { return _barMin; }
            set
            {
                SetProperty(ref _barMin, value, () => BarMin);
            }
        }

        private int _barValue;
        public int BarValue
        {
            get { return _barValue; }
            set
            {
                SetProperty(ref _barValue, value, () => BarValue);
            }
        }

        private string _barMessage;
        public string BarMessage
        {
            get { return _barMessage; }
            set
            {
                SetProperty(ref _barMessage, value, () => BarMessage);
            }
        }


        #endregion

        #region Commands

        private ICommand _cmdSelectSpatialReference;
        public ICommand CmdSelectSpatialReference { get { return _cmdSelectSpatialReference ?? (_cmdSelectSpatialReference = new RelayCommand(() => SelectSpatialReference(), () => true)); } }

        private ICommand _cmdSRMap;
        public ICommand CmdSRMap { get { return _cmdSRMap ?? (_cmdSRMap = new RelayCommand(() => SelectMapSR(), () => true)); } }

        private ICommand _cmdSRUser;
        public ICommand CmdSRUser { get { return _cmdSRUser ?? (_cmdSRUser = new RelayCommand(() => SelectUserSR(), () => true)); } }

        private ICommand _cmdSRLayer;
        public ICommand CmdSRLayer { get { return _cmdSRLayer ?? (_cmdSRLayer = new RelayCommand(() => SelectLayerSR(), () => true)); } }

        private ICommand _cmdBuildPlanningUnits;
        public ICommand CmdBuildPlanningUnits { get { return _cmdBuildPlanningUnits ?? (_cmdBuildPlanningUnits = new RelayCommand(async () => await BuildPlanningUnits(), () => true)); } }


        public ICommand cmdOK => new RelayCommand((paramProWin) =>
        {
            // TODO: set dialog result and close the window
            (paramProWin as ProWindow).DialogResult = true;
            (paramProWin as ProWindow).Close();
        }, () => true);

        public ICommand cmdCancel => new RelayCommand((paramProWin) =>
        {
            // TODO: set dialog result and close the window
            (paramProWin as ProWindow).DialogResult = false;
            (paramProWin as ProWindow).Close();
        }, () => true);

        #endregion

        #region Methods

        /// <summary>
        /// This method runs when the ProWindow Loaded event occurs.
        /// </summary>
        internal async void OnProWinLoaded()
        {
            try
            {
                #region Spatial Reference Information

                MapView mapView = MapView.Active;
                Map map = mapView.Map;
                _mapSR = map.SpatialReference;
                this.MapSRName = _mapSR.Name;

                // layers

                List<SpatialReference> SRList = new List<SpatialReference>();

                var lyrs = map.GetLayersAsFlattenedList().ToList();

                foreach (var lyr in lyrs)
                {
                    await QueuedTask.Run(() =>
                    {
                        var sr = lyr.GetSpatialReference();
                        if (sr != null)
                        {
                            if (!SRList.Contains(sr))
                            {
                                if ((!sr.IsUnknown) && (!sr.IsGeographic))
                                SRList.Add(sr);
                            }
                        }
                    });
                }

                this.LayerSRList = SRList;

                // SR Radio Options enabled/disabled
                this.SRLayerIsEnabled = (SRList.Count > 0);
                this.SRMapIsEnabled = (!_mapSR.IsUnknown) && (!_mapSR.IsGeographic);
                this.SRUserIsEnabled = false;

                // Graphics Layers having selected Polygon Graphics
                var glyrs = map.GetLayersAsFlattenedList().OfType<GraphicsLayer>().ToList();
                var polygraphicList = new List<CIMPolygonGraphic>();

                Dictionary<GraphicsLayer, int> DICT_GraphicLayer_SelPolyCount = new Dictionary<GraphicsLayer, int>();

                foreach (var glyr in glyrs)
                {
                    var selElems = glyr.GetSelectedElements().OfType<GraphicElement>();
                    int c = 0;

                    foreach (var elem in selElems)
                    {
                        await QueuedTask.Run(() =>
                        {
                            var g = elem.GetGraphic();
                            if (g is CIMPolygonGraphic)
                            {
                                c++;
                            }
                        });                        
                    }

                    if (c > 0)
                    {
                        DICT_GraphicLayer_SelPolyCount.Add(glyr, c);
                    }
                }

                this.GraphicsLayerList = DICT_GraphicLayer_SelPolyCount.Keys.ToList();
                this.GraphicsLayerIsEnabled = (DICT_GraphicLayer_SelPolyCount.Count > 0);

                // Polygon Feature Layers having selection
                var flyrs = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where((fl) => fl.SelectionCount > 0 && fl.ShapeType == esriGeometryType.esriGeometryPolygon).ToList();

                this.FeatureLayerList = flyrs;
                this.FeatureLayerIsEnabled = flyrs.Count > 0;

                // Buffer
                this.BufferValue = "0";
                this.BufferUnitMetersIsChecked = true;

                // Grid Type combo box
                this.GridTypeList = Enum.GetNames(typeof(PlanningUnitTileShape)).ToList();

                this.SelectedGridType = PlanningUnitTileShape.SQUARE.ToString();

                // Tile area units
                this.TileAreaKmIsSelected = true;

                StringBuilder sb = new StringBuilder();

                sb.AppendLine("Map Name: " + map.Name);
                sb.AppendLine("Spatial Reference: " + _mapSR.Name);

                this.TestInfo = sb.ToString();

                #endregion
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        internal async Task<bool> BuildPlanningUnits()
        {
            CancelableProgressorSource cps = null;  // use this for QueuedTask.Run tasks that take a while.  Otherwise, just use the progressbar on the window

            try
            {
                #region INITIALIZATION

                // Reset the Progress Bar
                UpdateProgress("Initializing...", false, 0, 10, 0);

                // Notify users what will happen if they proceed
                if (ProMsgBox.Show("The Planning Units layer is the heart of the various tabular data used by PRZ Tools.  Building a new Planning Units layer invalidates all existing tables since the planning unit IDs and count may be different." +
                    Environment.NewLine + Environment.NewLine +
                    "If you proceed, all associated tables and feature classes in the PRZ File Geodatabase WILL BE DELETED!!" +
                    Environment.NewLine + Environment.NewLine +
                    "Do you wish to proceed?" + 
                    Environment.NewLine + Environment.NewLine +
                    "Choose wisely...",
                    "Table/Feature Class Overwrite Warning", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                    System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    UpdateProgress("", false, 0, 1, 0);
                    return false;
                }

                // Proceed if all user input is validated
                if (!CanBuildPlanningUnits())
                {
                    ProMsgBox.Show("Not all user input properly specified.  This message needs to be improved.");
                    UpdateProgress("User Input Error", true);
                    return false;
                }

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Verify that PRZ workspace geodatabase is available
                bool gdb_exists = await PRZH.ProjectGDBExists();
                if (!gdb_exists)
                {
                    ProMsgBox.Show("PRZ Workspace Geodatabase not found.  Please verify that your workspace is set up correctly.");
                    UpdateProgress("PRZ Workspace Geodatabase not found.", true);
                    return false;
                }
                else
                {
                    UpdateProgress("Project Workspace geodatabase exists...", true);
                }

                // Prepare Output Spatial Reference
                var OutputSR = GetOutputSR();
                if (OutputSR == null)
                {
                    ProMsgBox.Show("Please specify a valid Output Spatial Reference.");
                    UpdateProgress("Output Spatial Reference not specified.", true);
                    return false;
                }
                else
                {
                    UpdateProgress("Output Spatial Reference: " + OutputSR.Name, true);
                }

                // Get Buffer Distance in meters
                double buffer_distance = 0;
                string buffer_distance_text = BufferValue.Trim();

                if (buffer_distance_text == "")
                {
                    buffer_distance_text = "0";
                }

                if (!double.TryParse(buffer_distance_text, out buffer_distance))
                {
                    ProMsgBox.Show("Invalid Buffer Distance specified.  Must be a number >= 0, or left blank.");
                    UpdateProgress("Invalid Buffer Distance specified.", true);
                    return false;
                }
                else if (buffer_distance < 0)
                {
                    ProMsgBox.Show("Invalid Buffer Distance specified.  Must be a number >= 0, or left blank.");
                    UpdateProgress("Invalid Buffer Distance specified.", true);
                    return false;
                }
                else
                {
                    string s = "Buffer Distance: " + buffer_distance_text;

                    if (BufferUnitMetersIsChecked)
                    {
                        s += " m";
                    }
                    else if (BufferUnitKilometersIsChecked)
                    {
                        s += " km";
                        buffer_distance = buffer_distance * 1000.0;
                    }

                    UpdateProgress(s, true);
                }

                // Validate tile type
                if (string.IsNullOrEmpty(SelectedGridType))
                {
                    ProMsgBox.Show("No Tile Type Specified");
                    UpdateProgress("No Tile Type specified...", true);
                    return false;
                }

                string gridtype = SelectedGridType;
                var GridType = PlanningUnitTileShape.SQUARE;
                switch (gridtype)
                {
                    case "SQUARE":
                        GridType = PlanningUnitTileShape.SQUARE;
                        break;
                    default:
                        return false;
                }

                // Get Tile Area in square meters
                double tile_area = 0;
                string tile_area_text = TileArea.Trim();

                if (tile_area_text == "")
                {
                    tile_area_text = "0";
                }

                if (!double.TryParse(tile_area_text, out tile_area))
                {
                    ProMsgBox.Show("Invalid Tile Area specified.  Must be a number > 0.");
                    UpdateProgress("Invalid Tile Area specified.", true);
                    return false;
                }
                else if (tile_area <= 0)
                {
                    ProMsgBox.Show("Invalid Tile Area specified.  Must be a number > 0.");
                    UpdateProgress("Invalid Tile Area specified.", true);
                    return false;
                }
                else
                {
                    string s = "Tile Area: " + tile_area_text;

                    if (TileAreaMIsSelected)
                    {
                        s += " m\xB2";
                    }
                    else if (TileAreaAcIsSelected)
                    {
                        s += " ac";
                        tile_area = tile_area * 4046.86;
                    }
                    else if (TileAreaHaIsSelected)
                    {
                        s += " ha";
                        tile_area = tile_area * 10000.0;
                    }
                    else if (TileAreaKmIsSelected)
                    {
                        s += " km\xB2";
                        tile_area = tile_area * 1000000.0;
                    }

                    UpdateProgress(s, true);
                }

                // Get Tile Positioning XY
                string xtrim = string.IsNullOrEmpty(GridAlign_X) ? "" : GridAlign_X.Trim();
                string ytrim = string.IsNullOrEmpty(GridAlign_Y) ? "" : GridAlign_Y.Trim();
                bool UseGridAlign = false;
                bool GridAlignIssues = false;
                double gridalignx;
                double gridaligny;

                if (xtrim.Length > 0 && ytrim.Length > 0)
                {
                    if (!double.TryParse(xtrim, out gridalignx))
                    {
                        GridAlignIssues = true;                        
                    }
                    else if (!double.TryParse(ytrim, out gridaligny))
                    {
                        GridAlignIssues = true;
                    }
                    else
                    {
                        UseGridAlign = true;
                    }
                }
                else if (xtrim.Length > 0 && ytrim.Length == 0)
                {
                    GridAlignIssues = true;
                }
                else if (xtrim.Length == 0 && ytrim.Length > 0)
                {
                    GridAlignIssues = true;
                }

                if (GridAlignIssues)
                {
                    ProMsgBox.Show("Invalid Tile Grid Alignment Coordinates.  Both X and Y values must be numeric, or both must be blank.");
                    UpdateProgress("Invalid Tile Grid Alignment Coordinates.", true);
                    return false;
                }
                else if (UseGridAlign)
                {
                    UpdateProgress("Using Grid Alignment Coordinates.", true);
                }
                else
                {
                    UpdateProgress("No Grid Alignment Coordinates.", true);
                }

                #endregion

                #region STUDY AREA

                // Retrieve Polygons to construct Study Area + Buffered Study Area
                List<Polygon> study_area_polys = new List<Polygon>();  // capture all individual selected polygons
                Polygon study_area_poly = null;

                if (GraphicsLayerIsChecked)
                {
                    // Get the selected polygon graphics from the graphics layer
                    GraphicsLayer gl = SelectedGraphicsLayer;

                    var selElems = gl.GetSelectedElements().OfType<GraphicElement>();

                    await QueuedTask.Run(() =>
                    {
                        PolygonBuilder polyBuilder = new PolygonBuilder(gl.GetSpatialReference());

                        foreach (var elem in selElems)
                        {

                            var g = elem.GetGraphic();
                            if (g is CIMPolygonGraphic)
                            {
                                var p = g as CIMPolygonGraphic;
                                var s = p.Polygon.Clone() as Polygon;

                                polyBuilder.AddParts(s.Parts);
                                Polygon p3 = (Polygon)GeometryEngine.Instance.Project(s, OutputSR);
                                study_area_polys.Add(p3);
                            }
                        }

                        Polygon poly = polyBuilder.ToGeometry();
                        study_area_poly = (Polygon)GeometryEngine.Instance.Project(poly, OutputSR);
                    });
                }
                else if (FeatureLayerIsChecked)
                {
                    // Get the selected polygon features from the selected feature layer
                    FeatureLayer fl = SelectedFeatureLayer;

                    await QueuedTask.Run(() =>
                    {
                        PolygonBuilder polyBuilder = new PolygonBuilder(fl.GetSpatialReference());

                        using (Selection sel = fl.GetSelection())
                        {
                            using (RowCursor cur = sel.Search(null, false))
                            {
                                while (cur.MoveNext())
                                {
                                    using (Feature feat = (Feature)cur.Current)
                                    {
                                        // process feature
                                        var s = feat.GetShape().Clone() as Polygon;
                                        polyBuilder.AddParts(s.Parts);
                                        Polygon p3 = (Polygon)GeometryEngine.Instance.Project(s, OutputSR);
                                        study_area_polys.Add(p3);
                                    }
                                }
                            }
                        }

                        Polygon poly = polyBuilder.ToGeometry();
                        study_area_poly = (Polygon)GeometryEngine.Instance.Project(poly, OutputSR);                                               
                    });
                }

                // Generate Buffered Polygons (buffer might be 0)
                Polygon study_area_poly_buffered = GeometryEngine.Instance.Buffer(study_area_poly, buffer_distance) as Polygon;
                Polygon study_area_polys_buffered = await QueuedTask.Run(() =>
                {
                    return GeometryEngine.Instance.Buffer(study_area_polys, buffer_distance) as Polygon;
                });


                // I now have study area polygons
                // I want to build the following feature classes:
                // 1. Study Area Polys
                // 2. Study Area Single (merged) Poly (multipart feature if required)
                // 3. Buffered Study Area Polys
                // 4. Buffered Multipart Study Area Poly

                // Remove any Planning Unit layers
                var map = MapView.Active.Map;
                var flyrs = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                List<Layer> LayersToDelete = new List<Layer>();
                string gdbpath = PRZH.GetProjectGDBPath();
                string pufcpath = PRZH.GetPlanningUnitFCPath();

                foreach (FeatureLayer flyr in flyrs)
                {
                    await QueuedTask.Run(() =>
                    {
                        FeatureClass fc = flyr.GetFeatureClass();
                        string fcpath = fc.GetPath().AbsolutePath;

                        if (fcpath.Replace(@"/", @"\") == pufcpath)
                        {
                            LayersToDelete.Add(flyr);
                        }
                    });
                }

                await QueuedTask.Run(() =>
                {
                    map.RemoveLayers(LayersToDelete);
                    MapView.Active.RedrawAsync(false);
                });

                // *** GEOPROCESSING BEGINS!! ***

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                string toolOutput;
                int max = 15;
                int val = 0;

                // Delete all tables from PU GDB
                List<string> delnames = new List<string>();

                await QueuedTask.Run(async () =>
                {
                    using (Geodatabase gdb = await PRZH.GetProjectGDB())
                    {
                        var fcdefs = gdb.GetDefinitions<FeatureClassDefinition>();
                        foreach (var d in fcdefs)
                        {
                            delnames.Add(d.GetName());
                        }

                        var tabdefs = gdb.GetDefinitions<TableDefinition>();
                        foreach (var d in tabdefs)
                        {
                            delnames.Add(d.GetName());
                        }

                        var dsdefs = gdb.GetDefinitions<FeatureDatasetDefinition>();
                        foreach (var u in dsdefs)
                        {
                            delnames.Add(u.GetName());
                        }
                    }
                });

                foreach (string s in delnames)
                {
                    ProMsgBox.Show(s);
                }

                // I'm Here, trying to delete all items in the geodatabase
                await QueuedTask.Run(async () =>
                {
                    using (Geodatabase gdb = await PRZH.GetProjectGDB())
                    {
                        SchemaBuilder sb = new SchemaBuilder(gdb);
                    }
                });


                return false;







                // Delete existing Planning Unit FC
                UpdateProgress(PRZH.WriteLog("Deleting Planning Unit Feature Class..."), true, max, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_PLANNING_UNITS, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                UpdateProgress(PRZH.WriteLog("Create Feature Class: " + ((toolOutput is null) ? "failed or cancelled by user" : "successful")), true);
                if (toolOutput is null) return false;

                // Build the new empty Planning Unit FC
                UpdateProgress(PRZH.WriteLog("Creating empty Planning Unit Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_PLANNING_UNITS, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                UpdateProgress(PRZH.WriteLog("Create Feature Class: " + ((toolOutput is null) ? "failed or cancelled by user" : "successful")), true);
                if (toolOutput is null) return false;

                // Add Fields to Planning Unit FC
                string fldPUID = PRZC.c_FLD_PUFC_ID + " LONG 'Planning Unit ID' # # #;";
                string fldPUCost = PRZC.c_FLD_PUFC_COST + " DOUBLE 'Cost' # 1 #;";
                string fldPUStatus = PRZC.c_FLD_PUFC_STATUS + " LONG 'Status' # 2 #;";
                string flds = fldPUID + fldPUCost + fldPUStatus;

                UpdateProgress(PRZH.WriteLog("Adding Fields..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pufcpath, flds);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, null, GPExecuteToolFlags.RefreshProjectItems);
                UpdateProgress(PRZH.WriteLog("Add Fields: " + ((toolOutput is null) ? "failed or cancelled by user" : "successful")), true);
                if (toolOutput is null) return false;



                // *** BUILD THE GRID

                // calculate my dimensions
                double tile_edge_length = 0;
                double tile_width = 0;
                double tile_center_to_right = 0;
                double tile_height = 0;
                double tile_center_to_top = 0;
                int tiles_across = 0;
                int tiles_up = 0;

                switch(GridType)
                {
                    case PlanningUnitTileShape.SQUARE:
                        tile_edge_length = Math.Sqrt(tile_area);
                        tile_width = tile_edge_length;
                        tile_height = tile_edge_length;
                        tile_center_to_right = tile_width / 2.0;
                        tile_center_to_top = tile_height / 2.0;
                        break;
                    case PlanningUnitTileShape.HEXAGON:
                        tile_edge_length = Math.Sqrt((2 * tile_area) / (3 * Math.Sqrt(3)));
                        tile_width = tile_edge_length * 2;
                        tile_height = 2 * (tile_edge_length * Math.Sin((60 * Math.PI) / 180));
                        tile_center_to_right = tile_width / 2;
                        tile_center_to_top = tile_height / 2;
                        break;
                    default:
                        return false;
                }

                // how many rows & columns of tiles do I need to cover the area?

                // 1. get envelope from buffered study area
                // 2. get dimensions of envelope (height + width)
                // 3. get lower left coords of envelope

                Envelope env = study_area_poly_buffered.Extent;
                double env_width = env.Width;
                double env_height = env.Height;
                MapPoint env_ll_point = null;

                await QueuedTask.Run(() =>
                {
                    MapPointBuilder mpbuilder = new MapPointBuilder(env.XMin, env.YMin, OutputSR);
                    env_ll_point = mpbuilder.ToGeometry();
                });

                switch (GridType)
                {
                    case PlanningUnitTileShape.SQUARE:
                        tiles_across = (int)Math.Ceiling(env_width / tile_width) + 3;
                        tiles_up = (int)Math.Ceiling(env_height / tile_height) + 3;
                        break;
                    case PlanningUnitTileShape.HEXAGON:
                        double temp = Math.Round(env_height / tile_center_to_top);
                        if ((temp % 2) == 1)
                            temp++;
                        tiles_up = Convert.ToInt32((temp / 2) + 3);
                        temp = (((2 * env_width) - (4 * tile_edge_length)) / (3 * tile_edge_length)) + 3;
                        tiles_across = Convert.ToInt32(temp) + 3;
                        break;
                    default:
                        return false;
                }








                #endregion





                UpdateProgress("Operation Complete!", true, 0, 3, 3);
                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                UpdateProgress(ex.Message, true);
                return false;
            }
            finally
            {
                if (cps != null)
                    cps.Dispose();
            }
        }

        private async Task<bool> DoSomeWork(CancelableProgressorSource cps)
        {
//            UpdateProgressStatus("Doing Some Work", 0, 10, 0);
            cps.Message = "whoah there nelly";
            cps.Status = "ABC";

            int i = 0;

            while (i < 10)
            {
                i++;

                await QueuedTask.Run(async () =>
                {
                    await Task.Delay(1000);
                    if (cps.Progressor.CancellationToken.IsCancellationRequested)
                    {
                        // someone clicked the cancel button
//                        UpdateBarStatus("ERROR.  ERROR. ERROR. ERROR.", 0, 1, 0);
                        return;
                    }
                }, cps.Progressor);
                
                if (cps.Progressor.CancellationToken.IsCancellationRequested)
                {
                    // someone clicked the cancel button
  //                  UpdateBarStatus("ERROR.  ERROR. ERROR. ERROR.", 0, 1, 0);
                    return false;
                }

 //               UpdateBarStatus("Doing Some Work - " + i.ToString(), 0, 10, i);
            }

            return true;
        }

        private async Task DoSomeWork2()
        {
            //UpdateProgressStatus("Doing Some Work 2", 0, 500, 0);

            int i = 0;

            while (i < 500)
            {
                i++;

                await QueuedTask.Run(async () =>
                {
                    await Task.Delay(1);
                });

               // UpdateProgressStatus("Doing Some Work 2 - " + i.ToString(), 0, 500, i);
            }
        }

        private async Task DoSomeWork3()
        {
            UpdateProgress("Doing Some Work 3", false, 0, 20, 0);

            int i = 0;

            while (i < 20)
            {
                i++;

                await QueuedTask.Run(async () =>
                {
                    await Task.Delay(300);
                    UpdateProgress("Doing Some Work within QueuedTask.Run - " + i.ToString(), false, 0, 20, i);
                });

            }
        }

        private void SelectSpatialReference()
        {
            try
            {
                ProMsgBox.Show("This will eventually be the Coordinate Systems Picker dialog");

                _userSR = SpatialReferences.WebMercator;
                _outputSR = _userSR;

                if (this.SelectedLayerSR != null)
                {
                    ProMsgBox.Show("Selected SR: " + this.SelectedLayerSR.Name);
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private void SelectMapSR()
        {
            try
            {
                _outputSR = _mapSR;
                this.TestInfo = _mapSR.Name;

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private void SelectUserSR()
        {
            try
            {
                this.TestInfo = "Enabled Select User SR Mode";
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private void SelectLayerSR()
        {
            try
            {
                this.TestInfo = "Enabled Select LAYER SR Mode";
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        
        /// <summary>
        /// Returns true if all required input parameters are valid.  False otherwise.
        /// </summary>
        /// <returns></returns>
        private bool CanBuildPlanningUnits()
        {
            try
            {

                // First validate the Output SR
                if (this.SRMapIsChecked)
                {
                    // If checked, we have a valid output SR   
                }
                else if (this.SRLayerIsChecked)
                {
                    // If checked, we have at least one valid Layer
                    // Check to see if user has selected a valid layer
                    if (this.SelectedLayerSR == null)
                    {
                        // no valid layer means no output SR
                        return false;
                    }
                }
                else if (this.SRUserIsChecked)
                {
                    // not implemented yet
                    return false;
                }
                else
                {
                    // No radio buttons selected, return false
                    return false;
                }

                // Next validate Study Area Source
                if (GraphicsLayerIsChecked)
                {
                    if (SelectedGraphicsLayer == null)
                    {
                        return false;
                    }
                }
                else if (FeatureLayerIsChecked)
                {
                    if (SelectedFeatureLayer == null)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                // Next validate the buffer value
                if (!string.IsNullOrEmpty(BufferValue))
                {
                    double buffval;
                    if (double.TryParse(BufferValue.Trim(), out buffval))
                    {
                        if (buffval < 0)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                // Next validate the X and Y grid alignment variables

                string xtrim = (string.IsNullOrEmpty(GridAlign_X) ? "" : GridAlign_X.Trim());
                string ytrim = (string.IsNullOrEmpty(GridAlign_Y) ? "" : GridAlign_Y.Trim());

                if (xtrim.Length > 0 && ytrim.Length > 0)
                {
                    double x;
                    double y;

                    if (!double.TryParse(xtrim, out x))
                    {
                        return false;
                    }
                    if (!double.TryParse(ytrim, out y))
                    {
                        return false;
                    }
                }
                else if (xtrim.Length > 0 && ytrim.Length == 0)
                {
                    return false;
                }
                else if (xtrim.Length == 0 && ytrim.Length > 0)
                {
                    return false;
                }

                // Next, validate the Tile Type
                if (string.IsNullOrEmpty(SelectedGridType))
                {
                    return false;
                }

                // Tile Area
                if (!string.IsNullOrEmpty(TileArea))
                {
                    double tilearea;

                    if (double.TryParse(TileArea.Trim(), out tilearea))
                    {
                        if (tilearea < 0)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }


        private void UpdateProgress(string message, bool append)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                UpdateProgressMaster(message, append, null, null, null);
            }
            else
            {
                ProApp.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
                {
                    UpdateProgressMaster(message, append, null, null, null);
                }));
            }
        }

        private void UpdateProgress(string message, bool append, int val)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                UpdateProgressMaster(message, append, null, null, val);
            }
            else
            {
                ProApp.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
                {
                    UpdateProgressMaster(message, append, null, null, val);
                }));
            }
        }

        private void UpdateProgress(string message, bool append, int max, int val)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                UpdateProgressMaster(message, append, null, max, val);
            }
            else
            {
                ProApp.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
                {
                    UpdateProgressMaster(message, append, null, max, val);
                }));
            }
        }

        private void UpdateProgress(string message, bool append, int min, int max, int val)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                UpdateProgressMaster(message, append, min, max, val);
            }
            else
            {
                ProApp.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
                {
                    UpdateProgressMaster(message, append, min, max, val);
                }));
            }
        }

        private void UpdateProgressMaster(string message, bool? append, int? min, int? max, int? val)
        {
            try
            {
                // Update the Message if required
                if (append != null)
                {
                    if (string.IsNullOrEmpty(message))
                    {
                        BarMessage = (bool)append ? (BarMessage + Environment.NewLine + "") : "";
                    }
                    else
                    {
                        BarMessage = (bool)append ? (BarMessage + Environment.NewLine + message) : message;
                    }
                }

                // Update the Min property
                if (min != null)
                {
                    BarMin = (int)min;
                }

                // Update the Max property
                if (max != null)
                {
                    BarMax = (int)max;
                }

                // Update the Value property
                if (val != null)
                {
                    BarValue = (int)val;
                }


            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        #endregion

        private SpatialReference GetOutputSR()
        {
            try
            {
                if (this.SRMapIsChecked)
                {
                    return _mapSR;
                }
                else if (this.SRLayerIsChecked)
                {
                    if (this.SelectedLayerSR != null)
                    {
                        return this.SelectedLayerSR;
                    }
                }
                else if (this.SRUserIsChecked)
                {
                    // not implemented yet
                }

                return null;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

    }
}




//                await DoSomeWork2();

//                await DoSomeWork3();


//progressorSource.Max = 30;
//progressorSource.Value = 0;
//progressorSource.Status = "Phase 1...";
//progressDialog.Show();
////                progressor.Status = "Phase 1...";

//int i = 0;

//while (i < 10)
//{
//    i++;
//    progressorSource.Value += 1;
//    progressorSource.Message = "Step " + i.ToString();

//    await QueuedTask.Run(async () =>
//    {
//        await Task.Delay(1000);
//    });

//    if (progressorSource.CancellationTokenSource.IsCancellationRequested)
//    {
//        return;
//    }
//}

//progressorSource.Value = 0;
//progressorSource.Max = 5;
////progressor.Value = 0;
////progressor.Max = 5;
//progressorSource.Status = "Phase 2...";

//i = 0;

//while (i < 5)
//{
//    i++;
//    //progressor.Value += 1;
//    progressorSource.Value += 1;
//    progressorSource.Message = "Step " + i.ToString();
//    //progressor.Message = "Step " + i.ToString();

//    await QueuedTask.Run(async () =>
//    {
//        await Task.Delay(1000);
//    });

//    if (progressorSource.CancellationTokenSource.IsCancellationRequested)
//    {
//        return;
//    }
//}
