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
    public sealed class NewCommandCallbacks
    {
        /// <summary>
        /// Callback to be executed on first run of the template engine.
        /// </summary>
        public Action<IEngineEnvironmentSettings>? OnFirstRun { get; init; }

        /// <summary>
        /// Callback to be executed to restore a project.
        /// Parameters: <br/>
        /// - path to project to restore (string) - absolute path. <br/>
        /// </summary>
        public Func<string, bool>? RestoreProject { get; init; }

        /// <summary>
        /// Callback to be executed to add reference to a project.
        /// Parameters: <br/>
        /// - path to project to add references to (string) - absolute path <br/>
        /// - paths to projects(s) to reference (IReadOnlyList&lt;string&gt;) - absolute paths.
        /// </summary>
        public Func<string, IReadOnlyList<string>, bool>? AddProjectReference { get; init; }

        /// <summary>
        /// Callback to be executed to add package reference to a project.
        /// Parameters: <br/>
        /// - project path (string) - absolute path <br/>
        /// - package name (string) <br/>
        /// - package version (string, optional).
        /// </summary>
        public Func<string, string, string?, bool>? AddPackageReference { get; init; }

        /// <summary>
        /// Callback to be executed to add projects to solution.
        /// Parameters: <br/>
        /// - solution path (string) - absolute path <br/>
        /// - projects to add (IReadOnlyList&lt;string&gt;) - absolute paths<br/>
        /// - target folder in solution (string).
        /// </summary>
        public Func<string, IReadOnlyList<string>, string?, bool>? AddProjectsToSolution { get; init; }
    }
}
