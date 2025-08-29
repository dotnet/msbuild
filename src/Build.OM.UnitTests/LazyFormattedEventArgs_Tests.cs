// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.Framework
{
    /// <summary>
    /// Tests for LazyFormattedEventArgs
    /// </summary>
    public class LazyFormattedEventArgs_Tests
    {
#if FEATURE_CODETASKFACTORY
        /// <summary>
        /// Don't crash when task logs with too few format markers
        /// </summary>
        [Fact]
        public void DoNotCrashOnInvalidFormatExpression()
        {
            string content = @"
 <Project DefaultTargets=`t` ToolsVersion=`msbuilddefaulttoolsversion`>
   <UsingTask
     TaskName=`Crash`
     TaskFactory=`CodeTaskFactory`
     AssemblyFile=`$(MSBuildToolsPath)" + Path.DirectorySeparatorChar + @"Microsoft.Build.Tasks.Core.dll` >
     <Task>
       <Code Type=`Fragment` Language=`cs`>
         this.Log.LogError(`Correct: {0}`, `[goodone]`);
         this.Log.LogError(`This is a message logged from a task {1} blah blah [crashing].`, `[crasher]`);

            try
            {
                this.Log.LogError(`Correct: {0}`, 4224);
                this.Log.LogError(`Malformed: {1}`, 42); // Line 13
                throw new InvalidOperationException();
            }
            catch (Exception e)
            {
                this.Log.LogError(`Catching: {0}`, e.GetType().Name);
            }
            finally
            {
                this.Log.LogError(`Finally`);
            }

            try
            {
                this.Log.LogError(`Correct: {0}`, 4224);
                throw new InvalidOperationException();
            }
            catch (Exception e)
            {
                this.Log.LogError(`Catching: {0}`, e.GetType().Name);
                this.Log.LogError(`Malformed: {1}`, 42); // Line 19
            }
            finally
            {
                this.Log.LogError(`Finally`);
            }

            try
            {
                this.Log.LogError(`Correct: {0}`, 4224);
                throw new InvalidOperationException();
            }
            catch (Exception e)
            {
                this.Log.LogError(`Catching: {0}`, e.GetType().Name);
            }
            finally
            {
                this.Log.LogError(`Finally`);
                this.Log.LogError(`Malformed: {1}`, 42); // Line 24
            }

       </Code>
     </Task>
   </UsingTask>

        <Target Name=`t`>
             <Crash />
        </Target>
</Project>
                ";

            MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(content);

            log.AssertLogContains("[goodone]");
            log.AssertLogContains("[crashing]");
        }
#endif
    }
}
