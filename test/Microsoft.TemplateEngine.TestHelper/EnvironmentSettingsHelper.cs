// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class EnvironmentSettingsHelper : IDisposable
    {
        private readonly List<string> _foldersToCleanup = new List<string>();
        private readonly List<EngineEnvironmentSettings> _engineEnvironmentToDispose = new List<EngineEnvironmentSettings>();

        public IEngineEnvironmentSettings CreateEnvironment(
            string? locale = null,
            bool virtualize = false,
            [CallerMemberName] string hostIdentifier = "",
            bool loadDefaultGenerator = true)
        {
            if (string.IsNullOrEmpty(locale))
            {
                locale = "en-US";
            }
            List<Assembly> builtIns = new List<Assembly>();
            if (loadDefaultGenerator)
            {
                builtIns.Add(typeof(Orchestrator.RunnableProjects.Abstractions.IMacro).Assembly);
            }

            ITemplateEngineHost host = new TestHost(hostIdentifier: string.IsNullOrWhiteSpace(hostIdentifier) ? "TestRunner" : hostIdentifier)
            {
                BuiltInComponents = new AssemblyComponentCatalog(builtIns),
                FileSystem = new MonitoredFileSystem(new PhysicalFileSystem()),
                FallbackHostTemplateConfigNames = new[] { "dotnetcli" }
            };
            CultureInfo.CurrentUICulture = new CultureInfo(locale);
            EngineEnvironmentSettings engineEnvironmentSettings;
            if (virtualize)
            {
                engineEnvironmentSettings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            }
            else
            {
                var templateEngineRoot = Path.Combine(CreateTemporaryFolder(), ".templateengine");
                engineEnvironmentSettings = new EngineEnvironmentSettings(host, settingsLocation: templateEngineRoot);
            }
            _engineEnvironmentToDispose.Add(engineEnvironmentSettings);
            return engineEnvironmentSettings;
        }

        public string CreateTemporaryFolder(string name = "")
        {
            string folder = TestUtils.CreateTemporaryFolder(name);
            _foldersToCleanup.Add(folder);
            return folder;
        }

        public void Dispose()
        {
            _engineEnvironmentToDispose.ForEach(e => e.Dispose());
            _foldersToCleanup.ForEach(f => Directory.Delete(f, true));
        }
    }
}
