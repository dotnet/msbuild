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

        public BuildSubmissionStartedEventArgs(
            IEnumerable? globalProperties,
            IEnumerable<string>? entryProjectsFullPath,
            IEnumerable<string>? targetNames,
            BuildRequestDataFlags? flags,
            int? submissionId)
        {
            GlobalProperties = globalProperties;
            EntryProjectsFullPath = entryProjectsFullPath;
            TargetNames = targetNames;
            Flags = flags;
            SubmissionId = submissionId;
        }

        // Dictionary<string, string?>
        public IEnumerable? GlobalProperties { get; set; }

        // IEnumerable<string>
        public IEnumerable<string>? EntryProjectsFullPath { get; set; }

        // ICollection<string>
        public IEnumerable<string>? TargetNames { get; set; }

        public BuildRequestDataFlags? Flags { get; set; }

        public int? SubmissionId { get; set; }

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            var properties = GlobalProperties.Cast<DictionaryEntry>().Where(entry => entry.Key != null && entry.Value != null);
            writer.Write7BitEncodedInt(properties.Count());
            foreach (var entry in properties)
            {
                writer.Write((string)entry.Key);
                writer.Write((string?)entry.Value ?? "");
            }

            writer.Write7BitEncodedInt(EntryProjectsFullPath.Count());
            foreach(var entry in EntryProjectsFullPath)
            {
                writer.Write((string)entry);
            }

            writer.Write7BitEncodedInt(TargetNames.Count());
            foreach (var entry in TargetNames)
            {
                writer.Write((string)entry);
            }

            writer.Write7BitEncodedInt((int)Flags);
            writer.Write7BitEncodedInt((int)SubmissionId);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);
        }
    }
}
