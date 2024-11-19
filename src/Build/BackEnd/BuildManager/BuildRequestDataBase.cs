// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution
{
    public abstract class BuildRequestDataBase
    {
        protected BuildRequestDataBase(
            ICollection<string> targetNames,
            BuildRequestDataFlags flags,
            HostServices? hostServices)
        {
            ErrorUtilities.VerifyThrowArgumentNull(targetNames);
            foreach (string targetName in targetNames)
            {
                ErrorUtilities.VerifyThrowArgumentNull(targetName, "target");
            }

            TargetNames = new List<string>(targetNames);
            Flags = flags;
            HostServices = hostServices;
        }

        public abstract IEnumerable<string> EntryProjectsFullPath { get; }

        /// <summary>
        /// The name of the targets to build.
        /// </summary>
        /// <value>An array of targets in the project to be built.</value>
        public ICollection<string> TargetNames { get; protected set; }

        /// <summary>
        /// Extra flags for this BuildRequest.
        /// </summary>
        public BuildRequestDataFlags Flags { get; protected set; }

        /// <summary>
        /// Gets the global properties to use for this entry point.
        /// </summary>
        public abstract IReadOnlyDictionary<string, string?> GlobalPropertiesLookup { get; }

        public abstract bool IsGraphRequest { get; }

        /// <summary>
        /// Gets the HostServices object for this request.
        /// </summary>
        public HostServices? HostServices { get; }
    }

    public abstract class BuildRequestData<TRequestData, TResultData> : BuildRequestDataBase
        where TRequestData : BuildRequestData<TRequestData, TResultData>
        where TResultData : BuildResultBase
    {
        protected BuildRequestData(
            ICollection<string> targetNames,
            BuildRequestDataFlags flags,
            HostServices? hostServices)
            : base(targetNames, flags, hostServices)
        { }

        internal abstract BuildSubmissionBase<TRequestData, TResultData> CreateSubmission(
            BuildManager buildManager, int submissionId, TRequestData requestData, bool legacyThreadingSemantics);
    }
}
