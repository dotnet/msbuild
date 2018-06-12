// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>ToolTask that contains shared functionality between the AxImp and TlbImp tasks.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Reflection;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// ToolTask that contains shared functionality between the AxImp and TlbImp tasks.
    /// </summary>
    internal abstract class AxTlbBaseTask : ToolTaskExtension
    {
        #region Private Data

        /// <summary>
        /// True if the keyfile only contains the public key data, and thus 
        /// we should pass the file using the /publickey: parameter instead of
        /// /keyfile. 
        /// </summary>
        private bool _delaySigningAndKeyFileOnlyContainsPublicKey;

        #endregion

        #region Properties
        /// <summary>
        /// Force strong name delay signing.  Used with KeyFile or KeyContainer.
        /// </summary>
        public bool DelaySign
        {
            get => GetBoolParameterWithDefault(nameof(DelaySign), false);
            set => Bag[nameof(DelaySign)] = value;
        }

        /// <summary>
        /// Key container containing strong name key pair.
        /// </summary>
        public string KeyContainer
        {
            get => (string)Bag[nameof(KeyContainer)];
            set => Bag[nameof(KeyContainer)] = value;
        }

        /// <summary>
        /// File containing strong name key pair.
        /// </summary>
        public string KeyFile
        {
            get => (string)Bag[nameof(KeyFile)];
            set => Bag[nameof(KeyFile)] = value;
        }

        /// <summary>
        /// Path to the SDK directory where AxImp.exe and TlbImp.exe can be found
        /// </summary>
        public string SdkToolsPath
        {
            get => (string)Bag[nameof(SdkToolsPath)];
            set => Bag[nameof(SdkToolsPath)] = value;
        }

        #endregion // Properties

        #region ToolTask Members

        /// <summary>
        /// Returns the name of the tool to execute.  AxTlbBaseTask is not
        /// executable, so return null for the ToolName -- And make sure that 
        /// Execute() logs an error!
        /// </summary>
        protected override string ToolName { get; } = null;

        /// <summary>
        /// Invokes the ToolTask with the given parameters
        /// </summary>
        /// <returns>True if the task succeeded, false otherwise</returns>
        public override bool Execute()
        {
            // This is not a callable task on its own -- so need to make sure that
            // only descendant tasks who have defined their ToolName can be executed
            if (String.IsNullOrEmpty(ToolName))
            {
                Log.LogErrorWithCodeFromResources("AxTlbBaseTask.ToolNameMustBeSet");
                return false;
            }

            return base.Execute();
        }

        /// <summary>
        /// Adds commands for the tool being executed, that cannot be put in a response file.  
        /// </summary>
        /// <param name="commandLine">The CommandLineBuilderExtension to add the commands to</param>
        protected internal override void AddCommandLineCommands(CommandLineBuilderExtension commandLine)
        {
            AddStrongNameOptions(commandLine);
            base.AddCommandLineCommands(commandLine);
        }

        /// <summary>
        /// Generates the full path to the tool being executed by this ToolTask
        /// </summary>
        /// <returns>A string containing the full path of this tool, or null if the tool was not found</returns>
        protected override string GenerateFullPathToTool()
        {
            string pathToTool = null;

            pathToTool = SdkToolsPathUtility.GeneratePathToTool
            (
                SdkToolsPathUtility.FileInfoExists,
                Utilities.ProcessorArchitecture.CurrentProcessArchitecture,
                SdkToolsPath,
                ToolName,
                Log,
                true
            );

            return pathToTool;
        }

        /// <summary>
        /// Validates the parameters passed to the task
        /// </summary>
        /// <returns>True if parameters are valid</returns>
        protected override bool ValidateParameters()
        {
            // Verify that a path for the tool exists -- if the tool doesn't exist in it 
            // we'll worry about that later
            if ((String.IsNullOrEmpty(ToolPath) || !Directory.Exists(ToolPath)) &&
                (String.IsNullOrEmpty(SdkToolsPath) || !Directory.Exists(SdkToolsPath)))
            {
                Log.LogErrorWithCodeFromResources("AxTlbBaseTask.SdkOrToolPathNotSpecifiedOrInvalid", SdkToolsPath ?? "", ToolPath ?? "");
                return false;
            }

            if (ValidateStrongNameParameters())
            {
                // Allow the base class to do any validation it thinks necessary -- as far 
                // as we're concerned, parameters check out properly
                return base.ValidateParameters();
            }
            return false;
        }

        /// <summary>
        /// Adds options involving strong name signing -- syntax is the same between 
        /// AxImp and TlbImp
        /// </summary>
        /// <param name="commandLine">The command line to add options to</param>
        private void AddStrongNameOptions(CommandLineBuilderExtension commandLine)
        {
            commandLine.AppendWhenTrue("/delaysign", Bag, "DelaySign");

            // If we're delay-signing, we only need the public key, but if we use the /publickey
            // switch, it will consume the entire key file, assume that's just the public key, and 
            // throw an error.
            // 
            // So use /publickey if that's all our KeyFile contains, but KeyFile otherwise. 
            if (_delaySigningAndKeyFileOnlyContainsPublicKey)
            {
                commandLine.AppendSwitchIfNotNull("/publickey:", KeyFile);
            }
            else
            {
                commandLine.AppendSwitchIfNotNull("/keyfile:", KeyFile);
            }

            commandLine.AppendSwitchIfNotNull("/keycontainer:", KeyContainer);
        }

        /// <summary>
        /// Validates the parameters passed to the task that involve strong name signing --
        /// DelaySign, KeyContainer, and KeyFile
        /// </summary>
        /// <returns>true if the parameters are valid, false otherwise.</returns>
        private bool ValidateStrongNameParameters()
        {
            bool keyFileExists = false;

            // Make sure that if KeyFile is defined, it's a real file.
            if (!String.IsNullOrEmpty(KeyFile))
            {
                if (File.Exists(KeyFile))
                {
                    keyFileExists = true;
                }
                else
                {
                    Log.LogErrorWithCodeFromResources("AxTlbBaseTask.InvalidKeyFileSpecified", KeyFile);
                    return false;
                }
            }

            // Check if KeyContainer name is specified
            bool keyContainerSpecified = !String.IsNullOrEmpty(KeyContainer);

            // Cannot define both KeyFile and KeyContainer
            if (keyFileExists && keyContainerSpecified)
            {
                Log.LogErrorWithCodeFromResources("AxTlbBaseTask.CannotSpecifyBothKeyFileAndKeyContainer");
                return false;
            }

            // If this assembly is delay signed, either KeyFile or KeyContainer must be defined
            if (DelaySign && !keyFileExists && !keyContainerSpecified)
            {
                Log.LogErrorWithCodeFromResources("AxTlbBaseTask.CannotSpecifyDelaySignWithoutEitherKeyFileOrKeyContainer");
                return false;
            }

            // If KeyFile or KeyContainer is specified, verify that a key pair exists (or if delay-signed, 
            // even just a public key)
            if (keyFileExists || keyContainerSpecified)
            {
                StrongNameKeyPair keyPair;
                byte[] publicKey = null;

                try
                {
                    StrongNameUtils.GetStrongNameKey(Log, KeyFile, KeyContainer, out keyPair, out publicKey);
                }
                catch (StrongNameException e)
                {
                    Log.LogErrorFromException(e);
                    keyPair = null;

                    // don't return here -- let the appropriate error below get logged also.
                }

                if (DelaySign)
                {
                    if (publicKey == null)
                    {
                        Log.LogErrorWithCodeFromResources("AxTlbBaseTask.StrongNameUtils.NoPublicKeySpecified");
                        return false;
                    }
                    if (keyPair == null)
                    {
                        // record this so we know which switch to pass to the task
                        _delaySigningAndKeyFileOnlyContainsPublicKey = true;
                    }
                }
                else
                {
                    if (keyPair == null)
                    {
                        if (!String.IsNullOrEmpty(KeyContainer))
                        {
                            Log.LogErrorWithCodeFromResources("AxTlbBaseTask.StrongNameUtils.NoKeyPairInContainer", KeyContainer);
                            return false;
                        }
                        if (!String.IsNullOrEmpty(KeyFile))
                        {
                            Log.LogErrorWithCodeFromResources("AxTlbBaseTask.StrongNameUtils.NoKeyPairInFile", KeyFile);
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        #endregion // ToolTask Members
    }
}
