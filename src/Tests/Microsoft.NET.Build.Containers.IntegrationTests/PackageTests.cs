// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO.Compression;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.DotNet.Cli.Utils;
using Xunit;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class PackageTests
{
    [Fact]
    public void SanityTest_ContainerizeDependencies()
    {
        IReadOnlyList<string> knownPackageReferences = new List<string>()
        {
            "System.CommandLine",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Logging.Console"
        };
        IReadOnlyList<string> knownProjectReferences = new List<string>()
        {
            "..\\Microsoft.NET.Build.Containers\\Microsoft.NET.Build.Containers.csproj",
            "..\\..\\Cli\\Microsoft.DotNet.Cli.Utils\\Microsoft.DotNet.Cli.Utils.csproj"
        };

        string projectFilePath = Path.Combine(TestContext.Current.TestExecutionDirectory, "Container", "ProjectFiles", "containerize.csproj");
        XDocument project = XDocument.Load(projectFilePath);
        XNamespace ns = project.Root?.Name.Namespace ?? throw new InvalidOperationException("Project file is empty");

        IEnumerable<string?> packageReferences = project.Descendants().Where(element => element.Name.Equals(ns + "PackageReference")).Select(element => element.Attribute("Include")?.Value);
        packageReferences.Should().BeEquivalentTo(knownPackageReferences, $"Known package references for containerize project are different from actual. Check if this is expected. If the new package reference is expected, add it to {nameof(knownPackageReferences)} and verify they are included to NuGet package in package.csproj correctly");

        IEnumerable<string?> projectReferences = project.Descendants().Where(element => element.Name.Equals(ns + "ProjectReference")).Select(element => element.Attribute("Include")?.Value);
        projectReferences.Should().BeEquivalentTo(knownProjectReferences, $"Known project references for containerize project are different from actual. Check if this is expected. If the new project reference is expected, add it to {nameof(knownProjectReferences)} and verify they are included to NuGet package in package.csproj correctly");
    }

    [Fact]
    public void SanityTest_NET_Build_ContainersDependencies()
    {
        IReadOnlyList<string> knownPackageReferences = new List<string>()
        {
            "Microsoft.Build.Utilities.Core",
            "Microsoft.CodeAnalysis.PublicApiAnalyzers",
            "Nuget.Packaging",
            "Valleysoft.DockerCredsProvider",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Logging.Abstractions"
        };
        IReadOnlyList<string> knownProjectReferences = new List<string>()
        {
            "..\\..\\Cli\\Microsoft.DotNet.Cli.Utils\\Microsoft.DotNet.Cli.Utils.csproj"
        };

        string projectFilePath = Path.Combine(TestContext.Current.TestExecutionDirectory, "Container", "ProjectFiles", "Microsoft.NET.Build.Containers.csproj");
        XDocument project = XDocument.Load(projectFilePath);
        XNamespace ns = project.Root?.Name.Namespace ?? throw new InvalidOperationException("Project file is empty");

        IEnumerable<string?> packageReferences = project.Descendants().Where(element => element.Name.Equals(ns + "PackageReference")).Select(element => element.Attribute("Include")?.Value);
        packageReferences.Should().BeEquivalentTo(knownPackageReferences, $"Known package references for Microsoft.NET.Build.Containers project are different from actual. Check if this is expected. If the new package reference is expected, add it to {nameof(knownPackageReferences)} and verify they are included to NuGet package in package.csproj correctly");

        IEnumerable<string?> projectReferences = project.Descendants().Where(element => element.Name.Equals(ns + "ProjectReference")).Select(element => element.Attribute("Include")?.Value);
        projectReferences.Should().BeEquivalentTo(knownProjectReferences, $"Known project references for Microsoft.NET.Build.Containers project are different from actual. Check if this is expected. If the new project reference is expected, add it to {nameof(knownProjectReferences)} and verify they are included to NuGet package in package.csproj correctly");
    }

    [Fact]
    public void PackageContentTest()
    {
        string ignoredZipFileEntriesPrefix = "package/services/metadata";

        IReadOnlyList<string> packageContents = new List<string>()
        {
              "_rels/.rels",
              "[Content_Types].xml",
              "build/Microsoft.NET.Build.Containers.props",
              "build/Microsoft.NET.Build.Containers.targets",
              "containerize/containerize.dll",
              "containerize/containerize.runtimeconfig.json",
              "containerize/Microsoft.DotNet.Cli.Utils.dll",
              "containerize/Microsoft.Extensions.Configuration.Abstractions.dll",
              "containerize/Microsoft.Extensions.Configuration.Binder.dll",
              "containerize/Microsoft.Extensions.Configuration.dll",
              "containerize/Microsoft.Extensions.DependencyInjection.Abstractions.dll",
              "containerize/Microsoft.Extensions.DependencyInjection.dll",
              "containerize/Microsoft.Extensions.DependencyModel.dll",
              "containerize/Microsoft.Extensions.Logging.Abstractions.dll",
              "containerize/Microsoft.Extensions.Logging.Configuration.dll",
              "containerize/Microsoft.Extensions.Logging.Console.dll",
              "containerize/Microsoft.Extensions.Logging.dll",
              "containerize/Microsoft.Extensions.Options.ConfigurationExtensions.dll",
              "containerize/Microsoft.Extensions.Options.dll",
              "containerize/Microsoft.Extensions.Primitives.dll",
              "containerize/Microsoft.NET.Build.Containers.dll",
              "containerize/Newtonsoft.Json.dll",
              "containerize/NuGet.Common.dll",
              "containerize/NuGet.Configuration.dll",
              "containerize/NuGet.DependencyResolver.Core.dll",
              "containerize/NuGet.Frameworks.dll",
              "containerize/NuGet.LibraryModel.dll",
              "containerize/NuGet.Packaging.dll",
              "containerize/NuGet.ProjectModel.dll",
              "containerize/NuGet.Protocol.dll",
              "containerize/NuGet.Versioning.dll",
              "containerize/System.CommandLine.dll",
              "containerize/Valleysoft.DockerCredsProvider.dll",
              "Icon.png",
              "Microsoft.NET.Build.Containers.nuspec",
              "README.md",
              "tasks/net472/Microsoft.Extensions.DependencyInjection.Abstractions.dll",
              "tasks/net472/Microsoft.Extensions.DependencyInjection.dll",
              "tasks/net472/Microsoft.Extensions.DependencyModel.dll",
              "tasks/net472/Microsoft.Extensions.Logging.Abstractions.dll",
              "tasks/net472/Microsoft.Extensions.Logging.dll",
              "tasks/net472/Microsoft.Extensions.Options.dll",
              "tasks/net472/Microsoft.Extensions.Primitives.dll",
              "tasks/net472/Microsoft.NET.Build.Containers.dll",
              "tasks/net472/Newtonsoft.Json.dll",
              "tasks/net472/NuGet.Common.dll",
              "tasks/net472/NuGet.Configuration.dll",
              "tasks/net472/NuGet.DependencyResolver.Core.dll",
              "tasks/net472/NuGet.Frameworks.dll",
              "tasks/net472/NuGet.LibraryModel.dll",
              "tasks/net472/NuGet.Packaging.Core.dll",
              "tasks/net472/NuGet.Packaging.dll",
              "tasks/net472/NuGet.ProjectModel.dll",
              "tasks/net472/NuGet.Protocol.dll",
              "tasks/net472/NuGet.Versioning.dll",
              "tasks/net7.0/Microsoft.DotNet.Cli.Utils.dll",
              "tasks/net7.0/Microsoft.Extensions.DependencyInjection.Abstractions.dll",
              "tasks/net7.0/Microsoft.Extensions.DependencyInjection.dll",
              "tasks/net7.0/Microsoft.Extensions.DependencyModel.dll",
              "tasks/net7.0/Microsoft.Extensions.Logging.Abstractions.dll",
              "tasks/net7.0/Microsoft.Extensions.Logging.dll",
              "tasks/net7.0/Microsoft.Extensions.Options.dll",
              "tasks/net7.0/Microsoft.Extensions.Primitives.dll",
              "tasks/net7.0/Microsoft.NET.Build.Containers.deps.json",
              "tasks/net7.0/Microsoft.NET.Build.Containers.dll",
              "tasks/net7.0/Newtonsoft.Json.dll",
              "tasks/net7.0/NuGet.Common.dll",
              "tasks/net7.0/NuGet.Configuration.dll",
              "tasks/net7.0/NuGet.DependencyResolver.Core.dll",
              "tasks/net7.0/NuGet.Frameworks.dll",
              "tasks/net7.0/NuGet.LibraryModel.dll",
              "tasks/net7.0/NuGet.Packaging.dll",
              "tasks/net7.0/NuGet.Packaging.Core.dll",
              "tasks/net7.0/NuGet.ProjectModel.dll",
              "tasks/net7.0/NuGet.Protocol.dll",
              "tasks/net7.0/NuGet.Versioning.dll",
              "tasks/net7.0/Valleysoft.DockerCredsProvider.dll"
        };

        (string packageFilePath, string packageVersion) = ToolsetUtils.GetContainersPackagePath();
        using ZipArchive archive = new(File.OpenRead(packageFilePath), ZipArchiveMode.Read, false);

        IEnumerable<string> actualEntries = archive.Entries
            .Select(e => e.FullName)
            .Where(e => !e.StartsWith(ignoredZipFileEntriesPrefix, StringComparison.InvariantCultureIgnoreCase))
            .OrderBy(e => e);

        actualEntries
                .Should()
                .BeEquivalentTo(packageContents, $"{Path.GetFileName(packageFilePath)} content differs from expected. Please add the entry to the list, if the addition is expected.");
    }
}
