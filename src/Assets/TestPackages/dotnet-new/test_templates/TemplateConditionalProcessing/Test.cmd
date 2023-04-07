rem #if defaultTrue
set type = "DefaultTrueIncluded"
rem #else
set type = "DefaultTrueExcluded"
rem #endif

rem #if (defaultFalse)
set type = "DefaultFalseExcluded"
rem #else
set type = "DefaultFalseIncluded"
rem #endif