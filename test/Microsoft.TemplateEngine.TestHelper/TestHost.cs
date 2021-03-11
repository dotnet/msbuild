using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class TestHost : ITemplateEngineHost
    {
        public TestHost()
        {
            BuiltInComponents = new List<KeyValuePair<Guid, Func<Type>>>();
            HostParamDefaults = new Dictionary<string, string>();
            FileSystem = new PhysicalFileSystem();
        }

        public event Action<string, TimeSpan> TimingCompleted;

        public event Action<string, object> SymbolUsed;

        public delegate bool ParameterErrorHandler(ITemplateParameter parameter, string receivedValue, string message, out string newValue);

        public delegate bool NonCriticalErrorHandler(string code, string message, string currentFile, long currentPosition);

        public delegate void CriticalErrorHandler(string code, string message, string currentFile, long currentPosition);

        public event ParameterErrorHandler ParameterError;

        public event NonCriticalErrorHandler NonCriticalError;

        public event CriticalErrorHandler CriticalError;

        public event Action<string> MessageReceived;

        public Dictionary<string, string> HostParamDefaults { get; set; }

        public IPhysicalFileSystem FileSystem { get; set; }

        public string HostIdentifier { get; set; }

        public IReadOnlyList<string> FallbackHostTemplateConfigNames { get; set; }

        public string Version { get; set; }

        public IReadOnlyList<KeyValuePair<Guid, Func<Type>>> BuiltInComponents { get; set; }

        public bool TryGetHostParamDefault(string paramName, out string value)
        {
            return HostParamDefaults.TryGetValue(paramName, out value);
        }

        public void OnTimingCompleted(string label, TimeSpan timing)
        {
            TimingCompleted?.Invoke(label, timing);
        }

        public void OnSymbolUsed(string symbol, object value)
        {
            SymbolUsed?.Invoke(symbol, value);
        }

        public bool OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string newValue)
        {
            newValue = null;
            return ParameterError?.Invoke(parameter, receivedValue, message, out newValue) ?? false;
        }

        public bool OnNonCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            return NonCriticalError?.Invoke(code, message, currentFile, currentPosition) ?? false;
        }

        public void OnCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            CriticalError?.Invoke(code, message, currentFile, currentPosition);
        }

        public void LogMessage(string message)
        {
            MessageReceived?.Invoke(message);
        }

        public void VirtualizeDirectory(string path)
        {
            FileSystem = new InMemoryFileSystem(path, FileSystem);
        }

        public bool OnPotentiallyDestructiveChangesDetected(IReadOnlyList<IFileChange> changes, IReadOnlyList<IFileChange> destructiveChanges)
        {
            return true;
        }

        public bool OnConfirmPartialMatch(string name)
        {
            return true;
        }

        public void LogDiagnosticMessage(string message, string category, params string[] details)
        {
            Console.WriteLine("Diag: " + message + " (" + category + ")");
            foreach (string detail in details)
            {
                Console.WriteLine("      " + detail);
            }
        }

        public void LogTiming(string label, TimeSpan duration, int depth)
        {
            OnTimingCompleted(label, duration);
        }
    }
}
