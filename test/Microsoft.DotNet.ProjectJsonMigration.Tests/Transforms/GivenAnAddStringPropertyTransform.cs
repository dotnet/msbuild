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
    public class GivenAnAddStringPropertyTransform
    {
        [Fact]
        public void It_adds_a_property_to_the_project_with_string_value()
        {
            var propertyName = "Property1";
            var propertyValue = "TestValue1";

            var propertyTransform = new AddStringPropertyTransform(propertyName, t => true);
            var property = propertyTransform.Transform(propertyValue);
            property.Name.Should().Be(propertyName);
            property.Value.Should().Be(propertyValue);
        }

        [Fact]
        public void It_returns_null_when_propertyValue_is_null_and_condition_is_passed()
        {
            var propertyName = "Property1";
            string propertyValue = null;

            var propertyTransform = new AddStringPropertyTransform(propertyName, t => true);
            propertyTransform.Transform(propertyValue).Should().BeNull();
        }
        [Fact]
        public void It_returns_null_when_propertyValue_is_null_and_condition_is_not_passed()
        {
            var mockProj = ProjectRootElement.Create();
            var propertyName = "Property1";
            string propertyValue = null;

            var propertyTransform = new AddStringPropertyTransform(propertyName);
            propertyTransform.Transform(propertyValue).Should().BeNull();
        }


        [Fact]
        public void It_returns_null_when_condition_is_false()
        {
            var propertyName = "Property1";
            var propertyValue = "TestValue1";

            var propertyTransform = new AddStringPropertyTransform(propertyName, t => false);
            propertyTransform.Transform(propertyValue).Should().BeNull();
        }
    }
}
