using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Reflection;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZH = NCC.PRZTools.PRZHelper;
using PRZM = NCC.PRZTools.PRZMethods;

namespace NCC.PRZTools
{
    internal class Button_Settings_WTW : Button
    {

        protected override void OnClick()
        {
            try
            {
                ProMsgBox.Show("Not yet...");

                //SettingsWS dlg = new SettingsWS();                        // View
                //SettingsWSVM vm = (SettingsWSVM)dlg.DataContext;          // View Model

                //dlg.Owner = FrameworkApplication.Current.MainWindow;

                //// Closed Event Handler
                //dlg.Closed += (sender, e) =>
                //{
                //    // Event Handler for Dialog close in case I need to do things...
                //    // System.Diagnostics.Debug.WriteLine("Pro Window Dialog Closed";)
                //};

                //// Loaded Event Handler
                //dlg.Loaded += (sender, e) =>
                //{
                //    if (vm != null)
                //    {
                //        vm.OnProWinLoaded();
                //    }
                //};

                //var result = dlg.ShowDialog();

                //// Take whatever action required here once the dialog is close (true or false)
                //// do stuff here!

                //// MessageBox.Show(result.ToString());
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

    }
}
