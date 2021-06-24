using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using System;
using System.Reflection;

namespace NCC.PRZTools
{
    internal class Button_PlanningUnits : Button
    {

        protected override void OnClick()
        {
            try
            {
                PlanningUnits dlg = new PlanningUnits();
                dlg.Owner = FrameworkApplication.Current.MainWindow;
                dlg.Closed += (o, e) =>
                {
                    // Event Handler for Dialog close in case I need to do things...
                    // System.Diagnostics.Debug.WriteLine("Pro Window Dialog Closed";)
                };

                var result = dlg.ShowDialog();

                // Take whatever action required here once the dialog is close (true or false)
                // do stuff here!
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

    }
}
