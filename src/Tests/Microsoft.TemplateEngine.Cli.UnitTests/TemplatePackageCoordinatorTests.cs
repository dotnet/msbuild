// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using static Microsoft.TemplateEngine.Cli.NuGet.NugetApiManager;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class TemplatePackageCoordinatorTests
    {
        [Fact]
        public void DisplayLocalPackageMetadata()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            TemplatePackageManager templatePackageManager = A.Fake<TemplatePackageManager>();
            var packageCoordinator = new TemplatePackageCoordinator(settings, templatePackageManager);
            BufferedReporter bufferedReporter = new BufferedReporter();

            var localPackage = A.Fake<IManagedTemplatePackage>();
            A.CallTo(() => localPackage.Identifier).Returns("testPackage");
            A.CallTo(() => localPackage.GetDetails())
                .Returns(new Dictionary<string, string>
                {
                    { "Author", "Microsoft" },
                    { "NuGetSource", "ANuGetSource" }
                });

            packageCoordinator.DisplayLocalPackageMetadata(localPackage, bufferedReporter);
            bufferedReporter.Lines.Should()
                .Contain("testPackage")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_Authors}:")
                .And.Contain("      Microsoft")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_RepoUrl}: ANuGetSource");
        }

        [Fact]
        public void DisplayLocalPackageMetadata_NoData()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            TemplatePackageManager templatePackageManager = A.Fake<TemplatePackageManager>();
            var packageCoordinator = new TemplatePackageCoordinator(settings, templatePackageManager);
            BufferedReporter bufferedReporter = new BufferedReporter();

            var localPackage = A.Fake<IManagedTemplatePackage>();
            A.CallTo(() => localPackage.Identifier).Returns("testPackage");
            A.CallTo(() => localPackage.GetDetails())
                .Returns(new Dictionary<string, string> { });

            packageCoordinator.DisplayLocalPackageMetadata(localPackage, bufferedReporter);
            bufferedReporter.Lines.Should()
                .ContainSingle("testPackage");
        }

        [Fact]
        public void DisplayNuGetPackageMetadata()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            TemplatePackageManager templatePackageManager = A.Fake<TemplatePackageManager>();
            var packageCoordinator = new TemplatePackageCoordinator(settings, templatePackageManager);
            BufferedReporter bufferedReporter = new BufferedReporter();

            var version = new NuGetVersion("1.5.24");
            var identity = new PackageIdentity("PackageId", version);
            var licenseMetadata = A.Fake<LicenseMetadata>();
            var searchMetadata = A.Fake<IPackageSearchMetadata>();
            A.CallTo(() => searchMetadata.Authors).Returns("PackageAuthor");
            A.CallTo(() => searchMetadata.Identity).Returns(identity);
            A.CallTo(() => searchMetadata.Description).Returns("This is the package description");
            A.CallTo(() => searchMetadata.ProjectUrl).Returns(new Uri("http://github.com"));
            A.CallTo(() => searchMetadata.LicenseUrl).Returns(new Uri("https://github.com/dotnet/sdk"));
            A.CallTo(() => searchMetadata.LicenseMetadata).Returns(licenseMetadata);
            A.CallTo(() => searchMetadata.LicenseMetadata.LicenseExpression.ToString()).Returns("MIT");

            var extraMetadata = A.Fake<IPackageSearchMetadata>();
            A.CallTo(() => extraMetadata.Owners).Returns("packageOwner");

            var source = new PackageSource("packageSource");
            var nugetPackage = new NugetPackageMetadata(
                source,
                searchMetadata,
                extraMetadata);

            packageCoordinator.DisplayNuGetPackageMetadata(nugetPackage, bufferedReporter);
            bufferedReporter.Lines.Should()
                .Contain("PackageId")
                .And.Contain("   Package version: 1.5.24")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_Description}: This is the package description")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_Authors}:")
                .And.Contain("      PackageAuthor")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_Owners}:")
                .And.Contain("      https://nuget.org/profiles/packageOwner")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_LicenseExpression}: https://licenses.nuget.org/MIT")
                .And.Contain($"      {LocalizableStrings.DetailsCommand_Property_LicenseUrl}: https://github.com/dotnet/sdk")
                .And.Contain($"      {LocalizableStrings.DetailsCommand_Property_RepoUrl}: http://github.com/")
                .And.NotContain($"   {LocalizableStrings.DetailsCommand_Property_PrefixReserved}: true");
        }

        [Fact]
        public void DisplayNuGetPackageMetadata_PrefixReserved()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            TemplatePackageManager templatePackageManager = A.Fake<TemplatePackageManager>();
            var packageCoordinator = new TemplatePackageCoordinator(settings, templatePackageManager);
            BufferedReporter bufferedReporter = new BufferedReporter();

            var version = new NuGetVersion("1.5.24");
            var identity = new PackageIdentity("PackageId", version);
            var licenseMetadata = A.Fake<LicenseMetadata>();
            var searchMetadata = A.Fake<IPackageSearchMetadata>();
            A.CallTo(() => searchMetadata.Authors).Returns("PackageAuthor");
            A.CallTo(() => searchMetadata.Identity).Returns(identity);
            A.CallTo(() => searchMetadata.Description).Returns("This is the package description");
            A.CallTo(() => searchMetadata.ProjectUrl).Returns(new Uri("http://github.com"));
            A.CallTo(() => searchMetadata.LicenseUrl).Returns(new Uri("https://github.com/dotnet/sdk"));
            A.CallTo(() => searchMetadata.LicenseMetadata).Returns(licenseMetadata);
            A.CallTo(() => searchMetadata.LicenseMetadata.LicenseExpression.ToString()).Returns("MIT");

            var extraMetadata = A.Fake<IPackageSearchMetadata>();
            A.CallTo(() => extraMetadata.Owners).Returns("packageOwner");
            A.CallTo(() => extraMetadata.PrefixReserved).Returns(true);

            var source = new PackageSource("https://api.nuget.org/v3/index.json");
            var nugetPackage = new NugetPackageMetadata(
                source,
                searchMetadata,
                extraMetadata);

            packageCoordinator.DisplayNuGetPackageMetadata(nugetPackage, bufferedReporter);
            bufferedReporter.Lines.Should()
                .Contain("PackageId")
                .And.Contain("   Package version: 1.5.24")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_Description}: This is the package description")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_Authors}:")
                .And.Contain("      PackageAuthor")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_Owners}:")
                .And.Contain("      https://nuget.org/profiles/packageOwner")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_LicenseExpression}: https://licenses.nuget.org/MIT")
                .And.Contain($"      {LocalizableStrings.DetailsCommand_Property_LicenseUrl}: https://github.com/dotnet/sdk")
                .And.Contain($"      {LocalizableStrings.DetailsCommand_Property_RepoUrl}: http://github.com/")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_PrefixReserved}: True");
        }

        [Fact]
        public void DisplayNuGetPackageMetadata_MultipleAuthors()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            TemplatePackageManager templatePackageManager = A.Fake<TemplatePackageManager>();
            var packageCoordinator = new TemplatePackageCoordinator(settings, templatePackageManager);
            BufferedReporter bufferedReporter = new BufferedReporter();

            var version = new NuGetVersion("1.5.24");
            var identity = new PackageIdentity("PackageId", version);
            var licenseMetadata = A.Fake<LicenseMetadata>();
            var searchMetadata = A.Fake<IPackageSearchMetadata>();
            A.CallTo(() => searchMetadata.Authors).Returns("author1, author2, author3");
            A.CallTo(() => searchMetadata.Identity).Returns(identity);
            A.CallTo(() => searchMetadata.Description).Returns("This is the package description");
            A.CallTo(() => searchMetadata.ProjectUrl).Returns(new Uri("http://github.com"));
            A.CallTo(() => searchMetadata.LicenseUrl).Returns(new Uri("https://github.com/dotnet/sdk"));
            A.CallTo(() => searchMetadata.LicenseMetadata).Returns(licenseMetadata);
            A.CallTo(() => searchMetadata.LicenseMetadata.LicenseExpression.ToString()).Returns("MIT");

            var extraMetadata = A.Fake<IPackageSearchMetadata>();
            A.CallTo(() => extraMetadata.Owners).Returns("packageOwner");
            A.CallTo(() => extraMetadata.PrefixReserved).Returns(true);

            var source = new PackageSource("packageSource");
            var nugetPackage = new NugetPackageMetadata(
                source,
                searchMetadata,
                extraMetadata);

            packageCoordinator.DisplayNuGetPackageMetadata(nugetPackage, bufferedReporter);
            bufferedReporter.Lines.Should()
                .Contain("PackageId")
                .And.Contain("   Package version: 1.5.24")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_Description}: This is the package description")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_Authors}:")
                .And.Contain("      author1")
                .And.Contain("      author2")
                .And.Contain("      author3");
        }

        [Fact]
        public void DisplayNuGetPackageMetadata_MultipleOwners()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            TemplatePackageManager templatePackageManager = A.Fake<TemplatePackageManager>();
            var packageCoordinator = new TemplatePackageCoordinator(settings, templatePackageManager);
            BufferedReporter bufferedReporter = new BufferedReporter();

            var version = new NuGetVersion("1.5.24");
            var identity = new PackageIdentity("PackageId", version);
            var licenseMetadata = A.Fake<LicenseMetadata>();
            var searchMetadata = A.Fake<IPackageSearchMetadata>();
            A.CallTo(() => searchMetadata.Authors).Returns("PackageAuthor");
            A.CallTo(() => searchMetadata.Identity).Returns(identity);
            A.CallTo(() => searchMetadata.Description).Returns("This is the package description");
            A.CallTo(() => searchMetadata.ProjectUrl).Returns(new Uri("http://github.com"));
            A.CallTo(() => searchMetadata.LicenseUrl).Returns(new Uri("https://github.com/dotnet/sdk"));
            A.CallTo(() => searchMetadata.LicenseMetadata).Returns(licenseMetadata);
            A.CallTo(() => searchMetadata.LicenseMetadata.LicenseExpression.ToString()).Returns("MIT");

            var extraMetadata = A.Fake<IPackageSearchMetadata>();
            A.CallTo(() => extraMetadata.Owners).Returns("owner1, owner2");
            A.CallTo(() => extraMetadata.PrefixReserved).Returns(true);

            var source = new PackageSource("packageSource");
            var nugetPackage = new NugetPackageMetadata(
                source,
                searchMetadata,
                extraMetadata);

            packageCoordinator.DisplayNuGetPackageMetadata(nugetPackage, bufferedReporter);
            bufferedReporter.Lines.Should()
                .Contain("PackageId")
                .And.Contain("   Package version: 1.5.24")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_Description}: This is the package description")
                .And.Contain($"   {LocalizableStrings.DetailsCommand_Property_Owners}:")
                .And.Contain("      https://nuget.org/profiles/owner1")
                .And.Contain("      https://nuget.org/profiles/owner2");
        }

    }
}
