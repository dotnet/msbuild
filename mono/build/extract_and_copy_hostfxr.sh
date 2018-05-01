#!/bin/sh

if [ $# -ne 2 ]; then
	echo "Usage: $0 </path/to/hostfxr-pkg-file> <dest_dir>"
	exit 1
fi

TMPDIR=`mktemp -d`

OLDCWD=`pwd`
cd $TMPDIR

pkgutil --expand $1 out

cd out
cat Payload | gunzip -dc | cpio -i
find host -type f -exec cp {} $2 \;

cd $OLDCWD
rm -Rf $TMPDIR
