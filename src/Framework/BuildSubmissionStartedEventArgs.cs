// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    public sealed class BuildSubmissionStartedEventArgs : BuildStatusEventArgs
    {
        public IReadOnlyDictionary<string, string?>? GlobalProperties { get; set; }

        public IEnumerable<string>? EntryProjectsFullPath { get; set; }

        public ICollection<string>? TargetNames { get; set; }

        public BuildRequestDataFlags? Flags { get; set; }

        public int? SubmissionId { get; set; }

        public BuildSubmissionStartedEventArgs(
            IReadOnlyDictionary<string, string?>? globalProperties,
            IEnumerable<string>? entryProjectsFullPath,
            ICollection<string>? targetNames,
            BuildRequestDataFlags? flags,
            int? submissionId)
        {
            GlobalProperties = globalProperties;
            EntryProjectsFullPath = entryProjectsFullPath;
            TargetNames = targetNames;
            Flags = flags;
            SubmissionId = submissionId;
        }

        internal override void WriteToStream(BinaryWriter writer)
        {
            // TODO
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            // TODO
        }
    }
}
