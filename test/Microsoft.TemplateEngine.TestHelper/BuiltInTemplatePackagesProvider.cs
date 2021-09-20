// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class BuiltInTemplatePackagesProviderFactory : ITemplatePackageProviderFactory
    {
        public static List<(Type, IIdentifiedComponent)> Components { get; } = new() { (typeof(ITemplatePackageProviderFactory), new BuiltInTemplatePackagesProviderFactory()) };

        public static readonly Guid FactoryId = new Guid("{B9EE7CC5-D3AD-4982-94A4-CDF9E1C7FFCA}");

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

            public event Action? TemplatePackagesChanged;

#pragma warning restore CS0067

            public ITemplatePackageProviderFactory Factory { get; }

            public Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync(CancellationToken cancellationToken)
            {
                List<ITemplatePackage> templatePackages = new List<ITemplatePackage>();

                List<ITemplatePackage> toInstallList = new List<ITemplatePackage>();
                var repoRoot = Path.GetDirectoryName(typeof(BuiltInTemplatePackagesProviderFactory).Assembly.Location);
                while (repoRoot != null && !File.Exists(Path.Combine(repoRoot, "Microsoft.TemplateEngine.sln")))
                {
                    repoRoot = Path.GetDirectoryName(repoRoot);
                }
                if (repoRoot == null)
                {
                    _settings.Host.Logger.LogDebug("Couldn't the setup package location, because \"Microsoft.TemplateEngine.sln\" is not in any of parent directories.");
                    return Task.FromResult((IReadOnlyList<ITemplatePackage>)templatePackages);
                }
                string[] locations = new[]
                {
                    Path.Combine(repoRoot, "template_feed"),
                    Path.Combine(repoRoot, "test", "Microsoft.TemplateEngine.TestTemplates", "test_templates")
                };

                foreach (string location in locations)
                {
                    if (Directory.Exists(location))
                    {
                        toInstallList.Add(new TemplatePackage(this, new DirectoryInfo(location).FullName, File.GetLastWriteTime(location)));
                    }
                }
                return Task.FromResult((IReadOnlyList<ITemplatePackage>)toInstallList);
            }
        }
    }
}
