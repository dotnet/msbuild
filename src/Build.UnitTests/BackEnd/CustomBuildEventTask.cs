// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class CustomBuildEventTask : Task
    {
        public override bool Execute()
        {
            MyCustomBuildEventArgs customBuildEvent = new() { RawMessage = "A message from MyCustomBuildEventArgs" };
            BuildEngine.LogCustomEvent(customBuildEvent);

            return true;
        }

        [Serializable]
        public sealed class MyCustomBuildEventArgs : CustomBuildEventArgs { }
    }
}
