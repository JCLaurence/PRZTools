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

namespace NCC.PRZTools.Views
{
    internal class ShowCostImportFields : Button
    {

        private CostImportFields _costimportfields = null;

        protected override void OnClick()
        {
            //already open?
            if (_costimportfields != null)
                return;
            _costimportfields = new CostImportFields();
            _costimportfields.Owner = FrameworkApplication.Current.MainWindow;
            _costimportfields.Closed += (o, e) => { _costimportfields = null; };
            _costimportfields.Show();
            //uncomment for modal
            //_costimportfields.ShowDialog();
        }

    }
}
