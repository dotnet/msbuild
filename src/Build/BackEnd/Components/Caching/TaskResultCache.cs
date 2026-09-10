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
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;

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

    internal sealed class TaskResultCacheSession : IDisposable
    {
        internal const string CacheDirectoryPropertyName = "MSBuildTaskCacheDirectory";

        private const string ManifestFileName = "manifest.bin";
        private const string ManifestMagic = "MSBuild Task Result Cache";
        private const int ManifestVersion = 3;
        private const int MaximumEventCount = 1_000_000;
        private const int LockRetryCount = 300;
        private const int LockRetryDelayMilliseconds = 100;

        private readonly string _entryDirectory;
        private readonly string _key;
        private readonly IReadOnlyList<string> _outputPaths;
        private readonly FileStream _lockStream;

        private TaskResultCacheSession(
            string entryDirectory,
            string key,
            IReadOnlyList<string> outputPaths,
            FileStream lockStream)
        {
            _entryDirectory = entryDirectory;
            _key = key;
            _outputPaths = outputPaths;
            _lockStream = lockStream;
        }

        internal string Key => _key;

        internal static TaskResultCacheOpenResult TryOpen(
            ITask task,
            ICollection<string> parameterNames,
            string projectFullPath,
            string projectDirectory,
            string cacheDirectory,
            out TaskResultCacheSession session,
            out IReadOnlyList<TaskResultCacheEvent> events,
            out string reason)
        {
            session = null;
            events = null;
            reason = null;

            if (!TryCreateKey(
                    task,
                    parameterNames,
                    projectFullPath,
                    projectDirectory,
                    out string key,
                    out IReadOnlyList<string> outputPaths,
                    out reason))
            {
                return TaskResultCacheOpenResult.Ineligible;
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
                reason = e.Message;
                return TaskResultCacheOpenResult.Unavailable;
            }

            string entryDirectory = Path.Combine(
                normalizedCacheDirectory,
                key.Substring(0, 2),
                key.Substring(2));
            string lockPath = entryDirectory + ".lock";

            FileStream lockStream = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(entryDirectory));
                lockStream = AcquireLock(lockPath);
                session = new TaskResultCacheSession(
                    entryDirectory,
                    key,
                    outputPaths,
                    lockStream);
                lockStream = null;
            }
            catch (Exception e) when (IsExpectedCacheException(e))
            {
                reason = e.Message;
                return TaskResultCacheOpenResult.Unavailable;
            }
            finally
            {
                lockStream?.Dispose();
            }

            if (!Directory.Exists(entryDirectory))
            {
                return TaskResultCacheOpenResult.Miss;
            }

            if (session.TryRestore(out events, out reason))
            {
                return TaskResultCacheOpenResult.Hit;
            }

            session.DeleteEntryBestEffort();
            return TaskResultCacheOpenResult.Miss;
        }

        internal bool TryStore(
            IReadOnlyList<TaskResultCacheEvent> events,
            out string reason)
        {
            reason = null;
            string temporaryDirectory = _entryDirectory + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                if (Directory.Exists(_entryDirectory))
                {
                    reason = "A corrupt cache entry could not be removed.";
                    return false;
                }

                Directory.CreateDirectory(temporaryDirectory);
                var outputs = new List<CachedOutput>(_outputPaths.Count);
                for (int i = 0; i < _outputPaths.Count; i++)
                {
                    string outputPath = _outputPaths[i];
                    if (Directory.Exists(outputPath))
                    {
                        reason = $"Declared output \"{outputPath}\" is a directory.";
                        return false;
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
                        reason = $"Declared output \"{outputPath}\" is not a regular file.";
                        return false;
                    }

#if NET
                    int unixFileMode = OperatingSystem.IsWindows()
                        ? 0
                        : (int)File.GetUnixFileMode(outputPath);
#else
                    const int unixFileMode = 0;
#endif
                    string payloadPath = GetPayloadPath(temporaryDirectory, i);
                    byte[] hash = CopyAndHash(outputPath, payloadPath, out long length);
                    outputs.Add(new CachedOutput(
                        outputPath,
                        present: true,
                        length,
                        hash,
                        attributes,
                        unixFileMode));
                }

                WriteManifest(
                    Path.Combine(temporaryDirectory, ManifestFileName),
                    outputs,
                    events);
                Directory.Move(temporaryDirectory, _entryDirectory);
                return true;
            }
            catch (Exception e) when (IsExpectedCacheException(e))
            {
                reason = e.Message;
                return false;
            }
            finally
            {
                DeleteDirectoryBestEffort(temporaryDirectory);
            }
        }

        public void Dispose()
        {
            _lockStream.Dispose();
        }

        private bool TryRestore(
            out IReadOnlyList<TaskResultCacheEvent> events,
            out string reason)
        {
            events = null;
            reason = null;

            try
            {
                IReadOnlyList<CachedOutput> outputs = ReadManifest(
                    Path.Combine(_entryDirectory, ManifestFileName),
                    out events);
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

                    if (!output.Present)
                    {
                        continue;
                    }

                    string payloadPath = GetPayloadPath(_entryDirectory, i);
                    if (!File.Exists(payloadPath) ||
                        new FileInfo(payloadPath).Length != output.Length ||
                        !HashesEqual(HashFile(payloadPath), output.Hash))
                    {
                        throw new InvalidDataException("A cached output payload is missing or corrupt.");
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
                        File.Copy(GetPayloadPath(_entryDirectory, i), temporaryFile);
                        temporaryFiles[i] = temporaryFile;
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

                return true;
            }
            catch (Exception e) when (IsExpectedCacheException(e))
            {
                reason = e.Message;
                events = null;
                return false;
            }
        }

        private static bool TryCreateKey(
            ITask task,
            ICollection<string> parameterNames,
            string projectFullPath,
            string projectDirectory,
            out string key,
            out IReadOnlyList<string> outputPaths,
            out string reason)
        {
            key = null;
            outputPaths = null;
            reason = null;

            Type taskType = task.GetType();
            string taskAssemblyPath = taskType.Assembly.Location;
            if (String.IsNullOrEmpty(taskAssemblyPath) || !File.Exists(taskAssemblyPath))
            {
                reason = "The task assembly does not have a readable file location.";
                return false;
            }

            if (!TryGetDeclaredPaths(
                    taskType,
                    task,
                    "DeclaredInputs",
                    projectDirectory,
                    out IReadOnlyList<string> inputPaths,
                    out reason) ||
                !TryGetDeclaredPaths(
                    taskType,
                    task,
                    "DeclaredOutputs",
                    projectDirectory,
                    out outputPaths,
                    out reason))
            {
                return false;
            }

            try
            {
                using SHA256 hash = SHA256.Create();
                using var hashStream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
                using var writer = new BinaryWriter(hashStream, Encoding.UTF8, leaveOpen: true);

                writer.Write(ManifestVersion);
                writer.Write(taskType.AssemblyQualifiedName);
                writer.Write(typeof(TaskResultCacheSession).Assembly.GetName().Version.ToString());
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

                WriteFileContent(writer, hashStream, taskAssemblyPath);

                if (!TryWriteTaskParameters(writer, task, parameterNames, out reason))
                {
                    return false;
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
                        reason = $"Declared input \"{inputPath}\" is a directory.";
                        return false;
                    }

                    if (!File.Exists(inputPath))
                    {
                        writer.Write((byte)0);
                        continue;
                    }

                    writer.Write((byte)1);
                    WriteFileContent(writer, hashStream, inputPath);
                }

                writer.Flush();
                hashStream.FlushFinalBlock();
                key = ToHex(hash.Hash);
                return true;
            }
            catch (Exception e) when (IsExpectedCacheException(e) || e is TargetInvocationException)
            {
                reason = e.InnerException?.Message ?? e.Message;
                return false;
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

        private static FileStream AcquireLock(string lockPath)
        {
            IOException lastException = null;
            for (int retry = 0; retry < LockRetryCount; retry++)
            {
                try
                {
                    return new FileStream(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                }
                catch (IOException e)
                {
                    lastException = e;
                    Thread.Sleep(LockRetryDelayMilliseconds);
                }
            }

            throw lastException ?? new IOException("Could not acquire the cache entry lock.");
        }

        private void WriteManifest(
            string manifestPath,
            IReadOnlyList<CachedOutput> outputs,
            IReadOnlyList<TaskResultCacheEvent> events)
        {
            using var stream = new FileStream(
                manifestPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
            writer.Write(ManifestMagic);
            writer.Write(ManifestVersion);
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
        }

        private IReadOnlyList<CachedOutput> ReadManifest(
            string manifestPath,
            out IReadOnlyList<TaskResultCacheEvent> events)
        {
            using var stream = new FileStream(
                manifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            if (!String.Equals(reader.ReadString(), ManifestMagic, StringComparison.Ordinal) ||
                reader.ReadInt32() != ManifestVersion ||
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

            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException("The cache manifest contains trailing data.");
            }

            events = cachedEvents;
            return outputs;
        }

        private static void WriteFileContent(
            BinaryWriter writer,
            CryptoStream hashStream,
            string path)
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            writer.Write(stream.Length);
            writer.Flush();
            stream.CopyTo(hashStream);
        }

        private static byte[] CopyAndHash(
            string sourcePath,
            string destinationPath,
            out long length)
        {
            using SHA256 hash = SHA256.Create();
            using var source = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            using var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            using var hashStream = new CryptoStream(destination, hash, CryptoStreamMode.Write);
            source.CopyTo(hashStream);
            length = source.Length;
            hashStream.FlushFinalBlock();
            return hash.Hash;
        }

        private static byte[] HashFile(string path)
        {
            using SHA256 hash = SHA256.Create();
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            return hash.ComputeHash(stream);
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

        private void DeleteEntryBestEffort()
        {
            DeleteDirectoryBestEffort(_entryDirectory);
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
