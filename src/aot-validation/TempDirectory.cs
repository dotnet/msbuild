// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.AotValidation;

/// <summary>
/// A disposable, uniquely-named temporary directory that is recursively deleted on <see cref="Dispose"/>.
///
/// This mirrors the MSBuild test infrastructure's <c>TransientTestFolder</c>
/// (src/UnitTests.Shared/TestEnvironment.cs), including its guard against deleting an obviously-wrong
/// path. The harness is deliberately isolated from the test-infrastructure assemblies, so this is a
/// small self-contained equivalent rather than a reference to that type.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "msb-aot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// The absolute path of the created directory.
    /// </summary>
    public string Path { get; }

    public void Dispose()
    {
        // Basic safety checks before a recursive delete (mirrors TransientTestFolder.Revert): never delete
        // a non-rooted path or the temp root itself.
        if (string.IsNullOrEmpty(Path)
            || !System.IO.Path.IsPathRooted(Path)
            || System.IO.Path.GetFullPath(Path) == System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()))
        {
            return;
        }

        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup: a transiently locked file under the temp directory must not fail the test.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
