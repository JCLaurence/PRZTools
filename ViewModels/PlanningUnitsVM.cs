using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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

        private readonly Map _map = MapView.Active.Map;
        private SpatialReference userSR = null;

        private CancellationTokenSource _cts = null;
        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);
        private bool _operation_Cmd_IsEnabled;
        private bool _operationIsUnderway = false;
        private Cursor _proWindowCursor;

        private bool _pu_exists = false;
        private bool _gdb_exists = false;

        #region COMMANDS

        private ICommand _cmdSelectSpatialReference;
        private ICommand _cmdGeneratePlanningUnits;
        private ICommand _cmdCancel;
        private ICommand _cmdClearLog;

        #endregion

        #region COMPONENT STATUS INDICATORS

        // Project GDB
        private string _compStat_Img_ProjectGDB_Path;
        private string _compStat_Txt_ProjectGDB_Label;

        // Planning Unit Dataset
        private string _compStat_Img_PlanningUnits_Path;
        private string _compStat_Txt_PlanningUnits_Label;

        #endregion

        #region OPERATION STATUS INDICATORS

        private Visibility _opStat_Img_Visibility = Visibility.Collapsed;
        private string _opStat_Txt_Label;

        #endregion

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

        #region UNITS

        // lengths
        private const string c_M = "m";
        private const string c_KM = "km";

        // areas
        private const string c_M2 = "m\u00B2";
        private const string c_AC = "ac";
        private const string c_HA = "ha";
        private const string c_KM2 = "km\u00B2";

        #endregion

        #endregion

        #region PROPERTIES

        public ProgressManager PM
        {
            get => _pm; set => SetProperty(ref _pm, value, () => PM);
        }

        public bool Operation_Cmd_IsEnabled
        {
            get => _operation_Cmd_IsEnabled;
            set => SetProperty(ref _operation_Cmd_IsEnabled, value, () => Operation_Cmd_IsEnabled);
        }

        public bool OperationIsUnderway
        {
            get => _operationIsUnderway;
        }

        public Cursor ProWindowCursor
        {
            get => _proWindowCursor;
            set => SetProperty(ref _proWindowCursor, value, () => ProWindowCursor);
        }

        #region COMPONENT STATUS INDICATORS

        // Project Geodatabase
        public string CompStat_Img_ProjectGDB_Path
        {
            get => _compStat_Img_ProjectGDB_Path;
            set => SetProperty(ref _compStat_Img_ProjectGDB_Path, value, () => CompStat_Img_ProjectGDB_Path);
        }

        public string CompStat_Txt_ProjectGDB_Label
        {
            get => _compStat_Txt_ProjectGDB_Label;
            set => SetProperty(ref _compStat_Txt_ProjectGDB_Label, value, () => CompStat_Txt_ProjectGDB_Label);
        }

        // Planning Units Dataset
        public string CompStat_Img_PlanningUnits_Path
        {
            get => _compStat_Img_PlanningUnits_Path;
            set => SetProperty(ref _compStat_Img_PlanningUnits_Path, value, () => CompStat_Img_PlanningUnits_Path);
        }

        public string CompStat_Txt_PlanningUnits_Label
        {
            get => _compStat_Txt_PlanningUnits_Label;
            set => SetProperty(ref _compStat_Txt_PlanningUnits_Label, value, () => CompStat_Txt_PlanningUnits_Label);
        }

        #endregion

        #region OPERATION STATUS INDICATORS

        public Visibility OpStat_Img_Visibility
        {
            get => _opStat_Img_Visibility;
            set => SetProperty(ref _opStat_Img_Visibility, value, () => OpStat_Img_Visibility);
        }

        public string OpStat_Txt_Label
        {
            get => _opStat_Txt_Label;
            set => SetProperty(ref _opStat_Txt_Label, value, () => OpStat_Txt_Label);
        }

        #endregion

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

        #endregion

        #region COMMANDS

        public ICommand CmdSelectSpatialReference => _cmdSelectSpatialReference ?? (_cmdSelectSpatialReference = new RelayCommand(() => SelectSpatialReference(), () => true));

        public ICommand CmdGeneratePlanningUnits => _cmdGeneratePlanningUnits ?? (_cmdGeneratePlanningUnits = new RelayCommand(async () =>
        {
            // Change UI to Underway
            StartOpUI();

            // Start the operation
            using (_cts = new CancellationTokenSource())
            {
                await GeneratePlanningUnits(_cts.Token);
            }

            // Set source to null (it's already disposed)
            _cts = null;

            // Validate controls
            await ValidateControls();

            // Reset UI to Idle
            ResetOpUI();

        }, () => true, true, false));

        public ICommand CmdCancel => _cmdCancel ?? (_cmdCancel = new RelayCommand(() =>
        {
            if (_cts != null)
            {
                // Optionally notify the user or prompt the user here

                // Cancel the operation
                _cts.Cancel();
            }
        }, () => _cts != null, true, false));

        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true, true, false));

        #endregion

        #region METHODS

        public async Task OnProWinLoaded()
        {
            try
            {
                // Initialize the Progress Bar & Log
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

                // Configure a few controls
                await ValidateControls();

                // Reset the UI
                ResetOpUI();
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task GeneratePlanningUnits(CancellationToken token)
        {
            bool edits_are_disabled = !Project.Current.IsEditingEnabled;
            int val = 0;
            int max = 50;

            try
            {
                #region INITIALIZATION

                #region EDITING CHECK

                // Check for currently unsaved edits in the project
                if (Project.Current.HasEdits)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro Project has unsaved edits.  Please save all edits before proceeding.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("This ArcGIS Pro Project has some unsaved edits.  Please save all edits before proceeding.");
                    return;
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
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Unable to enable editing for this ArcGIS Pro Project.", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Unable to enable editing for this ArcGIS Pro Project.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("ArcGIS Pro editing enabled."), true, ++val);
                    }
                }

                #endregion

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags_GP = GPExecuteToolFlags.GPThread;
                GPExecuteToolFlags toolFlags_GPRefresh = GPExecuteToolFlags.GPThread | GPExecuteToolFlags.RefreshProjectItems;
                string toolOutput;

                // Initialize ProgressBar and Progress Log
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Planning Unit Dataset Generator..."), false, max, ++val);

                // Validation: Ensure the Project Geodatabase Exists
                string gdbpath = PRZH.GetPath_ProjectGDB();
                var try_gdbexists = await PRZH.GDBExists_Project();

                if (!try_gdbexists.exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Project Geodatabase not found: {gdbpath}", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show($"Project Geodatabase not found at {gdbpath}.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Project Geodatabase found at {gdbpath}."), true, ++val);
                }

                // Validation: Output Spatial Reference
                SpatialReference OutputSR = null;

                if (PUSource_Rad_NatGrid_IsChecked)
                {
                    OutputSR = await PRZH.GetSR_PRZCanadaAlbers();
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
                    if (userSR != null && (userSR.IsProjected & userSR.Unit.FactoryCode == 9001))
                    {
                        OutputSR = userSR;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("User-specified Spatial Reference is missing or invalid.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("User-specified spatial reference is missing or invalid (must be projected with linear units = meters)");
                        return;
                    }
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Output spatial reference has not been specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify an output spatial reference.", "Validation");
                    return;
                }
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Output Spatial Reference set to {OutputSR.Name}"), true, ++val);

                // Validation: Study Area Source Geometry
                if (SASource_Rad_Graphic_IsChecked)
                {
                    if (SASource_Cmb_Graphic_SelectedGraphicsLayer == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Study Area Source Geometry - no graphics layer is selected.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Study Area Source Geometry - no graphics layer is selected.", "Validation");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Study Area Source Geometry - graphics layer name: " + SASource_Cmb_Graphic_SelectedGraphicsLayer.Name), true, ++val);
                    }
                }
                else if (SASource_Rad_Layer_IsChecked)
                {
                    if (SASource_Cmb_Layer_SelectedFeatureLayer == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Study Area Source Geometry - no feature layer is selected.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Study Area Source Geometry - no feature layer is selected", "Validation");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Study Area Source Geometry - feature layer name: " + SASource_Cmb_Layer_SelectedFeatureLayer.Name), true, ++val);
                    }
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Study Area Source Geometry - no graphics or feature layer is selected.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Study Area Source Geometry - no graphics or feature layer is selected.", "Validation");
                    return;
                }

                // Validation: Study Area Buffer Distance
                string buffer_dist_text = string.IsNullOrEmpty(SASource_Txt_BufferDistance) ? "0" : ((SASource_Txt_BufferDistance.Trim() == "") ? "0" : SASource_Txt_BufferDistance.Trim());

                if (!double.TryParse(buffer_dist_text, out double buffer_dist))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Invalid buffer distance specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Invalid Buffer Distance specified.  The value must be numeric and >= 0, or blank.", "Validation");
                    return;
                }
                else if (buffer_dist < 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Invalid buffer distance specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Invalid Buffer Distance specified.  The value must be >= 0", "Validation");
                    return;
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

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Buffer Distance = " + bu), true, ++val);

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
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("No National Grid dimension is specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Please specify a National Grid dimension", "Validation");
                        return;
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
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Invalid National Grid dimension specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Invalid National Grid dimension specified.", "Validation");
                        return;
                    }
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"National Grid dimension = {dimension}, side length (m) = {natgrid_sidelength}"), true, ++val);
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
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Missing or invalid Tile Area", LogMessageType.VALIDATION_ERROR), true, ++val);
                            ProMsgBox.Show("Please specify a valid Tile Area.  Value must be numeric and greater than 0", "Validation");
                            return;
                        }
                        else if (customgrid_tile_area_m2 <= 0)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Missing or invalid Tile Area", LogMessageType.VALIDATION_ERROR), true, ++val);
                            ProMsgBox.Show("Please specify a valid Tile Area.  Value must be numeric and greater than 0", "Validation");
                            return;
                        }
                        else
                        {
                            string au = "";

                            // validate the selected tile area units
                            if (PUSource_Cmb_TileArea_SelectedUnit.Equals(default(KeyValuePair<int, string>)))
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("No tile area units specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                                ProMsgBox.Show("Please specify the tile area units.", "Validation");
                                return;
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
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Invalid tile area units specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                                    ProMsgBox.Show("Invalid tile area units specified.", "Validation");
                                    return;
                            }

                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Tile Area = " + au), true, ++val);

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
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Missing or invalid Tile Side", LogMessageType.VALIDATION_ERROR), true, ++val);
                            ProMsgBox.Show("Please specify a valid Tile Side.  Value must be numeric and greater than 0", "Validation");
                            return;
                        }
                        else if (customgrid_tile_side_m <= 0)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Missing or invalid Tile Side", LogMessageType.VALIDATION_ERROR), true, ++val);
                            ProMsgBox.Show("Please specify a valid Tile Side.  Value must be numeric and greater than 0", "Validation");
                            return;
                        }
                        else
                        {
                            string su = "";

                            // validate the selected tile area units
                            if (PUSource_Cmb_TileSide_SelectedUnit.Equals(default(KeyValuePair<int, string>)))
                            {
                                PRZH.UpdateProgress(PM, PRZH.WriteLog("No tile side units specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                                ProMsgBox.Show("Please specify the tile side units.", "Validation");
                                return;
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
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Invalid tile side units specified.", LogMessageType.VALIDATION_ERROR), true, ++val);
                                    ProMsgBox.Show("Invalid tile side units specified.", "Validation");
                                    return;
                            }

                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Tile Side = " + su), true, ++val);

                            // Update the tile area based on the selected side (I don't think I need this)
                            customgrid_tile_area_m2 = Math.Pow(customgrid_tile_side_m, 2);
                        }
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Custom Grid tile size not defined either by area or length.", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Please specify the Custom Grid tile size, either by area or side length.", "Validation");
                        return;
                    }
                }
                else if (PUSource_Rad_Layer_IsChecked)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Unsupported Functionality", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Planning Unit Geometry from a Feature Layer is not yet supported.");
                    return;
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
                    return;
                }

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                #endregion

                PRZH.CheckForCancellation(token);

                #region STRIP MAP AND GDB

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Removing PRZ layers and standalone tables from map..."), true, ++val);
                var tryremove = await PRZH.RemovePRZItemsFromMap(_map);

                if (!tryremove.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to remove all layers and standalone tables where source = {gdbpath}\n{tryremove.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Unable to remove PRZ layers and standalone tables from map\n{tryremove.message}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"All PRZ layers and standalone tables removed from map"), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Delete all Items from Project GDB
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleting all objects from the PRZ project geodatabase at {gdbpath}..."), true, ++val);
                var trydel = await PRZH.DeleteProjectGDBContents();

                if (!trydel.success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to delete all objects from {gdbpath}.\n{trydel.message}", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Unable to delete all objects from {gdbpath}.\n{trydel.message}");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Deleted all objects from {gdbpath}."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                #endregion

                PRZH.CheckForCancellation(token);

                #region STUDY AREA GEOMETRY

                // Retrieve Polygons to construct Study Area + Buffered Study Area
                Polygon SA_poly = null;

                if (SASource_Rad_Graphic_IsChecked)
                {
                    // Get the selected polygon graphics from the graphics layer
                    GraphicsLayer gl = SASource_Cmb_Graphic_SelectedGraphicsLayer;

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
                                polyelems++;
                            }
                        }

                        Polygon poly = polyBuilder.ToGeometry();
                        SA_poly = (Polygon)GeometryEngine.Instance.Project(poly, OutputSR);
                    });
                }
                else if (SASource_Rad_Layer_IsChecked)
                {
                    // Get the selected polygon features from the selected feature layer
                    FeatureLayer fl = SASource_Cmb_Layer_SelectedFeatureLayer;
                    int selpol = 0;

                    await QueuedTask.Run(() =>
                    {
                        PolygonBuilder polyBuilder = new PolygonBuilder(fl.GetSpatialReference());

                        using (Selection selection = fl.GetSelection())
                        using (RowCursor rowCursor = selection.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Feature feat = (Feature)rowCursor.Current)
                                {
                                    // process feature
                                    var s = feat.GetShape().Clone() as Polygon;
                                    polyBuilder.AddParts(s.Parts);
                                    selpol++;
                                }
                            }
                        }

                        Polygon poly = polyBuilder.ToGeometry();
                        SA_poly = (Polygon)GeometryEngine.Instance.Project(poly, OutputSR);
                    });
                }

                // Generate Buffered Polygons (buffer might be 0)
                Polygon SA_poly_buffer = GeometryEngine.Instance.Buffer(SA_poly, buffer_dist_m) as Polygon;
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Study Area >> Buffered by {buffer_dist_m} meter{((buffer_dist_m == 1) ? "" : "s")}"), true, ++val);

                #endregion

                PRZH.CheckForCancellation(token);

                #region CREATE STUDY AREA FEATURE CLASSES

                string safcpath = PRZH.GetPath_Project(PRZC.c_FC_STUDY_AREA_MAIN).path;

                // Build the new empty Main Study Area FC
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating study area feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_STUDY_AREA_MAIN, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(
                    workspace: gdbpath,
                    outputCoordinateSystem: OutputSR,
                    overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error creating study area feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error creating study area feature class.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Study area feature class created successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Add Fields to Main Study Area FC
                string fldArea_m = PRZC.c_FLD_FC_STUDYAREA_AREA_M2 + " DOUBLE 'Square m' # 0 #;";
                string fldArea_ac = PRZC.c_FLD_FC_STUDYAREA_AREA_AC + " DOUBLE 'Acres' # 0 #;";
                string fldArea_ha = PRZC.c_FLD_FC_STUDYAREA_AREA_HA + " DOUBLE 'Hectares' # 0 #;";
                string fldArea_km = PRZC.c_FLD_FC_STUDYAREA_AREA_KM2 + " DOUBLE 'Square km' # 0 #;";

                string SAflds = fldArea_m + fldArea_ac + fldArea_ha + fldArea_km;

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Adding fields to study area feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(safcpath, SAflds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding fields to study area feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error adding fields to study area feature class.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Add the feature
                await QueuedTask.Run(() =>
                {
                    var tryget_gdb = PRZH.GetGDB_Project();

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(PRZC.c_FC_STUDY_AREA_MAIN))
                    using (FeatureClassDefinition fcDef = featureClass.GetDefinition())
                    using (RowBuffer rowBuffer = featureClass.CreateRowBuffer())
                    {
                        geodatabase.ApplyEdits(() =>
                        {
                            // Field values
                            rowBuffer[fcDef.GetShapeField()] = SA_poly;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_M2] = SA_poly.Area;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_AC] = SA_poly.Area * PRZC.c_CONVERT_M2_TO_AC;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_HA] = SA_poly.Area * PRZC.c_CONVERT_M2_TO_HA;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_KM2] = SA_poly.Area * PRZC.c_CONVERT_M2_TO_KM2;

                            // Create the row
                            featureClass.CreateRow(rowBuffer);
                        });
                    }
                });

                PRZH.CheckForCancellation(token);

                string sabufffcpath = PRZH.GetPath_Project(PRZC.c_FC_STUDY_AREA_MAIN_BUFFERED).path;

                // Build the new empty Buffered Study Area FC
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating buffered study area feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FC_STUDY_AREA_MAIN_BUFFERED, "POLYGON", "", "DISABLED", "DISABLED", OutputSR, "", "", "", "", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(
                    workspace: gdbpath,
                    outputCoordinateSystem: OutputSR,
                    overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureclass_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error creating buffered study area feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error creating buffered study area feature class.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Buffered study area feature class created successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Add Fields to Buffered Study Area FC
                string fldBArea_m2 = PRZC.c_FLD_FC_STUDYAREA_AREA_M2 + " DOUBLE 'Square m' # 0 #;";
                string fldBArea_ac = PRZC.c_FLD_FC_STUDYAREA_AREA_AC + " DOUBLE 'Acres' # 0 #;";
                string fldBArea_ha = PRZC.c_FLD_FC_STUDYAREA_AREA_HA + " DOUBLE 'Hectares' # 0 #;";
                string fldBArea_km = PRZC.c_FLD_FC_STUDYAREA_AREA_KM2 + " DOUBLE 'Square km' # 0 #;";

                string SABflds = fldBArea_m2 + fldBArea_ac + fldBArea_ha + fldBArea_km;

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Adding fields to buffered study area feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(sabufffcpath, SABflds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: OutputSR, overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error adding fields to buffered study area feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error adding fields to buffered study area feature class.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"fields added successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Add the feature
                await QueuedTask.Run(() =>
                {
                    var tryget_gdb = PRZH.GetGDB_Project();

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(PRZC.c_FC_STUDY_AREA_MAIN_BUFFERED))
                    using (FeatureClassDefinition fcDef = featureClass.GetDefinition())
                    using (RowBuffer rowBuffer = featureClass.CreateRowBuffer())
                    {
                        geodatabase.ApplyEdits(() =>
                        {
                            // Field values
                            rowBuffer[fcDef.GetShapeField()] = SA_poly_buffer;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_M2] = SA_poly_buffer.Area;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_AC] = SA_poly_buffer.Area * PRZC.c_CONVERT_M2_TO_AC;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_HA] = SA_poly_buffer.Area * PRZC.c_CONVERT_M2_TO_HA;
                            rowBuffer[PRZC.c_FLD_FC_STUDYAREA_AREA_KM2] = SA_poly_buffer.Area * PRZC.c_CONVERT_M2_TO_KM2;

                            // Create the row
                            featureClass.CreateRow(rowBuffer);
                        });
                    }
                });

                #endregion

                PRZH.CheckForCancellation(token);

                #region CREATE PLANNING UNIT DATASETS

                #region CREATE RASTER DATASET

                // First create the Raster Dataset
                bool is_nat = false;
                Envelope extent = null;
                double cell_size = 0;
                int columns = 0;
                int rows = 0;

                // Get Grid Information (extent, cell size, etc)
                if (PUSource_Rad_NatGrid_IsChecked)
                {
                    // Get Extent
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving study area extent from national grid..."), true, ++val);
                    var gridInfo = await NationalGrid.GetNatGridBoundsFromStudyArea(SA_poly_buffer, dimension);
                    if (!gridInfo.success)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve national grid extent.\n\nMessage: {gridInfo.message}", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Unable to retrieve national grid extent.\n\nMessage: {gridInfo.message}");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"National grid extent retrieved."), true, ++val);
                        extent = gridInfo.gridEnv;
                        cell_size = natgrid_sidelength;
                        columns = gridInfo.tilesAcross;
                        rows = gridInfo.tilesUp;
                        is_nat = true;
                    }
                }
                else if (PUSource_Rad_CustomGrid_IsChecked)
                {
                    // Get Extent
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Retrieving study area extent from custom grid..."), true, ++val);
                    var gridInfo = GetCustomGridBoundsFromStudyArea(SA_poly_buffer, customgrid_tile_side_m);

                    if (!gridInfo.success)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve Custom Grid extent envelope.\n{gridInfo.message}", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Unable to retrieve Custom Grid extent envelope.\n{gridInfo.message}");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Custom Grid extent envelope retrieved."), true, ++val);
                        extent = gridInfo.gridEnv;
                        cell_size = customgrid_tile_side_m;
                        columns = gridInfo.tilesAcross;
                        rows = gridInfo.tilesUp;
                    }
                }

                PRZH.CheckForCancellation(token);

                // Build Constant Raster
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Creating preliminary (constant-value) raster..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_TEMP_1, 0, "INTEGER", cell_size, extent);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(
                    workspace: gdbpath,
                    overwriteoutput: true,
                    outputCoordinateSystem: OutputSR);
                toolOutput = await PRZH.RunGPTool("CreateConstantRaster_sa", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating the {PRZC.c_RAS_TEMP_1} raster dataset.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error creating the {PRZC.c_RAS_TEMP_1} raster dataset.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"{PRZC.c_RAS_TEMP_1} raster dataset created successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Change Bit Depth to allow large integer id values
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Copying to better bit depth..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_TEMP_1, PRZC.c_RAS_TEMP_2, "", "", "", "", "", "32_BIT_UNSIGNED");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(
                    workspace: gdbpath,
                    overwriteoutput: true,
                    outputCoordinateSystem: OutputSR,
                    cellSize: cell_size);
                toolOutput = await PRZH.RunGPTool("CopyRaster_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error copying the {PRZC.c_RAS_PLANNING_UNITS} raster dataset.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error copying the {PRZC.c_RAS_PLANNING_UNITS} raster dataset.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Raster dataset copied successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Create "Accelerated" Buffer
                Polygon speedy_buffer = (Polygon)GeometryEngine.Instance.AccelerateForRelationalOperations(SA_poly_buffer);

                // Create dictionary of pu ids and associated national grid cell numbers
                Dictionary<int, long> DICT_PUID_and_cellnums = new Dictionary<int, long>();

                // Assign Planning Unit ID values
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Identifying and numbering raster cells within buffered study area..."), true, ++val);
                await QueuedTask.Run(async () =>
                {
                    // Get expanded bit depth raster
                    var tryget_ras = PRZH.GetRaster_Project(PRZC.c_RAS_TEMP_2);

                    using (RasterDataset rasterDataset = tryget_ras.rasterDataset)
                    {
                        // Get the raster
                        Raster raster = rasterDataset.CreateFullRaster();

                        // Get the row and column counts
                        int rowcount = raster.GetHeight();
                        int colcount = raster.GetWidth();

                        // Get Coords of the UL corner of the extent
                        double origin_UL_x = extent.XMin;
                        double origin_UL_y = extent.YMax;

                        // Define the pixel block dimensions for the raster cursor
                        int block_height = 500;
                        int block_width = colcount;
                        int block_counter = 0;

                        // Start the ID numbering at 1
                        uint pu_id = 1;   // (unsigned integer!)

                        // Create Raster Cursor
                        using (RasterCursor rasterCursor = raster.CreateCursor(block_width, block_height))
                        {
                            do
                            {
                                // Cycle through each pixelblock
                                using (PixelBlock pixelBlock = rasterCursor.Current)
                                {
                                    // Get Current Pixel Block dimensions
                                    int pb_height = pixelBlock.GetHeight();
                                    int pb_width = pixelBlock.GetWidth();

                                    // Get Current Pixel Block Offsets (i.e. rows & cols from UL origin)
                                    int pixelBlock_offset_x = rasterCursor.GetTopLeft().Item1;
                                    int pixelBlock_offset_y = rasterCursor.GetTopLeft().Item2;

                                    // Get array of pixel block values
                                    var pixelBlock_array = pixelBlock.GetPixelData(0, true);

                                    // Row loop
                                    for (int r = 0; r < pb_height; r++)
                                    {
                                        PRZH.CheckForCancellation(token);

                                        // pixel YMin and YMax based on current row
                                        double pixel_YMax = origin_UL_y - (pixelBlock_offset_y * cell_size) - (r * cell_size);
                                        double pixel_YMin = pixel_YMax - cell_size;

                                        // Pixel loop
                                        for (int c = 0; c < pb_width; c++)
                                        {
                                            // pixel XMin and XMax based on current column
                                            double pixel_XMin = origin_UL_x + (pixelBlock_offset_x * cell_size) + (c * cell_size);
                                            double pixel_XMax = pixel_XMin + cell_size;

                                            // Build the extent of the current pixel
                                            Envelope env = EnvelopeBuilder.CreateEnvelope(pixel_XMin, pixel_YMin, pixel_XMax, pixel_YMax, OutputSR);

                                            // Check pixel extent for intersection with speedy buffer
                                            if (GeometryEngine.Instance.Intersects(speedy_buffer, env))
                                            {
                                                // set the pixel value to a new pu id
                                                pixelBlock_array.SetValue(pu_id, c, r);

                                                if (is_nat)
                                                {
                                                    var cn = NationalGrid.GetCellNumberFromULXY(Convert.ToInt32(pixel_XMin), Convert.ToInt32(pixel_YMax), dimension);

                                                    if (cn.success)
                                                    {
                                                        int puid = Convert.ToInt32(pu_id);  // possible overflow exception, except my planning unit grids will never go over int32 max value.
                                                        DICT_PUID_and_cellnums.Add(puid, cn.cell_number);
                                                    }
                                                }

                                                // don't forget to increment me!
                                                pu_id++;
                                            }
                                        }
                                    }

                                    // update the pixel block with the adjusted array
                                    pixelBlock.SetPixelData(0, pixelBlock_array);

                                    // write pixel block to raster
                                    raster.Write(pixelBlock_offset_x, pixelBlock_offset_y, pixelBlock);

                                    block_counter++;
                                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Pixel Block #{block_counter} written."), true, ++val);
                                }
                            } while (rasterCursor.MoveNext());
                        }

                        // Save the raster
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Saving updated raster to {PRZC.c_RAS_TEMP_3}."), true, max, ++val);
                        raster.SaveAs(PRZC.c_RAS_TEMP_3, rasterDataset.GetDatastore(), "GRID");
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Raster dataset saved successfully."), true, max, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Convert zero values to NoData
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Converting zeros to NoData..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_TEMP_3, PRZC.c_RAS_TEMP_3, PRZC.c_RAS_PLANNING_UNITS, "VALUE = 0");
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(
                        workspace: gdbpath,
                        overwriteoutput: true,
                        outputCoordinateSystem: OutputSR,
                        cellSize: cell_size);
                    toolOutput = await PRZH.RunGPTool("SetNull_sa", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error converting zeros to NoData.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error converting zeros to NoData.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"NoData values written successfully."), true, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Delete the temp rasters
                    string d = PRZC.c_RAS_TEMP_1 + ";" + PRZC.c_RAS_TEMP_2 + ";" + PRZC.c_RAS_TEMP_3;
                    toolParams = Geoprocessing.MakeValueArray(d);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error deleting temp rasters.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error deleting temp rasters.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Temp rasters deleted."), true, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Build Pyramids
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Building pyramids..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS, -1);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("BuildPyramids_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error building pyramids.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error building pyramids.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"pyramids built successfully."), true, ++val);
                    }

                    PRZH.CheckForCancellation(token);

                    // Calculate Statistics
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Calculating Statistics..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("CalculateStatistics_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error calculating statistics.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show($"Error calculating statistics.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Statistics calculated successfully."), true, ++val);
                    }
                });

                // Ensure raster is present
                var tryex_puras = await PRZH.RasterExists_Project(PRZC.c_RAS_PLANNING_UNITS);
                if (!tryex_puras.exists)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Unable to retrieve the {PRZC.c_RAS_PLANNING_UNITS} raster.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Unable to retrieve the {PRZC.c_RAS_PLANNING_UNITS} raster.");
                    return;
                }

                // Build the Raster Attribute table...
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Building raster attribute table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS, "OVERWRITE");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(
                    workspace: gdbpath,
                    overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("BuildRasterAttributeTable_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error Building the raster attribute table for the {PRZC.c_RAS_PLANNING_UNITS} raster.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error Building the raster attribute table for the {PRZC.c_RAS_PLANNING_UNITS} raster.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Raster attribute table built successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Add fields to Raster Attribute Table
                string fldPUID = PRZC.c_FLD_RAS_PU_ID + " LONG 'Planning Unit ID' # # #;";
                string fldNatGridCellNum = PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER + " LONG 'National Grid Cell Number' # 0 #;";
                string fldPUAreaM = PRZC.c_FLD_RAS_PU_AREA_M2 + " DOUBLE 'Square m' # 0 #;";
                string fldPUAreaAC = PRZC.c_FLD_RAS_PU_AREA_AC + " DOUBLE 'Acres' # 0 #;";
                string fldPUAreaHA = PRZC.c_FLD_RAS_PU_AREA_HA + " DOUBLE 'Hectares' # 0 #;";
                string fldPUAreaKM = PRZC.c_FLD_RAS_PU_AREA_KM2 + " DOUBLE 'Square km' # 0 #;";

                string flds = fldPUID + (is_nat ? fldNatGridCellNum : "") + fldPUAreaM + fldPUAreaAC + fldPUAreaHA + fldPUAreaKM;

                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to {PRZC.c_RAS_PLANNING_UNITS} raster attribute table..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS, flds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(
                    workspace: gdbpath,
                    overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to {PRZC.c_RAS_PLANNING_UNITS} raster attribute table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error adding fields to {PRZC.c_RAS_PLANNING_UNITS} raster attribute table.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Populate Raster Attribute Table
                await QueuedTask.Run(() =>
                {
                    // area values
                    double area_m2 = cell_size * cell_size;
                    double area_ac = area_m2 * PRZC.c_CONVERT_M2_TO_AC;
                    double area_ha = area_m2 * PRZC.c_CONVERT_M2_TO_HA;
                    double area_km2 = area_m2 * PRZC.c_CONVERT_M2_TO_KM2;

                    var tryget_gdb = PRZH.GetGDB_Project();

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    using (RasterDataset rasterDataset = geodatabase.OpenDataset<RasterDataset>(PRZC.c_RAS_PLANNING_UNITS))
                    using (Raster raster = rasterDataset.CreateFullRaster())
                    using (Table table = raster.GetAttributeTable())
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        geodatabase.ApplyEdits(() =>
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int pu_id = Convert.ToInt32(row[PRZC.c_FLD_RAS_PU_VALUE]);

                                    row[PRZC.c_FLD_RAS_PU_ID] = pu_id;
                                    row[PRZC.c_FLD_RAS_PU_AREA_M2] = area_m2;
                                    row[PRZC.c_FLD_RAS_PU_AREA_AC] = area_ac;
                                    row[PRZC.c_FLD_RAS_PU_AREA_HA] = area_ha;
                                    row[PRZC.c_FLD_RAS_PU_AREA_KM2] = area_km2;

                                    if (is_nat && DICT_PUID_and_cellnums.ContainsKey(pu_id))
                                    {
                                        row[PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER] = DICT_PUID_and_cellnums[pu_id];
                                    }

                                    row.Store();
                                }
                            }
                        });
                    }
                });

                PRZH.CheckForCancellation(token);

                // Index the PU ID field
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing the {PRZC.c_FLD_RAS_PU_ID} field..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS, PRZC.c_FLD_RAS_PU_ID, "ix" + PRZC.c_FLD_RAS_PU_ID);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error indexing field.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field indexed successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                if (is_nat)
                {
                    // Index the cell number field
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing the {PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER} field..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS, PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER, "ix" + PRZC.c_FLD_RAS_PU_NATGRID_CELL_NUMBER);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Error indexing field.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Field indexed successfully."), true, ++val);
                    }
                }

                #endregion

                #region CREATE FEATURE CLASS

                // Convert Raster to Polygon FC
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Converting {PRZC.c_RAS_PLANNING_UNITS} raster to polygon feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_RAS_PLANNING_UNITS, PRZC.c_FC_PLANNING_UNITS, "NO_SIMPLIFY", PRZC.c_FLD_RAS_PU_VALUE, "SINGLE_OUTER_PART");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(
                    workspace: gdbpath,
                    overwriteoutput: true,
                    outputCoordinateSystem: OutputSR);
                toolOutput = await PRZH.RunGPTool("RasterToPolygon_conversion", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error converting raster to feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error converting raster to feature class.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Raster converted to feature class."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Delete unnecessary fields I
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting unnecessary fields..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_PLANNING_UNITS, "gridcode", "KEEP_FIELDS");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting fields.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error deleting fields.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("fields deleted."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Add Fields to FC
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Adding fields to {PRZC.c_FC_PLANNING_UNITS} feature class..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_PLANNING_UNITS, flds);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(
                    workspace: gdbpath,
                    overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("AddFields_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error adding fields to {PRZC.c_FC_PLANNING_UNITS} feature class.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error adding fields to {PRZC.c_FC_PLANNING_UNITS} feature class.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Fields added successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Calculate PU ID field
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Calculating {PRZC.c_FLD_FC_PU_ID} field..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_PLANNING_UNITS, PRZC.c_FLD_FC_PU_ID, "!gridcode!", "PYTHON3", "", "LONG", "");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("CalculateField_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error calculating {PRZC.c_FLD_FC_PU_ID} field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error calculating {PRZC.c_FLD_FC_PU_ID} field.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field calculated successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Populate rest of FC attribute table
                await QueuedTask.Run(() =>
                {
                    // area values
                    double area_m2 = cell_size * cell_size;
                    double area_ac = area_m2 * PRZC.c_CONVERT_M2_TO_AC;
                    double area_ha = area_m2 * PRZC.c_CONVERT_M2_TO_HA;
                    double area_km2 = area_m2 * PRZC.c_CONVERT_M2_TO_KM2;

                    var tryget_gdb = PRZH.GetGDB_Project();

                    using (Geodatabase geodatabase = tryget_gdb.geodatabase)
                    using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(PRZC.c_FC_PLANNING_UNITS))
                    using (RowCursor rowCursor = featureClass.Search(null, false))
                    {
                        geodatabase.ApplyEdits(() =>
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int pu_id = Convert.ToInt32(row[PRZC.c_FLD_FC_PU_ID]);

                                    row[PRZC.c_FLD_RAS_PU_ID] = pu_id;
                                    row[PRZC.c_FLD_RAS_PU_AREA_M2] = area_m2;
                                    row[PRZC.c_FLD_RAS_PU_AREA_AC] = area_ac;
                                    row[PRZC.c_FLD_RAS_PU_AREA_HA] = area_ha;
                                    row[PRZC.c_FLD_RAS_PU_AREA_KM2] = area_km2;

                                    if (is_nat && DICT_PUID_and_cellnums.ContainsKey(pu_id))
                                    {
                                        row[PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER] = DICT_PUID_and_cellnums[pu_id];
                                    }

                                    row.Store();
                                }
                            }
                        });
                    }
                });

                PRZH.CheckForCancellation(token);

                // Delete unnecessary fields II
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting gridcode field..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_PLANNING_UNITS, "gridcode");
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("DeleteField_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error deleting field.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("field deleted."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Index the PU ID field
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing the {PRZC.c_FLD_FC_PU_ID} field..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_PLANNING_UNITS, PRZC.c_FLD_FC_PU_ID, "ix" + PRZC.c_FLD_FC_PU_ID);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags_GP);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error indexing field.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Field indexed successfully."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                if (is_nat)
                {
                    // Index the Cell Number field
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Indexing the {PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER} field..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PRZC.c_FC_PLANNING_UNITS, PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER, "ix" + PRZC.c_FLD_FC_PU_NATGRID_CELL_NUMBER);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                    toolOutput = await PRZH.RunGPTool("AddIndex_management", toolParams, toolEnvs, toolFlags_GP);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error indexing field.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        ProMsgBox.Show("Error indexing field.");
                        return;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Field indexed successfully."), true, ++val);
                    }
                }

                #endregion

                #endregion

                PRZH.CheckForCancellation(token);

                #region CREATE FEATURE DATASETS

                // Create the national fds
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating {PRZC.c_FDS_NATIONAL_ELEMENTS} feature dataset..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FDS_NATIONAL_ELEMENTS, OutputSR);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(
                    workspace: gdbpath,
                    overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureDataset_management", toolParams, toolEnvs, toolFlags_GP | GPExecuteToolFlags.AddToHistory);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating feature dataset.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error creating feature dataset.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Feature dataset created."), true, ++val);
                }

                PRZH.CheckForCancellation(token);

                // Create the regional fds
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Creating {PRZC.c_FDS_REGIONAL_ELEMENTS} feature dataset..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath, PRZC.c_FDS_REGIONAL_ELEMENTS, OutputSR);
                toolEnvs = Geoprocessing.MakeEnvironmentArray(
                    workspace: gdbpath,
                    overwriteoutput: true);
                toolOutput = await PRZH.RunGPTool("CreateFeatureDataset_management", toolParams, toolEnvs, toolFlags_GP | GPExecuteToolFlags.AddToHistory);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Error creating feature dataset.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error creating feature dataset.");
                    return;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Feature dataset created."), true, ++val);
                }

                #endregion

                PRZH.CheckForCancellation(token);

                #region WRAP UP

                // Compact the Geodatabase
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacting the Geodatabase..."), true, ++val);
                toolParams = Geoprocessing.MakeValueArray(gdbpath);
                toolOutput = await PRZH.RunGPTool("Compact_management", toolParams, null, toolFlags_GPRefresh);
                if (toolOutput == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error compacting the geodatabase. GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Error compacting the geodatabase.");
                    return;
                }

                PRZH.CheckForCancellation(token);

                // Refresh the Map & TOC
                if (!(await PRZH.RedrawPRZLayers(_map)).success)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Error redrawing the PRZ layers.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show($"Error redrawing the PRZ layers.");
                    return;
                }

                // Wrap things up
                stopwatch.Stop();
                string message = PRZH.GetElapsedTimeMessage(stopwatch.Elapsed);
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Construction completed successfully!"), true, 1, 1);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(message), true, 1, 1);

                ProMsgBox.Show("Construction Completed Successfully!" + Environment.NewLine + Environment.NewLine + message);

                #endregion

            }
            catch (OperationCanceledException)
            {
                // Cancelled by user
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Operation cancelled by user.", LogMessageType.CANCELLATION), true, ++val);
                ProMsgBox.Show($"Operation cancelled by user.");
            }
            catch (Exception ex)
            {
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
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
                #region SHOW DIALOG

                CoordSysDialog dlg = new CoordSysDialog();                    // View
                CoordSysDialogVM vm = (CoordSysDialogVM)dlg.DataContext;      // View Model
                

                dlg.Owner = FrameworkApplication.Current.MainWindow;

                // Closing event handler
                dlg.Closing += (o, e) =>
                {
                    // Event handler for Dialog closing event

                    //if (vm.OperationIsUnderway)
                    //{
                    //    ProMsgBox.Show("Operation is underway.  Please cancel the operation before closing this window.");
                    //    e.Cancel = true;
                    //}
                };

                // Closed Event Handler
                dlg.Closed += (o, e) =>
                {
                    // Event Handler for Dialog close in case I need to do things...
                    // ProMsgBox.Show("Closed...");
                    // System.Diagnostics.Debug.WriteLine("Pro Window Dialog Closed";)

                    
                };

                // Loaded Event Handler
                dlg.Loaded += async (sender, e) =>
                {
                    //if (vm != null)
                    //{
                    //    await vm.OnProWinLoaded();
                    //}
                };

                var result = dlg.ShowDialog();

                if (vm.SelectedSpatialReference != null)
                {
                    OutputSR_Txt_User_SRName = vm.SelectedSpatialReference.Name;
                    userSR = vm.SelectedSpatialReference;
                }
                else
                {
                    userSR = null;
                }

                // Take whatever action required here once the dialog is closed (true or false)
                // do stuff here!

                #endregion






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

        private async Task ValidateControls()
        {
            try
            {
                // Establish Geodatabase existence
                var tryex_gdb = await PRZH.GDBExists_Project();
                _gdb_exists = tryex_gdb.exists;

                // Establish Planning Unit Existence:
                var tryex_pu = await PRZH.PUExists();
                _pu_exists = tryex_pu.exists;

                // Planning Unit Dataset Label
                if (!_pu_exists || tryex_pu.puLayerType == PlanningUnitLayerType.UNKNOWN)
                {
                    CompStat_Txt_PlanningUnits_Label = "Planning Unit Dataset does not exist.";
                }
                else if (tryex_pu.puLayerType == PlanningUnitLayerType.FEATURE)
                {
                    CompStat_Txt_PlanningUnits_Label = "Planning Unit Dataset exists (Feature Class).";
                }
                else if (tryex_pu.puLayerType == PlanningUnitLayerType.RASTER)
                {
                    CompStat_Txt_PlanningUnits_Label = "Planning Unit Dataset exists (Raster Dataset).";
                }
                else
                {
                    CompStat_Txt_PlanningUnits_Label = "Planning Unit Dataset does not exist.";
                }

                // Project GDB Label
                if (_gdb_exists)
                {
                    CompStat_Txt_ProjectGDB_Label = "Project Geodatabase exists.";
                }
                else
                {
                    CompStat_Txt_ProjectGDB_Label = "Project Geodatabase DOES NOT EXIST.";
                }

                // Planning Units Icon
                if (_gdb_exists)
                {
                    CompStat_Img_ProjectGDB_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_Img_ProjectGDB_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_No16.png";
                }

                // Planning Units Icon
                if (_pu_exists)
                {
                    CompStat_Img_PlanningUnits_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Yes16.png";
                }
                else
                {
                    CompStat_Img_PlanningUnits_Path = "pack://application:,,,/PRZTools;component/ImagesWPF/ComponentStatus_Warn16.png";
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private void StartOpUI()
        {
            _operationIsUnderway = true;
            Operation_Cmd_IsEnabled = false;
            OpStat_Img_Visibility = Visibility.Visible;
            OpStat_Txt_Label = "Processing...";
            ProWindowCursor = Cursors.Wait;
        }

        private void ResetOpUI()
        {
            ProWindowCursor = Cursors.Arrow;
            Operation_Cmd_IsEnabled = _gdb_exists;
            OpStat_Img_Visibility = Visibility.Hidden;
            OpStat_Txt_Label = "Idle";
            _operationIsUnderway = false;
        }

        #endregion


    }
}
















