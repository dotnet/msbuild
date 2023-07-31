// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Telemetry.PersistenceChannel
{
    internal static class PersistenceChannelDebugLog
    {
        private static readonly bool _isEnabled = IsEnabledByEnvironment();

        private static bool IsEnabledByEnvironment()
        {
            var environmentProvider = new EnvironmentProvider();
            return environmentProvider.GetEnvironmentVariableAsBool("DOTNET_ENABLE_PERSISTENCE_CHANNEL_DEBUG_OUTPUT", false);
        }

        public static void WriteLine(string message)
        {
            if (_isEnabled)
            {
                Reporter.Output.WriteLine(message);
            }
        }

        internal static void WriteException(Exception exception, string format, params string[] args)
        {
            var message = string.Format(CultureInfo.InvariantCulture, format, args);
            WriteLine(string.Format(CultureInfo.InvariantCulture, "{0} Exception: {1}", message, exception.ToString()));
        }
    }
}
