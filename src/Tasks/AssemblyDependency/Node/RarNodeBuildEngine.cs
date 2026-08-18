// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    /// <summary>
    /// Minimal build engine implementation to buffer logging events for collection by the host.
    /// We only need to care about properties which are accessed by TaskLoggingHelper, as they will determine the
    /// contents of the created log messages.
    /// </summary>
    internal class RarNodeBuildEngine : EngineServices, IBuildEngine10
    {
        private MessageImportance _minimumMessageImportance;
        private bool _isTaskInputLoggingEnabled;

        public int LineNumberOfTaskNode { get; private set; }

        public int ColumnNumberOfTaskNode { get; private set; }

        public string? ProjectFileOfTaskNode { get; private set; }

        public EngineServices EngineServices => this;

        public override bool IsTaskInputLoggingEnabled => _isTaskInputLoggingEnabled;

        public override bool IsOutOfProcRarNodeEnabled => false;

        public bool AllowFailureWithoutError { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool ContinueOnError => throw new NotImplementedException();

        public bool IsRunningMultipleNodes => throw new NotImplementedException();

        internal void Setup(
            int lineNumberOfTaskNode,
            int columnNumberOfTaskNode,
            string? projectFileOfTaskNode,
            MessageImportance minimumMessageImportance,
            bool isTaskInputLoggingEnabled)
        {
            LineNumberOfTaskNode = lineNumberOfTaskNode;
            ColumnNumberOfTaskNode = columnNumberOfTaskNode;
            ProjectFileOfTaskNode = projectFileOfTaskNode;
            _minimumMessageImportance = minimumMessageImportance;
            _isTaskInputLoggingEnabled = isTaskInputLoggingEnabled;
        }

        public override bool LogsMessagesOfImportance(MessageImportance importance) => importance <= _minimumMessageImportance;

        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            // TODO: Buffer to a channel for the endpoint to consume.
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            // TODO: Buffer to a channel for the endpoint to consume.
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            // TODO: Buffer to a channel for the endpoint to consume.
        }

        public void LogCustomEvent(CustomBuildEventArgs e) => throw new NotImplementedException();

        public void LogTelemetry(string eventName, IDictionary<string, string> properties) => throw new NotImplementedException();

        public bool ShouldTreatWarningAsError(string warningCode) => false;

        public bool BuildProjectFile(
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties,
            IDictionary targetOutputs,
            string toolsVersion) => throw new NotImplementedException();

        public bool BuildProjectFile(
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties,
            IDictionary targetOutputs) => throw new NotImplementedException();

        public BuildEngineResult BuildProjectFilesInParallel(
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IList<string>[] removeGlobalProperties,
            string[] toolsVersion,
            bool returnTargetOutputs) => throw new NotImplementedException();

        public bool BuildProjectFilesInParallel(
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IDictionary[] targetOutputsPerProject,
            string[] toolsVersion,
            bool useResultsCache,
            bool unloadProjectsOnCompletion) => throw new NotImplementedException();

        public object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime) => throw new NotImplementedException();

        public void Reacquire() => throw new NotImplementedException();

        public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection) => throw new NotImplementedException();

        public object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime) => throw new NotImplementedException();

        public void Yield() => throw new NotImplementedException();

        public int RequestCores(int requestedCores) => throw new NotImplementedException();

        public void ReleaseCores(int coresToRelease) => throw new NotImplementedException();

        public IReadOnlyDictionary<string, string> GetGlobalProperties() => throw new NotImplementedException();
    }
}
