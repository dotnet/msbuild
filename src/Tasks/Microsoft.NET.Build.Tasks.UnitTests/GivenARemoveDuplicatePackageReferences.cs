// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenARemoveDuplicatePackageReferenceTask
    {
        private static ITaskItem[] GetPackageRefItems(List<PackageIdentity> packages)
            => packages.Select(kvp => new MockTaskItem(
                itemSpec: kvp.Id,
                metadata: new Dictionary<string, string> { { "Version", kvp.Version.ToString() } })).ToArray();

        [Fact]
        public void RemoveDuplicatePackageReference()
        {
            var knownpackage = new List<PackageIdentity>();

            knownpackage.Add(new PackageIdentity("Microsoft.NETCore.Targets", NuGetVersion.Parse("1.2.0-beta-24821-02")));
            knownpackage.Add(new PackageIdentity("System.Private.Uri", NuGetVersion.Parse("4.4.0-beta-24821-02")));
            knownpackage.Add(new PackageIdentity("Microsoft.NETCore.CoreDisTools", NuGetVersion.Parse("1.0.1-prerelease-00001")));
            knownpackage.Add(new PackageIdentity("Microsoft.NETCore.Platforms", NuGetVersion.Parse("1.2.0-beta-24821-02")));

            //duplicates
            knownpackage.Add(new PackageIdentity("Microsoft.NETCore.Targets", NuGetVersion.Parse("1.2.0-beta-24821-02")));
            knownpackage.Add(new PackageIdentity("Microsoft.NETCore.CoreDisTools", NuGetVersion.Parse("1.0.1-prerelease-00001")));

            var packagelistWithoutDups = new HashSet<PackageIdentity>(knownpackage);

            Assert.True(knownpackage.Count() > packagelistWithoutDups.Count());

            // execute task
            var task = new RemoveDuplicatePackageReferences()
            {
                InputPackageReferences = GetPackageRefItems(knownpackage)

            };
            task.Execute().Should().BeTrue();

            task.UniquePackageReferences.Count().Should().Be(packagelistWithoutDups.Count());

            var uniquePackages = new List<PackageIdentity>();
            foreach (var item in task.UniquePackageReferences)
            {
                var pkgName = item.ItemSpec;
                var pkgVersion = NuGetVersion.Parse(item.GetMetadata("Version"));
                uniquePackages.Add(new PackageIdentity(pkgName, pkgVersion));
            }

            foreach (var pkg in uniquePackages)
            {
                packagelistWithoutDups.Should().Contain(elem => elem.Equals(pkg), "package {0}, version {1} was not expected to be stored", pkg.Id, pkg.Version);
            }
        }

    }
}