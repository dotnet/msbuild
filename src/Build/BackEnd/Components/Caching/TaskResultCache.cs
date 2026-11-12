// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable enable

namespace Microsoft.Build.BackEnd.Components.Caching
{
    internal enum TaskResultCacheOpenResult
    {
        Ineligible,
        Unavailable,
        Miss,
        Hit,
    }

    internal readonly struct TaskResultCacheOpenResponse
    {
        internal TaskResultCacheOpenResponse(
            TaskResultCacheOpenResult result,
            TaskResultCacheSession? session = null,
            IReadOnlyList<TaskResultCacheEvent>? events = null,
            string? reason = null)
        {
            Result = result;
            Session = session;
            Events = events;
            Reason = reason;
        }

        internal TaskResultCacheOpenResult Result { get; }

        internal TaskResultCacheSession? Session { get; }

        internal IReadOnlyList<TaskResultCacheEvent>? Events { get; }

        internal string? Reason { get; }
    }

    internal readonly struct TaskResultCacheStoreResponse
    {
        internal TaskResultCacheStoreResponse(bool success, string? reason = null)
        {
            Success = success;
            Reason = reason;
        }

        internal bool Success { get; }

        internal string? Reason { get; }
    }

    internal sealed class TaskResultCacheSession
    {
        internal const string CacheDirectoryPropertyName = "MSBuildContentCacheDirectory";
        internal const string CacheEnabledPropertyName = "MSBuildContentCacheEnabled";
        internal const string CacheSizeMBPropertyName = "MSBuildContentCacheSizeMB";

        private const long BytesPerMB = 1024 * 1024;
        private const long DefaultCacheSizeMB = 10_000;
        private const string ManifestFileName = "manifest.bin";
        private const string ManifestMagic = "MSBuild Task Result Cache";
        private const int MaximumEventCount = 1_000_000;
        private const int MaximumOutputCount = 1_000_000;
        private const int Sha256HashLength = 32;
        private const string TrimLockFileName = ".trim.lock";
        private static readonly TimeSpan s_trimInterval = TimeSpan.FromHours(1);

        private readonly string _cacheDirectory;
        private readonly string _entryDirectory;
        private readonly TaskResultCacheFileDigestCache _fileDigestCache;
        private readonly string _key;
        private readonly long _maximumCacheSizeBytes;

        private TaskResultCacheSession(
            string cacheDirectory,
            string entryDirectory,
            string key,
            long maximumCacheSizeBytes,
            TaskResultCacheFileDigestCache fileDigestCache)
        {
            _cacheDirectory = cacheDirectory;
            _entryDirectory = entryDirectory;
            _fileDigestCache = fileDigestCache;
            _key = key;
            _maximumCacheSizeBytes = maximumCacheSizeBytes;
        }

        internal string Key => _key;

        internal static string? ResolveCacheDirectory(string configuredDirectory, string enabled)
        {
            if (!String.IsNullOrWhiteSpace(configuredDirectory))
            {
                return configuredDirectory;
            }

            return ConversionUtilities.TryConvertStringToBool(enabled, out bool isEnabled) && isEnabled
                ? GetDefaultCacheDirectory()
                : null;
        }

        internal static string GetDefaultCacheDirectory()
        {
            string userCacheDirectory;
            if (NativeMethodsShared.IsUnixLike)
            {
                string? xdgCacheDirectory = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
                userCacheDirectory = !String.IsNullOrWhiteSpace(xdgCacheDirectory) &&
                    Path.IsPathRooted(xdgCacheDirectory)
                    ? xdgCacheDirectory
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".cache");
            }
            else
            {
                userCacheDirectory = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
            }

            return Path.Combine(userCacheDirectory, "msbuild", "content-cache");
        }

        internal static async ValueTask<TaskResultCacheOpenResponse> TryOpenAsync(
            ITask task,
            ICollection<string> parameterNames,
            string projectFullPath,
            string projectDirectory,
            string cacheDirectory,
            string cacheSizeMB,
            TaskResultCacheFileDigestCache fileDigestCache,
            CancellationToken cancellationToken)
        {
            if (!TryResolveCacheSizeBytes(cacheSizeMB, out long maximumCacheSizeBytes))
            {
                return new TaskResultCacheOpenResponse(
                    TaskResultCacheOpenResult.Unavailable,
                    reason: $"The {CacheSizeMBPropertyName} property must be a non-negative integer.");
            }

            TaskResultCacheKeyResponse keyResponse = await TryCreateKeyAsync(
                    task,
                    parameterNames,
                    projectFullPath,
                    projectDirectory,
                    fileDigestCache,
                    cancellationToken);
            if (!keyResponse.Success)
            {
                return new TaskResultCacheOpenResponse(
                    TaskResultCacheOpenResult.Ineligible,
                    reason: keyResponse.Reason);
            }

            string normalizedCacheDirectory;
            try
            {
                normalizedCacheDirectory = Path.IsPathRooted(cacheDirectory)
                    ? FileUtilities.NormalizePath(cacheDirectory)
                    : FileUtilities.NormalizePath(projectDirectory, cacheDirectory);
            }
            catch (Exception e) when (IsExpectedCacheException(e))
            {
                return new TaskResultCacheOpenResponse(
                    TaskResultCacheOpenResult.Unavailable,
                    reason: e.Message);
            }

            string key = keyResponse.Key!;
            string entryDirectory = Path.Combine(
                normalizedCacheDirectory,
                key.Substring(0, 2),
                key.Substring(2));
            var session = new TaskResultCacheSession(
                normalizedCacheDirectory,
                entryDirectory,
                key,
                maximumCacheSizeBytes,
                fileDigestCache);

            if (!Directory.Exists(entryDirectory))
            {
                return new TaskResultCacheOpenResponse(
                    TaskResultCacheOpenResult.Miss,
                    session);
            }

            TaskResultCacheRestoreResponse restoreResponse =
                await session.TryRestoreAsync(cancellationToken);

            if (restoreResponse.Success)
            {
                session.TouchEntryBestEffort();
                return new TaskResultCacheOpenResponse(
                    TaskResultCacheOpenResult.Hit,
                    session,
                    restoreResponse.Events);
            }

            TryDeleteEntryBestEffort(session._entryDirectory);

            return new TaskResultCacheOpenResponse(
                TaskResultCacheOpenResult.Miss,
                session,
                reason: restoreResponse.Reason);
        }

        internal async ValueTask<TaskResultCacheStoreResponse> TryStoreAsync(
            IReadOnlyList<string> outputPaths,
            IReadOnlyList<TaskResultCacheEvent> events,
            CancellationToken cancellationToken)
        {
            string temporaryDirectory = _entryDirectory + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                Directory.CreateDirectory(temporaryDirectory);
                var outputs = new List<CachedOutput>(outputPaths.Count);
                for (int i = 0; i < outputPaths.Count; i++)
                {
                    string outputPath = outputPaths[i];
                    if (Directory.Exists(outputPath))
                    {
                        return new TaskResultCacheStoreResponse(
                            success: false,
                            $"Declared output \"{outputPath}\" is a directory.");
                    }

                    if (!File.Exists(outputPath))
                    {
                        outputs.Add(new CachedOutput(
                            outputPath,
                            Present: false,
                            Length: 0,
                            Hash: [],
                            Attributes: 0,
                            UnixFileMode: 0));
                        continue;
                    }

                    FileAttributes attributes = File.GetAttributes(outputPath);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return new TaskResultCacheStoreResponse(
                            success: false,
                            $"Declared output \"{outputPath}\" is not a regular file.");
                    }

#if NET
                    int unixFileMode = OperatingSystem.IsWindows()
                        ? 0
                        : (int)File.GetUnixFileMode(outputPath);
#else
                    const int unixFileMode = 0;
#endif
                    TaskResultCacheFileDigest digest =
                        await _fileDigestCache.GetDigestAsync(outputPath, cancellationToken);
                    string payloadPath = GetPayloadPath(temporaryDirectory, i);
                    File.Copy(outputPath, payloadPath);
                    outputs.Add(new CachedOutput(
                        outputPath,
                        Present: true,
                        digest.Length,
                        digest.Hash,
                        Attributes: attributes,
                        UnixFileMode: unixFileMode));
                }

                await WriteManifestAsync(
                    Path.Combine(temporaryDirectory, ManifestFileName),
                    outputs,
                    events,
                    cancellationToken);
                try
                {
                    Directory.Move(temporaryDirectory, _entryDirectory);
                }
                catch (IOException) when (Directory.Exists(_entryDirectory))
                {
                }

                TrimCacheBestEffort();
                return new TaskResultCacheStoreResponse(success: true);
            }
            catch (Exception e) when (IsExpectedCacheException(e))
            {
                return new TaskResultCacheStoreResponse(success: false, e.Message);
            }
            finally
            {
                DeleteDirectoryBestEffort(temporaryDirectory);
            }
        }

        internal static bool TryResolveCacheSizeBytes(string configuredSizeMB, out long maximumCacheSizeBytes)
        {
            long sizeMB;
            if (String.IsNullOrWhiteSpace(configuredSizeMB))
            {
                sizeMB = DefaultCacheSizeMB;
            }
            else if (!long.TryParse(
                         configuredSizeMB,
                         NumberStyles.Integer,
                         CultureInfo.InvariantCulture,
                         out sizeMB) ||
                     sizeMB < 0)
            {
                maximumCacheSizeBytes = 0;
                return false;
            }

            try
            {
                maximumCacheSizeBytes = checked(sizeMB * BytesPerMB);
                return true;
            }
            catch (OverflowException)
            {
                maximumCacheSizeBytes = 0;
                return false;
            }
        }

        private async ValueTask<TaskResultCacheRestoreResponse> TryRestoreAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                TaskResultCacheManifest manifest = await ReadManifestAsync(
                    Path.Combine(_entryDirectory, ManifestFileName),
                    cancellationToken);
                IReadOnlyList<CachedOutput> outputs = manifest.Outputs;

                var temporaryFiles = new string?[outputs.Count];
                var backupFiles = new string?[outputs.Count];
                var publishedOutputs = new bool[outputs.Count];
                bool restoreSucceeded = false;
                try
                {
                    for (int i = 0; i < outputs.Count; i++)
                    {
                        CachedOutput output = outputs[i];
                        if (!output.Present)
                        {
                            continue;
                        }

                        string? outputDirectory = Path.GetDirectoryName(output.Path);
                        if (!String.IsNullOrEmpty(outputDirectory))
                        {
                            Directory.CreateDirectory(outputDirectory);
                        }

                        string temporaryFile =
                            output.Path + "." + Guid.NewGuid().ToString("N") + ".msbuild-cache";
                        temporaryFiles[i] = temporaryFile;
                        TaskResultCacheFileDigest digest =
                            await CopyPayloadAsync(
                                GetPayloadPath(_entryDirectory, i),
                                temporaryFile,
                                cancellationToken);
                        if (digest.Length != output.Length ||
                            !digest.Hash.AsSpan().SequenceEqual(output.Hash))
                        {
                            throw new InvalidDataException("A cached output payload is missing or corrupt.");
                        }
                    }

                    for (int i = 0; i < outputs.Count; i++)
                    {
                        string outputPath = outputs[i].Path;
                        if (File.Exists(outputPath))
                        {
                            string backupFile =
                                outputPath + "." + Guid.NewGuid().ToString("N") + ".msbuild-cache-backup";
                            File.Move(outputPath, backupFile);
                            backupFiles[i] = backupFile;
                        }
                    }

                    for (int i = 0; i < outputs.Count; i++)
                    {
                        CachedOutput output = outputs[i];
                        if (!output.Present)
                        {
                            File.Delete(output.Path);
                            continue;
                        }

                        File.Move(temporaryFiles[i]!, output.Path);
                        publishedOutputs[i] = true;
                        temporaryFiles[i] = null;
                        File.SetLastWriteTimeUtc(output.Path, DateTime.UtcNow);
#if NET
                        if (!OperatingSystem.IsWindows())
                        {
                            File.SetUnixFileMode(output.Path, (UnixFileMode)output.UnixFileMode);
                        }
#endif
                        File.SetAttributes(output.Path, output.Attributes);
                    }

                    for (int i = 0; i < outputs.Count; i++)
                    {
                        CachedOutput output = outputs[i];
                        if (output.Present)
                        {
                            _fileDigestCache.Seed(
                                output.Path,
                                new TaskResultCacheFileDigest(output.Length, output.Hash));
                        }
                    }

                    restoreSucceeded = true;
                }
                catch (Exception e) when (IsExpectedCacheException(e))
                {
                    RollbackRestoredOutputs(outputs, backupFiles, publishedOutputs);
                    throw;
                }
                finally
                {
                    for (int i = 0; i < temporaryFiles.Length; i++)
                    {
                        string? temporaryFile = temporaryFiles[i];
                        if (temporaryFile is not null)
                        {
                            File.Delete(temporaryFile);
                        }
                    }

                    if (restoreSucceeded)
                    {
                        for (int i = 0; i < backupFiles.Length; i++)
                        {
                            DeleteFileBestEffort(backupFiles[i]);
                        }
                    }
                }

                return new TaskResultCacheRestoreResponse(
                    Success: true,
                    manifest.Events);
            }
            catch (Exception e) when (IsExpectedCacheException(e))
            {
                return new TaskResultCacheRestoreResponse(Success: false, Reason: e.Message);
            }
        }

        private static void RollbackRestoredOutputs(
            IReadOnlyList<CachedOutput> outputs,
            string?[] backupFiles,
            bool[] publishedOutputs)
        {
            Exception? rollbackException = null;
            for (int i = outputs.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (publishedOutputs[i])
                    {
                        DeleteFile(outputs[i].Path);
                    }

                    string? backupFile = backupFiles[i];
                    if (backupFile is not null)
                    {
                        File.Move(backupFile, outputs[i].Path);
                        backupFiles[i] = null;
                    }
                }
                catch (Exception e) when (IsExpectedCacheException(e))
                {
                    rollbackException ??= e;
                }
            }

            if (rollbackException is not null)
            {
                throw new IOException(
                    "The content cache could not restore the previous output state.",
                    rollbackException);
            }
        }

        private static void DeleteFile(string path)
        {
            if (NativeMethodsShared.IsWindows && File.Exists(path))
            {
                File.SetAttributes(
                    path,
                    File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            }

            File.Delete(path);
        }

        private static void DeleteFileBestEffort(string? path)
        {
            if (path is null)
            {
                return;
            }

            try
            {
                DeleteFile(path);
            }
            catch (Exception e) when (IsExpectedCacheException(e))
            {
            }
        }

        private static async ValueTask<TaskResultCacheFileDigest> CopyPayloadAsync(
            string sourcePath,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            using var source = CreateAsyncFileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var destination = CreateAsyncFileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using SHA256 hash = SHA256.Create();
            using var hashStream = new CryptoStream(destination, hash, CryptoStreamMode.Write, leaveOpen: true);
            await source.CopyToAsync(hashStream, 81920, cancellationToken);
            hashStream.FlushFinalBlock();
            return new TaskResultCacheFileDigest(destination.Position, hash.Hash!);
        }

        private static async ValueTask<TaskResultCacheKeyResponse> TryCreateKeyAsync(
            ITask task,
            ICollection<string> parameterNames,
            string projectFullPath,
            string projectDirectory,
            TaskResultCacheFileDigestCache fileDigestCache,
            CancellationToken cancellationToken)
        {
            try
            {
                Type taskType = task.GetType();
                string taskAssemblyPath = taskType.Assembly.Location;
                if (String.IsNullOrEmpty(taskAssemblyPath) || !File.Exists(taskAssemblyPath))
                {
                    return new TaskResultCacheKeyResponse(
                        Success: false,
                        Reason: "The task assembly does not have a readable file location.");
                }

                if (!TryGetFileProperty(task, "OutputFiles", out _, out string? reason) ||
                    !TryGetFilePaths(
                        task,
                        "InputFiles",
                        projectDirectory,
                        out IReadOnlyList<string>? inputPaths,
                        out reason))
                {
                    return new TaskResultCacheKeyResponse(
                        Success: false,
                        Reason: reason);
                }

                using SHA256 hash = SHA256.Create();
                using var hashStream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
                using ITranslator translator = BinaryTranslator.GetWriteTranslator(hashStream);
                BinaryWriter writer = translator.Writer;

                writer.Write(taskType.AssemblyQualifiedName!);
                writer.Write(typeof(Microsoft.Build.Execution.BuildManager).Module.ModuleVersionId.ToByteArray());
                writer.Write(RuntimeInformation.FrameworkDescription);
                writer.Write(RuntimeInformation.OSArchitecture.ToString());
                writer.Write(RuntimeInformation.ProcessArchitecture.ToString());
                writer.Write(CultureInfo.CurrentCulture.Name);
                writer.Write(CultureInfo.CurrentUICulture.Name);
                writer.Write(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "OSX" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" :
                    "Unknown");
                writer.Write(FileUtilities.NormalizePath(projectFullPath));
                writer.Write(FileUtilities.NormalizePath(projectDirectory));

                await WriteFileDigestAsync(
                    writer,
                    taskAssemblyPath,
                    fileDigestCache,
                    cancellationToken);

                if (!TryWriteTaskParameters(translator, task, parameterNames, out reason))
                {
                    return new TaskResultCacheKeyResponse(Success: false, Reason: reason);
                }

                writer.Write(inputPaths.Count);
                for (int i = 0; i < inputPaths.Count; i++)
                {
                    string inputPath = inputPaths[i];
                    writer.Write(inputPath);
                    if (Directory.Exists(inputPath))
                    {
                        return new TaskResultCacheKeyResponse(
                            Success: false,
                            Reason: $"Declared input \"{inputPath}\" is a directory.");
                    }

                    if (!File.Exists(inputPath))
                    {
                        writer.Write((byte)0);
                        continue;
                    }

                    writer.Write((byte)1);
                    await WriteFileDigestAsync(
                        writer,
                        inputPath,
                        fileDigestCache,
                        cancellationToken);
                }

                writer.Flush();
                hashStream.FlushFinalBlock();
                return new TaskResultCacheKeyResponse(
                    Success: true,
                    ToHex(hash.Hash!));
            }
            catch (Exception e) when (IsExpectedCacheException(e) || e is TargetInvocationException)
            {
                return new TaskResultCacheKeyResponse(
                    Success: false,
                    Reason: e.InnerException?.Message ?? e.Message);
            }
        }

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2075",
            Justification = "Declared-IO cache binding reflects only over task types, which are already rooted by task loading.")]
        private static bool TryWriteTaskParameters(
            ITranslator translator,
            ITask task,
            ICollection<string> parameterNames,
            out string? reason)
        {
            BinaryWriter writer = translator.Writer;
            reason = null;
            Type taskType = task.GetType();
            PropertyInfo[] properties = taskType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var propertiesByName = new Dictionary<string, PropertyInfo>(MSBuildNameIgnoreCaseComparer.Default);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (property.GetIndexParameters().Length == 0)
                {
                    propertiesByName[property.Name] = property;
                }
            }

            var sortedParameterNames = new List<string>(parameterNames);
            sortedParameterNames.Sort(StringComparer.OrdinalIgnoreCase);
            writer.Write(sortedParameterNames.Count);
            for (int i = 0; i < sortedParameterNames.Count; i++)
            {
                string parameterName = sortedParameterNames[i];
                if (!propertiesByName.TryGetValue(parameterName, out PropertyInfo? property) ||
                    property.GetMethod is null)
                {
                    reason = $"Task parameter \"{parameterName}\" does not have a readable property.";
                    return false;
                }

                writer.Write(parameterName);
                writer.Write(property.PropertyType.AssemblyQualifiedName!);
                if (!TryWriteValue(translator, property.GetValue(task), out reason))
                {
                    reason = $"Task parameter \"{parameterName}\" cannot be cached: {reason}";
                    return false;
                }
            }

            return true;
        }

        private static bool TryWriteValue(
            ITranslator translator,
            object? value,
            out string? reason)
        {
            BinaryWriter writer = translator.Writer;
            reason = null;
            if (value is null)
            {
                writer.Write((byte)0);
                return true;
            }

            writer.Write((byte)1);
            switch (value)
            {
                case string stringValue:
                    writer.Write((byte)1);
                    writer.Write(stringValue);
                    return true;

                case ITaskItem taskItem:
                    writer.Write((byte)2);
                    new TaskParameter(taskItem).Translate(translator);
                    return true;

                case Array array:
                    writer.Write((byte)3);
                    writer.Write(array.Length);
                    for (int i = 0; i < array.Length; i++)
                    {
                        if (!TryWriteValue(translator, array.GetValue(i), out reason))
                        {
                            return false;
                        }
                    }

                    return true;

                case DateTime dateTime:
                    writer.Write((byte)4);
                    writer.Write(dateTime.ToBinary());
                    return true;

                case decimal decimalValue:
                    writer.Write((byte)5);
                    int[] decimalBits = decimal.GetBits(decimalValue);
                    for (int i = 0; i < decimalBits.Length; i++)
                    {
                        writer.Write(decimalBits[i]);
                    }

                    return true;

                case double doubleValue:
                    writer.Write((byte)6);
                    writer.Write(BitConverter.DoubleToInt64Bits(doubleValue));
                    return true;

                case float floatValue:
                    writer.Write((byte)7);
                    writer.Write(BitConverter.GetBytes(floatValue));
                    return true;

                case IConvertible convertible:
                    writer.Write((byte)8);
                    writer.Write(value.GetType().AssemblyQualifiedName!);
                    writer.Write(convertible.ToString(CultureInfo.InvariantCulture)!);
                    return true;

                default:
                    reason = $"value type \"{value.GetType().FullName}\" is unsupported.";
                    return false;
            }
        }

        internal static bool TryGetFilePaths(
            ITask task,
            string propertyName,
            string projectDirectory,
            [NotNullWhen(true)] out IReadOnlyList<string>? paths,
            out string? reason)
        {
            try
            {
                paths = null;
                reason = null;
                if (!TryGetFileProperty(task, propertyName, out PropertyInfo? property, out reason))
                {
                    return false;
                }

                if (property.GetValue(task) is not IEnumerable<string> values)
                {
                    reason =
                        $"Task property \"{propertyName}\" must return a non-null IEnumerable<string>.";
                    return false;
                }

                StringComparer pathComparer = NativeMethodsShared.IsWindows
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal;
                var seen = new HashSet<string>(pathComparer);
                var result = new List<string>();
                foreach (string value in values)
                {
                    string path = FileUtilities.NormalizePath(projectDirectory, value);
                    if (seen.Add(path))
                    {
                        result.Add(path);
                    }
                }

                result.Sort(pathComparer);
                paths = result;
                return true;
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                paths = null;
                reason = e.InnerException?.Message ?? e.Message;
                return false;
            }
        }

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2075",
            Justification = "Declared-IO cache binding reflects only over task types, which are already rooted by task loading.")]
        private static bool TryGetFileProperty(
            ITask task,
            string propertyName,
            [NotNullWhen(true)] out PropertyInfo? property,
            out string? reason)
        {
            try
            {
                property = task.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (property?.GetGetMethod() is not null &&
                    property.GetSetMethod() is null &&
                    property.GetIndexParameters().Length == 0 &&
                    typeof(IEnumerable<string>).IsAssignableFrom(property.PropertyType))
                {
                    reason = null;
                    return true;
                }

                property = null;
                reason = $"Task property \"{propertyName}\" must be a publicly readable IEnumerable<string> without a public setter.";
                return false;
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                property = null;
                reason = e.InnerException?.Message ?? e.Message;
                return false;
            }
        }

        private async ValueTask WriteManifestAsync(
            string manifestPath,
            IReadOnlyList<CachedOutput> outputs,
            IReadOnlyList<TaskResultCacheEvent> events,
            CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true);
            writer.Write(ManifestMagic);
            writer.Write(_key);
            writer.Write(outputs.Count);
            for (int i = 0; i < outputs.Count; i++)
            {
                CachedOutput output = outputs[i];
                writer.Write(output.Path);
                writer.Write(output.Present);
                if (output.Present)
                {
                    writer.Write(output.Length);
                    writer.Write(output.Hash);
                    writer.Write((int)output.Attributes);
                    writer.Write(output.UnixFileMode);
                }
            }

            writer.Write(events.Count);
            for (int i = 0; i < events.Count; i++)
            {
                events[i].Write(writer);
            }

            writer.Flush();
            buffer.Position = 0;
            using var stream = CreateAsyncFileStream(
                manifestPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            await buffer.CopyToAsync(stream, 81920, cancellationToken);
        }

        private async ValueTask<TaskResultCacheManifest> ReadManifestAsync(
            string manifestPath,
            CancellationToken cancellationToken)
        {
            using var stream = CreateAsyncFileStream(
                manifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, 81920, cancellationToken);
            buffer.Position = 0;
            using var reader = new BinaryReader(buffer, Encoding.UTF8, leaveOpen: false);
            if (!String.Equals(reader.ReadString(), ManifestMagic, StringComparison.Ordinal) ||
                !String.Equals(reader.ReadString(), _key, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The cache manifest header is invalid.");
            }

            int outputCount = reader.ReadInt32();
            if (outputCount < 0 || outputCount > MaximumOutputCount)
            {
                throw new InvalidDataException("The cache manifest output count is invalid.");
            }

            var outputs = new CachedOutput[outputCount];
            for (int i = 0; i < outputCount; i++)
            {
                string path = reader.ReadString();
                bool present = reader.ReadBoolean();
                if (present)
                {
                    long length = reader.ReadInt64();
                    byte[] hash = reader.ReadBytes(Sha256HashLength);
                    if (hash.Length != Sha256HashLength)
                    {
                        throw new InvalidDataException("The cache manifest output digest is invalid.");
                    }

                    outputs[i] = new CachedOutput(
                        path,
                        present,
                        length,
                        hash,
                        (FileAttributes)reader.ReadInt32(),
                        reader.ReadInt32());
                }
                else
                {
                    outputs[i] = new CachedOutput(
                        path,
                        present,
                        Length: 0,
                        Hash: [],
                        Attributes: 0,
                        UnixFileMode: 0);
                }
            }

            int eventCount = reader.ReadInt32();
            if (eventCount < 0 || eventCount > MaximumEventCount)
            {
                throw new InvalidDataException("The cache manifest event count is invalid.");
            }

            var cachedEvents = new TaskResultCacheEvent[eventCount];
            for (int i = 0; i < eventCount; i++)
            {
                cachedEvents[i] = TaskResultCacheEvent.Read(reader);
            }

            if (buffer.Position != buffer.Length)
            {
                throw new InvalidDataException("The cache manifest contains trailing data.");
            }

            return new TaskResultCacheManifest(outputs, cachedEvents);
        }

        private static async ValueTask WriteFileDigestAsync(
            BinaryWriter writer,
            string path,
            TaskResultCacheFileDigestCache fileDigestCache,
            CancellationToken cancellationToken)
        {
            TaskResultCacheFileDigest digest =
                await fileDigestCache.GetDigestAsync(path, cancellationToken);
            writer.Write(digest.Length);
            writer.Write(digest.Hash.Length);
            writer.Write(digest.Hash);
        }

        private static FileStream CreateAsyncFileStream(
            string path,
            FileMode mode,
            FileAccess access,
            FileShare share)
        {
            return new FileStream(
                path,
                mode,
                access,
                share,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }

        private static string ToHex(byte[] bytes)
        {
            const string hex = "0123456789abcdef";
            var characters = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                characters[i * 2] = hex[bytes[i] >> 4];
                characters[(i * 2) + 1] = hex[bytes[i] & 0x0F];
            }

            return new string(characters);
        }

        private static string GetPayloadPath(string entryDirectory, int index)
        {
            return Path.Combine(entryDirectory, index.ToString(CultureInfo.InvariantCulture) + ".bin");
        }

        private void TouchEntryBestEffort()
        {
            try
            {
                File.SetLastWriteTimeUtc(
                    Path.Combine(_entryDirectory, ManifestFileName),
                    DateTime.UtcNow);
            }
            catch (Exception e) when (IsExpectedCacheException(e))
            {
            }
        }

        private void TrimCacheBestEffort()
        {
            if (_maximumCacheSizeBytes == 0)
            {
                return;
            }

            try
            {
                using var trimLock = new FileStream(
                    Path.Combine(_cacheDirectory, TrimLockFileName),
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
                long nowTicks = DateTime.UtcNow.Ticks;
                if (trimLock.Length == sizeof(long))
                {
                    using var reader = new BinaryReader(trimLock, Encoding.UTF8, leaveOpen: true);
                    if (reader.ReadInt64() > nowTicks)
                    {
                        return;
                    }
                }

                TrimCache();

                trimLock.Position = 0;
                using var writer = new BinaryWriter(trimLock, Encoding.UTF8, leaveOpen: true);
                writer.Write(DateTime.UtcNow.Add(s_trimInterval).Ticks);
                trimLock.SetLength(sizeof(long));
            }
            catch (Exception e) when (IsExpectedCacheException(e))
            {
            }
        }

        private void TrimCache()
        {
            var entries = new List<CacheEntry>();
            long totalSize = 0;
            foreach (string shardDirectory in Directory.EnumerateDirectories(_cacheDirectory))
            {
                if (!IsHexName(Path.GetFileName(shardDirectory), expectedLength: 2))
                {
                    continue;
                }

                foreach (string entryDirectory in Directory.EnumerateDirectories(shardDirectory))
                {
                    string name = Path.GetFileName(entryDirectory);
                    if (IsHexName(name, expectedLength: 62))
                    {
                        if (TryGetCacheEntry(entryDirectory, out CacheEntry entry))
                        {
                            entries.Add(entry);
                            totalSize += entry.Size;
                        }
                    }
                    else if (IsDeletionTombstone(name))
                    {
                        DeleteDirectoryBestEffort(entryDirectory);
                    }
                }
            }

            if (totalSize <= _maximumCacheSizeBytes)
            {
                return;
            }

            entries.Sort(static (left, right) =>
            {
                int comparison = left.LastAccessTimeUtc.CompareTo(right.LastAccessTimeUtc);
                return comparison != 0
                    ? comparison
                    : StringComparer.Ordinal.Compare(left.Path, right.Path);
            });
            for (int i = 0; i < entries.Count && totalSize > _maximumCacheSizeBytes; i++)
            {
                CacheEntry entry = entries[i];
                if (TryDeleteEntryBestEffort(entry.Path))
                {
                    totalSize -= entry.Size;
                }
            }
        }

        private static bool TryGetCacheEntry(string entryDirectory, out CacheEntry entry)
        {
            try
            {
                long size = 0;
                foreach (string file in Directory.EnumerateFiles(entryDirectory))
                {
                    size += new FileInfo(file).Length;
                }

                entry = new CacheEntry(
                    entryDirectory,
                    size,
                    File.GetLastWriteTimeUtc(Path.Combine(entryDirectory, ManifestFileName)));
                return true;
            }
            catch (Exception e) when (IsExpectedCacheException(e))
            {
                entry = default;
                return false;
            }
        }

        private static bool IsHexName(string name, int expectedLength) =>
            name is not null && IsHexName(name.AsSpan(), expectedLength);

        private static bool IsDeletionTombstone(string name) =>
            name.Length > 62 &&
            IsHexName(name.AsSpan(0, 62), expectedLength: 62) &&
            name.AsSpan(62).StartsWith(".delete-", StringComparison.Ordinal);

        private static bool IsHexName(ReadOnlySpan<char> name, int expectedLength)
        {
            if (name.Length != expectedLength)
            {
                return false;
            }

            for (int i = 0; i < name.Length; i++)
            {
                if (name[i] is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryDeleteEntryBestEffort(string path)
        {
            string deletionPath = path + ".delete-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.Move(path, deletionPath);
            }
            catch (Exception e) when (IsExpectedCacheException(e))
            {
                return false;
            }

            DeleteDirectoryBestEffort(deletionPath);
            return true;
        }

        private static void DeleteDirectoryBestEffort(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (Exception e) when (IsExpectedCacheException(e))
            {
            }
        }

        private static bool IsExpectedCacheException(Exception e)
        {
            return e is IOException or
                UnauthorizedAccessException or
                InvalidDataException or
                CryptographicException or
                ArgumentException or
                NotSupportedException;
        }

        private readonly record struct TaskResultCacheKeyResponse(
            bool Success,
            string? Key = null,
            string? Reason = null
        );

        private readonly record struct TaskResultCacheRestoreResponse(
            bool Success,
            IReadOnlyList<TaskResultCacheEvent>? Events = null,
            string? Reason = null
        );

        private readonly record struct TaskResultCacheManifest(
            IReadOnlyList<CachedOutput> Outputs,
            IReadOnlyList<TaskResultCacheEvent> Events
        );

        private readonly record struct CachedOutput(
            string Path,
            bool Present,
            long Length,
            byte[] Hash,
            FileAttributes Attributes,
            int UnixFileMode
        );

        private readonly record struct CacheEntry(
            string Path,
            long Size,
            DateTime LastAccessTimeUtc
        );
    }
}
