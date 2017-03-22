using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Serializes BuildEventArgs-derived objects into a provided BinaryWriter
    /// </summary>
    internal class BuildEventArgsWriter
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
            // the cases are ordered by most used first for performance
            if (e is BuildMessageEventArgs)
            {
                Write((BuildMessageEventArgs)e);
            }
            else if (e is TaskStartedEventArgs)
            {
                Write((TaskStartedEventArgs)e);
            }
            else if (e is TaskFinishedEventArgs)
            {
                Write((TaskFinishedEventArgs)e);
            }
            else if (e is TargetStartedEventArgs)
            {
                Write((TargetStartedEventArgs)e);
            }
            else if (e is TargetFinishedEventArgs)
            {
                Write((TargetFinishedEventArgs)e);
            }
            else if (e is BuildErrorEventArgs)
            {
                Write((BuildErrorEventArgs)e);
            }
            else if (e is BuildWarningEventArgs)
            {
                Write((BuildWarningEventArgs)e);
            }
            else if (e is ProjectStartedEventArgs)
            {
                Write((ProjectStartedEventArgs)e);
            }
            else if (e is ProjectFinishedEventArgs)
            {
                Write((ProjectFinishedEventArgs)e);
            }
            else if (e is BuildStartedEventArgs)
            {
                Write((BuildStartedEventArgs)e);
            }
            else if (e is BuildFinishedEventArgs)
            {
                Write((BuildFinishedEventArgs)e);
            }
            else
            {
                // convert all unrecognized objects to message
                // and just preserve the message
                var buildMessageEventArgs = new BuildMessageEventArgs(
                    e.Message,
                    e.HelpKeyword,
                    e.SenderName,
                    MessageImportance.Normal);
                Write(buildMessageEventArgs);
            }
        }

        private void Write(BuildStartedEventArgs e)
        {
            Write(BinaryLogRecordKind.BuildStarted);
            WriteBuildEventArgsFields(e);
            Write(e.BuildEnvironment);
        }

        private void Write(BuildFinishedEventArgs e)
        {
            Write(BinaryLogRecordKind.BuildFinished);
            WriteBuildEventArgsFields(e);
            Write(e.Succeeded);
        }

        private void Write(ProjectStartedEventArgs e)
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

            WriteProperties(e.Properties);

            WriteItems(e.Items);
        }

        private void Write(ProjectFinishedEventArgs e)
        {
            Write(BinaryLogRecordKind.ProjectFinished);
            WriteBuildEventArgsFields(e);
            WriteOptionalString(e.ProjectFile);
            Write(e.Succeeded);
        }

        private void Write(TargetStartedEventArgs e)
        {
            Write(BinaryLogRecordKind.TargetStarted);
            WriteBuildEventArgsFields(e);
            WriteOptionalString(e.TargetName);
            WriteOptionalString(e.ProjectFile);
            WriteOptionalString(e.TargetFile);
            WriteOptionalString(e.ParentTarget);
        }

        private void Write(TargetFinishedEventArgs e)
        {
            Write(BinaryLogRecordKind.TargetFinished);
            WriteBuildEventArgsFields(e);
            Write(e.Succeeded);
            WriteOptionalString(e.ProjectFile);
            WriteOptionalString(e.TargetFile);
            WriteOptionalString(e.TargetName);
            WriteItemList(e.TargetOutputs);
        }

        private void Write(TaskStartedEventArgs e)
        {
            Write(BinaryLogRecordKind.TaskStarted);
            WriteBuildEventArgsFields(e);
            WriteOptionalString(e.TaskName);
            WriteOptionalString(e.ProjectFile);
            WriteOptionalString(e.TaskFile);
        }

        private void Write(TaskFinishedEventArgs e)
        {
            Write(BinaryLogRecordKind.TaskFinished);
            WriteBuildEventArgsFields(e);
            Write(e.Succeeded);
            WriteOptionalString(e.TaskName);
            WriteOptionalString(e.ProjectFile);
            WriteOptionalString(e.TaskFile);
        }

        private void Write(BuildErrorEventArgs e)
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

        private void Write(BuildWarningEventArgs e)
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

        private void Write(BuildMessageEventArgs e)
        {
            if (e is CriticalBuildMessageEventArgs)
            {
                Write((CriticalBuildMessageEventArgs)e);
                return;
            }
            else if (e is TaskCommandLineEventArgs)
            {
                Write((TaskCommandLineEventArgs)e);
                return;
            }

            Write(BinaryLogRecordKind.Message);
            WriteMessageFields(e);
        }

        private void Write(CriticalBuildMessageEventArgs e)
        {
            Write(BinaryLogRecordKind.CriticalBuildMessage);
            WriteMessageFields(e);
        }

        private void Write(TaskCommandLineEventArgs e)
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
                Write(item.GetMetadata(metadataName));
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
        }

        private void Write<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        {
            if (keyValuePairs != null && keyValuePairs.Any())
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
    }
}
