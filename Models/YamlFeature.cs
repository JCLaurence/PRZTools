using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRZC = NCC.PRZTools.PRZConstants;

namespace NCC.PRZTools
{
    public class YamlFeature
    {
        public YamlFeature()
        {
        }

        // Feature Name (a label)
        public string name;

        // The variable associated with this feature
        public YamlVariable variable;

        // Indicates if this feature is to be "used" in the WTW calculations
        public bool status = true;

        // Indicates if the feature's associated layer is visible in the WTW viewer
        public bool visible = true;

        // Goal or target: ranges from 0 to 1, inclusive
        public double goal = 0;

        // Lower threshold: ranges from 0 to 1, inclusive.  Must also be <= goal.
        public double limit_goal = 0;

    }
}
