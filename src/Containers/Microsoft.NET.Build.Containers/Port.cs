// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

// Explicitly lowercase to ease parsing - the incoming values are
// lowercased by spec
public enum PortType
{
    tcp,
    udp
}

public record struct Port(int Number, PortType Type);
