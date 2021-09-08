using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NCC.PRZTools
{
    public class YamlPackage
    {
        public YamlPackage()
        {
        }

        public string name;                 // Not sure how the package name gets used or if there's a naming convention or what

        public string spatial_path;         // what are the supported path formats?  example shows asdf/asdf/asdf/asdf.tif

        public string attribute_path;       // same

        public string boundary_path;        // same

        public string mode = "advanced";    // not sure what the supported values are, this should be an enum

        public YamlTheme[] themes;

        public YamlWeight[] weights;

        public YamlInclude[] includes;

    }
}
