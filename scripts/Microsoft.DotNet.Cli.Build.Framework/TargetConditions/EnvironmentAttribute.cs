using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Build.Framework
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class EnvironmentAttribute : TargetConditionAttribute
    {
        private string _envVar;
        private string[] _expectedVals;

        public EnvironmentAttribute(string envVar, params string[] expectedVals)
        {
            if (string.IsNullOrEmpty(envVar))
            {
                throw new ArgumentNullException("envVar");
            }

            _envVar = envVar;
            _expectedVals = expectedVals;
        }

        public override bool EvaluateCondition()
        {
            var actualVal = Environment.GetEnvironmentVariable(_envVar);

            foreach (var expectedVal in _expectedVals)
            {
                if (string.Equals(actualVal, expectedVal, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
