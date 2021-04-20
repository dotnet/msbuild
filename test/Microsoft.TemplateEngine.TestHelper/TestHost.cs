// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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
        }

        public delegate bool ParameterErrorHandler(ITemplateParameter parameter, string receivedValue, string message, out string newValue);

        public delegate bool NonCriticalErrorHandler(string code, string message, string currentFile, long currentPosition);

        public delegate void CriticalErrorHandler(string code, string message, string currentFile, long currentPosition);

        public event Action<string, TimeSpan>? TimingCompleted;

        public event Action<string, object>? SymbolUsed;

        public event ParameterErrorHandler? ParameterError;

        public event NonCriticalErrorHandler? NonCriticalError;

        public event CriticalErrorHandler? CriticalError;

        public event Action<string>? MessageReceived;

        public Dictionary<string, string> HostParamDefaults { get; set; }

        public IPhysicalFileSystem FileSystem { get; set; }

        public string HostIdentifier { get; }

        public IReadOnlyList<string>? FallbackHostTemplateConfigNames { get; set; }

        public string Version { get; }

        public IReadOnlyList<KeyValuePair<Guid, Func<Type>>> BuiltInComponents { get; set; }

        public bool TryGetHostParamDefault(string paramName, out string value)
        {
            return HostParamDefaults.TryGetValue(paramName, out value);
        }

        public void OnTimingCompleted(string label, TimeSpan timing)
        {
            Console.WriteLine($"[{HostIdentifier}][{nameof(OnTimingCompleted)}][{label}]: completed in {timing.ToString()}");
            TimingCompleted?.Invoke(label, timing);
        }

        public void OnSymbolUsed(string symbol, object value)
        {
            Console.WriteLine($"[{HostIdentifier}][{nameof(OnSymbolUsed)}] symbol {symbol} with value: {value}");
            SymbolUsed?.Invoke(symbol, value);
        }

        public bool OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string? newValue)
        {
            newValue = null;
            Console.WriteLine($"[{HostIdentifier}][{nameof(OnParameterError)}] {message}; parameter '{parameter.Name}', received value '{receivedValue}'");
            return ParameterError?.Invoke(parameter, receivedValue, message, out newValue) ?? false;
        }

        public bool OnNonCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            Console.WriteLine($"[{HostIdentifier}][{nameof(OnNonCriticalError)}] {message} (code: {code}, current file: {currentFile}, currentPosition: {currentPosition})");
            return NonCriticalError?.Invoke(code, message, currentFile, currentPosition) ?? false;
        }

        public void OnCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            Console.WriteLine($"[{HostIdentifier}][{nameof(OnCriticalError)}] {message} (code: {code}, current file: {currentFile}, currentPosition: {currentPosition})");
            CriticalError?.Invoke(code, message, currentFile, currentPosition);
        }

        public void LogMessage(string message)
        {
            Console.WriteLine($"[{HostIdentifier}][{nameof(LogMessage)}] {message}");
            MessageReceived?.Invoke(message);
        }

        public void VirtualizeDirectory(string path)
        {
            FileSystem = new InMemoryFileSystem(path, FileSystem);
        }

        public bool OnPotentiallyDestructiveChangesDetected(IReadOnlyList<IFileChange> changes, IReadOnlyList<IFileChange> destructiveChanges)
        {
            var sb = new StringBuilder();
            sb.Append("Changes: ").AppendLine(string.Join("|", changes.Select(change => $"{change.ChangeKind} in {change.TargetRelativePath}")));
            sb.Append("Destructive changes: ").AppendLine(string.Join("|", destructiveChanges.Select(change => $"{change.ChangeKind} in {change.TargetRelativePath}")));

            Console.WriteLine($"[{HostIdentifier}][{nameof(OnPotentiallyDestructiveChangesDetected)}] {sb.ToString()}");
            return true;
        }

        public bool OnConfirmPartialMatch(string name)
        {
            Console.WriteLine($"[{HostIdentifier}][{nameof(OnConfirmPartialMatch)}] {name}");
            return true;
        }

        public void LogDiagnosticMessage(string message, string category, params string[] details)
        {
            Console.WriteLine($"[{HostIdentifier}][{nameof(LogDiagnosticMessage)}][{category}] {message} (details: {string.Join(";", details)})");
        }

        public void LogTiming(string label, TimeSpan duration, int depth)
        {
            OnTimingCompleted(label, duration);
        }
    }
}
