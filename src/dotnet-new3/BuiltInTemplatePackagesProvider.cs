using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace dotnet_new3
{
    /// <summary>
    /// Factory responsible for adding "console", "library" and other templates contained in "template_feed" to "dotnet new3".
    /// </summary>
    class BuiltInTemplatePackagesProviderFactory : ITemplatePackageProviderFactory
    {
        public string DisplayName => "new3 BuiltIn";

        public Guid Id { get; } = new Guid("{3227D09D-C1EA-48F1-A33B-1F132BFD9F06}");

        public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
        {
            return new BuiltInTemplatePackagesProvider(this, settings);
        }

        class BuiltInTemplatePackagesProvider : ITemplatePackageProvider
        {
            private readonly IEngineEnvironmentSettings _settings;

            public BuiltInTemplatePackagesProvider(BuiltInTemplatePackagesProviderFactory factory, IEngineEnvironmentSettings settings)
            {
                _settings = settings;
                Factory = factory;
            }

            public ITemplatePackageProviderFactory Factory { get; }

            event Action ITemplatePackageProvider.TemplatePackagesChanged
            {
                add { }
                remove { }
            }

            public Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync(CancellationToken cancellationToken)
            {
                List<ITemplatePackage> templatePackages = new List<ITemplatePackage>();

                string dn3Path = _settings.Environment.GetEnvironmentVariable("DN3");
                if (string.IsNullOrEmpty(dn3Path))
                {
                    string path = typeof(Program).Assembly.Location;
                    while (path != null && !File.Exists(Path.Combine(path, "Microsoft.TemplateEngine.sln")))
                    {
                        path = Path.GetDirectoryName(path);
                    }
                    if (path == null)
                    {
                        _settings.Host.LogDiagnosticMessage("Couldn't the setup package location, because \"Microsoft.TemplateEngine.sln\" is not in any of parent directories.", "Install");
                        return Task.FromResult((IReadOnlyList<ITemplatePackage>)templatePackages);
                    }
                    Environment.SetEnvironmentVariable("DN3", path);
                }

                Paths paths = new Paths(_settings);

                if (paths.FileExists(paths.Global.DefaultInstallTemplateList))
                {
                    IFileLastWriteTimeSource fileSystem = _settings.Host.FileSystem as IFileLastWriteTimeSource;
                    foreach (string sourceLocation in paths.ReadAllText(paths.Global.DefaultInstallTemplateList).Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string expandedPath = Environment.ExpandEnvironmentVariables(sourceLocation).Replace('\\', Path.DirectorySeparatorChar);
                        IEnumerable<string> expandedPaths = InstallRequestPathResolution.ExpandMaskedPath(expandedPath, _settings);
                        templatePackages.AddRange(expandedPaths.Select(path => new TemplatePackage(this, path, fileSystem?.GetLastWriteTimeUtc(path) ?? File.GetLastWriteTime(path))));
                    }
                }

                return Task.FromResult((IReadOnlyList<ITemplatePackage>)templatePackages);
            }
        }
    }
}
