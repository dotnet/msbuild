// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.ApiCompatibility.Mapping;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Moq;

namespace Microsoft.DotNet.ApiCompatibility.Tests.Mapping
{
    public class MemberMapperTests
    {
        [Fact]
        public void MemberMapper_Ctor_PropertiesSet()
        {
            IRuleRunner ruleRunner = Mock.Of<IRuleRunner>();
            IMapperSettings mapperSettings = Mock.Of<IMapperSettings>();
            int rightSetSize = 5;
            ITypeMapper containingType = Mock.Of<ITypeMapper>();

            MemberMapper memberMapper = new(ruleRunner, mapperSettings, rightSetSize, containingType);

            Assert.Null(memberMapper.Left);
            Assert.Equal(mapperSettings, memberMapper.Settings);
            Assert.Equal(rightSetSize, memberMapper.Right.Length);
            Assert.Equal(containingType, memberMapper.ContainingType);
        }
    }
}
