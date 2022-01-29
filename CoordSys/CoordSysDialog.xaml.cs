using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace NCC.PRZTools
{
    /// <summary>
    /// Interaction logic for CoordSysDialog.xaml
    /// </summary>
    public partial class CoordSysDialog : ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        internal CoordSysDialogVM vm = new CoordSysDialogVM();

        public CoordSysDialog()
        {
            InitializeComponent();

            this.DataContext = vm;

            this.CoordinateSystemsControl.SelectedSpatialReferenceChanged += (s, args) => {
                vm.SelectedSpatialReference = args.SpatialReference;
            };
        }

        public SpatialReference SpatialReference => vm.SelectedSpatialReference;

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
