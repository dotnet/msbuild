using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace dotnet_new3
{
    // A (probably) temporary implementation of ITemplateEngineHost, for testing
    public class DotNetNew3TemplateEngineHost : ITemplateEngineHost
    {
        private IReadOnlyDictionary<string, string> _HostDefaults { get; }

        public DotNetNew3TemplateEngineHost(string locale)
        {
            Locale = locale;
            _HostDefaults = new Dictionary<string, string>();
        }

        public DotNetNew3TemplateEngineHost(string locale, Dictionary<string, string> defaults)
        {
            Locale = locale;
            _HostDefaults = defaults;
        }

        public string Locale { get; private set; }

        public void LogMessage(string message)
        {
            Console.WriteLine("LogMessage: {0}", message);
        }

        public void OnCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            throw new NotImplementedException();
        }

        public bool OnNonCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            throw new NotImplementedException();
        }

        public bool OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string newValue)
        {
            Console.WriteLine("DotNetNew3TemplateEngineHost::OnParameterError() called");
            Console.WriteLine("\tError message: {0}", message);
            Console.WriteLine("Parameter name = {0}", parameter.Name);
            Console.WriteLine("Parameter value = {0}", receivedValue);
            Console.WriteLine("Enter a new value for the param, or:");
            newValue = Console.ReadLine();
            return ! string.IsNullOrEmpty(newValue);
        }

        public void OnSymbolUsed(string symbol, object value)
        {
            throw new NotImplementedException();
        }

        public void OnTimingCompleted(string label, TimeSpan timing)
        {
            LogMessage(string.Format("{0}: {1} ms", label, timing.TotalMilliseconds));
        }

        // stub that will be built out soon.
        public bool TryGetHostParamDefault(string paramName, out string value)
        {
            value = null;
            return false;
        }
    }
}
