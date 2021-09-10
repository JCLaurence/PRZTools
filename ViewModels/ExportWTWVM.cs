using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZC = NCC.PRZTools.PRZConstants;
using PRZH = NCC.PRZTools.PRZHelper;
using PRZM = NCC.PRZTools.PRZMethods;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.Converters;


namespace NCC.PRZTools
{
    public class ExportWTWVM : PropertyChangedBase
    {
        public ExportWTWVM()
        {
        }

        #region Properties


        private ProgressManager _pm = ProgressManager.CreateProgressManager(50);    // initialized to min=0, current=0, message=""
        public ProgressManager PM
        {
            get => _pm; set => SetProperty(ref _pm, value, () => PM);
        }

        #endregion

        #region Commands

        private ICommand _cmdExport;
        public ICommand CmdExport => _cmdExport ?? (_cmdExport = new RelayCommand(() => ExportWTWPackage(), () => true));

        private ICommand _cmdClearLog;
        public ICommand CmdClearLog => _cmdClearLog ?? (_cmdClearLog = new RelayCommand(() =>
        {
            PRZH.UpdateProgress(PM, "", false, 0, 1, 0);
        }, () => true));


        #endregion

        #region Methods

        public void OnProWinLoaded()
        {
            try
            {
                // Initialize the Progress Bar & Log
                PRZH.UpdateProgress(PM, "", false, 0, 1, 0);

                //// Populate the Statistics combo
                //CostStatisticList = new List<string>()
                //{
                //    CostStatistics.MEAN.ToString(),
                //    CostStatistics.MEDIAN.ToString(),
                //    CostStatistics.MAXIMUM.ToString(),
                //    CostStatistics.MINIMUM.ToString(),
                //    CostStatistics.SUM.ToString()
                //};

                //SelectedCostStatistic = CostStatistics.MEAN.ToString();

                //// Populate the list of Cost Layers
                //Map map = MapView.Active.Map;

                //List<RasterLayer> costLayers = PRZH.GetRasterLayers_COST(map);

                //DeriveCostIsEnabled = costLayers != null && costLayers.Count > 0;

                //CostLayerList = costLayers;



            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        private async Task<bool> ExportWTWPackage()
        {
            int val = 0;

            try
            {
                YamlLegend l = new YamlLegend();
                l.colors = new string[] { "a", "b", "c" };
                l.labels = new string[] { "label a", "label b", "label c" };
                l.type = "manual";

                ISerializer builder = new SerializerBuilder().DisableAliases().Build();
                string s = builder.Serialize(l);

                ProMsgBox.Show(s, "YAML!");

                var builder2 = new SerializerBuilder().DisableAliases().JsonCompatible().Build();
                string t = builder2.Serialize(l);

                ProMsgBox.Show(t, "JSON!");

                var builder3 = new SerializerBuilder().DisableAliases().ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build();
                string u = builder3.Serialize(l);

                ProMsgBox.Show(u, "Yaml No Nulls!");

                return true;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                PRZH.UpdateProgress(PM, PRZH.WriteLog(ex.Message, LogMessageType.ERROR), true, ++val);
                return false;
            }
        }

        #endregion

    }
}
