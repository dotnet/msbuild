# Drafting and authorized issue publication

## Read or draft first

Use a repository-qualified issue identity. These PowerShell variables must come
from the user's selected target, not example issue numbers:

```powershell
gh issue view $number --repo $repository --json id,url,title,body,updatedAt,state,assignees,labels
if ($LASTEXITCODE -ne 0) { throw "Issue retrieval failed." }
```

Preserve the body and `updatedAt` when preparing an edit. Write multiline content
to an explicitly selected local draft file (prefer the session workspace for
temporary drafts), and use `--body-file` rather than shell-quoting multiline text.
Do not create a committed planning document merely to run a GitHub command.

## Content safeguards

- Keep objective, decision-relevant content: problem, scope, exit criteria, reproduction, and supporting evidence.
- Use neutral language. Say "the team agreed" only when agreement is established; label unresolved questions and hypotheses.
- Exclude credentials, confidential dates/details, private room or recording identifiers, and unnecessary personal information.
- Prefer public GitHub handles when attribution is necessary and authorized. Do not publish raw internal chat, M365 responses, or speculation about another team's motives.
- Present the proposed publication unless the exact content/action is already approved. Authorization to draft is not authorization to post.

## Authorized single-issue operations

Choose only the requested operation; this block is a menu, not a sequence:

```powershell
gh issue comment $number --repo $repository --body-file $draftPath
gh issue edit $number --repo $repository --body-file $draftPath
gh issue create --repo $repository --title $title --body-file $draftPath
gh issue edit $number --repo $repository --add-label $label --add-assignee $login
```

Check `$LASTEXITCODE` immediately after the selected command; stop if it fails.
Do not execute the next menu item or retry a possibly successful creation/comment.
Read back the returned issue/comment identity and requested state.

`gh issue edit --body-file` replaces the body. There is no append operation:
fetch, prepare the intended replacement, and re-read the body/update time before
writing. If it changed, reconcile instead of overwriting concurrent edits. A
comment is often more appropriate for a status update.

## Bulk and project follow-through

Prepare an explicit target list with per-item drafts or old/new fields. Recheck
authorization for the whole set. Record each completed action and stop on the
first unexpected failure; a retry must reconcile current state to avoid duplicate
comments/issues. Do not use a shell loop over unreviewed search results.

A newly created issue may not be auto-added to the desired project. If adding it
and setting its sprint were also authorized, use the returned content node ID and
the verified project in [project fields](project-fields.md). Add the item, check
its returned item ID, then update the field in a separate operation.
