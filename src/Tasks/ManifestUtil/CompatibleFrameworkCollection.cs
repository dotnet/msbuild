// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    [ComVisible(false)]
    public sealed class CompatibleFrameworkCollection : IEnumerable
    {
        private readonly List<CompatibleFramework> _list = new List<CompatibleFramework>();

        internal CompatibleFrameworkCollection(CompatibleFramework[] compatibleFrameworks)
        {
            if (compatibleFrameworks == null)
            {
                return;
            }
            _list.AddRange(compatibleFrameworks);
        }

        public CompatibleFramework this[int index] => _list[index];

        public void Add(CompatibleFramework compatibleFramework)
        {
            _list.Add(compatibleFramework);
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

        internal CompatibleFramework[] ToArray()
        {
            return _list.ToArray();
        }
    }
}
