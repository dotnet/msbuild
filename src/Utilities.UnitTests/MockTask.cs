// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Resources;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    internal sealed class MockTask : Task
    {
        internal MockTask()
            : this(true)
        {
        }

        internal MockTask(bool registerResources)
        {
            if (registerResources)
            {
                RegisterResources();
            }
        }

        private void RegisterResources() => Log.TaskResources = new ResourceManager("Microsoft.Build.Utilities.UnitTests.strings", typeof(MockTask).GetTypeInfo().Assembly);

        public override bool Execute() => true;
    }
}
