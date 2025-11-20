// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure.EditorConfig;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Represents the parsed parameters for a BinaryLogger.
    /// </summary>
    public sealed class BinaryLoggerParameters
    {
        /// <summary>
        /// Gets the log file path. Returns null if not specified or if the path contains wildcards.
        /// </summary>
        public string LogFilePath { get; internal set; }

        /// <summary>
        /// Gets the project imports collection mode.
        /// </summary>
        public BinaryLogger.ProjectImportsCollectionMode ProjectImportsCollectionMode { get; internal set; } = BinaryLogger.ProjectImportsCollectionMode.Embed;

        /// <summary>
        /// Gets whether the ProjectImports parameter was explicitly specified in the parameters string.
        /// </summary>
        internal bool HasProjectImportsParameter { get; set; }

        /// <summary>
        /// Gets whether to omit initial info from the log.
        /// </summary>
        public bool OmitInitialInfo { get; internal set; }
    }

    /// <summary>
    /// A logger that serializes all incoming BuildEventArgs in a compressed binary file (*.binlog). The file
    /// can later be played back and piped into other loggers (file, console, etc) to reconstruct the log contents
    /// as if a real build was happening. Additionally, this format can be read by tools for
    /// analysis or visualization. Since the file format preserves structure, tools don't have to parse
    /// text logs that erase a lot of useful information.
    /// </summary>
    /// <remarks>The logger is public so that it can be instantiated from MSBuild.exe via command-line switch.</remarks>
    public sealed class BinaryLogger : ILogger
    {
        // version 2:
        //   - new BuildEventContext.EvaluationId
        //   - new record kinds: ProjectEvaluationStarted, ProjectEvaluationFinished
        // version 3:
        //   - new ProjectImportedEventArgs.ImportIgnored
        // version 4:
        //   - new TargetSkippedEventArgs
        //   - new TargetStartedEventArgs.BuildReason
        // version 5:
        //   - new EvaluationFinished.ProfilerResult
        // version 6:
        //   - Ids and parent ids for the evaluation locations
        // version 7:
        //   - Include ProjectStartedEventArgs.GlobalProperties
        // version 8:
        //   - This was used in a now-reverted change but is the same as 9.
        // version 9:
        //   - new record kinds: EnvironmentVariableRead, PropertyReassignment, UninitializedPropertyRead
        // version 10:
        //   - new record kinds:
        //      * String - deduplicate strings by hashing and write a string record before it's used
        //      * NameValueList - deduplicate arrays of name-value pairs such as properties, items and metadata
        //                        in a separate record and refer to those records from regular records
        //                        where a list used to be written in-place
        // version 11:
        //   - new record kind: TaskParameterEventArgs
        // version 12:
        //   - add GlobalProperties, Properties and Items on ProjectEvaluationFinished
        // version 13:
        //   - don't log Message where it can be recovered
        //   - log arguments for LazyFormattedBuildEventArgs
        //   - TargetSkippedEventArgs: added OriginallySucceeded, Condition, EvaluatedCondition
        // version 14:
        //   - TargetSkippedEventArgs: added SkipReason, OriginalBuildEventContext
        // version 15:
        //   - new record kind: ResponseFileUsedEventArgs
        // version 16:
        //   - AssemblyLoadBuildEventArgs
        // version 17:
        //   - Added extended data for types implementing IExtendedBuildEventArgs
        // version 18:
        //   - Making ProjectStartedEventArgs, ProjectEvaluationFinishedEventArgs, AssemblyLoadBuildEventArgs equal
        //     between de/serialization roundtrips.
        //   - Adding serialized events lengths - to support forward compatible reading
        // version 19:
        //   - GeneratedFileUsedEventArgs exposed for brief period of time (so let's continue with 20)
        // version 20:
        //   - TaskStartedEventArgs: Added TaskAssemblyLocation property
        // version 21:
        //   - TaskParameterEventArgs: Added ParameterName and PropertyName properties
        // version 22:
        //    - extend EnvironmentVariableRead with location where environment variable was used.
        // version 23:
        //    - new record kinds: BuildCheckMessageEvent, BuildCheckWarningEvent, BuildCheckErrorEvent,
        //    BuildCheckTracingEvent, BuildCheckAcquisitionEvent, BuildSubmissionStartedEvent
        // version 24:
        //    - new record kind: BuildCanceledEventArgs
        // version 25:
        //    - add extra information to PropertyInitialValueSetEventArgs and PropertyReassignmentEventArgs and change message formatting logic.

        // MAKE SURE YOU KEEP BuildEventArgsWriter AND StructuredLogViewer.BuildEventArgsWriter IN SYNC WITH THE CHANGES ABOVE.
        // Both components must stay in sync to avoid issues with logging or event handling in the products.

        // This should be never changed.
        // The minimum version of the binary log reader that can read log of above version.
        internal const int ForwardCompatibilityMinimalVersion = 18;

        // The current version of the binary log representation.
        // Changes with each update of the binary log format.
        internal const int FileFormatVersion = 25;

        // The minimum version of the binary log reader that can read log of above version.
        // This should be changed only when the binary log format is changed in a way that would prevent it from being
        // read by older readers. (changing of the individual BuildEventArgs or adding new is fine - as reader can
        // skip them if they are not known to it. Example of change requiring the increment would be the introduction of strings deduplication)
        internal const int MinimumReaderVersion = 18;

        // Parameter name constants
        private const string LogFileParameterPrefix = "LogFile=";
        private const string BinlogFileExtension = ".binlog";
        private const string OmitInitialInfoParameter = "OmitInitialInfo";
        private const string ProjectImportsNoneParameter = "ProjectImports=None";
        private const string ProjectImportsEmbedParameter = "ProjectImports=Embed";
        private const string ProjectImportsZipFileParameter = "ProjectImports=ZipFile";

        private Stream stream;
        private BinaryWriter binaryWriter;
        private BuildEventArgsWriter eventArgsWriter;
        private ProjectImportsCollector projectImportsCollector;
        private bool _initialTargetOutputLogging;
        private bool _initialLogImports;
        private string _initialIsBinaryLoggerEnabled;

        /// <summary>
        /// Describes whether to collect the project files (including imported project files) used during the build.
        /// If the project files are collected they can be embedded in the log file or as a separate zip archive.
        /// </summary>
        public enum ProjectImportsCollectionMode
        {
            /// <summary>
            /// Don't collect any files during the build.
            /// </summary>
            None,

            /// <summary>
            /// Embed all project files directly in the log file.
            /// </summary>
            Embed,

            /// <summary>
            /// Create an external .ProjectImports.zip archive for the project files.
            /// </summary>
            ZipFile,
        }

        /// <summary>
        /// Parses the parameters string for a BinaryLogger.
        /// </summary>
        /// <param name="parametersString">The parameters string to parse (e.g., "LogFile=msbuild.binlog;ProjectImports=None").</param>
        /// <returns>A <see cref="BinaryLoggerParameters"/> object containing the parsed parameters.</returns>
        /// <exception cref="LoggerException">Thrown when the parameters string contains invalid parameters.</exception>
        /// <remarks>
        /// This method parses the semicolon-delimited parameters string used by the BinaryLogger.
        /// Supported parameters include:
        /// - LogFile=&lt;path&gt; or just &lt;path&gt; (must end with .binlog): specifies the output file path
        /// - ProjectImports=None|Embed|ZipFile: controls project imports collection
        /// - OmitInitialInfo: omits initial build information
        /// 
        /// Wildcards ({}) in the LogFile path are NOT expanded by this method. The returned LogFilePath
        /// will be null for wildcard patterns, and callers should handle expansion separately if needed.
        /// </remarks>
        public static BinaryLoggerParameters ParseParameters(string parametersString)
        {
            if (parametersString == null)
            {
                throw new LoggerException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("InvalidBinaryLoggerParameters", ""));
            }

            var result = new BinaryLoggerParameters();
            var parameters = parametersString.Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries);

            foreach (var parameter in parameters)
            {
                if (TryParseProjectImports(parameter, result))
                {
                    continue;
                }

                if (string.Equals(parameter, OmitInitialInfoParameter, StringComparison.OrdinalIgnoreCase))
                {
                    result.OmitInitialInfo = true;
                    continue;
                }

                if (TryParsePathParameter(parameter, out string filePath))
                {
                    result.LogFilePath = filePath;
                    continue;
                }

                throw new LoggerException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("InvalidBinaryLoggerParameters", parameter));
            }

            return result;
        }

        /// <summary>
        /// Attempts to parse a ProjectImports parameter.
        /// </summary>
        /// <param name="parameter">The parameter to parse.</param>
        /// <param name="result">The BinaryLoggerParameters object to update.</param>
        /// <returns>True if the parameter was a ProjectImports parameter; otherwise, false.</returns>
        private static bool TryParseProjectImports(string parameter, BinaryLoggerParameters result)
        {
            return TrySetProjectImportsMode(parameter, ProjectImportsNoneParameter, ProjectImportsCollectionMode.None, result)
                || TrySetProjectImportsMode(parameter, ProjectImportsEmbedParameter, ProjectImportsCollectionMode.Embed, result)
                || TrySetProjectImportsMode(parameter, ProjectImportsZipFileParameter, ProjectImportsCollectionMode.ZipFile, result);
        }

        /// <summary>
        /// Attempts to match and set a ProjectImports mode.
        /// </summary>
        /// <param name="parameter">The parameter to check.</param>
        /// <param name="expectedParameter">The expected parameter string.</param>
        /// <param name="mode">The mode to set if matched.</param>
        /// <param name="result">The BinaryLoggerParameters object to update.</param>
        /// <returns>True if the parameter matched; otherwise, false.</returns>
        private static bool TrySetProjectImportsMode(string parameter, string expectedParameter, ProjectImportsCollectionMode mode, BinaryLoggerParameters result)
        {
            if (string.Equals(parameter, expectedParameter, StringComparison.OrdinalIgnoreCase))
            {
                result.ProjectImportsCollectionMode = mode;
                result.HasProjectImportsParameter = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to parse a file path parameter from a BinaryLogger parameter string.
        /// </summary>
        /// <param name="parameter">The parameter to parse.</param>
        /// <param name="filePath">The parsed file path, or null if the parameter contains wildcards.</param>
        /// <returns>True if the parameter is a valid file path parameter; otherwise, false.</returns>
        /// <remarks>
        /// This method recognizes file paths in the following formats:
        /// - "LogFile=&lt;path&gt;"
        /// - "&lt;path&gt;" (must end with .binlog)
        /// 
        /// If the path contains wildcards ({}), the method returns true but sets filePath to null,
        /// as wildcard expansion requires runtime context.
        /// </remarks>
        private static bool TryParsePathParameter(string parameter, out string filePath)
        {
            bool hasPathPrefix = parameter.StartsWith(LogFileParameterPrefix, StringComparison.OrdinalIgnoreCase);

            if (hasPathPrefix)
            {
                parameter = parameter.Substring(LogFileParameterPrefix.Length);
            }

            parameter = parameter.Trim('"');

            bool isWildcard = ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_12) && parameter.Contains("{}");
            bool hasProperExtension = parameter.EndsWith(BinlogFileExtension, StringComparison.OrdinalIgnoreCase);

            filePath = parameter;

            if (isWildcard)
            {
                // For wildcards, we return true to indicate this is a valid path parameter,
                // but set filePath to null since we can't expand it without instance context
                filePath = null;
                return true;
            }

            return hasProperExtension;
        }

        /// <summary>
        /// Gets or sets whether to capture and embed project and target source files used during the build.
        /// </summary>
        public ProjectImportsCollectionMode CollectProjectImports { get; set; } = ProjectImportsCollectionMode.Embed;

        internal string FilePath { get; private set; }

        /// <summary> Gets or sets the verbosity level.</summary>
        /// <remarks>
        /// The binary logger Verbosity is always maximum (Diagnostic). It tries to capture as much
        /// information as possible.
        /// </remarks>
        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Diagnostic;

        /// <summary>
        /// Gets or sets the parameters. The only supported parameter is the output log file path (for example, "msbuild.binlog").
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Optional expander of wildcard(s) within the LogFile path parameter of a binlog <see cref="Parameters"/>.
        /// Wildcards can be used in the LogFile parameter in a form for curly brackets ('{}', '{[param]}').
        /// Currently, the only supported wildcard is '{}', the optional parameters within the curly brackets
        ///  are not currently supported, however the string parameter to the <see cref="PathParameterExpander"/> func
        /// is reserved for this purpose.
        /// </summary>
        internal Func<string, string> PathParameterExpander { private get; set; } = ExpandPathParameter;

        /// <summary>
        /// Initializes the logger by subscribing to events of the specified event source and embedded content source.
        /// </summary>
        public void Initialize(IEventSource eventSource)
        {
            _initialTargetOutputLogging = Traits.Instance.EnableTargetOutputLogging;
            _initialLogImports = Traits.Instance.EscapeHatches.LogProjectImports;
            _initialIsBinaryLoggerEnabled = Environment.GetEnvironmentVariable("MSBUILDBINARYLOGGERENABLED");

            Environment.SetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING", "true");
            Environment.SetEnvironmentVariable("MSBUILDLOGIMPORTS", "1");
            Environment.SetEnvironmentVariable("MSBUILDBINARYLOGGERENABLED", bool.TrueString);

            Traits.Instance.EscapeHatches.LogProjectImports = true;
            Traits.Instance.EnableTargetOutputLogging = true;
            bool logPropertiesAndItemsAfterEvaluation = Traits.Instance.EscapeHatches.LogPropertiesAndItemsAfterEvaluation ?? true;

            ProcessParameters(out bool omitInitialInfo);
            var replayEventSource = eventSource as IBinaryLogReplaySource;

            try
            {
                string logDirectory = null;
                try
                {
                    logDirectory = Path.GetDirectoryName(FilePath);
                }
                catch (Exception)
                {
                    // Directory creation is best-effort; if finding its path fails don't create the directory
                    // and possibly let the FileStream constructor below report the failure
                }

                if (logDirectory != null)
                {
                    Directory.CreateDirectory(logDirectory);
                }

                stream = new FileStream(FilePath, FileMode.Create);

                if (CollectProjectImports != ProjectImportsCollectionMode.None && replayEventSource == null)
                {
                    projectImportsCollector = new ProjectImportsCollector(FilePath, CollectProjectImports == ProjectImportsCollectionMode.ZipFile);
                    projectImportsCollector.FileIOExceptionEvent += EventSource_AnyEventRaised;
                }

                if (eventSource is IEventSource3 eventSource3)
                {
                    eventSource3.IncludeEvaluationMetaprojects();
                }

                if (logPropertiesAndItemsAfterEvaluation && eventSource is IEventSource4 eventSource4)
                {
                    eventSource4.IncludeEvaluationPropertiesAndItems();
                }
            }
            catch (Exception e)
            {
                string errorCode;
                string helpKeyword;
                string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out errorCode, out helpKeyword, "InvalidFileLoggerFile", FilePath, e.Message);
                throw new LoggerException(message, e, errorCode, helpKeyword);
            }

            stream = new GZipStream(stream, CompressionLevel.Optimal);

            // wrapping the GZipStream in a buffered stream significantly improves performance
            // and the max throughput is reached with a 32K buffer. See details here:
            // https://github.com/dotnet/runtime/issues/39233#issuecomment-745598847
            stream = new BufferedStream(stream, bufferSize: 32768);
            binaryWriter = new BinaryWriter(stream);
            eventArgsWriter = new BuildEventArgsWriter(binaryWriter);

            if (projectImportsCollector != null)
            {
                eventArgsWriter.EmbedFile += EventArgsWriter_EmbedFile;
            }

            if (replayEventSource != null)
            {
                if (CollectProjectImports == ProjectImportsCollectionMode.Embed)
                {
                    replayEventSource.EmbeddedContentRead += args =>
                        eventArgsWriter.WriteBlob(args.ContentKind, args.ContentStream);
                }
                else if (CollectProjectImports == ProjectImportsCollectionMode.ZipFile)
                {
                    replayEventSource.EmbeddedContentRead += args =>
                        ProjectImportsCollector.FlushBlobToFile(FilePath, args.ContentStream);
                }

                // If raw events are provided - let's try to use the advantage.
                // But other subscribers can later on subscribe to structured events -
                //  for this reason we do only subscribe delayed.
                replayEventSource.DeferredInitialize(
                    // For raw events we cannot write the initial info - as we cannot write
                    //  at the same time as raw events are being written - this would break the deduplicated strings store.
                    // But we need to write the version info - but since we read/write raw - let's not change the version info.
                    () =>
                    {
                        binaryWriter.Write(replayEventSource.FileFormatVersion);
                        binaryWriter.Write(replayEventSource.MinimumReaderVersion);
                        replayEventSource.RawLogRecordReceived += RawEvents_LogDataSliceReceived;
                        // Replay separated strings here as well (and do not deduplicate! It would skew string indexes)
                        replayEventSource.StringReadDone += strArg => eventArgsWriter.WriteStringRecord(strArg.StringToBeUsed);
                    },
                    SubscribeToStructuredEvents);
            }
            else
            {
                SubscribeToStructuredEvents();
            }

            KnownTelemetry.LoggingConfigurationTelemetry.BinaryLogger = true;

            void SubscribeToStructuredEvents()
            {
                // Write the version info - the latest version is written only for structured events replaying
                //  as raw events do not change structure - hence the version is the same as the one they were written with.
                binaryWriter.Write(FileFormatVersion);
                binaryWriter.Write(MinimumReaderVersion);

                if (!omitInitialInfo)
                {
                    LogInitialInfo();
                }

                eventSource.AnyEventRaised += EventSource_AnyEventRaised;
            }
        }

        private void EventArgsWriter_EmbedFile(string filePath)
        {
            if (projectImportsCollector != null)
            {
                projectImportsCollector.AddFile(filePath);
            }
        }

        private void LogInitialInfo()
        {
            LogMessage("BinLogFilePath=" + FilePath);
            LogMessage("CurrentUICulture=" + System.Globalization.CultureInfo.CurrentUICulture.Name);
        }

        private void LogMessage(string text)
        {
            var args = new BuildMessageEventArgs(text, helpKeyword: null, senderName: "BinaryLogger", MessageImportance.Normal);
            args.BuildEventContext = BuildEventContext.Invalid;
            Write(args);
        }

        /// <summary>
        /// Closes the underlying file stream.
        /// </summary>
        public void Shutdown()
        {
            Environment.SetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING", _initialTargetOutputLogging ? "true" : null);
            Environment.SetEnvironmentVariable("MSBUILDLOGIMPORTS", _initialLogImports ? "1" : null);
            Environment.SetEnvironmentVariable("MSBUILDBINARYLOGGERENABLED", _initialIsBinaryLoggerEnabled);

            Traits.Instance.EscapeHatches.LogProjectImports = _initialLogImports;
            Traits.Instance.EnableTargetOutputLogging = _initialTargetOutputLogging;

            if (projectImportsCollector != null)
            {
                // Write the build check editorconfig file paths to the log
                foreach (var filePath in EditorConfigParser.EditorConfigFilePaths)
                {
                    projectImportsCollector.AddFile(filePath);
                }
                EditorConfigParser.ClearEditorConfigFilePaths();
                projectImportsCollector.Close();

                if (CollectProjectImports == ProjectImportsCollectionMode.Embed)
                {
                    projectImportsCollector.ProcessResult(
                        streamToEmbed => eventArgsWriter.WriteBlob(BinaryLogRecordKind.ProjectImportArchive, streamToEmbed),
                        LogMessage);

                    projectImportsCollector.DeleteArchive();
                }

                projectImportsCollector.FileIOExceptionEvent -= EventSource_AnyEventRaised;
                projectImportsCollector = null;
            }


            if (stream != null)
            {
                // It's hard to determine whether we're at the end of decoding GZipStream
                // so add an explicit 0 at the end to signify end of file
                stream.WriteByte((byte)BinaryLogRecordKind.EndOfFile);
                stream.Flush();
                stream.Dispose();
                stream = null;
            }
        }

        private void RawEvents_LogDataSliceReceived(BinaryLogRecordKind recordKind, Stream stream)
        {
            eventArgsWriter.WriteBlob(recordKind, stream);
        }

        private void EventSource_AnyEventRaised(object sender, BuildEventArgs e)
        {
            Write(e);
        }

        private void Write(BuildEventArgs e)
        {
            if (stream != null)
            {
                if (projectImportsCollector != null)
                {
                    CollectImports(e);
                }

                if (DoNotWriteToBinlog(e))
                {
                    return;
                }

                // TODO: think about queuing to avoid contention
                lock (eventArgsWriter)
                {
                    eventArgsWriter.Write(e);
                }
            }
        }

        private static bool DoNotWriteToBinlog(BuildEventArgs e)
        {
            return e is GeneratedFileUsedEventArgs;
        }

        private void CollectImports(BuildEventArgs e)
        {
            if (e is ProjectImportedEventArgs importArgs && importArgs.ImportedProjectFile != null)
            {
                projectImportsCollector.AddFile(importArgs.ImportedProjectFile);
            }
            else if (e is ProjectStartedEventArgs projectArgs)
            {
                projectImportsCollector.AddFile(projectArgs.ProjectFile);
            }
            else if (e is MetaprojectGeneratedEventArgs { metaprojectXml: { } } metaprojectArgs)
            {
                projectImportsCollector.AddFileFromMemory(metaprojectArgs.ProjectFile, metaprojectArgs.metaprojectXml);
            }
            else if (e is ResponseFileUsedEventArgs responseFileArgs && responseFileArgs.ResponseFilePath != null)
            {
                projectImportsCollector.AddFile(responseFileArgs.ResponseFilePath);
            }
            else if (e is GeneratedFileUsedEventArgs generatedFileUsedEventArgs && generatedFileUsedEventArgs.FilePath != null)
            {
                string fullPath = Path.GetFullPath(generatedFileUsedEventArgs.FilePath);
                projectImportsCollector.AddFileFromMemory(fullPath, generatedFileUsedEventArgs.Content);
            }
        }

        /// <summary>
        /// Processes the parameters given to the logger from MSBuild.
        /// </summary>
        /// <exception cref="LoggerException">
        /// </exception>
        private void ProcessParameters(out bool omitInitialInfo)
        {
            var parsedParams = ParseParameters(Parameters);
            
            omitInitialInfo = parsedParams.OmitInitialInfo;
            
            // Only set CollectProjectImports if it was explicitly specified in parameters
            if (parsedParams.HasProjectImportsParameter)
            {
                CollectProjectImports = parsedParams.ProjectImportsCollectionMode;
            }

            // Handle the file path - expand wildcards if needed
            if (parsedParams.LogFilePath == null)
            {
                // Either no path was specified, or it contained wildcards
                // Check if any parameter was a wildcard path
                var parameters = Parameters.Split(MSBuildConstants.SemicolonChar, StringSplitOptions.RemoveEmptyEntries);
                foreach (var parameter in parameters)
                {
                    if (TryInterpretPathParameter(parameter, out string filePath))
                    {
                        FilePath = filePath;
                        break;
                    }
                }
            }
            else
            {
                FilePath = parsedParams.LogFilePath;
            }

            if (FilePath == null)
            {
                FilePath = "msbuild.binlog";
            }
            KnownTelemetry.LoggingConfigurationTelemetry.BinaryLoggerUsedDefaultName = FilePath == "msbuild.binlog";

            try
            {
                FilePath = Path.GetFullPath(FilePath);
            }
            catch (Exception e)
            {
                string errorCode;
                string helpKeyword;
                string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out errorCode, out helpKeyword, "InvalidFileLoggerFile", FilePath, e.Message);
                throw new LoggerException(message, e, errorCode, helpKeyword);
            }
        }

        private bool TryInterpretPathParameter(string parameter, out string filePath)
        {
            bool hasPathPrefix = parameter.StartsWith(LogFileParameterPrefix, StringComparison.OrdinalIgnoreCase);

            if (hasPathPrefix)
            {
                parameter = parameter.Substring(LogFileParameterPrefix.Length);
            }

            parameter = parameter.Trim('"');

            bool isWildcard = ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_12) && parameter.Contains("{}");
            bool hasProperExtension = parameter.EndsWith(BinlogFileExtension, StringComparison.OrdinalIgnoreCase);
            filePath = parameter;

            if (!isWildcard)
            {
                return hasProperExtension;
            }

            filePath = parameter.Replace("{}", GetUniqueStamp(), StringComparison.Ordinal);

            if (!hasProperExtension)
            {
                filePath += BinlogFileExtension;
            }
            return true;
        }

        private string GetUniqueStamp()
            => (PathParameterExpander ?? ExpandPathParameter)(string.Empty);

        private static string ExpandPathParameter(string parameters)
            => $"{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}--{EnvironmentUtilities.CurrentProcessId}--{StringUtils.GenerateRandomString(6)}";
    }
}
