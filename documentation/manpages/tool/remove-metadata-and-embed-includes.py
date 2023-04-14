#!/usr/bin/env python3
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import re
import os
import os.path
import sys

def git_root(file):
    dirname = os.path.dirname(file)
    while True:
        if os.path.isdir(os.path.join(dirname, '.git')):
            return dirname
        dirname = os.path.abspath(os.path.join(dirname, '..'))
        if dirname == '/':
            assert False, 'at root directory now'

def read_lines_document_file(this_file, original_lines):
    result = []

    lines = original_lines

    # metadata section is optional
    if lines[0] == '---' + os.linesep:
        # remove first ---
        lines = lines[1:]

        # find index of second --- and remove that and everything before it
        for i in range(len(lines)):
            if lines[i] == '---' + os.linesep:
                lines = lines[i+1:]
                break

    for line in lines:
        if '[!INCLUDE' in line:
            match = re.search(r'\[!INCLUDE *\[[^\]]+\] *\(([^)]+)\)', line)
            if match:
                relative_path = match.groups()[0]
                if relative_path.startswith('~/'):
                    git_repo_root = git_root(this_file) + '/'
                    file_to_include = os.path.join(git_repo_root, relative_path[2:])
                else:
                    file_to_include = os.path.join(os.path.dirname(this_file), relative_path)
                with open(file_to_include) as f:
                    lines_to_include = f.readlines()
                    result.extend(read_lines_document_file(file_to_include, lines_to_include))
            else:
                assert False, 'Unable to parse: ' + line
        else:
            result.append(line)
    return result

def main(args):
    filename = args[1]
    with open(filename) as original:
        lines = read_lines_document_file(filename, original.readlines())
        with open(filename + '.tmp', 'w') as output:
            for line in lines:
                output.write(line)

    os.replace(filename + '.tmp',  filename)

if __name__ == '__main__':
    sys.exit(main(sys.argv))
