// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    public class ProjectBuildStats
    {
        // TODO: this might be configurable e.g. via telemetry sensitivity level?
        private const int TopNTasksToReport = 5;

        public ProjectBuildStats(bool isDeserialized)
        {
            if (!isDeserialized)
            {
                _topTasksByCumulativeExecution =
                    // sorted in descending order, plus we cannot return 0 on equality as SortedList would throw
                    new SortedList<TimeSpan, string>(Comparer<TimeSpan>.Create((a, b) => b >= a ? 1 : -1));
            }
        }

        public void AddTask(string name, TimeSpan cumulativeExectionTime, short executionsCount, bool isCustom)
        {
            if (TopNTasksToReport > 0 && cumulativeExectionTime > TimeSpan.Zero)
            {
                if (_topTasksByCumulativeExecution!.Count == 0 ||
                    _topTasksByCumulativeExecution.Last().Key < cumulativeExectionTime)
                {
                    _topTasksByCumulativeExecution.Add(cumulativeExectionTime, (isCustom ? "Custom:" : null) + name);
                }

                while (_topTasksByCumulativeExecution!.Count > TopNTasksToReport)
                {
                    _topTasksByCumulativeExecution.RemoveAt(_topTasksByCumulativeExecution.Count - 1);
                }
            }

            TotalTasksCount++;
            TotalTasksExecution += cumulativeExectionTime;
            TotalTasksExecutionsCount += executionsCount;
            if (executionsCount > 0)
            {
                TotalExecutedTasksCount++;
            }

            if (isCustom)
            {
                CustomTasksCount++;
                TotalCustomTasksExecution += cumulativeExectionTime;
                CustomTasksExecutionsCount += executionsCount;
                if (executionsCount > 0)
                {
                    ExecutedCustomTasksCount++;
                }
            }
        }

        public short TotalTasksCount { get; set; }
        public short CustomTasksCount { get; set; }
        public short TotalTasksExecutionsCount { get; set; }
        public short TotalExecutedTasksCount { get; set; }
        public short CustomTasksExecutionsCount { get; set; }
        public short ExecutedCustomTasksCount { get; set; }
        public TimeSpan TotalTasksExecution { get; set; }
        public TimeSpan TotalCustomTasksExecution { get; set; }

        public short TotalTargetsCount { get; set; }
        public short CustomTargetsCount { get; set; }
        public short TotalTargetsExecutionsCount { get; set; }
        public short ExecutedCustomTargetsCount { get; set; }

        // todo top N tasks - names (unhashed if not custom) and time

        public IReadOnlyCollection<KeyValuePair<TimeSpan, string>> TopTasksByCumulativeExecution =>
            _topTasksByCumulativeExecution ?? _topTasksDeserialized ?? [];

        internal void SetDeserializedTopN(IReadOnlyCollection<KeyValuePair<TimeSpan, string>> topNTasks)
        {
            _topTasksDeserialized = topNTasks;
        }

        private IReadOnlyCollection<KeyValuePair<TimeSpan, string>>? _topTasksDeserialized;
        private readonly SortedList<TimeSpan, string>? _topTasksByCumulativeExecution;
    }

    /// <summary>
    /// Arguments for project finished events
    /// </summary>
    // WARNING: marking a type [Serializable] without implementing
    // ISerializable imposes a serialization contract -- it is a
    // promise to never change the type's fields i.e. the type is
    // immutable; adding new fields in the next version of the type
    // without following certain special FX guidelines, can break both
    // forward and backward compatibility
    [Serializable]
    public class ProjectFinishedEventArgs : BuildStatusEventArgs
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        protected ProjectFinishedEventArgs()
            : base()
        {
            // do nothing
        }

        /// <summary>
        /// This constructor allows event data to be initialized.
        /// Sender is assumed to be "MSBuild".
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="projectFile">name of the project</param>
        /// <param name="succeeded">true indicates project built successfully</param>
        public ProjectFinishedEventArgs(
            string? message,
            string? helpKeyword,
            string? projectFile,
            bool succeeded)
            : this(message, helpKeyword, projectFile, succeeded, DateTime.UtcNow)
        {
        }

        /// <summary>
        /// This constructor allows event data to be initialized.
        /// Sender is assumed to be "MSBuild". This constructor allows the timestamp to be set as well
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="projectFile">name of the project</param>
        /// <param name="succeeded">true indicates project built successfully</param>
        /// <param name="eventTimestamp">Timestamp when the event was created</param>
        public ProjectFinishedEventArgs(
            string? message,
            string? helpKeyword,
            string? projectFile,
            bool succeeded,
            DateTime eventTimestamp)
            : base(message, helpKeyword, "MSBuild", eventTimestamp)
        {
            this.projectFile = projectFile;
            this.succeeded = succeeded;
        }

        private string? projectFile;
        private bool succeeded;

        #region CustomSerializationToStream
        /// <summary>
        /// Serializes to a stream through a binary writer
        /// </summary>
        /// <param name="writer">Binary writer which is attached to the stream the event will be serialized into</param>
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            writer.WriteOptionalString(projectFile);
            writer.Write(succeeded);

            if (ProjectBuildStats != null)
            {
                writer.Write((byte)1);
                writer.Write7BitEncodedInt(ProjectBuildStats.TotalTasksCount);
                writer.Write7BitEncodedInt(ProjectBuildStats.CustomTasksCount);
                writer.Write7BitEncodedInt(ProjectBuildStats.TotalTasksExecutionsCount);
                writer.Write7BitEncodedInt(ProjectBuildStats.TotalExecutedTasksCount);
                writer.Write7BitEncodedInt(ProjectBuildStats.CustomTasksExecutionsCount);
                writer.Write7BitEncodedInt(ProjectBuildStats.ExecutedCustomTasksCount);

                writer.Write(ProjectBuildStats.TotalTasksExecution.Ticks);
                writer.Write(ProjectBuildStats.TotalCustomTasksExecution.Ticks);

                writer.Write7BitEncodedInt(ProjectBuildStats.TotalTargetsCount);
                writer.Write7BitEncodedInt(ProjectBuildStats.CustomTargetsCount);
                writer.Write7BitEncodedInt(ProjectBuildStats.TotalTargetsExecutionsCount);
                writer.Write7BitEncodedInt(ProjectBuildStats.ExecutedCustomTargetsCount);

                writer.Write7BitEncodedInt(ProjectBuildStats.TopTasksByCumulativeExecution.Count);
                foreach (var pair in ProjectBuildStats.TopTasksByCumulativeExecution)
                {
                    writer.Write(pair.Key.Ticks);
                    writer.Write(pair.Value);
                }
            }
            else
            {
                writer.Write((byte)0);
            }
        }

        /// <summary>
        /// Deserializes from a stream through a binary reader
        /// </summary>
        /// <param name="reader">Binary reader which is attached to the stream the event will be deserialized from</param>
        /// <param name="version">The version of the runtime the message packet was created from</param>
        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);

            projectFile = reader.ReadByte() == 0 ? null : reader.ReadString();
            succeeded = reader.ReadBoolean();

            if (reader.ReadByte() == 1)
            {
                ProjectBuildStats = new ProjectBuildStats(true)
                {
                    TotalTasksCount = (short)reader.Read7BitEncodedInt(),
                    CustomTasksCount = (short)reader.Read7BitEncodedInt(),
                    TotalTasksExecutionsCount = (short)reader.Read7BitEncodedInt(),
                    TotalExecutedTasksCount = (short)reader.Read7BitEncodedInt(),
                    CustomTasksExecutionsCount = (short)reader.Read7BitEncodedInt(),
                    ExecutedCustomTasksCount = (short)reader.Read7BitEncodedInt(),
                    TotalTasksExecution = TimeSpan.FromTicks(reader.ReadInt64()),
                    TotalCustomTasksExecution = TimeSpan.FromTicks(reader.ReadInt64()),

                    TotalTargetsCount = (short)reader.Read7BitEncodedInt(),
                    CustomTargetsCount = (short)reader.Read7BitEncodedInt(),
                    TotalTargetsExecutionsCount = (short)reader.Read7BitEncodedInt(),
                    ExecutedCustomTargetsCount = (short)reader.Read7BitEncodedInt(),
                };

                ProjectBuildStats.SetDeserializedTopN(ReadTaskStats(reader));
            }
        }

        private static IReadOnlyCollection<KeyValuePair<TimeSpan, string>> ReadTaskStats(BinaryReader reader)
        {
            int cnt = reader.Read7BitEncodedInt();
            List<KeyValuePair<TimeSpan, string>> list = new (cnt);
            for (int _ = 0; _ < cnt; _++)
            {
                list.Add(new KeyValuePair<TimeSpan, string>(TimeSpan.FromTicks(reader.ReadInt64()), reader.ReadString()));
            }

            return list;
        }

        #endregion

        /// <summary>
        /// Project name
        /// </summary>
        public string? ProjectFile => projectFile;

        /// <summary>
        /// True if project built successfully, false otherwise
        /// </summary>
        public bool Succeeded => succeeded;

        public override string Message
        {
            get
            {
                if (RawMessage == null)
                {
                    RawMessage = FormatResourceStringIgnoreCodeAndKeyword(Succeeded ? "ProjectFinishedSuccess" : "ProjectFinishedFailure", Path.GetFileName(ProjectFile));
                }

                return RawMessage;
            }
        }

        // public int Foo1 { get; set; }

        public ProjectBuildStats? ProjectBuildStats { get; set; }
    }
}
