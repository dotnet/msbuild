// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Provides a collection for manifest file references.
    /// </summary>
    [ComVisible(false)]
    public sealed class FileReferenceCollection : IEnumerable
    {
        private readonly List<FileReference> _list = new List<FileReference>();

        internal FileReferenceCollection(FileReference[] array)
        {
            if (array == null)
            {
                return;
            }
            _list.AddRange(array);
        }

        /// <summary>
        /// Gets the element at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the entry to get.</param>
        /// <returns>The file reference instance.</returns>
        public FileReference this[int index] => _list[index];

        /// <summary>
        /// Adds the specified assembly reference to the collection.
        /// </summary>
        /// <param name="path">The specified file reference to add.</param>
        /// <returns>The added file reference instance.</returns>
        public FileReference Add(string path)
        {
            return Add(new FileReference(path));
        }

        /// <summary>
        /// Adds the specified assembly reference to the collection.
        /// </summary>
        /// <param name="file">The specified file reference to add.</param>
        /// <returns>The added file reference instance.</returns>
        public FileReference Add(FileReference file)
        {
            _list.Add(file);
            return file;
        }

        /// <summary>
        /// Removes all objects from the collection.
        /// </summary>
        public void Clear()
        {
            _list.Clear();
        }

        /// <summary>
        /// Gets the number of objects contained in the collection.
        /// </summary>
        public int Count => _list.Count;

        /// <summary>
        /// Finds a file reference in the collection by the specified target path.
        /// </summary>
        /// <param name="targetPath">The specified target path.</param>
        /// <returns>The found file reference.</returns>
        public FileReference FindTargetPath(string targetPath)
        {
            if (String.IsNullOrEmpty(targetPath))
            {
                return null;
            }
            foreach (FileReference f in _list)
            {
                if (String.Compare(targetPath, f.TargetPath, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return f;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns an enumerator that can iterate through the collection.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        /// <summary>
        /// Removes the specified file reference from the collection.
        /// </summary>
        /// <param name="file">The specified file reference to remove.</param>
        public void Remove(FileReference file)
        {
            _list.Remove(file);
        }

        internal FileReference[] ToArray()
        {
            return _list.ToArray();
        }
    }
}
