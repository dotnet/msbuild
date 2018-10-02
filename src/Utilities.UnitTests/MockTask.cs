// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Resources;
using Microsoft.Build.Utilities;

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
