// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    internal static class CliTestHostFactory
    {
        public static ICliTemplateEngineHost GetVirtualHost(
                [CallerMemberName] string hostIdentifier = "",
                IEnvironment? environment = null,
                IReadOnlyList<(Type, IIdentifiedComponent)>? additionalComponents = null,
                IReadOnlyDictionary<string, string>? defaultParameters = null)
        {
            CliTestHost host = new(hostIdentifier: hostIdentifier, additionalComponents: additionalComponents);
            environment ??= new DefaultEnvironment();

            if (defaultParameters != null)
            {
                foreach (KeyValuePair<string, string> parameter in defaultParameters)
                {
                    host.HostParamDefaults[parameter.Key] = parameter.Value;
                }
            }
            ((ITemplateEngineHost)host).VirtualizeDirectory(new DefaultPathInfo(environment, host).GlobalSettingsDir);
            return host;
        }

        private class CliTestHost : ICliTemplateEngineHost
        {
            private readonly ILoggerFactory _loggerFactory;
            private readonly ILogger _logger;
            private readonly string _hostIdentifier;
            private readonly string _version;
            private readonly IReadOnlyList<(Type, IIdentifiedComponent)> _builtIns;
            private readonly IReadOnlyList<string> _fallbackNames;

            internal CliTestHost(
                [CallerMemberName] string hostIdentifier = "",
                string version = "1.0.0",
                bool loadDefaultGenerator = true,
                IReadOnlyList<(Type, IIdentifiedComponent)>? additionalComponents = null,
                IPhysicalFileSystem? fileSystem = null,
                IReadOnlyList<string>? fallbackNames = null,
                IEnumerable<ILoggerProvider>? addLoggerProviders = null)
            {
                _hostIdentifier = string.IsNullOrWhiteSpace(hostIdentifier) ? "TestRunner" : hostIdentifier;
                _version = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version;

                var builtIns = new List<(Type, IIdentifiedComponent)>();
                if (additionalComponents != null)
                {
                    builtIns.AddRange(additionalComponents);
                }
                builtIns.AddRange(Edge.Components.AllComponents);
                if (loadDefaultGenerator)
                {
                    builtIns.AddRange(Orchestrator.RunnableProjects.Components.AllComponents);
                }

                _builtIns = builtIns;
                HostParamDefaults = new Dictionary<string, string>();
                FileSystem = fileSystem ?? new PhysicalFileSystem();

                _loggerFactory = new TestLoggerFactory();
                addLoggerProviders?.ToList().ForEach(_loggerFactory.AddProvider);
                _logger = _loggerFactory.CreateLogger("Cli Test Host");
                _fallbackNames = fallbackNames ?? new[] { "dotnetcli" };
            }

            internal Dictionary<string, string> HostParamDefaults { get; set; } = new Dictionary<string, string>();

            public IPhysicalFileSystem FileSystem { get; private set; }

            string ITemplateEngineHost.HostIdentifier => _hostIdentifier;

            IReadOnlyList<string> ITemplateEngineHost.FallbackHostTemplateConfigNames => _fallbackNames;

            string ITemplateEngineHost.Version => _version;

            IReadOnlyList<(Type, IIdentifiedComponent)> ITemplateEngineHost.BuiltInComponents => _builtIns;

            ILogger ITemplateEngineHost.Logger => _logger;

            ILoggerFactory ITemplateEngineHost.LoggerFactory => _loggerFactory;

            public string OutputPath => FileSystem.GetCurrentDirectory();

            public bool IsCustomOutputPath => false;

            bool ITemplateEngineHost.TryGetHostParamDefault(string paramName, out string? value)
            {
                return HostParamDefaults.TryGetValue(paramName, out value);
            }

            void ITemplateEngineHost.VirtualizeDirectory(string path)
            {
                FileSystem = new InMemoryFileSystem(path, FileSystem);
            }

            public void Dispose()
            {
                _loggerFactory?.Dispose();
            }

            #region Obsolete methods
#pragma warning disable CA1041 // Provide ObsoleteAttribute message
            [Obsolete]
            void ITemplateEngineHost.OnSymbolUsed(string symbol, object value)
            {
                //do nothing
            }

            [Obsolete]
            bool ITemplateEngineHost.OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string newValue)
            {
                //do nothing
                newValue = receivedValue;
                return false;
            }

            [Obsolete]
            bool ITemplateEngineHost.OnNonCriticalError(string code, string message, string currentFile, long currentPosition)
            {
                //do nothing
                return false;
            }

            [Obsolete]
            void ITemplateEngineHost.OnCriticalError(string code, string message, string currentFile, long currentPosition)
            {
                //do nothing
            }

            [Obsolete]
            void ITemplateEngineHost.LogMessage(string message)
            {
                //do nothing
            }

            [Obsolete]
            bool ITemplateEngineHost.OnPotentiallyDestructiveChangesDetected(IReadOnlyList<IFileChange> changes, IReadOnlyList<IFileChange> destructiveChanges)
            {
                //do nothing
                return false;
            }

            [Obsolete]
            bool ITemplateEngineHost.OnConfirmPartialMatch(string name)
            {
                //do nothing
                return false;
            }

            [Obsolete]
            void ITemplateEngineHost.LogDiagnosticMessage(string message, string category, params string[] details)
            {
                //do nothing
            }

            [Obsolete]
            void ITemplateEngineHost.LogTiming(string label, TimeSpan duration, int depth)
            {
                //do nothing
            }
#pragma warning restore CA1041 // Provide ObsoleteAttribute message
            #endregion
        }
    }
}
