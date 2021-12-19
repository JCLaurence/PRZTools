using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Reflection;
using System.Threading.Tasks;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZH = NCC.PRZTools.PRZHelper;
using PRZC = NCC.PRZTools.PRZConstants;

namespace NCC.PRZTools
{
    internal class Button_PUCost : Button
    {

        protected override async void OnClick()
        {
            try
            {
                #region Project Workspace Check

                // Verify that the Project Folder exists
                if (!PRZH.FolderExists_Project().exists)
                {
                    ProMsgBox.Show($"Unable to retrieve Project Folder at: {PRZH.GetPath_ProjectFolder()}");
                    return;
                }

                // Verify that the Project Geodatabase exists
                var try_exists = await PRZH.GDBExists_Project();
                if (!try_exists.exists)
                {
                    ProMsgBox.Show($"Unable to retrieve project geodatabase.\n{try_exists.message}");
                    return;
                }

                #endregion

                #region MapView Check

                // Ensure that there is an active MapView
                var mapView = MapView.Active;
                if (mapView == null)
                {
                    ProMsgBox.Show("Not sure how this is possible, but there is no active Map View.  Huh???");
                    return;
                }

                // Ensure that MapView is ready to work with
                if (!mapView.IsReady)
                {
                    ProMsgBox.Show("The Map View is not ready!  Try again later.");
                    return;
                }

                // Ensure that the MapView is a regular 2D MapView
                if (mapView.ViewingMode != MapViewingMode.Map)
                {
                    ProMsgBox.Show("The Map View must be a regular 2D Map View.  Please change the viewing mode to 2D.");
                    return;
                }

                Map map = mapView.Map;
                if (map.MapType != MapType.Map)
                {
                    ProMsgBox.Show("The Map must be of type 'Map'");
                    return;
                }

                #endregion

                #region Layers Check

                // Ensure the PRZ Group Layer is set up
                //if (!await PRZM.ValidatePRZGroupLayers())
                //{
                //    ProMsgBox.Show("Unable to Validate PRZ Layers");
                //    return;
                //}

                // Ensure the Planning Unit Layer is present
                if (!(await PRZH.FCExists_Project(PRZC.c_FC_PLANNING_UNITS)).exists)
                {
                    ProMsgBox.Show("You must first construct a Planning Unit Feature Class.");
                    return;
                }

                // Ensure that the active map has an acceptable spatial reference
                // TODO: Determine what constitutes a valid SR
                SpatialReference SR = map.SpatialReference;

                #endregion

                #region Configure and Show the Planning Unit Cost Dialog

                PUCost dlg = new PUCost();                              // View
                PUCostVM vm = (PUCostVM)dlg.DataContext;                // View Model

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
