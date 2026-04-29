// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Internal;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd;

/// <summary>
/// Regression tests for https://github.com/dotnet/msbuild/issues/13594.
///
/// Two MSBuild invocations launched with different temporary directories
/// (e.g. different <c>TMP</c>/<c>TEMP</c>/<c>TMPDIR</c> environment variables)
/// must NOT reuse each other's worker nodes. Otherwise build A finishing and
/// clearing its temp folder breaks the still-running build B which expected its
/// own folder to remain available on the shared node.
///
/// The contract validated here: the <see cref="Handshake"/> salt incorporates
/// the per-process effective temp directory for non-TaskHost paths, so the
/// handshake key differs when the temp directory differs and the node-reuse
/// lookup will refuse to bind to a foreign node.
///
/// TaskHost paths are intentionally exempted from the salt input. By default
/// task-host processes do not survive past a single build (cross-build TaskHost
/// reuse requires the rarely-set <c>MSBUILDREUSETASKHOSTNODES=1</c> opt-in and
/// crashed parents kill sidecars via the pipe-link monitor), so the bug from
/// #13594 cannot manifest there in normal use. Excluding TaskHost paths from
/// the salt also avoids introducing a handshake-protocol mismatch on the NET
/// TaskHost path (parent .NET Framework MSBuild ↔ child shipped from a
/// separately-released .NET SDK), which intentionally tolerates parent/child
/// version skew.
/// </summary>
public sealed class HandshakeTempDir_Tests
{
    private readonly ITestOutputHelper _output;

    public HandshakeTempDir_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Handshake_DifferentTempDirectory_ProducesDifferentKey()
    {
        // Use distinct, fully qualified directories so Path.GetTempPath() returns
        // a different normalized value in each TestEnvironment scope.
        string keyA;
        string keyB;

        using (TestEnvironment env = TestEnvironment.Create(_output))
        {
            TransientTestFolder folder = env.CreateFolder(createFolder: true);
            env.SetTempPath(folder.Path);
            keyA = new Handshake(HandshakeOptions.NodeReuse).GetKey();
        }

        using (TestEnvironment env = TestEnvironment.Create(_output))
        {
            TransientTestFolder folder = env.CreateFolder(createFolder: true);
            env.SetTempPath(folder.Path);
            keyB = new Handshake(HandshakeOptions.NodeReuse).GetKey();
        }

        keyA.ShouldNotBe(keyB,
            "Two MSBuild invocations using different temp directories must produce " +
            "distinct handshake keys so they do not reuse each other's nodes (issue #13594).");
    }

    [Fact]
    public void Handshake_SameTempDirectory_ProducesSameKey()
    {
        // Sanity check: the temp dir contribution is deterministic, so two handshakes
        // built under the same temp dir still match (otherwise legitimate node reuse breaks).
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder folder = env.CreateFolder(createFolder: true);
        env.SetTempPath(folder.Path);

        string key1 = new Handshake(HandshakeOptions.NodeReuse).GetKey();
        string key2 = new Handshake(HandshakeOptions.NodeReuse).GetKey();

        key1.ShouldBe(key2,
            "Identical environments must still produce identical handshake keys; " +
            "otherwise node reuse would never succeed.");
    }

    [Fact]
    public void ServerNodeHandshake_DifferentTempDirectory_ProducesDifferentHash()
    {
        // MSBuildServer pipe names are derived from ServerNodeHandshake.ComputeHash();
        // changing TMP must yield a different server pipe so different temp envs do not
        // collide on the same long-lived server process.
        string hashA;
        string hashB;

        using (TestEnvironment env = TestEnvironment.Create(_output))
        {
            TransientTestFolder folder = env.CreateFolder(createFolder: true);
            env.SetTempPath(folder.Path);
            hashA = new ServerNodeHandshake(HandshakeOptions.None).ComputeHash();
        }

        using (TestEnvironment env = TestEnvironment.Create(_output))
        {
            TransientTestFolder folder = env.CreateFolder(createFolder: true);
            env.SetTempPath(folder.Path);
            hashB = new ServerNodeHandshake(HandshakeOptions.None).ComputeHash();
        }

        hashA.ShouldNotBe(hashB,
            "MSBuildServer pipe-name hashes must differ when the temp directory differs " +
            "so two builds with different TMP cannot share the same server (issue #13594).");
    }

    /// <summary>
    /// TaskHost handshakes (here: NET TaskHost) intentionally do NOT incorporate the temp
    /// directory in their salt. This pins that exemption so a future change cannot accidentally
    /// re-introduce a handshake-protocol mismatch on the NET TaskHost path, which is the only
    /// handshake that tolerates parent (VS) / child (SDK) version skew across release trains.
    ///
    /// The exemption is also safe with respect to the original #13594 bug because cross-build
    /// TaskHost reuse is gated on the off-by-default <c>MSBUILDREUSETASKHOSTNODES=1</c> escape
    /// hatch (see <c>Traits.EscapeHatches.ReuseTaskHostNodes</c>) and on the parent surviving
    /// long enough to send <c>NodeBuildComplete(PrepareForReuse=true)</c>; if the parent
    /// crashes, the sidecar shuts itself down via <c>OnLinkStatusChanged(ConnectionFailed)</c>.
    /// </summary>
    [Fact]
    public void Handshake_NetTaskHost_DifferentTempDirectory_ProducesSameKey()
    {
        const HandshakeOptions netTaskHostOptions = HandshakeOptions.NET | HandshakeOptions.TaskHost;
        // A toolsDirectory must be supplied for NET TaskHost handshakes; the actual path
        // is irrelevant as long as it is identical across the two constructions.
        string toolsDirectory = Path.Combine(Path.GetTempPath(), "fixed_tools_dir_for_test");

        string keyA;
        string keyB;

        using (TestEnvironment env = TestEnvironment.Create(_output))
        {
            TransientTestFolder folder = env.CreateFolder(createFolder: true);
            env.SetTempPath(folder.Path);
            keyA = new Handshake(netTaskHostOptions, toolsDirectory).GetKey();
        }

        using (TestEnvironment env = TestEnvironment.Create(_output))
        {
            TransientTestFolder folder = env.CreateFolder(createFolder: true);
            env.SetTempPath(folder.Path);
            keyB = new Handshake(netTaskHostOptions, toolsDirectory).GetKey();
        }

        keyA.ShouldBe(keyB,
            "NET TaskHost handshake salt must NOT incorporate Path.GetTempPath(): the NET " +
            "TaskHost path tolerates VS-vs-SDK version skew and an unsynchronized salt input " +
            "would introduce a protocol mismatch. The bug from #13594 cannot manifest here " +
            "in practice anyway because cross-build TaskHost reuse requires the off-by-default " +
            "MSBUILDREUSETASKHOSTNODES=1 opt-in.");
    }
}
