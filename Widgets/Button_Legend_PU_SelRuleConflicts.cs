using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Reflection;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZH = NCC.PRZTools.PRZHelper;

namespace NCC.PRZTools
{
    internal class Button_Legend_PU_SelRuleConflicts : Button
    {
        protected override async void OnClick()
        {
            try
            {
                #region Project Workspace Check

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

                #region Get the FL and update the legend

                await QueuedTask.Run(async () =>
                {
                    if (PRZH.PRZLayerExists(map, PRZLayerNames.PU))
                    {
                        FeatureLayer featureLayer = (FeatureLayer)PRZH.GetPRZLayer(map, PRZLayerNames.PU);
                        await PRZH.ApplyLegend_PU_SelRuleConflicts(featureLayer);
                        featureLayer.SetVisibility(true);
                    }
                    else
                    {
                        ProMsgBox.Show("Planning Unit Layer is not present.  Please reload the PRZ Layers");
                    }
                });

                #endregion
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }
    }
}

