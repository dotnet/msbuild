﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

#nullable disable

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    internal class PackageCollection : IEnumerable
    {
        private readonly List<Package> _list = new List<Package>();
        private readonly Dictionary<string, Package> _cultures = new Dictionary<string, Package>(StringComparer.OrdinalIgnoreCase);

        public Package Item(int index)
        {
            return _list[index];
        }

        public Package Package(string culture)
        {
            return _cultures.TryGetValue(culture, out Package package) ? package : null;
        }

        public int Count => _list.Count;

        internal void Add(Package package)
        {
            if (!_cultures.ContainsKey(package.Culture))
            {
                _list.Add(package);
                _cultures.Add(package.Culture, package);
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
