#include "stdafx.h"
#include <string>
#include <iostream>

#if defaultTrue
class DefaultTrueIncluded { }
#else
class DefaultTrueExcluded { }
#endif

//-:cnd:noEmit
#if defaultTrue
class InsideUnknownDirectiveNoEmit { }
#endif
//+:cnd:noEmit

#if (defaultFalse)
class DefaultFalseExcluded { }
#else
class DefaultFalseIncluded { }
#endif

// Without noEmit the following line will be emitted
//-:cnd
#if defaultFalse
class InsideUnknownDirectiveEmit { }
#endif
//+:cnd

