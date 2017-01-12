// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using System.Linq;
using Xunit;
using FluentAssertions;
using System;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigratePackagesToTheirLTSVersions : PackageDependenciesTestBase
    {
        [Theory]
        [InlineData("Microsoft.NETCore.App", "1.0.0", "Microsoft.NETCore.App", "1.0.3")]
        [InlineData("Microsoft.NETCore.App", "1.0.3-preview2", "Microsoft.NETCore.App", "1.0.3")]
        [InlineData("NETStandard.Library", "1.4.0", "NETStandard.Library", "1.6.0")]
        public void ItUpliftsMetaPackages(
            string sourcePackageName,
            string sourceVersion,
            string targetPackageName,
            string targetVersion)
        {
            ValidatePackageMigration(sourcePackageName, sourceVersion, targetPackageName, targetVersion);
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.Antiforgery", "1.0.0", "Microsoft.AspNetCore.Antiforgery", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Mvc", "1.0.0", "Microsoft.AspNetCore.Mvc", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Mvc.Abstractions", "1.0.0", "Microsoft.AspNetCore.Mvc.Abstractions", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Mvc.ApiExplorer", "1.0.0", "Microsoft.AspNetCore.Mvc.ApiExplorer", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Mvc.Core", "1.0.0", "Microsoft.AspNetCore.Mvc.Core", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Mvc.Cors", "1.0.0", "Microsoft.AspNetCore.Mvc.Cors", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Mvc.DataAnnotations", "1.0.0", "Microsoft.AspNetCore.Mvc.DataAnnotations", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Mvc.Formatters.Json", "1.0.0", "Microsoft.AspNetCore.Mvc.Formatters.Json", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Mvc.Formatters.Xml", "1.0.0", "Microsoft.AspNetCore.Mvc.Formatters.Xml", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Mvc.Localization", "1.0.0", "Microsoft.AspNetCore.Mvc.Localization", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Mvc.Razor", "1.0.0", "Microsoft.AspNetCore.Mvc.Razor", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Mvc.Razor.Host", "1.0.0", "Microsoft.AspNetCore.Mvc.Razor.Host", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Mvc.TagHelpers", "1.0.0", "Microsoft.AspNetCore.Mvc.TagHelpers", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Mvc.ViewFeatures", "1.0.0", "Microsoft.AspNetCore.Mvc.ViewFeatures", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Mvc.WebApiCompatShim", "1.0.0", "Microsoft.AspNetCore.Mvc.WebApiCompatShim", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Routing", "1.0.0", "Microsoft.AspNetCore.Routing", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Routing.Abstractions", "1.0.0", "Microsoft.AspNetCore.Routing.Abstractions", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Server.Kestrel", "1.0.0", "Microsoft.AspNetCore.Server.Kestrel", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.AspNetCore.Server.Kestrel.Https", "1.0.0", "Microsoft.AspNetCore.Server.Kestrel.Https", ConstantPackageVersions.AspNet101PackagesVersion)]
        public void ItUpliftsAspNetCorePackages(
            string sourcePackageName,
            string sourceVersion,
            string targetPackageName,
            string targetVersion)
        {
            ValidatePackageMigration(sourcePackageName, sourceVersion, targetPackageName, targetVersion);
        }

        [Theory]
        [InlineData("Microsoft.EntityFrameworkCore", "1.0.0", "Microsoft.EntityFrameworkCore", ConstantPackageVersions.EntityFramework101PackagesVersion)]
        [InlineData("Microsoft.EntityFrameworkCore.InMemory", "1.0.0", "Microsoft.EntityFrameworkCore.InMemory", ConstantPackageVersions.EntityFramework101PackagesVersion)]
        [InlineData("Microsoft.EntityFrameworkCore.Relational", "1.0.0", "Microsoft.EntityFrameworkCore.Relational", ConstantPackageVersions.EntityFramework101PackagesVersion)]
        [InlineData("Microsoft.EntityFrameworkCore.Relational.Design", "1.0.0", "Microsoft.EntityFrameworkCore.Relational.Design", ConstantPackageVersions.EntityFramework101PackagesVersion)]
        [InlineData("Microsoft.EntityFrameworkCore.Relational.Design.Specification.Tests", "1.0.0", "Microsoft.EntityFrameworkCore.Relational.Design.Specification.Tests", ConstantPackageVersions.EntityFramework101PackagesVersion)]
        [InlineData("Microsoft.EntityFrameworkCore.Relational.Specification.Tests", "1.0.0", "Microsoft.EntityFrameworkCore.Relational.Specification.Tests", ConstantPackageVersions.EntityFramework101PackagesVersion)]
        [InlineData("Microsoft.EntityFrameworkCore.Specification.Tests", "1.0.0", "Microsoft.EntityFrameworkCore.Specification.Tests", ConstantPackageVersions.EntityFramework101PackagesVersion)]
        [InlineData("Microsoft.EntityFrameworkCore.Sqlite", "1.0.0", "Microsoft.EntityFrameworkCore.Sqlite", ConstantPackageVersions.EntityFramework101PackagesVersion)]
        [InlineData("Microsoft.EntityFrameworkCore.Sqlite.Design", "1.0.0", "Microsoft.EntityFrameworkCore.Sqlite.Design", ConstantPackageVersions.EntityFramework101PackagesVersion)]
        [InlineData("Microsoft.EntityFrameworkCore.SqlServer", "1.0.0", "Microsoft.EntityFrameworkCore.SqlServer", ConstantPackageVersions.EntityFramework101PackagesVersion)]
        [InlineData("Microsoft.EntityFrameworkCore.SqlServer.Design", "1.0.0", "Microsoft.EntityFrameworkCore.SqlServer.Design", ConstantPackageVersions.EntityFramework101PackagesVersion)]
        public void ItUpliftsEntityFrameworkCorePackages(
            string sourcePackageName,
            string sourceVersion,
            string targetPackageName,
            string targetVersion)
        {
            ValidatePackageMigration(sourcePackageName, sourceVersion, targetPackageName, targetVersion);
        }

        [Theory]
        [InlineData("Microsoft.NETCore.JIT", "1.0.0", "Microsoft.NETCore.JIT", "1.0.5")]
        [InlineData("Microsoft.NETCore.Runtime.CoreCLR", "1.0.0", "Microsoft.NETCore.Runtime.CoreCLR", "1.0.5")]
        [InlineData("Microsoft.NETCore.DotNetHost", "1.0.0", "Microsoft.NETCore.DotNetHost", "1.0.1")]
        [InlineData("Microsoft.NETCore.DotNetHostPolicy", "1.0.0", "Microsoft.NETCore.DotNetHostPolicy", "1.0.3")]
        [InlineData("Microsoft.NETCore.DotNetHostResolver", "1.0.0", "Microsoft.NETCore.DotNetHostResolver", "1.0.1")]
        [InlineData("Microsoft.NETCore.Platforms", "1.0.0", "Microsoft.NETCore.Platforms", "1.0.2")]
        [InlineData("Microsoft.NETCore.Targets", "1.0.0", "Microsoft.NETCore.Targets", "1.0.1")]
        [InlineData("Microsoft.NETCore.Windows.ApiSets", "1.0.0", "Microsoft.NETCore.Windows.ApiSets", "1.0.1")]
        public void ItUpliftsCoreCLRPackages(
            string sourcePackageName,
            string sourceVersion,
            string targetPackageName,
            string targetVersion)
        {
            ValidatePackageMigration(sourcePackageName, sourceVersion, targetPackageName, targetVersion);
        }

        [Theory]
        [InlineData("System.Net.Http", "1.0.0", "System.Net.Http", "4.1.1")]
        [InlineData("System.AppContext", "1.0.0", "System.AppContext", "4.1.0")]
        [InlineData("System.Buffers", "1.0.0", "System.Buffers", "4.0.0")]
        [InlineData("System.Collections", "1.0.0", "System.Collections", "4.0.11")]
        [InlineData("System.Collections.Concurrent", "1.0.0", "System.Collections.Concurrent", "4.0.12")]
        [InlineData("System.Collections.Immutable", "1.0.0", "System.Collections.Immutable", "1.2.0")]
        [InlineData("System.ComponentModel", "1.0.0", "System.ComponentModel", "4.0.1")]
        [InlineData("System.ComponentModel.Annotations", "1.0.0", "System.ComponentModel.Annotations", "4.0.1")]
        [InlineData("System.Console", "1.0.0", "System.Console", "4.0.0")]
        [InlineData("System.Diagnostics.Debug", "1.0.0", "System.Diagnostics.Debug", "4.0.11")]
        [InlineData("System.Diagnostics.DiagnosticSource", "1.0.0", "System.Diagnostics.DiagnosticSource", "4.0.0")]
        [InlineData("System.Diagnostics.FileVersionInfo", "1.0.0", "System.Diagnostics.FileVersionInfo", "4.0.0")]
        [InlineData("System.Diagnostics.Process", "1.0.0", "System.Diagnostics.Process", "4.1.0")]
        [InlineData("System.Diagnostics.StackTrace", "1.0.0", "System.Diagnostics.StackTrace", "4.0.1")]
        [InlineData("System.Diagnostics.Tools", "1.0.0", "System.Diagnostics.Tools", "4.0.1")]
        [InlineData("System.Diagnostics.Tracing", "1.0.0", "System.Diagnostics.Tracing", "4.1.0")]
        [InlineData("System.Dynamic.Runtime", "1.0.0", "System.Dynamic.Runtime", "4.0.11")]
        [InlineData("System.Globalization", "1.0.0", "System.Globalization", "4.0.11")]
        [InlineData("System.Globalization.Calendars", "1.0.0", "System.Globalization.Calendars", "4.0.1")]
        [InlineData("System.Globalization.Extensions", "1.0.0", "System.Globalization.Extensions", "4.0.1")]
        [InlineData("System.IO", "1.0.0", "System.IO", "4.1.0")]
        [InlineData("System.IO.Compression", "1.0.0", "System.IO.Compression", "4.1.0")]
        [InlineData("System.IO.Compression.ZipFile", "1.0.0", "System.IO.Compression.ZipFile", "4.0.1")]
        [InlineData("System.IO.MemoryMappedFiles", "1.0.0", "System.IO.MemoryMappedFiles", "4.0.0")]
        [InlineData("System.IO.UnmanagedMemoryStream", "1.0.0", "System.IO.UnmanagedMemoryStream", "4.0.1")]
        [InlineData("System.Linq", "1.0.0", "System.Linq", "4.1.0")]
        [InlineData("System.Linq.Expressions", "1.0.0", "System.Linq.Expressions", "4.1.0")]
        [InlineData("System.Linq.Parallel", "1.0.0", "System.Linq.Parallel", "4.0.1")]
        [InlineData("System.Linq.Queryable", "1.0.0", "System.Linq.Queryable", "4.0.1")]
        [InlineData("System.Net.NameResolution", "1.0.0", "System.Net.NameResolution", "4.0.0")]
        [InlineData("System.Net.Primitives", "1.0.0", "System.Net.Primitives", "4.0.11")]
        [InlineData("System.Net.Requests", "1.0.0", "System.Net.Requests", "4.0.11")]
        [InlineData("System.Net.Security", "1.0.0", "System.Net.Security", "4.0.0")]
        [InlineData("System.Net.Sockets", "1.0.0", "System.Net.Sockets", "4.1.0")]
        [InlineData("System.Net.WebHeaderCollection", "1.0.0", "System.Net.WebHeaderCollection", "4.0.1")]
        [InlineData("System.Numerics.Vectors", "1.0.0", "System.Numerics.Vectors", "4.1.1")]
        [InlineData("System.ObjectModel", "1.0.0", "System.ObjectModel", "4.0.12")]
        [InlineData("System.Reflection", "1.0.0", "System.Reflection", "4.1.0")]
        [InlineData("System.Reflection.DispatchProxy", "1.0.0", "System.Reflection.DispatchProxy", "4.0.1")]
        [InlineData("System.Reflection.Emit", "1.0.0", "System.Reflection.Emit", "4.0.1")]
        [InlineData("System.Reflection.Emit.ILGeneration", "1.0.0", "System.Reflection.Emit.ILGeneration", "4.0.1")]
        [InlineData("System.Reflection.Emit.Lightweight", "1.0.0", "System.Reflection.Emit.Lightweight", "4.0.1")]
        [InlineData("System.Reflection.Extensions", "1.0.0", "System.Reflection.Extensions", "4.0.1")]
        [InlineData("System.Reflection.Metadata", "1.0.0", "System.Reflection.Metadata", "1.3.0")]
        [InlineData("System.Reflection.Primitives", "1.0.0", "System.Reflection.Primitives", "4.0.1")]
        [InlineData("System.Reflection.TypeExtensions", "1.0.0", "System.Reflection.TypeExtensions", "4.1.0")]
        [InlineData("System.Resources.Reader", "1.0.0", "System.Resources.Reader", "4.0.0")]
        [InlineData("System.Resources.ResourceManager", "1.0.0", "System.Resources.ResourceManager", "4.0.1")]
        [InlineData("System.Runtime", "1.0.0", "System.Runtime", "4.1.0")]
        [InlineData("System.Runtime.Extensions", "1.0.0", "System.Runtime.Extensions", "4.1.0")]
        [InlineData("System.Runtime.Handles", "1.0.0", "System.Runtime.Handles", "4.0.1")]
        [InlineData("System.Runtime.InteropServices", "1.0.0", "System.Runtime.InteropServices", "4.1.0")]
        [InlineData("System.Runtime.InteropServices.RuntimeInformation", "1.0.0", "System.Runtime.InteropServices.RuntimeInformation", "4.0.0")]
        [InlineData("System.Runtime.Loader", "1.0.0", "System.Runtime.Loader", "4.0.0")]
        [InlineData("System.Runtime.Numerics", "1.0.0", "System.Runtime.Numerics", "4.0.1")]
        [InlineData("System.Security.Claims", "1.0.0", "System.Security.Claims", "4.0.1")]
        [InlineData("System.Security.Cryptography.Algorithms", "1.0.0", "System.Security.Cryptography.Algorithms", "4.2.0")]
        [InlineData("System.Security.Cryptography.Cng", "1.0.0", "System.Security.Cryptography.Cng", "4.2.0")]
        [InlineData("System.Security.Cryptography.Csp", "1.0.0", "System.Security.Cryptography.Csp", "4.0.0")]
        [InlineData("System.Security.Cryptography.Encoding", "1.0.0", "System.Security.Cryptography.Encoding", "4.0.0")]
        [InlineData("System.Security.Cryptography.OpenSsl", "1.0.0", "System.Security.Cryptography.OpenSsl", "4.0.0")]
        [InlineData("System.Security.Cryptography.Primitives", "1.0.0", "System.Security.Cryptography.Primitives", "4.0.0")]
        [InlineData("System.Security.Cryptography.X509Certificates", "1.0.0", "System.Security.Cryptography.X509Certificates", "4.1.0")]
        [InlineData("System.Security.Principal", "1.0.0", "System.Security.Principal", "4.0.1")]
        [InlineData("System.Security.Principal.Windows", "1.0.0", "System.Security.Principal.Windows", "4.0.0")]
        [InlineData("System.Text.Encoding", "1.0.0", "System.Text.Encoding", "4.0.11")]
        [InlineData("System.Text.Encoding.CodePages", "1.0.0", "System.Text.Encoding.CodePages", "4.0.1")]
        [InlineData("System.Text.Encoding.Extensions", "1.0.0", "System.Text.Encoding.Extensions", "4.0.11")]
        [InlineData("System.Text.RegularExpressions", "1.0.0", "System.Text.RegularExpressions", "4.1.0")]
        [InlineData("System.Threading", "1.0.0", "System.Threading", "4.0.11")]
        [InlineData("System.Threading.Overlapped", "1.0.0", "System.Threading.Overlapped", "4.0.1")]
        [InlineData("System.Threading.Tasks", "1.0.0", "System.Threading.Tasks", "4.0.11")]
        [InlineData("System.Threading.Tasks.Dataflow", "1.0.0", "System.Threading.Tasks.Dataflow", "4.6.0")]
        [InlineData("System.Threading.Tasks.Extensions", "1.0.0", "System.Threading.Tasks.Extensions", "4.0.0")]
        [InlineData("System.Threading.Tasks.Parallel", "1.0.0", "System.Threading.Tasks.Parallel", "4.0.1")]
        [InlineData("System.Threading.Thread", "1.0.0", "System.Threading.Thread", "4.0.0")]
        [InlineData("System.Threading.ThreadPool", "1.0.0", "System.Threading.ThreadPool", "4.0.10")]
        [InlineData("System.Threading.Timer", "1.0.0", "System.Threading.Timer", "4.0.1")]
        [InlineData("System.Xml.ReaderWriter", "1.0.0", "System.Xml.ReaderWriter", "4.0.11")]
        [InlineData("System.Xml.XDocument", "1.0.0", "System.Xml.XDocument", "4.0.11")]
        [InlineData("System.Xml.XmlDocument", "1.0.0", "System.Xml.XmlDocument", "4.0.1")]
        [InlineData("System.Xml.XPath", "1.0.0", "System.Xml.XPath", "4.0.1")]
        [InlineData("System.Xml.XPath.XmlDocument", "1.0.0", "System.Xml.XPath.XmlDocument", "4.0.1")]
        [InlineData("runtime.native.System", "1.0.0", "runtime.native.System", "4.0.0")]
        [InlineData("runtime.native.System.IO.Compression", "1.0.0", "runtime.native.System.IO.Compression", "4.1.0")]
        [InlineData("runtime.native.System.Net.Http", "1.0.0", "runtime.native.System.Net.Http", "4.0.1")]
        [InlineData("runtime.native.System.Net.Security", "1.0.0", "runtime.native.System.Net.Security", "4.0.1")]
        [InlineData("runtime.native.System.Security.Cryptography", "1.0.0", "runtime.native.System.Security.Cryptography", "4.0.0")]
        [InlineData("Libuv", "1.0.0", "Libuv", "1.9.1")]
        [InlineData("Microsoft.CodeAnalysis.Analyzers", "1.0.0", "Microsoft.CodeAnalysis.Analyzers", "1.1.0")]
        [InlineData("Microsoft.CodeAnalysis.Common", "1.0.0", "Microsoft.CodeAnalysis.Common", "1.3.0")]
        [InlineData("Microsoft.CodeAnalysis.CSharp", "1.0.0", "Microsoft.CodeAnalysis.CSharp", "1.3.0")]
        [InlineData("Microsoft.CodeAnalysis.VisualBasic", "1.0.0", "Microsoft.CodeAnalysis.VisualBasic", "1.3.0")]
        [InlineData("Microsoft.CSharp", "1.0.0", "Microsoft.CSharp", "4.0.1")]
        [InlineData("Microsoft.VisualBasic", "1.0.0", "Microsoft.VisualBasic", "10.0.1")]
        [InlineData("Microsoft.Win32.Primitives", "1.0.0", "Microsoft.Win32.Primitives", "4.0.1")]
        [InlineData("Microsoft.Win32.Registry", "1.0.0", "Microsoft.Win32.Registry", "4.0.0")]
        [InlineData("System.IO.FileSystem", "1.0.0", "System.IO.FileSystem", "4.0.1")]
        [InlineData("System.IO.FileSystem.Primitives", "1.0.0", "System.IO.FileSystem.Primitives", "4.0.1")]
        [InlineData("System.IO.FileSystem.Watcher", "1.0.0", "System.IO.FileSystem.Watcher", "4.0.0")]
        public void ItUpliftsMicrosoftNETCoreAppPackages(
            string sourcePackageName,
            string sourceVersion,
            string targetPackageName,
            string targetVersion)
        {
            ValidatePackageMigration(sourcePackageName, sourceVersion, targetPackageName, targetVersion);
        }

        [Theory]
        [InlineData("Microsoft.Extensions.Logging", "1.0.0", "Microsoft.Extensions.Logging", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.Extensions.Logging.Console", "1.0.0", "Microsoft.Extensions.Logging.Console", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.Extensions.Logging.Debug", "1.0.0", "Microsoft.Extensions.Logging.Debug", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.Extensions.Configuration.Json", "1.0.0", "Microsoft.Extensions.Configuration.Json", ConstantPackageVersions.AspNet101PackagesVersion)]
        [InlineData("Microsoft.Extensions.Configuration.UserSecrets", "1.0.0", "Microsoft.Extensions.Configuration.UserSecrets", ConstantPackageVersions.AspNet101PackagesVersion)]
        public void ItUpliftsMicrosoftExtensionsPackages(
            string sourcePackageName,
            string sourceVersion,
            string targetPackageName,
            string targetVersion)
        {
            ValidatePackageMigration(sourcePackageName, sourceVersion, targetPackageName, targetVersion);
        }

        private void ValidatePackageMigration(
            string sourcePackageName,
            string sourceVersion,
            string targetPackageName,
            string targetVersion)
        {
            var mockProj = RunPackageDependenciesRuleOnPj("{ \"dependencies\": { \"" + sourcePackageName + "\" : { \"version\": \"" + sourceVersion + "\", \"type\": \"build\" } } }");

            var packageRef = mockProj.Items.First(i => i.Include == targetPackageName && i.ItemType == "PackageReference");

            packageRef.GetMetadataWithName("Version").Value.Should().Be(targetVersion);
        }
    }
}