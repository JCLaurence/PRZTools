namespace NCC.PRZTools
{

    /// <summary>
    /// Used on the WorkspaceSettings dialog to indicate the content listing mode
    /// </summary>
    internal enum WorkspaceDisplayMode
    {
        DIR,    // Workspace Folder contents
        GDB,    // Workspace File GDB contents
        LOG     // Workspace Log File contents
    }

    /// <summary>
    /// Applies to individual messages logged to the PRZ log file. 
    /// </summary>
    internal enum LogMessageType
    {
        INFO,
        WARNING,
        ERROR
    }

    /// <summary>
    /// 
    /// </summary>
    internal enum PlanningUnitTileShape
    {
        SQUARE,
        HEXAGON
    }

    internal static class PRZConstants
    {
        // LAYER NAMES
        internal const string c_GROUPLAYER_PRZ = "PRZ";
        internal const string c_GROUPLAYER_COST = "COST";
        internal const string c_GROUPLAYER_STATUS = "STATUS";
        internal const string c_GROUPLAYER_STATUS_INCLUDE = "LOCKED IN";
        internal const string c_GROUPLAYER_STATUS_EXCLUDE = "LOCKED OUT";
        internal const string c_GROUPLAYER_CF = "CONSERVATION FEATURES";
        internal const string c_LAYER_PLANNING_UNITS = "Planning Units";

        // MAJOR OBJECTS
        internal const string c_TEMP_FILEGDB_NAME = "PRZTemp.gdb";
        internal const string c_FILENAME_MARXAN = "Marxan.exe";
        internal const string c_FILENAME_MARXAN32 = "Marxan_v243_x32.exe";
        internal const string c_FILENAME_MARXAN64 = "Marxan_v243_x64.exe";
        internal const string c_FILENAME_INPUTDAT = "input.dat";
        internal const string C_FILENAME_INEDIT = "Inedit.exe";

        internal const string c_FILENAME_OUTPUT_BESTSOLUTION = "_best.txt";
        internal const string c_FILENAME_OUTPUT_SUMMEDSOLUTION = "_ssoln.txt";
        internal const string c_FILENAME_OUTPUT_RUNSUMMARY = "_sum.txt";
        internal const string c_FILENAME_OUTPUT_MISSINGVALUES = "_mvbest.txt";

        // EXPORT TO FILE NAMES
        internal const string c_FILENAME_EXPORT_PU = "pu.dat";
        internal const string c_FILENAME_EXPORT_CF = "cf.dat";
        internal const string c_FILENAME_EXPORT_PUVCF = "puvscf.dat";
        internal const string c_FILENAME_EXPORT_BL = "bound.dat";

        // FEATURE CLASSES
        internal const string c_FC_PLANNING_UNITS = "PU";
        internal const string c_FC_STUDY_AREA_MAIN = "study_area";
        internal const string c_FC_STUDY_AREA_MAIN_BUFFERED = "study_area_buffered";
        internal const string c_FC_STUDY_AREA_MULTI = "study_area_multi";
        internal const string c_FC_STUDY_AREA_MULTI_BUFFERED = "study_area_multi_buffered";

        internal const string c_PREFIX_PUCOMP = "pucf_tmp";
        internal const string c_SUFFIX_PUCOMP_INT = "_Int";
        internal const string c_SUFFIX_PUCOMP_DSLV = "_Dslv";

        // TABLE NAMES
        internal const string c_TABLENAME_STATUSINFO = "PU_Status";
        internal const string c_TABLENAME_BOUNDARYLENGTH = "PU_Boundary";
        internal const string c_TABLENAME_EXTERIORTILES = "PU_OuterTiles";
        internal const string c_TABLENAME_CF = "CF";
        internal const string c_TABLENAME_PUVCF = "PU_CF";
        internal const string c_TABLENAME_BESTSOLUTION = "MXN_BestSolution";
        internal const string c_TABLENAME_SUMSOLUTION = "MXN_SummedSolution";
        internal const string c_TABLENAME_RUNSUMMARY = "MXN_RunSummary";
        internal const string c_TABLENAME_MISSINGVALUES = "MXN_BestSolution_MV";
        internal const string c_TABLENAME_SOLUTIONPREFIX = "MXN_Solution_";

        // FIELD NAMES
        // Run Summary
        internal const string c_FLD_RUNSUM_RUNID = "Run_Number";
        internal const string c_FLD_RUNSUM_SCORE = "Score";
        internal const string c_FLD_RUNSUM_COST = "Cost";
        internal const string c_FLD_RUNSUM_PLANNINGUNITS = "Planning_Units";
        internal const string c_FLD_RUNSUM_BOUNDLENGTH = "Boundary_Length";
        internal const string c_FLD_RUNSUM_CONNECTIVITYTOTAL = "Connectivity_Total";
        internal const string c_FLD_RUNSUM_CONNECTIVITYIN = "Connectivity_In";
        internal const string c_FLD_RUNSUM_CONNECTIVITYEDGE = "Connectivity_Edge";
        internal const string c_FLD_RUNSUM_CONNECTIVITYOUT = "Connectivity_Out";
        internal const string c_FLD_RUNSUM_CONNECTIVITYINFRACTION = "Connectivity_In_Fraction";
        internal const string c_FLD_RUNSUM_PENALTY = "Penalty";
        internal const string c_FLD_RUNSUM_SHORTFALL = "Shortfall";
        internal const string c_FLD_RUNSUM_MISSINGVALUES = "Missing_Values";

        // missing values
        internal const string c_FLD_MV_CFID = "CFID";
        internal const string c_FLD_MV_CFNAME = "CF_Name";
        internal const string c_FLD_MV_TARGET = "Target";
        internal const string c_FLD_MV_AMOUNTHELD = "AmountHeld";
        internal const string c_FLD_MV_OCCTARGET = "OccurrenceTarget";
        internal const string c_FLD_MV_OCCHELD = "OccurrencesHeld";
        internal const string c_FLD_MV_SEPTARGET = "SeparationTarget";
        internal const string c_FLD_MV_SEPACHIEVED = "SeparationAchieved";
        internal const string c_FLD_MV_TARGETMET = "TargetMet";
        internal const string c_FLD_MV_MPM = "MPM";

        // Best Solution table
        internal const string c_FLD_BESTSOLN_PUID = "PUID";
        internal const string c_FLD_BESTSOLN_SELECTED = "selected";

        // Solution Tables
        internal const string c_FLD_SOLN_PUID = "PUID";
        internal const string c_FLD_SOLN_SELECTED = "selected";

        // Sum Solution table
        internal const string c_FLD_SUMSOLN_PUID = "PUID";
        internal const string c_FLD_SUMSOLN_FREQUENCY = "frequency";

        // Planning Unit Feature Class		
        internal const string c_FLD_PUFC_ID = "id";
        internal const string c_FLD_PUFC_STATUS = "status";
        internal const string c_FLD_PUFC_COST = "cost";
        internal const string c_FLD_NCC_ID = "ncc_id";

        // Status DataTable Fields
        internal const string c_FLD_DATATABLE_STATUS_INDEX = "index";
        internal const string c_FLD_DATATABLE_STATUS_NAME = "name";
        internal const string c_FLD_DATATABLE_STATUS_THRESHOLD = "threshold";
        internal const string c_FLD_DATATABLE_STATUS_STATUS = "status";

        // Status Info table
        internal const string c_FLD_STATUSINFO_ID = "id";
        internal const string c_FLD_STATUSINFO_QUICKSTATUS = "quickstatus";
        internal const string c_FLD_STATUSINFO_CONFLICT = "conflict";

        // Boundary Length table		
        internal const string c_FLD_BL_ID1 = "id1";
        internal const string c_FLD_BL_ID2 = "id2";
        internal const string c_FLD_BL_BOUNDARY = "boundary";
        internal const string c_FLD_BL_EXTERNAL = "external";

        // Exterior Tiles table
        internal const string c_FLD_EXTILE_ID = "id";
        internal const string c_FLD_EXTILE_EXTERIOR = "exterior";
        internal const string c_FLD_EXTILE_OPENSIDES = "opensides";

        // Planning Unit Versus CF (PUVCF) table		
        internal const string c_FLD_PUVCF_ID = "id";
        internal const string c_FLD_PUVCF_CFCOUNT = "cf_count";

        // Conservation Features table
        internal const string c_FLD_CF_ID = "id";         //THE CONSERVATION FEATURE ID
        internal const string c_FLD_CF_NAME = "name";     //THE CONSERVATION FEATURE NAME
        internal const string c_FLD_CF_TARGET = "target";     //THE TARGET (DOUBLE)
        internal const string c_FLD_CF_TARGETPROP = "prop";   //THE PROPORTIONAL TARGET (INT 0 TO 100)
        internal const string c_FLD_CF_TARGETOCC = "targetocc";  // THE NUMBER OF PLANNING UNITS THAT MUST CONTAIN THE CF (LONG - alternative to target)
        internal const string c_FLD_CF_SPF = "spf";           //SPECIES PENALTY FACTOR (>0)
        internal const string c_FLD_CF_THRESHOLD = "threshold";
        internal const string c_FLD_CF_LAYERINDEX = "layerindex"; //layer index in group layer
        internal const string c_FLD_CF_LAYERNAME = "layername";   //layer name in group layer
        internal const string c_FLD_CF_WHERECLAUSE = "whereclause";   //whereclause (may be blank)
        internal const string c_FLD_CF_TOTALAREA_M2 = "totalarea_m2";
        internal const string c_FLD_CF_TOTALAREA_HA = "totalarea_ha";
        internal const string c_FLD_CF_TOTALAREA_KM2 = "totalarea_km2";
        internal const string c_FLD_CF_TILECOUNT = "tilecount";
        internal const string c_FLD_CF_USE = "use";               //use this field in marxan - "yes", "no"

        internal const string c_FLD_CFDT_LAYERINDEX = "index";
        internal const string c_FLD_CFDT_LAYERNAME = "name";
        internal const string c_FLD_CFDT_LAYERTHRESHOLD = "threshold";
        internal const string c_FLD_CFDT_TARGETPROP = "targetprop";
        internal const string c_FLD_CFDT_WHERECLAUSE = "whereclause";
        internal const string c_FLD_CFDT_CFID = "cfid";
        internal const string c_FLD_CFDT_CFNAME = "cfname";

        internal const int c_ADJACENTFEATURECOUNT_HEXAGON = 6;    //number of features adjacent to a completely encircled hexagon feature
        internal const int c_ADJACENTFEATURECOUNT_SQUARE = 4;     //number of features adjacent to a completely encirled square or diamond feature


        // LOGGING & DIRECTORIES
        internal const string c_PRZ_LOGFILE = "PRZ.log";
        internal const string c_PRZ_PROJECT_FGDB = "PRZ.gdb";
        internal const string c_USER_PROFILE_WORKDIR = "PRZTools";


    }
}
