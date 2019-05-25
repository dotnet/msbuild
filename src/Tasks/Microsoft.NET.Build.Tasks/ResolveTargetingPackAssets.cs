using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class ResolveTargetingPackAssets : TaskBase
    {
        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] ResolvedTargetingPacks { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] RuntimeFrameworks { get; set; } = Array.Empty<ITaskItem>();

        public bool GenerateErrorForMissingTargetingPacks { get; set; }

        [Output]
        public ITaskItem[] ReferencesToAdd { get; set; }

        [Output]
        public ITaskItem[] PlatformManifests { get; set; }

        [Output]
        public string PackageConflictPreferredPackages { get; set; }

        [Output]
        public ITaskItem[] PackageConflictOverrides { get; set; }

        [Output]
        public ITaskItem[] UsedRuntimeFrameworks { get; set; }

        private Dictionary<string, List<string>> _assemblyProfiles;

        public ResolveTargetingPackAssets()
        {
            //  Hard-code assembly profiles for WindowDesktop targeting pack here until
            //  they are added to its FrameworkList.xml: https://github.com/dotnet/core-setup/issues/6210
            _assemblyProfiles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            var bothProfiles = new List<string>() { "WindowsForms", "WPF" };

            foreach (var assemblyName in new []
            {
                "Accessibility",
                "Microsoft.Win32.Registry",
                "Microsoft.Win32.SystemEvents",
                "System.CodeDom",
                "System.Configuration.ConfigurationManager",
                "System.Diagnostics.EventLog",
                "System.DirectoryServices",
                "System.IO.FileSystem.AccessControl",
                "System.Media.SoundPlayer",
                "System.Security.AccessControl",
                "System.Security.Cryptography.Cng",
                "System.Security.Cryptography.Pkcs",
                "System.Security.Cryptography.ProtectedData",
                "System.Security.Cryptography.Xml",
                "System.Security.Permissions",
                "System.Security.Principal.Windows",
                "System.Threading.AccessControl",
                "System.Windows.Extensions",
            })
            {
                _assemblyProfiles[assemblyName] = bothProfiles;
            }

            var wpfProfile = new List<string>() { "WPF" };

            foreach (var assemblyName in new []
            {
                "DirectWriteForwarder",
                "PenImc_cor3",
                "PresentationCore-CommonResources",
                "PresentationCore",
                "PresentationFramework-SystemCore",
                "PresentationFramework-SystemData",
                "PresentationFramework-SystemDrawing",
                "PresentationFramework-SystemXml",
                "PresentationFramework-SystemXmlLinq",
                "PresentationFramework.Aero",
                "PresentationFramework.Aero2",
                "PresentationFramework.AeroLite",
                "PresentationFramework.Classic",
                "PresentationFramework",
                "PresentationFramework.Luna",
                "PresentationFramework.Royale",
                "PresentationNative_cor3",
                "PresentationUI",
                "ReachFramework",
                "System.Printing",
                "System.Windows.Controls.Ribbon",
                "System.Windows.Input.Manipulations",
                "System.Windows.Presentation",
                "System.Xaml",
                "UIAutomationClient",
                "UIAutomationClientSideProviders",
                "UIAutomationProvider",
                "UIAutomationTypes",
                "WindowsBase",
                "WPFgfx_cor3",
            })
            {
                _assemblyProfiles[assemblyName] = wpfProfile;
            }

            var windowsFormsProfile = new List<string>() { "WindowsForms" };

            foreach (var assemblyName in new[]
            {
                "System.Design",
                "System.Drawing.Common",
                "System.Drawing.Design",
                "System.Drawing.Design.Primitives",
                "System.Windows.Forms.Design",
                "System.Windows.Forms.Design.Editors",
                "System.Windows.Forms",
            })
            {
                _assemblyProfiles[assemblyName] = windowsFormsProfile;
            }

        }

        protected override void ExecuteCore()
        {
            List<TaskItem> referencesToAdd = new List<TaskItem>();
            List<TaskItem> platformManifests = new List<TaskItem>();
            PackageConflictPreferredPackages = string.Empty;
            List<TaskItem> packageConflictOverrides = new List<TaskItem>();

            var resolvedTargetingPacks = ResolvedTargetingPacks.ToDictionary(item => item.ItemSpec, StringComparer.OrdinalIgnoreCase);

            foreach (var frameworkReference in FrameworkReferences)
            {
                ITaskItem targetingPack;
                resolvedTargetingPacks.TryGetValue(frameworkReference.ItemSpec, out targetingPack);
                string targetingPackRoot = targetingPack?.GetMetadata("Path");
 
                if (string.IsNullOrEmpty(targetingPackRoot) || !Directory.Exists(targetingPackRoot))
                {
                    if (GenerateErrorForMissingTargetingPacks)
                    {
                        Log.LogError(Strings.UnknownFrameworkReference, frameworkReference.ItemSpec);
                    }
                }
                else
                {
                    string targetingPackFormat = targetingPack.GetMetadata("TargetingPackFormat");

                    if (targetingPackFormat.Equals("NETStandardLegacy", StringComparison.OrdinalIgnoreCase))
                    {
                        AddNetStandardTargetingPackAssets(targetingPack, targetingPackRoot, referencesToAdd);
                    }
                    else
                    {
                        string targetingPackTargetFramework = targetingPack.GetMetadata("TargetFramework");
                        if (string.IsNullOrEmpty(targetingPackTargetFramework))
                        {
                            targetingPackTargetFramework = "netcoreapp3.0";
                        }

                        string targetingPackDataPath = Path.Combine(targetingPackRoot, "data");
                        string[] possibleDllFolders = new[]
                        {
                            Path.Combine(targetingPackRoot, "ref", targetingPackTargetFramework),
                            targetingPackDataPath
                        };

                        string[] possibleManifestPaths = new[]
                        {
                            Path.Combine(targetingPackRoot, "build", targetingPackTargetFramework,
                                targetingPack.GetMetadata(MetadataKeys.PackageName) + ".PlatformManifest.txt"),
                            Path.Combine(targetingPackDataPath, "PlatformManifest.txt"),
                            Path.Combine(targetingPackDataPath,
                                        targetingPack.GetMetadata(MetadataKeys.PackageName) + ".PlatformManifest.txt"),
                        };

                        string targetingPackDllFolder = possibleDllFolders.First(path =>
                                    Directory.Exists(path) &&
                                    Directory.GetFiles(path, "*.dll").Any());

                        string platformManifestPath = possibleManifestPaths.FirstOrDefault(File.Exists);

                        string packageOverridesPath = Path.Combine(targetingPackDataPath, "PackageOverrides.txt");

                        string frameworkListPath = Path.Combine(targetingPackDataPath, "FrameworkList.xml");

                        if (File.Exists(frameworkListPath))
                        {
                            AddReferencesFromFrameworkList(frameworkListPath, targetingPackDllFolder,
                                                           targetingPack, referencesToAdd);
                        }
                        else
                        {
                            foreach (var dll in Directory.GetFiles(targetingPackDllFolder, "*.dll"))
                            {
                                var reference = CreateReferenceItem(dll, targetingPack);

                                referencesToAdd.Add(reference);
                            }
                        }

                        if (platformManifestPath != null)
                        {
                            platformManifests.Add(new TaskItem(platformManifestPath));
                        }

                        if (File.Exists(packageOverridesPath))
                        {
                            packageConflictOverrides.Add(CreatePackageOverride(targetingPack.GetMetadata("RuntimeFrameworkName"), packageOverridesPath));
                        }

                        if (targetingPack.ItemSpec.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase))
                        {
                            //  Hardcode this for now.  Load this from the targeting pack once we have "real" targeting packs
                            //  https://github.com/dotnet/cli/issues/10581
                            PackageConflictPreferredPackages = "Microsoft.NETCore.App;runtime.linux-x64.Microsoft.NETCore.App;runtime.linux-x64.Microsoft.NETCore.App;runtime.linux-musl-x64.Microsoft.NETCore.App;runtime.linux-musl-x64.Microsoft.NETCore.App;runtime.rhel.6-x64.Microsoft.NETCore.App;runtime.rhel.6-x64.Microsoft.NETCore.App;runtime.osx-x64.Microsoft.NETCore.App;runtime.osx-x64.Microsoft.NETCore.App;runtime.freebsd-x64.Microsoft.NETCore.App;runtime.freebsd-x64.Microsoft.NETCore.App;runtime.win-x86.Microsoft.NETCore.App;runtime.win-x86.Microsoft.NETCore.App;runtime.win-arm.Microsoft.NETCore.App;runtime.win-arm.Microsoft.NETCore.App;runtime.win-arm64.Microsoft.NETCore.App;runtime.win-arm64.Microsoft.NETCore.App;runtime.linux-arm.Microsoft.NETCore.App;runtime.linux-arm.Microsoft.NETCore.App;runtime.linux-arm64.Microsoft.NETCore.App;runtime.linux-arm64.Microsoft.NETCore.App;runtime.tizen.4.0.0-armel.Microsoft.NETCore.App;runtime.tizen.4.0.0-armel.Microsoft.NETCore.App;runtime.tizen.5.0.0-armel.Microsoft.NETCore.App;runtime.tizen.5.0.0-armel.Microsoft.NETCore.App;runtime.win-x64.Microsoft.NETCore.App;runtime.win-x64.Microsoft.NETCore.App";
                        }
                    }
                }
            }

            //  Calculate which RuntimeFramework items should actually be used based on framework references
            HashSet<string> frameworkReferenceNames = new HashSet<string>(FrameworkReferences.Select(fr => fr.ItemSpec), StringComparer.OrdinalIgnoreCase);
            UsedRuntimeFrameworks = RuntimeFrameworks.Where(rf => frameworkReferenceNames.Contains(rf.GetMetadata(MetadataKeys.FrameworkName)))
                                    .ToArray();

            //  Filter out duplicate references (which can happen when referencing two different profiles that overlap)
            List<TaskItem> deduplicatedReferences = DeduplicateItems(referencesToAdd);
            ReferencesToAdd = deduplicatedReferences.Distinct() .ToArray();

            PlatformManifests = platformManifests.ToArray();
            PackageConflictOverrides = packageConflictOverrides.ToArray();
        }

        //  Get distinct items based on case-insensitive ItemSpec comparison
        private static List<TaskItem> DeduplicateItems(List<TaskItem> items)
        {
            HashSet<string> seenItemSpecs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<TaskItem> deduplicatedItems = new List<TaskItem>(items.Count);
            foreach (var item in items)
            {
                if (seenItemSpecs.Add(item.ItemSpec))
                {
                    deduplicatedItems.Add(item);
                }
            }
            return deduplicatedItems;
        }

        private TaskItem CreatePackageOverride(string runtimeFrameworkName, string packageOverridesPath)
        {
            TaskItem packageOverride = new TaskItem(runtimeFrameworkName);
            packageOverride.SetMetadata("OverriddenPackages", File.ReadAllText(packageOverridesPath));
            return packageOverride;
        }

        private void AddNetStandardTargetingPackAssets(ITaskItem targetingPack, string targetingPackRoot, List<TaskItem> referencesToAdd)
        {
            string targetingPackTargetFramework = targetingPack.GetMetadata("TargetFramework");
            string targetingPackAssetPath = Path.Combine(targetingPackRoot, "build", targetingPackTargetFramework, "ref");

            foreach (var dll in Directory.GetFiles(targetingPackAssetPath, "*.dll"))
            {
                var reference = CreateReferenceItem(dll, targetingPack);

                if (!Path.GetFileName(dll).Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase))
                {
                    reference.SetMetadata("Facade", "true");
                }

                referencesToAdd.Add(reference);
            }
        }

        private void AddReferencesFromFrameworkList(string frameworkListPath, string targetingPackDllFolder,
            ITaskItem targetingPack, List<TaskItem> referenceItems)
        {
            XDocument frameworkListDoc = XDocument.Load(frameworkListPath);

            string profile = targetingPack.GetMetadata("Profile");

            foreach (var fileElement in frameworkListDoc.Root.Elements("File"))
            {
                string assemblyName = fileElement.Attribute("AssemblyName").Value;

                if (!string.IsNullOrEmpty(profile))
                {
                    _assemblyProfiles.TryGetValue(assemblyName, out var assemblyProfiles);
                    if (assemblyProfiles == null)
                    {
                        //  If profile was specified but this assembly doesn't belong to any profiles, don't reference it
                        continue;
                    }
                    if (!assemblyProfiles.Contains(profile, StringComparer.OrdinalIgnoreCase))
                    {
                        //  Assembly wasn't in profile specified, so don't reference it
                        continue;
                    }
                }

                var dllPath = Path.Combine(targetingPackDllFolder, assemblyName + ".dll");
                var referenceItem = CreateReferenceItem(dllPath, targetingPack);

                referenceItem.SetMetadata("AssemblyVersion", fileElement.Attribute("AssemblyVersion").Value);
                referenceItem.SetMetadata("FileVersion", fileElement.Attribute("FileVersion").Value);
                referenceItem.SetMetadata("PublicKeyToken", fileElement.Attribute("PublicKeyToken").Value);

                referenceItems.Add(referenceItem);
            }
        }

        private TaskItem CreateReferenceItem(string dll, ITaskItem targetingPack)
        {
            var reference = new TaskItem(dll);

            reference.SetMetadata(MetadataKeys.ExternallyResolved, "true");
            reference.SetMetadata(MetadataKeys.Private, "false");
            reference.SetMetadata("Visible", "false");
            reference.SetMetadata(MetadataKeys.NuGetPackageId, targetingPack.GetMetadata(MetadataKeys.PackageName));
            reference.SetMetadata(MetadataKeys.NuGetPackageVersion, targetingPack.GetMetadata(MetadataKeys.PackageVersion));

            //  TODO: Once we work out what metadata we should use here to display these references grouped under the targeting pack
            //  in solution explorer, set that metadata here.These metadata values are based on what PCLs were using.
            //  https://github.com/dotnet/sdk/issues/2802
            reference.SetMetadata("WinMDFile", "false");
            reference.SetMetadata("ReferenceGroupingDisplayName", targetingPack.ItemSpec);
            reference.SetMetadata("ReferenceGrouping", targetingPack.ItemSpec);
            reference.SetMetadata("ResolvedFrom", "TargetingPack");
            reference.SetMetadata("IsSystemReference", "true");

            reference.SetMetadata("FrameworkName", targetingPack.ItemSpec);
            reference.SetMetadata("FrameworkVersion", targetingPack.GetMetadata(MetadataKeys.PackageVersion));
            
            return reference;
        }
    }
}
