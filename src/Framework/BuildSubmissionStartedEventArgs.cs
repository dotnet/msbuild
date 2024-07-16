// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    // QUESTIONS: I have a base, but the imports are a bit problematic, bc the types are in Microsoft.BuildExecution
    // which I don't think is a great import to make in this case.
    [Serializable]
    public class BuildSubmissionStartedEventArgs : EventArgs
    {
        public IDictionary<string, string> GlobalProperties { get; protected set; }

        public string EntryProjectFullPath { get; protected set; }

        public ICollection<string> TargetNames { get; protected set; }

        public BuildRequestDataFlags Flags { get; protected set; }

        private string? SubmissionId;

        public BuildSubmissionStartedEventArgs(BuildRequestDataBase requestData)
        {
            requestData.EntryProjectFullPath = EntryProjectFullPath;
            requestData.TargetNames = TargetNames;
            requestData.Flags = Flags;
            requestData.SubmissionId = SubmissionId;
        }
    }
}
