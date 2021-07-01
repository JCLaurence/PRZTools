using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
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



        private string _testInfo;
        public string TestInfo
        {
            get { return _testInfo; }
            set
            {
                SetProperty(ref _testInfo, value, () => TestInfo);
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
                                if (!sr.IsUnknown)
                                SRList.Add(sr);
                            }
                        }
                    });
                }

                this.LayerSRList = SRList;

                // SR Radio Options enabled/disabled
                this.SRLayerIsEnabled = (SRList.Count > 0);
                this.SRMapIsEnabled = (!_mapSR.IsUnknown);
                this.SRUserIsEnabled = false;

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

        internal async Task BuildPlanningUnits()
        {
            try
            {
                MsgBox.Show("Generating! Actually, not really.");
            }
            catch (Exception ex)
            {
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
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

        #endregion

    }
}