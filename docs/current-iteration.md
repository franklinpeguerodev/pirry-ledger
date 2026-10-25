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
2. **Who creates and who resolves.** A Standard user creates requests (RF-GP-01); an Administrator approves or rejects them. An Administrator holds access to every operation by role, so the permission check passes for an Administrator without any request, and no rule forbids an Administrator from creating a request. Nobody resolves their own request (RF-GP-07), and the rule compares identities, not roles: it rejects whoever resolves a request when they are the same user who created it, whatever role they had then or have now. The README shows two ways to trigger it, the direct one (an Administrator creates a request and tries to resolve it) and the one the rule exists for (a Standard user creates a request, is promoted to Administrator while it is Pending, and then tries to resolve it). The README leads with the realistic path as the main demonstration of RF-GP-07; the direct path stays as a second, shorter check.
3. **Where it lives.** A new module `PirryLedger.Core.Permissions`, layered like the existing ones (Domain, Application, Infrastructure, Api), owner of its data. Other pieces consult granted permissions through an interface declared in `PirryLedger.Core.Contracts`. A new test project `Tests/PirryLedger.Core.Permissions.Tests`.
4. **Role changes invalidate sessions.** `User.ChangeRole` increments `CredentialVersion`, the same mechanism that already invalidates sessions on password change (RF-CA-12) and deactivation (RF-CA-20). After a role change (typically a promotion to Administrator), the affected user must re-login for their next request to reflect the new role. This is what makes the realistic path of RF-GP-07 testable: an Administrator cannot act on a request they created with their previous role until they log in again and the new role is in the request. Detailed in `docs/adr/009-role-changes-invalidate-sessions.md`.
5. **Where the access machinery lives.** The pure data types of the access machinery (`Operation` enum and `AccessLevel` enum) live in `PirryLedger.Core.Contracts.Auth`, available to any module that declares routes. The table that pairs operations with their required level (`RoleRequirements`) and the runtime filter (`WithAccess`, `AccessFilter`, `OperationMetadata`) stay in `PirryLedger.Core.AccessControl.Application` and `PirryLedger.Core.AccessControl.Api` respectively. The reason for the partial move is a layering constraint: `AccessFilter.ApplyAsync` depends on `PirryLedger.Core.AccessControl.Application.Authenticate`, and `RoleRequirements.RequiredRole` returns `PirryLedger.Core.AccessControl.Domain.Enums.Role`. Moving these to `Contracts` would force `Contracts` to reference `AccessControl`, inverting the dependency direction. As a consequence, when `PirryLedger.Core.Permissions.Api` declares its own routes in the next iteration, it references `PirryLedger.Core.AccessControl.Api` for `WithAccess`. The full move (including the filter) is recorded as a future option: it requires an `IAuthenticator` interface in `Contracts` and an implementation in `AccessControl.Application`.
6. **The role travels as text in the responses.** The administrative listing returned the role as the number of the enum, so the client had to know the order of the enum to know whether a user is an Administrator or a Standard. RF-CA-21 asks the listing to say the role, so the Host registers a `JsonStringEnumConverter` for HTTP responses and the role travels as `"Administrator"` or `"Standard"`, the same words the column `ac_users.role` already stores. Decided by Franklin on 2026-10-24; recorded in `docs/adr/010-role-as-text-in-responses.md`.

## Open decisions (ask, do not assume)

None for this iteration.

## Deferred

Work and decisions consciously left out of this iteration. Nothing here is
waiting for a decision: each item states what it blocks and why it is not being
resolved now.

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

File: `docs/bitacora-asignacion-2.md`, the path the assignment brief fixes.
It documents at least one real task of this assignment delegated to the agent: what was asked, what it returned, and at least one case where the agent was wrong, how it was detected and how it was corrected, with evidence of the real session. Nothing is invented and nothing is reused from Asignación 1. The agent drafts each entry; Franklin reviews it before it is pushed.

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