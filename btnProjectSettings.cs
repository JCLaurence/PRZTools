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
using System.Text;
using System.Threading.Tasks;

namespace NCC.PRZTools
{
    internal class btnProjectSettings : Button
    {

        private prowinProject _prowinProject = null;

        protected override void OnClick()
        {
            //already open?
            if (_prowinProject != null)
            {
                if (_prowinProject.WindowState == System.Windows.WindowState.Minimized)
                {
                    _prowinProject.WindowState = System.Windows.WindowState.Normal;
                }

                return;
            }

            _prowinProject = new prowinProject();
            _prowinProject.Owner = FrameworkApplication.Current.MainWindow;
            _prowinProject.Closed += (o, e) => { _prowinProject = null; };
            _prowinProject.ShowDialog();
        }

    }
}
