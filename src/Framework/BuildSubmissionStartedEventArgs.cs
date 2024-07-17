// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    [Serializable]
    public class BuildSubmissionStartedEventArgs : BuildStatusEventArgs
    {
        public IReadOnlyDictionary<string, string?> GlobalProperties { get; protected set; }

        public IEnumerable<string> EntryProjectsFullPath { get; protected set; }

        public ICollection<string> TargetNames { get; protected set; }

        public BuildRequestDataFlags Flags { get; protected set; }

        public int SubmissionId { get; protected set; }

        public BuildSubmissionStartedEventArgs(
            IReadOnlyDictionary<string, string?> globalProperties,
            IEnumerable<string> entryProjectsFullPath,
            ICollection<string> targetNames,
            BuildRequestDataFlags flags,
            int submissionId)
            : base()
        {
            GlobalProperties = globalProperties;
            EntryProjectsFullPath = entryProjectsFullPath;
            TargetNames = targetNames;
            Flags = flags;
            SubmissionId = submissionId;
        }
    }
}
