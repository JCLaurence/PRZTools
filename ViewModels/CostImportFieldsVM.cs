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
using System.Windows.Input;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZC = NCC.PRZTools.PRZConstants;
using PRZH = NCC.PRZTools.PRZHelper;

namespace NCC.PRZTools
{
    public class CostImportFieldsVM : PropertyChangedBase
    {
        public CostImportFieldsVM()
        {
        }

        #region Fields

        private PUCostVM _costParent;
        public PUCostVM CostParent
        {
            get => _costParent;
            set => SetProperty(ref _costParent, value, () => CostParent);
        }

        #endregion

        #region Properties

        private string _dsName;
        public string DSName
        {
            get => _dsName;
            set => SetProperty(ref _dsName, value, () => DSName);
        }
        private string _dsPath;
        public string DSPath
        {
            get => _dsPath;
            set => SetProperty(ref _dsPath, value, () => DSPath);
        }

        private string _dsType;
        public string DSType
        {
            get => _dsType;
            set => SetProperty(ref _dsType, value, () => DSType);
        }

        private string _headerText;
        public string HeaderText
        {
            get => _headerText;
            set => SetProperty(ref _headerText, value, () => HeaderText);
        }

        private List<string> _numericFields;
        public List<string> NumericFields
        {
            get => _numericFields;
            set => SetProperty(ref _numericFields, value, () => NumericFields);
        }

        private List<string> _intFields;
        public List<string> IntFields
        {
            get => _intFields;
            set => SetProperty(ref _intFields, value, () => IntFields);
        }

        private string _selectedPUIDField;
        public string SelectedPUIDField
        {
            get => _selectedPUIDField;
            set
            {
                SetProperty(ref _selectedPUIDField, value, () => SelectedPUIDField);
                ReviewOKEnabled();
            }            
        }

        private string _selectedCostField;
        public string SelectedCostField
        {
            get => _selectedCostField;
            set
            {
                SetProperty(ref _selectedCostField, value, () => SelectedCostField);
                ReviewOKEnabled();
            }
        }

        private bool _cmdOKIsEnabled = false;
        public bool CmdOKIsEnabled
        {
            get => _cmdOKIsEnabled;
            set => SetProperty(ref _cmdOKIsEnabled, value, () => CmdOKIsEnabled);
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


        public ICommand CmdOK => new RelayCommand((paramProWin) =>
        {
            // set parent property values
            CostParent.ImportFieldPUID = SelectedPUIDField;
            CostParent.ImportFieldCost = SelectedCostField;

            (paramProWin as ProWindow).DialogResult = true;
            (paramProWin as ProWindow).Close();
        }, () => true);

        public ICommand CmdCancel => new RelayCommand((paramProWin) =>
        {
            // set parent property values (not necessary, unlike CmdOK)
            CostParent.ImportFieldPUID = SelectedPUIDField;
            CostParent.ImportFieldCost = SelectedCostField;

            (paramProWin as ProWindow).DialogResult = false;
            (paramProWin as ProWindow).Close();
        }, () => true);

        #endregion

        #region Methods

        public async void OnProWinLoaded()
        {
            try
            {
                HeaderText = DSName + " " + DSType;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }


        private void ReviewOKEnabled()
        {
            if (!string.IsNullOrEmpty(SelectedPUIDField) && !string.IsNullOrEmpty(SelectedCostField))
            {
                CmdOKIsEnabled = true;
            }
            else
            {
                CmdOKIsEnabled = false;
            }
        }



        #endregion


    }
}