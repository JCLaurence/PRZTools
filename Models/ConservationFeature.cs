using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Mapping;


namespace NCC.PRZTools
{
    public class ConservationFeature
    {
        public ConservationFeature()
        {

        }

        // Conservation Feature Properties

        public int cf_id { get; set; }                  // unique ID

        public string cf_name { get; set; }             // unique CF name

        public int cf_min_threshold_pct { get; set; }           // cf minimum threshold

        public int cf_target_pct { get; set; }              // CF-level specific target proportion

        public string cf_whereclause { get; set; }        // Filter for CF based on specific feature subset of Source Layer (Vector only)

        public bool? cf_in_use { get; set; }                // Indicates if the CF should be included in any calculations

        public double cf_area_m2 { get; set; }             // Area (m2) of CF across all Planning Units

        public double cf_area_ac { get; set; }             // Area (ac) of CF across all Planning Units

        public double cf_area_ha { get; set; }             // Area (ha) of CF across all Planning Units

        public double cf_area_km2 { get; set; }            // Area (km2) of CF across all Planning Units

        public int cf_pucount { get; set; }                 // Number of Planning Units containing CF (above min thresh)


        // Layer Properties

        public string lyr_name { get; set; }          // Source Layer Name

        public string lyr_type { get; set; }          // Source Layer Type (Raster or Vector)

        public Layer lyr_object { get; set; }                // Source Layer object

        public string lyr_json { get; set; }          // Store the CIMFeaturelayer.ToJson output

        public int lyr_min_threshold_pct { get; set; }        // layer minimum threshold

        public int lyr_target_pct { get; set; }           // Layer-level default target













    }
}
