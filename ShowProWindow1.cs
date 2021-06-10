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
    internal class ShowProWindow1 : Button
    {

        private ProWindow1 _prowindow1 = null;

        protected override void OnClick()
        {
            //already open?
            if (_prowindow1 != null)
            {
                if (_prowindow1.WindowState == System.Windows.WindowState.Minimized)
                {
                    _prowindow1.WindowState = System.Windows.WindowState.Normal;
                }

                return;
            }

            _prowindow1 = new ProWindow1();
            _prowindow1.Owner = FrameworkApplication.Current.MainWindow;
            _prowindow1.Closed += (o, e) => { _prowindow1 = null; };
            _prowindow1.Show();
            //uncomment for modal
            //_prowindow1.ShowDialog();
        }

    }
}
