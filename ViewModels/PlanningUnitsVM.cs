using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
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

        // lengths
        private const string c_M = "m";
        private const string c_KM = "km";

        // areas
        private const string c_M2 = "m\u00B2";
        private const string c_AC = "ac";
        private const string c_HA = "ha";
        private const string c_KM2 = "km\u00B2";


        #region PLANNING UNIT SOURCE GEOMETRY

        private bool _puSource_Rad_NatGrid_IsChecked;
        private bool _puSource_Rad_CustomGrid_IsChecked;
        private bool _puSource_Rad_Layer_IsChecked;

        private bool _puSource_Rad_CustomGrid_TileArea_IsChecked;
        private string _puSource_Txt_CustomGrid_TileArea;

        private bool _puSource_Rad_CustomGrid_TileSide_IsChecked;
        private string _puSource_Txt_CustomGrid_TileSide;

        private bool _puSource_Rad_NatGrid_1M_IsChecked;
        private bool _puSource_Rad_NatGrid_10M_IsChecked;
        private bool _puSource_Rad_NatGrid_100M_IsChecked;
        private bool _puSource_Rad_NatGrid_1Km_IsChecked;
        private bool _puSource_Rad_NatGrid_10Km_IsChecked;
        private bool _puSource_Rad_NatGrid_100Km_IsChecked;

        private Visibility _puSource_Vis_NatGrid_Controls = Visibility.Collapsed;
        private Visibility _puSource_Vis_CustomGrid_Controls = Visibility.Collapsed;
        private Visibility _puSource_Vis_Layer_Controls = Visibility.Collapsed;
        private Visibility _puSource_Vis_CustomGrid_TileArea_Controls = Visibility.Hidden;
        private Visibility _puSource_Vis_CustomGrid_TileSide_Controls = Visibility.Hidden;

        private List<FeatureLayer> _puSource_Cmb_Layer_FeatureLayers;
        private FeatureLayer _puSource_Cmb_Layer_SelectedFeatureLayer;

        private Dictionary<int, string> _puSource_Cmb_TileArea_Units;
        private KeyValuePair<int, string> _puSource_Cmb_TileArea_SelectedUnit;

        private Dictionary<int, string> _puSource_Cmb_TileSide_Units;
        private KeyValuePair<int, string> _puSource_Cmb_TileSide_SelectedUnit;

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

        #region OUTPUT FORMAT

        private bool _outputFormat_Rad_GISFormat_Vector_IsChecked;
        private bool _outputFormat_Rad_GISFormat_Raster_IsChecked;

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
        public Visibility PUSource_Vis_CustomGrid_TileArea_Controls
        {
            get => _puSource_Vis_CustomGrid_TileArea_Controls;
            set => SetProperty(ref _puSource_Vis_CustomGrid_TileArea_Controls, value, () => PUSource_Vis_CustomGrid_TileArea_Controls);
        }
        public Visibility PUSource_Vis_CustomGrid_TileSide_Controls
        {
            get => _puSource_Vis_CustomGrid_TileSide_Controls;
            set => SetProperty(ref _puSource_Vis_CustomGrid_TileSide_Controls, value, () => PUSource_Vis_CustomGrid_TileSide_Controls);
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
        public string PUSource_Txt_CustomGrid_TileSide
        {
            get => _puSource_Txt_CustomGrid_TileSide;
            set
            {
                SetProperty(ref _puSource_Txt_CustomGrid_TileSide, value, () => PUSource_Txt_CustomGrid_TileSide);
                Properties.Settings.Default.DEFAULT_TILE_SIDE = value;
                Properties.Settings.Default.Save();
            }
        }

        public Dictionary<int, string> PUSource_Cmb_TileArea_Units
        {
            get => _puSource_Cmb_TileArea_Units;
            set => SetProperty(ref _puSource_Cmb_TileArea_Units, value, () => PUSource_Cmb_TileArea_Units);
        }
        public KeyValuePair<int, string> PUSource_Cmb_TileArea_SelectedUnit
        {
            get => _puSource_Cmb_TileArea_SelectedUnit;
            set
            {
                SetProperty(ref _puSource_Cmb_TileArea_SelectedUnit, value, () => PUSource_Cmb_TileArea_SelectedUnit);
                if (!value.Equals(default(KeyValuePair<int, string>)))
                {
                    Properties.Settings.Default.DEFAULT_TILE_AREA_UNITS = value.Key;
                    Properties.Settings.Default.Save();
                }
            }
        }
        public Dictionary<int, string> PUSource_Cmb_TileSide_Units
        {
            get => _puSource_Cmb_TileSide_Units;
            set => SetProperty(ref _puSource_Cmb_TileSide_Units, value, () => PUSource_Cmb_TileSide_Units);
        }
        public KeyValuePair<int, string> PUSource_Cmb_TileSide_SelectedUnit
        {
            get => _puSource_Cmb_TileSide_SelectedUnit;
            set
            {
                SetProperty(ref _puSource_Cmb_TileSide_SelectedUnit, value, () => PUSource_Cmb_TileSide_SelectedUnit);
                if (!value.Equals(default(KeyValuePair<int, string>)))
                {
                    Properties.Settings.Default.DEFAULT_TILE_SIDE_UNITS = value.Key;
                    Properties.Settings.Default.Save();
                }
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

        public bool PUSource_Rad_CustomGrid_TileArea_IsChecked
        {
            get => _puSource_Rad_CustomGrid_TileArea_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_CustomGrid_TileArea_IsChecked, value, () => PUSource_Rad_CustomGrid_TileArea_IsChecked);
                PUSource_Vis_CustomGrid_TileArea_Controls = value ? Visibility.Visible : Visibility.Hidden;
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_PU_CUSTOMGRID_TYPE = "AREA";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool PUSource_Rad_CustomGrid_TileSide_IsChecked
        {
            get => _puSource_Rad_CustomGrid_TileSide_IsChecked;
            set
            {
                SetProperty(ref _puSource_Rad_CustomGrid_TileSide_IsChecked, value, () => PUSource_Rad_CustomGrid_TileSide_IsChecked);
                PUSource_Vis_CustomGrid_TileSide_Controls = value ? Visibility.Visible : Visibility.Hidden;
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_PU_CUSTOMGRID_TYPE = "SIDE";
                    Properties.Settings.Default.Save();
                }
            }
        }



        //public bool PUSource_Rad_TileArea_M_IsChecked
        //{
        //    get => _puSource_Rad_TileArea_M_IsChecked;
        //    set
        //    {
        //        SetProperty(ref _puSource_Rad_TileArea_M_IsChecked, value, () => PUSource_Rad_TileArea_M_IsChecked);
        //        if (value)
        //        {
        //            Properties.Settings.Default.DEFAULT_TILE_AREA_UNITS = "M";
        //            Properties.Settings.Default.Save();
        //        }
        //    }
        //}
        //public bool PUSource_Rad_TileArea_Ac_IsChecked
        //{
        //    get => _puSource_Rad_TileArea_Ac_IsChecked;
        //    set
        //    {
        //        SetProperty(ref _puSource_Rad_TileArea_Ac_IsChecked, value, () => PUSource_Rad_TileArea_Ac_IsChecked);
        //        if (value)
        //        {
        //            Properties.Settings.Default.DEFAULT_TILE_AREA_UNITS = "AC";
        //            Properties.Settings.Default.Save();
        //        }
        //    }
        //}
        //public bool PUSource_Rad_TileArea_Ha_IsChecked
        //{
        //    get => _puSource_Rad_TileArea_Ha_IsChecked;
        //    set
        //    {
        //        SetProperty(ref _puSource_Rad_TileArea_Ha_IsChecked, value, () => PUSource_Rad_TileArea_Ha_IsChecked);
        //        if (value)
        //        {
        //            Properties.Settings.Default.DEFAULT_TILE_AREA_UNITS = "HA";
        //            Properties.Settings.Default.Save();
        //        }
        //    }
        //}
        //public bool PUSource_Rad_TileArea_Km_IsChecked
        //{
        //    get => _puSource_Rad_TileArea_Km_IsChecked;
        //    set
        //    {
        //        SetProperty(ref _puSource_Rad_TileArea_Km_IsChecked, value, () => PUSource_Rad_TileArea_Km_IsChecked);
        //        if (value)
        //        {
        //            Properties.Settings.Default.DEFAULT_TILE_AREA_UNITS = "KM";
        //            Properties.Settings.Default.Save();
        //        }
        //    }
        //}
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

        #region OUTPUT FORMAT

        public bool OutputFormat_Rad_GISFormat_Vector_IsChecked
        {
            get => _outputFormat_Rad_GISFormat_Vector_IsChecked;
            set
            {
                SetProperty(ref _outputFormat_Rad_GISFormat_Vector_IsChecked, value, () => OutputFormat_Rad_GISFormat_Vector_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_OUTPUT_FORMAT = "VECTOR";
                    Properties.Settings.Default.Save();
                }
            }
        }
        public bool OutputFormat_Rad_GISFormat_Raster_IsChecked
        {
            get => _outputFormat_Rad_GISFormat_Raster_IsChecked;
            set
            {
                SetProperty(ref _outputFormat_Rad_GISFormat_Raster_IsChecked, value, () => OutputFormat_Rad_GISFormat_Raster_IsChecked);
                if (value)
                {
                    Properties.Settings.Default.DEFAULT_OUTPUT_FORMAT = "RASTER";
                    Properties.Settings.Default.Save();
                }
            }
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

                // Custom Grid Tile Type
                string customgridtype = Properties.Settings.Default.DEFAULT_PU_CUSTOMGRID_TYPE;
                if (string.IsNullOrEmpty(customgridtype) || customgridtype == "AREA")
                {
                    PUSource_Rad_CustomGrid_TileArea_IsChecked = true;
                }
                else if (customgridtype == "SIDE")
                {
                    PUSource_Rad_CustomGrid_TileSide_IsChecked = true;
                }
                else
                {
                    PUSource_Rad_CustomGrid_TileArea_IsChecked = true;
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

                // Custom Grid Tile Area Units ComboBox
                Dictionary<int, string> tileareaunits = new Dictionary<int, string>();
                tileareaunits.Add(1, "m\u00B2");
                tileareaunits.Add(2, "ac");
                tileareaunits.Add(3, "ha");
                tileareaunits.Add(4, "km\u00B2");
                PUSource_Cmb_TileArea_Units = tileareaunits;

                int saved_areaunit = Properties.Settings.Default.DEFAULT_TILE_AREA_UNITS;
                if (tileareaunits.ContainsKey(saved_areaunit))
                {
                    KeyValuePair<int, string> kvp = new KeyValuePair<int, string>(saved_areaunit, tileareaunits[saved_areaunit]);
                    PUSource_Cmb_TileArea_SelectedUnit = kvp;
                }
                else
                {
                    KeyValuePair<int, string> kvp = new KeyValuePair<int, string>(1, tileareaunits[1]);
                    PUSource_Cmb_TileArea_SelectedUnit = kvp;
                }

                // Custom Grid Tile Side
                string tile_side = Properties.Settings.Default.DEFAULT_TILE_SIDE;

                if (string.IsNullOrEmpty(tile_side))
                {
                    PUSource_Txt_CustomGrid_TileSide = "1";
                }
                else if (double.TryParse(tile_side, out double tileside))
                {
                    PUSource_Txt_CustomGrid_TileSide = (tileside <= 0) ? "1" : tile_side;
                }
                else
                {
                    PUSource_Txt_CustomGrid_TileSide = "1";
                }

                // Custom Grid Tile Side Units ComboBox
                Dictionary<int, string> tilesideunits = new Dictionary<int, string>();
                tilesideunits.Add(1, "m");
                tilesideunits.Add(2, "km");
                PUSource_Cmb_TileSide_Units = tilesideunits;

                int saved_sideunit = Properties.Settings.Default.DEFAULT_TILE_SIDE_UNITS;
                if (tilesideunits.ContainsKey(saved_sideunit))
                {
                    KeyValuePair<int, string> kvp = new KeyValuePair<int, string>(saved_sideunit, tilesideunits[saved_sideunit]);
                    PUSource_Cmb_TileSide_SelectedUnit = kvp;
                }
                else
                {
                    KeyValuePair<int, string> kvp = new KeyValuePair<int, string>(1, tilesideunits[1]);
                    PUSource_Cmb_TileSide_SelectedUnit = kvp;
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

                #region OUTPUT FORMAT

                // GIS Format radio buttons
                string gis_format = Properties.Settings.Default.DEFAULT_OUTPUT_FORMAT;
                if (string.IsNullOrEmpty(gis_format) || gis_format == "RASTER")
                {
                    OutputFormat_Rad_GISFormat_Raster_IsChecked = true;
                }
                else if (gis_format == "VECTOR")
                {
                    OutputFormat_Rad_GISFormat_Vector_IsChecked = true;
                }
                else
                {
                    OutputFormat_Rad_GISFormat_Raster_IsChecked = true;
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
            int max = 50;

            try
            {
                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

                #region VALIDATION

                // Initialize ProgressBar and Progress Log
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Planning Unit Generator..."), false, max, ++val);

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
                if (!await PRZH.GDBExists_Project())
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

                // Validation: Ensure the National db exists (if user has specified National Grid)
                string natpath = PRZH.GetPath_NationalDB();
                var result = await PRZH.GDBExists_National();
                if (PUSource_Rad_NatGrid_IsChecked)
                {
                    if (!result.exists)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> National Geodatabase not found: {natpath}", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("National Database not found at this path:" +
                                       Environment.NewLine +
                                       natpath +
                                       Environment.NewLine + Environment.NewLine +
                                       "Please specify a valid National Database.", "Validation");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> National Geodatabase is OK: {gdbpath}"), true, ++val);
                    }
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

                // Nat Grid - Misc
                int natgrid_sidelength = 0;
                NationalGridDimension dimension = NationalGridDimension.SideLength_1m;

                // Custom Grid - Misc
                double customgrid_tile_area_m2 = 0;
                double customgrid_tile_side_m = 0;

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
                        dimension = NationalGridDimension.SideLength_1m;
                        natgrid_sidelength = 1;
                    }
                    else if (PUSource_Rad_NatGrid_10M_IsChecked)
                    {
                        dimension = NationalGridDimension.SideLength_10m;
                        natgrid_sidelength = 10;
                    }
                    else if (PUSource_Rad_NatGrid_100M_IsChecked)
                    {
                        dimension = NationalGridDimension.SideLength_100m;
                        natgrid_sidelength = 100;
                    }
                    else if (PUSource_Rad_NatGrid_1Km_IsChecked)
                    {
                        dimension = NationalGridDimension.SideLength_1000m;
                        natgrid_sidelength = 1000;
                    }
                    else if (PUSource_Rad_NatGrid_10Km_IsChecked)
                    {
                        dimension = NationalGridDimension.SideLength_10000m;
                        natgrid_sidelength = 10000;
                    }
                    else if (PUSource_Rad_NatGrid_100Km_IsChecked)
                    {
                        dimension = NationalGridDimension.SideLength_100000m;
                        natgrid_sidelength = 100000;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Invalid National Grid dimension specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Invalid National Grid dimension specified.", "Validation");
                        return false;
                    }
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Validation >> National Grid dimension = {dimension}, side length (m) = {natgrid_sidelength}"), true, ++val);
                }
                else if (PUSource_Rad_CustomGrid_IsChecked)
                {
                    // Ensure that one of the two tile options have been selected
                    if (PUSource_Rad_CustomGrid_TileArea_IsChecked)
                    {
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

                            // validate the selected tile area units
                            if (PUSource_Cmb_TileArea_SelectedUnit.Equals(default(KeyValuePair<int, string>)))
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> No tile area units specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                                ProMsgBox.Show("Please specify the tile area units.", "Validation");
                                return false;
                            }

                            switch (PUSource_Cmb_TileArea_SelectedUnit.Key)
                            {
                                case 1:
                                    au = tile_area_text + " m\u00B2";
                                    break;

                                case 2:
                                    au = tile_area_text + " ac";
                                    customgrid_tile_area_m2 /= PRZC.c_CONVERT_M2_TO_AC;
                                    break;

                                case 3:
                                    au = tile_area_text + " ha";
                                    customgrid_tile_area_m2 /= PRZC.c_CONVERT_M2_TO_HA;
                                    break;

                                case 4:
                                    au = tile_area_text + " km\u00B2";
                                    customgrid_tile_area_m2 /= PRZC.c_CONVERT_M2_TO_KM2;
                                    break;

                                default:
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Invalid tile area units specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                                    ProMsgBox.Show("Invalid tile area units specified.", "Validation");
                                    return false;
                            }

                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Tile Area = " + au), true, ++val);

                            // Update the tile side length based on the selected area
                            customgrid_tile_side_m = Math.Sqrt(customgrid_tile_area_m2);
                        }
                    }
                    else if (PUSource_Rad_CustomGrid_TileSide_IsChecked)
                    {
                        // Validation: Tile Side
                        string tile_side_text = string.IsNullOrEmpty(PUSource_Txt_CustomGrid_TileSide) ? "0" : ((PUSource_Txt_CustomGrid_TileSide.Trim() == "") ? "0" : PUSource_Txt_CustomGrid_TileSide.Trim());

                        if (!double.TryParse(tile_side_text, out customgrid_tile_side_m))
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Tile Side", LogMessageType.VALIDATION_ERROR), true, ++val);
                            ProMsgBox.Show("Please specify a valid Tile Side.  Value must be numeric and greater than 0", "Validation");
                            return false;
                        }
                        else if (customgrid_tile_side_m <= 0)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Tile Side", LogMessageType.VALIDATION_ERROR), true, ++val);
                            ProMsgBox.Show("Please specify a valid Tile Side.  Value must be numeric and greater than 0", "Validation");
                            return false;
                        }
                        else
                        {
                            string su = "";

                            // validate the selected tile area units
                            if (PUSource_Cmb_TileSide_SelectedUnit.Equals(default(KeyValuePair<int, string>)))
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> No tile side units specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                                ProMsgBox.Show("Please specify the tile side units.", "Validation");
                                return false;
                            }

                            switch (PUSource_Cmb_TileSide_SelectedUnit.Key)
                            {
                                case 1:
                                    su = tile_side_text + " m";
                                    break;

                                case 2:
                                    su = tile_side_text + " km";
                                    customgrid_tile_side_m *= 1000.0;
                                    break;

                                default:
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Invalid tile side units specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                                    ProMsgBox.Show("Invalid tile side units specified.", "Validation");
                                    return false;
                            }

                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Tile Side = " + su), true, ++val);

                            // Update the tile area based on the selected side (I don't think I need this)
                            customgrid_tile_area_m2 = Math.Pow(customgrid_tile_side_m, 2);
                        }
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Custom Grid tile size not defined either by area or length.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Please specify the Custom Grid tile size, either by area or side length.", "Validation");
                        return false;
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

                #region STRIP MAP AND GDB

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Removing PRZ layers and standalone tables from map..."), true, ++val);
                if (!await PRZH.RemovePRZItemsFromMap(_map))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to remove all layers and standalone tables where source = {gdbpath}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Unable to remove PRZ layers and standalone tables from map");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"All PRZ layers and standalone tables removed from map"), true, ++val);
                }

                // Delete all Items from Project GDB
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting all objects from the PRZ project geodatabase at {gdbpath}..."), true, ++val);
                if (!await PRZH.DeleteProjectGDBContents())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to delete all objects from {gdbpath}.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Unable to delete all objects from {gdbpath}.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleted all objects from {gdbpath}."), true, ++val);
                }

                // Create the ElementPresence domain
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating the {PRZC.c_DOMAIN_ELEMENT_PRESENCE} coded value domain..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_DOMAIN_ELEMENT_PRESENCE, "", "SHORT", "CODED", "DEFAULT", "DEFAULT");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateDomain_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating {PRZC.c_DOMAIN_ELEMENT_PRESENCE} domain.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error creating {PRZC.c_DOMAIN_ELEMENT_PRESENCE} domain.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Domain created."), true, ++val);
                }

                // Add coded value #1
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding coded value 1 to the {PRZC.c_DOMAIN_ELEMENT_PRESENCE} domain..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_DOMAIN_ELEMENT_PRESENCE, (int)NationalElementPresence.Present, NationalElementPresence.Present.ToString());
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddCodedValueToDomain_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding coded value to domain.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error adding coded value to domain.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Coded value added."), true, ++val);
                }

                // Add coded value #2
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding coded value 2 to the {PRZC.c_DOMAIN_ELEMENT_PRESENCE} domain..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_DOMAIN_ELEMENT_PRESENCE, (int)NationalElementPresence.Absent, NationalElementPresence.Absent.ToString());
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddCodedValueToDomain_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding coded value to domain.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error adding coded value to domain.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Coded value added."), true, ++val);
                }

                #endregion

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

                #region CREATE STUDY AREA FEATURE CLASSES

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

                #region CREATE PLANNING UNITS

                // Prepare an accelerated buffer poly for use in the GeometryEngine's 'intersects' method (potentially helps speed things up)
                Polygon accelerated_buffer = (Polygon)GeometryEngine.Instance.AccelerateForRelationalOperations(SA_poly_buffer);
                PlanningUnitLayerType puLayerType = PlanningUnitLayerType.FEATURE;
                
                // RASTER OPTION
                if (OutputFormat_Rad_GISFormat_Raster_IsChecked)
                {
                    // Common National/Custom Raster stuff goes here...
                    puLayerType = PlanningUnitLayerType.RASTER;

                    // National Grid
                    if (PUSource_Rad_NatGrid_IsChecked)
                    {
                        // Get Extent
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving national grid extent for study area..."), true, ++val);
                        (bool success, Envelope gridEnv, string message, int tilesAcross, int tilesUp) gridBounds = NationalGrid.GetNatGridBoundsFromStudyArea(SA_poly_buffer, dimension);
                        if (!gridBounds.success)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve national grid extent.\n\nMessage: {gridBounds.message}", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Unable to retrieve national grid extent.\n\nMessage: {gridBounds.message}");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"National grid extent retrieved."), true, ++val);
                        }

                        // Build Constant Raster
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating preliminary (constant-value) raster..."), true, ++val);
                        object[] o = { PRZC.c_RAS_TEMP_1, 0, "INTEGER", natgrid_sidelength, gridBounds.gridEnv };
                        toolParams = Geoprocessing.MakeValueArray(o);
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("CreateConstantRaster_sa", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {PRZC.c_RAS_TEMP_1} raster dataset.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error creating the {PRZC.c_RAS_TEMP_1} raster dataset.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_TEMP_1} raster dataset created successfully."), true, ++val);
                        }

                        // Copy the raster to the correct bitdepth of int32 (to allow storage of humongous ID numbers
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Copying to better bit depth..."), true, ++val);

                        object[] o2 = { PRZC.c_RAS_TEMP_1, PRZC.c_RAS_TEMP_2, "", "", "1", "", "", "32_BIT_UNSIGNED", "", "", "", "", "", "" };

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

                        // Assign Planning Unit IDs
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Assign Planning Unit ID values to pixels within the buffered study area..."), true, ++val);

                        //Dictionary<int, string> identifiers_by_id = new Dictionary<int, string>();
                        Dictionary<int, long> cell_numbers_by_id = new Dictionary<int, long>();

                        if (!await QueuedTask.Run(async () =>
                        {
                            try
                            {
                                using (Geodatabase geodatabase = await PRZH.GetGDB_Project())
                                {
                                    if (!await PRZH.RasterExists(geodatabase, PRZC.c_RAS_TEMP_2))
                                    {
                                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to find {PRZC.c_RAS_TEMP_2} raster dataset."), true, ++val);
                                        ProMsgBox.Show($"Unable to find {PRZC.c_RAS_TEMP_2} raster dataset.");
                                        return false;
                                    }

                                    using (RasterDataset rasterDataset = geodatabase.OpenDataset<RasterDataset>(PRZC.c_RAS_TEMP_2))
                                    {
                                        // Get the virtual Raster from Band 1 of the Raster Dataset
                                        Raster raster = rasterDataset.CreateFullRaster();

                                        // Create a PixelBlock 1 row high, stretching across all columns
                                        PixelBlock pixelBlock = raster.CreatePixelBlock(raster.GetWidth(), 1);

                                        // Get the top left corner coords of the top left cell in the envelope (cell 0,0)
                                        int OriginCell_ULX = Convert.ToInt32(gridBounds.gridEnv.XMin);
                                        int OriginCell_ULY = Convert.ToInt32(gridBounds.gridEnv.YMax);

                                        int puid = 1;

                                        // Loop through each row of tiles (top to bottom)
                                        for (int row = 1; row <= gridBounds.tilesUp; row++)
                                        {
                                            // Read a raster row into the pixel block
                                            raster.Read(0, row - 1, pixelBlock);

                                            // write the pixel block contents to an array
                                            int[,] array = (int[,])pixelBlock.GetPixelData(0, true);

                                            // Get the Y-coord of the current row (YMax of row)
                                            double CurrentCell_ULY = OriginCell_ULY - ((row - 1) * natgrid_sidelength);

                                            // Loop through each cell (left to right) within the parent row
                                            for (int col = 1; col <= gridBounds.tilesAcross; col++)
                                            {
                                                // Get the X-coord of the current column (XMin of column)
                                                double CurrentCell_ULX = OriginCell_ULX + ((col - 1) * natgrid_sidelength);

                                                // Build the envelope for the current cell
                                                Envelope tileEnv = EnvelopeBuilder.CreateEnvelope(CurrentCell_ULX, CurrentCell_ULY - natgrid_sidelength, CurrentCell_ULX + natgrid_sidelength, CurrentCell_ULY, OutputSR);

                                                if (GeometryEngine.Instance.Intersects(accelerated_buffer, tileEnv))
                                                {
                                                    // I need to alter the pixel value here to puid++
                                                    array[col - 1, 0] = puid;
                                                    //identifiers_by_id.Add(puid, NationalGridInfo.GetIdentifierFromULXY((int)CurrentCell_ULX, (int)CurrentCell_ULY, dimension));
                                                    var cn = NationalGrid.GetCellNumberFromULXY(Convert.ToInt32(CurrentCell_ULX), Convert.ToInt32(CurrentCell_ULY), dimension);
                                                    if (cn.success)
                                                    {
                                                        cell_numbers_by_id.Add(puid, cn.cell_number);
                                                    }
                                                    puid++;
                                                }
                                            }

                                            // write the array values back to the pixel block, even if unchanged
                                            pixelBlock.SetPixelData(0, array);

                                            // write the pixel block data back to the raster
                                            raster.Write(0, row - 1, pixelBlock);

                                            PRZH.UpdateProgress(PM, null, true, gridBounds.tilesUp, row - 1);
                                        }

                                        // Persist the raster to the geodatabase
                                        raster.SaveAs(PRZC.c_RAS_TEMP_3, geodatabase, "GRID");

                                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_TEMP_3} raster dataset saved successfully."), true, max, ++val);
                                    }
                                }

                                // Convert zero values to NoData
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Converting zeros to NoData..."), true, ++val);
                                toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_TEMP_3, PRZC.c_RAS_TEMP_3, PRZC.c_RAS_PLANNING_UNITS, "VALUE = 0");
                                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                                toolOutput = await PRZH.RunGPTool("SetNull_sa", toolParams, toolEnvs, toolFlags);
                                if (toolOutput == null)
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error converting zeros to NoData.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                                    ProMsgBox.Show($"Error converting zeros to NoData.");
                                    return false;
                                }
                                else
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"NoData values written successfully."), true, ++val);
                                }

                                // Delete the temp rasters
                                string rasters_to_delete = PRZC.c_RAS_TEMP_1 + ";" + PRZC.c_RAS_TEMP_2 + ";" + PRZC.c_RAS_TEMP_3;
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

                                return true;
                            }
                            catch (Exception ex)
                            {
                                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                                return false;
                            }
                        }))
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error assigning Planning Unit IDs to pixels.", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error assigning Planning Unit IDs to pixels.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit IDs assigned."), true, ++val);
                        }

                        // Ensure I've got my new raster...
                        string puraspath = PRZH.GetPath_Raster_PU();
                        if (!await PRZH.RasterExists_PU())
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve the {PRZC.c_RAS_PLANNING_UNITS} raster.", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Unable to retrieve the {PRZC.c_RAS_PLANNING_UNITS} raster.");
                            return false;
                        }

                        // Build the Raster Attribute table...
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Building raster attribute table..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(puraspath, "OVERWRITE");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("BuildRasterAttributeTable_management", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error Building the raster attribute table for the {PRZC.c_RAS_PLANNING_UNITS} raster.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error Building the raster attribute table for the {PRZC.c_RAS_PLANNING_UNITS} raster.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Raster attribute table built successfully."), true, ++val);
                        }

                        // Add fields to Raster Attribute Table
                        string fldPUID = PRZC.c_FLD_FC_PU_ID + " LONG 'Planning Unit ID' # # #;";
                        //string fldNatGridID = PRZC.c_FLD_FC_PU_NATGRID_ID + " TEXT 'National Grid ID' 20 # #;";
                        string fldNatGridCellNum = PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER + " LONG 'National Grid Cell Number' # 0 #;";
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

                        string flds = fldPUID + fldNatGridCellNum + fldPUEffectiveRule + fldConflict + fldPUCost + fldPUAreaM + fldPUAreaAC + fldPUAreaHA + fldPUAreaKM + fldCFCount + fldSharedPerim + fldUnsharedPerim + fldHasUnsharedPerim;

                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to {PRZC.c_RAS_PLANNING_UNITS} raster attribute table..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(puraspath, flds);
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to {PRZC.c_RAS_PLANNING_UNITS} raster attribute table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error adding fields to {PRZC.c_RAS_PLANNING_UNITS} raster attribute table.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully."), true, ++val);
                        }

                        // Populate raster attribute table
                        if (!await UpdateRasterAttributeTable(gridBounds.gridEnv, gridBounds.tilesAcross, gridBounds.tilesUp, natgrid_sidelength, true, cell_numbers_by_id))
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to populate raster attribute table.", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Unable to populate raster attribute table.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Raster attribute table fields populated."), true, ++val);
                        }
                    }

                    // Custom Grid
                    else if (PUSource_Rad_CustomGrid_IsChecked)
                    {
                        // Get Extent Envelope
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving custom grid extent for study area..."), true, ++val);
                        var gridBounds = GetCustomGridBoundsFromStudyArea(SA_poly_buffer, customgrid_tile_side_m);

                        if (!gridBounds.success)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve Custom Grid extent envelope.\n{gridBounds.message}", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Unable to retrieve Custom Grid extent envelope.\n{gridBounds.message}");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Custom Grid extent envelope retrieved."), true, ++val);
                        }

                        // Retrieve the vitals of the custom grid extent envelope
                        int tiles_across = gridBounds.tilesAcross;
                        int tiles_up = gridBounds.tilesUp;
                        double side_length = customgrid_tile_side_m;

                        // Build Constant Raster
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating preliminary (constant-value) raster..."), true, ++val);
                        object[] o = { PRZC.c_RAS_TEMP_1, 0, "INTEGER", side_length, gridBounds.gridEnv };
                        toolParams = Geoprocessing.MakeValueArray(o);
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("CreateConstantRaster_sa", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {PRZC.c_RAS_TEMP_1} raster dataset.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error creating the {PRZC.c_RAS_TEMP_1} raster dataset.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_TEMP_1} raster dataset created successfully."), true, ++val);
                        }

                        // Copy the raster to the correct bitdepth of int32 (to allow storage of humongous ID numbers
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Copying to better bit depth..."), true, ++val);

                        object[] o2 = { PRZC.c_RAS_TEMP_1, PRZC.c_RAS_TEMP_2, "", "", "1", "", "", "32_BIT_UNSIGNED", "", "", "", "", "", "" };

                        toolParams = Geoprocessing.MakeValueArray(o2);
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("CopyRaster_management", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying the {PRZC.c_RAS_TEMP_2} raster dataset.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error copying the {PRZC.c_RAS_TEMP_2} raster dataset.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_TEMP_2} raster dataset copied successfully."), true, ++val);
                        }

                        // Assign Planning Unit IDs
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Assign Planning Unit ID values to pixels within the buffered study area..."), true, ++val);

                        if (!await QueuedTask.Run(async () =>
                        {
                            try
                            {
                                using (Geodatabase geodatabase = await PRZH.GetGDB_Project())
                                {
                                    if (!await PRZH.RasterExists(geodatabase, PRZC.c_RAS_TEMP_2))
                                    {
                                        return false;
                                    }

                                    using (RasterDataset rasterDataset = geodatabase.OpenDataset<RasterDataset>(PRZC.c_RAS_TEMP_2))
                                    {
                                        // Get the virtual Raster from Band 1 of the Raster Dataset
                                        int[] bands = { 0 };
                                        Raster raster = rasterDataset.CreateFullRaster();

                                        // Create a PixelBlock 1 row high, stretching across all columns
                                        PixelBlock pixelBlock = raster.CreatePixelBlock(raster.GetWidth(), 1);

                                        // Get the top left corner coords of the top left cell in the envelope (cell 0,0)
                                        double OriginCell_ULX = gridBounds.gridEnv.XMin;
                                        double OriginCell_ULY = gridBounds.gridEnv.YMax;

                                        int puid = 1;

                                        // Loop through each row of tiles (top to bottom)
                                        for (int row = 1; row <= gridBounds.tilesUp; row++)
                                        {
                                            // Read a raster row into the pixel block
                                            raster.Read(0, row - 1, pixelBlock);

                                            // write the pixel block contents to an array
                                            int[,] array = (int[,])pixelBlock.GetPixelData(0, true);

                                            // Get the Y-coord of the current row (YMax of row)
                                            double CurrentCell_ULY = OriginCell_ULY - ((row - 1) * side_length);

                                            // Loop through each cell (left to right) within the parent row
                                            for (int col = 1; col <= gridBounds.tilesAcross; col++)
                                            {
                                                // Get the X-coord of the current column (XMin of column)
                                                double CurrentCell_ULX = OriginCell_ULX + ((col - 1) * side_length);

                                                // Build the envelope for the current cell
                                                Envelope tileEnv = EnvelopeBuilder.CreateEnvelope(CurrentCell_ULX, CurrentCell_ULY - side_length, CurrentCell_ULX + side_length, CurrentCell_ULY, OutputSR);

                                                if (GeometryEngine.Instance.Intersects(accelerated_buffer, tileEnv))
                                                {
                                                    // I need to alter the pixel value here to puid++
                                                    array[col - 1, 0] = puid++;
                                                }
                                            }

                                            // write the array values back to the pixel block, even if unchanged
                                            pixelBlock.SetPixelData(0, array);

                                            // write the pixel block data back to the raster
                                            raster.Write(0, row - 1, pixelBlock);

                                            PRZH.UpdateProgress(PM, null, true, gridBounds.tilesUp, row - 1);
                                        }

                                        // Persist the raster to the geodatabase
                                        raster.SaveAs(PRZC.c_RAS_TEMP_3, geodatabase, "GRID");

                                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_TEMP_3} raster dataset saved successfully."), true, max, ++val);
                                    }
                                }

                                // Convert zero values to NoData
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Converting zeros to NoData..."), true, ++val);
                                toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_TEMP_3, PRZC.c_RAS_TEMP_3, PRZC.c_RAS_PLANNING_UNITS, "VALUE = 0");
                                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                                toolOutput = await PRZH.RunGPTool("SetNull_sa", toolParams, toolEnvs, toolFlags);
                                if (toolOutput == null)
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error converting zeros to NoData.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                                    ProMsgBox.Show($"Error converting zeros to NoData.");
                                    return false;
                                }
                                else
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"NoData values written successfully."), true, ++val);
                                }

                                // Delete the temp rasters
                                string rasters_to_delete = PRZC.c_RAS_TEMP_1 + ";" + PRZC.c_RAS_TEMP_2 + ";" + PRZC.c_RAS_TEMP_3;
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

                                return true;
                            }
                            catch (Exception ex)
                            {
                                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                                return false;
                            }
                        }))
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error assigning Planning Unit IDs to pixels.", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error assigning Planning Unit IDs to pixels.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Planning Unit IDs assigned."), true, ++val);
                        }

                        // Ensure I've got my new raster...
                        string puraspath = PRZH.GetPath_Raster_PU();
                        if (!await PRZH.RasterExists_PU())
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve the {PRZC.c_RAS_PLANNING_UNITS} raster.", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Unable to retrieve the {PRZC.c_RAS_PLANNING_UNITS} raster.");
                            return false;
                        }

                        // Build the Raster Attribute table...
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Building raster attribute table..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(puraspath, "OVERWRITE");
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("BuildRasterAttributeTable_management", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error Building the raster attribute table for the {PRZC.c_RAS_PLANNING_UNITS} raster.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error Building the raster attribute table for the {PRZC.c_RAS_PLANNING_UNITS} raster.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Raster attribute table built successfully."), true, ++val);
                        }

                        // Add fields to Raster Attribute Table
                        string fldPUID = PRZC.c_FLD_FC_PU_ID + " LONG 'Planning Unit ID' # # #;";
                        //string fldNatGridID = PRZC.c_FLD_FC_PU_NATGRID_ID + " TEXT 'National Grid ID' 20 # #;";
                        string fldNatGridCellNum = PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER + " LONG 'National Grid Cell Number' # 0 #;";
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

                        string flds = fldPUID + fldNatGridCellNum + fldPUEffectiveRule + fldConflict + fldPUCost + fldPUAreaM + fldPUAreaAC + fldPUAreaHA + fldPUAreaKM + fldCFCount + fldSharedPerim + fldUnsharedPerim + fldHasUnsharedPerim;

                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to {PRZC.c_RAS_PLANNING_UNITS} raster attribute table..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(puraspath, flds);
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                        toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to {PRZC.c_RAS_PLANNING_UNITS} raster attribute table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Error adding fields to {PRZC.c_RAS_PLANNING_UNITS} raster attribute table.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully."), true, ++val);
                        }

                        // Populate raster attribute table
                        if (!await UpdateRasterAttributeTable(gridBounds.gridEnv, tiles_across, tiles_up, side_length, false))
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to populate raster attribute table.", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Unable to populate raster attribute table.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Raster attribute table fields populated."), true, ++val);
                        }
                    }

                    // Features
                    else if (PUSource_Rad_Layer_IsChecked)
                    {
                        // TODO: Implement a Raster-based feature planning unit
                    }
                }

                // VECTOR OPTION
                else if (OutputFormat_Rad_GISFormat_Vector_IsChecked)
                {
                    puLayerType = PlanningUnitLayerType.FEATURE;

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
                    //string fldNatGridID = PRZC.c_FLD_FC_PU_NATGRID_ID + " TEXT 'National Grid ID' 20 # #;";
                    string fldNatGridCellNum = PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER + " LONG 'National Grid Cell Number' # 0 #;";
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

                    string flds = fldPUID + fldNatGridCellNum + fldPUEffectiveRule + fldConflict + fldPUCost + fldPUAreaM + fldPUAreaAC + fldPUAreaHA + fldPUAreaKM + fldCFCount + fldSharedPerim + fldUnsharedPerim + fldHasUnsharedPerim;

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

                    // National Grid
                    if (PUSource_Rad_NatGrid_IsChecked)
                    {
                        // Retrieve the required grid extent based on the specified National Grid dimension and the buffered study area
                        var res = NationalGrid.GetNatGridBoundsFromStudyArea(SA_poly_buffer, dimension);

                        if (!res.success)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve National Grid extent envelope.\n{res.message}", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Unable to retrieve National Grid extent envelope.\n{res.message}");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Retrieved National Grid extent envelope for study area."), true, ++val);
                        }
                        //ProMsgBox.Show($"Dimension: {dimension}\nXMin: {res.gridEnv.XMin}\nYMin: {res.gridEnv.YMin}\nXMax: {res.gridEnv.XMax}\nYMax: {res.gridEnv.YMax}\nTilesAcross: {res.tilesAcross}\nTiles Up: {res.tilesUp}\nSide Length: {natgrid_sidelength}");

                        // Load the National Grid Tiles
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Loading the National Grid tiles"), true, ++val);
                        if (!await LoadNationalGridTiles(SA_poly_buffer, res.gridEnv, res.tilesAcross, res.tilesUp, natgrid_sidelength, dimension))
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error loading tiles", LogMessageType.ERROR), true, PM.Current);
                            ProMsgBox.Show($"Error loading tiles.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"National Grid tiles loaded"), true, PM.Current);
                        }

                        val = PM.Current;
                    }

                    // Custom Grid
                    else if (PUSource_Rad_CustomGrid_IsChecked)
                    {
                        // Retrieve the required grid extent based on the custom grid side length and buffered study area
                        var res = GetCustomGridBoundsFromStudyArea(SA_poly_buffer, customgrid_tile_side_m);

                        if (!res.success)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve Custom Grid extent envelope.\n{res.message}", LogMessageType.ERROR), true, ++val);
                            ProMsgBox.Show($"Unable to retrieve Custom Grid extent envelope.\n{res.message}");
                            return false;
                        }

                        // Retrieve the vitals of the custom grid extent envelope
                        int tiles_across = res.tilesAcross;
                        int tiles_up = res.tilesUp;
                        double side_length = customgrid_tile_side_m;

                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Importing tiles into {PRZC.c_FC_PLANNING_UNITS} feature class..."), true, ++val);
                        if (!await LoadCustomGridTiles_EditOp(SA_poly_buffer, res.gridEnv, tiles_across, tiles_up, side_length))
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Error loading tiles...", LogMessageType.ERROR), true, PM.Current);
                            ProMsgBox.Show("Error loading tiles.");
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Tiles imported successfully."), true, PM.Current);
                        }

                        val = PM.Current;
                    }

                    // Features
                    else if (PUSource_Rad_Layer_IsChecked)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"unsupported option", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"unsupported option");
                        return false;
                    }

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
                }

                #endregion

                #region NATIONAL DATABASE

                if (PUSource_Rad_NatGrid_IsChecked)
                {
                    if (!await ProcessNationalDbTables(puLayerType))
                    {
                        ProMsgBox.Show("Error processing National Tables");
                        return false;
                    }
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
                if (!await PRZH.RedrawPRZLayers(_map))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error redrawing the PRZ layers.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error redrawing the PRZ layers.");
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

        private async Task<bool> LoadNationalGridTiles(Polygon buffered_study_area, Envelope gridEnvelope, int tiles_across, int tiles_up, int side_length, NationalGridDimension dimension)
        {
            bool edits_are_disabled = !Project.Current.IsEditingEnabled;
            int val = PM.Current;
            int max = PM.Max;

            try
            {
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

                // If editing is disabled, enable it temporarily (and disable again in the finally block)
                if (edits_are_disabled)
                {
                    if (!await Project.Current.SetIsEditingEnabledAsync(true))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to enabled editing for this ArcGIS Pro Project.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Unable to enabled editing for this ArcGIS Pro Project.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing enabled."), true, ++val);
                    }
                }

                // Do the work here!
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Generating grid tiles (maximum {tiles_across*tiles_up} potential tiles)..."), true, ++val);

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
                        loader.SelectModifiedFeatures = false;

                        // Prepare an accelerated buffer poly for use in the GeometryEngine's 'intersects' method (potentially helps speed things up)
                        Polygon accelerated_buffer = (Polygon)GeometryEngine.Instance.AccelerateForRelationalOperations(buffered_study_area);

                        // Grid Origin:  Get top left corner coords of the top left tile in the envelope
                        double OriginTile_ULX = gridEnvelope.XMin;
                        double OriginTile_ULY = gridEnvelope.YMax;

                        int puid = 1;   // planning unit id 
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
                                    for (int row = 1; row <= tiles_up; row++)
                                    {
                                        for (int col = 1; col <= tiles_across; col++)
                                        {
                                            // Tile construction will be based on the upper left corner XY of the tile
                                            double CurrentTile_ULX = OriginTile_ULX + ((col - 1) * side_length);
                                            double CurrentTile_ULY = OriginTile_ULY - ((row - 1) * side_length);

                                            Envelope tileEnv = EnvelopeBuilder.CreateEnvelope(CurrentTile_ULX, CurrentTile_ULY - side_length, CurrentTile_ULX + side_length, CurrentTile_ULY, gridEnvelope.SpatialReference);

                                            // Determine if the tile poly intersects with the study area buffer polygon
                                            if (GeometryEngine.Instance.Intersects(accelerated_buffer, tileEnv))
                                            {
                                                // Create the polygon
                                                Polygon tilePoly = PolygonBuilderEx.CreatePolygon(tileEnv, gridEnvelope.SpatialReference);

                                                rowBuffer[fcDef.GetShapeField()] = tilePoly;
                                                rowBuffer[PRZC.c_FLD_FC_PU_ID] = puid++;
                                                //rowBuffer[PRZC.c_FLD_FC_PU_NATGRID_ID] = NationalGridInfo.GetIdentifierFromULXY(Convert.ToInt32(CurrentTile_ULX), Convert.ToInt32(CurrentTile_ULY), dimension);

                                                var cn = NationalGrid.GetCellNumberFromULXY(Convert.ToInt32(CurrentTile_ULX), Convert.ToInt32(CurrentTile_ULY), dimension);
                                                if (cn.success)
                                                {
                                                    rowBuffer[PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER] = cn.cell_number;
                                                }

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

                                        PRZH.UpdateProgress(PM, null, true, tiles_up, row - 1);
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error importing National Grid tiles.", LogMessageType.ERROR), true, max, val++);
                    ProMsgBox.Show($"Error importing National Grid tiles.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"National Grid tiles imported."), true, max, val++);
                    return true;
                }
            }
            catch (Exception ex)
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, max, val++);
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

        private async Task<bool> LoadCustomGridTiles_EditOp(Polygon buffered_study_area, Envelope gridEnvelope, int tiles_across, int tiles_up, double side_length)
        {
            bool edits_are_disabled = !Project.Current.IsEditingEnabled;
            int val = PM.Current;
            int max = PM.Max;

            try
            {
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

                // If editing is disabled, enable it temporarily (and disable again in the finally block)
                if (edits_are_disabled)
                {
                    if (!await Project.Current.SetIsEditingEnabledAsync(true))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to enabled editing for this ArcGIS Pro Project.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Unable to enabled editing for this ArcGIS Pro Project.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing enabled."), true, ++val);
                    }
                }

                // Do the work here!
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Generating grid tiles (maximum {tiles_up * tiles_across} potential tiles)..."), true, ++val);

                if (!await QueuedTask.Run(async () =>
                {
                    bool success = false;

                    try
                    {
                        var loader = new EditOperation();
                        loader.Name = "Custom Grid Tile Loader";
                        loader.ShowProgressor = false;
                        loader.ShowModalMessageAfterFailure = false;
                        loader.SelectNewFeatures = false;
                        loader.SelectModifiedFeatures = false;

                        // Prepare an accelerated buffer poly for use in the GeometryEngine's 'intersects' method (potentially helps speed things up)
                        Polygon accelerated_buffer = (Polygon)GeometryEngine.Instance.AccelerateForRelationalOperations(buffered_study_area);

                        // Grid Origin:  Get top left corner coords of the top left tile in the envelope
                        double OriginTile_ULX = gridEnvelope.XMin;
                        double OriginTile_ULY = gridEnvelope.YMax;

                        int puid = 1;   // planning unit id
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
                                    for (int row = 1; row <= tiles_up; row++)
                                    {
                                        for (int col = 1; col <= tiles_across; col++)
                                        {
                                            // Tile construction will be based on the upper left corner XY of the tile
                                            double CurrentTile_ULX = OriginTile_ULX + ((col - 1) * side_length);
                                            double CurrentTile_ULY = OriginTile_ULY - ((row - 1) * side_length);

                                            Envelope tileEnv = EnvelopeBuilder.CreateEnvelope(CurrentTile_ULX, CurrentTile_ULY - side_length, CurrentTile_ULX + side_length, CurrentTile_ULY, gridEnvelope.SpatialReference);

                                            // Determine if the tile poly intersects with the study area buffer polygon
                                            if (GeometryEngine.Instance.Intersects(accelerated_buffer, tileEnv))
                                            {
                                                // create the polygon
                                                Polygon tilePoly = PolygonBuilder.CreatePolygon(tileEnv, gridEnvelope.SpatialReference);

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

                                        PRZH.UpdateProgress(PM, null, true, tiles_up, row - 1);
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Tile import error.", LogMessageType.ERROR), true, max, val++);
                    ProMsgBox.Show($"Tile import Error.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Tiles imported."), true, max, val++);
                    return true;
                }
            }
            catch (Exception ex)
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, max, val++);
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

        private (bool success, Envelope gridEnv, string message, int tilesAcross, int tilesUp) GetCustomGridBoundsFromStudyArea(Geometry sa_geom, double side_length)
        {
            try
            {
                #region VALIDATION

                // Ensure that side length is valid
                if (side_length <= 0)
                {
                    return (false, null, "Side length is <= 0", 0, 0);
                }

                // Ensure that geometry is not null or empty
                if (sa_geom == null || sa_geom.IsEmpty)
                {
                    return (false, null, "Geometry is null or empty", 0, 0);
                }

                // Ensure the geometry is either an envelope or a polygon
                if (!(sa_geom is Envelope | sa_geom is Polygon))
                {
                    return (false, null, "Geometry is not of type envelope or polygon", 0, 0);
                }

                // Simplify the geometry
                if (!GeometryEngine.Instance.IsSimpleAsFeature(sa_geom))
                {
                    sa_geom = GeometryEngine.Instance.SimplifyAsFeature(sa_geom);
                }

                #endregion

                #region GENERATE OUTPUT ENVELOPE

                // Get the study area envelope
                Envelope sa_envelope = sa_geom.Extent;

                // get some dimensions
                double envWidth = sa_envelope.Width;
                double envHeight = sa_envelope.Height;

                // X direction
                double tiles_across_float = envWidth / side_length;
                double leftover_distance_across = envWidth % side_length;   // distance in meters

                // Y direction
                double tiles_up_float = envHeight / side_length;
                double leftover_distance_up = envHeight % side_length;      // distance in meters

                int tiles_up = 0;
                int tiles_across = 0;

                double X_adjustment = 0;
                double Y_adjustment = 0;

                // Process the Width
                if (leftover_distance_across > 0)
                {
                    tiles_across = (int)Math.Ceiling(tiles_across_float);   // bump up tiles across by one
                    X_adjustment = (side_length - leftover_distance_across) / 2.0;
                }
                else
                {
                    tiles_across = (int)tiles_across_float;
                }

                // Process the Height
                if (leftover_distance_up > 0)
                {
                    tiles_up = (int)Math.Ceiling(tiles_up_float);
                    Y_adjustment = (side_length - leftover_distance_up) / 2.0;
                }
                else
                {
                    tiles_up = (int)tiles_up_float;
                }

                // generate Output Envelope
                double outputXMin = sa_envelope.XMin - X_adjustment;
                double outputXMax = sa_envelope.XMax + X_adjustment;
                double outputYMin = sa_envelope.YMin - Y_adjustment;
                double outputYMax = sa_envelope.YMax + Y_adjustment;

                Envelope outputEnv = EnvelopeBuilderEx.CreateEnvelope(outputXMin, outputYMin, outputXMax, outputYMax, sa_envelope.SpatialReference);

                #endregion

                return (true, outputEnv, "Success", tiles_across, tiles_up);
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return (false, null, ex.Message, 0, 0);
            }
        }

        private async Task<bool> UpdateRasterAttributeTable(Envelope gridEnv, int tiles_across, int tiles_up, double side_length, bool natgrid, Dictionary<int, long> cell_numbers_by_id = default(Dictionary<int, long>))
        {
            bool edits_are_disabled = !Project.Current.IsEditingEnabled;
            int val = PM.Current;
            int max = PM.Max;

            try
            {
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

                // If editing is disabled, enable it temporarily (and disable again in the finally block)
                if (edits_are_disabled)
                {
                    if (!await Project.Current.SetIsEditingEnabledAsync(true))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to enabled editing for this ArcGIS Pro Project.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Unable to enabled editing for this ArcGIS Pro Project.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing enabled."), true, ++val);
                    }
                }

                // Do the work here!
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Updating raster attribute table..."), true, ++val);

                if (!await QueuedTask.Run(async () =>
                {
                    bool success = false;

                    try
                    {
                        var loader = new EditOperation();
                        loader.Name = "Raster Attribute Updater";
                        loader.ShowProgressor = false;
                        loader.ShowModalMessageAfterFailure = false;
                        loader.SelectNewFeatures = false;
                        loader.SelectModifiedFeatures = false;

                        using (Table tab = (await PRZH.GetRaster_PU()).CreateFullRaster().GetAttributeTable())
                        {
                            loader.Callback(async (context) =>
                            {
                                using (RasterDataset rasterDataset = await PRZH.GetRaster_PU())
                                using (Raster raster = rasterDataset.CreateFullRaster())
                                using (Table table = raster.GetAttributeTable())
                                using (RowCursor rowCursor = table.Search(null, false))
                                {
                                    while (rowCursor.MoveNext())
                                    {
                                        using (Row row = rowCursor.Current)
                                        {
                                            int puid = Convert.ToInt32(row[PRZC.c_FLD_RAS_PU_VALUE]);

                                            double area_m2 = side_length * side_length;

                                            row[PRZC.c_FLD_RAS_PU_ID] = puid;
                                            row[PRZC.c_FLD_RAS_PU_CONFLICT] = 0;
                                            row[PRZC.c_FLD_RAS_PU_COST] = 1;
                                            row[PRZC.c_FLD_RAS_PU_AREA_M2] = area_m2;
                                            row[PRZC.c_FLD_RAS_PU_AREA_AC] = area_m2 * PRZC.c_CONVERT_M2_TO_AC;
                                            row[PRZC.c_FLD_RAS_PU_AREA_HA] = area_m2 * PRZC.c_CONVERT_M2_TO_HA;
                                            row[PRZC.c_FLD_RAS_PU_AREA_KM2] = area_m2 * PRZC.c_CONVERT_M2_TO_KM2;
                                            row[PRZC.c_FLD_RAS_PU_FEATURECOUNT] = 0;
                                            row[PRZC.c_FLD_RAS_PU_SHARED_PERIM] = 0;
                                            row[PRZC.c_FLD_RAS_PU_UNSHARED_PERIM] = 0;
                                            row[PRZC.c_FLD_RAS_PU_HAS_UNSHARED_PERIM] = 0;

                                            if (natgrid)
                                            {
                                                //row[PRZC.c_FLD_RAS_PU_NATGRID_ID] = identifiers_by_id[puid];
                                                row[PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER] = cell_numbers_by_id[puid];
                                            }

                                            row.Store();
                                            context.Invalidate(row);
                                        }
                                    }
                                }
                            }, tab);
                        }

                        // Execute all the queued "creates"
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Executing Edit Operation!  This one might take a while..."), true, max, ++val);
                        success = loader.Execute();

                        if (success)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Performing attribute edits..."), true, max, ++val);
                            if (!await Project.Current.SaveEditsAsync())
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Error performing attribute edits.", LogMessageType.ERROR), true, max, ++val);
                                ProMsgBox.Show($"Error performing attribute edits.");
                                return false;
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Edits performed."), true, max, ++val);
                            }
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Edit Operation error: unable to update raster attribute table: {loader.ErrorMessage}", LogMessageType.ERROR), true, max, ++val);
                            ProMsgBox.Show($"Edit Operation error: unable to update raster attribute table: {loader.ErrorMessage}");
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error updating raster attribute table.", LogMessageType.ERROR), true, max, val++);
                    ProMsgBox.Show($"Error updating raster attribute table.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Raster attribute table updated."), true, max, val++);
                    return true;
                }
            }
            catch (Exception ex)
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, max, val++);
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

        private async Task<bool> ProcessNationalDbTables(PlanningUnitLayerType puLayerType)
        {
            bool edits_are_disabled = !Project.Current.IsEditingEnabled;
            int val = PM.Current;
            int max = PM.Max;

            try
            {
                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

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

                // If editing is disabled, enable it temporarily (and disable again in the finally block)
                if (edits_are_disabled)
                {
                    if (!await Project.Current.SetIsEditingEnabledAsync(true))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to enabled editing for this ArcGIS Pro Project.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Unable to enabled editing for this ArcGIS Pro Project.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing enabled."), true, ++val);
                    }
                }

                #region RETRIEVE AND PREPARE INFO FROM NATIONAL DATABASE

                // COPY THE ELEMENT TABLE
                string gdbpath = PRZH.GetPath_ProjectGDB();
                string natdbpath = PRZH.GetPath_NationalDB();

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Copying {PRZC.c_TABLE_NAT_ELEMENTS} Table..."), true, ++val);
                string inputelempath = Path.Combine(natdbpath, PRZC.c_TABLE_NAT_ELEMENTS);
                toolParams = Geoprocessing.MakeValueArray(inputelempath, PRZC.c_TABLE_NAT_ELEMENTS, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("Copy_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying {PRZC.c_TABLE_NAT_ELEMENTS} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error copying {PRZC.c_TABLE_NAT_ELEMENTS} table.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Table copied successfully."), true, ++val);
                }

                // INSERT EXTRA FIELDS INTO ELEMENT TABLE
                string fldPresence = PRZC.c_FLD_TAB_ELEMENT_PRESENCE + " SHORT 'Presence' # 2 '" + PRZC.c_DOMAIN_ELEMENT_PRESENCE + "';";
                string flds = fldPresence;

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to the local {PRZC.c_TABLE_NAT_ELEMENTS} table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_TABLE_NAT_ELEMENTS, flds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to the local {PRZC.c_TABLE_NAT_ELEMENTS} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error adding fields to the local {PRZC.c_TABLE_NAT_ELEMENTS} table.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully."), true, ++val);
                }

                // COPY THE THEMES TABLE
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Copying {PRZC.c_TABLE_NAT_THEMES} Table..."), true, ++val);
                string inputthemepath = Path.Combine(natdbpath, PRZC.c_TABLE_NAT_THEMES);
                toolParams = Geoprocessing.MakeValueArray(inputthemepath, PRZC.c_TABLE_NAT_THEMES, "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("Copy_management", toolParams, toolEnvs, toolFlags);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying {PRZC.c_TABLE_NAT_THEMES} table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error copying {PRZC.c_TABLE_NAT_THEMES} table.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Table copied successfully."), true, ++val);
                }

                // Get the National Themes (list of NatTheme objects sorted by Theme ID)
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving national themes..."), true, ++val);
                var theme_outcome = await PRZH.GetNationalThemes();
                if (!theme_outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving national themes.\n{theme_outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving national themes.\n{theme_outcome.message}");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved {theme_outcome.themes.Count} national themes."), true, ++val);
                }
                List<NatTheme> themes = theme_outcome.themes;

                // Get the Active National Elements
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving national elements..."), true, ++val);
                var elem_outcome = await PRZH.GetNationalElements();
                if (!elem_outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error retrieving national elements.\n{elem_outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error retrieving national elements.\n{elem_outcome.message}");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved {elem_outcome.elements.Count} national elements."), true, ++val);
                }
                List<NatElement> elements = elem_outcome.elements;

                #endregion

                #region RETRIEVE INTERSECTING ELEMENTS

                // Iterate through the Planning Unit Attribute Table (Raster or Feature) and copy the cell numbers into a hashset

                var outcome = await PRZH.GetCellNumbers();
                if (!outcome.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve cell numbers\n{outcome.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Unable to retrieve cell numbers\n{outcome.message}");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieved hashset with {outcome.cell_numbers.Count} cell numbers."), true, ++val);
                }

                HashSet<long> puCellNumbers = outcome.cell_numbers;

                List<int> elements_with_intersection = new List<int>();

                foreach (var element in elements)
                {
                    var (result, dict) = await PRZH.GetElementTableIntersection(element.ElementTable, puCellNumbers);

                    // attempt to get intersection failed
                    if (!result)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve values from the {element.ElementTable} table.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Unable to retrieve values from the {element.ElementTable} table.");
                        return false;
                    }

                    // no intersection
                    else if (dict.Count == 0)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{element.ElementTable} table: no intersection with planning units."), true, ++val);
                        continue;
                    }

                    // intersection!
                    else
                    {
                        elements_with_intersection.Add(element.ElementID);
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"{element.ElementTable} table: intersection with {dict.Keys.Count} planning units."), true, ++val);
                    }

                    // Create the table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating the {element.ElementTable} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(gdbpath, element.ElementTable, "", "", "Element " + element.ElementID.ToString("D5"));
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("CreateTable_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {element.ElementTable} table.  GP Tool failed or was cancelled by user.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error creating the {element.ElementTable} table.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Created the {element.ElementTable} table."), true, ++val);
                    }

                    // Add fields to the table
                    string fldCellNum = PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER + " LONG 'Cell Number' # 0 #;";
                    string fldCellVal = PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_VALUE + " DOUBLE 'Cell Value' # 0 #;";

                    string fields = fldCellNum + fldCellVal;

                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to the {element.ElementTable} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(element.ElementTable, fields);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
                    toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to the {element.ElementTable} table.  GP Tool failed or was cancelled by user.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error adding fields to the {element.ElementTable} table.");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Fields added successfully."), true, ++val);
                    }

                    // add record for each KVP in dict
                    if (!await QueuedTask.Run(async () =>
                    {
                        bool success = false;

                        try
                        {
                            var loader = new EditOperation();
                            loader.Name = "Element Table Inserts";
                            loader.ShowProgressor = false;
                            loader.ShowModalMessageAfterFailure = false;
                            loader.SelectNewFeatures = false;
                            loader.SelectModifiedFeatures = false;

                            int flusher = 0;

                            using (Table tab = await PRZH.GetTable(element.ElementTable))
                            {
                                loader.Callback(async (context) =>
                                {
                                    using (Table table = await PRZH.GetTable(element.ElementTable))
                                    using (InsertCursor insertCursor = table.CreateInsertCursor())
                                    using (RowBuffer rowBuffer = table.CreateRowBuffer())
                                    {
                                        foreach(var kvp in dict)
                                        {
                                            rowBuffer[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER] = kvp.Key;
                                            rowBuffer[PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_VALUE] = kvp.Value;

                                            insertCursor.Insert(rowBuffer);

                                            flusher++;

                                            if (flusher == 10000)
                                            {
                                                insertCursor.Flush();
                                                flusher = 0;
                                            }
                                        }

                                        insertCursor.Flush();
                                    }
                                }, tab);
                            }

                            // Execute all the queued "creates"
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Executing Edit Operation - row inserts into {element.ElementTable}..."), true, max, ++val);
                            success = loader.Execute();

                            if (!success)
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Edit Operation error: unable to add rows to {element.ElementTable}", LogMessageType.ERROR), true, max, ++val);
                                ProMsgBox.Show($"Edit Operation error: unable to add rows to {element.ElementTable}");
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Rows added to {element.ElementTable}."), true, max, ++val);
                                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Saving new rows in {element.ElementTable}..."), true, max, ++val);
                                if (!await Project.Current.SaveEditsAsync())
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error saving new rows.", LogMessageType.ERROR), true, max, ++val);
                                    ProMsgBox.Show($"Error saving new rows.");
                                    return false;
                                }
                                else
                                {
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog("New rows saved."), true, max, ++val);
                                }
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
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Edit Operation error: unable to add rows to {element.ElementTable}", LogMessageType.ERROR), true, max, ++val);
                        ProMsgBox.Show($"Edit Operation error: unable to add rows to {element.ElementTable}");
                        return false;
                    }

                    // index the cell number field
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing {PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER} field in the {element.ElementTable} table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(element.ElementTable, PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER, "ix" + PRZC.c_FLD_TAB_NAT_ELEMVAL_CELL_NUMBER, "", "");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, overwriteoutput: true);
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
                }

                #endregion

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Found {elements_with_intersection.Count} intersecting elements."), true, ++val);

                #region UPDATE THE LOCAL ELEMENT TABLE PRESENCE FIELD

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Updating {PRZC.c_FLD_TAB_ELEMENT_PRESENCE} field in local {PRZC.c_TABLE_NAT_ELEMENTS} table..."), true, ++val);

                if (!await QueuedTask.Run(async () =>
                {
                    bool success = false;

                    try
                    {
                        var loader = new EditOperation();
                        loader.Name = $"{PRZC.c_FLD_TAB_ELEMENT_PRESENCE} field updater";
                        loader.ShowProgressor = false;
                        loader.ShowModalMessageAfterFailure = false;
                        loader.SelectNewFeatures = false;
                        loader.SelectModifiedFeatures = false;

                        using (Table tab = await PRZH.GetTable(PRZC.c_TABLE_NAT_ELEMENTS))
                        {
                            loader.Callback(async (context) =>
                            {
                                using (Table table = await PRZH.GetTable(PRZC.c_TABLE_NAT_ELEMENTS))
                                using (RowCursor rowCursor = table.Search(null, false))
                                {
                                    while (rowCursor.MoveNext())
                                    {
                                        using (Row row = rowCursor.Current)
                                        {
                                            int element_id = Convert.ToInt32(row[PRZC.c_FLD_TAB_ELEMENT_ELEMENT_ID]);

                                            if (elements_with_intersection.Contains(element_id))
                                            {
                                                row[PRZC.c_FLD_TAB_ELEMENT_PRESENCE] = (int)NationalElementPresence.Present;
                                            }
                                            else
                                            {
                                                row[PRZC.c_FLD_TAB_ELEMENT_PRESENCE] = (int)NationalElementPresence.Absent;
                                            }

                                            row.Store();
                                            context.Invalidate(row);
                                        }
                                    }
                                }
                            }, tab);
                        }

                        // Execute all the queued "creates"
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Executing Edit Operation - updating element record presence..."), true, max, ++val);
                        success = loader.Execute();

                        if (success)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Saving updates..."), true, max, ++val);
                            if (!await Project.Current.SaveEditsAsync())
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Error saving updates.", LogMessageType.ERROR), true, max, ++val);
                                ProMsgBox.Show($"Error saving updates.");
                                return false;
                            }
                            else
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("Updates saved."), true, max, ++val);
                            }
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog($"Edit Operation error: unable to update records: {loader.ErrorMessage}", LogMessageType.ERROR), true, max, ++val);
                            ProMsgBox.Show($"Edit Operation error: unable to update records: {loader.ErrorMessage}");
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error updating {PRZC.c_FLD_TAB_ELEMENT_PRESENCE} field.", LogMessageType.ERROR), true, val++);
                    ProMsgBox.Show($"Error updating {PRZC.c_FLD_TAB_ELEMENT_PRESENCE} field.");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Field updated."), true, val++);
                    return true;
                }

                #endregion
            }
            catch (Exception ex)
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, max, val++);
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

        private async Task<bool> Test()
        {
            try
            {
                await QueuedTask.Run(async () =>
                {
                    using (Geodatabase geodatabase = await PRZH.GetGDB_Project())
                    {
                        SchemaBuilder schemaBuilder = new SchemaBuilder(geodatabase);

                        using (var domain = geodatabase.GetDomains().FirstOrDefault(d => d.GetName() == "ElementPresence"))
                        {
                            if (domain != null && domain is CodedValueDomain cvd)
                            {
                                ProMsgBox.Show("Found it");
                                CodedValueDomainDescription descr = new CodedValueDomainDescription(cvd);
                                schemaBuilder.Delete(descr);
                                if (!schemaBuilder.Build())
                                {
                                    string errormessages = string.Join(" -- ", schemaBuilder.ErrorMessages);
                                    ProMsgBox.Show(errormessages);
                                    return;
                                }
                                else
                                {
                                    ProMsgBox.Show("Deleted it!");
                                }
                            }
                            else
                            {
                                ProMsgBox.Show("It ain't there");
                            }
                        }

                        CodedValueDomainDescription cvDomainDescr = new CodedValueDomainDescription(
                            "ElementPresence",
                            FieldType.SmallInteger,
                            new SortedList<object, string> { { 1, NationalElementPresence.Present.ToString() }, { 2, NationalElementPresence.Absent.ToString() } })
                        {
                            SplitPolicy = SplitPolicy.Duplicate,
                            MergePolicy = MergePolicy.DefaultValue
                        };

                        schemaBuilder.Create(cvDomainDescr);
                        ProMsgBox.Show("building...");

                        if (!schemaBuilder.Build())
                        {
                            string errormessages = string.Join(" -- ", schemaBuilder.ErrorMessages);
                            ProMsgBox.Show(errormessages);
                            return;
                        }
                        else
                        {
                            ProMsgBox.Show("Built it!");
                        }
                    }

                });

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message);
                return false;
            }
        }
        #endregion


    }
}
















