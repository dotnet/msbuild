/* Builtin function expansion for GNU Make.
Copyright (C) 1988-2012 Free Software Foundation, Inc.
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

#include "make.h"
#include "filedef.h"
#include "variable.h"
#include "dep.h"
#include "job.h"
#include "commands.h"
#include "debug.h"

#ifdef _AMIGA
#include "amiga.h"
#endif


struct function_table_entry
  {
    const char *name;
    unsigned char len;
    unsigned char minimum_args;
    unsigned char maximum_args;
    char expand_args;
    char *(*func_ptr) (char *output, char **argv, const char *fname);
  };

static unsigned long
function_table_entry_hash_1 (const void *keyv)
{
  const struct function_table_entry *key = keyv;
  return_STRING_N_HASH_1 (key->name, key->len);
}

static unsigned long
function_table_entry_hash_2 (const void *keyv)
{
  const struct function_table_entry *key = keyv;
  return_STRING_N_HASH_2 (key->name, key->len);
}

static int
function_table_entry_hash_cmp (const void *xv, const void *yv)
{
  const struct function_table_entry *x = xv;
  const struct function_table_entry *y = yv;
  int result = x->len - y->len;
  if (result)
    return result;
  return_STRING_N_COMPARE (x->name, y->name, x->len);
}

static struct hash_table function_table;


/* Store into VARIABLE_BUFFER at O the result of scanning TEXT and replacing
   each occurrence of SUBST with REPLACE. TEXT is null-terminated.  SLEN is
   the length of SUBST and RLEN is the length of REPLACE.  If BY_WORD is
   nonzero, substitutions are done only on matches which are complete
   whitespace-delimited words.  */

char *
subst_expand (char *o, const char *text, const char *subst, const char *replace,
              unsigned int slen, unsigned int rlen, int by_word)
{
  const char *t = text;
  const char *p;

  if (slen == 0 && !by_word)
    {
      /* The first occurrence of "" in any string is its end.  */
      o = variable_buffer_output (o, t, strlen (t));
      if (rlen > 0)
	o = variable_buffer_output (o, replace, rlen);
      return o;
    }

  do
    {
      if (by_word && slen == 0)
	/* When matching by words, the empty string should match
	   the end of each word, rather than the end of the whole text.  */
	p = end_of_token (next_token (t));
      else
	{
	  p = strstr (t, subst);
	  if (p == 0)
	    {
	      /* No more matches.  Output everything left on the end.  */
	      o = variable_buffer_output (o, t, strlen (t));
	      return o;
	    }
	}

      /* Output everything before this occurrence of the string to replace.  */
      if (p > t)
	o = variable_buffer_output (o, t, p - t);

      /* If we're substituting only by fully matched words,
	 or only at the ends of words, check that this case qualifies.  */
      if (by_word
          && ((p > text && !isblank ((unsigned char)p[-1]))
              || (p[slen] != '\0' && !isblank ((unsigned char)p[slen]))))
	/* Struck out.  Output the rest of the string that is
	   no longer to be replaced.  */
	o = variable_buffer_output (o, subst, slen);
      else if (rlen > 0)
	/* Output the replacement string.  */
	o = variable_buffer_output (o, replace, rlen);

      /* Advance T past the string to be replaced.  */
      t = p + slen;
    } while (*t != '\0');

  return o;
}


/* Store into VARIABLE_BUFFER at O the result of scanning TEXT
   and replacing strings matching PATTERN with REPLACE.
   If PATTERN_PERCENT is not nil, PATTERN has already been
   run through find_percent, and PATTERN_PERCENT is the result.
   If REPLACE_PERCENT is not nil, REPLACE has already been
   run through find_percent, and REPLACE_PERCENT is the result.
   Note that we expect PATTERN_PERCENT and REPLACE_PERCENT to point to the
   character _AFTER_ the %, not to the % itself.
*/

char *
patsubst_expand_pat (char *o, const char *text,
                     const char *pattern, const char *replace,
                     const char *pattern_percent, const char *replace_percent)
{
  unsigned int pattern_prepercent_len, pattern_postpercent_len;
  unsigned int replace_prepercent_len, replace_postpercent_len;
  const char *t;
  unsigned int len;
  int doneany = 0;

  /* Record the length of REPLACE before and after the % so we don't have to
     compute these lengths more than once.  */
  if (replace_percent)
    {
      replace_prepercent_len = replace_percent - replace - 1;
      replace_postpercent_len = strlen (replace_percent);
    }
  else
    {
      replace_prepercent_len = strlen (replace);
      replace_postpercent_len = 0;
    }

  if (!pattern_percent)
    /* With no % in the pattern, this is just a simple substitution.  */
    return subst_expand (o, text, pattern, replace,
			 strlen (pattern), strlen (replace), 1);

  /* Record the length of PATTERN before and after the %
     so we don't have to compute it more than once.  */
  pattern_prepercent_len = pattern_percent - pattern - 1;
  pattern_postpercent_len = strlen (pattern_percent);

  while ((t = find_next_token (&text, &len)) != 0)
    {
      int fail = 0;

      /* Is it big enough to match?  */
      if (len < pattern_prepercent_len + pattern_postpercent_len)
	fail = 1;

      /* Does the prefix match? */
      if (!fail && pattern_prepercent_len > 0
	  && (*t != *pattern
	      || t[pattern_prepercent_len - 1] != pattern_percent[-2]
	      || !strneq (t + 1, pattern + 1, pattern_prepercent_len - 1)))
	fail = 1;

      /* Does the suffix match? */
      if (!fail && pattern_postpercent_len > 0
	  && (t[len - 1] != pattern_percent[pattern_postpercent_len - 1]
	      || t[len - pattern_postpercent_len] != *pattern_percent
	      || !strneq (&t[len - pattern_postpercent_len],
			  pattern_percent, pattern_postpercent_len - 1)))
	fail = 1;

      if (fail)
	/* It didn't match.  Output the string.  */
	o = variable_buffer_output (o, t, len);
      else
	{
	  /* It matched.  Output the replacement.  */

	  /* Output the part of the replacement before the %.  */
	  o = variable_buffer_output (o, replace, replace_prepercent_len);

	  if (replace_percent != 0)
	    {
	      /* Output the part of the matched string that
		 matched the % in the pattern.  */
	      o = variable_buffer_output (o, t + pattern_prepercent_len,
					  len - (pattern_prepercent_len
						 + pattern_postpercent_len));
	      /* Output the part of the replacement after the %.  */
	      o = variable_buffer_output (o, replace_percent,
					  replace_postpercent_len);
	    }
	}

      /* Output a space, but not if the replacement is "".  */
      if (fail || replace_prepercent_len > 0
	  || (replace_percent != 0 && len + replace_postpercent_len > 0))
	{
	  o = variable_buffer_output (o, " ", 1);
	  doneany = 1;
	}
    }
  if (doneany)
    /* Kill the last space.  */
    --o;

  return o;
}

/* Store into VARIABLE_BUFFER at O the result of scanning TEXT
   and replacing strings matching PATTERN with REPLACE.
   If PATTERN_PERCENT is not nil, PATTERN has already been
   run through find_percent, and PATTERN_PERCENT is the result.
   If REPLACE_PERCENT is not nil, REPLACE has already been
   run through find_percent, and REPLACE_PERCENT is the result.
   Note that we expect PATTERN_PERCENT and REPLACE_PERCENT to point to the
   character _AFTER_ the %, not to the % itself.
*/

char *
patsubst_expand (char *o, const char *text, char *pattern, char *replace)
{
  const char *pattern_percent = find_percent (pattern);
  const char *replace_percent = find_percent (replace);

  /* If there's a percent in the pattern or replacement skip it.  */
  if (replace_percent)
    ++replace_percent;
  if (pattern_percent)
    ++pattern_percent;

  return patsubst_expand_pat (o, text, pattern, replace,
                              pattern_percent, replace_percent);
}


/* Look up a function by name.  */

static const struct function_table_entry *
lookup_function (const char *s)
{
  const char *e = s;

  while (*e && ( (*e >= 'a' && *e <= 'z') || *e == '-'))
    e++;
  if (*e == '\0' || isblank ((unsigned char) *e))
    {
      struct function_table_entry function_table_entry_key;
      function_table_entry_key.name = s;
      function_table_entry_key.len = e - s;

      return hash_find_item (&function_table, &function_table_entry_key);
    }
  return 0;
}


/* Return 1 if PATTERN matches STR, 0 if not.  */

int
pattern_matches (const char *pattern, const char *percent, const char *str)
{
  unsigned int sfxlen, strlength;

  if (percent == 0)
    {
      unsigned int len = strlen (pattern) + 1;
      char *new_chars = alloca (len);
      memcpy (new_chars, pattern, len);
      percent = find_percent (new_chars);
      if (percent == 0)
	return streq (new_chars, str);
      pattern = new_chars;
    }

  sfxlen = strlen (percent + 1);
  strlength = strlen (str);

  if (strlength < (percent - pattern) + sfxlen
      || !strneq (pattern, str, percent - pattern))
    return 0;

  return !strcmp (percent + 1, str + (strlength - sfxlen));
}


/* Find the next comma or ENDPAREN (counting nested STARTPAREN and
   ENDPARENtheses), starting at PTR before END.  Return a pointer to
   next character.

   If no next argument is found, return NULL.
*/

static char *
find_next_argument (char startparen, char endparen,
                    const char *ptr, const char *end)
{
  int count = 0;

  for (; ptr < end; ++ptr)
    if (*ptr == startparen)
      ++count;

    else if (*ptr == endparen)
      {
	--count;
	if (count < 0)
	  return NULL;
      }

    else if (*ptr == ',' && !count)
      return (char *)ptr;

  /* We didn't find anything.  */
  return NULL;
}


/* Glob-expand LINE.  The returned pointer is
   only good until the next call to string_glob.  */

static char *
string_glob (char *line)
{
  static char *result = 0;
  static unsigned int length;
  struct nameseq *chain;
  unsigned int idx;

  chain = PARSE_FILE_SEQ (&line, struct nameseq, '\0', NULL,
                          /* We do not want parse_file_seq to strip './'s.
                             That would break examples like:
                             $(patsubst ./%.c,obj/%.o,$(wildcard ./?*.c)).  */
                          PARSEFS_NOSTRIP|PARSEFS_NOCACHE|PARSEFS_EXISTS);

  if (result == 0)
    {
      length = 100;
      result = xmalloc (100);
    }

  idx = 0;
  while (chain != 0)
    {
      struct nameseq *next = chain->next;
      unsigned int len = strlen (chain->name);

      if (idx + len + 1 > length)
        {
          length += (len + 1) * 2;
          result = xrealloc (result, length);
        }
      memcpy (&result[idx], chain->name, len);
      idx += len;
      result[idx++] = ' ';

      /* Because we used PARSEFS_NOCACHE above, we have to free() NAME.  */
      free ((char *)chain->name);
      free (chain);
      chain = next;
    }

  /* Kill the last space and terminate the string.  */
  if (idx == 0)
    result[0] = '\0';
  else
    result[idx - 1] = '\0';

  return result;
}

/*
  Builtin functions
 */

static char *
func_patsubst (char *o, char **argv, const char *funcname UNUSED)
{
  o = patsubst_expand (o, argv[2], argv[0], argv[1]);
  return o;
}


static char *
func_join (char *o, char **argv, const char *funcname UNUSED)
{
  int doneany = 0;

  /* Write each word of the first argument directly followed
     by the corresponding word of the second argument.
     If the two arguments have a different number of words,
     the excess words are just output separated by blanks.  */
  const char *tp;
  const char *pp;
  const char *list1_iterator = argv[0];
  const char *list2_iterator = argv[1];
  do
    {
      unsigned int len1, len2;

      tp = find_next_token (&list1_iterator, &len1);
      if (tp != 0)
	o = variable_buffer_output (o, tp, len1);

      pp = find_next_token (&list2_iterator, &len2);
      if (pp != 0)
	o = variable_buffer_output (o, pp, len2);

      if (tp != 0 || pp != 0)
	{
	  o = variable_buffer_output (o, " ", 1);
	  doneany = 1;
	}
    }
  while (tp != 0 || pp != 0);
  if (doneany)
    /* Kill the last blank.  */
    --o;

  return o;
}


static char *
func_origin (char *o, char **argv, const char *funcname UNUSED)
{
  /* Expand the argument.  */
  struct variable *v = lookup_variable (argv[0], strlen (argv[0]));
  if (v == 0)
    o = variable_buffer_output (o, "undefined", 9);
  else
    switch (v->origin)
      {
      default:
      case o_invalid:
	abort ();
	break;
      case o_default:
	o = variable_buffer_output (o, "default", 7);
	break;
      case o_env:
	o = variable_buffer_output (o, "environment", 11);
	break;
      case o_file:
	o = variable_buffer_output (o, "file", 4);
	break;
      case o_env_override:
	o = variable_buffer_output (o, "environment override", 20);
	break;
      case o_command:
	o = variable_buffer_output (o, "command line", 12);
	break;
      case o_override:
	o = variable_buffer_output (o, "override", 8);
	break;
      case o_automatic:
	o = variable_buffer_output (o, "automatic", 9);
	break;
      }

  return o;
}

static char *
func_flavor (char *o, char **argv, const char *funcname UNUSED)
{
  struct variable *v = lookup_variable (argv[0], strlen (argv[0]));

  if (v == 0)
    o = variable_buffer_output (o, "undefined", 9);
  else
    if (v->recursive)
      o = variable_buffer_output (o, "recursive", 9);
    else
      o = variable_buffer_output (o, "simple", 6);

  return o;
}

#ifdef VMS
# define IS_PATHSEP(c) ((c) == ']')
#else
# ifdef HAVE_DOS_PATHS
#  define IS_PATHSEP(c) ((c) == '/' || (c) == '\\')
# else
#  define IS_PATHSEP(c) ((c) == '/')
# endif
#endif


static char *
func_notdir_suffix (char *o, char **argv, const char *funcname)
{
  /* Expand the argument.  */
  const char *list_iterator = argv[0];
  const char *p2;
  int doneany =0;
  unsigned int len=0;

  int is_suffix = funcname[0] == 's';
  int is_notdir = !is_suffix;
  while ((p2 = find_next_token (&list_iterator, &len)) != 0)
    {
      const char *p = p2 + len;


      while (p >= p2 && (!is_suffix || *p != '.'))
	{
	  if (IS_PATHSEP (*p))
	    break;
	  --p;
	}

      if (p >= p2)
	{
	  if (is_notdir)
	    ++p;
	  else if (*p != '.')
	    continue;
	  o = variable_buffer_output (o, p, len - (p - p2));
	}
#ifdef HAVE_DOS_PATHS
      /* Handle the case of "d:foo/bar".  */
      else if (is_notdir && p2[0] && p2[1] == ':')
	{
	  p = p2 + 2;
	  o = variable_buffer_output (o, p, len - (p - p2));
	}
#endif
      else if (is_notdir)
	o = variable_buffer_output (o, p2, len);

      if (is_notdir || p >= p2)
	{
	  o = variable_buffer_output (o, " ", 1);
	  doneany = 1;
	}
    }

  if (doneany)
    /* Kill last space.  */
    --o;

  return o;
}


static char *
func_basename_dir (char *o, char **argv, const char *funcname)
{
  /* Expand the argument.  */
  const char *p3 = argv[0];
  const char *p2;
  int doneany = 0;
  unsigned int len = 0;

  int is_basename = funcname[0] == 'b';
  int is_dir = !is_basename;

  while ((p2 = find_next_token (&p3, &len)) != 0)
    {
      const char *p = p2 + len;
      while (p >= p2 && (!is_basename  || *p != '.'))
        {
          if (IS_PATHSEP (*p))
            break;
          --p;
        }

      if (p >= p2 && (is_dir))
        o = variable_buffer_output (o, p2, ++p - p2);
      else if (p >= p2 && (*p == '.'))
        o = variable_buffer_output (o, p2, p - p2);
#ifdef HAVE_DOS_PATHS
      /* Handle the "d:foobar" case */
      else if (p2[0] && p2[1] == ':' && is_dir)
        o = variable_buffer_output (o, p2, 2);
#endif
      else if (is_dir)
#ifdef VMS
        o = variable_buffer_output (o, "[]", 2);
#else
#ifndef _AMIGA
      o = variable_buffer_output (o, "./", 2);
#else
      ; /* Just a nop...  */
#endif /* AMIGA */
#endif /* !VMS */
      else
        /* The entire name is the basename.  */
        o = variable_buffer_output (o, p2, len);

      o = variable_buffer_output (o, " ", 1);
      doneany = 1;
    }

  if (doneany)
    /* Kill last space.  */
    --o;

  return o;
}

static char *
func_addsuffix_addprefix (char *o, char **argv, const char *funcname)
{
  int fixlen = strlen (argv[0]);
  const char *list_iterator = argv[1];
  int is_addprefix = funcname[3] == 'p';
  int is_addsuffix = !is_addprefix;

  int doneany = 0;
  const char *p;
  unsigned int len;

  while ((p = find_next_token (&list_iterator, &len)) != 0)
    {
      if (is_addprefix)
	o = variable_buffer_output (o, argv[0], fixlen);
      o = variable_buffer_output (o, p, len);
      if (is_addsuffix)
	o = variable_buffer_output (o, argv[0], fixlen);
      o = variable_buffer_output (o, " ", 1);
      doneany = 1;
    }

  if (doneany)
    /* Kill last space.  */
    --o;

  return o;
}

static char *
func_subst (char *o, char **argv, const char *funcname UNUSED)
{
  o = subst_expand (o, argv[2], argv[0], argv[1], strlen (argv[0]),
		    strlen (argv[1]), 0);

  return o;
}


static char *
func_firstword (char *o, char **argv, const char *funcname UNUSED)
{
  unsigned int i;
  const char *words = argv[0];    /* Use a temp variable for find_next_token */
  const char *p = find_next_token (&words, &i);

  if (p != 0)
    o = variable_buffer_output (o, p, i);

  return o;
}

static char *
func_lastword (char *o, char **argv, const char *funcname UNUSED)
{
  unsigned int i;
  const char *words = argv[0];    /* Use a temp variable for find_next_token */
  const char *p = NULL;
  const char *t;

  while ((t = find_next_token (&words, &i)))
    p = t;

  if (p != 0)
    o = variable_buffer_output (o, p, i);

  return o;
}

static char *
func_words (char *o, char **argv, const char *funcname UNUSED)
{
  int i = 0;
  const char *word_iterator = argv[0];
  char buf[20];

  while (find_next_token (&word_iterator, NULL) != 0)
    ++i;

  sprintf (buf, "%d", i);
  o = variable_buffer_output (o, buf, strlen (buf));

  return o;
}

/* Set begpp to point to the first non-whitespace character of the string,
 * and endpp to point to the last non-whitespace character of the string.
 * If the string is empty or contains nothing but whitespace, endpp will be
 * begpp-1.
 */
char *
strip_whitespace (const char **begpp, const char **endpp)
{
  while (*begpp <= *endpp && isspace ((unsigned char)**begpp))
    (*begpp) ++;
  while (*endpp >= *begpp && isspace ((unsigned char)**endpp))
    (*endpp) --;
  return (char *)*begpp;
}

static void
check_numeric (const char *s, const char *msg)
{
  const char *end = s + strlen (s) - 1;
  const char *beg = s;
  strip_whitespace (&s, &end);

  for (; s <= end; ++s)
    if (!ISDIGIT (*s))  /* ISDIGIT only evals its arg once: see make.h.  */
      break;

  if (s <= end || end - beg < 0)
    fatal (*expanding_var, "%s: '%s'", msg, beg);
}



static char *
func_word (char *o, char **argv, const char *funcname UNUSED)
{
  const char *end_p;
  const char *p;
  int i;

  /* Check the first argument.  */
  check_numeric (argv[0], _("non-numeric first argument to 'word' function"));
  i = atoi (argv[0]);

  if (i == 0)
    fatal (*expanding_var,
           _("first argument to 'word' function must be greater than 0"));

  end_p = argv[1];
  while ((p = find_next_token (&end_p, 0)) != 0)
    if (--i == 0)
      break;

  if (i == 0)
    o = variable_buffer_output (o, p, end_p - p);

  return o;
}

static char *
func_wordlist (char *o, char **argv, const char *funcname UNUSED)
{
  int start, count;

  /* Check the arguments.  */
  check_numeric (argv[0],
		 _("non-numeric first argument to 'wordlist' function"));
  check_numeric (argv[1],
		 _("non-numeric second argument to 'wordlist' function"));

  start = atoi (argv[0]);
  if (start < 1)
    fatal (*expanding_var,
           "invalid first argument to 'wordlist' function: '%d'", start);

  count = atoi (argv[1]) - start + 1;

  if (count > 0)
    {
      const char *p;
      const char *end_p = argv[2];

      /* Find the beginning of the "start"th word.  */
      while (((p = find_next_token (&end_p, 0)) != 0) && --start)
        ;

      if (p)
        {
          /* Find the end of the "count"th word from start.  */
          while (--count && (find_next_token (&end_p, 0) != 0))
            ;

          /* Return the stuff in the middle.  */
          o = variable_buffer_output (o, p, end_p - p);
        }
    }

  return o;
}

static char *
func_findstring (char *o, char **argv, const char *funcname UNUSED)
{
  /* Find the first occurrence of the first string in the second.  */
  if (strstr (argv[1], argv[0]) != 0)
    o = variable_buffer_output (o, argv[0], strlen (argv[0]));

  return o;
}

static char *
func_foreach (char *o, char **argv, const char *funcname UNUSED)
{
  /* expand only the first two.  */
  char *varname = expand_argument (argv[0], NULL);
  char *list = expand_argument (argv[1], NULL);
  const char *body = argv[2];

  int doneany = 0;
  const char *list_iterator = list;
  const char *p;
  unsigned int len;
  struct variable *var;

  push_new_variable_scope ();
  var = define_variable (varname, strlen (varname), "", o_automatic, 0);

  /* loop through LIST,  put the value in VAR and expand BODY */
  while ((p = find_next_token (&list_iterator, &len)) != 0)
    {
      char *result = 0;

      free (var->value);
      var->value = xstrndup (p, len);

      result = allocated_variable_expand (body);

      o = variable_buffer_output (o, result, strlen (result));
      o = variable_buffer_output (o, " ", 1);
      doneany = 1;
      free (result);
    }

  if (doneany)
    /* Kill the last space.  */
    --o;

  pop_variable_scope ();
  free (varname);
  free (list);

  return o;
}

struct a_word
{
  struct a_word *next;
  struct a_word *chain;
  char *str;
  int length;
  int matched;
};

static unsigned long
a_word_hash_1 (const void *key)
{
  return_STRING_HASH_1 (((struct a_word const *) key)->str);
}

static unsigned long
a_word_hash_2 (const void *key)
{
  return_STRING_HASH_2 (((struct a_word const *) key)->str);
}

static int
a_word_hash_cmp (const void *x, const void *y)
{
  int result = ((struct a_word const *) x)->length - ((struct a_word const *) y)->length;
  if (result)
    return result;
  return_STRING_COMPARE (((struct a_word const *) x)->str,
			 ((struct a_word const *) y)->str);
}

struct a_pattern
{
  struct a_pattern *next;
  char *str;
  char *percent;
  int length;
};

static char *
func_filter_filterout (char *o, char **argv, const char *funcname)
{
  struct a_word *wordhead;
  struct a_word **wordtail;
  struct a_word *wp;
  struct a_pattern *pathead;
  struct a_pattern **pattail;
  struct a_pattern *pp;

  struct hash_table a_word_table;
  int is_filter = funcname[CSTRLEN ("filter")] == '\0';
  const char *pat_iterator = argv[0];
  const char *word_iterator = argv[1];
  int literals = 0;
  int words = 0;
  int hashing = 0;
  char *p;
  unsigned int len;

  /* Chop ARGV[0] up into patterns to match against the words.
     We don't need to preserve it because our caller frees all the
     argument memory anyway.  */

  pattail = &pathead;
  while ((p = find_next_token (&pat_iterator, &len)) != 0)
    {
      struct a_pattern *pat = alloca (sizeof (struct a_pattern));

      *pattail = pat;
      pattail = &pat->next;

      if (*pat_iterator != '\0')
	++pat_iterator;

      pat->str = p;
      p[len] = '\0';
      pat->percent = find_percent (p);
      if (pat->percent == 0)
	literals++;

      /* find_percent() might shorten the string so LEN is wrong.  */
      pat->length = strlen (pat->str);
    }
  *pattail = 0;

  /* Chop ARGV[1] up into words to match against the patterns.  */

  wordtail = &wordhead;
  while ((p = find_next_token (&word_iterator, &len)) != 0)
    {
      struct a_word *word = alloca (sizeof (struct a_word));

      *wordtail = word;
      wordtail = &word->next;

      if (*word_iterator != '\0')
	++word_iterator;

      p[len] = '\0';
      word->str = p;
      word->length = len;
      word->matched = 0;
      word->chain = 0;
      words++;
    }
  *wordtail = 0;

  /* Only use a hash table if arg list lengths justifies the cost.  */
  hashing = (literals >= 2 && (literals * words) >= 10);
  if (hashing)
    {
      hash_init (&a_word_table, words, a_word_hash_1, a_word_hash_2,
                 a_word_hash_cmp);
      for (wp = wordhead; wp != 0; wp = wp->next)
	{
	  struct a_word *owp = hash_insert (&a_word_table, wp);
	  if (owp)
	    wp->chain = owp;
	}
    }

  if (words)
    {
      int doneany = 0;

      /* Run each pattern through the words, killing words.  */
      for (pp = pathead; pp != 0; pp = pp->next)
	{
	  if (pp->percent)
	    for (wp = wordhead; wp != 0; wp = wp->next)
	      wp->matched |= pattern_matches (pp->str, pp->percent, wp->str);
	  else if (hashing)
	    {
	      struct a_word a_word_key;
	      a_word_key.str = pp->str;
	      a_word_key.length = pp->length;
	      wp = hash_find_item (&a_word_table, &a_word_key);
	      while (wp)
		{
		  wp->matched |= 1;
		  wp = wp->chain;
		}
	    }
	  else
	    for (wp = wordhead; wp != 0; wp = wp->next)
	      wp->matched |= (wp->length == pp->length
			      && strneq (pp->str, wp->str, wp->length));
	}

      /* Output the words that matched (or didn't, for filter-out).  */
      for (wp = wordhead; wp != 0; wp = wp->next)
	if (is_filter ? wp->matched : !wp->matched)
	  {
	    o = variable_buffer_output (o, wp->str, strlen (wp->str));
	    o = variable_buffer_output (o, " ", 1);
	    doneany = 1;
	  }

      if (doneany)
	/* Kill the last space.  */
	--o;
    }

  if (hashing)
    hash_free (&a_word_table, 0);

  return o;
}


static char *
func_strip (char *o, char **argv, const char *funcname UNUSED)
{
  const char *p = argv[0];
  int doneany = 0;

  while (*p != '\0')
    {
      int i=0;
      const char *word_start;

      while (isspace ((unsigned char)*p))
	++p;
      word_start = p;
      for (i=0; *p != '\0' && !isspace ((unsigned char)*p); ++p, ++i)
	{}
      if (!i)
	break;
      o = variable_buffer_output (o, word_start, i);
      o = variable_buffer_output (o, " ", 1);
      doneany = 1;
    }

  if (doneany)
    /* Kill the last space.  */
    --o;

  return o;
}

/*
  Print a warning or fatal message.
*/
static char *
func_error (char *o, char **argv, const char *funcname)
{
  char **argvp;
  char *msg, *p;
  int len;

  /* The arguments will be broken on commas.  Rather than create yet
     another special case where function arguments aren't broken up,
     just create a format string that puts them back together.  */
  for (len=0, argvp=argv; *argvp != 0; ++argvp)
    len += strlen (*argvp) + 2;

  p = msg = alloca (len + 1);

  for (argvp=argv; argvp[1] != 0; ++argvp)
    {
      strcpy (p, *argvp);
      p += strlen (*argvp);
      *(p++) = ',';
      *(p++) = ' ';
    }
  strcpy (p, *argvp);

  switch (*funcname) {
    case 'e':
      fatal (reading_file, "%s", msg);

    case 'w':
      error (reading_file, "%s", msg);
      break;

    case 'i':
      printf ("%s\n", msg);
      fflush(stdout);
      break;

    default:
      fatal (*expanding_var, "Internal error: func_error: '%s'", funcname);
  }

  /* The warning function expands to the empty string.  */
  return o;
}


/*
  chop argv[0] into words, and sort them.
 */
static char *
func_sort (char *o, char **argv, const char *funcname UNUSED)
{
  const char *t;
  char **words;
  int wordi;
  char *p;
  unsigned int len;
  int i;

  /* Find the maximum number of words we'll have.  */
  t = argv[0];
  wordi = 0;
  while ((p = find_next_token (&t, NULL)) != 0)
    {
      ++t;
      ++wordi;
    }

  words = xmalloc ((wordi == 0 ? 1 : wordi) * sizeof (char *));

  /* Now assign pointers to each string in the array.  */
  t = argv[0];
  wordi = 0;
  while ((p = find_next_token (&t, &len)) != 0)
    {
      ++t;
      p[len] = '\0';
      words[wordi++] = p;
    }

  if (wordi)
    {
      /* Now sort the list of words.  */
      qsort (words, wordi, sizeof (char *), alpha_compare);

      /* Now write the sorted list, uniquified.  */
      for (i = 0; i < wordi; ++i)
        {
          len = strlen (words[i]);
          if (i == wordi - 1 || strlen (words[i + 1]) != len
              || strcmp (words[i], words[i + 1]))
            {
              o = variable_buffer_output (o, words[i], len);
              o = variable_buffer_output (o, " ", 1);
            }
        }

      /* Kill the last space.  */
      --o;
    }

  free (words);

  return o;
}

/*
  $(if condition,true-part[,false-part])

  CONDITION is false iff it evaluates to an empty string.  White
  space before and after condition are stripped before evaluation.

  If CONDITION is true, then TRUE-PART is evaluated, otherwise FALSE-PART is
  evaluated (if it exists).  Because only one of the two PARTs is evaluated,
  you can use $(if ...) to create side-effects (with $(shell ...), for
  example).
*/

static char *
func_if (char *o, char **argv, const char *funcname UNUSED)
{
  const char *begp = argv[0];
  const char *endp = begp + strlen (argv[0]) - 1;
  int result = 0;

  /* Find the result of the condition: if we have a value, and it's not
     empty, the condition is true.  If we don't have a value, or it's the
     empty string, then it's false.  */

  strip_whitespace (&begp, &endp);

  if (begp <= endp)
    {
      char *expansion = expand_argument (begp, endp+1);

      result = strlen (expansion);
      free (expansion);
    }

  /* If the result is true (1) we want to eval the first argument, and if
     it's false (0) we want to eval the second.  If the argument doesn't
     exist we do nothing, otherwise expand it and add to the buffer.  */

  argv += 1 + !result;

  if (*argv)
    {
      char *expansion = expand_argument (*argv, NULL);

      o = variable_buffer_output (o, expansion, strlen (expansion));

      free (expansion);
    }

  return o;
}

/*
  $(or condition1[,condition2[,condition3[...]]])

  A CONDITION is false iff it evaluates to an empty string.  White
  space before and after CONDITION are stripped before evaluation.

  CONDITION1 is evaluated.  If it's true, then this is the result of
  expansion.  If it's false, CONDITION2 is evaluated, and so on.  If none of
  the conditions are true, the expansion is the empty string.

  Once a CONDITION is true no further conditions are evaluated
  (short-circuiting).
*/

static char *
func_or (char *o, char **argv, const char *funcname UNUSED)
{
  for ( ; *argv ; ++argv)
    {
      const char *begp = *argv;
      const char *endp = begp + strlen (*argv) - 1;
      char *expansion;
      int result = 0;

      /* Find the result of the condition: if it's false keep going.  */

      strip_whitespace (&begp, &endp);

      if (begp > endp)
        continue;

      expansion = expand_argument (begp, endp+1);
      result = strlen (expansion);

      /* If the result is false keep going.  */
      if (!result)
        {
          free (expansion);
          continue;
        }

      /* It's true!  Keep this result and return.  */
      o = variable_buffer_output (o, expansion, result);
      free (expansion);
      break;
    }

  return o;
}

/*
  $(and condition1[,condition2[,condition3[...]]])

  A CONDITION is false iff it evaluates to an empty string.  White
  space before and after CONDITION are stripped before evaluation.

  CONDITION1 is evaluated.  If it's false, then this is the result of
  expansion.  If it's true, CONDITION2 is evaluated, and so on.  If all of
  the conditions are true, the expansion is the result of the last condition.

  Once a CONDITION is false no further conditions are evaluated
  (short-circuiting).
*/

static char *
func_and (char *o, char **argv, const char *funcname UNUSED)
{
  char *expansion;
  int result;

  while (1)
    {
      const char *begp = *argv;
      const char *endp = begp + strlen (*argv) - 1;

      /* An empty condition is always false.  */
      strip_whitespace (&begp, &endp);
      if (begp > endp)
        return o;

      expansion = expand_argument (begp, endp+1);
      result = strlen (expansion);

      /* If the result is false, stop here: we're done.  */
      if (!result)
        break;

      /* Otherwise the result is true.  If this is the last one, keep this
         result and quit.  Otherwise go on to the next one!  */

      if (*(++argv))
        free (expansion);
      else
        {
          o = variable_buffer_output (o, expansion, result);
          break;
        }
    }

  free (expansion);

  return o;
}

static char *
func_wildcard (char *o, char **argv, const char *funcname UNUSED)
{
#ifdef _AMIGA
   o = wildcard_expansion (argv[0], o);
#else
   char *p = string_glob (argv[0]);
   o = variable_buffer_output (o, p, strlen (p));
#endif
   return o;
}

/*
  $(eval <makefile string>)

  Always resolves to the empty string.

  Treat the arguments as a segment of makefile, and parse them.
*/

static char *
func_eval (char *o, char **argv, const char *funcname UNUSED)
{
  char *buf;
  unsigned int len;

  /* Eval the buffer.  Pop the current variable buffer setting so that the
     eval'd code can use its own without conflicting.  */

  install_variable_buffer (&buf, &len);

  eval_buffer (argv[0]);

  restore_variable_buffer (buf, len);

  return o;
}


static char *
func_value (char *o, char **argv, const char *funcname UNUSED)
{
  /* Look up the variable.  */
  struct variable *v = lookup_variable (argv[0], strlen (argv[0]));

  /* Copy its value into the output buffer without expanding it.  */
  if (v)
    o = variable_buffer_output (o, v->value, strlen(v->value));

  return o;
}

/*
  \r is replaced on UNIX as well. Is this desirable?
 */
static void
fold_newlines (char *buffer, unsigned int *length, int trim_newlines)
{
  char *dst = buffer;
  char *src = buffer;
  char *last_nonnl = buffer - 1;
  src[*length] = 0;
  for (; *src != '\0'; ++src)
    {
      if (src[0] == '\r' && src[1] == '\n')
	continue;
      if (*src == '\n')
	{
	  *dst++ = ' ';
	}
      else
	{
	  last_nonnl = dst;
	  *dst++ = *src;
	}
    }

  if (!trim_newlines && (last_nonnl < (dst - 2)))
    last_nonnl = dst - 2;

  *(++last_nonnl) = '\0';
  *length = last_nonnl - buffer;
}



int shell_function_pid = 0, shell_function_completed;


#ifdef WINDOWS32
/*untested*/

#include <windows.h>
#include <io.h>
#include "sub_proc.h"


void
windows32_openpipe (int *pipedes, pid_t *pid_p, char **command_argv, char **envp)
{
  SECURITY_ATTRIBUTES saAttr;
  HANDLE hIn = INVALID_HANDLE_VALUE;
  HANDLE hErr = INVALID_HANDLE_VALUE;
  HANDLE hChildOutRd;
  HANDLE hChildOutWr;
  HANDLE hProcess, tmpIn, tmpErr;
  DWORD e;

  saAttr.nLength = sizeof (SECURITY_ATTRIBUTES);
  saAttr.bInheritHandle = TRUE;
  saAttr.lpSecurityDescriptor = NULL;

  /* Standard handles returned by GetStdHandle can be NULL or
     INVALID_HANDLE_VALUE if the parent process closed them.  If that
     happens, we open the null device and pass its handle to
     process_begin below as the corresponding handle to inherit.  */
  tmpIn = GetStdHandle(STD_INPUT_HANDLE);
  if (DuplicateHandle (GetCurrentProcess(),
		      tmpIn,
		      GetCurrentProcess(),
		      &hIn,
		      0,
		      TRUE,
		      DUPLICATE_SAME_ACCESS) == FALSE) {
    if ((e = GetLastError()) == ERROR_INVALID_HANDLE) {
      tmpIn = CreateFile("NUL", GENERIC_READ,
			 FILE_SHARE_READ | FILE_SHARE_WRITE, NULL,
			 OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
      if (tmpIn != INVALID_HANDLE_VALUE
	  && DuplicateHandle(GetCurrentProcess(),
			     tmpIn,
			     GetCurrentProcess(),
			     &hIn,
			     0,
			     TRUE,
			     DUPLICATE_SAME_ACCESS) == FALSE)
	CloseHandle(tmpIn);
    }
    if (hIn == INVALID_HANDLE_VALUE)
      fatal (NILF, _("windows32_openpipe: DuplicateHandle(In) failed (e=%ld)\n"), e);
  }
  tmpErr = GetStdHandle(STD_ERROR_HANDLE);
  if (DuplicateHandle(GetCurrentProcess(),
		      tmpErr,
		      GetCurrentProcess(),
		      &hErr,
		      0,
		      TRUE,
		      DUPLICATE_SAME_ACCESS) == FALSE) {
    if ((e = GetLastError()) == ERROR_INVALID_HANDLE) {
      tmpErr = CreateFile("NUL", GENERIC_WRITE,
			  FILE_SHARE_READ | FILE_SHARE_WRITE, NULL,
			  OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
      if (tmpErr != INVALID_HANDLE_VALUE
	  && DuplicateHandle(GetCurrentProcess(),
			     tmpErr,
			     GetCurrentProcess(),
			     &hErr,
			     0,
			     TRUE,
			     DUPLICATE_SAME_ACCESS) == FALSE)
	CloseHandle(tmpErr);
    }
    if (hErr == INVALID_HANDLE_VALUE)
      fatal (NILF, _("windows32_openpipe: DuplicateHandle(Err) failed (e=%ld)\n"), e);
  }

  if (!CreatePipe(&hChildOutRd, &hChildOutWr, &saAttr, 0))
    fatal (NILF, _("CreatePipe() failed (e=%ld)\n"), GetLastError());

  hProcess = process_init_fd(hIn, hChildOutWr, hErr);

  if (!hProcess)
    fatal (NILF, _("windows32_openpipe(): process_init_fd() failed\n"));

  /* make sure that CreateProcess() has Path it needs */
  sync_Path_environment();
  /* 'sync_Path_environment' may realloc 'environ', so take note of
     the new value.  */
  envp = environ;

  if (!process_begin(hProcess, command_argv, envp, command_argv[0], NULL)) {
    /* register process for wait */
	process_register(hProcess);

    /* set the pid for returning to caller */
	*pid_p = (pid_t) hProcess;

    /* set up to read data from child */
	pipedes[0] = _open_osfhandle((intptr_t) hChildOutRd, O_RDONLY);

    /* this will be closed almost right away */
	pipedes[1] = _open_osfhandle((intptr_t) hChildOutWr, O_APPEND);
  } else {
    /* reap/cleanup the failed process */
	process_cleanup(hProcess);

    /* close handles which were duplicated, they weren't used */
	if (hIn != INVALID_HANDLE_VALUE)
	  CloseHandle(hIn);
	if (hErr != INVALID_HANDLE_VALUE)
	  CloseHandle(hErr);

	/* close pipe handles, they won't be used */
	CloseHandle(hChildOutRd);
	CloseHandle(hChildOutWr);

    /* set status for return */
    pipedes[0] = pipedes[1] = -1;
    *pid_p = (pid_t)-1;
  }
}
#endif


#ifdef __MSDOS__
FILE *
msdos_openpipe (int* pipedes, int *pidp, char *text)
{
  FILE *fpipe=0;
  /* MSDOS can't fork, but it has 'popen'.  */
  struct variable *sh = lookup_variable ("SHELL", 5);
  int e;
  extern int dos_command_running, dos_status;

  /* Make sure not to bother processing an empty line.  */
  while (isblank ((unsigned char)*text))
    ++text;
  if (*text == '\0')
    return 0;

  if (sh)
    {
      char buf[PATH_MAX + 7];
      /* This makes sure $SHELL value is used by $(shell), even
	 though the target environment is not passed to it.  */
      sprintf (buf, "SHELL=%s", sh->value);
      putenv (buf);
    }

  e = errno;
  errno = 0;
  dos_command_running = 1;
  dos_status = 0;
  /* If dos_status becomes non-zero, it means the child process
     was interrupted by a signal, like SIGINT or SIGQUIT.  See
     fatal_error_signal in commands.c.  */
  fpipe = popen (text, "rt");
  dos_command_running = 0;
  if (!fpipe || dos_status)
    {
      pipedes[0] = -1;
      *pidp = -1;
      if (dos_status)
	errno = EINTR;
      else if (errno == 0)
	errno = ENOMEM;
      shell_function_completed = -1;
    }
  else
    {
      pipedes[0] = fileno (fpipe);
      *pidp = 42; /* Yes, the Meaning of Life, the Universe, and Everything! */
      errno = e;
      shell_function_completed = 1;
    }
  return fpipe;
}
#endif

/*
  Do shell spawning, with the naughty bits for different OSes.
 */

#ifdef VMS

/* VMS can't do $(shell ...)  */

char *
func_shell_base (char *o, char **argv, int trim_newlines)
{
  fprintf (stderr, "This platform does not support shell\n");
  die (EXIT_FAILURE);
}

#define func_shell 0

#else
#ifndef _AMIGA
char *
func_shell_base (char *o, char **argv, int trim_newlines)
{
  char *batch_filename = NULL;

#ifdef __MSDOS__
  FILE *fpipe;
#endif
  char **command_argv;
  const char *error_prefix;
  char **envp;
  int pipedes[2];
  pid_t pid;

#ifndef __MSDOS__
#ifdef WINDOWS32
  /* Reset just_print_flag.  This is needed on Windows when batch files
     are used to run the commands, because we normally refrain from
     creating batch files under -n.  */
  int j_p_f = just_print_flag;

  just_print_flag = 0;
#endif
  /* Construct the argument list.  */
  command_argv = construct_command_argv (argv[0], NULL, NULL, 0,
                                         &batch_filename);
  if (command_argv == 0)
    {
#ifdef WINDOWS32
      just_print_flag = j_p_f;
#endif
      return o;
    }
#endif

  /* Using a target environment for 'shell' loses in cases like:
     export var = $(shell echo foobie)
     because target_environment hits a loop trying to expand $(var)
     to put it in the environment.  This is even more confusing when
     var was not explicitly exported, but just appeared in the
     calling environment.

     See Savannah bug #10593.

  envp = target_environment (NILF);
  */

  envp = environ;

  /* For error messages.  */
  if (reading_file && reading_file->filenm)
    {
      char *p = alloca (strlen (reading_file->filenm)+11+4);
      sprintf (p, "%s:%lu: ", reading_file->filenm, reading_file->lineno);
      error_prefix = p;
    }
  else
    error_prefix = "";

#if defined(__MSDOS__)
  fpipe = msdos_openpipe (pipedes, &pid, argv[0]);
  if (pipedes[0] < 0)
    {
      perror_with_name (error_prefix, "pipe");
      return o;
    }
#elif defined(WINDOWS32)
  windows32_openpipe (pipedes, &pid, command_argv, envp);
  /* Restore the value of just_print_flag.  */
  just_print_flag = j_p_f;

  if (pipedes[0] < 0)
    {
      /* Open of the pipe failed, mark as failed execution.  */
      shell_function_completed = -1;
      return o;
    }
  else
#else
  if (pipe (pipedes) < 0)
    {
      perror_with_name (error_prefix, "pipe");
      return o;
    }

# ifdef __EMX__
  /* close some handles that are unnecessary for the child process */
  CLOSE_ON_EXEC(pipedes[1]);
  CLOSE_ON_EXEC(pipedes[0]);
  /* Never use fork()/exec() here! Use spawn() instead in exec_command() */
  pid = child_execute_job (0, pipedes[1], command_argv, envp);
  if (pid < 0)
    perror_with_name (error_prefix, "spawn");
# else /* ! __EMX__ */
  pid = fork ();
  if (pid < 0)
    perror_with_name (error_prefix, "fork");
  else if (pid == 0)
    child_execute_job (0, pipedes[1], command_argv, envp);
  else
# endif
#endif
    {
      /* We are the parent.  */
      char *buffer;
      unsigned int maxlen, i;
      int cc;

      /* Record the PID for reap_children.  */
      shell_function_pid = pid;
#ifndef  __MSDOS__
      shell_function_completed = 0;

      /* Free the storage only the child needed.  */
      free (command_argv[0]);
      free (command_argv);

      /* Close the write side of the pipe.  We test for -1, since
	 pipedes[1] is -1 on MS-Windows, and some versions of MS
	 libraries barf when 'close' is called with -1.  */
      if (pipedes[1] >= 0)
	close (pipedes[1]);
#endif

      /* Set up and read from the pipe.  */

      maxlen = 200;
      buffer = xmalloc (maxlen + 1);

      /* Read from the pipe until it gets EOF.  */
      for (i = 0; ; i += cc)
	{
	  if (i == maxlen)
	    {
	      maxlen += 512;
	      buffer = xrealloc (buffer, maxlen + 1);
	    }

	  EINTRLOOP (cc, read (pipedes[0], &buffer[i], maxlen - i));
	  if (cc <= 0)
	    break;
	}
      buffer[i] = '\0';

      /* Close the read side of the pipe.  */
#ifdef  __MSDOS__
      if (fpipe)
	(void) pclose (fpipe);
#else
      (void) close (pipedes[0]);
#endif

      /* Loop until child_handler or reap_children()  sets
         shell_function_completed to the status of our child shell.  */
      while (shell_function_completed == 0)
	reap_children (1, 0);

      if (batch_filename) {
	DB (DB_VERBOSE, (_("Cleaning up temporary batch file %s\n"),
                       batch_filename));
	remove (batch_filename);
	free (batch_filename);
      }
      shell_function_pid = 0;

      /* The child_handler function will set shell_function_completed
	 to 1 when the child dies normally, or to -1 if it
	 dies with status 127, which is most likely an exec fail.  */

      if (shell_function_completed == -1)
	{
	  /* This likely means that the execvp failed, so we should just
	     write the error message in the pipe from the child.  */
	  fputs (buffer, stderr);
	  fflush (stderr);
	}
      else
	{
	  /* The child finished normally.  Replace all newlines in its output
	     with spaces, and put that in the variable output buffer.  */
	  fold_newlines (buffer, &i, trim_newlines);
	  o = variable_buffer_output (o, buffer, i);
	}

      free (buffer);
    }

  return o;
}

#else	/* _AMIGA */

/* Do the Amiga version of func_shell.  */

char *
func_shell_base (char *o, char **argv, int trim_newlines)
{
  /* Amiga can't fork nor spawn, but I can start a program with
     redirection of my choice.  However, this means that we
     don't have an opportunity to reopen stdout to trap it.  Thus,
     we save our own stdout onto a new descriptor and dup a temp
     file's descriptor onto our stdout temporarily.  After we
     spawn the shell program, we dup our own stdout back to the
     stdout descriptor.  The buffer reading is the same as above,
     except that we're now reading from a file.  */

#include <dos/dos.h>
#include <proto/dos.h>

  BPTR child_stdout;
  char tmp_output[FILENAME_MAX];
  unsigned int maxlen = 200, i;
  int cc;
  char * buffer, * ptr;
  char ** aptr;
  int len = 0;
  char* batch_filename = NULL;

  /* Construct the argument list.  */
  command_argv = construct_command_argv (argv[0], NULL, NULL, 0,
                                         &batch_filename);
  if (command_argv == 0)
    return o;

  /* Note the mktemp() is a security hole, but this only runs on Amiga.
     Ideally we would use main.c:open_tmpfile(), but this uses a special
     Open(), not fopen(), and I'm not familiar enough with the code to mess
     with it.  */
  strcpy (tmp_output, "t:MakeshXXXXXXXX");
  mktemp (tmp_output);
  child_stdout = Open (tmp_output, MODE_NEWFILE);

  for (aptr=command_argv; *aptr; aptr++)
    len += strlen (*aptr) + 1;

  buffer = xmalloc (len + 1);
  ptr = buffer;

  for (aptr=command_argv; *aptr; aptr++)
    {
      strcpy (ptr, *aptr);
      ptr += strlen (ptr) + 1;
      *ptr ++ = ' ';
      *ptr = 0;
    }

  ptr[-1] = '\n';

  Execute (buffer, NULL, child_stdout);
  free (buffer);

  Close (child_stdout);

  child_stdout = Open (tmp_output, MODE_OLDFILE);

  buffer = xmalloc (maxlen);
  i = 0;
  do
    {
      if (i == maxlen)
	{
	  maxlen += 512;
	  buffer = xrealloc (buffer, maxlen + 1);
	}

      cc = Read (child_stdout, &buffer[i], maxlen - i);
      if (cc > 0)
	i += cc;
    } while (cc > 0);

  Close (child_stdout);

  fold_newlines (buffer, &i, trim_newlines);
  o = variable_buffer_output (o, buffer, i);
  free (buffer);
  return o;
}
#endif  /* _AMIGA */

char *
func_shell (char *o, char **argv, const char *funcname UNUSED)
{
  return func_shell_base (o, argv, 1);
}
#endif  /* !VMS */

#ifdef EXPERIMENTAL

/*
  equality. Return is string-boolean, ie, the empty string is false.
 */
static char *
func_eq (char *o, char **argv, char *funcname UNUSED)
{
  int result = ! strcmp (argv[0], argv[1]);
  o = variable_buffer_output (o,  result ? "1" : "", result);
  return o;
}


/*
  string-boolean not operator.
 */
static char *
func_not (char *o, char **argv, char *funcname UNUSED)
{
  const char *s = argv[0];
  int result = 0;
  while (isspace ((unsigned char)*s))
    s++;
  result = ! (*s);
  o = variable_buffer_output (o,  result ? "1" : "", result);
  return o;
}
#endif


#ifdef HAVE_DOS_PATHS
#define IS_ABSOLUTE(n) (n[0] && n[1] == ':')
#define ROOT_LEN 3
#else
#define IS_ABSOLUTE(n) (n[0] == '/')
#define ROOT_LEN 1
#endif

/* Return the absolute name of file NAME which does not contain any '.',
   '..' components nor any repeated path separators ('/').   */

static char *
abspath (const char *name, char *apath)
{
  char *dest;
  const char *start, *end, *apath_limit;
  unsigned long root_len = ROOT_LEN;

  if (name[0] == '\0' || apath == NULL)
    return NULL;

  apath_limit = apath + GET_PATH_MAX;

  if (!IS_ABSOLUTE(name))
    {
      /* It is unlikely we would make it until here but just to make sure. */
      if (!starting_directory)
	return NULL;

      strcpy (apath, starting_directory);

#ifdef HAVE_DOS_PATHS
      if (IS_PATHSEP(name[0]))
	{
	  if (IS_PATHSEP(name[1]))
	    {
	      /* A UNC.  Don't prepend a drive letter.  */
	      apath[0] = name[0];
	      apath[1] = name[1];
	      root_len = 2;
	    }
	  /* We have /foo, an absolute file name except for the drive
	     letter.  Assume the missing drive letter is the current
	     drive, which we can get if we remove from starting_directory
	     everything past the root directory.  */
	  apath[root_len] = '\0';
	}
#endif

      dest = strchr (apath, '\0');
    }
  else
    {
      strncpy (apath, name, root_len);
      apath[root_len] = '\0';
      dest = apath + root_len;
      /* Get past the root, since we already copied it.  */
      name += root_len;
#ifdef HAVE_DOS_PATHS
      if (!IS_PATHSEP(apath[2]))
	{
	  /* Convert d:foo into d:./foo and increase root_len.  */
	  apath[2] = '.';
	  apath[3] = '/';
	  dest++;
	  root_len++;
	  /* strncpy above copied one character too many.  */
	  name--;
	}
      else
	apath[2] = '/';	/* make sure it's a forward slash */
#endif
    }

  for (start = end = name; *start != '\0'; start = end)
    {
      unsigned long len;

      /* Skip sequence of multiple path-separators.  */
      while (IS_PATHSEP(*start))
	++start;

      /* Find end of path component.  */
      for (end = start; *end != '\0' && !IS_PATHSEP(*end); ++end)
        ;

      len = end - start;

      if (len == 0)
	break;
      else if (len == 1 && start[0] == '.')
	/* nothing */;
      else if (len == 2 && start[0] == '.' && start[1] == '.')
	{
	  /* Back up to previous component, ignore if at root already.  */
	  if (dest > apath + root_len)
	    for (--dest; !IS_PATHSEP(dest[-1]); --dest);
	}
      else
	{
	  if (!IS_PATHSEP(dest[-1]))
            *dest++ = '/';

	  if (dest + len >= apath_limit)
            return NULL;

	  dest = memcpy (dest, start, len);
          dest += len;
	  *dest = '\0';
	}
    }

  /* Unless it is root strip trailing separator.  */
  if (dest > apath + root_len && IS_PATHSEP(dest[-1]))
    --dest;

  *dest = '\0';

  return apath;
}


static char *
func_realpath (char *o, char **argv, const char *funcname UNUSED)
{
  /* Expand the argument.  */
  const char *p = argv[0];
  const char *path = 0;
  int doneany = 0;
  unsigned int len = 0;
#ifndef HAVE_REALPATH
  struct stat st;
#endif
  PATH_VAR (in);
  PATH_VAR (out);

  while ((path = find_next_token (&p, &len)) != 0)
    {
      if (len < GET_PATH_MAX)
        {
          strncpy (in, path, len);
          in[len] = '\0';

          if (
#ifdef HAVE_REALPATH
              realpath (in, out)
#else
              abspath (in, out) && stat (out, &st) == 0
#endif
             )
            {
              o = variable_buffer_output (o, out, strlen (out));
              o = variable_buffer_output (o, " ", 1);
              doneany = 1;
            }
        }
    }

  /* Kill last space.  */
  if (doneany)
    --o;

  return o;
}

static char *
func_file (char *o, char **argv, const char *funcname UNUSED)
{
  char *fn = argv[0];

  if (fn[0] == '>')
    {
      FILE *fp;
      const char *mode = "w";

      /* We are writing a file.  */
      ++fn;
      if (fn[0] == '>')
        {
          mode = "a";
          ++fn;
        }
      fn = next_token (fn);

      fp = fopen (fn, mode);
      if (fp == NULL)
        fatal (reading_file, _("open: %s: %s"), fn, strerror (errno));
      else
        {
          int l = strlen (argv[1]);
          int nl = (l == 0 || argv[1][l-1] != '\n');

          if (fputs (argv[1], fp) == EOF || (nl && fputc('\n', fp) == EOF))
            fatal (reading_file, _("write: %s: %s"), fn, strerror (errno));

          fclose (fp);
        }
    }
  else
    fatal (reading_file, _("Invalid file operation: %s"), fn);

  return o;
}

static char *
func_abspath (char *o, char **argv, const char *funcname UNUSED)
{
  /* Expand the argument.  */
  const char *p = argv[0];
  const char *path = 0;
  int doneany = 0;
  unsigned int len = 0;
  PATH_VAR (in);
  PATH_VAR (out);

  while ((path = find_next_token (&p, &len)) != 0)
    {
      if (len < GET_PATH_MAX)
        {
          strncpy (in, path, len);
          in[len] = '\0';

          if (abspath (in, out))
            {
              o = variable_buffer_output (o, out, strlen (out));
              o = variable_buffer_output (o, " ", 1);
              doneany = 1;
            }
        }
    }

  /* Kill last space.  */
  if (doneany)
    --o;

  return o;
}

/* Lookup table for builtin functions.

   This doesn't have to be sorted; we use a straight lookup.  We might gain
   some efficiency by moving most often used functions to the start of the
   table.

   If MAXIMUM_ARGS is 0, that means there is no maximum and all
   comma-separated values are treated as arguments.

   EXPAND_ARGS means that all arguments should be expanded before invocation.
   Functions that do namespace tricks (foreach) don't automatically expand.  */

static char *func_call (char *o, char **argv, const char *funcname);


static struct function_table_entry function_table_init[] =
{
 /* Name/size */                    /* MIN MAX EXP? Function */
  { STRING_SIZE_TUPLE("abspath"),       0,  1,  1,  func_abspath},
  { STRING_SIZE_TUPLE("addprefix"),     2,  2,  1,  func_addsuffix_addprefix},
  { STRING_SIZE_TUPLE("addsuffix"),     2,  2,  1,  func_addsuffix_addprefix},
  { STRING_SIZE_TUPLE("basename"),      0,  1,  1,  func_basename_dir},
  { STRING_SIZE_TUPLE("dir"),           0,  1,  1,  func_basename_dir},
  { STRING_SIZE_TUPLE("notdir"),        0,  1,  1,  func_notdir_suffix},
  { STRING_SIZE_TUPLE("subst"),         3,  3,  1,  func_subst},
  { STRING_SIZE_TUPLE("suffix"),        0,  1,  1,  func_notdir_suffix},
  { STRING_SIZE_TUPLE("filter"),        2,  2,  1,  func_filter_filterout},
  { STRING_SIZE_TUPLE("filter-out"),    2,  2,  1,  func_filter_filterout},
  { STRING_SIZE_TUPLE("findstring"),    2,  2,  1,  func_findstring},
  { STRING_SIZE_TUPLE("firstword"),     0,  1,  1,  func_firstword},
  { STRING_SIZE_TUPLE("flavor"),        0,  1,  1,  func_flavor},
  { STRING_SIZE_TUPLE("join"),          2,  2,  1,  func_join},
  { STRING_SIZE_TUPLE("lastword"),      0,  1,  1,  func_lastword},
  { STRING_SIZE_TUPLE("patsubst"),      3,  3,  1,  func_patsubst},
  { STRING_SIZE_TUPLE("realpath"),      0,  1,  1,  func_realpath},
  { STRING_SIZE_TUPLE("shell"),         0,  1,  1,  func_shell},
  { STRING_SIZE_TUPLE("sort"),          0,  1,  1,  func_sort},
  { STRING_SIZE_TUPLE("strip"),         0,  1,  1,  func_strip},
  { STRING_SIZE_TUPLE("wildcard"),      0,  1,  1,  func_wildcard},
  { STRING_SIZE_TUPLE("word"),          2,  2,  1,  func_word},
  { STRING_SIZE_TUPLE("wordlist"),      3,  3,  1,  func_wordlist},
  { STRING_SIZE_TUPLE("words"),         0,  1,  1,  func_words},
  { STRING_SIZE_TUPLE("origin"),        0,  1,  1,  func_origin},
  { STRING_SIZE_TUPLE("foreach"),       3,  3,  0,  func_foreach},
  { STRING_SIZE_TUPLE("call"),          1,  0,  1,  func_call},
  { STRING_SIZE_TUPLE("info"),          0,  1,  1,  func_error},
  { STRING_SIZE_TUPLE("error"),         0,  1,  1,  func_error},
  { STRING_SIZE_TUPLE("warning"),       0,  1,  1,  func_error},
  { STRING_SIZE_TUPLE("if"),            2,  3,  0,  func_if},
  { STRING_SIZE_TUPLE("or"),            1,  0,  0,  func_or},
  { STRING_SIZE_TUPLE("and"),           1,  0,  0,  func_and},
  { STRING_SIZE_TUPLE("value"),         0,  1,  1,  func_value},
  { STRING_SIZE_TUPLE("eval"),          0,  1,  1,  func_eval},
  { STRING_SIZE_TUPLE("file"),          1,  2,  1,  func_file},
#ifdef EXPERIMENTAL
  { STRING_SIZE_TUPLE("eq"),            2,  2,  1,  func_eq},
  { STRING_SIZE_TUPLE("not"),           0,  1,  1,  func_not},
#endif
};

#define FUNCTION_TABLE_ENTRIES (sizeof (function_table_init) / sizeof (struct function_table_entry))


/* These must come after the definition of function_table.  */

static char *
expand_builtin_function (char *o, int argc, char **argv,
                         const struct function_table_entry *entry_p)
{
  if (argc < (int)entry_p->minimum_args)
    fatal (*expanding_var,
           _("insufficient number of arguments (%d) to function '%s'"),
           argc, entry_p->name);

  /* I suppose technically some function could do something with no
     arguments, but so far none do, so just test it for all functions here
     rather than in each one.  We can change it later if necessary.  */

  if (!argc)
    return o;

  if (!entry_p->func_ptr)
    fatal (*expanding_var,
           _("unimplemented on this platform: function '%s'"), entry_p->name);

  return entry_p->func_ptr (o, argv, entry_p->name);
}

/* Check for a function invocation in *STRINGP.  *STRINGP points at the
   opening ( or { and is not null-terminated.  If a function invocation
   is found, expand it into the buffer at *OP, updating *OP, incrementing
   *STRINGP past the reference and returning nonzero.  If not, return zero.  */

int
handle_function (char **op, const char **stringp)
{
  const struct function_table_entry *entry_p;
  char openparen = (*stringp)[0];
  char closeparen = openparen == '(' ? ')' : '}';
  const char *beg;
  const char *end;
  int count = 0;
  char *abeg = NULL;
  char **argv, **argvp;
  int nargs;

  beg = *stringp + 1;

  entry_p = lookup_function (beg);

  if (!entry_p)
    return 0;

  /* We found a builtin function.  Find the beginning of its arguments (skip
     whitespace after the name).  */

  beg = next_token (beg + entry_p->len);

  /* Find the end of the function invocation, counting nested use of
     whichever kind of parens we use.  Since we're looking, count commas
     to get a rough estimate of how many arguments we might have.  The
     count might be high, but it'll never be low.  */

  for (nargs=1, end=beg; *end != '\0'; ++end)
    if (*end == ',')
      ++nargs;
    else if (*end == openparen)
      ++count;
    else if (*end == closeparen && --count < 0)
      break;

  if (count >= 0)
    fatal (*expanding_var,
	   _("unterminated call to function '%s': missing '%c'"),
	   entry_p->name, closeparen);

  *stringp = end;

  /* Get some memory to store the arg pointers.  */
  argvp = argv = alloca (sizeof (char *) * (nargs + 2));

  /* Chop the string into arguments, then a nul.  As soon as we hit
     MAXIMUM_ARGS (if it's >0) assume the rest of the string is part of the
     last argument.

     If we're expanding, store pointers to the expansion of each one.  If
     not, make a duplicate of the string and point into that, nul-terminating
     each argument.  */

  if (entry_p->expand_args)
    {
      const char *p;
      for (p=beg, nargs=0; p <= end; ++argvp)
        {
          const char *next;

          ++nargs;

          if (nargs == entry_p->maximum_args
              || (! (next = find_next_argument (openparen, closeparen, p, end))))
            next = end;

          *argvp = expand_argument (p, next);
          p = next + 1;
        }
    }
  else
    {
      int len = end - beg;
      char *p, *aend;

      abeg = xmalloc (len+1);
      memcpy (abeg, beg, len);
      abeg[len] = '\0';
      aend = abeg + len;

      for (p=abeg, nargs=0; p <= aend; ++argvp)
        {
          char *next;

          ++nargs;

          if (nargs == entry_p->maximum_args
              || (! (next = find_next_argument (openparen, closeparen, p, aend))))
            next = aend;

          *argvp = p;
          *next = '\0';
          p = next + 1;
        }
    }
  *argvp = NULL;

  /* Finally!  Run the function...  */
  *op = expand_builtin_function (*op, nargs, argv, entry_p);

  /* Free memory.  */
  if (entry_p->expand_args)
    for (argvp=argv; *argvp != 0; ++argvp)
      free (*argvp);
  else if (abeg)
    free (abeg);

  return 1;
}


/* User-defined functions.  Expand the first argument as either a builtin
   function or a make variable, in the context of the rest of the arguments
   assigned to $1, $2, ... $N.  $0 is the name of the function.  */

static char *
func_call (char *o, char **argv, const char *funcname UNUSED)
{
  static int max_args = 0;
  char *fname;
  char *cp;
  char *body;
  int flen;
  int i;
  int saved_args;
  const struct function_table_entry *entry_p;
  struct variable *v;

  /* There is no way to define a variable with a space in the name, so strip
     leading and trailing whitespace as a favor to the user.  */
  fname = argv[0];
  while (*fname != '\0' && isspace ((unsigned char)*fname))
    ++fname;

  cp = fname + strlen (fname) - 1;
  while (cp > fname && isspace ((unsigned char)*cp))
    --cp;
  cp[1] = '\0';

  /* Calling nothing is a no-op */
  if (*fname == '\0')
    return o;

  /* Are we invoking a builtin function?  */

  entry_p = lookup_function (fname);
  if (entry_p)
    {
      /* How many arguments do we have?  */
      for (i=0; argv[i+1]; ++i)
        ;
      return expand_builtin_function (o, i, argv+1, entry_p);
    }

  /* Not a builtin, so the first argument is the name of a variable to be
     expanded and interpreted as a function.  Find it.  */
  flen = strlen (fname);

  v = lookup_variable (fname, flen);

  if (v == 0)
    warn_undefined (fname, flen);

  if (v == 0 || *v->value == '\0')
    return o;

  body = alloca (flen + 4);
  body[0] = '$';
  body[1] = '(';
  memcpy (body + 2, fname, flen);
  body[flen+2] = ')';
  body[flen+3] = '\0';

  /* Set up arguments $(1) .. $(N).  $(0) is the function name.  */

  push_new_variable_scope ();

  for (i=0; *argv; ++i, ++argv)
    {
      char num[11];

      sprintf (num, "%d", i);
      define_variable (num, strlen (num), *argv, o_automatic, 0);
    }

  /* If the number of arguments we have is < max_args, it means we're inside
     a recursive invocation of $(call ...).  Fill in the remaining arguments
     in the new scope with the empty value, to hide them from this
     invocation.  */

  for (; i < max_args; ++i)
    {
      char num[11];

      sprintf (num, "%d", i);
      define_variable (num, strlen (num), "", o_automatic, 0);
    }

  /* Expand the body in the context of the arguments, adding the result to
     the variable buffer.  */

  v->exp_count = EXP_COUNT_MAX;

  saved_args = max_args;
  max_args = i;
  o = variable_expand_string (o, body, flen+3);
  max_args = saved_args;

  v->exp_count = 0;

  pop_variable_scope ();

  return o + strlen (o);
}

void
define_new_function(const struct floc *flocp,
                    const char *name, int min, int max, int expand,
                    char *(*func)(char *, char **, const char *))
{
  size_t len = strlen (name);
  struct function_table_entry *ent = xmalloc (sizeof (struct function_table_entry));

  if (len > 255)
    fatal (flocp, _("Function name too long: %s\n"), name);
  if (min < 0 || min > 255)
    fatal (flocp, _("Invalid minimum argument count (%d) for function %s\n"),
           min, name);
  if (max < 0 || max > 255 || max < min)
    fatal (flocp, _("Invalid maximum argument count (%d) for function %s\n"),
           max, name);

  ent->name = name;
  ent->len = len;
  ent->minimum_args = min;
  ent->maximum_args = max;
  ent->expand_args = expand ? 1 : 0;
  ent->func_ptr = func;

  hash_insert (&function_table, ent);
}

void
hash_init_function_table (void)
{
  hash_init (&function_table, FUNCTION_TABLE_ENTRIES * 2,
	     function_table_entry_hash_1, function_table_entry_hash_2,
	     function_table_entry_hash_cmp);
  hash_load (&function_table, function_table_init,
	     FUNCTION_TABLE_ENTRIES, sizeof (struct function_table_entry));
}
