// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Microsoft.Build.BuildEngine;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class BuildPropertyGroupProxy_Tests
    {
        [Test]
        public void BasicProxying()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            BuildProperty p1 = new BuildProperty("name1", "value1", PropertyType.EnvironmentProperty);
            BuildProperty p2 = new BuildProperty("name2", "value2", PropertyType.GlobalProperty);
            pg.SetProperty(p1);
            pg.SetProperty(p2);

            BuildPropertyGroupProxy proxy = new BuildPropertyGroupProxy(pg);

            Hashtable list = new Hashtable(StringComparer.OrdinalIgnoreCase);

            foreach (DictionaryEntry prop in proxy)
            {
                list.Add(prop.Key, prop.Value);
            }

            Assertion.Assert(list.Count == 2);
            Assertion.Assert((string)list["name1"] == "value1");
            Assertion.Assert((string)list["name2"] == "value2");
        }

        /// <summary>
        /// It is essential that there is no way to modify the original
        /// collection through the proxy.
        /// </summary>
        [Test]
        public void CantModifyThroughEnumerator()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            // Only NormalProperties are modifiable anyway
            BuildProperty p1 = new BuildProperty("name1", "value1", PropertyType.NormalProperty);
            pg.SetProperty(p1);

            BuildPropertyGroupProxy proxy = new BuildPropertyGroupProxy(pg);

            Hashtable list = new Hashtable(StringComparer.OrdinalIgnoreCase);

            // Get the one property
            foreach (DictionaryEntry prop in proxy)
            {
                list.Add(prop.Key, prop.Value);
            }

            // Change the property
            Assertion.Assert((string)list["name1"] == "value1");
            list["name1"] = "newValue";
            Assertion.Assert((string)list["name1"] == "newValue");

            // Get the property again
            list = new Hashtable(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry prop in proxy)
            {
                list.Add(prop.Key, prop.Value);
            }

            // Property value hasn't changed
            Assertion.Assert((string)list["name1"] == "value1");
        }
    }
}
