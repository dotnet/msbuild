// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

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
