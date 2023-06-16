// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// The reason that a unification happened
    /// </summary>
    internal enum UnificationReason
    {
        /// <summary>
        /// This reference was not unified.
        /// </summary>
        DidntUnify,

        /// <summary>
        /// Unified because this was a framework assembly and it the current fusion
        /// loader rules would unify to a different version.
        /// </summary>
        FrameworkRetarget,

        /// <summary>
        /// Unified because of a binding redirect coming from either an explicit
        /// app.config file or implicitly because AutoUnify was true.
        /// </summary>
        BecauseOfBindingRedirect
    }
}
