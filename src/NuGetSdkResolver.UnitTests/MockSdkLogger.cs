// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System.Collections.Generic;
using SdkLoggerBase = Microsoft.Build.Framework.SdkLogger;

namespace NuGet.MSBuildSdkResolver.UnitTests
{
    /// <summary>
    /// A mock implementation of <see cref="SdkLoggerBase"/> that stores logged messages.
    /// </summary>
    public class MockSdkLogger : SdkLoggerBase
    {
        /// <summary>
        /// Stores the list of messages that have been logged.
        /// </summary>
        private readonly List<KeyValuePair<string, MessageImportance>> _messages = new List<KeyValuePair<string, MessageImportance>>();

        /// <summary>
        /// Gets a list of messages that have been logged.
        /// </summary>
        public IReadOnlyCollection<KeyValuePair<string, MessageImportance>> LoggedMessages
        {
            get { return _messages; }
        }

        /// <inheritdoc cref="LogMessage"/>
        public override void LogMessage(string message, MessageImportance messageImportance = MessageImportance.Low)
        {
            _messages.Add(new KeyValuePair<string, MessageImportance>(message, messageImportance));
        }
    }
}
