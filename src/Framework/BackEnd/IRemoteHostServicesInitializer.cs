// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd;

/// <summary>
///  Initializes host services from remote registrations without exposing assembly-specific initialization APIs.
/// </summary>
internal interface IRemoteHostServicesInitializer
{
    /// <summary>
    ///  Initializes a newly created host-services instance with remote host object registrations.
    /// </summary>
    /// <param name="data">The remote host object registrations.</param>
    void Initialize(HostServicesData data);
}
