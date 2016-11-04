// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class PackageDependencyInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string PrivateAssets { get; set; }
    }

    internal class PackageConstants
    {
        public const string SdkPackageName = "Microsoft.NET.Sdk";
        public const string WebSdkPackageName = "Microsoft.NET.Sdk.Web";
        public const string TestSdkPackageName = "Microsoft.NET.Test.Sdk";
        public const string XUnitPackageName = "xunit";
        public const string XUnitRunnerPackageName = "xunit.runner.visualstudio";
        public const string MstestTestAdapterName = "MSTest.TestAdapter";
        public const string MstestTestFrameworkName = "MSTest.TestFramework";
        public const string NetStandardPackageName = "NETStandard.Library";
        public const string NetStandardPackageVersion = "1.6.0";

        public static readonly IDictionary<string, PackageDependencyInfo> ProjectDependencyPackages = 
            new Dictionary<string, PackageDependencyInfo> {
                {"Microsoft.EntityFrameworkCore.Tools", new PackageDependencyInfo {
                    Name = "Microsoft.EntityFrameworkCore.Tools",
                    Version = ConstantPackageVersions.AspNetToolsVersion } },
                { "Microsoft.AspNetCore.Razor.Tools", null },
                { "Microsoft.AspNetCore.Razor.Design", null },
                { "Microsoft.VisualStudio.Web.CodeGenerators.Mvc", new PackageDependencyInfo {
                    Name = "Microsoft.VisualStudio.Web.CodeGeneration.Design",
                    Version = ConstantPackageVersions.AspNetToolsVersion } },
                { "Microsoft.VisualStudio.Web.CodeGeneration.Tools", null},
                { TestSdkPackageName, new PackageDependencyInfo {
                    Name = TestSdkPackageName,
                    Version = ConstantPackageVersions.TestSdkPackageVersion } },
                { XUnitPackageName, new PackageDependencyInfo {
                    Name = XUnitPackageName,
                    Version = ConstantPackageVersions.XUnitPackageVersion } },
                { XUnitRunnerPackageName, new PackageDependencyInfo {
                    Name = XUnitRunnerPackageName,
                    Version = ConstantPackageVersions.XUnitRunnerPackageVersion } },
                { MstestTestAdapterName, new PackageDependencyInfo {
                    Name = MstestTestAdapterName,
                    Version = ConstantPackageVersions.MstestTestAdapterVersion } },
                { MstestTestFrameworkName, new PackageDependencyInfo {
                    Name = MstestTestFrameworkName,
                    Version = ConstantPackageVersions.MstestTestFrameworkVersion } },
        };

        public static readonly IDictionary<string, PackageDependencyInfo> ProjectToolPackages = 
            new Dictionary<string, PackageDependencyInfo> {
                {"Microsoft.EntityFrameworkCore.Tools", new PackageDependencyInfo {
                    Name = "Microsoft.EntityFrameworkCore.Tools.DotNet",
                    Version = ConstantPackageVersions.AspNetToolsVersion } },
                { "Microsoft.AspNetCore.Razor.Tools", null },
                { "Microsoft.VisualStudio.Web.CodeGeneration.Tools", new PackageDependencyInfo {
                    Name = "Microsoft.VisualStudio.Web.CodeGeneration.Tools",
                    Version = ConstantPackageVersions.AspNetToolsVersion } },
                { "Microsoft.DotNet.Watcher.Tools", new PackageDependencyInfo {
                    Name = "Microsoft.DotNet.Watcher.Tools",
                    Version = ConstantPackageVersions.AspNetToolsVersion } },
                { "Microsoft.Extensions.SecretManager.Tools", new PackageDependencyInfo {
                    Name = "Microsoft.Extensions.SecretManager.Tools",
                    Version = ConstantPackageVersions.AspNetToolsVersion } },
                { "Microsoft.AspNetCore.Server.IISIntegration.Tools", null},
                { "BundlerMinifier.Core", new PackageDependencyInfo {
                    Name = "BundlerMinifier.Core",
                    Version = ConstantPackageVersions.BundleMinifierToolVersion } }
        };
    }
}