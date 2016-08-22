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
    public class GivenAnAddPropertyTransform
    {
        [Fact]
        public void It_returns_a_property_with_specified_value()
        {
            var propertyName = "Property1";
            var propertyValue = "Value1";

            var propertyTransform = new AddPropertyTransform<string>(propertyName, propertyValue, t=>true);
            var property = propertyTransform.Transform("_");

            property.Name.Should().Be(propertyName);
            property.Value.Should().Be(propertyValue);
        }

        [Fact]
        public void It_returns_a_property_with_computed_value()
        {
            var propertyName = "Property1";
            var propertyValue = "Value1";

            var propertyTransform = new AddPropertyTransform<string>(propertyName, t => t.ToUpper(), t => true);
            var property = propertyTransform.Transform(propertyValue);

            property.Name.Should().Be(propertyName);
            property.Value.Should().Be(propertyValue.ToUpper());
        }

        [Fact]
        public void It_returns_null_when_condition_is_false()
        {
            var propertyName = "Property1";
            var propertyValue = "Value1";

            var propertyTransform = new AddPropertyTransform<string>(propertyName, propertyValue, t => false);
            propertyTransform.Transform(propertyValue).Should().BeNull();
        }

        [Fact]
        public void It_returns_a_property_when_source_is_null_and_propertyValue_is_a_string()
        {
            var propertyName = "Property1";
            var propertyValue = "Value1";

            var propertyTransform = new AddPropertyTransform<string>(
                propertyName, 
                propertyValue, 
                t => true);
            var property = propertyTransform.Transform(null);
            property.Should().NotBeNull();
            property.Value.Should().Be(propertyValue);
        }

        [Fact]
        public void It_returns_a_property_when_source_is_null_and_propertyValue_is_a_Func_that_handles_null()
        {
            var propertyName = "Property1";
            var propertyValue = "Value1";

            var propertyTransform = new AddPropertyTransform<string>(
                propertyName,
                t=> t == null ? propertyValue.ToUpper() : propertyValue.ToLower(),
                t => true);
            var property = propertyTransform.Transform(null);
            property.Value.Should().Be(propertyValue.ToUpper());
        }

        [Fact]
        public void It_throws_when_source_is_null_and_propertyValue_is_a_Func_that_doesnt_handle_null()
        {
            var propertyName = "Property1";

            var propertyTransform = new AddPropertyTransform<string>(
                propertyName,
                t => t.ToUpper(),
                t => true);

            Action transform = () => propertyTransform.Transform(null);
            transform.ShouldThrow<Exception>();
        }
    }
}
