// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dependency
{
    public class Alpha
    {
        public static string GetString()
        {
            return nameof(Alpha) + "." + nameof(GetString);
        }
    }
}
