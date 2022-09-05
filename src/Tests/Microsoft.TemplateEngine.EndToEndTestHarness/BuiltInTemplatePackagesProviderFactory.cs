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
        private string testTemplatesRoot;
        public string DisplayName => "E2E Harness BuiltIn";

        public Guid Id { get; } = new Guid("{3227D09D-C1EA-48F1-A33B-1F132BFD9F00}");

        public BuiltInTemplatePackagesProviderFactory(string testAssetsRoot)
        {
            testTemplatesRoot = testAssetsRoot;
        }

        public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
        {
            string[] locations = new[]
            {
                Path.Combine(testTemplatesRoot, "test_templates")
            };
            return new DefaultTemplatePackageProvider(this, settings, null, locations);
        }
    }
}
