using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
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

        public async void OnProWinLoaded()
        {
            try
            {
                //// Initialize the Progress Bar & Log
                //PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                //// Set the Conflict Override value default
                //SelectedOverrideOption = c_OVERRIDE_INCLUDE;

                //// Populate the Grid
                //bool Populated = await PopulateConflictGrid();

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

                bool? ok = dlg.ShowDialog();

                if (!ok.HasValue || dlg.Items.Count() == 0)
                {
                    return false;
                }

                Item item = dlg.Items.First();
                if (item == null)
                {
                    return false;
                }

                Dataset DS = null;
                bool IsOK = false;
                bool IsFC = false;

                List<string> LIST_NumericFieldNames = new List<string>();
                string DSName = "";
                string DSPath = "";

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
                        }

                        // Is it a Feature Class?
                        if (ds is FeatureClass)
                        {
                            IsOK = true;
                            IsFC = true;
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
                        }
                    }
                });

                if (!IsOK)
                {
                    ProMsgBox.Show("Selected Item is not a Geodatabase Table or Geodatabase Feature Class");
                    return false;
                }
                else if (LIST_NumericFieldNames.Count == 0)
                {
                    ProMsgBox.Show("Selected Dataset has NO numeric attribute fields outside of the OID or FeatureID fields."
                                    + Environment.NewLine + Environment.NewLine +
                                   "Numeric Fields are required to map Planning Unit ID and Cost values.");
                    return false;
                }
                else
                {
                    LIST_NumericFieldNames.Sort();
                }

                // The User has selected a valid GDB Table or FC.
                // Next steps:
                // 1. Prompt the user to specify the PU ID and Cost fields
                //      - PU ID field must be int/long/double
                //      - Cost field must be int/double (basically, numeric values > 0)
                // 2. Retrieve a Dictionary of PU FC ID -> Cost Values
                //      - this will tell me
                //          a) how many rows/features to look for
                //          b) exact ID values to match
                // 3. Retrieve a dictionary of DS ID -> Cost values
                // 4. Compare DICTs:
                //      - ensure that for each PU ID in PUFC, there is a matching PU ID in DS
                //      

                #region Configure and Show the CostImportFields Dialog

                CostImportFields CIFdlg = new CostImportFields
                {
                    Owner = FrameworkApplication.Current.MainWindow
                };                                                                  // View

                CostImportFieldsVM vm = (CostImportFieldsVM)CIFdlg.DataContext;    // View Model
                vm.NumericFields = LIST_NumericFieldNames;
                vm.CostParent = this;
                vm.DSName = DSName;
                vm.DSPath = DSPath;


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

                bool? result = CIFdlg.ShowDialog();

                // Take whatever action required here once the dialog is close (true or false)
                // do stuff here!

                ProMsgBox.Show("Result is: " + (result.HasValue ? result.ToString() : "null"));

                if (result == true)
                {
                    ProMsgBox.Show("PUID Field Name: " + ImportFieldPUID + Environment.NewLine + Environment.NewLine +
                                   "Cost Field Name: " + ImportFieldCost);
                }
                else
                {
                    ProMsgBox.Show("User Cancelled or something weird happened");
                    return false;
                }
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
                                    row[PRZC.c_FLD_PUFC_COST] = constant_cost_double;
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

                                    feature[PRZC.c_FLD_PUFC_COST] = area;
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
                else
                {
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