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
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public class PUStatusVM : PropertyChangedBase
    {
        public PUStatusVM()
        {
        }

        #region Fields


        #endregion

        #region Properties

        private ObservableCollection<StatusConflict> _conflicts;

        public ObservableCollection<StatusConflict> Conflicts
        {
            get { return _conflicts; }
            set
            {
                _conflicts = value;
                SetProperty(ref _conflicts, value, () => Conflicts);
            }
        }

        private StatusConflict _selectedConflict;
        public StatusConflict SelectedConflict
        {
            get => _selectedConflict; set => SetProperty(ref _selectedConflict, value, () => SelectedConflict);
        }

        #endregion

        #region Commands

        private ICommand _cmdTest;
        public ICommand CmdTest => _cmdTest ?? (_cmdTest= new RelayCommand(() => Tester(), () => true));




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
                if (!await PRZH.StatusInfoTableExists())
                {
                    await CreateStatusInfoTable();
                }



                // Get the list of Status Conflicts
                List<StatusConflict> l = new List<StatusConflict>
                {
                    new StatusConflict()
                    {
                        conflict_num = 4,
                        pu_count = 7,
                        include = "Layer a",
                        exclude = "Layer b",
                        conflict_area_ac = 2.41,
                        conflict_area_ha = 1.1,
                        conflict_area_km2 = 0.011
                    },
                    new StatusConflict()
                    {
                        conflict_num = 2,
                        pu_count = 22,
                        include = "Layer d",
                        exclude = "Layer r",
                        conflict_area_ac = 109.1,
                        conflict_area_ha = 40.3,
                        conflict_area_km2 = 0.04
                    }
                };

                // Sort them
                l.Sort((x, y) => x.conflict_num.CompareTo(y.conflict_num));

                // Set the property
                _conflicts = new ObservableCollection<StatusConflict>(l);
                NotifyPropertyChanged(() => Conflicts);



                //#region Spatial Reference Information

                //MapView mapView = MapView.Active;
                //Map map = mapView.Map;
                //_mapSR = map.SpatialReference;
                //MapSRName = _mapSR.Name;
                //bool isMapSRProjM = false;

                //if (_mapSR.IsProjected)
                //{
                //    Unit u = _mapSR.Unit; // should be LinearUnit, since SR is projected
                //    LinearUnit lu = u as LinearUnit;
                //    if (lu.FactoryCode == 9001) // meter
                //    {
                //        isMapSRProjM = true;
                //    }
                //}

                //// layers

                //List<SpatialReference> SRList = new List<SpatialReference>();

                //var lyrs = map.GetLayersAsFlattenedList().ToList();

                //foreach (var lyr in lyrs)
                //{
                //    await QueuedTask.Run(() =>
                //    {
                //        var sr = lyr.GetSpatialReference();
                //        if (sr != null)
                //        {
                //            if (!SRList.Contains(sr))
                //            {
                //                if ((!sr.IsUnknown) && (!sr.IsGeographic))
                //                {
                //                    Unit u = sr.Unit;

                //                    if (u.UnitType == ArcGIS.Core.Geometry.UnitType.Linear)
                //                    {
                //                        LinearUnit lu = u as LinearUnit;

                //                        if (lu.FactoryCode == 9001) // Meter
                //                        {
                //                            SRList.Add(sr);
                //                        }
                //                    }
                //                }
                //            }
                //        }
                //    });
                //}

                //this.LayerSRList = SRList;

                //// SR Radio Options enabled/disabled
                //this.SRLayerIsEnabled = (SRList.Count > 0);
                //this.SRMapIsEnabled = isMapSRProjM;
                //this.SRUserIsEnabled = false;

                //// Graphics Layers having selected Polygon Graphics
                //var glyrs = map.GetLayersAsFlattenedList().OfType<GraphicsLayer>().ToList();
                //var polygraphicList = new List<CIMPolygonGraphic>();

                //Dictionary<GraphicsLayer, int> DICT_GraphicLayer_SelPolyCount = new Dictionary<GraphicsLayer, int>();

                //List<GraphicsLayer> gls = new List<GraphicsLayer>();
                //foreach (var glyr in glyrs)
                //{
                //    var selElems = glyr.GetSelectedElements().OfType<GraphicElement>();
                //    int c = 0;

                //    foreach (var elem in selElems)
                //    {
                //        await QueuedTask.Run(() =>
                //        {
                //            var g = elem.GetGraphic();
                //            if (g is CIMPolygonGraphic)
                //            {
                //                c++;
                //            }
                //        });
                //    }

                //    if (c > 0)
                //    {
                //        DICT_GraphicLayer_SelPolyCount.Add(glyr, c);
                //    }
                //}

                //this.GraphicsLayerList = DICT_GraphicLayer_SelPolyCount.Keys.ToList();
                //this.GraphicsLayerIsEnabled = (DICT_GraphicLayer_SelPolyCount.Count > 0);

                //// Polygon Feature Layers having selection
                //var flyrs = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where((fl) => fl.SelectionCount > 0 && fl.ShapeType == esriGeometryType.esriGeometryPolygon).ToList();

                //this.FeatureLayerList = flyrs;
                //this.FeatureLayerIsEnabled = flyrs.Count > 0;

                //// Buffer
                //this.BufferValue = "0";
                //this.BufferUnitKilometersIsChecked = true;

                //// Grid Type combo box
                //this.GridTypeList = Enum.GetNames(typeof(PlanningUnitTileShape)).ToList();

                //this.SelectedGridType = PlanningUnitTileShape.SQUARE.ToString();

                //// Tile area units
                //this.TileAreaKmIsSelected = true;

                //StringBuilder sb = new StringBuilder();

                //sb.AppendLine("Map Name: " + map.Name);
                //sb.AppendLine("Spatial Reference: " + _mapSR.Name);

                //BarMin = 0;
                //BarMax = 3;
                //BarValue = 0;

                //#endregion

            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> CreateStatusInfoTable()
        {
            try
            {
                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                string toolOutput;

                string path = PRZH.GetStatusInfoTablePath();
                // I'm Here!!!!

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



                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        public bool Tester()
        {
            try
            {

                if (SelectedConflict != null)
                {
                    ProMsgBox.Show(SelectedConflict.conflict_num.ToString());
                }




                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }



        #endregion

        #region Event Handlers


        #endregion

    }
}