// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Reporting
{
    public sealed class Build
    {
        public string Repo { get; set; }

        public string Branch { get; set; }

        public string Architecture { get; set; }

        public string Locale { get; set; }

        public string GitHash { get; set; }

        public string BuildName { get; set; }

        public DateTime TimeStamp { get; set; }
    }
}
