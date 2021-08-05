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
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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

                // Initialize a few thingies
                Map map = MapView.Active.Map;

                // Initialize ProgressBar and Progress Log
                int max = 50;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Status Calculator..."), false, max, ++val);

                // Validation: Ensure the Project Geodatabase Exists
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
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Class not found in the Project Geodatabase.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    return false;
                }
                else
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Planning Unit Feature Class is OK: " + pufcpath), true, ++val);
                }

                // Validation: Ensure the Default Status Threshold is valid
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

                // Validation: Ensure three required Layers are present
                if (!PRZH.PRZLayerExists(map, PRZLayerNames.STATUS_INCLUDE) || !PRZH.PRZLayerExists(map, PRZLayerNames.STATUS_EXCLUDE) || !PRZH.PRZLayerExists(map, PRZLayerNames.PU))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Layers are missing.  Please reload PRZ layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("PRZ Layers are missing.  Please reload the PRZ Layers and try again.", "Validation");
                    return false;
                }

                // Validation: Ensure that at least one Feature Layer is present in either of the two group layers
                var LIST_IncludeFL = PRZH.GetFeatureLayers_STATUS_INCLUDE(map);
                var LIST_ExcludeFL = PRZH.GetFeatureLayers_STATUS_EXCLUDE(map);

                if (LIST_IncludeFL == null || LIST_ExcludeFL == null)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Unable to retrieve contents of Status Include or Status Exclude Group Layers.  Please reload PRZ layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("Unable to retrieve contents of Status Include or Status Exclude Group Layers.  Please reload the PRZ Layers and try again.", "Validation");
                    return false;
                }

                if (LIST_IncludeFL.Count == 0 && LIST_ExcludeFL.Count == 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> No Feature Layers found within Status Include or Status Exclude group layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("There must be at least one Feature Layer within either the Status INCLUDE or the Status EXCLUDE group layers.", "Validation");
                    return false;
                }

                // Validation: Prompt User for permission to proceed
                if (ProMsgBox.Show("You sure you want to do this?", "Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation, System.Windows.MessageBoxResult.Cancel)
                        == System.Windows.MessageBoxResult.Cancel)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out"), true, ++val);
                    return false;
                }

                if (ProMsgBox.Show("If you proceed, the Planning Unit Status table will be overwritten if it exists in the Project Geodatabase." +
                   Environment.NewLine + Environment.NewLine +
                   "Additionally, the contents of the 'status' field in the Planning Unit Feature Class will be updated." +
                   Environment.NewLine + Environment.NewLine +
                   "Do you wish to proceed?" +
                   Environment.NewLine + Environment.NewLine +
                   "Choose wisely...",
                   "TABLE OVERWRITE WARNING",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out of Status Calculation."), true, ++val);
                    return false;
                }

                #endregion

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Some GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                string toolOutput;

                #region PREPARE THE LAYER DATATABLES

                // Create Include and Exclude Data Tables
                // Each DataTable will contain one row per layer in that category (e.g. Include DT might contain 3 rows, one row per layer within the INclude group layer)
                DataTable DT_IncludeLayers = new DataTable("INCLUDE");    // doesn't really need a name
                DT_IncludeLayers.Columns.Add(PRZC.c_FLD_DATATABLE_STATUS_INDEX, Type.GetType("System.Int32"));
                DT_IncludeLayers.Columns.Add(PRZC.c_FLD_DATATABLE_STATUS_NAME, Type.GetType("System.String"));
                DT_IncludeLayers.Columns.Add(PRZC.c_FLD_DATATABLE_STATUS_THRESHOLD, Type.GetType("System.Double"));
                DT_IncludeLayers.Columns.Add(PRZC.c_FLD_DATATABLE_STATUS_STATUS, Type.GetType("System.Int32"));

                DataTable DT_ExcludeLayers = DT_IncludeLayers.Clone();

                if (!await PopulateLayerTable(PRZLayerNames.STATUS_INCLUDE, DT_IncludeLayers))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("INCLUDE Layers >> Unable to populate Data Table.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Unable to populate the INCLUDE Data Table.", "INCLUDE Layers");
                    return false;
                }

                if (!await PopulateLayerTable(PRZLayerNames.STATUS_EXCLUDE, DT_ExcludeLayers))
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("EXCLUDE Layers >> Unable to populate Data Table.", LogMessageType.ERROR), true, ++val);
                    ProMsgBox.Show("Unable to populate the EXCLUDE Data Table.", "EXCLUDE Layers");
                    return false;
                }

                // Ensure that at least one row exists in one of the 2 DataTables.  Otherwise, quit

                if (DT_IncludeLayers.Rows.Count == 0 && DT_ExcludeLayers.Rows.Count == 0)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Status Calculator >> No valid Polygon Feature Layers found from INCLUDE or EXCLUDE group layers.", LogMessageType.VALIDATION_ERROR), true, ++val);
                    ProMsgBox.Show("There must be at least one valid Polygon Feature Layer in either the INCLUDE or EXCLUDE group layers.", "Status Calculator");
                    return false;
                }

                #endregion

                #region RETRIEVE PUID > AREA_M2 DICTIONARY FOR EACH PLANNING UNIT

                Dictionary<int, double> DICT_PUID_and_assoc_area_m2 = new Dictionary<int, double>();

                await QueuedTask.Run(async () =>
                {
                    using (Geodatabase gdb = await PRZH.GetProjectGDB())
                    using (FeatureClass puFC = gdb.OpenDataset<FeatureClass>(PRZC.c_FC_PLANNING_UNITS))
                    using (RowCursor rowCursor = puFC.Search(null, false))
                    {
                        // Get the Definition
                        FeatureClassDefinition fcDef = puFC.GetDefinition();

                        // Field Indexes
                        int ixPUID = fcDef.FindField(PRZC.c_FLD_PUFC_ID);
                        int ixStatus = fcDef.FindField(PRZC.c_FLD_PUFC_STATUS);
                        int ixCost = fcDef.FindField(PRZC.c_FLD_PUFC_COST);
                        // I'm here!!!
                        int ixArea_m2 = fcDef.FindField(PRZC.c_FLD_BESTSOLN_PUID)







                    }
                });


                #endregion

                return false;


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

        private async Task<bool> PopulateLayerTable(PRZLayerNames layer, DataTable DT)
        {
            try
            {
                Map map = MapView.Active.Map;

                List<FeatureLayer> LIST_FL = null;
                string group = "";
                int status_val;

                switch (layer)
                {
                    case PRZLayerNames.STATUS_INCLUDE:
                        LIST_FL = PRZH.GetFeatureLayers_STATUS_INCLUDE(map);
                        status_val = 2;
                        group = "INCLUDE";
                        break;
                    case PRZLayerNames.STATUS_EXCLUDE:
                        LIST_FL = PRZH.GetFeatureLayers_STATUS_EXCLUDE(map);
                        status_val = 3;
                        group = "EXCLUDE";
                        break;
                    default:
                        return false;
                }

                for (int i = 0; i < LIST_FL.Count; i++) // if the list has no members, this whole for loop will be skipped and we'll return true, which is good.
                {
                    // VALIDATE THE FEATURE LAYER
                    FeatureLayer FL = LIST_FL[i];

                    // Make sure the layer has source data and is not an invalid layer
                    if (!await QueuedTask.Run(() =>
                    {
                        FeatureClass FC = FL.GetFeatureClass();
                        bool exists = FC != null;       // if the FL has a valid source, FC will not be null.  If the FL doesn't, FC will be null
                        return exists;                  // return true = FC exists, false = FC doesn't exist.
                    }))
                    {
                        if (ProMsgBox.Show("The Feature Layer '" + FL.Name + "' has no Data Source.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                            group + " Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                            == System.Windows.MessageBoxResult.Cancel)
                        {
                            return false;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    // Make sure the layer has a valid spatial reference
                    if (await QueuedTask.Run(() =>
                    {
                        SpatialReference SR = FL.GetSpatialReference();
                        return (SR == null || SR.IsUnknown);        // return true = invalid SR, or false = valid SR
                    }))
                    {
                        if (ProMsgBox.Show("The Feature Layer '" + FL.Name + "' has a NULL or UNKNOWN Spatial Reference.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                            group + " Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                            == System.Windows.MessageBoxResult.Cancel)
                        {
                            return false;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    // Make sure the layer is a polygon layer
                    if (FL.ShapeType != esriGeometryType.esriGeometryPolygon)
                    {
                        if (ProMsgBox.Show("The Feature Layer '" + FL.Name + "' is NOT a Polygon Feature Layer.  Click OK to skip this layer and continue, or click CANCEL to quit.",
                            group + " Layer Validation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                            == System.Windows.MessageBoxResult.Cancel)
                        {
                            return false;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    // NOW CHECK THE LAYER NAME FOR A USER-SUPPLIED THRESHOLD
                    string original_layer_name = FL.Name;
                    string layer_name;
                    int threshold_int;

                    string pattern = @"^\[\d{1,3}\]";
                    Regex regex = new Regex(pattern);
                    Match match = regex.Match(original_layer_name);

                    if (match.Success)
                    {
                        string matched_pattern = match.Value;   // match.Value is the [n], [nn], or [nnn] substring includng the square brackets
                        layer_name = original_layer_name.Substring(matched_pattern.Length).Trim();  // layer name minus the [n], [nn], or [nnn] substring
                        string threshold_text = matched_pattern.Replace("[", "").Replace("]", "");  // leaves just the 1, 2, or 3 numeric digits, no more brackets

                        threshold_int = int.Parse(threshold_text);  // integer value

                        if (threshold_int < 0 | threshold_int > 100)
                        {
                            string message = "An invalid threshold of " + threshold_int.ToString() + " has been specified for:" +
                                             Environment.NewLine + Environment.NewLine +
                                             "Layer: " + original_layer_name + Environment.NewLine +
                                             "Group Layer: " + group + Environment.NewLine + Environment.NewLine +
                                             "Threshold must be in the range 0 to 100." + Environment.NewLine + Environment.NewLine +
                                             "Click OK to skip this layer and continue, or click CANCEL to quit";
                            
                            if (ProMsgBox.Show(message, group + " Layer Validation", System.Windows.MessageBoxButton.OKCancel,
                                                System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK) 
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return false;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // check the name length
                        if (layer_name.Length == 0)
                        {
                            string message = "Layer '" + original_layer_name + "' has a zero-length name once the threshold value is removed." +
                                             Environment.NewLine + Environment.NewLine +
                                             "Click OK to skip this layer and continue, or click CANCEL to quit";

                            if (ProMsgBox.Show(message, group + " Layer Validation", System.Windows.MessageBoxButton.OKCancel,
                                                System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return false;
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                    else
                    {
                        layer_name = original_layer_name;

                        // check the name length
                        if (layer_name.Length == 0)
                        {
                            string message = "Layer '" + original_layer_name + "' has a zero-length name." +
                                             Environment.NewLine + Environment.NewLine +
                                             "Click OK to skip this layer and continue, or click CANCEL to quit";

                            if (ProMsgBox.Show(message, group + " Layer Validation", System.Windows.MessageBoxButton.OKCancel,
                                                System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.OK)
                                == System.Windows.MessageBoxResult.Cancel)
                            {
                                return false;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // get the default threshold for this layer
                        threshold_int = int.Parse(Properties.Settings.Default.DEFAULT_STATUS_THRESHOLD);   // use default value
                    }

                    double threshold_double = threshold_int / 100.0;    // convert threshold to a double between 0 and 1 inclusive

                    // ADD ROW TO DATATABLE
                    DataRow DR = DT.NewRow();
                    DR[PRZC.c_FLD_DATATABLE_STATUS_INDEX] = i;
                    DR[PRZC.c_FLD_DATATABLE_STATUS_NAME] = layer_name;
                    DR[PRZC.c_FLD_DATATABLE_STATUS_THRESHOLD] = threshold_double;
                    DR[PRZC.c_FLD_DATATABLE_STATUS_STATUS] = status_val;

                    DT.Rows.Add(DR);
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











