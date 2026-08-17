// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// The value of the copyLocal flag and why it was set that way.
    /// </summary>
    internal enum CopyLocalState
    {
        /// <summary>
        /// The copy local state is undecided right now.
        /// </summary>
        Undecided,

        /// <summary>
        /// The Reference should be CopyLocal='true' because it wasn't 'no' for any reason.
        /// </summary>
        YesBecauseOfHeuristic,

        /// <summary>
        /// The Reference should be CopyLocal='true' because its source item has Private='true'
        /// </summary>
        YesBecauseReferenceItemHadMetadata,

        /// <summary>
        /// The Reference should be CopyLocal='false' because it is a framework file.
        /// </summary>
        NoBecauseFrameworkFile,

        /// <summary>
        /// The Reference should be CopyLocal='false' because it is a prerequisite file.
        /// </summary>
        NoBecausePrerequisite,

        /// <summary>
        /// The Reference should be CopyLocal='false' because the Private attribute is set to 'false' in the project.
        /// </summary>
        NoBecauseReferenceItemHadMetadata,

        /// <summary>
        /// The Reference should be CopyLocal='false' because it was resolved from the GAC.
        /// </summary>
        NoBecauseReferenceResolvedFromGAC,

        /// <summary>
        /// Legacy behavior, CopyLocal='false' when the assembly is found in the GAC (even when it was resolved elsewhere).
        /// </summary>
        NoBecauseReferenceFoundInGAC,

        /// <summary>
        /// The Reference should be CopyLocal='false' because it lost a conflict between an same-named assembly file.
        /// </summary>
        NoBecauseConflictVictim,

        /// <summary>
        /// The reference was unresolved. It can't be copied to the bin directory because it wasn't found.
        /// </summary>
        NoBecauseUnresolved,

        /// <summary>
        /// The reference was embedded. It shouldn't be copied to the bin directory because it won't be loaded at runtime.
        /// </summary>
        NoBecauseEmbedded,

        /// <summary>
        /// The property copyLocalDependenciesWhenParentReferenceInGac is set to false and all the parent source items were found in the GAC.
        /// </summary>
        NoBecauseParentReferencesFoundInGAC,

        /// <summary>
        /// The "assembly" should not be copied because it is a bad image—possibly not managed, possibly not an assembly at all.
        /// </summary>
        NoBecauseBadImage,
    }

    /// <remarks>
    /// Helper methods for dealing with CopyLocalState enumeration.
    /// </remarks>
    internal static class CopyLocalStateUtility
    {
        /// <summary>
        /// Returns the true or false from a CopyLocalState.
        /// </summary>
        internal static bool IsCopyLocal(CopyLocalState state)
        {
            switch (state)
            {
                case CopyLocalState.YesBecauseOfHeuristic:
                case CopyLocalState.YesBecauseReferenceItemHadMetadata:
                    return true;
                case CopyLocalState.NoBecauseConflictVictim:
                case CopyLocalState.NoBecauseUnresolved:
                case CopyLocalState.NoBecauseFrameworkFile:
                case CopyLocalState.NoBecausePrerequisite:
                case CopyLocalState.NoBecauseReferenceItemHadMetadata:
                case CopyLocalState.NoBecauseReferenceResolvedFromGAC:
                case CopyLocalState.NoBecauseReferenceFoundInGAC:
                case CopyLocalState.NoBecauseEmbedded:
                case CopyLocalState.NoBecauseParentReferencesFoundInGAC:
                case CopyLocalState.NoBecauseBadImage:
                    return false;
                default:
                    throw new InternalErrorException("Unexpected CopyLocal flag.");
                    // Used to be:
                    //
                    //   ErrorUtilities.VerifyThrow(false, "Unexpected CopyLocal flag.");
                    //
                    // but this popped up constantly when debugging because its call
                    // directly by a property accessor in Reference.
            }
        }
    }
}
