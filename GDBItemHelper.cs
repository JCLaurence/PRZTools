using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;


namespace NCC.PRZTools
{
    //GDBItemHelper utility class for retrieving datasets from File, Enterprise, or Shapefile GDB items
    internal static class GDBItemHelper
    {

        // Keywords
        public const string FeatureClassKeyword = "Feature Class";
        public const string FeatureDatasetKeyword = "Feature Dataset";
        public const string TableKeyword = "Table";
        public const string RelationshipClassKeyword = "Relationship Class";

        //Must use QueuedTask
        public static Dataset GetDatasetFromItem(Item item)
        {
            //self-documenting...
            if (!QueuedTask.OnWorker)
                throw new ArcGIS.Core.CalledOnWrongThreadException();

            //File
            if (item.TypeID.StartsWith("fgdb_") || item.TypeID.EndsWith("_fgdb"))
                return OpenDatasetForExtension(".gdb", item);
            //Enterprise
            if (item.TypeID.StartsWith("egdb_") || item.TypeID.EndsWith("_egdb"))
                return OpenDatasetForExtension(".sde", item);
            //Shape
            if (item.TypeID.StartsWith("shapefile_"))
                return OpenDatasetForExtension(".shp", item);
            return null;
        }

        //use this flavor if you only have a path
        public static Dataset GetDatasetFromPath(string path)
        {
            //self-documenting...
            if (!QueuedTask.OnWorker)
                throw new ArcGIS.Core.CalledOnWrongThreadException();

            //check the path is valid - File, Enterprise, Shape
            int indexOfgdb = path.LastIndexOf(".gdb", StringComparison.InvariantCultureIgnoreCase);
            if (indexOfgdb == -1)
                indexOfgdb = path.LastIndexOf(".sde", StringComparison.InvariantCultureIgnoreCase);
            if (indexOfgdb == -1)
                indexOfgdb = path.LastIndexOf(".shp", StringComparison.InvariantCultureIgnoreCase);
            if (indexOfgdb == -1)
                return null;

            //make the item
            var item = ItemFactory.Instance.Create(path);
            //retrieve the dataset
            return GetDatasetFromItem(item);
        }

        private static Dataset OpenDatasetForExtension(string extension, Item item)
        {

            int indexOfgdb = item.Path.LastIndexOf(extension,
                                         StringComparison.InvariantCultureIgnoreCase);
            if (indexOfgdb == -1)
                return null;

            string gdbPath = item.Path.Substring(0, indexOfgdb + extension.Length);

            if (extension == ".sde")
            {
                using (var geodatabase = new Geodatabase(
                                            new DatabaseConnectionFile(new Uri(gdbPath))))
                {
                    if (item.TypeKeywords.Contains(FeatureClassKeyword))
                        return geodatabase.OpenDataset<FeatureClass>(item.Name);

                    if (item.TypeKeywords.Contains(FeatureDatasetKeyword))
                        return geodatabase.OpenDataset<FeatureDataset>(item.Name);

                    if (item.TypeKeywords.Contains(TableKeyword))
                        return geodatabase.OpenDataset<Table>(item.Name);

                    if (item.TypeKeywords.Contains(RelationshipClassKeyword))
                        return geodatabase.OpenDataset<RelationshipClass>(item.Name);
                }
            }
            else if (extension == ".gdb")
            {
                using (var geodatabase = new Geodatabase(
                                            new FileGeodatabaseConnectionPath(new Uri(gdbPath))))
                {
                    if (item.TypeKeywords.Contains(FeatureClassKeyword))
                        return geodatabase.OpenDataset<FeatureClass>(item.Name);

                    if (item.TypeKeywords.Contains(FeatureDatasetKeyword))
                        return geodatabase.OpenDataset<FeatureDataset>(item.Name);

                    if (item.TypeKeywords.Contains(TableKeyword))
                        return geodatabase.OpenDataset<Table>(item.Name);

                    if (item.TypeKeywords.Contains(RelationshipClassKeyword))
                        return geodatabase.OpenDataset<RelationshipClass>(item.Name);
                }
            }
            else if (extension == ".shp")
            {
                var shapePath = System.IO.Path.GetDirectoryName(gdbPath);
                using (var shape = new FileSystemDatastore(new FileSystemConnectionPath(
                                                               new Uri(shapePath),
                                                               FileSystemDatastoreType.Shapefile)))
                {
                    return shape.OpenDataset<FeatureClass>(item.Name);
                }
            }
            return null;
        }
    }


}
