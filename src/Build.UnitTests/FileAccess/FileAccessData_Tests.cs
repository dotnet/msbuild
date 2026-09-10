// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_REPORTFILEACCESSES

using Microsoft.Build.BackEnd;
using Microsoft.Build.Experimental.FileAccess;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.FileAccess
{
    public class FileAccessData_Tests
    {
        [Fact]
        public void TranslationRoundTrip_PreservesAllFields()
        {
            FileAccessData original = new(
                ReportedFileOperation.CreateFile,
                RequestedAccess.Enumerate,
                processId: 1234,
                id: 42,
                correlationId: 7,
                error: 0,
                DesiredAccess.GENERIC_READ,
                FlagsAndAttributes.FILE_ATTRIBUTE_NORMAL,
                @"C:\repo\src",
                processArgs: "msbuild.exe foo.proj",
                isAnAugmentedFileAccess: true,
                enumeratePattern: "*.cs",
                openedFileOrDirectoryAttributes: FlagsAndAttributes.FILE_ATTRIBUTE_DIRECTORY);

            ((ITranslatable)original).Translate(TranslationHelpers.GetWriteTranslator());

            object boxed = default(FileAccessData);
            ((ITranslatable)boxed).Translate(TranslationHelpers.GetReadTranslator());
            FileAccessData roundTripped = (FileAccessData)boxed;

            roundTripped.Operation.ShouldBe(original.Operation);
            roundTripped.RequestedAccess.ShouldBe(original.RequestedAccess);
            roundTripped.ProcessId.ShouldBe(original.ProcessId);
            roundTripped.Id.ShouldBe(original.Id);
            roundTripped.CorrelationId.ShouldBe(original.CorrelationId);
            roundTripped.Error.ShouldBe(original.Error);
            roundTripped.DesiredAccess.ShouldBe(original.DesiredAccess);
            roundTripped.FlagsAndAttributes.ShouldBe(original.FlagsAndAttributes);
            roundTripped.Path.ShouldBe(original.Path);
            roundTripped.ProcessArgs.ShouldBe(original.ProcessArgs);
            roundTripped.IsAnAugmentedFileAccess.ShouldBe(original.IsAnAugmentedFileAccess);
            roundTripped.EnumeratePattern.ShouldBe(original.EnumeratePattern);
            roundTripped.OpenedFileOrDirectoryAttributes.ShouldBe(original.OpenedFileOrDirectoryAttributes);
        }

        [Fact]
        public void TranslationRoundTrip_NullEnumeratePattern_IsPreserved()
        {
            FileAccessData original = new(
                ReportedFileOperation.CreateFile,
                RequestedAccess.Read,
                processId: 1,
                id: 1,
                correlationId: 0,
                error: 0,
                DesiredAccess.GENERIC_READ,
                FlagsAndAttributes.FILE_ATTRIBUTE_NORMAL,
                @"C:\repo\foo.cs",
                processArgs: null,
                isAnAugmentedFileAccess: false,
                enumeratePattern: null,
                openedFileOrDirectoryAttributes: FlagsAndAttributes.FILE_ATTRIBUTE_NORMAL);

            ((ITranslatable)original).Translate(TranslationHelpers.GetWriteTranslator());

            object boxed = default(FileAccessData);
            ((ITranslatable)boxed).Translate(TranslationHelpers.GetReadTranslator());
            FileAccessData roundTripped = (FileAccessData)boxed;

            roundTripped.EnumeratePattern.ShouldBeNull();
            roundTripped.OpenedFileOrDirectoryAttributes.ShouldBe(FlagsAndAttributes.FILE_ATTRIBUTE_NORMAL);
        }
    }
}

#endif
