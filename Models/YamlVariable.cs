using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRZC = NCC.PRZTools.PRZConstants;

namespace NCC.PRZTools
{
    public class YamlVariable
    {
        public YamlVariable()
        {
        }

        // Name of the Attributes CSV column associated with this variable's parent feature
        public string index;

        // Unit label.  Any string is OK (e.g. 'ha', 'mm/year', 'sandwich'), but must at least be an empty string ( "" )
        public string units = "";

        // Scope of the variable ('regional', 'national', or 'missing')
        public string provenance = WTWProvenanceType.national.ToString();

        // Legend associated with this variable
        public YamlLegend legend;

    }
}
