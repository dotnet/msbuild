// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace dia2
{
    [DefaultMember("symIndexId"), Guid("CB787B2F-BD6C-4635-BA52-933126BD2DCD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IDiaSymbol
    {
        [DispId(0)]
        uint symIndexId
        {

            get;
        }
        [DispId(1)]
        uint symTag
        {

            get;
        }
        [DispId(2)]
        string name
        {

            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }
        [DispId(3)]
        IDiaSymbol lexicalParent
        {

            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }
        [DispId(4)]
        IDiaSymbol classParent
        {

            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }
        [DispId(5)]
        IDiaSymbol type
        {

            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }
        [DispId(6)]
        uint dataKind
        {

            get;
        }
        [DispId(7)]
        uint locationType
        {

            get;
        }
        [DispId(8)]
        uint addressSection
        {

            get;
        }
        [DispId(9)]
        uint addressOffset
        {

            get;
        }
        [DispId(10)]
        uint relativeVirtualAddress
        {

            get;
        }
        [DispId(11)]
        ulong virtualAddress
        {

            get;
        }
        [DispId(12)]
        uint registerId
        {

            get;
        }
        [DispId(13)]
        int offset
        {

            get;
        }
        [DispId(14)]
        ulong length
        {

            get;
        }
        [DispId(15)]
        uint slot
        {

            get;
        }
        [DispId(16)]
        int volatileType
        {

            get;
        }
        [DispId(17)]
        int constType
        {

            get;
        }
        [DispId(18)]
        int unalignedType
        {

            get;
        }
        [DispId(19)]
        uint access
        {

            get;
        }
        [DispId(20)]
        string libraryName
        {

            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }
        [DispId(21)]
        uint platform
        {

            get;
        }
        [DispId(22)]
        uint language
        {

            get;
        }
        [DispId(23)]
        int editAndContinueEnabled
        {

            get;
        }
        [DispId(24)]
        uint frontEndMajor
        {

            get;
        }
        [DispId(25)]
        uint frontEndMinor
        {

            get;
        }
        [DispId(26)]
        uint frontEndBuild
        {

            get;
        }
        [DispId(27)]
        uint backEndMajor
        {

            get;
        }
        [DispId(28)]
        uint backEndMinor
        {

            get;
        }
        [DispId(29)]
        uint backEndBuild
        {

            get;
        }
        [DispId(30)]
        string sourceFileName
        {

            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }
        [DispId(31)]
        string unused
        {

            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }
        [DispId(32)]
        uint thunkOrdinal
        {

            get;
        }
        [DispId(33)]
        int thisAdjust
        {

            get;
        }
        [DispId(34)]
        uint virtualBaseOffset
        {

            get;
        }
        [DispId(35)]
        int @virtual
        {

            get;
        }
        [DispId(36)]
        int intro
        {

            get;
        }
        [DispId(37)]
        int pure
        {

            get;
        }
        [DispId(38)]
        uint callingConvention
        {

            get;
        }
        [DispId(39)]
        object value
        {

            get;
        }
        [DispId(40)]
        uint baseType
        {

            get;
        }
        [DispId(41)]
        uint token
        {

            get;
        }
        [DispId(42)]
        uint timeStamp
        {

            get;
        }
        [DispId(43)]
        Guid guid
        {

            get;
        }
        [DispId(44)]
        string symbolsFileName
        {

            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }
        [DispId(46)]
        int reference
        {

            get;
        }
        [DispId(47)]
        uint count
        {

            get;
        }
        [DispId(49)]
        uint bitPosition
        {

            get;
        }
        [DispId(50)]
        IDiaSymbol arrayIndexType
        {

            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }
        [DispId(51)]
        int packed
        {

            get;
        }
        [DispId(52)]
        int constructor
        {

            get;
        }
        [DispId(53)]
        int overloadedOperator
        {

            get;
        }
        [DispId(54)]
        int nested
        {

            get;
        }
        [DispId(55)]
        int hasNestedTypes
        {

            get;
        }
        [DispId(56)]
        int hasAssignmentOperator
        {

            get;
        }
        [DispId(57)]
        int hasCastOperator
        {

            get;
        }
        [DispId(58)]
        int scoped
        {

            get;
        }
        [DispId(59)]
        int virtualBaseClass
        {

            get;
        }
        [DispId(60)]
        int indirectVirtualBaseClass
        {

            get;
        }
        [DispId(61)]
        int virtualBasePointerOffset
        {

            get;
        }
        [DispId(62)]
        IDiaSymbol virtualTableShape
        {

            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }
        [DispId(64)]
        uint lexicalParentId
        {

            get;
        }
        [DispId(65)]
        uint classParentId
        {

            get;
        }
        [DispId(66)]
        uint typeId
        {

            get;
        }
        [DispId(67)]
        uint arrayIndexTypeId
        {

            get;
        }
        [DispId(68)]
        uint virtualTableShapeId
        {

            get;
        }
        [DispId(69)]
        int code
        {

            get;
        }
        [DispId(70)]
        int function
        {

            get;
        }
        [DispId(71)]
        int managed
        {

            get;
        }
        [DispId(72)]
        int msil
        {

            get;
        }
        [DispId(73)]
        uint virtualBaseDispIndex
        {

            get;
        }
        [DispId(74)]
        string undecoratedName
        {

            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }
        [DispId(75)]
        uint age
        {

            get;
        }
        [DispId(76)]
        uint signature
        {

            get;
        }
        [DispId(77)]
        int compilerGenerated
        {

            get;
        }
        [DispId(78)]
        int addressTaken
        {

            get;
        }
        [DispId(79)]
        uint rank
        {

            get;
        }
        [DispId(80)]
        IDiaSymbol lowerBound
        {

            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }
        [DispId(81)]
        IDiaSymbol upperBound
        {

            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }
        [DispId(82)]
        uint lowerBoundId
        {

            get;
        }
        [DispId(83)]
        uint upperBoundId
        {

            get;
        }
        [DispId(84)]
        uint targetSection
        {

            get;
        }
        [DispId(85)]
        uint targetOffset
        {

            get;
        }
        [DispId(86)]
        uint targetRelativeVirtualAddress
        {

            get;
        }
        [DispId(87)]
        ulong targetVirtualAddress
        {

            get;
        }
        [DispId(88)]
        uint machineType
        {

            get;
        }
        [DispId(89)]
        uint oemId
        {

            get;
        }
        [DispId(90)]
        uint oemSymbolId
        {

            get;
        }
        [DispId(91)]
        IDiaSymbol objectPointerType
        {

            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }
        [DispId(92)]
        uint udtKind
        {

            get;
        }
        [DispId(93)]
        int noReturn
        {

            get;
        }
        [DispId(94)]
        int customCallingConvention
        {

            get;
        }
        [DispId(95)]
        int noInline
        {

            get;
        }
        [DispId(96)]
        int optimizedCodeDebugInfo
        {

            get;
        }
        [DispId(97)]
        int notReached
        {

            get;
        }
        [DispId(98)]
        int interruptReturn
        {

            get;
        }
        [DispId(99)]
        int farReturn
        {

            get;
        }
        [DispId(100)]
        int isStatic
        {

            get;
        }
        [DispId(101)]
        int hasDebugInfo
        {

            get;
        }
        [DispId(102)]
        int isLTCG
        {

            get;
        }
        [DispId(103)]
        int isDataAligned
        {

            get;
        }
        [DispId(104)]
        int hasSecurityChecks
        {

            get;
        }
        [DispId(105)]
        string compilerName
        {

            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }
        [DispId(106)]
        int hasAlloca
        {

            get;
        }
        [DispId(107)]
        int hasSetJump
        {

            get;
        }
        [DispId(108)]
        int hasLongJump
        {

            get;
        }
        [DispId(109)]
        int hasInlAsm
        {

            get;
        }
        [DispId(110)]
        int hasEH
        {

            get;
        }
        [DispId(111)]
        int hasSEH
        {

            get;
        }
        [DispId(112)]
        int hasEHa
        {

            get;
        }
        [DispId(113)]
        int isNaked
        {

            get;
        }
        [DispId(114)]
        int isAggregated
        {

            get;
        }
        [DispId(115)]
        int isSplitted
        {

            get;
        }
        [DispId(116)]
        IDiaSymbol container
        {

            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }
        [DispId(117)]
        int inlSpec
        {

            get;
        }
        [DispId(118)]
        int noStackOrdering
        {

            get;
        }
        [DispId(119)]
        IDiaSymbol virtualBaseTableType
        {

            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }
        [DispId(120)]
        int hasManagedCode
        {

            get;
        }
        [DispId(121)]
        int isHotpatchable
        {

            get;
        }
        [DispId(122)]
        int isCVTCIL
        {

            get;
        }
        [DispId(123)]
        int isMSILNetmodule
        {

            get;
        }
        [DispId(124)]
        int isCTypes
        {

            get;
        }
        [DispId(125)]
        int isStripped
        {

            get;
        }
        [DispId(126)]
        uint frontEndQFE
        {

            get;
        }
        [DispId(127)]
        uint backEndQFE
        {

            get;
        }
        [DispId(128)]
        int wasInlined
        {

            get;
        }
        [DispId(129)]
        int strictGSCheck
        {

            get;
        }
        [DispId(130)]
        int isCxxReturnUdt
        {

            get;
        }
        [DispId(131)]
        int isConstructorVirtualBase
        {

            get;
        }
        [DispId(132)]
        int RValueReference
        {

            get;
        }
        [DispId(133)]
        IDiaSymbol unmodifiedType
        {

            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }
        [DispId(134)]
        int framePointerPresent
        {

            get;
        }
        [DispId(135)]
        int isSafeBuffers
        {

            get;
        }
        [DispId(136)]
        int intrinsic
        {

            get;
        }
        [DispId(137)]
        int @sealed
        {

            get;
        }
        [DispId(138)]
        int hfaFloat
        {

            get;
        }
        [DispId(139)]
        int hfaDouble
        {

            get;
        }
        [DispId(140)]
        uint liveRangeStartAddressSection
        {

            get;
        }
        [DispId(141)]
        uint liveRangeStartAddressOffset
        {

            get;
        }
        [DispId(142)]
        uint liveRangeStartRelativeVirtualAddress
        {

            get;
        }
        [DispId(143)]
        uint countLiveRanges
        {

            get;
        }
        [DispId(144)]
        ulong liveRangeLength
        {

            get;
        }
        [DispId(145)]
        uint offsetInUdt
        {

            get;
        }
        [DispId(146)]
        uint paramBasePointerRegisterId
        {

            get;
        }
        [DispId(147)]
        uint localBasePointerRegisterId
        {

            get;
        }
        [DispId(148)]
        int isLocationControlFlowDependent
        {

            get;
        }
        [DispId(149)]
        uint stride
        {

            get;
        }
        [DispId(150)]
        uint numberOfRows
        {

            get;
        }
        [DispId(151)]
        uint numberOfColumns
        {

            get;
        }
        [DispId(152)]
        int isMatrixRowMajor
        {

            get;
        }
        [DispId(153)]
        int isReturnValue
        {

            get;
        }
        [DispId(154)]
        int isOptimizedAway
        {

            get;
        }
        [DispId(155)]
        uint builtInKind
        {

            get;
        }
        [DispId(156)]
        uint registerType
        {

            get;
        }
        [DispId(157)]
        uint baseDataSlot
        {

            get;
        }
        [DispId(158)]
        uint baseDataOffset
        {

            get;
        }
        [DispId(159)]
        uint textureSlot
        {

            get;
        }
        [DispId(160)]
        uint samplerSlot
        {

            get;
        }
        [DispId(161)]
        uint uavSlot
        {

            get;
        }
        [DispId(162)]
        uint sizeInUdt
        {

            get;
        }
        [DispId(163)]
        uint memorySpaceKind
        {

            get;
        }
        [DispId(164)]
        uint unmodifiedTypeId
        {

            get;
        }
        [DispId(165)]
        uint subTypeId
        {

            get;
        }
        [DispId(166)]
        IDiaSymbol subType
        {

            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }
        [DispId(167)]
        uint numberOfModifiers
        {

            get;
        }
        [DispId(168)]
        uint numberOfRegisterIndices
        {

            get;
        }
        [DispId(169)]
        int isHLSLData
        {

            get;
        }
        [DispId(170)]
        int isPointerToDataMember
        {

            get;
        }
        [DispId(171)]
        int isPointerToMemberFunction
        {

            get;
        }
        [DispId(172)]
        int isSingleInheritance
        {

            get;
        }
        [DispId(173)]
        int isMultipleInheritance
        {

            get;
        }
        [DispId(174)]
        int isVirtualInheritance
        {

            get;
        }
        [DispId(175)]
        int restrictedType
        {

            get;
        }
        [DispId(176)]
        int isPointerBasedOnSymbolValue
        {

            get;
        }
        [DispId(177)]
        IDiaSymbol baseSymbol
        {

            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }
        [DispId(178)]
        uint baseSymbolId
        {

            get;
        }
        [DispId(179)]
        string objectFileName
        {

            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }
        [DispId(184)]
        int isSdl
        {

            get;
        }
        [DispId(185)]
        int isWinRTPointer
        {

            get;
        }
        [DispId(186)]
        int isRefUdt
        {

            get;
        }
        [DispId(187)]
        int isValueUdt
        {

            get;
        }
        [DispId(188)]
        int isInterfaceUdt
        {

            get;
        }
        [DispId(189)]
        int isPGO
        {

            get;
        }
        [DispId(190)]
        int hasValidPGOCounts
        {

            get;
        }
        [DispId(191)]
        int isOptimizedForSpeed
        {

            get;
        }
        [DispId(192)]
        uint PGOEntryCount
        {

            get;
        }
        [DispId(193)]
        uint PGOEdgeCount
        {

            get;
        }
        [DispId(194)]
        ulong PGODynamicInstructionCount
        {

            get;
        }
        [DispId(195)]
        uint staticSize
        {

            get;
        }
        [DispId(196)]
        uint finalLiveStaticSize
        {

            get;
        }
        [DispId(197)]
        string phaseName
        {

            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }
        [DispId(198)]
        int hasControlFlowCheck
        {

            get;
        }
        [DispId(199)]
        int constantExport
        {

            get;
        }
        [DispId(200)]
        int dataExport
        {

            get;
        }
        [DispId(201)]
        int privateExport
        {

            get;
        }
        [DispId(202)]
        int noNameExport
        {

            get;
        }
        [DispId(203)]
        int exportHasExplicitlyAssignedOrdinal
        {

            get;
        }
        [DispId(204)]
        int exportIsForwarder
        {

            get;
        }
        [DispId(205)]
        uint ordinal
        {

            get;
        }

        void get_dataBytes([In] uint cbData, out uint pcbData, out byte pbData);

        void findChildren([In] SymTagEnum symTag, [MarshalAs(UnmanagedType.LPWStr)] [In] string name, [In] uint compareFlags, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findChildrenEx([In] SymTagEnum symTag, [MarshalAs(UnmanagedType.LPWStr)] [In] string name, [In] uint compareFlags, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findChildrenExByAddr([In] SymTagEnum symTag, [MarshalAs(UnmanagedType.LPWStr)] [In] string name, [In] uint compareFlags, [In] uint isect, [In] uint offset, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findChildrenExByVA([In] SymTagEnum symTag, [MarshalAs(UnmanagedType.LPWStr)] [In] string name, [In] uint compareFlags, [In] ulong va, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findChildrenExByRVA([In] SymTagEnum symTag, [MarshalAs(UnmanagedType.LPWStr)] [In] string name, [In] uint compareFlags, [In] uint rva, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void get_types([In] uint cTypes, out uint pcTypes, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol pTypes);

        void get_typeIds([In] uint cTypeIds, out uint pcTypeIds, out uint pdwTypeIds);

        void get_undecoratedNameEx([In] uint undecorateOptions, [MarshalAs(UnmanagedType.BStr)] out string name);

        void get_numericProperties([In] uint cnt, out uint pcnt, out uint pProperties);

        void get_modifierValues([In] uint cnt, out uint pcnt, out ushort pModifiers);

        void findInlineFramesByAddr([In] uint isect, [In] uint offset, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findInlineFramesByRVA([In] uint rva, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findInlineFramesByVA([In] ulong va, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findInlineeLines([MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findInlineeLinesByAddr([In] uint isect, [In] uint offset, [In] uint length, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findInlineeLinesByRVA([In] uint rva, [In] uint length, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findInlineeLinesByVA([In] ulong va, [In] uint length, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void getSrcLineOnTypeDefn([MarshalAs(UnmanagedType.Interface)] out IDiaLineNumber ppResult);
    }
}
