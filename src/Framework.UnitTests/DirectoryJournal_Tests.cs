// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_WINDOWSINTEROP
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Shouldly;
using Windows.Win32;
using Windows.Win32.System.Ioctl;
using Xunit;

namespace Microsoft.Build.UnitTests;

[SupportedOSPlatform("windows6.1")]
public sealed class DirectoryJournal_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    public static bool WindowsSymbolicLinksAvailable =>
        WindowsTemporaryDirectoryAvailable && new RequiresSymbolicLinksFactAttribute().Skip is null;
    public static bool WindowsJournalAndSymbolicLinksAvailable =>
        WindowsJournalAvailable && WindowsSymbolicLinksAvailable;
    public static bool WindowsTemporaryDirectoryAvailable =>
        Microsoft.Build.Framework.NativeMethods.IsWindows && DirectoryJournal.IsSupportedPath(Path.GetTempPath());
    public static bool WindowsJournalAvailable
    {
        get
        {
            if (!WindowsTemporaryDirectoryAvailable) { return false; }
            var journal = new DirectoryJournal();
            try { return journal.TryRead(Path.GetTempPath(), out _); }
            finally { journal.Clear(); }
        }
    }

    [WindowsOnlyTheory]
    [InlineData("NTFS", true, true)]
    [InlineData("ReFS", true, true)]
    [InlineData("refs", true, true)]
    [InlineData("NTFS", false, false)]
    [InlineData("ReFS", false, false)]
    [InlineData("FAT32", true, false)]
    [InlineData("", true, false)]
    public void VolumeAdmissionRequiresASupportedFilesystemAndJournalCapability(string fileSystem, bool journalSupported, bool expected)
    {
        uint flags = journalSupported ? PInvoke.FILE_SUPPORTS_USN_JOURNAL : 0;
        DirectoryJournal.IsSupportedFileSystem(fileSystem.AsSpan(), flags).ShouldBe(expected);
    }

    [WindowsOnlyTheory]
    [InlineData("NTFS")]
    [InlineData("ReFS")]
    public void NativeVolumeAdmissionPreservesInactiveJournalFallback(string fileSystem)
    {
        string fixtureRoot = GetNativeFixtureRoot(fileSystem);
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFolder(Path.Combine(fixtureRoot, "msbuild-journal-" + Guid.NewGuid().ToString("N"))).Path;
        var journal = new DirectoryJournal();
        try
        {
            DirectoryStampStatus status = journal.Read(path, out _, out DirectoryJournal.RejectionReason rejection);
            if (status == DirectoryStampStatus.Available)
            {
                rejection.ShouldBe(DirectoryJournal.RejectionReason.None);
            }
            else
            {
                rejection.ShouldBeOneOf(DirectoryJournal.RejectionReason.JournalInactive, DirectoryJournal.RejectionReason.AccessDenied);
                journal.Read(path, out _, out DirectoryJournal.RejectionReason cached).ShouldBe(DirectoryStampStatus.Unavailable);
                cached.ShouldBe(rejection);
            }
            _output.WriteLine($"Native {fileSystem} admission: {status}, {rejection}.");
        }
        finally { journal.Clear(); }
    }

    [WindowsOnlyTheory]
    [InlineData("NTFS")]
    [InlineData("ReFS")]
    public void NativeJournalInvalidatesCreateRenameAndDelete(string fileSystem)
    {
        string fixtureRoot = GetNativeFixtureRoot(fileSystem);
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFolder(Path.Combine(fixtureRoot, "msbuild-journal-" + Guid.NewGuid().ToString("N"))).Path;
        var journal = new DirectoryJournal();
        try
        {
            if (journal.Read(path, out _, out DirectoryJournal.RejectionReason rejection) != DirectoryStampStatus.Available)
            {
                rejection.ShouldBeOneOf(DirectoryJournal.RejectionReason.JournalInactive, DirectoryJournal.RejectionReason.AccessDenied);
                Assert.Skip($"Native {fileSystem} journal is not accessible and active: {rejection}.");
            }
            void CheckMutation(Action mutate)
            {
                journal.Read(path, out DirectoryChangeStamp before).ShouldBe(DirectoryStampStatus.Available);
                mutate();
                journal.Read(path, out DirectoryChangeStamp after).ShouldBe(DirectoryStampStatus.Available);
                after.ShouldNotBe(before);
            }
            string first = Path.Combine(path, "first.cs");
            string second = Path.Combine(path, "second.cs");
            CheckMutation(() => File.WriteAllText(first, ""));
            CheckMutation(() => File.Move(first, second));
            CheckMutation(() => File.Delete(second));
        }
        finally { journal.Clear(); }
    }

    [WindowsOnlyTheory]
    [InlineData("NTFS")]
    [InlineData("ReFS")]
    public void NativeBatchedGlobBypassesDependencyScansAndInvalidatesAfterMembershipChanges(string fileSystem)
    {
        string fixtureRoot = GetNativeFixtureRoot(fileSystem);
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDDIRECTORYCACHEEXCLUDEROOTS", null);
        string root = env.CreateFolder(Path.Combine(fixtureRoot, "msbuild-batched-" + Guid.NewGuid().ToString("N"))).Path;
        var cache = new DirectoryListingCache();
        try
        {
            DirectoryListingCache first = cache.CreateEvaluationCache();
            if (!first.TryReadDirectoryChangeTime(root, out DirectoryChangeStamp stamp))
            {
                Assert.Skip($"Native {fileSystem} journal is not accessible and active.");
            }
            IReadOnlyList<string> snapshot = first.Store(root, FileMatcher.FileSystemEntity.Files, [], stamp);
            GlobResultExperiment initial = GlobResultExperiment.Create(first)!;
            var collector = new GlobResultExperiment.Collector();
            initial.RecordListing(collector, root, FileMatcher.FileSystemEntity.Files, snapshot);
            initial.Store("batched", [], FileMatcher.SearchAction.RunSearch, null, collector);
            GlobResultExperiment next = GlobResultExperiment.Create(cache.CreateEvaluationCache())!;
            var journal = (DirectoryJournal)typeof(DirectoryListingCache)
                .GetField("_journal", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cache)!;
            DirectorySnapshotStatus RejectScan(string _, FileMatcher.FileSystemEntity __, IReadOnlyList<string> ___)
                => throw new InvalidOperationException("The batched hit must not scan dependency snapshots.");
            bool RejectExistence(string _) => throw new InvalidOperationException("The batched hit must not probe existence dependencies.");

            next.TryGet("batched", RejectScan, RejectExistence, out _).ShouldBeTrue();
            long nativeReads = journal.GetStatistics().NativeReads;
            long batches = journal.GetStatistics().Batches;
            next.TryGet("batched", RejectScan, RejectExistence, out _).ShouldBeTrue();
            journal.GetStatistics().NativeReads.ShouldBe(nativeReads);
            journal.GetStatistics().Batches.ShouldBeGreaterThan(batches);

            File.WriteAllText(Path.Combine(root, "New.cs"), "");
            GlobResultExperiment afterChange = GlobResultExperiment.Create(cache.CreateEvaluationCache())!;
            afterChange.TryGet("batched", RejectScan, RejectExistence, out _).ShouldBeFalse();
            journal.GetStatistics().InvalidatedSubscriptions.ShouldBeGreaterThan(0);
        }
        finally
        {
            cache.Clear();
        }
    }

    [Fact(Skip = "Requires an accessible NTFS or ReFS change journal.", SkipType = typeof(DirectoryJournal_Tests), SkipUnless = nameof(WindowsJournalAvailable))]
    public void NativeBatchedGlobsDoNotMixNewSharedResultsWithOlderEvaluationSnapshots()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDDIRECTORYCACHEEXCLUDEROOTS", null);
        string root = env.CreateFolder().Path;
        File.WriteAllText(Path.Combine(root, "Old.cs"), "");
        var cache = new DirectoryListingCache();
        FileMatcher Matcher() => new(FileSystems.Default,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(), directoryListingCache: cache);
        try
        {
            FileMatcher oldEvaluation = Matcher();
            oldEvaluation.GetFiles(root, "O*.cs").FileList.Length.ShouldBe(1);
            File.WriteAllText(Path.Combine(root, "New.cs"), "");
            Matcher().GetFiles(root, "N*.cs").FileList.Length.ShouldBe(1);

            oldEvaluation.GetFiles(root, "N*.cs").FileList.ShouldBeEmpty(
                "A new shared result must not replace the older directory snapshot already observed in this evaluation.");
            Matcher().GetFiles(root, "N*.cs").FileList.Length.ShouldBe(1);
        }
        finally
        {
            cache.Clear();
        }
    }

    [WindowsOnlyFact]
    public void RenameInvalidatesOldAndNewParentsIncludingTheFirstUsn()
    {
        var state = CreateState();
        DirectoryJournal.DirectoryId oldParent = new(10);
        DirectoryJournal.DirectoryId newParent = new(20);
        DirectoryJournal.DirectoryId file = new(30);
        state.Track(@"C:\old", oldParent);
        state.Track(@"C:\new", newParent);
        state.TryGetVersion(@"C:\old", out long oldVersion).ShouldBeTrue();
        state.TryGetVersion(@"C:\new", out long newVersion).ShouldBeTrue();

        state.ApplyBuffer(Buffer(128,
            Entry(0, file, oldParent, PInvoke.USN_REASON_RENAME_OLD_NAME),
            Entry(64, file, newParent, PInvoke.USN_REASON_RENAME_NEW_NAME | PInvoke.USN_REASON_CLOSE))).ShouldBeTrue();

        state.TryGetVersion(@"C:\old", out long currentOld).ShouldBeTrue();
        state.TryGetVersion(@"C:\new", out long currentNew).ShouldBeTrue();
        currentOld.ShouldNotBe(oldVersion);
        currentNew.ShouldNotBe(newVersion);
    }

    [WindowsOnlyFact]
    public void ReverseSubscriptionsInvalidateOnlyDependentGlobsAndDetachAllTheirEdges()
    {
        var state = CreateState();
        state.Track(@"C:\", new(1), usn: 0, enumerated: false);
        state.Track(@"C:\input", new(10), usn: 0);
        state.Track(@"C:\other", new(20), usn: 0);
        state.TryAnchor(@"C:\input").ShouldBeTrue();
        state.TryAnchor(@"C:\other").ShouldBeTrue();
        state.TryGetVersion(@"C:\input", out long inputVersion).ShouldBeTrue();
        state.TryGetVersion(@"C:\other", out long otherVersion).ShouldBeTrue();
        var journal = new DirectoryJournal();
        using var dependent = new DirectoryJournal.Subscription(journal);
        using var unaffected = new DirectoryJournal.Subscription(journal);
        var reader = new FakeReader();
        state.Subscribe(@"C:\input", new(state.Epoch, 10, 0, inputVersion), dependent, reader).ShouldBeTrue();
        state.Subscribe(@"C:\other", new(state.Epoch, 20, 0, otherVersion), dependent, reader).ShouldBeTrue();
        state.Subscribe(@"C:\other", new(state.Epoch, 20, 0, otherVersion), unaffected, reader).ShouldBeTrue();

        state.ApplyBuffer(Buffer(64, Entry(0, new(30), new(10), PInvoke.USN_REASON_FILE_CREATE))).ShouldBeTrue();

        dependent.Valid.ShouldBeFalse();
        dependent.Nodes.ShouldBeEmpty("Invalidation must detach reverse edges on all dependencies, not only the changed one.");
        unaffected.Valid.ShouldBeTrue();
        state.TryGetVersion(@"C:\other", out long after).ShouldBeTrue();
        after.ShouldBe(otherVersion);
        unaffected.Dispose();
        unaffected.Nodes.ShouldBeEmpty();
    }

    [WindowsOnlyTheory]
    [InlineData(PInvoke.USN_REASON_RENAME_OLD_NAME)]
    [InlineData(PInvoke.USN_REASON_REPARSE_POINT_CHANGE)]
    [InlineData(PInvoke.USN_REASON_FILE_DELETE)]
    public void AncestorChangesInvalidateReverseSubscriptionsForDescendants(uint reason)
    {
        var state = CreateState();
        state.Track(@"C:\", new(1), usn: 0, enumerated: false);
        state.Track(@"C:\input", new(10), usn: 0, enumerated: false);
        state.Track(@"C:\input\child", new(11), usn: 0);
        state.TryAnchor(@"C:\input\child").ShouldBeTrue();
        state.TryGetVersion(@"C:\input\child", out long version).ShouldBeTrue();
        using var subscription = new DirectoryJournal.Subscription(new DirectoryJournal());
        state.Subscribe(@"C:\input\child", new(state.Epoch, 11, 0, version), subscription, new FakeReader()).ShouldBeTrue();

        state.ApplyBuffer(Buffer(64, Entry(0, new(10), new(1), reason))).ShouldBeTrue();

        subscription.Valid.ShouldBeFalse();
        subscription.Nodes.ShouldBeEmpty();
    }

    [WindowsOnlyFact]
    public void LostHistoryInvalidatesAllReverseSubscriptions()
    {
        var state = CreateState();
        state.Track(@"C:\", new(1), usn: 0, enumerated: false);
        state.Track(@"C:\input", new(10), usn: 0);
        state.TryAnchor(@"C:\input").ShouldBeTrue();
        state.TryGetVersion(@"C:\input", out long version).ShouldBeTrue();
        using var subscription = new DirectoryJournal.Subscription(new DirectoryJournal());
        state.Subscribe(@"C:\input", new(state.Epoch, 10, 0, version), subscription, new FakeReader()).ShouldBeTrue();

        state.ObserveHead(new() { UsnJournalID = 2, NextUsn = 64 }).ShouldBeFalse();

        state.Disabled.ShouldBeTrue();
        subscription.Valid.ShouldBeFalse();
        subscription.Nodes.ShouldBeEmpty();
    }

    [WindowsOnlyFact]
    public void RegistrationRejectsOldStampsAndCatchesChangesAfterReverseEdgesAreAdded()
    {
        var state = CreateState();
        state.Track(@"C:\", new(1), usn: 0, enumerated: false);
        state.Track(@"C:\input", new(10), usn: 0);
        state.TryAnchor(@"C:\input").ShouldBeTrue();
        state.TryGetVersion(@"C:\input", out long before).ShouldBeTrue();
        state.ApplyBuffer(Buffer(64, Entry(0, new(30), new(10), PInvoke.USN_REASON_FILE_CREATE))).ShouldBeTrue();
        using var stale = new DirectoryJournal.Subscription(new DirectoryJournal());
        state.Subscribe(@"C:\input", new(state.Epoch, 10, 0, before), stale, new FakeReader()).ShouldBeFalse();

        state.TryGetVersion(@"C:\input", out long after).ShouldBeTrue();
        using var current = new DirectoryJournal.Subscription(new DirectoryJournal());
        state.Subscribe(@"C:\input", new(state.Epoch, 10, 0, after), current, new FakeReader()).ShouldBeTrue();
        state.ApplyBuffer(Buffer(128, Entry(64, new(31), new(10), PInvoke.USN_REASON_FILE_CREATE))).ShouldBeTrue();
        current.Valid.ShouldBeFalse();
    }

    [WindowsOnlyTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void UnclosedOrUnavailableQualificationRecordsRequireNativeGuards(bool recordAvailable, bool closed)
    {
        var state = new DirectoryJournal.JournalState(new() { UsnJournalID = 1, NextUsn = 128 });
        state.Track(@"C:\", new(1), usn: 0, enumerated: false);
        state.Track(@"C:\input", new(10), usn: 64);
        state.TryAnchor(@"C:\input").ShouldBeTrue();
        state.TryGetVersion(@"C:\input", out long version).ShouldBeTrue();
        var reader = new FakeReader
        {
            ReadBuffer = _ => recordAvailable
                ? Buffer(128, Entry(64, new(10), new(1), PInvoke.USN_REASON_REPARSE_POINT_CHANGE
                    | (closed ? PInvoke.USN_REASON_CLOSE : 0)))
                : Buffer(128),
        };
        using var subscription = new DirectoryJournal.Subscription(new DirectoryJournal());
        state.Subscribe(@"C:\input", new(state.Epoch, 10, 0, version), subscription, reader).ShouldBeTrue();
        subscription.Guards.ContainsKey(@"C:\input").ShouldBe(!recordAvailable || !closed);
    }

    [WindowsOnlyTheory]
    [InlineData(PInvoke.USN_REASON_HARD_LINK_CHANGE)]
    [InlineData(PInvoke.USN_REASON_SECURITY_CHANGE)]
    [InlineData(PInvoke.USN_REASON_REPARSE_POINT_CHANGE)]
    [InlineData(PInvoke.USN_REASON_RENAME_OLD_NAME)]
    [InlineData(PInvoke.USN_REASON_FILE_DELETE)]
    [InlineData(PInvoke.USN_REASON_FILE_CREATE)]
    public void UnrelatedUnclosedChangesDoNotInvalidateTrackedPaths(uint reason)
    {
        var state = CreateState();
        DirectoryJournal.DirectoryId directory = new(10);
        state.Track(@"C:\fixture", directory);
        state.TryGetVersion(@"C:\fixture", out long before).ShouldBeTrue();
        ulong epoch = state.Epoch;

        state.ApplyBuffer(Buffer(64, Entry(0, new(20), new(30), reason))).ShouldBeTrue();
        state.TryGetVersion(@"C:\fixture", out long after).ShouldBeTrue();
        after.ShouldBe(before);
        state.Epoch.ShouldBe(epoch);

        state.ApplyBuffer(Buffer(128, Entry(64, new(20), new(30), reason | PInvoke.USN_REASON_CLOSE))).ShouldBeTrue();
        state.TryGetVersion(@"C:\fixture", out after).ShouldBeTrue();
        after.ShouldBe(before);
    }

    [WindowsOnlyTheory]
    [InlineData(PInvoke.USN_REASON_FILE_CREATE)]
    [InlineData(PInvoke.USN_REASON_FILE_DELETE)]
    [InlineData(PInvoke.USN_REASON_HARD_LINK_CHANGE)]
    [InlineData(PInvoke.USN_REASON_RENAME_OLD_NAME)]
    [InlineData(PInvoke.USN_REASON_RENAME_NEW_NAME)]
    public void MembershipChangesAffectOnlyEnumeratedParentsWithoutAVolumeWideCloseWait(uint reason)
    {
        var state = CreateState();
        DirectoryJournal.DirectoryId parent = new(10);
        state.Track(@"C:\", new(1), enumerated: false);
        state.Track(@"C:\fixture", parent);
        state.Track(@"C:\fixture\child", new(11));
        state.TryGetVersion(@"C:\fixture", out long before).ShouldBeTrue();
        state.TryGetVersion(@"C:\fixture\child", out long childBefore).ShouldBeTrue();

        state.ApplyBuffer(Buffer(64, Entry(0, new(20), new(1), reason))).ShouldBeTrue();
        state.TryGetVersion(@"C:\fixture", out long after).ShouldBeTrue();
        after.ShouldBe(before, "The volume root is a container, not an enumerated input.");
        state.ApplyBuffer(Buffer(128, Entry(64, new(20), parent, reason))).ShouldBeTrue();
        state.TryGetVersion(@"C:\fixture", out after).ShouldBeTrue();
        after.ShouldNotBe(before);
        state.TryGetVersion(@"C:\fixture\child", out long childAfter).ShouldBeTrue();
        childAfter.ShouldBe(childBefore, "Parent membership changes do not replace the child directory.");
    }

    [WindowsOnlyTheory]
    [InlineData(PInvoke.USN_REASON_FILE_DELETE)]
    [InlineData(PInvoke.USN_REASON_REPARSE_POINT_CHANGE)]
    [InlineData(PInvoke.USN_REASON_DATA_OVERWRITE)]
    [InlineData(PInvoke.USN_REASON_DATA_EXTEND)]
    [InlineData(PInvoke.USN_REASON_DATA_TRUNCATION)]
    [InlineData(PInvoke.USN_REASON_STREAM_CHANGE)]
    [InlineData(PInvoke.USN_REASON_RENAME_OLD_NAME)]
    public void DirectAncestorChangesInvalidateOnlyTheirTrackedSubtree(uint reason)
    {
        var state = CreateState();
        state.Track(@"C:\", new(1), enumerated: false);
        state.Track(@"C:\fixture", new(10), enumerated: false);
        state.Track(@"C:\fixture\child", new(11));
        state.Track(@"C:\unrelated", new(12));
        state.TryGetVersion(@"C:\unrelated", out long before).ShouldBeTrue();
        state.ApplyBuffer(Buffer(64, Entry(0, new(10), new(1), reason))).ShouldBeTrue();
        state.IsAnchored(@"C:\fixture", new(10)).ShouldBeFalse();
        state.TryGetVersion(@"C:\fixture\child", out _).ShouldBeFalse();
        state.TryGetVersion(@"C:\unrelated", out long after).ShouldBeTrue();
        after.ShouldBe(before);
    }

    [WindowsOnlyFact]
    public void SingleLinkChangesAreFilteredByTrackedParentPath()
    {
        var state = CreateState();
        state.Track(@"C:\a", new(10), enumerated: false);
        state.Track(@"C:\b", new(20), enumerated: false);
        state.Track(@"C:\a\child", new(30));
        state.Track(@"C:\b\child", new(30));
        state.TryGetVersion(@"C:\a\child", out long aBefore).ShouldBeTrue();
        state.TryGetVersion(@"C:\b\child", out long bBefore).ShouldBeTrue();

        state.ApplyBuffer(Buffer(64, Entry(0, new(30), new(40), PInvoke.USN_REASON_HARD_LINK_CHANGE))).ShouldBeTrue();
        state.TryGetVersion(@"C:\a\child", out long aAfter).ShouldBeTrue();
        aAfter.ShouldBe(aBefore, "A link under an untracked parent is irrelevant.");
        state.ApplyBuffer(Buffer(128, Entry(64, new(30), new(10), PInvoke.USN_REASON_RENAME_OLD_NAME))).ShouldBeTrue();
        state.TryGetVersion(@"C:\a\child", out _).ShouldBeFalse();
        state.TryGetVersion(@"C:\b\child", out long bAfter).ShouldBeTrue();
        bAfter.ShouldBe(bBefore);
    }

    [WindowsOnlyFact]
    public void MultipleParentIncarnationsConservativelyAffectAllTrackedLinks()
    {
        var state = CreateState();
        state.Track(@"C:\a", new(10), enumerated: false);
        state.Track(@"C:\b", new(10), enumerated: false);
        state.Track(@"C:\a\child", new(30));
        state.Track(@"C:\b\child", new(30));
        state.ApplyBuffer(Buffer(64, Entry(0, new(30), new(10), PInvoke.USN_REASON_HARD_LINK_CHANGE))).ShouldBeTrue();
        state.TryGetVersion(@"C:\a\child", out _).ShouldBeFalse();
        state.TryGetVersion(@"C:\b\child", out _).ShouldBeFalse();
    }

    [WindowsOnlyFact]
    public void DirectChangesBeforeOrAtTheTrackedUsnDoNotInvalidateANewerSubscription()
    {
        var state = CreateState();
        state.Track(@"C:\fixture", new(10), usn: 64);
        state.TryGetVersion(@"C:\fixture", out long before).ShouldBeTrue();
        state.ApplyBuffer(Buffer(128,
            Entry(0, new(10), new(1), PInvoke.USN_REASON_REPARSE_POINT_CHANGE),
            Entry(64, new(10), new(1), PInvoke.USN_REASON_REPARSE_POINT_CHANGE))).ShouldBeTrue();
        state.TryGetVersion(@"C:\fixture", out long after).ShouldBeTrue();
        after.ShouldBe(before);
        state.ApplyBuffer(Buffer(192, Entry(128, new(10), new(1), PInvoke.USN_REASON_REPARSE_POINT_CHANGE))).ShouldBeTrue();
        state.TryGetVersion(@"C:\fixture", out _).ShouldBeFalse();
    }

    [WindowsOnlyFact]
    public void NativeZeroUsnDoesNotHideTheFirstDirectAncestorChange()
    {
        var state = CreateState();
        state.Track(@"C:\fixture", new(10), usn: 0, enumerated: false);
        state.Track(@"C:\fixture\child", new(11), usn: 0);
        state.TryGetVersion(@"C:\fixture\child", out _).ShouldBeTrue();
        state.ApplyBuffer(Buffer(64, Entry(0, new(10), new(1), PInvoke.USN_REASON_REPARSE_POINT_CHANGE))).ShouldBeTrue();
        state.TryGetVersion(@"C:\fixture\child", out _).ShouldBeFalse();
    }

    [WindowsOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedQualificationCannotLeaveAnIncompleteAnchorForADescendant(bool failDuringAncestorRegistration)
    {
        const string parent = @"C:\r\p";
        const string child = @"C:\r\p\q";
        var state = CreateState();
        var journal = new DirectoryJournal();
        var reader = new FakeReader { QuerySucceeds = _ => failDuringAncestorRegistration };
        bool failAtRoot = true;
        List<string> visited = [];
        bool ReadAncestor(string path, out DirectoryJournal.Record record)
        {
            visited.Add(path);
            record = path switch
            {
                @"C:\r\p" => new(64, new(30), new(20), 0, 0, 0x10),
                @"C:\r" => new(64, new(20), new(1), 0, 0, 0x10),
                @"C:\" => new(64, new(1), new(1), 0, 0, 0x10),
                _ => throw new InvalidOperationException(path),
            };
            return !failAtRoot || path != @"C:\";
        }
        bool WalkParent(string path) => DirectoryJournal.TrackAncestors(path, new(20), state, ReadAncestor);
        bool WalkChild(string path) => DirectoryJournal.TrackAncestors(path, new(30), state, ReadAncestor);

        journal.Qualify(parent, Guid.Empty, new(30), state, reader, WalkParent, out _, usn: 0)
            .ShouldBe(DirectoryStampStatus.Unavailable);
        state.IsAnchored(parent, new(30)).ShouldBeFalse();
        state.IsAnchored(@"C:\r", new(20)).ShouldBeFalse();

        visited.Clear();
        reader.QuerySucceeds = _ => true;
        failAtRoot = false;
        journal.Qualify(child, Guid.Empty, new(40), state, reader, WalkChild, out _, usn: 0)
            .ShouldBe(DirectoryStampStatus.Available);
        visited.ShouldBe([parent, @"C:\r", @"C:\"]);
        state.IsAnchored(parent, new(30)).ShouldBeTrue();
        state.IsAnchored(child, new(40)).ShouldBeTrue();

        state.ApplyBuffer(Buffer(128, Entry(64, new(20), new(1), PInvoke.USN_REASON_RENAME_OLD_NAME))).ShouldBeTrue();
        state.IsAnchored(child, new(40)).ShouldBeFalse();
        state.TryGetVersion(child, out _).ShouldBeFalse();
    }

    [WindowsOnlyFact]
    public void RetrackingAReplacedPathInvalidatesItsOldDescendantSubscriptions()
    {
        var state = CreateState();
        state.Track(@"C:\fixture", new(10), enumerated: false);
        state.Track(@"C:\fixture\child", new(11));
        state.TryGetVersion(@"C:\fixture\child", out long before).ShouldBeTrue();
        state.Track(@"C:\fixture", new(20), usn: 128, enumerated: false);
        state.TryGetVersion(@"C:\fixture\child", out _).ShouldBeFalse();
        state.Track(@"C:\fixture\child", new(21), usn: 128);
        state.TryGetVersion(@"C:\fixture\child", out long after).ShouldBeTrue();
        after.ShouldNotBe(before);
    }

    [WindowsOnlyFact]
    public void QualificationMemoIgnoresUnrelatedChangesButRechecksAnInvalidatedListing()
    {
        var journal = new DirectoryJournal();
        var state = CreateState();
        var reader = new FakeReader { Head = _ => new() { UsnJournalID = 1, NextUsn = state.Cursor } };
        int walks = 0;
        bool Walk(string _) { walks++; return true; }
        DirectoryStampStatus Qualify() => journal.Qualify(@"C:\fixture", Guid.Empty, new(10), state, reader, Walk, out _);

        Qualify().ShouldBe(DirectoryStampStatus.Available);
        Qualify().ShouldBe(DirectoryStampStatus.Available);
        walks.ShouldBe(1);
        state.ApplyBuffer(Buffer(64, Entry(0, new(20), new(30), PInvoke.USN_REASON_SECURITY_CHANGE | PInvoke.USN_REASON_CLOSE))).ShouldBeTrue();
        Qualify().ShouldBe(DirectoryStampStatus.Available);
        walks.ShouldBe(1);
        state.ApplyBuffer(Buffer(128, Entry(64, new(20), new(10), PInvoke.USN_REASON_FILE_CREATE | PInvoke.USN_REASON_CLOSE))).ShouldBeTrue();
        Qualify().ShouldBe(DirectoryStampStatus.Available);
        walks.ShouldBe(2);
    }

    [WindowsOnlyFact]
    public void PathInvalidationDuringQualificationCannotSeedTheMemo()
    {
        var journal = new DirectoryJournal();
        var state = CreateState();
        var reader = new FakeReader { Head = _ => new() { UsnJournalID = 1, NextUsn = state.Cursor } };
        int walks = 0;
        bool Walk(string _)
        {
            if (++walks == 1)
            {
                state.ApplyBuffer(Buffer(64, Entry(0, new(10), new(30),
                    PInvoke.USN_REASON_REPARSE_POINT_CHANGE | PInvoke.USN_REASON_CLOSE))).ShouldBeTrue();
            }
            return true;
        }
        DirectoryStampStatus Qualify() => journal.Qualify(@"C:\fixture", Guid.Empty, new(10), state, reader, Walk, out _);

        Qualify().ShouldBe(DirectoryStampStatus.Unavailable);
        Qualify().ShouldBe(DirectoryStampStatus.Available);
        walks.ShouldBe(2, "A path invalidated during the walk must not memoize the obsolete qualification.");
        Qualify().ShouldBe(DirectoryStampStatus.Available);
        walks.ShouldBe(2);
    }

    [WindowsOnlyFact]
    public void FailedAncestorWalkDoesNotSeedTheMemo()
    {
        var journal = new DirectoryJournal();
        var state = CreateState();
        var reader = new FakeReader();
        int walks = 0;
        bool Walk(string _) => ++walks > 1;
        DirectoryStampStatus Qualify() => journal.Qualify(@"C:\fixture", Guid.Empty, new(10), state, reader, Walk, out _);
        Qualify().ShouldBe(DirectoryStampStatus.Unavailable);
        Qualify().ShouldBe(DirectoryStampStatus.Available);
        walks.ShouldBe(2);
    }

    [WindowsOnlyFact]
    public void CurrentHeadBootstrapRechecksAncestorEligibilityWithoutNewJournalRecords()
    {
        var journal = new DirectoryJournal();
        var state = new DirectoryJournal.JournalState(new()
        {
            UsnJournalID = 1,
            FirstUsn = 64,
            NextUsn = 128,
        });
        var reader = new FakeReader
        {
            Head = _ => new() { UsnJournalID = 1, FirstUsn = 64, NextUsn = 128 },
        };
        int walks = 0;
        bool Walk(string _) => ++walks == 1;
        DirectoryStampStatus Qualify() => journal.Qualify(@"C:\fixture", Guid.Empty, new(10), state, reader, Walk, out _);

        Qualify().ShouldBe(DirectoryStampStatus.Available);
        Qualify().ShouldBe(DirectoryStampStatus.Unavailable);
        walks.ShouldBe(2);
    }

    [WindowsOnlyFact]
    public void ValidatedResolvedPathsReuseCurrentHeadQualificationsUntilMembershipChanges()
    {
        var journal = new DirectoryJournal();
        var state = new DirectoryJournal.JournalState(new()
        {
            UsnJournalID = 1,
            FirstUsn = 64,
            NextUsn = 128,
        });
        var reader = new FakeReader
        {
            Head = _ => new() { UsnJournalID = 1, FirstUsn = 64, NextUsn = state.Cursor },
        };
        int walks = 0;
        bool Walk(string _) { walks++; return true; }
        DirectoryStampStatus Qualify() => journal.Qualify(@"C:\fixture", Guid.Empty, new(10), state,
            reader, Walk, out _, resolvedPathValidated: true);

        Qualify().ShouldBe(DirectoryStampStatus.Available);
        Qualify().ShouldBe(DirectoryStampStatus.Available);
        walks.ShouldBe(1);

        state.ApplyBuffer(Buffer(192, Entry(128, new(20), new(10), PInvoke.USN_REASON_FILE_CREATE))).ShouldBeTrue();
        Qualify().ShouldBe(DirectoryStampStatus.Available);
        walks.ShouldBe(2);
        Qualify().ShouldBe(DirectoryStampStatus.Available);
        walks.ShouldBe(2);
    }

    [WindowsOnlyFact]
    public void BasicInformationChangesDoNotInvalidateNameOnlyListings()
    {
        var state = CreateState();
        DirectoryJournal.DirectoryId directory = new(10);
        state.Track(@"C:\fixture", directory);
        state.TryGetVersion(@"C:\fixture", out long before).ShouldBeTrue();
        ulong epoch = state.Epoch;
        state.ApplyBuffer(Buffer(64, Entry(0, new(20), directory, PInvoke.USN_REASON_BASIC_INFO_CHANGE))).ShouldBeTrue();
        state.TryGetVersion(@"C:\fixture", out long version).ShouldBeTrue();
        version.ShouldBe(before);
        state.Epoch.ShouldBe(epoch);
    }

    [WindowsOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroStartReplayRejectsConcurrentTrimmingIncludingAfterAnEmptyJournal(bool initiallyEmpty)
    {
        var state = CreateState();
        var reader = new FakeReader();
        if (initiallyEmpty)
        {
            reader.Head = _ => new() { UsnJournalID = 1 };
            DirectoryJournal.CatchUp(state, reader, @"C:\fixture", new(10), out _).ShouldBe(DirectoryStampStatus.Available);
        }
        int firstQuery = reader.Queries;
        reader.Head = query => new()
        {
            UsnJournalID = 1,
            NextUsn = 128,
            FirstUsn = query == firstQuery + 1 ? 0 : 32,
        };
        reader.ReadBuffer = _ => Buffer(128, Entry(64, new(20), new(10), PInvoke.USN_REASON_CLOSE));
        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", new(10), out _).ShouldBe(DirectoryStampStatus.Unavailable);
        state.Disabled.ShouldBeTrue();
    }

    [WindowsOnlyFact]
    public void ZeroStartCompletenessRequirementSurvivesTransientPostQueryFailure()
    {
        var state = CreateState();
        var reader = new FakeReader
        {
            Head = query => new() { UsnJournalID = 1, NextUsn = 64, FirstUsn = query <= 2 ? 0 : 32 },
            QuerySucceeds = query => query != 2,
        };
        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", new(10), out _).ShouldBe(DirectoryStampStatus.Unavailable);
        state.Cursor.ShouldBe(64);
        state.Disabled.ShouldBeFalse();
        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", new(10), out _).ShouldBe(DirectoryStampStatus.Unavailable);
        state.Disabled.ShouldBeTrue();
    }

    [WindowsOnlyFact]
    public async Task CapabilityChecksDoNotWaitForNativeValidation()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var journal = new DirectoryJournal((string _, out DirectoryChangeStamp stamp, out DirectoryJournal.RejectionReason reason) =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();
            stamp = default;
            reason = DirectoryJournal.RejectionReason.None;
            return DirectoryStampStatus.Unavailable;
        });
        Task<bool> validation = Task.Run(() => journal.TryRead(@"C:\fixture", out _));
        Task<bool>? capability = null;
        bool completed = false;
        try
        {
            entered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            capability = Task.Run(() => journal.CanAttemptSharing(@"D:\unrelated"));
            completed = await Task.WhenAny(capability, Task.Delay(TimeSpan.FromSeconds(5))) == capability;
        }
        finally
        {
            release.Set();
            await validation;
            if (capability is not null) { await capability; }
        }

        completed.ShouldBeTrue("A capability probe must not wait behind native journal validation.");
        capability!.Result.ShouldBeTrue();
    }

    [WindowsOnlyFact]
    public async Task DirectoryReadsDoNotSerializeIndependentNativeValidation()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var journal = new DirectoryJournal((string path, out DirectoryChangeStamp stamp, out DirectoryJournal.RejectionReason reason) =>
        {
            if (path.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();
            }
            stamp = default;
            reason = DirectoryJournal.RejectionReason.None;
            return DirectoryStampStatus.Available;
        });
        Task<bool> blocked = Task.Run(() => journal.TryRead(@"C:\fixture", out _));
        Task<bool>? independent = null;
        bool completed = false;
        try
        {
            entered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            independent = Task.Run(() => journal.TryRead(@"D:\unrelated", out _));
            completed = await Task.WhenAny(independent, Task.Delay(TimeSpan.FromSeconds(5))) == independent;
        }
        finally
        {
            release.Set();
            await blocked;
            if (independent is not null) { await independent; }
        }

        completed.ShouldBeTrue("Independent directory opens and metadata reads must not hold the shared journal-state lock.");
        independent!.Result.ShouldBeTrue();
        journal.GetStatistics().NativeReads.ShouldBe(2);
        journal.GetStatistics().Stamps.ShouldBe(2);
    }

    [WindowsOnlyTheory]
    [InlineData(0)]
    [InlineData(128)]
    public void IdleCatchUpQueriesTheHeadOnlyOnce(long checkpoint)
    {
        USN_JOURNAL_DATA_V0 info = new() { UsnJournalID = 1, NextUsn = checkpoint };
        var state = new DirectoryJournal.JournalState(info);
        var reader = new FakeReader
        {
            Head = _ => info,
            ReadBuffer = _ => throw new InvalidOperationException("An up-to-date checkpoint must not replay records."),
        };

        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", new(10), out _)
            .ShouldBe(DirectoryStampStatus.Available);
        reader.Queries.ShouldBe(1);
    }

    [WindowsOnlyFact]
    public void ANewHeadIsDrainedBeforeIssuingAStamp()
    {
        var state = CreateState();
        var reader = new FakeReader
        {
            Head = query => new() { UsnJournalID = 1, NextUsn = query == 1 ? 64 : 128 },
            ReadBuffer = start => start == 0 ? Buffer(64) : Buffer(128, Entry(64, new(20), new(10), PInvoke.USN_REASON_FILE_CREATE)),
        };
        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", new(10), out _).ShouldBe(DirectoryStampStatus.Available);
        state.Cursor.ShouldBe(128);
        state.Disabled.ShouldBeFalse();
        state.TryGetVersion(@"C:\fixture", out _).ShouldBeTrue();
    }

    [WindowsOnlyFact]
    public void ContinuousHeadGrowthAndReplayBudgetsDeferWithoutPoisoningTheVolume()
    {
        var state = CreateState();
        var reader = new FakeReader
        {
            Head = query => new() { UsnJournalID = 1, NextUsn = query * 64 },
            ReadBuffer = start => Buffer(start + 64),
        };
        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", new(10), out _).ShouldBe(DirectoryStampStatus.Unavailable);
        reader.Queries.ShouldBe(4);
        state.Disabled.ShouldBeFalse();
        reader.Head = _ => new() { UsnJournalID = 1, NextUsn = state.Cursor };
        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", new(10), out _).ShouldBe(DirectoryStampStatus.Available);

        state = CreateState();
        reader.Head = _ => new() { UsnJournalID = 1, NextUsn = 128 };
        reader.ReadBuffer = start => Buffer(start + 64,
            Entry(start, new(20), new(10), PInvoke.USN_REASON_FILE_CREATE | PInvoke.USN_REASON_CLOSE));
        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", new(10), out _, maximumReadBytes: 72).ShouldBe(DirectoryStampStatus.Unavailable);
        state.Cursor.ShouldBe(64);
        state.Disabled.ShouldBeFalse();
        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", new(10), out _, maximumReadBytes: 72).ShouldBe(DirectoryStampStatus.Available);
        state.Cursor.ShouldBe(128);
    }

    [WindowsOnlyFact]
    public void AnEmptyNonProgressingReadDefersWithoutDisablingTracking()
    {
        var state = CreateState();
        var reader = new FakeReader
        {
            Head = _ => new() { UsnJournalID = 1, NextUsn = 64 },
            ReadBuffer = start => Buffer(start),
        };
        DirectoryJournal.DirectoryId directory = new(10);
        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", directory, out _).ShouldBe(DirectoryStampStatus.Unavailable);
        state.Cursor.ShouldBe(0);
        state.Disabled.ShouldBeFalse();

        reader.ReadBuffer = _ => Buffer(64, Entry(0, new(20), directory,
            PInvoke.USN_REASON_FILE_CREATE | PInvoke.USN_REASON_CLOSE));
        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", directory, out long version).ShouldBe(DirectoryStampStatus.Available);
        version.ShouldBeGreaterThan(0);
    }

    [WindowsOnlyFact]
    public void RejectedRootsAvoidNativeCallsUntilClearWithoutPoisoningOtherRoots()
    {
        int calls = 0;
        var journal = new DirectoryJournal((string path, out DirectoryChangeStamp stamp, out DirectoryJournal.RejectionReason reason) =>
        {
            calls++;
            stamp = default;
            reason = path.StartsWith("C:", StringComparison.OrdinalIgnoreCase)
                ? DirectoryJournal.RejectionReason.IncompleteHistory : DirectoryJournal.RejectionReason.None;
            return DirectoryStampStatus.Unavailable;
        });
        journal.TryRead(@"C:\first", out _).ShouldBeFalse();
        journal.CanAttemptSharing(@"C:\second").ShouldBeFalse();
        journal.TryRead(@"c:\second", out _).ShouldBeFalse();
        calls.ShouldBe(1);
        journal.GetStatistics().NegativeHits.ShouldBe(1);
        journal.TryRead(@"D:\missing", out _).ShouldBeFalse();
        journal.CanAttemptSharing(@"D:\other").ShouldBeTrue();
        journal.TryRead(@"D:\other", out _).ShouldBeFalse();
        calls.ShouldBe(3);
        journal.Clear();
        journal.TryRead(@"C:\first", out _).ShouldBeFalse();
        calls.ShouldBe(4);
    }

    [WindowsOnlyTheory]
    [InlineData(64, 0)]
    [InlineData(0, 64)]
    [InlineData(64, 64)]
    public void RetainedStartupHistoryTracksOnlyChangesAfterTheCurrentCheckpoint(long firstUsn, long lowestUsn)
    {
        USN_JOURNAL_DATA_V0 info = new()
        {
            UsnJournalID = 1,
            FirstUsn = firstUsn,
            LowestValidUsn = lowestUsn,
            NextUsn = 128,
        };
        var state = new DirectoryJournal.JournalState(info);
        state.Disabled.ShouldBeFalse();
        state.Cursor.ShouldBe(128);
        var reader = new FakeReader
        {
            Head = _ => info,
            ReadBuffer = _ => throw new InvalidOperationException("Historical records must not be replayed at startup."),
        };
        DirectoryJournal.DirectoryId directory = new(10);
        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", directory, out long before).ShouldBe(DirectoryStampStatus.Available);

        info.NextUsn = 192;
        reader.ReadBuffer = _ => Buffer(192, Entry(128, new(20), directory,
            PInvoke.USN_REASON_FILE_CREATE | PInvoke.USN_REASON_CLOSE));
        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", directory, out long after).ShouldBe(DirectoryStampStatus.Available);
        after.ShouldBeGreaterThan(before);
    }

    [WindowsOnlyFact]
    public void TrimmingBeforeTheCurrentCheckpointIsAllowedButLosingUnreadChangesIsNot()
    {
        var state = new DirectoryJournal.JournalState(new()
        {
            UsnJournalID = 1,
            FirstUsn = 64,
            NextUsn = 128,
        });
        var reader = new FakeReader
        {
            Head = _ => new() { UsnJournalID = 1, FirstUsn = 96, LowestValidUsn = 64, NextUsn = 128 },
        };
        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", new(10), out _).ShouldBe(DirectoryStampStatus.Available);
        reader.Head = _ => new() { UsnJournalID = 1, FirstUsn = 129, NextUsn = 192 };
        DirectoryJournal.CatchUp(state, reader, @"C:\fixture", new(10), out _).ShouldBe(DirectoryStampStatus.Unavailable);
        state.Disabled.ShouldBeTrue();
    }

    [WindowsOnlyTheory]
    [InlineData(-1, 0, 128)]
    [InlineData(0, -1, 128)]
    [InlineData(129, 0, 128)]
    [InlineData(0, 129, 128)]
    public void InvalidStartupCheckpointsCannotSeedSnapshots(long firstUsn, long lowestUsn, long nextUsn)
    {
        var state = new DirectoryJournal.JournalState(new()
        {
            UsnJournalID = 1,
            FirstUsn = firstUsn,
            LowestValidUsn = lowestUsn,
            NextUsn = nextUsn,
        });
        state.Track(@"C:\fixture", new(10));
        state.TryGetVersion(@"C:\fixture", out _).ShouldBeFalse();
    }

    [WindowsOnlyFact]
    public void JournalReplacementTruncationAndRewindAreRejected()
    {
        var state = CreateState();
        state.ApplyBuffer(Buffer(64)).ShouldBeTrue();
        USN_JOURNAL_DATA_V0 info = new() { UsnJournalID = 1, NextUsn = 128 };
        state.IsContinuous(info).ShouldBeTrue();

        info.UsnJournalID = 2;
        state.IsContinuous(info).ShouldBeFalse();
        info.UsnJournalID = 1;
        info.FirstUsn = 65;
        state.IsContinuous(info).ShouldBeFalse();
        info.FirstUsn = 0;
        info.LowestValidUsn = 65;
        state.IsContinuous(info).ShouldBeFalse();
        info.LowestValidUsn = 0;
        info.NextUsn = 63;
        state.IsContinuous(info).ShouldBeFalse();

        state.Disable();
        info.NextUsn = 128;
        state.IsContinuous(info).ShouldBeFalse();
    }

    [WindowsOnlyTheory]
    [InlineData("YAAAAAMAAAD1CQAAAAABAAAAAAAAAAAABQAAAAAABQAAAAAAAAAAAFhcPeIDAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAARAAAADgBMAFcAaQBuAGQAbwB3AHMAAAAAAAAA")]
    [InlineData("UAAAAAIAAAD1CQAAAAABAAUAAAAAAAUAWFw94gMAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABEAAAAOADwAVwBpAG4AZABvAHcAcwAAAAAAAAA=")]
    public void RecordParserReadsCapturedNtfsV2AndV3Records(string fixture)
    {
        // Captured with FSCTL_READ_FILE_USN_DATA, not the synthetic journal encoder below.
        DirectoryJournal.TryParse(Convert.FromBase64String(fixture), out DirectoryJournal.Record record).ShouldBeTrue();
        record.Id.ShouldBe(new(0x10000000009f5));
        record.Parent.ShouldBe(new(0x5000000000005));
        record.Usn.ShouldBe(16680574040);
        record.Reason.ShouldBe(0u);
        record.Attributes.ShouldBe(0x11u);
    }

    [WindowsOnlyFact]
    public void RecordParserPreservesCaptured128BitRefsIdentities()
    {
        byte[] fixture = Convert.FromBase64String("UAAAAAMAAAAAAAAAAAAAAAAGAAAAAAAAAAAAAAAAAAAABgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAAgBMAC4AAAA=");
        DirectoryJournal.TryParse(fixture, out DirectoryJournal.Record record).ShouldBeTrue();
        record.Id.ShouldBe(new(0, 0x600));
        record.Parent.ShouldBe(record.Id);
        record.Attributes.ShouldBe(0x10u);
    }

    [WindowsOnlyFact]
    public void RefsMembershipChangesDistinguishTheHighBitsOfParentIdentities()
    {
        var state = CreateState();
        DirectoryJournal.DirectoryId parent = new(10, 0x600);
        DirectoryJournal.DirectoryId otherParent = new(10, 0x601);
        state.Track(@"Q:\fixture", parent);
        state.Track(@"Q:\other", otherParent);
        state.TryGetVersion(@"Q:\fixture", out long before).ShouldBeTrue();
        state.TryGetVersion(@"Q:\other", out long otherBefore).ShouldBeTrue();

        state.ApplyBuffer(Buffer(128,
            Entry(0, new(20, 0x700), parent, PInvoke.USN_REASON_FILE_CREATE | PInvoke.USN_REASON_CLOSE, major: 3))).ShouldBeTrue();

        state.TryGetVersion(@"Q:\fixture", out long after).ShouldBeTrue();
        state.TryGetVersion(@"Q:\other", out long otherAfter).ShouldBeTrue();
        after.ShouldBeGreaterThan(before);
        otherAfter.ShouldBe(otherBefore);
    }

    [WindowsOnlyFact]
    public void UnprivilegedRecordsWithoutNamesInvalidateTheirParent()
    {
        // Captured from our file's CREATE|CLOSE record; identifying fields are normalized.
        byte[] fixture = Convert.FromBase64String("UAAAAAMAAAAKAAAAAAAAAAAAAAAAAAAAFAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAQCAAAAAAAAAAAAgAAAAAABMAAAAAAA=");
        DirectoryJournal.TryParse(fixture, out DirectoryJournal.Record record).ShouldBeTrue();
        DirectoryJournal.DirectoryId parent = new(20);
        record.Parent.ShouldBe(parent);
        var state = CreateState();
        state.Track(@"C:\fixture", parent);
        state.TryGetVersion(@"C:\fixture", out long before).ShouldBeTrue();
        state.ApplyBuffer(Buffer(128, fixture)).ShouldBeTrue();
        state.TryGetVersion(@"C:\fixture", out long after).ShouldBeTrue();
        after.ShouldBeGreaterThan(before);
    }

    [WindowsOnlyFact]
    public void MalformedAndNonProgressingJournalBuffersAreRejected()
    {
        byte[] entry = Entry(0, new(10), new(20), PInvoke.USN_REASON_FILE_CREATE);
        BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(58), 1000);
        CreateState().ApplyBuffer(Buffer(64, entry)).ShouldBeFalse();

        entry = Entry(0, new(10), new(20), PInvoke.USN_REASON_FILE_CREATE);
        BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(4), 4);
        CreateState().ApplyBuffer(Buffer(64, entry)).ShouldBeFalse();
        CreateState().ApplyBuffer(Buffer(0)).ShouldBeFalse();
        CreateState().ApplyBuffer(Buffer(64, Entry(64, new(10), new(20), PInvoke.USN_REASON_FILE_CREATE))).ShouldBeFalse();
        CreateState().ApplyBuffer(Buffer(128,
            Entry(64, new(10), new(20), PInvoke.USN_REASON_FILE_CREATE),
            Entry(32, new(30), new(20), PInvoke.USN_REASON_FILE_CREATE))).ShouldBeFalse();
    }

    [Fact(Skip = "Requires an accessible NTFS or ReFS change journal.", SkipType = typeof(DirectoryJournal_Tests), SkipUnless = nameof(WindowsJournalAvailable))]
    public void NativeUnrelatedRenamePreservesSharedGlobButMembershipAndAncestorChangesInvalidateIt()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDGLOBRESULTEXPERIMENT", "validated");
        env.SetEnvironmentVariable("MSBUILDDIRECTORYCACHEEXCLUDEROOTS", null);
        string root = env.CreateFolder().Path;
        string input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        string child = Directory.CreateDirectory(Path.Combine(input, "child")).FullName;
        string unrelated = Directory.CreateDirectory(Path.Combine(root, "unrelated")).FullName;
        File.WriteAllText(Path.Combine(child, "Old.cs"), "");
        string external = Path.Combine(unrelated, "external.txt");
        File.WriteAllText(external, "");
        var cache = new DirectoryListingCache();
        int enumerations = 0;
        IReadOnlyList<string> Enumerate(FileMatcher.FileSystemEntity kind, string path, out bool succeeded)
        {
            enumerations++;
            succeeded = true;
            return kind switch
            {
                FileMatcher.FileSystemEntity.Files => Directory.GetFiles(path),
                FileMatcher.FileSystemEntity.Directories => Directory.GetDirectories(path),
                _ => Directory.GetFileSystemEntries(path),
            };
        }
        string[] Query() => new FileMatcher(FileSystems.Default,
            (kind, path, _, _, _) => Enumerate(kind, path, out _),
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            directoryListingCache: cache, getFileSystemEntriesWithStatus: Enumerate).GetFiles(child, "*.cs").FileList;

        try
        {
            Query().Length.ShouldBe(1);
            int warmedEnumerations = enumerations;
            long hits = GlobResultExperiment.GetStatistics()[1];
            Query().Length.ShouldBe(1);
            enumerations.ShouldBe(warmedEnumerations);
            GlobResultExperiment.GetStatistics()[1].ShouldBeGreaterThan(hits);

            File.Move(external, Path.Combine(unrelated, "renamed.txt"));
            hits = GlobResultExperiment.GetStatistics()[1];
            Query().Length.ShouldBe(1);
            enumerations.ShouldBe(warmedEnumerations, "An owned rename outside the input must not invalidate its listing.");
            GlobResultExperiment.GetStatistics()[1].ShouldBeGreaterThan(hits);

            File.WriteAllText(Path.Combine(child, "New.cs"), "");
            Query().Length.ShouldBe(2);
            enumerations.ShouldBeGreaterThan(warmedEnumerations);

            Directory.Move(input, input + "-old");
            Directory.CreateDirectory(child);
            File.WriteAllText(Path.Combine(child, "Replacement.cs"), "");
            string[] actual = Query();
            actual.Length.ShouldBe(1);
            Path.GetFileName(actual[0]).ShouldBe("Replacement.cs");
        }
        finally
        {
            cache.Clear();
        }
    }

    [Fact(Skip = "Requires an ordinary fixed-drive temporary directory.", SkipType = typeof(DirectoryJournal_Tests), SkipUnless = nameof(WindowsTemporaryDirectoryAvailable))]
    public void DirectoryCapabilityIsRecheckedAfterReplacement()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFolder().Path;
        DirectoryJournal.IsSupportedPath(path).ShouldBeTrue();
        Directory.Delete(path);
        File.WriteAllText(path, "");
        DirectoryJournal.IsSupportedPath(path).ShouldBeFalse();
        File.Delete(path);
        Directory.CreateDirectory(path);
        DirectoryJournal.IsSupportedPath(path).ShouldBeTrue();
    }

    [Fact(Skip = "Requires an accessible NTFS change journal.", SkipType = typeof(DirectoryJournal_Tests), SkipUnless = nameof(WindowsJournalAvailable))]
    public void NativeUnchangedDirectoriesReuseAncestorQualification()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFolder().Path;
        var journal = new DirectoryJournal();
        try
        {
            journal.TryRead(path, out DirectoryChangeStamp before).ShouldBeTrue();
            long walks = journal.GetStatistics().AncestorWalks;
            walks.ShouldBeGreaterThan(0);
            journal.TryRead(path, out DirectoryChangeStamp after).ShouldBeTrue();
            after.ShouldBe(before);
            journal.GetStatistics().AncestorWalks.ShouldBe(walks);

            File.WriteAllText(Path.Combine(path, "New.cs"), "");
            journal.TryRead(path, out after).ShouldBeTrue();
            after.ShouldNotBe(before);
            journal.GetStatistics().AncestorWalks.ShouldBeGreaterThan(walks);
        }
        finally
        {
            journal.Clear();
        }
    }

#if FEATURE_SYMLINK_TARGET
    [Fact(Skip = "Requires an accessible NTFS journal and Windows symbolic links.", SkipType = typeof(DirectoryJournal_Tests), SkipUnless = nameof(WindowsJournalAndSymbolicLinksAvailable))]
    public void NativeResolvedPathChecksRejectReparseAncestorsAfterQualification()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        string input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
        string child = Directory.CreateDirectory(Path.Combine(input, "child")).FullName;
        string target = Path.Combine(root, "target");
        var journal = new DirectoryJournal();
        try
        {
            journal.TryRead(child, out DirectoryChangeStamp before).ShouldBeTrue();
            journal.TryRead(child, out _).ShouldBeTrue();
            Directory.Move(input, target);
            journal.TryRead(Path.Combine(target, "child"), out DirectoryChangeStamp moved).ShouldBeTrue();
            moved.IdentityLow.ShouldBe(before.IdentityLow);
            moved.IdentityHigh.ShouldBe(before.IdentityHigh);

            Directory.CreateSymbolicLink(input, target);
            journal.TryRead(child, out _).ShouldBeFalse("A matching directory ID must not bypass the resolved-path check.");
        }
        finally
        {
            journal.Clear();
        }
    }

    [Fact(Skip = "Requires Windows symbolic-link support.", SkipType = typeof(DirectoryJournal_Tests), SkipUnless = nameof(WindowsSymbolicLinksAvailable))]
    public void ReparseAncestorsPreventSharingOnOtherwiseOrdinaryDirectories()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        string target = Directory.CreateDirectory(Path.Combine(root, "target")).FullName;
        string child = Directory.CreateDirectory(Path.Combine(target, "child")).FullName;
        string link = Path.Combine(root, "link");
        Directory.CreateSymbolicLink(link, target);

        DirectoryJournal.IsSupportedPath(child).ShouldBeTrue();
        DirectoryJournal.IsSupportedPath(Path.Combine(link, "child")).ShouldBeFalse();
        DirectoryJournal.IsSupportedPath(link).ShouldBeFalse();
    }
#endif

    private static DirectoryJournal.JournalState CreateState()
        => new(new USN_JOURNAL_DATA_V0 { UsnJournalID = 1 });

    private static string GetNativeFixtureRoot(string fileSystem)
    {
        string variable = $"MSBUILD_DIRECTORY_CACHE_TEST_{fileSystem.ToUpperInvariant()}_ROOT";
        string? configured = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrEmpty(configured))
        {
            Assert.Skip($"Set {variable} to an existing {fileSystem} fixture directory.");
        }
        string path = Path.GetFullPath(configured!);
        new DriveInfo(Path.GetPathRoot(path)!).DriveFormat.ShouldBe(fileSystem);
        return path;
    }

    private sealed class FakeReader : DirectoryJournal.IJournalReader
    {
        internal int Queries;
        internal Func<int, USN_JOURNAL_DATA_V0> Head = _ => new() { UsnJournalID = 1 };
        internal Func<int, bool> QuerySucceeds = _ => true;
        internal Func<long, byte[]> ReadBuffer = start => Buffer(start + 64);
        public bool Query(out USN_JOURNAL_DATA_V0 info) { info = Head(++Queries); return QuerySucceeds(Queries); }
        public bool Read(long start, out ReadOnlyMemory<byte> data) { data = ReadBuffer(start); return true; }
    }

    private static byte[] Buffer(long nextUsn, params byte[][] entries)
    {
        int length = sizeof(long);
        foreach (byte[] entry in entries)
        {
            length += entry.Length;
        }

        byte[] buffer = new byte[length];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, nextUsn);
        int offset = sizeof(long);
        foreach (byte[] entry in entries)
        {
            entry.CopyTo(buffer, offset);
            offset += entry.Length;
        }

        return buffer;
    }

    private static byte[] Entry(long usn, DirectoryJournal.DirectoryId file, DirectoryJournal.DirectoryId parent, uint reason, ushort major = 2)
    {
        int extra = major == 3 ? 16 : 0;
        byte[] data = new byte[64 + extra];
        BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)data.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), major);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(8), file.Low);
        if (major == 3)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(16), file.High);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(32), parent.High);
        }

        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(16 + extra / 2), parent.Low);
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(24 + extra), usn);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(40 + extra), reason);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(56 + extra), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(58 + extra), (ushort)(60 + extra));
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(60 + extra), 'x');
        return data;
    }
}
#endif
