// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Construction;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    public class AddPropertyTransform<T> : ConditionalTransform<T, ProjectPropertyElement>
    {
        public string PropertyName { get; }

        private readonly ProjectRootElement _propertyObjectGenerator = ProjectRootElement.Create();
        private readonly string _propertyValue;
        private readonly Func<T,string> _propertyValueFunc;

        private string _msbuildCondition = null;

        public AddPropertyTransform(string propertyName, string propertyValue, Func<T,bool> condition)
            : base(condition)
        {
            PropertyName = propertyName;
            _propertyValue = propertyValue;
        }

        public AddPropertyTransform(string propertyName, Func<T, string> propertyValueFunc, Func<T,bool> condition)
            : base(condition)
        {
            PropertyName = propertyName;
            _propertyValueFunc = propertyValueFunc;
        }

        public AddPropertyTransform<T> WithMSBuildCondition(string condition)
        {
            _msbuildCondition = condition;
            return this;
        }

        public override ProjectPropertyElement ConditionallyTransform(T source)
        {
            string propertyValue = GetPropertyValue(source);

            var property = _propertyObjectGenerator.CreatePropertyElement(PropertyName);
            property.Value = propertyValue;
            
            if (!string.IsNullOrEmpty(_msbuildCondition))
            {
                property.Condition = _msbuildCondition;
            }
            
            return property;
        }

        public string GetPropertyValue(T source)
        {
            return _propertyValue ?? _propertyValueFunc(source);
        }
    }
}
