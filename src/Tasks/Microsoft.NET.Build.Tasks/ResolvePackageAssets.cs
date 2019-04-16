// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Resolve package assets from projects.assets.json into MSBuild items.
    ///
    /// Optimized for fast incrementality using an intermediate, binary assets.cache
    /// file that contains only the data that is actually returned for the current
    /// TFM/RID/etc. and written in a format that is easily decoded to ITaskItem
    /// arrays without undue allocation.
    /// </summary>
    public sealed class ResolvePackageAssets : TaskBase
    {
        /// <summary>
        /// Path to assets.json.
        /// </summary>
        public string ProjectAssetsFile { get; set; }

        /// <summary>
        /// Path to assets.cache file.
        /// </summary>
        [Required]
        public string ProjectAssetsCacheFile { get; set; }

        /// <summary>
        /// Path to project file (.csproj|.vbproj|.fsproj)
        /// </summary>
        [Required]
        public string ProjectPath { get; set; }

        /// <summary>
        /// TFM to use for compile-time assets.
        /// </summary>
        [Required]
        public string TargetFrameworkMoniker { get; set; }

        /// <summary>
        /// RID to use for runtime assets (may be empty)
        /// </summary>
        public string RuntimeIdentifier { get; set; }

        /// <summary>
        /// The platform library name for resolving copy local assets.
        /// </summary>
        public string PlatformLibraryName { get; set; }

        /// <summary>
        /// The runtime frameworks for resolving copy local assets.
        /// </summary>
        public ITaskItem[] RuntimeFrameworks { get; set; }

        /// <summary>
        /// Whether or not the the copy local is for a self-contained application.
        /// </summary>
        public bool IsSelfContained { get; set; }

        /// <summary>
        /// The languages to filter the resource assmblies for.
        /// </summary>
        public ITaskItem[] SatelliteResourceLanguages { get; set; }

        /// <summary>
        /// Do not write package assets cache to disk nor attempt to read previous cache from disk.
        /// </summary>
        public bool DisablePackageAssetsCache { get; set; }

        /// <summary>
        /// Do not generate transitive project references.
        /// </summary>
        public bool DisableTransitiveProjectReferences { get; set; }

        /// <summary>
        /// Do not add references to framework assemblies as specified by packages.
        /// </summary>
        public bool DisableFrameworkAssemblies { get; set; }

        /// <summary>
        /// Do not resolve runtime targets.
        /// </summary>
        public bool DisableRuntimeTargets { get; set; }

        /// <summary>
        /// Log messages from assets log to build error/warning/message.
        /// </summary>
        public bool EmitAssetsLogMessages { get; set; }

        /// <summary>
        /// Set ExternallyResolved=true metadata on reference items to indicate to MSBuild ResolveAssemblyReferences
        /// that these are resolved by an external system (in this case nuget) and therefore several steps can be
        /// skipped as an optimization.
        /// </summary>
        public bool MarkPackageReferencesAsExternallyResolved { get; set; }

        /// <summary>
        /// Project language ($(ProjectLanguage) in common targets -"VB" or "C#" or "F#" ).
        /// Impacts applicability of analyzer assets.
        /// </summary>
        public string ProjectLanguage { get; set; }

        /// <summary>
        /// Check that there is at least one package dependency in the RID graph that is not in the RID-agnostic graph.
        /// Used as a heuristic to detect invalid RIDs.
        /// </summary>
        public bool EnsureRuntimePackageDependencies { get; set; }

        /// <summary>
        /// Specifies whether to validate that the version of the implicit platform packages in the assets
        /// file matches the version specified by <see cref="ExpectedPlatformPackages"/>
        /// </summary>
        public bool VerifyMatchingImplicitPackageVersion { get; set; }

        /// <summary>
        /// Implicitly referenced platform packages.  If set, then an error will be generated if the
        /// version of the specified packages from the assets file does not match the expected versions.
        /// </summary>
        public ITaskItem[] ExpectedPlatformPackages { get; set; }

        /// <summary>
        /// The RuntimeIdentifiers that shims will be generated for.
        /// </summary>
        public ITaskItem[] ShimRuntimeIdentifiers { get; set; }

        /// <summary>
        /// The file name of Apphost asset.
        /// </summary>
        [Required]
        public string DotNetAppHostExecutableNameWithoutExtension { get; set; }

        /// <summary>
        /// Full paths to assemblies from packages to pass to compiler as analyzers.
        /// </summary>
        [Output]
        public ITaskItem[] Analyzers { get; private set; }

        /// <summary>
        /// Full paths to assemblies from packages to compiler as references.
        /// </summary>
        [Output]
        public ITaskItem[] CompileTimeAssemblies { get; private set; }

        /// <summary>
        /// Content files from package that require preprocessing.
        /// Content files that do not require preprocessing are written directly to .g.props by nuget restore.
        /// </summary>
        [Output]
        public ITaskItem[] ContentFilesToPreprocess { get; private set; }

        /// <summary>
        /// Simple names of framework assemblies that packages request to be added as framework references.
        /// </summary>
        [Output]
        public ITaskItem[] FrameworkAssemblies { get; private set; }

        /// <summary>
        /// Full paths to native libraries from packages to run against.
        /// </summary>
        [Output]
        public ITaskItem[] NativeLibraries { get; private set; }

        /// <summary>
        /// The package folders from the assets file (ie the paths under which package assets may be found)
        /// </summary>
        [Output]
        public ITaskItem[] PackageFolders { get; set; }

        /// <summary>
        /// Full paths to satellite assemblies from packages.
        /// </summary>
        [Output]
        public ITaskItem[] ResourceAssemblies { get; private set; }

        /// <summary>
        /// Full paths to managed assemblies from packages to run against.
        /// </summary>
        [Output]
        public ITaskItem[] RuntimeAssemblies { get; private set; }

        /// <summary>
        /// Full paths to RID-specific assets that go in runtimes/ folder on publish.
        /// </summary>
        [Output]
        public ITaskItem[] RuntimeTargets { get; private set; }

        /// <summary>
        /// Relative paths to project files that are referenced transitively (but not directly).
        /// </summary>
        [Output]
        public ITaskItem[] TransitiveProjectReferences { get; private set; }

        /// <summary>
        /// Relative paths for Apphost for different ShimRuntimeIdentifiers with RuntimeIdentifier as meta data
        /// </summary>
        [Output]
        public ITaskItem[] ApphostsForShimRuntimeIdentifiers { get; private set; }

        /// <summary>
        /// Messages from the assets file.
        /// These are logged directly and therefore not returned to the targets (note private here).
        /// However,they are still stored as ITaskItem[] so that the same cache reader/writer code
        /// can be used for message items and asset items.
        /// </summary>
        private ITaskItem[] _logMessages;

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Package Asset Cache File Format Details
        //
        // Encodings of Int32, Byte[], String as defined by System.IO.BinaryReader/Writer.
        //
        // There are 3 sections, written in the following order:
        //
        // 1. Header
        // ---------
        // Encodes format and enough information to quickly decide if cache is still valid.
        //
        // Header:
        //   Int32 Signature: Spells PKGA ("package assets") when 4 little-endian bytes are interpreted as ASCII chars.
        //   Int32 Version: Increased whenever format changes to prevent issues when building incrementally with a different SDK.
        //   Byte[] SettingsHash: SHA-256 of settings that require the cache to be invalidated when changed.
        //   Int32 MetadataStringTableOffset: Byte offset in file to start of the metadata string table.
        //
        // 2. ItemGroup[] ItemGroups
        // --------------
        // There is one ItemGroup for each ITaskItem[] output (Analyzers, CompileTimeAssemblies, etc.)
        // Count and order of item groups is constant and therefore not encoded in to the file.
        //
        // ItemGroup:
        //   Int32   ItemCount
        //   Item[]  Items
        //
        // Item:
        //    String      ItemSpec (not index to string table because it generally unique)
        //    Int32       MetadataCount
        //    Metadata[]  Metadata
        //
        // Metadata:
        //    Int32 Key: Index in to MetadataStringTable for metadata key
        //    Int32 Value: Index in to MetadataStringTable for metadata value
        //
        // 3. MetadataStringTable
        // ----------------------
        // Indexes keys and values of item metadata to compress the cache file
        //
        // MetadataStringTable:
        //    Int32 MetadataStringCount
        //    String[] MetadataStrings
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        private const int CacheFormatSignature = ('P' << 0) | ('K' << 8) | ('G' << 16) | ('A' << 24);
        private const int CacheFormatVersion = 7;
        private static readonly Encoding TextEncoding = Encoding.UTF8;
        private const int SettingsHashLength = 256 / 8;
        private HashAlgorithm CreateSettingsHash() => SHA256.Create();

        protected override void ExecuteCore()
        {
            if (string.IsNullOrEmpty(ProjectAssetsFile))
            {
                throw new BuildErrorException(Strings.AssetsFileNotSet);
            }

            ReadItemGroups();
            SetImplicitMetadataForCompileTimeAssemblies();
            SetImplicitMetadataForFrameworkAssemblies();
            LogMessagesToMSBuild();
        }

        private void ReadItemGroups()
        {
            using (var reader = new CacheReader(this))
            {
                // NOTE: Order (alphabetical by group name followed by log messages) must match writer.
                Analyzers = reader.ReadItemGroup();
                ApphostsForShimRuntimeIdentifiers = reader.ReadItemGroup();
                CompileTimeAssemblies = reader.ReadItemGroup();
                ContentFilesToPreprocess = reader.ReadItemGroup();
                FrameworkAssemblies = reader.ReadItemGroup();
                NativeLibraries = reader.ReadItemGroup();
                PackageFolders = reader.ReadItemGroup();
                ResourceAssemblies = reader.ReadItemGroup();
                RuntimeAssemblies = reader.ReadItemGroup();
                RuntimeTargets = reader.ReadItemGroup();
                TransitiveProjectReferences = reader.ReadItemGroup();

                _logMessages = reader.ReadItemGroup();
            }
        }

        private void SetImplicitMetadataForCompileTimeAssemblies()
        {
            string externallyResolved = MarkPackageReferencesAsExternallyResolved ? "true" : "";

            foreach (var item in CompileTimeAssemblies)
            {
                item.SetMetadata(MetadataKeys.NuGetSourceType, "Package");
                item.SetMetadata(MetadataKeys.Private, "false");
                item.SetMetadata(MetadataKeys.HintPath, item.ItemSpec);
                item.SetMetadata(MetadataKeys.ExternallyResolved, externallyResolved);
            }
        }

        private void SetImplicitMetadataForFrameworkAssemblies()
        {
            foreach (var item in FrameworkAssemblies)
            {
                item.SetMetadata(MetadataKeys.NuGetIsFrameworkReference, "true");
                item.SetMetadata(MetadataKeys.NuGetSourceType, "Package");
                item.SetMetadata(MetadataKeys.Pack, "false");
                item.SetMetadata(MetadataKeys.Private, "false");
            }
        }

        private void LogMessagesToMSBuild()
        {
            if (!EmitAssetsLogMessages)
            {
                return;
            }

            foreach (var item in _logMessages)
            {
                Log.Log(
                    new Message(
                        text: item.ItemSpec,
                        level: GetMessageLevel(item.GetMetadata(MetadataKeys.Severity)),
                        code: item.GetMetadata(MetadataKeys.DiagnosticCode),
                        file: ProjectPath));
            }
        }

        private static MessageLevel GetMessageLevel(string severity)
        {
            switch (severity)
            {
                case nameof(LogLevel.Error):
                    return MessageLevel.Error;
                case nameof(LogLevel.Warning):
                    return MessageLevel.Warning;
                default:
                    return MessageLevel.NormalImportance;
            }
        }

        internal byte[] HashSettings()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, TextEncoding, leaveOpen: true))
                {
                    writer.Write(DisablePackageAssetsCache);
                    writer.Write(DisableFrameworkAssemblies);
                    writer.Write(DisableRuntimeTargets);
                    writer.Write(DisableTransitiveProjectReferences);
                    writer.Write(DotNetAppHostExecutableNameWithoutExtension);
                    writer.Write(EmitAssetsLogMessages);
                    writer.Write(EnsureRuntimePackageDependencies);
                    writer.Write(MarkPackageReferencesAsExternallyResolved);
                    if (ExpectedPlatformPackages != null)
                    {
                        foreach (var implicitPackage in ExpectedPlatformPackages)
                        {
                            writer.Write(implicitPackage.ItemSpec ?? "");
                            writer.Write(implicitPackage.GetMetadata(MetadataKeys.Version) ?? "");
                        }
                    }
                    writer.Write(ProjectAssetsCacheFile);
                    writer.Write(ProjectAssetsFile ?? "");
                    writer.Write(PlatformLibraryName ?? "");
                    if (RuntimeFrameworks != null)
                    {
                        foreach (var framework in RuntimeFrameworks)
                        {
                            writer.Write(framework.ItemSpec ?? "");
                        }
                    }
                    writer.Write(IsSelfContained);
                    if (SatelliteResourceLanguages != null)
                    {
                        foreach (var language in SatelliteResourceLanguages)
                        {
                            writer.Write(language.ItemSpec ?? "");
                        }
                    }
                    writer.Write(ProjectLanguage ?? "");
                    writer.Write(ProjectPath);
                    writer.Write(RuntimeIdentifier ?? "");
                    if (ShimRuntimeIdentifiers != null)
                    {
                        foreach (var r in ShimRuntimeIdentifiers)
                        {
                            writer.Write(r.ItemSpec ?? "");
                        }
                    }
                    writer.Write(TargetFrameworkMoniker);
                    writer.Write(VerifyMatchingImplicitPackageVersion);
                }

                stream.Position = 0;

                using (var hash = CreateSettingsHash())
                {
                    return hash.ComputeHash(stream);
                }
            }
        }

        private sealed class CacheReader : IDisposable
        {
            private BinaryReader _reader;
            private string[] _metadataStringTable;

            public CacheReader(ResolvePackageAssets task)
            {
                byte[] settingsHash = task.HashSettings();

                if (!task.DisablePackageAssetsCache)
                {
                    // I/O errors can occur here if there are parallel calls to resolve package assets
                    // for the same project configured with the same intermediate directory. This can
                    // (for example) happen when design-time builds and real builds overlap.
                    //
                    // If there is an I/O error, then we fall back to the same in-memory approach below
                    // as when DisablePackageAssetsCache is set to true.
                    try
                    {
                        _reader = CreateReaderFromDisk(task, settingsHash);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }

                if (_reader == null)
                {
                    _reader = CreateReaderFromMemory(task, settingsHash);
                }

                ReadMetadataStringTable();
            }

            private static BinaryReader CreateReaderFromMemory(ResolvePackageAssets task, byte[] settingsHash)
            {
                if (!task.DisablePackageAssetsCache)
                {
                    task.Log.LogMessage(MessageImportance.High, Strings.UnableToUsePackageAssetsCache);
                }

                var stream = new MemoryStream();
                using (var writer = new CacheWriter(task, stream))
                {
                    writer.Write();
                }

                stream.Position = 0;
                return OpenCacheStream(stream, settingsHash);
            }

            private static BinaryReader CreateReaderFromDisk(ResolvePackageAssets task, byte[] settingsHash)
            {
                Debug.Assert(!task.DisablePackageAssetsCache);

                BinaryReader reader = null;
                try
                {
                    if (File.GetLastWriteTimeUtc(task.ProjectAssetsCacheFile) > File.GetLastWriteTimeUtc(task.ProjectAssetsFile))
                    {
                        reader = OpenCacheFile(task.ProjectAssetsCacheFile, settingsHash);
                    }
                }
                catch (IOException) { }
                catch (InvalidDataException) { }
                catch (UnauthorizedAccessException) { }

                if (reader == null)
                {
                    using (var writer = new CacheWriter(task))
                    {
                        writer.Write();
                    }

                    reader = OpenCacheFile(task.ProjectAssetsCacheFile, settingsHash);
                }

                return reader;
            }

            private static BinaryReader OpenCacheStream(Stream stream, byte[] settingsHash)
            {
                var reader = new BinaryReader(stream, TextEncoding, leaveOpen: false);

                try
                {
                    ValidateHeader(reader, settingsHash);
                }
                catch
                {
                    reader.Dispose();
                    throw;
                }

                return reader;
            }

            private static BinaryReader OpenCacheFile(string path, byte[] settingsHash)
            {
                var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return OpenCacheStream(stream, settingsHash);
            }

            private static void ValidateHeader(BinaryReader reader, byte[] settingsHash)
            {
                if (reader.ReadInt32() != CacheFormatSignature
                    || reader.ReadInt32() != CacheFormatVersion
                    || !reader.ReadBytes(SettingsHashLength).SequenceEqual(settingsHash))
                {
                    throw new InvalidDataException();
                }
            }

            private void ReadMetadataStringTable()
            {
                int stringTablePosition = _reader.ReadInt32();
                int savedPosition = Position;
                Position = stringTablePosition;

                _metadataStringTable = new string[_reader.ReadInt32()];
                for (int i = 0; i < _metadataStringTable.Length; i++)
                {
                    _metadataStringTable[i] = _reader.ReadString();
                }

                Position = savedPosition;
            }

            private int Position
            {
                get => checked((int)_reader.BaseStream.Position);
                set => _reader.BaseStream.Position = value;
            }

            public void Dispose()
            {
                _reader.Dispose();
            }

            internal ITaskItem[] ReadItemGroup()
            {
                var items = new ITaskItem[_reader.ReadInt32()];

                for (int i = 0; i < items.Length; i++)
                {
                    items[i] = ReadItem();
                }

                return items;
            }

            private ITaskItem ReadItem()
            {
                var item = new TaskItem(_reader.ReadString());
                int metadataCount = _reader.ReadInt32();

                for (int i = 0; i < metadataCount; i++)
                {
                    string key = _metadataStringTable[_reader.ReadInt32()];
                    string value = _metadataStringTable[_reader.ReadInt32()];
                    item.SetMetadata(key, value);
                }

                return item;
            }
        }

        private sealed class CacheWriter : IDisposable
        {
            private const int InitialStringTableCapacity = 32;

            private ResolvePackageAssets _task;
            private BinaryWriter _writer;
            private LockFile _lockFile;
            private NuGetPackageResolver _packageResolver;
            private LockFileTarget _compileTimeTarget;
            private LockFileTarget _runtimeTarget;
            private Dictionary<string, int> _stringTable;
            private List<string> _metadataStrings;
            private List<int> _bufferedMetadata;
            private HashSet<string> _platformPackageExclusions;
            private Placeholder _metadataStringTablePosition;
            private NuGetFramework _targetFramework;
            private int _itemCount;

            public CacheWriter(ResolvePackageAssets task, Stream stream = null)
            {
                _targetFramework = NuGetUtils.ParseFrameworkName(task.TargetFrameworkMoniker);

                _task = task;
                _lockFile = new LockFileCache(task).GetLockFile(task.ProjectAssetsFile);
                _packageResolver = NuGetPackageResolver.CreateResolver(_lockFile);
                _compileTimeTarget = _lockFile.GetTargetAndThrowIfNotFound(_targetFramework, runtime: null);
                _runtimeTarget = _lockFile.GetTargetAndThrowIfNotFound(_targetFramework, _task.RuntimeIdentifier);
                _stringTable = new Dictionary<string, int>(InitialStringTableCapacity, StringComparer.Ordinal);
                _metadataStrings = new List<string>(InitialStringTableCapacity);
                _bufferedMetadata = new List<int>();
                _platformPackageExclusions = GetPlatformPackageExclusions();

                if (stream == null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(task.ProjectAssetsCacheFile));
                    stream = File.Open(task.ProjectAssetsCacheFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                    _writer = new BinaryWriter(stream, TextEncoding, leaveOpen: false);
                }
                else
                {
                    _writer = new BinaryWriter(stream, TextEncoding, leaveOpen: true);
                }
            }

            public void Dispose()
            {
                _writer.Dispose();
            }

            private void FlushMetadata()
            {
                if (_itemCount == 0)
                {
                    return;
                }

                Debug.Assert((_bufferedMetadata.Count % 2) == 0);

                _writer.Write(_bufferedMetadata.Count / 2);

                foreach (int m in _bufferedMetadata)
                {
                    _writer.Write(m);
                }

                _bufferedMetadata.Clear();
            }

            public void Write()
            {
                WriteHeader();
                WriteItemGroups();
                WriteMetadataStringTable();

                // Write signature last so that we will not attempt to use an incomplete cache file and instead
                // regenerate it.
                WriteToPlaceholder(new Placeholder(0), CacheFormatSignature);
            }

            private void WriteHeader()
            {
                // Leave room for signature, which we only write at the very end so that we will
                // not attempt to use a cache file corrupted by a prior crash.
                WritePlaceholder();

                _writer.Write(CacheFormatVersion);

                byte[] hash = _task.HashSettings();
                _writer.Write(_task.HashSettings());
                _metadataStringTablePosition = WritePlaceholder();
            }

            private void WriteItemGroups()
            {
                // NOTE: Order (alphabetical by group name followed by log messages) must match reader.
                WriteItemGroup(WriteAnalyzers);
                WriteItemGroup(WriteApphostsForShimRuntimeIdentifiers);
                WriteItemGroup(WriteCompileTimeAssemblies);
                WriteItemGroup(WriteContentFilesToPreprocess);
                WriteItemGroup(WriteFrameworkAssemblies);
                WriteItemGroup(WriteNativeLibraries);
                WriteItemGroup(WritePackageFolders);
                WriteItemGroup(WriteResourceAssemblies);
                WriteItemGroup(WriteRuntimeAssemblies);
                WriteItemGroup(WriteRuntimeTargets);
                WriteItemGroup(WriteTransitiveProjectReferences);

                WriteItemGroup(WriteLogMessages);
            }

            private void WriteMetadataStringTable()
            {
                int savedPosition = Position;

                _writer.Write(_metadataStrings.Count);

                foreach (var s in _metadataStrings)
                {
                    _writer.Write(s);
                }

                WriteToPlaceholder(_metadataStringTablePosition, savedPosition);
            }

            private int Position
            {
                get => checked((int)_writer.BaseStream.Position);
                set => _writer.BaseStream.Position = value;
            }

            private struct Placeholder
            {
                public readonly int Position;
                public Placeholder(int position) { Position = position; }
            }

            private Placeholder WritePlaceholder()
            {
                var placeholder = new Placeholder(Position);
                _writer.Write(int.MinValue);
                return placeholder;
            }

            private void WriteToPlaceholder(Placeholder placeholder, int value)
            {
                int savedPosition = Position;
                Position = placeholder.Position;
                _writer.Write(value);
                Position = savedPosition;
            }

            private void WriteAnalyzers()
            {
                Dictionary<string, LockFileTargetLibrary> targetLibraries = null;

                foreach (var library in _lockFile.Libraries)
                {
                    if (!library.IsPackage())
                    {
                        continue;
                    }

                    foreach (var file in library.Files)
                    {
                        if (!NuGetUtils.IsApplicableAnalyzer(file, _task.ProjectLanguage))
                        {
                            continue;
                        }

                        if (targetLibraries == null)
                        {
                            targetLibraries = _runtimeTarget
                                .Libraries
                                .ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);
                        }

                        if (targetLibraries.TryGetValue(library.Name, out var targetLibrary))
                        {
                            WriteItem(_packageResolver.ResolvePackageAssetPath(targetLibrary, file), targetLibrary);
                        }
                    }
                }
            }

            private void WriteItemGroup(Action writeItems)
            {
                var placeholder = WritePlaceholder();
                _itemCount = 0;
                writeItems();
                FlushMetadata();
                WriteToPlaceholder(placeholder, _itemCount);
            }

            private void WriteCompileTimeAssemblies()
            {
                WriteItems(
                    _compileTimeTarget,
                    package => package.CompileTimeAssemblies);
            }

            private void WriteContentFilesToPreprocess()
            {
                WriteItems(
                    _runtimeTarget,
                    p => p.ContentFiles,
                    filter: asset => !string.IsNullOrEmpty(asset.PPOutputPath),
                    writeMetadata: (package, asset) =>
                    {
                        WriteMetadata(MetadataKeys.BuildAction, asset.BuildAction.ToString());
                        WriteMetadata(MetadataKeys.CopyToOutput, asset.CopyToOutput.ToString());
                        WriteMetadata(MetadataKeys.PPOutputPath, asset.PPOutputPath);
                        WriteMetadata(MetadataKeys.OutputPath, asset.OutputPath);
                        WriteMetadata(MetadataKeys.CodeLanguage, asset.CodeLanguage);
                    });
            }

            private void WriteFrameworkAssemblies()
            {
                if (_task.DisableFrameworkAssemblies)
                {
                    return;
                }

                //  Keep track of Framework assemblies that we've already written items for,
                //  in order to only create one item for each Framework assembly.
                //  This means that if multiple packages have a dependency on the same
                //  Framework assembly, we will no longer emit separate items for each one.
                //  This should make the logs a lot cleaner and easier to understand,
                //  and may improve perf.  If you really want to know all the packages
                //  that brought in a framework assembly, you can look in the assets
                //  file.
                var writtenFrameworkAssemblies = new HashSet<string>(StringComparer.Ordinal);

                foreach (var library in _compileTimeTarget.Libraries)
                {
                    if (!library.IsPackage())
                    {
                        continue;
                    }

                    foreach (string frameworkAssembly in library.FrameworkAssemblies)
                    {
                        if (writtenFrameworkAssemblies.Add(frameworkAssembly))
                        {
                            WriteItem(frameworkAssembly, library);
                        }
                    }
                }
            }

            private void WriteLogMessages()
            {
                string GetSeverity(LogLevel level)
                {
                    switch (level)
                    {
                        case LogLevel.Warning: return nameof(LogLevel.Warning);
                        case LogLevel.Error: return nameof(LogLevel.Error);
                        default: return ""; // treated as info
                    }
                }

                foreach (var message in _lockFile.LogMessages)
                {
                    WriteItem(message.Message);
                    WriteMetadata(MetadataKeys.DiagnosticCode, message.Code.ToString());
                    WriteMetadata(MetadataKeys.Severity, GetSeverity(message.Level));
                }

                WriteAdditionalLogMessages();
            }

            /// <summary>
            /// Writes log messages which are not directly in the assets file, but are based on conditions
            /// this task evaluates
            /// </summary>
            private void WriteAdditionalLogMessages()
            {
                WriteUnsupportedRuntimeIdentifierMessageIfNecessary();
                WriteMismatchedPlatformPackageVersionMessageIfNecessary();
            }

            private void WriteUnsupportedRuntimeIdentifierMessageIfNecessary()
            {
                if (_task.EnsureRuntimePackageDependencies && !string.IsNullOrEmpty(_task.RuntimeIdentifier))
                {
                    if (_compileTimeTarget.Libraries.Count >= _runtimeTarget.Libraries.Count)
                    {
                        WriteItem(string.Format(Strings.UnsupportedRuntimeIdentifier, _task.RuntimeIdentifier));
                        WriteMetadata(MetadataKeys.Severity, nameof(LogLevel.Error));
                    }
                }
            }

            private static readonly char[] _specialNuGetVersionChars = new char[]
                {
                    '*',
                    '(', ')',
                    '[', ']'
                };

            private void WriteMismatchedPlatformPackageVersionMessageIfNecessary()
            {
                bool hasTwoPeriods(string s)
                {
                    int firstPeriodIndex = s.IndexOf('.');
                    if (firstPeriodIndex < 0)
                    {
                        return false;
                    }
                    int secondPeriodIndex = s.IndexOf('.', firstPeriodIndex + 1);
                    return secondPeriodIndex >= 0;
                }

                if (_task.VerifyMatchingImplicitPackageVersion &&
                    _task.ExpectedPlatformPackages != null)
                {
                    foreach (var implicitPackage in _task.ExpectedPlatformPackages)
                    {
                        var packageName = implicitPackage.ItemSpec;
                        var expectedVersion = implicitPackage.GetMetadata(MetadataKeys.Version);

                        if (string.IsNullOrEmpty(packageName) ||
                            string.IsNullOrEmpty(expectedVersion) ||
                            //  If RuntimeFrameworkVersion was specified as a version range or a floating version,
                            //  then we can't compare the versions directly, so just skip the check
                            expectedVersion.IndexOfAny(_specialNuGetVersionChars) >= 0)
                        {
                            continue;
                        }

                        var restoredPackage = _runtimeTarget.GetLibrary(packageName);
                        if (restoredPackage != null)
                        {
                            var restoredVersion = restoredPackage.Version.ToNormalizedString();

                            //  Normalize expected version.  For example, converts "2.0" to "2.0.0"
                            if (!hasTwoPeriods(expectedVersion))
                            {
                                expectedVersion += ".0";
                            }

                            if (restoredVersion != expectedVersion)
                            {
                                WriteItem(string.Format(Strings.MismatchedPlatformPackageVersion,
                                                        packageName,
                                                        restoredVersion,
                                                        expectedVersion));
                                WriteMetadata(MetadataKeys.Severity, nameof(LogLevel.Error));
                            }
                        }
                    }
                }
            }

            private void WriteNativeLibraries()
            {
                WriteItems(
                    _runtimeTarget,
                    package => package.NativeLibraries,
                    writeMetadata: (package, asset) =>
                    {
                        WriteMetadata(MetadataKeys.AssetType, "native");
                        if (ShouldCopyLocalPackageAssets(package))
                        {
                            WriteCopyLocalMetadata(package, Path.GetFileName(asset.Path), "native");
                        }
                    });
            }

            private void WriteApphostsForShimRuntimeIdentifiers()
            {
                if (!CanResolveApphostFromFrameworkReference())
                {
                    return;
                }

                if (_task.ShimRuntimeIdentifiers == null || _task.ShimRuntimeIdentifiers.Length == 0)
                {
                    return;
                }

                foreach (var runtimeIdentifier in _task.ShimRuntimeIdentifiers.Select(r => r.ItemSpec))
                {
                    LockFileTarget runtimeTarget = _lockFile.GetTargetAndThrowIfNotFound(_targetFramework, runtimeIdentifier);

                    var apphostName = _task.DotNetAppHostExecutableNameWithoutExtension + ExecutableExtension.ForRuntimeIdentifier(runtimeIdentifier);

                    Tuple<string, LockFileTargetLibrary> resolvedPackageAssetPathAndLibrary = FindApphostInRuntimeTarget(apphostName, runtimeTarget);

                    WriteItem(resolvedPackageAssetPathAndLibrary.Item1, resolvedPackageAssetPathAndLibrary.Item2);
                    WriteMetadata(MetadataKeys.RuntimeIdentifier, runtimeIdentifier);
                }
            }

            /// <summary>
            /// After netcoreapp3.0 apphost is resolved during ResolveFrameworkReferences. It should return nothing here
            /// </summary>
            private bool CanResolveApphostFromFrameworkReference()
            {
                if (_targetFramework.Version.Major >= 3
                    && _targetFramework.Framework.Equals(".NETCoreApp", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            }

            private void WritePackageFolders()
            {
                foreach (var packageFolder in _lockFile.PackageFolders)
                {
                    WriteItem(packageFolder.Path);
                }
            }

            private void WriteResourceAssemblies()
            {
                WriteItems(
                    _runtimeTarget,
                    package => package.ResourceAssemblies.Where(asset =>
                        _task.SatelliteResourceLanguages == null ||
                        _task.SatelliteResourceLanguages.Any(lang =>
                            string.Equals(asset.Properties["locale"], lang.ItemSpec, StringComparison.OrdinalIgnoreCase))),
                    writeMetadata: (package, asset) =>
                    {
                        WriteMetadata(MetadataKeys.AssetType, "resources");
                        string locale = asset.Properties["locale"];
                        if (ShouldCopyLocalPackageAssets(package))
                        {
                            WriteCopyLocalMetadata(
                                package,
                                Path.GetFileName(asset.Path),
                                "resources",
                                destinationSubDirectory: locale + Path.DirectorySeparatorChar);
                        }
                        else
                        {
                            WriteMetadata(MetadataKeys.DestinationSubDirectory, locale + Path.DirectorySeparatorChar);
                        }
                        WriteMetadata(MetadataKeys.Culture, locale);
                    });
            }

            private void WriteRuntimeAssemblies()
            {
                WriteItems(
                    _runtimeTarget,
                    package => package.RuntimeAssemblies,
                    writeMetadata: (package, asset) =>
                    {
                        WriteMetadata(MetadataKeys.AssetType, "runtime");
                        if (ShouldCopyLocalPackageAssets(package))
                        {
                            WriteCopyLocalMetadata(package, Path.GetFileName(asset.Path), "runtime");
                        }
                    });
            }

            private void WriteRuntimeTargets()
            {
                if (_task.DisableRuntimeTargets)
                {
                    return;
                }

                WriteItems(
                    _runtimeTarget,
                    package => package.RuntimeTargets,
                    writeMetadata: (package, asset) =>
                    {
                        WriteMetadata(MetadataKeys.AssetType, asset.AssetType.ToLowerInvariant());
                        if (ShouldCopyLocalPackageAssets(package))
                        {
                            WriteCopyLocalMetadata(
                                package,
                                Path.GetFileName(asset.Path),
                                asset.AssetType.ToLowerInvariant(),
                                destinationSubDirectory: Path.GetDirectoryName(asset.Path) + Path.DirectorySeparatorChar);
                        }
                        else
                        {
                            WriteMetadata(MetadataKeys.DestinationSubDirectory, Path.GetDirectoryName(asset.Path) + Path.DirectorySeparatorChar);
                        }
                        WriteMetadata(MetadataKeys.RuntimeIdentifier, asset.Runtime);
                    });
            }

            private void WriteTransitiveProjectReferences()
            {
                if (_task.DisableTransitiveProjectReferences)
                {
                    return;
                }

                Dictionary<string, string> projectReferencePaths = null;
                HashSet<string> directProjectDependencies = null;

                foreach (var library in _runtimeTarget.Libraries)
                {
                    if (!library.IsTransitiveProjectReference(_lockFile, ref directProjectDependencies))
                    {
                        continue;
                    }

                    if (projectReferencePaths == null)
                    {
                        projectReferencePaths = GetProjectReferencePaths(_lockFile);
                    }

                    if (!directProjectDependencies.Contains(library.Name))
                    {
                        WriteItem(projectReferencePaths[library.Name], library);
                    }
                }
            }

            private void WriteItems<T>(
                LockFileTarget target,
                Func<LockFileTargetLibrary, IEnumerable<T>> getAssets,
                Func<T, bool> filter = null,
                Action<LockFileTargetLibrary, T> writeMetadata = null)
                where T : LockFileItem
            {
                foreach (var library in target.Libraries)
                {
                    if (!library.IsPackage())
                    {
                        continue;
                    }

                    foreach (T asset in getAssets(library))
                    {
                        if (asset.IsPlaceholderFile() || (filter != null && !filter.Invoke(asset)))
                        {
                            continue;
                        }

                        string itemSpec = _packageResolver.ResolvePackageAssetPath(library, asset.Path);
                        WriteItem(itemSpec, library);
                        WriteMetadata(MetadataKeys.PathInPackage, asset.Path);
                        WriteMetadata(MetadataKeys.PackageName, library.Name);
                        WriteMetadata(MetadataKeys.PackageVersion, library.Version.ToString().ToLowerInvariant());

                        writeMetadata?.Invoke(library, asset);
                    }
                }
            }

            private void WriteItem(string itemSpec)
            {
                FlushMetadata();
                _itemCount++;
                _writer.Write(ProjectCollection.Escape(itemSpec));
            }

            private void WriteItem(string itemSpec, LockFileTargetLibrary package)
            {
                WriteItem(itemSpec);
                WriteMetadata(MetadataKeys.NuGetPackageId, package.Name);
                WriteMetadata(MetadataKeys.NuGetPackageVersion, package.Version.ToNormalizedString());
            }

            private void WriteMetadata(string key, string value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _bufferedMetadata.Add(GetMetadataIndex(key));
                    _bufferedMetadata.Add(GetMetadataIndex(value));
                }
            }

            private void WriteCopyLocalMetadata(LockFileTargetLibrary package, string assetsFileName, string assetType, string destinationSubDirectory = null)
            {
                WriteMetadata(MetadataKeys.CopyLocal, "true");
                WriteMetadata(
                    MetadataKeys.DestinationSubPath,
                    string.IsNullOrEmpty(destinationSubDirectory) ?
                        assetsFileName :
                        Path.Combine(destinationSubDirectory, assetsFileName));
                if (!string.IsNullOrEmpty(destinationSubDirectory))
                {
                    WriteMetadata(MetadataKeys.DestinationSubDirectory, destinationSubDirectory);
                }
            }

            private int GetMetadataIndex(string value)
            {
                if (!_stringTable.TryGetValue(value, out int index))
                {
                    index = _metadataStrings.Count;
                    _stringTable.Add(value, index);
                    _metadataStrings.Add(value);
                }

                return index;
            }

            private bool ShouldCopyLocalPackageAssets(LockFileTargetLibrary package)
            {
                return _platformPackageExclusions == null || !_platformPackageExclusions.Contains(package.Name);
            }

            private HashSet<string> GetPlatformPackageExclusions()
            {
                // Only exclude packages for framework-dependent applications
                if (_task.IsSelfContained && !string.IsNullOrEmpty(_runtimeTarget.RuntimeIdentifier))
                {
                    return null;
                }

                var packageExclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var libraryLookup = _runtimeTarget.Libraries.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

                // Exclude the platform library
                if (_task.PlatformLibraryName != null)
                {
                    var platformLibrary = _runtimeTarget.GetLibrary(_task.PlatformLibraryName);
                    if (platformLibrary != null)
                    {
                        packageExclusions.UnionWith(_runtimeTarget.GetPlatformExclusionList(platformLibrary, libraryLookup));
                    }
                }

                return packageExclusions;
            }

            private static Dictionary<string, string> GetProjectReferencePaths(LockFile lockFile)
            {
                Dictionary<string, string> paths = new Dictionary<string, string>();

                foreach (var library in lockFile.Libraries)
                {
                    if (library.IsProject())
                    {
                        paths[library.Name] = NuGetPackageResolver.NormalizeRelativePath(library.MSBuildProject);
                    }
                }

                return paths;
            }

            private Tuple<string, LockFileTargetLibrary> FindApphostInRuntimeTarget(string apphostName, LockFileTarget runtimeTarget)
            {
                foreach (LockFileTargetLibrary library in runtimeTarget.Libraries)
                {
                    if (!library.IsPackage())
                    {
                        continue;
                    }

                    foreach (LockFileItem asset in library.NativeLibraries)
                    {
                        if (asset.IsPlaceholderFile())
                        {
                            continue;
                        }

                        var resolvedPackageAssetPath = _packageResolver.ResolvePackageAssetPath(library, asset.Path);

                        if (Path.GetFileName(resolvedPackageAssetPath) == apphostName)
                        {
                            return new Tuple<string, LockFileTargetLibrary>(resolvedPackageAssetPath, library);
                        }
                    }
                }

                throw new BuildErrorException(Strings.CannotFindApphostForRid, runtimeTarget.RuntimeIdentifier);
            }
        }
    }
}
