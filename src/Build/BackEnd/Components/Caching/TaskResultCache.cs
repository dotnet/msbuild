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
        internal const string CacheDirectoryPropertyName = "MSBuildTaskCacheDirectory";
        internal const string CacheEnabledPropertyName = "MSBuildTaskCacheEnabled";
        internal const string CacheSizeMBPropertyName = "MSBuildTaskCacheSizeMB";

        private const long BytesPerMB = 1024 * 1024;
        private const long DefaultCacheSizeMB = 10_000;
        private const string ManifestFileName = "manifest.bin";
        private const string ManifestMagic = "MSBuild Task Result Cache";
        private const int MaximumEventCount = 1_000_000;
        private const string TrimLockFileName = ".trim.lock";
        private static readonly TimeSpan s_trimInterval = TimeSpan.FromMinutes(1);

        private readonly string _cacheDirectory;
        private readonly string _entryDirectory;
        private readonly string _key;
        private readonly long _maximumCacheSizeBytes;
        private readonly IReadOnlyList<string> _outputPaths;

        private TaskResultCacheSession(
            string cacheDirectory,
            string entryDirectory,
            string key,
            long maximumCacheSizeBytes,
            IReadOnlyList<string> outputPaths)
        {
            _cacheDirectory = cacheDirectory;
            _entryDirectory = entryDirectory;
            _key = key;
            _maximumCacheSizeBytes = maximumCacheSizeBytes;
            _outputPaths = outputPaths;
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

            return Path.Combine(userCacheDirectory, "msbuild", "task-result-cache");
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
                keyResponse.OutputPaths!);

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
            IReadOnlyList<TaskResultCacheEvent> events,
            CancellationToken cancellationToken)
        {
            string temporaryDirectory = _entryDirectory + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                Directory.CreateDirectory(temporaryDirectory);
                var outputs = new List<CachedOutput>(_outputPaths.Count);
                for (int i = 0; i < _outputPaths.Count; i++)
                {
                    string outputPath = _outputPaths[i];
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
                    string payloadPath = GetPayloadPath(temporaryDirectory, i);
                    File.Copy(outputPath, payloadPath);
                    outputs.Add(new CachedOutput(
                        outputPath,
                        Present: true,
                        Length: new FileInfo(payloadPath).Length,
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
                if (outputs.Count != _outputPaths.Count)
                {
                    throw new InvalidDataException("The cached output count does not match the invocation.");
                }

                for (int i = 0; i < outputs.Count; i++)
                {
                    CachedOutput output = outputs[i];
                    if (!String.Equals(output.Path, _outputPaths[i], StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("The cached output paths do not match the invocation.");
                    }
                }

                var temporaryFiles = new string?[outputs.Count];
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
                        File.Copy(GetPayloadPath(_entryDirectory, i), temporaryFile);
                        if (new FileInfo(temporaryFile).Length != output.Length)
                        {
                            throw new InvalidDataException("A cached output payload is missing or corrupt.");
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

                        File.Delete(output.Path);
                        File.Move(temporaryFiles[i]!, output.Path);
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

        private static async ValueTask<TaskResultCacheKeyResponse> TryCreateKeyAsync(
            ITask task,
            ICollection<string> parameterNames,
            string projectFullPath,
            string projectDirectory,
            TaskResultCacheFileDigestCache fileDigestCache,
            CancellationToken cancellationToken)
        {
            Type taskType = task.GetType();
            string taskAssemblyPath = taskType.Assembly.Location;
            if (String.IsNullOrEmpty(taskAssemblyPath) || !File.Exists(taskAssemblyPath))
            {
                return new TaskResultCacheKeyResponse(
                    Success: false,
                    Reason: "The task assembly does not have a readable file location.");
            }

            if (!TryGetDeclaredPaths(
                    taskType,
                    task,
                    "DeclaredInputs",
                    projectDirectory,
                    out IReadOnlyList<string>? inputPaths,
                    out string? reason) ||
                !TryGetDeclaredPaths(
                    taskType,
                    task,
                    "DeclaredOutputs",
                    projectDirectory,
                    out IReadOnlyList<string>? outputPaths,
                    out reason))
            {
                return new TaskResultCacheKeyResponse(Success: false, Reason: reason);
            }

            try
            {
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

                writer.Write(outputPaths.Count);
                for (int i = 0; i < outputPaths.Count; i++)
                {
                    writer.Write(outputPaths[i]);
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
                    ToHex(hash.Hash!),
                    outputPaths);
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

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2070",
            Justification = "Declared-IO cache binding reflects only over task types, which are already rooted by task loading.")]
        private static bool TryGetDeclaredPaths(
            Type taskType,
            ITask task,
            string propertyName,
            string projectDirectory,
            [NotNullWhen(true)] out IReadOnlyList<string>? paths,
            out string? reason)
        {
            paths = null;
            reason = null;
            PropertyInfo? property = taskType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property?.GetMethod is null || property.GetValue(task) is not ITaskItem[] items)
            {
                reason = $"Task property \"{propertyName}\" is not a readable ITaskItem array.";
                return false;
            }

            StringComparer pathComparer = NativeMethodsShared.IsWindows
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var seen = new HashSet<string>(pathComparer);
            var result = new List<string>(items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                string path = FileUtilities.NormalizePath(projectDirectory, items[i].ItemSpec);
                if (seen.Add(path))
                {
                    result.Add(path);
                }
            }

            result.Sort(pathComparer);
            paths = result;
            return true;
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
            if (outputCount < 0 || outputCount > _outputPaths.Count)
            {
                throw new InvalidDataException("The cache manifest output count is invalid.");
            }

            var outputs = new CachedOutput[outputCount];
            for (int i = 0; i < outputCount; i++)
            {
                string path = reader.ReadString();
                bool present = reader.ReadBoolean();
                outputs[i] = present
                    ? new CachedOutput(
                        path,
                        present,
                        reader.ReadInt64(),
                        (FileAttributes)reader.ReadInt32(),
                        reader.ReadInt32())
                    : new CachedOutput(
                        path,
                        present,
                        Length: 0,
                        Attributes: 0,
                        UnixFileMode: 0);
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
            IReadOnlyList<string>? OutputPaths = null,
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
