using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Serializes BuildEventArgs-derived objects into a provided BinaryWriter
    /// </summary>
    internal class BuildEventArgsWriter : IBuildEventArgsWriteVisitor
    {
        private readonly BinaryWriter binaryWriter;

        /// <summary>
        /// Initializes a new instance of BuildEventArgsWriter with a BinaryWriter
        /// </summary>
        /// <param name="binaryWriter">A BinaryWriter to write the BuildEventArgs instances to</param>
        public BuildEventArgsWriter(BinaryWriter binaryWriter)
        {
            this.binaryWriter = binaryWriter;
        }

        /// <summary>
        /// Write a provided instance of BuildEventArgs to the BinaryWriter
        /// </summary>
        public void Write(BuildEventArgs e)
        {
            e.Visit(this);
        }

        void IBuildEventArgsWriteVisitor.Visit(BuildEventArgs e)
        {
            // convert all unrecognized objects to message
            // and just preserve the message
            var buildMessageEventArgs = new BuildMessageEventArgs(
                e.Message,
                e.HelpKeyword,
                e.SenderName,
                MessageImportance.Normal,
                e.Timestamp);
            buildMessageEventArgs.BuildEventContext = e.BuildEventContext ?? BuildEventContext.Invalid;
            ((IBuildEventArgsWriteVisitor)this).Visit(buildMessageEventArgs);
        }

        public void WriteBlob(BinaryLogRecordKind kind, byte[] bytes)
        {
            Write(kind);
            Write(bytes.Length);
            Write(bytes);
        }

        void IBuildEventArgsWriteVisitor.Visit(BuildStartedEventArgs e)
        {
            Write(BinaryLogRecordKind.BuildStarted);
            WriteBuildEventArgsFields(e);
            Write(e.BuildEnvironment);
        }

        void IBuildEventArgsWriteVisitor.Visit(BuildFinishedEventArgs e)
        {
            Write(BinaryLogRecordKind.BuildFinished);
            WriteBuildEventArgsFields(e);
            Write(e.Succeeded);
        }

        void IBuildEventArgsWriteVisitor.Visit(ProjectEvaluationStartedEventArgs e)
        {
            Write(BinaryLogRecordKind.ProjectEvaluationStarted);
            WriteBuildEventArgsFields(e);
            Write(e.ProjectFile);
        }

        void IBuildEventArgsWriteVisitor.Visit(ProjectEvaluationFinishedEventArgs e)
        {
            Write(BinaryLogRecordKind.ProjectEvaluationFinished);

            WriteBuildEventArgsFields(e);
            Write(e.ProjectFile);

            Write(e.ProfilerResult.HasValue);
            if (e.ProfilerResult.HasValue)
            {
                Write(e.ProfilerResult.Value.ProfiledLocations.Count);

                foreach (var item in e.ProfilerResult.Value.ProfiledLocations)
                {
                    Write(item.Key);
                    Write(item.Value);
                }
            }
        }

        void IBuildEventArgsWriteVisitor.Visit(ProjectStartedEventArgs e)
        {
            Write(BinaryLogRecordKind.ProjectStarted);
            WriteBuildEventArgsFields(e);

            if (e.ParentProjectBuildEventContext == null)
            {
                Write(false);
            }
            else
            {
                Write(true);
                Write(e.ParentProjectBuildEventContext);
            }

            WriteOptionalString(e.ProjectFile);

            Write(e.ProjectId);
            Write(e.TargetNames);
            WriteOptionalString(e.ToolsVersion);

            if (e.GlobalProperties == null)
            {
                Write(false);
            }
            else
            {
                Write(true);
                Write(e.GlobalProperties);
            }

            WriteProperties(e.Properties);

            WriteItems(e.Items);
        }

        void IBuildEventArgsWriteVisitor.Visit(ProjectFinishedEventArgs e)
        {
            Write(BinaryLogRecordKind.ProjectFinished);
            WriteBuildEventArgsFields(e);
            WriteOptionalString(e.ProjectFile);
            Write(e.Succeeded);
        }

        void IBuildEventArgsWriteVisitor.Visit(TargetStartedEventArgs e)
        {
            Write(BinaryLogRecordKind.TargetStarted);
            WriteBuildEventArgsFields(e);
            WriteOptionalString(e.TargetName);
            WriteOptionalString(e.ProjectFile);
            WriteOptionalString(e.TargetFile);
            WriteOptionalString(e.ParentTarget);
            Write((int)e.BuildReason);
        }

        void IBuildEventArgsWriteVisitor.Visit(TargetFinishedEventArgs e)
        {
            Write(BinaryLogRecordKind.TargetFinished);
            WriteBuildEventArgsFields(e);
            Write(e.Succeeded);
            WriteOptionalString(e.ProjectFile);
            WriteOptionalString(e.TargetFile);
            WriteOptionalString(e.TargetName);
            WriteItemList(e.TargetOutputs);
        }

        void IBuildEventArgsWriteVisitor.Visit(TaskStartedEventArgs e)
        {
            Write(BinaryLogRecordKind.TaskStarted);
            WriteBuildEventArgsFields(e);
            WriteOptionalString(e.TaskName);
            WriteOptionalString(e.ProjectFile);
            WriteOptionalString(e.TaskFile);
        }

        void IBuildEventArgsWriteVisitor.Visit(TaskFinishedEventArgs e)
        {
            Write(BinaryLogRecordKind.TaskFinished);
            WriteBuildEventArgsFields(e);
            Write(e.Succeeded);
            WriteOptionalString(e.TaskName);
            WriteOptionalString(e.ProjectFile);
            WriteOptionalString(e.TaskFile);
        }

        void IBuildEventArgsWriteVisitor.Visit(BuildErrorEventArgs e)
        {
            Write(BinaryLogRecordKind.Error);
            WriteBuildEventArgsFields(e);
            WriteOptionalString(e.Subcategory);
            WriteOptionalString(e.Code);
            WriteOptionalString(e.File);
            WriteOptionalString(e.ProjectFile);
            Write(e.LineNumber);
            Write(e.ColumnNumber);
            Write(e.EndLineNumber);
            Write(e.EndColumnNumber);
        }

        void IBuildEventArgsWriteVisitor.Visit(BuildWarningEventArgs e)
        {
            Write(BinaryLogRecordKind.Warning);
            WriteBuildEventArgsFields(e);
            WriteOptionalString(e.Subcategory);
            WriteOptionalString(e.Code);
            WriteOptionalString(e.File);
            WriteOptionalString(e.ProjectFile);
            Write(e.LineNumber);
            Write(e.ColumnNumber);
            Write(e.EndLineNumber);
            Write(e.EndColumnNumber);
        }

        void IBuildEventArgsWriteVisitor.Visit(BuildMessageEventArgs e)
        {
            Write(BinaryLogRecordKind.Message);
            WriteMessageFields(e);
        }

        void IBuildEventArgsWriteVisitor.Visit(ProjectImportedEventArgs e)
        {
            Write(BinaryLogRecordKind.ProjectImported);
            WriteMessageFields(e);
            Write(e.ImportIgnored);
            WriteOptionalString(e.ImportedProjectFile);
            WriteOptionalString(e.UnexpandedProject);
        }

        void IBuildEventArgsWriteVisitor.Visit(TargetSkippedEventArgs e)
        {
            Write(BinaryLogRecordKind.TargetSkipped);
            WriteMessageFields(e);
            WriteOptionalString(e.TargetFile);
            WriteOptionalString(e.TargetName);
            WriteOptionalString(e.ParentTarget);
            Write((int)e.BuildReason);
        }

        void IBuildEventArgsWriteVisitor.Visit(CriticalBuildMessageEventArgs e)
        {
            Write(BinaryLogRecordKind.CriticalBuildMessage);
            WriteMessageFields(e);
        }

        void IBuildEventArgsWriteVisitor.Visit(PropertyReassignmentEventArgs e)
        {
            Write(BinaryLogRecordKind.PropertyReassignment);
            WriteMessageFields(e);
            Write(e.PropertyName);
            Write(e.PreviousValue);
            Write(e.NewValue);
            Write(e.Location);
        }

        void IBuildEventArgsWriteVisitor.Visit(UninitializedPropertyReadEventArgs e)
        {
            Write(BinaryLogRecordKind.UninitializedPropertyRead);
            WriteMessageFields(e);
            Write(e.PropertyName);
        }

        void IBuildEventArgsWriteVisitor.Visit(PropertyInitialValueSetEventArgs e)
        {
            Write(BinaryLogRecordKind.PropertyInitialValueSet);
            WriteMessageFields(e);
            Write(e.PropertyName);
            Write(e.PropertyValue);
            Write(e.PropertySource);
        }

        void IBuildEventArgsWriteVisitor.Visit(EnvironmentVariableReadEventArgs e)
        {
            Write(BinaryLogRecordKind.EnvironmentVariableRead);
            WriteMessageFields(e);
            Write(e.EnvironmentVariableName);
        }

        void IBuildEventArgsWriteVisitor.Visit(TaskCommandLineEventArgs e)
        {
            Write(BinaryLogRecordKind.TaskCommandLine);
            WriteMessageFields(e);
            WriteOptionalString(e.CommandLine);
            WriteOptionalString(e.TaskName);
        }

        private void WriteBuildEventArgsFields(BuildEventArgs e)
        {
            var flags = GetBuildEventArgsFieldFlags(e);
            Write((int)flags);
            WriteBaseFields(e, flags);
        }

        private void WriteBaseFields(BuildEventArgs e, BuildEventArgsFieldFlags flags)
        {
            if ((flags & BuildEventArgsFieldFlags.Message) != 0)
            {
                Write(e.Message);
            }

            if ((flags & BuildEventArgsFieldFlags.BuildEventContext) != 0)
            {
                Write(e.BuildEventContext);
            }

            if ((flags & BuildEventArgsFieldFlags.ThreadId) != 0)
            {
                Write(e.ThreadId);
            }

            if ((flags & BuildEventArgsFieldFlags.HelpHeyword) != 0)
            {
                Write(e.HelpKeyword);
            }

            if ((flags & BuildEventArgsFieldFlags.SenderName) != 0)
            {
                Write(e.SenderName);
            }

            if ((flags & BuildEventArgsFieldFlags.Timestamp) != 0)
            {
                Write(e.Timestamp);
            }
        }

        private void WriteMessageFields(BuildMessageEventArgs e)
        {
            var flags = GetBuildEventArgsFieldFlags(e);
            flags = GetMessageFlags(e, flags);

            Write((int)flags);

            WriteBaseFields(e, flags);

            if ((flags & BuildEventArgsFieldFlags.Subcategory) != 0)
            {
                Write(e.Subcategory);
            }

            if ((flags & BuildEventArgsFieldFlags.Code) != 0)
            {
                Write(e.Code);
            }

            if ((flags & BuildEventArgsFieldFlags.File) != 0)
            {
                Write(e.File);
            }

            if ((flags & BuildEventArgsFieldFlags.ProjectFile) != 0)
            {
                Write(e.ProjectFile);
            }

            if ((flags & BuildEventArgsFieldFlags.LineNumber) != 0)
            {
                Write(e.LineNumber);
            }

            if ((flags & BuildEventArgsFieldFlags.ColumnNumber) != 0)
            {
                Write(e.ColumnNumber);
            }

            if ((flags & BuildEventArgsFieldFlags.EndLineNumber) != 0)
            {
                Write(e.EndLineNumber);
            }

            if ((flags & BuildEventArgsFieldFlags.EndColumnNumber) != 0)
            {
                Write(e.EndColumnNumber);
            }

            Write((int)e.Importance);
        }

        private static BuildEventArgsFieldFlags GetMessageFlags(BuildMessageEventArgs e, BuildEventArgsFieldFlags flags)
        {
            if (e.Subcategory != null)
            {
                flags |= BuildEventArgsFieldFlags.Subcategory;
            }

            if (e.Code != null)
            {
                flags |= BuildEventArgsFieldFlags.Code;
            }

            if (e.File != null)
            {
                flags |= BuildEventArgsFieldFlags.File;
            }

            if (e.ProjectFile != null)
            {
                flags |= BuildEventArgsFieldFlags.ProjectFile;
            }

            if (e.LineNumber != 0)
            {
                flags |= BuildEventArgsFieldFlags.LineNumber;
            }

            if (e.ColumnNumber != 0)
            {
                flags |= BuildEventArgsFieldFlags.ColumnNumber;
            }

            if (e.EndLineNumber != 0)
            {
                flags |= BuildEventArgsFieldFlags.EndLineNumber;
            }

            if (e.EndColumnNumber != 0)
            {
                flags |= BuildEventArgsFieldFlags.EndColumnNumber;
            }

            return flags;
        }

        private static BuildEventArgsFieldFlags GetBuildEventArgsFieldFlags(BuildEventArgs e)
        {
            var flags = BuildEventArgsFieldFlags.None;
            if (e.BuildEventContext != null)
            {
                flags |= BuildEventArgsFieldFlags.BuildEventContext;
            }

            if (e.HelpKeyword != null)
            {
                flags |= BuildEventArgsFieldFlags.HelpHeyword;
            }

            if (!string.IsNullOrEmpty(e.Message))
            {
                flags |= BuildEventArgsFieldFlags.Message;
            }

            // no need to waste space for the default sender name
            if (e.SenderName != null && e.SenderName != "MSBuild")
            {
                flags |= BuildEventArgsFieldFlags.SenderName;
            }

            if (e.ThreadId > 0)
            {
                flags |= BuildEventArgsFieldFlags.ThreadId;
            }

            if (e.Timestamp != default(DateTime))
            {
                flags |= BuildEventArgsFieldFlags.Timestamp;
            }

            return flags;
        }

        private void WriteItemList(IEnumerable items)
        {
            var taskItems = items as IEnumerable<ITaskItem>;
            if (taskItems != null)
            {
                Write(taskItems.Count());

                foreach (var item in taskItems)
                {
                    Write(item);
                }

                return;
            }

            Write(0);
        }

        private void WriteItems(IEnumerable items)
        {
            if (items == null)
            {
                Write(0);
                return;
            }

            var entries = items.OfType<DictionaryEntry>()
                .Where(e => e.Key is string && e.Value is ITaskItem)
                .ToArray();
            Write(entries.Length);

            foreach (DictionaryEntry entry in entries)
            {
                string key = entry.Key as string;
                ITaskItem item = entry.Value as ITaskItem;
                Write(key);
                Write(item);
            }
        }

        private void Write(ITaskItem item)
        {
            Write(item.ItemSpec);
            IDictionary customMetadata = item.CloneCustomMetadata();
            Write(customMetadata.Count);

            foreach (string metadataName in customMetadata.Keys)
            {
                Write(metadataName);
                string valueOrError;

                try
                {
                    valueOrError = item.GetMetadata(metadataName);
                }
                catch (InvalidProjectFileException e)
                {
                    valueOrError = e.Message;
                }
                // Temporarily try catch all to mitigate frequent NullReferenceExceptions in
                // the logging code until CopyOnWritePropertyDictionary is replaced with
                // ImmutableDictionary. Calling into Debug.Fail to crash the process in case
                // the exception occures in Debug builds.
                catch (Exception e)
                {
                    valueOrError = e.Message;
                    Debug.Fail(e.ToString());
                }

                Write(valueOrError);
            }
        }

        private void WriteProperties(IEnumerable properties)
        {
            if (properties == null)
            {
                Write(0);
                return;
            }

            // there are no guarantees that the properties iterator won't change, so
            // take a snapshot and work with the readonly copy
            var propertiesArray = properties.OfType<DictionaryEntry>().ToArray();

            Write(propertiesArray.Length);

            foreach (DictionaryEntry entry in propertiesArray)
            {
                if (entry.Key is string && entry.Value is string)
                {
                    Write((string)entry.Key);
                    Write((string)entry.Value);
                }
                else
                {
                    // to keep the count accurate
                    Write("");
                    Write("");
                }
            }
        }

        private void Write(BuildEventContext buildEventContext)
        {
            Write(buildEventContext.NodeId);
            Write(buildEventContext.ProjectContextId);
            Write(buildEventContext.TargetId);
            Write(buildEventContext.TaskId);
            Write(buildEventContext.SubmissionId);
            Write(buildEventContext.ProjectInstanceId);
            Write(buildEventContext.EvaluationId);
        }

        private void Write<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        {
            if (keyValuePairs?.Any() == true)
            {
                Write(keyValuePairs.Count());
                foreach (var kvp in keyValuePairs)
                {
                    Write(kvp.Key.ToString());
                    Write(kvp.Value.ToString());
                }
            }
            else
            {
                Write(false);
            }
        }

        private void Write(BinaryLogRecordKind kind)
        {
            Write((int)kind);
        }

        private void Write(int value)
        {
            Write7BitEncodedInt(binaryWriter, value);
        }

        private void Write(long value)
        {
            binaryWriter.Write(value);
        }

        private void Write7BitEncodedInt(BinaryWriter writer, int value)
        {
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint)value;   // support negative numbers
            while (v >= 0x80)
            {
                writer.Write((byte)(v | 0x80));
                v >>= 7;
            }
            writer.Write((byte)v);
        }

        private void Write(byte[] bytes)
        {
            binaryWriter.Write(bytes);
        }

        private void Write(bool boolean)
        {
            binaryWriter.Write(boolean);
        }

        private void Write(string text)
        {
            if (text != null)
            {
                binaryWriter.Write(text);
            }
            else
            {
                binaryWriter.Write(false);
            }
        }

        private void WriteOptionalString(string text)
        {
            if (text == null)
            {
                Write(false);
            }
            else
            {
                Write(true);
                Write(text);
            }
        }

        private void Write(DateTime timestamp)
        {
            binaryWriter.Write(timestamp.Ticks);
            Write((int)timestamp.Kind);
        }

        private void Write(TimeSpan timeSpan)
        {
            binaryWriter.Write(timeSpan.Ticks);
        }

        private void Write(EvaluationLocation item)
        {
            WriteOptionalString(item.ElementName);
            WriteOptionalString(item.ElementDescription);
            WriteOptionalString(item.EvaluationPassDescription);
            WriteOptionalString(item.File);
            Write((int)item.Kind);
            Write((int)item.EvaluationPass);

            Write(item.Line.HasValue);
            if (item.Line.HasValue)
            {
                Write(item.Line.Value);
            }

            Write(item.Id);
            Write(item.ParentId.HasValue);
            if (item.ParentId.HasValue)
            {
                Write(item.ParentId.Value);
            }
        }

        private void Write(ProfiledLocation e)
        {
            Write(e.NumberOfHits);
            Write(e.ExclusiveTime);
            Write(e.InclusiveTime);
        }
    }
}
