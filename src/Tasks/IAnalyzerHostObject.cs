// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Microsoft.Build.Framework;

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
