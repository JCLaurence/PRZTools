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
using proDlg = ArcGIS.Desktop.Framework.Dialogs;

namespace NCC.PRZTools
{
    /// <summary>
    /// Interaction logic for ProWindow1.xaml
    /// </summary>
    public partial class prowinProject : ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        public prowinProject()
        {
            InitializeComponent();
        }

        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            proDlg.MessageBox.Show("message", "title", MessageBoxButton.OKCancel, MessageBoxImage.Information);
        }
    }
}
