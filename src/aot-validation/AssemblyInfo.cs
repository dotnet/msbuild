// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

// The harness exercises process-global engine state - the SDK resolver and its MSBuildSDKsPath probe, the
// BuildManager (in-process builds), and the host registries. The MTP test host runs tests in parallel by
// default, which races on that global state (for example one test's temporary MSBuildSDKsPath override
// versus another test resolving an SDK concurrently). Run the harness serially so each test sees a clean,
// uncontended engine; the suite is small and fast, so this costs little.
[assembly: DoNotParallelize]
