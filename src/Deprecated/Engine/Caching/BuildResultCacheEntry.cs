// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System.IO;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// A cache entry representing a build result (task items + build success/failure)
    /// </summary>
    internal class BuildResultCacheEntry : BuildItemCacheEntry
    {
        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        internal BuildResultCacheEntry()
        {
        }

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="taskItems"></param>
        /// <param name="buildResult"></param>
        internal BuildResultCacheEntry(string name, BuildItem[] buildItems, bool buildResult)
            : base(name, buildItems)
        {
            this.buildResult = buildResult;
        }

        #endregion

        #region Properties

        private bool buildResult;

        /// <summary>
        /// Build result of this target (success, failure, skipped)
        /// </summary>
        internal bool BuildResult
        {
            get { return buildResult; }
            set { buildResult = value; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the given cache entry contains equivalent contents
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        internal override bool IsEquivalent(CacheEntry other)
        {
            if ((other == null) || (other.GetType() != this.GetType()))
            {
                return false;
            }

            if (!base.IsEquivalent(other))
            {
                return false;
            }

            return this.BuildResult == ((BuildResultCacheEntry)other).BuildResult;
        }
        #endregion

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            writer.Write(buildResult);
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            buildResult = reader.ReadBoolean();
        }
        #endregion
    }
}
