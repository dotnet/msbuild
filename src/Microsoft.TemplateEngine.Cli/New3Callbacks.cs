// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli
{
    /// <summary>
    /// The set of callbacks that should be implemented by callers of <code>New3Command.Run</code>.
    /// These callbacks provide a mechanism for the template engine to invoke these operations without
    /// requiring a built-time dependency on the actual implementation.
    /// </summary>
    internal sealed class New3Callbacks
    {
        /// <summary>
        /// Callback to be executed on first run of the template engine.
        /// </summary>
        internal Action<IEngineEnvironmentSettings> OnFirstRun { get; set; }

        /// <summary>
        /// Callback to be executed to restore a project.
        /// </summary>
        internal Func<string, bool> RestoreProject { get; set; }
    }
}
