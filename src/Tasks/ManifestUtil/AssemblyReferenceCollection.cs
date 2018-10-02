// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Provides a collection for manifest assembly references.
    /// </summary>
    [ComVisible(false)]
    public sealed class AssemblyReferenceCollection : IEnumerable
    {
        private readonly List<AssemblyReference> _list = new List<AssemblyReference>();

        internal AssemblyReferenceCollection(AssemblyReference[] array)
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
        /// <returns>The assembly reference instance.</returns>
        public AssemblyReference this[int index] => _list[index];

        /// <summary>
        /// Adds the specified assembly reference to the collection.
        /// </summary>
        /// <param name="path">The specified assembly reference to add.</param>
        /// <returns>The added assembly reference instance.</returns>
        public AssemblyReference Add(string path)
        {
            return Add(new AssemblyReference(path));
        }

        /// <summary>
        /// Adds the specified assembly reference to the collection.
        /// </summary>
        /// <param name="assembly">The specified assembly reference to add.</param>
        /// <returns>The added assembly reference instance.</returns>
        public AssemblyReference Add(AssemblyReference assembly)
        {
            _list.Add(assembly);
            return assembly;
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
        /// Finds an assembly reference in the collection by simple name.
        /// </summary>
        /// <param name="name">The specified assembly simple name.</param>
        /// <returns>The found assembly reference.</returns>
        public AssemblyReference Find(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                return null;
            }
            foreach (AssemblyReference a in _list)
            {
                if (a.AssemblyIdentity != null && String.Compare(
                        name,
                        a.AssemblyIdentity.Name,
                        StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return a;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds an assembly reference in the collection by the specified assembly identity.
        /// </summary>
        /// <param name="identity">The specified assembly identity.</param>
        /// <returns>The found assembly reference.</returns>
        public AssemblyReference Find(AssemblyIdentity identity)
        {
            if (identity == null)
            {
                return null;
            }

            foreach (AssemblyReference a in _list)
            {
                AssemblyIdentity listItemIdentity = a.AssemblyIdentity;

                // if the item in our list doesn't have an identity but is a managed assembly, 
                //  we calculate it by reading the file from disk to find its identity.
                //
                // note that this is here specifically to deal with the scenario when we are being
                //  asked to find a reference to one of our sentinel assemblies which are known to 
                //  be managed assemblies. doing this ensures that our sentinel assemblies do not 
                //  show up twice in the manifest.
                //
                // we are assuming the incoming identity for the sentinel assembly really is the
                //  sentinel assembly that MS owns and emits in manifests for ClickOnce application
                //  prereq verification for .Net 2.0, 3.0 and 3.5 frameworks. otherwise, we expect
                //  the incoming identity to fail the comparison (because something like the
                //  public key token won't match if there is a user-owned reference to something
                //  that has the same name as a sentinel assembly).
                //
                // note that we only read the file from disk if the incoming identity's name matches
                //  the file-name of the item in the list to avoid unnecessarily loading every
                //  reference in the list of references
                //
                if (listItemIdentity == null &&
                    identity.Name != null &&
                    a.SourcePath != null &&
                    a.ReferenceType == AssemblyReferenceType.ManagedAssembly &&
                    String.Equals(identity.Name, System.IO.Path.GetFileNameWithoutExtension(a.SourcePath), StringComparison.OrdinalIgnoreCase))
                {
                    listItemIdentity = AssemblyIdentity.FromManagedAssembly(a.SourcePath);
                }

                if (AssemblyIdentity.IsEqual(listItemIdentity, identity))
                {
                    return a;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds an assembly reference in the collection by the specified target path.
        /// </summary>
        /// <param name="targetPath">The specified target path.</param>
        /// <returns>The found assembly reference.</returns>
        public AssemblyReference FindTargetPath(string targetPath)
        {
            if (String.IsNullOrEmpty(targetPath))
            {
                return null;
            }
            foreach (AssemblyReference a in _list)
            {
                if (String.Compare(targetPath, a.TargetPath, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return a;
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
        /// Removes the specified assembly reference from the collection.
        /// </summary>
        /// <param name="assemblyReference">The specified assembly reference to remove.</param>
        public void Remove(AssemblyReference assemblyReference)
        {
            _list.Remove(assemblyReference);
        }

        internal AssemblyReference[] ToArray()
        {
            return _list.ToArray();
        }
    }
}
