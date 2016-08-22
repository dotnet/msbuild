using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace Microsoft.DotNet.Migration.Tests
{
    public class GivenAnAddBoolPropertyTransform
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_returns_a_property_to_the_project_with_boolean_value(bool propertyValue)
        {
            var propertyName = "Property1";
        
            var propertyTransform = new AddBoolPropertyTransform(propertyName, t => true);
            var property = propertyTransform.Transform(propertyValue);

            property.Name.Should().Be(propertyName);
            property.Value.Should().Be(propertyValue.ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_returns_null_when_condition_is_false(bool propertyValue)
        {
            var propertyName = "Property1";

            var propertyTransform = new AddBoolPropertyTransform(propertyName, t => false);
            propertyTransform.Transform(propertyValue).Should().BeNull();
        }
    }
}
