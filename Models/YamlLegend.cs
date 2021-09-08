using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NCC.PRZTools
{
    public class YamlLegend
    {
        public YamlLegend()
        {
        }

        public string type;         // what are the acceptable values here.  Examples show 'categorical', 'continuous', and 'manual'

        public string[] colors;     // array elements are color strings in the format #E41A1C

        public string[] labels;     // only seem to be used in the YamlInclude object...

    }
}
