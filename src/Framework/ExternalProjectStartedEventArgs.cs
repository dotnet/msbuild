// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for external project started events
    /// </summary>
    // WARNING: marking a type [Serializable] without implementing
    // ISerializable imposes a serialization contract -- it is a
    // promise to never change the type's fields i.e. the type is
    // immutable; adding new fields in the next version of the type
    // without following certain special FX guidelines, can break both
    // forward and backward compatibility
    // NOTE: Although this class has been modified and do not longer relay on [Serializable]
    // and BinaryFormatter. We have left it [Serializable] for backward compatibility reasons.
    [Serializable]
    public class ExternalProjectStartedEventArgs : CustomBuildEventArgs
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        protected ExternalProjectStartedEventArgs()
            : base()
        {
            // nothing to do here, move along.
        }

        /// <summary>
        /// Useful constructor
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword</param>
        /// <param name="senderName">name of the object sending this event</param>
        /// <param name="projectFile">project name</param>
        /// <param name="targetNames">targets we are going to build (empty indicates default targets)</param>
        public ExternalProjectStartedEventArgs(
            string message,
            string helpKeyword,
            string senderName,
            string projectFile,
            string targetNames)
            : this(message, helpKeyword, senderName, projectFile, targetNames, DateTime.UtcNow)
        {
        }

        /// <summary>
        /// Useful constructor, including the ability to set the timestamp of the event
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword</param>
        /// <param name="senderName">name of the object sending this event</param>
        /// <param name="projectFile">project name</param>
        /// <param name="targetNames">targets we are going to build (empty indicates default targets)</param>
        /// <param name="eventTimestamp">Timestamp when the event was created</param>
        public ExternalProjectStartedEventArgs(
            string message,
            string helpKeyword,
            string senderName,
            string projectFile,
            string targetNames,
            DateTime eventTimestamp)
            : base(message, helpKeyword, senderName, eventTimestamp)
        {
            this.projectFile = projectFile;
            this.targetNames = targetNames;
        }

        private string projectFile;

        /// <summary>
        /// Project name
        /// </summary>
        public string ProjectFile
        {
            get
            {
                return projectFile;
            }
        }

        private string targetNames;

        /// <summary>
        /// Targets that we will build in the project. This may mean different things for different project types,
        /// our tasks will put something like Rebuild, Clean, etc. here. This may be null if the project is being
        /// built with the default target.
        /// </summary>
        public string TargetNames
        {
            get
            {
                return targetNames;
            }
        }

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            writer.WriteOptionalString(projectFile);
            writer.WriteOptionalString(targetNames);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);
            projectFile = reader.ReadOptionalString();
            targetNames = reader.ReadOptionalString();
        }
    }
}
