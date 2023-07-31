// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class TelemetryEventEntry
    {
        public static event EventHandler<InstrumentationEventArgs> EntryPosted;
        public static ITelemetryFilter TelemetryFilter { get; set; } = new BlockFilter();

        public static void TrackEvent(
            string eventName = null,
            IDictionary<string, string> properties = null,
            IDictionary<string, double> measurements = null)
        {
            EntryPosted?.Invoke(typeof(TelemetryEventEntry),
                new InstrumentationEventArgs(eventName, properties, measurements));
        }

        public static void SendFiltered(object o = null)
        {
            if (o == null)
            {
                return;
            }

            foreach (ApplicationInsightsEntryFormat entry in TelemetryFilter.Filter(o))
            {
                TrackEvent(entry.EventName, entry.Properties, entry.Measurements);
            }
        }

        public static void Subscribe(Action<string,
            IDictionary<string, string>,
            IDictionary<string, double>> subscriber)
        {
            void Handler(object sender, InstrumentationEventArgs eventArgs)
            {
                subscriber(eventArgs.EventName, eventArgs.Properties, eventArgs.Measurements);
            }

            EntryPosted += Handler;
        }
    }

    public sealed class PerformanceMeasurement : IDisposable
    {
        private readonly Stopwatch _timer;
        private readonly Dictionary<string, double> _data;
        private readonly string _name;

        public PerformanceMeasurement(Dictionary<string, double> data, string name)
        {
            // Measurement is a no-op if we don't have a dictionary to store the entry.
            if (data == null)
            {
                return;
            }

            _data = data;
            _name = name;
            _timer = Stopwatch.StartNew();
        }

        public void Dispose() => _data?.Add(_name, _timer.Elapsed.TotalMilliseconds);
    }

    public class BlockFilter : ITelemetryFilter
    {
        public IEnumerable<ApplicationInsightsEntryFormat> Filter(object o)
        {
            return new List<ApplicationInsightsEntryFormat>();
        }
    }

    public class InstrumentationEventArgs : EventArgs
    {
        internal InstrumentationEventArgs(
            string eventName,
            IDictionary<string, string> properties,
            IDictionary<string, double> measurements)
        {
            EventName = eventName;
            Properties = properties;
            Measurements = measurements;
        }

        public string EventName { get; }
        public IDictionary<string, string> Properties { get; }
        public IDictionary<string, double> Measurements { get; }
    }

    public class ApplicationInsightsEntryFormat
    {
        public ApplicationInsightsEntryFormat(
            string eventName = null,
            IDictionary<string, string> properties = null,
            IDictionary<string, double> measurements = null)
        {
            EventName = eventName;
            Properties = properties;
            Measurements = measurements;
        }

        public string EventName { get; }
        public IDictionary<string, string> Properties { get; }
        public IDictionary<string, double> Measurements { get; }

        public ApplicationInsightsEntryFormat WithAppliedToPropertiesValue(Func<string, string> func)
        {
            var appliedProperties = Properties.ToDictionary(p => p.Key, p => func(p.Value));
            return new ApplicationInsightsEntryFormat(EventName, appliedProperties, Measurements);
        }
    }
}
