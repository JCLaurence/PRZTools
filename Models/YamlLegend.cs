using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRZC = NCC.PRZTools.PRZConstants;

namespace NCC.PRZTools
{
    public class YamlLegend
    {
        public YamlLegend()
        {
        }

        // Legend Type ('categorical', 'continuous', or 'manual')
        public string type = WTWLegendType.continuous.ToString();

        // Legend Class color array (hex format)
        public string[] colors = new string[]
        {
            "#ffffff",
            "#f7b740",
            "#4e7a07",
            "#05789c"
        };

        // Legend Class Labels (only for manual legends)
        public string[] labels;

    }
}
