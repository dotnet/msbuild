// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.NET.TestFramework
{
    public class TestConstants
    {
        //  Intended to be used as an argument to XUnit's CollectionAttribute to prevent tests which change static telemetry state from interfering with each other
        public const string UsesStaticTelemetryState = nameof(UsesStaticTelemetryState);
    }
}
