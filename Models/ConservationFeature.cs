﻿using System;
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

        public int cf_id { get; set; }                  // unique ID

        public string cf_name { get; set; }             // unique CF name

        public int threshold_layer { get; set; }        // layer minimum threshold

        public int threshold_cf { get; set; }           // cf minimum threshold

        public double area_m2 { get; set; }             // Area (m2) of CF across all Planning Units

        public double area_ac { get; set; }             // Area (ac) of CF across all Planning Units

        public double area_ha { get; set; }             // Area (ha) of CF across all Planning Units

        public double area_km2 { get; set; }            // Area (km2) of CF across all Planning Units

        public Layer layer { get; set; }                // Source Layer object

        public string layer_name { get; set; }          // Source Layer Name

        public string layer_type { get; set; }          // Source Layer Type (Raster or Vector)

        public string where_clause { get; set; }        // Filter for CF based on specific feature subset of Source Layer (Vector only)

        public bool in_use { get; set; }                // Indicates if the CF should be included in any calculations

        public int target_layer { get; set; }           // Layer-level default target

        public int target_cf { get; set; }              // CF-level specific target










    }
}
