using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using PRZM = NCC.PRZTools.PRZMethods;
using PRZC = NCC.PRZTools.PRZConstants;

namespace NCC.PRZTools
{
    internal class Button_LoadPRZLayers : Button
    {
        protected override async void OnClick()
        {
            try
            {
                var loaded = await PRZM.ValidatePRZGroupLayers();

                if (!loaded)
                {
                    MessageBox.Show("Unable to Validate PRZ Layers");
                    return;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }
    }
}
