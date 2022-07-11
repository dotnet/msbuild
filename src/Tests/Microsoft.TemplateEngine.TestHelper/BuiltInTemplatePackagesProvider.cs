// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class BuiltInTemplatePackagesProviderFactory : ITemplatePackageProviderFactory
    {
        public static List<(Type, IIdentifiedComponent)> GetComponents(bool includeTestTemplates = true)
        {
            return new() { (typeof(ITemplatePackageProviderFactory), new BuiltInTemplatePackagesProviderFactory(includeTestTemplates)) };
        }

        public static readonly Guid FactoryId = new Guid("{B9EE7CC5-D3AD-4982-94A4-CDF9E1C7FFCA}");
        private readonly bool _includeTestTemplates;

        public string DisplayName => "new3 BuiltIn";

        public Guid Id => FactoryId;

        private BuiltInTemplatePackagesProviderFactory(bool includeTestTemplates = true)
        {
            _includeTestTemplates = includeTestTemplates;
        }

        public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
        {
            return new BuiltInTemplatePackagesProvider(this, settings, _includeTestTemplates);
        }

        private class BuiltInTemplatePackagesProvider : ITemplatePackageProvider
        {
            private readonly IEngineEnvironmentSettings _settings;
            private readonly bool _includeTestTemplates;

            public BuiltInTemplatePackagesProvider(BuiltInTemplatePackagesProviderFactory factory, IEngineEnvironmentSettings settings, bool includeTestTemplates = true)
            {
                _settings = settings;
                _includeTestTemplates = includeTestTemplates;
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
                List<string> locations = new List<string>()
                {
                    Path.Combine(repoRoot, "template_feed"),
                };

                if (_includeTestTemplates)
                {
                    locations.Add(Path.Combine(repoRoot, "test", "Microsoft.TemplateEngine.TestTemplates", "test_templates"));
                }

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
