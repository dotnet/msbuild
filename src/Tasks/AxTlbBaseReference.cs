// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Common abstract base for aximp and tlbimp COM reference wrapper classes. 
    /// They share the resolution method and only differ in constructing the wrapper file name.
    /// </summary>
    internal abstract class AxTlbBaseReference : ComReference
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
        /// <param name="executeAsTool">True if GenerateWrapper() should generate the wrapper out-of-proc using aximp.exe or tlbimp.exe</param>
        /// <param name="toolPath">Path to the SDK tools directory where aximp.exe or tlbimp.exe can be found</param>
        /// <param name="buildEngine">BuildEngine of parent task; needed for logging purposes when generating wrapper out-of-proc</param>
        internal AxTlbBaseReference(TaskLoggingHelper taskLoggingHelper, bool silent, IComReferenceResolver resolverCallback, ComReferenceInfo referenceInfo, string itemName, string outputDirectory, bool delaySign, string keyFile, string keyContainer, bool includeTypeLibVersionInName, bool executeAsTool, string toolPath, IBuildEngine buildEngine, string[] environmentVariables)
            : base(taskLoggingHelper, silent, referenceInfo, itemName)
        {
            ResolverCallback = resolverCallback;
            OutputDirectory = outputDirectory;
            IncludeTypeLibVersionInName = includeTypeLibVersionInName;

            BuildEngine = buildEngine;
            EnvironmentVariables = environmentVariables;
            DelaySign = delaySign;
            ExecuteAsTool = executeAsTool;
            KeyFile = keyFile;
            KeyContainer = keyContainer;
            ToolPath = toolPath;
        }

        #endregion

        #region Properties

        /// <summary>
        /// directory we should write the wrapper to
        /// </summary>
        protected virtual string OutputDirectory { get; }

        /// <summary>
        /// callback interface for resolving dependent COM refs/NET assemblies
        /// </summary>
        protected IComReferenceResolver ResolverCallback { get; }

        /// <summary>
        /// container name for public/private keys
        /// </summary>
        protected string KeyContainer { get; set; }

        /// <summary>
        /// file containing public/private keys
        /// </summary>
        protected string KeyFile { get; set; }

        /// <summary>
        /// True if generated wrappers should be delay signed
        /// </summary>
        protected bool DelaySign { get; set; }

        /// <summary>
        /// Property to allow multitargeting of ResolveComReferences:  If true, tlbimp.exe and 
        /// aximp.exe from the appropriate target framework will be run out-of-proc to generate
        /// the necessary wrapper assemblies.  
        /// </summary>
        protected bool ExecuteAsTool { get; set; }

        /// <summary>
        /// The BuildEngine of the ResolveComReference instance that created this instance
        /// of the class:  necessary for passing to the AxImp or TlbImp task that is spawned
        /// when ExecuteAsTool is set to true
        /// </summary>
        protected IBuildEngine BuildEngine { get; set; }

        /// <summary>
        /// Environment variables to pass to the tool.
        /// </summary>        
        protected string[] EnvironmentVariables { get; set; }

        /// <summary>
        /// If ExecuteAsTool is true, this must be set to the SDK 
        /// tools path for the framework version being targeted. 
        /// </summary>
        protected string ToolPath { get; set; }

        /// <summary>
        /// When true, we include the typelib version number in the name.
        /// </summary>
        protected bool IncludeTypeLibVersionInName { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Checks if there's a preexisting wrapper for this reference. 
        /// </summary>
        internal override bool FindExistingWrapper(out ComReferenceWrapperInfo wrapperInfo, DateTime componentTimestamp)
        {
            wrapperInfo = null;

            string wrapperPath = GetWrapperPath();

            // now see if the wrapper assembly actually exists
            if (!File.Exists(wrapperPath))
            {
                return false;
            }

            wrapperInfo = new ComReferenceWrapperInfo { path = wrapperPath };

            return IsWrapperUpToDate(wrapperInfo, componentTimestamp);
        }

        /// <summary>
        /// Checks if the existing wrapper is up to date.
        /// </summary>
        protected virtual bool IsWrapperUpToDate(ComReferenceWrapperInfo wrapperInfo, DateTime componentTimestamp)
        {
            Debug.Assert(!string.IsNullOrEmpty(ReferenceInfo.strippedTypeLibPath), "ReferenceInfo.path should be valid if we got here");
            if (string.IsNullOrEmpty(ReferenceInfo.strippedTypeLibPath))
            {
                throw new ComReferenceResolutionException();
            }

            // if wrapper doesn't exist, wrapper is obviously not up to date
            if (!File.Exists(wrapperInfo.path))
            {
                return false;
            }

            // if typelib file has a DIFFERENT last write time, wrapper is not up to date
            // the reason we're comparing write times in an unusual way is that type libraries are unusual
            // "source files" for wrappers. If you upgrade/downgrade a system component, its write
            // time may be earlier than before but we should still regenerate the wrapper.
            if (DateTime.Compare(File.GetLastWriteTime(ReferenceInfo.strippedTypeLibPath), componentTimestamp) != 0)
            {
                return false;
            }

            // Compare our the existing wrapper's strong name state to the one we are requesting. 
            if (!SigningRequirementsMatchExistingWrapper(wrapperInfo))
            {
                return false;
            }

            // ok, everything's looking fine, now just verify the assembly file is valid
            try
            {
                wrapperInfo.assembly = Assembly.UnsafeLoadFrom(wrapperInfo.path);
            }
            catch (BadImageFormatException)
            {
                // ouch, this assembly is malformed... need to regenerate the wrapper.
                wrapperInfo.assembly = null;
            }

            return (wrapperInfo.assembly != null);
        }

        /// <summary>
        /// Constructs the wrapper file path. 
        /// </summary>
        internal string GetWrapperPath()
        {
            // combine with the specified output directory
            return Path.Combine(OutputDirectory, GetWrapperFileName());
        }

        /// <summary>
        /// Helper method for constructing wrapper file name.
        /// </summary>
        internal string GetWrapperFileName()
        {
            return GetWrapperFileNameInternal(ReferenceInfo.typeLibName);
        }

        /*
         * Method:  GetWrapperFileName
         * 
         * 
         */
        /// <summary>
        /// Constructs the wrapper file name from a type library name. Specialized wrappers must override it if 
        /// they want to use the Resolve method from this class.
        /// </summary>
        protected abstract string GetWrapperFileNameInternal(string typeLibName);

        /// <summary>
        /// Static version of GetWrapperFileName for use when calling from the outside.
        /// This version need only be used if the interop DLL needs to include the typelib version in the name
        /// Default implementation
        /// </summary>
        /// <param name="interopDllHeader">XXX, when the interop DLL is of the form XXX.typeLibName.[Y.Z.]dll</param>
        /// <param name="typeLibName">The typelib to generate the wrapper name for</param>
        /// <param name="includeTypeLibVersionInName">True if the interop name should include the typelib's version</param>
        /// <param name="majorVerNum">Major version number to append to the interop DLL's name</param>
        /// <param name="minorVerNum">Minor version number to append to the interop DLL's name</param>
        internal static string GetWrapperFileName(string interopDllHeader, string typeLibName, bool includeTypeLibVersionInName, short majorVerNum, short minorVerNum)
        {
            // create wrapper name of the format XXX.YYY[.Z.W].dll, where
            // XXX = the header ("Interop." or the like)
            // YYY = typeLibName
            // Z.W = optional TLB version number
            var builder = new StringBuilder(interopDllHeader);
            builder.Append(typeLibName);

            if (includeTypeLibVersionInName)
            {
                builder.Append('.');
                builder.Append(majorVerNum);
                builder.Append('.');
                builder.Append(minorVerNum);
            }

            builder.Append(".dll");

            return builder.ToString();
        }

        /// <summary>
        /// Given our KeyFile, KeyContainer, and DelaySign parameters, generate the public / private 
        /// key pair and validate that it exists to the extent needed.  
        /// </summary>
        internal void GetAndValidateStrongNameKey(out StrongNameKeyPair keyPair, out byte[] publicKey)
        {
            keyPair = null;
            publicKey = null;

            // get key pair/public key
            StrongNameUtils.GetStrongNameKey(Log, KeyFile, KeyContainer, out keyPair, out publicKey);

            // make sure we give as much data to the typelib converter as necessary but not more, or we might end up 
            // with something we didn't want
            if (DelaySign)
            {
                keyPair = null;

                if (publicKey == null)
                {
                    Log.LogErrorWithCodeFromResources(null, ReferenceInfo.SourceItemSpec, 0, 0, 0, 0, "StrongNameUtils.NoPublicKeySpecified");
                    throw new StrongNameException();
                }
            }
            else
            {
                publicKey = null;

                // If the user did not specify delay sign and we didn't get a public/private
                // key pair then we have an error since a public key by itself is not enough
                // to fully sign the assembly. (only if either KeyContainer or KeyFile was specified though)
                if (keyPair == null)
                {
                    if (!string.IsNullOrEmpty(KeyContainer))
                    {
                        Log.LogErrorWithCodeFromResources(null, ReferenceInfo.SourceItemSpec, 0, 0, 0, 0, "ResolveComReference.StrongNameUtils.NoKeyPairInContainer", KeyContainer);
                        throw new StrongNameException();
                    }
                    if (!string.IsNullOrEmpty(KeyFile))
                    {
                        Log.LogErrorWithCodeFromResources(null, ReferenceInfo.SourceItemSpec, 0, 0, 0, 0, "ResolveComReference.StrongNameUtils.NoKeyPairInFile", KeyFile);
                        throw new StrongNameException();
                    }
                }
            }
        }

        /// <summary>
        /// Compare the strong name signing state of the existing wrapper to the signing 
        /// state we are requesting in this run of the task. Return true if they match (e.g.
        /// from a signing perspective, the wrapper is up-to-date) or false otherwise.
        /// </summary>
        private bool SigningRequirementsMatchExistingWrapper(ComReferenceWrapperInfo wrapperInfo)
        {
            StrongNameLevel desiredStrongNameLevel = StrongNameLevel.None;

            if (!string.IsNullOrEmpty(KeyFile) || !string.IsNullOrEmpty(KeyContainer))
            {
                desiredStrongNameLevel = DelaySign ? StrongNameLevel.DelaySigned : StrongNameLevel.FullySigned;
            }

            // ...and see what we have already
            StrongNameLevel currentStrongNameLevel = StrongNameUtils.GetAssemblyStrongNameLevel(wrapperInfo.path);

            // if not matching, need to regenerate wrapper
            if (desiredStrongNameLevel != currentStrongNameLevel)
            {
                return false;
            }

            // if the wrapper needs a strong name, see if the public keys match
            if (desiredStrongNameLevel == StrongNameLevel.DelaySigned ||
                desiredStrongNameLevel == StrongNameLevel.FullySigned)
            {
                // get desired public key
                StrongNameUtils.GetStrongNameKey(Log, KeyFile, KeyContainer, out _, out byte[] desiredPublicKey);

                // get current public key
                AssemblyName assemblyName = AssemblyName.GetAssemblyName(wrapperInfo.path);

                if (assemblyName == null)
                {
                    return false;
                }

                byte[] currentPublicKey = assemblyName.GetPublicKey();

                if (currentPublicKey.Length != desiredPublicKey.Length)
                {
                    return false;
                }

                // compare public keys byte by byte
                for (int i = 0; i < currentPublicKey.Length; i++)
                {
                    if (currentPublicKey[i] != desiredPublicKey[i])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion
    }
}
