// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System.Collections;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// An enumerable wrapper for a BuildPropertyGroup that allows read-only
    /// access to the properties.
    /// </summary>
    /// <remarks>
    /// This class is designed to be passed to loggers.
    /// The expense of copying properties is only incurred if and when
    /// a logger chooses to enumerate over it.
    /// </remarks>
    /// <owner>danmose</owner>
    internal class BuildPropertyGroupProxy : IEnumerable
    {
        // Property group that this proxies
        private BuildPropertyGroup backingPropertyGroup;

        private BuildPropertyGroupProxy()
        {
            // Do nothing
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="propertyGroup">Property group this class should proxy</param>
        public BuildPropertyGroupProxy(BuildPropertyGroup propertyGroup)
        {
            this.backingPropertyGroup = propertyGroup;
        }

        /// <summary>
        /// Returns an enumerator that provides copies of the property name-value pairs
        /// in the backing property group.
        /// </summary>
        /// <returns></returns>
        public IEnumerator GetEnumerator()
        {
            foreach (BuildProperty prop in backingPropertyGroup)
            {
                // No need to clone the property; just return copies of the name and value
                yield return new DictionaryEntry(prop.Name, prop.FinalValue);
            }
        }
    }
}
