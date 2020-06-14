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
    public class ResolveReadyToRunCompilers : TaskBase
    {
        public bool EmitSymbols { get; set; }
        public bool ReadyToRunUseCrossgen2 { get; set; }

        [Required]
        public ITaskItem[] RuntimePacks { get; set; }
        public ITaskItem[] Crossgen2Packs { get; set; }
        [Required]
        public ITaskItem[] TargetingPacks { get; set; }
        [Required]
        public string RuntimeGraphPath { get; set; }
        [Required]
        public string NETCoreSdkRuntimeIdentifier { get; set; }

        [Output]
        public ITaskItem CrossgenTool { get; set; }
        [Output]
        public ITaskItem Crossgen2Tool { get; set; }

        internal struct CrossgenToolInfo
        {
            public string ToolPath;
            public string PackagePath;
            public string ClrJitPath;
            public string DiaSymReaderPath;
        }

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
                out _);

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

            // Crossgen2 only supports the following host->target compilation scenarios in net5.0:
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
