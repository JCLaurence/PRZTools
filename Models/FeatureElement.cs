using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace NCC.PRZTools
{
    public class FeatureElement
    {
        public FeatureElement()
        {

        }

        #region FIELDS

        private int _cf_id;
        private string _cf_name;
        private int _cf_minthreshold = 0;           // default to zero
        private int _cf_goal = 50;                  // default to 50
        private int _cf_enabled = 1;                // default to 1 (enabled)
        private int _cf_hidden = 0;                 // default to 0 (not hidden)
        private double _cf_area_m2 = 0;             // default to 0
        private double _cf_area_ac = 0;             // default to 0
        private double _cf_area_ha = 0;             // default to 0
        private double _cf_area_km2 = 0;            // default to 0
        private int _cf_pucount = 0;                // default to 0
        private string _cf_whereclause;

        private string _layer_name;
        private FeatureLayerType _layer_type;
        private Layer _layer_object;
        private string _layer_json;
        private int _layer_minthreshold = 0;        // default to 0
        private int _layer_goal = 50;               // default to 50

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Unique ID of the Feature.
        /// </summary>
        public int CF_ID
        {
            get => _cf_id;
            set => _cf_id = value;
        }

        /// <summary>
        /// Unique Name for the Feature.
        /// </summary>
        public string CF_Name
        {
            get => _cf_name;
            set => _cf_name = value;
        }

        /// <summary>
        /// Integer between 0 and 100 inclusive.  Represents the % of the Planning Unit's area below which
        /// any coverage of this Feature will be deemed to not count as 'present' on the planning unit.
        /// </summary>
        public int CF_MinThreshold
        {
            get => _cf_minthreshold;
            set => _cf_minthreshold = value;
        }

        /// <summary>
        /// Integer between 0 and 100 inclusive.  Represents the preservation goal (or target) for this
        /// Feature across all planning units, expressed as a %.
        /// </summary>
        public int CF_Goal
        {
            get => _cf_goal;
            set => _cf_goal = value;
        }

        /// <summary>
        /// Integer, 0 (disabled) and 1 (enabled).  Applies to the Where to Work Tool.
        /// </summary>
        public int CF_Enabled
        {
            get => _cf_enabled;
            set => _cf_enabled = value;
        }

        /// <summary>
        /// Integer, 0 (not hidden) and 1 (hidden).  Applies to the Where to Work Tool.
        /// </summary>
        public int CF_Hidden
        {
            get => _cf_hidden;
            set => _cf_hidden = value;
        }

        /// <summary>
        /// Total Area (m2) of Feature summed across all planning units.
        /// </summary>
        public double CF_Area_M2
        {
            get => _cf_area_m2;
            set => _cf_area_m2 = value;
        }

        /// <summary>
        /// Total Area (ac) of Feature summed across all planning units.
        /// </summary>
        public double CF_Area_Ac
        {
            get => _cf_area_ac;
            set => _cf_area_ac = value;
        }

        /// <summary>
        /// Total Area (ha) of Feature summed across all planning units.
        /// </summary>
        public double CF_Area_Ha
        {
            get => _cf_area_ha;
            set => _cf_area_ha = value;
        }

        /// <summary>
        /// Total Area (km2) of Feature summed across all planning units.
        /// </summary>
        public double CF_Area_Km2
        {
            get => _cf_area_km2;
            set => _cf_area_km2 = value;
        }

        /// <summary>
        /// Number of Planning Units having coverage greater than the minimum threshold
        ///  of the Feature within them.
        /// </summary>
        public int CF_PUCount
        {
            get => _cf_pucount;
            set => _cf_pucount = value;
        }

        /// <summary>
        /// Layer Where Clause (where applicable) defining subset of layer features in a Feature.
        /// Only applies to Feature Layers with Unique Values legends
        /// </summary>
        public string CF_WhereClause
        {
            get => _cf_whereclause;
            set => _cf_whereclause = value;
        }

        /// <summary>
        /// Name of source layer from which the Feature derives.
        /// </summary>
        public string Layer_Name
        {
            get => _layer_name;
            set => _layer_name = value;
        }

        /// <summary>
        /// Type of source layer (Raster or Vector)
        /// </summary>
        public FeatureLayerType Layer_Type
        {
            get => _layer_type;
            set => _layer_type = value;
        }

        /// <summary>
        /// The ArcGIS.Desktop.Mapping.Layer Object associated with the source layer.
        /// </summary>
        public Layer Layer_Object
        {
            get => _layer_object;
            set => _layer_object = value;
        }

        /// <summary>
        /// Source layer object serialized to JSON string
        /// </summary>
        public string Layer_Json
        {
            get => _layer_json;
            set => _layer_json = value;
        }

        /// <summary>
        /// Integer between 0 and 100 inclusive.  This value is an interim value in the calculation
        /// of Feature minimum thresholds.  It is stored but not used in any calculations.
        /// </summary>
        public int Layer_MinThreshold
        {
            get => _layer_minthreshold;
            set => _layer_minthreshold = value;
        }

        /// <summary>
        /// Integer between 0 and 100 inclusive.  This value is an interim value in the calculation
        /// of Feature Goals.  It is stored but not used in any calculations.
        /// </summary>
        public int Layer_Goal
        {
            get => _layer_goal;
            set => _layer_goal = value;
        }

        #endregion
    }
}
