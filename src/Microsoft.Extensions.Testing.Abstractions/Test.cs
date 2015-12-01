// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Extensions.Testing.Abstractions
{
    public class Test
    {
        public Test()
        {
            Properties = new Dictionary<string, object>(StringComparer.Ordinal);
        }

        public string CodeFilePath { get; set; }

        public string DisplayName { get; set; }

        public string FullyQualifiedName { get; set; }

        public Guid? Id { get; set; }

        public int? LineNumber { get; set; }

        public IDictionary<string, object> Properties { get; private set; }
    }
}