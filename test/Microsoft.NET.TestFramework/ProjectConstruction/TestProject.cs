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

        //  Apply to SDK Projects
        public string TargetFramework { get; set; }
        public string TargetFrameworks { get; set; }

        //  TargetFrameworkVersion applies to non-SDK projects
        public string TargetFrameworkVersion { get; set; }


        public List<TestProject> ReferencedProjects { get; } = new List<TestProject>();

        //  TODO: Source files

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
                throw new NotImplementedException("Non-SDK project generation not yet supported");
            }

            var projectXml = XDocument.Load(sourceProject);

            var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");

            var propertyGroup = projectXml.Root.Elements(ns + "PropertyGroup").First();

            var packageReferenceItemGroup = projectXml.Root.Elements(ns + "ItemGroup")
                .FirstOrDefault(itemGroup => itemGroup.Elements(ns + "PackageReference").Count() > 0);
            if (packageReferenceItemGroup == null)
            {
                packageReferenceItemGroup = projectXml.Root.Elements(ns + "ItemGroup")
                    .First();
            }

            if (IsSdkProject)
            {
                if (this.TargetFramework != null)
                {
                    propertyGroup.Add(new XElement(ns + "TargetFramework", this.TargetFramework));
                }
                if (this.TargetFrameworks != null)
                {
                    propertyGroup.Add(new XElement(ns + "TargetFrameworks", this.TargetFrameworks));
                }

                if (this.IsExe)
                {
                    packageReferenceItemGroup.Add(new XElement(ns + "PackageReference",
                            new XAttribute("Include", "Microsoft.NETCore.App"),
                            new XAttribute("Version", "1.0.1")
                        ));
                }
                else
                {
                    packageReferenceItemGroup.Add(new XElement(ns + "PackageReference",
                            new XAttribute("Include", "NETStandard.Library"),
                            new XAttribute("Version", "1.6.0")
                        ));
                }

                projectXml.Descendants(ns +"PackageReference")
                        .FirstOrDefault(pr => pr.Attribute("Include")?.Value == "Microsoft.NET.Sdk")
                        ?.Element(ns + "Version")
                        ?.SetValue('[' + targetTestAsset.BuildVersion + ']');
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
        }

        public override string ToString()
        {
            StringBuilder ret = new StringBuilder();
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
            if (!string.IsNullOrEmpty(TargetFramework))
            {
                ret.Append(TargetFramework);
            }
            if (!string.IsNullOrEmpty(TargetFrameworks))
            {
                ret.Append("(" + TargetFramework + ")");
            }
            if (!string.IsNullOrEmpty(TargetFrameworkVersion))
            {
                ret.Append(TargetFrameworkVersion);
            }
            return ret.ToString();
        }
    }
}
