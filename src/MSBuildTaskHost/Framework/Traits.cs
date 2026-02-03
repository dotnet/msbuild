// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    ///     Represents toggleable features of the MSBuild engine.
    /// </summary>
    internal class Traits
    {
        private static Traits _instance = new Traits();

        public static Traits Instance
        {
            get
            {
                if (BuildEnvironmentState.s_runningTests)
                {
                    return new Traits();
                }
                return _instance;
            }
        }

        public Traits()
        {
            EscapeHatches = new EscapeHatches();

            DebugEngine = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBuildDebugEngine"));
            DebugNodeCommunication = DebugEngine || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDDEBUGCOMM"));
        }

        public EscapeHatches EscapeHatches { get; }

        public readonly bool DebugEngine;
        public readonly bool DebugNodeCommunication;
    }

    internal class EscapeHatches
    {
        /// <summary>
        /// Allow node reuse of TaskHost nodes. This results in task assemblies locked past the build lifetime, preventing them from being rebuilt if custom tasks change, but may improve performance.
        /// </summary>
        public readonly bool ReuseTaskHostNodes = Environment.GetEnvironmentVariable("MSBUILDREUSETASKHOSTNODES") == "1";

        /// <summary>
        /// Disable the use of paths longer than Windows MAX_PATH limits (260 characters) when running on a long path enabled OS.
        /// </summary>
        public readonly bool DisableLongPaths = Environment.GetEnvironmentVariable("MSBUILDDISABLELONGPATHS") == "1";

        /// <summary>
        /// Throws InternalErrorException.
        /// </summary>
        /// <remarks>
        /// Clone of ErrorUtilities.ThrowInternalError which isn't available in Framework.
        /// </remarks>
        public static void ThrowInternalError(string message)
        {
            throw new InternalErrorException(message);
        }
    }
}
