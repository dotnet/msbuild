// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class LogOutputProperties_Test
    {
        // Test if a project with LogOutputProperties defined prints the correct error message
        [Fact]
        public void TestMinimalWithError()
        {
            ParallelConsoleLogger L = new ParallelConsoleLogger(LoggerVerbosity.Minimal);

            BuildEventContext bec = new BuildEventContext(1, 2, 3, 4);
            BuildStartedEventArgs bse = new BuildStartedEventArgs("bs", null);
            bse.BuildEventContext = bec;
            L.BuildStartedHandler(null, bse);

            Hashtable items = new Hashtable();
            Microsoft.Build.Utilities.TaskItem task = new Microsoft.Build.Utilities.TaskItem("frame");
            items.Add("LogOutputProperties", task);
            ProjectStartedEventArgs pse = new ProjectStartedEventArgs(-1, "ps", null, "fname", "", null, items, new BuildEventContext(1, 2, 3, 4));
            pse.BuildEventContext = bec;
            L.ProjectStartedHandler(bec, pse);

            BuildErrorEventArgs beea = new BuildErrorEventArgs("VBC",
                            "31415", "file.vb", 42, 0, 0, 0,
                            "Some long message", "help", "sender");
            beea.ProjectFile = "fname";
            beea.BuildEventContext = bec;
            string error = EventArgsFormatting.FormatEventMessage(beea, true, L.propertyOutputMap[(1, 3)]);

            error.ShouldBe("file.vb(42): VBC error 31415: Some long message [fname> frame]");
        }

        // Test if a project with LogOutputProperties defined prints the correct warning message
        [Fact]
        public void TestMinimalWithWarning()
        {
            ParallelConsoleLogger L = new ParallelConsoleLogger(LoggerVerbosity.Minimal);

            BuildEventContext bec = new BuildEventContext(1, 2, 3, 4);
            BuildStartedEventArgs bse = new BuildStartedEventArgs("bs", null);
            bse.BuildEventContext = bec;
            L.BuildStartedHandler(null, bse);

            Hashtable items = new Hashtable();
            Microsoft.Build.Utilities.TaskItem task = new Microsoft.Build.Utilities.TaskItem("frame");
            items.Add("LogOutputProperties", task);
            ProjectStartedEventArgs pse = new ProjectStartedEventArgs(-1, "ps", null, "fname", "", null, items, new BuildEventContext(1, 2, 3, 4));
            pse.BuildEventContext = bec;
            L.ProjectStartedHandler(bec, pse);

            BuildWarningEventArgs bwea = new BuildWarningEventArgs("VBC",
                            "31415", "file.vb", 42, 0, 0, 0,
                            "Some long message", "help", "sender");
            bwea.ProjectFile = "fname";
            bwea.BuildEventContext = bec;
            string warning = EventArgsFormatting.FormatEventMessage(bwea, true, L.propertyOutputMap[(1, 3)]);

            warning.ShouldBe("file.vb(42): VBC warning 31415: Some long message [fname> frame]");
        }

        // Test if a project with MULTIPLE LogOutputProperties defined prints the correct warning message
        [Fact]
        public void TestMinimalErrorWithMultipleProps()
        {
            ParallelConsoleLogger L = new ParallelConsoleLogger(LoggerVerbosity.Minimal);

            BuildEventContext bec = new BuildEventContext(1, 2, 3, 4);
            BuildStartedEventArgs bse = new BuildStartedEventArgs("bs", null);
            bse.BuildEventContext = bec;
            L.BuildStartedHandler(null, bse);

            List<DictionaryEntry> items = new List<DictionaryEntry>();
            Microsoft.Build.Utilities.TaskItem task = new Microsoft.Build.Utilities.TaskItem("frame");
            items.Add(new DictionaryEntry("LogOutputProperties", task));
            Microsoft.Build.Utilities.TaskItem task2 = new Microsoft.Build.Utilities.TaskItem("schema");
            items.Add(new DictionaryEntry("LogOutputProperties", task2));
            ProjectStartedEventArgs pse = new ProjectStartedEventArgs(-1, "ps", null, "fname", "", null, items, new BuildEventContext(1, 2, 3, 4));
            pse.BuildEventContext = bec;
            L.ProjectStartedHandler(bec, pse);

            BuildWarningEventArgs bwea = new BuildWarningEventArgs("VBC",
                            "31415", "file.vb", 42, 0, 0, 0,
                            "Some long message", "help", "sender");
            bwea.ProjectFile = "fname";
            bwea.BuildEventContext = bec;
            string warning = EventArgsFormatting.FormatEventMessage(bwea, true, L.propertyOutputMap[(1, 3)]);

            warning.ShouldBe("file.vb(42): VBC warning 31415: Some long message [fname> frame schema]");
        }

    }
}
