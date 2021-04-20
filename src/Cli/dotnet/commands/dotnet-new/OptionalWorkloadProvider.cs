using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TemplateLocator;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Utils
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
            var optionalWorkloadLocator = new TemplateLocator();
            var sdkDirectory = Path.GetDirectoryName(typeof(DotnetFiles).Assembly.Location);
            var sdkVersion = Path.GetFileName(sdkDirectory);
            var dotnetRootPath = Path.GetDirectoryName(Path.GetDirectoryName(sdkDirectory));

            var packages = optionalWorkloadLocator.GetDotnetSdkTemplatePackages(sdkVersion, dotnetRootPath);
            var fileSystem = _environmentSettings.Host.FileSystem as IFileLastWriteTimeSource;
            foreach (IOptionalSdkTemplatePackageInfo packageInfo in packages)
            {
                list.Add(new TemplatePackage(this, packageInfo.Path, fileSystem?.GetLastWriteTimeUtc(packageInfo.Path) ?? File.GetLastWriteTime(packageInfo.Path)));
            }
            return Task.FromResult<IReadOnlyList<ITemplatePackage>>(list);
        }
    }
}
