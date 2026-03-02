// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Data.Sqlite;

#if NET
using System.Text.Json;
#endif

#nullable enable

namespace SqliteLogger
{
    internal sealed class DatabaseWriter : IDisposable
    {
        private const int BatchSize = 1000;

        private readonly SqliteConnection _connection;
        private SqliteTransaction? _transaction;
        private int _eventCount;

        // The BuildId for the current run (auto-generated rowid).
        private long _buildId;

        // Context tracking: MSBuild context ids -> our rowids.
        private readonly Dictionary<int, long> _projectsByContext = new Dictionary<int, long>();
        private readonly Dictionary<(int ctx, int targetId), long> _targetsByContext = new Dictionary<(int, int), long>();
        private readonly Dictionary<(int ctx, int taskId), long> _tasksByContext = new Dictionary<(int, int), long>();
        private readonly Dictionary<int, long> _evaluationsById = new Dictionary<int, long>();

        // File deduplication: tracks which file paths have already been stored
        private readonly HashSet<string> _seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Options
        private readonly bool _includeTaskInputs;
        private readonly bool _verboseMessages;
        private readonly bool _includeEvalProperties;
        private readonly bool _includeEvalItems;

        // All prepared statements (for transaction reassignment)
        private readonly List<SqliteCommand> _allCommands = new List<SqliteCommand>();

        // Prepared statements
        private readonly SqliteCommand _insertBuild;
        private readonly SqliteCommand _updateBuild;
        private readonly SqliteCommand _insertEvaluation;
        private readonly SqliteCommand _updateEvaluation;
        private readonly SqliteCommand _insertEvalProperty;
        private readonly SqliteCommand _insertEvalItem;
        private readonly SqliteCommand _insertProject;
        private readonly SqliteCommand _updateProject;
        private readonly SqliteCommand _insertTarget;
        private readonly SqliteCommand _updateTarget;
        private readonly SqliteCommand _insertTask;
        private readonly SqliteCommand _updateTask;
        private readonly SqliteCommand _insertTaskParameter;
        private readonly SqliteCommand _insertFile;
        private readonly SqliteCommand _insertDiagnostic;
        private readonly SqliteCommand _insertMessage;

        internal DatabaseWriter(string filePath, bool includeTaskInputs, bool verboseMessages, bool includeEvalProperties, bool includeEvalItems)
        {
            _includeTaskInputs = includeTaskInputs;
            _verboseMessages = verboseMessages;
            _includeEvalProperties = includeEvalProperties;
            _includeEvalItems = includeEvalItems;

            _connection = new SqliteConnection($"Data Source={filePath}");
            _connection.Open();

            SchemaInitializer.Initialize(_connection);

            _transaction = _connection.BeginTransaction();

            // Prepare all statements
            _insertBuild = PrepareCommand(
                "INSERT INTO Build (StartTimeMs, MSBuildVersion) VALUES (@start, @ver); SELECT last_insert_rowid();",
                "@start", "@ver");

            _updateBuild = PrepareCommand(
                "UPDATE Build SET EndTimeMs=@end, Succeeded=@ok WHERE BuildId=@id",
                "@end", "@ok", "@id");

            _insertEvaluation = PrepareCommand(
                "INSERT INTO Evaluations (BuildId, MsBuildEvalId, ProjectFile, StartTimeMs) VALUES (@bid, @eid, @file, @start); SELECT last_insert_rowid();",
                "@bid", "@eid", "@file", "@start");

            _updateEvaluation = PrepareCommand(
                "UPDATE Evaluations SET EndTimeMs=@end WHERE EvaluationId=@id",
                "@end", "@id");

            _insertEvalProperty = PrepareCommand(
                "INSERT OR IGNORE INTO EvaluationProperties (EvaluationId, BuildId, Name, Value) VALUES (@eid, @bid, @name, @val)",
                "@eid", "@bid", "@name", "@val");

            _insertEvalItem = PrepareCommand(
                "INSERT INTO EvaluationItems (EvaluationId, BuildId, ItemType, ItemSpec, Metadata) VALUES (@eid, @bid, @type, @spec, @meta)",
                "@eid", "@bid", "@type", "@spec", "@meta");

            _insertProject = PrepareCommand(
                "INSERT INTO Projects (BuildId, ProjectContextId, NodeId, EvaluationId, ParentProjectId, ProjectFile, TargetNames, GlobalProperties, StartTimeMs) " +
                "VALUES (@bid, @ctx, @node, @eval, @parent, @file, @targets, @props, @start); SELECT last_insert_rowid();",
                "@bid", "@ctx", "@node", "@eval", "@parent", "@file", "@targets", "@props", "@start");

            _updateProject = PrepareCommand(
                "UPDATE Projects SET EndTimeMs=@end, Succeeded=@ok WHERE ProjectId=@id",
                "@end", "@ok", "@id");

            _insertTarget = PrepareCommand(
                "INSERT INTO Targets (BuildId, ProjectId, NodeId, ProjectFile, Name, TargetFile, ParentTarget, BuildReason, StartTimeMs) " +
                "VALUES (@bid, @pid, @node, @file, @name, @tfile, @parent, @reason, @start); SELECT last_insert_rowid();",
                "@bid", "@pid", "@node", "@file", "@name", "@tfile", "@parent", "@reason", "@start");

            _updateTarget = PrepareCommand(
                "UPDATE Targets SET EndTimeMs=@end, Succeeded=@ok WHERE TargetId=@id",
                "@end", "@ok", "@id");

            _insertTask = PrepareCommand(
                "INSERT INTO Tasks (BuildId, TargetId, ProjectId, NodeId, ProjectFile, Name, TaskAssembly, StartTimeMs) " +
                "VALUES (@bid, @tid, @pid, @node, @file, @name, @asm, @start); SELECT last_insert_rowid();",
                "@bid", "@tid", "@pid", "@node", "@file", "@name", "@asm", "@start");

            _updateTask = PrepareCommand(
                "UPDATE Tasks SET EndTimeMs=@end, Succeeded=@ok WHERE TaskId=@id",
                "@end", "@ok", "@id");

            _insertTaskParameter = PrepareCommand(
                "INSERT INTO TaskParameters (BuildId, TaskId, Kind, ParameterName, ItemType, Items) VALUES (@bid, @tid, @kind, @pname, @itype, @items)",
                "@bid", "@tid", "@kind", "@pname", "@itype", "@items");

            _insertFile = PrepareCommand(
                "INSERT OR IGNORE INTO Files (FilePath, Content) VALUES (@path, @content)",
                "@path", "@content");

            _insertDiagnostic = PrepareCommand(
                "INSERT INTO Diagnostics (BuildId, Severity, Code, Message, File, LineNumber, ColumnNumber, EndLineNumber, EndColumnNumber, ProjectFile, ProjectId, TargetId, TaskId, TimestampMs) " +
                "VALUES (@bid, @sev, @code, @msg, @file, @line, @col, @eline, @ecol, @pfile, @pid, @tid, @taskid, @ts)",
                "@bid", "@sev", "@code", "@msg", "@file", "@line", "@col", "@eline", "@ecol", "@pfile", "@pid", "@tid", "@taskid", "@ts");

            _insertMessage = PrepareCommand(
                "INSERT INTO Messages (BuildId, Importance, Message, ProjectId, TargetId, TaskId, TimestampMs) " +
                "VALUES (@bid, @imp, @msg, @pid, @tid, @taskid, @ts)",
                "@bid", "@imp", "@msg", "@pid", "@tid", "@taskid", "@ts");
        }

        private SqliteCommand PrepareCommand(string sql, params string[] paramNames)
        {
            SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = _transaction;
            foreach (string p in paramNames)
            {
                cmd.Parameters.Add(new SqliteParameter(p, DBNull.Value));
            }
            cmd.Prepare();
            _allCommands.Add(cmd);
            return cmd;
        }

        private void CheckpointTransaction()
        {
            _eventCount++;
            if (_eventCount >= BatchSize)
            {
                _transaction?.Commit();
                _transaction = _connection.BeginTransaction();
                foreach (SqliteCommand cmd in _allCommands)
                {
                    cmd.Transaction = _transaction;
                }
                _eventCount = 0;
            }
        }

        private static object DbValue(string? value) => value is null ? (object)DBNull.Value : value;
        private static object DbValue(long? value) => value.HasValue ? (object)value.Value : DBNull.Value;
        private static object DbBool(bool value) => value ? 1L : 0L;

        #region Build events

        internal void OnBuildStarted(BuildStartedEventArgs e)
        {
            SetParam(_insertBuild, "@start", Timestamps.ToUnixMilliseconds(e.Timestamp));
            SetParam(_insertBuild, "@ver", DbValue(e.SenderName));
            _buildId = (long)_insertBuild.ExecuteScalar()!;
            CheckpointTransaction();
        }

        internal void OnBuildFinished(BuildFinishedEventArgs e)
        {
            SetParam(_updateBuild, "@end", Timestamps.ToUnixMilliseconds(e.Timestamp));
            SetParam(_updateBuild, "@ok", DbBool(e.Succeeded));
            SetParam(_updateBuild, "@id", _buildId);
            _updateBuild.ExecuteNonQuery();
            CheckpointTransaction();
        }

        #endregion

        #region Evaluation events

        internal void HandleEvaluationStarted(ProjectEvaluationStartedEventArgs e)
        {
            int msBuildEvalId = e.BuildEventContext?.EvaluationId ?? -1;

            SetParam(_insertEvaluation, "@bid", _buildId);
            SetParam(_insertEvaluation, "@eid", msBuildEvalId);
            SetParam(_insertEvaluation, "@file", DbValue(e.ProjectFile));
            SetParam(_insertEvaluation, "@start", Timestamps.ToUnixMilliseconds(e.Timestamp));
            long rowId = (long)_insertEvaluation.ExecuteScalar()!;
            _evaluationsById[msBuildEvalId] = rowId;
            CheckpointTransaction();
        }

        internal void HandleEvaluationFinished(ProjectEvaluationFinishedEventArgs e)
        {
            int msBuildEvalId = e.BuildEventContext?.EvaluationId ?? -1;
            if (!_evaluationsById.TryGetValue(msBuildEvalId, out long rowId))
            {
                return;
            }

            SetParam(_updateEvaluation, "@end", Timestamps.ToUnixMilliseconds(e.Timestamp));
            SetParam(_updateEvaluation, "@id", rowId);
            _updateEvaluation.ExecuteNonQuery();
            CheckpointTransaction();

            // Properties
            if (_includeEvalProperties && e.Properties is IEnumerable properties)
            {
                foreach (object item in properties)
                {
                    if (item is DictionaryEntry entry)
                    {
                        SetParam(_insertEvalProperty, "@eid", rowId);
                        SetParam(_insertEvalProperty, "@bid", _buildId);
                        SetParam(_insertEvalProperty, "@name", entry.Key?.ToString() ?? string.Empty);
                        SetParam(_insertEvalProperty, "@val", DbValue(entry.Value?.ToString()));
                        _insertEvalProperty.ExecuteNonQuery();
                        CheckpointTransaction();
                    }
                }
            }

            // Items
            if (_includeEvalItems && e.Items is IEnumerable items)
            {
                foreach (object item in items)
                {
                    if (item is DictionaryEntry entry && entry.Value is ITaskItem taskItem)
                    {
                        string? metadata = GetTaskItemMetadataJson(taskItem);
                        SetParam(_insertEvalItem, "@eid", rowId);
                        SetParam(_insertEvalItem, "@bid", _buildId);
                        SetParam(_insertEvalItem, "@type", entry.Key?.ToString() ?? string.Empty);
                        SetParam(_insertEvalItem, "@spec", taskItem.ItemSpec);
                        SetParam(_insertEvalItem, "@meta", DbValue(metadata));
                        _insertEvalItem.ExecuteNonQuery();
                        CheckpointTransaction();
                    }
                }
            }
        }

        #endregion

        #region Project events

        internal void HandleProjectStarted(ProjectStartedEventArgs e)
        {
            BuildEventContext? ctx = e.BuildEventContext;
            int contextId = ctx?.ProjectContextId ?? -1;
            int nodeId = ctx?.NodeId ?? -1;

            // Resolve evaluation FK
            int msBuildEvalId = ctx?.EvaluationId ?? -1;
            _evaluationsById.TryGetValue(msBuildEvalId, out long evalRowId);

            // Resolve parent project FK
            long? parentRowId = null;
            if (e.ParentProjectBuildEventContext is BuildEventContext parentCtx &&
                parentCtx.ProjectContextId != BuildEventContext.InvalidProjectContextId)
            {
                _projectsByContext.TryGetValue(parentCtx.ProjectContextId, out long pid);
                if (pid != 0)
                {
                    parentRowId = pid;
                }
            }

            // Serialize global properties
            string? globalPropsJson = SerializeGlobalProperties(e.GlobalProperties);

            SetParam(_insertProject, "@bid", _buildId);
            SetParam(_insertProject, "@ctx", contextId);
            SetParam(_insertProject, "@node", nodeId);
            SetParam(_insertProject, "@eval", evalRowId != 0 ? (object)evalRowId : DBNull.Value);
            SetParam(_insertProject, "@parent", DbValue(parentRowId));
            SetParam(_insertProject, "@file", DbValue(e.ProjectFile));
            SetParam(_insertProject, "@targets", DbValue(e.TargetNames));
            SetParam(_insertProject, "@props", DbValue(globalPropsJson));
            SetParam(_insertProject, "@start", Timestamps.ToUnixMilliseconds(e.Timestamp));
            long rowId = (long)_insertProject.ExecuteScalar()!;
            _projectsByContext[contextId] = rowId;
            CheckpointTransaction();

            CollectFileFromDisk(e.ProjectFile);
        }

        internal void HandleProjectFinished(ProjectFinishedEventArgs e)
        {
            int contextId = e.BuildEventContext?.ProjectContextId ?? -1;
            if (!_projectsByContext.TryGetValue(contextId, out long rowId))
            {
                return;
            }

            SetParam(_updateProject, "@end", Timestamps.ToUnixMilliseconds(e.Timestamp));
            SetParam(_updateProject, "@ok", DbBool(e.Succeeded));
            SetParam(_updateProject, "@id", rowId);
            _updateProject.ExecuteNonQuery();
            CheckpointTransaction();
        }

        #endregion

        #region Target events

        internal void HandleTargetStarted(TargetStartedEventArgs e)
        {
            BuildEventContext? ctx = e.BuildEventContext;
            int contextId = ctx?.ProjectContextId ?? -1;
            int nodeId = ctx?.NodeId ?? -1;
            int msBuildTargetId = ctx?.TargetId ?? -1;

            _projectsByContext.TryGetValue(contextId, out long projectRowId);

            SetParam(_insertTarget, "@bid", _buildId);
            SetParam(_insertTarget, "@pid", projectRowId != 0 ? (object)projectRowId : DBNull.Value);
            SetParam(_insertTarget, "@node", nodeId);
            SetParam(_insertTarget, "@file", DbValue(e.ProjectFile));
            SetParam(_insertTarget, "@name", DbValue(e.TargetName));
            SetParam(_insertTarget, "@tfile", DbValue(e.TargetFile));
            SetParam(_insertTarget, "@parent", DbValue(e.ParentTarget));
            SetParam(_insertTarget, "@reason", e.BuildReason.ToString());
            SetParam(_insertTarget, "@start", Timestamps.ToUnixMilliseconds(e.Timestamp));
            long rowId = (long)_insertTarget.ExecuteScalar()!;
            _targetsByContext[(contextId, msBuildTargetId)] = rowId;
            CheckpointTransaction();
        }

        internal void HandleTargetFinished(TargetFinishedEventArgs e)
        {
            BuildEventContext? ctx = e.BuildEventContext;
            int contextId = ctx?.ProjectContextId ?? -1;
            int msBuildTargetId = ctx?.TargetId ?? -1;

            if (!_targetsByContext.TryGetValue((contextId, msBuildTargetId), out long rowId))
            {
                return;
            }

            SetParam(_updateTarget, "@end", Timestamps.ToUnixMilliseconds(e.Timestamp));
            SetParam(_updateTarget, "@ok", DbBool(e.Succeeded));
            SetParam(_updateTarget, "@id", rowId);
            _updateTarget.ExecuteNonQuery();
            CheckpointTransaction();
        }

        internal void HandleTargetSkipped(TargetSkippedEventArgs e)
        {
            BuildEventContext? ctx = e.BuildEventContext;
            int contextId = ctx?.ProjectContextId ?? -1;

            _projectsByContext.TryGetValue(contextId, out long projectRowId);

            // For skipped targets, insert a complete row with Skipped=1
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.Transaction = _transaction;
            cmd.CommandText =
                "INSERT INTO Targets (BuildId, ProjectId, NodeId, ProjectFile, Name, TargetFile, ParentTarget, BuildReason, Skipped, SkipReason, Succeeded, StartTimeMs, EndTimeMs) " +
                "VALUES (@bid, @pid, @node, @file, @name, @tfile, @parent, @reason, 1, @skipreason, 1, @ts, @ts)";
            cmd.Parameters.AddWithValue("@bid", _buildId);
            cmd.Parameters.AddWithValue("@pid", projectRowId != 0 ? (object)projectRowId : DBNull.Value);
            cmd.Parameters.AddWithValue("@node", ctx?.NodeId ?? -1);
            cmd.Parameters.AddWithValue("@file", DbValue(e.ProjectFile));
            cmd.Parameters.AddWithValue("@name", DbValue(e.TargetName));
            cmd.Parameters.AddWithValue("@tfile", DbValue(e.TargetFile));
            cmd.Parameters.AddWithValue("@parent", DbValue(e.ParentTarget));
            cmd.Parameters.AddWithValue("@reason", e.BuildReason.ToString());
            cmd.Parameters.AddWithValue("@skipreason", e.SkipReason.ToString());
            cmd.Parameters.AddWithValue("@ts", Timestamps.ToUnixMilliseconds(e.Timestamp));
            cmd.ExecuteNonQuery();
            CheckpointTransaction();
        }

        #endregion

        #region Task events

        internal void HandleTaskStarted(TaskStartedEventArgs e)
        {
            BuildEventContext? ctx = e.BuildEventContext;
            int contextId = ctx?.ProjectContextId ?? -1;
            int nodeId = ctx?.NodeId ?? -1;
            int msBuildTargetId = ctx?.TargetId ?? -1;
            int msBuildTaskId = ctx?.TaskId ?? -1;

            _targetsByContext.TryGetValue((contextId, msBuildTargetId), out long targetRowId);
            _projectsByContext.TryGetValue(contextId, out long projectRowId);

            SetParam(_insertTask, "@bid", _buildId);
            SetParam(_insertTask, "@tid", targetRowId != 0 ? (object)targetRowId : DBNull.Value);
            SetParam(_insertTask, "@pid", projectRowId != 0 ? (object)projectRowId : DBNull.Value);
            SetParam(_insertTask, "@node", nodeId);
            SetParam(_insertTask, "@file", DbValue(e.ProjectFile));
            SetParam(_insertTask, "@name", DbValue(e.TaskName));
            SetParam(_insertTask, "@asm", DbValue(e.TaskAssemblyLocation));
            SetParam(_insertTask, "@start", Timestamps.ToUnixMilliseconds(e.Timestamp));
            long rowId = (long)_insertTask.ExecuteScalar()!;
            _tasksByContext[(contextId, msBuildTaskId)] = rowId;
            CheckpointTransaction();
        }

        internal void HandleTaskFinished(TaskFinishedEventArgs e)
        {
            BuildEventContext? ctx = e.BuildEventContext;
            int contextId = ctx?.ProjectContextId ?? -1;
            int msBuildTaskId = ctx?.TaskId ?? -1;

            if (!_tasksByContext.TryGetValue((contextId, msBuildTaskId), out long rowId))
            {
                return;
            }

            SetParam(_updateTask, "@end", Timestamps.ToUnixMilliseconds(e.Timestamp));
            SetParam(_updateTask, "@ok", DbBool(e.Succeeded));
            SetParam(_updateTask, "@id", rowId);
            _updateTask.ExecuteNonQuery();
            CheckpointTransaction();
        }

        internal void HandleTaskParameter(TaskParameterEventArgs e)
        {
            if (!_includeTaskInputs)
            {
                return;
            }

            BuildEventContext? ctx = e.BuildEventContext;
            int contextId = ctx?.ProjectContextId ?? -1;
            int msBuildTaskId = ctx?.TaskId ?? -1;

            if (!_tasksByContext.TryGetValue((contextId, msBuildTaskId), out long taskRowId))
            {
                return;
            }

            string? itemsJson = SerializeTaskParameterItems(e.Items);

            SetParam(_insertTaskParameter, "@bid", _buildId);
            SetParam(_insertTaskParameter, "@tid", taskRowId);
            SetParam(_insertTaskParameter, "@kind", e.Kind.ToString());
            SetParam(_insertTaskParameter, "@pname", DbValue(e.ParameterName));
            SetParam(_insertTaskParameter, "@itype", DbValue(e.ItemType));
            SetParam(_insertTaskParameter, "@items", DbValue(itemsJson));
            _insertTaskParameter.ExecuteNonQuery();
            CheckpointTransaction();
        }

        #endregion

        #region Diagnostics

        internal void OnError(BuildErrorEventArgs e)
        {
            InsertDiagnostic("Error", e.Code, e.Message, e.File, e.LineNumber, e.ColumnNumber, e.EndLineNumber, e.EndColumnNumber, e.ProjectFile, e.BuildEventContext, e.Timestamp);
        }

        internal void OnWarning(BuildWarningEventArgs e)
        {
            InsertDiagnostic("Warning", e.Code, e.Message, e.File, e.LineNumber, e.ColumnNumber, e.EndLineNumber, e.EndColumnNumber, e.ProjectFile, e.BuildEventContext, e.Timestamp);
        }

        private void InsertDiagnostic(string severity, string? code, string? message, string? file, int line, int col, int endLine, int endCol, string? projectFile, BuildEventContext? ctx, DateTime timestamp)
        {
            int contextId = ctx?.ProjectContextId ?? -1;
            int msBuildTargetId = ctx?.TargetId ?? -1;
            int msBuildTaskId = ctx?.TaskId ?? -1;

            _projectsByContext.TryGetValue(contextId, out long projectRowId);
            _targetsByContext.TryGetValue((contextId, msBuildTargetId), out long targetRowId);
            _tasksByContext.TryGetValue((contextId, msBuildTaskId), out long taskRowId);

            SetParam(_insertDiagnostic, "@bid", _buildId);
            SetParam(_insertDiagnostic, "@sev", severity);
            SetParam(_insertDiagnostic, "@code", DbValue(code));
            SetParam(_insertDiagnostic, "@msg", (object?)message ?? string.Empty);
            SetParam(_insertDiagnostic, "@file", DbValue(file));
            SetParam(_insertDiagnostic, "@line", line);
            SetParam(_insertDiagnostic, "@col", col);
            SetParam(_insertDiagnostic, "@eline", endLine);
            SetParam(_insertDiagnostic, "@ecol", endCol);
            SetParam(_insertDiagnostic, "@pfile", DbValue(projectFile));
            SetParam(_insertDiagnostic, "@pid", projectRowId != 0 ? (object)projectRowId : DBNull.Value);
            SetParam(_insertDiagnostic, "@tid", targetRowId != 0 ? (object)targetRowId : DBNull.Value);
            SetParam(_insertDiagnostic, "@taskid", taskRowId != 0 ? (object)taskRowId : DBNull.Value);
            SetParam(_insertDiagnostic, "@ts", Timestamps.ToUnixMilliseconds(timestamp));
            _insertDiagnostic.ExecuteNonQuery();
            CheckpointTransaction();
        }

        #endregion

        #region Messages

        internal void OnMessage(BuildMessageEventArgs e)
        {
            // Dispatch special subtypes first
            if (e is TargetSkippedEventArgs skipped)
            {
                HandleTargetSkipped(skipped);
                return;
            }

            if (e is TaskParameterEventArgs taskParam)
            {
                HandleTaskParameter(taskParam);
                return;
            }

            // Collect imported file contents (same files the binary logger embeds)
            CollectImportedFiles(e);

            // Filter by importance
            if (!_verboseMessages && e.Importance == MessageImportance.Low)
            {
                return;
            }

            BuildEventContext? ctx = e.BuildEventContext;
            int contextId = ctx?.ProjectContextId ?? -1;
            int msBuildTargetId = ctx?.TargetId ?? -1;
            int msBuildTaskId = ctx?.TaskId ?? -1;

            _projectsByContext.TryGetValue(contextId, out long projectRowId);
            _targetsByContext.TryGetValue((contextId, msBuildTargetId), out long targetRowId);
            _tasksByContext.TryGetValue((contextId, msBuildTaskId), out long taskRowId);

            SetParam(_insertMessage, "@bid", _buildId);
            SetParam(_insertMessage, "@imp", e.Importance.ToString());
            SetParam(_insertMessage, "@msg", (object?)e.Message ?? string.Empty);
            SetParam(_insertMessage, "@pid", projectRowId != 0 ? (object)projectRowId : DBNull.Value);
            SetParam(_insertMessage, "@tid", targetRowId != 0 ? (object)targetRowId : DBNull.Value);
            SetParam(_insertMessage, "@taskid", taskRowId != 0 ? (object)taskRowId : DBNull.Value);
            SetParam(_insertMessage, "@ts", Timestamps.ToUnixMilliseconds(e.Timestamp));
            _insertMessage.ExecuteNonQuery();
            CheckpointTransaction();
        }

        #endregion

        #region StatusEventRaised dispatch

        internal void OnStatusEvent(BuildStatusEventArgs e)
        {
            switch (e)
            {
                case ProjectEvaluationStartedEventArgs evalStarted:
                    HandleEvaluationStarted(evalStarted);
                    break;
                case ProjectEvaluationFinishedEventArgs evalFinished:
                    HandleEvaluationFinished(evalFinished);
                    break;
                case ProjectStartedEventArgs projStarted:
                    HandleProjectStarted(projStarted);
                    break;
                case ProjectFinishedEventArgs projFinished:
                    HandleProjectFinished(projFinished);
                    break;
                case TargetStartedEventArgs tgtStarted:
                    HandleTargetStarted(tgtStarted);
                    break;
                case TargetFinishedEventArgs tgtFinished:
                    HandleTargetFinished(tgtFinished);
                    break;
                case TaskStartedEventArgs taskStarted:
                    HandleTaskStarted(taskStarted);
                    break;
                case TaskFinishedEventArgs taskFinished:
                    HandleTaskFinished(taskFinished);
                    break;
            }
        }

        #endregion

        #region Shutdown / Dispose

        internal void Flush()
        {
            _transaction?.Commit();
            _transaction = null;
        }

        public void Dispose()
        {
            Flush();
            _insertBuild.Dispose();
            _updateBuild.Dispose();
            _insertEvaluation.Dispose();
            _updateEvaluation.Dispose();
            _insertEvalProperty.Dispose();
            _insertEvalItem.Dispose();
            _insertProject.Dispose();
            _updateProject.Dispose();
            _insertTarget.Dispose();
            _updateTarget.Dispose();
            _insertTask.Dispose();
            _updateTask.Dispose();
            _insertTaskParameter.Dispose();
            _insertFile.Dispose();
            _insertDiagnostic.Dispose();
            _insertMessage.Dispose();
            _connection.Dispose();
        }

        #endregion

        #region Helpers

        // Well-known MSBuild item metadata that are computed from the file system
        // based on ItemSpec. Stripping these saves ~31% of TaskParameters JSON with
        // zero information loss — they can be recomputed from ItemSpec at any time.
        private static readonly HashSet<string> BuiltinFileMetadata = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FullPath", "RootDir", "Filename", "Extension",
            "RelativeDir", "Directory", "RecursiveDir", "Identity",
            "ModifiedTime", "CreatedTime", "AccessedTime",
            "DefiningProjectFullPath", "DefiningProjectDirectory",
            "DefiningProjectName", "DefiningProjectExtension",
        };

        private static void SetParam(SqliteCommand cmd, string name, object value)
        {
            cmd.Parameters[name].Value = value;
        }

        private static string? SerializeGlobalProperties(IDictionary<string, string>? properties)
        {
            if (properties is null || properties.Count == 0)
            {
                return null;
            }

#if NET
            return JsonSerializer.Serialize(properties);
#else
            // Simple JSON serialization for .NET Framework / netstandard
            var sb = new System.Text.StringBuilder("{");
            bool first = true;
            foreach (var kvp in properties)
            {
                if (!first)
                {
                    sb.Append(',');
                }
                first = false;
                sb.Append('"').Append(EscapeJsonString(kvp.Key)).Append("\":\"").Append(EscapeJsonString(kvp.Value)).Append('"');
            }
            sb.Append('}');
            return sb.ToString();
#endif
        }

        private static string? GetTaskItemMetadataJson(ITaskItem taskItem)
        {
            ICollection metadataNames = taskItem.MetadataNames;
            if (metadataNames is null || metadataNames.Count == 0)
            {
                return null;
            }

#if NET
            var dict = new Dictionary<string, string>();
            foreach (string name in metadataNames)
            {
                if (!BuiltinFileMetadata.Contains(name))
                {
                    dict[name] = taskItem.GetMetadata(name);
                }
            }
            return dict.Count > 0 ? JsonSerializer.Serialize(dict) : null;
#else
            var sb = new System.Text.StringBuilder("{");
            bool first = true;
            foreach (string name in metadataNames)
            {
                if (BuiltinFileMetadata.Contains(name))
                {
                    continue;
                }
                if (!first)
                {
                    sb.Append(',');
                }
                first = false;
                string val = taskItem.GetMetadata(name);
                sb.Append('"').Append(EscapeJsonString(name)).Append("\":\"").Append(EscapeJsonString(val)).Append('"');
            }
            return first ? null : sb.Append('}').ToString();
#endif
        }

        private static string? SerializeTaskParameterItems(IList? items)
        {
            if (items is null || items.Count == 0)
            {
                return null;
            }

#if NET
            var list = new List<Dictionary<string, object>>();
            foreach (object? item in items)
            {
                if (item is ITaskItem ti)
                {
                    var dict = new Dictionary<string, object> { ["ItemSpec"] = ti.ItemSpec };
                    ICollection metadataNames = ti.MetadataNames;
                    if (metadataNames is { Count: > 0 })
                    {
                        var meta = new Dictionary<string, string>();
                        foreach (string name in metadataNames)
                        {
                            if (!BuiltinFileMetadata.Contains(name))
                            {
                                meta[name] = ti.GetMetadata(name);
                            }
                        }
                        if (meta.Count > 0)
                        {
                            dict["Metadata"] = meta;
                        }
                    }
                    list.Add(dict);
                }
                else if (item is not null)
                {
                    list.Add(new Dictionary<string, object> { ["ItemSpec"] = item.ToString()! });
                }
            }
            return JsonSerializer.Serialize(list);
#else
            var sb = new System.Text.StringBuilder("[");
            bool first = true;
            foreach (object item in items)
            {
                if (!first)
                {
                    sb.Append(',');
                }
                first = false;
                if (item is ITaskItem ti)
                {
                    sb.Append("{\"ItemSpec\":\"").Append(EscapeJsonString(ti.ItemSpec)).Append('"');
                    ICollection metadataNames = ti.MetadataNames;
                    if (metadataNames is { Count: > 0 })
                    {
                        var metaSb = new System.Text.StringBuilder();
                        bool mFirst = true;
                        foreach (string name in metadataNames)
                        {
                            if (BuiltinFileMetadata.Contains(name))
                            {
                                continue;
                            }
                            if (!mFirst)
                            {
                                metaSb.Append(',');
                            }
                            mFirst = false;
                            metaSb.Append('"').Append(EscapeJsonString(name)).Append("\":\"").Append(EscapeJsonString(ti.GetMetadata(name))).Append('"');
                        }
                        if (metaSb.Length > 0)
                        {
                            sb.Append(",\"Metadata\":{").Append(metaSb).Append('}');
                        }
                    }
                    sb.Append('}');
                }
                else if (item is not null)
                {
                    sb.Append("{\"ItemSpec\":\"").Append(EscapeJsonString(item.ToString())).Append("\"}");
                }
            }
            sb.Append(']');
            return sb.ToString();
#endif
        }

        /// <summary>
        /// Collect imported file contents from message events, mirroring
        /// the same files the binary logger embeds into binlogs.
        /// </summary>
        private void CollectImportedFiles(BuildMessageEventArgs e)
        {
            if (e is ProjectImportedEventArgs imported && imported.ImportedProjectFile is not null)
            {
                CollectFileFromDisk(imported.ImportedProjectFile);
            }
            else if (e is MetaprojectGeneratedEventArgs meta && meta.metaprojectXml is not null)
            {
                CollectFileFromMemory(meta.ProjectFile, meta.metaprojectXml);
            }
            else if (e is ResponseFileUsedEventArgs resp && resp.ResponseFilePath is not null)
            {
                CollectFileFromDisk(resp.ResponseFilePath);
            }
            else if (e.GetType().Name == "GeneratedFileUsedEventArgs")
            {
                // GeneratedFileUsedEventArgs is internal to Microsoft.Build.Framework,
                // so we access its properties via reflection.
                Type t = e.GetType();
                string? filePath = t.GetProperty("FilePath")?.GetValue(e) as string;
                string? content = t.GetProperty("Content")?.GetValue(e) as string;
                if (filePath is not null)
                {
                    CollectFileFromMemory(filePath, content);
                }
            }
        }

        private void CollectFileFromDisk(string? filePath)
        {
            if (filePath is null || !_seenFiles.Add(filePath))
            {
                return;
            }

            byte[]? content = null;
            try
            {
                if (File.Exists(filePath))
                {
                    content = File.ReadAllBytes(filePath);
                }
            }
            catch
            {
                // File may be locked or inaccessible; store the path with null content.
            }

            InsertFile(filePath, content);
        }

        private void CollectFileFromMemory(string? filePath, string? content)
        {
            if (filePath is null || !_seenFiles.Add(filePath))
            {
                return;
            }

            byte[]? bytes = content is not null ? System.Text.Encoding.UTF8.GetBytes(content) : null;
            InsertFile(filePath, bytes);
        }

        private void InsertFile(string filePath, byte[]? content)
        {
            SetParam(_insertFile, "@path", filePath);
            SetParam(_insertFile, "@content", content is null ? (object)DBNull.Value : content);
            _insertFile.ExecuteNonQuery();
            CheckpointTransaction();
        }

#if !NET
        private static string EscapeJsonString(string? value)
        {
            if (value is null)
            {
                return string.Empty;
            }
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
#endif

        #endregion
    }
}
