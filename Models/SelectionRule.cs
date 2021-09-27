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

        #region FIELDS

        private int _sr_id;
        private string _sr_name;
        private SelectionRuleType _sr_ruletype;
        private SelectionRuleLayerType _sr_layertype;
        private Layer _sr_layerobject;
        private string _sr_layerjson;
        private int _sr_minthreshold = 0;                  // default to 0
        private int _sr_enabled = 1;                        // default to 1
        private int _sr_hidden = 0;                         // default to 0
        private double _sr_area_m2 = 0;                     // default to 0
        private double _sr_area_ac = 0;                     // default to 0
        private double _sr_area_ha = 0;                     // default to 0
        private double _sr_area_km2 = 0;                    // default to 0
        private int _sr_pucount = 0;                        // default to 0

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Unique ID of the selection rule.
        /// </summary>
        public int SR_ID
        {
            get => _sr_id;
            set => _sr_id = value;
        }

        /// <summary>
        /// Unique name of the selection rule.
        /// </summary>
        public string SR_Name
        {
            get => _sr_name;
            set => _sr_name = value;
        }

        /// <summary>
        /// Selection Rules can be either Include or Exclude rule types.
        /// </summary>
        public SelectionRuleType SR_RuleType
        {
            get => _sr_ruletype;
            set => _sr_ruletype = value;
        }

        /// <summary>
        /// The source layer can be of type Vector or Raster.
        /// </summary>
        public SelectionRuleLayerType SR_LayerType
        {
            get => _sr_layertype;
            set => _sr_layertype = value;
        }

        /// <summary>
        /// The ArcGIS.Desktop.Mapping.Layer Object associated with the source layer.
        /// </summary>
        public Layer SR_LayerObject
        {
            get => _sr_layerobject;
            set => _sr_layerobject = value;
        }

        /// <summary>
        /// Source layer object serialized to JSON string
        /// </summary>
        public string SR_LayerJson
        {
            get => _sr_layerjson;
            set => _sr_layerjson = value;
        }

        /// <summary>
        /// Integer between 0 and 100 inclusive.  Represents the % of the Planning Unit's area below which
        /// any coverage of this Selection Rule will be deemed to not count as 'present' on the planning unit.
        /// </summary>
        public int SR_MinThreshold
        {
            get => _sr_minthreshold;
            set => _sr_minthreshold = value;
        }

        /// <summary>
        /// Integer, 0 (disabled) and 1 (enabled).  Applies to the Where to Work Tool.
        /// </summary>
        public int SR_Enabled
        {
            get => _sr_enabled;
            set => _sr_enabled = value;
        }

        /// <summary>
        /// Integer, 0 (not hidden) and 1 (hidden).  Applies to the Where to Work Tool.
        /// </summary>
        public int SR_Hidden
        {
            get => _sr_hidden;
            set => _sr_hidden = value;
        }

        /// <summary>
        /// Total Area (m2) covered by the Selection Rule, summed across all planning units.
        /// </summary>
        public double SR_Area_M2
        {
            get => _sr_area_m2;
            set => _sr_area_m2 = value;
        }

        /// <summary>
        /// Total Area (ac) covered by the Selection Rule, summed across all planning units.
        /// </summary>
        public double SR_Area_Ac
        {
            get => _sr_area_ac;
            set => _sr_area_ac = value;
        }

        /// <summary>
        /// Total Area (ha) covered by the Selection Rule, summed across all planning units.
        /// </summary>
        public double SR_Area_Ha
        {
            get => _sr_area_ha;
            set => _sr_area_ha = value;
        }

        /// <summary>
        /// Total Area (km2) covered by the Selection Rule, summed across all planning units.
        /// </summary>
        public double SR_Area_Km2
        {
            get => _sr_area_km2;
            set => _sr_area_km2 = value;
        }

        /// <summary>
        /// Number of Planning Units having coverage greater than the minimum threshold
        /// of the Selection Rule within them.
        /// </summary>
        public int SR_PUCount
        {
            get => _sr_pucount;
            set => _sr_pucount = value;
        }

        #endregion



    }
}
