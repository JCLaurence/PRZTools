using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Geometry;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZC = NCC.PRZTools.PRZConstants;
using PRZH = NCC.PRZTools.PRZHelper;
using PRZM = NCC.PRZTools.PRZMethods;

namespace NCC.PRZTools
{
    public enum BoundaryRelationship
    {
        GeometriesAreEqual,
        GeometriesAreDisjoint,
        GridEnvelopeContainsGeometry,
        GeometryContainsGridEnvelope,
        GeometriesOverlap,
        GeometriesTouch,
        UndefinedRelationship
    }

    public enum NationalGridDimension
    {
        dim0_1m,
        dim1_10m,
        dim2_100m,
        dim3_1000m,
        dim4_10000m,
        dim5_100000m
    }

    public class NationalGridInfo
    {
        static NationalGridInfo()
        {
            CANADA_ALBERS_SR = PRZH.GetSR_PRZCanadaAlbers();
        }

        public NationalGridInfo(Polygon gridPoly)
        {
            try
            {
                var res = GenerateFromPolygon(gridPoly);

                _cellIsValid = res.success;
                _constructorMessage = res.message;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        public NationalGridInfo(string identifier)
        {
            try
            {
                var res = GenerateFromIdentifier(identifier);

                _cellIsValid = res.success;
                _constructorMessage = res.message;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

        #region FIELDS

        private const int c_MIN_X_COORDINATE = -2500000;    // Minimum X for National Grid's extent
        private const int c_MIN_Y_COORDINATE = 200000;      // Minimum Y "
        private const int c_MAX_X_COORDINATE = 3200000;     // Maximum X "
        private const int c_MAX_Y_COORDINATE = 4900000;     // Maximum Y "

        private string _cellIdentifier;
        private bool _cellIsValid = false;
        private string _constructorMessage;
        private Polygon _cellPolygon;
        private Envelope _cellEnvelope;
        private MapPoint _cellCenterPoint;
        private double _cellSideLength;
        private double _cellArea;
        private double _cell_MinX;
        private double _cell_MinY;
        private double _cell_MaxX;
        private double _cell_MaxY;

        #endregion

        #region STATIC PROPERTIES

        public static int MIN_X_COORDINATE
        {
            get => c_MIN_X_COORDINATE;
        }
        public static int MIN_Y_COORDINATE
        {
            get => c_MIN_Y_COORDINATE;
        }
        public static int MAX_X_COORDINATE
        {
            get => c_MAX_X_COORDINATE;
        }
        public static int MAX_Y_COORDINATE
        {
            get => c_MAX_Y_COORDINATE;
        }
        public static SpatialReference CANADA_ALBERS_SR { get; private set; }

        #endregion

        #region INSTANCE PROPERTIES

        public Polygon CellPolygon
        {
            get => _cellPolygon;
        }
        public Envelope CellEnvelope
        {
            get => _cellEnvelope;
        }
        public MapPoint CellCenterPoint
        {
            get => _cellCenterPoint;
        }
        public bool CellIsValid
        {
            get => _cellIsValid;
        }
        public string ConstructorMessage
        {
            get => _constructorMessage;
        }
        public double CellSideLength
        {
            get => _cellSideLength;
        }
        public string CellIdentifier
        {
            get => _cellIdentifier;
        }
        public double Cell_MinX
        {
            get => _cell_MinX;
        }
        public double Cell_MinY
        {
            get => _cell_MinY;
        }
        public double Cell_MaxX
        {
            get => _cell_MaxX;
        }
        public double Cell_MaxY
        {
            get => _cell_MaxY;
        }
        public double CellArea
        {
            get => _cellArea;
        }

        #endregion

        #region STATIC METHODS

        /// <summary>
        /// Assembles a National Grid Identifier from the three separate components (x, y, and dimension)
        /// </summary>
        /// <param name="XMIN"></param>
        /// <param name="YMIN"></param>
        /// <param name="gridDimension"></param>
        /// <returns></returns>
        public static string GetIdentifier(int XMIN, int YMIN, int gridDimension)
        {
            try
            {
                string x_suffix = (XMIN < 0) ? "W" : "E";

                string abscissa = Math.Abs(XMIN).ToString("D7");
                string ordinate = YMIN.ToString("D7");
                string dimension = gridDimension.ToString();

                string identifier = abscissa + x_suffix + "_" + ordinate + "_" + dimension;
                return identifier;
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return "";
            }
        }

        /// <summary>
        /// Returns an envelope corresponding to the full outer bounds of the National Grid system.
        /// </summary>
        /// <returns></returns>
        public static Envelope GetOuterBounds()
        {
            try
            {
                // Retrieve National Grid Spatial Reference
                EnvelopeBuilderEx builderEx = new EnvelopeBuilderEx(MIN_X_COORDINATE, MIN_Y_COORDINATE, MAX_X_COORDINATE, MAX_Y_COORDINATE, CANADA_ALBERS_SR);
                return (Envelope)builderEx.ToGeometry();
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return null;
            }
        }

        /// <summary>
        /// For a supplied geometry (of type Polygon or Envelope), determines the spatial relationship between the geometry
        /// and the National Grid's outer bounds envelope.
        /// </summary>
        /// <param name="geom"></param>
        /// <returns></returns>
        public static (bool success, BoundaryRelationship relationship, string message) SpatialRelationshipWithOuterBounds(Geometry geom)
        {
            try
            {
                // no null geometry!
                if (geom == null || geom.IsEmpty)
                {
                    return (false, BoundaryRelationship.UndefinedRelationship, "Null or empty geometry provided");
                }

                // Ensure the geometry is either an envelope or a polygon
                if (!(geom is Envelope | geom is Polygon))
                {
                    return (false, BoundaryRelationship.UndefinedRelationship, "geometry is not of type envelope or polygon");
                }

                // Simplify the geometry
                if (!GeometryEngine.Instance.IsSimpleAsFeature(geom))
                {
                    geom = GeometryEngine.Instance.SimplifyAsFeature(geom);
                }

                // Project the geometry if required
                SpatialReference geomSR = geom.SpatialReference;

                if (!SpatialReference.AreEqual(CANADA_ALBERS_SR, geomSR))
                {
                    geom = GeometryEngine.Instance.Project(geom, CANADA_ALBERS_SR);
                }

                Geometry gridEnvelope = GetOuterBounds();

                // Geometries are EQUAL
                if (GeometryEngine.Instance.Equals(gridEnvelope, geom))
                {
                    return (true, BoundaryRelationship.GeometriesAreEqual, "Success");
                }

                // Geometries are DISJOINT
                else if (GeometryEngine.Instance.Disjoint(gridEnvelope, geom))
                {
                    return (true, BoundaryRelationship.GeometriesAreDisjoint, "Success");
                }

                // Grid Envelope CONTAINS Geometry
                else if (GeometryEngine.Instance.Contains(gridEnvelope, geom))
                {
                    return (true, BoundaryRelationship.GridEnvelopeContainsGeometry, "Success");
                }

                // Grid Envelope is WITHIN Geometry (this one is weird and unlikely)
                else if (GeometryEngine.Instance.Within(gridEnvelope, geom))
                {
                    return (true, BoundaryRelationship.GeometryContainsGridEnvelope, "Success");
                }

                // Grid Envelope OVERLAPS Geometry
                else if (GeometryEngine.Instance.Overlaps(gridEnvelope, geom))
                {
                    return (true, BoundaryRelationship.GeometriesOverlap, "Success");
                }

                // Grid Envelope TOUCHES Geometry
                else if (GeometryEngine.Instance.Touches(gridEnvelope, geom))
                {
                    return (true, BoundaryRelationship.GeometriesTouch, "Success");
                }

                // Some other strange spatial relationship we want nothing to do with
                else
                {
                    return (false, BoundaryRelationship.UndefinedRelationship, "Undefined Relationship");
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return (false, BoundaryRelationship.UndefinedRelationship, ex.Message);
            }
        }

        /// <summary>
        /// Returns an Envelope that encompasses the supplied geometry, and that can be filled completely with
        /// whole square tiles of the size defined by the supplied grid dimension.
        /// </summary>
        /// <param name="study_area_geom"></param>
        /// <param name="grid_dimension"></param>
        /// <returns></returns>
        public static (bool success, Envelope gridEnv, string message, int tilesAcross, int tilesUp) GetGridBoundsFromStudyArea(Geometry geom, NationalGridDimension gridDimension)
        {
            try
            {
                #region VALIDATION

                // Ensure that geometry is not null or empty
                if (geom == null || geom.IsEmpty)
                {
                    return (false, null, "Geometry is null or empty", 0, 0);
                }

                // Ensure the geometry is either an envelope or a polygon
                if (!(geom is Envelope | geom is Polygon))
                {
                    return (false, null, "Geometry is not of type envelope or polygon", 0, 0);
                }

                // Simplify the geometry
                if (!GeometryEngine.Instance.IsSimpleAsFeature(geom))
                {
                    geom = GeometryEngine.Instance.SimplifyAsFeature(geom);
                }

                // Project the geometry if required
                SpatialReference geomSR = geom.SpatialReference;

                if (!SpatialReference.AreEqual(CANADA_ALBERS_SR, geomSR))
                {
                    geom = GeometryEngine.Instance.Project(geom, CANADA_ALBERS_SR);
                }

                // Get the National Grid Envelope
                Envelope gridEnv = GetOuterBounds();

                // Get the geometry Envelope
                Envelope geomEnv = geom.Extent;

                // Get the supplied grid dimension
                int dimension = (int)gridDimension;
                if (dimension < 0 | dimension > 5)
                {
                    return (false, null, "invalid grid dimension provided", 0, 0);
                }

                #endregion

                #region GENERATE ENVELOPE

                // Multiplier
                int multiplier = (int)Math.Pow(10, dimension);

                // Convert envelope coordinate values to integers
                int XMIN = (int)Math.Floor(geomEnv.XMin);
                int YMIN = (int)Math.Floor(geomEnv.YMin);
                int XMAX = (int)Math.Ceiling(geomEnv.XMax);
                int YMAX = (int)Math.Ceiling(geomEnv.YMax);

                // Adjust to specified dimension
                XMIN = (XMIN / multiplier) * multiplier;
                YMIN = (YMIN / multiplier) * multiplier;
                XMAX = (XMAX / multiplier) * multiplier;
                YMAX = (YMAX / multiplier) * multiplier;

                // Generate final values
                int XMIN_NEW = ((double)XMIN > geomEnv.XMin) ? XMIN - multiplier : XMIN;
                int YMIN_NEW = ((double)YMIN > geomEnv.YMin) ? YMIN - multiplier : YMIN;
                int XMAX_NEW = ((double)XMAX < geomEnv.XMax) ? XMAX + multiplier : XMAX;
                int YMAX_NEW = ((double)YMAX < geomEnv.YMax) ? YMAX + multiplier : YMAX;

                // Generate the new envelope
                Envelope outputEnv = EnvelopeBuilderEx.CreateEnvelope(XMIN_NEW, YMIN_NEW, XMAX_NEW, YMAX_NEW, CANADA_ALBERS_SR);

                if (outputEnv == null || outputEnv.IsEmpty)
                {
                    return (false, null, "Output envelope is null or empty", 0, 0);
                }

                // Ensure that the output envelope does not lie outside the national grid outer bounds
                if (XMIN_NEW < c_MIN_X_COORDINATE | YMIN_NEW < c_MIN_Y_COORDINATE | XMAX_NEW > c_MAX_X_COORDINATE | YMAX_NEW > c_MAX_Y_COORDINATE)
                {
                    return (false, null, "Output envelope lies wholly or partly outside of the National Grid outer bounds", 0, 0);
                }

                // Determine row and column count
                int outputWidth = Convert.ToInt32(outputEnv.Width);
                int outputHeight = Convert.ToInt32(outputEnv.Height);

                if (outputWidth == 0 | outputHeight == 0)
                {
                    return (false, null, "Output envelope has either no height or no width", 0, 0);
                }

                int tiles_across = outputWidth / multiplier;       // integer division
                int tiles_up = outputHeight / multiplier;          // integer division

                if (tiles_across == 0 | tiles_up == 0)
                {
                    return (false, null, "Unable to determine tile counts", 0, 0);
                }

                #endregion

                return (true, outputEnv, "Success", tiles_across, tiles_up);
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return (false, null, ex.Message, 0, 0);
            }
        }

        #endregion

        #region INSTANCE METHODS

        /// <summary>
        /// Instantiate a National Grid Info object based on a grid cell polygon.
        /// </summary>
        /// <param name="cellPoly"></param>
        /// <returns></returns>
        private (bool success, string message) GenerateFromPolygon(Polygon cellPoly)
        {
            try
            {
                // Ensure no null or empty geometry provided
                if (cellPoly == null || cellPoly.IsEmpty)
                {
                    return (false, "Cell geometry is null or empty");
                }

                // Polygon must have valid spatial reference
                SpatialReference cellSR = cellPoly.SpatialReference;

                if (cellSR == null)
                {
                    return (false, "Cell geometry has null spatial reference");
                }

                // Polygon spatial reference must be same as National Grid spatial reference
                if (!SpatialReference.AreEqual(CANADA_ALBERS_SR, cellSR))
                {
                    return (false, "Cell geometry has incorrect spatial reference");
                }

                // Polygon should have exactly 5 points (start and end points are coincident)
                var cellVertices = cellPoly.Points;
                if (cellPoly.PointCount != 5)
                {
                    return (false, "Cell geometry has more than 5 vertices");
                }

                // Get Polygon Envelope
                Envelope cellEnv = cellPoly.Extent;

                if (cellEnv == null || cellEnv.IsEmpty)
                {
                    return (false, "Cell geometry envelope is null or empty");
                }

                // Convert Envelope to Polygon
                Polygon cellEnvPoly = PolygonBuilderEx.CreatePolygon(cellEnv, CANADA_ALBERS_SR);

                if (cellEnvPoly == null || cellEnvPoly.IsEmpty)
                {
                    return (false, "Envelope to Polygon produces null or empty geometry");
                }

                // Compare polygons.  They must be identical
                bool are_equal = GeometryEngine.Instance.Equals(cellPoly, cellEnvPoly);

                if (!are_equal)
                {
                    return (false, "Cell geometry is not equal to its own envelope");
                }

                // Test squareness by comparing areas (i know geometry is rectangular and axis-aligned with SR)
                double perimeter = cellPoly.Length;
                double side_length = perimeter / 4.0;
                double area_from_sides = side_length * side_length;
                double area_from_poly = cellPoly.Area;

                if (area_from_sides != area_from_poly)
                {
                    return (false, "Cell geometry is not square");
                }

                // Ensure that the square side length is 1, 10, 100, 1000, or 10000 meters
                if (side_length != 1 & side_length != 10 & side_length != 100 & side_length != 1000 & side_length != 10000 & side_length != 100000)
                {
                    return (false, "Cell geometry has invalid side length for the national grid");
                }

                // Now assess the square's alignment
                double MinX = cellEnv.XMin;
                double MinY = cellEnv.YMin;

                double remainderX = Math.Abs(MinX % side_length);
                double remainderY = Math.Abs(MinY % side_length);

                if (remainderX != 0 & remainderY != 0)
                {
                    return (false, $"Cell geometry is misaligned with the {side_length} meter National Grid along both the X and Y axes");
                }
                else if (remainderX != 0)
                {
                    return (false, $"Cell geometry is misaligned with the {side_length} meter National Grid along the X axis");
                }
                else if (remainderY != 0)
                {
                    return (false, $"Cell geometry is misaligned with the {side_length} meter National Grid along the Y axis");
                }

                // Ensure that polygon does not lie even partly outside the National Grid outer bounds
                if (cellEnv.XMin < c_MIN_X_COORDINATE |
                    cellEnv.XMax > c_MAX_X_COORDINATE |
                    cellEnv.YMin < c_MIN_Y_COORDINATE |
                    cellEnv.YMax > c_MAX_Y_COORDINATE)
                {
                    return (false, "Cell geometry falls outside the National Grid outer bounds");
                }

                // Cell Geometry is fully validated!

                // Populate Instance Fields
                _cellPolygon = cellPoly;
                _cellEnvelope = cellEnv;
                _cellSideLength = side_length;
                _cellCenterPoint = cellEnv.Center;
                _cellArea = cellPoly.Area;
                _cell_MinX = cellEnv.XMin;
                _cell_MinY = cellEnv.YMin;
                _cell_MaxX = cellEnv.XMax;
                _cell_MaxY = cellEnv.YMax;

                // generate identifier from lower left coords and side length
                string Xsuffix = (cellEnv.XMin < 0) ? "W" : "E";

                int xmin = Convert.ToInt32(cellEnv.XMin);
                int ymin = Convert.ToInt32(cellEnv.YMin);

                string abscissa = Math.Abs(xmin).ToString("D7");
                string ordinate = ymin.ToString("D7");

                string dimension = "";

                switch(side_length)
                {
                    case 1:
                        dimension = "0";
                        break;
                    case 10:
                        dimension = "1";
                        break;
                    case 100:
                        dimension = "2";
                        break;
                    case 1000:
                        dimension = "3";
                        break;
                    case 10000:
                        dimension = "4";
                        break;
                    case 100000:
                        dimension = "5";
                        break;
                }

                string identifier = abscissa + Xsuffix + "_" + ordinate + "_" + dimension;
                _cellIdentifier = identifier;

                return (true, "success");
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Instantiate a National Grid Info object from a grid cell identifier.
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        private (bool success, string message) GenerateFromIdentifier(string identifier)
        {
            try
            {
                // Variables
                string identifier_element_1;         // Element 1 of parsed identifier (the X Coordinate + E/W modifier)
                string identifier_element_2;         // Element 2 of parsed identifier (The Y Coordinate)
                string identifier_element_3;         // Element 3 of parsed identifier (the grid size indicator (0, 1, 2, 3, 4, or 5)

                string x_coord_string;
                bool xIsPositive;

                int x_coord;                    // Canada Albers X coordinate (-9,999,999 to 9,999,999)
                int y_coord;                    // Canada Albers Y coordinate (0 to 9,999,999)
                int grid_size;                  // NCC National Grid scale (0=1m, 1=10m, 2=100m, 3=1km, 4=10km, 5=100km
                int side_length;                // length of the side of a single grid cell in meters, at the associated scale (grid_size)

                // Length validation (total length of identifier should always be 18 chars)
                if (identifier.Length != 18)
                {
                    return (false, "Identifier string is not 18 characters in length");
                }

                string[] parsed = identifier.Split('_');    // should contain 3 elements

                // parsed array should contain 3 elements
                if (parsed.Length != 3)
                {
                    return (false, "Identifier split on '_' does not have 3 elements");
                }

                // validate the x string (e.g. "3199000E")
                identifier_element_1 = parsed[0];
                if (identifier_element_1.Length != 8)
                {
                    return (false, "First element of identifier is not 8 characters in length");
                }

                if (identifier_element_1.EndsWith("E"))
                {
                    xIsPositive = true;
                }
                else if (identifier_element_1.EndsWith("W"))
                {
                    xIsPositive = false;
                }
                else
                {
                    return (false, "First element of identifier does not end in 'E' or 'W'");
                }

                x_coord_string = identifier_element_1.Substring(0, 7);

                foreach (char c in x_coord_string)
                {
                    if (c < '0' || c > '9')
                    {
                        return (false, "A character in the first identifier element is not a numeric digit 0 to 9");
                    }
                }

                if (!int.TryParse(x_coord_string, out x_coord))
                {
                    return (false, "First 7 digits of first element of identifier cannot be parsed to an integer value");
                }

                // Enforce need for "E" Identifier where X = 0
                if (x_coord == 0 && identifier_element_1.EndsWith("W"))
                {
                    return (false, "X coordinate of 0 must have associated 'E' qualifier, not 'W' qualifier");
                }

                if (!xIsPositive)
                {
                    x_coord *= -1;
                }

                // validate the y string (e.g. 4899000)
                identifier_element_2 = parsed[1];
                if (identifier_element_2.Length != 7)
                {
                    return (false, "Second element of identifier is not 7 characters in length");
                }

                foreach (char c in identifier_element_2)
                {
                    if (c < '0' || c > '9')
                    {
                        return (false, "A character in the second identifier element is not a numeric digit 0 to 9");
                    }
                }

                if (!int.TryParse(identifier_element_2, out y_coord))
                {
                    return (false, "The second element of the identifier cannot be parsed to an integer value");
                }

                // Validate the 3rd element (should be 0, 1, 2, 3, 4, 5)
                identifier_element_3 = parsed[2];

                if (identifier_element_3.Length != 1)
                {
                    return (false, "The third element of the identifier is not 1 character in length");
                }

                foreach (char c in identifier_element_2)
                {
                    if (c < '0' || c > '9')
                    {
                        return (false, "The third element of the identifier contains a character other than numeric digit 0-9");
                    }
                }

                if (!int.TryParse(identifier_element_3, out grid_size))
                {
                    return (false, "The third element of the identifier cannot be parsed to an integer value");
                }

                if (grid_size < 0 | grid_size > 5)
                {
                    return (false, "The third element of the identifier is an integer outside the allowed range of 0-5 inclusive");
                }

                // Calculate the side length of a single grid cell
                side_length = (int)Math.Pow(10, grid_size);

                // Assemble an envelope for the grid cell
                Envelope cellEnv = EnvelopeBuilderEx.CreateEnvelope(x_coord, y_coord, x_coord + side_length, y_coord + side_length, CANADA_ALBERS_SR);

                if (cellEnv == null || cellEnv.IsEmpty)
                {
                    return (false, "Grid cell envelope is null or empty");
                }

                // Create a Polygon from the envelope
                Polygon cellPoly = PolygonBuilderEx.CreatePolygon(cellEnv, CANADA_ALBERS_SR);

                if (cellPoly == null || cellPoly.IsEmpty)
                {
                    return (false, "Grid cell polygon is null or empty");
                }

                // Determine spatial relationship between cell polygon and Grid Bounds
                var result = SpatialRelationshipWithOuterBounds(cellPoly);

                if (!result.success)
                {
                    return (false, "Unable to determine spatial relationship between cell polygon and national grid bounds");
                }

                // Validate the spatial relationship
                if (result.relationship == BoundaryRelationship.GeometriesAreDisjoint)
                {
                    return (false, "Cell polygon lies entirely outside the national grid bounds");
                }
                else if (result.relationship == BoundaryRelationship.GeometriesAreEqual)
                {
                    return (false, "Cell polygon is identical to the national grid bounds.  What are you doing over there?");
                }
                else if (result.relationship == BoundaryRelationship.GeometriesOverlap)
                {
                    return (false, "Cell polygon is partly inside, and partly outside, the national grid bounds.");
                }
                else if (result.relationship == BoundaryRelationship.GeometriesTouch)
                {
                    return (false, "Cell polygon is adjacent to the national grid bounds, sharing node(s) and/or edge(s)");
                }
                else if (result.relationship == BoundaryRelationship.GeometryContainsGridEnvelope)
                {
                    return (false, "Cell polygon contains the national grid bounds entirely.  What are you doing over there?");
                }
                else if (result.relationship == BoundaryRelationship.UndefinedRelationship)
                {
                    return (false, "Spatial relationship between cell polygon and national grid bounds is undetermined.");
                }
                else if (result.relationship == BoundaryRelationship.GridEnvelopeContainsGeometry)
                {
                    // cell polygon is valid in all ways.  Populate the various properties of this class instance

                    _cellPolygon = cellPoly;
                    _cellEnvelope = cellEnv;
                    _cellSideLength = side_length;
                    _cellCenterPoint = cellEnv.Center;
                    _cellArea = cellPoly.Area;
                    _cellIdentifier = identifier;
                    _cell_MinX = cellEnv.XMin;
                    _cell_MinY = cellEnv.YMin;
                    _cell_MaxX = cellEnv.XMax;
                    _cell_MaxY = cellEnv.YMax;

                    // i'm here!!!

                    return (true, "Cell polygon lies entirely within the national grid bounds.");
                }
                else
                {
                    return (false, "Unreachable code reached.  Astounding!");
                }
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
                return (false, ex.Message);
            }
        }




        #endregion

    }
}
