// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class DirectoryChangeTime_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData(0xEF53u, true)]
    [InlineData(0x58465342u, true)]
    [InlineData(0x9123683Eu, true)]
    [InlineData(0x01021994u, true)]
    [InlineData(0x6969u, false)]
    [InlineData(0x65735546u, false)]
    [InlineData(0x794C7630u, false)]
    [InlineData(0x9FA0u, false)]
    public void LinuxAdmissionCoversLocalFamiliesAndRejectsUnsupportedProviders(uint fileSystem, bool expected)
        => DirectoryChangeTime.IsSupportedLinuxFileSystem(fileSystem).ShouldBe(expected);

    [Theory]
    [InlineData("apfs", 0x1000u, true)]
    [InlineData("hfs", 0x1000u, true)]
    [InlineData("apfs", 0u, false)]
    [InlineData("hfs", 0u, false)]
    [InlineData("nfs", 0x1000u, false)]
    [InlineData("smbfs", 0x1000u, false)]
    [InlineData("webdav", 0x1000u, false)]
    public void MacOSAdmissionRequiresALocalApfsOrHfsVolume(string fileSystem, uint flags, bool expected)
        => DirectoryChangeTime.IsSupportedMacOSFileSystem(Encoding.UTF8.GetBytes(fileSystem), flags).ShouldBe(expected);

    [Theory]
    [InlineData("ctime")]
    [InlineData("mtime")]
    [InlineData("size")]
    [InlineData("inode")]
    [InlineData("mount")]
    [InlineData("mode")]
    public void LinuxMetadataChangesInvalidateTheCachedListing(string changed)
    {
        var data = LinuxData();
        DirectoryChangeTime.CreateLinuxStamp(data, out DirectoryChangeStamp before).ShouldBe(DirectoryStampStatus.Available);
        switch (changed)
        {
            case "ctime": data.ChangeNanoseconds++; break;
            case "mtime": data.ModifiedNanoseconds++; break;
            case "size": data.Size++; break;
            case "inode": data.Identity++; break;
            case "mount": data.MountId++; break;
            case "mode": data.FileType = (DirectoryChangeTime.UnixFileType)((ushort)data.FileType ^ 0x80); break;
        }
        DirectoryChangeTime.CreateLinuxStamp(data, out DirectoryChangeStamp after).ShouldBe(DirectoryStampStatus.Available);
        AssertListingInvalidated(before, after);
    }

    [Theory]
    [InlineData("ctime")]
    [InlineData("mtime")]
    [InlineData("size")]
    [InlineData("inode")]
    [InlineData("device")]
    [InlineData("generation")]
    public void MacOSMetadataChangesInvalidateTheCachedListing(string changed)
    {
        var data = MacOSData();
        DirectoryChangeTime.CreateMacOSStamp(data, 7, out DirectoryChangeStamp before).ShouldBe(DirectoryStampStatus.Available);
        switch (changed)
        {
            case "ctime": data.ChangeNanoseconds++; break;
            case "mtime": data.ModifiedNanoseconds++; break;
            case "size": data.Size++; break;
            case "inode": data.Identity++; break;
            case "device": data.Device++; break;
            case "generation": data.Generation++; break;
        }
        DirectoryChangeTime.CreateMacOSStamp(data, 7, out DirectoryChangeStamp after).ShouldBe(DirectoryStampStatus.Available);
        AssertListingInvalidated(before, after);
    }

    [Theory]
    [InlineData((uint)DirectoryChangeTime.StatxFields.Type)]
    [InlineData((uint)DirectoryChangeTime.StatxFields.Mode)]
    [InlineData((uint)DirectoryChangeTime.StatxFields.Identity)]
    [InlineData((uint)DirectoryChangeTime.StatxFields.MountId)]
    [InlineData((uint)DirectoryChangeTime.StatxFields.ChangeTime)]
    [InlineData((uint)DirectoryChangeTime.StatxFields.ModificationTime)]
    [InlineData((uint)DirectoryChangeTime.StatxFields.Size)]
    public void IncompleteLinuxMetadataCannotProduceAStamp(uint missing)
    {
        var data = LinuxData();
        data.Fields &= ~(DirectoryChangeTime.StatxFields)missing;
        DirectoryChangeTime.CreateLinuxStamp(data, out _).ShouldBe(DirectoryStampStatus.Unavailable);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000_000)]
    public void MacOSRejectsInvalidTimestampNanoseconds(long nanoseconds)
    {
        var data = MacOSData();
        data.ChangeNanoseconds = nanoseconds;
        DirectoryChangeTime.CreateMacOSStamp(data, 7, out _).ShouldBe(DirectoryStampStatus.Unavailable);
        data = MacOSData();
        data.ModifiedNanoseconds = nanoseconds;
        DirectoryChangeTime.CreateMacOSStamp(data, 7, out _).ShouldBe(DirectoryStampStatus.Unavailable);
    }

    [Fact]
    public void BothUnixBackendsRejectSymlinksInsteadOfFollowingTheirTargets()
    {
        var linux = LinuxData();
        linux.FileType = (DirectoryChangeTime.UnixFileType)0xA000;
        DirectoryChangeTime.CreateLinuxStamp(linux, out _).ShouldBe(DirectoryStampStatus.Unavailable);
        var mac = MacOSData();
        mac.FileType = linux.FileType;
        DirectoryChangeTime.CreateMacOSStamp(mac, 7, out _).ShouldBe(DirectoryStampStatus.Unavailable);
    }

    [Fact]
    public void FutureModificationTimeCannotEnterTheCacheEvenWithAnOldCtime()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var stamp = new DirectoryChangeStamp(1, 2, 3, now - 60, ModifiedSeconds: now + 60);
        stamp.IsOldEnoughToCache(DateTime.UtcNow).ShouldBeFalse();
    }

    [Theory]
    [InlineData("ext2")]
    [InlineData("ext3")]
    [InlineData("ext4")]
    [InlineData("xfs")]
    [InlineData("btrfs")]
    [InlineData("tmpfs")]
    [InlineData("apfs")]
    [InlineData("hfs")]
    public void NativeFilesystemInvalidatesMembershipAndReplacement(string fileSystem)
    {
        bool macOS = fileSystem is "apfs" or "hfs";
        if (macOS ? !OperatingSystem.IsMacOS() : !OperatingSystem.IsLinux())
        {
            Assert.Skip($"Requires a native {fileSystem} filesystem.");
        }
        string variable = $"MSBUILD_DIRECTORY_CACHE_TEST_{fileSystem.ToUpperInvariant()}_ROOT";
        string? configured = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrEmpty(configured))
        {
            Assert.Skip($"Set {variable} to an existing {fileSystem} fixture directory.");
        }
        string fixtureRoot = Path.GetFullPath(configured!);
        string actual = macOS ? new DriveInfo(fixtureRoot).DriveFormat : LinuxFileSystemAt(fixtureRoot);
        actual.ShouldBe(fileSystem, "Native coverage must run on the named filesystem, not just another supported provider.");
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder(Path.Combine(fixtureRoot, "msbuild-metadata-" + Guid.NewGuid().ToString("N"))).Path;
        string input = Path.Combine(root, "input");
        Directory.CreateDirectory(input);
        var cache = new DirectoryListingCache();
        void CheckMutation(Action mutate)
        {
            DirectoryChangeTime.TryRead(input, out DirectoryChangeStamp before).ShouldBeTrue();
            Thread.Sleep(1100);
            cache.CanStore(before, DateTime.UtcNow).ShouldBeTrue();
            cache.Store(input, FileMatcher.FileSystemEntity.Files, Directory.GetFiles(input), before);
            cache.TryGet(input, FileMatcher.FileSystemEntity.Files, out _).ShouldBeTrue();
            mutate();
            DirectoryChangeTime.TryRead(input, out DirectoryChangeStamp after).ShouldBeTrue();
            after.ShouldNotBe(before);
            cache.TryGet(input, FileMatcher.FileSystemEntity.Files, out _).ShouldBeFalse();
        }
        string first = Path.Combine(input, "first.cs");
        string second = Path.Combine(input, "second.cs");
        CheckMutation(() => File.WriteAllText(first, ""));
        CheckMutation(() => File.Move(first, second));
        CheckMutation(() => File.Delete(second));
        CheckMutation(() =>
        {
            Directory.Move(input, Path.Combine(root, "previous"));
            Directory.CreateDirectory(input);
        });
        _output.WriteLine($"Native create/rename/delete/replacement coverage passed on {fileSystem}: {fixtureRoot}");
    }

    private static string LinuxFileSystemAt(string path)
    {
        string result = "";
        int longest = -1;
        foreach (string line in File.ReadLines("/proc/self/mountinfo"))
        {
            string[] fields = line.Split(' ');
            string mount = fields[4].Replace("\\040", " ").Replace("\\011", "\t").Replace("\\134", "\\");
            if (mount.Length <= longest || !(path == mount || mount == "/" || path.StartsWith(mount + "/", StringComparison.Ordinal)))
            {
                continue;
            }
            int separator = Array.IndexOf(fields, "-");
            if (separator < 0 || separator + 1 >= fields.Length) { continue; }
            longest = mount.Length;
            result = fields[separator + 1];
        }
        return result;
    }

    private static void AssertListingInvalidated(DirectoryChangeStamp before, DirectoryChangeStamp after)
    {
        DirectoryChangeStamp current = before;
        DirectoryStampStatus Read(string _, out DirectoryChangeStamp stamp)
        {
            stamp = current;
            return DirectoryStampStatus.Available;
        }
        var cache = new DirectoryListingCache(Read);
        cache.Store("fixture", FileMatcher.FileSystemEntity.Files, ["old.cs"], before);
        cache.TryGet("fixture", FileMatcher.FileSystemEntity.Files, out _).ShouldBeTrue();
        current = after;
        cache.TryGet("fixture", FileMatcher.FileSystemEntity.Files, out _).ShouldBeFalse();
    }

    private static DirectoryChangeTime.StatxData LinuxData() => new()
    {
        Fields = DirectoryChangeTime.RequiredLinuxFields,
        FileType = DirectoryChangeTime.UnixFileType.Directory,
        Identity = 10,
        MountId = 20,
        ChangeSeconds = 1,
        ChangeNanoseconds = 123_456_700,
        ModifiedSeconds = 1,
        ModifiedNanoseconds = 123_456_700,
        Size = 4096,
        DeviceMajor = 8,
        DeviceMinor = 1,
    };

    private static DirectoryChangeTime.MacOSStatData MacOSData() => new()
    {
        Device = 8,
        Identity = 10,
        FileType = DirectoryChangeTime.UnixFileType.Directory,
        ChangeSeconds = 1,
        ChangeNanoseconds = 123_456_700,
        ModifiedSeconds = 1,
        ModifiedNanoseconds = 123_456_700,
        Size = 4096,
        Generation = 2,
    };
}
#endif
