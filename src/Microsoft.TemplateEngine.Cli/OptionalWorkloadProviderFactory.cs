using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge.Mount.Archive;
using System;

namespace Microsoft.TemplateEngine.Utils
{
    internal partial class OptionalWorkloadProviderFactory : ITemplatePackageProviderFactory
    {
        public static readonly Guid FactoryId = new Guid("{FAE2BB7C-054D-481B-B75C-E9F524193D56}");

        public Guid Id => FactoryId;

        public string DisplayName => "OptionalWorkloads";

        public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
        {
            return new OptionalWorkloadProvider(this, settings);
        }
    }
}
