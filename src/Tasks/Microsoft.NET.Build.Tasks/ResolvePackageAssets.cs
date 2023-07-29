// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;

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
        #region Input Items

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
        /// TargetFramework to use for compile-time assets.
        /// </summary>
        [Required]
        public string TargetFramework { get; set; }

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
        /// Disables FrameworkReferences from referenced projects or packages
        /// </summary>
        public bool DisableTransitiveFrameworkReferences { get; set; }

        /// <summary>
        /// Do not add references to framework assemblies as specified by packages.
        /// </summary>
        public bool DisableFrameworkAssemblies { get; set; }

        /// <summary>
        /// Whether or not resolved runtime target assets should be copied locally.
        /// </summary>
        public bool CopyLocalRuntimeTargetAssets { get; set; }

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
        /// Optional version of the compiler API (E.g. 'roslyn3.9', 'roslyn4.0')
        /// Impacts applicability of analyzer assets.
        /// </summary>
        public string CompilerApiVersion { get; set; }

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

        public ITaskItem[] PackageReferences { get; set; }

        /// <summary>
        /// The file name of Apphost asset.
        /// </summary>
        [Required]
        public string DotNetAppHostExecutableNameWithoutExtension { get; set; }

        /// <summary>
        /// True indicates we are doing a design-time build. Otherwise we are in a build.
        /// </summary>
        public bool DesignTimeBuild { get; set; }

        /// <summary>
        /// Eg: "Microsoft.NETCore.App;NETStandard.Library"
        /// </summary>
        [Required]
        public string DefaultImplicitPackages { get; set; }

        #endregion

        #region Output Items
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

        [Output]
        public ITaskItem[] FrameworkReferences { get; private set; }

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

        [Output]
        public ITaskItem[] PackageDependencies { get; private set; }

        /// <summary>
        /// Filters and projects items produced by <see cref="ResolvePackageDependencies"/> for consumption by
        /// the dependencies tree, via design-time builds.
        /// </summary>
        /// <remarks>
        /// Changes to the implementation of output must be coordinated with <c>PackageRuleHandler</c>
        /// in the dotnet/project-system repo.
        /// </remarks>
        [Output]
        public ITaskItem[] PackageDependenciesDesignTime { get; private set; }

        /// <summary>
        /// List of symbol files (.pdb) related to NuGet packages.
        /// </summary>
        /// <remarks>
        /// Pdb files to be copied to the output directory
        /// </remarks>
        [Output]
        public ITaskItem[] DebugSymbolsFiles { get; private set;}

        /// <summary>
        /// List of xml files related to NuGet packages.
        /// </summary>
        /// <remarks>
        ///  The XML files should only be included in the publish output if PublishReferencesDocumentationFiles is true
        /// </remarks>
        [Output]
        public ITaskItem[] ReferenceDocumentationFiles { get; private set; }

        #endregion

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
        private const int CacheFormatVersion = 12;
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
                DebugSymbolsFiles = reader.ReadItemGroup();
                FrameworkAssemblies = reader.ReadItemGroup();
                FrameworkReferences = reader.ReadItemGroup();
                NativeLibraries = reader.ReadItemGroup();
                PackageDependencies = reader.ReadItemGroup();
                PackageDependenciesDesignTime = reader.ReadItemGroup();
                PackageFolders = reader.ReadItemGroup();
                ReferenceDocumentationFiles = reader.ReadItemGroup();
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
                    writer.Write(CopyLocalRuntimeTargetAssets);
                    writer.Write(DisableTransitiveProjectReferences);
                    writer.Write(DisableTransitiveFrameworkReferences);
                    writer.Write(DotNetAppHostExecutableNameWithoutExtension);
                    writer.Write(EmitAssetsLogMessages);
                    writer.Write(EnsureRuntimePackageDependencies);
                    writer.Write(MarkPackageReferencesAsExternallyResolved);
                    if (PackageReferences != null)
                    {
                        foreach (var packageReference in PackageReferences)
                        {
                            writer.Write(packageReference.ItemSpec ?? "");
                            writer.Write(packageReference.GetMetadata(MetadataKeys.Version));
                            writer.Write(packageReference.GetMetadata(MetadataKeys.Publish));
                        }
                    }
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
                    writer.Write(CompilerApiVersion ?? "");
                    writer.Write(ProjectPath);
                    writer.Write(RuntimeIdentifier ?? "");
                    if (ShimRuntimeIdentifiers != null)
                    {
                        foreach (var r in ShimRuntimeIdentifiers)
                        {
                            writer.Write(r.ItemSpec ?? "");
                        }
                    }
                    writer.Write(TargetFramework);
                    writer.Write(VerifyMatchingImplicitPackageVersion);
                    writer.Write(DefaultImplicitPackages ?? "");
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
                    task.Log.LogMessage(MessageImportance.High, Strings.UnableToUsePackageAssetsCache_Info);
                }

                Stream stream;
                using (var writer = new CacheWriter(task))
                {
                    stream = writer.WriteToMemoryStream();
                }

                return OpenCacheStream(stream, settingsHash);
            }

            private static BinaryReader CreateReaderFromDisk(ResolvePackageAssets task, byte[] settingsHash)
            {
                Debug.Assert(!task.DisablePackageAssetsCache);

                BinaryReader reader = null;
                try
                {
                    if (IsCacheFileUpToDate())
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
                        if (writer.CanWriteToCacheFile)
                        {
                            writer.WriteToCacheFile();
                            reader = OpenCacheFile(task.ProjectAssetsCacheFile, settingsHash);
                        }
                        else
                        {
                            var stream = writer.WriteToMemoryStream();
                            reader = OpenCacheStream(stream, settingsHash);
                        }
                    }
                }

                return reader;

                bool IsCacheFileUpToDate() => File.GetLastWriteTimeUtc(task.ProjectAssetsCacheFile) > File.GetLastWriteTimeUtc(task.ProjectAssetsFile);
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

        internal sealed class CacheWriter : IDisposable
        {
            private const int InitialStringTableCapacity = 32;

            private ResolvePackageAssets _task;
            private BinaryWriter _writer;
            private LockFile _lockFile;
            private LockFileTarget _compileTimeTarget;
            private IPackageResolver _packageResolver;
            private LockFileTarget _runtimeTarget;
            private Dictionary<string, int> _stringTable;
            private List<string> _metadataStrings;
            private List<int> _bufferedMetadata;
            private HashSet<string> _copyLocalPackageExclusions;
            private HashSet<string> _publishPackageExclusions;
            private Placeholder _metadataStringTablePosition;
            private string _targetFramework;
            private int _itemCount;

            public bool CanWriteToCacheFile { get; set; }

            private bool MismatchedAssetsFile => !CanWriteToCacheFile;

            private const string NetCorePlatformLibrary = "Microsoft.NETCore.App";

            private const char RelatedPropertySeparator = ';';

            /// <summary>
            /// This constructor should only be used for testing - IPackgeResolver carries a lot of
            /// state so using mocks really help with testing this component.
            /// </summary>
            public CacheWriter(ResolvePackageAssets task, IPackageResolver resolver) : this(task)
            {
                _packageResolver = resolver;
            }

            public CacheWriter(ResolvePackageAssets task)
            {
                _targetFramework = task.TargetFramework;

                _task = task;
                _lockFile = new LockFileCache(task).GetLockFile(task.ProjectAssetsFile);
                _packageResolver = NuGetPackageResolver.CreateResolver(_lockFile);

                //  If we are doing a design-time build, we do not want to fail the build if we can't find the
                //  target framework and/or runtime identifier in the assets file.  This is because the design-time
                //  build needs to succeed in order to get the right information in order to run a restore in order
                //  to write the assets file with the correct information.

                //  So if we can't find the right target in the lock file and are doing a design-time build, we use
                //  an empty lock file target instead of throwing an error, and we don't save the results to the
                //  cache file.
                CanWriteToCacheFile = true;
                if (task.DesignTimeBuild)
                {
                    _compileTimeTarget = _lockFile.GetTargetAndReturnNullIfNotFound(_targetFramework, runtimeIdentifier: null);
                    _runtimeTarget = _lockFile.GetTargetAndReturnNullIfNotFound(_targetFramework, _task.RuntimeIdentifier);
                    if (_compileTimeTarget == null)
                    {
                        _compileTimeTarget = new LockFileTarget();
                        CanWriteToCacheFile = false;
                    }
                    if (_runtimeTarget == null)
                    {
                        _runtimeTarget = new LockFileTarget();
                        CanWriteToCacheFile = false;
                    }
                }
                else
                {
                    _compileTimeTarget = _lockFile.GetTargetAndThrowIfNotFound(_targetFramework, runtimeIdentifier: null); 
                    _runtimeTarget = _lockFile.GetTargetAndThrowIfNotFound(_targetFramework, _task.RuntimeIdentifier);
                }
                

                _stringTable = new Dictionary<string, int>(InitialStringTableCapacity, StringComparer.Ordinal);
                _metadataStrings = new List<string>(InitialStringTableCapacity);
                _bufferedMetadata = new List<int>();

                //  If the assets file doesn't match the inputs, don't bother trying to compute package exclusions
                if (!MismatchedAssetsFile)
                {
                    ComputePackageExclusions();
                }
            }

            public void WriteToCacheFile()
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_task.ProjectAssetsCacheFile));
                var stream = File.Open(_task.ProjectAssetsCacheFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                using (_writer = new BinaryWriter(stream, TextEncoding, leaveOpen: false))
                {
                    Write();
                }
            }

            public Stream WriteToMemoryStream()
            {
                var stream = new MemoryStream();
                _writer = new BinaryWriter(stream, TextEncoding, leaveOpen: true);
                Write();
                stream.Position = 0;
                return stream;
            }

            public void Dispose()
            {
                _writer?.Dispose();
                _writer = null;
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

            private void Write()
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
                WriteItemGroup(WriteDebugSymbolsFiles);
                WriteItemGroup(WriteFrameworkAssemblies);
                WriteItemGroup(WriteFrameworkReferences);
                WriteItemGroup(WriteNativeLibraries);
                WriteItemGroup(WritePackageDependencies);
                WriteItemGroup(WritePackageDependenciesDesignTime);
                WriteItemGroup(WritePackageFolders);
                WriteItemGroup(WriteReferenceDocumentationFiles);
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

            class LibraryComparer : IEqualityComparer<(string, NuGetVersion)>
            {
                public bool Equals((string, NuGetVersion) l1, (string, NuGetVersion) l2)
                {
                    return StringComparer.OrdinalIgnoreCase.Equals(l1.Item1, l2.Item1)
                        && l1.Item2.Equals(l2.Item2);
                    
                }
                public int GetHashCode((string, NuGetVersion) library)
                {
#if NET
                    return HashCode.Combine(
                        StringComparer.OrdinalIgnoreCase.GetHashCode(library.Item1),
                        library.Item2.GetHashCode());
#else
                    int hashCode = -1507694697;
                    hashCode = hashCode * -1521134295 + StringComparer.OrdinalIgnoreCase.GetHashCode(library.Item1);
                    hashCode = hashCode * -1521134295 + library.Item2.GetHashCode();
                    return hashCode;
#endif
                }
            }

            private void WriteAnalyzers()
            {
                AnalyzerResolver resolver = new AnalyzerResolver(this);

                foreach (var library in _lockFile.Libraries)
                {
                    if (!library.IsPackage())
                    {
                        continue;
                    }

                    foreach (var file in library.Files)
                    {
                        resolver.AddFile(file, library);
                    }

                    resolver.CompleteLibraryAnalyzers();
                }
            }

            /// <summary>
            /// Resolves the correct analyzer assets from a NuGet package.
            /// </summary>
            /// <remarks>
            /// This allows packages to ship multiple analyzers that target different versions
            /// of the compiler. For example, a package may include:
            ///
            /// "analyzers/dotnet/roslyn3.7/analyzer.dll"
            /// "analyzers/dotnet/roslyn3.8/analyzer.dll"
            /// "analyzers/dotnet/roslyn4.0/analyzer.dll"
            ///
            /// When the <paramref name="compilerApiVersion"/> is 'roslyn3.9', only the assets 
            /// in the folder with the highest applicable compiler version are picked.
            /// In this case,
            /// 
            /// "analyzers/dotnet/roslyn3.8/analyzer.dll"
            /// 
            /// will be picked, and the other analyzer assets will be excluded.
            /// </remarks>
            private class AnalyzerResolver
            {
                private readonly CacheWriter _cacheWriter;
                private readonly string? _compilerNameSearchString;
                private readonly Version? _compilerVersion;
                private Dictionary<(string, NuGetVersion), LockFileTargetLibrary>? _targetLibraries;
                private List<(string, LockFileLibrary, Version)>? _potentialAnalyzers;
                private Version _maxApplicableVersion;

                private Dictionary<(string, NuGetVersion), LockFileTargetLibrary> TargetLibraries =>
                    _targetLibraries ??=
                        _cacheWriter._compileTimeTarget.Libraries.ToDictionary(l => (l.Name, l.Version), new LibraryComparer());

                public AnalyzerResolver(CacheWriter cacheWriter)
                {
                    _cacheWriter = cacheWriter;

                    if (ParseCompilerApiVersion(_cacheWriter._task.CompilerApiVersion, out ReadOnlyMemory<char> compilerName, out Version compilerVersion))
                    {
#if NET
                        _compilerNameSearchString = string.Concat("/".AsSpan(), compilerName.Span);
#else
                        _compilerNameSearchString = "/" + compilerName;
#endif                   
                        _compilerVersion = compilerVersion;
                    }
                }

                public void AddFile(string file, LockFileLibrary library)
                {
                    if (NuGetUtils.IsApplicableAnalyzer(file, _cacheWriter._task.ProjectLanguage))
                    {
                        if (IsFileCompilerVersionSpecific(file, out Version fileCompilerVersion))
                        {
                            if (fileCompilerVersion > _compilerVersion)
                            {
                                // version is too high - skip this file
                                return;
                            }

                            _potentialAnalyzers ??= new List<(string, LockFileLibrary, Version)>();
                            _potentialAnalyzers.Add((file, library, fileCompilerVersion));

                            if (_maxApplicableVersion == null || fileCompilerVersion > _maxApplicableVersion)
                            {
                                _maxApplicableVersion = fileCompilerVersion;
                            }
                        }
                        else
                        {
                            // if this file isn't specific to a compiler version, just write it directly
                            WriteAnalyzer(file, library);
                        }
                    }
                }

                private bool IsFileCompilerVersionSpecific(string file, out Version fileCompilerVersion)
                {
                    fileCompilerVersion = null;

                    if (_compilerNameSearchString == null)
                    {
                        // unable to tell if this file is specific to a compiler version
                        return false;
                    }

                    int compilerNameStart = file.IndexOf(_compilerNameSearchString);
                    if (compilerNameStart == -1)
                    {
                        return false;
                    }

                    int compilerVersionStart = compilerNameStart + _compilerNameSearchString.Length;
                    int compilerVersionStop = file.IndexOf('/', compilerVersionStart);
                    if (compilerVersionStop == -1)
                    {
                        return false;
                    }

                    return TryParseVersion(file, compilerVersionStart, compilerVersionStop - compilerVersionStart, out fileCompilerVersion);
                }

                public void CompleteLibraryAnalyzers()
                {
                    if (_maxApplicableVersion != null && _potentialAnalyzers?.Count > 0)
                    {
                        foreach (var (file, library, version) in _potentialAnalyzers)
                        {
                            if (version == _maxApplicableVersion)
                            {
                                WriteAnalyzer(file, library);
                            }
                        }
                    }

                    // clear the variables that are scoped per library
                    _maxApplicableVersion = null;
                    _potentialAnalyzers?.Clear();
                }

                private void WriteAnalyzer(string file, LockFileLibrary library)
                {
                    if (TargetLibraries.TryGetValue((library.Name, library.Version), out var targetLibrary))
                    {
                        _cacheWriter.WriteItem(_cacheWriter._packageResolver.ResolvePackageAssetPath(targetLibrary, file), targetLibrary);
                    }
                }

                /// <summary>
                /// Parses the <paramref name="compilerApiVersion"/> string into its component parts:
                /// compilerName:, e.g. "roslyn"
                /// compilerVersion: e.g. 3.9
                /// </summary>
                private static bool ParseCompilerApiVersion(string compilerApiVersion, out ReadOnlyMemory<char> compilerName, out Version compilerVersion)
                {
                    compilerName = default;
                    compilerVersion = default;

                    if (string.IsNullOrEmpty(compilerApiVersion))
                    {
                        return false;
                    }

                    int compilerVersionStart = -1;
                    for (int i = 0; i < compilerApiVersion.Length; i++)
                    {
                        if (char.IsDigit(compilerApiVersion[i]))
                        {
                            compilerVersionStart = i;
                            break;
                        }
                    }

                    if (compilerVersionStart > 0)
                    {
                        if (TryParseVersion(compilerApiVersion, compilerVersionStart, out compilerVersion))
                        {
                            compilerName = compilerApiVersion.AsMemory(0, compilerVersionStart);
                            return true;
                        }
                    }

                    // didn't find a compiler name or version
                    return false;
                }

                private static bool TryParseVersion(string value, int startIndex, out Version version) =>
                    TryParseVersion(value, startIndex, value.Length - startIndex, out version);

                private static bool TryParseVersion(string value, int startIndex, int length, out Version version)
                {
#if NET
                    return Version.TryParse(value.AsSpan(startIndex, length), out version);
#else
                    return Version.TryParse(value.Substring(startIndex, length), out version);
#endif
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
                    package => package.CompileTimeAssemblies,
                    filter: null,
                    writeMetadata: (package, asset) =>
                    {
                        if (asset.Properties.TryGetValue(LockFileItem.AliasesProperty, out var aliases))
                        {
                            WriteMetadata(MetadataKeys.Aliases, aliases);
                        }
                    });
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

            private void WriteDebugSymbolsFiles()
            {
                WriteDebugItems(
                    p => p.RuntimeAssemblies,
                    MetadataKeys.PdbExtension);
            }

            private void WriteReferenceDocumentationFiles()
            {
                WriteDebugItems(
                    p => p.CompileTimeAssemblies,
                    MetadataKeys.XmlExtension);
            }

            private void WriteDebugItems(
                Func<LockFileTargetLibrary, IEnumerable<LockFileItem>> getAssets,
                string extension)
            {
                foreach (var library in _runtimeTarget.Libraries)
                {
                    if (!library.IsPackage())
                    {
                        continue;
                    }

                    foreach (LockFileItem asset in getAssets(library))
                    {
                        if (asset.IsPlaceholderFile() || !asset.Properties.ContainsKey(MetadataKeys.RelatedProperty))
                        {
                            continue;
                        }

                        string itemSpec = _packageResolver.ResolvePackageAssetPath(library, asset.Path);

                        string relatedExtensions = asset.Properties[MetadataKeys.RelatedProperty];

                        foreach (string fileExtension in relatedExtensions.Split(RelatedPropertySeparator))
                        {
                            if (StringComparer.InvariantCulture.Equals(fileExtension, extension))
                            {
                                string xmlFilePath = Path.ChangeExtension(itemSpec, fileExtension);
                                if (File.Exists(xmlFilePath))
                                {
                                    WriteItem(xmlFilePath, library);
                                }
                            }
                        }
                    }
                }
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
                static string GetSeverity(LogLevel level)
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
                static bool hasTwoPeriods(string s)
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
                        WriteCopyLocalMetadataIfNeeded(package, Path.GetFileName(asset.Path));
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
                    bool throwIfAssetsFileTargetNotFound = !_task.DesignTimeBuild;

                    LockFileTarget runtimeTarget;
                    if (_task.DesignTimeBuild)
                    {
                        runtimeTarget = _lockFile.GetTargetAndReturnNullIfNotFound(_targetFramework, runtimeIdentifier) ?? new LockFileTarget();
                    }
                    else
                    {
                        runtimeTarget = _lockFile.GetTargetAndThrowIfNotFound(_targetFramework, runtimeIdentifier);
                    }

                    var apphostName = _task.DotNetAppHostExecutableNameWithoutExtension + ExecutableExtension.ForRuntimeIdentifier(runtimeIdentifier);

                    Tuple<string, LockFileTargetLibrary> resolvedPackageAssetPathAndLibrary = FindApphostInRuntimeTarget(apphostName, runtimeTarget);

                    WriteItem(resolvedPackageAssetPathAndLibrary.Item1, resolvedPackageAssetPathAndLibrary.Item2);
                    WriteMetadata(MetadataKeys.RuntimeIdentifier, runtimeIdentifier);
                }
            }

            /// <summary>
            /// After netcoreapp3.0 apphost is resolved during ProcessFrameworkReferences. It should return nothing here
            /// </summary>
            private bool CanResolveApphostFromFrameworkReference()
            {
                if (!CanWriteToCacheFile)
                {
                    //  If we can't write to the cache file, it's because this is a design-time build where the
                    //  TargetFramework doesn't match what's in the assets file.  So don't try looking up the
                    //  TargetFramework in the assets file.
                    return false;
                }
                else
                { 
                    var targetFramework = _lockFile.GetTargetAndThrowIfNotFound(_targetFramework, null).TargetFramework;

                    if (targetFramework.Version.Major >= 3
                        && targetFramework.Framework.Equals(".NETCoreApp", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    return true;
                }
            }

            private void WritePackageFolders()
            {
                foreach (var packageFolder in _lockFile.PackageFolders)
                {
                    WriteItem(packageFolder.Path);
                }
            }

            private void WritePackageDependencies()
            {
                foreach (var library in _runtimeTarget.Libraries)
                {
                    if (library.IsPackage())
                    {
                        WriteItem(library.Name);
                    }
                }
            }

            private void WritePackageDependenciesDesignTime()
            {
                var implicitPackageReferences = CollectSDKReferencesDesignTime.GetImplicitPackageReferences(_task.DefaultImplicitPackages);

                // Scan PackageDependencies to build the set of packages in our target.
                var allowItemSpecs = GetPackageDependencies();

                foreach (var package in _lockFile.Libraries)
                {
                    var packageVersion = package.Version.ToNormalizedString();
                    string packageId = $"{package.Name}/{packageVersion}";

                    // Find PackageDefinitions that match our allowed item specs
                    if (string.IsNullOrEmpty(package.Name) || !allowItemSpecs.Contains(packageId))
                    {
                        // Only include packages from the allow list.
                        // This excludes transitive packages and those from other targets.
                        continue;
                    }

                    var dependencyType = GetDependencyType(package.Type);

                    if (dependencyType == DependencyType.Package ||
                        dependencyType == DependencyType.Unresolved)
                    {
                        WriteItem(packageId);
                        WriteMetadata(MetadataKeys.Name, package.Name);

                        var version = packageVersion ?? string.Empty;
                        WriteMetadata(MetadataKeys.Version, version);

                        var isImplicitlyDefined = implicitPackageReferences.Contains(package.Name);
                        WriteMetadata(MetadataKeys.IsImplicitlyDefined, isImplicitlyDefined.ToString());

                        string resolvedPackagePath = _packageResolver.GetPackageDirectory(package.Name, package.Version);
                        var resolvedPath = resolvedPackagePath ?? string.Empty;
                        var resolved = !string.IsNullOrEmpty(resolvedPath);
                        WriteMetadata(MetadataKeys.Resolved, resolved.ToString());

                        string itemPath = package.Path ?? string.Empty;
                        var path = (resolved
                            ? resolvedPath
                            : itemPath) ?? string.Empty;
                        WriteMetadata(MetadataKeys.Path, path);

                        string itemDiagnosticLevel = GetPackageDiagnosticLevel(package);
                        var diagnosticLevel = itemDiagnosticLevel ?? string.Empty;
                        WriteMetadata(MetadataKeys.DiagnosticLevel, diagnosticLevel);
                    }
                }

                HashSet<string> GetPackageDependencies()
                {
                    HashSet<string> projectFileDependencies = _lockFile.GetProjectFileDependencySet(_compileTimeTarget.Name);

                    HashSet<string> results = new(StringComparer.OrdinalIgnoreCase);

                    foreach (var package in _compileTimeTarget.Libraries)
                    {
                        if (projectFileDependencies.Contains(package.Name))
                        {
                            string itemSpec = GetPackageId(package);

                            bool added = results.Add(itemSpec);

                            Debug.Assert(added);
                        }
                    }

                    return results;
                }

                static string GetPackageId(LockFileTargetLibrary package) => $"{package.Name}/{package.Version.ToNormalizedString()}";

                string GetPackageDiagnosticLevel(LockFileLibrary package)
                {
                    string target = _task.TargetFramework ?? "";

                    var messages = _lockFile.LogMessages.Where(log =>
                        log.LibraryId == package.Name &&
                        log.TargetGraphs.Any(tg =>
                        {
                            var parts = tg.Split(LockFile.DirectorySeparatorChar);
                            var parsedTargetGraph = NuGetFramework.Parse(parts[0]);
                            var alias = _lockFile.PackageSpec.TargetFrameworks
                                .FirstOrDefault(tf => tf.FrameworkName == parsedTargetGraph)
                                ?.TargetAlias ?? tg;
                            return alias == target;
                        }));

                    if (!messages.Any())
                    {
                        return string.Empty;
                    }

                    return messages.Max(log => log.Level).ToString();
                }

                static DependencyType GetDependencyType(string dependencyTypeString)
                {
                    Enum.TryParse(dependencyTypeString, ignoreCase: true, out DependencyType dependencyType);
                    return dependencyType;
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
                        // Locales from packages can be free-form, so we normalize them to the standard
                        // forms here. If the locale is mixed-case, that can cause issues on case-sensitive
                        // file systems when the locale-specific assets are copied.
                        try
                        {
                            var normalizedLocale = System.Globalization.CultureInfo.GetCultureInfo(locale).Name;
                            if (normalizedLocale != locale)
                            {
                                var tfm = _lockFile.GetTargetAndThrowIfNotFound(_targetFramework, null).TargetFramework;
                                if (tfm.Version.Major >= 7)
                                {
                                    _task.Log.LogWarning(Strings.PackageContainsIncorrectlyCasedLocale, package.Name, package.Version.ToNormalizedString(), locale, normalizedLocale);
                                }
                                else
                                {
                                    _task.Log.LogMessage(Strings.PackageContainsIncorrectlyCasedLocale, package.Name, package.Version.ToNormalizedString(), locale, normalizedLocale);
                                }
                            }
                            locale = normalizedLocale;
                        }
                        catch (System.Globalization.CultureNotFoundException cnf)
                        {
                            var tfm = _lockFile.GetTargetAndThrowIfNotFound(_targetFramework, null).TargetFramework;
                            if (tfm.Version.Major >= 7)
                            {
                                _task.Log.LogWarning(Strings.PackageContainsUnknownLocale, package.Name, package.Version.ToNormalizedString(), cnf.InvalidCultureName);
                            } else
                            {
                                _task.Log.LogMessage(Strings.PackageContainsUnknownLocale, package.Name, package.Version.ToNormalizedString(), cnf.InvalidCultureName);
                            }

                            // We could potentially strip this unknown locale at this point, but we do not.
                            // Locale data can change over time (it's typically an OS database that's kept updated),
                            // and the data on the system running the build may not be the same data as
                            // the system executing the built code. So we should be permissive for this case.
                        }
                        bool wroteCopyLocalMetadata = WriteCopyLocalMetadataIfNeeded(
                                package,
                                Path.GetFileName(asset.Path),
                                destinationSubDirectory: locale + Path.DirectorySeparatorChar);
                        if (!wroteCopyLocalMetadata)
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
                        WriteCopyLocalMetadataIfNeeded(package, Path.GetFileName(asset.Path));
                    });
            }

            private void WriteRuntimeTargets()
            {
                WriteItems(
                    _runtimeTarget,
                    package => package.RuntimeTargets,
                    writeMetadata: (package, asset) =>
                    {
                        WriteMetadata(MetadataKeys.AssetType, asset.AssetType.ToLowerInvariant());
                        bool wroteCopyLocalMetadata = false;
                        if (_task.CopyLocalRuntimeTargetAssets)
                        {
                            wroteCopyLocalMetadata = WriteCopyLocalMetadataIfNeeded(
                                package,
                                Path.GetFileName(asset.Path),
                                destinationSubDirectory: Path.GetDirectoryName(asset.Path) + Path.DirectorySeparatorChar);
                        }
                        if (!wroteCopyLocalMetadata)
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
                    if (!library.IsTransitiveProjectReference(_lockFile, ref directProjectDependencies, 
                        _lockFile.GetLockFileTargetAlias(_lockFile.GetTargetAndReturnNullIfNotFound(_targetFramework, null))))
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

            private void WriteFrameworkReferences()
            {
                if (_task.DisableTransitiveFrameworkReferences)
                {
                    return;
                }

                HashSet<string> writtenFrameworkReferences = null;

                foreach (var library in _runtimeTarget.Libraries)
                {
                    foreach (var frameworkReference in library.FrameworkReferences)
                    {
                        if (writtenFrameworkReferences == null)
                        {
                            writtenFrameworkReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        }
                        if (writtenFrameworkReferences.Add(frameworkReference))
                        {
                            WriteItem(frameworkReference);
                        }
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

            private bool WriteCopyLocalMetadataIfNeeded(LockFileTargetLibrary package, string assetsFileName, string destinationSubDirectory = null)
            {
                bool shouldCopyLocal = true;
                if (_copyLocalPackageExclusions != null && _copyLocalPackageExclusions.Contains(package.Name))
                {
                    shouldCopyLocal = false;
                }
                bool shouldIncludeInPublish = shouldCopyLocal;
                if (shouldIncludeInPublish && _publishPackageExclusions != null && _publishPackageExclusions.Contains(package.Name))
                {
                    shouldIncludeInPublish = false;
                }

                if (!shouldCopyLocal&& !shouldIncludeInPublish)
                {
                    return false;
                }

                if (shouldCopyLocal)
                {
                    WriteMetadata(MetadataKeys.CopyLocal, "true");
                    if (!shouldIncludeInPublish)
                    {
                        WriteMetadata(MetadataKeys.CopyToPublishDirectory, "false");
                    }
                }
                WriteMetadata(
                    MetadataKeys.DestinationSubPath,
                    string.IsNullOrEmpty(destinationSubDirectory) ?
                        assetsFileName :
                        Path.Combine(destinationSubDirectory, assetsFileName));
                if (!string.IsNullOrEmpty(destinationSubDirectory))
                {
                    WriteMetadata(MetadataKeys.DestinationSubDirectory, destinationSubDirectory);
                }

                return true;
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

            private void ComputePackageExclusions()
            {
                var copyLocalPackageExclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var libraryLookup = _runtimeTarget.Libraries
                    .GroupBy(p => p.Name)
                    .Select(g => g.First())
                    .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

                // Only exclude platform packages for framework-dependent applications
                if ((!_task.IsSelfContained || string.IsNullOrEmpty(_runtimeTarget.RuntimeIdentifier)) &&
                    _task.PlatformLibraryName != null)
                {
                    // Exclude the platform library
                    var platformLibrary = _runtimeTarget.GetLibrary(_task.PlatformLibraryName);
                    if (platformLibrary != null)
                    {
                        copyLocalPackageExclusions.UnionWith(_runtimeTarget.GetPlatformExclusionList(platformLibrary, libraryLookup));

                        // If the platform library is not Microsoft.NETCore.App, treat it as an implicit dependency.
                        // This makes it so Microsoft.AspNet.* 2.x platforms also exclude Microsoft.NETCore.App files.
                        if (!String.Equals(platformLibrary.Name, NetCorePlatformLibrary, StringComparison.OrdinalIgnoreCase))
                        {
                            var library = _runtimeTarget.GetLibrary(NetCorePlatformLibrary);
                            if (library != null)
                            {
                                copyLocalPackageExclusions.UnionWith(_runtimeTarget.GetPlatformExclusionList(library, libraryLookup));
                            }
                        }
                    }
                }

                if (_task.PackageReferences != null)
                {
                    var excludeFromPublishPackageReferences = _task.PackageReferences
                        .Where(pr => pr.GetBooleanMetadata(MetadataKeys.Publish) == false)
                        .ToList();

                    if (excludeFromPublishPackageReferences.Any())
                    {

                        var topLevelDependencies = ProjectContext.GetTopLevelDependencies(_lockFile, _runtimeTarget);

                        //  Exclude transitive dependencies of excluded packages unless they are also dependencies
                        //  of non-excluded packages

                        HashSet<string> includedDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        HashSet<string> excludeFromPublishPackageIds = new HashSet<string>(
                            excludeFromPublishPackageReferences.Select(pr => pr.ItemSpec),
                            StringComparer.OrdinalIgnoreCase);

                        Stack<string> dependenciesToWalk = new Stack<string>(
                            topLevelDependencies.Except(excludeFromPublishPackageIds, StringComparer.OrdinalIgnoreCase));

                        while (dependenciesToWalk.Any())
                        {
                            var dependencyName = dependenciesToWalk.Pop();
                            if (!includedDependencies.Contains(dependencyName))
                            {
                                //  There may not be a library in the assets file if a referenced project has
                                //  PrivateAssets="all" for a package reference, and there is a package in the graph
                                //  that depends on the same packge.
                                if (libraryLookup.TryGetValue(dependencyName, out var library))
                                {
                                    includedDependencies.Add(dependencyName);
                                    foreach (var newDependency in library.Dependencies)
                                    {
                                        dependenciesToWalk.Push(newDependency.Id);
                                    }
                                }
                            }
                        }

                        var publishPackageExclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (var library in _runtimeTarget.Libraries)
                        {
                            //  Libraries explicitly marked as exclude from publish should be excluded from
                            //  publish even if there are other transitive dependencies to them
                            if (excludeFromPublishPackageIds.Contains(library.Name))
                            {
                                publishPackageExclusions.Add(library.Name);
                            }

                            if (!includedDependencies.Contains(library.Name))
                            {
                                publishPackageExclusions.Add(library.Name);
                            }
                        }

                        if (publishPackageExclusions.Any())
                        {
                            _publishPackageExclusions = publishPackageExclusions;
                        }
                    }
                }

                if (copyLocalPackageExclusions.Any())
                {
                    _copyLocalPackageExclusions = copyLocalPackageExclusions;
                }
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
