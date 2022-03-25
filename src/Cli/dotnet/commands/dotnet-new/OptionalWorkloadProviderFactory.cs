using Microsoft.DotNet.TemplateLocator;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using System;

namespace Microsoft.DotNet.Tools.New
{
    /// <summary>
    /// TemplateEngine calls this when it wants to gather list of installed template packages.
    /// This provider is responsible for gathering option workload packages via <see cref="TemplateLocator"/>.
    /// </summary>
    internal class OptionalWorkloadProviderFactory : ITemplatePackageProviderFactory
    {
        public static readonly Guid FactoryId = new Guid("{FAE2BB7C-054D-481B-B75C-E9F524193D56}");

        public Guid Id => FactoryId;

        public string DisplayName => "Optional workloads";

        public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
        {
            return new OptionalWorkloadProvider(this, settings);
        }
    }
}
