using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NCC.PRZTools
{
    public class YamlTheme
    {
        public YamlTheme()
        {
        }

        public string name;                 // name of theme

        public bool mandatory;              // not sure how this value gets used in web application

        public string icon;                 // format appears to be map-marked-alt, probably web application specific icon

        public YamlFeature[] feature;

    }
}
