#!/bin/sh
echo "This line goes to stdout"
>&2 echo "This line goes to stderr"
exit 1
