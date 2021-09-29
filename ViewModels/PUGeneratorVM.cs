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
    public class PUGeneratorVM : PropertyChangedBase
    {
        public PUGeneratorVM()
        {
        }

        #region FIELDS

        private SpatialReference _outputSR;
        private SpatialReference _mapSR;
        private SpatialReference _userSR;

        private bool _srMapIsChecked;
        private bool _srLayerIsChecked;
        private bool _srUserIsChecked;
        private string _mapSRName;
        private string _userSRName;
        private List<SpatialReference> _layerSRList;
        private bool _srMapIsEnabled;
        private bool _srLayerIsEnabled;
        private bool _srUserIsEnabled;
        private SpatialReference _selectedLayerSR;
        private List<GraphicsLayer> _graphicsLayerList;
        private GraphicsLayer _selectedGraphicsLayer;
        private List<FeatureLayer> _featureLayerList;
        private FeatureLayer _selectedFeatureLayer;
        private string _bufferValue;
        private string _gridAlign_X;
        private string _gridAlign_Y;
        private bool _bufferUnitMetersIsChecked;
        private bool _bufferUnitKilometersIsChecked;
        private List<string> _gridTypeList;
        private string _selectedGridType;
        private string _tileArea;
        private bool _tileAreaMIsSelected;
        private bool _tileAreaAcIsSelected;
        private bool _tileAreaHaIsSelected;
        private bool _tileAreaKmIsSelected;
        private bool _buildIsEnabled;
        private bool _graphicsLayerIsEnabled;
        private bool _graphicsLayerIsChecked;
        private bool _featureLayerIsEnabled;
        private bool _featureLayerIsChecked;
        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""


        #endregion

        #region PROPERTIES

        public bool SRMapIsChecked
        {
            get => _srMapIsChecked; set => SetProperty(ref _srMapIsChecked, value, () => SRMapIsChecked);
        }

        public bool SRLayerIsChecked
        {
            get => _srLayerIsChecked; set => SetProperty(ref _srLayerIsChecked, value, () => SRLayerIsChecked);
        }

        public bool SRUserIsChecked
        {
            get => _srUserIsChecked; set => SetProperty(ref _srUserIsChecked, value, () => SRUserIsChecked);
        }

        public string MapSRName
        {
            get => _mapSRName; set => SetProperty(ref _mapSRName, value, () => MapSRName);
        }

        public string UserSRName
        {
            get => _userSRName; set => SetProperty(ref _userSRName, value, () => UserSRName);
        }

        public List<SpatialReference> LayerSRList
        {
            get => _layerSRList; set => SetProperty(ref _layerSRList, value, () => LayerSRList);
        }

        public bool SRMapIsEnabled
        {
            get => _srMapIsEnabled; set => SetProperty(ref _srMapIsEnabled, value, () => SRMapIsEnabled);
        }

        public bool SRLayerIsEnabled
        {
            get => _srLayerIsEnabled; set => SetProperty(ref _srLayerIsEnabled, value, () => SRLayerIsEnabled);
        }

        public bool SRUserIsEnabled
        {
            get => _srUserIsEnabled; set => SetProperty(ref _srUserIsEnabled, value, () => SRUserIsEnabled);
        }

        public SpatialReference SelectedLayerSR
        {
            get => _selectedLayerSR; set => SetProperty(ref _selectedLayerSR, value, () => SelectedLayerSR);
        }

        public List<GraphicsLayer> GraphicsLayerList
        {
            get => _graphicsLayerList; set => SetProperty(ref _graphicsLayerList, value, () => GraphicsLayerList);
        }

        public GraphicsLayer SelectedGraphicsLayer
        {
            get => _selectedGraphicsLayer; set => SetProperty(ref _selectedGraphicsLayer, value, () => SelectedGraphicsLayer);
        }

        public List<FeatureLayer> FeatureLayerList
        {
            get => _featureLayerList; set => SetProperty(ref _featureLayerList, value, () => FeatureLayerList);
        }

        public FeatureLayer SelectedFeatureLayer
        {
            get => _selectedFeatureLayer; set => SetProperty(ref _selectedFeatureLayer, value, () => SelectedFeatureLayer);
        }

        public string BufferValue
        {
            get => _bufferValue; set => SetProperty(ref _bufferValue, value, () => BufferValue);
        }

        public string GridAlign_X
        {
            get => _gridAlign_X; set => SetProperty(ref _gridAlign_X, value, () => GridAlign_X);
        }

        public string GridAlign_Y
        {
            get => _gridAlign_Y; set => SetProperty(ref _gridAlign_Y, value, () => GridAlign_Y);
        }

        public bool BufferUnitMetersIsChecked
        {
            get => _bufferUnitMetersIsChecked; set => SetProperty(ref _bufferUnitMetersIsChecked, value, () => BufferUnitMetersIsChecked);
        }

        public bool BufferUnitKilometersIsChecked
        {
            get => _bufferUnitKilometersIsChecked; set => SetProperty(ref _bufferUnitKilometersIsChecked, value, () => BufferUnitKilometersIsChecked);
        }

        public List<string> GridTypeList
        {
            get => _gridTypeList; set => SetProperty(ref _gridTypeList, value, () => GridTypeList);
        }

        public string SelectedGridType
        {
            get => _selectedGridType; set => SetProperty(ref _selectedGridType, value, () => SelectedGridType);
        }

        public string TileArea
        {
            get => _tileArea; set => SetProperty(ref _tileArea, value, () => TileArea);
        }

        public bool TileAreaMIsSelected
        {
            get => _tileAreaMIsSelected; set => SetProperty(ref _tileAreaMIsSelected, value, () => TileAreaMIsSelected);
        }

        public bool TileAreaAcIsSelected
        {
            get => _tileAreaAcIsSelected; set => SetProperty(ref _tileAreaAcIsSelected, value, () => TileAreaAcIsSelected);
        }

        public bool TileAreaHaIsSelected
        {
            get => _tileAreaHaIsSelected; set => SetProperty(ref _tileAreaHaIsSelected, value, () => TileAreaHaIsSelected);
        }

        public bool TileAreaKmIsSelected
        {
            get => _tileAreaKmIsSelected; set => SetProperty(ref _tileAreaKmIsSelected, value, () => TileAreaKmIsSelected);
        }

        public bool BuildIsEnabled
        {
            get => _buildIsEnabled; set => SetProperty(ref _buildIsEnabled, value, () => BuildIsEnabled);
        }

        public bool GraphicsLayerIsEnabled
        {
            get => _graphicsLayerIsEnabled; set => SetProperty(ref _graphicsLayerIsEnabled, value, () => GraphicsLayerIsEnabled);
        }

        public bool GraphicsLayerIsChecked
        {
            get => _graphicsLayerIsChecked; set => SetProperty(ref _graphicsLayerIsChecked, value, () => GraphicsLayerIsChecked);
        }

        public bool FeatureLayerIsEnabled
        {
            get => _featureLayerIsEnabled; set => SetProperty(ref _featureLayerIsEnabled, value, () => FeatureLayerIsEnabled);
        }

        public bool FeatureLayerIsChecked
        {
            get => _featureLayerIsChecked; set => SetProperty(ref _featureLayerIsChecked, value, () => FeatureLayerIsChecked);
        }

        public ProgressManager PM
        {
            get => _pm; set => SetProperty(ref _pm, value, () => PM);
        }


        #endregion

        #region COMMANDS

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

        private ICommand _cmdClearLog;
        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true));

        #endregion

        #region METHODS

        public async Task OnProWinLoaded()
        {
            try
            {
                // Initialize the Progress Bar & Log
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

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

                var SRList = new List<SpatialReference>();

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

                #endregion

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
                            CIMGraphic g = elem.GetGraphic();
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

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        internal async Task<bool> BuildPlanningUnits()
        {
            int val = 0;

            try
            {
                #region INITIALIZATION AND USER INPUT VALIDATION

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Initialize ProgressBar and Progress Log
                int max = 50;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Planning Unit Generator..."), false, max, ++val);

                // Validation: Project Geodatabase
                string gdbpath = PRZH.GetPath_ProjectGDB();
                if (!await PRZH.ProjectGDBExists())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Project Geodatabase not found: " + gdbpath, LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Project Geodatabase not found at this path:" + 
                                   Environment.NewLine +
                                   gdbpath + 
                                   Environment.NewLine + Environment.NewLine + 
                                   "Please specify a valid Project Workspace.", "Validation");

                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Project Geodatabase is OK: " + gdbpath), true, ++val);
                }

                // Validation: Output Spatial Reference
                var OutputSR = GetOutputSR();
                if (OutputSR == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Unspecified or invalid output Spatial Reference", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Output Spatial Reference", "Validation");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Output Spatial Reference is OK: " + OutputSR.Name), true, ++val);
                }

                // Validation: Study Area Source Geometry
                if (GraphicsLayerIsChecked)
                {
                    if (SelectedGraphicsLayer == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> No Graphics Layer was selected.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("You must specify a Graphics Layer", "Validation");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Graphics Layer Name: " + SelectedGraphicsLayer.Name), true, ++val);
                    }
                }
                else if (FeatureLayerIsChecked)
                {
                    if (SelectedFeatureLayer == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> No Feature Layer was selected.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("You must specify a Feature Layer", "Validation");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Feature Layer Name: " + SelectedFeatureLayer.Name), true, ++val);
                    }
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> No Graphics Layer or Feature Layer was selected.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("You must select either a Graphics Layer or a Feature Layer", "Validation");
                    return false;
                }

                // Validation: Buffer Distance
                string buffer_dist_text = string.IsNullOrEmpty(BufferValue) ? "0" : ((BufferValue.Trim() == "") ? "0" : BufferValue.Trim());

                if (!double.TryParse(buffer_dist_text, out double buffer_dist))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Invalid buffer distance specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Invalid Buffer Distance specified.  The value must be numeric and >= 0, or blank.", "Validation");
                    return false;
                }
                else if (buffer_dist < 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Invalid buffer distance specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
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

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Buffer Distance = " + bu), true, ++val);

                // Validation: Tile Shape
                string tile_shape = (string.IsNullOrEmpty(SelectedGridType) ? "" : SelectedGridType);
                if (tile_shape == "")
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Tile shape not specified", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a tile shape", "Validation");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Tile Shape is " + tile_shape), true, ++val);
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Tile Area", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Tile Area.  Value must be numeric and greater than 0", "Validation");
                    return false;
                }
                else if (tile_area_m2 <= 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Tile Area", LogMessageType.VALIDATION_ERROR), true, ++val);
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

                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Tile Area = " + au), true, ++val);
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Invalid tile grid alignment coordinates.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Invalid Tile Grid Alignment Coordinates.  Both X and Y values must be numeric, or both must be blank.", "Validation");
                    return false;
                }
                else if (align_grid)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Tile grid alignment coordinates are OK: (" + gridalign_x_text + ", " + gridalign_y_text + ")"), true, ++val);
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> No tile grid alignment was specified, and that's OK."), true, ++val);
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out."), true, ++val);
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

                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Study Area >> Retrieved " + polyelems.ToString() + " selected polygon(s) from Graphics Layer: " + gl.Name), true, ++val);
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

                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Study Area >> Retrieved " + selpol.ToString() + " selected polygon(s) from Layer: " + fl.Name), true, ++val);
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

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Study Area >> Buffered by " + buffer_dist_m.ToString() + ((buffer_dist_m == 1) ? " meter" : " meters")), true, ++val);

                #endregion

                #region STRIP MAP AND GDB

                // Remove any PRZ GDB Layers or Tables from Active Map
                var map = MapView.Active.Map;
                var flyrs = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                List<Layer> LayersToDelete = new List<Layer>();

                if (!await RemovePRZLayersAndTables())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to remove all Layers and Standalone Tables with source = " + gdbpath, LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Removed all Layers and Standalone Tables with source = " + gdbpath), true, ++val);
                }

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                string toolOutput;

                // Delete all Items from Project GDB
                if (!await PRZH.ClearProjectGDB())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to delete Feature Datasets, Feature Classes, and Tables from " + gdbpath, LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleted all Feature Datasets, Feature Classes, and Tables from " + gdbpath), true, ++val);
                }

                #endregion

                #region CREATE PLANNING UNIT FC

                string pufcpath = PRZH.GetPath_FC_PU();

                // Build the new empty Planning Unit FC
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating Planning Unit Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_PLANNING_UNITS, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error creating Planning Unit FC.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Add Fields to Planning Unit FC
                string fldPUID = PRZC.c_FLD_FC_PU_ID + " LONG 'Planning Unit ID' # # #;";
                string fldNCCID = PRZC.c_FLD_FC_PU_NCC_ID + " LONG 'NCC ID' # # #;";
                string fldPUEffectiveRule = PRZC.c_FLD_FC_PU_EFFECTIVE_RULE + " TEXT 'Effective Rule' 50 # #;";
                string fldConflict = PRZC.c_FLD_FC_PU_CONFLICT + " LONG 'Rule Conflict' # 0 #;";
                string fldPUCost = PRZC.c_FLD_FC_PU_COST + " DOUBLE 'Cost' # 1 #;";
                string fldPUAreaM = PRZC.c_FLD_FC_PU_AREA_M2 + " DOUBLE 'Square m' # 1 #;";
                string fldPUAreaAC = PRZC.c_FLD_FC_PU_AREA_AC + " DOUBLE 'Acres' # 1 #;";
                string fldPUAreaHA = PRZC.c_FLD_FC_PU_AREA_HA + " DOUBLE 'Hectares' # 1 #;";
                string fldPUAreaKM = PRZC.c_FLD_FC_PU_AREA_KM2 + " DOUBLE 'Square km' # 1 #;";
                string fldCFCount = PRZC.c_FLD_FC_PU_FEATURECOUNT + " LONG 'Conservation Feature Count' # 0 #;";
                string fldSharedPerim = PRZC.c_FLD_FC_PU_SHARED_PERIM + " DOUBLE 'Shared Perimeter (m)' # 0 #;";
                string fldUnsharedPerim = PRZC.c_FLD_FC_PU_UNSHARED_PERIM + " DOUBLE 'Unshared Perimeter (m)' # 0 #;";

                string fldHasUnsharedPerim = PRZC.c_FLD_FC_PU_HAS_UNSHARED_PERIM + " LONG 'Has Unshared Perimeter' # 0 #;";

                string flds = fldPUID + fldNCCID + fldPUEffectiveRule + fldConflict + fldPUCost + fldPUAreaM + fldPUAreaAC + fldPUAreaHA + fldPUAreaKM + fldCFCount + fldSharedPerim + fldUnsharedPerim + fldHasUnsharedPerim;

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Adding fields to Planning Unit FC..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pufcpath, flds);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, null, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding fields to Planning Unit FC.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully."), true, ++val);
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

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Loading Tiles into Planning Unit Feature Class..."), true, ++val);
                if (!await LoadTiles(tileinfo))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Tile Load failed...", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Retain only those tiles overlapping the buffered study area polygon
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting Tiles outside of buffered study area..."), true, ++val);
                if (!await StripTiles(SA_poly_buffer))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Tile Deletion failed...", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Index the PU ID field
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Indexing fields in the Planning Unit Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_PLANNING_UNITS, PRZC.c_FLD_FC_PU_ID, "ix" + PRZC.c_FLD_FC_PU_ID, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, GPExecuteToolFlags.None);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                #endregion

                #region CREATE STUDY AREA FC

                string safcpath = PRZH.GetPath_FC_StudyArea();

                // Build the new empty Main Study Area FC
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating Main Study Area Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_STUDY_AREA_MAIN, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error creating main Study Area Feature Class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Add Fields to Main Study Area FC
                string fldArea_ac = PRZC.c_FLD_FC_STUDYAREA_AREA_AC + " DOUBLE 'Acres' # 1 #;";
                string fldArea_ha = PRZC.c_FLD_FC_STUDYAREA_AREA_HA + " DOUBLE 'Hectares' # 1 #;";
                string fldArea_km = PRZC.c_FLD_FC_STUDYAREA_AREA_KM2 + " DOUBLE 'Square km' # 1 #;";
                string SAflds = fldArea_ac + fldArea_ha + fldArea_km;

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Adding fields to Main Study Area Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(safcpath, SAflds);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, null, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding fields to main Study Area FC.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
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
                            int ixAcres = fcDef.FindField(PRZC.c_FLD_FC_STUDYAREA_AREA_AC);
                            int ixHectares = fcDef.FindField(PRZC.c_FLD_FC_STUDYAREA_AREA_HA);
                            int ixKm2 = fcDef.FindField(PRZC.c_FLD_FC_STUDYAREA_AREA_KM2);

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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error loading polygon feature into Study Area Feature Class.", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    // it did work, yay.
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Loaded polygon feature into Study Area Feature Class"), true, ++val);
                }


                #endregion

                #region CREATE BUFFERED STUDY AREA FC

                string sabufffcpath = PRZH.GetPath_FC_StudyAreaBuffer();

                // Build the new empty Main Study Area FC
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating Buffered Study Area Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_STUDY_AREA_MAIN_BUFFERED, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error creating Buffered Study Area Feature Class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Add Fields to Buffered Study Area FC
                string fldBArea_ac = PRZC.c_FLD_FC_STUDYAREA_AREA_AC + " DOUBLE 'Acres' # 1 #;";
                string fldBArea_ha = PRZC.c_FLD_FC_STUDYAREA_AREA_HA + " DOUBLE 'Hectares' # 1 #;";
                string fldBArea_km = PRZC.c_FLD_FC_STUDYAREA_AREA_KM2 + " DOUBLE 'Square km' # 1 #;";
                string SABflds = fldBArea_ac + fldBArea_ha + fldBArea_km;

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Adding fields to Buffered Study Area Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(sabufffcpath, SABflds);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, null, GPExecuteToolFlags.RefreshProjectItems);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding fields to Buffered Study Area FC.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
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
                            int ixAcres = fcDef.FindField(PRZC.c_FLD_FC_STUDYAREA_AREA_AC);
                            int ixHectares = fcDef.FindField(PRZC.c_FLD_FC_STUDYAREA_AREA_HA);
                            int ixKm2 = fcDef.FindField(PRZC.c_FLD_FC_STUDYAREA_AREA_KM2);

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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error loading polygon feature into Buffered Study Area Feature Class.", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    // it did work, yay.
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Loaded polygon feature into Buffered Study Area Feature Class"), true, ++val);
                }

                #endregion

                // Compact the Geodatabase
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacting the Geodatabase..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath);
                toolOutput = await PRZH.RunGPTool("Compact_management", toolParams, null, GPExecuteToolFlags.None);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error compacting the geodatabase. GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Refresh the Map & TOC
                if (!await PRZM.ValidatePRZGroupLayers())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error validating PRZ layers...", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Wrap things up
                stopwatch.Stop();
                string message = PRZH.GetElapsedTimeMessage(stopwatch.Elapsed);
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Construction completed successfully!"), true, 1, 1);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(message), true, 1, 1);

                ProMsgBox.Show("Construction Completed Sucessfully!" + Environment.NewLine + Environment.NewLine + message);

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                return false;
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
                    using (FeatureClass puFC = await PRZH.GetFC_PU())
                    using (RowBuffer rowBuffer = puFC.CreateRowBuffer())
                    using (InsertCursor insertCursor = puFC.CreateInsertCursor())
                    using (FeatureClassDefinition fcDef = puFC.GetDefinition())
                    {
                        try
                        {
                            // Set Shape-related values
                            switch (planningUnitTileInfo.tile_shape)
                            {
                                case PlanningUnitTileShape.SQUARE:
                                    for(int row = 0; row < planningUnitTileInfo.tiles_up; row++)
                                    {
                                        for (int col = 0; col < planningUnitTileInfo.tiles_across; col++)
                                        {
                                            // Get shape
                                            double CurrentX = planningUnitTileInfo.LL_Point.X + (col * planningUnitTileInfo.tile_edge_length);
                                            double CurrentY = planningUnitTileInfo.LL_Point.Y + (row * planningUnitTileInfo.tile_edge_length);
                                            Polygon poly = await BuildTileSquare(CurrentX, CurrentY, planningUnitTileInfo.tile_edge_length, planningUnitTileInfo.LL_Point.SpatialReference);

                                            // Prepare area values
                                            double m = poly.Area;
                                            double ac = m * PRZC.c_CONVERT_M2_TO_AC;
                                            double ha = m * PRZC.c_CONVERT_M2_TO_HA;
                                            double km = m * PRZC.c_CONVERT_M2_TO_KM2;

                                            // set shape-related values
                                            rowBuffer[fcDef.GetShapeField()] = poly;
                                            rowBuffer[PRZC.c_FLD_FC_PU_AREA_M2] = m;
                                            rowBuffer[PRZC.c_FLD_FC_PU_AREA_AC] = ac;
                                            rowBuffer[PRZC.c_FLD_FC_PU_AREA_HA] = ha;
                                            rowBuffer[PRZC.c_FLD_FC_PU_AREA_KM2] = km;

                                            // Set common values
                                            rowBuffer[PRZC.c_FLD_FC_PU_CONFLICT] = 0;
                                            rowBuffer[PRZC.c_FLD_FC_PU_COST] = 1;
                                            rowBuffer[PRZC.c_FLD_FC_PU_SHARED_PERIM] = 0;
                                            rowBuffer[PRZC.c_FLD_FC_PU_HAS_UNSHARED_PERIM] = 0;

                                            // Finally, insert the row
                                            insertCursor.Insert(rowBuffer);
                                            insertCursor.Flush();
                                        }
                                    }
                                    break;

                                case PlanningUnitTileShape.HEXAGON:
                                    double hex_horizoffset = planningUnitTileInfo.tile_center_to_right + (planningUnitTileInfo.tile_edge_length / 2.0);

                                    for (int col = 0; col < planningUnitTileInfo.tiles_across; col++)
                                    {
                                        for (int row = 0;  row < planningUnitTileInfo.tiles_up; row++)
                                        {
                                            // Get shape
                                            double CurrentX = planningUnitTileInfo.LL_Point.X + (col * hex_horizoffset);
                                            double CurrentY = planningUnitTileInfo.LL_Point.Y + (row * (2 * planningUnitTileInfo.tile_center_to_top)) - ((col % 2) * planningUnitTileInfo.tile_center_to_top);
                                            Polygon poly = await BuildTileHexagon(CurrentX, CurrentY, planningUnitTileInfo.tile_center_to_right, planningUnitTileInfo.tile_center_to_top, planningUnitTileInfo.LL_Point.SpatialReference);


                                            // Prepare area values
                                            double m = poly.Area;
                                            double ac = m * PRZC.c_CONVERT_M2_TO_AC;
                                            double ha = m * PRZC.c_CONVERT_M2_TO_HA;
                                            double km = m * PRZC.c_CONVERT_M2_TO_KM2;

                                            // set shape-related values
                                            rowBuffer[fcDef.GetShapeField()] = poly;
                                            rowBuffer[PRZC.c_FLD_FC_PU_AREA_M2] = m;
                                            rowBuffer[PRZC.c_FLD_FC_PU_AREA_AC] = ac;
                                            rowBuffer[PRZC.c_FLD_FC_PU_AREA_HA] = ha;
                                            rowBuffer[PRZC.c_FLD_FC_PU_AREA_KM2] = km;

                                            // Set common values
                                            rowBuffer[PRZC.c_FLD_FC_PU_CONFLICT] = 0;
                                            rowBuffer[PRZC.c_FLD_FC_PU_COST] = 1;
                                            rowBuffer[PRZC.c_FLD_FC_PU_SHARED_PERIM] = 0;
                                            rowBuffer[PRZC.c_FLD_FC_PU_HAS_UNSHARED_PERIM] = 0;

                                            // Finally, insert the row
                                            insertCursor.Insert(rowBuffer);
                                            insertCursor.Flush();
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
                                    row[PRZC.c_FLD_FC_PU_ID] = id;
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

        #endregion


    }
}

