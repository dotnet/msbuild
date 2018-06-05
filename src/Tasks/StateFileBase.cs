// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <remarks>
    /// Base class for task state files.
    /// </remarks>
    [Serializable()]
    internal class StateFileBase
    {
        // Current version for serialization. This should be changed when breaking changes
        // are made to this class.
        // Note: Consider that changes can break VS2015 RTM which did not have a version check.
        // Version 4/5 - VS2017.7:
        //   Unify .NET Core + Full Framework. Custom serialization on some types that are no
        //   longer [Serializable].
        private const byte CurrentSerializationVersion = 5;

        // Version this instance is serialized with.
        private byte _serializedVersion = CurrentSerializationVersion;

        /// <summary>
        /// Writes the contents of this object out to the specified file.
        /// </summary>
        internal virtual void SerializeCache(string stateFile, TaskLoggingHelper log)
        {
            try
            {
                if (!string.IsNullOrEmpty(stateFile))
                {
                    if (File.Exists(stateFile))
                    {
                        File.Delete(stateFile);
                    }

                    using (var s = new FileStream(stateFile, FileMode.CreateNew))
                    {
                        var formatter = new BinaryFormatter();
                        formatter.Serialize(s, this);
                    }
                }
            }
            catch (Exception e)
            {
                // If there was a problem writing the file (like it's read-only or locked on disk, for
                // example), then eat the exception and log a warning.  Otherwise, rethrow.
                if (ExceptionHandling.NotExpectedSerializationException(e))
                    throw;

                // Not being able to serialize the cache is not an error, but we let the user know anyway.
                // Don't want to hold up processing just because we couldn't read the file.
                log.LogWarningWithCodeFromResources("General.CouldNotWriteStateFile", stateFile, e.Message);
            }
        }

        /// <summary>
        /// Reads the specified file from disk into a StateFileBase derived object.
        /// </summary>
        internal static StateFileBase DeserializeCache(string stateFile, TaskLoggingHelper log, Type requiredReturnType)
        {
            StateFileBase retVal = null;

            // First, we read the cache from disk if one exists, or if one does not exist
            // then we create one.  
            try
            {
                if (!string.IsNullOrEmpty(stateFile) && File.Exists(stateFile))
                {
                    using (FileStream s = new FileStream(stateFile, FileMode.Open))
                    {
                        var formatter = new BinaryFormatter();
                        object deserializedObject = formatter.Deserialize(s);
                        retVal = deserializedObject as StateFileBase;

                        // If the deserialized object is null then there would be no cast error but retVal would still be null
                        // only log the message if there would have been a cast error
                        if (retVal == null && deserializedObject != null)
                        {
                            // When upgrading to Visual Studio 2008 and running the build for the first time the resource cache files are replaced which causes a cast error due
                            // to a new version number on the tasks class. "Unable to cast object of type 'Microsoft.Build.Tasks.SystemState' to type 'Microsoft.Build.Tasks.StateFileBase".
                            // If there is an invalid cast, a message rather than a warning should be emitted.
                            log.LogMessageFromResources("General.CouldNotReadStateFileMessage", stateFile, log.FormatResourceString("General.IncompatibleStateFileType"));
                        }

                        if ((retVal != null) && (!requiredReturnType.IsInstanceOfType(retVal)))
                        {
                            log.LogWarningWithCodeFromResources("General.CouldNotReadStateFile", stateFile,
                                log.FormatResourceString("General.IncompatibleStateFileType"));
                            retVal = null;
                        }

                        // If we get back a valid object and internals were changed, things are likely to be null. Check the version before we use it.
                        if (retVal != null && retVal._serializedVersion != CurrentSerializationVersion)
                        {
                            log.LogMessageFromResources("General.CouldNotReadStateFileMessage", stateFile, log.FormatResourceString("General.IncompatibleStateFileType"));
                            retVal = null;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                // The deserialization process seems like it can throw just about 
                // any exception imaginable.  Catch them all here.
                // Not being able to deserialize the cache is not an error, but we let the user know anyway.
                // Don't want to hold up processing just because we couldn't read the file.
                log.LogWarningWithCodeFromResources("General.CouldNotReadStateFile", stateFile, e.Message);
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
                    if (File.Exists(stateFile))
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
