// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Reflection.Metadata;
using System.Reflection;

namespace Microsoft.NET.Build.Tasks
{
    public class PrepareForReadyToRunCompilation : TaskBase
    {
        public ITaskItem[] FilesToPublish { get; set; }
        public string[] ExcludeList { get; set; }
        public bool EmitSymbols { get; set; }

        [Required]
        public string OutputPath { get; set; }
        [Required]
        public ITaskItem[] RuntimePacks { get; set; }
        [Required]
        public ITaskItem[] KnownFrameworkReferences { get; set; }
        [Required]
        public string RuntimeGraphPath { get; set; }
        [Required]
        public string NETCoreSdkRuntimeIdentifier { get; set; }

        [Output]
        public ITaskItem CrossgenTool { get; set; }

        // Output lists of files to compile. Currently crossgen has to run in two steps, the first to generate the R2R image
        // and the second to create native PDBs for the compiled images (the output of the first step is an input to the second step)
        [Output]
        public ITaskItem[] ReadyToRunCompileList => _compileList.ToArray();
        [Output]
        public ITaskItem[] ReadyToRunSymbolsCompileList => _symbolsCompileList.ToArray();

        // Output files to publish after compilation. These lists are equivalent to the input list, but contain the new
        // paths to the compiled R2R images and native PDBs.
        [Output]
        public ITaskItem[] ReadyToRunFilesToPublish => _r2rFiles.ToArray();

        private List<ITaskItem> _compileList = new List<ITaskItem>();
        private List<ITaskItem> _symbolsCompileList = new List<ITaskItem>();

        private List<ITaskItem> _r2rFiles = new List<ITaskItem>();

        private string _runtimeIdentifier;
        private string _packagePath;
        private string _crossgenPath;
        private string _clrjitPath;
        private string _diasymreaderPath;

        private Architecture _targetArchitecture;

        protected override void ExecuteCore()
        {
            // Get the list of runtime identifiers that we support and can target
            ITaskItem frameworkRef = KnownFrameworkReferences.Where(item => String.Compare(item.ItemSpec, "Microsoft.NETCore.App", true) == 0).SingleOrDefault();
            string supportedRuntimeIdentifiers = frameworkRef == null ? null : frameworkRef.GetMetadata("RuntimePackRuntimeIdentifiers");

            // Get information on the runtime package used for the current target
            ITaskItem frameworkPack = RuntimePacks.Where(pack => pack.ItemSpec.EndsWith(".Microsoft.NETCore.App", StringComparison.InvariantCultureIgnoreCase)).SingleOrDefault();
            _runtimeIdentifier = frameworkPack == null ? null : frameworkPack.GetMetadata(MetadataKeys.RuntimeIdentifier);
            _packagePath = frameworkPack == null ? null : frameworkPack.GetMetadata(MetadataKeys.PackageDirectory);

            var runtimeGraph = new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath);
            var supportedRIDsList = supportedRuntimeIdentifiers == null ? Array.Empty<string>() : supportedRuntimeIdentifiers.Split(';');

            // Get the best RID for the host machine, which will be used to validate that we can run crossgen for the target platform and architecture
            string hostRuntimeIdentifier = NuGetUtils.GetBestMatchingRid(
                runtimeGraph,
                NETCoreSdkRuntimeIdentifier,
                supportedRIDsList,
                out bool wasInGraph);

            if (hostRuntimeIdentifier == null || _runtimeIdentifier == null || _packagePath == null)
            {
                Log.LogError(Strings.ReadyToRunNoValidRuntimePackageError);
                return;
            }

            if (!ExtractTargetPlatformAndArchitecture(_runtimeIdentifier, out string targetPlatform, out _targetArchitecture) ||
                !ExtractTargetPlatformAndArchitecture(hostRuntimeIdentifier, out string hostPlatform, out Architecture hostArchitecture) ||
                targetPlatform != hostPlatform)
            {
                Log.LogError(Strings.ReadyToRunTargetNotSuppotedError);
                return;
            }

            if (!GetCrossgenComponentsPaths() || !File.Exists(_crossgenPath) || !File.Exists(_clrjitPath))
            {
                Log.LogError(Strings.ReadyToRunTargetNotSuppotedError);
                return;
            }

            // Create tool task item
            CrossgenTool = new TaskItem(_crossgenPath);
            CrossgenTool.SetMetadata("JitPath", _clrjitPath);
            if (!String.IsNullOrEmpty(_diasymreaderPath))
                CrossgenTool.SetMetadata("DiaSymReader", _diasymreaderPath);

            // Process input lists of files
            ProcessInputFileList(FilesToPublish, _compileList, _symbolsCompileList, _r2rFiles);
        }

        void ProcessInputFileList(ITaskItem[] inputFiles, List<ITaskItem> imageCompilationList, List<ITaskItem> symbolsCompilationList, List<ITaskItem> r2rFilesPublishList)
        {
            if (inputFiles == null)
            {
                return;
            }

            foreach (var file in inputFiles)
            {
                if (!InputFileEligibleForCompilation(file))
                    continue;

                var outputR2RImageRelativePath = file.GetMetadata(MetadataKeys.RelativePath);
                var outputR2RImage = Path.Combine(OutputPath, outputR2RImageRelativePath);

                // This TaskItem is the IL->R2R entry, for an input assembly that needs to be compiled into a R2R image. This will be used as
                // an input to the ReadyToRunCompiler task
                TaskItem r2rCompilationEntry = new TaskItem(file);
                r2rCompilationEntry.SetMetadata("OutputR2RImage", outputR2RImage);
                r2rCompilationEntry.RemoveMetadata(MetadataKeys.OriginalItemSpec);
                imageCompilationList.Add(r2rCompilationEntry);

                // This TaskItem corresponds to the output R2R image. It is equivalent to the input TaskItem, only the ItemSpec for it points to the new path
                // for the newly created R2R image
                TaskItem r2rFileToPublish = new TaskItem(file);
                r2rFileToPublish.ItemSpec = outputR2RImage;
                r2rFileToPublish.RemoveMetadata(MetadataKeys.OriginalItemSpec);
                r2rFilesPublishList.Add(r2rFileToPublish);

                // Note: ReadyToRun PDB/Map files are not needed for debugging. They are only used for profiling, therefore the default behavior is to not generate them
                // unless an explicit PublishReadyToRunEmitSymbols flag is enabled by the app developer. There is also another way to profile that the runtime supports, which does
                // not rely on the native PDBs/Map files, so creating them is really an opt-in option, typically used by advanced users.
                // For debugging, only the IL PDBs are required.
                if (EmitSymbols)
                {
                    string outputPDBImageRelativePath = null, outputPDBImage = null, createPDBCommand = null;

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _diasymreaderPath != null && File.Exists(_diasymreaderPath))
                    {
                        outputPDBImage = Path.ChangeExtension(outputR2RImage, "ni.pdb");
                        outputPDBImageRelativePath = Path.ChangeExtension(outputR2RImageRelativePath, "ni.pdb");
                        createPDBCommand = $"/CreatePDB \"{Path.GetDirectoryName(outputPDBImage)}\"";
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        using (FileStream fs = new FileStream(file.ItemSpec, FileMode.Open, FileAccess.Read))
                        {
                            PEReader pereader = new PEReader(fs);
                            MetadataReader mdReader = pereader.GetMetadataReader();
                            Guid mvid = mdReader.GetGuid(mdReader.GetModuleDefinition().Mvid);

                            outputPDBImage = Path.ChangeExtension(file.ItemSpec, "ni.{" + mvid + "}.map");
                            outputPDBImageRelativePath = Path.ChangeExtension(outputR2RImageRelativePath, "ni.{" + mvid + "}.map");
                            createPDBCommand = $"/CreatePerfMap \"{Path.GetDirectoryName(outputPDBImage)}\"";
                        }
                    }

                    if (outputPDBImage != null)
                    {
                        // This TaskItem is the R2R->R2RPDB entry, for a R2R image that was just created, and for which we need to create native PDBs. This will be used as
                        // an input to the ReadyToRunCompiler task
                        TaskItem pdbCompilationEntry = new TaskItem(file);
                        pdbCompilationEntry.ItemSpec = outputR2RImage;
                        pdbCompilationEntry.SetMetadata("OutputPDBImage", outputPDBImage);
                        pdbCompilationEntry.SetMetadata("CreatePDBCommand", createPDBCommand);
                        symbolsCompilationList.Add(pdbCompilationEntry);

                        // This TaskItem corresponds to the output PDB image. It is equivalent to the input TaskItem, only the ItemSpec for it points to the new path
                        // for the newly created PDB image.
                        TaskItem r2rSymbolsFileToPublish = new TaskItem(file);
                        r2rSymbolsFileToPublish.ItemSpec = outputPDBImage;
                        r2rSymbolsFileToPublish.SetMetadata(MetadataKeys.RelativePath, outputPDBImageRelativePath);
                        r2rSymbolsFileToPublish.RemoveMetadata(MetadataKeys.OriginalItemSpec);
                        r2rFilesPublishList.Add(r2rSymbolsFileToPublish);
                    }
                }
            }
        }

        bool InputFileEligibleForCompilation(ITaskItem file)
        {
            // Check if the file is explicitly excluded from being compiled
            if (ExcludeList != null)
            {
                foreach (var item in ExcludeList)
                {
                    if (String.Compare(Path.GetFileName(file.ItemSpec), item, true) == 0)
                    {
                        return false;
                    }
                }
            }

            // Check to see if this is a valid ILOnly image that we can compile
            using (FileStream fs = new FileStream(file.ItemSpec, FileMode.Open, FileAccess.Read))
            {
                try
                {
                    using (var pereader = new PEReader(fs))
                    {
                        if (!pereader.HasMetadata)
                            return false;

                        if ((pereader.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) != CorFlags.ILOnly)
                            return false;

                        MetadataReader mdReader = pereader.GetMetadataReader();
                        if (!mdReader.IsAssembly)
                        {
                            return false;
                        }

                        // Skip reference assemblies and assemblies that reference winmds
                        if (ReferencesWinMD(mdReader) || IsReferenceAssembly(mdReader) || !HasILCode(pereader, mdReader))
                        {
                            return false;
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    // Not a valid assembly file
                    return false;
                }
            }

            return true;
        }

        private bool IsReferenceAssembly(MetadataReader mdReader)
        {
            foreach (var attributeHandle in mdReader.GetAssemblyDefinition().GetCustomAttributes())
            {
                EntityHandle attributeCtor = mdReader.GetCustomAttribute(attributeHandle).Constructor;

                StringHandle attributeTypeName = default;
                StringHandle attributeTypeNamespace = default;

                if (attributeCtor.Kind == HandleKind.MemberReference)
                {
                    EntityHandle attributeMemberParent = mdReader.GetMemberReference((MemberReferenceHandle)attributeCtor).Parent;
                    if (attributeMemberParent.Kind == HandleKind.TypeReference)
                    {
                        TypeReference attributeTypeRef = mdReader.GetTypeReference((TypeReferenceHandle)attributeMemberParent);
                        attributeTypeName = attributeTypeRef.Name;
                        attributeTypeNamespace = attributeTypeRef.Namespace;
                    }
                }
                else if (attributeCtor.Kind == HandleKind.MethodDefinition)
                {
                    TypeDefinitionHandle attributeTypeDefHandle = mdReader.GetMethodDefinition((MethodDefinitionHandle)attributeCtor).GetDeclaringType();
                    TypeDefinition attributeTypeDef = mdReader.GetTypeDefinition(attributeTypeDefHandle);
                    attributeTypeName = attributeTypeDef.Name;
                    attributeTypeNamespace = attributeTypeDef.Namespace;
                }

                if (!attributeTypeName.IsNil &&
                    !attributeTypeNamespace.IsNil &&
                    mdReader.StringComparer.Equals(attributeTypeName, "ReferenceAssemblyAttribute") &&
                    mdReader.StringComparer.Equals(attributeTypeNamespace, "System.Runtime.CompilerServices"))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ReferencesWinMD(MetadataReader mdReader)
        {
            foreach (var assemblyRefHandle in mdReader.AssemblyReferences)
            {
                AssemblyReference assemblyRef = mdReader.GetAssemblyReference(assemblyRefHandle);
                if ((assemblyRef.Flags & AssemblyFlags.WindowsRuntime) == AssemblyFlags.WindowsRuntime)
                    return true;
            }

            return false;
        }

        private bool HasILCode(PEReader peReader, MetadataReader mdReader)
        {
            foreach (var methoddefHandle in mdReader.MethodDefinitions)
            {
                MethodDefinition methodDef = mdReader.GetMethodDefinition(methoddefHandle);
                if (methodDef.RelativeVirtualAddress > 0)
                {
                    return true;
                }
            }

            return false;
        }

        bool ExtractTargetPlatformAndArchitecture(string runtimeIdentifier, out string platform, out Architecture architecture)
        {
            platform = null;
            architecture = default;

            int separator = runtimeIdentifier.LastIndexOf('-');
            if (separator < 0 || separator >= runtimeIdentifier.Length)
            {
                return false;
            }

            platform = runtimeIdentifier.Substring(0, separator).ToLowerInvariant();
            string architectureStr = runtimeIdentifier.Substring(separator + 1).ToLowerInvariant();

            switch (architectureStr)
            {
                case "arm":
                    architecture = Architecture.Arm;
                    break;
                case "arm64":
                    architecture = Architecture.Arm64;
                    break;
                case "x64":
                    architecture = Architecture.X64;
                    break;
                case "x86":
                    architecture = Architecture.X86;
                    break;
                default:
                    return false;
            }

            return true;
        }

        bool GetCrossgenComponentsPaths()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (_targetArchitecture == Architecture.Arm)
                {
                    _crossgenPath = Path.Combine(_packagePath, "tools", "x86_arm", "crossgen.exe");
                    _clrjitPath = Path.Combine(_packagePath, "runtimes", "x86_arm", "native", "clrjit.dll");
                    _diasymreaderPath = Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native", "Microsoft.DiaSymReader.Native.x86.dll");
                }
                else if (_targetArchitecture == Architecture.Arm64)
                {
                    // We only have 64-bit hosted compilers for ARM64.
                    if (RuntimeInformation.OSArchitecture != Architecture.X64)
                        return false;

                    _crossgenPath = Path.Combine(_packagePath, "tools", "x64_arm64", "crossgen.exe");
                    _clrjitPath = Path.Combine(_packagePath, "runtimes", "x64_arm64", "native", "clrjit.dll");
                    _diasymreaderPath = Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native", "Microsoft.DiaSymReader.Native.amd64.dll");
                }
                else
                {
                    _crossgenPath = Path.Combine(_packagePath, "tools", "crossgen.exe");
                    _clrjitPath = Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native", "clrjit.dll");
                    if (_targetArchitecture == Architecture.X64)
                        _diasymreaderPath = Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native", "Microsoft.DiaSymReader.Native.amd64.dll");
                    else
                        _diasymreaderPath = Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native", "Microsoft.DiaSymReader.Native.x86.dll");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (_targetArchitecture == Architecture.Arm || _targetArchitecture == Architecture.Arm64)
                {
                    // We only have x64 hosted crossgen for both ARM target architectures
                    if (RuntimeInformation.OSArchitecture != Architecture.X64)
                        return false;

                    string xarchPath = (_targetArchitecture == Architecture.Arm ? "x64_arm" : "x64_arm64");
                    _crossgenPath = Path.Combine(_packagePath, "tools", xarchPath, "crossgen");
                    _clrjitPath = Path.Combine(_packagePath, "runtimes", xarchPath, "native", "libclrjit.so");
                }
                else
                {
                    _crossgenPath = Path.Combine(_packagePath, "tools", "crossgen");
                    _clrjitPath = Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native", "libclrjit.so");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Only x64 supported for OSX
                if (_targetArchitecture != Architecture.X64 || RuntimeInformation.OSArchitecture != Architecture.X64)
                    return false;

                _crossgenPath = Path.Combine(_packagePath, "tools", "crossgen");
                _clrjitPath = Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native", "libclrjit.dylib");
            }
            else
            {
                // Unknown platform
                return false;
            }

            return true;
        }
    }
}
