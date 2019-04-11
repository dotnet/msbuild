using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;

namespace Microsoft.NET.TestFramework.ProjectConstruction
{
    public class TestProject
    {
        public string Name { get; set; }

        public bool IsSdkProject { get; set; }

        public bool IsExe { get; set; }

        //  Applies to SDK Projects
        public string TargetFrameworks { get; set; }

        public string RuntimeFrameworkVersion { get; set; }

        public string RuntimeIdentifier { get; set; }

        //  TargetFrameworkVersion applies to non-SDK projects
        public string TargetFrameworkVersion { get; set; }

        public string TargetFrameworkProfile { get; set; }

        public List<TestProject> ReferencedProjects { get; } = new List<TestProject>();

        public List<string> References { get; } = new List<string>();

        public List<TestPackageReference> PackageReferences { get; } = new List<TestPackageReference>();

        public List<TestPackageReference> DotNetCliToolReferences { get; } = new List<TestPackageReference>();
        
        public List<CopyFilesTarget> CopyFilesTargets { get; } = new List<CopyFilesTarget>();

        public Dictionary<string, string> SourceFiles { get; } = new Dictionary<string, string>();

        public Dictionary<string, string> EmbeddedResources { get; } = new Dictionary<string, string>();

        public Dictionary<string, string> AdditionalProperties { get; } = new Dictionary<string, string>();

        public Dictionary<string, string> AdditionalItems { get; } = new Dictionary<string, string>();

        private static string GetShortTargetFrameworkIdentifier(string targetFramework)
        {
            int identifierLength = 0;
            for (; identifierLength < targetFramework.Length; identifierLength++)
            {
                if (!char.IsLetter(targetFramework[identifierLength]))
                {
                    break;
                }
            }

            string identifier = targetFramework.Substring(0, identifierLength);
            return identifier;
        }

        public IEnumerable<string> ShortTargetFrameworkIdentifiers
        {
            get
            {
                if (!IsSdkProject)
                {
                    //  Assume .NET Framework
                    yield return "net";
                    yield break;
                }

                foreach (var target in TargetFrameworks.Split(';'))
                {
                    yield return GetShortTargetFrameworkIdentifier(target);
                }
            }
        }

        public bool BuildsOnNonWindows
        {
            get
            {
                if (!IsSdkProject)
                {
                    return false;
                }

                //  Currently can't build projects targeting .NET Framework on non-Windows: https://github.com/dotnet/sdk/issues/335
                foreach (var identifier in ShortTargetFrameworkIdentifiers)
                {
                    if (identifier.Equals("net", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        internal void Create(TestAsset targetTestAsset, string testProjectsSourceFolder)
        {
            string targetFolder = Path.Combine(targetTestAsset.Path, this.Name);
            Directory.CreateDirectory(targetFolder);

            string targetProjectPath = Path.Combine(targetFolder, this.Name + ".csproj");

            string sourceProject;
            string sourceProjectBase = Path.Combine(testProjectsSourceFolder, "ProjectConstruction");
            if (IsSdkProject)
            {
                sourceProject = Path.Combine(sourceProjectBase, "SdkProject", "SdkProject.csproj");
            }
            else
            {
                sourceProject = Path.Combine(sourceProjectBase, "NetFrameworkProject", "NetFrameworkProject.csproj");
            }

            //  Copy any additional files from template
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(sourceProject)))
            {
                if (file != sourceProject)
                {
                    File.Copy(file, Path.Combine(targetFolder, Path.GetFileName(file)));
                }
            }

            var projectXml = XDocument.Load(sourceProject);

            var ns = projectXml.Root.Name.Namespace;

            var propertyGroup = projectXml.Root.Elements(ns + "PropertyGroup").First();

            var packageReferenceItemGroup = projectXml.Root.Elements(ns + "ItemGroup")
                .FirstOrDefault(itemGroup => itemGroup.Elements(ns + "PackageReference").Count() > 0);
            if (packageReferenceItemGroup == null)
            {
                packageReferenceItemGroup = projectXml.Root.Elements(ns + "ItemGroup")
                    .FirstOrDefault();
            }
            if (packageReferenceItemGroup == null)
            {
                packageReferenceItemGroup = new XElement(ns + "ItemGroup");
                projectXml.Root.Add(packageReferenceItemGroup);
            }

            foreach (TestPackageReference packageReference in PackageReferences)
            {
                var packageReferenceElement = new XElement(ns + "PackageReference",
                    new XAttribute("Include", packageReference.ID));
                if (packageReference.Version != null)
                {
                    packageReferenceElement.Add(new XAttribute("Version", packageReference.Version));
                }
                if (packageReference.PrivateAssets != null)
                {
                    packageReferenceElement.Add(new XAttribute("PrivateAssets", packageReference.PrivateAssets));
                }
                packageReferenceItemGroup.Add(packageReferenceElement);
            }

            foreach (TestPackageReference dotnetCliToolReference in DotNetCliToolReferences)
            {
                packageReferenceItemGroup.Add(new XElement(ns + "DotNetCliToolReference",
                    new XAttribute("Include", $"{dotnetCliToolReference.ID}"),
                    new XAttribute("Version", $"{dotnetCliToolReference.Version}")));
            }

            //  If targeting .NET Framework and a required targeting pack isn't installed, add a
            //  PackageReference to get the targeting pack from a NuGet package
            if (NeedsReferenceAssemblyPackages())
            {
                packageReferenceItemGroup.Add(new XElement(ns + "PackageReference",
                    new XAttribute("Include", $"Microsoft.NETFramework.ReferenceAssemblies"),
                    new XAttribute("Version", $"1.0.0-alpha-5")));

                propertyGroup.Add(new XElement(ns + "RestoreAdditionalProjectSources", "$(RestoreAdditionalProjectSources);https://dotnet.myget.org/F/roslyn-tools/api/v3/index.json"));
            }

            var targetFrameworks = IsSdkProject ? TargetFrameworks.Split(';') : new[] { "net" };

            if (IsSdkProject)
            {
                if (this.TargetFrameworks.Contains(";"))
                {
                    propertyGroup.Add(new XElement(ns + "TargetFrameworks", this.TargetFrameworks));
                }
                else
                {
                    propertyGroup.Add(new XElement(ns + "TargetFramework", this.TargetFrameworks));
                }

                if (!string.IsNullOrEmpty(this.RuntimeFrameworkVersion))
                {
                    propertyGroup.Add(new XElement(ns + "RuntimeFrameworkVersion", this.RuntimeFrameworkVersion));
                }

                if (!string.IsNullOrEmpty(this.RuntimeIdentifier))
                {
                    propertyGroup.Add(new XElement(ns + "RuntimeIdentifier", this.RuntimeIdentifier));
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(this.TargetFrameworkProfile))
                {
                    propertyGroup.Add(new XElement(ns + "TargetFrameworkProfile", this.TargetFrameworkProfile));

                    //  To construct an accurate PCL project file, we must modify the import of the CSharp targets;
                    //    building/testing the SDK requires a VSDev command prompt which sets 'VSINSTALLDIR'
                    var importGroup = projectXml.Root.Elements(ns + "Import").Last();
                    importGroup.Attribute("Project").Value = "$(VSINSTALLDIR)\\MSBuild\\Microsoft\\Portable\\$(TargetFrameworkVersion)\\Microsoft.Portable.CSharp.targets";
                }

                propertyGroup.Element(ns + "TargetFrameworkVersion").SetValue(this.TargetFrameworkVersion);
            }

            foreach (var additionalProperty in AdditionalProperties)
            {
                propertyGroup.Add(new XElement(ns + additionalProperty.Key, additionalProperty.Value));
            }

            if (AdditionalItems.Any())
            {
                foreach (var additionalItem in AdditionalItems)
                {
                    var additionalItemGroup = projectXml.Root.Elements(ns + "ItemGroup").FirstOrDefault();
                    if (additionalItemGroup == null)
                    {
                        additionalItemGroup = new XElement(ns + "ItemGroup");
                        projectXml.Root.Add(packageReferenceItemGroup);
                    }
                    additionalItemGroup.Add(new XElement(
                        ns + additionalItem.Key, 
                        new XAttribute("Include", additionalItem.Value)));
                }
            }

            if (this.IsExe)
            {
                propertyGroup.Element(ns + "OutputType").SetValue("Exe");
            }

            if (this.ReferencedProjects.Any())
            {
                var projectReferenceItemGroup = projectXml.Root.Elements(ns + "ItemGroup")
                    .FirstOrDefault(itemGroup => itemGroup.Elements(ns + "ProjectReference").Count() > 0);
                if (projectReferenceItemGroup == null)
                {
                    projectReferenceItemGroup = new XElement(ns + "ItemGroup");
                    packageReferenceItemGroup.AddBeforeSelf(projectReferenceItemGroup);
                }
                foreach (var referencedProject in ReferencedProjects)
                {
                    projectReferenceItemGroup.Add(new XElement(ns + "ProjectReference",
                    new XAttribute("Include", $"../{referencedProject.Name}/{referencedProject.Name}.csproj")));
                }
            }

            if (this.References.Any())
            {
                var referenceItemGroup = projectXml.Root.Elements(ns + "ItemGroup")
                    .FirstOrDefault(itemGroup => itemGroup.Elements(ns + "Reference").Count() > 0);
                if (referenceItemGroup == null)
                {
                    referenceItemGroup = new XElement(ns + "ItemGroup");
                    packageReferenceItemGroup.AddBeforeSelf(referenceItemGroup);
                }

                foreach (var reference in References)
                {
                    referenceItemGroup.Add(new XElement(ns + "Reference",
                        new XAttribute("Include", reference)));
                }
            }
            
            if (this.CopyFilesTargets.Any())
            {
                foreach (var copyFilesTarget in CopyFilesTargets)
                {
                    var target = new XElement(ns + "Target",
                        new XAttribute("Name", copyFilesTarget.TargetName),
                        new XAttribute("AfterTargets", copyFilesTarget.TargetToRunAfter));

                    target.Add(new XElement(ns + "Copy",
                        new XAttribute("SourceFiles", copyFilesTarget.SourceFiles),
                        new XAttribute("DestinationFolder", copyFilesTarget.Destination)));

                    projectXml.Root.Add(target);
                }
            }

            using (var file = File.CreateText(targetProjectPath))
            {
                projectXml.Save(file);
            }

            if (SourceFiles.Count == 0)
            {
                string source;

                if (this.IsExe)
                {
                    source =
    @"using System;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(""Hello World!"");
";

                    foreach (var dependency in this.ReferencedProjects)
                    {
                        source += $"        Console.WriteLine({dependency.Name}.{dependency.Name}Class.Name);" + Environment.NewLine;
                        source += $"        Console.WriteLine({dependency.Name}.{dependency.Name}Class.List);" + Environment.NewLine;
                    }

                    source +=
    @"    }
}";
                }
                else
                {
                    source =
    $@"using System;
using System.Collections.Generic;

namespace {this.Name}
{{
    public class {this.Name}Class
    {{
        public static string Name {{ get {{ return ""{this.Name}""; }} }}
        public static List<string> List {{ get {{ return null; }} }}
";
                    foreach (var dependency in this.ReferencedProjects)
                    {
                        source += $"        public string {dependency.Name}Name {{ get {{ return {dependency.Name}.{dependency.Name}Class.Name; }} }}" + Environment.NewLine;
                        source += $"        public List<string> {dependency.Name}List {{ get {{ return {dependency.Name}.{dependency.Name}Class.List; }} }}" + Environment.NewLine;
                    }

                    source +=
    @"    }
}";
                }
                string sourcePath = Path.Combine(targetFolder, this.Name + ".cs");

                File.WriteAllText(sourcePath, source);
            }
            else
            {
                foreach (var kvp in SourceFiles)
                {
                    File.WriteAllText(Path.Combine(targetFolder, kvp.Key), kvp.Value);
                }
            }

            foreach (var kvp in EmbeddedResources)
            {
                File.WriteAllText(Path.Combine(targetFolder, kvp.Key), kvp.Value);
            }
        }

        private bool NeedsReferenceAssemblyPackages()
        {
            //  Check to see if NuGet packages for reference assemblies need to be referenced (because
            //  the targeting pack is not installed)
            bool needsReferenceAssemblyPackages = false;
            if (IsSdkProject)
            {
                foreach (var shortFrameworkName in TargetFrameworks.Split(';').Where(tf => GetShortTargetFrameworkIdentifier(tf) == "net"))
                {
                    //  Normalize version to the form used in the reference assemblies path
                    var version = NuGetFramework.Parse(shortFrameworkName).Version;
                    version = new Version(version.Major, version.Minor, version.Build);
                    if (version.Build == 0)
                    {
                        version = new Version(version.Major, version.Minor);
                    }
                    
                    if (!ReferenceAssembliesAreInstalled(version.ToString()))
                    {
                        needsReferenceAssemblyPackages = true;
                    }
                }
            }
            else
            {
                needsReferenceAssemblyPackages = !ReferenceAssembliesAreInstalled(TargetFrameworkVersion);
            }

            return needsReferenceAssemblyPackages;
        }

        private bool ReferenceAssembliesAreInstalled(string targetFrameworkVersion)
        {
            if (!targetFrameworkVersion.StartsWith('v'))
            {
                targetFrameworkVersion = "v" + targetFrameworkVersion;
            }

            // Use the MSBuild API to find the path to the 4.6.1 reference assemblies, and locate the desired reference assemblies relative to that.
            var net461referenceAssemblies = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version461);
            if (net461referenceAssemblies == null)
            {
                //  4.6.1 reference assemblies not found, assume that the version we want isn't available either
                return false;
            }
            var requestedReferenceAssembliesPath = Path.Combine(new DirectoryInfo(net461referenceAssemblies).Parent.FullName, targetFrameworkVersion);
            return Directory.Exists(requestedReferenceAssembliesPath);
        }

        public override string ToString()
        {
            var ret = new StringBuilder();
            if (!string.IsNullOrEmpty(Name))
            {
                ret.Append(Name);
            }
            if (IsSdkProject)
            {
                ret.Append("Sdk");
            }
            if (IsExe)
            {
                ret.Append("Exe");
            }
            if (!string.IsNullOrEmpty(TargetFrameworks))
            {
                ret.Append(TargetFrameworks);
            }
            if (!string.IsNullOrEmpty(TargetFrameworkProfile))
            {
                ret.Append(TargetFrameworkProfile);
            }
            else if (!string.IsNullOrEmpty(TargetFrameworkVersion))
            {
                ret.Append(TargetFrameworkVersion);
            }
            return ret.ToString();
        }
    }
}
