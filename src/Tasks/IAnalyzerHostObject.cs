// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Tasks.Hosting
{
    /// <summary>
    /// Defines an interface for the Vbc/Csc tasks to communicate information about
    /// analyzers and rulesets to the IDE.
    /// </summary>
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    [Guid("B5A95716-2053-4B70-9FBF-E4148EBA96BC")]
    public interface IAnalyzerHostObject
    {
        bool SetAnalyzers(ITaskItem[] analyzers);
        bool SetRuleSet(string ruleSetFile);
        bool SetAdditionalFiles(ITaskItem[] additionalFiles);
    }
}
