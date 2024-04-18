// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.BuildCheck.Infrastructure.EditorConfig;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using static Microsoft.Build.BuildCheck.Infrastructure.EditorConfig.EditorConfigGlobsMatcher;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class CustomConfigurationData_Tests
{
    [Fact]
    public void TestCustomConfigurationData_Equals_ShouldBeTrue_NullInstance()
    {
        var customConfigurationData1 = CustomConfigurationData.Null;
        var customConfigurationData2 = CustomConfigurationData.Null;

        customConfigurationData1.Equals(customConfigurationData2).ShouldBeTrue();
    }

    [Fact]
    public void TestCustomConfigurationData_Equals_ShouldBeTrue_SameInstance()
    {
        var customConfigurationData1 = new CustomConfigurationData("testRuleId");
        var customConfigurationData2 = customConfigurationData1;

        customConfigurationData1.Equals(customConfigurationData2).ShouldBeTrue();
    }

    [Fact]
    public void TestCustomConfigurationData_Equals_ShouldBeFalse_DifferentObjectType()
    {
        var customConfigurationData1 = new CustomConfigurationData("testRuleId");
        var customConfigurationData2 = new object();

        customConfigurationData1.Equals(customConfigurationData2).ShouldBeFalse();
    }

    [Fact]
    public void TestCustomConfigurationData_Equals_ShouldBeTrue_DifferentInstanceSameValues()
    {
        var customConfigurationData1 = new CustomConfigurationData("testRuleId");
        var customConfigurationData2 = new CustomConfigurationData("testRuleId");

        customConfigurationData1.Equals(customConfigurationData2).ShouldBeTrue();
    }


    [Fact]
    public void TestCustomConfigurationData_Equals_ShouldBeTrue_CustomConfigDataSame()
    {
        var config1 = new Dictionary<string, string>()
        {
            { "key1", "val1" }
        };

        var config2 = new Dictionary<string, string>()
        {
            { "key1", "val1" }
        };
        var customConfigurationData1 = new CustomConfigurationData("testRuleId", config1);
        var customConfigurationData2 = new CustomConfigurationData("testRuleId", config2);

        customConfigurationData1.Equals(customConfigurationData2).ShouldBeTrue();
    }


    [Fact]
    public void TestCustomConfigurationData_Equals_ShouldBeFalse_CustomConfigDataDifferent()
    {
        var config = new Dictionary<string, string>()
        {
            { "key1", "val1" }
        };
        var customConfigurationData1 = new CustomConfigurationData("testRuleId", config);
        var customConfigurationData2 = new CustomConfigurationData("testRuleId");

        customConfigurationData1.Equals(customConfigurationData2).ShouldBeFalse();
    }

    [Fact]
    public void TestCustomConfigurationData_Equals_ShouldBeFalse_CustomConfigDataDifferentKeys()
    {
        var config1 = new Dictionary<string, string>()
        {
            { "key1", "val1" }
        };

        var config2 = new Dictionary<string, string>()
        {
            { "key2", "val2" }
        };

        var customConfigurationData1 = new CustomConfigurationData("testRuleId", config1);
        var customConfigurationData2 = new CustomConfigurationData("testRuleId", config2);

        customConfigurationData1.Equals(customConfigurationData2).ShouldBeFalse();
    }

    [Fact]
    public void TestCustomConfigurationData_Equals_ShouldBeFalse_CustomConfigDataDifferentValues()
    {
        var config1 = new Dictionary<string, string>()
        {
            { "key1", "val1" }
        };

        var config2 = new Dictionary<string, string>()
        {
            { "key1", "val2" }
        };

        var customConfigurationData1 = new CustomConfigurationData("testRuleId", config1);
        var customConfigurationData2 = new CustomConfigurationData("testRuleId", config2);

        customConfigurationData1.Equals(customConfigurationData2).ShouldBeFalse();
    }

    [Fact]
    public void TestCustomConfigurationData_Equals_ShouldBeTrue_CustomConfigDataKeysOrderDiffers()
    {
        var config1 = new Dictionary<string, string>()
        {
            { "key1", "val1" },
            { "key2", "val2" }
        };

        var config2 = new Dictionary<string, string>()
        {
            { "key2", "val2" },
            { "key1", "val1" }
        };

        var customConfigurationData1 = new CustomConfigurationData("testRuleId", config1);
        var customConfigurationData2 = new CustomConfigurationData("testRuleId", config2);

        customConfigurationData1.Equals(customConfigurationData2).ShouldBeTrue();
    }
}
