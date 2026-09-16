// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
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

#nullable disable

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
            TaskResultCacheSession session = null,
            IReadOnlyList<TaskResultCacheEvent> events = null,
            string reason = null)
        {
            Result = result;
            Session = session;
            Events = events;
            Reason = reason;
        }

        internal TaskResultCacheOpenResult Result { get; }

        internal TaskResultCacheSession Session { get; }

        internal IReadOnlyList<TaskResultCacheEvent> Events { get; }

        internal string Reason { get; }
    }

    internal readonly struct TaskResultCacheStoreResponse
    {
        internal TaskResultCacheStoreResponse(bool success, string reason = null)
        {
            Success = success;
            Reason = reason;
        }

        internal bool Success { get; }

        internal string Reason { get; }
    }

    internal sealed class TaskResultCacheSession
    {
        internal const string CacheDirectoryPropertyName = "MSBuildTaskCacheDirectory";
        internal const string CacheEnabledPropertyName = "MSBuildTaskCacheEnabled";

        private const string ManifestFileName = "manifest.bin";
        private const string ManifestMagic = "MSBuild Task Result Cache";
        private const int MaximumEventCount = 1_000_000;

        private readonly string _entryDirectory;
        private readonly string _key;
        private readonly IReadOnlyList<string> _outputPaths;

        private TaskResultCacheSession(
            string entryDirectory,
            string key,
            IReadOnlyList<string> outputPaths)
        {
            _entryDirectory = entryDirectory;
            _key = key;
            _outputPaths = outputPaths;
        }

        internal string Key => _key;

        internal static string ResolveCacheDirectory(string configuredDirectory, string enabled)
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
                string xdgCacheDirectory = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
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
            TaskResultCacheFileDigestCache fileDigestCache,
            CancellationToken cancellationToken)
        {
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

            string entryDirectory = Path.Combine(
                normalizedCacheDirectory,
                keyResponse.Key.Substring(0, 2),
                keyResponse.Key.Substring(2));
            var session = new TaskResultCacheSession(
                entryDirectory,
                keyResponse.Key,
                keyResponse.OutputPaths);

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
                return new TaskResultCacheOpenResponse(
                    TaskResultCacheOpenResult.Hit,
                    session,
                    restoreResponse.Events);
            }

            if (!session.QuarantineEntryBestEffort())
            {
                return new TaskResultCacheOpenResponse(
                    TaskResultCacheOpenResult.Unavailable,
                    reason: restoreResponse.Reason);
            }

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
                            present: false,
                            length: 0,
                            hash: null,
                            attributes: 0,
                            unixFileMode: 0));
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
                    (byte[] hash, long length) =
                        await CopyAndHashAsync(outputPath, payloadPath, cancellationToken);
                    outputs.Add(new CachedOutput(
                        outputPath,
                        present: true,
                        length,
                        hash,
                        attributes,
                        unixFileMode));
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

                var temporaryFiles = new string[outputs.Count];
                try
                {
                    for (int i = 0; i < outputs.Count; i++)
                    {
                        CachedOutput output = outputs[i];
                        if (!output.Present)
                        {
                            continue;
                        }

                        string outputDirectory = Path.GetDirectoryName(output.Path);
                        if (!String.IsNullOrEmpty(outputDirectory))
                        {
                            Directory.CreateDirectory(outputDirectory);
                        }

                        string temporaryFile =
                            output.Path + "." + Guid.NewGuid().ToString("N") + ".msbuild-cache";
                        temporaryFiles[i] = temporaryFile;
                        (byte[] hash, long length) = await CopyAndHashAsync(
                            GetPayloadPath(_entryDirectory, i),
                            temporaryFile,
                            cancellationToken);
                        if (length != output.Length ||
                            !HashesEqual(hash, output.Hash))
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
                        File.Move(temporaryFiles[i], output.Path);
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
                        if (temporaryFiles[i] is not null)
                        {
                            File.Delete(temporaryFiles[i]);
                        }
                    }
                }

                return new TaskResultCacheRestoreResponse(
                    success: true,
                    manifest.Events);
            }
            catch (Exception e) when (IsExpectedCacheException(e))
            {
                return new TaskResultCacheRestoreResponse(success: false, reason: e.Message);
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
                    success: false,
                    reason: "The task assembly does not have a readable file location.");
            }

            if (!TryGetDeclaredPaths(
                    taskType,
                    task,
                    "DeclaredInputs",
                    projectDirectory,
                    out IReadOnlyList<string> inputPaths,
                    out string reason) ||
                !TryGetDeclaredPaths(
                    taskType,
                    task,
                    "DeclaredOutputs",
                    projectDirectory,
                    out IReadOnlyList<string> outputPaths,
                    out reason))
            {
                return new TaskResultCacheKeyResponse(success: false, reason: reason);
            }

            try
            {
                using SHA256 hash = SHA256.Create();
                using var hashStream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
                using var writer = new BinaryWriter(hashStream, Encoding.UTF8, leaveOpen: true);

                writer.Write(taskType.AssemblyQualifiedName);
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

                if (!TryWriteTaskParameters(writer, task, parameterNames, out reason))
                {
                    return new TaskResultCacheKeyResponse(success: false, reason: reason);
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
                            success: false,
                            reason: $"Declared input \"{inputPath}\" is a directory.");
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
                    success: true,
                    ToHex(hash.Hash),
                    outputPaths);
            }
            catch (Exception e) when (IsExpectedCacheException(e) || e is TargetInvocationException)
            {
                return new TaskResultCacheKeyResponse(
                    success: false,
                    reason: e.InnerException?.Message ?? e.Message);
            }
        }

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2075",
            Justification = "Declared-IO cache binding reflects only over task types, which are already rooted by task loading.")]
        private static bool TryWriteTaskParameters(
            BinaryWriter writer,
            ITask task,
            ICollection<string> parameterNames,
            out string reason)
        {
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
                if (!propertiesByName.TryGetValue(parameterName, out PropertyInfo property) ||
                    property.GetMethod is null)
                {
                    reason = $"Task parameter \"{parameterName}\" does not have a readable property.";
                    return false;
                }

                writer.Write(parameterName);
                writer.Write(property.PropertyType.AssemblyQualifiedName);
                if (!TryWriteValue(writer, property.GetValue(task), out reason))
                {
                    reason = $"Task parameter \"{parameterName}\" cannot be cached: {reason}";
                    return false;
                }
            }

            return true;
        }

        private static bool TryWriteValue(
            BinaryWriter writer,
            object value,
            out string reason)
        {
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
                    WriteTaskItem(writer, taskItem);
                    return true;

                case Array array:
                    writer.Write((byte)3);
                    writer.Write(array.Length);
                    for (int i = 0; i < array.Length; i++)
                    {
                        if (!TryWriteValue(writer, array.GetValue(i), out reason))
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
                    writer.Write(value.GetType().AssemblyQualifiedName);
                    writer.Write(convertible.ToString(CultureInfo.InvariantCulture));
                    return true;

                default:
                    reason = $"value type \"{value.GetType().FullName}\" is unsupported.";
                    return false;
            }
        }

        private static void WriteTaskItem(BinaryWriter writer, ITaskItem taskItem)
        {
            writer.Write(taskItem.ItemSpec ?? String.Empty);
            IDictionary metadata = taskItem.CloneCustomMetadata();
            var metadataNames = new List<string>(metadata.Count);
            foreach (DictionaryEntry entry in metadata)
            {
                metadataNames.Add((string)entry.Key);
            }

            metadataNames.Sort(StringComparer.OrdinalIgnoreCase);
            writer.Write(metadataNames.Count);
            for (int i = 0; i < metadataNames.Count; i++)
            {
                string metadataName = metadataNames[i];
                writer.Write(metadataName);
                writer.Write(Convert.ToString(metadata[metadataName], CultureInfo.InvariantCulture) ?? String.Empty);
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
            out IReadOnlyList<string> paths,
            out string reason)
        {
            paths = null;
            reason = null;
            PropertyInfo property = taskType.GetProperty(
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
                        reader.ReadBytes(32),
                        (FileAttributes)reader.ReadInt32(),
                        reader.ReadInt32())
                    : new CachedOutput(
                        path,
                        present,
                        length: 0,
                        hash: null,
                        attributes: 0,
                        unixFileMode: 0);
                if (present && outputs[i].Hash.Length != 32)
                {
                    throw new InvalidDataException("The cache manifest contains an invalid output hash.");
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

        private static async ValueTask<(byte[] Hash, long Length)> CopyAndHashAsync(
            string sourcePath,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            using SHA256 hash = SHA256.Create();
            using var source = CreateAsyncFileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            using var destination = CreateAsyncFileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            using var hashStream = new CryptoStream(destination, hash, CryptoStreamMode.Write);
            await source.CopyToAsync(hashStream, 81920, cancellationToken);
            hashStream.FlushFinalBlock();
            return (hash.Hash, source.Position);
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

        private static bool HashesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            int difference = 0;
            for (int i = 0; i < left.Length; i++)
            {
                difference |= left[i] ^ right[i];
            }

            return difference == 0;
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

        private bool QuarantineEntryBestEffort()
        {
            string tombstoneDirectory =
                _entryDirectory + ".bad-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.Move(_entryDirectory, tombstoneDirectory);
            }
            catch (Exception e) when (IsExpectedCacheException(e))
            {
                return false;
            }

            DeleteDirectoryBestEffort(tombstoneDirectory);
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

        private readonly struct TaskResultCacheKeyResponse
        {
            internal TaskResultCacheKeyResponse(
                bool success,
                string key = null,
                IReadOnlyList<string> outputPaths = null,
                string reason = null)
            {
                Success = success;
                Key = key;
                OutputPaths = outputPaths;
                Reason = reason;
            }

            internal bool Success { get; }

            internal string Key { get; }

            internal IReadOnlyList<string> OutputPaths { get; }

            internal string Reason { get; }
        }

        private readonly struct TaskResultCacheRestoreResponse
        {
            internal TaskResultCacheRestoreResponse(
                bool success,
                IReadOnlyList<TaskResultCacheEvent> events = null,
                string reason = null)
            {
                Success = success;
                Events = events;
                Reason = reason;
            }

            internal bool Success { get; }

            internal IReadOnlyList<TaskResultCacheEvent> Events { get; }

            internal string Reason { get; }
        }

        private readonly struct TaskResultCacheManifest
        {
            internal TaskResultCacheManifest(
                IReadOnlyList<CachedOutput> outputs,
                IReadOnlyList<TaskResultCacheEvent> events)
            {
                Outputs = outputs;
                Events = events;
            }

            internal IReadOnlyList<CachedOutput> Outputs { get; }

            internal IReadOnlyList<TaskResultCacheEvent> Events { get; }
        }

        private sealed class CachedOutput
        {
            internal CachedOutput(
                string path,
                bool present,
                long length,
                byte[] hash,
                FileAttributes attributes,
                int unixFileMode)
            {
                Path = path;
                Present = present;
                Length = length;
                Hash = hash;
                Attributes = attributes;
                UnixFileMode = unixFileMode;
            }

            internal string Path { get; }

            internal bool Present { get; }

            internal long Length { get; }

            internal byte[] Hash { get; }

            internal FileAttributes Attributes { get; }

            internal int UnixFileMode { get; }
        }
    }
}
