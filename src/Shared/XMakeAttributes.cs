// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Contains the names of the known attributes in the XML project file.
    /// </summary>
    internal static class XMakeAttributes
    {
        internal const string condition = "Condition";
        internal const string executeTargets = "ExecuteTargets";
        internal const string name = "Name";
        internal const string msbuildVersion = "MSBuildVersion";
        internal const string xmlns = "xmlns";
        internal const string defaultTargets = "DefaultTargets";
        internal const string initialTargets = "InitialTargets";
        internal const string treatAsLocalProperty = "TreatAsLocalProperty";
        internal const string dependsOnTargets = "DependsOnTargets";
        internal const string beforeTargets = "BeforeTargets";
        internal const string afterTargets = "AfterTargets";
        internal const string include = "Include";
        internal const string exclude = "Exclude";
        internal const string remove = "Remove";
        internal const string update = "Update";
        internal const string keepMetadata = "KeepMetadata";
        internal const string removeMetadata = "RemoveMetadata";
        internal const string keepDuplicates = "KeepDuplicates";
        internal const string inputs = "Inputs";
        internal const string outputs = "Outputs";
        internal const string keepDuplicateOutputs = "KeepDuplicateOutputs";
        internal const string assemblyName = "AssemblyName";
        internal const string assemblyFile = "AssemblyFile";
        internal const string taskName = "TaskName";
        internal const string continueOnError = "ContinueOnError";
        internal const string project = "Project";
        internal const string taskParameter = "TaskParameter";
        internal const string itemName = "ItemName";
        internal const string propertyName = "PropertyName";
        internal const string sdk = "Sdk";
        internal const string sdkName = "Name";
        internal const string sdkVersion = "Version";
        internal const string sdkMinimumVersion = "MinimumVersion";
        internal const string toolsVersion = "ToolsVersion";
        internal const string runtime = "Runtime";
        internal const string msbuildRuntime = "MSBuildRuntime";
        internal const string architecture = "Architecture";
        internal const string msbuildArchitecture = "MSBuildArchitecture";
        internal const string taskFactory = "TaskFactory";
        internal const string parameterType = "ParameterType";
        internal const string required = "Required";
        internal const string output = "Output";
        internal const string defaultValue = "DefaultValue";
        internal const string evaluate = "Evaluate";
        internal const string label = "Label";
        internal const string returns = "Returns";

        // Obsolete
        internal const string requiredRuntime = "RequiredRuntime";
        internal const string requiredPlatform = "RequiredPlatform";

        internal struct ContinueOnErrorValues
        {
            internal const string errorAndContinue = "ErrorAndContinue";
            internal const string errorAndStop = "ErrorAndStop";
            internal const string warnAndContinue = "WarnAndContinue";
        }

        internal struct MSBuildRuntimeValues
        {
            internal const string clr2 = "CLR2";
            internal const string clr4 = "CLR4";
            internal const string currentRuntime = "CurrentRuntime";
            internal const string any = "*";
        }

        internal struct MSBuildArchitectureValues
        {
            internal const string x86 = "x86";
            internal const string x64 = "x64";
            internal const string currentArchitecture = "CurrentArchitecture";
            internal const string any = "*";
        }

        /////////////////////////////////////////////////////////////////////////////////////////////
        // If we ever add a new MSBuild namespace (or change this one) we must update the registry key
        // we set during install to disable the XSL debugger from working on MSBuild format files.
        /////////////////////////////////////////////////////////////////////////////////////////////
        internal const string defaultXmlNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

        private static readonly HashSet<string> KnownSpecialTaskAttributes = new HashSet<string> { condition, continueOnError, msbuildRuntime, msbuildArchitecture, xmlns };

        private static readonly HashSet<string> KnownSpecialTaskAttributesIgnoreCase = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { condition, continueOnError, msbuildRuntime, msbuildArchitecture, xmlns };

        private static readonly HashSet<string> KnownBatchingTargetAttributes = new HashSet<string> { name, condition, dependsOnTargets, beforeTargets, afterTargets };

        private static readonly HashSet<string> ValidMSBuildRuntimeValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { MSBuildRuntimeValues.clr2, MSBuildRuntimeValues.clr4, MSBuildRuntimeValues.currentRuntime, MSBuildRuntimeValues.any };

        private static readonly HashSet<string> ValidMSBuildArchitectureValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { MSBuildArchitectureValues.x86, MSBuildArchitectureValues.x64, MSBuildArchitectureValues.currentArchitecture, MSBuildArchitectureValues.any };

        /// <summary>
        /// Returns true if and only if the specified attribute is one of the attributes that the engine specifically recognizes
        /// on a task and treats in a special way.
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns>true, if given attribute is a reserved task attribute</returns>
        internal static bool IsSpecialTaskAttribute(string attribute)
        {
            // Currently the known "special" attributes for a task are:
            //  Condition, ContinueOnError
            //
            // We want to match case-sensitively on all of them
            return KnownSpecialTaskAttributes.Contains(attribute);
        }

        /// <summary>
        /// Checks if the specified attribute is a reserved task attribute with incorrect casing.
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns>true, if the given attribute is reserved and badly cased</returns>
        internal static bool IsBadlyCasedSpecialTaskAttribute(string attribute)
        {
            return !IsSpecialTaskAttribute(attribute) && KnownSpecialTaskAttributesIgnoreCase.Contains(attribute);
        }

        /// <summary>
        /// Indicates if the specified attribute cannot be used for batching targets.
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns>true, if a target cannot batch on the given attribute</returns>
        internal static bool IsNonBatchingTargetAttribute(string attribute)
        {
            return KnownBatchingTargetAttributes.Contains(attribute);
        }

        /// <summary>
        /// Returns true if the given string is a valid member of the MSBuildRuntimeValues set
        /// </summary>
        internal static bool IsValidMSBuildRuntimeValue(string runtime)
        {
            return runtime == null || ValidMSBuildRuntimeValues.Contains(runtime);
        }

        /// <summary>
        /// Returns true if the given string is a valid member of the MSBuildArchitectureValues set
        /// </summary>
        internal static bool IsValidMSBuildArchitectureValue(string architecture)
        {
            return architecture == null || ValidMSBuildArchitectureValues.Contains(architecture);
        }

        /// <summary>
        /// Compares two members of MSBuildRuntimeValues, returning true if they count as a match, and false otherwise.  
        /// </summary>
        internal static bool RuntimeValuesMatch(string runtimeA, string runtimeB)
        {
            ErrorUtilities.VerifyThrow(runtimeA != String.Empty && runtimeB != String.Empty, "We should never get an empty string passed to this method");

            if (runtimeA == null || runtimeB == null)
            {
                // neither one cares, or only one cares, so they match by default. 
                return true;
            }

            if (runtimeA.Equals(runtimeB, StringComparison.OrdinalIgnoreCase))
            {
                // if they are equal, of course they match
                return true;
            }

            if (runtimeA.Equals(MSBuildRuntimeValues.any, StringComparison.OrdinalIgnoreCase) || runtimeB.Equals(MSBuildRuntimeValues.any, StringComparison.OrdinalIgnoreCase))
            {
                // one or both explicitly don't care -- still a match. 
                return true;
            }

            if ((runtimeA.Equals(MSBuildRuntimeValues.currentRuntime, StringComparison.OrdinalIgnoreCase) && runtimeB.Equals(MSBuildRuntimeValues.clr4, StringComparison.OrdinalIgnoreCase)) ||
                (runtimeA.Equals(MSBuildRuntimeValues.clr4, StringComparison.OrdinalIgnoreCase) && runtimeB.Equals(MSBuildRuntimeValues.currentRuntime, StringComparison.OrdinalIgnoreCase)))
            {
                // CLR4 is the current runtime, so this is also a match. 
                return true;
            }

            // if none of the above is true, then it doesn't match ...
            return false;
        }

        /// <summary>
        /// Given two MSBuildRuntime values, returns the concrete result of merging the two.  If the merge fails, the merged runtime 
        /// string is returned null, and the return value of the method is false.  Otherwise, if the merge succeeds, the method returns 
        /// true with the merged runtime value.  E.g.: 
        /// "CLR4" + "CLR2" = null (false)
        /// "CLR2" + "don't care" = "CLR2" (true)
        /// "current runtime" + "CLR4" = "CLR4" (true) 
        /// "current runtime" + "don't care" = "CLR4" (true)
        /// If both specify "don't care", then defaults to the current runtime -- CLR4. 
        /// A null or empty string is interpreted as "don't care".
        /// </summary>
        internal static bool TryMergeRuntimeValues(string runtimeA, string runtimeB, out string mergedRuntime)
        {
            ErrorUtilities.VerifyThrow(runtimeA != String.Empty && runtimeB != String.Empty, "We should never get an empty string passed to this method");

            // set up the defaults
            if (runtimeA == null)
            {
                runtimeA = MSBuildRuntimeValues.any;
            }

            if (runtimeB == null)
            {
                runtimeB = MSBuildRuntimeValues.any;
            }

            // if they're equal, then there's no problem -- just return the equivalent runtime.  
            if (runtimeA.Equals(runtimeB, StringComparison.OrdinalIgnoreCase))
            {
                if (runtimeA.Equals(MSBuildRuntimeValues.currentRuntime, StringComparison.OrdinalIgnoreCase) ||
                    runtimeA.Equals(MSBuildRuntimeValues.any, StringComparison.OrdinalIgnoreCase))
                {
                    mergedRuntime = MSBuildRuntimeValues.clr4;
                }
                else
                {
                    mergedRuntime = runtimeA;
                }

                return true;
            }

            // if both A and B are one of CLR4, don't care, or current, then the end result will be CLR4 no matter what.  
            if (
                (
                 runtimeA.Equals(MSBuildRuntimeValues.clr4, StringComparison.OrdinalIgnoreCase) ||
                 runtimeA.Equals(MSBuildRuntimeValues.currentRuntime, StringComparison.OrdinalIgnoreCase) ||
                 runtimeA.Equals(MSBuildRuntimeValues.any, StringComparison.OrdinalIgnoreCase)
                ) &&
                (
                 runtimeB.Equals(MSBuildRuntimeValues.clr4, StringComparison.OrdinalIgnoreCase) ||
                 runtimeB.Equals(MSBuildRuntimeValues.currentRuntime, StringComparison.OrdinalIgnoreCase) ||
                 runtimeB.Equals(MSBuildRuntimeValues.any, StringComparison.OrdinalIgnoreCase)
                )
               )
            {
                mergedRuntime = MSBuildRuntimeValues.clr4;
                return true;
            }

            // If A doesn't care, then it's B -- and we can say B straight out, because if B were one of the 
            // special cases (current runtime or don't care) then it would already have been caught in the 
            // previous clause. 
            if (runtimeA.Equals(MSBuildRuntimeValues.any, StringComparison.OrdinalIgnoreCase))
            {
                mergedRuntime = runtimeB;
                return true;
            }

            // And vice versa
            if (runtimeB.Equals(MSBuildRuntimeValues.any, StringComparison.OrdinalIgnoreCase))
            {
                mergedRuntime = runtimeA;
                return true;
            }

            // and now we've run out of things that it could be -- all the remaining options are non-matches.  
            mergedRuntime = null;
            return false;
        }

        /// <summary>
        /// Compares two members of MSBuildArchitectureValues, returning true if they count as a match, and false otherwise.  
        /// </summary>
        internal static bool ArchitectureValuesMatch(string architectureA, string architectureB)
        {
            ErrorUtilities.VerifyThrow(architectureA != String.Empty && architectureB != String.Empty, "We should never get an empty string passed to this method");

            if (architectureA == null || architectureB == null)
            {
                // neither one cares, or only one cares, so they match by default. 
                return true;
            }

            if (architectureA.Equals(architectureB, StringComparison.OrdinalIgnoreCase))
            {
                // if they are equal, of course they match
                return true;
            }

            if (architectureA.Equals(MSBuildArchitectureValues.any, StringComparison.OrdinalIgnoreCase) || architectureB.Equals(MSBuildArchitectureValues.any, StringComparison.OrdinalIgnoreCase))
            {
                // one or both explicitly don't care -- still a match. 
                return true;
            }

            string currentArchitecture = GetCurrentMSBuildArchitecture();

            if ((architectureA.Equals(MSBuildArchitectureValues.currentArchitecture, StringComparison.OrdinalIgnoreCase) && architectureB.Equals(currentArchitecture, StringComparison.OrdinalIgnoreCase)) ||
                (architectureA.Equals(currentArchitecture, StringComparison.OrdinalIgnoreCase) && architectureB.Equals(MSBuildArchitectureValues.currentArchitecture, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // if none of the above is true, then it doesn't match ...
            return false;
        }

        /// <summary>
        /// Given an MSBuildRuntime value that may be non-explicit -- e.g. "CurrentRuntime" or "Any" --
        /// return the specific MSBuildRuntime value that it would map to in this case. If it does not map 
        /// to any known runtime, just return it as is -- maybe someone else knows what to do with it; if 
        /// not, they'll certainly have more context on logging or throwing the error. 
        /// </summary>
        internal static string GetExplicitMSBuildRuntime(string runtime)
        {
            if (runtime == null ||
                MSBuildRuntimeValues.any.Equals(runtime, StringComparison.OrdinalIgnoreCase) ||
                MSBuildRuntimeValues.currentRuntime.Equals(runtime, StringComparison.OrdinalIgnoreCase))
            {
                // Default to CLR4.
                return MSBuildRuntimeValues.clr4;
            }
            else
            {
                // either it's already a valid, specific runtime, or we don't know what to do with it.  Either way, return. 
                return runtime;
            }
        }

        /// <summary>
        /// Given two MSBuildArchitecture values, returns the concrete result of merging the two.  If the merge fails, the merged architecture 
        /// string is returned null, and the return value of the method is false.  Otherwise, if the merge succeeds, the method returns 
        /// true with the merged architecture value.  E.g.: 
        /// "x86" + "x64" = null (false)
        /// "x86" + "don't care" = "x86" (true)
        /// "current architecture" + "x86" = "x86" (true) on a 32-bit process, and null (false) on a 64-bit process
        /// "current architecture" + "don't care" = "x86" (true) on a 32-bit process, and "x64" (true) on a 64-bit process
        /// A null or empty string is interpreted as "don't care".
        /// If both specify "don't care", then defaults to whatever the current process architecture is.  
        /// </summary>
        internal static bool TryMergeArchitectureValues(string architectureA, string architectureB, out string mergedArchitecture)
        {
            ErrorUtilities.VerifyThrow(architectureA != String.Empty && architectureB != String.Empty, "We should never get an empty string passed to this method");

            // set up the defaults
            if (architectureA == null)
            {
                architectureA = MSBuildArchitectureValues.any;
            }

            if (architectureB == null)
            {
                architectureB = MSBuildArchitectureValues.any;
            }

            string currentArchitecture = GetCurrentMSBuildArchitecture();

            // if they're equal, then there's no problem -- just return the equivalent runtime.  
            if (architectureA.Equals(architectureB, StringComparison.OrdinalIgnoreCase))
            {
                if (architectureA.Equals(MSBuildArchitectureValues.currentArchitecture, StringComparison.OrdinalIgnoreCase) ||
                    architectureA.Equals(MSBuildArchitectureValues.any, StringComparison.OrdinalIgnoreCase))
                {
                    mergedArchitecture = currentArchitecture;
                }
                else
                {
                    mergedArchitecture = architectureA;
                }

                return true;
            }

            // if both A and B are one of CLR4, don't care, or current, then the end result will be CLR4 no matter what.  
            if (
                (
                 architectureA.Equals(currentArchitecture, StringComparison.OrdinalIgnoreCase) ||
                 architectureA.Equals(MSBuildArchitectureValues.currentArchitecture, StringComparison.OrdinalIgnoreCase) ||
                 architectureA.Equals(MSBuildArchitectureValues.any, StringComparison.OrdinalIgnoreCase)
                ) &&
                (
                 architectureB.Equals(currentArchitecture, StringComparison.OrdinalIgnoreCase) ||
                 architectureB.Equals(MSBuildArchitectureValues.currentArchitecture, StringComparison.OrdinalIgnoreCase) ||
                 architectureB.Equals(MSBuildArchitectureValues.any, StringComparison.OrdinalIgnoreCase)
                )
               )
            {
                mergedArchitecture = currentArchitecture;
                return true;
            }

            // If A doesn't care, then it's B -- and we can say B straight out, because if B were one of the 
            // special cases (current runtime or don't care) then it would already have been caught in the 
            // previous clause. 
            if (architectureA.Equals(MSBuildArchitectureValues.any, StringComparison.OrdinalIgnoreCase))
            {
                mergedArchitecture = architectureB;
                return true;
            }

            // And vice versa
            if (architectureB.Equals(MSBuildArchitectureValues.any, StringComparison.OrdinalIgnoreCase))
            {
                mergedArchitecture = architectureA;
                return true;
            }

            // and now we've run out of things that it could be -- all the remaining options are non-matches.  
            mergedArchitecture = null;
            return false;
        }

        /// <summary>
        /// Returns the MSBuildArchitecture value corresponding to the current process' architecture. 
        /// </summary>
        /// <comments>
        /// Revisit if we ever run on something other than Intel.  
        /// </comments>
        internal static string GetCurrentMSBuildArchitecture()
        {
            string currentArchitecture = (IntPtr.Size == sizeof(Int64)) ? MSBuildArchitectureValues.x64 : MSBuildArchitectureValues.x86;
            return currentArchitecture;
        }

        /// <summary>
        /// Given an MSBuildArchitecture value that may be non-explicit -- e.g. "CurrentArchitecture" or "Any" --
        /// return the specific MSBuildArchitecture value that it would map to in this case.  If it does not map 
        /// to any known architecture, just return it as is -- maybe someone else knows what to do with it; if 
        /// not, they'll certainly have more context on logging or throwing the error. 
        /// </summary>
        internal static string GetExplicitMSBuildArchitecture(string architecture)
        {
            if (architecture == null ||
                MSBuildArchitectureValues.any.Equals(architecture, StringComparison.OrdinalIgnoreCase) ||
                MSBuildArchitectureValues.currentArchitecture.Equals(architecture, StringComparison.OrdinalIgnoreCase))
            {
                string currentArchitecture = GetCurrentMSBuildArchitecture();
                return currentArchitecture;
            }
            else
            {
                // either it's already a valid, specific architecture, or we don't know what to do with it.  Either way, return. 
                return architecture;
            }
        }
    }
}
