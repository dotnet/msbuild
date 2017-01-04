using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.NET.TestFramework.ProjectConstruction
{
    public class TestProject
    {
        public string Name { get; set; }

        public bool IsSdkProject { get; set; }

        public bool IsExe { get; set; }

        //  Applies to SDK Projects
        public string TargetFrameworks { get; set; }

        //  TargetFrameworkVersion applies to non-SDK projects
        public string TargetFrameworkVersion { get; set; }

        public List<TestProject> ReferencedProjects { get; } = new List<TestProject>();

        public Dictionary<string, string> SourceFiles { get; } = new Dictionary<string, string>();

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

                if (this.IsExe && targetFrameworks.Any(identifier => GetShortTargetFrameworkIdentifier(identifier).Equals("net", StringComparison.OrdinalIgnoreCase)))
                {
                    propertyGroup.Add(new XElement(ns + "RuntimeIdentifier", "win7-x86"));
                }

                //  Update SDK reference to the version under test
                targetTestAsset.SetSdkVersion(projectXml);
                
            }
            else
            {
                var targetFrameworkVersionElement = propertyGroup.Element(ns + "TargetFrameworkVersion");
                targetFrameworkVersionElement.SetValue(this.TargetFrameworkVersion);
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
                    }

                    source +=
    @"    }
}";
                }
                else
                {
                    source =
    $@"using System;

namespace {this.Name}
{{
    public class {this.Name}Class
    {{
        public static string Name {{ get {{ return ""{this.Name}""; }} }}
    }}
}}";
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
            if (!string.IsNullOrEmpty(TargetFrameworkVersion))
            {
                ret.Append(TargetFrameworkVersion);
            }
            return ret.ToString();
        }
    }
}
