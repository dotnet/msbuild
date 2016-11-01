// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class PackageConstants
    {
        public const string SdkPackageName = "Microsoft.NET.Sdk";
        public const string WebSdkPackageName = "Microsoft.NET.Sdk.Web";
        public const string TestSdkPackageName = "Microsoft.NET.Test.Sdk";
        public const string TestSdkPackageVersion = "15.0.0-preview-20161024-02";
        public const string XUnitPackageName = "xunit";
        public const string XUnitPackageVersion = "2.2.0-beta3-build3402";
        public const string XUnitRunnerPackageName = "xunit.runner.visualstudio";
        public const string XUnitRunnerPackageVersion = "2.2.0-beta4-build1188";

        public static readonly IDictionary<string, string> AspProjectDependencyToolsPackages = new Dictionary<string, string> {
            {"Microsoft.EntityFrameworkCore.Tools", "Microsoft.EntityFrameworkCore.Tools"},
            {"Microsoft.AspNetCore.Razor.Tools", "Microsoft.AspNetCore.Razor.Design"},
            {"Microsoft.AspNetCore.Razor.Design", "Microsoft.AspNetCore.Razor.Design"},
            {"Microsoft.VisualStudio.Web.CodeGenerators.Mvc", "Microsoft.VisualStudio.Web.CodGeneration.Design"},
            {"Microsoft.VisualStudio.Web.CodeGeneration.Tools", ""},
        };

        public static readonly IDictionary<string, string> AspProjectToolsPackages = new Dictionary<string, string> {
            {"Microsoft.EntityFrameworkCore.Tools", "Microsoft.EntityFrameworkCore.Tools.DotNet"},
            {"Microsoft.AspNetCore.Razor.Tools", "Microsoft.AspNetCore.Razor.Tools"},
            {"Microsoft.VisualStudio.Web.CodeGeneration.Tools", "Microsoft.VisualStudio.Web.CodeGeneration.Tools"},
            {"Microsoft.DotNet.Watcher.Tools", "Microsoft.DotNet.Watcher.Tools"},
            {"Microsoft.Extensions.SecretManager.Tools", "Microsoft.Extensions.SecretManager.Tools"},
            {"Microsoft.AspNetCore.Server.IISIntegration.Tools", ""}
        };
    }
}