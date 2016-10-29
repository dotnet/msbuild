// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class PackageConstants
    {
        public const string SdkPackageName = "Microsoft.NET.Sdk";
        public const string WebSdkPackageName = "Microsoft.NET.Sdk.Web";

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
            {"Microsoft.VisualStudio.Web.CodeGeneration.Tools", "Microsoft.VisualStudio.Web.CodGeneration.Tools"},
            {"Microsoft.DotNet.Watcher.Tools", "Microsoft.DotNet.Watcher.Tools"},
            {"Microsoft.Extensions.SecretManager.Tools", "Microsoft.Extensions.SecretManager.Tools"},
            {"Microsoft.AspNetCore.Server.IISIntegration.Tools", ""}
        };
    }
}