using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
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
using MsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZH = NCC.PRZTools.PRZHelper;
using PRZC = NCC.PRZTools.PRZConstants;

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


        private List<string> _gridTypeList = new List<string> { "Square"};
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
                var flyrs = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where((fl) => fl.SelectionCount > 0).ToList();

                this.FeatureLayerList = flyrs;
                this.FeatureLayerIsEnabled = flyrs.Count > 0;

                // Buffer
                this.BufferValue = "0";
                this.BufferUnitMetersIsChecked = true;

                // Grid Type combo box
                this.SelectedGridType = "Square";

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
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        internal async Task Progressor_NonCancelable(string message)
        {
            ProgressorSource ps = new ProgressorSource(message, false);

            int numSecondsDelay = 5;
            //If you run this in the DEBUGGER you will NOT see the dialog
            await QueuedTask.Run(() => Task.Delay(numSecondsDelay * 1000).Wait(), ps.Progressor);
        }

        internal async Task<bool> ProgressorTest(int x, Progressor progressor)
        {
            bool result = false;

            return result;
        }

        internal async Task BuildPlanningUnits()
        {
            ProgressDialog pd = null;

            try
            {
                pd = new ProgressDialog("", "Cancelled by User", false);
                var cps = new CancelableProgressorSource(pd);



                UpdateBarStatus("", 0, 10, 0);

                bool a = await DoSomeWork(cps);

                if (!a)
                {
                    MsgBox.Show("Cancelled by User.");
                    return;
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


                UpdateBarStatus("Operation Complete!", 0, 3, 3);
            }
            catch (Exception ex)
            {
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
            finally
            {
                if (pd != null)
                    pd.Dispose();
            }
        }

        private async Task<bool> DoSomeWork(CancelableProgressorSource cps)
        {
            UpdateBarStatus("Doing Some Work", 0, 10, 0);
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
                        UpdateBarStatus("ERROR.  ERROR. ERROR. ERROR.", 0, 1, 0);
                        return;
                    }
                }, cps.Progressor);
                
                if (cps.Progressor.CancellationToken.IsCancellationRequested)
                {
                    // someone clicked the cancel button
                    UpdateBarStatus("ERROR.  ERROR. ERROR. ERROR.", 0, 1, 0);
                    return false;
                }

                UpdateBarStatus("Doing Some Work - " + i.ToString(), 0, 10, i);
            }

            return true;
        }

        private async Task DoSomeWork2()
        {
            UpdateBarStatus("Doing Some Work 2", 0, 500, 0);

            int i = 0;

            while (i < 500)
            {
                i++;

                await QueuedTask.Run(async () =>
                {
                    await Task.Delay(1);
                });

                UpdateBarStatus("Doing Some Work 2 - " + i.ToString(), 0, 500, i);
            }
        }

        private async Task DoSomeWork3()
        {
            UpdateBarStatus("Doing Some Work 3", 0, 20, 0);

            int i = 0;

            while (i < 20)
            {
                i++;

                await QueuedTask.Run(async () =>
                {
                    await Task.Delay(300);
                    UpdateBarStatus("Doing Some Work within QueuedTask.Run - " + i.ToString(), 0, 20, i);
                });

            }
        }

        private void SelectSpatialReference()
        {
            try
            {
                MsgBox.Show("This will eventually be the Coordinate Systems Picker dialog");

                _userSR = SpatialReferences.WebMercator;
                _outputSR = _userSR;

                if (this.SelectedLayerSR != null)
                {
                    MsgBox.Show("Selected SR: " + this.SelectedLayerSR.Name);
                }
            }
            catch (Exception ex)
            {
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
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
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
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
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
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
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
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

                string xtrim = GridAlign_X.ToString().Trim();
                string ytrim = GridAlign_Y.ToString().Trim();

                if (!string.IsNullOrEmpty(xtrim) && !string.IsNullOrEmpty(ytrim))
                {
                    double x;
                    double y;

                    if (!double.TryParse(xtrim, out x) | !double.TryParse(ytrim, out y))
                    {
                        return false;
                    }
                }
                else if (!string.IsNullOrEmpty(xtrim) && string.IsNullOrEmpty(ytrim))
                {
                    return false;
                }
                else if (string.IsNullOrEmpty(xtrim) && !string.IsNullOrEmpty(ytrim))
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
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        private void UpdateBarStatus(string message, int min, int max, int val)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                BarMessage = message;
                BarMin = min;
                BarMax = max;
                BarValue = val;
            }
            else
            {
                ProApp.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
                {
                    BarMessage = message;
                    BarMin = min;
                    BarMax = max;
                    BarValue = val;
                }));
            }

        }

        

        #endregion

    }
}