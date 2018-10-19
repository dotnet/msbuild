using System;
using System.IO;

using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;

using Bond;
using Bond.Protocols;

namespace Microsoft.Build.Tasks
{
    internal class StateFileCache<T> where T : StateFileCachePayload, new()
    {
        private const byte CurrentSerializationVersion = 6;

        internal static void InitializeSerializer()
        {
            var output = new OutputBuffer();
            var writer = new SimpleBinaryWriter<OutputBuffer>(output);
            Serialize.To(writer, new T());

            var input = new InputBuffer(output.Data);
            var reader = new SimpleBinaryReader<InputBuffer>(input);
            Deserialize<T>.From(reader);
        }

        internal static T DeserializeCache(string stateFile, TaskLoggingHelper log)
        {
            T retVal = default(T);

            // First, we read the cache from disk if one exists, or if one does not exist
            // then we create one.  
            try
            {
                if (!string.IsNullOrEmpty(stateFile) && FileSystems.Default.FileExists(stateFile))
                {
                    using (FileStream s = new FileStream(stateFile, FileMode.Open))
                    {
                        var input = new InputStream(s);
                        var reader = new SimpleBinaryReader<InputBuffer>(input);
                        retVal = Deserialize<T>.From(reader);

                        // If we get back a valid object and internals were changed, things are likely to be null. Check the version before we use it.
                        if (retVal.SerializedVersion != CurrentSerializationVersion)
                        {
                            log.LogMessageFromResources("General.CouldNotReadStateFileMessage", stateFile,
                                log.FormatResourceString("General.IncompatibleStateFileType"));
                            retVal = default(T);
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

        internal static void SerializeCache(string stateFile, TaskLoggingHelper log, T payload)
        {
            payload.SerializedVersion = CurrentSerializationVersion;

            try
            {
                if (!string.IsNullOrEmpty(stateFile))
                {
                    if (FileSystems.Default.FileExists(stateFile))
                    {
                        File.Delete(stateFile);
                    }

                    using (var s = new FileStream(stateFile, FileMode.CreateNew))
                    {
                        var output = new OutputStream(s);
                        var writer = new SimpleBinaryWriter<OutputBuffer>(output);
                        Serialize.To(writer, payload);
                        output.Flush();
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
    }

    [Bond.Schema]
    internal class StateFileCachePayload
    {
        [Bond.Id(0)]
        internal byte SerializedVersion { get; set; }
    }
}
