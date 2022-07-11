//#if defaultTrue
function DefaultTrueIncluded() { }
//#else
function DefaultTrueExcluded() { }
//#endif

//-:cnd:noEmit
//#if defaultTrue
function InsideUnknownDirectiveNoEmit() { }
//#endif
//+:cnd:noEmit

//#if (defaultFalse)
function DefaultFalseExcluded() { }
//#else
function DefaultFalseIncluded() { }
//#endif

// Without noEmit the following line will be emitted
//-:cnd
//#if defaultFalse
function InsideUnknownDirectiveEmit() { }
//#endif
//+:cnd