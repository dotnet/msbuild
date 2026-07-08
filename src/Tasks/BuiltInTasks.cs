// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Pre-registers the commonly used built-in MSBuild tasks with the host task registry so they run with
    /// no assembly loading or reflection - the path required in a trimmed or Native AOT host.
    /// </summary>
    /// <remarks>
    /// The MSBuild engine (Microsoft.Build) cannot reference this task assembly
    /// (Microsoft.Build.Tasks.Core), so these registrations are published from here. A host that runs the
    /// engine in-process under trimming/AOT calls <see cref="RegisterAll"/> once at startup, before its
    /// first build, after which a stock build can run these tasks with the reflective task-execution path
    /// disabled. Each registration roots the task type's public constructor and properties for trimming, so
    /// construction and parameter binding stay trim-safe.
    /// </remarks>
    public static class BuiltInTasks
    {
        /// <summary>
        /// Registers the commonly used built-in tasks with the host task registry.
        /// </summary>
        public static void RegisterAll()
        {
            Utilities.Task.RegisterTask<Message>(nameof(Message));
            Utilities.Task.RegisterTask<Warning>(nameof(Warning));
            Utilities.Task.RegisterTask<Error>(nameof(Error));
            Utilities.Task.RegisterTask<MakeDir>(nameof(MakeDir));
            Utilities.Task.RegisterTask<RemoveDir>(nameof(RemoveDir));
            Utilities.Task.RegisterTask<Copy>(nameof(Copy));
            Utilities.Task.RegisterTask<Delete>(nameof(Delete));
            Utilities.Task.RegisterTask<Touch>(nameof(Touch));
            Utilities.Task.RegisterTask<WriteLinesToFile>(nameof(WriteLinesToFile));
            Utilities.Task.RegisterTask<ReadLinesFromFile>(nameof(ReadLinesFromFile));
        }
    }
}
