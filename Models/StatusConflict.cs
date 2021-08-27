using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NCC.PRZTools
{
    public class StatusConflict
    {

        public int conflict_num { get; set; }
        public int pu_count { get; set; }
        public string include_layer_name { get; set; }
        public string exclude_layer_name { get; set; }

        public int include_area_field_index { get; set; }
        public int exclude_area_field_index { get; set; }


    }
}
