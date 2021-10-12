using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
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
using System.Windows;
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

        #region FIELDS

        private Map _map = MapView.Active.Map;
        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""

        private ICommand _cmdSelectSpatialReference;
        private ICommand _cmdGeneratePlanningUnits;
        private ICommand _cmdClearLog;
        private ICommand _cmdTest;

        #region PLANNING UNIT SOURCE GEOMETRY

        private bool _puSource_Chk_UseRasterFormat;

        private bool _puSource_Rad_NatGrid_IsChecked;
        private bool _puSource_Rad_CustomGrid_IsChecked;
        private bool _puSource_Rad_Layer_IsChecked;

        private List<string> _puSource_Cmb_CustomGrid_TileShapes;
        private string _puSource_Cmb_CustomGrid_SelectedTileShape;
        private string _puSource_Txt_CustomGrid_TileArea;
        private bool _puSource_Rad_TileArea_M_IsChecked;
        private bool _puSource_Rad_TileArea_Ac_IsChecked;
        private bool _puSource_Rad_TileArea_Ha_IsChecked;
        private bool _puSource_Rad_TileArea_Km_IsChecked;

        private bool _puSource_Rad_NatGrid_1M_IsChecked;
        private bool _puSource_Rad_NatGrid_10M_IsChecked;
        private bool _puSource_Rad_NatGrid_100M_IsChecked;
        private bool _puSource_Rad_NatGrid_1Km_IsChecked;
        private bool _puSource_Rad_NatGrid_10Km_IsChecked;
        private bool _puSource_Rad_NatGrid_100Km_IsChecked;

        private Visibility _puSource_Vis_NatGrid_Controls = Visibility.Collapsed;
        private Visibility _puSource_Vis_CustomGrid_Controls = Visibility.Collapsed;
        private Visibility _puSource_Vis_Layer_Controls = Visibility.Collapsed;

        private List<FeatureLayer> _puSource_Cmb_Layer_FeatureLayers;
        private FeatureLayer _puSource_Cmb_Layer_SelectedFeatureLayer;

        #endregion

        #region STUDY AREA SOURCE GEOMETRY

        private bool _saSource_Rad_Graphic_IsChecked;
        private bool _saSource_Rad_Layer_IsChecked;

        private Visibility _saSource_Vis_Graphic_Controls = Visibility.Collapsed;
        private Visibility _saSource_Vis_Layer_Controls = Visibility.Collapsed;

        private string _saSource_Txt_BufferDistance;
        private bool _saSource_Rad_BufferDistance_M_IsChecked;
        private bool _saSource_Rad_BufferDistance_Km_IsChecked;

        private List<GraphicsLayer> _saSource_Cmb_Graphic_GraphicsLayers;
        private GraphicsLayer _saSource_Cmb_Graphic_SelectedGraphicsLayer;

        private List<FeatureLayer> _saSource_Cmb_Layer_FeatureLayers;
        private FeatureLayer _saSource_Cmb_Layer_SelectedFeatureLayer;


        #endregion

        #region OUTPUT SPATIAL REFERENCE

        private Visibility _outputSR_Vis_Border = Visibility.Visible;
        private Visibility _outputSR_Vis_Map_Controls = Visibility.Collapsed;
        private Visibility _outputSR_Vis_Layer_Controls = Visibility.Collapsed;
        private Visibility _outputSR_Vis_User_Controls = Visibility.Collapsed;
        private bool _outputSR_Rad_Map_IsEnabled;
        private bool _outputSR_Rad_Map_IsChecked;
        private bool _outputSR_Rad_Layer_IsChecked;
        private bool _outputSR_Rad_User_IsChecked;
        private string _outputSR_Txt_Map_SRName;
        private string _outputSR_Txt_User_SRName;
        private List<SpatialReference> _outputSR_Cmb_Layer_SpatialReferences;
        private SpatialReference _outputSR_Cmb_Layer_SelectedSpatialReference;

        #endregion

        #endregion

        #region PROPERTIES

        #region PLANNING UNIT SOURCE GEOMETRY

        public bool PUSource_Chk_UseRasterFormat
        {
            get => _puSource_Chk_UseRasterFormat;
            set
            {
                SetProperty(ref _puSource_Chk_UseRasterFormat, value, () => PUSource_Chk_UseRasterFormat);
                Properties.Settings.Default.DEFAULT_PU_USE_RASTER_FORMAT = value;
                Properties.Settings.Default.Save();
            }
        }

        public bool PUSource_Rad_NatGrid_IsChecked
        {
            get => _puSource_Rad_NatGrid_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_NatGrid_IsChecked, value, () => PUSource_Rad_NatGrid_IsChecked);
                OutputSR_Vis_Border = value ? Visibility.Collapsed : Visibility.Visible;
                PUSource_Vis_NatGrid_Controls = value ? Visibility.Visible : Visibility.Collapsed;
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_PU_GEOMETRY_SOURCE = "NATGRID";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool PUSource_Rad_CustomGrid_IsChecked
        {
            get => _puSource_Rad_CustomGrid_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_CustomGrid_IsChecked, value, () => PUSource_Rad_CustomGrid_IsChecked);
                PUSource_Vis_CustomGrid_Controls = value ? Visibility.Visible : Visibility.Collapsed;
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_PU_GEOMETRY_SOURCE = "CUSTOMGRID";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool PUSource_Rad_Layer_IsChecked
        {
            get => _puSource_Rad_Layer_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_Layer_IsChecked, value, () => PUSource_Rad_Layer_IsChecked);
                PUSource_Vis_Layer_Controls = value ? Visibility.Visible : Visibility.Collapsed;
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_PU_GEOMETRY_SOURCE = "LAYER";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public List<string> PUSource_Cmb_CustomGrid_TileShapes
        {
            get => _puSource_Cmb_CustomGrid_TileShapes;
            set => SetProperty(ref _puSource_Cmb_CustomGrid_TileShapes, value, () => PUSource_Cmb_CustomGrid_TileShapes);
        }
        public string PUSource_Cmb_CustomGrid_SelectedTileShape
        {
            get => _puSource_Cmb_CustomGrid_SelectedTileShape;
            set
            {
                SetProperty(ref _puSource_Cmb_CustomGrid_SelectedTileShape, value, () => PUSource_Cmb_CustomGrid_SelectedTileShape);
                Properties.Settings.Default.DEFAULT_TILE_SHAPE = value;
                Properties.Settings.Default.Save();
            }
        }
        public Visibility PUSource_Vis_NatGrid_Controls
        {
            get => _puSource_Vis_NatGrid_Controls;
            set => SetProperty(ref _puSource_Vis_NatGrid_Controls, value, () => PUSource_Vis_NatGrid_Controls);
        }
        public Visibility PUSource_Vis_CustomGrid_Controls
        {
            get => _puSource_Vis_CustomGrid_Controls;
            set => SetProperty(ref _puSource_Vis_CustomGrid_Controls, value, () => PUSource_Vis_CustomGrid_Controls);
        }
        public Visibility PUSource_Vis_Layer_Controls
        {
            get => _puSource_Vis_Layer_Controls;
            set => SetProperty(ref _puSource_Vis_Layer_Controls, value, () => PUSource_Vis_Layer_Controls);
        }
        public string PUSource_Txt_CustomGrid_TileArea
        {
            get => _puSource_Txt_CustomGrid_TileArea;
            set
            {
                SetProperty(ref _puSource_Txt_CustomGrid_TileArea, value, () => PUSource_Txt_CustomGrid_TileArea);
                Properties.Settings.Default.DEFAULT_TILE_AREA = value;
                Properties.Settings.Default.Save();
            }
        }
        public bool PUSource_Rad_NatGrid_1M_IsChecked
        {
            get => _puSource_Rad_NatGrid_1M_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_NatGrid_1M_IsChecked, value, () => PUSource_Rad_NatGrid_1M_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_NATGRID_DIMENSION = "0";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool PUSource_Rad_NatGrid_10M_IsChecked
        {
            get => _puSource_Rad_NatGrid_10M_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_NatGrid_10M_IsChecked, value, () => PUSource_Rad_NatGrid_10M_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_NATGRID_DIMENSION = "1";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool PUSource_Rad_NatGrid_100M_IsChecked
        {
            get => _puSource_Rad_NatGrid_100M_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_NatGrid_100M_IsChecked, value, () => PUSource_Rad_NatGrid_100M_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_NATGRID_DIMENSION = "2";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool PUSource_Rad_NatGrid_1Km_IsChecked
        {
            get => _puSource_Rad_NatGrid_1Km_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_NatGrid_1Km_IsChecked, value, () => PUSource_Rad_NatGrid_1Km_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_NATGRID_DIMENSION = "3";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool PUSource_Rad_NatGrid_10Km_IsChecked
        {
            get => _puSource_Rad_NatGrid_10Km_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_NatGrid_10Km_IsChecked, value, () => PUSource_Rad_NatGrid_10Km_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_NATGRID_DIMENSION = "4";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool PUSource_Rad_NatGrid_100Km_IsChecked
        {
            get => _puSource_Rad_NatGrid_100Km_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_NatGrid_100Km_IsChecked, value, () => PUSource_Rad_NatGrid_100Km_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_NATGRID_DIMENSION = "5";
                    Properties.Settings.Default.Save();
                }
            }
        }

        public bool PUSource_Rad_TileArea_M_IsChecked
        {
            get => _puSource_Rad_TileArea_M_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_TileArea_M_IsChecked, value, () => PUSource_Rad_TileArea_M_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_TILE_AREA_UNITS = "M";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool PUSource_Rad_TileArea_Ac_IsChecked
        {
            get => _puSource_Rad_TileArea_Ac_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_TileArea_Ac_IsChecked, value, () => PUSource_Rad_TileArea_Ac_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_TILE_AREA_UNITS = "AC";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool PUSource_Rad_TileArea_Ha_IsChecked
        {
            get => _puSource_Rad_TileArea_Ha_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_TileArea_Ha_IsChecked, value, () => PUSource_Rad_TileArea_Ha_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_TILE_AREA_UNITS = "HA";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool PUSource_Rad_TileArea_Km_IsChecked
        {
            get => _puSource_Rad_TileArea_Km_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_TileArea_Km_IsChecked, value, () => PUSource_Rad_TileArea_Km_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_TILE_AREA_UNITS = "KM";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public List<FeatureLayer> PUSource_Cmb_Layer_FeatureLayers
        {
            get => _puSource_Cmb_Layer_FeatureLayers;
            set => SetProperty(ref _puSource_Cmb_Layer_FeatureLayers, value, () => PUSource_Cmb_Layer_FeatureLayers);
        }
        public FeatureLayer PUSource_Cmb_Layer_SelectedFeatureLayer
        {
            get => _puSource_Cmb_Layer_SelectedFeatureLayer;
            set => SetProperty(ref _puSource_Cmb_Layer_SelectedFeatureLayer, value, () => PUSource_Cmb_Layer_SelectedFeatureLayer);
        }

        #endregion

        #region STUDY AREA SOURCE GEOMETRY

        public bool SASource_Rad_Graphic_IsChecked
        {
            get => _saSource_Rad_Graphic_IsChecked;
            set
            {
                SetProperty(ref _saSource_Rad_Graphic_IsChecked, value, () => SASource_Rad_Graphic_IsChecked);
                SASource_Vis_Graphic_Controls = value ? Visibility.Visible : Visibility.Collapsed;
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_SA_GEOMETRY_SOURCE = "GRAPHIC";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool SASource_Rad_Layer_IsChecked
        {
            get => _saSource_Rad_Layer_IsChecked;
            set
            {
                SetProperty(ref _saSource_Rad_Layer_IsChecked, value, () => SASource_Rad_Layer_IsChecked);
                SASource_Vis_Layer_Controls = value ? Visibility.Visible : Visibility.Collapsed;
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_SA_GEOMETRY_SOURCE = "LAYER";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public Visibility SASource_Vis_Graphic_Controls
        {
            get => _saSource_Vis_Graphic_Controls;
            set => SetProperty(ref _saSource_Vis_Graphic_Controls, value, () => SASource_Vis_Graphic_Controls);
        }
        public Visibility SASource_Vis_Layer_Controls
        {
            get => _saSource_Vis_Layer_Controls;
            set => SetProperty(ref _saSource_Vis_Layer_Controls, value, () => SASource_Vis_Layer_Controls);
        }
        public string SASource_Txt_BufferDistance
        {
            get => _saSource_Txt_BufferDistance;
            set
            {
                SetProperty(ref _saSource_Txt_BufferDistance, value, () => SASource_Txt_BufferDistance);
                Properties.Settings.Default.DEFAULT_SA_BUFFER_DISTANCE = value;
                Properties.Settings.Default.Save();
            }
        }
        public bool SASource_Rad_BufferDistance_M_IsChecked
        {
            get => _saSource_Rad_BufferDistance_M_IsChecked;
            set
            {
                SetProperty(ref _saSource_Rad_BufferDistance_M_IsChecked, value, () => SASource_Rad_BufferDistance_M_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_SA_BUFFER_DISTANCE_UNITS = "M";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool SASource_Rad_BufferDistance_Km_IsChecked
        {
            get => _saSource_Rad_BufferDistance_Km_IsChecked;
            set
            {
                SetProperty(ref _saSource_Rad_BufferDistance_Km_IsChecked, value, () => SASource_Rad_BufferDistance_Km_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_SA_BUFFER_DISTANCE_UNITS = "KM";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public List<GraphicsLayer> SASource_Cmb_Graphic_GraphicsLayers
        {
            get => _saSource_Cmb_Graphic_GraphicsLayers;
            set => SetProperty(ref _saSource_Cmb_Graphic_GraphicsLayers, value, () => SASource_Cmb_Graphic_GraphicsLayers);
        }
        public GraphicsLayer SASource_Cmb_Graphic_SelectedGraphicsLayer
        {
            get => _saSource_Cmb_Graphic_SelectedGraphicsLayer;
            set => SetProperty(ref _saSource_Cmb_Graphic_SelectedGraphicsLayer, value, () => SASource_Cmb_Graphic_SelectedGraphicsLayer);
        }
        public List<FeatureLayer> SASource_Cmb_Layer_FeatureLayers
        {
            get => _saSource_Cmb_Layer_FeatureLayers;
            set => SetProperty(ref _saSource_Cmb_Layer_FeatureLayers, value, () => SASource_Cmb_Layer_FeatureLayers);
        }
        public FeatureLayer SASource_Cmb_Layer_SelectedFeatureLayer
        {
            get => _saSource_Cmb_Layer_SelectedFeatureLayer;
            set => SetProperty(ref _saSource_Cmb_Layer_SelectedFeatureLayer, value, () => SASource_Cmb_Layer_SelectedFeatureLayer);
        }


        #endregion

        #region OUTPUT SPATIAL REFERENCE

        public Visibility OutputSR_Vis_Border
        {
            get => _outputSR_Vis_Border;
            set => SetProperty(ref _outputSR_Vis_Border, value, () => OutputSR_Vis_Border);
        }
        public Visibility OutputSR_Vis_Map_Controls
        {
            get => _outputSR_Vis_Map_Controls;
            set => SetProperty(ref _outputSR_Vis_Map_Controls, value, () => OutputSR_Vis_Map_Controls);
        }
        public Visibility OutputSR_Vis_Layer_Controls
        {
            get => _outputSR_Vis_Layer_Controls;
            set => SetProperty(ref _outputSR_Vis_Layer_Controls, value, () => OutputSR_Vis_Layer_Controls);
        }
        public Visibility OutputSR_Vis_User_Controls
        {
            get => _outputSR_Vis_User_Controls;
            set => SetProperty(ref _outputSR_Vis_User_Controls, value, () => OutputSR_Vis_User_Controls);
        }
        public bool OutputSR_Rad_Map_IsEnabled
        {
            get => _outputSR_Rad_Map_IsEnabled;
            set => SetProperty(ref _outputSR_Rad_Map_IsEnabled, value, () => OutputSR_Rad_Map_IsEnabled);
        }
        public bool OutputSR_Rad_Map_IsChecked
        {
            get => _outputSR_Rad_Map_IsChecked;
            set
            {
                SetProperty(ref _outputSR_Rad_Map_IsChecked, value, () => OutputSR_Rad_Map_IsChecked);
                OutputSR_Vis_Map_Controls = value ? Visibility.Visible: Visibility.Collapsed;
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_OUTPUTSR_SOURCE = "MAP";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool OutputSR_Rad_Layer_IsChecked
        {
            get => _outputSR_Rad_Layer_IsChecked;
            set
            {
                SetProperty(ref _outputSR_Rad_Layer_IsChecked, value, () => OutputSR_Rad_Layer_IsChecked);
                OutputSR_Vis_Layer_Controls = value ? Visibility.Visible : Visibility.Collapsed;
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_OUTPUTSR_SOURCE = "LAYER";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool OutputSR_Rad_User_IsChecked
        {
            get => _outputSR_Rad_User_IsChecked;
            set
            {
                SetProperty(ref _outputSR_Rad_User_IsChecked, value, () => OutputSR_Rad_User_IsChecked);
                OutputSR_Vis_User_Controls = value ? Visibility.Visible : Visibility.Collapsed;
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_OUTPUTSR_SOURCE = "USER";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public string OutputSR_Txt_Map_SRName
        {
            get => _outputSR_Txt_Map_SRName;
            set => SetProperty(ref _outputSR_Txt_Map_SRName, value, () => OutputSR_Txt_Map_SRName);
        }
        public string OutputSR_Txt_User_SRName
        {
            get => _outputSR_Txt_User_SRName;
            set => SetProperty(ref _outputSR_Txt_User_SRName, value, () => OutputSR_Txt_User_SRName);
        }
        public List<SpatialReference> OutputSR_Cmb_Layer_SpatialReferences
        {
            get => _outputSR_Cmb_Layer_SpatialReferences;
            set => SetProperty(ref _outputSR_Cmb_Layer_SpatialReferences, value, () => OutputSR_Cmb_Layer_SpatialReferences);
        }
        public SpatialReference OutputSR_Cmb_Layer_SelectedSpatialReference
        {
            get => _outputSR_Cmb_Layer_SelectedSpatialReference;
            set => SetProperty(ref _outputSR_Cmb_Layer_SelectedSpatialReference, value, () => OutputSR_Cmb_Layer_SelectedSpatialReference);
        }

        #endregion

        public ProgressManager PM
        {
            get => _pm; set => SetProperty(ref _pm, value, () => PM);
        }

        #endregion

        #region COMMANDS

        public ICommand CmdSelectSpatialReference => _cmdSelectSpatialReference ?? (_cmdSelectSpatialReference = new RelayCommand(() => SelectSpatialReference(), () => true));

        public ICommand CmdGeneratePlanningUnits => _cmdGeneratePlanningUnits ?? (_cmdGeneratePlanningUnits = new RelayCommand(async () => await GeneratePlanningUnits(), () => true));

        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true));

        public ICommand CmdTest => _cmdTest ?? (_cmdTest = new RelayCommand(async () => await Test(), () => true));
        #endregion

        #region METHODS

        public async Task OnProWinLoaded()
        {
            try
            {
                // Clear the Progress Bar
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                #region PLANNING UNIT SOURCE GEOMETRY

                // Geometry Source Radio Buttons
                string pusrc = Properties.Settings.Default.DEFAULT_PU_GEOMETRY_SOURCE;
                if (string.IsNullOrEmpty(pusrc) || pusrc == "NATGRID")
                {
                    PUSource_Rad_NatGrid_IsChecked = true;
                }
                else if (pusrc == "CUSTOMGRID")
                {
                    PUSource_Rad_CustomGrid_IsChecked = true;
                }
                else if (pusrc == "LAYER")
                {
                    PUSource_Rad_Layer_IsChecked = true;
                }
                else
                {
                    PUSource_Rad_NatGrid_IsChecked = true;
                }

                // National Grid - Dimension
                string dimension = Properties.Settings.Default.DEFAULT_NATGRID_DIMENSION;   //
                if (string.IsNullOrEmpty(dimension) || dimension == "3")
                {
                    PUSource_Rad_NatGrid_1Km_IsChecked = true;
                }
                else if (dimension == "0")
                {
                    PUSource_Rad_NatGrid_1M_IsChecked = true;
                }
                else if (dimension == "1")
                {
                    PUSource_Rad_NatGrid_10M_IsChecked = true;
                }
                else if (dimension == "2")
                {
                    PUSource_Rad_NatGrid_100M_IsChecked = true;
                }
                else if (dimension == "4")
                {
                    PUSource_Rad_NatGrid_10Km_IsChecked = true;
                }
                else if (dimension == "5")
                {
                    PUSource_Rad_NatGrid_100Km_IsChecked = true;
                }
                else
                {
                    PUSource_Rad_NatGrid_1Km_IsChecked = true;
                }

                // Custom Grid - Tile Shapes
                PUSource_Cmb_CustomGrid_TileShapes = Enum.GetNames(typeof(CustomGridTileShape)).ToList();

                string tile_type = Properties.Settings.Default.DEFAULT_TILE_SHAPE;

                if (string.IsNullOrEmpty(tile_type))
                {
                    PUSource_Cmb_CustomGrid_SelectedTileShape = CustomGridTileShape.SQUARE.ToString();
                }
                else
                {
                    PUSource_Cmb_CustomGrid_SelectedTileShape = (tile_type == CustomGridTileShape.HEXAGON.ToString()) ? CustomGridTileShape.HEXAGON.ToString() : CustomGridTileShape.SQUARE.ToString();
                }

                // Custom Grid Tile Area
                string tile_area = Properties.Settings.Default.DEFAULT_TILE_AREA;

                if (string.IsNullOrEmpty(tile_area))
                {
                    PUSource_Txt_CustomGrid_TileArea = "1";
                }
                else if (double.TryParse(tile_area, out double tilearea))
                {
                    PUSource_Txt_CustomGrid_TileArea = (tilearea <= 0) ? "1" : tile_area;
                }
                else
                {
                    PUSource_Txt_CustomGrid_TileArea = "1";
                }

                // Custom Grid Tile Area Units
                string area_units = Properties.Settings.Default.DEFAULT_TILE_AREA_UNITS;

                switch (area_units)
                {
                    case "M":
                        PUSource_Rad_TileArea_M_IsChecked = true;
                        break;
                    case "AC":
                        PUSource_Rad_TileArea_Ac_IsChecked = true;
                        break;
                    case "HA":
                        PUSource_Rad_TileArea_Ha_IsChecked = true;
                        break;
                    case "KM":
                    default:
                        PUSource_Rad_TileArea_Km_IsChecked = true;
                        break;
                }

                // Feature Layers Listing
                List<FeatureLayer> featureLayers = new List<FeatureLayer>();

                if (!await QueuedTask.Run(() =>
                {
                    try
                    {
                        var flayers = _map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(fl => fl.ShapeType == esriGeometryType.esriGeometryPolygon);

                        foreach (var flayer in flayers)
                        {
                            using (FeatureClass fc = flayer.GetFeatureClass())
                            {
                                if (fc == null)
                                {
                                    continue;
                                }
                            }

                            SpatialReference sr = flayer.GetSpatialReference();
                            if (sr == null || sr.IsUnknown)
                            {
                                continue;
                            }

                            featureLayers.Add(flayer);
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
                    ProMsgBox.Show($"Error retrieving list of valid polygon feature layers.");
                    return;
                }
                else
                {
                    PUSource_Cmb_Layer_FeatureLayers = featureLayers;
                }

                // Use Raster checkbox setting
                PUSource_Chk_UseRasterFormat = Properties.Settings.Default.DEFAULT_PU_USE_RASTER_FORMAT;

                #endregion

                #region STUDY AREA SOURCE GEOMETRY

                // Geometry Source
                string sasrc = Properties.Settings.Default.DEFAULT_SA_GEOMETRY_SOURCE;
                if (string.IsNullOrEmpty(sasrc) || sasrc == "LAYER")
                {
                    SASource_Rad_Layer_IsChecked = true;
                }
                else if (sasrc == "GRAPHIC")
                {
                    SASource_Rad_Graphic_IsChecked = true;
                }
                else
                {
                    SASource_Rad_Layer_IsChecked = true;
                }

                // Buffer Distance
                string dist = Properties.Settings.Default.DEFAULT_SA_BUFFER_DISTANCE;

                if (string.IsNullOrEmpty(dist))
                {
                    SASource_Txt_BufferDistance = "0";
                }
                else if (double.TryParse(dist, out double bd))
                {
                    SASource_Txt_BufferDistance = (bd < 0) ? "0" : dist;
                }
                else
                {
                    SASource_Txt_BufferDistance = "0";
                }

                // Buffer Distance Units
                string bd_units = Properties.Settings.Default.DEFAULT_SA_BUFFER_DISTANCE_UNITS;

                switch (bd_units)
                {
                    case "M":
                        SASource_Rad_BufferDistance_M_IsChecked = true;
                        break;

                    case "KM":
                    default:
                        SASource_Rad_BufferDistance_Km_IsChecked = true;
                        break;
                }

                // Graphics Layers
                List<GraphicsLayer> graphicsLayers = new List<GraphicsLayer>();

                if (!await QueuedTask.Run(() =>
                {
                    try
                    {
                        var GLs = _map.GetLayersAsFlattenedList().OfType<GraphicsLayer>();

                        foreach (var GL in GLs)
                        {
                            var selectedElements = GL.GetSelectedElements().OfType<GraphicElement>();
                            bool hasPolys = false;

                            foreach (var elem in selectedElements)
                            {
                                CIMGraphic g = elem.GetGraphic();

                                if (g is CIMPolygonGraphic)
                                {
                                    hasPolys = true;
                                    break;
                                }
                            }

                            if (hasPolys)
                            {
                                graphicsLayers.Add(GL);
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
                    ProMsgBox.Show($"Error retrieving list of graphics layers.");
                    return;
                }
                else
                {
                    SASource_Cmb_Graphic_GraphicsLayers = graphicsLayers;
                }

                // Feature Layers
                var FLayersWithSelections = new List<FeatureLayer>();

                if (!await QueuedTask.Run(() =>
                {
                    try
                    {
                        var flayers = _map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(fl => fl.ShapeType == esriGeometryType.esriGeometryPolygon);

                        foreach (var flayer in flayers)
                        {
                            using (FeatureClass fc = flayer.GetFeatureClass())
                            {
                                if (fc == null)
                                {
                                    continue;
                                }
                            }

                            SpatialReference sr = flayer.GetSpatialReference();
                            if (sr == null || sr.IsUnknown)
                            {
                                continue;
                            }

                            if (flayer.SelectionCount == 0)
                            {
                                continue;
                            }

                            FLayersWithSelections.Add(flayer);
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
                    ProMsgBox.Show($"Error retrieving list of graphics layers.");
                    return;
                }
                else
                {
                    SASource_Cmb_Layer_FeatureLayers = FLayersWithSelections;
                }

                #endregion

                #region OUTPUT SPATIAL REFERENCE

                // Map Spatial Reference
                OutputSR_Rad_Map_IsEnabled = _map.SpatialReference.IsProjected && _map.SpatialReference.Unit.FactoryCode == 9001;   // might only really need the second condition here...
                OutputSR_Txt_Map_SRName = string.IsNullOrEmpty(_map.SpatialReference.Name) ? "(null, unknown, or unnamed spatial reference)" : _map.SpatialReference.Name;

                // Layer Spatial References
                List<SpatialReference> SRs = new List<SpatialReference>();

                if (!await QueuedTask.Run(() =>
                {
                    try
                    {
                        var layers = _map.GetLayersAsFlattenedList();

                        foreach (Layer layer in layers)
                        {
                            SpatialReference sr = layer.GetSpatialReference();

                            if (sr != null)
                            {
                                if (sr.IsProjected && sr.Unit.FactoryCode == 9001)
                                {
                                    bool already_added = false;

                                    foreach (SpatialReference spat in SRs)
                                    {
                                        if (SpatialReference.AreEqual(spat, sr))
                                        {
                                            already_added = true;
                                            break;
                                        }
                                    }

                                    if (!already_added)
                                    {
                                        SRs.Add(sr);
                                    }
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
                    ProMsgBox.Show($"Error retrieving spatial references of map layers.");
                    return;
                }
                else
                {
                    OutputSR_Cmb_Layer_SpatialReferences = SRs;
                }

                string output_sr_source = Properties.Settings.Default.DEFAULT_OUTPUTSR_SOURCE;

                switch (output_sr_source)
                {
                    case "MAP":
                        if (OutputSR_Rad_Map_IsEnabled)
                        {
                            OutputSR_Rad_Map_IsChecked = true;
                        }
                        else
                        {
                            OutputSR_Rad_Layer_IsChecked = true;
                            Properties.Settings.Default.DEFAULT_OUTPUTSR_SOURCE = "LAYER";
                            Properties.Settings.Default.Save();
                        }
                        break;

                    case "USER":
                        OutputSR_Rad_User_IsChecked = true;
                        break;

                    case "LAYER":
                        OutputSR_Rad_Layer_IsChecked = true;
                        break;

                    default:
                        OutputSR_Rad_Layer_IsChecked = true;
                        Properties.Settings.Default.DEFAULT_OUTPUTSR_SOURCE = "LAYER";
                        Properties.Settings.Default.Save();
                        break;
                }

                #endregion

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> GeneratePlanningUnits()
        {
            int val = 0;

            try
            {
                #region VALIDATION I

                // Initialize ProgressBar and Progress Log
                int max = 50;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Selection Rules Generator..."), false, max, ++val);

                // Check for currently unsaved edits in the project
                if (Project.Current.HasEdits)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro Project has unsaved edits.  Please save all edits before proceeding.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("This ArcGIS Pro Project has some unsaved edits.  Please save all edits before proceeding.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro Project has no unsaved edits.  Proceeding..."), true, ++val);
                }

                // Validation: Ensure the Project Geodatabase Exists
                string gdbpath = PRZH.GetPath_ProjectGDB();
                if (!await PRZH.ProjectGDBExists())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Project Geodatabase not found: {gdbpath}", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Project Geodatabase not found at this path:" +
                                   Environment.NewLine +
                                   gdbpath +
                                   Environment.NewLine + Environment.NewLine +
                                   "Please specify a valid Project Workspace.", "Validation");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Project Geodatabase is OK: {gdbpath}"), true, ++val);
                }

                // Validation: Output Spatial Reference
                SpatialReference OutputSR = null;

                if (PUSource_Rad_NatGrid_IsChecked)
                {
                    var natsr = PRZH.GetSR_PRZCanadaAlbers();
                    if (natsr == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Unable to retrieve PRZ Canada Albers projection.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Unable to retrieve PRZ Canada Albers projection.", "Validation");
                        return false;
                    }
                    else
                    {
                        OutputSR = natsr;
                    }
                }
                else if (OutputSR_Rad_Map_IsChecked)
                {
                    OutputSR = _map.SpatialReference;
                }
                else if (OutputSR_Rad_Layer_IsChecked && OutputSR_Cmb_Layer_SelectedSpatialReference != null)
                {
                    OutputSR = OutputSR_Cmb_Layer_SelectedSpatialReference;
                }
                else if (OutputSR_Rad_User_IsChecked)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> User-specified Output spatial reference is not an option (yet).", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("User-specified Output spatial reference is not an option (yet).", "Validation");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Output spatial reference has not been specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify an output spatial reference.", "Validation");
                    return false;
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Output Spatial Reference = {OutputSR.Name}"), true, ++val);


                // Validation: Study Area Source Geometry
                if (SASource_Rad_Graphic_IsChecked)
                {
                    if (SASource_Cmb_Graphic_SelectedGraphicsLayer == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Study Area Source Geometry - no graphics layer is selected.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Study Area Source Geometry - no graphics layer is selected.", "Validation");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Study Area Source Geometry - graphics layer name: " + SASource_Cmb_Graphic_SelectedGraphicsLayer.Name), true, ++val);
                    }
                }
                else if (SASource_Rad_Layer_IsChecked)
                {
                    if (SASource_Cmb_Layer_SelectedFeatureLayer == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Study Area Source Geometry - no feature layer is selected.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Study Area Source Geometry - no feature layer is selected", "Validation");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Study Area Source Geometry - feature layer name: " + SASource_Cmb_Layer_SelectedFeatureLayer.Name), true, ++val);
                    }
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Study Area Source Geometry - no graphics or feature layer is selected.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Study Area Source Geometry - no graphics or feature layer is selected.", "Validation");
                    return false;
                }

                // Validation: Study Area Buffer Distance
                string buffer_dist_text = string.IsNullOrEmpty(SASource_Txt_BufferDistance) ? "0" : ((SASource_Txt_BufferDistance.Trim() == "") ? "0" : SASource_Txt_BufferDistance.Trim());

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

                if (SASource_Rad_BufferDistance_M_IsChecked)
                {
                    bu = buffer_dist_text + " m";
                    buffer_dist_m = buffer_dist;
                }
                else if (SASource_Rad_BufferDistance_Km_IsChecked)
                {
                    bu = buffer_dist_text + " km";
                    buffer_dist_m = buffer_dist * 1000.0;
                }

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Buffer Distance = " + bu), true, ++val);

                #endregion

                #region VALIDATION II

                // Miscellaneous variables
                double customgrid_tile_area_m2 = 0;
                var customgrid_tile_shape = CustomGridTileShape.SQUARE;
                int natgrid_dimension = 0;
                int natgrid_sidelength = 0;

                if (PUSource_Rad_NatGrid_IsChecked)
                {
                    // Ensure that one of the national grid dimensions have been picked
                    if (!PUSource_Rad_NatGrid_1M_IsChecked && !PUSource_Rad_NatGrid_10M_IsChecked && !PUSource_Rad_NatGrid_100M_IsChecked && !PUSource_Rad_NatGrid_1Km_IsChecked && !PUSource_Rad_NatGrid_10Km_IsChecked & !PUSource_Rad_NatGrid_100Km_IsChecked)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> No National Grid dimension is specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Please specify a National Grid dimension", "Validation");
                        return false;
                    }

                    // calculate dimension and side length
                    if (PUSource_Rad_NatGrid_1M_IsChecked)
                    {
                        natgrid_dimension = 0;
                        natgrid_sidelength = 1;
                    }
                    else if (PUSource_Rad_NatGrid_10M_IsChecked)
                    {
                        natgrid_dimension = 1;
                        natgrid_sidelength = 10;
                    }
                    else if (PUSource_Rad_NatGrid_100M_IsChecked)
                    {
                        natgrid_dimension = 2;
                        natgrid_sidelength = 100;
                    }
                    else if (PUSource_Rad_NatGrid_1Km_IsChecked)
                    {
                        natgrid_dimension = 3;
                        natgrid_sidelength = 1000;
                    }
                    else if (PUSource_Rad_NatGrid_10Km_IsChecked)
                    {
                        natgrid_dimension = 4;
                        natgrid_sidelength = 10000;
                    }
                    else if (PUSource_Rad_NatGrid_100Km_IsChecked)
                    {
                        natgrid_dimension = 5;
                        natgrid_sidelength = 100000;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Invalid National Grid dimension specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Invalid National Grid dimension specified.", "Validation");
                        return false;
                    }

                    // Notify of Raster Option choice
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Raster option selected: {PUSource_Chk_UseRasterFormat}"), true, ++val);
                }
                else if (PUSource_Rad_CustomGrid_IsChecked)
                {
                    // Validation: Tile Shape
                    string tile_shape = string.IsNullOrEmpty(_puSource_Cmb_CustomGrid_SelectedTileShape) ? "" : _puSource_Cmb_CustomGrid_SelectedTileShape;
                    if (tile_shape == "")
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Tile shape not specified", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Please specify a tile shape", "Validation");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> Tile Shape = {tile_shape}"), true, ++val);
                    }

                    switch (tile_shape)
                    {
                        case "SQUARE":
                            customgrid_tile_shape = CustomGridTileShape.SQUARE;
                            break;
                        case "HEXAGON":
                            customgrid_tile_shape = CustomGridTileShape.HEXAGON;
                            break;
                        default:
                            return false;
                    }

                    // Validation: Tile Area
                    string tile_area_text = string.IsNullOrEmpty(PUSource_Txt_CustomGrid_TileArea) ? "0" : ((PUSource_Txt_CustomGrid_TileArea.Trim() == "") ? "0" : PUSource_Txt_CustomGrid_TileArea.Trim());

                    if (!double.TryParse(tile_area_text, out customgrid_tile_area_m2))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Tile Area", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Please specify a valid Tile Area.  Value must be numeric and greater than 0", "Validation");
                        return false;
                    }
                    else if (customgrid_tile_area_m2 <= 0)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Tile Area", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Please specify a valid Tile Area.  Value must be numeric and greater than 0", "Validation");
                        return false;
                    }
                    else
                    {
                        string au = "";

                        if (PUSource_Rad_TileArea_M_IsChecked)
                        {
                            au = tile_area_text + " m\xB2";
                        }
                        else if (PUSource_Rad_TileArea_Ac_IsChecked)
                        {
                            au = tile_area_text + " ac";
                            customgrid_tile_area_m2 /= PRZC.c_CONVERT_M2_TO_AC;
                        }
                        else if (PUSource_Rad_TileArea_Ha_IsChecked)
                        {
                            au = tile_area_text + " ha";
                            customgrid_tile_area_m2 /= PRZC.c_CONVERT_M2_TO_HA;
                        }
                        else if (PUSource_Rad_TileArea_Km_IsChecked)
                        {
                            au = tile_area_text + " km\xB2";
                            customgrid_tile_area_m2 /= PRZC.c_CONVERT_M2_TO_KM2;
                        }

                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Tile Area = " + au), true, ++val);
                    }
                }
                else if (PUSource_Rad_Layer_IsChecked)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Unsupported Functionality", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Planning Unit Geometry from a Feature Layer is not yet supported.");
                    return false;
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
                                   "GEODATABASE OVERWRITE WARNING",
                                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out."), true, ++val);
                    return false;
                }

                #endregion

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                #region STUDY AREA GEOMETRY

                // Retrieve Polygons to construct Study Area + Buffered Study Area
                Polygon SA_poly = null;

                if (SASource_Rad_Graphic_IsChecked)
                {
                    // Get the selected polygon graphics from the graphics layer
                    GraphicsLayer gl = SASource_Cmb_Graphic_SelectedGraphicsLayer;

                    var selElems = gl.GetSelectedElements().OfType<GraphicElement>();
                    int polyelems = 0;

                    if (!await QueuedTask.Run(() =>
                    {
                        try
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
                                    polyelems++;
                                }
                            }

                            Polygon poly = polyBuilder.ToGeometry();
                            SA_poly = (Polygon)GeometryEngine.Instance.Project(poly, OutputSR);

                            return true;
                        }
                        catch (Exception ex)
                        {
                            ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                            return false;
                        }
                    }))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving selected polygon graphic element.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error retrieving selected polygon graphic element.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Study Area >> Retrieved {polyelems} selected polygon(s) from the {gl.Name} graphics layer."), true, ++val);
                    }
                }
                else if (SASource_Rad_Layer_IsChecked)
                {
                    // Get the selected polygon features from the selected feature layer
                    FeatureLayer fl = SASource_Cmb_Layer_SelectedFeatureLayer;
                    int selpol = 0;

                    if (!await QueuedTask.Run(() =>
                    {
                        try
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
                                            selpol++;
                                        }
                                    }
                                }
                            }

                            Polygon poly = polyBuilder.ToGeometry();
                            SA_poly = (Polygon)GeometryEngine.Instance.Project(poly, OutputSR);

                            return true;
                        }
                        catch (Exception ex)
                        {
                            ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                            return false;
                        }
                    }))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving selected polygons.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error retrieving selected polygons.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Study Area >> Retrieved {selpol} selected polygon(s) from the {fl.Name} feature layer."), true, ++val);
                    }
                }

                // Generate Buffered Polygons (buffer might be 0)
                Polygon SA_poly_buffer = GeometryEngine.Instance.Buffer(SA_poly, buffer_dist_m) as Polygon;
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Study Area >> Buffered by {buffer_dist_m} meter{((buffer_dist_m == 1) ? "" : "s")}"), true, ++val);

                #endregion

                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                #region STRIP MAP AND GDB

                // Remove any PRZ GDB Layers or Tables from Active Map
                var flyrs = _map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                List<Layer> LayersToDelete = new List<Layer>();

                if (!await RemovePRZLayersAndTables())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to remove all layers and standalone tables where source = {gdbpath}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Unable to remove layers and standalone tables from PRZ geodatabase");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Removed all layers and standalone tables where source = {gdbpath}"), true, ++val);
                }

                // Delete all Items from Project GDB
                if (!await PRZH.ClearProjectGDB())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to delete all feature classes, tables, raster datasets, and feature datasets from " + gdbpath, LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Unable to delete all feature classes, tables, raster datasets, or feature datasets from {gdbpath}");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleted all feature classes, tables, raster datasets, and feature datasets from " + gdbpath), true, ++val);
                }

                #endregion

                #region CREATE PLANNING UNIT RASTER

                if (PUSource_Chk_UseRasterFormat && PUSource_Rad_NatGrid_IsChecked)
                {
                    // Prepare an accelerated buffer poly for use in the GeometryEngine's 'intersects' method (potentially helps speed things up)
                    Polygon accelerated_buffer = (Polygon)GeometryEngine.Instance.AccelerateForRelationalOperations(SA_poly_buffer);

                    string tempRaster1 = "TempRas1";
                    string tempRaster2 = "TempRas2";

                    var res = NationalGridInfo.GetGridBoundsFromStudyArea(SA_poly_buffer, (NationalGridDimension)natgrid_dimension);
                    if (!res.success)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve National Grid extent envelope.\n{res.message}", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Unable to retrieve National Grid extent envelope.\n{res.message}");
                        return false;
                    }

                    // Load the National Grid Tiles
                    Envelope gridEnv = res.gridEnv;
                    int tiles_across = res.tilesAcross;
                    int tiles_up = res.tilesUp;
                    int side_length = natgrid_sidelength;
                    object[] o = { tempRaster1, 0, "INTEGER", side_length, gridEnv };

                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating Planning Unit Raster Dataset..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(o);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("CreateConstantRaster_sa", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {PRZC.c_RAS_PLANNING_UNITS} raster dataset.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error creating the {PRZC.c_RAS_PLANNING_UNITS} raster dataset.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Raster dataset created successfully."), true, ++val);
                    }

                    // Copy the raster to the correct bitdepth of int32 (to allow storage of humongous ID numbers
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Copying to better bit depth..."), true, ++val);

                    object[] o2 = { tempRaster1, tempRaster2, "", "", "1", "", "", "32_BIT_UNSIGNED", "", "", "", "", "", "" };

                    toolParams = Geoprocessing.MakeValueArray(o2);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("CopyRaster_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying the {PRZC.c_RAS_PLANNING_UNITS} raster dataset.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error copying the {PRZC.c_RAS_PLANNING_UNITS} raster dataset.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Raster dataset copied successfully."), true, ++val);
                    }

                    // Next step...
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Try next step..."), true, ++val);
                    if (!await QueuedTask.Run(async () =>
                    {
                        try
                        {
                            using (Geodatabase geodatabase = await PRZH.GetProjectGDB())
                            {
                                if (!await PRZH.RasterExists(geodatabase, tempRaster2))
                                {
                                    return false;
                                }

                                using (RasterDataset rasterDataset = geodatabase.OpenDataset<RasterDataset>(tempRaster2))
                                {
                                    // Get the virtual Raster from Band 1 of the Raster Dataset
                                    int[] bands = { 0 };
                                    Raster raster = rasterDataset.CreateRaster(bands);

                                    // Create a PixelBlock 1 row high, stretching across all columns
                                    PixelBlock pixelBlock = raster.CreatePixelBlock(raster.GetWidth(), 1);

                                    // Get the top left corner coords of the top left cell in the envelope (cell 0,0)
                                    int OriginCell_ULX = Convert.ToInt32(gridEnv.XMin);
                                    int OriginCell_ULY = Convert.ToInt32(gridEnv.YMax);

                                    int puid = 1;

                                    // Loop through each row of tiles (top to bottom)
                                    for (int row = 0; row < tiles_up; row++)
                                    {
                                        // Read a raster row into the pixel block
                                        raster.Read(0, row, pixelBlock);

                                        // write the pixel block contents to an array
                                        int[,] array = (int[,])pixelBlock.GetPixelData(0, true);

                                        // Get the Y-coord of the current row (YMax of row)
                                        double CurrentCell_ULY = OriginCell_ULY - (row * side_length);

                                        // Loop through each cell (left to right) within the parent row
                                        for (int col = 0; col < tiles_across; col++)
                                        {
                                            // Get the X-coord of the current column (XMin of column)
                                            double CurrentCell_ULX = OriginCell_ULX + (col * side_length);

                                            // Build the envelope for the current cell
                                            Envelope tileEnv = EnvelopeBuilder.CreateEnvelope(CurrentCell_ULX, CurrentCell_ULY - side_length, CurrentCell_ULX + side_length, CurrentCell_ULY, OutputSR);

                                            if (GeometryEngine.Instance.Intersects(accelerated_buffer, tileEnv))
                                            {
                                                // I need to alter the pixel value here to puid++
                                                array[col, 0] = puid++;
                                            }
                                        }

                                        // write the array values back to the pixel block, even if unchanged
                                        pixelBlock.SetPixelData(0, array);

                                        // write the pixel block data back to the raster
                                        raster.Write(0, row, pixelBlock);
                                    }

                                    // All zeros now are nodata
                                    raster.SetNoDataValue(0);

                                    // Persist the raster to the geodatabase
                                    raster.SaveAs(PRZC.c_RAS_PLANNING_UNITS, geodatabase, "GRID");
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"saved."), true, ++val);
                                }
                            }

                            // Delete the temp rasters
                            string rasters_to_delete = tempRaster1 + ";" + tempRaster2;
                            toolParams = Geoprocessing.MakeValueArray(rasters_to_delete, "");
                            toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                            toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                            if (toolOutput == null)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting temp rasters.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                                ProMsgBox.Show($"Error deleting temp rasters.");
                                return false;
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Temp rasters deleted."), true, ++val);
                            }

                            // Calculate Statistics on pu raster
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Calculating Statistics..."), true, ++val);
                            toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS);
                            toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                            toolOutput = await PRZH.RunGPTool("CalculateStatistics_management", toolParams, toolEnvs, toolFlags);
                            if (toolOutput == null)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error calculating statistics.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                                ProMsgBox.Show($"Error calculating statistics.");
                                return false;
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Statistics calculated successfully."), true, ++val);
                            }

                            // Build Pyramids for pu raster
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Building pyramids..."), true, ++val);
                            toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS, -1, "", "", "", "", "");
                            toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                            toolOutput = await PRZH.RunGPTool("BuildPyramids_management", toolParams, toolEnvs, toolFlags);
                            if (toolOutput == null)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error building pyramids.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                                ProMsgBox.Show($"Error building pyramids.");
                                return false;
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"pyramids built successfully."), true, ++val);
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
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Something bad.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Something bad here.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Something good happened here."), true, ++val);
                    }
                }

                return true;

                #endregion

                #region CREATE PLANNING UNIT FC

                string pufcpath = PRZH.GetPath_FC_PU();

                // Build the new empty Planning Unit FC
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating Planning Unit Feature Class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_PLANNING_UNITS, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {PRZC.c_FC_PLANNING_UNITS} feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error creating the {PRZC.c_FC_PLANNING_UNITS} feature class.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Feature class created successfully."), true, ++val);
                }

                // Add Fields to Planning Unit FC
                string fldPUID = PRZC.c_FLD_FC_PU_ID + " LONG 'Planning Unit ID' # # #;";
                string fldNatGridID = PRZC.c_FLD_FC_PU_NATGRID_ID + " TEXT 'National Grid ID' 20 # #;";
                string fldPUEffectiveRule = PRZC.c_FLD_FC_PU_EFFECTIVE_RULE + " TEXT 'Effective Rule' 10 # #;";
                string fldConflict = PRZC.c_FLD_FC_PU_CONFLICT + " LONG 'Rule Conflict' # 0 #;";
                string fldPUCost = PRZC.c_FLD_FC_PU_COST + " DOUBLE 'Cost' # 1 #;";
                string fldPUAreaM = PRZC.c_FLD_FC_PU_AREA_M2 + " DOUBLE 'Square m' # 0 #;";
                string fldPUAreaAC = PRZC.c_FLD_FC_PU_AREA_AC + " DOUBLE 'Acres' # 0 #;";
                string fldPUAreaHA = PRZC.c_FLD_FC_PU_AREA_HA + " DOUBLE 'Hectares' # 0 #;";
                string fldPUAreaKM = PRZC.c_FLD_FC_PU_AREA_KM2 + " DOUBLE 'Square km' # 0 #;";
                string fldCFCount = PRZC.c_FLD_FC_PU_FEATURECOUNT + " LONG 'Conservation Feature Count' # 0 #;";
                string fldSharedPerim = PRZC.c_FLD_FC_PU_SHARED_PERIM + " DOUBLE 'Shared Perimeter (m)' # 0 #;";
                string fldUnsharedPerim = PRZC.c_FLD_FC_PU_UNSHARED_PERIM + " DOUBLE 'Unshared Perimeter (m)' # 0 #;";
                string fldHasUnsharedPerim = PRZC.c_FLD_FC_PU_HAS_UNSHARED_PERIM + " LONG 'Has Unshared Perimeter' # 0 #;";

                string flds = fldPUID + fldNatGridID + fldPUEffectiveRule + fldConflict + fldPUCost + fldPUAreaM + fldPUAreaAC + fldPUAreaHA + fldPUAreaKM + fldCFCount + fldSharedPerim + fldUnsharedPerim + fldHasUnsharedPerim;

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to {PRZC.c_FC_PLANNING_UNITS} feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(pufcpath, flds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to {PRZC.c_FC_PLANNING_UNITS} feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error adding fields to {PRZC.c_FC_PLANNING_UNITS} feature class.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully."), true, ++val);
                }

                #region IMPORT PLANNING UNIT FEATURES BASED ON SOURCE TYPE

                if (PUSource_Rad_NatGrid_IsChecked)
                {
                    // Retrieve the required grid extent based on the specified National Grid dimension and the buffered study area
                    var res = NationalGridInfo.GetGridBoundsFromStudyArea(SA_poly_buffer, (NationalGridDimension)natgrid_dimension);

                    if (!res.success)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve National Grid extent envelope.\n{res.message}", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Unable to retrieve National Grid extent envelope.\n{res.message}");
                        return false;
                    }

                    // Load the National Grid Tiles
                    Envelope gridEnv = res.gridEnv;
                    int tiles_across = res.tilesAcross;
                    int tiles_up = res.tilesUp;
                    int side_length = natgrid_sidelength;

                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Loading the National Grid tiles"), true, ++val);
                    if (!await LoadNationalGridTiles(SA_poly_buffer, gridEnv, tiles_across, tiles_up, side_length, natgrid_dimension))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error loading tiles", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error loading tiles.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"National Grid tiles loaded"), true, ++val);
                    }

                }
                else if (PUSource_Rad_CustomGrid_IsChecked)
                {
                    // Assemble the Grid
                    double tile_edge_length = 0;
                    double tile_width = 0;
                    double tile_center_to_right = 0;
                    double tile_height = 0;
                    double tile_center_to_top = 0;
                    int tiles_across = 0;
                    int tiles_up = 0;

                    switch (customgrid_tile_shape)
                    {
                        case CustomGridTileShape.SQUARE:
                            tile_edge_length = Math.Sqrt(customgrid_tile_area_m2);
                            tile_width = tile_edge_length;
                            tile_height = tile_edge_length;
                            tile_center_to_right = tile_width / 2.0;
                            tile_center_to_top = tile_height / 2.0;
                            break;
                        case CustomGridTileShape.HEXAGON:
                            tile_edge_length = Math.Sqrt((2 * customgrid_tile_area_m2) / (3 * Math.Sqrt(3)));
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

                    if (!await QueuedTask.Run(() =>
                    {
                        try
                        {
                            MapPointBuilder mpbuilder = new MapPointBuilder(env.XMin, env.YMin, OutputSR);
                            env_ll_point = mpbuilder.ToGeometry();

                            return true;
                        }
                        catch (Exception ex)
                        {
                            ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                            return false;
                        }
                    }))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving lower left corner of buffer extent.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error retrieving lower left corner of buffer extent.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"lower left corner of extent retrieved."), true, ++val);
                    }

                    switch (customgrid_tile_shape)
                    {
                        case CustomGridTileShape.SQUARE:
                            tiles_across = (int)Math.Ceiling(env_width / tile_width) + 3;
                            tiles_up = (int)Math.Ceiling(env_height / tile_height) + 3;
                            break;
                        case CustomGridTileShape.HEXAGON:
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

                    PlanningUnitTileInfo tileinfo = new PlanningUnitTileInfo
                    {
                        LL_Point = env_ll_point,
                        tiles_across = tiles_across,
                        tiles_up = tiles_up,
                        tile_area = customgrid_tile_area_m2,
                        tile_center_to_right = tile_center_to_right,
                        tile_center_to_top = tile_center_to_top,
                        tile_edge_length = tile_edge_length,
                        tile_height = tile_height,
                        tile_shape = customgrid_tile_shape,
                        tile_width = tile_width
                    };

                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Importing tiles into {PRZC.c_FC_PLANNING_UNITS} feature class..."), true, ++val);
                    if (!await LoadCustomGridTiles_EditOp(tileinfo, SA_poly_buffer))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error importing tiles...", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Error importing tiles.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Tiles imported successfully."), true, ++val);
                    }
                }
                else if (PUSource_Rad_Layer_IsChecked)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"unsupported option", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"unsupported option");
                    return false;
                }

                #endregion

                // Index the PU ID field
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing {PRZC.c_FLD_FC_PU_ID} field in the {PRZC.c_FC_PLANNING_UNITS} feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_PLANNING_UNITS, PRZC.c_FLD_FC_PU_ID, "ix" + PRZC.c_FLD_FC_PU_ID, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error indexing field.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field indexed successfully."), true, ++val);
                }

                #endregion

                #region CREATE STUDY AREA FC

                string safcpath = PRZH.GetPath_FC_StudyArea();

                // Build the new empty Main Study Area FC
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating study area feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_STUDY_AREA_MAIN, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error creating study area feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error creating study area feature class.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Study area feature class created successfully."), true, ++val);
                }

                // Add Fields to Main Study Area FC
                string fldArea_m = PRZC.c_FLD_FC_STUDYAREA_AREA_M2 + " DOUBLE 'Square m' # 0 #;";
                string fldArea_ac = PRZC.c_FLD_FC_STUDYAREA_AREA_AC + " DOUBLE 'Acres' # 0 #;";
                string fldArea_ha = PRZC.c_FLD_FC_STUDYAREA_AREA_HA + " DOUBLE 'Hectares' # 0 #;";
                string fldArea_km = PRZC.c_FLD_FC_STUDYAREA_AREA_KM2 + " DOUBLE 'Square km' # 0 #;";

                string SAflds = fldArea_m + fldArea_ac + fldArea_ha + fldArea_km;

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Adding fields to study area feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(safcpath, SAflds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding fields to study area feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error adding fields to study area feature class.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully."), true, ++val);
                }

                // Add the geometry
                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (FeatureClass fc = await PRZH.GetFC_StudyArea())
                        using (FeatureClassDefinition fcDef = fc.GetDefinition())
                        using (InsertCursor insertCursor = fc.CreateInsertCursor())
                        using (RowBuffer rowBuffer = fc.CreateRowBuffer())
                        {
                            // Field values
                            rowBuffer[fcDef.GetShapeField()] = SA_poly;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_M2] = SA_poly.Area;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_AC] = SA_poly.Area * PRZC.c_CONVERT_M2_TO_AC;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_HA] = SA_poly.Area * PRZC.c_CONVERT_M2_TO_HA;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_KM2] = SA_poly.Area * PRZC.c_CONVERT_M2_TO_KM2;

                            // Finally, insert the row
                            insertCursor.Insert(rowBuffer);
                            insertCursor.Flush();
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the study area feature.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error creating the study area feature.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Study area feature created successfully."), true, ++val);
                }

                #endregion

                #region CREATE BUFFERED STUDY AREA FC

                string sabufffcpath = PRZH.GetPath_FC_StudyAreaBuffer();

                // Build the new empty Main Study Area FC
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating buffered study area feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_STUDY_AREA_MAIN_BUFFERED, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error creating buffered study area feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error creating buffered study area feature class.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Buffered study area feature class created successfully."), true, ++val);
                }

                // Add Fields to Buffered Study Area FC
                string fldBArea_m2 = PRZC.c_FLD_FC_STUDYAREA_AREA_M2 + " DOUBLE 'Square m' # 0 #;";
                string fldBArea_ac = PRZC.c_FLD_FC_STUDYAREA_AREA_AC + " DOUBLE 'Acres' # 0 #;";
                string fldBArea_ha = PRZC.c_FLD_FC_STUDYAREA_AREA_HA + " DOUBLE 'Hectares' # 0 #;";
                string fldBArea_km = PRZC.c_FLD_FC_STUDYAREA_AREA_KM2 + " DOUBLE 'Square km' # 0 #;";

                string SABflds = fldBArea_m2 + fldBArea_ac + fldBArea_ha + fldBArea_km;

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Adding fields to buffered study area feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(sabufffcpath, SABflds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding fields to buffered study area feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error adding fields to buffered study area feature class.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"fields added successfully."), true, ++val);
                }

                // Add geometry
                if (!await QueuedTask.Run(async () =>
                {
                    try
                    {
                        using (FeatureClass fc = await PRZH.GetFC_StudyAreaBuffer())
                        using (FeatureClassDefinition fcDef = fc.GetDefinition())
                        using (InsertCursor insertCursor = fc.CreateInsertCursor())
                        using (RowBuffer rowBuffer = fc.CreateRowBuffer())
                        {
                            // Field values
                            rowBuffer[fcDef.GetShapeField()] = SA_poly_buffer;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_M2] = SA_poly_buffer.Area;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_AC] = SA_poly_buffer.Area * PRZC.c_CONVERT_M2_TO_AC;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_HA] = SA_poly_buffer.Area * PRZC.c_CONVERT_M2_TO_HA;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_KM2] = SA_poly_buffer.Area * PRZC.c_CONVERT_M2_TO_KM2;

                            // Finally, insert the row
                            insertCursor.Insert(rowBuffer);
                            insertCursor.Flush();
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the study area buffer feature.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error creating the study area buffer feature.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Study area buffer feature created successfully."), true, ++val);
                }

                #endregion

                #region WRAP UP

                // Compact the Geodatabase
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacting the Geodatabase..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath);
                toolOutput = await PRZH.RunGPTool("Compact_management", toolParams, null, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error compacting the geodatabase. GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error compacting the geodatabase.");
                    return false;
                }

                // Refresh the Map & TOC
                if (!await PRZM.ValidatePRZGroupLayers())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error validating PRZ layers.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error validating PRZ layers.");
                    return false;
                }

                // Wrap things up
                stopwatch.Stop();
                string message = PRZH.GetElapsedTimeMessage(stopwatch.Elapsed);
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Construction completed successfully!"), true, 1, 1);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(message), true, 1, 1);

                ProMsgBox.Show("Construction Completed Successfully!" + Environment.NewLine + Environment.NewLine + message);

                #endregion

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                return false;
            }
        }

        private async Task<bool> RemovePRZLayersAndTables()
        {
            try
            {
                List<StandaloneTable> tables_to_delete = new List<StandaloneTable>();
                List<Layer> layers_to_delete = new List<Layer>();

                await QueuedTask.Run(async () =>
                {
                    using (var geodatabase = await PRZH.GetProjectGDB())
                    {
                        string gdbpath = geodatabase.GetPath().AbsolutePath;

                        // Standalone Tables
                        var standalone_tables = _map.StandaloneTables.ToList();
                        foreach (var standalone_table in standalone_tables)
                        {
                            var uri = standalone_table.GetPath();
                            if (uri != null)
                            {
                                string table_path = uri.AbsolutePath;

                                if (table_path.StartsWith(gdbpath))
                                {
                                    tables_to_delete.Add(standalone_table);
                                }
                            }
                        }

                        // Layers
                        var layers = _map.GetLayersAsFlattenedList().ToList();
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

                    _map.RemoveStandaloneTables(tables_to_delete);
                    _map.RemoveLayers(layers_to_delete);
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

        private async Task<bool> LoadNationalGridTiles(Polygon buffered_study_area, Envelope gridEnvelope, int tiles_across, int tiles_up, int side_length, int dimension)
        {
            bool edits_are_disabled = !Project.Current.IsEditingEnabled;
            int val = 0;
            int max = tiles_up;

            try
            {
                // Check for currently unsaved edits in the project
                if (Project.Current.HasEdits)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro Project has unsaved edits.  Please save all edits before proceeding.", LogMessageType.ERROR), true, max, ++val);
                    ProMsgBox.Show("This ArcGIS Pro Project has some unsaved edits.  Please save all edits before proceeding.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro Project has no unsaved edits.  Proceeding..."), true, max, ++val);
                }

                // If editing is disabled, enable it temporarily (and disable again in the finally block)
                if (edits_are_disabled)
                {
                    if (!await Project.Current.SetIsEditingEnabledAsync(true))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to enabled editing for this ArcGIS Pro Project.", LogMessageType.ERROR), true, max, ++val);
                        ProMsgBox.Show("Unable to enabled editing for this ArcGIS Pro Project.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing enabled."), true, max, ++val);
                    }
                }

                // Do the work here!
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Generating grid tiles (maximum {tiles_across*tiles_up} potential tiles)..."), true, max, ++val);

                if (!await QueuedTask.Run(async () =>
                {
                    bool success = false;

                    try
                    {
                        var loader = new EditOperation();
                        loader.Name = "National Grid Tile Loader";
                        loader.ShowProgressor = false;
                        loader.ShowModalMessageAfterFailure = false;
                        loader.SelectNewFeatures = false;


                        // Prepare an accelerated buffer poly for use in the GeometryEngine's 'intersects' method (potentially helps speed things up)
                        Polygon accelerated_buffer = (Polygon)GeometryEngine.Instance.AccelerateForRelationalOperations(buffered_study_area);

                        // Planning Unit ID variable
                        int puid = 1;

                        // Grid values
                        int XMIN = Convert.ToInt32(gridEnvelope.XMin);
                        int YMIN = Convert.ToInt32(gridEnvelope.YMin);

                        int flusher = 0;

                        using (FeatureClass fc = await PRZH.GetFC_PU())
                        {
                            loader.Callback(async (context) =>
                            {
                                using (FeatureClass featureClass = await PRZH.GetFC_PU())
                                using (FeatureClassDefinition fcDef = featureClass.GetDefinition())
                                using (InsertCursor insertCursor = featureClass.CreateInsertCursor())
                                using (RowBuffer rowBuffer = featureClass.CreateRowBuffer())
                                {
                                    for (int row = 0; row < tiles_up; row++)
                                    {
                                        for (int col = 0; col < tiles_across; col++)
                                        {
                                            // Tile construction will be based on lower left corner XY and side length
                                            double CurrentX = XMIN + (col * side_length);
                                            double CurrentY = YMIN + (row * side_length);

                                            Envelope tileEnv = EnvelopeBuilderEx.CreateEnvelope(CurrentX, CurrentY, CurrentX + side_length, CurrentY + side_length, gridEnvelope.SpatialReference);

                                            // Determine if the tile poly intersects with the study area buffer polygon
                                            if (GeometryEngine.Instance.Intersects(accelerated_buffer, tileEnv))
                                            {
                                                // Create the polygon
                                                Polygon tilePoly = PolygonBuilderEx.CreatePolygon(tileEnv);

                                                rowBuffer[fcDef.GetShapeField()] = tilePoly;
                                                rowBuffer[PRZC.c_FLD_FC_PU_ID] = puid++;
                                                rowBuffer[PRZC.c_FLD_FC_PU_NATGRID_ID] = NationalGridInfo.GetIdentifier(Convert.ToInt32(CurrentX), Convert.ToInt32(CurrentY), dimension);
                                                rowBuffer[PRZC.c_FLD_FC_PU_AREA_M2] = tilePoly.Area;
                                                rowBuffer[PRZC.c_FLD_FC_PU_AREA_AC] = tilePoly.Area * PRZC.c_CONVERT_M2_TO_AC;
                                                rowBuffer[PRZC.c_FLD_FC_PU_AREA_HA] = tilePoly.Area * PRZC.c_CONVERT_M2_TO_HA;
                                                rowBuffer[PRZC.c_FLD_FC_PU_AREA_KM2] = tilePoly.Area * PRZC.c_CONVERT_M2_TO_KM2;
                                                rowBuffer[PRZC.c_FLD_FC_PU_CONFLICT] = 0;
                                                rowBuffer[PRZC.c_FLD_FC_PU_COST] = 0;
                                                rowBuffer[PRZC.c_FLD_FC_PU_FEATURECOUNT] = 0;
                                                rowBuffer[PRZC.c_FLD_FC_PU_HAS_UNSHARED_PERIM] = 0;
                                                rowBuffer[PRZC.c_FLD_FC_PU_SHARED_PERIM] = 0;
                                                rowBuffer[PRZC.c_FLD_FC_PU_UNSHARED_PERIM] = 0;

                                                insertCursor.Insert(rowBuffer);

                                                flusher++;

                                                if (flusher == 10000)
                                                {
                                                    insertCursor.Flush();
                                                    flusher = 0;
                                                }
                                            }
                                        }

                                        PRZH.UpdateProgress(PM, null, true, row);
                                    }

                                    insertCursor.Flush();
                                }
                            }, fc);
                        }

                        // Execute all the queued "creates"
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Executing Edit Operation!  This one might take a while..."), true, max, ++val);
                        success = loader.Execute();

                        if (success)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Saving National Grid tile imports..."), true, max, ++val);
                            if (!await Project.Current.SaveEditsAsync())
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Error saving imported tiles.", LogMessageType.ERROR), true, max, ++val);
                                ProMsgBox.Show($"Error saving imported tiles.");
                                return false;
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Imports saved."), true, max, ++val);
                            }
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Edit Operation error: unable to create tiles: {loader.ErrorMessage}", LogMessageType.ERROR), true, max, ++val);
                            ProMsgBox.Show($"Edit Operation error: unable to create tiles: {loader.ErrorMessage}");
                        }

                        return success;
                    }
                    catch (Exception ex)
                    {
                        ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                        return false;
                    }
                }))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error importing National Grid tiles.", LogMessageType.ERROR), true, val++);
                    ProMsgBox.Show($"Error importing National Grid tiles.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"National Grid tiles imported."), true, val++);
                    return true;
                }
            }
            catch (Exception ex)
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, val++);
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
            finally
            {
                // reset disabled editing status
                if (edits_are_disabled)
                {
                    await Project.Current.SetIsEditingEnabledAsync(false);
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing disabled."), true, max, ++val);
                }
            }
        }

        private async Task<bool> LoadCustomGridTiles_EditOp(PlanningUnitTileInfo tileInfo, Polygon study_area_buffer_poly)
        {
            bool edits_are_disabled = !Project.Current.IsEditingEnabled;
            int val = 0;
            int max = tileInfo.tiles_up;

            try
            {
                // Check for currently unsaved edits in the project
                if (Project.Current.HasEdits)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro Project has unsaved edits.  Please save all edits before proceeding.", LogMessageType.ERROR), true, max, ++val);
                    ProMsgBox.Show("This ArcGIS Pro Project has some unsaved edits.  Please save all edits before proceeding.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro Project has no unsaved edits.  Proceeding..."), true, max, ++val);
                }

                // If editing is disabled, enable it temporarily (and disable again in the finally block)
                if (edits_are_disabled)
                {
                    if (!await Project.Current.SetIsEditingEnabledAsync(true))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to enabled editing for this ArcGIS Pro Project.", LogMessageType.ERROR), true, max, ++val);
                        ProMsgBox.Show("Unable to enabled editing for this ArcGIS Pro Project.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing enabled."), true, max, ++val);
                    }
                }

                // Do the work here!
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Generating grid tiles (maximum {tileInfo.tiles_up * tileInfo.tiles_across} potential tiles)..."), true, max, ++val);

                if (!await QueuedTask.Run(async () =>
                {
                    bool success = false;

                    try
                    {
                        var loader = new EditOperation();
                        loader.Name = "Tile Loader";
                        loader.ShowProgressor = false;
                        loader.ShowModalMessageAfterFailure = false;
                        loader.SelectNewFeatures = false;
                        loader.SelectModifiedFeatures = false;

                        // Prepare an accelerated buffer poly for use in the GeometryEngine's 'intersects' method (potentially helps speed things up)
                        Polygon accelerated_buffer = (Polygon)GeometryEngine.Instance.AccelerateForRelationalOperations(study_area_buffer_poly);

                        // Planning Unit ID variable
                        int puid = 1;

                        double hex_horizoffset = tileInfo.tile_center_to_right + (tileInfo.tile_edge_length / 2.0);     // this is specifically for hexagon tiles
                        int flusher = 0;

                        using (FeatureClass fc = await PRZH.GetFC_PU())
                        {
                            loader.Callback(async (context) =>
                            {
                                using (FeatureClass featureClass = await PRZH.GetFC_PU())
                                using (FeatureClassDefinition fcDef = featureClass.GetDefinition())
                                using (InsertCursor insertCursor = featureClass.CreateInsertCursor())
                                using (RowBuffer rowBuffer = featureClass.CreateRowBuffer())
                                {
                                    for (int row = 0; row < tileInfo.tiles_up; row++)
                                    {
                                        for (int col = 0; col < tileInfo.tiles_across; col++)
                                        {
                                            double CurrentX;
                                            double CurrentY;
                                            Polygon tilePoly = null;

                                            switch (tileInfo.tile_shape)
                                            {
                                                case CustomGridTileShape.SQUARE:
                                                    CurrentX = tileInfo.LL_Point.X + (col * tileInfo.tile_edge_length);
                                                    CurrentY = tileInfo.LL_Point.Y + (row * tileInfo.tile_edge_length);
                                                    Envelope tileEnv = EnvelopeBuilderEx.CreateEnvelope(CurrentX, CurrentY, CurrentX + tileInfo.tile_edge_length, CurrentY + tileInfo.tile_edge_length, tileInfo.LL_Point.SpatialReference);
                                                    tilePoly = PolygonBuilderEx.CreatePolygon(tileEnv, tileInfo.LL_Point.SpatialReference);
                                                    break;

                                                case CustomGridTileShape.HEXAGON:
                                                    CurrentX = tileInfo.LL_Point.X + (col * hex_horizoffset);
                                                    CurrentY = tileInfo.LL_Point.Y + (row * (2 * tileInfo.tile_center_to_top)) - ((col % 2) * tileInfo.tile_center_to_top);

                                                    List<Coordinate2D> hexcoords = new List<Coordinate2D>();

                                                    hexcoords.Add(new Coordinate2D(CurrentX - tileInfo.tile_center_to_right, CurrentY));
                                                    hexcoords.Add(new Coordinate2D(CurrentX - (tileInfo.tile_center_to_right / 2.0), CurrentY + tileInfo.tile_center_to_top));
                                                    hexcoords.Add(new Coordinate2D(CurrentX + (tileInfo.tile_center_to_right / 2.0), CurrentY + tileInfo.tile_center_to_top));
                                                    hexcoords.Add(new Coordinate2D(CurrentX + tileInfo.tile_center_to_right, CurrentY));
                                                    hexcoords.Add(new Coordinate2D(CurrentX + (tileInfo.tile_center_to_right / 2.0), CurrentY - tileInfo.tile_center_to_top));
                                                    hexcoords.Add(new Coordinate2D(CurrentX - (tileInfo.tile_center_to_right / 2.0), CurrentY - tileInfo.tile_center_to_top));

                                                    tilePoly = PolygonBuilderEx.CreatePolygon(hexcoords, tileInfo.LL_Point.SpatialReference);
                                                    break;
                                            }

                                            // Determine if the tile poly intersects with the study area buffer polygon
                                            if (GeometryEngine.Instance.Intersects(accelerated_buffer, tilePoly))
                                            {
                                                rowBuffer[fcDef.GetShapeField()] = tilePoly;
                                                rowBuffer[PRZC.c_FLD_FC_PU_ID] = puid++;
                                                rowBuffer[PRZC.c_FLD_FC_PU_AREA_M2] = tilePoly.Area;
                                                rowBuffer[PRZC.c_FLD_FC_PU_AREA_AC] = tilePoly.Area * PRZC.c_CONVERT_M2_TO_AC;
                                                rowBuffer[PRZC.c_FLD_FC_PU_AREA_HA] = tilePoly.Area * PRZC.c_CONVERT_M2_TO_HA;
                                                rowBuffer[PRZC.c_FLD_FC_PU_AREA_KM2] = tilePoly.Area * PRZC.c_CONVERT_M2_TO_KM2;
                                                rowBuffer[PRZC.c_FLD_FC_PU_CONFLICT] = 0;
                                                rowBuffer[PRZC.c_FLD_FC_PU_COST] = 0;
                                                rowBuffer[PRZC.c_FLD_FC_PU_FEATURECOUNT] = 0;
                                                rowBuffer[PRZC.c_FLD_FC_PU_HAS_UNSHARED_PERIM] = 0;
                                                rowBuffer[PRZC.c_FLD_FC_PU_SHARED_PERIM] = 0;
                                                rowBuffer[PRZC.c_FLD_FC_PU_UNSHARED_PERIM] = 0;

                                                insertCursor.Insert(rowBuffer);

                                                flusher++;

                                                if (flusher == 10000)
                                                {
                                                    insertCursor.Flush();
                                                    flusher = 0;
                                                }
                                            }
                                        }

                                        PRZH.UpdateProgress(PM, null, true, row);
                                    }

                                    insertCursor.Flush();
                                }
                            }, fc);
                        }

                        // Execute all the queued "creates"
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Executing Edit Operation!  This one might take a while..."), true, max, ++val);
                        success = loader.Execute();

                        if (success)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Saving custom grid tile imports..."), true, max, ++val);
                            if (!await Project.Current.SaveEditsAsync())
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Error saving imported tiles.", LogMessageType.ERROR), true, max, ++val);
                                ProMsgBox.Show($"Error saving imported tiles.");
                                return false;
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Imports saved."), true, max, ++val);
                            }
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Edit Operation error: unable to create tiles: {loader.ErrorMessage}", LogMessageType.ERROR), true, max, ++val);
                            ProMsgBox.Show($"Edit Operation error: unable to create tiles: {loader.ErrorMessage}");
                        }

                        return success;
                    }
                    catch (Exception ex)
                    {
                        ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                        return false;
                    }
                }))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Tile import error.", LogMessageType.ERROR), true, val++);
                    ProMsgBox.Show($"Tile import Error.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Tiles imported."), true, val++);
                    return true;
                }
            }
            catch (Exception ex)
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, val++);
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
            finally
            {
                // reset disabled editing status
                if (edits_are_disabled)
                {
                    await Project.Current.SetIsEditingEnabledAsync(false);
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing disabled."), true, max, ++val);
                }
            }
        }

        private async Task<bool> LoadCustomGridTiles_ApplyEdits(PlanningUnitTileInfo tileInfo, Polygon study_area_buffer_poly)
        {
            bool edits_are_disabled = !Project.Current.IsEditingEnabled;
            int val = 0;
            int max = tileInfo.tiles_up;

            try
            {
                // Check for currently unsaved edits in the project
                if (Project.Current.HasEdits)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro Project has unsaved edits.  Please save all edits before proceeding.", LogMessageType.ERROR), true, max, ++val);
                    ProMsgBox.Show("This ArcGIS Pro Project has some unsaved edits.  Please save all edits before proceeding.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro Project has no unsaved edits.  Proceeding..."), true, max, ++val);
                }

                // If editing is disabled, enable it temporarily (and disable again in the finally block)
                if (edits_are_disabled)
                {
                    if (!await Project.Current.SetIsEditingEnabledAsync(true))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to enabled editing for this ArcGIS Pro Project.", LogMessageType.ERROR), true, max, ++val);
                        ProMsgBox.Show("Unable to enabled editing for this ArcGIS Pro Project.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing enabled."), true, max, ++val);
                    }
                }

                // Do the work here!
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Generating grid tiles (maximum {tileInfo.tiles_up * tileInfo.tiles_across} potential tiles)..."), true, max, ++val);

                if (!await QueuedTask.Run(async () =>
                {
                    bool success = false;

                    try
                    {
                        // Prepare an accelerated buffer poly for use in the GeometryEngine's 'intersects' method (potentially helps speed things up)
                        Polygon accelerated_buffer = (Polygon)GeometryEngine.Instance.AccelerateForRelationalOperations(study_area_buffer_poly);

                        // Planning Unit ID variable
                        int puid = 1;

                        double hex_horizoffset = tileInfo.tile_center_to_right + (tileInfo.tile_edge_length / 2.0);     // this is specifically for hexagon tiles
                        int flusher = 0;

                        using (Geodatabase geodatabase = await PRZH.GetProjectGDB())
                        using (FeatureClass featureClass = await PRZH.GetFC_PU())
                        using (FeatureClassDefinition fcDef = featureClass.GetDefinition())
                        using (InsertCursor insertCursor = featureClass.CreateInsertCursor())
                        using (RowBuffer rowBuffer = featureClass.CreateRowBuffer())
                        {
                            geodatabase.ApplyEdits(() =>
                            {
                                for (int row = 0; row < tileInfo.tiles_up; row++)
                                {
                                    for (int col = 0; col < tileInfo.tiles_across; col++)
                                    {
                                        double CurrentX;
                                        double CurrentY;
                                        Polygon tilePoly = null;

                                        switch (tileInfo.tile_shape)
                                        {
                                            case CustomGridTileShape.SQUARE:
                                                CurrentX = tileInfo.LL_Point.X + (col * tileInfo.tile_edge_length);
                                                CurrentY = tileInfo.LL_Point.Y + (row * tileInfo.tile_edge_length);
                                                Envelope tileEnv = EnvelopeBuilderEx.CreateEnvelope(CurrentX, CurrentY, CurrentX + tileInfo.tile_edge_length, CurrentY + tileInfo.tile_edge_length, tileInfo.LL_Point.SpatialReference);
                                                tilePoly = PolygonBuilderEx.CreatePolygon(tileEnv, tileInfo.LL_Point.SpatialReference);
                                                break;

                                            case CustomGridTileShape.HEXAGON:
                                                CurrentX = tileInfo.LL_Point.X + (col * hex_horizoffset);
                                                CurrentY = tileInfo.LL_Point.Y + (row * (2 * tileInfo.tile_center_to_top)) - ((col % 2) * tileInfo.tile_center_to_top);

                                                List<Coordinate2D> hexcoords = new List<Coordinate2D>();

                                                hexcoords.Add(new Coordinate2D(CurrentX - tileInfo.tile_center_to_right, CurrentY));
                                                hexcoords.Add(new Coordinate2D(CurrentX - (tileInfo.tile_center_to_right / 2.0), CurrentY + tileInfo.tile_center_to_top));
                                                hexcoords.Add(new Coordinate2D(CurrentX + (tileInfo.tile_center_to_right / 2.0), CurrentY + tileInfo.tile_center_to_top));
                                                hexcoords.Add(new Coordinate2D(CurrentX + tileInfo.tile_center_to_right, CurrentY));
                                                hexcoords.Add(new Coordinate2D(CurrentX + (tileInfo.tile_center_to_right / 2.0), CurrentY - tileInfo.tile_center_to_top));
                                                hexcoords.Add(new Coordinate2D(CurrentX - (tileInfo.tile_center_to_right / 2.0), CurrentY - tileInfo.tile_center_to_top));

                                                tilePoly = PolygonBuilderEx.CreatePolygon(hexcoords, tileInfo.LL_Point.SpatialReference);
                                                break;
                                        }

                                        // Determine if the tile poly intersects with the study area buffer polygon
                                        if (GeometryEngine.Instance.Intersects(accelerated_buffer, tilePoly))
                                        {
                                            rowBuffer[fcDef.GetShapeField()] = tilePoly;
                                            rowBuffer[PRZC.c_FLD_FC_PU_ID] = puid++;
                                            rowBuffer[PRZC.c_FLD_FC_PU_AREA_M2] = tilePoly.Area;
                                            rowBuffer[PRZC.c_FLD_FC_PU_AREA_AC] = tilePoly.Area * PRZC.c_CONVERT_M2_TO_AC;
                                            rowBuffer[PRZC.c_FLD_FC_PU_AREA_HA] = tilePoly.Area * PRZC.c_CONVERT_M2_TO_HA;
                                            rowBuffer[PRZC.c_FLD_FC_PU_AREA_KM2] = tilePoly.Area * PRZC.c_CONVERT_M2_TO_KM2;
                                            rowBuffer[PRZC.c_FLD_FC_PU_CONFLICT] = 0;
                                            rowBuffer[PRZC.c_FLD_FC_PU_COST] = 0;
                                            rowBuffer[PRZC.c_FLD_FC_PU_FEATURECOUNT] = 0;
                                            rowBuffer[PRZC.c_FLD_FC_PU_HAS_UNSHARED_PERIM] = 0;
                                            rowBuffer[PRZC.c_FLD_FC_PU_SHARED_PERIM] = 0;
                                            rowBuffer[PRZC.c_FLD_FC_PU_UNSHARED_PERIM] = 0;

                                            insertCursor.Insert(rowBuffer);

                                            flusher++;

                                            if (flusher == 10000)
                                            {
                                                insertCursor.Flush();
                                                flusher = 0;
                                            }
                                        }
                                    }

                                    PRZH.UpdateProgress(PM, null, true, row);
                                }

                                insertCursor.Flush();
                            });
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Tile import error.", LogMessageType.ERROR), true, val++);
                    ProMsgBox.Show($"Tile import Error.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Tiles imported."), true, val++);
                    return true;
                }
            }
            catch (Exception ex)
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, val++);
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
            finally
            {
                // reset disabled editing status
                if (edits_are_disabled)
                {
                    await Project.Current.SetIsEditingEnabledAsync(false);
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing disabled."), true, max, ++val);
                }
            }
        }

        private void SelectSpatialReference()
        {
            try
            {
                ProMsgBox.Show("This will eventually be the Coordinate Systems Picker dialog");
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> Test()
        {
            try
            {

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        #endregion


    }
}
















