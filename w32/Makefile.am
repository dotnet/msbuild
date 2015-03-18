# Makefile.am to create libw32.a for mingw32 host.
# Copyright (C) 1997-2012 Free Software Foundation, Inc.
# This file is part of GNU Make.
#
# GNU Make is free software; you can redistribute it and/or modify it under
# the terms of the GNU General Public License as published by the Free Software
# Foundation; either version 3 of the License, or (at your option) any later
# version.
#
# GNU Make is distributed in the hope that it will be useful, but WITHOUT ANY
# WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
# FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more
# details.
#
# You should have received a copy of the GNU General Public License along with
# this program.  If not, see <http://www.gnu.org/licenses/>.

noinst_LIBRARIES = libw32.a

libw32_a_SOURCES =  subproc/misc.c subproc/sub_proc.c subproc/w32err.c \
		    pathstuff.c

libw32_a_CPPFLAGS = -I$(srcdir)/include -I$(srcdir)/subproc -I$(top_srcdir)
