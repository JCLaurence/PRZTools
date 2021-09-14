using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRZC = NCC.PRZTools.PRZConstants;

namespace NCC.PRZTools
{
    public class YamlTheme
    {
        public YamlTheme()
        {
        }

        // Theme Name (a label)
        public string name;

        // Array of Features
        public YamlFeature[] feature;

    }
}
