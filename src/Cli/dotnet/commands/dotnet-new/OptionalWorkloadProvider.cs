// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Configurer;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.DotNet.Tools.New
{
    internal class OptionalWorkloadProvider : ITemplatePackageProvider
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;

        internal OptionalWorkloadProvider(ITemplatePackageProviderFactory factory, IEngineEnvironmentSettings settings)
        {
            this.Factory = factory;
            this._environmentSettings = settings;
        }

        public ITemplatePackageProviderFactory Factory { get; }

        // To avoid warnings about unused, its implemented via add/remove
        event Action ITemplatePackageProvider.TemplatePackagesChanged
        {
            add { }
            remove { }
        }

        public Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync(CancellationToken cancellationToken)
        {
            var list = new List<TemplatePackage>();
            var optionalWorkloadLocator = new TemplateLocator.TemplateLocator();
            var sdkDirectory = Path.GetDirectoryName(typeof(DotnetFiles).Assembly.Location);
            var sdkVersion = Path.GetFileName(sdkDirectory);
            var dotnetRootPath = Path.GetDirectoryName(Path.GetDirectoryName(sdkDirectory));
            string userProfileDir = CliFolderPathCalculator.DotnetUserProfileFolderPath;

            var packages = optionalWorkloadLocator.GetDotnetSdkTemplatePackages(sdkVersion, dotnetRootPath, userProfileDir);
            var fileSystem = _environmentSettings.Host.FileSystem;
            foreach (var packageInfo in packages)
            {
                list.Add(new TemplatePackage(this, packageInfo.Path, fileSystem.GetLastWriteTimeUtc(packageInfo.Path)));
            }
            return Task.FromResult<IReadOnlyList<ITemplatePackage>>(list);
        }
    }
}
