// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Linq;

// TYPELIBATTR clashes with the one in InteropServices.
using TYPELIBATTR = System.Runtime.InteropServices.ComTypes.TYPELIBATTR;
using UtilitiesProcessorArchitecture = Microsoft.Build.Utilities.ProcessorArchitecture;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /*
     * Class:   TlbReference
     * 
     * COM reference wrapper class for the tlbimp tool.
     *
     */
    internal class TlbReference : AxTlbBaseReference, ITypeLibImporterNotifySink
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
        /// <param name="executeAsTool">True if GenerateWrapper() should generate the wrapper out-of-proc using tlbimp.exe</param>
        /// <param name="sdkToolsPath">Path to the SDK tools directory where tlbimp.exe can be found</param>
        /// <param name="buildEngine">BuildEngine of parent task; needed for logging purposes when generating wrapper out-of-proc</param>
        internal TlbReference(TaskLoggingHelper taskLoggingHelper, bool silent, IComReferenceResolver resolverCallback, IEnumerable<string> referenceFiles, ComReferenceInfo referenceInfo, string itemName, string outputDirectory, bool hasTemporaryWrapper,
            bool delaySign, string keyFile, string keyContainer, bool noClassMembers, string targetProcessorArchitecture, bool includeTypeLibVersionInName, bool executeAsTool, string sdkToolsPath, IBuildEngine buildEngine, string[] environmentVariables)
            : base(taskLoggingHelper, silent, resolverCallback, referenceInfo, itemName, outputDirectory, delaySign, keyFile, keyContainer, includeTypeLibVersionInName, executeAsTool, sdkToolsPath, buildEngine, environmentVariables)
        {
            HasTemporaryWrapper = hasTemporaryWrapper;
            _noClassMembers = noClassMembers;
            _targetProcessorArchitecture = targetProcessorArchitecture;
            _referenceFiles = referenceFiles;
        }

        #endregion

        #region Properties

        /// <summary>
        /// does this reference have a temporary (i.e. written to tmp directory) wrapper?
        /// </summary>
        private bool HasTemporaryWrapper { get; }

        /// <summary>
        /// directory we should write the wrapper to
        /// </summary>
        protected override string OutputDirectory => (HasTemporaryWrapper) ? Path.GetTempPath() : base.OutputDirectory;

        private readonly bool _noClassMembers;
        private readonly string _targetProcessorArchitecture;
        private readonly IEnumerable<string> _referenceFiles;

        #endregion

        #region Methods

        /*
         * Method:  GetWrapperFileName
         * 
         * Constructs the wrapper file name from a type library name.
         */
        protected override string GetWrapperFileNameInternal(string typeLibName)
        {
            return GetWrapperFileName("Interop.", typeLibName, IncludeTypeLibVersionInName, ReferenceInfo.attr.wMajorVerNum, ReferenceInfo.attr.wMinorVerNum);
        }

        /// <summary>
        /// Static version of GetWrapperFileName, as it really doesn't depend on
        /// anything specific to the class, and this way it can be called using
        /// TlbReference.GetWrapperFileName from outside
        /// </summary>
        /// <param name="typeLibName">The typelib to generate the wrapper name for</param>
        /// <returns>The appropriate wrapper filename</returns>
        internal static string GetWrapperFileName(string typeLibName)
        {
            return GetWrapperFileName(typeLibName, false /* don't include version in name */, 1, 0 /* v1.0 = some random version that won't be used */);
        }

        /// <summary>
        /// Static version of GetWrapperFileName, as it really doesn't depend on
        /// anything specific to the class, and this way it can be called using
        /// TlbReference.GetWrapperFileName from outside
        /// </summary>
        /// <param name="typeLibName">The typelib to generate the wrapper name for</param>
        /// <param name="includeTypeLibVersionInName">True if the interop name should include the typelib's version</param>
        /// <param name="majorVerNum">Major version number to append to the interop DLL's name</param>
        /// <param name="minorVerNum">Minor version number to append to the interop DLL's name</param>
        /// <returns>The appropriate wrapper filename</returns>
        internal static string GetWrapperFileName(string typeLibName, bool includeTypeLibVersionInName, short majorVerNum, short minorVerNum)
        {
            return GetWrapperFileName("Interop.", typeLibName, includeTypeLibVersionInName, majorVerNum, minorVerNum);
        }

        /*
         * Method:  FindExistingWrapper
         * 
         * Checks if there's a preexisting wrapper for this reference.
         */
        internal override bool FindExistingWrapper(out ComReferenceWrapperInfo wrapperInfo, DateTime componentTimestamp)
        {
            if (!HasTemporaryWrapper)
            {
                return base.FindExistingWrapper(out wrapperInfo, componentTimestamp);
            }

            // if this reference has a temporary wrapper, it can't possibly have a preexisting one
            wrapperInfo = null;
            return false;
        }

        /*
         * Method:  GenerateWrapper
         * 
         * Generates a wrapper for this reference.
         */
        internal bool GenerateWrapper(out ComReferenceWrapperInfo wrapperInfo)
        {
            wrapperInfo = null;

            string rootNamespace = ReferenceInfo.typeLibName;
            string wrapperPath = GetWrapperPath();
            bool generateWrapperSucceeded = true;

            if (ExecuteAsTool)
            {
                // delegate generation of the assembly to an instance of the TlbImp ToolTask. MUST
                // HAVE SET SDKTOOLSPATH TO THE TARGET SDK TO WORK
                var tlbImp = new ResolveComReference.TlbImp
                {
                    BuildEngine = BuildEngine,
                    EnvironmentVariables = EnvironmentVariables,
                    DelaySign = DelaySign,
                    KeyContainer = KeyContainer,
                    KeyFile = KeyFile,
                    OutputAssembly = wrapperPath,
                    ToolPath = ToolPath,
                    TypeLibName = ReferenceInfo.fullTypeLibPath,
                    AssemblyNamespace = rootNamespace,
                    AssemblyVersion = null,
                    PreventClassMembers = _noClassMembers,
                    SafeArrayAsSystemArray = true,
                    Silent = Silent,
                    Transform = ResolveComReference.TlbImpTransformFlags.TransformDispRetVals
                };

                if (_referenceFiles != null)
                {
                    // Issue is that there may be reference dependencies that need to be passed in. It is possible
                    // that the set of references will also contain the file that is meant to be written here (when reference resolution
                    // found the file in the output folder). We need to filter out this case.
                    var fullPathToOutput = Path.GetFullPath(wrapperPath); // Current directory is the directory of the project file.
                    tlbImp.ReferenceFiles = _referenceFiles.Where(rf => String.Compare(fullPathToOutput, rf, StringComparison.OrdinalIgnoreCase) != 0).ToArray();
                }

                switch (_targetProcessorArchitecture)
                {
                    case UtilitiesProcessorArchitecture.MSIL:
                        tlbImp.Machine = "Agnostic";
                        break;
                    case UtilitiesProcessorArchitecture.AMD64:
                        tlbImp.Machine = "X64";
                        break;
                    case UtilitiesProcessorArchitecture.IA64:
                        tlbImp.Machine = "Itanium";
                        break;
                    case UtilitiesProcessorArchitecture.X86:
                        tlbImp.Machine = "X86";
                        break;
                    case UtilitiesProcessorArchitecture.ARM:
                        tlbImp.Machine = "ARM";
                        break;
                    case null:
                        break;
                    default:
                        // Transmit the flag directly from the .targets files and rely on tlbimp.exe to produce a good error message.
                        tlbImp.Machine = _targetProcessorArchitecture;
                        break;
                }

                generateWrapperSucceeded = tlbImp.Execute();

                // store the wrapper info...
                wrapperInfo = new ComReferenceWrapperInfo { path = (HasTemporaryWrapper) ? null : wrapperPath };

                // Changed to ReflectionOnlyLoadFrom, related to bug:
                //  RCR: Bad COM-interop assemblies being generated when using 64-bit MSBuild to build a project that is targeting 32-bit platform
                // The original call to UnsafeLoadFrom loads the assembly in preparation for execution. If the assembly is x86 and this is x64 msbuild.exe then we
                // have problems (UnsafeLoadFrom will fail). We only use this assembly for reference resolution so we don't need to be ready to execute the code.
                //
                // Its actually not clear to me that we even need to load the assembly at all. Reference resoluton is only used in the !ExecuteAsTool which is not
                // where we are right now.
                //
                // If we really do need to load it then:
                //
                //  wrapperInfo.assembly = Assembly.ReflectionOnlyLoadFrom(wrapperPath);
            }
            else
            {
                // use framework classes in-proc to generate the assembly
                var converter = new TypeLibConverter();
                AssemblyBuilder assemblyBuilder;

                GetAndValidateStrongNameKey(out StrongNameKeyPair keyPair, out byte[] publicKey);

                try
                {
                    TypeLibImporterFlags flags = TypeLibImporterFlags.SafeArrayAsSystemArray | TypeLibImporterFlags.TransformDispRetVals;

                    if (_noClassMembers)
                    {
                        flags |= TypeLibImporterFlags.PreventClassMembers;
                    }

                    switch (_targetProcessorArchitecture)
                    {
                        case UtilitiesProcessorArchitecture.MSIL:
                            flags |= TypeLibImporterFlags.ImportAsAgnostic;
                            break;
                        case UtilitiesProcessorArchitecture.AMD64:
                            flags |= TypeLibImporterFlags.ImportAsX64;
                            break;
                        case UtilitiesProcessorArchitecture.IA64:
                            flags |= TypeLibImporterFlags.ImportAsItanium;
                            break;
                        case UtilitiesProcessorArchitecture.X86:
                            flags |= TypeLibImporterFlags.ImportAsX86;
                            break;
#if !MONO
                        case UtilitiesProcessorArchitecture.ARM:
                            flags |= TypeLibImporterFlags.ImportAsArm;
                            break;
#endif
                        default:
                            // Let the type importer decide.
                            break;
                    }

                    // Start the conversion process. We'll get callbacks on ITypeLibImporterNotifySink to resolve dependent refs.
                    assemblyBuilder = converter.ConvertTypeLibToAssembly(ReferenceInfo.typeLibPointer, wrapperPath,
                        flags, this, publicKey, keyPair, rootNamespace, null);
                }
                catch (COMException ex)
                {
                    if (!Silent)
                    {
                        Log.LogWarningWithCodeFromResources("ResolveComReference.ErrorCreatingWrapperAssembly", ItemName, ex.Message);
                    }

                    throw new ComReferenceResolutionException(ex);
                }

                // if we're done, and this is not a temporary wrapper, write it out to disk
                if (!HasTemporaryWrapper)
                {
                    WriteWrapperToDisk(assemblyBuilder, wrapperPath);
                }

                // store the wrapper info...
                wrapperInfo = new ComReferenceWrapperInfo
                {
                    path = (HasTemporaryWrapper) ? null : wrapperPath,
                    assembly = assemblyBuilder
                };
            }

            // ...and we're done!
            return generateWrapperSucceeded;
        }

        /*
         * Method:  WriteWrapperToDisk
         * 
         * Writes the generated wrapper out to disk. Should only be called for permanent wrappers.
         */
        private void WriteWrapperToDisk(AssemblyBuilder assemblyBuilder, string wrapperPath)
        {
            try
            {
                var wrapperFile = new FileInfo(wrapperPath);

                if (wrapperFile.Exists)
                {
                    wrapperFile.Delete();
                }

                switch (_targetProcessorArchitecture)
                {
                    case UtilitiesProcessorArchitecture.X86:
                        assemblyBuilder.Save
                            (
                                wrapperFile.Name,
                                PortableExecutableKinds.ILOnly | PortableExecutableKinds.Required32Bit,
                                ImageFileMachine.I386
                            );
                        break;
                    case UtilitiesProcessorArchitecture.AMD64:
                        assemblyBuilder.Save
                            (
                                wrapperFile.Name,
                                PortableExecutableKinds.ILOnly | PortableExecutableKinds.PE32Plus,
                                ImageFileMachine.AMD64
                            );
                        break;
                    case UtilitiesProcessorArchitecture.IA64:
                        assemblyBuilder.Save
                            (
                                wrapperFile.Name,
                                PortableExecutableKinds.ILOnly | PortableExecutableKinds.PE32Plus,
                                ImageFileMachine.IA64
                            );
                        break;
                    case UtilitiesProcessorArchitecture.ARM:
                        assemblyBuilder.Save
                            (
                                wrapperFile.Name,
                                PortableExecutableKinds.ILOnly | PortableExecutableKinds.Required32Bit,
                                ImageFileMachine.ARM
                            );
                        break;
                    case UtilitiesProcessorArchitecture.MSIL:
                    default:
                        // If no target processor architecture was passed, we assume MSIL; calling Save
                        // with no parameters should be equivalent to saving as ILOnly.  
                        assemblyBuilder.Save(wrapperFile.Name);
                        break;
                }

                // AssemblyBuilder doesn't always throw when it's supposed to write stuff to a non-writable 
                // network path. Make sure that the assembly actually got written to where we wanted it to.
                File.GetLastWriteTime(wrapperPath);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                if (!Silent)
                {
                    Log.LogWarningWithCodeFromResources("ResolveComReference.ErrorCreatingWrapperAssembly", ItemName, e.Message);
                }

                throw new ComReferenceResolutionException(e);
            }
        }

        #endregion

        #region ITypeLibImporterNotifySink Members

        /*
         * Method:  ITypeLibImporterNotifySink.ResolveRef
         * 
         * Implementation of ITypeLibImporterNotifySink.ResolveRef - this method is called by the NDP type lib converter 
         * to resolve dependencies.
         * We should never return null here - it's not documented as the proper way of failing dependency resolution.
         * Instead, we use an exception to abort the conversion process.
         */
        Assembly ITypeLibImporterNotifySink.ResolveRef(object objTypeLib)
        {
            // get attributes for our dependent typelib
            ITypeLib typeLib = (ITypeLib)objTypeLib;
            ComReference.GetTypeLibAttrForTypeLib(ref typeLib, out TYPELIBATTR attr);

            // call our callback to do the dirty work for us
            if (!ResolverCallback.ResolveComClassicReference(attr, base.OutputDirectory, null, null, out ComReferenceWrapperInfo wrapperInfo))
            {
                if (!Silent)
                {
                    Log.LogWarningWithCodeFromResources("ResolveComReference.FailedToResolveDependentComReference", attr.guid, attr.wMajorVerNum, attr.wMinorVerNum);
                }

                throw new ComReferenceResolutionException();
            }

            Debug.Assert(wrapperInfo.assembly != null, "Successfully resolved assembly cannot be null!");
            if (wrapperInfo.assembly == null)
            {
                throw new ComReferenceResolutionException();
            }

            if (!Silent)
            {
                Log.LogMessageFromResources(MessageImportance.Low, "ResolveComReference.ResolvedDependentComReference",
                    attr.guid, attr.wMajorVerNum, attr.wMinorVerNum, wrapperInfo.path);
            }

            Debug.Assert(wrapperInfo.assembly != null, "Expected a non-null wrapperInfo.assembly. It should have been loaded in GenerateWrapper if it was going to be necessary.");
            return wrapperInfo.assembly;
        }

        /*
         * Method:  ITypeLibImporterNotifySink.ReportEvent
         * 
         * Implementation of ITypeLibImporterNotifySink.ReportEvent - this method gets called by NDP type lib converter
         * to report various messages (like "type blahblah converted" or "failed to convert type blahblah").
         */
        void ITypeLibImporterNotifySink.ReportEvent(ImporterEventKind eventKind, int eventCode, string eventMsg)
        {
            if (!Silent)
            {
                if (eventKind == ImporterEventKind.ERROR_REFTOINVALIDTYPELIB)
                {
                    Log.LogWarningWithCodeFromResources("ResolveComReference.ResolutionWarning", ReferenceInfo.SourceItemSpec, ReferenceInfo.strippedTypeLibPath, eventMsg);
                }
                else if (eventKind == ImporterEventKind.NOTIF_CONVERTWARNING)
                {
                    Log.LogWarningWithCodeFromResources("ResolveComReference.ResolutionWarning", ReferenceInfo.SourceItemSpec, ReferenceInfo.strippedTypeLibPath, eventMsg);
                }
                else if (eventKind == ImporterEventKind.NOTIF_TYPECONVERTED)
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "ResolveComReference.ResolutionMessage", ReferenceInfo.SourceItemSpec, ReferenceInfo.strippedTypeLibPath, eventMsg);
                }
                else
                {
                    Debug.Assert(false, "Unknown ImporterEventKind value");
                    Log.LogMessageFromResources(MessageImportance.Low, "ResolveComReference.ResolutionMessage", ReferenceInfo.SourceItemSpec, ReferenceInfo.strippedTypeLibPath, eventMsg);
                }
            }
            else
            {
                if (eventKind != ImporterEventKind.ERROR_REFTOINVALIDTYPELIB && eventKind != ImporterEventKind.NOTIF_CONVERTWARNING && eventKind != ImporterEventKind.NOTIF_TYPECONVERTED)
                {
                    Debug.Assert(false, "Unknown ImporterEventKind value");
                }
            }
        }

        #endregion
    }
}
