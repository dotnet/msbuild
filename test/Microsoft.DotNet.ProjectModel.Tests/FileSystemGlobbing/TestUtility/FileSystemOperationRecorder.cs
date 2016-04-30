// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Tests.TestUtility
{
    internal class FileSystemOperationRecorder
    {
        public IList<IDictionary<string, object>> Records = new List<IDictionary<string, object>>();

        public void Add(string action, object values)
        {
            var record = new Dictionary<string, object>
            {
                {"action", action }
            };

            foreach (var p in values.GetType().GetTypeInfo().DeclaredProperties)
            {
                record[p.Name] = p.GetValue(values);
            }

            Records.Add(record);
        }
    }
}
