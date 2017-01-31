// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Resources;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    internal sealed class MockTask : Task
    {
        internal MockTask() : base()
        {
            RegisterResources();
        }

        internal MockTask(bool registerResources)
        {
            if (registerResources)
            {
                RegisterResources();
            }
        }

        private void RegisterResources()
        {
            ResourceManager rm = new ResourceManager("Microsoft.Build.Utilities.UnitTests.strings",
                typeof(MockTask).GetTypeInfo().Assembly);
            this.Log.TaskResources = rm;
        }

        public override bool Execute()
        {
            return true;
        }
    }
}
