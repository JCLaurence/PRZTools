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

        private string _defaultThreshold = Properties.Settings.Default.DEFAULT_STATUS_THRESHOLD;
        public string DefaultThreshold
        {
            get => _defaultThreshold;

            set
            {
                SetProperty(ref _defaultThreshold, value, () => DefaultThreshold);
                Properties.Settings.Default.DEFAULT_STATUS_THRESHOLD = value;
                Properties.Settings.Default.Save();
            }
        }


        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""
        public ProgressManager PM
        {
            get => _pm;

            set => SetProperty(ref _pm, value, () => PM);
        }

        #endregion

        #region Commands

        private ICommand _cmdClearLog;
        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true));

        private ICommand _cmdTest;
        public ICommand CmdTest => _cmdTest ?? (_cmdTest= new RelayCommand(() => Tester(), () => true));

        private ICommand _calculateStatus;
        public ICommand CmdCalculateStatus => _calculateStatus ?? (_calculateStatus = new RelayCommand(() => CalculateStatus(), () => true));

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
                // Initialize the Progress Bar & Log
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);




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

        private async Task<bool> CalculateStatus()
        {
            CancelableProgressorSource cps = null;  // use this for QueuedTask.Run tasks that take a while.  Otherwise, just use the progressbar on the window
            int val = 0;

            try
            {
                #region INITIALIZATION AND USER INPUT VALIDATION

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                string toolOutput;

                // Initialize ProgressBar and Progress Log
                int max = 50;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Status Calculator..."), false, max, ++val);

                // Validation: Project Geodatabase
                string gdbpath = PRZH.GetProjectGDBPath();
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

                // Validation: Ensure the Planning Unit FC exists
                string pufcpath = PRZH.GetPlanningUnitFCPath();
                if (!await PRZH.PlanningUnitFCExists())
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Class not found.", LogMessageType.ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Class is OK: " + pufcpath), true, ++val);
                }

                // Validation: Default Threshold
                string threshold_text = string.IsNullOrEmpty(DefaultThreshold) ? "0" : ((DefaultThreshold.Trim() == "") ? "0" : DefaultThreshold.Trim());

                if (!double.TryParse(threshold_text, out double threshold_double))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Threshold value", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Threshold value.  Value must be a number between 0 and 100 (inclusive)", "Validation");
                    return false;
                }
                else if (threshold_double < 0 | threshold_double > 100)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Threshold value", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Please specify a valid Threshold value.  Value must be a number between 0 and 100 (inclusive)", "Validation");
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Default Threshold = " + threshold_text), true, ++val);
                }


                // User Prompt
                if (ProMsgBox.Show("You sure you want to do this?", "Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation, System.Windows.MessageBoxResult.Cancel)
                        == System.Windows.MessageBoxResult.Cancel)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out"), true, ++val);
                    return false;
                }

                #endregion

                return false;

                        // 1. Prompt User for OK.  The Planning Unit Status Table will be overwritten, and the planning unit status field will be updated

                        // 2. Validation: ensure the planning unit layer is there

                        // 2. stopwatch

                        // *** I MAY NOT NEED THIS ***
                        // 3. Retrieve tile info (side length, side count, tile area (not sure why).
                        //      I can't rely on just tiles, as my planning unit FC has to eventually be a non-tiled FC
                        // *** I MAY NOT NEED THIS ***

                        // 4. Validate default threshold, must be 0 >= threshold >= 100

                // 5. Validate the presence of the Status group layer and the Include and Exclude group layers

                // 6. Create 2 DataTables: one for Include layer info, and one for Exclude layer info
                //      columns are: INDEX (int), NAME (string), THRESHOLD (double), and STATUS (int)       // threshold might be int, not double (?)

                // 7. Cycle through each Layer in INCLUDE group layer, validate and add info to INCLUDE data table
                //          if no layer, return
                //          foreach layer
                //              a) if not a feature layer, or invalid, or unknown SR, or not a polygon FL
                //                      prompt user to either skip this layer, or abort the whole thing
                //              b) Validate the layer name
                //                      Examine name for the threshold pattern ( [0], [00], or [000])
                //                      if found, ensure that extracted value is a double between 0 and 100 inclusive
                //                          if not, prompt user to skip layer, or cancel the whole thing
                //                      Ensure that the remaining layer name (after [0] has been extracted) is valid
                //              c) Add row to data table
                //                      INDEX=i  (for i = 0, i < Layers.count, i++)
                //                      NAME = layer name (name with [0] extracted)
                //                      THRESHOLD = extracted double value, or default value, whichever got set
                //                      STATUS = If current group layer is INCLUDE, then set to 2.  If current group layer is EXCLUDE, then set to 3.

                // 8. Repeat #7 for EXCLUDE group layer

                // 9. If both data tables contain zero rows, exit since there are no input layers to calculate status with

                // 9.5 build a dictionary<int, double> of key=PUID, value=pu area in m2 for the entire planning unit FC

                // 10. Build the Status Info Table (use GP Tools)
                //      - delete if exists
                //      - Clear PU layer selection
                //      - CopyRows - copy the PU Layer attribute table to a new table (the Status Info Table)
                //      - delete all fields except for OID and the PUID fields
                //      - index the PUID field
                //      - Add new attributes
                //          - QUICKSTATUS (long)
                //          - CONFLICT (long)

                // 11. Add additional columns to Status Info table for the INCLUDE group layer

                //      Foreach row in the INCLUDE datatable
                //          get all values from the row
                //          Add the NAME field              column name = "IN" + LayerNumber + "_Name"              (text, 75)
                //          Add the STATUS field            column name = "IN" + LayerNumber + "_Status"            (short)
                //          Add the AREA field              column name = "IN" + LayerNumber + "_Area"              (double)
                //          Add the THRESHOLD field         column name = "IN" + LayerNumber + "Threshold"          (double)

                //          Populate the new fields for each row in the Status Info table (i.e. for each planning unit)
                //              set NAME to first 75 chars of layer name
                //              set AREA to 0
                //              set STATUS to 2
                //              set THRESHOLD to the layer threshold / default threshold (whichever was set above)

                // 12. Repeat for EXCLUDE group layer
                //      Prefix is "EX" instead of "IN"
                //      Status value is 3 instead of 2

                // 13. Intersect INCLUDE layers with Planning Unit layer
                //      foreach row in INCLUDE datatable
                //          get values from row
                //          Intersect row layer with planning unit FC (keep all attributes)
                //          Dissolve intersection on the PUID field (create multipart features)
                //          cycle through each row in the dissolve FC, get the PUID and area values, and update the Status Info table
                //          for each row (representing each planning unit for which the layer has even the slightest overlap)
                //              get dissolve feature PUID
                //              get dissolve feature area (m2)
                //              get area of full planning unit having same PUID, from dictionary<int, double> from step 9.5
                //              calculate layer overlap for this PU, as dissolve feature area/full PU area ( a percentage)
                //              if %overlap >= THRESHOLD
                //                  Update the Status Info Table row of the same PUID, SPECIFICALLY THE AREA field
                //                      SET statusinfo area field for column prefix + layernumber + "_area" = dissolve feature area
                //          delete 2 temp layers
                //      
                // 14. Repeat for EXCLUDE group layer

                // 15. Update Quickstatus and Conflict Info
                //          get 2 lists of Status Info table AREA field indexes (List<int>), one for INCLUDE layers and one for EXCLUDE layers
                //          Foreach row in Status Info table
                //              get PUID
                //              if any of the INCLUDE area columns have a value > 0, set IncludeFlag = true
                //              if any of the EXCLUDE area columns have a value > 0, set ExcludeFlag = true
                //              
                //              if IncludeFlag is true
                //                  set Status Info QuickStat column to 2
                //                  add to DICT<int, int> => PUID,2             -- Status Values DICT
                //              else if ExcludeFlag is true
                //                  Status Info QuickStat column = 3
                //                  add to DICT                                 -- Status Values DICT
                //              else
                //                  Status Info QuickStat Column = 0
                //                  add to DICT                                 -- Status Values DICT
                //              
                //              if both flags are true, set CONFLICT field = 1
                //              else set CONFLICT field = 0

                // 16. Update Planning Unit FC QuickStatus column

                //          foreach row in PU FC
                //              get value from Status Values DICT for key = PUID
                //              set PU FC Status column value to this value


                // Compact the Geodatabase
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Compacting the geodatabase..."), true, ++val);
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
            finally
            {
                if (cps != null)
                    cps.Dispose();
            }
        }

        public bool Tester()
        {
            try
            {

                //if (SelectedConflict != null)
                //{
                //    ProMsgBox.Show(SelectedConflict.conflict_num.ToString());
                //}
                //else
                //{
                //    ProMsgBox.Show("nulllll");
                //}

                //PM.Message = "blueberry";

                var gl = PRZH.GetGroupLayer_STATUS(MapView.Active.Map);

                if (gl == null)
                {
                    ProMsgBox.Show("null");
                }
                else
                {
                    ProMsgBox.Show(gl.Name);
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