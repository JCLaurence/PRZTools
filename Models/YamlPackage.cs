using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRZC = NCC.PRZTools.PRZConstants;

namespace NCC.PRZTools
{
    public class YamlPackage
    {
        public YamlPackage()
        {
        }

        // Project Name (required)
        public string name;

        // Author Name (optional but defaults in WTW to Richard Schuster)
        public string author_name = "Richard Schuster";

        // Author Email (optional but defaults in WTW to richard.schuster@natureconservancy.ca)
        public string author_email = "richard.schuster@natureconservancy.ca";

        // Name of shapefile - .shp extension and no path (e.g. the_shapefile.shp)
        public string spatial_path = PRZC.c_FILE_WTW_EXPORT_SHP;

        // Name of attribute csv file - .csv extension and no path (e.g. the_attributes.csv)
        public string attribute_path = PRZC.c_FILE_WTW_EXPORT_ATTR;

        // Name of boundary csv file - .cxv extension and no path (.e.g. the_boundary.csv)
        public string boundary_path = PRZC.c_FILE_WTW_EXPORT_BND;

        // must be 'beginner', 'advanced', or 'missing'
        public string mode = WTWModeType.beginner.ToString();

        public YamlTheme[] themes;

        public YamlWeight[] weights;

        public YamlInclude[] includes;

    }
}
