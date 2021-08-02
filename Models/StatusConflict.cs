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
        public double conflict_area_ac { get; set; }
        public double conflict_area_ha { get; set; }
        public double conflict_area_km2 { get; set; }
        public string include { get; set; }
        public string exclude { get; set; }

    }
}
