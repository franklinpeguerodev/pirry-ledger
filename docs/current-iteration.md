# Current Iteration

Franklin replaces this file at the start of each assignment. The agent reads it before planning any work and treats it as the scope boundary. If it is missing or stale, the agent asks.

## Active assignment

**Práctica 2 — Una suite que detecta defectos: Gestión de permisos** (Programación III, Bloque 3, Week 7, 8 points, individual). Deadline as published on the virtual platform; late delivery up to 24 hours later at 50 % penalty.

Brief and rubric: `docs/assignments/practica-2.md`. Defect annex: `docs/assignments/practica-2-defectos.md` (variant A; the enrolment number ends in 0).

## Requirement files to read

`docs/requirements/README.md` (index) first, then `permissions.md` and `design-rules.md`. Read `access-control.md` and `notifications.md` only to protect what is already delivered: section 1.3 of the brief re-verifies access control.

## Goal

Deliver at the tag `practica-2`: the Permissions suite covering RF-GP-01 to RF-GP-07 with every test identified by its ID, six tagged defect commits that the suite detects, and the README sections `Pruebas` and `Defectos`.

## In scope

| Area | Requirement IDs | Brief |
|---|---|---|
| Suite over the Permissions piece, at least one test per rule, each citing its ID | RF-GP-01 to RF-GP-07 | 1.1 |
| Six planted defects, one tag each, parent `practica-2` | variant A, defects 1 to 6 | 1.2 |
| README section `Defectos`, one line per defect naming the test that detects it | — | 1.2 |
| README section `Pruebas`, one command that runs everything | — | 1.1 |
| Cumulative review of Access control, following the README | RF-CA-01 to RF-CA-22 (verify only) | 1.3 |
| `docs/maquina-de-estados.md` documents the permission request machine | RF-GP-01 to RF-GP-07, RF-NEG-09 | Franklin's decision |
| Logbook `docs/bitacora-practica-2.md` | — | AGENTS.md |

## Out of scope (do not build)

- New functionality of the Permissions piece. It is delivered; this iteration writes tests, not features.
- Tests of the business state machine (`Invoice`, week 8).
- Audit records (RF-AUD-02, week 14), notifications to Administrators and requester (RF-NOT-01, RF-NOT-02, weeks 11 and 12), documents, reports.
- Any change to the mail queue.
- Any change to the schema or a new migration.

## Decisions taken by Franklin

1. **Two new tests before tagging.** The current suite detects four of the six defects. It cannot detect defect 4 (the `requester_id` filter lives in `PermissionRequestRepository`, and no test instantiates it) nor defect 5 (nothing asserts that `Approved -> Pending` is forbidden). The suite gains a test of the real repository against EF InMemory, and the state machine tests become an exhaustive 4x4 matrix. Then the six defects are planted and each one is shown to fail at least one test.
2. **Defects live outside the normal flow.** Each defect is a commit on a branch created from the tag, tagged `practica-2-defecto-N`, and never merged into `develop` or `main`. The branch is deleted; the tag keeps the commit alive.
3. **The tag goes on `main`.** Like `asignacion-2`: work in `develop`, merge into `main`, tag there. The six defect tags point to commits whose parent is `practica-2`.
4. **The brief wins over the requirements files** when they disagree, as `docs/requirements/README.md` states.
5. **The logbook is a new file**, `docs/bitacora-practica-2.md`. The brief does not grade it; `AGENTS.md` requires it, and mixing two graded deliveries in one file would blur both.
6. **The permission request machine is documented.** `docs/maquina-de-estados.md` documents `Invoice` only, and its title says so. Practice 2 adds the second machine to the same file, with its transition table and the note that the two machines share no code (RF-NEG-09). It earns no points; it closes a documentation gap and gives the README `Defectos` section something to point to. Its own commit, separate from the replacement of this file.
7. **Table headers in Spanish, entity names in English.** The `Invoice` table of `docs/maquina-de-estados.md` uses English headers inside a Spanish document, while `docs/requirements/permissions.md` uses Spanish ones. The headers become Spanish; the state names stay as the code declares them (`Draft`, `Pending`, `Approved`, `Applied`, `Rejected`), because they are identifiers and the tests use them.

## Open decisions (ask, do not assume)

None for this iteration.

## Deferred

Work and decisions consciously left out of this iteration. Nothing here is waiting for a decision: each item states what it blocks and why it is not being resolved now.

- **Do not run `dotnet ef migrations add` in the permissions module.** Its model
  declares no foreign keys to `ac_users`, and its migration snapshot was only
  generated on 2026-10-20. Until it is settled whether those keys belong in the
  model (declared by table name) or stay out of the database for good, any new
  migration would be generated from a model that contradicts how the tables were
  designed. The alternatives and their consequences are in the logbook, Entry 8.
- **The four foreign keys to `ac_users` are not in the database.**
  `requester_id`, `approver_id`, `user_id` and `granted_by_user_id` carry no
  referential constraint. Integrity comes from the application, which takes both
  identities from the authenticated session rather than from the request body.
  It stays that way until the decision above is taken.
- **Missing migration metadata is caught by hand, not by a test.** A migration
  committed without its `.Designer.cs` sits in the repository while EF Core
  ignores it silently, and `dotnet ef database update` reports the database as
  up to date. That is how the permissions migration went unnoticed until the run
  of 2026-10-20 (logbook, Entry 8). No automated check guards it yet.

## Constraints

- The Asignación 2 test conditions still apply: no full application, no database and no SMTP server (RD-12); independent tests; arrange, act and assert; one single command.
- Every test of RF-GP-01 to RF-GP-07 identifies its ID in its name or in a comment.
- No existing test is deleted or weakened. If one must change, the pull request says why.
- A defect commit modifies only production code of the Permissions piece. It never touches `Tests/`.
- A defect commit's message says which file and which line changed.
- Do not break what is delivered: Access control keeps working, and its 96 tests keep passing.
- Code is in English; code comments and user-visible messages are in Spanish.

## Tests

- `dotnet test pirry-ledger.slnx` from a clean clone is the single command of the README `Pruebas` section.
- The suite must fail for every planted defect. A defect that leaves the suite green means the suite is incomplete: extend it, re-tag `practica-2` and plant the defect again.
- EF InMemory is allowed to exercise a real repository; PostgreSQL is not needed.

## Logbook

File: `docs/bitacora-practica-2.md`. It documents at least one real task of this practice delegated to the agent: what was asked, what it returned, and at least one case where the agent was wrong, how it was detected and how it was corrected, with evidence of the real session. Nothing is invented and nothing is reused from the previous logbooks. The agent drafts each entry; Franklin reviews it before it is pushed.

## Definition of done

- RF-GP-01 to RF-GP-07 each have at least one test that cites the ID, and the suite passes at the tag.
- Six defect tags exist, each parent is `practica-2`, each commit touches only Permissions production code, and each one makes at least one test fail.
- README has `Pruebas` (one command) and `Defectos` (one line per defect, naming the test).
- Access control works in the four fronts of section 1.3, following the README.
- Both state machines are documented in `docs/maquina-de-estados.md`.
- Documentation updated in the same pull requests.
- Tags pushed and the repository URL and tag name submitted in Moodle, after Franklin's authorization.

## How it will be checked

`git clone` and `git checkout practica-2`; the command of the README `Pruebas` section; for each defect, `git diff practica-2 practica-2-defecto-N --stat`, the commit message, and the same command over the tag, which must report at least one failure; the README `Defectos` section against the tests that fail; the names of the tests against the RF-GP IDs; and Access control, started from the README, in its four fronts.

## Delivered earlier

Practice 1 (Access control, mail queue and the structure of the business state machine) and Asignación 2 (Permissions management and its first suite) are delivered. Their decisions live in `docs/adr/`; their commands in `README.md`; their briefs in `docs/assignments/`.
