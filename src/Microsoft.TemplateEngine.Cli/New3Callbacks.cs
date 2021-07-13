// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli
{
    /// <summary>
    /// The set of callbacks that should be implemented by callers of <c>New3Command.Run</c>.
    /// These callbacks provide a mechanism for the template engine to invoke these operations without
    /// requiring a built-time dependency on the actual implementation.
    /// </summary>
    public sealed class New3Callbacks
    {
        /// <summary>
        /// Callback to be executed on first run of the template engine.
        /// </summary>
        public Action<IEngineEnvironmentSettings> OnFirstRun { get; set; }

        /// <summary>
        /// Callback to be executed to restore a project.
        /// </summary>
        public Func<string, bool> RestoreProject { get; set; }
    }
}
