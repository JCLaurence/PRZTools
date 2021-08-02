using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZC = NCC.PRZTools.PRZConstants;
using PRZH = NCC.PRZTools.PRZHelper;
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
            get => _srMapIsChecked; set => SetProperty(ref _srMapIsChecked, value, () => SRMapIsChecked);
        }

        private bool _srLayerIsChecked;
        public bool SRLayerIsChecked
        {
            get => _srLayerIsChecked; set => SetProperty(ref _srLayerIsChecked, value, () => SRLayerIsChecked);
        }

        private bool _srUserIsChecked;
        public bool SRUserIsChecked
        {
            get => _srUserIsChecked; set => SetProperty(ref _srUserIsChecked, value, () => SRUserIsChecked);
        }

        private string _mapSRName;
        public string MapSRName
        {
            get => _mapSRName; set => SetProperty(ref _mapSRName, value, () => MapSRName);
        }

        private string _userSRName;
        public string UserSRName
        {
            get => _userSRName; set => SetProperty(ref _userSRName, value, () => UserSRName);
        }

        private List<SpatialReference> _layerSRList;
        public List<SpatialReference> LayerSRList
        {
            get => _layerSRList; set => SetProperty(ref _layerSRList, value, () => LayerSRList);
        }

        private bool _srMapIsEnabled;
        public bool SRMapIsEnabled
        {
            get => _srMapIsEnabled; set => SetProperty(ref _srMapIsEnabled, value, () => SRMapIsEnabled);
        }

        private bool _srLayerIsEnabled;
        public bool SRLayerIsEnabled
        {
            get => _srLayerIsEnabled; set => SetProperty(ref _srLayerIsEnabled, value, () => SRLayerIsEnabled);
        }

        private bool _srUserIsEnabled;
        public bool SRUserIsEnabled
        {
            get => _srUserIsEnabled; set => SetProperty(ref _srUserIsEnabled, value, () => SRUserIsEnabled);
        }

        private SpatialReference _selectedLayerSR;
        public SpatialReference SelectedLayerSR
        {
            get => _selectedLayerSR; set => SetProperty(ref _selectedLayerSR, value, () => SelectedLayerSR);
        }

        private List<GraphicsLayer> _graphicsLayerList;
        public List<GraphicsLayer> GraphicsLayerList
        {
            get => _graphicsLayerList; set => SetProperty(ref _graphicsLayerList, value, () => GraphicsLayerList);
        }

        private GraphicsLayer _selectedGraphicsLayer;
        public GraphicsLayer SelectedGraphicsLayer
        {
            get => _selectedGraphicsLayer; set => SetProperty(ref _selectedGraphicsLayer, value, () => SelectedGraphicsLayer);
        }

        private List<FeatureLayer> _featureLayerList;
        public List<FeatureLayer> FeatureLayerList
        {
            get => _featureLayerList; set => SetProperty(ref _featureLayerList, value, () => FeatureLayerList);
        }

        private FeatureLayer _selectedFeatureLayer;
        public FeatureLayer SelectedFeatureLayer
        {
            get => _selectedFeatureLayer; set => SetProperty(ref _selectedFeatureLayer, value, () => SelectedFeatureLayer);
        }

        private string _bufferValue;
        public string BufferValue
        {
            get => _bufferValue; set => SetProperty(ref _bufferValue, value, () => BufferValue);
        }

        private string _gridAlign_X;
        public string GridAlign_X
        {
            get => _gridAlign_X; set => SetProperty(ref _gridAlign_X, value, () => GridAlign_X);
        }

        private string _gridAlign_Y;
        public string GridAlign_Y
        {
            get => _gridAlign_Y; set => SetProperty(ref _gridAlign_Y, value, () => GridAlign_Y);
        }

        private bool _bufferUnitMetersIsChecked;
        public bool BufferUnitMetersIsChecked
        {
            get => _bufferUnitMetersIsChecked; set => SetProperty(ref _bufferUnitMetersIsChecked, value, () => BufferUnitMetersIsChecked);
        }

        private bool _bufferUnitKilometersIsChecked;
        public bool BufferUnitKilometersIsChecked
        {
            get => _bufferUnitKilometersIsChecked; set => SetProperty(ref _bufferUnitKilometersIsChecked, value, () => BufferUnitKilometersIsChecked);
        }

        private List<string> _gridTypeList;
        public List<string> GridTypeList
        {
            get => _gridTypeList; set => SetProperty(ref _gridTypeList, value, () => GridTypeList);
        }

        private string _selectedGridType;
        public string SelectedGridType
        {
            get => _selectedGridType; set => SetProperty(ref _selectedGridType, value, () => SelectedGridType);
        }

        private string _tileArea;
        public string TileArea
        {
            get => _tileArea; set => SetProperty(ref _tileArea, value, () => TileArea);
        }

        private bool _tileAreaMIsSelected;
        public bool TileAreaMIsSelected
        {
            get => _tileAreaMIsSelected; set => SetProperty(ref _tileAreaMIsSelected, value, () => TileAreaMIsSelected);
        }

        private bool _tileAreaAcIsSelected;
        public bool TileAreaAcIsSelected
        {
            get => _tileAreaAcIsSelected; set => SetProperty(ref _tileAreaAcIsSelected, value, () => TileAreaAcIsSelected);
        }

        private bool _tileAreaHaIsSelected;
        public bool TileAreaHaIsSelected
        {
            get => _tileAreaHaIsSelected; set => SetProperty(ref _tileAreaHaIsSelected, value, () => TileAreaHaIsSelected);
        }

        private bool _tileAreaKmIsSelected;
        public bool TileAreaKmIsSelected
        {
            get => _tileAreaKmIsSelected; set => SetProperty(ref _tileAreaKmIsSelected, value, () => TileAreaKmIsSelected);
        }

        private bool _buildIsEnabled;
        public bool BuildIsEnabled
        {
            get => _buildIsEnabled; set => SetProperty(ref _buildIsEnabled, value, () => BuildIsEnabled);
        }

        private bool _graphicsLayerIsEnabled;
        public bool GraphicsLayerIsEnabled
        {
            get => _graphicsLayerIsEnabled; set => SetProperty(ref _graphicsLayerIsEnabled, value, () => GraphicsLayerIsEnabled);
        }

        private bool _graphicsLayerIsChecked;
        public bool GraphicsLayerIsChecked
        {
            get => _graphicsLayerIsChecked; set => SetProperty(ref _graphicsLayerIsChecked, value, () => GraphicsLayerIsChecked);
        }

        private bool _featureLayerIsEnabled;
        public bool FeatureLayerIsEnabled
        {
            get => _featureLayerIsEnabled; set => SetProperty(ref _featureLayerIsEnabled, value, () => FeatureLayerIsEnabled);
        }

        private bool _featureLayerIsChecked;
        public bool FeatureLayerIsChecked
        {
            get => _featureLayerIsChecked; set => SetProperty(ref _featureLayerIsChecked, value, () => FeatureLayerIsChecked);
        }

        private int _barMax;
        public int BarMax
        {
            get => _barMax; set => SetProperty(ref _barMax, value, () => BarMax);
        }

        private int _barMin;
        public int BarMin
        {
            get => _barMin; set => SetProperty(ref _barMin, value, () => BarMin);
        }

        private int _barValue;
        public int BarValue
        {
            get => _barValue; set => SetProperty(ref _barValue, value, () => BarValue);
        }

        private string _barMessage;
        public string BarMessage
        {
            get => _barMessage; set => SetProperty(ref _barMessage, value, () => BarMessage);
        }

        #endregion

        #region Commands

        private ICommand _cmdSelectSpatialReference;
        public ICommand CmdSelectSpatialReference => _cmdSelectSpatialReference ?? (_cmdSelectSpatialReference = new RelayCommand(() => SelectSpatialReference(), () => true));

        private ICommand _cmdSRMap;
        public ICommand CmdSRMap => _cmdSRMap ?? (_cmdSRMap = new RelayCommand(() => SelectMapSR(), () => true));

        private ICommand _cmdSRUser;
        public ICommand CmdSRUser => _cmdSRUser ?? (_cmdSRUser = new RelayCommand(() => SelectUserSR(), () => true));

        private ICommand _cmdSRLayer;
        public ICommand CmdSRLayer => _cmdSRLayer ?? (_cmdSRLayer = new RelayCommand(() => SelectLayerSR(), () => true));

        private ICommand _cmdBuildPlanningUnits;
        public ICommand CmdBuildPlanningUnits => _cmdBuildPlanningUnits ?? (_cmdBuildPlanningUnits = new RelayCommand(async () => await BuildPlanningUnits(), () => true));

        private ICommand _cmdClearConstructionLog;
        public ICommand CmdClearConstructionLog => _cmdClearConstructionLog ?? (_cmdClearConstructionLog = new RelayCommand(() =>
        {
            BarMessage = "";
            BarMin = 0;
            BarValue = 0;
            BarMax = 3;
        }, () => true));

        public ICommand cmdOK => new RelayCommand((paramProWin) =>
        {
            (paramProWin as ProWindow).DialogResult = true;
            (paramProWin as ProWindow).Close();
        }, () => true);

        public ICommand cmdCancel => new RelayCommand((paramProWin) =>
        {
            (paramProWin as ProWindow).DialogResult = false;
            (paramProWin as ProWindow).Close();
        }, () => true);

        #endregion

        #region Methods

        public async void OnProWinLoaded()
        {
            try
            {
                #region Spatial Reference Information

                MapView mapView = MapView.Active;
                Map map = mapView.Map;
                _mapSR = map.SpatialReference;
                MapSRName = _mapSR.Name;
                bool isMapSRProjM = false;

                if (_mapSR.IsProjected)
                {
                    Unit u = _mapSR.Unit; // should be LinearUnit, since SR is projected
                    LinearUnit lu = u as LinearUnit;
                    if (lu.FactoryCode == 9001) // meter
                    {
                        isMapSRProjM = true;
                    }
                }

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
                                {
                                    Unit u = sr.Unit;

                                    if (u.UnitType == ArcGIS.Core.Geometry.UnitType.Linear)
                                    {
                                        LinearUnit lu = u as LinearUnit;

                                        if (lu.FactoryCode == 9001) // Meter
                                        {
                                            SRList.Add(sr);
                                        }
                                    }
                                }
                            }
                        }
                    });
                }

                this.LayerSRList = SRList;

                // SR Radio Options enabled/disabled
                this.SRLayerIsEnabled = (SRList.Count > 0);
                this.SRMapIsEnabled = isMapSRProjM;
                this.SRUserIsEnabled = false;

                // Graphics Layers having selected Polygon Graphics
                var glyrs = map.GetLayersAsFlattenedList().OfType<GraphicsLayer>().ToList();
                var polygraphicList = new List<CIMPolygonGraphic>();

                Dictionary<GraphicsLayer, int> DICT_GraphicLayer_SelPolyCount = new Dictionary<GraphicsLayer, int>();

                List<GraphicsLayer> gls = new List<GraphicsLayer>();
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
                this.BufferUnitKilometersIsChecked = true;

                // Grid Type combo box
                this.GridTypeList = Enum.GetNames(typeof(PlanningUnitTileShape)).ToList();

                this.SelectedGridType = PlanningUnitTileShape.SQUARE.ToString();

                // Tile area units
                this.TileAreaKmIsSelected = true;

                StringBuilder sb = new StringBuilder();

                sb.AppendLine("Map Name: " + map.Name);
                sb.AppendLine("Spatial Reference: " + _mapSR.Name);

                BarMin = 0;
                BarMax = 3;
                BarValue = 0;

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
            int val = 0;

            try
            {
                #region INITIALIZATION AND USER INPUT VALIDATION

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Initialize ProgressBar and Progress Log
                int max = 30;
                UpdateProgress(PRZH.WriteLog("Initializing the Planning Unit Constructor..."), false, max, ++val); // First message in Progress Log

                // Validation: Project Geodatabase
                string gdbpath = PRZH.GetProjectGDBPath();
                if (!await PRZH.ProjectGDBExists())
                {
                    UpdateProgress(PRZH.WriteLog("Validation >> Project Geodatabase not found: " + gdbpath, LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Project Geodatabase not found at this path:" + 
                                   Environment.NewLine +
                                   gdbpath + 
                                   Environment.NewLine + Environment.NewLine + 
                                   "Please specify a valid Project Workspace.", "Validation");

                    return false;
                }
                else
                {
                    UpdateProgress(PRZH.WriteLog("Validation >> Project Geodatabase is OK: " + gdbpath), true, ++val);
                }

                // Validation: Output Spatial Reference
                var OutputSR = GetOutputSR();
                if (OutputSR == null)
                {
                    UpdateProgress(PRZH.WriteLog("Validation >> Unspecified or invalid output Spatial Reference", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Output Spatial Reference", "Validation");
                    return false;
                }
                else
                {
                    UpdateProgress(PRZH.WriteLog("Validation >> Output Spatial Reference is OK: " + OutputSR.Name), true, ++val);
                }

                // Validation: Study Area Source Geometry
                if (GraphicsLayerIsChecked)
                {
                    if (SelectedGraphicsLayer == null)
                    {
                        UpdateProgress(PRZH.WriteLog("Validation >> No Graphics Layer was selected.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("You must specify a Graphics Layer", "Validation");
                        return false;
                    }
                    else
                    {
                        UpdateProgress(PRZH.WriteLog("Validation >> Graphics Layer Name: " + SelectedGraphicsLayer.Name), true, ++val);
                    }
                }
                else if (FeatureLayerIsChecked)
                {
                    if (SelectedFeatureLayer == null)
                    {
                        UpdateProgress(PRZH.WriteLog("Validation >> No Feature Layer was selected.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("You must specify a Feature Layer", "Validation");
                        return false;
                    }
                    else
                    {
                        UpdateProgress(PRZH.WriteLog("Validation >> Feature Layer Name: " + SelectedFeatureLayer.Name), true, ++val);
                    }
                }
                else
                {
                    UpdateProgress(PRZH.WriteLog("Validation >> No Graphics Layer or Feature Layer was selected.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("You must select either a Graphics Layer or a Feature Layer", "Validation");
                    return false;
                }

                // Validation: Buffer Distance
                string buffer_dist_text = string.IsNullOrEmpty(BufferValue) ? "0" : ((BufferValue.Trim() == "") ? "0" : BufferValue.Trim());

                if (!double.TryParse(buffer_dist_text, out double buffer_dist))
                {
                    UpdateProgress(PRZH.WriteLog("Validation >> Invalid buffer distance specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Invalid Buffer Distance specified.  The value must be numeric and >= 0, or blank.", "Validation");
                    return false;
                }
                else if (buffer_dist < 0)
                {
                    UpdateProgress(PRZH.WriteLog("Validation >> Invalid buffer distance specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Invalid Buffer Distance specified.  The value must be >= 0", "Validation");
                    return false;
                }

                double buffer_dist_m = 0;
                string bu = "";

                if (BufferUnitMetersIsChecked)
                {
                    bu = buffer_dist_text + " m";
                    buffer_dist_m = buffer_dist;
                }
                else if (BufferUnitKilometersIsChecked)
                {
                    bu = buffer_dist_text + " km";
                    buffer_dist_m = buffer_dist * 1000.0;
                }

                UpdateProgress(PRZH.WriteLog("Validation >> Buffer Distance = " + bu), true, ++val);

                // Validation: Tile Shape
                string tile_shape = (string.IsNullOrEmpty(SelectedGridType) ? "" : SelectedGridType);
                if (tile_shape == "")
                {
                    UpdateProgress(PRZH.WriteLog("Validation >> Tile shape not specified", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a tile shape", "Validation");
                    return false;
                }
                else
                {
                    UpdateProgress(PRZH.WriteLog("Validation >> Tile Shape is " + tile_shape), true, ++val);
                }

                var TileShape = PlanningUnitTileShape.SQUARE;
                switch (tile_shape)
                {
                    case "SQUARE":
                        TileShape = PlanningUnitTileShape.SQUARE;
                        break;
                    case "HEXAGON":
                        TileShape = PlanningUnitTileShape.HEXAGON;
                        break;
                    default:
                        return false;
                }

                // Validation: Tile Area
                string tile_area_text = string.IsNullOrEmpty(TileArea) ? "0" : ((TileArea.Trim() == "") ? "0" : TileArea.Trim());

                if (!double.TryParse(tile_area_text, out double tile_area_m2))
                {
                    UpdateProgress(PRZH.WriteLog("Validation >> Missing or invalid Tile Area", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Tile Area.  Value must be numeric and greater than 0", "Validation");
                    return false;
                }
                else if (tile_area_m2 <= 0)
                {
                    UpdateProgress(PRZH.WriteLog("Validation >> Missing or invalid Tile Area", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Tile Area.  Value must be numeric and greater than 0", "Validation");
                    return false;
                }
                else
                {
                    string au = "";

                    if (TileAreaMIsSelected)
                    {
                        au = tile_area_text + " m\xB2";
                    }
                    else if (TileAreaAcIsSelected)
                    {
                        au = tile_area_text + " ac";
                        tile_area_m2 *= 4046.86;
                    }
                    else if (TileAreaHaIsSelected)
                    {
                        au = tile_area_text + " ha";
                        tile_area_m2 *= 10000.0;
                    }
                    else if (TileAreaKmIsSelected)
                    {
                        au = tile_area_text + " km\xB2";
                        tile_area_m2 *= 1000000.0;
                    }

                    UpdateProgress(PRZH.WriteLog("Validation >> Tile Area = " + au), true, ++val);
                }

                // Validation: Grid Alignment Coordinates
                string gridalign_x_text = string.IsNullOrEmpty(GridAlign_X) ? "" : GridAlign_X.Trim();
                string gridalign_y_text = string.IsNullOrEmpty(GridAlign_Y) ? "" : GridAlign_Y.Trim();

                double gridalign_x_coord = 0;
                double gridalign_y_coord = 0;

                bool align_grid = false;
                bool alignment_param_error = false;

                if (gridalign_x_text.Length > 0 && gridalign_y_text.Length > 0)
                {
                    if (!double.TryParse(gridalign_x_text, out gridalign_x_coord))
                    {
                        alignment_param_error = true;
                    }
                    else if (!double.TryParse(gridalign_y_text, out gridalign_y_coord))
                    {
                        alignment_param_error = true;
                    }
                    else
                    {
                        align_grid = true;
                    }
                }
                else if (gridalign_x_text.Length > 0 && gridalign_y_text.Length == 0)
                {
                    alignment_param_error = true;
                }
                else if (gridalign_x_text.Length == 0 && gridalign_y_text.Length > 0)
                {
                    alignment_param_error = true;
                }

                if (alignment_param_error)
                {
                    UpdateProgress(PRZH.WriteLog("Validation >> Invalid tile grid alignment coordinates.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Invalid Tile Grid Alignment Coordinates.  Both X and Y values must be numeric, or both must be blank.", "Validation");
                    return false;
                }
                else if (align_grid)
                {
                    UpdateProgress(PRZH.WriteLog("Validation >> Tile grid alignment coordinates are OK: (" + gridalign_x_text + ", " + gridalign_y_text + ")"), true, ++val);
                }
                else
                {
                    UpdateProgress(PRZH.WriteLog("Validation >> No tile grid alignment was specified, and that's OK."), true, ++val);
                }

                // Notify users what will happen if they proceed
                if (ProMsgBox.Show("The Planning Units and Study Area layers are the heart of the various tabular data used by PRZ Tools.  " +
                                   "Running this task invalidates all existing tables since the planning unit IDs and count may be different." +
                                   Environment.NewLine + Environment.NewLine +
                                   "If you proceed, all tables and feature classes in the PRZ File Geodatabase WILL BE DELETED!!" +
                                   Environment.NewLine + Environment.NewLine +
                                   "Do you wish to proceed?" +
                                   Environment.NewLine + Environment.NewLine +
                                   "Choose wisely...",
                                   "Table/Feature Class Overwrite Warning",
                                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    UpdateProgress(PRZH.WriteLog("User bailed out."), true, ++val);
                    return false;
                }

                #endregion

                #region STUDY AREA GEOMETRY

                // Retrieve Polygons to construct Study Area + Buffered Study Area
                List<Polygon> LIST_SA_polys = new List<Polygon>();
                Polygon SA_poly = null;

                if (GraphicsLayerIsChecked)
                {
                    // Get the selected polygon graphics from the graphics layer
                    GraphicsLayer gl = SelectedGraphicsLayer;

                    var selElems = gl.GetSelectedElements().OfType<GraphicElement>();
                    int polyelems = 0;

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
                                LIST_SA_polys.Add(p3);
                                polyelems++;
                            }
                        }

                        Polygon poly = polyBuilder.ToGeometry();
                        SA_poly = (Polygon)GeometryEngine.Instance.Project(poly, OutputSR);
                    });

                    UpdateProgress(PRZH.WriteLog("Study Area >> Retrieved " + polyelems.ToString() + " selected polygon(s) from Graphics Layer: " + gl.Name), true, ++val);
                }
                else if (FeatureLayerIsChecked)
                {
                    // Get the selected polygon features from the selected feature layer
                    FeatureLayer fl = SelectedFeatureLayer;

                    int selpol = 0;

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
                                        LIST_SA_polys.Add(p3);
                                        selpol++;
                                    }
                                }
                            }
                        }

                        Polygon poly = polyBuilder.ToGeometry();
                        SA_poly = (Polygon)GeometryEngine.Instance.Project(poly, OutputSR);                                               
                    });

                    UpdateProgress(PRZH.WriteLog("Study Area >> Retrieved " + selpol.ToString() + " selected polygon(s) from Layer: " + fl.Name), true, ++val);
                }

                // Generate Buffered Polygons (buffer might be 0)
                Polygon SA_poly_buffer = GeometryEngine.Instance.Buffer(SA_poly, buffer_dist_m) as Polygon;
                Polygon SA_polys_buffer = await QueuedTask.Run(() =>
                {
                    return GeometryEngine.Instance.Buffer(LIST_SA_polys, buffer_dist_m) as Polygon;
                }); // I probably don't need this one, it was just a test of a different way to get my final single buffer poly

                List<Polygon> LIST_BufferedPolys = new List<Polygon>();
                foreach (Polygon p in LIST_SA_polys)
                {
                    Polygon b = GeometryEngine.Instance.Buffer(p, buffer_dist_m) as Polygon;
                    LIST_BufferedPolys.Add(b);
                }

                UpdateProgress(PRZH.WriteLog("Study Area >> Buffered by " + buffer_dist_m.ToString() + ((buffer_dist_m == 1) ? " meter" : " meters")), true, ++val);

                #endregion

                #region STRIP MAP AND GDB

                // Remove any PRZ GDB Layers or Tables from Active Map
                var map = MapView.Active.Map;
                var flyrs = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                List<Layer> LayersToDelete = new List<Layer>();

                if (!await RemovePRZLayersAndTables())
                {
                    UpdateProgress(PRZH.WriteLog("Unable to remove all Layers and Standalone Tables with source = " + gdbpath, LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    UpdateProgress(PRZH.WriteLog("Removed all Layers and Standalone Tables with source = " + gdbpath), true, ++val);
                }

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                string toolOutput;

                // Delete all Items from Project GDB
                if (!await PRZH.ClearProjectGDB())
                {
                    UpdateProgress(PRZH.WriteLog("Unable to delete Feature Datasets, Feature Classes, and Tables from " + gdbpath, LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    UpdateProgress(PRZH.WriteLog("Deleted all Feature Datasets, Feature Classes, and Tables from " + gdbpath), true, ++val);
                }

                #endregion

                #region CREATE PLANNING UNIT FC

                string pufcpath = PRZH.GetPlanningUnitFCPath();

                // Build the new empty Planning Unit FC
                UpdateProgress(PRZH.WriteLog("Creating Planning Unit Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_PLANNING_UNITS, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    UpdateProgress(PRZH.WriteLog("Error creating Planning Unit FC.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Add Fields to Planning Unit FC
                string fldPUID = PRZC.c_FLD_PUFC_ID + " LONG 'Planning Unit ID' # # #;";
                string fldNCCID = PRZC.c_FLD_PUFC_NCC_ID + " LONG 'NCC ID' # # #;";
                string fldPUCost = PRZC.c_FLD_PUFC_COST + " DOUBLE 'Cost' # 1 #;";
                string fldPUStatus = PRZC.c_FLD_PUFC_STATUS + " LONG 'Status' # 2 #;";
                string flds = fldPUID + fldNCCID + fldPUCost + fldPUStatus;

                UpdateProgress(PRZH.WriteLog("Adding fields to Planning Unit FC..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pufcpath, flds);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, null, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    UpdateProgress(PRZH.WriteLog("Error adding fields to Planning Unit FC.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Assemble the Grid
                double tile_edge_length = 0;
                double tile_width = 0;
                double tile_center_to_right = 0;
                double tile_height = 0;
                double tile_center_to_top = 0;
                int tiles_across = 0;
                int tiles_up = 0;

                switch(TileShape)
                {
                    case PlanningUnitTileShape.SQUARE:
                        tile_edge_length = Math.Sqrt(tile_area_m2);
                        tile_width = tile_edge_length;
                        tile_height = tile_edge_length;
                        tile_center_to_right = tile_width / 2.0;
                        tile_center_to_top = tile_height / 2.0;
                        break;
                    case PlanningUnitTileShape.HEXAGON:
                        tile_edge_length = Math.Sqrt((2 * tile_area_m2) / (3 * Math.Sqrt(3)));
                        tile_width = tile_edge_length * 2;
                        tile_height = 2 * (tile_edge_length * Math.Sin((60 * Math.PI) / 180));
                        tile_center_to_right = tile_width / 2;
                        tile_center_to_top = tile_height / 2;
                        break;
                    default:
                        return false;
                }

                Envelope env = SA_poly_buffer.Extent;
                double env_width = env.Width;
                double env_height = env.Height;
                MapPoint env_ll_point = null;

                await QueuedTask.Run(() =>
                {
                    MapPointBuilder mpbuilder = new MapPointBuilder(env.XMin, env.YMin, OutputSR);
                    env_ll_point = mpbuilder.ToGeometry();
                });

                switch (TileShape)
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

                PlanningUnitTileInfo tileinfo = new PlanningUnitTileInfo();
                tileinfo.LL_Point = env_ll_point;
                tileinfo.tiles_across = tiles_across;
                tileinfo.tiles_up = tiles_up;
                tileinfo.tile_area = tile_area_m2;
                tileinfo.tile_center_to_right = tile_center_to_right;
                tileinfo.tile_center_to_top = tile_center_to_top;
                tileinfo.tile_edge_length = tile_edge_length;
                tileinfo.tile_height = tile_height;
                tileinfo.tile_shape = TileShape;
                tileinfo.tile_width = tile_width;

                UpdateProgress(PRZH.WriteLog("Loading Tiles into Planning Unit Feature Class..."), true, ++val);
                if (!await LoadTiles(tileinfo))
                {
                    UpdateProgress(PRZH.WriteLog("Tile Load failed...", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Retain only those tiles overlapping the buffered study area polygon
                UpdateProgress(PRZH.WriteLog("Deleting Tiles outside of buffered study area..."), true, ++val);
                if (!await StripTiles(SA_poly_buffer))
                {
                    UpdateProgress(PRZH.WriteLog("Tile Deletion failed...", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Index the PU ID field
                UpdateProgress(PRZH.WriteLog("Indexing fields in the Planning Unit Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_PLANNING_UNITS, PRZC.c_FLD_PUFC_ID, "ix" + PRZC.c_FLD_PUFC_ID, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, GPExecuteToolFlags.None);
                if (toolOutput == null)
                {
                    UpdateProgress(PRZH.WriteLog("Error indexing fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                #endregion

                #region CREATE STUDY AREA FC

                string safcpath = PRZH.GetStudyAreaFCPath();

                // Build the new empty Main Study Area FC
                UpdateProgress(PRZH.WriteLog("Creating Main Study Area Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_STUDY_AREA_MAIN, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    UpdateProgress(PRZH.WriteLog("Error creating main Study Area Feature Class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Add Fields to Main Study Area FC
                string fldArea_ac = PRZC.c_FLD_SAFC_AREA_AC + " DOUBLE 'Acres' # 1 #;";
                string fldArea_ha = PRZC.c_FLD_SAFC_AREA_HA + " DOUBLE 'Hectares' # 1 #;";
                string fldArea_km = PRZC.c_FLD_SAFC_AREA_KM + " DOUBLE 'Square km' # 1 #;";
                string SAflds = fldArea_ac + fldArea_ha + fldArea_km;

                UpdateProgress(PRZH.WriteLog("Adding fields to Main Study Area Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(safcpath, SAflds);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, null, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    UpdateProgress(PRZH.WriteLog("Error adding fields to main Study Area FC.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Add Feature to Main Study Area FC
                if (!await QueuedTask.Run(async () =>
                {
                    using (Geodatabase gdb = await PRZH.GetProjectGDB())
                    using (FeatureClass saFC = gdb.OpenDataset<FeatureClass>(PRZC.c_FC_STUDY_AREA_MAIN))
                    using (RowBuffer rowBuffer = saFC.CreateRowBuffer())
                    {
                        try
                        {
                            // Get the Definition
                            FeatureClassDefinition fcDef = saFC.GetDefinition();

                            // Field Indexes
                            int ixShape = fcDef.FindField(fcDef.GetShapeField());
                            int ixAcres = fcDef.FindField(PRZC.c_FLD_SAFC_AREA_AC);
                            int ixHectares = fcDef.FindField(PRZC.c_FLD_SAFC_AREA_HA);
                            int ixKm2 = fcDef.FindField(PRZC.c_FLD_SAFC_AREA_KM);

                            // Prepare area values
                            double ac = SA_poly.Area * PRZC.c_CONVERT_M2_TO_AC;
                            double ha = SA_poly.Area * PRZC.c_CONVERT_M2_TO_HA;
                            double km2 = SA_poly.Area * PRZC.c_CONVERT_M2_TO_KM2;

                            // Assign values for a one-time row creation
                            rowBuffer[ixShape] = SA_poly;
                            rowBuffer[ixAcres] = ac;
                            rowBuffer[ixHectares] = ha;
                            rowBuffer[ixKm2] = km2;

                            // Create the one new row
                            using (Feature feature = saFC.CreateRow(rowBuffer))
                            {
                                feature.Store();    // this may not be necessary
                            }

                            return true;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }
                }))
                {
                    // it did not work
                    UpdateProgress(PRZH.WriteLog("Error loading polygon feature into Study Area Feature Class.", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    // it did work, yay.
                    UpdateProgress(PRZH.WriteLog("Loaded polygon feature into Study Area Feature Class"), true, ++val);
                }


                #endregion

                #region CREATE STUDY AREA (MULTI) FC

                string samultifcpath = PRZH.GetStudyAreaMultiFCPath();

                // Build the new empty Multi Study Area FC
                UpdateProgress(PRZH.WriteLog("Creating Multi-part Study Area Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_STUDY_AREA_MULTI, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    UpdateProgress(PRZH.WriteLog("Error creating multi-part Study Area Feature Class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Add Fields to Multi Study Area FC
                string fldMultiArea_ac = PRZC.c_FLD_SAFC_AREA_AC + " DOUBLE 'Acres' # 1 #;";
                string fldMultiArea_ha = PRZC.c_FLD_SAFC_AREA_HA + " DOUBLE 'Hectares' # 1 #;";
                string fldMultiArea_km = PRZC.c_FLD_SAFC_AREA_KM + " DOUBLE 'Square km' # 1 #;";
                string SAMultiflds = fldMultiArea_ac + fldMultiArea_ha + fldMultiArea_km;

                UpdateProgress(PRZH.WriteLog("Adding fields to multi-part Study Area Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(samultifcpath, SAMultiflds);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, null, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    UpdateProgress(PRZH.WriteLog("Error adding fields to multi-part Study Area FC.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Add Features to Multi Study Area FC
                if (!await QueuedTask.Run(async () =>
                {
                    using (Geodatabase gdb = await PRZH.GetProjectGDB())
                    using (FeatureClass samFC = gdb.OpenDataset<FeatureClass>(PRZC.c_FC_STUDY_AREA_MULTI))
                    using (RowBuffer rowBuffer = samFC.CreateRowBuffer())
                    using (InsertCursor insertCursor = samFC.CreateInsertCursor())
                    {
                        try
                        {
                            // Get the Definition
                            FeatureClassDefinition fcDef = samFC.GetDefinition();

                            // Field Indexes
                            int ixShape = fcDef.FindField(fcDef.GetShapeField());
                            int ixAcres = fcDef.FindField(PRZC.c_FLD_SAFC_AREA_AC);
                            int ixHectares = fcDef.FindField(PRZC.c_FLD_SAFC_AREA_HA);
                            int ixKm2 = fcDef.FindField(PRZC.c_FLD_SAFC_AREA_KM);

                            foreach (Polygon sapoly in LIST_SA_polys)
                            {
                                // Prepare area values
                                double ac = sapoly.Area * PRZC.c_CONVERT_M2_TO_AC;
                                double ha = sapoly.Area * PRZC.c_CONVERT_M2_TO_HA;
                                double km2 = sapoly.Area * PRZC.c_CONVERT_M2_TO_KM2;

                                rowBuffer[ixShape] = sapoly;
                                rowBuffer[ixAcres] = ac;
                                rowBuffer[ixHectares] = ha;
                                rowBuffer[ixKm2] = km2;

                                insertCursor.Insert(rowBuffer);
                                insertCursor.Flush();
                            }

                            return true;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }
                }))
                {
                    // it did not work
                    UpdateProgress(PRZH.WriteLog("Error loading polygon feature into multi-part Study Area Feature Class.", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    // it did work, yay.
                    UpdateProgress(PRZH.WriteLog("Loaded polygon feature into multi-part Study Area Feature Class"), true, ++val);
                }







                #endregion

                #region CREATE BUFFERED STUDY AREA FC

                string sabufffcpath = PRZH.GetStudyAreaBufferFCPath();

                // Build the new empty Main Study Area FC
                UpdateProgress(PRZH.WriteLog("Creating Buffered Study Area Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_STUDY_AREA_MAIN_BUFFERED, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    UpdateProgress(PRZH.WriteLog("Error creating Buffered Study Area Feature Class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Add Fields to Buffered Study Area FC
                string fldBArea_ac = PRZC.c_FLD_SAFC_AREA_AC + " DOUBLE 'Acres' # 1 #;";
                string fldBArea_ha = PRZC.c_FLD_SAFC_AREA_HA + " DOUBLE 'Hectares' # 1 #;";
                string fldBArea_km = PRZC.c_FLD_SAFC_AREA_KM + " DOUBLE 'Square km' # 1 #;";
                string SABflds = fldBArea_ac + fldBArea_ha + fldBArea_km;

                UpdateProgress(PRZH.WriteLog("Adding fields to Buffered Study Area Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(sabufffcpath, SABflds);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, null, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    UpdateProgress(PRZH.WriteLog("Error adding fields to Buffered Study Area FC.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Add Feature to Buffered Study Area FC
                if (!await QueuedTask.Run(async () =>
                {
                    using (Geodatabase gdb = await PRZH.GetProjectGDB())
                    using (FeatureClass sabFC = gdb.OpenDataset<FeatureClass>(PRZC.c_FC_STUDY_AREA_MAIN_BUFFERED))
                    using (RowBuffer rowBuffer = sabFC.CreateRowBuffer())
                    {
                        try
                        {
                            // Get the Definition
                            FeatureClassDefinition fcDef = sabFC.GetDefinition();

                            // Field Indexes
                            int ixShape = fcDef.FindField(fcDef.GetShapeField());
                            int ixAcres = fcDef.FindField(PRZC.c_FLD_SAFC_AREA_AC);
                            int ixHectares = fcDef.FindField(PRZC.c_FLD_SAFC_AREA_HA);
                            int ixKm2 = fcDef.FindField(PRZC.c_FLD_SAFC_AREA_KM);

                            // Prepare area values
                            double ac = SA_poly_buffer.Area * PRZC.c_CONVERT_M2_TO_AC;
                            double ha = SA_poly_buffer.Area * PRZC.c_CONVERT_M2_TO_HA;
                            double km2 = SA_poly_buffer.Area * PRZC.c_CONVERT_M2_TO_KM2;

                            // Assign values for a one-time row creation
                            rowBuffer[ixShape] = SA_poly_buffer;
                            rowBuffer[ixAcres] = ac;
                            rowBuffer[ixHectares] = ha;
                            rowBuffer[ixKm2] = km2;

                            // Create the one new row
                            using (Feature feature = sabFC.CreateRow(rowBuffer))
                            {
                                feature.Store();    // this may not be necessary
                            }

                            return true;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }
                }))
                {
                    // it did not work
                    UpdateProgress(PRZH.WriteLog("Error loading polygon feature into Buffered Study Area Feature Class.", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    // it did work, yay.
                    UpdateProgress(PRZH.WriteLog("Loaded polygon feature into Buffered Study Area Feature Class"), true, ++val);
                }

                #endregion

                #region CREATE BUFFERED STUDY AREA (MULTI) FC

                string multibfcpath = PRZH.GetStudyAreaBufferMultiFCPath();

                // Build the new empty Buffered Multi Study Area FC
                UpdateProgress(PRZH.WriteLog("Creating Multi-part Buffered Study Area Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_STUDY_AREA_MULTI_BUFFERED, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    UpdateProgress(PRZH.WriteLog("Error creating multi-part buffered Study Area Feature Class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Add Fields to Multi Buffered Study Area FC
                string fldMultiBArea_ac = PRZC.c_FLD_SAFC_AREA_AC + " DOUBLE 'Acres' # 1 #;";
                string fldMultiBArea_ha = PRZC.c_FLD_SAFC_AREA_HA + " DOUBLE 'Hectares' # 1 #;";
                string fldMultiBArea_km = PRZC.c_FLD_SAFC_AREA_KM + " DOUBLE 'Square km' # 1 #;";
                string SAMultiBflds = fldMultiBArea_ac + fldMultiBArea_ha + fldMultiBArea_km;

                UpdateProgress(PRZH.WriteLog("Adding fields to multi-part Buffered Study Area Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(multibfcpath, SAMultiBflds);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, null, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    UpdateProgress(PRZH.WriteLog("Error adding fields to multi-part Buffered Study Area FC.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Add Features to Multi Buffered Study Area FC
                if (!await QueuedTask.Run(async () =>
                {
                    using (Geodatabase gdb = await PRZH.GetProjectGDB())
                    using (FeatureClass sambFC = gdb.OpenDataset<FeatureClass>(PRZC.c_FC_STUDY_AREA_MULTI_BUFFERED))
                    using (RowBuffer rowBuffer = sambFC.CreateRowBuffer())
                    using (InsertCursor insertCursor = sambFC.CreateInsertCursor())
                    {
                        try
                        {
                            // Get the Definition
                            FeatureClassDefinition fcDef = sambFC.GetDefinition();

                            // Field Indexes
                            int ixShape = fcDef.FindField(fcDef.GetShapeField());
                            int ixAcres = fcDef.FindField(PRZC.c_FLD_SAFC_AREA_AC);
                            int ixHectares = fcDef.FindField(PRZC.c_FLD_SAFC_AREA_HA);
                            int ixKm2 = fcDef.FindField(PRZC.c_FLD_SAFC_AREA_KM);

                            foreach (Polygon c in LIST_BufferedPolys)
                            {
                                // Prepare area values
                                double ac = c.Area * PRZC.c_CONVERT_M2_TO_AC;
                                double ha = c.Area * PRZC.c_CONVERT_M2_TO_HA;
                                double km2 = c.Area * PRZC.c_CONVERT_M2_TO_KM2;

                                rowBuffer[ixShape] = c;
                                rowBuffer[ixAcres] = ac;
                                rowBuffer[ixHectares] = ha;
                                rowBuffer[ixKm2] = km2;

                                insertCursor.Insert(rowBuffer);
                                insertCursor.Flush();
                            }

                            return true;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }
                }))
                {
                    // it did not work
                    UpdateProgress(PRZH.WriteLog("Error loading polygon feature into multi-part Buffered Study Area Feature Class.", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    // it did work, yay.
                    UpdateProgress(PRZH.WriteLog("Loaded polygon feature into multi-part Buffered Study Area Feature Class"), true, ++val);
                }

                #endregion

                // Compact the Geodatabase
                UpdateProgress(PRZH.WriteLog("Compacting the Geodatabase..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath);
                toolOutput = await PRZH.RunGPTool("Compact_management", toolParams, null, GPExecuteToolFlags.None);
                if (toolOutput == null)
                {
                    UpdateProgress(PRZH.WriteLog("Error compacting the geodatabase. GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Refresh the Map & TOC
                if (!await PRZM.ValidatePRZGroupLayers())
                {
                    UpdateProgress(PRZH.WriteLog("Error validating PRZ layers...", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Wrap things up
                stopwatch.Stop();
                string message = PRZH.GetElapsedTimeMessage(stopwatch.Elapsed);
                UpdateProgress(PRZH.WriteLog("Construction completed successfully!"), true, 1, 1);
                UpdateProgress(PRZH.WriteLog(message), true, 1, 1);

                ProMsgBox.Show("Construction Completed Sucessfully!" + Environment.NewLine + Environment.NewLine + message);

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                UpdateProgress(PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                return false;
            }
            finally
            {
                if (cps != null)
                    cps.Dispose();
            }
        }

        internal async Task<bool> RemovePRZLayersAndTables()
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

        internal async Task<bool> LoadTiles(PlanningUnitTileInfo planningUnitTileInfo)
        {
            try
            {
                await QueuedTask.Run(async () => 
                {
                    using (Geodatabase gdb = await PRZH.GetProjectGDB())
                    using (FeatureClass puFC = gdb.OpenDataset<FeatureClass>(PRZC.c_FC_PLANNING_UNITS))
                    {
                        RowBuffer rowBuffer = null;
                        InsertCursor insertCursor = null;

                        try
                        {
                            // Get the Definition
                            FeatureClassDefinition fcDef = puFC.GetDefinition();

                            // Field Indexes
                            int ixShape = fcDef.FindField(fcDef.GetShapeField());
                            int ixPUID = fcDef.FindField(PRZC.c_FLD_PUFC_ID);
                            int ixStatus = fcDef.FindField(PRZC.c_FLD_PUFC_STATUS);
                            int ixCost = fcDef.FindField(PRZC.c_FLD_PUFC_COST);

                            //int puid = 1;

                            insertCursor = puFC.CreateInsertCursor();
                            rowBuffer = puFC.CreateRowBuffer();

                            switch (planningUnitTileInfo.tile_shape)
                            {
                                case PlanningUnitTileShape.SQUARE:
                                    for(int row = 0; row < planningUnitTileInfo.tiles_up; row++)
                                    {
                                        for (int col = 0; col < planningUnitTileInfo.tiles_across; col++)
                                        {
                                            double CurrentX = planningUnitTileInfo.LL_Point.X + (col * planningUnitTileInfo.tile_edge_length);
                                            double CurrentY = planningUnitTileInfo.LL_Point.Y + (row * planningUnitTileInfo.tile_edge_length);
                                            Polygon poly = await BuildTileSquare(CurrentX, CurrentY, planningUnitTileInfo.tile_edge_length, planningUnitTileInfo.LL_Point.SpatialReference);

                                            rowBuffer[ixStatus] = 0;
                                            rowBuffer[ixCost] = 1;
                                            rowBuffer[ixShape] = poly;
                                            //rowBuffer[ixPUID] = puid;

                                            insertCursor.Insert(rowBuffer);
                                            insertCursor.Flush();               // may not be necessary, or only if there are lots of tiles being written?

                                            //puid++;
                                        }
                                    }
                                    break;

                                case PlanningUnitTileShape.HEXAGON:
                                    double hex_horizoffset = planningUnitTileInfo.tile_center_to_right + (planningUnitTileInfo.tile_edge_length / 2.0);

                                    for (int col = 0; col < planningUnitTileInfo.tiles_across; col++)
                                    {
                                        for (int row = 0;  row < planningUnitTileInfo.tiles_up; row++)
                                        {
                                            double CurrentX = planningUnitTileInfo.LL_Point.X + (col * hex_horizoffset);
                                            double CurrentY = planningUnitTileInfo.LL_Point.Y + (row * (2 * planningUnitTileInfo.tile_center_to_top)) - ((col % 2) * planningUnitTileInfo.tile_center_to_top);

                                            Polygon poly = await BuildTileHexagon(CurrentX, CurrentY, planningUnitTileInfo.tile_center_to_right, planningUnitTileInfo.tile_center_to_top, planningUnitTileInfo.LL_Point.SpatialReference);

                                            rowBuffer[ixStatus] = 0;
                                            rowBuffer[ixCost] = 1;
                                            rowBuffer[ixShape] = poly;
                                            //rowBuffer[ixPUID] = puid;

                                            insertCursor.Insert(rowBuffer);
                                            insertCursor.Flush();               // may not be necessary, or only if there are lots of tiles being written?

                                            //puid++;
                                        }
                                    }
                                    break;

                                default:
                                    return;
                            }
                        }
                        catch (Exception ex)
                        {
                            ProMsgBox.Show(ex.Message);
                            return;
                        }
                        finally
                        {
                            if (rowBuffer != null)
                                rowBuffer.Dispose();
                            if (insertCursor != null)
                                insertCursor.Dispose();
                        }
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                //ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
            finally
            {
            }
        }

        internal async Task<bool> StripTiles(Polygon study_area)
        {
            try
            {
                await QueuedTask.Run(async () =>
                {
                    using (Geodatabase gdb = await PRZH.GetProjectGDB())
                    using (FeatureClass puFC = gdb.OpenDataset<FeatureClass>(PRZC.c_FC_PLANNING_UNITS))
                    {
                        // Build the spatial filter
                        SpatialQueryFilter sqFilter = new SpatialQueryFilter();
                        sqFilter.WhereClause = "";
                        sqFilter.OutputSpatialReference = study_area.SpatialReference;
                        sqFilter.FilterGeometry = study_area;
                        sqFilter.SpatialRelationship = SpatialRelationship.Intersects;
                        sqFilter.SubFields = "*";

                        // identify all the features I want to keep
                        List<long> oids = new List<long>();
                        using (RowCursor rowCursor = puFC.Search(sqFilter, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                // these are the rows I want to keep
                                using (Feature feature = (Feature)rowCursor.Current)
                                {
                                    oids.Add(feature.GetObjectID());
                                }
                            }
                        }

                        // delete unwanted tiles
                        using (RowCursor rowCursor = puFC.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    long oid = row.GetObjectID();

                                    if (!oids.Contains(oid))
                                    {
                                        row.Delete();
                                    }
                                }
                            }
                        }

                        // update remaining tile PUID values
                        using (RowCursor rowCursor = puFC.Search(null, false))
                        {
                            int id = 1;
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    row[PRZC.c_FLD_PUFC_ID] = id;
                                    row.Store();
                                    id++;
                                }
                            }
                        }
                    }
                });


                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
            finally
            {
            }
        }

        internal async Task<Polygon> BuildTileSquare(double xmin, double ymin, double side_length, SpatialReference SR)
        {
            try
            {
                Polygon polygon = null;

                await QueuedTask.Run(() =>
                {
                    MapPoint ll = MapPointBuilder.CreateMapPoint(xmin, ymin, SR);
                    MapPoint ur = MapPointBuilder.CreateMapPoint(xmin + side_length, ymin + side_length, SR);
                    Envelope env = EnvelopeBuilder.CreateEnvelope(ll, ur, SR);
                    polygon = PolygonBuilder.CreatePolygon(env, SR);
                });

                return polygon;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        internal async Task<Polygon> BuildTileHexagon(double xmin, double ymin, double center_to_vertex, double center_to_edge, SpatialReference SR)
        {
            try
            {
                Polygon polygon = null;

                await QueuedTask.Run(() =>
                {
                    MapPoint mp1 = MapPointBuilder.CreateMapPoint(xmin - center_to_vertex, ymin, SR);
                    MapPoint mp2 = MapPointBuilder.CreateMapPoint(xmin - (center_to_vertex/2.0), ymin + center_to_edge, SR);
                    MapPoint mp3 = MapPointBuilder.CreateMapPoint(xmin + (center_to_vertex/2.0), ymin + center_to_edge, SR);
                    MapPoint mp4 = MapPointBuilder.CreateMapPoint(xmin + center_to_vertex, ymin, SR);
                    MapPoint mp5 = MapPointBuilder.CreateMapPoint(xmin + (center_to_vertex/2.0), ymin - center_to_edge, SR);
                    MapPoint mp6 = MapPointBuilder.CreateMapPoint(xmin - (center_to_vertex/2.0), ymin - center_to_edge, SR);

                    List<MapPoint> mps = new List<MapPoint>() { mp1, mp2, mp3, mp4, mp5, mp6 };
                    polygon = PolygonBuilder.CreatePolygon(mps, SR);
                });

                return polygon;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
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
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

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
                if (SRMapIsChecked)
                {
                    return _mapSR;
                }
                else if (SRLayerIsChecked)
                {
                    if (SelectedLayerSR != null)
                    {
                        return this.SelectedLayerSR;
                    }
                }
                else if (SRUserIsChecked)
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

