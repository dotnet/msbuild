/* GNU Guile interface for GNU Make.
Copyright (C) 2011-2012 Free Software Foundation, Inc.
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
#include "debug.h"
#include "dep.h"
#include "variable.h"

#include <libguile.h>

static SCM make_mod = SCM_EOL;
static SCM obj_to_str = SCM_EOL;

/* Convert an SCM object into a string.  */
static char *
cvt_scm_to_str (SCM obj)
{
  return scm_to_locale_string (scm_call_1 (obj_to_str, obj));
}

/* Perform the GNU make expansion function.  */
static SCM
guile_expand_wrapper (SCM obj)
{
  char *str = cvt_scm_to_str (obj);
  SCM ret;
  char *res;

  DB (DB_BASIC, (_("guile: Expanding '%s'\n"), str));
  res = allocated_variable_expand (str);
  ret = scm_from_locale_string (res);

  free (str);
  free (res);

  return ret;
}

/* Invoked by scm_c_define_module(), in the context of the GNU make module.  */
static void
guile_define_module (void *data UNUSED)
{
/* Ingest the predefined Guile module for GNU make.  */
#include "gmk-default.h"

  /* Register a subr for GNU make's eval capability.  */
  scm_c_define_gsubr ("gmk-expand", 1, 0, 0, guile_expand_wrapper);

  /* Define the rest of the module.  */
  scm_c_eval_string (GUILE_module_defn);
}

/* Initialize the GNU make Guile module.  */
static void *
guile_init (void *arg UNUSED)
{
  /* Define the module.  */
  make_mod = scm_c_define_module ("gnu make", guile_define_module, NULL);

  /* Get a reference to the object-to-string translator, for later.  */
  obj_to_str = scm_variable_ref (scm_c_module_lookup (make_mod, "obj-to-str"));

  /* Import the GNU make module exports into the generic space.  */
  scm_c_eval_string ("(use-modules (gnu make))");

  return NULL;
}

static void *
internal_guile_eval (void *arg)
{
  return cvt_scm_to_str (scm_c_eval_string (arg));
}

/* This is the function registered with make  */
static char *
func_guile (char *o, char **argv, const char *funcname UNUSED)
{
  if (argv[0] && argv[0][0] != '\0')
    {
      char *str = scm_with_guile (internal_guile_eval, argv[0]);
      if (str)
        {
          o = variable_buffer_output (o, str, strlen (str));
          free (str);
        }
    }

  return o;
}

/* ----- Public interface ----- */

int
setup_guile ()
{
  /* Initialize the Guile interpreter.  */
  scm_with_guile (guile_init, NULL);

  /* Create a make function "guile".  */
  define_new_function (NILF, "guile", 0, 1, 1, func_guile);

  /* Add 'guile' to the list of features. */
  do_variable_definition (NILF, ".FEATURES", "guile", o_default, f_append, 0);

  return 1;
}
