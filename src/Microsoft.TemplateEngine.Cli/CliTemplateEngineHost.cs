// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Cli.CommandParsing;

namespace Microsoft.TemplateEngine.Cli
{
    internal class CliTemplateEngineHost : ITemplateEngineHost
    {
        private readonly ITemplateEngineHost _baseHost;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private INewCommandInput _commandInput;

        internal CliTemplateEngineHost(ITemplateEngineHost baseHost, INewCommandInput commandInput)
        {
            _baseHost = baseHost;
            _commandInput = commandInput;

            bool enableVerboseLogging = bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE"), out bool value) && value;
            _loggerFactory =
                Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                    builder
                        .SetMinimumLevel(enableVerboseLogging ? LogLevel.Trace : LogLevel.Information)
                        .AddConsole(config => config.FormatterName = nameof(CliConsoleFormatter))
                        .AddConsoleFormatter<CliConsoleFormatter, ConsoleFormatterOptions>(config =>
                        {
                            config.IncludeScopes = true;
                            config.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
                        }));
            _logger = _loggerFactory.CreateLogger<CliTemplateEngineHost>();
        }

        public IPhysicalFileSystem FileSystem => _baseHost.FileSystem;

        public string HostIdentifier => _baseHost.HostIdentifier;

        public IReadOnlyList<string> FallbackHostTemplateConfigNames => _baseHost.FallbackHostTemplateConfigNames;

        public string Version => _baseHost.Version;

        public virtual IReadOnlyList<(Type, IIdentifiedComponent)> BuiltInComponents => _baseHost.BuiltInComponents;

        public ILogger Logger => _logger;

        public ILoggerFactory LoggerFactory => _loggerFactory;

        private bool GlobalJsonFileExistsInPath
        {
            get
            {
                const string fileName = "global.json";

                string? workingPath = Path.Combine(FileSystem.GetCurrentDirectory(), _commandInput.OutputPath);
                bool found = false;

                do
                {
                    string checkPath = Path.Combine(workingPath, fileName);
                    found = FileSystem.FileExists(checkPath);
                    if (!found)
                    {
                        workingPath = Path.GetDirectoryName(workingPath.TrimEnd('/', '\\'));

                        if (string.IsNullOrWhiteSpace(workingPath) || !FileSystem.DirectoryExists(workingPath))
                        {
                            workingPath = null;
                        }
                    }
                }
                while (!found && (workingPath != null));

                return found;
            }
        }

        public virtual bool TryGetHostParamDefault(string paramName, out string? value)
        {
            switch (paramName)
            {
                case "GlobalJsonExists":
                    value = GlobalJsonFileExistsInPath.ToString();
                    return true;
                default:
                    return _baseHost.TryGetHostParamDefault(paramName, out value);
            }
        }

        public void VirtualizeDirectory(string path)
        {
            _baseHost.VirtualizeDirectory(path);
        }

        internal void ResetCommand(INewCommandInput commandInput)
        {
            _commandInput = commandInput ?? throw new ArgumentNullException(nameof(commandInput));
        }

        #region Obsolete
        [Obsolete]
#pragma warning disable SA1202 // Elements should be ordered by access
        void ITemplateEngineHost.LogMessage(string message)
#pragma warning restore SA1202 // Elements should be ordered by access
        {
            //do nothing
        }

        [Obsolete]
        void ITemplateEngineHost.OnCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            //do nothing
        }

        [Obsolete]
        bool ITemplateEngineHost.OnNonCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            //do nothing
            return false;
        }

        [Obsolete]
        bool ITemplateEngineHost.OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string newValue)
        {
            //do nothing
            newValue = "";
            return false;
        }

        [Obsolete]
        void ITemplateEngineHost.OnSymbolUsed(string symbol, object value)
        {
            //do nothing
        }

        [Obsolete]
        bool ITemplateEngineHost.OnConfirmPartialMatch(string name)
        {
            return true;
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

        [Obsolete]
        bool ITemplateEngineHost.OnPotentiallyDestructiveChangesDetected(IReadOnlyList<IFileChange> changes, IReadOnlyList<IFileChange> destructiveChanges)
        {
            //do nothing - CreationStatusResult is handled instead in TemplateInvoker
            return false;
        }
        #endregion
    }
}
