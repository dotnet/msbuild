// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <remarks>
    /// Base class for task state files.
    /// </remarks>
    internal abstract class StateFileBase
    {
        // Current version for serialization. This should be changed when breaking changes
        // are made to this class.
        // Note: Consider that changes can break VS2015 RTM which did not have a version check.
        // Version 4/5 - VS2017.7:
        //   Unify .NET Core + Full Framework. Custom serialization on some types that are no
        //   longer [Serializable].
        internal const byte CurrentSerializationVersion = 6;

        // Version this instance is serialized with.
        private byte _serializedVersion = CurrentSerializationVersion;

        /// <summary>
        /// True if <see cref="SerializeCache"/> should create the state file and serialize ourselves, false otherwise.
        /// </summary>
        internal virtual bool HasStateToSave => true;

        /// <summary>
        /// Writes the contents of this object out to the specified file.
        /// </summary>
        internal virtual void SerializeCache(string stateFile, TaskLoggingHelper log, bool serializeEmptyState = false)
        {
            try
            {
                if (!string.IsNullOrEmpty(stateFile))
                {
                    if (FileSystems.Default.FileExists(stateFile))
                    {
                        File.Delete(stateFile);
                    }

                    if (serializeEmptyState || HasStateToSave)
                    {
                        using (var s = new FileStream(stateFile, FileMode.CreateNew))
                        {
                            var translator = BinaryTranslator.GetWriteTranslator(s);
                            translator.Translate(ref _serializedVersion);
                            Translate(translator);
                        }
                    }
                }
            }
            // If there was a problem writing the file (like it's read-only or locked on disk, for
            // example), then eat the exception and log a warning.  Otherwise, rethrow.
            catch (Exception e) when (!ExceptionHandling.NotExpectedSerializationException(e))
            {
                // Not being able to serialize the cache is not an error, but we let the user know anyway.
                // Don't want to hold up processing just because we couldn't read the file.
                log.LogWarningWithCodeFromResources("General.CouldNotWriteStateFile", stateFile, e.Message);
            }
        }

        public abstract void Translate(ITranslator translator);

        /// <summary>
        /// Reads the specified file from disk into a StateFileBase derived object.
        /// </summary>
        internal static T DeserializeCache<T>(string stateFile, TaskLoggingHelper log) where T : StateFileBase
        {
            T retVal = null;

            // First, we read the cache from disk if one exists, or if one does not exist, we create one.
            try
            {
                if (!string.IsNullOrEmpty(stateFile) && FileSystems.Default.FileExists(stateFile))
                {
                    using (FileStream s = File.OpenRead(stateFile))
                    {
                        using var translator = BinaryTranslator.GetReadTranslator(s, InterningBinaryReader.PoolingBuffer);

                        byte version = 0;
                        translator.Translate(ref version);
                        // If the version is wrong, log a message not a warning. This could be a valid cache with the wrong version preventing correct deserialization.
                        // For the latter case, internals may be unexpectedly null.
                        if (version != CurrentSerializationVersion)
                        {
                            log.LogMessageFromResources("General.CouldNotReadStateFileMessage", stateFile, log.FormatResourceString("General.IncompatibleStateFileType"));
                            return null;
                        }

                        var constructors = typeof(T).GetConstructors();
                        foreach (var constructor in constructors)
                        {
                            var parameters = constructor.GetParameters();
                            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(ITranslator))
                            {
                                retVal = constructor.Invoke(new object[] { translator }) as T;
                            }
                        }

                        if (retVal == null)
                        {
                            log.LogMessageFromResources("General.CouldNotReadStateFileMessage", stateFile,
                                log.FormatResourceString("General.IncompatibleStateFileType"));
                        }
                    }
                }
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                // The deserialization process seems like it can throw just about
                // any exception imaginable.  Catch them all here.
                // Not being able to deserialize the cache is not an error, but we let the user know anyway.
                // Don't want to hold up processing just because we couldn't read the file.
                log.LogMessageFromResources("General.CouldNotReadStateFileMessage", stateFile, e.Message);
            }

            return retVal;
        }

        /// <summary>
        /// Deletes the state file from disk
        /// </summary>
        /// <param name="stateFile"></param>
        /// <param name="log"></param>
        internal static void DeleteFile(string stateFile, TaskLoggingHelper log)
        {
            try
            {
                if (!string.IsNullOrEmpty(stateFile))
                {
                    if (FileSystems.Default.FileExists(stateFile))
                    {
                        File.Delete(stateFile);
                    }
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                log.LogWarningWithCodeFromResources("General.CouldNotDeleteStateFile", stateFile, e.Message);
            }
        }
    }
}
