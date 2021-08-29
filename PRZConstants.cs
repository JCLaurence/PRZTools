namespace NCC.PRZTools
{

    /// <summary>
    /// Used on the WorkspaceSettings dialog to indicate the content listing mode
    /// </summary>
    public enum WorkspaceDisplayMode
    {
        DIR,    // Workspace Folder contents
        GDB,    // Workspace File GDB contents
        LOG     // Workspace Log File contents
    }

    /// <summary>
    /// Applies to individual messages logged to the PRZ log file. 
    /// </summary>
    public enum LogMessageType
    {
        INFO,
        WARNING,
        VALIDATION_ERROR,
        ERROR
    }

    /// <summary>
    /// 
    /// </summary>
    public enum PlanningUnitTileShape
    {
        SQUARE,
        HEXAGON
    }

    public enum PRZLayerNames
    {
        MAIN,
        STATUS,
        STATUS_INCLUDE,
        STATUS_EXCLUDE,
        COST,
        CF,
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

    public enum CFLayerType
    {
        RASTER,
        VECTOR
    }

    public static class PRZConstants
    {
        // LAYER NAMES
        public const string c_GROUPLAYER_PRZ = "PRIORITIZATION TOOLS";
        public const string c_GROUPLAYER_COST = "COST";
        public const string c_GROUPLAYER_STATUS = "STATUS";
        public const string c_GROUPLAYER_STATUS_INCLUDE = "INCLUDE";
        public const string c_GROUPLAYER_STATUS_EXCLUDE = "EXCLUDE";
        public const string c_GROUPLAYER_CF = "CONSERVATION FEATURES";
        public const string c_LAYER_PLANNING_UNITS = "Planning Units";
        public const string c_LAYER_STUDY_AREA = "Study Area";
        public const string c_LAYER_STUDY_AREA_BUFFER = "Study Area Buffer";

        // FILES
        public const string c_TEMP_FILEGDB_NAME = "PRZTemp.gdb";
        public const string c_FILENAME_MARXAN = "Marxan.exe";
        public const string c_FILENAME_MARXAN32 = "Marxan_v243_x32.exe";
        public const string c_FILENAME_MARXAN64 = "Marxan_v243_x64.exe";
        public const string c_FILENAME_INPUTDAT = "input.dat";
        public const string C_FILENAME_INEDIT = "Inedit.exe";

        public const string c_FILENAME_OUTPUT_BESTSOLUTION = "_best.txt";
        public const string c_FILENAME_OUTPUT_SUMMEDSOLUTION = "_ssoln.txt";
        public const string c_FILENAME_OUTPUT_RUNSUMMARY = "_sum.txt";
        public const string c_FILENAME_OUTPUT_MISSINGVALUES = "_mvbest.txt";

        // EXPORT TO FILE NAMES
        public const string c_FILENAME_EXPORT_PU = "pu.dat";
        public const string c_FILENAME_EXPORT_CF = "cf.dat";
        public const string c_FILENAME_EXPORT_PUVCF = "puvscf.dat";
        public const string c_FILENAME_EXPORT_BL = "bound.dat";

        // FEATURE CLASSES
        public const string c_FC_PLANNING_UNITS = "PU";
        public const string c_FC_STUDY_AREA_MAIN = "study_area";
        public const string c_FC_STUDY_AREA_MAIN_BUFFERED = "study_area_buffered";
        public const string c_FC_STUDY_AREA_MULTI = "study_area_multi";
        public const string c_FC_STUDY_AREA_MULTI_BUFFERED = "study_area_multi_buffered";

        public const string c_PREFIX_PUCOMP = "pucf_tmp";
        public const string c_SUFFIX_PUCOMP_INT = "_Int";
        public const string c_SUFFIX_PUCOMP_DSLV = "_Dslv";

        // TABLE NAMES
        public const string c_TABLE_STATUSINFO = "PU_Status";
        public const string c_TABLE_BOUNDARYLENGTH = "PU_Boundary";
        public const string c_TABLE_EXTERIORTILES = "PU_OuterTiles";
        public const string c_TABLE_CF = "CF";
        public const string c_TABLE_PUVCF = "PU_CF";
        public const string c_TABLE_BESTSOLUTION = "MXN_BestSolution";
        public const string c_TABLE_SUMSOLUTION = "MXN_SummedSolution";
        public const string c_TABLE_RUNSUMMARY = "MXN_RunSummary";
        public const string c_TABLE_MISSINGVALUES = "MXN_BestSolution_MV";
        public const string c_TABLE_SOLUTIONPREFIX = "MXN_Solution_";
        public const string c_TABLE_COSTSTATS = "PU_CostStats";

        // FIELD NAMES

        // Conservation Features table
        public const string c_FLD_CF_ID = "id";                     // THE CONSERVATION FEATURE ID
        public const string c_FLD_CF_NAME = "name";                 // THE CONSERVATION FEATURE NAME
        public const string c_FLD_CF_TARGET = "target";             // THE TARGET (DOUBLE)
        public const string c_FLD_CF_TARGETPROP = "prop";           // THE PROPORTIONAL TARGET (INT 0 TO 100)
        public const string c_FLD_CF_TARGETOCC = "targetocc";       // THE NUMBER OF PLANNING UNITS THAT MUST CONTAIN THE CF (LONG - alternative to target)
        public const string c_FLD_CF_SPF = "spf";                   // SPECIES PENALTY FACTOR (>0)
        public const string c_FLD_CF_THRESHOLD = "threshold";
        public const string c_FLD_CF_LAYERINDEX = "layerindex"; //layer index in group layer
        public const string c_FLD_CF_LAYERNAME = "layername";   //layer name in group layer
        public const string c_FLD_CF_WHERECLAUSE = "whereclause";   //whereclause (may be blank)
        public const string c_FLD_CF_TOTALAREA_M2 = "totalarea_m2";
        public const string c_FLD_CF_TOTALAREA_HA = "totalarea_ha";
        public const string c_FLD_CF_TOTALAREA_KM2 = "totalarea_km2";
        public const string c_FLD_CF_TILECOUNT = "tilecount";
        public const string c_FLD_CF_USE = "use";               //use this field in marxan - "yes", "no"

        // Conservation Feature DataTable
        public const string c_FLD_CFDT_LAYER = "layer";
        public const string c_FLD_CFDT_LAYERINDEX = "index";
        public const string c_FLD_CFDT_LAYERNAME = "name";
        public const string c_FLD_CFDT_LAYERTHRESHOLD = "threshold";
        public const string c_FLD_CFDT_TARGETPROP = "targetprop";
        public const string c_FLD_CFDT_WHERECLAUSE = "whereclause";
        public const string c_FLD_CFDT_CFID = "cfid";
        public const string c_FLD_CFDT_CFNAME = "cfname";

        // Cost Stats
        public const string c_FLD_COST_ID = c_FLD_PUFC_ID;
        public const string c_FLD_COST_COUNT = "COUNT";
        public const string c_FLD_COST_AREA = "AREA";
        public const string c_FLD_COST_MIN = "MIN";
        public const string c_FLD_COST_MAX = "MAX";
        public const string c_FLD_COST_RANGE = "RANGE";
        public const string c_FLD_COST_MEAN = "MEAN";
        public const string c_FLD_COST_SUM = "SUM";
        public const string c_FLD_COST_STD = "STD";
        public const string c_FLD_COST_MEDIAN = "MEDIAN";

        // Run Summary
        public const string c_FLD_RUNSUM_RUNID = "Run_Number";
        public const string c_FLD_RUNSUM_SCORE = "Score";
        public const string c_FLD_RUNSUM_COST = "Cost";
        public const string c_FLD_RUNSUM_PLANNINGUNITS = "Planning_Units";
        public const string c_FLD_RUNSUM_BOUNDLENGTH = "Boundary_Length";
        public const string c_FLD_RUNSUM_CONNECTIVITYTOTAL = "Connectivity_Total";
        public const string c_FLD_RUNSUM_CONNECTIVITYIN = "Connectivity_In";
        public const string c_FLD_RUNSUM_CONNECTIVITYEDGE = "Connectivity_Edge";
        public const string c_FLD_RUNSUM_CONNECTIVITYOUT = "Connectivity_Out";
        public const string c_FLD_RUNSUM_CONNECTIVITYINFRACTION = "Connectivity_In_Fraction";
        public const string c_FLD_RUNSUM_PENALTY = "Penalty";
        public const string c_FLD_RUNSUM_SHORTFALL = "Shortfall";
        public const string c_FLD_RUNSUM_MISSINGVALUES = "Missing_Values";

        // missing values
        public const string c_FLD_MV_CFID = "CFID";
        public const string c_FLD_MV_CFNAME = "CF_Name";
        public const string c_FLD_MV_TARGET = "Target";
        public const string c_FLD_MV_AMOUNTHELD = "AmountHeld";
        public const string c_FLD_MV_OCCTARGET = "OccurrenceTarget";
        public const string c_FLD_MV_OCCHELD = "OccurrencesHeld";
        public const string c_FLD_MV_SEPTARGET = "SeparationTarget";
        public const string c_FLD_MV_SEPACHIEVED = "SeparationAchieved";
        public const string c_FLD_MV_TARGETMET = "TargetMet";
        public const string c_FLD_MV_MPM = "MPM";

        // Best Solution table
        public const string c_FLD_BESTSOLN_PUID = "PUID";
        public const string c_FLD_BESTSOLN_SELECTED = "selected";

        // Solution Tables
        public const string c_FLD_SOLN_PUID = "PUID";
        public const string c_FLD_SOLN_SELECTED = "selected";

        // Sum Solution table
        public const string c_FLD_SUMSOLN_PUID = "PUID";
        public const string c_FLD_SUMSOLN_FREQUENCY = "frequency";

        // Planning Unit Feature Class		
        public const string c_FLD_PUFC_ID = "id";
        public const string c_FLD_PUFC_STATUS = "status";
        public const string c_FLD_PUFC_COST = "cost";
        public const string c_FLD_PUFC_NCC_ID = "ncc_id";
        public const string c_FLD_PUFC_AREA_M = "square_m";
        public const string c_FLD_PUFC_AREA_AC = "acres";
        public const string c_FLD_PUFC_AREA_HA = "hectares";
        public const string c_FLD_PUFC_AREA_KM = "square_km";
        public const string c_FLD_PUFC_CONFLICT = "conflict";

        // Study Area Feature Class
        public const string c_FLD_SAFC_AREA_AC = "acres";
        public const string c_FLD_SAFC_AREA_HA = "hectares";
        public const string c_FLD_SAFC_AREA_KM = "square_km";

        // Status DataTable Fields
        public const string c_FLD_DATATABLE_STATUS_LAYER = "layer";
        public const string c_FLD_DATATABLE_STATUS_INDEX = "index";
        public const string c_FLD_DATATABLE_STATUS_NAME = "name";
        public const string c_FLD_DATATABLE_STATUS_THRESHOLD = "threshold";
        public const string c_FLD_DATATABLE_STATUS_STATUS = "status";

        // Status Info table
        public const string c_FLD_STATUSINFO_ID = c_FLD_PUFC_ID;
        public const string c_FLD_STATUSINFO_QUICKSTATUS = "quickstatus";
        public const string c_FLD_STATUSINFO_CONFLICT = "conflict";

        // Boundary Length table		
        public const string c_FLD_BL_ID1 = "id1";
        public const string c_FLD_BL_ID2 = "id2";
        public const string c_FLD_BL_BOUNDARY = "boundary";
        public const string c_FLD_BL_EXTERNAL = "external";

        // Exterior Tiles table
        public const string c_FLD_EXTILE_ID = "id";
        public const string c_FLD_EXTILE_EXTERIOR = "exterior";
        public const string c_FLD_EXTILE_OPENSIDES = "opensides";

        // Planning Unit Versus CF (PUVCF) table		
        public const string c_FLD_PUVCF_ID = "id";
        public const string c_FLD_PUVCF_CFCOUNT = "cf_count";



        public const int c_ADJACENTFEATURECOUNT_HEXAGON = 6;    //number of features adjacent to a completely encircled hexagon feature
        public const int c_ADJACENTFEATURECOUNT_SQUARE = 4;     //number of features adjacent to a completely encirled square or diamond feature

        // CONVERSION
        public const double c_CONVERT_M2_TO_AC = 0.000247105;
        public const double c_CONVERT_M2_TO_HA = 0.0001;
        public const double c_CONVERT_M2_TO_KM2 = 0.000001;

        // LOGGING & DIRECTORIES
        public const string c_PRZ_LOGFILE = "PRZ.log";
        public const string c_PRZ_PROJECT_FGDB = "PRZ.gdb";
        public const string c_USER_PROFILE_WORKDIR = "PRZTools";


    }
}
