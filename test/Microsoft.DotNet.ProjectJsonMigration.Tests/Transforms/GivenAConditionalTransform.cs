using Microsoft.DotNet.ProjectJsonMigration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenAConditionalTransform
    {
        [Fact]
        public void It_returns_null_when_condition_is_false()
        {
            var conditionalTransform = new TestConditionalTransform(t => false);
            conditionalTransform.Transform("astring").Should().BeNull();
        }

        [Fact]
        public void It_returns_result_of_ConditionallyTransform_when_condition_is_true()
        {
            var conditionalTransform = new TestConditionalTransform(t => true);

            var property = conditionalTransform.Transform("astring");
            property.Should().NotBeNull();
            property.Name.Should().Be("astring");
            property.Value.Should().Be("astring");
        }

        private class TestConditionalTransform : ConditionalTransform<string, ProjectPropertyElement>
        {
            public TestConditionalTransform(Func<string, bool> condition) : base(condition) { }

            public override ProjectPropertyElement ConditionallyTransform(string source)
            {
                var property = ProjectRootElement.Create().CreatePropertyElement(source);
                property.Value = source;
                return property;
            }
        }
    }
}
