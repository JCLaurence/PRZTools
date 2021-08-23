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

namespace NCC.PRZTools
{
    /// <summary>
    /// Interaction logic for PlanningUnitsDialog.xaml
    /// </summary>
    public partial class PUGenerator : ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        internal PUGeneratorVM vm = new PUGeneratorVM();

        public PUGenerator()
        {
            InitializeComponent();
            this.DataContext = vm;
        }

    }
}
