// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    [ComVisible(false)]
    public sealed class FileAssociationCollection : IEnumerable
    {
        private readonly List<FileAssociation> _list = new List<FileAssociation>();

        internal FileAssociationCollection(FileAssociation[] fileAssociations)
        {
            if (fileAssociations == null)
            {
                return;
            }
            _list.AddRange(fileAssociations);
        }

        public FileAssociation this[int index] => _list[index];

        public void Add(FileAssociation fileAssociation)
        {
            _list.Add(fileAssociation);
        }

        public void Clear()
        {
            _list.Clear();
        }

        public int Count => _list.Count;

        public IEnumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        internal FileAssociation[] ToArray()
        {
            return _list.ToArray();
        }
    }
}
