using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using MsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZH = NCC.PRZTools.PRZHelper;
using PRZC = NCC.PRZTools.PRZConstants;


namespace NCC.PRZTools
{
    internal class Button_PlanningUnits : Button
    {

        protected override async void OnClick()
        {
            try
            {
                #region Project Workspace Check

                // Check that WS exists
                var ws = PRZH.GetProjectWorkspaceDirectory();
                if (ws == null)
                {
                    MsgBox.Show("Project Workspace is either invalid or has not been set.  Please set a valid Project Workspace.");
                    return;
                }

                // Check that Workspace GDB exists
                var gdbexists = await PRZMethods.ProjectWorkspaceGDBExists();
                if (!gdbexists)
                {
                    MsgBox.Show("Project Workspace GDB does not exist.  Please Initialize or Reset your Project Workspace.");
                    return;
                }

                #endregion

                #region MapView Check

                // Ensure that there is an active MapView
                var mapView = MapView.Active;
                if (mapView == null)
                {
                    MsgBox.Show("Not sure how this is possible, but there is no active Map View.  Huh???");
                    return;
                }

                // Ensure that MapView is ready to work with
                if (!mapView.IsReady)
                {
                    MsgBox.Show("The Map View is not ready!  Try again later.");
                    return;
                }

                // Ensure that the MapView is a regular 2D MapView
                if (mapView.ViewingMode != MapViewingMode.Map)
                {
                    MsgBox.Show("The Map View must be a regular 2D Map View.  Please change the viewing mode to 2D.");
                    return;
                }

                #endregion

                #region Configure and Show the Planning Units Dialog

                PlanningUnits dlg = new PlanningUnits();                    // View
                PlanningUnitsVM vm = (PlanningUnitsVM)dlg.DataContext;      // View Model

                dlg.Owner = FrameworkApplication.Current.MainWindow;

                // Closed Event Handler
                dlg.Closed += (sender, e) =>
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
                MsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

    }
}
