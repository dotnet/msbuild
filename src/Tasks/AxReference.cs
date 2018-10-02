// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Reflection;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /*
     * Class:   AxReference
     * 
     * COM reference wrapper class for the ActiveX controls.
     *
     */
    internal class AxReference : AxTlbBaseReference
    {
        #region Constructors
        /// <summary>
        /// internal constructor
        /// </summary>
        /// <param name="taskLoggingHelper">task logger instance used for logging</param>
        /// <param name="resolverCallback">callback interface for resolving dependent COM refs/NET assemblies</param>
        /// <param name="referenceInfo">cached reference information (typelib pointer, original task item, typelib name etc.)</param>
        /// <param name="itemName">reference name (for better logging experience)</param>
        /// <param name="outputDirectory">directory we should write the wrapper to</param>
        /// <param name="delaySign">delay sign wrappers?</param>
        /// <param name="keyFile">file containing public/private keys</param>
        /// <param name="keyContainer">container name for public/private keys</param>
        /// <param name="executeAsTool">True if GenerateWrapper() should generate the wrapper out-of-proc using aximp.exe</param>
        /// <param name="sdkToolsPath">Path to the SDK tools directory where aximp.exe can be found</param>
        /// <param name="buildEngine">BuildEngine of parent task; needed for logging purposes when generating wrapper out-of-proc</param>
        internal AxReference(TaskLoggingHelper taskLoggingHelper, bool silent, IComReferenceResolver resolverCallback, ComReferenceInfo referenceInfo, string itemName, string outputDirectory,
            bool delaySign, string keyFile, string keyContainer, bool includeTypeLibVersionInName, string sdkToolsPath, IBuildEngine buildEngine, string[] environmentVariables)
            : base(taskLoggingHelper, silent, resolverCallback, referenceInfo, itemName, outputDirectory, delaySign, keyFile, keyContainer, includeTypeLibVersionInName, true /* always execute as tool */, sdkToolsPath, buildEngine, environmentVariables)
        {
            // do nothing
        }

        #endregion

        #region Methods

        /// <summary>
        /// Constructs the wrapper file name from a type library name.
        /// </summary>
        protected override string GetWrapperFileNameInternal(string typeLibName)
        {
            return GetWrapperFileName("AxInterop.", typeLibName, IncludeTypeLibVersionInName, ReferenceInfo.attr.wMajorVerNum, ReferenceInfo.attr.wMinorVerNum);
        }

        /// <summary>
        /// Generates a wrapper for this reference.
        /// </summary>
        internal bool GenerateWrapper(out ComReferenceWrapperInfo wrapperInfo)
        {
            wrapperInfo = null;

            // The tool gets the public key for itself, but we get it here anyway to
            // give nice messages in errors cases.
            GetAndValidateStrongNameKey(out _, out _);

            string tlbName = ReferenceInfo.taskItem.GetMetadata(ComReferenceItemMetadataNames.tlbReferenceName);

            // Generate wrapper out-of-proc using aximp.exe from the target framework.  MUST
            // HAVE SET SDKTOOLSPATH TO THE TARGET SDK TO WORK

            var axImp = new ResolveComReference.AxImp();

            if (ReferenceInfo != null)
            {
                axImp.ActiveXControlName = ReferenceInfo.strippedTypeLibPath;
            }

            axImp.BuildEngine = BuildEngine;
            axImp.ToolPath = ToolPath;
            axImp.EnvironmentVariables = EnvironmentVariables;
            axImp.DelaySign = DelaySign;
            axImp.GenerateSource = false;
            axImp.KeyContainer = KeyContainer;
            axImp.KeyFile = KeyFile;
            axImp.Silent = Silent;
            if (ReferenceInfo?.primaryOfAxImpRef?.resolvedWrapper?.path != null)
            {
                // This path should hit unless there was a prior resolution error or bug in the resolution code.
                // The reason is that everything (tlbs and pias) gets resolved before AxImp references.
                axImp.RuntimeCallableWrapperAssembly = ReferenceInfo.primaryOfAxImpRef.resolvedWrapper.path;
            }
            axImp.OutputAssembly = Path.Combine(OutputDirectory, GetWrapperFileName());

            bool generateWrapperSucceeded = axImp.Execute();

            string wrapperPath = GetWrapperPath();

            // store the wrapper info...
            wrapperInfo = new ComReferenceWrapperInfo { path = wrapperPath };
            wrapperInfo.assembly = Assembly.UnsafeLoadFrom(wrapperInfo.path);

            // ...and we're done!
            return generateWrapperSucceeded;
        }

        #endregion
    }
}
