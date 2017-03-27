// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Construction;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    internal class AddPropertyTransform<T> : ConditionalTransform<T, ProjectPropertyElement>
    {
        public string PropertyName { get; }

        private readonly ProjectRootElement _propertyObjectGenerator = ProjectRootElement.Create();
        private readonly string _propertyValue;
        private readonly Func<T,string> _propertyValueFunc;

        private Func<T, string> _msbuildConditionFunc = null;

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
            _msbuildConditionFunc = source => condition;
            return this;
        }

        public AddPropertyTransform<T> WithMSBuildCondition(Func<T, string> conditionFunc)
        {
            _msbuildConditionFunc = conditionFunc;
            return this;
        }

        public override ProjectPropertyElement ConditionallyTransform(T source)
        {
            string propertyValue = GetPropertyValue(source);

            var property = _propertyObjectGenerator.CreatePropertyElement(PropertyName);
            property.Value = propertyValue;
            
            if (_msbuildConditionFunc != null)
            {
                property.Condition = _msbuildConditionFunc(source);
            }
            
            return property;
        }

        public string GetPropertyValue(T source)
        {
            return _propertyValue ?? _propertyValueFunc(source);
        }
    }
}
