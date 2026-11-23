# Context

## Branch diff against `{{BASE}}`

!`git diff {{BASE}}...HEAD`

## Commits on this branch

!`git log {{BASE}}..HEAD --format='--- %h %s%n%b'`

# Task

You are the reviewer. The implementer has just committed one issue on this branch. Review the diff above against three sources and fix what you find, on this branch:

1. **The issue**: the file under `.scratch/` that the diff closes. Every acceptance criterion it lists.
2. **The spec**: `.scratch/<feature>/spec.md`, every story the issue cites.
3. **The standards**: `CODING_STANDARDS.md`, every rule, against every changed file.

The environment is the same as the implementer's: a Linux container where the tests run under Mono, with no UI, Explorer or shell registration. After you, a Windows gate builds the signed assembly and runs the whole suite, the `WindowsOnly` UI tests included, before the merge.

## Steps

1. Read the issue, the cited spec stories, `CODING_STANDARDS.md` and `GLOSSARY.md` (if present). Read each changed file in full, not only the hunks.
2. List every finding: a criterion or story the code misses or narrows, a rule broken, a bug. The step is done when every criterion, every cited story and every rule has been checked against the diff.
3. Fix each finding that you can fix in the code, test-first where behaviour changes. Leave behaviour the issue did not ask for unchanged.
4. Verify, and fix any failure:
   - `dotnet build Tests/XisfPreview.Tests.csproj -p:SignAssembly=false -p:SkipKillSurrogates=true --nologo -v q`
   - `bash Tests/run-linux-tests.sh`
5. Append one comment to the issue with `node .sandcastle/issues.mjs comment <id> "<text>"`: the findings, which you fixed, and any you could not fix. A finding you could not fix also goes on the issue's **Needs manual verification** list in that comment, and you set the issue's `Status:` line to `needs-verification`.
6. Commit the fixes and the issue file in one commit whose message starts with `RALPH review:` and names the issue id. When there are no findings, still add the comment and commit it.

When the commit is made, output <promise>COMPLETE</promise>.
