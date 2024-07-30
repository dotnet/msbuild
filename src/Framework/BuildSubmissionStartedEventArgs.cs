// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    public sealed class BuildSubmissionStartedEventArgs : BuildStatusEventArgs
    {
        /// <summary>
        /// Constructor with default values.
        /// </summary>
        public BuildSubmissionStartedEventArgs()
        {
            GlobalProperties = new Dictionary<string, string?>();
            EntryProjectsFullPath = Enumerable.Empty<string>();
            TargetNames = Enumerable.Empty<string>();
            Flags = BuildRequestDataFlags.None;
            SubmissionId = 0;
        }

        public BuildSubmissionStartedEventArgs(
            IReadOnlyDictionary<string, string?> globalProperties,
            IEnumerable<string> entryProjectsFullPath,
            IEnumerable<string> targetNames,
            BuildRequestDataFlags flags,
            int submissionId)
        {
            GlobalProperties = globalProperties;
            EntryProjectsFullPath = entryProjectsFullPath;
            TargetNames = targetNames;
            Flags = flags;
            SubmissionId = submissionId;
        }

        public IReadOnlyDictionary<string, string?> GlobalProperties { get; set; }

        public IEnumerable<string> EntryProjectsFullPath { get; set; }

        public IEnumerable<string> TargetNames { get; set; }

        public BuildRequestDataFlags Flags { get; set; }

        public int SubmissionId { get; set; }

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            writer.Write(GlobalProperties.Count);
            foreach (var entry in GlobalProperties)
            {
                writer.Write((string)entry.Key);
                writer.Write((string?)entry.Value ?? "");
            }

            writer.Write(EntryProjectsFullPath.Count());
            foreach(var entry in EntryProjectsFullPath)
            {
                writer.Write((string)entry);
            }

            writer.Write(TargetNames.Count());
            foreach (var entry in TargetNames)
            {
                writer.Write((string)entry);
            }

            writer.Write((int)Flags);
            writer.Write((int)SubmissionId);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);

            int numberOfProperties = reader.ReadInt32();
            Dictionary<string, string?> globalProperties = new Dictionary<string, string?>(numberOfProperties);
            for (int i = 0; i < numberOfProperties; i++)
            {
                string key = reader.ReadString();
                string value = reader.ReadString();

                if (key != null && value != null)
                {
                    globalProperties[key] = value;
                }
            }

            GlobalProperties = globalProperties;

            int numberOfEntries = reader.ReadInt32();
            var entries = new string[numberOfEntries];
            for (int i = 0; i < numberOfEntries; i++)
            {
                entries[i] = reader.ReadString();
            }

            EntryProjectsFullPath = entries;

            int numberOfTargets = reader.ReadInt32();
            var targets = new string[numberOfTargets];
            for (int i = 0;i < numberOfTargets; i++)
            {
                targets[i] = reader.ReadString();
            }

            TargetNames = targets;

            Flags = (BuildRequestDataFlags)reader.ReadInt32();
            SubmissionId = reader.ReadInt32();
        }
    }
}
