// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
#if FEATURE_APPDOMAIN
using TaskEngineAssemblyResolver = Microsoft.Build.BackEnd.Logging.TaskEngineAssemblyResolver;
#endif

#nullable disable

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// A packet to encapsulate a BuildEventArg logging message.
    /// Contents:
    /// Build Event Type
    /// Build Event Args
    /// </summary>
    internal class LogMessagePacket : LogMessagePacketBase
    {
        /// <summary>
        /// Encapsulates the buildEventArg in this packet.
        /// </summary>
        internal LogMessagePacket(KeyValuePair<int, BuildEventArgs>? nodeBuildEvent)
            : base(nodeBuildEvent, null)
        {
        }

        /// <summary>
        /// Constructor for deserialization
        /// </summary>
        private LogMessagePacket(ITranslator translator)
            : base(translator)
        {
            Translate(translator);
        }

        /// <summary>
        /// Factory for serialization
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            return new LogMessagePacket(translator);
        }
    }
}
