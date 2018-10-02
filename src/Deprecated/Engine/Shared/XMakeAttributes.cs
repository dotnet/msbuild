// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Xml;
using System.Globalization;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// Contains the names of the known attributes in the XML project file.
    /// </summary>
    /// <owner>RGoel</owner>
    internal static class XMakeAttributes
    {
        internal const string condition                 = "Condition";
        internal const string executeTargets            = "ExecuteTargets";
        internal const string name                      = "Name";
        internal const string msbuildVersion            = "MSBuildVersion";
        internal const string xmlns                     = "xmlns";
        internal const string defaultTargets            = "DefaultTargets";
        internal const string initialTargets            = "InitialTargets";
        internal const string dependsOnTargets          = "DependsOnTargets";
        internal const string beforeTargets             = "BeforeTargets";
        internal const string afterTargets              = "AfterTargets";
        internal const string include                   = "Include";
        internal const string exclude                   = "Exclude";
        internal const string remove                    = "Remove";
        internal const string keepMetadata              = "KeepMetadata";
        internal const string removeMetadata            = "RemoveMetadata";
        internal const string keepDuplicates            = "KeepDuplicates";
        internal const string inputs                    = "Inputs";
        internal const string outputs                   = "Outputs";
        internal const string keepDuplicateOutputs      = "KeepDuplicateOutputs";
        internal const string assemblyName              = "AssemblyName";
        internal const string assemblyFile              = "AssemblyFile";
        internal const string taskName                  = "TaskName";
        internal const string continueOnError           = "ContinueOnError";
        internal const string project                   = "Project";
        internal const string taskParameter             = "TaskParameter";
        internal const string itemName                  = "ItemName";
        internal const string propertyName              = "PropertyName";
        internal const string toolsVersion              = "ToolsVersion";
        internal const string requiredRuntime           = "RequiredRuntime";
        internal const string requiredPlatform          = "RequiredPlatform";
        internal const string runtime                   = "Runtime";
        internal const string msbuildRuntime            = "MSBuildRuntime";
        internal const string architecture              = "Architecture";
        internal const string msbuildArchitecture       = "MSBuildArchitecture";
        internal const string taskFactory               = "TaskFactory";
        internal const string parameterType             = "ParameterType";
        internal const string required                  = "Required";
        internal const string output                    = "Output";
        internal const string defaultValue              = "DefaultValue";
        internal const string evaluate                  = "Evaluate";
        internal const string label                     = "Label";
        internal const string returns                   = "Returns";

        internal const string defaultXmlNamespace       = "http://schemas.microsoft.com/developer/msbuild/2003";

        /// <summary>
        /// Returns true if and only if the specified attribute is one of the attributes that the engine specifically recognizes
        /// on a task and treats in a special way.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="attribute"></param>
        /// <returns>true, if given attribute is a reserved task attribute</returns>
        internal static bool IsSpecialTaskAttribute
        (
            string attribute
        )
        {
            // Currently the known "special" attributes for a task are:
            //  Condition, ContinueOnError
            //
            // We want to match case-sensitively on all of them
            return ((attribute == condition) ||
                    (attribute == continueOnError) ||
                    (attribute == msbuildRuntime) ||
                    (attribute == msbuildArchitecture) ||
                    (attribute == xmlns));

        }

        /// <summary>
        /// Checks if the specified attribute is a reserved task attribute with incorrect casing.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="attribute"></param>
        /// <returns>true, if the given attribute is reserved and badly cased</returns>
        internal static bool IsBadlyCasedSpecialTaskAttribute(string attribute)
        {
            return (!IsSpecialTaskAttribute(attribute) &&
                ((String.Compare(attribute, condition, StringComparison.OrdinalIgnoreCase) == 0) ||
                (String.Compare(attribute, continueOnError, StringComparison.OrdinalIgnoreCase) == 0) || 
                (String.Compare(attribute, msbuildRuntime, StringComparison.OrdinalIgnoreCase) == 0) || 
                (String.Compare(attribute, msbuildArchitecture, StringComparison.OrdinalIgnoreCase) == 0)));
        }

        /// <summary>
        /// Indicates if the specified attribute cannot be used for batching targets.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="attribute"></param>
        /// <returns>true, if a target cannot batch on the given attribute</returns>
        internal static bool IsNonBatchingTargetAttribute(string attribute)
        {
            return ((attribute == name) ||
                    (attribute == condition) ||
                    (attribute == dependsOnTargets));
        }
    }
}
