namespace NCC.PRZTools
{

    internal enum WorkspaceDisplayMode
    {
        DIR,
        GDB,
        LOG
    }

    internal static class PRZConstants
    {
        // LAYER NAMES
        public const string c_GROUPLAYER_PRZ = "PRZ";
        public const string c_GROUPLAYER_COST = "COST";
        public const string c_GROUPLAYER_STATUS = "STATUS";
        public const string c_GROUPLAYER_STATUS_INCLUDE = "LOCKED IN";
        public const string c_GROUPLAYER_STATUS_EXCLUDE = "LOCKED OUT";
        public const string c_GROUPLAYER_CF = "CONSERVATION FEATURES";
        public const string c_LAYER_PLANNING_UNITS = "Planning Units";

        // MAJOR OBJECTS
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

        public const string c_PREFIX_PUCOMP = "pucf_tmp";
        public const string c_SUFFIX_PUCOMP_INT = "_Int";
        public const string c_SUFFIX_PUCOMP_DSLV = "_Dslv";

        // TABLE NAMES
        public const string c_TABLENAME_STATUSINFO = "PU_Status";
        public const string c_TABLENAME_BOUNDARYLENGTH = "PU_Boundary";
        public const string c_TABLENAME_EXTERIORTILES = "PU_OuterTiles";
        public const string c_TABLENAME_CF = "CF";
        public const string c_TABLENAME_PUVCF = "PU_CF";
        public const string c_TABLENAME_BESTSOLUTION = "MXN_BestSolution";
        public const string c_TABLENAME_SUMSOLUTION = "MXN_SummedSolution";
        public const string c_TABLENAME_RUNSUMMARY = "MXN_RunSummary";
        public const string c_TABLENAME_MISSINGVALUES = "MXN_BestSolution_MV";
        public const string c_TABLENAME_SOLUTIONPREFIX = "MXN_Solution_";

        // FIELD NAMES
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

        // Status DataTable Fields
        public const string c_FLD_DATATABLE_STATUS_INDEX = "index";
        public const string c_FLD_DATATABLE_STATUS_NAME = "name";
        public const string c_FLD_DATATABLE_STATUS_THRESHOLD = "threshold";
        public const string c_FLD_DATATABLE_STATUS_STATUS = "status";

        // Status Info table
        public const string c_FLD_STATUSINFO_ID = "id";
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

        // Conservation Features table
        public const string c_FLD_CF_ID = "id";         //THE CONSERVATION FEATURE ID
        public const string c_FLD_CF_NAME = "name";     //THE CONSERVATION FEATURE NAME
        public const string c_FLD_CF_TARGET = "target";     //THE TARGET (DOUBLE)
        public const string c_FLD_CF_TARGETPROP = "prop";   //THE PROPORTIONAL TARGET (INT 0 TO 100)
        public const string c_FLD_CF_TARGETOCC = "targetocc";  // THE NUMBER OF PLANNING UNITS THAT MUST CONTAIN THE CF (LONG - alternative to target)
        public const string c_FLD_CF_SPF = "spf";           //SPECIES PENALTY FACTOR (>0)
        public const string c_FLD_CF_THRESHOLD = "threshold";
        public const string c_FLD_CF_LAYERINDEX = "layerindex"; //layer index in group layer
        public const string c_FLD_CF_LAYERNAME = "layername";   //layer name in group layer
        public const string c_FLD_CF_WHERECLAUSE = "whereclause";   //whereclause (may be blank)
        public const string c_FLD_CF_TOTALAREA_M2 = "totalarea_m2";
        public const string c_FLD_CF_TOTALAREA_HA = "totalarea_ha";
        public const string c_FLD_CF_TOTALAREA_KM2 = "totalarea_km2";
        public const string c_FLD_CF_TILECOUNT = "tilecount";
        public const string c_FLD_CF_USE = "use";               //use this field in marxan - "yes", "no"

        public const string c_FLD_CFDT_LAYERINDEX = "index";
        public const string c_FLD_CFDT_LAYERNAME = "name";
        public const string c_FLD_CFDT_LAYERTHRESHOLD = "threshold";
        public const string c_FLD_CFDT_TARGETPROP = "targetprop";
        public const string c_FLD_CFDT_WHERECLAUSE = "whereclause";
        public const string c_FLD_CFDT_CFID = "cfid";
        public const string c_FLD_CFDT_CFNAME = "cfname";

        public const int c_ADJACENTFEATURECOUNT_HEXAGON = 6;    //number of features adjacent to a completely encircled hexagon feature
        public const int c_ADJACENTFEATURECOUNT_SQUARE = 4;     //number of features adjacent to a completely encirled square or diamond feature


        // LOGGING & DIRECTORIES
        public const string c_PRZ_LOGFILE = "PRZ.log";
        public const string c_PRZ_PROJECT_FGDB = "PRZ.gdb";
        public const string c_USER_PROFILE_WORKDIR = "PRZTools";


    }
}
