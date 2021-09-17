using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZC = NCC.PRZTools.PRZConstants;
using PRZH = NCC.PRZTools.PRZHelper;
using PRZM = NCC.PRZTools.PRZMethods;


namespace NCC.PRZTools
{
    public class PUCostVM : PropertyChangedBase
    {
        public PUCostVM()
        {
        }

        #region Fields

        #endregion

        #region Properties

        private List<RasterLayer> _costLayerList;
        public List<RasterLayer> CostLayerList
        {
            get => _costLayerList; set => SetProperty(ref _costLayerList, value, () => CostLayerList);
        }

        private RasterLayer _selectedCostLayer;
        public RasterLayer SelectedCostLayer
        {
            get => _selectedCostLayer; set => SetProperty(ref _selectedCostLayer, value, () => SelectedCostLayer);
        }

        private List<string> _costStatisticList;
        public List<string> CostStatisticList
        {
            get => _costStatisticList; set => SetProperty(ref _costStatisticList, value, () => CostStatisticList);
        }

        private string _selectedCostStatistic = "";
        public string SelectedCostStatistic
        {
            get => _selectedCostStatistic; set => SetProperty(ref _selectedCostStatistic, value, () => SelectedCostStatistic);
        }


        private string _importFieldPUID;
        public string ImportFieldPUID
        {
            get => _importFieldPUID;
            set => SetProperty(ref _importFieldPUID, value, () => ImportFieldPUID);
        }

        private string _importFieldCost;
        public string ImportFieldCost
        {
            get => _importFieldCost;
            set => SetProperty(ref _importFieldCost, value, () => ImportFieldCost);
        }


        private string _constantCost = Properties.Settings.Default.COST_CONSTANT_VALUE;
        public string ConstantCost
        {
            get => _constantCost;

            set
            {
                SetProperty(ref _constantCost, value, () => ConstantCost);
                Properties.Settings.Default.COST_CONSTANT_VALUE = value;
                Properties.Settings.Default.Save();
            }
        }

        private bool _constantCostIsChecked = true;
        public bool ConstantCostIsChecked
        {
            get => _constantCostIsChecked; set => SetProperty(ref _constantCostIsChecked, value, () => ConstantCostIsChecked);
        }

        private bool _areaCostIsChecked = false;
        public bool AreaCostIsChecked
        {
            get => _areaCostIsChecked; set => SetProperty(ref _areaCostIsChecked, value, () => AreaCostIsChecked);
        }

        private bool _importCostIsChecked = false;
        public bool ImportCostIsChecked
        {
            get => _importCostIsChecked; set => SetProperty(ref _importCostIsChecked, value, () => ImportCostIsChecked);
        }

        private bool _deriveCostIsChecked = false;
        public bool DeriveCostIsChecked
        {
            get => _deriveCostIsChecked; set => SetProperty(ref _deriveCostIsChecked, value, () => DeriveCostIsChecked);
        }

        private bool _deriveCostIsEnabled = false;
        public bool DeriveCostIsEnabled
        {
            get => _deriveCostIsEnabled; set => SetProperty(ref _deriveCostIsEnabled, value, () => DeriveCostIsEnabled);
        }

        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""
        public ProgressManager PM
        {
            get => _pm; set => SetProperty(ref _pm, value, () => PM);
        }


        #endregion

        #region Commands

        private ICommand _cmdClearLog;
        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true));

        private ICommand _cmdCalculateCost;
        public ICommand CmdCalculateCost => _cmdCalculateCost ?? (_cmdCalculateCost = new RelayCommand(() => CalculateCost(), () => true));

        private ICommand _cmdImportTable;
        public ICommand CmdImportTable => _cmdImportTable ?? (_cmdImportTable = new RelayCommand(() => ImportTable(), () => true));

        #endregion

        #region Methods

        public void OnProWinLoaded()
        {
            try
            {
                // Initialize the Progress Bar & Log
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                // Populate the Statistics combo
                CostStatisticList = new List<string>()
                { 
                    CostStatistics.MEAN.ToString(),
                    CostStatistics.MEDIAN.ToString(),
                    CostStatistics.MAXIMUM.ToString(),
                    CostStatistics.MINIMUM.ToString(),
                    CostStatistics.SUM.ToString()
                };

                SelectedCostStatistic = CostStatistics.MEAN.ToString();

                // Populate the list of Cost Layers
                Map map = MapView.Active.Map;

                List<RasterLayer> costLayers = PRZH.GetRasterLayers_COST(map);

                DeriveCostIsEnabled = costLayers != null && costLayers.Count > 0;

                CostLayerList = costLayers;



            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> ImportTable()
        {
            int val = 0;

            try
            {
                string gdbpath = PRZH.GetProjectGDBPath();

                BrowseProjectFilter bf = new BrowseProjectFilter
                {
                    Name = "Tables and Feature Classes"
                };

                // Tables or Feature Classes
                bf.AddCanBeFlag(BrowseProjectFilter.FilterFlag.Table);
                bf.AddCanBeFlag(BrowseProjectFilter.FilterFlag.FeatureClass);

                bf.Includes.Add("FolderConnection");
                bf.Includes.Add("GDB");
                bf.Excludes.Add("esri_browsePlaces_Online");

                OpenItemDialog dlg = new OpenItemDialog
                {
                    Title = "Cost Import: Select a Table or Feature Class",
                    InitialLocation = gdbpath,
                    MultiSelect = false,
                    AlwaysUseInitialLocation = false,
                    BrowseFilter = bf
                };

                bool? result1 = dlg.ShowDialog();

                if (!result1.HasValue || !result1.Value || dlg.Items.Count() == 0)
                {
                    return false;
                }

                Item item = dlg.Items.First();
                if (item == null)
                {
                    return false;
                }

                bool IsOK = false;

                List<string> LIST_NumericFieldNames = new List<string>();
                List<string> LIST_IntFieldNames = new List<string>();
                string DSName = "";
                string DSPath = "";
                string DSType = "";

                await QueuedTask.Run(() =>
                {
                    using (Dataset ds = GDBItemHelper.GetDatasetFromItem(item))
                    {
                        DSName = ds.GetName();
                        DSPath = ds.GetPath().AbsolutePath;
                        // Is it a table?
                        if (ds is Table)
                        {
                            IsOK = true;
                            DSType = "Table";
                        }

                        // Is it a Feature Class?
                        if (ds is FeatureClass)
                        {
                            IsOK = true;
                            DSType = "Feature Class";
                        }

                        if (IsOK)
                        {
                            Table table = (Table)ds;
                            TableDefinition tDef = table.GetDefinition();
                            List<Field> numericFields = tDef.GetFields().Where(f => f.FieldType == FieldType.Double
                                                                            | f.FieldType == FieldType.Integer
                                                                            | f.FieldType == FieldType.Single
                                                                            | f.FieldType == FieldType.SmallInteger).ToList();

                            foreach (Field fld in numericFields) using (fld)
                            {
                                LIST_NumericFieldNames.Add(fld.Name);
                            }

                            List<Field> intFields = tDef.GetFields().Where(f => f.FieldType == FieldType.Integer
                                                | f.FieldType == FieldType.SmallInteger).ToList();

                            foreach (Field fld in intFields) using (fld)
                                {
                                    LIST_IntFieldNames.Add(fld.Name);
                                }
                        }
                    }
                });

                if (!IsOK)
                {
                    ProMsgBox.Show("Selected Item is not a Geodatabase Table or Geodatabase Feature Class");
                    return false;
                }
                else if (LIST_NumericFieldNames.Count == 0 | LIST_IntFieldNames.Count == 0)
                {
                    ProMsgBox.Show("Selected Dataset doesn't have required numeric attribute fields outside of the OID or FeatureID fields."
                                    + Environment.NewLine + Environment.NewLine +
                                   "Numeric Fields are required to map Planning Unit ID and Cost values.");
                    return false;
                }
                else
                {
                    LIST_NumericFieldNames.Sort();
                    LIST_IntFieldNames.Sort();
                }

                #region Configure and Show the CostImportFields Dialog

                CostImportFields CIFdlg = new CostImportFields
                {
                    Owner = FrameworkApplication.Current.MainWindow
                };                                                                  // View

                CostImportFieldsVM vm = (CostImportFieldsVM)CIFdlg.DataContext;    // View Model
                vm.NumericFields = LIST_NumericFieldNames;
                vm.IntFields = LIST_IntFieldNames;
                vm.CostParent = this;
                vm.DSName = DSName;
                vm.DSPath = DSPath;
                vm.DSType = DSType;


                // Closed Event Handler
                CIFdlg.Closed += (o, e) =>
                {
                    // Event Handler for Dialog close in case I need to do things...
                    // System.Diagnostics.Debug.WriteLine("Pro Window Dialog Closed";)
                };

                // Loaded Event Handler
                CIFdlg.Loaded += (sender, e) =>
                {
                    if (vm != null)
                    {
                        vm.OnProWinLoaded();
                    }
                };

                bool? result2 = CIFdlg.ShowDialog();

                // Take whatever action required here once the dialog is close (true or false)
                // do stuff here!

                if (!result2.HasValue || result2.Value == false)
                {
                    // Cancelled by user
                    return false;
                }

                #endregion

                // User has now specified the PUID and Cost fields from their selected Dataset

                // Next, I must do a lot of validation on the contents of the selected Dataset
                // 1. Get a Dictionary of PUID => Cost from the PU FC
                // 2. Get the same Dictionary from the Dataset
                // 3. Validate the Dataset:
                //      - NULL, zero, or <0 values in the PUID column?
                //      - DUPLICATE values in PUID column?
                //      - or...
                //      - DUPLICATE values in PUID column having different COST values? Definitely check for this one, especially where PUID is also in PU FC
                //
                //      - Null, zero, or <0 values in the COST column?
                //      
                // 4. Comparison Validation
                //      - For each PUID in PU FC, a matching PUID in Dataset? not necessarily a requirement (ie. update only some of the planning units
                //      - 


                //if (result == true)
                //{
                //    ProMsgBox.Show("PUID Field Name: " + ImportFieldPUID + Environment.NewLine + Environment.NewLine +
                //                   "Cost Field Name: " + ImportFieldCost);
                //}

                // Configure Dictionaries
                Dictionary<int, double> DICT_PUFC_PUID_and_cost = new Dictionary<int, double>();    // (PUID => Cost) from the Planning Unit FC
                Dictionary<int, double?> DICT_DS_PUID_and_cost = new Dictionary<int, double?>();    // (PUID => Cost) from the Dataset
                List<int> LIST_DS_PUID_multiple_costs = new List<int>();

                int nullDSPUIDCount = 0;
                int nullDSCostCount = 0;

                // Populate the PUFC dictionary
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Retrieving Dictionary of PUID => Cost from Planning Unit Feature Class..."), true, ++val);
                await QueuedTask.Run(async () =>
                {
                    using (Table table = await PRZH.GetPlanningUnitFC())
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                int puid = (int)row[PRZC.c_FLD_FC_PU_ID];
                                double cost = (double)row[PRZC.c_FLD_FC_PU_COST];

                                DICT_PUFC_PUID_and_cost.Add(puid, cost);
                            }
                        }
                    }
                });

                // Populate the Dataset dictionary
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Retrieving Dictionary of PUID => Cost from Import Dataset: " + DSName), true, ++val);
                await QueuedTask.Run(() =>
                {
                    using (Table table = (Table)GDBItemHelper.GetDatasetFromItem(item))
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                int? puid_nullable = (int?)row[ImportFieldPUID];
                                double? cost_nullable = (double?)row[ImportFieldCost];

                                // If PUID is null, skip this row
                                if (!puid_nullable.HasValue)
                                {
                                    nullDSPUIDCount++;
                                    continue;
                                }

                                int puid = puid_nullable.Value;

                                if (DICT_DS_PUID_and_cost.ContainsKey(puid))
                                {
                                    double? existing_cost = DICT_DS_PUID_and_cost[puid];

                                    if (!cost_nullable.Equals(existing_cost))
                                    {
                                        LIST_DS_PUID_multiple_costs.Add(puid);
                                    }
                                }
                                else
                                {
                                    DICT_DS_PUID_and_cost.Add(puid, cost_nullable);
                                }
                            }
                        }
                    }
                });

                // Review the PU FC Dictionary (Do I need to do this?)
                var PUFCKeys = DICT_PUFC_PUID_and_cost.Keys.ToList();
                var PUFCValues = DICT_PUFC_PUID_and_cost.Values.ToList();

                int PUFC_minPUID = PUFCKeys.Min();  // should be 1
                int PUFC_maxPUID = PUFCKeys.Max();
                int PUFC_Count = PUFCKeys.Count;
                double PUFC_minCost = PUFCValues.Min();
                double PUFC_maxCost = PUFCValues.Max();

                // Review the DS Dictionary (Definitely need to do this!)
                var DSKeys = DICT_DS_PUID_and_cost.Keys.ToList();
                var DSValues = DICT_DS_PUID_and_cost.Values.ToList();

                int DS_minPUID = DSKeys.Min();
                int DS_maxPUID = DSKeys.Max();
                int DS_Count = DSKeys.Count;
                double DS_minCost = DSValues.Min().GetValueOrDefault();
                double DS_maxCost = DSValues.Max().GetValueOrDefault();

                // I need to fill a dictionary of PUID => cost values that I will use to
                // update PU FC cost field.

                // Only PUIDs existing in both DICT can be added.
                Dictionary<int, double> DICT_Updator = new Dictionary<int, double>();

                foreach(var kvp in DICT_PUFC_PUID_and_cost)
                {
                    int puid = kvp.Key;
                    
                    if (DICT_DS_PUID_and_cost.ContainsKey(puid))
                    {
                        double c = DICT_DS_PUID_and_cost[puid].GetValueOrDefault();
                        DICT_Updator.Add(puid, c);
                    }
                }

                // Verify that at least 1 KVP was added to dict
                if (DICT_Updator.Count == 0)
                {
                    ProMsgBox.Show("No matching PUID values were found in the specified Dataset...");
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Specified Dataset has no matching Planning Unit ID values.  Ending import.", LogMessageType.ERROR), true, ++val);
                    return false;
                }

                // Prompt User to proceed or cancel
                string match_count = (DICT_Updator.Count == 1) ? "1 matching planning unit ID found." : DICT_Updator.Count.ToString() + " matching planning unit IDs found.";
                if (ProMsgBox.Show(match_count +
                   Environment.NewLine + Environment.NewLine +
                   "Click OK to update the Planning Unit Feature Class cost field with matching values." +
                   Environment.NewLine + Environment.NewLine +
                   "Choose wisely...",
                   "FIELD CALCULATE WARNING",
                   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                {
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out of Cost Import Process."), true, ++val);
                    return false;
                }

                // Do the update
                PRZH.UpdateProgress(PM, PRZH.WriteLog($"Updating FC: Setting cost value for {DICT_Updator.Count} feature(s)."), true, ++val);

                await QueuedTask.Run(async () =>
                {
                    using (FeatureClass featureClass = await PRZH.GetPlanningUnitFC())
                    using (RowCursor rowCursor = featureClass.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                int puid = (int)row[PRZC.c_FLD_FC_PU_ID];

                                if (DICT_Updator.ContainsKey(puid))
                                {
                                    row[PRZC.c_FLD_FC_PU_COST] = DICT_Updator[puid];
                                    row.Store();
                                }
                            }
                        }
                    }
                });

                PRZH.UpdateProgress(PM, PRZH.WriteLog("Cost values successfully imported."), true, ++val);
                ProMsgBox.Show("Cost Imported Successfully");

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                return false;
            }
        }

        private async Task<bool> CalculateCost()
        {
            int val = 0;

            try
            {
                // Initialize a few thingies
                Map map = MapView.Active.Map;

                // Initialize ProgressBar and Progress Log
                int max = 50;
                PRZH.UpdateProgress(PM, PRZH.WriteLog("Initializing the Cost Calculator..."), false, max, ++val);

                // Start a stopwatch
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Declare some generic GP variables
                IReadOnlyList<string> toolParams;
                IReadOnlyList<KeyValuePair<string, string>> toolEnvs;
                GPExecuteToolFlags toolFlags = GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;
                string toolOutput;

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

                // Determine the Cost Option specified by the user
                if (ConstantCostIsChecked)
                {
                    // Validation: Ensure the Default Status Threshold is valid
                    string constant_cost_text = string.IsNullOrEmpty(ConstantCost) ? "0" : ((ConstantCost.Trim() == "") ? "0" : ConstantCost.Trim());

                    if (!double.TryParse(constant_cost_text, out double constant_cost_double))
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Constant Cost value", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Please specify a valid Constant Cost value.  Value must be a number greater than zero.", "Validation");
                        return false;
                    }
                    else if (constant_cost_double <= 0)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Missing or invalid Constant Cost value", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("Please specify a valid Constant Cost value.  Value must be a number greater than zero.", "Validation");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Validation >> Constant Cost = " + constant_cost_text), true, ++val);
                    }

                    // Validation: Prompt User for permission to proceed
                    if (ProMsgBox.Show("If you proceed, the contents of the Cost field in the Planning Unit Feature Class will be overwritten." +
                       Environment.NewLine + Environment.NewLine +
                       Environment.NewLine + Environment.NewLine +
                       "Do you wish to proceed?" +
                       Environment.NewLine + Environment.NewLine +
                       "Choose wisely...",
                       "COLUMN OVERWRITE WARNING",
                       System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                       System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out of Cost Calculation."), true, ++val);
                        return false;
                    }

                    // Update PUFC cost column with the constant value
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Updating FC: Setting cost = " + constant_cost_double.ToString() + " for all features."), true, ++val);
                    await QueuedTask.Run(async () =>
                    {
                        using (FeatureClass featureClass = await PRZH.GetPlanningUnitFC())
                        using (RowCursor rowCursor = featureClass.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    row[PRZC.c_FLD_FC_PU_COST] = constant_cost_double;
                                    row.Store();
                                }
                            }
                        }
                    });

                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Cost updated successfully."), true, ++val);
                    ProMsgBox.Show("Cost Updated Successfully");
                }
                else if (AreaCostIsChecked)
                {
                    // Validation: Prompt User for permission to proceed
                    if (ProMsgBox.Show("If you proceed, the contents of the Cost field in the Planning Unit Feature Class will be overwritten." +
                       Environment.NewLine + Environment.NewLine +
                       Environment.NewLine + Environment.NewLine +
                       "Do you wish to proceed?" +
                       Environment.NewLine + Environment.NewLine +
                       "Choose wisely...",
                       "COLUMN OVERWRITE WARNING",
                       System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                       System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out of Cost Calculation."), true, ++val);
                        return false;
                    }

                    // Update PUFC cost column with the area value of the feature's geometry
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Updating FC: Setting cost = area for all features."), true, ++val);
                    await QueuedTask.Run(async () =>
                    {
                        using (FeatureClass featureClass = await PRZH.GetPlanningUnitFC())
                        using (RowCursor rowCursor = featureClass.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Feature feature = (Feature)rowCursor.Current)
                                {
                                    Polygon p = (Polygon)feature.GetShape();
                                    double area = p.Area;

                                    feature[PRZC.c_FLD_FC_PU_COST] = area;
                                    feature.Store();
                                }
                            }
                        }
                    });

                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Cost updated successfully."), true, ++val);
                    ProMsgBox.Show("Cost Updated Successfully");
                }
                else if (ImportCostIsChecked)
                {
                    ProMsgBox.Show("Still working on this one...");

                }
                else if (DeriveCostIsChecked)
                {
                    if (SelectedCostLayer is null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Cost Layer not specified...", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("You must specify a Cost Layer");
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog($"Cost Layer: {SelectedCostLayer.Name}"), true, ++val);
                    }

                    if (SelectedCostStatistic == "")
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Cost Statistic not specified...", LogMessageType.VALIDATION_ERROR), true, ++val);
                        ProMsgBox.Show("You must specify a Cost Statistic");
                        return false;
                    }

                    //// Validation: Prompt User for permission to proceed
                    //if (ProMsgBox.Show("If you proceed, the contents of the Cost field in the Planning Unit Feature Class will be overwritten." +
                    //   Environment.NewLine + Environment.NewLine +
                    //   Environment.NewLine + Environment.NewLine +
                    //   "Do you wish to proceed?" +
                    //   Environment.NewLine + Environment.NewLine +
                    //   "Choose wisely...",
                    //   "COLUMN OVERWRITE WARNING",
                    //   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                    //   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                    //{
                    //    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out of Cost Calculation."), true, ++val);
                    //    return false;
                    //}



                    // Get the PUFC SR and Extent
                    SpatialReference PUFC_SR = null;
                    Envelope PUFC_Extent = null;
                    await QueuedTask.Run(async () =>
                    {
                        using (FeatureClass fc = await PRZH.GetPlanningUnitFC())
                        using (FeatureClassDefinition fcDef = fc.GetDefinition())
                        {
                            PUFC_SR = fcDef.GetSpatialReference();

                            PUFC_Extent = fc.GetExtent();
                        }
                    });

                    // Get the PU Feature Layer and its Spatial Reference
                    FeatureLayer PUFL = null;
                    SpatialReference PUFL_SR = null;
                    await QueuedTask.Run(() =>
                    {
                        PUFL = PRZH.GetFeatureLayer_PU(map);
                        PUFL_SR = PUFL.GetSpatialReference();
                    });

                    // Get the Cost Raster Layer and its SRs
                    RasterLayer CostRL = SelectedCostLayer; // not necessary
                    SpatialReference CostRL_SR = null;
                    SpatialReference CostR_SR = null;

                    await QueuedTask.Run(() =>
                    {
                        CostRL_SR = SelectedCostLayer.GetSpatialReference();

                        using (Raster costRaster = SelectedCostLayer.GetRaster())
                        {
                            CostR_SR = costRaster.GetSpatialReference();
                        }
                    });

                    // Delete the cost stats table if present
                    string cost_stats_path = PRZH.GetCostStatsTablePath();

                    if (await PRZH.CostStatsTableExists())
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Deleting Cost Stats table..."), true, ++val);
                        toolParams = Geoprocessing.MakeValueArray(cost_stats_path);
                        toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath);
                        toolOutput = await PRZH.RunGPTool("Delete_management", toolParams, toolEnvs, toolFlags);
                        if (toolOutput == null)
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Error deleting the Cost Stats table.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                            return false;
                        }
                        else
                        {
                            PRZH.UpdateProgress(PM, PRZH.WriteLog("Successfully deleted the Cost Stats table."), true, ++val);
                        }
                    }

                    // Calculate Zonal Statistics as Table
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Executing Zonal Statistics as Table..."), true, ++val);
                    toolParams = Geoprocessing.MakeValueArray(PUFL, PRZC.c_FLD_FC_PU_ID, CostRL, cost_stats_path);
                    toolEnvs = Geoprocessing.MakeEnvironmentArray(workspace: gdbpath, outputCoordinateSystem: PUFC_SR, overwriteoutput: true, extent: PUFC_Extent);
                    toolOutput = await PRZH.RunGPTool("ZonalStatisticsAsTable_sa", toolParams, toolEnvs, toolFlags);
                    if (toolOutput == null)
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Error executing the Zonal Statistics as Table tool.  GP Tool failed or was cancelled by user", LogMessageType.ERROR), true, ++val);
                        return false;
                    }
                    else
                    {
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Zonal Statistics as Table tool completed successfully."), true, ++val);
                    }

                    // Cost Stats Table is now calculated
                    // The user specified a stat (mean, median, etc)
                    // based on the specified stat, transfer the stat values to PUFC for each planning unit id

                    string cost_field = "";
                    string puid_field = PRZC.c_FLD_COST_ID;

                    if (SelectedCostStatistic == CostStatistics.MAXIMUM.ToString())
                    {
                        cost_field = PRZC.c_FLD_COST_MAX;
                    }
                    else if (SelectedCostStatistic == CostStatistics.MINIMUM.ToString())
                    {
                        cost_field = PRZC.c_FLD_COST_MIN;
                    }
                    else if (SelectedCostStatistic == CostStatistics.MEAN.ToString())
                    {
                        cost_field = PRZC.c_FLD_COST_MEAN;
                    }
                    else if (SelectedCostStatistic == CostStatistics.MEDIAN.ToString())
                    {
                        cost_field = PRZC.c_FLD_COST_MEDIAN;
                    }
                    else if (SelectedCostStatistic == CostStatistics.SUM.ToString())
                    {
                        cost_field = PRZC.c_FLD_COST_SUM;
                    }
                    else
                    {
                        ProMsgBox.Show("Invalid Selected Cost Statistic");
                        return false;
                    }

                    // Configure Dictionaries
                    Dictionary<int, double> DICT_PUFC_PUID_and_cost = new Dictionary<int, double>();    // (PUID => Cost) from the Planning Unit FC
                    Dictionary<int, double?> DICT_COSTSTATS_PUID_and_cost = new Dictionary<int, double?>();    // (PUID => Cost) from the Cost Stats table
                    List<int> LIST_DS_PUID_multiple_costs = new List<int>();

                    int nullCostStatsPUIDCount = 0;
                    int nullCostStatsCostCount = 0;

                    // Populate the PUFC dictionary
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Retrieving Dictionary of PUID => Cost from Planning Unit Feature Class..."), true, ++val);
                    await QueuedTask.Run(async () =>
                    {
                        using (Table table = await PRZH.GetPlanningUnitFC())
                        using (RowCursor rowCursor = table.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int puid = (int)row[PRZC.c_FLD_FC_PU_ID];
                                    double cost = (double)row[PRZC.c_FLD_FC_PU_COST];

                                    DICT_PUFC_PUID_and_cost.Add(puid, cost);
                                }
                            }
                        }
                    });

                    // Populate the CostStats dictionary
                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Retrieving Dictionary of PUID => Cost from Cost Stats table"), true, ++val);
                    await QueuedTask.Run(async () =>
                    {
                        using (Table table = await PRZH.GetCostStatsTable())
                        using (RowCursor rowCursor = table.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int? puid_nullable = (int?)row[puid_field];
                                    double? cost_nullable = (double?)row[cost_field];

                                    // If PUID is null, skip this row
                                    if (!puid_nullable.HasValue)
                                    {
                                        nullCostStatsPUIDCount++;
                                        continue;
                                    }

                                    int puid = puid_nullable.Value;

                                    if (DICT_COSTSTATS_PUID_and_cost.ContainsKey(puid))
                                    {
                                        double? existing_cost = DICT_COSTSTATS_PUID_and_cost[puid];

                                        if (!cost_nullable.Equals(existing_cost))
                                        {
                                            LIST_DS_PUID_multiple_costs.Add(puid);
                                        }
                                    }
                                    else
                                    {
                                        DICT_COSTSTATS_PUID_and_cost.Add(puid, cost_nullable);
                                    }
                                }
                            }
                        }
                    });

                    // Review the PU FC Dictionary (Do I need to do this?)
                    var PUFCKeys = DICT_PUFC_PUID_and_cost.Keys.ToList();
                    var PUFCValues = DICT_PUFC_PUID_and_cost.Values.ToList();

                    int PUFC_minPUID = PUFCKeys.Min();  // should be 1
                    int PUFC_maxPUID = PUFCKeys.Max();
                    int PUFC_Count = PUFCKeys.Count;
                    double PUFC_minCost = PUFCValues.Min();
                    double PUFC_maxCost = PUFCValues.Max();

                    // Review the CostStats Dictionary (Definitely need to do this!)
                    var CostStatsKeys = DICT_COSTSTATS_PUID_and_cost.Keys.ToList();
                    var CostStatsValues = DICT_COSTSTATS_PUID_and_cost.Values.ToList();

                    int CostStats_minPUID = CostStatsKeys.Min();
                    int CostStats_maxPUID = CostStatsKeys.Max();
                    int CostStats_Count = CostStatsKeys.Count;
                    double CostStats_minCost = CostStatsValues.Min().GetValueOrDefault();
                    double CostStats_maxCost = CostStatsValues.Max().GetValueOrDefault();

                    // I need to fill a dictionary of PUID => cost values that I will use to
                    // update PU FC cost field.

                    // Only PUIDs existing in both DICT can be added.
                    Dictionary<int, double> DICT_Updator = new Dictionary<int, double>();

                    foreach (var kvp in DICT_PUFC_PUID_and_cost)
                    {
                        int puid = kvp.Key;

                        if (DICT_COSTSTATS_PUID_and_cost.ContainsKey(puid))
                        {
                            double c = DICT_COSTSTATS_PUID_and_cost[puid].GetValueOrDefault();
                            DICT_Updator.Add(puid, c);
                        }
                    }

                    // Verify that at least 1 KVP was added to dict
                    if (DICT_Updator.Count == 0)
                    {
                        ProMsgBox.Show("No matching PUID values were found in the specified Dataset...");
                        PRZH.UpdateProgress(PM, PRZH.WriteLog("Stats Table has no matching PU ID values.  This is very strange. Exiting Cost Calculation.", LogMessageType.ERROR), true, ++val);
                        return false;
                    }

                    // Prompt User to proceed or cancel
                    //string match_count = (DICT_Updator.Count == 1) ? "1 matching planning unit ID found." : DICT_Updator.Count.ToString() + " matching planning unit IDs found.";
                    //if (ProMsgBox.Show(match_count +
                    //   Environment.NewLine + Environment.NewLine +
                    //   "Click OK to update the Planning Unit Feature Class cost field with matching values." +
                    //   Environment.NewLine + Environment.NewLine +
                    //   "Choose wisely...",
                    //   "FIELD CALCULATE WARNING",
                    //   System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Exclamation,
                    //   System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.Cancel)
                    //{
                    //    PRZH.UpdateProgress(PM, PRZH.WriteLog("User bailed out of Cost Import Process."), true, ++val);
                    //    return false;
                    //}

                    // Do the update
                    PRZH.UpdateProgress(PM, PRZH.WriteLog($"Updating FC: Setting cost value for {DICT_Updator.Count} feature(s)."), true, ++val);

                    await QueuedTask.Run(async () =>
                    {
                        using (FeatureClass featureClass = await PRZH.GetPlanningUnitFC())
                        using (RowCursor rowCursor = featureClass.Search(null, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (Row row = rowCursor.Current)
                                {
                                    int puid = (int)row[PRZC.c_FLD_FC_PU_ID];

                                    if (DICT_Updator.ContainsKey(puid))
                                    {
                                        row[PRZC.c_FLD_FC_PU_COST] = DICT_Updator[puid];
                                        row.Store();
                                    }
                                }
                            }
                        }
                    });

                    PRZH.UpdateProgress(PM, PRZH.WriteLog("Cost values successfully derived from raster."), true, ++val);
                    ProMsgBox.Show("Cost successfully derived from raster.");
                }
                else
                {
                    // don't think I could get here...
                    return false;
                }


                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                return false;
            }

        }

        #endregion


    }
}