﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// A packet to encapsulate a BuildEventArg logging message.
    /// Contents:
    /// Build Event Type
    /// Build Event Args
    /// </summary>
    internal class LogMessagePacket : LogMessagePacketBase
    {
        private static readonly TargetFinishedTranslator targetFinishedTranslator = new TargetFinishedTranslator(TranslateTargetFinishedEvent);

        /// <summary>
        /// Encapsulates the buildEventArg in this packet.
        /// </summary>
        internal LogMessagePacket(KeyValuePair<int, BuildEventArgs>? nodeBuildEvent)
            : base(nodeBuildEvent, targetFinishedTranslator)
        {
        }

        /// <summary>
        /// Constructor for deserialization
        /// </summary>
        private LogMessagePacket(ITranslator translator)
            : base(translator, targetFinishedTranslator)
        {
        }

        /// <summary>
        /// Factory for serialization
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            return new LogMessagePacket(translator);
        }

        /// <summary>
        /// Translate the TargetOutputs for the target finished event.
        /// </summary>
        private static void TranslateTargetFinishedEvent(ITranslator translator, TargetFinishedEventArgs finishedEvent)
        {
            List<TaskItem> targetOutputs = null;
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                if (finishedEvent.TargetOutputs != null)
                {
                    targetOutputs = new List<TaskItem>();
                    foreach (TaskItem item in finishedEvent.TargetOutputs)
                    {
                        targetOutputs.Add(item);
                    }
                }
            }

            translator.Translate<TaskItem>(ref targetOutputs, TaskItem.FactoryForDeserialization);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                finishedEvent.TargetOutputs = targetOutputs;
            }
        }
    }
}
