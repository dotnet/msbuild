// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    internal class PackageCollection : IEnumerable
    {
        private ArrayList _list;
        private Hashtable _cultures;

        public PackageCollection()
        {
            _list = new ArrayList();
            _cultures = new Hashtable();
        }

        public Package Item(int index)
        {
            return (Package)_list[index];
        }

        public Package Package(string culture)
        {
            if (_cultures.Contains(culture.ToLowerInvariant()))
            {
                return (Package)_cultures[culture.ToLowerInvariant()];
            }

            return null;
        }

        public int Count
        {
            get { return _list.Count; }
        }

        internal void Add(Package package)
        {
            if (!_cultures.Contains(package.Culture.ToLowerInvariant()))
            {
                _list.Add(package);
                _cultures.Add(package.Culture.ToLowerInvariant(), package);
            }
            else
            {
                Debug.Fail("Package with culture " + package.Culture + " has already been added.");
            }
        }

        public IEnumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }
    }
}
