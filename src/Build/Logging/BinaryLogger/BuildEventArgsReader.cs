using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Deserializes and returns BuildEventArgs-derived objects from a BinaryReader
    /// </summary>
    internal class BuildEventArgsReader
    {
        private readonly BinaryReader binaryReader;

        // reflection is needed to set these three fields because public constructors don't provide
        // a way to set these from the outside
        private static FieldInfo buildEventArgsFieldThreadId =
            typeof(BuildEventArgs).GetField("threadId", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo buildEventArgsFieldSenderName =
            typeof(BuildEventArgs).GetField("senderName", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo buildEventArgsFieldTimestamp =
            typeof(BuildEventArgs).GetField("timestamp", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Initializes a new instance of BuildEventArgsReader using a BinaryReader instance
        /// </summary>
        /// <param name="binaryReader">The BinaryReader to read BuildEventArgs from</param>
        public BuildEventArgsReader(BinaryReader binaryReader)
        {
            this.binaryReader = binaryReader;
        }

        /// <summary>
        /// Reads the next log record from the binary reader. If there are no more records, returns null.
        /// </summary>
        public BuildEventArgs Read()
        {
            BinaryLogRecordKind recordKind = (BinaryLogRecordKind)ReadInt32();

            BuildEventArgs result = null;
            switch (recordKind)
            {
                case BinaryLogRecordKind.EndOfFile:
                    break;
                case BinaryLogRecordKind.BuildStarted:
                    result = ReadBuildStartedEventArgs();
                    break;
                case BinaryLogRecordKind.BuildFinished:
                    result = ReadBuildFinishedEventArgs();
                    break;
                case BinaryLogRecordKind.ProjectStarted:
                    result = ReadProjectStartedEventArgs();
                    break;
                case BinaryLogRecordKind.ProjectFinished:
                    result = ReadProjectFinishedEventArgs();
                    break;
                case BinaryLogRecordKind.TargetStarted:
                    result = ReadTargetStartedEventArgs();
                    break;
                case BinaryLogRecordKind.TargetFinished:
                    result = ReadTargetFinishedEventArgs();
                    break;
                case BinaryLogRecordKind.TaskStarted:
                    result = ReadTaskStartedEventArgs();
                    break;
                case BinaryLogRecordKind.TaskFinished:
                    result = ReadTaskFinishedEventArgs();
                    break;
                case BinaryLogRecordKind.Error:
                    result = ReadBuildErrorEventArgs();
                    break;
                case BinaryLogRecordKind.Warning:
                    result = ReadBuildWarningEventArgs();
                    break;
                case BinaryLogRecordKind.Message:
                    result = ReadBuildMessageEventArgs();
                    break;
                case BinaryLogRecordKind.CriticalBuildMessage:
                    result = ReadCriticalBuildMessageEventArgs();
                    break;
                case BinaryLogRecordKind.TaskCommandLine:
                    result = ReadTaskCommandLineEventArgs();
                    break;
                default:
                    break;
            }

            return result;
        }

        private BuildEventArgs ReadBuildStartedEventArgs()
        {
            var fields = ReadBuildEventArgsFields();
            var environment = ReadStringDictionary();

            var e = new BuildStartedEventArgs(
                fields.Message,
                fields.HelpKeyword,
                environment);
            SetCommonFields(e, fields);
            return e;
        }

        private BuildEventArgs ReadBuildFinishedEventArgs()
        {
            var fields = ReadBuildEventArgsFields();
            var succeeded = ReadBoolean();

            var e = new BuildFinishedEventArgs(
                fields.Message,
                fields.HelpKeyword,
                succeeded,
                fields.Timestamp);
            SetCommonFields(e, fields);
            return e;
        }

        private BuildEventArgs ReadProjectStartedEventArgs()
        {
            var fields = ReadBuildEventArgsFields();
            BuildEventContext parentContext = null;
            if (ReadBoolean())
            {
                parentContext = ReadBuildEventContext();
            }

            var projectFile = ReadOptionalString();
            var projectId = ReadInt32();
            var targetNames = ReadString();
            var toolsVersion = ReadOptionalString();
            var propertyList = ReadPropertyList();
            var itemList = ReadItems();

            var e = new ProjectStartedEventArgs(
                projectId,
                fields.Message,
                fields.HelpKeyword,
                projectFile,
                targetNames,
                propertyList,
                itemList,
                parentContext,
                null,
                toolsVersion);
            SetCommonFields(e, fields);
            return e;
        }

        private BuildEventArgs ReadProjectFinishedEventArgs()
        {
            var fields = ReadBuildEventArgsFields();
            var projectFile = ReadOptionalString();
            var succeeded = ReadBoolean();

            var e = new ProjectFinishedEventArgs(
                fields.Message,
                fields.HelpKeyword,
                projectFile,
                succeeded,
                fields.Timestamp);
            SetCommonFields(e, fields);
            return e;
        }

        private BuildEventArgs ReadTargetStartedEventArgs()
        {
            var fields = ReadBuildEventArgsFields();
            var targetName = ReadOptionalString();
            var projectFile = ReadOptionalString();
            var targetFile = ReadOptionalString();
            var parentTarget = ReadOptionalString();

            var e = new TargetStartedEventArgs(
                fields.Message,
                fields.HelpKeyword,
                targetName,
                projectFile,
                targetFile,
                parentTarget,
                fields.Timestamp);
            SetCommonFields(e, fields);
            return e;
        }

        private BuildEventArgs ReadTargetFinishedEventArgs()
        {
            var fields = ReadBuildEventArgsFields();
            var succeeded = ReadBoolean();
            var projectFile = ReadOptionalString();
            var targetFile = ReadOptionalString();
            var targetName = ReadOptionalString();
            var targetOutputItemList = ReadItemList();

            var e = new TargetFinishedEventArgs(
                fields.Message,
                fields.HelpKeyword,
                targetName,
                projectFile,
                targetFile,
                succeeded,
                fields.Timestamp,
                targetOutputItemList);
            SetCommonFields(e, fields);
            return e;
        }

        private BuildEventArgs ReadTaskStartedEventArgs()
        {
            var fields = ReadBuildEventArgsFields();
            var taskName = ReadOptionalString();
            var projectFile = ReadOptionalString();
            var taskFile = ReadOptionalString();

            var e = new TaskStartedEventArgs(
                fields.Message,
                fields.HelpKeyword,
                projectFile,
                taskFile,
                taskName,
                fields.Timestamp);
            SetCommonFields(e, fields);
            return e;
        }

        private BuildEventArgs ReadTaskFinishedEventArgs()
        {
            var fields = ReadBuildEventArgsFields();
            var succeeded = ReadBoolean();
            var taskName = ReadOptionalString();
            var projectFile = ReadOptionalString();
            var taskFile = ReadOptionalString();

            var e = new TaskFinishedEventArgs(
                fields.Message,
                fields.HelpKeyword,
                projectFile,
                taskFile,
                taskName,
                succeeded,
                fields.Timestamp);
            SetCommonFields(e, fields);
            return e;
        }

        private BuildEventArgs ReadBuildErrorEventArgs()
        {
            var fields = ReadBuildEventArgsFields();
            ReadDiagnosticFields(fields);

            var e = new BuildErrorEventArgs(
                fields.Subcategory,
                fields.Code,
                fields.File,
                fields.LineNumber,
                fields.ColumnNumber,
                fields.EndLineNumber,
                fields.EndColumnNumber,
                fields.Message,
                fields.HelpKeyword,
                fields.SenderName,
                fields.Timestamp);
            e.BuildEventContext = fields.BuildEventContext;
            e.ProjectFile = fields.ProjectFile;
            return e;
        }

        private BuildEventArgs ReadBuildWarningEventArgs()
        {
            var fields = ReadBuildEventArgsFields();
            ReadDiagnosticFields(fields);

            var e = new BuildWarningEventArgs(
                fields.Subcategory,
                fields.Code,
                fields.File,
                fields.LineNumber,
                fields.ColumnNumber,
                fields.EndLineNumber,
                fields.EndColumnNumber,
                fields.Message,
                fields.HelpKeyword,
                fields.SenderName,
                fields.Timestamp);
            e.BuildEventContext = fields.BuildEventContext;
            e.ProjectFile = fields.ProjectFile;
            return e;
        }

        private BuildEventArgs ReadBuildMessageEventArgs()
        {
            var fields = ReadBuildEventArgsFields();
            var importance = (MessageImportance)ReadInt32();

            var e = new BuildMessageEventArgs(
                fields.Subcategory,
                fields.Code,
                fields.File,
                fields.LineNumber,
                fields.ColumnNumber,
                fields.EndLineNumber,
                fields.EndColumnNumber,
                fields.Message,
                fields.HelpKeyword,
                fields.SenderName,
                importance,
                fields.Timestamp);
            e.BuildEventContext = fields.BuildEventContext;
            e.ProjectFile = fields.ProjectFile;
            return e;
        }

        private BuildEventArgs ReadTaskCommandLineEventArgs()
        {
            var fields = ReadBuildEventArgsFields();
            var importance = (MessageImportance)ReadInt32();
            var commandLine = ReadOptionalString();
            var taskName = ReadOptionalString();

            var e = new TaskCommandLineEventArgs(
                commandLine,
                taskName,
                importance,
                fields.Timestamp);
            e.BuildEventContext = fields.BuildEventContext;
            e.ProjectFile = fields.ProjectFile;
            return e;
        }

        private BuildEventArgs ReadCriticalBuildMessageEventArgs()
        {
            var fields = ReadBuildEventArgsFields();
            var importance = (MessageImportance)ReadInt32();

            var e = new CriticalBuildMessageEventArgs(
                fields.Subcategory,
                fields.Code,
                fields.File,
                fields.LineNumber,
                fields.ColumnNumber,
                fields.EndLineNumber,
                fields.EndColumnNumber,
                fields.Message,
                fields.HelpKeyword,
                fields.SenderName,
                fields.Timestamp);
            e.BuildEventContext = fields.BuildEventContext;
            e.ProjectFile = fields.ProjectFile;
            return e;
        }

        /// <summary>
        /// For errors and warnings these 8 fields are written out explicitly
        /// (their presence is not marked as a bit in the flags). So we have to
        /// read explicitly.
        /// </summary>
        /// <param name="fields"></param>
        private void ReadDiagnosticFields(BuildEventArgsFields fields)
        {
            fields.Subcategory = ReadOptionalString();
            fields.Code = ReadOptionalString();
            fields.File = ReadOptionalString();
            fields.ProjectFile = ReadOptionalString();
            fields.LineNumber = ReadInt32();
            fields.ColumnNumber = ReadInt32();
            fields.EndLineNumber = ReadInt32();
            fields.EndColumnNumber = ReadInt32();
        }

        private BuildEventArgsFields ReadBuildEventArgsFields()
        {
            BuildEventArgsFieldFlags flags = (BuildEventArgsFieldFlags)ReadInt32();
            var result = new BuildEventArgsFields();
            result.Flags = flags;

            if ((flags & BuildEventArgsFieldFlags.Message) != 0)
            {
                result.Message = ReadString();
            }

            if ((flags & BuildEventArgsFieldFlags.BuildEventContext) != 0)
            {
                result.BuildEventContext = ReadBuildEventContext();
            }

            if ((flags & BuildEventArgsFieldFlags.ThreadId) != 0)
            {
                result.ThreadId = ReadInt32();
            }

            if ((flags & BuildEventArgsFieldFlags.HelpHeyword) != 0)
            {
                result.HelpKeyword = ReadString();
            }

            if ((flags & BuildEventArgsFieldFlags.SenderName) != 0)
            {
                result.SenderName = ReadString();
            }

            if ((flags & BuildEventArgsFieldFlags.Timestamp) != 0)
            {
                result.Timestamp = ReadDateTime();
            }

            if ((flags & BuildEventArgsFieldFlags.Subcategory) != 0)
            {
                result.Subcategory = ReadString();
            }

            if ((flags & BuildEventArgsFieldFlags.Code) != 0)
            {
                result.Code = ReadString();
            }

            if ((flags & BuildEventArgsFieldFlags.File) != 0)
            {
                result.File = ReadString();
            }

            if ((flags & BuildEventArgsFieldFlags.ProjectFile) != 0)
            {
                result.ProjectFile = ReadString();
            }

            if ((flags & BuildEventArgsFieldFlags.LineNumber) != 0)
            {
                result.LineNumber = ReadInt32();
            }

            if ((flags & BuildEventArgsFieldFlags.ColumnNumber) != 0)
            {
                result.ColumnNumber = ReadInt32();
            }

            if ((flags & BuildEventArgsFieldFlags.EndLineNumber) != 0)
            {
                result.EndLineNumber = ReadInt32();
            }

            if ((flags & BuildEventArgsFieldFlags.EndColumnNumber) != 0)
            {
                result.EndColumnNumber = ReadInt32();
            }

            return result;
        }

        private void SetCommonFields(BuildEventArgs buildEventArgs, BuildEventArgsFields fields)
        {
            buildEventArgs.BuildEventContext = fields.BuildEventContext;

            if ((fields.Flags & BuildEventArgsFieldFlags.ThreadId) != 0)
            {
                buildEventArgsFieldThreadId.SetValue(buildEventArgs, fields.ThreadId);
            }

            if ((fields.Flags & BuildEventArgsFieldFlags.SenderName) != 0)
            {
                buildEventArgsFieldSenderName.SetValue(buildEventArgs, fields.SenderName);
            }

            if ((fields.Flags & BuildEventArgsFieldFlags.Timestamp) != 0)
            {
                buildEventArgsFieldTimestamp.SetValue(buildEventArgs, fields.Timestamp);
            }
        }

        private ArrayList ReadPropertyList()
        {
            var properties = ReadStringDictionary();
            if (properties == null)
            {
                return null;
            }

            var list = new ArrayList();
            foreach (var property in properties)
            {
                var entry = new DictionaryEntry(property.Key, property.Value);
                list.Add(entry);
            }

            return list;
        }

        private BuildEventContext ReadBuildEventContext()
        {
            int nodeId = ReadInt32();
            int projectContextId = ReadInt32();
            int targetId = ReadInt32();
            int taskId = ReadInt32();
            int submissionId = ReadInt32();
            int projectInstanceId = ReadInt32();

            var result = new BuildEventContext(
                submissionId,
                nodeId,
                projectInstanceId,
                projectContextId,
                targetId,
                taskId);
            return result;
        }

        private Dictionary<string, string> ReadStringDictionary()
        {
            int count = ReadInt32();

            if (count == 0)
            {
                return null;
            }

            Dictionary<string, string> result = new Dictionary<string, string>(count);
            for (int i = 0; i < count; i++)
            {
                string key = ReadString();
                string value = ReadString();
                result[key] = value;
            }

            return result;
        }

        private class TaskItem : ITaskItem
        {
            public string ItemSpec { get; set; }
            public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

            public int MetadataCount => Metadata.Count;

            public ICollection MetadataNames => Metadata.Keys;

            public IDictionary CloneCustomMetadata()
            {
                return Metadata;
            }

            public void CopyMetadataTo(ITaskItem destinationItem)
            {
                throw new NotImplementedException();
            }

            public string GetMetadata(string metadataName)
            {
                return Metadata[metadataName];
            }

            public void RemoveMetadata(string metadataName)
            {
                throw new NotImplementedException();
            }

            public void SetMetadata(string metadataName, string metadataValue)
            {
                throw new NotImplementedException();
            }
        }

        private ITaskItem ReadItem()
        {
            var item = new TaskItem();
            item.ItemSpec = ReadString();

            int count = ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string name = ReadString();
                string value = ReadString();
                item.Metadata[name] = value;
            }

            return item;
        }

        private IEnumerable ReadItems()
        {
            int count = ReadInt32();
            if (count == 0)
            {
                return null;
            }

            var list = new List<DictionaryEntry>(count);

            for (int i = 0; i < count; i++)
            {
                string key = ReadString();
                ITaskItem item = ReadItem();
                list.Add(new DictionaryEntry(key, item));
            }

            return list;
        }

        private IEnumerable ReadItemList()
        {
            int count = ReadInt32();
            if (count == 0)
            {
                return null;
            }

            var list = new List<ITaskItem>(count);

            for (int i = 0; i < count; i++)
            {
                ITaskItem item = ReadItem();
                list.Add(item);
            }

            return list;
        }

        private string ReadOptionalString()
        {
            if (ReadBoolean())
            {
                return ReadString();
            }
            else
            {
                return null;
            }
        }

        private string ReadString()
        {
            return binaryReader.ReadString();
        }

        private int ReadInt32()
        {
            return Read7BitEncodedInt(binaryReader);
        }

        private bool ReadBoolean()
        {
            return binaryReader.ReadBoolean();
        }

        private DateTime ReadDateTime()
        {
            return new DateTime(binaryReader.ReadInt64(), (DateTimeKind)ReadInt32());
        }

        private int Read7BitEncodedInt(BinaryReader reader)
        {
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            int count = 0;
            int shift = 0;
            byte b;
            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                {
                    throw new FormatException();
                }

                // ReadByte handles end of stream cases for us.
                b = reader.ReadByte();
                count |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return count;
        }
    }
}
