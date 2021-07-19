using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;


namespace NCC.PRZTools
{
    internal class PlanningUnitTileInfo
    {
        internal PlanningUnitTileShape tile_shape;
        internal double tile_area;
        internal int tiles_up;
        internal int tiles_across;
        internal double tile_edge_length;
        internal double tile_width;
        internal double tile_height;
        internal double tile_center_to_right;
        internal double tile_center_to_top;
        internal MapPoint LL_Point;

        internal PlanningUnitTileInfo()
        {
        }

    }
}
