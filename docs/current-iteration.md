# Current Iteration

Franklin replaces this file at the start of each assignment. The agent reads it before planning any work and treats it as the scope boundary. If it is missing or stale, the agent asks.

## Active assignment

**Asignación 2 — Gestión de permisos y su primera suite de pruebas** (Programación III, Week 6, 5 points, individual). Opens Wednesday 14 October 2026; closes Tuesday 20 October 2026, 11:59 p. m. (late delivery up to 24 hours later at 50 % penalty).
Brief and rubric: `docs/assignments/asignacion-2.md`.

## Requirement files to read

`docs/requirements/README.md` (index) first, then `design-rules.md`, `permissions.md` and `entities.md`. Read `access-control.md` only to protect what is already delivered (see "Constraints").

## Goal

Deliver three things at the tag `asignacion-2`: the Permissions management piece working, a first unit test suite on it, and the logbook with the agent.

## In scope

| Area | Requirement IDs |
|---|---|
| Permission requests: create, list pending, approve, reject, apply, forbidden transitions, nobody resolves their own | RF-GP-01 to RF-GP-07 |
| Cross-cutting design | RD-04, RD-06, RD-07, RD-09, RD-12 |
| Unit test suite on the piece (valid transition, forbidden transition) | assignment section 1.2 |
| README: how to trigger each RF-GP criterion, and a section titled exactly `Pruebas` | assignment sections 1.1 and 1.2 |
| Logbook `docs/bitacora-asignacion-2.md` | assignment section 1.3 |

## Out of scope (do not build)

- Audit records for the resolution of a request (RF-AUD-02, week 14). Design so they can be added later, but do not implement them.
- Notifications to Administrators and requester (RF-NOT-01, RF-NOT-02, weeks 11 and 12).
- Documents, reports.
- Tests of the business state machine (`Invoice`, week 8).
- Any change to the mail queue.

## Decisions taken by Franklin

1. **What a permission is.** A fixed catalog of named permissions defined in one place in the code, plus one demonstration operation that is only allowed to users who hold the granted permission. Franklin's choice for the demonstration: a single permission `ViewSalesSummary` and a route that returns a fixed sales summary sample, living in the permissions module (not in the business module). The permission requirement of that route must be readable in the same single place where the other routes declare their access, not hidden inside the handler. The agent confirms the route name and the exact shape in the plan and waits for approval.
2. **Who creates and who resolves.** A Standard user creates requests; an Administrator approves or rejects them.
3. **Where it lives.** A new module `PirryLedger.Core.Permissions`, layered like the existing ones (Domain, Application, Infrastructure, Api), owner of its data. Other pieces consult granted permissions through an interface declared in `PirryLedger.Core.Contracts`. A new test project `Tests/PirryLedger.Core.Permissions.Tests`.

## Open decisions (ask, do not assume)

- **Can an Administrator create a request?** Franklin will decide later. RF-GP-07 describes an Administrator trying to resolve a request created by themselves, so if Administrators cannot create requests that criterion cannot be triggered through the API and the README could not show how to provoke it. When this reaches the plan, the agent presents the options and the consequence for RF-GP-07 and waits.

## Constraints

- The transitions of a request are resolved in one single component (RD-04), following the pattern of `InvoiceStateMachine`. The permissions state machine shares no code and no enum with the business one (RF-NEG-09).
- Every new route is declared in the single role-requirements table (`ExigenciasDeRol`). The test that enumerates the real routes of the Host must keep passing and will fail if a route is not declared (RF-CA-05, RD-06).
- Applying an approved request happens automatically in the same operation that approves it: the request ends in `Applied`, and the requester can then execute the action with no further step (RF-GP-05).
- Requests are persisted with their own migration and survive an application restart (RD-09). Follow the table and column naming of `docs/adr/004-snake-case-column-naming.md` and the module prefix pattern already used by the other modules; the plan proposes the prefix.
- All external input is validated and a body that is missing or malformed gives a controlled `400`, never an unhandled exception (RD-07, RD-08).
- All times come from the injectable clock (RD-11).
- Do not break what is delivered: the existing tests keep passing and Access control keeps working (it is re-verified in later graded reviews).
- Code is in English; code comments and user-visible messages are in Spanish. Propose English names for `SolicitudPermiso` and its states (`Pendiente`, `Aprobada`, `Aplicada`, `Rechazada`) and record the mapping in the README.

## Tests (assignment section 1.2)

- xUnit, in `Tests/PirryLedger.Core.Permissions.Tests`, no application, database or SMTP server needed.
- At least one test of a valid transition, verifying the resulting state and the data the transition records (who and when for approval, or the reason for rejection).
- At least one test of a forbidden transition, verifying the rejection and that the state did not change.
- Each test has three identifiable phases (arrange, act, assert), verifies one behavior and its name says which one.
- Tests are independent: they pass in any order and one by one.
- One single command, documented under the README heading `Pruebas`, runs the whole suite from a clean clone.

## Logbook

File: `docs/bitacora-asignacion-2.md`, directly in `docs/` (the assignment fixes the path and it must be inside the tag). It documents at least one real task of this assignment delegated to the agent: what was asked, what it returned, and at least one case where the agent was wrong, how it was detected and how it was corrected, with evidence of the real session. Nothing is invented and nothing is reused from Asignación 1. The agent drafts each entry; Franklin reviews it before it is pushed.

## Definition of done

- Seven RF-GP rules meet their acceptance criterion and requests survive a restart.
- README explains how to trigger each RF-GP criterion and has a `Pruebas` section with one command.
- Suite passes from a clean clone, together with the previously delivered tests.
- Logbook complete with at least one real agent error.
- Documentation updated in the same pull requests (README, this file, ADRs authorized by Franklin).
- Tag `asignacion-2` pushed, and the repository URL and tag name submitted in Moodle.

## How it will be checked

The repo is cloned and `asignacion-2` checked out; the README is followed to the letter. Then: create, approve, reject with a reason and approve twice a request; run the command of the `Pruebas` section as written; read the valid-transition and forbidden-transition tests (what they prepare, execute and verify); read `docs/bitacora-asignacion-2.md`.

## Delivered earlier

Practice 1 (Access control, mail queue and the structure of the business state machine) is delivered. Its decisions live in `docs/adr/`; its commands in `README.md`; its brief in `docs/assignments/practice-1.md`.