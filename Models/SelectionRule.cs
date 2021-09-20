using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace NCC.PRZTools
{
    public class SelectionRule
    {
        public SelectionRule()
        {

        }

        public int sr_id { get; set; }                      // unique ID

        public string sr_name { get; set; }                 // unique Selection Rule name (probably corresponds to the layer name)

        public SelectionRuleType sr_rule_type { get; set; }      // Selection Rule Type enum value

        public SelectionRuleLayerType sr_layer_type { get; set; }        // Layer Type (raster or vector)

        public Layer sr_layer_object { get; set; }          // Layer object

        public string sr_layer_json { get; set; }           // Store the CIMFeaturelayer.ToJson output

        public int sr_min_threshold { get; set; }           // Minimum Threshold (0 to 100 inclusive)

        public int sr_enabled { get; set; }                 // 0 or 1.  1=enabled, 0=disabled

        public double sr_area_m2 { get; set; }              // Area (m2) of Rule across all Planning Units

        public double sr_area_ac { get; set; }              // Area (ac) of Rule across all Planning Units

        public double sr_area_ha { get; set; }              // Area (ha) of Rule across all Planning Units

        public double sr_area_km2 { get; set; }             // Area (km2) of Rule across all Planning Units

        public int sr_pucount { get; set; }                 // Number of Planning Units to which Rule applies (above minimum threshold)









    }
}
