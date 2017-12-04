using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackageObtainer
{
    internal class ToolPackageObtainer
    {
        private readonly Lazy<string> _bundledTargetFrameworkMoniker;
        private readonly Func<FilePath> _getTempProjectPath;
        private readonly IPackageToProjectFileAdder _packageToProjectFileAdder;
        private readonly IProjectRestorer _projectRestorer;
        private readonly DirectoryPath _toolsPath;

        public ToolPackageObtainer(
            DirectoryPath toolsPath,
            Func<FilePath> getTempProjectPath,
            Lazy<string> bundledTargetFrameworkMoniker,
            IPackageToProjectFileAdder packageToProjectFileAdder,
            IProjectRestorer projectRestorer
        )
        {
            _getTempProjectPath = getTempProjectPath;
            _bundledTargetFrameworkMoniker = bundledTargetFrameworkMoniker;
            _projectRestorer = projectRestorer ?? throw new ArgumentNullException(nameof(projectRestorer));
            _packageToProjectFileAdder = packageToProjectFileAdder ??
                                         throw new ArgumentNullException(nameof(packageToProjectFileAdder));
            _toolsPath = toolsPath;
        }

        public ToolConfigurationAndExecutableDirectory ObtainAndReturnExecutablePath(
            string packageId,
            string packageVersion = null,
            FilePath? nugetconfig = null,
            string targetframework = null)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (targetframework == null)
            {
                targetframework = _bundledTargetFrameworkMoniker.Value;
            }

            var packageVersionOrPlaceHolder = new PackageVersion(packageVersion);

            DirectoryPath individualToolVersion =
                CreateIndividualToolVersionDirectory(packageId, packageVersionOrPlaceHolder);

            FilePath tempProjectPath = CreateTempProject(
                packageId,
                packageVersionOrPlaceHolder,
                targetframework,
                individualToolVersion);

            if (packageVersionOrPlaceHolder.IsPlaceholder)
            {
                InvokeAddPackageRestore(
                    nugetconfig,
                    tempProjectPath,
                    packageId);
            }

            InvokeRestore(nugetconfig, tempProjectPath, individualToolVersion);

            if (packageVersionOrPlaceHolder.IsPlaceholder)
            {
                var concreteVersion =
                    new DirectoryInfo(
                        Directory.GetDirectories(
                            individualToolVersion.WithSubDirectories(packageId).Value).Single()).Name;
                DirectoryPath concreteVersionIndividualToolVersion =
                    individualToolVersion.GetParentPath().WithSubDirectories(concreteVersion);
                Directory.Move(individualToolVersion.Value, concreteVersionIndividualToolVersion.Value);

                individualToolVersion = concreteVersionIndividualToolVersion;
                packageVersion = concreteVersion;
            }

            ToolConfiguration toolConfiguration = GetConfiguration(packageId, packageVersion, individualToolVersion);

            return new ToolConfigurationAndExecutableDirectory(
                toolConfiguration,
                individualToolVersion.WithSubDirectories(
                    packageId,
                    packageVersion,
                    "tools",
                    targetframework));
        }

        private static ToolConfiguration GetConfiguration(
            string packageId,
            string packageVersion,
            DirectoryPath individualToolVersion)
        {
            FilePath toolConfigurationPath =
                individualToolVersion
                    .WithSubDirectories(packageId, packageVersion, "tools")
                    .WithFile("DotnetToolsConfig.xml");

            ToolConfiguration toolConfiguration =
                ToolConfigurationDeserializer.Deserialize(toolConfigurationPath.Value);

            return toolConfiguration;
        }

        private void InvokeRestore(
            FilePath? nugetconfig,
            FilePath tempProjectPath,
            DirectoryPath individualToolVersion)
        {
            _projectRestorer.Restore(tempProjectPath, individualToolVersion, nugetconfig);
        }

        private FilePath CreateTempProject(
            string packageId,
            PackageVersion packageVersion,
            string targetframework,
            DirectoryPath individualToolVersion)
        {
            FilePath tempProjectPath = _getTempProjectPath();
            if (Path.GetExtension(tempProjectPath.Value) != "csproj")
            {
                tempProjectPath = new FilePath(Path.ChangeExtension(tempProjectPath.Value, "csproj"));
            }

            EnsureDirectoryExists(tempProjectPath.GetDirectoryPath());
            var tempProjectContent = new XDocument(
                new XElement("Project",
                    new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                    new XElement("PropertyGroup",
                        new XElement("TargetFramework", targetframework),
                        new XElement("RestorePackagesPath", individualToolVersion.Value),
                        new XElement("DisableImplicitFrameworkReferences", "true")
                    ),
                    packageVersion.IsConcreteValue
                        ? new XElement("ItemGroup",
                            new XElement("PackageReference",
                                new XAttribute("Include", packageId),
                                new XAttribute("Version", packageVersion.Value)
                            ))
                        : null));

            File.WriteAllText(tempProjectPath.Value,
                tempProjectContent.ToString());

            return tempProjectPath;
        }

        private void InvokeAddPackageRestore(
            FilePath? nugetconfig,
            FilePath tempProjectPath,
            string packageId)
        {
            if (nugetconfig != null)
            {
                File.Copy(
                    nugetconfig.Value.Value,
                    tempProjectPath
                        .GetDirectoryPath()
                        .WithFile("nuget.config")
                        .Value);
            }

            _packageToProjectFileAdder.Add(tempProjectPath, packageId);
        }

        private DirectoryPath CreateIndividualToolVersionDirectory(
            string packageId,
            PackageVersion packageVersion)
        {
            DirectoryPath individualTool = _toolsPath.WithSubDirectories(packageId);
            DirectoryPath individualToolVersion = individualTool.WithSubDirectories(packageVersion.Value);
            EnsureDirectoryExists(individualToolVersion);
            return individualToolVersion;
        }

        private static void EnsureDirectoryExists(DirectoryPath path)
        {
            if (!Directory.Exists(path.Value))
            {
                Directory.CreateDirectory(path.Value);
            }
        }
    }
}
