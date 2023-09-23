// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
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

        /// <summary>
        /// A name for the test project that's used to isolate it from a test's root folder by appending it to the root test path.
        /// By default, it is the unhashed name of the function that instantiated the TestProject object.
        /// </summary>
        public string Name { get; set; }

        public bool IsSdkProject { get; set; } = true;

        public bool IsExe { get; set; }

        /// <summary>
        /// This value merely sets the OutputType and is not automatically tied here to whether the project is a WPF or Windows Form App Executable.
        /// </summary>
        public bool IsWinExe { get; set; }


        public string ProjectSdk { get; set; }

        /// <summary>
        /// Applies to SDK-style projects. If the value has only one target framework (ie no semicolons), the value will be used
        /// for the MSBuild TargetFramework (singular) property.  Otherwise, the value will be used for the TargetFrameworks property.
        /// </summary>
        public string TargetFrameworks { get; set; } = ToolsetInfo.CurrentTargetFramework;

        public string RuntimeFrameworkVersion { get; set; }

        public string RuntimeIdentifier { get; set; }

        // Set to either true, false, or empty string "". The empty string does not undefine SelfContained, it just doesn't specify it.
        public string SelfContained { get; set; } = "";

        //  TargetFrameworkVersion applies to non-SDK projects
        public string TargetFrameworkVersion { get; set; }

        public string TargetFrameworkProfile { get; set; }

        public bool UseArtifactsOutput { get; set; }

        public List<TestProject> ReferencedProjects { get; } = new List<TestProject>();

        public List<string> References { get; } = new List<string>();

        public List<string> FrameworkReferences { get; } = new List<string>();

        public List<TestPackageReference> PackageReferences { get; } = new List<TestPackageReference>();

        public List<TestPackageReference> DotNetCliToolReferences { get; } = new List<TestPackageReference>();

        public List<CopyFilesTarget> CopyFilesTargets { get; } = new List<CopyFilesTarget>();

        public Dictionary<string, string> SourceFiles { get; } = new Dictionary<string, string>();

        public Dictionary<string, string> EmbeddedResources { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Use this dictionary to set a property (the key) to a value for the created project.
        /// </summary>
        public Dictionary<string, string> AdditionalProperties { get; } = new Dictionary<string, string>();

        public List<KeyValuePair<string, Dictionary<string, string>>> AdditionalItems { get; } = new();

        public List<Action<XDocument>> ProjectChanges { get; } = new List<Action<XDocument>>();

        /// <summary>
        /// A list of properties to record the values for when the project is built.
        /// Values can be retrieved with <see cref="GetPropertyValues"/>
        /// </summary>
        public List<string> PropertiesToRecord { get; } = new List<string>();

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
            string targetFolder = Path.Combine(targetTestAsset.Path, Name);
            Directory.CreateDirectory(targetFolder);

            string targetProjectPath = Path.Combine(targetFolder, Name + targetExtension);

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
                var includeOrUpdate = packageReference.UpdatePackageReference ? "Update" : "Include";
                var packageReferenceElement = new XElement(ns + "PackageReference",
                    new XAttribute(includeOrUpdate, packageReference.ID));
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
                if (packageReference.Publish != null)
                {
                    packageReferenceElement.Add(new XAttribute("Publish", packageReference.Publish));
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
                if (TargetFrameworks.Contains(";"))
                {
                    propertyGroup.Add(new XElement(ns + "TargetFrameworks", TargetFrameworks));
                }
                else
                {
                    propertyGroup.Add(new XElement(ns + "TargetFramework", TargetFrameworks));
                }

                if (!string.IsNullOrEmpty(RuntimeFrameworkVersion))
                {
                    propertyGroup.Add(new XElement(ns + "RuntimeFrameworkVersion", RuntimeFrameworkVersion));
                }

                if (!string.IsNullOrEmpty(RuntimeIdentifier))
                {
                    propertyGroup.Add(new XElement(ns + "RuntimeIdentifier", RuntimeIdentifier));
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(TargetFrameworkProfile))
                {
                    propertyGroup.Add(new XElement(ns + "TargetFrameworkProfile", TargetFrameworkProfile));

                    //  To construct an accurate PCL project file, we must modify the import of the CSharp targets;
                    //    building/testing the SDK requires a VSDev command prompt which sets 'VSINSTALLDIR'
                    var importGroup = projectXml.Root.Elements(ns + "Import").Last();
                    importGroup.Attribute("Project").Value = "$(VSINSTALLDIR)\\MSBuild\\Microsoft\\Portable\\$(TargetFrameworkVersion)\\Microsoft.Portable.CSharp.targets";
                }

                propertyGroup.Element(ns + "TargetFrameworkVersion").SetValue(TargetFrameworkVersion);
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

            if (IsExe && !IsWinExe)
            {
                propertyGroup.Element(ns + "OutputType").SetValue("Exe");
            }
            else if (IsWinExe)
            {
                propertyGroup.Element(ns + "OutputType").SetValue("WinExe");
            }

            if (SelfContained != "")
            {
                propertyGroup.Add(new XElement(ns + "SelfContained", string.Equals(SelfContained, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false"));
            }

            if (ReferencedProjects.Any())
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

            if (References.Any())
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

            if (FrameworkReferences.Any())
            {
                var frameworkReferenceItemGroup = new XElement(ns + "ItemGroup");
                projectXml.Root.Add(frameworkReferenceItemGroup);
                foreach (var frameworkReference in FrameworkReferences)
                {
                    frameworkReferenceItemGroup.Add(new XElement(ns + "FrameworkReference",
                        new XAttribute("Include", frameworkReference)));
                }
            }

            if (CopyFilesTargets.Any())
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
                if (IsExe || IsWinExe)
                {
                    string source =
    @"using System;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(""Hello World!"");
";

                    foreach (var dependency in ReferencedProjects)
                    {
                        string safeDependencyName = dependency.Name.Replace('.', '_');

                        source += $"        Console.WriteLine({safeDependencyName}.{safeDependencyName}Class.Name);" + Environment.NewLine;
                        source += $"        Console.WriteLine({safeDependencyName}.{safeDependencyName}Class.List);" + Environment.NewLine;
                    }

                    source +=
    @"    }
}";
                    string sourcePath = Path.Combine(targetFolder, Name + "Program.cs");

                    File.WriteAllText(sourcePath, source);
                }

                {
                    string safeThisName = Name.Replace('.', '_');
                    string source =
    $@"using System;
using System.Collections.Generic;

namespace {safeThisName}
{{
    public class {safeThisName}Class
    {{
        public static string Name {{ get {{ return ""{Name}""; }} }}
        public static List<string> List {{ get {{ return null; }} }}
";
                    foreach (var dependency in ReferencedProjects)
                    {
                        string safeDependencyName = dependency.Name.Replace('.', '_');

                        source += $"        public string {safeDependencyName}Name {{ get {{ return {safeDependencyName}.{safeDependencyName}Class.Name; }} }}" + Environment.NewLine;
                        source += $"        public List<string> {safeDependencyName}List {{ get {{ return {safeDependencyName}.{safeDependencyName}Class.List; }} }}" + Environment.NewLine;
                    }

                    source +=
    @"    }
}";
                    string sourcePath = Path.Combine(targetFolder, Name + ".cs");

                    File.WriteAllText(sourcePath, source);
                }

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

            if (PropertiesToRecord.Any())
            {
                string propertiesElements = "";
                foreach (var propertyName in PropertiesToRecord)
                {
                    propertiesElements += $"      <LinesToWrite Include=`{propertyName}: $({propertyName})`/>" + Environment.NewLine;
                }

                string injectTargetContents =
    $@"<Project>
  <Target Name=`WritePropertyValues` BeforeTargets=`AfterBuild`>
    <ItemGroup>
{propertiesElements}
    </ItemGroup>
    <WriteLinesToFile
      File=`$(BaseIntermediateOutputPath)\$(Configuration)\$(TargetFramework)\PropertyValues.txt`
      Lines=`@(LinesToWrite)`
      Overwrite=`true`
      Encoding=`Unicode`
      />
  </Target>
</Project>";

                injectTargetContents = injectTargetContents.Replace('`', '"');

                string targetPath = Path.Combine(targetFolder, "obj", Name + ".csproj.WriteValuesToFile.g.targets");

                if (!Directory.Exists(Path.GetDirectoryName(targetPath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                }

                File.WriteAllText(targetPath, injectTargetContents);
            }
        }

        public void AddItem(string itemName, string attributeName, string attributeValue)
        {
            AddItem(itemName, new Dictionary<string, string>() { { attributeName, attributeValue } });
        }

        public void AddItem(string itemName, Dictionary<string, string> attributes)
        {
            AdditionalItems.Add(new(itemName, attributes));
        }

        public void RecordProperties(params string[] propertyNames)
        {
            PropertiesToRecord.AddRange(propertyNames);
        }

        /// <returns>
        /// A dictionary of property keys to property value strings, case sensitive.
        /// Only properties added to the <see cref="PropertiesToRecord"/> member will be observed.
        /// </returns>
        public Dictionary<string, string> GetPropertyValues(string testRoot, string targetFramework = null, string configuration = "Debug")
        {
            var propertyValues = new Dictionary<string, string>();

            string intermediateOutputPath = Path.Combine(testRoot, Name, "obj", configuration, targetFramework ?? TargetFrameworks);

            foreach (var line in File.ReadAllLines(Path.Combine(intermediateOutputPath, "PropertyValues.txt")))
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    string propertyName = line.Substring(0, colonIndex);
                    string propertyValue = line.Length == colonIndex + 1 ? string.Empty : line.Substring(colonIndex + 2);
                    propertyValues[propertyName] = propertyValue;
                }
            }

            return propertyValues;
        }

        public static bool ReferenceAssembliesAreInstalled(TargetDotNetFrameworkVersion targetFrameworkVersion)
        {
            var referenceAssemblies = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(targetFrameworkVersion);
            return referenceAssemblies != null;
        }

        private OutputPathCalculator GetOutputPathCalculator(string testRoot)
        {
            return OutputPathCalculator.FromProject(Path.Combine(testRoot, Name, Name + ".csproj"), this);
        }

        public string GetOutputDirectory(string testRoot, string targetFramework = null, string configuration = "Debug", string runtimeIdentifier = "")
        {
            return GetOutputPathCalculator(testRoot)
                .GetOutputDirectory(targetFramework, configuration, runtimeIdentifier);
        }
    }
}
