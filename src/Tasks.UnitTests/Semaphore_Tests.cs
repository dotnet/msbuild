// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Shouldly;
using Microsoft.Build.UnitTests;
using System.Threading;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class Semaphore_Tests
    {
        [Fact]
        public void TestRequestingInvalidNumCores()
        {
            // assume multiproc build of 40
            new Semaphore(40, 40, "cpuCount");
            MockEngine mockEngine = new MockEngine();
            
            SemaphoreCPUTask test = new SemaphoreCPUTask();
            test.BuildEngine = mockEngine;

            // 40 - 80 = 0 cores left (claimed 40)
            test.BuildEngine7.RequestCores(test, 12312).ShouldBe(40);
            test.BuildEngine7.RequestCores(test, 10).ShouldBe(0);

            // 0 + 39 = 39 cores left
            test.BuildEngine7.ReleaseCores(test, 39);

            // 39 - 100 = 0 cores left (claimed 39)
            test.BuildEngine7.RequestCores(test, 100).ShouldBe(39);

            // 0 + 0 = 0 cores left
            test.BuildEngine7.ReleaseCores(test, 0);
            test.BuildEngine7.RequestCores(test, 2).ShouldBe(0);

            //0 + 1 = 1 cores left
            test.BuildEngine7.ReleaseCores(test, 1);

            // 1 - 2 = 0 cores left (only claimed 1)
            test.BuildEngine7.RequestCores(test, 2).ShouldBe(1);
        }

        [Fact]
        public void TestReleasingInvalidNumCores()
        {
            // assume multiproc build of 40
            new Semaphore(40, 40, "cpuCount");
            MockEngine mockEngine = new MockEngine();

            SemaphoreCPUTask test = new SemaphoreCPUTask();
            test.BuildEngine = mockEngine;

            // should still be 40 cores
            test.BuildEngine7.ReleaseCores(test, -100);
            test.BuildEngine7.RequestCores(test, 41).ShouldBe(40);

            // should be 40 cores to take
            test.BuildEngine7.ReleaseCores(test, 50);
            test.BuildEngine7.RequestCores(test, 39).ShouldBe(39);

            test.BuildEngine7.RequestCores(test, 2).ShouldBe(1);
        }
    }
}
