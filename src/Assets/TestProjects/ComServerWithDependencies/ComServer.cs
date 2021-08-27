// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace COMServer
{
    [ComVisible(true)]
    [Guid("17B6329E-B025-4FC7-A854-97D34600C5A6")]
    public class Class1
    {
        public bool IsValidJson(string json)
        {
            try
            {
                JObject.Parse(json);
                return true;
            }
            catch (JsonReaderException)
            {
                return false;
            }
        }
    }
}
