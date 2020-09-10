// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal sealed class ResolveAssemblyReferenceBuildEngine : IBuildEngine
    {
        internal int EventCount => CustomBuildEvent.Count + MessageBuildEvent.Count + WarningBuildEvent.Count + ErrorBuildEvent.Count;
        internal List<CustomBuildEventArgs> CustomBuildEvent { get; } = new List<CustomBuildEventArgs>();
        internal List<BuildMessageEventArgs> MessageBuildEvent { get; } = new List<BuildMessageEventArgs>();
        internal List<BuildWarningEventArgs> WarningBuildEvent { get; } = new List<BuildWarningEventArgs>();
        internal List<BuildErrorEventArgs> ErrorBuildEvent { get; } = new List<BuildErrorEventArgs>();

        public bool AllowFailureWithoutError { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool IsRunningMultipleNodes => throw new NotImplementedException();

        public bool ContinueOnError => throw new NotImplementedException();

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        public string ProjectFileOfTaskNode => string.Empty;

       
        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            CustomBuildEvent.Add(e);
        }

        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            ErrorBuildEvent.Add(e);
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            MessageBuildEvent.Add(e);
        }

        public void LogTelemetry(string eventName, IDictionary<string, string> properties)
        {
            throw new NotImplementedException();
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            WarningBuildEvent.Add(e);
        }

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
        {
            throw new NotImplementedException();
        }
    }
}
