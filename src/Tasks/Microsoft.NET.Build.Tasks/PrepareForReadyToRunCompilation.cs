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
        public ITaskItem[] Assemblies { get; set; }
        public string[] ExcludeList { get; set; }
        public bool EmitSymbols { get; set; }
        public bool ReadyToRunUseCrossgen2 { get; set; }

        [Required]
        public string OutputPath { get; set; }
        [Required]
        public ITaskItem[] RuntimePacks { get; set; }
        public ITaskItem[] Crossgen2Packs { get; set; }
        [Required]
        public ITaskItem[] TargetingPacks { get; set; }
        [Required]
        public string RuntimeGraphPath { get; set; }
        [Required]
        public string NETCoreSdkRuntimeIdentifier { get; set; }
        [Required]
        public bool IncludeSymbolsInSingleFile { get; set; }

        [Output]
        public ITaskItem CrossgenTool { get; set; }
        [Output]
        public ITaskItem Crossgen2Tool { get; set; }

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

        [Output]
        public ITaskItem[] ReadyToRunAssembliesToReference => _r2rReferences.ToArray();

        internal struct CrossgenToolInfo
        {
            public string ToolPath;
            public string PackagePath;
            public string ClrJitPath;
            public string DiaSymReaderPath;
        }

        private List<ITaskItem> _compileList = new List<ITaskItem>();
        private List<ITaskItem> _symbolsCompileList = new List<ITaskItem>();
        private List<ITaskItem> _r2rFiles = new List<ITaskItem>();
        private List<ITaskItem> _r2rReferences = new List<ITaskItem>();

        private ITaskItem _runtimePack;
        private ITaskItem _crossgen2Pack;
        private string _targetRuntimeIdentifier;
        private string _hostRuntimeIdentifier;

        private CrossgenToolInfo _crossgenTool;
        private CrossgenToolInfo _crossgen2Tool;

        private Architecture _targetArchitecture;

        protected override void ExecuteCore()
        {
            _runtimePack = GetNETCoreAppRuntimePack();
            _crossgen2Pack = Crossgen2Packs?.FirstOrDefault();
            _targetRuntimeIdentifier = _runtimePack?.GetMetadata(MetadataKeys.RuntimeIdentifier);

            // Get the list of runtime identifiers that we support and can target 
            ITaskItem targetingPack = GetNETCoreAppTargetingPack();
            string supportedRuntimeIdentifiers = targetingPack?.GetMetadata(MetadataKeys.RuntimePackRuntimeIdentifiers);

            var runtimeGraph = new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath);
            var supportedRIDsList = supportedRuntimeIdentifiers == null ? Array.Empty<string>() : supportedRuntimeIdentifiers.Split(';');

            // Get the best RID for the host machine, which will be used to validate that we can run crossgen for the target platform and architecture
            _hostRuntimeIdentifier = NuGetUtils.GetBestMatchingRid(
                runtimeGraph,
                NETCoreSdkRuntimeIdentifier,
                supportedRIDsList,
                out bool wasInGraph);

            if (_hostRuntimeIdentifier == null || _targetRuntimeIdentifier == null)
            {
                Log.LogError(Strings.ReadyToRunNoValidRuntimePackageError);
                return;
            }

            if (ReadyToRunUseCrossgen2)
            {
                if (!ValidateCrossgen2Support())
                {
                    return;
                }

                // NOTE: Crossgen2 does not yet currently support emitting native symbols, and until this feature
                // is implemented, we will use crossgen for it. This should go away in the future when crossgen2 supports the feature.
                if (EmitSymbols && !ValidateCrossgenSupport())
                {
                    return;
                }
            }
            else
            {
                if (!ValidateCrossgenSupport())
                {
                    return;
                }
            }

            // Future: check DiaSymReaderPath in the _crossgen2Tool info when crossgen2 starts supporting emitting native symbols
            bool hasValidDiaSymReaderLib = String.IsNullOrEmpty(_crossgenTool.DiaSymReaderPath) ? false : File.Exists(_crossgenTool.DiaSymReaderPath);

            // Process input lists of files
            ProcessInputFileList(Assemblies, _compileList, _symbolsCompileList, _r2rFiles, _r2rReferences, hasValidDiaSymReaderLib);
        }

        private bool ValidateCrossgenSupport()
        {
            _crossgenTool.PackagePath = _runtimePack?.GetMetadata(MetadataKeys.PackageDirectory);
            if (_crossgenTool.PackagePath == null)
            {
                Log.LogError(Strings.ReadyToRunNoValidRuntimePackageError);
                return false;
            }

            if (!ExtractTargetPlatformAndArchitecture(_targetRuntimeIdentifier, out string targetPlatform, out _targetArchitecture) ||
                !ExtractTargetPlatformAndArchitecture(_hostRuntimeIdentifier, out string hostPlatform, out Architecture hostArchitecture) ||
                targetPlatform != hostPlatform)
            {
                Log.LogError(Strings.ReadyToRunTargetNotSupportedError);
                return false;
            }

            if (!GetCrossgenComponentsPaths())
            {
                Log.LogError(Strings.ReadyToRunTargetNotSupportedError);
                return false;
            }

            // Create tool task item
            CrossgenTool = new TaskItem(_crossgenTool.ToolPath);
            CrossgenTool.SetMetadata("JitPath", _crossgenTool.ClrJitPath);
            if (!String.IsNullOrEmpty(_crossgenTool.DiaSymReaderPath))
            {
                CrossgenTool.SetMetadata("DiaSymReader", _crossgenTool.DiaSymReaderPath);
            }

            return true;
        }

        private bool ValidateCrossgen2Support()
        {
            _crossgen2Tool.PackagePath = _crossgen2Pack?.GetMetadata(MetadataKeys.PackageDirectory);
            if (_crossgen2Tool.PackagePath == null)
            {
                Log.LogError(Strings.ReadyToRunNoValidRuntimePackageError);
                return false;
            }

            // Crossgen2 only supports the following host->target compilation scenarios in netcoreapp5.0:
            //      win-x64 -> win-x64
            //      linux-x64 -> linux-x64
            //      linux-musl-x64 -> linux-musl-x64
            if (_targetRuntimeIdentifier != _hostRuntimeIdentifier)
            {
                Log.LogError(Strings.ReadyToRunTargetNotSupportedError);
                return false;
            }

            if (!GetCrossgen2ComponentsPaths())
            {
                Log.LogError(Strings.ReadyToRunTargetNotSupportedError);
                return false;
            }

            // Create tool task item
            Crossgen2Tool = new TaskItem(_crossgen2Tool.ToolPath);
            Crossgen2Tool.SetMetadata("JitPath", _crossgen2Tool.ClrJitPath);
            if (!String.IsNullOrEmpty(_crossgen2Tool.DiaSymReaderPath))
            {
                Crossgen2Tool.SetMetadata("DiaSymReader", _crossgen2Tool.DiaSymReaderPath);
            }

            return true;
        }

        private ITaskItem GetNETCoreAppRuntimePack()
        {
            return GetNETCoreAppPack(RuntimePacks, MetadataKeys.FrameworkName);
        }

        private ITaskItem GetNETCoreAppTargetingPack()
        {
            return GetNETCoreAppPack(TargetingPacks, MetadataKeys.RuntimeFrameworkName);
        }

        private static ITaskItem GetNETCoreAppPack(ITaskItem[] packs, string metadataKey)
        { 
            return packs.SingleOrDefault(
                pack => pack.GetMetadata(metadataKey)
                            .Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase));
        }

        private void ProcessInputFileList(
            ITaskItem[] inputFiles, 
            List<ITaskItem> imageCompilationList,
            List<ITaskItem> symbolsCompilationList,
            List<ITaskItem> r2rFilesPublishList, 
            List<ITaskItem> r2rReferenceList,
            bool hasValidDiaSymReaderLib)
        {
            if (inputFiles == null)
            {
                return;
            }
            
            var exclusionSet = ExcludeList == null ? null : new HashSet<string>(ExcludeList, StringComparer.OrdinalIgnoreCase);

            foreach (var file in inputFiles)
            {
                var eligibility = GetInputFileEligibility(file, exclusionSet);

                if (eligibility == Eligibility.None)
                {
                    continue;
                }

                r2rReferenceList.Add(file);

                if (eligibility == Eligibility.ReferenceOnly)
                {
                    continue;
                }

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

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && hasValidDiaSymReaderLib)
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

                            outputPDBImage = Path.ChangeExtension(outputR2RImage, "ni.{" + mvid + "}.map");
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
                        if (!IncludeSymbolsInSingleFile)
                        {
                            r2rSymbolsFileToPublish.SetMetadata(MetadataKeys.ExcludeFromSingleFile, "true");
                        }

                        r2rFilesPublishList.Add(r2rSymbolsFileToPublish);
                    }
                }
            }
        }

        private enum Eligibility
        {
            None,
            ReferenceOnly,
            CompileAndReference
        };

        private static Eligibility GetInputFileEligibility(ITaskItem file, HashSet<string> exclusionSet)
        {
            // Check to see if this is a valid ILOnly image that we can compile
            using (FileStream fs = new FileStream(file.ItemSpec, FileMode.Open, FileAccess.Read))
            {
                try
                {
                    using (var pereader = new PEReader(fs))
                    {
                        if (!pereader.HasMetadata)
                        {
                            return Eligibility.None;
                        }

                        MetadataReader mdReader = pereader.GetMetadataReader();
                        if (!mdReader.IsAssembly)
                        {
                            return Eligibility.None;
                        }
                        
                        if (IsReferenceAssembly(mdReader))
                        {
                            // crossgen can only take implementation assemblies, even as references
                            return Eligibility.None;
                        }

                        if ((pereader.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) != CorFlags.ILOnly)
                        {
                            return Eligibility.ReferenceOnly;
                        }

                        if (file.HasMetadataValue(MetadataKeys.ReferenceOnly, "true"))
                        {
                            return Eligibility.ReferenceOnly;
                        }

                        if (exclusionSet != null && exclusionSet.Contains(Path.GetFileName(file.ItemSpec)))
                        {
                            return Eligibility.ReferenceOnly;
                        }

                        // save these most expensive checks for last. We don't want to scan all references for IL code
                        if (ReferencesWinMD(mdReader) || !HasILCode(pereader, mdReader))
                        {
                            return Eligibility.ReferenceOnly;
                        }

                        return Eligibility.CompileAndReference;
                    }
                }
                catch (BadImageFormatException)
                {
                    // Not a valid assembly file
                    return Eligibility.None;
                }
            }
        }

        private static bool IsReferenceAssembly(MetadataReader mdReader)
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

        private static bool ReferencesWinMD(MetadataReader mdReader)
        {
            foreach (var assemblyRefHandle in mdReader.AssemblyReferences)
            {
                AssemblyReference assemblyRef = mdReader.GetAssemblyReference(assemblyRefHandle);
                if ((assemblyRef.Flags & AssemblyFlags.WindowsRuntime) == AssemblyFlags.WindowsRuntime)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasILCode(PEReader peReader, MetadataReader mdReader)
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

        private static bool ExtractTargetPlatformAndArchitecture(string runtimeIdentifier, out string platform, out Architecture architecture)
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

        private bool GetCrossgenComponentsPaths()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (_targetArchitecture == Architecture.Arm)
                {
                    if (RuntimeInformation.OSArchitecture == _targetArchitecture)
                    {
                        // We can run native arm32 bits on an arm64 host in WOW mode
                        _crossgenTool.ToolPath = Path.Combine(_crossgenTool.PackagePath, "tools", "crossgen.exe");
                        _crossgenTool.ClrJitPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", _targetRuntimeIdentifier, "native", "clrjit.dll");
                        _crossgenTool.DiaSymReaderPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", _targetRuntimeIdentifier, "native", "Microsoft.DiaSymReader.Native.arm.dll");
                    }
                    else
                    {
                        // We can use the x86-hosted crossgen compiler to target ARM
                        _crossgenTool.ToolPath = Path.Combine(_crossgenTool.PackagePath, "tools", "x86_arm", "crossgen.exe");
                        _crossgenTool.ClrJitPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", "x86_arm", "native", "clrjit.dll");
                        _crossgenTool.DiaSymReaderPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", _targetRuntimeIdentifier, "native", "Microsoft.DiaSymReader.Native.x86.dll");
                    }
                }
                else if (_targetArchitecture == Architecture.Arm64)
                {
                    if (RuntimeInformation.OSArchitecture == _targetArchitecture)
                    {
                        _crossgenTool.ToolPath = Path.Combine(_crossgenTool.PackagePath, "tools", "crossgen.exe");
                        _crossgenTool.ClrJitPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", _targetRuntimeIdentifier, "native", "clrjit.dll");
                        _crossgenTool.DiaSymReaderPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", _targetRuntimeIdentifier, "native", "Microsoft.DiaSymReader.Native.arm64.dll");
                    }
                    else
                    {
                        // We only have 64-bit hosted compilers for ARM64.
                        if (RuntimeInformation.OSArchitecture != Architecture.X64)
                        {
                            return false;
                        }

                        _crossgenTool.ToolPath = Path.Combine(_crossgenTool.PackagePath, "tools", "x64_arm64", "crossgen.exe");
                        _crossgenTool.ClrJitPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", "x64_arm64", "native", "clrjit.dll");
                        _crossgenTool.DiaSymReaderPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", _targetRuntimeIdentifier, "native", "Microsoft.DiaSymReader.Native.amd64.dll");
                    }
                }
                else
                {
                    _crossgenTool.ToolPath = Path.Combine(_crossgenTool.PackagePath, "tools", "crossgen.exe");
                    _crossgenTool.ClrJitPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", _targetRuntimeIdentifier, "native", "clrjit.dll");
                    if (_targetArchitecture == Architecture.X64)
                    {
                        _crossgenTool.DiaSymReaderPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", _targetRuntimeIdentifier, "native", "Microsoft.DiaSymReader.Native.amd64.dll");
                    }
                    else
                    {
                        _crossgenTool.DiaSymReaderPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", _targetRuntimeIdentifier, "native", "Microsoft.DiaSymReader.Native.x86.dll");
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (_targetArchitecture == Architecture.Arm || _targetArchitecture == Architecture.Arm64)
                {
                    if (RuntimeInformation.OSArchitecture == _targetArchitecture)
                    {
                        _crossgenTool.ToolPath = Path.Combine(_crossgenTool.PackagePath, "tools", "crossgen");
                        _crossgenTool.ClrJitPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", _targetRuntimeIdentifier, "native", "libclrjit.so");
                    }
                    else if (RuntimeInformation.OSArchitecture == Architecture.X64)
                    {
                        string xarchPath = (_targetArchitecture == Architecture.Arm ? "x64_arm" : "x64_arm64");
                        _crossgenTool.ToolPath = Path.Combine(_crossgenTool.PackagePath, "tools", xarchPath, "crossgen");
                        _crossgenTool.ClrJitPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", xarchPath, "native", "libclrjit.so");
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    _crossgenTool.ToolPath = Path.Combine(_crossgenTool.PackagePath, "tools", "crossgen");
                    _crossgenTool.ClrJitPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", _targetRuntimeIdentifier, "native", "libclrjit.so");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Only x64 supported for OSX
                if (_targetArchitecture != Architecture.X64 || RuntimeInformation.OSArchitecture != Architecture.X64)
                {
                    return false;
                }

                _crossgenTool.ToolPath = Path.Combine(_crossgenTool.PackagePath, "tools", "crossgen");
                _crossgenTool.ClrJitPath = Path.Combine(_crossgenTool.PackagePath, "runtimes", _targetRuntimeIdentifier, "native", "libclrjit.dylib");
            }
            else
            {
                // Unknown platform
                return false;
            }

            return File.Exists(_crossgenTool.ToolPath) && File.Exists(_crossgenTool.ClrJitPath);
        }

        private bool GetCrossgen2ComponentsPaths()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _crossgen2Tool.ToolPath = Path.Combine(_crossgen2Tool.PackagePath, "tools", "crossgen2.exe");
                _crossgen2Tool.ClrJitPath = Path.Combine(_crossgen2Tool.PackagePath, "tools", "clrjitilc.dll");
            }
            else
            {
                _crossgen2Tool.ToolPath = Path.Combine(_crossgen2Tool.PackagePath, "tools", "crossgen2");
                _crossgen2Tool.ClrJitPath = Path.Combine(_crossgen2Tool.PackagePath, "tools", "libclrjitilc.so");
            }

            return File.Exists(_crossgen2Tool.ToolPath) && File.Exists(_crossgen2Tool.ClrJitPath);
        }
    }
}
