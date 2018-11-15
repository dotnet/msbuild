// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Configurer;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.MSBuild
{
    public sealed class MSBuildLogger : INodeLogger
    {
        private readonly IFirstTimeUseNoticeSentinel _sentinel =
            new FirstTimeUseNoticeSentinel();
        private readonly ITelemetry _telemetry;
        private const string NewEventName = "msbuild";
        internal const string TargetFrameworkTelemetryEventName = "targetframeworkeval";

        internal const string TargetFrameworkVersionTelemetryPropertyKey = "TargetFrameworkVersion";
        internal const string UseWindowsFormsTelemetryPropertyKey = "UseWindowsForms";
        internal const string UseWPFTelemetryPropertyKey = "UseWPF";
        internal const string StringToRepresentPropertyNotSet = "null";

        public MSBuildLogger()
        {
            try
            {
                string sessionId =
                    Environment.GetEnvironmentVariable(MSBuildForwardingApp.TelemetrySessionIdEnvironmentVariableName);

                if (sessionId != null)
                {
                    _telemetry = new Telemetry(_sentinel, sessionId);
                }
            }
            catch (Exception)
            {
                // Exceptions during telemetry shouldn't cause anything else to fail
            }
        }

        public void Initialize(IEventSource eventSource, int nodeCount)
        {
            Initialize(eventSource);
        }

        public void Initialize(IEventSource eventSource)
        {
            try
            {
                if (_telemetry != null && _telemetry.Enabled)
                {
                    if (eventSource is IEventSource2 eventSource2)
                    {
                        eventSource2.TelemetryLogged += OnTelemetryLogged;
                    }
                }
            }
            catch (Exception)
            {
                // Exceptions during telemetry shouldn't cause anything else to fail
            }
        }

        internal static void FormatAndSend(ITelemetry telemetry, TelemetryEventArgs args)
        {
            if (args.EventName == TargetFrameworkTelemetryEventName)
            {
                var newEventName = $"msbuild/{TargetFrameworkTelemetryEventName}";
                Dictionary<string, string> maskedProperties = new Dictionary<string, string>();
                if (args.Properties.TryGetValue(TargetFrameworkVersionTelemetryPropertyKey, out string targetFrameworkVersionValue))
                {
                    maskedProperties.Add(TargetFrameworkVersionTelemetryPropertyKey, Sha256Hasher.HashWithNormalizedCasing(targetFrameworkVersionValue));
                }

                if (args.Properties.TryGetValue(UseWindowsFormsTelemetryPropertyKey, out string useWindowsFormsValue))
                {
                    maskedProperties.Add(UseWindowsFormsTelemetryPropertyKey, SanitizeToOnlyTrueFalseEmpty(useWindowsFormsValue));
                }

                if (args.Properties.TryGetValue(UseWPFTelemetryPropertyKey, out string useWPFValue))
                {
                    maskedProperties.Add(UseWPFTelemetryPropertyKey, SanitizeToOnlyTrueFalseEmpty(useWPFValue));
                }

                telemetry.TrackEvent(newEventName, maskedProperties, measurements: null);
            }
        }

        private static string SanitizeToOnlyTrueFalseEmpty(string value)
        {
            // MSBuild will throw when the task param contain empty
            // and if the field is empty json will emit the entry, so it still need to be set to something.
            if (value.Equals(StringToRepresentPropertyNotSet, StringComparison.Ordinal))
            {
                return StringToRepresentPropertyNotSet;
            }

            if (bool.TryParse(value, out bool boolValue))
            {
                return boolValue.ToString();
            }

            return false.ToString();
        }

        private void OnTelemetryLogged(object sender, TelemetryEventArgs args)
        {
            FormatAndSend(_telemetry, args);
        }

        public void Shutdown()
        {
            try
            {
                _sentinel?.Dispose();
            }
            catch (Exception)
            {
                // Exceptions during telemetry shouldn't cause anything else to fail
            }
        }

        public LoggerVerbosity Verbosity { get; set; }

        public string Parameters { get; set; }
    }
}
