// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>ToolTask that wraps TlbImp.exe, which generates assemblies from type libraries.</summary>
//-----------------------------------------------------------------------

using System;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Main class for the COM reference resolution task
    /// </summary>
    public sealed partial class ResolveComReference
    {
        /// <summary>
        /// Passed to the "Transform" property on the TlbImp task to indicate
        /// what transforms, if any, to apply to the type library during 
        /// assembly generation
        /// </summary>
        internal enum TlbImpTransformFlags
        {
            /// <summary>
            /// No transforms should be applied.
            /// </summary>
            None,

            /// <summary>
            /// Transforms [out, retval] parameters of methods on dispatch-only
            /// interfaces into return values.
            /// </summary>
            TransformDispRetVals,

            /// <summary>
            /// Mark all value classes as serializable.
            /// </summary>
            SerializableValueClasses
        }

        /// <summary>
        /// Defines the "TlbImp" MSBuild task, which enables using TlbImp.exe 
        /// to generate assemblies from type libraries.
        /// </summary>
        internal class TlbImp : AxTlbBaseTask
        {
            #region Properties
            /*
        Microsoft (R) .NET Framework Type Library to Assembly Converter 4.0.10719.0
        Copyright (C) Microsoft Corporation.  All rights reserved.

        Syntax: TlbImp TypeLibName [Options]
        Options:
            /out:FileName            File name of assembly to be produced
            /namespace:Namespace     Namespace of the assembly to be produced
            /asmversion:Version      Version number of the assembly to be produced
            /reference:FileName      File name of assembly to use to resolve references
            /tlbreference:FileName   File name of typelib to use to resolve references
            /publickey:FileName      File containing strong name public key
            /keyfile:FileName        File containing strong name key pair
            /keycontainer:FileName   Key container holding strong name key pair
            /delaysign               Force strong name delay signing
            /product:Product         The name of the product with which this assembly
                                     is distributed
            /productversion:Version  The version of the product with which this
                                     assembly is distributed
            /company:Company         The name of the company that produced this
                                     assembly
            /copyright:Copyright     Describes all copyright notices, trademarks, and
                                     registered trademarks that apply to this assembly
            /trademark:Trademark     Describes all trademarks and registered trademarks
                                     that apply to this assembly
            /unsafe                  Produce interfaces without runtime security checks
            /noclassmembers          Prevents TlbImp from adding members to classes
            /nologo                  Prevents TlbImp from displaying logo
            /silent                  Suppresses all output except for errors
            /silence:WarningNumber   Suppresses output for the given warning (Can not
                                     be used with /silent)
            /verbose                 Displays extra information
            /primary                 Produce a primary interop assembly
            /sysarray                Import SAFEARRAY as System.Array
            /machine:MachineType     Create an assembly for the specified machine type
            /transform:TransformName Perform the specified transformation
            /strictref               Only use assemblies specified using /reference and
                                     registered PIAs
            /strictref:nopia         Only use assemblies specified using /reference and
                                     ignore PIAs
            /? or /help              Display this usage message

        The assembly version must be specified as: Major.Minor.Build.Revision.

        Multiple reference assemblies can be specified by using the /reference option
        multiple times.

        Supported machine types:
            X86
            X64
            Itanium
            Agnostic

        Supported transforms:
            SerializableValueClasses Mark all value classes as serializable
            DispRet                  Apply the [out, retval] parameter transformation
                                     to methods of disp only interfaces

        A resource ID can optionally be appended to the TypeLibName when importing a
        type library from a module containing multiple type libraries.
         example: TlbImp MyModule.dll\1
       */

            /// <summary>
            /// Type library being imported to an assembly.
            /// </summary>
            public string TypeLibName
            {
                get => (string)Bag[nameof(TypeLibName)];
                set => Bag[nameof(TypeLibName)] = value;
            }

            /// <summary>
            /// Namespace of the generated assembly
            /// </summary>
            public string AssemblyNamespace
            {
                get => (string)Bag[nameof(AssemblyNamespace)];
                set => Bag[nameof(AssemblyNamespace)] = value;
            }

            /// <summary>
            /// Version of the generated assembly
            /// </summary>
            public Version AssemblyVersion
            {
                get => (Version)Bag[nameof(AssemblyVersion)];
                set => Bag[nameof(AssemblyVersion)] = value;
            }

            /// <summary>
            /// Create an assembly for the specified machine type
            /// Supported machine types:
            ///  X86
            ///  X64
            ///  Itanium
            ///  Agnostic
            /// </summary>
            public string Machine
            {
                get => (string)Bag[nameof(Machine)];
                set => Bag[nameof(Machine)] = value;
            }

            /// <summary>
            /// If true, suppresses displaying the logo
            /// </summary>
            public bool NoLogo
            {
                get => GetBoolParameterWithDefault(nameof(NoLogo), false);
                set => Bag[nameof(NoLogo)] = value;
            }

            /// <summary>
            /// File name of assembly to be produced.
            /// </summary>
            public string OutputAssembly
            {
                get => (string)Bag[nameof(OutputAssembly)];
                set => Bag[nameof(OutputAssembly)] = value;
            }

            /// <summary>
            /// If true, prevents TlbImp from adding members to classes
            /// </summary>
            public bool PreventClassMembers
            {
                get => GetBoolParameterWithDefault(nameof(PreventClassMembers), false);
                set => Bag[nameof(PreventClassMembers)] = value;
            }

            /// <summary>
            /// If true, import the SAFEARRAY type as System.Arrays
            /// </summary>
            public bool SafeArrayAsSystemArray
            {
                get => GetBoolParameterWithDefault(nameof(SafeArrayAsSystemArray), false);
                set => Bag[nameof(SafeArrayAsSystemArray)] = value;
            }

            /// <summary>
            /// If true, prevents AxImp from displaying success message.
            /// </summary>
            public bool Silent
            {
                get => GetBoolParameterWithDefault(nameof(Silent), false);
                set => Bag[nameof(Silent)] = value;
            }

            /// <summary>
            /// Transformation to be applied to the resulting assembly.
            /// </summary>
            public TlbImpTransformFlags Transform
            {
                get => GetTlbImpTransformFlagsParameterWithDefault(nameof(Transform), TlbImpTransformFlags.None);
                set => Bag[nameof(Transform)] = value;
            }

            /// <summary>
            /// If true, AxImp prints more information.
            /// </summary>
            public bool Verbose
            {
                get => GetBoolParameterWithDefault(nameof(Verbose), false);
                set => Bag[nameof(Verbose)] = value;
            }

            /// <summary>
            /// References to dependency assemblies.
            /// </summary>
            public string[] ReferenceFiles
            {
                get => (string[])Bag[nameof(ReferenceFiles)];
                set => Bag[nameof(ReferenceFiles)] = value;
            }

            #endregion // Properties

            #region ToolTask Members

            /// <summary>
            /// Returns the name of the tool to execute
            /// </summary>
            protected override string ToolName => "TlbImp.exe";

            /// <summary>
            /// Fills the provided CommandLineBuilderExtension with all the command line options used when
            /// executing this tool
            /// </summary>
            /// <param name="commandLine">Gets filled with command line commands</param>
            protected internal override void AddCommandLineCommands(CommandLineBuilderExtension commandLine)
            {
                // .ocx file being imported
                commandLine.AppendFileNameIfNotNull(TypeLibName);

                // options
                commandLine.AppendSwitchIfNotNull("/asmversion:", (AssemblyVersion != null) ? AssemblyVersion.ToString() : null);
                commandLine.AppendSwitchIfNotNull("/namespace:", AssemblyNamespace);
                commandLine.AppendSwitchIfNotNull("/machine:", Machine);
                commandLine.AppendWhenTrue("/noclassmembers", Bag, "PreventClassMembers");
                commandLine.AppendWhenTrue("/nologo", Bag, "NoLogo");
                commandLine.AppendSwitchIfNotNull("/out:", OutputAssembly);
                commandLine.AppendWhenTrue("/silent", Bag, "Silent");
                commandLine.AppendWhenTrue("/sysarray", Bag, "SafeArrayAsSystemArray");
                commandLine.AppendSwitchIfNotNull("/transform:", ConvertTransformFlagsToCommandLineCommand(Transform));
                commandLine.AppendWhenTrue("/verbose", Bag, "Verbose");
                if (ReferenceFiles != null)
                {
                    foreach (var referenceFile in ReferenceFiles)
                    {
                        commandLine.AppendSwitchIfNotNull("/reference:", referenceFile);
                    }
                }

                base.AddCommandLineCommands(commandLine);
            }

            /// <summary>
            /// Validates the parameters passed to the task
            /// </summary>
            /// <returns>True if parameters are valid</returns>
            protected override bool ValidateParameters()
            {
                // Verify that we were actually passed a .tlb to import
                if (String.IsNullOrEmpty(TypeLibName))
                {
                    Log.LogErrorWithCodeFromResources("TlbImp.NoInputFileSpecified");
                    return false;
                }

                // Verify that an allowed combination of TlbImpTransformFlags has been 
                // passed to the Transform property.
                if (!ValidateTransformFlags())
                {
                    Log.LogErrorWithCodeFromResources("TlbImp.InvalidTransformParameter", Transform.ToString());
                    return false;
                }

                return base.ValidateParameters();
            }

            /// <summary>
            /// Returns the TlbImpTransformFlags value stored in the hashtable under the provided
            /// parameter, or the default value passed if the value in the hashtable is null
            /// </summary>
            /// <param name="parameterName">The parameter used to retrieve the value from the hashtable</param>
            /// <param name="defaultValue">The default value to return if the hashtable value is null</param>
            /// <returns>The value contained in the hashtable, or if that's null, the default value passed to the method</returns>
            private TlbImpTransformFlags GetTlbImpTransformFlagsParameterWithDefault(string parameterName, TlbImpTransformFlags defaultValue)
            {
                object obj = Bag[parameterName];
                return (obj == null) ? defaultValue : (TlbImpTransformFlags)obj;
            }

            /// <summary>
            /// Verifies that an allowed combination of TlbImpTransformFlags has been 
            /// passed to the Transform property.
            /// </summary>
            /// <returns>True if Transform is valid and false otherwise</returns>
            private bool ValidateTransformFlags()
            {
                // Any flag on its own is fine ...
                switch (Transform)
                {
                    case TlbImpTransformFlags.None:
                        return true;
                    case TlbImpTransformFlags.SerializableValueClasses:
                        return true;
                    case TlbImpTransformFlags.TransformDispRetVals:
                        return true;
                }

                // ... But any and all other combinations of flags are disallowed.
                return false;
            }

            /// <summary>
            /// Converts a given flag to the equivalent parameter passed to the /transform: 
            /// option of tlbimp.exe
            /// </summary>
            /// <param name="flags">The TlbImpTransformFlags being converted</param>
            /// <returns>A string that can be passed to /transform: on the command line</returns>
            private static string ConvertTransformFlagsToCommandLineCommand(TlbImpTransformFlags flags)
            {
                switch (flags)
                {
                    case TlbImpTransformFlags.None:
                        return null;
                    case TlbImpTransformFlags.SerializableValueClasses:
                        return "SerializableValueClasses";
                    case TlbImpTransformFlags.TransformDispRetVals:
                        return "DispRet";
                }

                return null;
            }

            #endregion // ToolTask Members
        }
    }
}
