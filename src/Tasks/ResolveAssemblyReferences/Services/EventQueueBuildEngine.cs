using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Domain;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal class EventQueueBuildEngine : IBuildEngine5
    {
        public List<ResolveAssemblyReferenceBuildEventArgs> BuildEventArgsQueue = new List<ResolveAssemblyReferenceBuildEventArgs>();

        public bool IsRunningMultipleNodes => throw new NotImplementedException();

        public bool ContinueOnError => throw new NotImplementedException();

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        public string ProjectFileOfTaskNode => string.Empty;

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion)
        {
            throw new NotImplementedException();
        }

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
        {
            throw new NotImplementedException();
        }

        public BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs)
        {
            throw new NotImplementedException();
        }

        public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion)
        {
            throw new NotImplementedException();
        }

        public object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            throw new NotImplementedException();
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            throw new NotImplementedException();
        }

        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            long eventTimestamp = e.UtcTimestamp.Ticks;
            string[] messageArgs = ParseMessageArguments(e.UnparsedArguments);

            var buildEventArgs = new ResolveAssemblyReferenceBuildEventArgs
            {
                BuildEventArgsType = BuildEventArgsType.Error,
                Subcategory = e.Subcategory,
                Code = e.Code,
                File = e.File,
                LineNumber = e.LineNumber,
                ColumnNumber = e.ColumnNumber,
                EndLineNumber = e.EndLineNumber,
                EndColumnNumber = e.EndColumnNumber,
                Message = e.UnformattedMessage,
                HelpKeyword = e.HelpKeyword,
                SenderName = e.SenderName,
                EventTimestamp = eventTimestamp,
                MessageArgs = messageArgs
            };

            BuildEventArgsQueue.Add(buildEventArgs);
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            long eventTimestamp = e.UtcTimestamp.Ticks;
            string[] messageArgs = ParseMessageArguments(e.UnparsedArguments);

            var buildEventArgs = new ResolveAssemblyReferenceBuildEventArgs
            {
                BuildEventArgsType = BuildEventArgsType.Message,
                Subcategory = e.Subcategory,
                Code = e.Code,
                File = e.File,
                LineNumber = e.LineNumber,
                ColumnNumber = e.ColumnNumber,
                EndLineNumber = e.EndLineNumber,
                EndColumnNumber = e.EndColumnNumber,
                Message = e.UnformattedMessage,
                HelpKeyword = e.HelpKeyword,
                SenderName = e.SenderName,
                Importance = (int)e.Importance,
                EventTimestamp = eventTimestamp,
                MessageArgs = messageArgs
            };

            BuildEventArgsQueue.Add(buildEventArgs);
        }

        public void LogTelemetry(string eventName, IDictionary<string, string> properties)
        {
            throw new NotImplementedException();
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            long eventTimestamp = e.UtcTimestamp.Ticks;
            string[] messageArgs = ParseMessageArguments(e.UnparsedArguments);

            var buildEventArgs = new ResolveAssemblyReferenceBuildEventArgs
            {
                BuildEventArgsType = BuildEventArgsType.Warning,
                Subcategory = e.Subcategory,
                Code = e.Code,
                File = e.File,
                LineNumber = e.LineNumber,
                ColumnNumber = e.ColumnNumber,
                EndLineNumber = e.EndLineNumber,
                EndColumnNumber = e.EndColumnNumber,
                Message = e.UnformattedMessage,
                HelpKeyword = e.HelpKeyword,
                SenderName = e.SenderName,
                EventTimestamp = eventTimestamp,
                MessageArgs = messageArgs
            };

            BuildEventArgsQueue.Add(buildEventArgs);
        }

        public void Reacquire()
        {
            throw new NotImplementedException();
        }

        public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
        {
            throw new NotImplementedException();
        }

        public object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            throw new NotImplementedException();
        }

        public void Yield()
        {
            throw new NotImplementedException();
        }

        private static string[] ParseMessageArguments(object[] unparsedArgs)
        {
            int numArgs = unparsedArgs.Length;
            var parsedArgs = new string[numArgs];

            for (int i = 0; i < numArgs; i++)
            {
                parsedArgs[i] = Convert.ToString(unparsedArgs[i]);
            }

            return parsedArgs;
        }
    }
}
