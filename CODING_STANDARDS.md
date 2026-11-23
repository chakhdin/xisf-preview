# Coding standards

Applied at review. Each rule is a judgement call; check every rule against every changed file.

## Spec fidelity

- The change does what the issue's acceptance criteria and the quoted spec stories say, word for word. A criterion the code skips, or narrows ("not in this issue's criteria") while the spec asks for it, is a finding.
- Behaviour that only shows at run time (timing, ordering, concurrency) is traced through the code path that runs it, not just the code that sets it up.

## Domain language

- Names, comments and test names use the terms in `GLOSSARY.md` (if present). A word listed under _Avoid_ used for that glossary concept is a finding. The same word in an unrelated sense is fine.

## Comments

- Every comment and doc comment states what the code does now. A comment naming a mechanism, unit or type the code no longer uses is a finding.
- An empty `catch` carries a one-line comment saying why the error is ignored.

## Consistency with the existing code

- New code matches the file it lands in: member order, imports, formatting and naming.
- One concept has one implementation: before a new loop, formatter or key list, look for the existing one and reuse it.

## Tests

- Each behaviour change has a test that fails without it.
- A parser change has a test with a small file built in the test, and any size declared in a file header is checked before allocating.
