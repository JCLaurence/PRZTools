using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Reflection;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZH = NCC.PRZTools.PRZHelper;

namespace NCC.PRZTools
{
    internal class Button_BoundaryLengths : Button
    {

        protected override async void OnClick()
        {
            try
            {
                #region Project Workspace and Planning Unit Check

                // Check that WS exists
                bool wsexists = PRZH.FolderExists_Project();
                if (!wsexists)
                {
                    ProMsgBox.Show("Project Workspace is either invalid or has not been set.  Please set a valid Project Workspace.");
                    return;
                }

                // Check that Workspace GDB exists
                var gdbexists = await PRZH.GDBExists_Project();
                if (!gdbexists)
                {
                    ProMsgBox.Show("Project Workspace GDB does not exist.  Please Initialize or Reset your Project Workspace.");
                    return;
                }

                // Check for presence of Planning Unit data
                var result = await PRZH.PUExists();
                if (!result.exists)
                {
                    ProMsgBox.Show("Planning Unit Feature Class or Raster Dataset not found in the project geodatabase.");
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

                var result2 = dlg.ShowDialog();

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
