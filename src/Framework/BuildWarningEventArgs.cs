// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System;
using System.IO;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for warning events
    /// </summary>
    /// <remarks>
    /// WARNING: marking a type [Serializable] without implementing
    /// ISerializable imposes a serialization contract -- it is a
    /// promise to never change the type's fields i.e. the type is
    /// immutable; adding new fields in the next version of the type
    /// without following certain special FX guidelines, can break both
    /// forward and backward compatibility
    /// </remarks>
#if FEATURE_BINARY_SERIALIZATION
    [Serializable]
#endif
    public class BuildWarningEventArgs : LazyFormattedBuildEventArgs
    {
        /// <summary>
        /// Default constructor 
        /// </summary>
        protected BuildWarningEventArgs()
            : base()
        {
            // do nothing
        }

        /// <summary>
        /// This constructor allows all event data to be initialized
        /// </summary>
        /// <param name="subcategory">event subcategory</param>
        /// <param name="code">event code</param>
        /// <param name="file">file associated with the event</param>
        /// <param name="lineNumber">line number (0 if not applicable)</param>
        /// <param name="columnNumber">column number (0 if not applicable)</param>
        /// <param name="endLineNumber">end line number (0 if not applicable)</param>
        /// <param name="endColumnNumber">end column number (0 if not applicable)</param>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="senderName">name of event sender</param>
        public BuildWarningEventArgs
        (
            string subcategory,
            string code,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string message,
            string helpKeyword,
            string senderName
        )
            : this(subcategory, code, file, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, helpKeyword, senderName, DateTime.UtcNow)
        {
        }

        /// <summary>
        /// This constructor allows timestamp to be set
        /// </summary>
        /// <param name="subcategory">event subcategory</param>
        /// <param name="code">event code</param>
        /// <param name="file">file associated with the event</param>
        /// <param name="lineNumber">line number (0 if not applicable)</param>
        /// <param name="columnNumber">column number (0 if not applicable)</param>
        /// <param name="endLineNumber">end line number (0 if not applicable)</param>
        /// <param name="endColumnNumber">end column number (0 if not applicable)</param>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="senderName">name of event sender</param>
        /// <param name="eventTimestamp">custom timestamp for the event</param>
        public BuildWarningEventArgs
        (
            string subcategory,
            string code,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string message,
            string helpKeyword,
            string senderName,
            DateTime eventTimestamp
        )
            : this(subcategory, code, file, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, helpKeyword, senderName, eventTimestamp, null)
        {
            // do nothing
        }

        /// <summary>
        /// This constructor allows timestamp to be set
        /// </summary>
        /// <param name="subcategory">event subcategory</param>
        /// <param name="code">event code</param>
        /// <param name="file">file associated with the event</param>
        /// <param name="lineNumber">line number (0 if not applicable)</param>
        /// <param name="columnNumber">column number (0 if not applicable)</param>
        /// <param name="endLineNumber">end line number (0 if not applicable)</param>
        /// <param name="endColumnNumber">end column number (0 if not applicable)</param>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="senderName">name of event sender</param>
        /// <param name="eventTimestamp">custom timestamp for the event</param>
        /// <param name="messageArgs">message arguments</param>
        public BuildWarningEventArgs
        (
            string subcategory,
            string code,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string message,
            string helpKeyword,
            string senderName,
            DateTime eventTimestamp,
            params object[] messageArgs
        )
            : base(message, helpKeyword, senderName, eventTimestamp, messageArgs)
        {
            this.subcategory = subcategory;
            this.code = code;
            this.file = file;
            this.lineNumber = lineNumber;
            this.columnNumber = columnNumber;
            this.endLineNumber = endLineNumber;
            this.endColumnNumber = endColumnNumber;
        }

        private string subcategory;
        private string code;
        private string file;
        private string projectFile;
        private int lineNumber;
        private int columnNumber;
        private int endLineNumber;
        private int endColumnNumber;
#if FEATURE_BINARY_SERIALIZATION
        #region CustomSerializationToStream
        /// <summary>
        /// Serializes the Errorevent to a stream through a binary writer
        /// </summary>
        /// <param name="writer">Binary writer which is attached to the stream the event will be serialized into</param>
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            #region SubCategory
            if (subcategory == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(subcategory);
            }
            #endregion
            #region Code
            if (code == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(code);
            }
            #endregion
            #region File
            if (file == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(file);
            }
            #endregion
            #region ProjectFile
            if (projectFile == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(projectFile);
            }
            #endregion
            writer.Write((Int32)lineNumber);
            writer.Write((Int32)columnNumber);
            writer.Write((Int32)endLineNumber);
            writer.Write((Int32)endColumnNumber);
        }

        /// <summary>
        /// Deserializes from a stream through a binary reader
        /// </summary>
        /// <param name="reader">Binary reader which is attached to the stream the event will be deserialized from</param>
        /// <param name="version">The version of the runtime the message packet was created from</param>
        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);
            #region SubCategory
            if (reader.ReadByte() == 0)
            {
                subcategory = null;
            }
            else
            {
                subcategory = reader.ReadString();
            }
            #endregion
            #region Code
            if (reader.ReadByte() == 0)
            {
                code = null;
            }
            else
            {
                code = reader.ReadString();
            }
            #endregion
            #region File
            if (reader.ReadByte() == 0)
            {
                file = null;
            }
            else
            {
                file = reader.ReadString();
            }
            #endregion
            #region ProjectFile
            if (version > 20)
            {
                if (reader.ReadByte() == 0)
                {
                    projectFile = null;
                }
                else
                {
                    projectFile = reader.ReadString();
                }
            }
            #endregion
            lineNumber = reader.ReadInt32();
            columnNumber = reader.ReadInt32();
            endLineNumber = reader.ReadInt32();
            endColumnNumber = reader.ReadInt32();
        }
        #endregion
#endif

        /// <summary>
        /// The custom sub-type of the event.         
        /// </summary>
        public string Subcategory
        {
            get
            {
                return subcategory;
            }
        }

        /// <summary>
        /// Code associated with event. 
        /// </summary>
        public string Code
        {
            get
            {
                return code;
            }
        }

        /// <summary>
        /// File associated with event.   
        /// </summary>
        public string File
        {
            get
            {
                return file;
            }
        }

        /// <summary>
        /// Line number of interest in associated file. 
        /// </summary>
        public int LineNumber
        {
            get
            {
                return lineNumber;
            }
        }

        /// <summary>
        /// Column number of interest in associated file. 
        /// </summary>
        public int ColumnNumber
        {
            get
            {
                return columnNumber;
            }
        }

        /// <summary>
        /// Ending line number of interest in associated file. 
        /// </summary>
        public int EndLineNumber
        {
            get
            {
                return endLineNumber;
            }
        }

        /// <summary>
        /// Ending column number of interest in associated file. 
        /// </summary>
        public int EndColumnNumber
        {
            get
            {
                return endColumnNumber;
            }
        }

        /// <summary>
        /// The project which was building when the message was issued.
        /// </summary>
        public string ProjectFile
        {
            get
            {
                return projectFile;
            }

            set
            {
                projectFile = value;
            }
        }
    }
}
