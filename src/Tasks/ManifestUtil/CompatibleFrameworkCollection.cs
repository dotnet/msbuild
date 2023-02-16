// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

#nullable disable

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    [ComVisible(false)]
    public sealed class CompatibleFrameworkCollection : IEnumerable
    {
        private readonly List<CompatibleFramework> _list = new List<CompatibleFramework>();

        internal CompatibleFrameworkCollection(IEnumerable<CompatibleFramework> compatibleFrameworks)
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
