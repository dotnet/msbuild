// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class represents a collection of all UsingTask elements in a given project file.
    /// </summary>
    /// <owner>LukaszG</owner>
    public class UsingTaskCollection : IEnumerable, ICollection
    {
        #region Properties

        private ArrayList usingTasks;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor exposed to the outside world
        /// </summary>
        /// <owner>LukaszG</owner>
        internal UsingTaskCollection()
        {
            this.usingTasks = new ArrayList();
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// IEnumerable member method for returning the enumerator
        /// </summary>
        /// <returns></returns>
        /// <owner>LukaszG</owner>
        public IEnumerator GetEnumerator()
        {
            ErrorUtilities.VerifyThrow(this.usingTasks != null, "UsingTaskCollection's ArrayList not initialized!");
            return this.usingTasks.GetEnumerator();
        }

        #endregion

        #region ICollection Members

        /// <summary>
        /// ICollection member method for copying the contents of this collection into an array
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        /// <owner>LukaszG</owner>
        public void CopyTo(Array array, int index)
        {
            ErrorUtilities.VerifyThrow(this.usingTasks != null, "UsingTaskCollection's ArrayList not initialized!");
            this.usingTasks.CopyTo(array, index);
        }

        /// <summary>
        /// ICollection member property for returning the number of items in this collection
        /// </summary>
        /// <owner>LukaszG</owner>
        public int Count
        {
            get 
            {
                ErrorUtilities.VerifyThrow(this.usingTasks != null, "UsingTaskCollection's ArrayList not initialized!");
                return this.usingTasks.Count; 
            }
        }

        /// <summary>
        /// ICollection member property for determining whether this collection is thread-safe
        /// </summary>
        /// <owner>LukaszG</owner>
        public bool IsSynchronized
        {
            get 
            {
                ErrorUtilities.VerifyThrow(this.usingTasks != null, "UsingTaskCollection's ArrayList not initialized!");
                return this.usingTasks.IsSynchronized; 
            }
        }

        /// <summary>
        /// ICollection member property for returning this collection's synchronization object
        /// </summary>
        /// <owner>LukaszG</owner>
        public object SyncRoot
        {
            get
            {
                ErrorUtilities.VerifyThrow(this.usingTasks != null, "UsingTaskCollection's ArrayList not initialized!");
                return this.usingTasks.SyncRoot;
            }
        }

        #endregion

        #region Members

        /// <summary>
        /// Removes all UsingTasks from this collection. Does not alter the parent project's XML.
        /// </summary>
        /// <owner>LukaszG</owner>
        internal void Clear()
        {
            ErrorUtilities.VerifyThrow(this.usingTasks != null, "UsingTaskCollection's ArrayList not initialized!");
            this.usingTasks.Clear();
        }

        /// <summary>
        /// Adds a new UsingTask to this collection. Does not alter the parent project's XML.
        /// </summary>
        /// <param name="usingTask"></param>
        /// <owner>LukaszG</owner>
        internal void Add(UsingTask usingTask)
        {
            ErrorUtilities.VerifyThrow(this.usingTasks != null, "UsingTaskCollection's ArrayList not initialized!");
            this.usingTasks.Add(usingTask);
        }

        /// <summary>
        /// Gets the UsingTask object with the given index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        /// <owner>LukaszG</owner>
        internal UsingTask this[int index]
        {
            get
            {
                ErrorUtilities.VerifyThrow(this.usingTasks != null, "UsingTaskCollection's ArrayList not initialized!");
                return (UsingTask) this.usingTasks[index];
            }
        }

        /// <summary>
        /// Copy the contents of this collection into a strongly typed array
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        /// <owner>LukaszG</owner>
        public void CopyTo(UsingTask[] array, int index)
        {
            ErrorUtilities.VerifyThrow(this.usingTasks != null, "UsingTaskCollection's ArrayList not initialized!");
            this.usingTasks.CopyTo(array, index);
        }

        #endregion
    }
}
