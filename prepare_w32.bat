@echo off
@echo Windows32 CVS build preparation of config.h.W32 and NMakefile.
if not exist config.h.W32 copy config.h.W32.template config.h.W32
if not exist config.h copy config.h.W32 config.h
if not exist NMakefile copy NMakefile.template NMakefile
@echo Preparation complete.  Run build_w32.bat to compile and link.
