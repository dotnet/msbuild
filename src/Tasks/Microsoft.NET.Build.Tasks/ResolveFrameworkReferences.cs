using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// This class processes the FrameworkReference items.  It adds PackageReferences for the
    /// targeting packs which provide the reference assemblies, and creates RuntimeFramework
    /// items, which are written to the runtimeconfig file
    /// </summary>
    public class ResolveFrameworkReferences : TaskBase
    {
        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] KnownFrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        [Output]
        public ITaskItem[] PackageReferencesToAdd { get; set; }

        [Output]
        public ITaskItem[] RuntimeFrameworks { get; set; }

        [Output]
        public string[] UnresolvedFrameworkReferences { get; set; }

        protected override void ExecuteCore()
        {
            var knownFrameworkReferences = KnownFrameworkReferences.Select(item => new KnownFrameworkReference(item))
                .ToDictionary(kfr => kfr.Name);

            List<ITaskItem> packageReferencesToAdd = new List<ITaskItem>();
            List<ITaskItem> runtimeFrameworks = new List<ITaskItem>();
            List<string> unresolvedFrameworkReferences = new List<string>();


            foreach (var frameworkReference in FrameworkReferences)
            {
                KnownFrameworkReference knownFrameworkReference;
                if (knownFrameworkReferences.TryGetValue(frameworkReference.ItemSpec, out knownFrameworkReference))
                {
                    TaskItem packageReference = new TaskItem(knownFrameworkReference.TargetingPackName);
                    packageReference.SetMetadata(MetadataKeys.Version, knownFrameworkReference.TargetingPackVersion);
                    packageReference.SetMetadata(MetadataKeys.IsImplicitlyDefined, "true");
                    packageReference.SetMetadata("PrivateAssets", "true");

                    packageReferencesToAdd.Add(packageReference);

                    TaskItem runtimeFramework = new TaskItem(knownFrameworkReference.RuntimeFrameworkName);

                    //  Use default (non roll-forward) version for now.  Eventually we'll need to add support for rolling
                    //  forward, and for publishing assets from a runtime pack for self-contained apps
                    runtimeFramework.SetMetadata(MetadataKeys.Version, knownFrameworkReference.DefaultRuntimeFrameworkVersion);

                    runtimeFrameworks.Add(runtimeFramework);
                }
                else
                {
                    unresolvedFrameworkReferences.Add(frameworkReference.ItemSpec);
                }
            }

            if (packageReferencesToAdd.Any())
            {
                PackageReferencesToAdd = packageReferencesToAdd.ToArray();
            }

            if (runtimeFrameworks.Any())
            {
                RuntimeFrameworks = runtimeFrameworks.ToArray();
            }

            if (unresolvedFrameworkReferences.Any())
            {
                UnresolvedFrameworkReferences = unresolvedFrameworkReferences.ToArray();
            }
        }

        class KnownFrameworkReference
        {
            ITaskItem _item;
            public KnownFrameworkReference(ITaskItem item)
            {
                _item = item;
            }

            //  The name / itemspec of the FrameworkReference used in the project
            public string Name => _item.ItemSpec;

            //  The framework name to write to the runtimeconfig file (and the name of the folder under dotnet/shared)
            public string RuntimeFrameworkName => _item.GetMetadata("RuntimeFrameworkName");
            public string DefaultRuntimeFrameworkVersion => _item.GetMetadata("DefaultRuntimeFrameworkVersion");
            public string LatestRuntimeFrameworkVersion => _item.GetMetadata("LatestRuntimeFrameworkVersion");

            //  The ID of the targeting pack NuGet package to reference
            public string TargetingPackName => _item.GetMetadata("TargetingPackName");
            public string TargetingPackVersion => _item.GetMetadata("TargetingPackVersion");
        }
    }

    public class ReportUnknownFrameworkReferences : TaskBase
    {
        public string[] UnresolvedFrameworkReferences { get; set; }

        protected override void ExecuteCore()
        {
            if (UnresolvedFrameworkReferences != null)
            {
                foreach (var unresolvedFrameworkReference in UnresolvedFrameworkReferences)
                {
                    Log.LogError(Strings.UnknownFrameworkReference, unresolvedFrameworkReference);
                }
            }
        }
    }
}
