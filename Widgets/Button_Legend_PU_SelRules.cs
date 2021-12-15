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
    internal class Button_Legend_PU_SelRules : Button
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
                        FeatureLayer featureLayer = PRZH.GetFeatureLayer_PU(map);
                        await PRZH.ApplyLegend_PU_SelRules(featureLayer);
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
