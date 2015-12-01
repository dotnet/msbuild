// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace dia2
{
    public enum DataKind
    {
        DataIsUnknown,
        DataIsLocal,
        DataIsStaticLocal,
        DataIsParam,
        DataIsObjectPtr,
        DataIsFileStatic,
        DataIsGlobal,
        DataIsMember,
        DataIsStaticMember,
        DataIsConstant
    }
}