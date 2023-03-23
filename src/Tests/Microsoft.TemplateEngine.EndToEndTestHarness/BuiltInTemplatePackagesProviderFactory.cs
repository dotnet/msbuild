// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.EndToEndTestHarness
{
    internal class BuiltInTemplatePackagesProviderFactory : ITemplatePackageProviderFactory
    {
        private string testTemplatesRoot;

        public BuiltInTemplatePackagesProviderFactory(string testAssetsRoot)
        {
            testTemplatesRoot = testAssetsRoot;
        }

        public string DisplayName => "E2E Harness BuiltIn";

        public Guid Id { get; } = new Guid("{3227D09D-C1EA-48F1-A33B-1F132BFD9F00}");

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
