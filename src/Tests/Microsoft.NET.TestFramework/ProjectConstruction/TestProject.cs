using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;

namespace Microsoft.NET.TestFramework.ProjectConstruction
{
    public class TestProject
    {
        public TestProject([CallerMemberName] string name = null)
        {
            if (name != null)
            {
                Name = name;
            }
        }

        public string Name { get; set; }

        public bool IsSdkProject { get; set; } = true;

        public bool IsExe { get; set; }

        public bool IsWinExe { get; set; }

        public string ProjectSdk { get; set; }

        //  Applies to SDK Projects
        public string TargetFrameworks { get; set; }

        public string RuntimeFrameworkVersion { get; set; }

        public string RuntimeIdentifier { get; set; }

        //  TargetFrameworkVersion applies to non-SDK projects
        public string TargetFrameworkVersion { get; set; }

        public string TargetFrameworkProfile { get; set; }

        public List<TestProject> ReferencedProjects { get; } = new List<TestProject>();

        public List<string> References { get; } = new List<string>();

        public List<string> FrameworkReferences { get; } = new List<string>();

        public List<TestPackageReference> PackageReferences { get; } = new List<TestPackageReference>();

        public List<TestPackageReference> DotNetCliToolReferences { get; } = new List<TestPackageReference>();
        
        public List<CopyFilesTarget> CopyFilesTargets { get; } = new List<CopyFilesTarget>();

        public Dictionary<string, string> SourceFiles { get; } = new Dictionary<string, string>();

        public Dictionary<string, string> EmbeddedResources { get; } = new Dictionary<string, string>();

        public Dictionary<string, string> AdditionalProperties { get; } = new Dictionary<string, string>();

        public List<KeyValuePair<string, Dictionary<string, string>>> AdditionalItems { get; } = new ();

        public List<Action<XDocument>> ProjectChanges { get; } = new List<Action<XDocument>>();

        public IEnumerable<string> TargetFrameworkIdentifiers
        {
            get
            {
                if (!IsSdkProject)
                {
                    //  Assume .NET Framework
                    yield return ".NETFramework";
                    yield break;
                }

                foreach (var target in TargetFrameworks.Split(';'))
                {
                    yield return NuGetFramework.Parse(target).Framework;
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
                foreach (var identifier in TargetFrameworkIdentifiers)
                {
                    if (identifier.Equals(".NETFramework", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        internal void Create(TestAsset targetTestAsset, string testProjectsSourceFolder, string targetExtension = ".csproj")
        {
            string targetFolder = Path.Combine(targetTestAsset.Path, this.Name);
            Directory.CreateDirectory(targetFolder);

            string targetProjectPath = Path.Combine(targetFolder, this.Name + targetExtension);

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

            if (ProjectSdk != null)
            {
                projectXml.Root.Attribute("Sdk").Value = ProjectSdk;
            }

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
                if (packageReference.Aliases != null)
                {
                    packageReferenceElement.Add(new XAttribute("Aliases", packageReference.Aliases));
                }
                packageReferenceItemGroup.Add(packageReferenceElement);
            }

            foreach (TestPackageReference dotnetCliToolReference in DotNetCliToolReferences)
            {
                packageReferenceItemGroup.Add(new XElement(ns + "DotNetCliToolReference",
                    new XAttribute("Include", $"{dotnetCliToolReference.ID}"),
                    new XAttribute("Version", $"{dotnetCliToolReference.Version}")));
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
                    var item = new XElement(ns + additionalItem.Key);
                    foreach (var attribute in additionalItem.Value)
                        item.Add(new XAttribute(attribute.Key, attribute.Value));
                    additionalItemGroup.Add(item);
                }
            }

            if (this.IsExe && !this.IsWinExe)
            {
                propertyGroup.Element(ns + "OutputType").SetValue("Exe");
            }
            else if (this.IsWinExe)
            {
                propertyGroup.Element(ns + "OutputType").SetValue("WinExe");
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

            if (this.FrameworkReferences.Any())
            {
                var frameworkReferenceItemGroup = new XElement(ns + "ItemGroup");
                projectXml.Root.Add(frameworkReferenceItemGroup);
                foreach (var frameworkReference in FrameworkReferences)
                {
                    frameworkReferenceItemGroup.Add(new XElement(ns + "FrameworkReference",
                        new XAttribute("Include", frameworkReference)));
                }
            }
            
            if (this.CopyFilesTargets.Any())
            {
                foreach (var copyFilesTarget in CopyFilesTargets)
                {
                    var target = new XElement(ns + "Target",
                        new XAttribute("Name", copyFilesTarget.TargetName),
                        new XAttribute("AfterTargets", copyFilesTarget.TargetToRunAfter));

                    var copyElement = new XElement(ns + "Copy",
                        new XAttribute("SourceFiles", copyFilesTarget.SourceFiles),
                        new XAttribute("DestinationFolder", copyFilesTarget.Destination));

                    if (!string.IsNullOrEmpty(copyFilesTarget.Condition))
                    {
                        copyElement.SetAttributeValue("Condition", copyFilesTarget.Condition);
                    }

                    target.Add(copyElement);
                    projectXml.Root.Add(target);
                }
            }

            foreach (var projectChange in ProjectChanges)
            {
                projectChange(projectXml);
            }

            using (var file = File.CreateText(targetProjectPath))
            {
                projectXml.Save(file);
            }

            if (SourceFiles.Count == 0)
            {
                string source;

                if (this.IsExe || this.IsWinExe)
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

        public void AddItem(string itemName, string attributeName, string attributeValue)
        {
            AddItem(itemName, new Dictionary<string, string>() { { attributeName, attributeValue } } );
        }

        public void AddItem(string itemName, Dictionary<string, string> attributes)
        {
            AdditionalItems.Add(new(itemName, attributes));
        }

        public static bool ReferenceAssembliesAreInstalled(TargetDotNetFrameworkVersion targetFrameworkVersion)
        {
            var referenceAssemblies = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(targetFrameworkVersion);
            return referenceAssemblies != null;
        }
    }
}
