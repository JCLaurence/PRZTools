using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Controls;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZC = NCC.PRZTools.PRZConstants;
using PRZH = NCC.PRZTools.PRZHelper;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Input;

namespace NCC.PRZTools
{
    public class CoordSysDialogVM : PropertyChangedBase // TODO: Possibly remove the interface
    {
        private bool _showVCS = false;
        private SpatialReference _sr;
        private CoordinateSystemsControlProperties _props = null;

        public CoordSysDialogVM()
        {
            UpdateCoordinateControlProperties();
        }

        public string SelectedCoordinateSystemName => _sr != null ? _sr.Name : "";

        public SpatialReference SelectedSpatialReference
        {
            get => _sr;
            set
            {
                SetProperty(ref _sr, value, () => SelectedSpatialReference);
                UpdateCoordinateControlProperties();
            }
        }

        public bool ShowVCS
        {
            get => _showVCS;
            set
            {
                bool changed = SetProperty(ref _showVCS, value, () => ShowVCS);

                if (changed)
                {
                    UpdateCoordinateControlProperties();
                }
            }
        }

        public CoordinateSystemsControlProperties ControlProperties
        {
            get => _props;
            set => SetProperty(ref _props, value, () => ControlProperties);
        }

        private void UpdateCoordinateControlProperties()
        {
            var map = MapView.Active?.Map;
            var props = new CoordinateSystemsControlProperties()
            {
                Map = map,
                SpatialReference = this._sr,
                ShowVerticalCoordinateSystems = this.ShowVCS
            };
            this.ControlProperties = props;
        }
    }
}