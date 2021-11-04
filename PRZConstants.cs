namespace NCC.PRZTools
{

    #region ENUMERATIONS

    #region WHERE TO WORK

    public enum WTWLegendType
    {
        categorical,
        continuous,
        manual
    }

    public enum WTWModeType
    {
        beginner,
        advanced,
        missing
    }

    public enum WTWProvenanceType
    {
        regional,
        national,
        missing
    }

    #endregion

    #region PLANNING UNIT GENERATION

    #endregion

    public enum NationalDbType
    {
        FileGDB,
        EnterpriseGDB,
        Unknown
    }


    // SELECTION RULES

    public enum SelectionRuleLayerType
    {
        RASTER,
        FEATURE
    }

    public enum SelectionRuleType
    {
        INCLUDE,
        EXCLUDE
    }

    public enum FeatureLayerType
    {
        RASTER,
        FEATURE
    }

    public enum FieldCategory
    {
        NUMERIC,
        STRING,
        DATE,
        OTHER,
        UNKNOWN
    }

    public enum WorkspaceDisplayMode
    {
        DIR,    // Workspace Folder contents
        GDB     // Workspace File GDB contents
    }

    public enum LogMessageType
    {
        INFO,
        WARNING,
        VALIDATION_ERROR,
        ERROR
    }

    public enum PRZLayerNames
    {
        MAIN,
        SELRULES,
        SELRULES_INCLUDE,
        SELRULES_EXCLUDE,
        COST,
        FEATURES,
        PU,
        SA,
        SAB
    }

    public enum CostStatistics
    {
        MEAN,
        MAXIMUM,
        MEDIAN,
        MINIMUM,
        SUM
    }

    public enum PRZLayerRetrievalType
    {
        FEATURE,
        RASTER,
        BOTH
    }

    public enum PlanningUnitLayerType
    {
        FEATURE,
        RASTER
    }

    #endregion

    public static class PRZConstants
    {

        #region LAYER NAMES

        // GROUP LAYERS
        public const string c_GROUPLAYER_MAIN = "PRIORITIZATION TOOLS";
        public const string c_GROUPLAYER_COST = "COST";
        public const string c_GROUPLAYER_SELRULES = "SELECTION RULES";
        public const string c_GROUPLAYER_SELRULES_INCLUDE = "INCLUDE";
        public const string c_GROUPLAYER_SELRULES_EXCLUDE = "EXCLUDE";
        public const string c_GROUPLAYER_FEATURES = "FEATURES";

        // FEATURE LAYERS
        public const string c_LAYER_PLANNING_UNITS = "Planning Units";
        public const string c_LAYER_STUDY_AREA = "Study Area";
        public const string c_LAYER_STUDY_AREA_BUFFER = "Study Area Buffer";

        #endregion

        #region FILE AND FOLDER NAMES

        // FOLDERS
        public const string c_DIR_EXPORT_WTW = "EXPORT_WTW";

        // PROJECT FILES
        public const string c_FILE_PRZ_LOG = "PRZ.log";
        public const string c_FILE_PRZ_FGDB = "PRZ.gdb";         // Technically this is a folder, but lets treat it as a file here

        // EXPORT WTW FILES
        public const string c_FILE_WTW_EXPORT_SHP = "WTW_GEOMETRY.shp";
        public const string c_FILE_WTW_EXPORT_ATTR = "WTW_ATTRIBUTE.csv";
        public const string c_FILE_WTW_EXPORT_BND = "WTW_BOUNDARY.dat";
        public const string c_FILE_WTW_EXPORT_YAML = "WTW_CONFIG.yaml";

        #endregion

        #region GEODATABASE OBJECT NAMES

        // RASTER DATASETS
        public const string c_RAS_PLANNING_UNITS = "pu_ras";
        public const string c_RAS_TEMP_1 = "TempRas1";
        public const string c_RAS_TEMP_2 = "TempRas2";
        public const string c_RAS_TEMP_3 = "TempRas3";

        // FEATURE CLASSES
        public const string c_FC_PLANNING_UNITS = "planning_units";
        public const string c_FC_STUDY_AREA_MAIN = "study_area";
        public const string c_FC_STUDY_AREA_MAIN_BUFFERED = "study_area_buffered";

        public const string c_FC_TEMP_PUCF_PREFIX = "pucf_tmp";
        public const string c_FC_TEMP_PUCF_SUFFIX_INT = "_int";
        public const string c_FC_TEMP_PUCF_SUFFIX_DSLV = "_dslv";

        public const string c_FC_TEMP_PUSELRULES_PREFIX = "pusr_tmp";
        public const string c_FC_TEMP_PUSELRULES_SUFFIX_INT = "_int";
        public const string c_FC_TEMP_PUSELRULES_SUFFIX_DSLV = "_dslv";

        // TABLES
        public const string c_TABLE_PU_RAS_LOOKUP = "pu_ras_lookup";
        public const string c_TABLE_SELRULES = "selection_rules";
        public const string c_TABLE_PUSELRULES = "pu_selection_rules";
        public const string c_TABLE_PUBOUNDARY = "pu_boundary";
        public const string c_TABLE_FEATURES = "features";
        public const string c_TABLE_PUFEATURES = "pu_features";
        public const string c_TABLE_COSTSTATS = "pu_cost";      // this will change soon

        // NATIONAL DB TABLES
        public const string c_TABLE_NAT_GOAL = "goal";
        public const string c_TABLE_NAT_PREFIX_GOAL = "g_";

        #endregion

        #region COLUMN NAMES

        #region RASTER COLUMN NAMES

        public const string c_FLD_RAS_PU_VALUE = "Value";
        public const string c_FLD_RAS_PU_COUNT = "Count";
        public const string c_FLD_RAS_PU_ID = c_FLD_FC_PU_ID;
        public const string c_FLD_RAS_PU_NATGRID_ID = c_FLD_FC_PU_NATGRID_ID;
        public const string c_FLD_RAS_PU_CONFLICT = c_FLD_FC_PU_CONFLICT;
        public const string c_FLD_RAS_PU_COST = c_FLD_FC_PU_COST;
        public const string c_FLD_RAS_PU_AREA_M2 = c_FLD_FC_PU_AREA_M2;
        public const string c_FLD_RAS_PU_AREA_AC = c_FLD_FC_PU_AREA_AC;
        public const string c_FLD_RAS_PU_AREA_HA = c_FLD_FC_PU_AREA_HA;
        public const string c_FLD_RAS_PU_AREA_KM2 = c_FLD_FC_PU_AREA_KM2;
        public const string c_FLD_RAS_PU_FEATURECOUNT = c_FLD_FC_PU_FEATURECOUNT;
        public const string c_FLD_RAS_PU_SHARED_PERIM = c_FLD_FC_PU_SHARED_PERIM;
        public const string c_FLD_RAS_PU_UNSHARED_PERIM = c_FLD_FC_PU_UNSHARED_PERIM;
        public const string c_FLD_RAS_PU_HAS_UNSHARED_PERIM = c_FLD_FC_PU_HAS_UNSHARED_PERIM;

        #endregion

        #region FC COLUMN NAMES

        // PLANNING UNITS
        public const string c_FLD_FC_PU_ID = "id";
        public const string c_FLD_FC_PU_NATGRID_ID = "natgrid_id";
        public const string c_FLD_FC_PU_EFFECTIVE_RULE = "effective_rule";
        public const string c_FLD_FC_PU_COST = "cost";
        public const string c_FLD_FC_PU_AREA_M2 = "area_m2";
        public const string c_FLD_FC_PU_AREA_AC = "area_ac";
        public const string c_FLD_FC_PU_AREA_HA = "area_ha";
        public const string c_FLD_FC_PU_AREA_KM2 = "area_km2";
        public const string c_FLD_FC_PU_CONFLICT = "rule_conflict";
        public const string c_FLD_FC_PU_FEATURECOUNT = "feature_count";
        public const string c_FLD_FC_PU_SHARED_PERIM = "shared_perimeter_m";
        public const string c_FLD_FC_PU_UNSHARED_PERIM = "unshared_perimeter_m";
        public const string c_FLD_FC_PU_HAS_UNSHARED_PERIM = "has_unshared_perimeter";

        // STUDY AREA + STUDY AREA BUFFER
        public const string c_FLD_FC_STUDYAREA_AREA_M2 = "area_m2";
        public const string c_FLD_FC_STUDYAREA_AREA_AC = "area_ac";
        public const string c_FLD_FC_STUDYAREA_AREA_HA = "area_ha";
        public const string c_FLD_FC_STUDYAREA_AREA_KM2 = "area_km2";

        #endregion

        #region TABLE COLUMN NAMES

        // NATIONAL DB - MASTER GOAL TABLE
        public const string c_FLD_TAB_NAT_GOALS_ID = "id";
        public const string c_FLD_TAB_NAT_GOALS_NAME = "name";
        public const string c_FLD_TAB_NAT_GOALS_THEME = "theme";

        // NATIONAL DB - INDIVIDUAL GOAL TABLES
        public const string c_FLD_TAB_NAT_GOAL_CELL_NUMBER = "cell_number";
        public const string c_FLD_TAB_NAT_GOAL_CELL_VALUE = "cell_value";

        // PU + FEATURES
        public const string c_FLD_TAB_PUCF_ID = c_FLD_FC_PU_ID;
        public const string c_FLD_TAB_PUCF_FEATURECOUNT = c_FLD_FC_PU_FEATURECOUNT;
        public const string c_FLD_TAB_PUCF_PREFIX_CF = "CF_";
        public const string c_FLD_TAB_PUCF_SUFFIX_AREA = "_AREA";
        public const string c_FLD_TAB_PUCF_SUFFIX_COVERAGE = "_COV";
        public const string c_FLD_TAB_PUCF_SUFFIX_STATE = "_STATE";

        // PU + SELECTION RULES
        public const string c_FLD_TAB_PUSELRULES_ID = c_FLD_FC_PU_ID;
        public const string c_FLD_TAB_PUSELRULES_EFFECTIVE_RULE = "effective_rule";
        public const string c_FLD_TAB_PUSELRULES_CONFLICT = "rule_conflict";
        public const string c_FLD_TAB_PUSELRULES_PREFIX_INCLUDE = "IN_";
        public const string c_FLD_TAB_PUSELRULES_PREFIX_EXCLUDE = "EX_";
        public const string c_FLD_TAB_PUSELRULES_SUFFIX_AREA = "_AREA";
        public const string c_FLD_TAB_PUSELRULES_SUFFIX_COVERAGE = "_COV";
        public const string c_FLD_TAB_PUSELRULES_SUFFIX_STATE = "_STATE";

        // SELECTION RULES
        public const string c_FLD_TAB_SELRULES_ID = "sr_id";
        public const string c_FLD_TAB_SELRULES_NAME = "sr_name";
        public const string c_FLD_TAB_SELRULES_RULETYPE = "sr_rule_type";
        public const string c_FLD_TAB_SELRULES_LAYERTYPE = "sr_layer_type";
        public const string c_FLD_TAB_SELRULES_LAYERJSON = "sr_layer_json";
        public const string c_FLD_TAB_SELRULES_MIN_THRESHOLD = "sr_min_threshold";
        public const string c_FLD_TAB_SELRULES_ENABLED = "sr_enabled";
        public const string c_FLD_TAB_SELRULES_HIDDEN = "sr_hidden";
        public const string c_FLD_TAB_SELRULES_AREA_M2 = "sr_area_m2";
        public const string c_FLD_TAB_SELRULES_AREA_AC = "sr_area_ac";
        public const string c_FLD_TAB_SELRULES_AREA_HA = "sr_area_ha";
        public const string c_FLD_TAB_SELRULES_AREA_KM2 = "sr_area_km2";
        public const string c_FLD_TAB_SELRULES_PUCOUNT = "sr_pucount";

        // FEATURES
        public const string c_FLD_TAB_CF_ID = "cf_id";
        public const string c_FLD_TAB_CF_NAME = "cf_name";
        public const string c_FLD_TAB_CF_MIN_THRESHOLD = "cf_min_threshold";
        public const string c_FLD_TAB_CF_GOAL = "cf_goal";
        public const string c_FLD_TAB_CF_WHERECLAUSE = "cf_whereclause";
        public const string c_FLD_TAB_CF_ENABLED = "cf_enabled";
        public const string c_FLD_TAB_CF_HIDDEN = "cf_hidden";
        public const string c_FLD_TAB_CF_AREA_M2 = "cf_area_m2";
        public const string c_FLD_TAB_CF_AREA_AC = "cf_area_ac";
        public const string c_FLD_TAB_CF_AREA_HA = "cf_area_ha";
        public const string c_FLD_TAB_CF_AREA_KM2 = "cf_area_km2";
        public const string c_FLD_TAB_CF_PUCOUNT = "cf_pucount";
        public const string c_FLD_TAB_CF_LYR_NAME = "lyr_name";
        public const string c_FLD_TAB_CF_LYR_TYPE = "lyr_type";
        public const string c_FLD_TAB_CF_LYR_JSON = "lyr_json";
        public const string c_FLD_TAB_CF_LYR_MIN_THRESHOLD = "lyr_min_threshold";   // Probably not necessary
        public const string c_FLD_TAB_CF_LYR_GOAL = "lyr_goal";                 // Probably not necessary

        // BOUNDARY
        public const string c_FLD_TAB_BOUND_ID1 = "id1";
        public const string c_FLD_TAB_BOUND_ID2 = "id2";
        public const string c_FLD_TAB_BOUND_BOUNDARY = "boundary";

        // COST ZONAL STATS
        public const string c_FLD_COST_ID = c_FLD_FC_PU_ID;
        public const string c_FLD_COST_COUNT = "COUNT";
        public const string c_FLD_COST_AREA = "AREA";
        public const string c_FLD_COST_MIN = "MIN";
        public const string c_FLD_COST_MAX = "MAX";
        public const string c_FLD_COST_RANGE = "RANGE";
        public const string c_FLD_COST_MEAN = "MEAN";
        public const string c_FLD_COST_SUM = "SUM";
        public const string c_FLD_COST_STD = "STD";
        public const string c_FLD_COST_MEDIAN = "MEDIAN";

        // FEATURE ZONAL STATS
        public const string c_FLD_ZONALSTATS_ID = c_FLD_FC_PU_ID;
        public const string c_FLD_ZONALSTATS_COUNT = "COUNT";
        public const string c_FLD_ZONALSTATS_AREA = "AREA";
        public const string c_FLD_ZONALSTATS_MIN = "MIN";
        public const string c_FLD_ZONALSTATS_MAX = "MAX";
        public const string c_FLD_ZONALSTATS_RANGE = "RANGE";
        public const string c_FLD_ZONALSTATS_MEAN = "MEAN";
        public const string c_FLD_ZONALSTATS_SUM = "SUM";
        public const string c_FLD_ZONALSTATS_STD = "STD";
        public const string c_FLD_ZONALSTATS_MEDIAN = "MEDIAN";

        #endregion

        #endregion

        #region SPATIAL REFERENCES

        public const string c_SR_WKT_WGS84_CanadaAlbers = @"PROJCS[""Canada_Albers_WGS_1984"",GEOGCS[""GCS_WGS_1984"",DATUM[""D_WGS_1984"",SPHEROID[""WGS_1984"",6378137.0,298.257223563]],PRIMEM[""Greenwich"",0.0],UNIT[""Degree"",0.0174532925199433]],PROJECTION[""Albers""],PARAMETER[""False_Easting"",0.0],PARAMETER[""False_Northing"",0.0],PARAMETER[""Central_Meridian"",-96.0],PARAMETER[""Standard_Parallel_1"",50.0],PARAMETER[""Standard_Parallel_2"",70.0],PARAMETER[""Latitude_Of_Origin"",40.0],UNIT[""Meter"",1.0]]";

        #endregion

        // CONVERSION
        public const double c_CONVERT_M2_TO_AC = 0.000247105;
        public const double c_CONVERT_M2_TO_HA = 0.0001;
        public const double c_CONVERT_M2_TO_KM2 = 0.000001;

        // MISCELLANEOUS
        public const string c_REGEX_THRESHOLD_PERCENT_PATTERN_ANY = @"\[\d{1,3}\]";       // [n], [nn], or [nnn] anywhere in string
        public const string c_REGEX_THRESHOLD_PERCENT_PATTERN_START = @"^\[\d{1,3}\]";    // [n], [nn], or [nnn] start of string
        public const string c_REGEX_THRESHOLD_PERCENT_PATTERN_END = @"$\[\d{1,3}\]";      // [n], [nn], or [nnn] end of string
        public const string c_REGEX_GOAL_PERCENT_PATTERN_ANY = @"{\d{1,3}}";       // {n}, {nn}, or {nnn} anywhere in string
        public const string c_REGEX_GOAL_PERCENT_PATTERN_START = @"^{\d{1,3}}";    // {n}, {nn}, or {nnn} start of string
        public const string c_REGEX_GOAL_PERCENT_PATTERN_END = @"${\d{1,3}}";      // {n}, {nn}, or {nnn} end of string

    }
}
