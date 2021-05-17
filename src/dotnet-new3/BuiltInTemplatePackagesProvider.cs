// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Utils;

namespace Dotnet_new3
{
    /// <summary>
    /// Factory responsible for adding "console", "library" and other templates contained in "template_feed" to "dotnet new3".
    /// </summary>
    internal class BuiltInTemplatePackagesProviderFactory : ITemplatePackageProviderFactory
    {
        public static readonly Guid FactoryId = new Guid("{3227D09D-C1EA-48F1-A33B-1F132BFD9F06}");

        public string DisplayName => "new3 BuiltIn";

        public Guid Id => FactoryId;

        public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
        {
            return new BuiltInTemplatePackagesProvider(this, settings);
        }

        private class BuiltInTemplatePackagesProvider : ITemplatePackageProvider
        {
            private readonly IEngineEnvironmentSettings _settings;

            public BuiltInTemplatePackagesProvider(BuiltInTemplatePackagesProviderFactory factory, IEngineEnvironmentSettings settings)
            {
                _settings = settings;
                Factory = factory;
            }

#pragma warning disable CS0067

            public event Action TemplatePackagesChanged;

#pragma warning restore CS0067

            public ITemplatePackageProviderFactory Factory { get; }

            public Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync(CancellationToken cancellationToken)
            {
                List<ITemplatePackage> templatePackages = new List<ITemplatePackage>();
                string assemblyLocation = Path.GetDirectoryName(typeof(Program).Assembly.Location);
                string dn3Path = _settings.Environment.GetEnvironmentVariable("DN3");

                if (string.IsNullOrEmpty(dn3Path))
                {
                    string path = assemblyLocation;
                    while (path != null && !File.Exists(Path.Combine(path, "Microsoft.TemplateEngine.sln")))
                    {
                        path = Path.GetDirectoryName(path);
                    }
                    if (path == null)
                    {
                        _settings.Host.Logger.LogDebug("Couldn't the setup package location, because \"Microsoft.TemplateEngine.sln\" is not in any of parent directories.");
                        return Task.FromResult((IReadOnlyList<ITemplatePackage>)templatePackages);
                    }
                    Environment.SetEnvironmentVariable("DN3", path);
                }

                string defaultPackagesFilePath = Path.Combine(assemblyLocation, "defaultinstall.template.list");
                if (_settings.Host.FileSystem.FileExists(defaultPackagesFilePath))
                {
                    IFileLastWriteTimeSource fileSystem = _settings.Host.FileSystem as IFileLastWriteTimeSource;
                    string packagesToInstall = _settings.Host.FileSystem.ReadAllText(defaultPackagesFilePath);
                    foreach (string sourceLocation in packagesToInstall.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
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
