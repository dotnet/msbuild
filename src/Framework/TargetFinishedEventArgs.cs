// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System;
using System.IO;
using System.Collections;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for target finished events
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
    public class TargetFinishedEventArgs : BuildStatusEventArgs
    {
        /// <summary>
        /// Default constructor 
        /// </summary>
        protected TargetFinishedEventArgs()
            : base()
        {
            // do nothing
        }

        /// <summary>
        /// This constructor allows event data to be initialized.
        /// Sender is assumed to be "MSBuild".
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="targetName">target name</param>
        /// <param name="projectFile">project file</param>
        /// <param name="targetFile">file in which the target is defined</param>
        /// <param name="succeeded">true if target built successfully</param>
        public TargetFinishedEventArgs
        (
            string message,
            string helpKeyword,
            string targetName,
            string projectFile,
            string targetFile,
            bool succeeded
        )
            : this(message, helpKeyword, targetName, projectFile, targetFile, succeeded, DateTime.UtcNow, null)
        {
        }

        /// <summary>
        /// This constructor allows event data to be initialized.
        /// Sender is assumed to be "MSBuild".
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="targetName">target name</param>
        /// <param name="projectFile">project file</param>
        /// <param name="targetFile">file in which the target is defined</param>
        /// <param name="succeeded">true if target built successfully</param>
        /// <param name="targetOutputs">Target output items for the target. If batching will be null for everything except for the last target in the batch</param>
        public TargetFinishedEventArgs
        (
            string message,
            string helpKeyword,
            string targetName,
            string projectFile,
            string targetFile,
            bool succeeded,
            IEnumerable targetOutputs
        )
            : this(message, helpKeyword, targetName, projectFile, targetFile, succeeded, DateTime.UtcNow, targetOutputs)
        {
        }

        /// <summary>
        /// This constructor allows event data to be initialized including the timestamp when the event was created.
        /// Sender is assumed to be "MSBuild".
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="targetName">target name</param>
        /// <param name="projectFile">project file</param>
        /// <param name="targetFile">file in which the target is defined</param>
        /// <param name="succeeded">true if target built successfully</param>
        /// <param name="eventTimestamp">Timestamp when the event was created</param>
        /// <param name="targetOutputs">An <see cref="IEnumerable"/> containing the outputs of the target.</param>
        public TargetFinishedEventArgs
        (
            string message,
            string helpKeyword,
            string targetName,
            string projectFile,
            string targetFile,
            bool succeeded,
            DateTime eventTimestamp,
            IEnumerable targetOutputs
        )
            : base(message, helpKeyword, "MSBuild", eventTimestamp)
        {
            this.targetName = targetName;
            this.succeeded = succeeded;
            this.projectFile = projectFile;
            this.targetFile = targetFile;
            this.targetOutputs = targetOutputs;
        }

        private string projectFile;
        private string targetFile;
        private string targetName;
        private bool succeeded;
        private IEnumerable targetOutputs;

#if FEATURE_BINARY_SERIALIZATION
        #region CustomSerializationToStream
        /// <summary>
        /// Serializes to a stream through a binary writer
        /// </summary>
        /// <param name="writer">Binary writer which is attached to the stream the event will be serialized into</param>
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
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
            #region TargetFile
            if (targetFile == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(targetFile);
            }
            #endregion TargetFile
            #region TargetName
            if (targetName == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(targetName);
            }
            #endregion
            writer.Write(succeeded);
        }

        /// <summary>
        /// Deserializes from a stream through a binary reader
        /// </summary>
        /// <param name="reader">Binary reader which is attached to the stream the event will be deserialized from</param>
        /// <param name="version">The version of the runtime the message packet was created from</param>
        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);
            #region ProjectFile
            if (reader.ReadByte() == 0)
            {
                projectFile = null;
            }
            else
            {
                projectFile = reader.ReadString();
            }
            #endregion
            #region TargetFile
            if (reader.ReadByte() == 0)
            {
                targetFile = null;
            }
            else
            {
                targetFile = reader.ReadString();
            }
            #endregion
            #region TargetName
            if (reader.ReadByte() == 0)
            {
                targetName = null;
            }
            else
            {
                targetName = reader.ReadString();
            }
            #endregion
            succeeded = reader.ReadBoolean();
        }
        #endregion

#endif
        /// <summary>
        /// Target name
        /// </summary>
        public string TargetName
        {
            get
            {
                return targetName;
            }
        }

        /// <summary>
        /// True if target built successfully, false otherwise
        /// </summary>
        public bool Succeeded
        {
            get
            {
                return succeeded;
            }
        }

        /// <summary>
        /// Project file associated with event.   
        /// </summary>
        public string ProjectFile
        {
            get
            {
                return projectFile;
            }
        }

        /// <summary>
        /// File where this target was declared.
        /// </summary>
        public string TargetFile
        {
            get
            {
                return targetFile;
            }
        }

        /// <summary>
        /// Target outputs
        /// </summary>
        public IEnumerable TargetOutputs
        {
            get
            {
                return targetOutputs;
            }

            set
            {
                targetOutputs = value;
            }
        }
    }
}
