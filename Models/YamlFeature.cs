using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NCC.PRZTools
{
    public class YamlFeature
    {
        public YamlFeature()
        {
        }

        public string name;

        public YamlVariable variable;

        public bool initial_status;

        public bool initial_visible;

        public double initial_goal;

        public double min_goal;

        public double max_goal;

        public double step_goal;

        public double limit_goal;

        public double current;

        public string icon;



    }
}
