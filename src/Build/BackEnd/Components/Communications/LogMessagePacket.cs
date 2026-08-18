// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;
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
    internal sealed class LogMessagePacket : LogMessagePacketBase
    {
        [ThreadStatic]
        private static List<KeyValuePair<string, string>> reusablePropertyList;

        [ThreadStatic]
        private static List<(string itemType, object item)> reusableItemList;

        /// <summary>
        /// Encapsulates the buildEventArg in this packet.
        /// </summary>
        internal LogMessagePacket(KeyValuePair<int, BuildEventArgs>? nodeBuildEvent)
            : base(nodeBuildEvent)
        {
        }

        /// <summary>
        /// Constructor for deserialization
        /// </summary>
        private LogMessagePacket(ITranslator translator)
            : base(translator)
        {
        }

        /// <summary>
        /// Factory for serialization
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            return new LogMessagePacket(translator);
        }

        protected override bool EventCanSerializeItself(LoggingEventType eventType, MethodInfo methodInfo) => eventType switch
        {
            // Switch to serialization methods that we provide and don't use the WriteToStream inherited from
            // LazyFormattedBuildEventArgs.
            LoggingEventType.ProjectEvaluationStartedEvent => false,
            LoggingEventType.ProjectEvaluationFinishedEvent => false,
            LoggingEventType.ResponseFileUsedEvent => false,

            // Otherwise, defer to the base implementation.
            _ => base.EventCanSerializeItself(eventType, methodInfo),
        };

        protected override void WriteEventToStream(BuildEventArgs buildEvent, LoggingEventType eventType, ITranslator translator)
        {
            if (eventType == LoggingEventType.ProjectEvaluationStartedEvent)
            {
                WriteProjectEvaluationStartedEventToStream((ProjectEvaluationStartedEventArgs)buildEvent, translator);
                return;
            }
            else if (eventType == LoggingEventType.ProjectEvaluationFinishedEvent)
            {
                WriteProjectEvaluationFinishedEventToStream((ProjectEvaluationFinishedEventArgs)buildEvent, translator);
                return;
            }

            base.WriteEventToStream(buildEvent, eventType, translator);
        }

        protected override BuildEventArgs ReadEventFromStream(LoggingEventType eventType, ITranslator translator)
        {
            if (eventType == LoggingEventType.ProjectEvaluationStartedEvent)
            {
                return ReadProjectEvaluationStartedEventFromStream(translator);
            }
            else if (eventType == LoggingEventType.ProjectEvaluationFinishedEvent)
            {
                return ReadProjectEvaluationFinishedEventFromStream(translator);
            }

            return base.ReadEventFromStream(eventType, translator);
        }

        private static void WriteProjectEvaluationStartedEventToStream(ProjectEvaluationStartedEventArgs args, ITranslator translator)
            => WriteEvaluationEvent(args, args.ProjectFile, args.RawTimestamp, translator);

        private static void WriteProjectEvaluationFinishedEventToStream(ProjectEvaluationFinishedEventArgs args, ITranslator translator)
        {
            WriteEvaluationEvent(args, args.ProjectFile, args.RawTimestamp, translator);

            WriteProperties(args.GlobalProperties, translator);
            WriteProperties(args.Properties, translator);
            WriteItems(args.Items, translator);
            WriteProfileResult(args.ProfilerResult, translator);
        }

        private static void WriteEvaluationEvent(BuildStatusEventArgs args, string projectFile, DateTime timestamp, ITranslator translator)
        {
            var buildEventContext = args.BuildEventContext;
            translator.Translate(ref buildEventContext);
            translator.Translate(ref timestamp);
            translator.Translate(ref projectFile);
        }

        private static void WriteProfileResult(ProfilerResult? result, ITranslator translator)
        {
            bool hasValue = result.HasValue;
            translator.Translate(ref hasValue);
            if (hasValue)
            {
                var value = result.Value;
                var count = value.ProfiledLocations.Count;
                translator.Translate(ref count);

                foreach (var item in value.ProfiledLocations)
                {
                    WriteEvaluationLocation(translator, item.Key);
                    WriteProfiledLocation(translator, item.Value);
                }
            }
        }

        private static void WriteEvaluationLocation(ITranslator translator, EvaluationLocation evaluationLocation)
        {
            string elementName = evaluationLocation.ElementName;
            string elementDescription = evaluationLocation.ElementDescription;
            string evaluationPassDescription = evaluationLocation.EvaluationPassDescription;
            string file = evaluationLocation.File;
            int kind = (int)evaluationLocation.Kind;
            int evaluationPass = (int)evaluationLocation.EvaluationPass;
            bool lineHasValue = evaluationLocation.Line.HasValue;
            int line = lineHasValue ? evaluationLocation.Line.Value : 0;
            long id = evaluationLocation.Id;
            bool parentIdHasValue = evaluationLocation.ParentId.HasValue;
            long parentId = parentIdHasValue ? evaluationLocation.ParentId.Value : 0;

            translator.Translate(ref elementName);
            translator.Translate(ref elementDescription);
            translator.Translate(ref evaluationPassDescription);
            translator.Translate(ref file);

            translator.Translate(ref kind);
            translator.Translate(ref evaluationPass);

            translator.Translate(ref lineHasValue);
            if (lineHasValue)
            {
                translator.Translate(ref line);
            }

            translator.Translate(ref id);
            translator.Translate(ref parentIdHasValue);
            if (parentIdHasValue)
            {
                translator.Translate(ref parentId);
            }
        }

        private static void WriteProfiledLocation(ITranslator translator, ProfiledLocation profiledLocation)
        {
            int numberOfHits = profiledLocation.NumberOfHits;
            TimeSpan exclusiveTime = profiledLocation.ExclusiveTime;
            TimeSpan inclusiveTime = profiledLocation.InclusiveTime;
            translator.Translate(ref numberOfHits);
            translator.Translate(ref exclusiveTime);
            translator.Translate(ref inclusiveTime);
        }

        private static void WriteProperties(IEnumerable properties, ITranslator translator)
        {
            var writer = translator.Writer;
            if (properties == null)
            {
                writer.Write((byte)0);
                return;
            }

            if (reusablePropertyList == null)
            {
                reusablePropertyList = new List<KeyValuePair<string, string>>();
            }

            // it is expensive to access a ThreadStatic field every time
            var list = reusablePropertyList;

            Internal.Utilities.EnumerateProperties(properties, list, static (list, kvp) => list.Add(kvp));

            BinaryWriterExtensions.Write7BitEncodedInt(writer, list.Count);

            foreach (var item in list)
            {
                writer.Write(item.Key);
                writer.Write(item.Value);
            }

            list.Clear();
        }

        private static void WriteItems(IEnumerable items, ITranslator translator)
        {
            var writer = translator.Writer;
            if (items == null)
            {
                writer.Write((byte)0);
                return;
            }

            if (reusableItemList == null)
            {
                reusableItemList = new List<(string itemType, object item)>();
            }

            var list = reusableItemList;

            Internal.Utilities.EnumerateItems(items, dictionaryEntry =>
            {
                list.Add((dictionaryEntry.Key as string, dictionaryEntry.Value));
            });

            BinaryWriterExtensions.Write7BitEncodedInt(writer, list.Count);

            foreach (var kvp in list)
            {
                writer.Write(kvp.itemType);
                if (kvp.item is ITaskItem taskItem)
                {
                    writer.Write(taskItem.ItemSpec);
                    WriteMetadata(taskItem, writer);
                }
                else
                {
                    writer.Write(kvp.item?.ToString() ?? "");
                    writer.Write((byte)0);
                }
            }

            list.Clear();
        }

        private static void WriteMetadata(object metadataContainer, BinaryWriter writer)
        {
            if (metadataContainer is ITaskItem taskItem)
            {
                var metadata = taskItem.EnumerateMetadata();

                if (reusablePropertyList == null)
                {
                    reusablePropertyList = new List<KeyValuePair<string, string>>();
                }

                // it is expensive to access a ThreadStatic field every time
                var list = reusablePropertyList;

                foreach (var item in metadata)
                {
                    list.Add(item);
                }

                BinaryWriterExtensions.Write7BitEncodedInt(writer, list.Count);
                foreach (var kvp in list)
                {
                    writer.Write(kvp.Key ?? string.Empty);
                    writer.Write(kvp.Value ?? string.Empty);
                }

                list.Clear();
            }
            else
            {
                writer.Write((byte)0);
            }
        }

        private static ProjectEvaluationStartedEventArgs ReadProjectEvaluationStartedEventFromStream(ITranslator translator)
        {
            var (buildEventContext, timestamp, projectFile) = ReadEvaluationEvent(translator);

            var args = new ProjectEvaluationStartedEventArgs(
                ResourceUtilities.GetResourceString("EvaluationStarted"), projectFile);

            args.BuildEventContext = buildEventContext;
            args.RawTimestamp = timestamp;
            args.ProjectFile = projectFile;

            return args;
        }

        private static ProjectEvaluationFinishedEventArgs ReadProjectEvaluationFinishedEventFromStream(ITranslator translator)
        {
            var (buildEventContext, timestamp, projectFile) = ReadEvaluationEvent(translator);

            var args = new ProjectEvaluationFinishedEventArgs(
                ResourceUtilities.GetResourceString("EvaluationFinished"), projectFile);

            args.BuildEventContext = buildEventContext;
            args.RawTimestamp = timestamp;
            args.ProjectFile = projectFile;

            args.GlobalProperties = ReadProperties(translator);
            args.Properties = ReadProperties(translator);
            args.Items = ReadItems(translator);
            args.ProfilerResult = ReadProfileResult(translator);

            return args;
        }

        private static (BuildEventContext buildEventContext, DateTime timestamp, string projectFile)
            ReadEvaluationEvent(ITranslator translator)
        {
            BuildEventContext buildEventContext = null;
            translator.Translate(ref buildEventContext);

            DateTime timestamp = default;
            translator.Translate(ref timestamp);

            string projectFile = null;
            translator.Translate(ref projectFile);

            return (buildEventContext, timestamp, projectFile);
        }

        private static IEnumerable ReadProperties(ITranslator translator)
        {
            var reader = translator.Reader;
            int count = BinaryReaderExtensions.Read7BitEncodedInt(reader);
            if (count == 0)
            {
                return (DictionaryEntry[])[];
            }

            var list = new ArrayList(count);
            for (int i = 0; i < count; i++)
            {
                string key = reader.ReadString();
                string value = reader.ReadString();
                var entry = new DictionaryEntry(key, value);
                list.Add(entry);
            }

            return list;
        }

        private static IEnumerable ReadItems(ITranslator translator)
        {
            var reader = translator.Reader;

            int count = BinaryReaderExtensions.Read7BitEncodedInt(reader);
            if (count == 0)
            {
                return (DictionaryEntry[])[];
            }

            var list = new ArrayList(count);
            for (int i = 0; i < count; i++)
            {
                string itemType = reader.ReadString();
                string evaluatedValue = reader.ReadString();
                var metadata = ReadMetadata(reader);
                var taskItemData = new TaskItemData(evaluatedValue, metadata);
                var entry = new DictionaryEntry(itemType, taskItemData);
                list.Add(entry);
            }

            return list;
        }

        private static IDictionary<string, string> ReadMetadata(BinaryReader reader)
        {
            int count = BinaryReaderExtensions.Read7BitEncodedInt(reader);
            if (count == 0)
            {
                return null;
            }

            var list = ArrayDictionary<string, string>.Create(count);
            for (int i = 0; i < count; i++)
            {
                string key = reader.ReadString();
                string value = reader.ReadString();
                list.Add(key, value);
            }

            return list;
        }

        private static ProfilerResult? ReadProfileResult(ITranslator translator)
        {
            bool hasValue = false;
            translator.Translate(ref hasValue);
            if (!hasValue)
            {
                return null;
            }

            int count = 0;
            translator.Translate(ref count);

            var dictionary = new ArrayDictionary<EvaluationLocation, ProfiledLocation>(count);

            for (int i = 0; i < count; i++)
            {
                var evaluationLocation = ReadEvaluationLocation(translator);
                var profiledLocation = ReadProfiledLocation(translator);
                dictionary.Add(evaluationLocation, profiledLocation);
            }

            var result = new ProfilerResult(dictionary);
            return result;
        }

        private static EvaluationLocation ReadEvaluationLocation(ITranslator translator)
        {
            string elementName = default;
            string elementDescription = default;
            string evaluationPassDescription = default;
            string file = default;
            int kind = default;
            int evaluationPass = default;
            bool lineHasValue = default;
            int line = default;
            long id = default;
            bool parentIdHasValue = default;
            long parentId = default;

            translator.Translate(ref elementName);
            translator.Translate(ref elementDescription);
            translator.Translate(ref evaluationPassDescription);
            translator.Translate(ref file);

            translator.Translate(ref kind);
            translator.Translate(ref evaluationPass);

            translator.Translate(ref lineHasValue);
            if (lineHasValue)
            {
                translator.Translate(ref line);
            }

            translator.Translate(ref id);
            translator.Translate(ref parentIdHasValue);
            if (parentIdHasValue)
            {
                translator.Translate(ref parentId);
            }

            var evaluationLocation = new EvaluationLocation(
                id,
                parentIdHasValue ? parentId : null,
                (EvaluationPass)evaluationPass,
                evaluationPassDescription,
                file,
                lineHasValue ? line : null,
                elementName,
                elementDescription,
                (EvaluationLocationKind)kind);

            return evaluationLocation;
        }

        private static ProfiledLocation ReadProfiledLocation(ITranslator translator)
        {
            int numberOfHits = default;
            TimeSpan exclusiveTime = default;
            TimeSpan inclusiveTime = default;

            translator.Translate(ref numberOfHits);
            translator.Translate(ref exclusiveTime);
            translator.Translate(ref inclusiveTime);

            var profiledLocation = new ProfiledLocation(
                inclusiveTime,
                exclusiveTime,
                numberOfHits);

            return profiledLocation;
        }

        /// <summary>
        /// Translate the TargetOutputs for the target finished event.
        /// </summary>
        protected override void TranslateAdditionalProperties(ITranslator translator, LoggingEventType eventType, BuildEventArgs buildEvent)
        {
            if (eventType != LoggingEventType.TargetFinishedEvent)
            {
                return;
            }

            TargetFinishedEventArgs finishedEvent = (TargetFinishedEventArgs)buildEvent;
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
