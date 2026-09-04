// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Data.Sqlite;

namespace SqliteLogger
{
    internal static class SchemaInitializer
    {
        internal static void Initialize(SqliteConnection connection)
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = Ddl;
            cmd.ExecuteNonQuery();
        }

        private const string Ddl = @"
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA cache_size = -65536;

CREATE TABLE IF NOT EXISTS Build (
    BuildId         INTEGER PRIMARY KEY,
    StartTimeMs     INTEGER NOT NULL,
    EndTimeMs       INTEGER,
    Succeeded       INTEGER,
    MSBuildVersion  TEXT
);

CREATE TABLE IF NOT EXISTS Evaluations (
    EvaluationId    INTEGER PRIMARY KEY,
    BuildId         INTEGER NOT NULL REFERENCES Build(BuildId),
    MsBuildEvalId   INTEGER NOT NULL,
    ProjectFile     TEXT NOT NULL,
    StartTimeMs     INTEGER NOT NULL,
    EndTimeMs       INTEGER,
    DurationMs      INTEGER AS (EndTimeMs - StartTimeMs) STORED
);
CREATE INDEX IF NOT EXISTS idx_eval_project ON Evaluations(ProjectFile);
CREATE INDEX IF NOT EXISTS idx_eval_build   ON Evaluations(BuildId);

CREATE TABLE IF NOT EXISTS EvaluationProperties (
    EvaluationId    INTEGER NOT NULL REFERENCES Evaluations(EvaluationId),
    BuildId         INTEGER NOT NULL REFERENCES Build(BuildId),
    Name            TEXT NOT NULL,
    Value           TEXT,
    PRIMARY KEY (EvaluationId, Name)
) WITHOUT ROWID;
CREATE INDEX IF NOT EXISTS idx_evalprop_build ON EvaluationProperties(BuildId);

CREATE TABLE IF NOT EXISTS EvaluationItems (
    Id              INTEGER PRIMARY KEY,
    EvaluationId    INTEGER NOT NULL REFERENCES Evaluations(EvaluationId),
    BuildId         INTEGER NOT NULL REFERENCES Build(BuildId),
    ItemType        TEXT NOT NULL,
    ItemSpec        TEXT NOT NULL,
    Metadata        TEXT
);
CREATE INDEX IF NOT EXISTS idx_evalitems_type  ON EvaluationItems(EvaluationId, ItemType);
CREATE INDEX IF NOT EXISTS idx_evalitems_build ON EvaluationItems(BuildId);

CREATE TABLE IF NOT EXISTS Projects (
    ProjectId         INTEGER PRIMARY KEY,
    BuildId           INTEGER NOT NULL REFERENCES Build(BuildId),
    ProjectContextId  INTEGER NOT NULL,
    NodeId            INTEGER NOT NULL,
    EvaluationId      INTEGER REFERENCES Evaluations(EvaluationId),
    ParentProjectId   INTEGER REFERENCES Projects(ProjectId),
    ProjectFile       TEXT NOT NULL,
    TargetNames       TEXT,
    GlobalProperties  TEXT,
    StartTimeMs       INTEGER NOT NULL,
    EndTimeMs         INTEGER,
    Succeeded         INTEGER,
    DurationMs        INTEGER AS (EndTimeMs - StartTimeMs) STORED
);
CREATE INDEX IF NOT EXISTS idx_proj_file     ON Projects(ProjectFile);
CREATE INDEX IF NOT EXISTS idx_proj_ctx      ON Projects(BuildId, ProjectContextId);
CREATE INDEX IF NOT EXISTS idx_proj_duration ON Projects(DurationMs DESC);
CREATE INDEX IF NOT EXISTS idx_proj_build    ON Projects(BuildId);

CREATE TABLE IF NOT EXISTS Targets (
    TargetId          INTEGER PRIMARY KEY,
    BuildId           INTEGER NOT NULL REFERENCES Build(BuildId),
    ProjectId         INTEGER NOT NULL REFERENCES Projects(ProjectId),
    NodeId            INTEGER NOT NULL,
    ProjectFile       TEXT NOT NULL,
    Name              TEXT NOT NULL,
    TargetFile        TEXT,
    ParentTarget      TEXT,
    BuildReason       TEXT,
    Skipped           INTEGER NOT NULL DEFAULT 0,
    SkipReason        TEXT,
    Succeeded         INTEGER,
    StartTimeMs       INTEGER,
    EndTimeMs         INTEGER,
    DurationMs        INTEGER AS (EndTimeMs - StartTimeMs) STORED
);
CREATE INDEX IF NOT EXISTS idx_tgt_project  ON Targets(ProjectId);
CREATE INDEX IF NOT EXISTS idx_tgt_name     ON Targets(Name);
CREATE INDEX IF NOT EXISTS idx_tgt_duration ON Targets(DurationMs DESC) WHERE Skipped = 0;
CREATE INDEX IF NOT EXISTS idx_tgt_build    ON Targets(BuildId);

CREATE TABLE IF NOT EXISTS Tasks (
    TaskId            INTEGER PRIMARY KEY,
    BuildId           INTEGER NOT NULL REFERENCES Build(BuildId),
    TargetId          INTEGER NOT NULL REFERENCES Targets(TargetId),
    ProjectId         INTEGER NOT NULL,
    NodeId            INTEGER NOT NULL,
    ProjectFile       TEXT NOT NULL,
    Name              TEXT NOT NULL,
    TaskAssembly      TEXT,
    Succeeded         INTEGER,
    StartTimeMs       INTEGER NOT NULL,
    EndTimeMs         INTEGER,
    DurationMs        INTEGER AS (EndTimeMs - StartTimeMs) STORED
);
CREATE INDEX IF NOT EXISTS idx_task_target   ON Tasks(TargetId);
CREATE INDEX IF NOT EXISTS idx_task_name     ON Tasks(Name);
CREATE INDEX IF NOT EXISTS idx_task_duration ON Tasks(DurationMs DESC);
CREATE INDEX IF NOT EXISTS idx_task_build    ON Tasks(BuildId);

CREATE TABLE IF NOT EXISTS Strings (
    StringId  INTEGER PRIMARY KEY,
    Value     TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS TaskParameters (
    Id              INTEGER PRIMARY KEY,
    BuildId         INTEGER NOT NULL REFERENCES Build(BuildId),
    TaskId          INTEGER NOT NULL REFERENCES Tasks(TaskId),
    Kind            TEXT NOT NULL,
    ParameterName   TEXT,
    PropertyName    TEXT,
    ItemType        TEXT
);
CREATE INDEX IF NOT EXISTS idx_tp_task  ON TaskParameters(TaskId);
CREATE INDEX IF NOT EXISTS idx_tp_build ON TaskParameters(BuildId);

CREATE TABLE IF NOT EXISTS TaskParameterItems (
    Id              INTEGER PRIMARY KEY,
    ParameterId     INTEGER NOT NULL REFERENCES TaskParameters(Id),
    ItemSpecId      INTEGER NOT NULL REFERENCES Strings(StringId)
);
CREATE INDEX IF NOT EXISTS idx_tpi_param ON TaskParameterItems(ParameterId);

CREATE TABLE IF NOT EXISTS TaskParameterMetadata (
    ItemId    INTEGER NOT NULL REFERENCES TaskParameterItems(Id),
    KeyId     INTEGER NOT NULL REFERENCES Strings(StringId),
    ValueId   INTEGER NOT NULL REFERENCES Strings(StringId),
    PRIMARY KEY (ItemId, KeyId)
) WITHOUT ROWID;

CREATE TABLE IF NOT EXISTS Files (
    Id        INTEGER PRIMARY KEY,
    FilePath  TEXT NOT NULL UNIQUE,
    Content   BLOB
);

CREATE TABLE IF NOT EXISTS Diagnostics (
    Id              INTEGER PRIMARY KEY,
    BuildId         INTEGER NOT NULL REFERENCES Build(BuildId),
    Severity        TEXT NOT NULL,
    Code            TEXT,
    Message         TEXT NOT NULL,
    File            TEXT,
    LineNumber      INTEGER,
    ColumnNumber    INTEGER,
    EndLineNumber   INTEGER,
    EndColumnNumber INTEGER,
    ProjectFile     TEXT,
    ProjectId       INTEGER REFERENCES Projects(ProjectId),
    TargetId        INTEGER REFERENCES Targets(TargetId),
    TaskId          INTEGER REFERENCES Tasks(TaskId),
    TimestampMs     INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_diag_severity ON Diagnostics(Severity);
CREATE INDEX IF NOT EXISTS idx_diag_code     ON Diagnostics(Code) WHERE Code IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_diag_project  ON Diagnostics(ProjectId);
CREATE INDEX IF NOT EXISTS idx_diag_build    ON Diagnostics(BuildId);

CREATE TABLE IF NOT EXISTS Messages (
    Id              INTEGER PRIMARY KEY,
    BuildId         INTEGER NOT NULL REFERENCES Build(BuildId),
    Importance      TEXT NOT NULL,
    Message         TEXT NOT NULL,
    ProjectId       INTEGER REFERENCES Projects(ProjectId),
    TargetId        INTEGER REFERENCES Targets(TargetId),
    TaskId          INTEGER REFERENCES Tasks(TaskId),
    TimestampMs     INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_msg_project ON Messages(ProjectId);
CREATE INDEX IF NOT EXISTS idx_msg_build   ON Messages(BuildId);

-- Views for common queries

CREATE VIEW IF NOT EXISTS ExpensiveProjects AS
SELECT BuildId, ProjectFile,
       COUNT(*) AS ExecutionCount,
       SUM(DurationMs) AS TotalDurationMs,
       MAX(DurationMs) AS MaxDurationMs
FROM Projects WHERE DurationMs IS NOT NULL
GROUP BY BuildId, ProjectFile ORDER BY TotalDurationMs DESC;

CREATE VIEW IF NOT EXISTS ExpensiveTargets AS
SELECT BuildId, Name,
       COUNT(*) AS ExecutionCount,
       SUM(CASE WHEN Skipped=0 THEN 1 ELSE 0 END) AS RanCount,
       SUM(CASE WHEN Skipped=1 THEN 1 ELSE 0 END) AS SkippedCount,
       SUM(DurationMs) AS TotalDurationMs,
       MAX(DurationMs) AS MaxDurationMs
FROM Targets GROUP BY BuildId, Name ORDER BY TotalDurationMs DESC;

CREATE VIEW IF NOT EXISTS ExpensiveTasks AS
SELECT BuildId, Name,
       COUNT(*) AS ExecutionCount,
       SUM(DurationMs) AS TotalDurationMs,
       MAX(DurationMs) AS MaxDurationMs,
       MIN(DurationMs) AS MinDurationMs,
       AVG(DurationMs) AS AvgDurationMs
FROM Tasks WHERE DurationMs IS NOT NULL
GROUP BY BuildId, Name ORDER BY TotalDurationMs DESC;

CREATE VIEW IF NOT EXISTS Errors AS SELECT * FROM Diagnostics WHERE Severity='Error';
CREATE VIEW IF NOT EXISTS Warnings AS SELECT * FROM Diagnostics WHERE Severity='Warning';

CREATE VIEW IF NOT EXISTS NodeTimeline AS
SELECT BuildId, NodeId, StartTimeMs, EndTimeMs, DurationMs, Name AS TargetName, ProjectFile
FROM Targets WHERE Skipped=0 AND StartTimeMs IS NOT NULL
ORDER BY BuildId, NodeId, StartTimeMs;

-- Files with content (non-null only, for quick browsing)
CREATE VIEW IF NOT EXISTS FilesWithContent AS
SELECT Id, FilePath, LENGTH(Content) AS ContentLength
FROM Files WHERE Content IS NOT NULL;

-- Denormalized view: one row per task parameter item with strings resolved
CREATE VIEW IF NOT EXISTS TaskParameterItemsView AS
SELECT tp.Id AS ParameterId, tp.BuildId, tp.TaskId, tp.Kind, tp.ParameterName,
       tp.ItemType, tpi.Id AS ItemId, s.Value AS ItemSpec
FROM TaskParameters tp
JOIN TaskParameterItems tpi ON tpi.ParameterId = tp.Id
JOIN Strings s ON tpi.ItemSpecId = s.StringId;

-- Denormalized view: one row per metadata entry with all strings resolved
CREATE VIEW IF NOT EXISTS TaskParameterMetadataView AS
SELECT tpm.ItemId, sk.Value AS MetadataName, sv.Value AS MetadataValue
FROM TaskParameterMetadata tpm
JOIN Strings sk ON tpm.KeyId = sk.StringId
JOIN Strings sv ON tpm.ValueId = sv.StringId;

-- Full denormalized view: items joined with metadata as semicolon-delimited pairs
CREATE VIEW IF NOT EXISTS TaskParametersView AS
SELECT tp.Id AS ParameterId, tp.BuildId, tp.TaskId, tp.Kind, tp.ParameterName,
       tp.ItemType, si.Value AS ItemSpec,
       (SELECT GROUP_CONCAT(sk.Value || '=' || sv.Value, '; ')
        FROM TaskParameterMetadata tpm
        JOIN Strings sk ON tpm.KeyId = sk.StringId
        JOIN Strings sv ON tpm.ValueId = sv.StringId
        WHERE tpm.ItemId = tpi.Id
       ) AS Metadata
FROM TaskParameters tp
JOIN TaskParameterItems tpi ON tpi.ParameterId = tp.Id
JOIN Strings si ON tpi.ItemSpecId = si.StringId;

-- Timeline view: every item that exists or changes during a project's execution
CREATE VIEW IF NOT EXISTS ProjectItemTimeline AS
SELECT
    p.ProjectId,
    p.BuildId,
    e.EndTimeMs AS TimestampMs,
    'Evaluation' AS Source,
    NULL AS TargetName,
    NULL AS TaskName,
    ei.ItemType,
    ei.ItemSpec,
    ei.Metadata
FROM Projects p
JOIN Evaluations e ON e.EvaluationId = p.EvaluationId
JOIN EvaluationItems ei ON ei.EvaluationId = p.EvaluationId

UNION ALL

SELECT
    tsk.ProjectId,
    tp.BuildId,
    tsk.StartTimeMs AS TimestampMs,
    tp.Kind AS Source,
    tgt.Name AS TargetName,
    tsk.Name AS TaskName,
    tp.ItemType,
    si.Value AS ItemSpec,
    (SELECT GROUP_CONCAT(sk.Value || '=' || sv.Value, '; ')
     FROM TaskParameterMetadata tpm
     JOIN Strings sk ON tpm.KeyId = sk.StringId
     JOIN Strings sv ON tpm.ValueId = sv.StringId
     WHERE tpm.ItemId = tpi.Id
    ) AS Metadata
FROM TaskParameters tp
JOIN TaskParameterItems tpi ON tpi.ParameterId = tp.Id
JOIN Strings si ON tpi.ItemSpecId = si.StringId
JOIN Tasks tsk ON tp.TaskId = tsk.TaskId
JOIN Targets tgt ON tsk.TargetId = tgt.TargetId
WHERE tp.Kind IN ('AddItem', 'RemoveItem')
   OR (tp.Kind = 'TaskOutput' AND tp.PropertyName IS NULL)

ORDER BY TimestampMs, Source;

-- Timeline view: every property value set during a project's lifecycle
CREATE VIEW IF NOT EXISTS ProjectPropertyTimeline AS
SELECT
    p.ProjectId,
    p.BuildId,
    e.EndTimeMs AS TimestampMs,
    'Evaluation' AS Source,
    NULL AS TargetName,
    NULL AS TaskName,
    ep.Name AS PropertyName,
    ep.Value AS PropertyValue
FROM Projects p
JOIN Evaluations e ON e.EvaluationId = p.EvaluationId
JOIN EvaluationProperties ep ON ep.EvaluationId = p.EvaluationId

UNION ALL

SELECT
    tsk.ProjectId,
    tp.BuildId,
    tsk.StartTimeMs AS TimestampMs,
    tp.Kind AS Source,
    tgt.Name AS TargetName,
    tsk.Name AS TaskName,
    tp.PropertyName,
    si.Value AS PropertyValue
FROM TaskParameters tp
JOIN TaskParameterItems tpi ON tpi.ParameterId = tp.Id
JOIN Strings si ON tpi.ItemSpecId = si.StringId
JOIN Tasks tsk ON tp.TaskId = tsk.TaskId
JOIN Targets tgt ON tsk.TargetId = tgt.TargetId
WHERE tp.Kind = 'TaskOutput' AND tp.PropertyName IS NOT NULL

ORDER BY TimestampMs;
";
    }
}
