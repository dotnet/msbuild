// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Globbing;
using Microsoft.Build.Framework;
using Xunit;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.Globbing
{
    /// <summary>
    /// Simple test to verify that the FileSystemGlobbing trait functionality works
    /// </summary>
    public class GlobbingTraitDemo_Tests
    {
        [Fact]
        public void MSBuildGlobShouldMatchBasicPattern()
        {
            // Test basic functionality without changing traits
            var glob = MSBuildGlob.Parse("", "*.cs");
            
            Assert.True(glob.IsMatch("test.cs"));
            Assert.False(glob.IsMatch("test.txt"));
        }
        
        [Fact]
        public void TraitEnvironmentVariableCanBeSet()
        {
            // This test verifies that the trait system can detect the environment variable
            var originalValue = Environment.GetEnvironmentVariable("MSBUILD_USE_FILESYSTEMGLOBBING");
            
            try
            {
                // Set the environment variable
                Environment.SetEnvironmentVariable("MSBUILD_USE_FILESYSTEMGLOBBING", "1");
                
                // Force traits to update
                Traits.UpdateFromEnvironment();
                
                // Verify the trait is set
                Assert.True(Traits.Instance.UseFileSystemGlobbingForMSBuildGlob);
                
                // Clear the environment variable
                Environment.SetEnvironmentVariable("MSBUILD_USE_FILESYSTEMGLOBBING", null);
                
                // Force traits to update
                Traits.UpdateFromEnvironment();
                
                // Verify the trait is cleared
                Assert.False(Traits.Instance.UseFileSystemGlobbingForMSBuildGlob);
            }
            finally
            {
                // Restore original value
                Environment.SetEnvironmentVariable("MSBUILD_USE_FILESYSTEMGLOBBING", originalValue);
                Traits.UpdateFromEnvironment();
            }
        }
    }
}