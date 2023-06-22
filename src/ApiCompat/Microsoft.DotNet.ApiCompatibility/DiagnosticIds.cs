// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Class containing the strings representing the Diagnostic IDs that can be returned in the compatibility differences.
    /// </summary>
    public static class DiagnosticIds
    {
        public const string TypeMustExist = "CP0001";
        public const string MemberMustExist = "CP0002";
        public const string AssemblyIdentityMustMatch = "CP0003";
        public const string MatchingAssemblyDoesNotExist = "CP0004";
        public const string CannotAddAbstractMember = "CP0005";
        public const string CannotAddMemberToInterface = "CP0006";
        public const string CannotRemoveBaseType = "CP0007";
        public const string CannotRemoveBaseInterface = "CP0008";
        public const string CannotSealType = "CP0009";
        public const string EnumTypesMustMatch = "CP0010";
        public const string EnumValuesMustMatch = "CP0011";
        public const string CannotRemoveVirtualFromMember = "CP0012";
        public const string CannotAddVirtualToMember = "CP0013";
        public const string CannotRemoveAttribute = "CP0014";
        public const string CannotChangeAttribute = "CP0015";
        public const string CannotAddAttribute = "CP0016";
        public const string CannotChangeParameterName = "CP0017";
        public const string CannotAddSealedToInterfaceMember = "CP0018";
        public const string CannotReduceVisibility = "CP0019";
        public const string CannotExpandVisibility = "CP0020";

        // Assembly loading ids
        public const string AssemblyReferenceNotFound = "CP1002";
    }
}
