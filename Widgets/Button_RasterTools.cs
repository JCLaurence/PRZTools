using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Reflection;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZH = NCC.PRZTools.PRZHelper;

namespace NCC.PRZTools
{
    internal class Button_RasterTools : Button
    {

        protected override async void OnClick()
        {
            try
            {
                #region Project Workspace Check

                // Verify that the Project Folder exists
                if (!PRZH.FolderExists_Project())
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

                //// Ensure that there is an active MapView
                //var mapView = MapView.Active;
                //if (mapView == null)
                //{
                //    ProMsgBox.Show("Not sure how this is possible, but there is no active Map View.  Huh???");
                //    return;
                //}

                //// Ensure that MapView is ready to work with
                //if (!mapView.IsReady)
                //{
                //    ProMsgBox.Show("The Map View is not ready!  Try again later.");
                //    return;
                //}

                //// Ensure that the MapView is a regular 2D MapView
                //if (mapView.ViewingMode != MapViewingMode.Map)
                //{
                //    ProMsgBox.Show("The Map View must be a regular 2D Map View.  Please change the viewing mode to 2D.");
                //    return;
                //}

                //Map map = mapView.Map;
                //if (map.MapType != MapType.Map)
                //{
                //    ProMsgBox.Show("The Map must be of type 'Map'");
                //    return;
                //}

                #endregion

                #region Configure and Show the Planning Units Dialog

                RasterTools dlg = new RasterTools();
                RasterToolsVM vm = (RasterToolsVM)dlg.DataContext;

                dlg.Owner = FrameworkApplication.Current.MainWindow;

                // Closed Event Handler
                dlg.Closed += (sender, e) =>
                {
                    // Event Handler for Dialog close in case I need to do things...
                    // System.Diagnostics.Debug.WriteLine("Pro Window Dialog Closed";)
                };

                // Loaded Event Handler
                dlg.Loaded +=  (sender, e) => { if (vm != null) { vm.OnProWinLoaded(); } };

                var result = dlg.ShowDialog();
                // Take whatever action required here once the dialog is closed (true or false)
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
