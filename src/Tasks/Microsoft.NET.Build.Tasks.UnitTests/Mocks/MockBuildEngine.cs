// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class MockBuildEngine : IBuildEngine4
    {
        public int ColumnNumberOfTaskNode { get; set; }

        public bool ContinueOnError { get; set; }

        public int LineNumberOfTaskNode { get; set; }

        public string ProjectFileOfTaskNode { get; set; }

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
        {
            throw new NotImplementedException();
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            CustomEvents.Add(e);
        }

        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            Errors.Add(e);
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            Messages.Add(e);
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            Warnings.Add(e);
        }

        public virtual object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            RegisteredTaskObjectsQueries++;

            RegisteredTaskObjects.TryGetValue(key, out object ret);
            return ret;
        }
        
        public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
        {
            RegisteredTaskObjects.Add(key, obj);
        }

        public object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime) => null;
        public BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs) => new BuildEngineResult();
        public void Yield() {
        }
        public void Reacquire() {
        }
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion) => false;
        public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion) => false;

        public IList<CustomBuildEventArgs> CustomEvents { get; } = new List<CustomBuildEventArgs>();
        public IList<BuildErrorEventArgs> Errors { get; } = new List<BuildErrorEventArgs>();
        public IList<BuildMessageEventArgs> Messages { get; } = new List<BuildMessageEventArgs>();
        public IList<BuildWarningEventArgs> Warnings { get; } = new List<BuildWarningEventArgs>();
        public Dictionary<object, object> RegisteredTaskObjects { get; } = new Dictionary<object, object>();
        public int RegisteredTaskObjectsQueries = 0;

        public bool IsRunningMultipleNodes => throw new NotImplementedException();
    }
}
