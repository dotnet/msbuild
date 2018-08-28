using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Linq;
using System.Text;

namespace Mono.Build.Tasks
{
    public class FilterDeniedAssemblies : Task
    {
        static string s_deniedListFilename = "deniedAssembliesList.txt";

        static string s_deniedListFullPath => Path.Combine(
                                                    Path.GetDirectoryName(typeof (FilterDeniedAssemblies).Assembly.Location),
                                                    s_deniedListFilename);

        // Using the valueFactory overload to get exception caching
        static Lazy<ExclusionDB> s_db = new Lazy<ExclusionDB>(() => new ExclusionDB(s_deniedListFullPath));

        static bool s_haveWarnedAboutMissingList = false;

        public override bool Execute ()
        {
            FilteredReferences = References;

            if (!File.Exists (s_deniedListFullPath)) {
                if (!s_haveWarnedAboutMissingList) {
                    Log.LogMessage (MessageImportance.Low,
                                    $"Could not find the list of denied assemblies at {s_deniedListFullPath}. Please file a bug report at https://github.com/mono/mono/issues .");

                    s_haveWarnedAboutMissingList = true;
                }

                return !Log.HasLoggedErrors;
            }

            if (s_db.Value.Empty) {
                // nothing to filter!
                return !Log.HasLoggedErrors;
            }

            var deniedReferencesNotFixedItemsList = new List<ITaskItem> ();
            var filteredItems = new List<ITaskItem> ();

            foreach (var referenceItem in References) {
                // Try to find the path corresponding to a reference
                // - item Include might itself be a path
                // - or it might have a HintPath with the path
                bool foundInHintPath = false;
                var assemblyPathFromReference = referenceItem.GetMetadata("FullPath");

                if (!File.Exists (assemblyPathFromReference)) {
                    var hintPath = referenceItem.GetMetadata ("HintPath");
                    if (!String.IsNullOrEmpty (hintPath)) {
                        assemblyPathFromReference = Path.GetFullPath (hintPath);
                        if (!File.Exists(assemblyPathFromReference))
                            assemblyPathFromReference = null;
                        else
                            foundInHintPath = true;
                    }
                }

                if (assemblyPathFromReference != null && s_db.Value.IsDeniedAssembly (assemblyPathFromReference)) {
                    referenceItem.SetMetadata ("DeniedAssemblyPath", assemblyPathFromReference);

                    // Try to find the "safe" assembly under @SearchPaths, and update the reference

                    var assemblyFilename = Path.GetFileName (assemblyPathFromReference);
                    var safeAssemblyFilePath = SearchPaths
                                                .Select (d => Path.Combine (d, assemblyFilename))
                                                .Where (f => File.Exists (f))
                                                .FirstOrDefault ();

                    if (safeAssemblyFilePath != null) {
                        if (foundInHintPath)
                            referenceItem.SetMetadata ("HintPath", safeAssemblyFilePath);
                        else
                            referenceItem.ItemSpec = safeAssemblyFilePath;

                        referenceItem.SetMetadata ("FixedDeniedAssemblyPath", "true");

                        Log.LogMessage (MessageImportance.Low, $"Changed the denied (windows specific) assembly reference path from {assemblyPathFromReference} to the safe assembly path {safeAssemblyFilePath}.");
                    } else {
                        Log.LogMessage (MessageImportance.Low,
                                        $"Could not find the replacement assembly ({assemblyFilename}) for the Windows specific reference {assemblyPathFromReference}.");

                        referenceItem.SetMetadata ("FixedDeniedAssemblyPath", "false");
                        deniedReferencesNotFixedItemsList.Add (referenceItem);
                    }
                }

                filteredItems.Add (referenceItem);
            }

            DeniedReferencesThatCouldNotBeFixed = deniedReferencesNotFixedItemsList.ToArray ();
            FilteredReferences = filteredItems.ToArray ();

            return !Log.HasLoggedErrors;
        }

        [Required]
        public ITaskItem[]  References { get; set; }

        [Required]
        public string[]     SearchPaths { get; set; }

        [Output]
        public ITaskItem[]  DeniedReferencesThatCouldNotBeFixed { get; set; }

        [Output]
        public ITaskItem[]  FilteredReferences { get; set; }
    }

    class ExclusionDB
    {
        public HashSet<string>  ExclusionSet;
        public List<string>     ExclusionNamesList;
        public bool             Empty;

        public ExclusionDB(string deniedListFilePath)
        {
            Empty = true;
            ExclusionSet = new HashSet<string>();
            ExclusionNamesList = new List<string>();

            if (!File.Exists (deniedListFilePath))
                throw new FileNotFoundException($"Could not find the list of denied assemblies at {deniedListFilePath}", deniedListFilePath);

            var lines = File.ReadAllLines(deniedListFilePath);
            foreach (var line in lines) {
                var comma = line.IndexOf (",");
                if (comma < 0)
                    continue;

                var filename = line.Substring (0, comma).Trim ();
                if (filename.Length > 0) {
                    ExclusionSet.Add (line);
                    ExclusionNamesList.Add (filename);
                }
            }

            Empty = ExclusionNamesList.Count == 0;
        }

        public bool IsDeniedAssembly (string assemblyFullPath)
        {
            var assemblyFilename = Path.GetFileName (assemblyFullPath);

            return ExclusionNamesList.Contains (assemblyFilename) &&
                    ExclusionSet.Contains (CreateKeyForAssembly (assemblyFullPath));
        }

        static string CreateKeyForAssembly (string fullpath)
        {
            if (String.IsNullOrEmpty (fullpath) || !File.Exists (fullpath))
                return String.Empty;

            var filename = Path.GetFileName (fullpath);
            Version ver;

            using (var stream = File.OpenRead(fullpath))
            using (var peFile = new PEReader(stream))
            {
                var metadataReader = peFile.GetMetadataReader();

                var entry = metadataReader.GetAssemblyDefinition();
                ver = entry.Version;
                var guid = metadataReader.GetGuid(metadataReader.GetModuleDefinition().Mvid);

                var id = guid.ToString (null, CultureInfo.InvariantCulture).ToUpperInvariant ();
                return $"{filename},{id},{ver.Major},{ver.Minor},{ver.Build},{ver.Revision}";
            }
        }
    }
}
