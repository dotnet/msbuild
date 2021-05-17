// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.TestHelper
{
    internal class TestHost : ITemplateEngineHost
    {
        public TestHost([CallerMemberName] string hostIdentifier = "", string version = "1.0.0")
        {
            HostIdentifier = string.IsNullOrWhiteSpace(hostIdentifier) ? "TestRunner" : hostIdentifier;
            Version = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version;
            BuiltInComponents = new List<KeyValuePair<Guid, Func<Type>>>();
            HostParamDefaults = new Dictionary<string, string>();
            FileSystem = new PhysicalFileSystem();
            LoggerFactory =
                Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                    builder
                        .SetMinimumLevel(LogLevel.Trace)
                        .AddSimpleConsole(options =>
                        {
                            options.SingleLine = true;
                            options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";
                            options.IncludeScopes = true;
                        }));
            Logger = LoggerFactory.CreateLogger("Test Host");
        }

        public Dictionary<string, string> HostParamDefaults { get; set; }

        public IPhysicalFileSystem FileSystem { get; set; }

        public string HostIdentifier { get; }

        public IReadOnlyList<string> FallbackHostTemplateConfigNames { get; set; } = new List<string>();

        public string Version { get; }

        public IReadOnlyList<KeyValuePair<Guid, Func<Type>>> BuiltInComponents { get; set; }

        public ILogger Logger { get; private set; }

        public ILoggerFactory LoggerFactory { get; private set; }

        public bool TryGetHostParamDefault(string paramName, out string? value)
        {
            return HostParamDefaults.TryGetValue(paramName, out value);
        }

        public void VirtualizeDirectory(string path)
        {
            FileSystem = new InMemoryFileSystem(path, FileSystem);
        }

        [Obsolete]
        void ITemplateEngineHost.OnSymbolUsed(string symbol, object value)
        {
            //do nothing
        }

        [Obsolete]
        bool ITemplateEngineHost.OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string newValue)
        {
            //do nothing
            newValue = "";
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
    }
}
