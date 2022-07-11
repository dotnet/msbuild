//#if defaultTrue
declare class DefaultTrueIncluded { }
//#else
declare class DefaultTrueExcluded { }
//#endif

//-:cnd:noEmit
//#if defaultTrue
declare class InsideUnknownDirectiveNoEmit { }
//#endif
//+:cnd:noEmit

//#if defaultFalse
declare class DefaultFalseExcluded { }
//#else
declare class DefaultFalseIncluded { }
//#endif

// Without noEmit the following line will be emitted
//-:cnd
//#if defaultFalse
declare class InsideUnknownDirectiveEmit { }
//#endif
//+:cnd