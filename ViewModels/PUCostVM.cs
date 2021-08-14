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
using System.IO;
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
    public class PUCostVM : PropertyChangedBase
    {
        public PUCostVM()
        {
        }

        #region Fields

        #endregion

        #region Properties

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

        private ICommand _cmdCalculateCost;
        public ICommand CmdCalculateCost => _cmdCalculateCost ?? (_cmdCalculateCost = new RelayCommand(() => CalculateCost(), () => true));



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

        /*
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
         */

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

        private async Task<bool> CalculateCost()
        {
            int val = 0;

            try
            {

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