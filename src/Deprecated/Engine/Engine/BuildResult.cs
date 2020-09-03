// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Text;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is a container for build results travelling from the engine to the node
    /// </summary>
    internal class BuildResult
    {
        #region Constructions

        internal BuildResult()
        {
            // used for serialization
        }

        /// <summary>
        /// Constructor
        /// </summary>
        internal BuildResult
        (
            IDictionary outputsByTarget, Hashtable resultByTarget, bool evaluationResult, 
            int handleId, int requestId, int projectId, bool useResultCache, 
            string defaultTargets, string initialTargets,
            int totalTime, int engineTime, int taskTime
        )
        {
            this.outputsByTarget = outputsByTarget;
            this.resultByTarget = resultByTarget;
            this.handleId = handleId;
            this.requestId = requestId;
            this.projectId = projectId;
            this.flags = (byte)((evaluationResult ? 1 : 0 ) | (useResultCache ? 2 : 0));
            this.defaultTargets = defaultTargets;
            this.initialTargets = initialTargets;
            this.totalTime = totalTime;
            this.engineTime = engineTime;
            this.taskTime = taskTime;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        internal BuildResult
            (BuildResult buildResultToCopy, bool deepCopy)
        {
            ErrorUtilities.VerifyThrowArgumentNull(buildResultToCopy, "Cannot have a null build result passed in");
            this.flags = buildResultToCopy.flags;
            this.handleId = buildResultToCopy.handleId;
            this.requestId = buildResultToCopy.requestId;
            this.projectId = buildResultToCopy.projectId;
            this.outputsByTarget = new Hashtable();
            this.defaultTargets = buildResultToCopy.defaultTargets;
            this.initialTargets = buildResultToCopy.initialTargets;
            this.resultByTarget = new Hashtable(buildResultToCopy.resultByTarget, StringComparer.OrdinalIgnoreCase);

            if (buildResultToCopy.outputsByTarget == null)
            {
                return;
            }

            if (deepCopy)
            {
                // Copy all the old data
                foreach (DictionaryEntry entry in buildResultToCopy.outputsByTarget)
                {
                    // Make deep copies of all the target outputs before
                    // passing them back
                    BuildItem[] originalArray = (BuildItem[])entry.Value;
                    BuildItem[] itemArray = new BuildItem[originalArray.Length];
                    for (int i = 0; i < originalArray.Length; i++)
                    {
                        if (!originalArray[i].IsUninitializedItem)
                        {
                            itemArray[i] = originalArray[i].Clone();
                            itemArray[i].CloneVirtualMetadata();
                        }
                        else
                        {
                            itemArray[i] = new BuildItem(null, originalArray[i].FinalItemSpecEscaped);
                        }
                    }
                    
                    this.outputsByTarget.Add(entry.Key, itemArray);
                }
            }
            else
            {
                // Create a new hashtable but point at the same data
                foreach (DictionaryEntry entry in buildResultToCopy.outputsByTarget)
                {
                    this.outputsByTarget.Add(entry.Key, entry.Value);
                }
            }
        }

        #endregion

        #region Properties
        internal IDictionary OutputsByTarget
        {
            get
            {
                return this.outputsByTarget;
            }
        }

        internal Hashtable ResultByTarget
        {
            get
            {
                return this.resultByTarget;
            }
        }

        internal bool EvaluationResult
        {
            get
            {
                return (this.flags & 1) == 0 ? false : true;
            }
        }

        internal int HandleId
        {
            get
            {
                return this.handleId;
            }
            set
            {
                this.handleId = value;
            }
        }

        internal int RequestId
        {
            get
            {
                return this.requestId;
            }
            set
            {
                this.requestId = value;
            }
        }

        internal int ProjectId
        {
            get
            {
                return this.projectId;
            }
        }

        internal bool UseResultCache
        {
            get
            {
                return (this.flags & 2) == 0 ? false : true;
            }
        }

        internal string DefaultTargets
        {
            get
            {
                return this.defaultTargets;
            }
        }

        internal string InitialTargets
        {
            get
            {
                return this.initialTargets;
            }
        }

        /// <summary>
        /// Total time spent on the build request measured from the time it is received to the time build
        /// result is created. This number will be 0 if the result was in the cache.
        /// </summary>
        internal int TotalTime
        {
            get
            {
                return this.totalTime;
            }
        }

        /// <summary>
        /// Total time spent in the engine working on the build request. This number will be 0 if the result
        /// was in the cache.
        /// </summary>
        internal int EngineTime
        {
            get
            {
                return this.engineTime;
            }
        }

        /// <summary>
        /// Total time spent in the running tasks for the build request. This number will be 0 if the result
        /// was in the cache.
        /// </summary>
        internal int TaskTime
        {
            get
            {
                return this.taskTime;
            }
        }
        #endregion

        #region Methods

        /// <summary>
        /// BuildItems are passed around internally, including across the wire. Before passing these
        /// to tasks, they need to be converted into TaskItems using this method.
        /// </summary>
        internal void ConvertToTaskItems()
        {
            // If outputsByTarget was null then we dont have to re-create anything as nothing was passed over
            if (outputsByTarget != null)
            {
                string[] keys = new string[outputsByTarget.Count];
                outputsByTarget.Keys.CopyTo(keys, 0);
                for (int key_index = 0; key_index < keys.Length; key_index++)
                {
                    object key = keys[key_index];
                    BuildItem[] originalArray = (BuildItem[])outputsByTarget[key];
                    outputsByTarget[key] = BuildItem.ConvertBuildItemArrayToTaskItems(originalArray);
                }
            }
        }
        #endregion

        #region Data
        private IDictionary outputsByTarget;
        private Hashtable resultByTarget;
        private byte flags;
        private int handleId;
        private int requestId;
        private int projectId;
        private string defaultTargets;
        private string initialTargets;

        // Timing data related to the execution of the request
        private int totalTime;
        private int engineTime;
        private int taskTime;

        #endregion

        #region CustomSerializationToStream
        internal void WriteToStream(BinaryWriter writer)
        {
            ErrorUtilities.VerifyThrow(resultByTarget != null, "resultsByTarget cannot be null");
            #region OutputsByTarget
            if (outputsByTarget == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)outputsByTarget.Count);
                foreach (string key in outputsByTarget.Keys)
                {
                    writer.Write(key);
                    if (outputsByTarget[key] == null)
                    {
                        writer.Write((byte)0);
                    }
                    else
                    {
                        writer.Write((byte)1);
                        BuildItem[] items = ((BuildItem[])outputsByTarget[key]);
                        writer.Write((Int32)items.Length);
                        foreach (BuildItem item in items)
                        {
                            if (item == null)
                            {
                                writer.Write((byte)0);
                            }
                            writer.Write((byte)1);
                            item.WriteToStream(writer);
                        }
                    }
                }
            }
            #endregion
            #region ResultByTarget
            //Write Number of HashItems
            writer.Write((Int32)resultByTarget.Count);
            foreach (string key in resultByTarget.Keys)
            {
                writer.Write(key);
                writer.Write((Int32)resultByTarget[key]);
            }
            #endregion
            writer.Write((byte)flags);
            writer.Write((Int32)handleId);
            writer.Write((Int32)requestId);
            writer.Write((Int32)projectId);
            #region DefaultTargets
            if (defaultTargets == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(defaultTargets);
            }
            #endregion
            #region InitialTargets
            if (initialTargets == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(initialTargets);
            }
            #endregion
            #region Timing data
            writer.Write((Int32)totalTime);
            writer.Write((Int32)engineTime);
            writer.Write((Int32)taskTime);
            #endregion
        }

        internal static BuildResult CreateFromStream(BinaryReader reader)
        {
            BuildResult buildResult = new BuildResult();
            #region OutputsByTarget
            if (reader.ReadByte() == 0)
            {
                buildResult.outputsByTarget = null;
            }
            else
            {
                int numberOfElements = reader.ReadInt32();
                buildResult.outputsByTarget = new Hashtable(numberOfElements, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < numberOfElements; i++)
                {
                    string key = reader.ReadString();
                    BuildItem[] value = null;
                    if (reader.ReadByte() != 0)
                    {
                        int sizeOfArray = reader.ReadInt32();
                        value = new BuildItem[sizeOfArray];
                        for (int j = 0; j < sizeOfArray; j++)
                        {
                            BuildItem itemToAdd = null;
                            if (reader.ReadByte() != 0)
                            {
                                itemToAdd = new BuildItem(null, string.Empty);
                                itemToAdd.CreateFromStream(reader);
                            }
                            value[j] = itemToAdd;
                        }
                    }
                    buildResult.outputsByTarget.Add(key, value);
                }
            }
            #endregion
            #region ResultsByTarget
            //Write Number of HashItems
            int numberOfHashKeyValuePairs = reader.ReadInt32();
            buildResult.resultByTarget = new Hashtable(numberOfHashKeyValuePairs, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < numberOfHashKeyValuePairs; i++)
            {
                string key = reader.ReadString();
                int value = reader.ReadInt32();
                buildResult.resultByTarget.Add(key, (Target.BuildState)value);
            }
            #endregion
            buildResult.flags = reader.ReadByte();
            buildResult.handleId = reader.ReadInt32();
            buildResult.requestId = reader.ReadInt32();
            buildResult.projectId = reader.ReadInt32();
            #region DefaultTargets
            if (reader.ReadByte() == 0)
            {
                buildResult.defaultTargets = null;
            }
            else
            {
                buildResult.defaultTargets = reader.ReadString();
            }
            #endregion
            #region InitialTargets
            if (reader.ReadByte() == 0)
            {
                buildResult.initialTargets = null;
            }
            else
            {
                buildResult.initialTargets = reader.ReadString();
            }
            #endregion
            #region Timing data
            buildResult.totalTime = reader.ReadInt32();
            buildResult.engineTime = reader.ReadInt32();
            buildResult.taskTime = reader.ReadInt32();
            #endregion
            return buildResult;
        }
        #endregion
    }
}
