// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class RedirectConsoleWriter_Tests
    {
        [Fact]
        public void PeriodicFlushPreservesSurrogatePairsAcrossConsolePackets()
        {
            List<string> packets = [];
            using ManualResetEventSlim firstPacket = new();
            using (RedirectConsoleWriter writer = new(text =>
            {
                packets.Add(RoundTripConsoleText(text));
                firstPacket.Set();
            }))
            {
                writer.Write("prefix\uD83D");
                firstPacket.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue("the periodic flush must emit the complete prefix");
                writer.Write("\uDE00suffix");
            }

            string.Concat(packets).ShouldBe("prefix\uD83D\uDE00suffix");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ExplicitFlushAndDisposeFinalizeIncompleteSurrogates(bool dispose)
        {
            StringBuilder output = new();
            using RedirectConsoleWriter writer = new(text => output.Append(RoundTripConsoleText(text)));
            writer.Write('\uD83D');
            if (dispose)
            {
                writer.Dispose();
            }
            else
            {
                writer.Flush();
            }

            output.ToString().ShouldBe("\uFFFD");
        }

        private static string RoundTripConsoleText(string text)
        {
            using MemoryStream stream = new();
            new ConsoleWritePacket(text, ConsoleOutput.Standard).Translate(BinaryTranslator.GetWriteTranslator(stream));
            stream.Position = 0;
            ITranslator reader = BinaryTranslator.GetReadTranslator(stream, InterningBinaryReader.CreateSharedBuffer());
            return ((ConsoleWritePacket)ConsoleWritePacket.FactoryForDeserialization(reader)).Text;
        }

        [Theory]
        [InlineData("\n")]
        [InlineData("<EOL>")]
        public void NewLineChangesApplyToDelegatedAndInheritedWrites(string newLine)
        {
            StringBuilder output = new();
            using RedirectConsoleWriter redirect = new(text => output.Append(text));
            TextWriter writer = TextWriter.Synchronized(redirect);
            writer.WriteLine("before");
            writer.NewLine = newLine;
            writer.WriteLine("text");
            writer.WriteLine(42);
            writer.WriteLine();
            char[] characters = ['a', 'b'];
            writer.WriteLine(characters);
#if NET
            writer.WriteLine("span".AsSpan());
#else
            writer.WriteLine("span");
#endif
            writer.NewLine = null;
            writer.WriteLine("after");
            redirect.Dispose();

            string expected = $"before{Environment.NewLine}text{newLine}42{newLine}{newLine}ab{newLine}span{newLine}after{Environment.NewLine}";
            output.ToString().ShouldBe(expected);
            writer.NewLine.ShouldBe(Environment.NewLine);
            Should.NotThrow(() => writer.NewLine = "<disposed>");
            writer.NewLine.ShouldBe("<disposed>");
            writer.WriteLine("discarded");
            output.ToString().ShouldBe(expected);
        }

        [Theory]
        [InlineData("fr-FR", "1,5")]
        [InlineData("en-US", "1.5")]
        public async Task FormattingUsesCallingThreadsCulture(string cultureName, string expectedNumber)
        {
            StringBuilder output = new();
            using (RedirectConsoleWriter writer = await Task.Run(() =>
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                return new RedirectConsoleWriter(text => output.Append(text));
            }))
            {
                await Task.Run(() =>
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                    writer.FormatProvider.ShouldBe(CultureInfo.CurrentCulture);
                    writer.Write(1.5m);
                    writer.Write('|');
                    writer.Write((object)1.5m);
                    writer.Write('|');
                    writer.Write("{0}|", 1.5m);
                    writer.Write("{0}{1}|", 1.5m, string.Empty);
                    writer.Write("{0}{1}{2}|", 1.5m, string.Empty, string.Empty);
                    writer.Write("{0}{1}{2}{3}|", 1.5m, string.Empty, string.Empty, string.Empty);
                    object[] arguments = [1.5m, string.Empty, string.Empty, string.Empty];
                    writer.Write("{0}{1}{2}{3}|", arguments);
                    writer.WriteLine(1.5m);
                    writer.WriteLine((object)1.5m);
                    writer.WriteLine("{0}", 1.5m);
                    writer.WriteLine("{0}{1}", 1.5m, string.Empty);
                    writer.WriteLine("{0}{1}{2}", 1.5m, string.Empty, string.Empty);
                    writer.WriteLine("{0}{1}{2}{3}", 1.5m, string.Empty, string.Empty, string.Empty);
                    writer.WriteLine("{0}{1}{2}{3}", arguments);
                    writer.WriteLine($"interpolated={1.5m};invariant={1.5m.ToString(CultureInfo.InvariantCulture)}");
                });
            }

            output.ToString().Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries).ShouldBe(
            [
                string.Join("|", Enumerable.Repeat(expectedNumber, 8)),
                expectedNumber,
                expectedNumber,
                expectedNumber,
                expectedNumber,
                expectedNumber,
                expectedNumber,
                $"interpolated={expectedNumber};invariant=1.5",
            ]);
        }

        [Fact]
        public async Task EmitConsoleMessages()
        {
            StringBuilder sb = new StringBuilder();

            using (RedirectConsoleWriter writer = new(text => sb.Append(text)))
            {
                writer.WriteLine("Line 1");
                await Task.Delay(80); // should be somehow bigger than `RedirectConsoleWriter` flush period - see its constructor
                writer.Write("Line 2");
            }

            sb.ToString().ShouldBe($"Line 1{Environment.NewLine}Line 2");
        }

        [Fact]
        public void WriteAfterDispose_IsDiscardedWithoutThrowingOrInvokingCallback()
        {
            StringBuilder output = new();
            int callbackCount = 0;
            RedirectConsoleWriter writer = new(text =>
            {
                callbackCount++;
                output.Append(text);
            });

            writer.Write("before dispose");
            writer.Dispose();
            output.ToString().ShouldBe("before dispose");
            int callbackCountAfterDispose = callbackCount;

            Should.NotThrow(() => writer.WriteLine("after dispose"));
            writer.Flush();

            output.ToString().ShouldBe("before dispose");
            callbackCount.ShouldBe(callbackCountAfterDispose);
        }

        [Fact]
        public void FormattedWriteDoesNotAppendNewLine()
        {
            StringBuilder output = new();

            using (RedirectConsoleWriter writer = new(text => output.Append(text)))
            {
                object[] arguments = ["a", "b", "c", "d"];
                writer.Write("{0}{1}{2}{3}", arguments);
            }

            output.ToString().ShouldBe("abcd");
        }

        [Fact]
        public void EmptyFlushDoesNotInvokeCallback()
        {
            int callbackCount = 0;

            using (RedirectConsoleWriter writer = new(_ => callbackCount++))
            {
                writer.Flush();
            }

            callbackCount.ShouldBe(0);
        }
    }
}
