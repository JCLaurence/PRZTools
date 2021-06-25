using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PRZH = NCC.PRZTools.PRZHelper;
using PRZC = NCC.PRZTools.PRZConstants;
using PRZM = NCC.PRZTools.PRZMethods;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace NCC.PRZTools
{
    public class PUCostVM : PropertyChangedBase
    {
        public PUCostVM()
        {
        }

        //public string ConnectionString => Module1.ConnectionString;

        //private string _userName;
        //public string UserName
        //{
        //    get { return _userName; }
        //    set
        //    {
        //        SetProperty(ref _userName, value, () => UserName);
        //    }
        //}

        //private string _password;
        //public string Password
        //{
        //    get { return _password; }
        //    set
        //    {
        //        SetProperty(ref _password, value, () => Password);
        //    }
        //}

        #region Commands

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

        #region Event Handlers


        #endregion

    }
}