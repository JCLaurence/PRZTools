using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace NCC.PRZTools
{
    public class SelectionRuleConflict
    {
        public SelectionRuleConflict()
        {

        }

        public int conflict_id { get; set; }

        public int include_rule_id { get; set; }

        public string include_rule_name { get; set; }

        public string include_rule_statefield { get; set; }

        public int exclude_rule_id { get; set; }

        public string exclude_rule_name { get; set; }

        public string exclude_rule_statefield { get; set; }

        public int pu_count { get; set; }


    }
}
