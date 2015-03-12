// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Resources;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Tasks;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class ToolTaskExtensionTests
    {
        /// <summary>
        /// Verify that the resources in ToolTask/derived classes work correctly (are accessible with correct resource managers)
        /// With moving ToolTask into Utilities, tasks inheriting from it now have to deal with 3 (THREE!) resource streams,
        /// which has a lot of potential for breaking. Make sure that tasks can access all of them using the correct logger helpers.
        /// </summary>
        [TestMethod]
        public void TestResourceAccess()
        {
            Csc t = new Csc();
            MockEngine engine = new MockEngine();

            t.BuildEngine = engine;

            // No need to actually check the outputted strings. We only care that this doesn't throw, which means that 
            // the resource strings were reachable.

            // Normal CSC messages first, from private XMakeTasks resources. They should be accessible with t.Log
            t.Log.LogErrorWithCodeFromResources("Csc.AssemblyAliasContainsIllegalCharacters", "PlanetSide", "Knights of the Old Republic");
            t.Log.LogWarningWithCodeFromResources("Csc.InvalidParameter");
            t.Log.LogMessageFromResources("Vbc.ParameterHasInvalidValue", "Rome Total War", "Need for Speed Underground");

            // Now shared messages. Should be accessible with the private LogShared property
            PropertyInfo logShared = typeof(ToolTask).GetProperty("LogShared", BindingFlags.Instance | BindingFlags.NonPublic);
            TaskLoggingHelper log = (TaskLoggingHelper)logShared.GetValue(t, null);
            log.LogWarningWithCodeFromResources("Shared.FailedCreatingTempFile", "Gothic II");
            log.LogMessageFromResources("Shared.CannotConvertStringToBool", "foo");

            // Now private Utilities messages. Should be accessible with the private LogPrivate property
            PropertyInfo logPrivate = typeof(ToolTask).GetProperty("LogPrivate", BindingFlags.Instance | BindingFlags.NonPublic);
            log = (TaskLoggingHelper)logPrivate.GetValue(t, null);
            log.LogErrorWithCodeFromResources("ToolTask.CommandTooLong", "Painkiller");
            log.LogWarningWithCodeFromResources("ToolTask.CouldNotStartToolExecutable", "Fallout Tactics", "Fallout 2");
            log.LogMessageFromResources("ToolsLocationHelper.InvalidRedistFile", "Deus Ex", "Fallout");
        }

        /// <summary>
        /// Verify that the above method actually tests something, that is make sure that non-existent resources throw
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ResourceAccessSanityCheck()
        {
            Csc t = new Csc();
            MockEngine engine = new MockEngine();

            t.BuildEngine = engine;
            t.Log.LogErrorFromResources("Beyond Good and Evil");
        }

        /// <summary>
        /// Retrieve a non-existent value but ask for a default.
        /// </summary>
        [TestMethod]
        public void GetNonExistentBoolWithDefault()
        {
            Csc t = new Csc();
            Assert.AreEqual(5, t.GetIntParameterWithDefault("Key", 5));
        }

        /// <summary>
        /// Retrieve a value that exists, but ask for a default. We expect the
        /// real value to win.
        /// </summary>
        [TestMethod]
        public void GetBoolWithDefault()
        {
            Csc t = new Csc();
            t.Bag["Key"] = true;

            Assert.AreEqual(true, t.GetBoolParameterWithDefault("Key", false));
        }

        /// <summary>
        /// Retrieve a value that exists, but ask for a default. We expect the
        /// real value to win.
        /// </summary>
        [TestMethod]
        public void GetIntWithDefault()
        {
            Csc t = new Csc();
            t.Bag["Key"] = 5;

            Assert.AreEqual(5, t.GetIntParameterWithDefault("Key", 9));
        }
    }
}
