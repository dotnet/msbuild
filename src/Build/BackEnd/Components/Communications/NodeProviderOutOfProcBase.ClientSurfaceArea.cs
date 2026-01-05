// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Pipes;
#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
using System.Security.Principal;
#endif
using Microsoft.Build.Internal;

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Contains the shared pieces of code from NodeProviderOutOfProc
/// and NodeProviderOutOfProcTaskHost.
/// </summary>
internal abstract partial class NodeProviderOutOfProcBase
{
#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
    private static readonly WindowsIdentity s_currentWindowsIdentity = WindowsIdentity.GetCurrent();
#endif

    /// <summary>
    /// Connect to named pipe stream and ensure validate handshake and security.
    /// </summary>
    /// <remarks>
    /// Reused by MSBuild server client <see cref="Microsoft.Build.Experimental.MSBuildClient"/>.
    /// </remarks>
    internal static bool TryConnectToPipeStream(NamedPipeClientStream nodeStream, string pipeName, Handshake handshake, int timeout, out HandshakeResult result)
    {
        nodeStream.Connect(timeout);

#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
        if (NativeMethodsShared.IsWindows)
        {
            // Verify that the owner of the pipe is us.  This prevents a security hole where a remote node has
            // been faked up with ACLs that would let us attach to it.  It could then issue fake build requests back to
            // us, potentially causing us to execute builds that do harmful or unexpected things.  The pipe owner can
            // only be set to the user's own SID by a normal, unprivileged process.  The conditions where a faked up
            // remote node could set the owner to something else would also let it change owners on other objects, so
            // this would be a security flaw upstream of us.
            ValidateRemotePipeSecurityOnWindows(nodeStream);
        }
#endif

        HandshakeComponents handshakeComponents = handshake.RetrieveHandshakeComponents();
        foreach (var component in handshakeComponents.EnumerateComponents())
        {
            CommunicationsUtilities.Trace("Writing handshake part {0} ({1}) to pipe {2}", component.Key, component.Value, pipeName);
            nodeStream.WriteIntForHandshake(component.Value);
        }

        // This indicates that we have finished all the parts of our handshake; hopefully the endpoint has as well.
        nodeStream.WriteEndOfHandshakeSignal();

        CommunicationsUtilities.Trace("Reading handshake from pipe {0}", pipeName);

        if (

        nodeStream.TryReadEndOfHandshakeSignal(true,
#if NETCOREAPP2_1_OR_GREATER
        timeout,
#endif
        out HandshakeResult innerResult))
        {
            // We got a connection.
            CommunicationsUtilities.Trace("Successfully connected to pipe {0}...!", pipeName);
            result = HandshakeResult.Success(0);
            return true;
        }
        else
        {
            result = innerResult;
            return false;
        }
    }

#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
    // This code needs to be in a separate method so that we don't try (and fail) to load the Windows-only APIs when JIT-ing the code
    //  on non-Windows operating systems
    private static void ValidateRemotePipeSecurityOnWindows(NamedPipeClientStream nodeStream)
    {
        SecurityIdentifier identifier = s_currentWindowsIdentity.Owner;
#if FEATURE_PIPE_SECURITY
        PipeSecurity remoteSecurity = nodeStream.GetAccessControl();
#else
        var remoteSecurity = new PipeSecurity(nodeStream.SafePipeHandle, System.Security.AccessControl.AccessControlSections.Access |
            System.Security.AccessControl.AccessControlSections.Owner | System.Security.AccessControl.AccessControlSections.Group);
#endif
        IdentityReference remoteOwner = remoteSecurity.GetOwner(typeof(SecurityIdentifier));
        if (remoteOwner != identifier)
        {
            CommunicationsUtilities.Trace("The remote pipe owner {0} does not match {1}", remoteOwner.Value, identifier.Value);
            throw new System.UnauthorizedAccessException();
        }
    }
#endif
}
