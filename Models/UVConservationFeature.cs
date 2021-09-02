using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NCC.PRZTools
{
    public class UVConservationFeature
    {

        public UVConservationFeature()
        {
        }

        public string GroupHeading { get; set; }

        public string ClassLabel { get; set; }
        
        public string WhereClause { get; set; }

        public int GroupThreshold { get; set; }

        public int GroupTarget { get; set; }

        public int ClassThreshold { get; set; }

        public int ClassTarget { get; set; }

    }
}
