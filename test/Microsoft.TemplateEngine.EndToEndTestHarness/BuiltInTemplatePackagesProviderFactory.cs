// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.EndToEndTestHarness
{
    internal class BuiltInTemplatePackagesProviderFactory : ITemplatePackageProviderFactory
    {
        public string DisplayName => "E2E Harness BuiltIn";

        public Guid Id { get; } = new Guid("{3227D09D-C1EA-48F1-A33B-1F132BFD9F00}");

        public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
        {
            string codebase = typeof(Program).GetTypeInfo().Assembly.Location;
            Uri cb = new Uri(codebase);
            string asmPath = cb.LocalPath;
            string dir = Path.GetDirectoryName(asmPath);
            string[] locations = new[]
            {
                Path.Combine(dir, "..", "..", "..", "..", "..", "template_feed"),
                Path.Combine(dir, "..", "..", "..", "..", "..", "test", "Microsoft.TemplateEngine.TestTemplates", "test_templates")
            };
            return new DefaultTemplatePackageProvider(this, settings, null, locations);
        }
    }
}
