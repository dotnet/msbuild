// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;

namespace Microsoft.TemplateEngine.TestHelper
{
    /// <summary>
    /// Test cosnstraint factory. Creates a constraint wtih given type.
    /// If args == yes, the constraint returns <see cref="TemplateConstraintResult.Status.Allowed"/>,
    /// if args == no, the constraint returns <see cref="TemplateConstraintResult.Status.Restricted"/>, otherwise <see cref="TemplateConstraintResult.Status.NotEvaluated"/>.
    /// </summary>
    public class TestConstraintFactory : ITemplateConstraintFactory
    {
        public TestConstraintFactory(string type)
        {
            Type = type;
            Id = Guid.NewGuid();
        }

        public string Type { get; }

        public Guid Id { get; }

        public Task<ITemplateConstraint> CreateTemplateConstraintAsync(IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
        {
            return Task.FromResult((ITemplateConstraint)new TestConstraint(this));
        }

        private class TestConstraint : ITemplateConstraint
        {
            public TestConstraint(ITemplateConstraintFactory factory)
            {
                Type = factory.Type;
            }

            public string Type { get; }

            public string DisplayName => "Test Constraint";

            public TemplateConstraintResult Evaluate(string? args)
            {
                if (args == "yes")
                {
                    return TemplateConstraintResult.CreateAllowed(this);
                }
                else if (args == "no")
                {
                    return TemplateConstraintResult.CreateRestricted(this, "cannot run", "do smth");
                }
                return TemplateConstraintResult.CreateEvaluationFailure(this, "bad params");
            }
        }
    }
}
