/* Definitions of dependency data structures for GNU Make.
Copyright (C) 1988, 1989, 1990, 1991, 1992, 1993, 1994, 1995, 1996, 1997,
1998, 1999, 2000, 2001, 2002, 2003, 2004, 2005, 2006, 2007, 2008, 2009,
2010 Free Software Foundation, Inc.
This file is part of GNU Make.

GNU Make is free software; you can redistribute it and/or modify it under the
terms of the GNU General Public License as published by the Free Software
Foundation; either version 3 of the License, or (at your option) any later
version.

GNU Make is distributed in the hope that it will be useful, but WITHOUT ANY
WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR
A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with
this program.  If not, see <http://www.gnu.org/licenses/>.  */

/* Flag bits for the second argument to `read_makefile'.
   These flags are saved in the `changed' field of each
   `struct dep' in the chain returned by `read_all_makefiles'.  */

#define RM_NO_DEFAULT_GOAL	(1 << 0) /* Do not set default goal.  */
#define RM_INCLUDED		(1 << 1) /* Search makefile search path.  */
#define RM_DONTCARE		(1 << 2) /* No error if it doesn't exist.  */
#define RM_NO_TILDE		(1 << 3) /* Don't expand ~ in file name.  */
#define RM_NOFLAG		0

/* Structure representing one dependency of a file.
   Each struct file's `deps' points to a chain of these,
   chained through the `next'. `stem' is the stem for this
   dep line of static pattern rule or NULL.

   Note that the first two words of this match a struct nameseq.  */

struct dep
  {
    struct dep *next;
    const char *name;
    const char *stem;
    struct file *file;
    unsigned int changed : 8;
    unsigned int ignore_mtime : 1;
    unsigned int staticpattern : 1;
    unsigned int need_2nd_expansion : 1;
    unsigned int dontcare : 1;
  };


/* Structure used in chains of names, for parsing and globbing.  */

struct nameseq
  {
    struct nameseq *next;
    const char *name;
  };


#define PARSEFS_NONE    (0x0000)
#define PARSEFS_NOSTRIP (0x0001)
#define PARSEFS_NOAR    (0x0002)
#define PARSEFS_NOGLOB  (0x0004)
#define PARSEFS_EXISTS  (0x0008)
#define PARSEFS_NOCACHE (0x0010)

#define PARSE_FILE_SEQ(_s,_t,_c,_p,_f) \
            (_t *)parse_file_seq ((_s),sizeof (_t),(_c),(_p),(_f))

#ifdef VMS
void *parse_file_seq ();
#else
void *parse_file_seq (char **stringp, unsigned int size,
                      int stopchar, const char *prefix, int flags);
#endif

char *tilde_expand (const char *name);

#ifndef NO_ARCHIVES
struct nameseq *ar_glob (const char *arname, const char *member_pattern, unsigned int size);
#endif

#define dep_name(d)     ((d)->name == 0 ? (d)->file->name : (d)->name)

#define alloc_dep()     (xcalloc (sizeof (struct dep)))
#define free_ns(_n)     free (_n)
#define free_dep(_d)    free_ns (_d)

struct dep *copy_dep_chain (const struct dep *d);
void free_dep_chain (struct dep *d);
void free_ns_chain (struct nameseq *n);
struct dep *read_all_makefiles (const char **makefiles);
void eval_buffer (char *buffer);
int update_goal_chain (struct dep *goals);
