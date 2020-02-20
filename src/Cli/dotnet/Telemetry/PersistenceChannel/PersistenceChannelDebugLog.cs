// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
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
