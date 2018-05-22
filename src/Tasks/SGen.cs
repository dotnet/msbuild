// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Genererates a serialization assembly containing XML serializers for the input assembly.
    /// </summary>
    public class SGen : ToolTaskExtension
    {
        private string _buildAssemblyPath;
        #region Properties

        // Input files
        [Required]
        public string BuildAssemblyName
        {
            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(BuildAssemblyName));
                Bag[nameof(BuildAssemblyName)] = value;
            }
            get => (string)Bag[nameof(BuildAssemblyName)];
        }

        [Required]
        public string BuildAssemblyPath
        {
            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(BuildAssemblyPath));
                _buildAssemblyPath = value;
            }

            get
            {
                string thisPath;
                try
                {
                    thisPath = Path.GetFullPath(_buildAssemblyPath);
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    // If it is an Expected Exception log the error
                    Log.LogErrorWithCodeFromResources("SGen.InvalidPath", "BuildAssemblyPath", e.Message);
                    throw;
                }

                return thisPath;
            }
        }

        [Required]
        public bool ShouldGenerateSerializer
        {
            set => Bag[nameof(ShouldGenerateSerializer)] = value;
            get => GetBoolParameterWithDefault(nameof(ShouldGenerateSerializer), false);
        }

        [Required]
        public bool UseProxyTypes
        {
            set => Bag[nameof(UseProxyTypes)] = value;
            get => GetBoolParameterWithDefault(nameof(UseProxyTypes), false);
        }


        public bool UseKeep
        {
            set => Bag[nameof(UseKeep)] = value;
            get => GetBoolParameterWithDefault(nameof(UseKeep), false);
        }

        public string[] References
        {
            set => Bag[nameof(References)] = value;
            get => (string[])Bag[nameof(References)];
        }

        public string KeyContainer
        {
            set => Bag[nameof(KeyContainer)] = value;
            get => (string)Bag[nameof(KeyContainer)];
        }

        public string KeyFile
        {
            set => Bag[nameof(KeyFile)] = value;
            get => (string)Bag[nameof(KeyFile)];
        }

        public bool DelaySign
        {
            set => Bag[nameof(DelaySign)] = value;
            get => GetBoolParameterWithDefault(nameof(DelaySign), false);
        }

        [Output]
        public ITaskItem[] SerializationAssembly
        {
            set => Bag[nameof(SerializationAssembly)] = value;
            get => (ITaskItem[])Bag[nameof(SerializationAssembly)];
        }

        public string SerializationAssemblyName
        {
            get
            {
                Debug.Assert(BuildAssemblyName.Length > 0, "Build assembly name is blank");
                string prunedAssemblyName;
                try
                {
                    prunedAssemblyName = Path.GetFileNameWithoutExtension(BuildAssemblyName);
                }
                catch (ArgumentException e)
                {
                    Log.LogErrorWithCodeFromResources("SGen.InvalidPath", "BuildAssemblyName", e.Message);
                    throw;
                }
                prunedAssemblyName += ".XmlSerializers.dll";
                return prunedAssemblyName;
            }
        }

        private string SerializationAssemblyPath
        {
            get
            {
                Debug.Assert(BuildAssemblyPath.Length > 0, "Build assembly path is blank");
                return Path.Combine(BuildAssemblyPath, SerializationAssemblyName);
            }
        }

        private string AssemblyFullPath => Path.Combine(BuildAssemblyPath, BuildAssemblyName);

        public string SdkToolsPath
        {
            set => Bag[nameof(SdkToolsPath)] = value;
            get => (string)Bag[nameof(SdkToolsPath)];
        }

        /// <summary>
        /// Gets or Sets the Compiler Platform used by SGen to generate the output assembly.
        /// </summary>
        public string Platform
        {
            set => Bag[nameof(Platform)] = value;
            get => (string)Bag[nameof(Platform)];
        }

        /// <summary>
        /// Gets or Sets a list of specific Types to generate serialization code for, SGen will generate serialization code only for those types.
        /// </summary>
        public string[] Types
        {
            set => Bag[nameof(Types)] = value;
            get => (string[])Bag[nameof(Types)];
        }

        #endregion

        #region Tool Members
        /// <summary>
        /// The name of the tool to execute.
        /// </summary>
        protected override string ToolName => "sgen.exe";

        /// <summary>
        /// The full path of the tool to execute.
        /// </summary>
        protected override string GenerateFullPathToTool()
        {
            string pathToTool = null;

            // If COMPLUS_InstallRoot\COMPLUS_Version are set (the dogfood world), we want to find it there, instead of 
            // the SDK, which may or may not be installed. The following will look there.
            if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("COMPLUS_InstallRoot")) || !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("COMPLUS_Version")))
            {
                pathToTool = ToolLocationHelper.GetPathToDotNetFrameworkFile(ToolExe, TargetDotNetFrameworkVersion.Latest);
            }

            if (String.IsNullOrEmpty(pathToTool) || !File.Exists(pathToTool))
            {
                pathToTool = SdkToolsPathUtility.GeneratePathToTool(SdkToolsPathUtility.FileInfoExists, ProcessorArchitecture.CurrentProcessArchitecture, SdkToolsPath, ToolExe, Log, true);
            }

            return pathToTool;
        }

        /// <summary>
        /// Validate parameters, log errors and warnings and return true if Execute should proceed.
        /// </summary>
        protected override bool ValidateParameters()
        {
            // Ensure the references exist before passing them to SGen.exe
            if (References != null)
            {
                foreach (string reference in References)
                {
                    if (!File.Exists(reference))
                    {
                        Log.LogErrorWithCodeFromResources("SGen.ResourceNotFound", reference);
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Returns true if task execution is not necessary. Executed after ValidateParameters
        /// </summary>
        /// <returns></returns>
        protected override bool SkipTaskExecution()
        {
            return SerializationAssembly == null && !ShouldGenerateSerializer;
        }

        /// <summary>
        /// Returns a string with those switches and other information that can't go into a response file and
        /// must go directly onto the command line.
        /// Called after ValidateParameters and SkipTaskExecution
        /// </summary>
        protected override string GenerateCommandLineCommands()
        {
            var commandLineBuilder = new CommandLineBuilderExtension();
            bool serializationAssemblyPathExists = false;
            try
            {
                if (SerializationAssembly == null)
                {
                    Debug.Assert(ShouldGenerateSerializer, "GenerateCommandLineCommands() should not be called if ShouldGenerateSerializer is true and SerializationAssembly is null.");

                    SerializationAssembly = new ITaskItem[] { new TaskItem(SerializationAssemblyPath) };
                }

                // Add the assembly switch
                commandLineBuilder.AppendSwitchIfNotNull("/assembly:", AssemblyFullPath);

                commandLineBuilder.AppendWhenTrue("/proxytypes", Bag, "UseProxyTypes");

                //add the keep switch
                commandLineBuilder.AppendWhenTrue("/keep", Bag, "UseKeep");

                // Append the references, if any.
                if (References != null)
                {
                    foreach (string reference in References)
                    {
                        commandLineBuilder.AppendSwitchIfNotNull("/reference:", reference);
                    }
                }

                //Append the Types to the command line, if any.
                if (Types != null)
                {
                    foreach (string type in Types)
                    {
                        commandLineBuilder.AppendSwitchIfNotNull("/type:", type);
                    }
                }

                // The arguments to the "/compiler" switch are themselves switches to be passed to
                // the compiler when generating the serialization assembly.

                // Add the compiler command switches for strong naming on the serialization assembly          
                if (KeyFile != null)
                {
                    commandLineBuilder.AppendNestedSwitch("/compiler:", "/keyfile:", KeyFile);
                }
                else if (KeyContainer != null)
                {
                    commandLineBuilder.AppendNestedSwitch("/compiler:", "/keycontainer:", KeyContainer);
                }

                commandLineBuilder.AppendPlusOrMinusSwitch("/compiler:/delaysign", Bag, "DelaySign");

                // Add the Platform switch to the compiler.
                if (Platform != null)
                {
                    commandLineBuilder.AppendNestedSwitch("/compiler:", "/platform:", Platform);
                }

                serializationAssemblyPathExists = File.Exists(SerializationAssemblyPath);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                // Ignore the expected exceptions because they have already been logged
            }

            // Delete the assembly if it already exists.
            if (serializationAssemblyPathExists)
            {
                try
                {
                    File.Delete(SerializationAssemblyPath);
                }
                // Of all of the exceptions that can be thrown on a File.Delete, the only ones we need to
                // be immediately concerned with are the UnauthorizedAccessException and the IOException
                // (file is in use exception).  We need to make sure that the assembly is gone before we
                // try to produce a new one because it is possible that after some changes were made to the
                // base assembly, there will, in fact, not be a serialization assembly produced.  We cannot
                // leave the earlier produced assembly around to be propagated by later processes.
                catch (UnauthorizedAccessException e)
                {
                    Log.LogErrorWithCodeFromResources("SGen.CouldNotDeleteSerializer", SerializationAssemblyPath, e.Message);
                }
                catch (IOException e)
                {
                    Log.LogErrorWithCodeFromResources("SGen.CouldNotDeleteSerializer", SerializationAssemblyPath, e.Message);
                }
                // The DirectoryNotFoundException is safely ignorable since that means that there is no
                // existing serialization assembly.  This would be extremely unlikely anyway because we
                // found the serializer just a couple of milliseconds ago.
            }

            return commandLineBuilder.ToString();
        }

        #endregion
    }
}
