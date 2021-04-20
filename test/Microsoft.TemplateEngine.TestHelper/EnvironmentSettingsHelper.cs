// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class EnvironmentSettingsHelper : IDisposable
    {
        private List<string> foldersToCleanup = new List<string>();
        private List<EngineEnvironmentSettings> engineEnvironmentToDispose = new List<EngineEnvironmentSettings>();

        public IEngineEnvironmentSettings CreateEnvironment(string locale = null, bool virtualize = false)
        {
            if (string.IsNullOrEmpty(locale))
            {
                locale = "en-US";
            }

            ITemplateEngineHost host = new TestHost
            {
                HostIdentifier = "TestRunner",
                Version = "1.0.0.0",
                BuiltInComponents = new AssemblyComponentCatalog(new List<Assembly>()
                {
                    typeof(Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions.IMacro).Assembly,   //RunnableProject
                    typeof(SettingsLoader).Assembly   //Edge
                }),
                FileSystem = new MonitoredFileSystem(new PhysicalFileSystem()),
                FallbackHostTemplateConfigNames = new[] { "dotnetcli" }
            };
            CultureInfo.CurrentUICulture = new CultureInfo(locale);
            EngineEnvironmentSettings engineEnvironmentSettings;
            if (virtualize)
            {
                engineEnvironmentSettings = new EngineEnvironmentSettings(host, (x) => new SettingsLoader(x));
                host.VirtualizeDirectory(engineEnvironmentSettings.Paths.TemplateEngineRootDir);
            }
            else
            {
                var tempateEngineRoot = Path.Combine(CreateTemporaryFolder(), ".templateengine");
                engineEnvironmentSettings = new EngineEnvironmentSettings(host, (x) => new SettingsLoader(x), tempateEngineRoot);
            }
            engineEnvironmentToDispose.Add(engineEnvironmentSettings);
            return engineEnvironmentSettings;
        }

        public string CreateTemporaryFolder(string name = "")
        {
            string folder = TestUtils.CreateTemporaryFolder(name);
            foldersToCleanup.Add(folder);
            return folder;
        }

        public void Dispose()
        {
            engineEnvironmentToDispose.ForEach(e => e.Dispose());
            foldersToCleanup.ForEach(f => Directory.Delete(f, true));
        }
    }
}
