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
    internal class Button_BoundaryLengths : Button
    {

        protected override async void OnClick()
        {
            try
            {
                #region Project Workspace  and PUFC Check

                // Check that WS exists
                bool wsexists = PRZH.ProjectWSExists();
                if (!wsexists)
                {
                    ProMsgBox.Show("Project Workspace is either invalid or has not been set.  Please set a valid Project Workspace.");
                    return;
                }

                // Check that Workspace GDB exists
                var gdbexists = await PRZH.ProjectGDBExists();
                if (!gdbexists)
                {
                    ProMsgBox.Show("Project Workspace GDB does not exist.  Please Initialize or Reset your Project Workspace.");
                    return;
                }

                // Check that Planning Unit FC exists
                var pufcexists = await PRZH.PlanningUnitFCExists();
                if (!pufcexists)
                {
                    ProMsgBox.Show("Planning Unit Feature Class does not exist.  You must construct a Planning Unit Feature Class first.");
                    return;
                }

                #endregion

                #region Configure and Show the Boundary Length Dialog

                BoundaryLengths dlg = new BoundaryLengths();                    // View
                BoundaryLengthsVM vm = (BoundaryLengthsVM)dlg.DataContext;      // View Model

                dlg.Owner = FrameworkApplication.Current.MainWindow;

                // Closed Event Handler
                dlg.Closed += (o, e) =>
                {
                    // Event Handler for Dialog close in case I need to do things...
                    // System.Diagnostics.Debug.WriteLine("Pro Window Dialog Closed";)
                };

                // Loaded Event Handler
                dlg.Loaded += (sender, e) =>
                {
                    if (vm != null)
                    {
                        vm.OnProWinLoaded();
                    }
                };

                var result = dlg.ShowDialog();

                // Take whatever action required here once the dialog is close (true or false)
                // do stuff here!

                #endregion
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

    }
}
