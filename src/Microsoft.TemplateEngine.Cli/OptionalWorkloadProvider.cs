using Microsoft.DotNet.TemplateLocator;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Edge;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Utils
{
    internal partial class OptionalWorkloadProviderFactory
    {
        class OptionalWorkloadProvider : ITemplatePackageProvider
        {
            private IEngineEnvironmentSettings EnvironmentSettings;

            public OptionalWorkloadProvider(ITemplatePackageProviderFactory factory, IEngineEnvironmentSettings settings)
            {
                this.Factory = factory;
                this.EnvironmentSettings = settings;
            }

            public ITemplatePackageProviderFactory Factory { get; }

            event Action ITemplatePackageProvider.TemplatePackagesChanged
            {
                add { }
                remove { }
            }

            public Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync(CancellationToken cancellationToken)
            {
                string sdkVersion = EnvironmentSettings.Host.Version.Substring(1); // Host.Version (from SDK) has a leading "v" that need to remove.
                try
                {
                    var list = new List<TemplatePackage>();
                    var _paths = new Paths(EnvironmentSettings);
                    TemplateLocator optionalWorkloadLocator = new TemplateLocator();
                    string dotnetPath = Path.GetDirectoryName(Path.GetDirectoryName(_paths.Global.BaseDir));

                    var packages = optionalWorkloadLocator.GetDotnetSdkTemplatePackages(sdkVersion, dotnetPath);
                    var fileSystem = EnvironmentSettings.Host.FileSystem as IFileLastWriteTimeSource;
                    foreach (IOptionalSdkTemplatePackageInfo packageInfo in packages)
                    {
                        list.Add(new TemplatePackage(this, packageInfo.Path, fileSystem?.GetLastWriteTimeUtc(packageInfo.Path) ?? File.GetLastWriteTime(packageInfo.Path)));
                    }
                    return Task.FromResult<IReadOnlyList<ITemplatePackage>>(list);
                }
                catch (Exception ex)
                {
                    throw new HiveSynchronizationException(LocalizableStrings.OptionalWorkloadsSyncFailed, sdkVersion, ex);
                }
            }
        }
    }
}
