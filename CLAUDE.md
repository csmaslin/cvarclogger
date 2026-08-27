# Project rules (CvarcLogger)

Standing architectural rules for this project. These apply equally to the sibling CvarcCellLog
project (see its own CLAUDE.md) even though the two no longer share code.

## Rule 1: database content has exactly four entry points
The only ways QSO/log data may be written into the database are:
1. ADIF import
2. DB import/restore (loading another database file)
3. CAT (the radio auto-filling fields during a live QSO)
4. Manual entry (the operator typing into the app's own forms)

No other path may write QSO data into the database. When adding a feature, check that any new
way of getting data onto a form still funnels through manual entry/CAT/import rather than opening
a fifth, undocumented write path.

## Rule 2: working directory root and access scope
`C:\Users\user\Documents\Projects` is the root of the working directories. Access is permitted
for this directory and all of its subfolders and files (both sibling projects, CvarcCellLog and
CvarcLogger, live under it).

## Rule 3: projects are write-silo'd
All projects are silo'd for write — activity in one program will not write or change anything in
another. A sibling project is available for reference and lookup, as a model to help solve a
problem, not to reinvent the same solution from scratch — but never to copy from directly or to
modify.

## Rule 4: input fields standardize to the database
Input field length should match the database column it feeds/reads (input/output should match),
with one standing exception: the visual width cap of 20 characters on the entry box itself (a
field can still accept/store more than 20 characters; only its on-screen box is capped there).
Any other exception to this rule is unique to the individual program it appears in, and must be
noted in that program's own notes/comments explaining why the exception exists.

## Rule 5: commands are followed as given; problems are surfaced, not silently solved
All commands from the user are adhered to without change. If a problem is found with a command
(a conflict, a technical blocker, a concern), present the problem and a proposed solution first —
do not act on/implement that solution until it is explicitly granted.

## Rule 6: testing happens in the emulator, with the user driving
Advanced testing is done in emulation mode with user interaction, unless said otherwise. Build and
deploy to the emulator, then hand off for the user to verify — do not self-test with screenshots/
automated taps unless explicitly asked to.

## Rule 7: publish requirements
Every publish routine must ensure the program:
- Is signed.
- Has a version source (a single source-of-truth file the publish script reads the version from).
- Has a user-friendly change log (plain-language, separate from the technical CHANGELOG.txt).
- Has a developed "Program Overview and Data Flow.md".
Programs capable of running stand-alone must also include their user manual in the distribution:
the manual's `.docx` **and** its generated `.pdf` must both be deposited in the `publish\` folder.

## Rule 8: maintain human oversight
The user stays in control of the process at every step — no scope-expanding or risky action gets
taken just because it seems like the obvious next step, even when nothing is "wrong." Rules 5 and
6 are concrete instances of this general principle: Rule 5 is oversight at a decision point (a
problem surfaces, a solution is proposed, nothing is implemented until granted); Rule 6 is
oversight during testing (the user drives verification in the emulator, not automated self-checks).
Default to stopping at natural checkpoints — edit, then stop; build, then stop; deploy, then stop —
rather than chaining forward on an assumption of what comes next.

## Rule 9: write clean, maintainable code
Avoid magic numbers and overly complex logic where a simple alternative exists. Prefer named
constants over bare literals, and the straightforward approach over a clever one when both solve
the problem equally well.
