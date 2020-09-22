// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class wraps BuildEventArgs and used for sending BuildEventArgs across the node boundary
    /// </summary>
    internal class NodeLoggingEvent
    {
        // Enumeration of logging event types, this is used to recreate the correct type on deserialization
        private enum LoggingEventType
        {
            CustomEvent = 0,
            BuildErrorEvent = 1,
            BuildFinishedEvent = 2,
            BuildMessageEvent = 3,
            BuildStartedEvent = 4,
            BuildWarningEvent = 5,
            ProjectFinishedEvent = 6,
            ProjectStartedEvent = 7,
            TargetStartedEvent = 8,
            TargetFinishedEvent = 9,
            TaskStartedEvent = 10,
            TaskFinishedEvent = 11,
            TaskCommandLineEvent = 12
        }

        #region Constructors
        /// <summary>
        /// This new constructor is required for custom serialization
        /// </summary>
        internal NodeLoggingEvent()
        {
        }
        /// <summary>
        /// Create an instance of this class wrapping given BuildEventArgs
        /// </summary>
        internal NodeLoggingEvent(BuildEventArgs eventToLog)
        {
           this.e = eventToLog;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The BuildEventArgs wrapped by this class
        /// </summary>
        internal BuildEventArgs BuildEvent
        {
            get
            {
                return this.e;
            }
        }

        /// <summary>
        /// The ID of the central logger to which this event should be forwarded. By default 
        /// all regular non-forwarded events are sent to all loggers registered on the parent.
        /// </summary>
        virtual internal int LoggerId
        {
            get
            {
                return 0;
            }
        }

        #endregion

        #region CustomSerializationToStream

        /// <summary>
        /// Converts a BuildEventArg into its associated enumeration Id.
        /// Any event which is a derrived event not in the predefined list will be
        /// considered a custom event and use .net serialization
        /// </summary>
        private LoggingEventType GetLoggingEventId(BuildEventArgs eventArg)
        {
            Type eventType = eventArg.GetType();
            if (eventType == typeof(BuildMessageEventArgs))
            {
                return LoggingEventType.BuildMessageEvent;
            }
            else if (eventType == typeof(TaskCommandLineEventArgs))
            {
                return LoggingEventType.TaskCommandLineEvent;
            }
            else if (eventType == typeof(ProjectFinishedEventArgs))
            {
                return LoggingEventType.ProjectFinishedEvent;
            }
            else if (eventType == typeof(ProjectStartedEventArgs))
            {
                return LoggingEventType.ProjectStartedEvent;
            }
            else if (eventType == typeof(TargetStartedEventArgs))
            {
                return LoggingEventType.TargetStartedEvent;
            }
            else if (eventType == typeof(TargetFinishedEventArgs))
            {
                return LoggingEventType.TargetFinishedEvent;
            }
            else if (eventType == typeof(TaskStartedEventArgs))
            {
                return LoggingEventType.TaskStartedEvent;
            }
            else if (eventType == typeof(TaskFinishedEventArgs))
            {
                return LoggingEventType.TaskFinishedEvent;
            }
            else if (eventType == typeof(BuildFinishedEventArgs))
            {
                return LoggingEventType.BuildFinishedEvent;
            }
            else if (eventType == typeof(BuildStartedEventArgs))
            {
                return LoggingEventType.BuildStartedEvent;
            }
            else if (eventType == typeof(BuildWarningEventArgs))
            {
                return LoggingEventType.BuildWarningEvent;
            }
            if (eventType == typeof(BuildErrorEventArgs))
            {
                return LoggingEventType.BuildErrorEvent;
            }
            else
            {
                return LoggingEventType.CustomEvent;
            }
        }

        /// <summary>
        /// Takes in a id (LoggingEventType as an int) and creates the correct specific logging class
        /// </summary>
        private BuildEventArgs GetBuildEventArgFromId(LoggingEventType id)
        {
            switch (id)
            {
                case LoggingEventType.BuildErrorEvent:
                    return new BuildErrorEventArgs(null, null, null, -1, -1, -1, -1, null, null, null);
                case LoggingEventType.BuildFinishedEvent:
                    return new BuildFinishedEventArgs(null, null, false);
                case LoggingEventType.BuildMessageEvent:
                    return new BuildMessageEventArgs(null, null, null, MessageImportance.Normal);
                case LoggingEventType.BuildStartedEvent:
                    return new BuildStartedEventArgs(null, null);
                case LoggingEventType.BuildWarningEvent:
                    return new BuildWarningEventArgs(null, null, null, -1, -1, -1, -1,null,null,null);
                case LoggingEventType.ProjectFinishedEvent:
                    return new ProjectFinishedEventArgs(null, null, null, false);
                case LoggingEventType.ProjectStartedEvent:
                    return new ProjectStartedEventArgs(-1, null, null, null, null, null, null, null);
                case LoggingEventType.TargetStartedEvent:
                    return new TargetStartedEventArgs(null, null, null, null, null);
                case LoggingEventType.TargetFinishedEvent:
                    return new TargetFinishedEventArgs(null, null, null, null, null, false);
                case LoggingEventType.TaskStartedEvent:
                    return new TaskStartedEventArgs(null, null, null, null, null);
                case LoggingEventType.TaskFinishedEvent:
                    return new TaskFinishedEventArgs(null, null, null, null, null, false);
                case LoggingEventType.TaskCommandLineEvent:
                    return new TaskCommandLineEventArgs(null, null, MessageImportance.Normal);
                default:
                    ErrorUtilities.VerifyThrow(false, "Should not get to the default of getBuildEventArgFromId ID:" + id);
                    return null;
            }
        }

        internal virtual void WriteToStream(BinaryWriter writer, Hashtable loggingTypeCache)
        {
            LoggingEventType id = GetLoggingEventId(e);
            writer.Write((byte)id);

            if (id != LoggingEventType.CustomEvent)
            {
                if (!loggingTypeCache.ContainsKey(id))
                {
                    Type eventType = e.GetType();
                    MethodInfo methodInfo = eventType.GetMethod("WriteToStream", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod);
                    loggingTypeCache.Add(id, methodInfo);
                }
                if (loggingTypeCache[id] != null)
                {
                    ((MethodInfo)loggingTypeCache[id]).Invoke(e, new object[] { writer });
                }
                else
                {
                    // The customer serialization methods are not availiable, default to .net serialization
                    writer.BaseStream.Position--;
                    writer.Write((byte)0);
                    binaryFormatter.Serialize(writer.BaseStream, e);
                }
            }
            else
            {
                writer.Write(e.GetType().Assembly.Location);
                binaryFormatter.Serialize(writer.BaseStream, e);
            }
        }

        internal virtual void CreateFromStream(BinaryReader reader, Hashtable loggingTypeCache)
        {
            LoggingEventType id = (LoggingEventType)reader.ReadByte();
            if (LoggingEventType.CustomEvent != id)
            {
                e = GetBuildEventArgFromId(id);
                if (!loggingTypeCache.Contains(id))
                {
                    Type eventType = e.GetType();
                    MethodInfo methodInfo = eventType.GetMethod("CreateFromStream", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod);
                    loggingTypeCache.Add(id, methodInfo);
                }

                int packetVersion = (Environment.Version.Major * 10) + Environment.Version.Minor;
                ((MethodInfo)loggingTypeCache[id]).Invoke(e, new object[] { reader, packetVersion });
            }
            else
            {
                string fileLocation = reader.ReadString();
                bool resolveAssembly = false;
                lock (lockObject)
                {
                    if (customEventsLoaded == null)
                    {
                        customEventsLoaded = new Hashtable(StringComparer.OrdinalIgnoreCase);
                        resolveAssembly = true;
                    }
                    else
                    {
                        if (!customEventsLoaded.Contains(fileLocation))
                        {
                            resolveAssembly = true;
                        }
                    }
                    // If we are to resolve the assembly add it to the list of assemblies resolved
                    if (resolveAssembly)
                    {
                        customEventsLoaded.Add(fileLocation, null);
                    }
                }
                    if (resolveAssembly)
                    {
                        resolver = new TaskEngineAssemblyResolver();
                        resolver.InstallHandler();
                        resolver.Initialize(fileLocation);
                    }
                    try
                    {
                        e = (BuildEventArgs)binaryFormatter.Deserialize(reader.BaseStream);
                    }
                    finally
                    {
                        if (resolveAssembly)
                        {
                            resolver.RemoveHandler();
                            resolver = null;
                        }
                    }
            }
        }
        #endregion

        #region Data
        // The actual event wrapped by this container class
        private BuildEventArgs e;
        // Just need one of each per app domain
        private static BinaryFormatter binaryFormatter = new BinaryFormatter();
        private TaskEngineAssemblyResolver resolver;
        private static Hashtable customEventsLoaded;
        private static object lockObject = new object();
        #endregion
    }

    /// <summary>
    /// This class is used to associate wrapped BuildEventArgs with a loggerId which
    /// identifies which central logger this event should be delivered to.
    /// </summary>
    internal class NodeLoggingEventWithLoggerId : NodeLoggingEvent
    {
        #region Constructors

        internal NodeLoggingEventWithLoggerId()
        {
        }
        /// <summary>
        /// Create a wrapper for a given event associated with a particular loggerId
        /// </summary>
        internal NodeLoggingEventWithLoggerId(BuildEventArgs eventToLog, int loggerId)
            :base(eventToLog)
        {
            this.loggerId = loggerId;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The ID of the central logger to which this event should be forwarded
        /// </summary>
        internal override int LoggerId
        {
            get
            {
                return loggerId;
            }
        }
        #endregion

        #region CustomSerializationToStream

        internal override void WriteToStream(BinaryWriter writer, Hashtable loggingTypeCache)
        {
            base.WriteToStream(writer, loggingTypeCache);
            writer.Write((Int32)loggerId);
        }

        internal override void CreateFromStream(BinaryReader reader, Hashtable loggingTypeCache)
        {
            base.CreateFromStream(reader, loggingTypeCache);
            loggerId = reader.ReadInt32();
        }
        #endregion

        #region Data
        // The id of the central logger to which this event should be forwarded
        private int loggerId;
        #endregion
    }
}
