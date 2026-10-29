# Current Iteration

Franklin replaces this file at the start of each assignment. The agent reads it before planning any work and treats it as the scope boundary. If it is missing or stale, the agent asks.

## Active assignment

**Práctica 3 — Tu módulo de negocio, sus pruebas y su integración** (Programación III, Bloque 3, Week 8, 8 points, individual). Deadline as published on the virtual platform; late delivery up to 24 hours later at 50 % penalty.

Brief and rubric: `docs/assignments/practica-3.md`, transcribed from the professor's PDF. Two repository notes at its end apply: the rubric says "tres" defects but there are **four** (`practica-3-defecto-1` to `-4`), and the "entre 3 y 5 estados" of RF-NEG-03 does not limit this machine (see decision 2). The defects are described in section 1.3 of the brief itself, over Franklin's own tables, so there is no separate defects annex as in Práctica 2.

## Requirement files to read

`docs/requirements/README.md` (index) first, then `business-module.md`, `design-rules.md` and `permissions.md`. Read `access-control.md` only to protect what is already delivered: section 1.5 of the brief re-verifies access control and permissions.

`business-module.md` only holds RF-NEG-03, 04 and 05. RF-NEG-02, 06 and 09 (text in section 1.1 of the brief) are added to it in the same pull request that needs them, with the index updated. The professor's text of the existing rows is not edited.

## Goal

Deliver at the tag `practica-3`: the business module (invoicing) working and protected by Access control, the suite of its state machine, four defects planted by Franklin in his own code, one integration test between the business module and Permissions, and the README sections `Pruebas` and `Defectos`.

## In scope

| Area | Requirement IDs | Brief |
|---|---|---|
| At least five separable features of the invoicing module, each testable on its own and explained in the README | RF-NEG-02 | 1.1 |
| State machine with its states declared in one place | RF-NEG-03 | 1.1 |
| At least one explicitly forbidden transition, rejected without changing the state | RF-NEG-04 | 1.1 |
| At least one terminal state | RF-NEG-05 | 1.1 |
| Every business operation requires a valid session and a role check | RF-NEG-06 | 1.1 |
| The business machine is independent from the permission requests machine | RF-NEG-09 | 1.1 |
| At least one business operation that needs a permission granted by Permissions | — | 1.1 |
| Suite of the business machine: one test per row of the allowed and forbidden tables, plus the terminal states | — | 1.2 |
| Four planted defects, one tag each, parent `practica-3` | — | 1.3 |
| README sections `Pruebas` (one command) and `Defectos` (one line per defect naming the test) | — | 1.2, 1.3 |
| Integration test with the real Permissions and the real business module, both faces | — | 1.4 |
| Cumulative review: registration with activation, session, role rejection, password recovery, permission requests | RF-CA, RF-GP (verify only) | 1.5 |
| `docs/maquina-de-estados.md` documents the new invoice machine | RF-NEG-03, 04, 05, 09 | 1.1 |

Business features proposed for RF-NEG-02 (Franklin confirms the final list):

1. Issue the invoice: the client builds the draft and the server creates and persists the invoice already issued, with its items and the delivery mark.
2. Issue the invoice (marks it as delivery or not).
3. Collect: register the payments (cash, transfer), or leave the invoice as debt.
4. Resolve pending payments: verify transfers and register debt payments (Administrator).
5. Cancel with a permission, requested by whoever collects and approved by an Administrator.
6. Consult invoices (list and detail, by state). The Administrator also reads the edit history of an invoice.

## Out of scope (do not build)

- Notifications and reports of the business module (RF-NEG-07, RF-NEG-08, week 10). That includes the daily sales total and how pending delivery invoices count in it.
- Attaching documents to an entity (RF-NEG-10, optional).
- Investment (purchases and expenses), profit and the owner view.
- A delivery module: customers, addresses, couriers, delivery fee. Delivery exists only as a mark on the invoice.
- A debtor entity, or any control of several debts of the same person. A debt stores a name and nothing else.
- Storing a photo or any proof of a transfer. Franklin verifies transfers outside the system.
- A fiscal receipt (no NCF for now).
- Audit records (RF-AUD-02, week 14).
- Any change to the mail queue, and any new functionality of Access control.

## Decisions taken by Franklin

1. **Business entity and states.** `Invoice` carries the machine. Seven states, in English because they are identifiers: `Draft`, `Issued`, `PendingVerification`, `Paid`, `InDebt`, `Cancelled`, `NotDelivered`. `Issued` is shown to the user as "pendiente". Terminal states: `Cancelled` and `NotDelivered`.
2. **The number of states.** Franklin states that the professor confirmed it: the machine has a minimum of 3 states and **no upper limit**. The brief and the Práctica 1 rubric say "entre 3 y 5". Franklin authorizes writing "from 3 to N states" in the agent documents and keeps the professor's message as evidence. The text of the requirement files and of the professor's brief is not changed; the brief carries a repository note about it, so the rule "the brief wins" does not make the agent stop.
3. **Allowed transitions.**

   | desde | hacia | quién la ejecuta | condición |
   |---|---|---|---|
   | `Draft` | `Issued` | Quien cobra | Tiene al menos un ítem. Aquí se marca si es delivery. |
   | `Issued` | `Paid` | Quien cobra | Los pagos cubren el total y ninguno es transferencia. |
   | `Issued` | `PendingVerification` | Quien cobra | Algún pago es transferencia. |
   | `Issued` | `InDebt` | Quien cobra | Exige el nombre de la persona. |
   | `Issued` | `Cancelled` | Quien cobra | Sin permiso. |
   | `Issued` | `NotDelivered` | Quien cobra | La factura es de delivery. |
   | `PendingVerification` | `Paid` | Administrador | La transferencia llegó. |
   | `PendingVerification` | `InDebt` | Administrador | Transferencia rechazada. Exige el nombre de la persona. |
   | `InDebt` | `Paid` | Administrador | Los abonos cubren el total de la deuda. |
   | `Paid` | `Cancelled` | Quien solicitó el permiso | Existe un permiso aprobado para esa factura; se consume al anular. |
   | `InDebt` | `Cancelled` | Quien solicitó el permiso | Igual que la anterior. |

   Of the 49 possible pairs, 11 are allowed and 38 are forbidden, including the 7 that leave the state unchanged.
4. **Forbidden transitions, explicit.** `Issued -> Draft` (the first of the table); `Draft -> Paid`, `InDebt`, `PendingVerification`, `Cancelled`, `NotDelivered`; `PendingVerification -> Draft`, `Issued`, `Cancelled`, `NotDelivered`; `Paid -> Draft`, `Issued`, `PendingVerification`, `InDebt`, `NotDelivered`; `InDebt -> Draft`, `Issued`, `PendingVerification`, `NotDelivered`; every exit from `Cancelled` and `NotDelivered`. `Paid -> Cancelled` used to be the forbidden one in Práctica 1; it is now allowed with a permission, so the table of `docs/maquina-de-estados.md` changes.
5. **Who is who.** "Quien cobra" is a Standard user, and only a Standard user: an Administrator who tries to issue, collect or edit an invoice is rejected on the server. The Administrator (Franklin) verifies transfers, registers debt payments, approves cancellations and is the only one who can read the edit history. Because the requester is always Standard and the approver always an Administrator, RF-GP-07 never blocks the cancellation flow. Whoever works the counter needs a Standard account.
6. **Payments.** An invoice may be paid with several payments (split payment), each with a method (`Cash` or `Transfer`) and an amount. In cash, the amount received and the change are recorded. If any payment is a transfer, the whole invoice goes to `PendingVerification`. A transfer proof is not stored.
7. **Debt.** A debt always carries the name of the person, with no exception. Only an Administrator registers debt payments (amount and date, no method). The payment that covers the total moves the invoice to `Paid` in the same operation. Payments of a debt are records of the invoice, not transitions.
8. **Delivery.** The invoice is marked as delivery when it is issued. The paper invoice is handed out while the system still shows it as `Issued`; when the courier pays, whoever collects marks it paid. A delivery that cannot be delivered stays `Issued` until it is marked `NotDelivered` (terminal; if the customer shows up again, a new invoice is made). Only delivery invoices can go to `NotDelivered`.
9. **Editing and the draft.** The draft lives only in the client: nothing is persisted while the invoice is `Draft`, because storing a draft costs a write per touched item and leaves rows that nobody closes. The server receives the complete invoice and persists it already issued, so no row is ever stored in `Draft`. Items can be changed while the invoice is `Issued` and are frozen in every other state. Every edit made while the invoice is `Issued` is recorded with the name of the Standard user who made it and when (injected clock, RD-11). Only an Administrator can read that history.
10. **Permission to cancel.** Cancelling an invoice that is `Paid` or `InDebt` requires a permission for that single invoice. It is requested by the user who collects, approved by an Administrator, and consumed when used: each cancellation asks for its own permission. The permission name follows the convention `CancelInvoice:<invoice id>` and fits the `varchar(64)` columns. Permissions gets one new operation in its contract that deletes the grant. No migration (see "Deferred").
11. **Defects in this practice.** (1) Add `Issued -> Draft`. (2) Add `Cancelled -> Issued`, an exit from a terminal state. (3) Remove the check of the condition of `Draft -> Issued` (at least one item). (4) Remove the query to Permissions in the cancellation of a `Paid` or `InDebt` invoice; the integration test must detect it.
12. **The documentation of the project.** `AGENTS.md` now says delivery is handled only at the invoicing level.
13. **Defect procedure, the same as Práctica 2.** Each defect is a commit on a branch created from the tag `practica-3`, tagged `practica-3-defecto-N`, and never merged into `develop` or `main`. The branch is deleted; the tag keeps the commit alive. Work happens in `develop`, is merged into `main`, and `practica-3` is tagged there.
14. **Logbook.** `docs/bitacora-practica-3.md`, a new file. The brief does not grade it; `AGENTS.md` requires it.

## Open decisions (ask, do not assume)

None for this iteration. Two things are not decisions but steps of the plan below: ADR 010 (consumption of a permission grant, which touches ADR 008) is presented by the agent with its alternatives and written only after Franklin authorizes it; and the agent has not seen the code of Permissions or of the business module yet, so it reports real paths in step 0 before planning in detail.

## Work plan

The plan is written by behaviour, not by file name, because file names are confirmed in step 0. The workflow of `AGENTS.md` applies to every step: propose the plan of the step, wait for approval, write the tests and show them failing for the right reason, implement the minimum, run the command and show the real output, check the acceptance criterion point by point, and propose the commit. Every pull request ships its documentation and its entry in `docs/bitacora-practica-3.md` as its own commit, uses the four sections of the template, and is opened by Franklin. No push, merge or tag without his approval.

**Step 0 — Reconnaissance (no code, no pull request).** Read, without changing anything: the business module (entity, states, state machine, persistence, test project), the public contract of Permissions and how it stores the requested permission, the permission request and grant entities, `ApprovePermissionRequest`, `PermissionGrantRepository`, the single place where Access control declares the role of each operation (RF-CA-05), the README and `docs/maquina-de-estados.md`. Report: real paths, what already exists, what is missing, and anything that contradicts this file. Stop and wait.

**Step 1 — The invoice state machine** (RF-NEG-03, 04, 05, 09; RD-04). Seven states and the 11 allowed transitions resolved in one single component; the rest are rejected and leave the state unchanged. Rewrite the invoice part of `docs/maquina-de-estados.md` with the new tables (Spanish headers, English state names). Add RF-NEG-02, 06 and 09 to `business-module.md` and update the index. Tests first: one test per row of the allowed table, one per row of the forbidden table (rejection and unchanged state), one per terminal state. The machine references nothing of Permissions. The checks that belong to a transition (at least one item, delivery, payments) are proposed by the agent; the check of `Draft -> Issued` must be one single verification that can be removed on its own (defect 3).

**Step 2 — The rules of the invoice.** Items editable while the invoice is `Issued` and frozen elsewhere (the draft lives in the client); issuing with at least one item and the delivery mark; payments with method and amount, split payments, amount received and change; any transfer sends the invoice to `PendingVerification`; debt with a mandatory name; debt payments by an Administrator, the one that covers the total moves the invoice to `Paid`; `NotDelivered` only for delivery invoices; the edit history of `Issued`. Persistence in the module's own tables with its own migration, and the data survive a restart (RD-09). Invalid input is a controlled rejection (RD-07). Tests first, one behaviour each.

**Step 3 — Consuming a permission grant in Permissions.** Stop first: present ADR 010 with alternatives and trade-offs, and wait for Franklin. Then one new operation in the Permissions contract that deletes the grant, with tests in the Permissions suite. No migration (see Deferred). The agent proposes what happens when no grant exists and how the cancellation and the consumption stay consistent. The existing tests of Permissions are not weakened.

**Step 4 — The business operations, protected.** Expose the six features as operations protected by session and role (RF-NEG-06, RD-06), with the role of each declared in the single place of Access control. A Standard user is rejected on the operations of the Administrator, and an Administrator is rejected on the operations of whoever collects. The cancellation of a `Paid` or `InDebt` invoice consults Permissions for `CancelInvoice:<invoice id>`; that one line is the target of defect 4. An operation requests that permission on behalf of the user. Tests include requests built by hand. The README explains how to provoke each feature.

**Step 5 — The integration test** (section 1.4 of the brief). A test project that references the real business module and the real Permissions, with no doubles and no running application. Face one: the cancellation is rejected while the request is not approved. Face two: it succeeds after an Administrator approves it. It also shows that the permission of one invoice does not open another, and that the grant is gone after it is used. Before tagging, break the line of defect 4 by hand and show this test failing.

**Step 6 — Closing and delivery.** README sections `Pruebas` and `Defectos`; documentation checked against the code; the five fronts of section 1.5 verified by following the README. Then, with Franklin's authorization: merge into `main`, tag `practica-3`, plant the four defects from the tag, show each one failing at least one test, tag them, and push. Franklin submits the repository URL and the tag in Moodle.

## Deferred

Work and decisions consciously left out of this iteration. Nothing here is waiting for a decision: each item states what it blocks and why it is not being resolved now. Carried over from Práctica 2.

- **Do not run `dotnet ef migrations add` in the permissions module.** Its model declares no foreign keys to `ac_users`, and its migration snapshot was only generated on 2026-10-20. Until it is settled whether those keys belong in the model (declared by table name) or stay out of the database for good, any new migration would be generated from a model that contradicts how the tables were designed. The alternatives and their consequences are in the logbook of Práctica 2, Entry 8.
- **The four foreign keys to `ac_users` are not in the database.** `requester_id`, `approver_id`, `user_id` and `granted_by_user_id` carry no referential constraint. Integrity comes from the application, which takes both identities from the authenticated session. It stays that way until the decision above is taken.
- **Missing migration metadata is caught by hand, not by a test.** A migration committed without its `.Designer.cs` is ignored silently by EF Core. No automated check guards it yet.
- **The business module owns its tables.** Migrations for the tables of the invoicing module are part of this iteration; none touch the permissions module.
- **Daily sales and pending delivery invoices.** Franklin wants delivery invoices that are still `Issued` to count in the daily sales. It is decided in week 10 with the reports.

## Constraints

- RD-12: no full application, no database and no SMTP server in the tests of the machine; independent tests; arrange, act and assert; one single command.
- The integration test uses the real business module and the real Permissions, with no doubles replacing either of them, and without starting the application. It lives in its own test project that references both; the business module keeps depending only on the contracts project of the Core (RD-03, RF-NEG-09).
- The two state machines share no code, types or enums (RF-NEG-09).
- An Administrator who calls an operation of whoever collects is rejected on the server, and a test proves it. The edit history is readable only by an Administrator, and a test proves it.
- Transitions are resolved in one single component (RD-04). The role requirement of each operation is declared in one readable place (RF-CA-05).
- Test names are in Spanish and each one names the behaviour. Every rejection test also verifies that the state did not change.
- A defect commit modifies only production code and never touches `Tests/`. Its message says which file and which line changed.
- No existing test is deleted or weakened. If one must change, the pull request says why.
- Do not break what is delivered: Access control keeps its 96 tests passing, and the suites of Permissions keep passing.
- Code is in English; code comments and user-visible messages are in Spanish.

## Tests

- `dotnet test pirry-ledger.slnx` from a clean clone is the single command of the README `Pruebas` section.
- One test per row of the table of allowed transitions and of the table of forbidden ones of `docs/maquina-de-estados.md`, one test for each terminal state, and the tests of the rules of this iteration (items frozen outside `Issued`, mandatory name of the debt, change, payments).
- The suite must fail for every planted defect. A defect that leaves the suite green means the suite is incomplete: extend it, re-tag `practica-3` and plant the defect again.

## Logbook

File: `docs/bitacora-practica-3.md`, a new file. It documents at least one real task of this practice delegated to the agent: what was asked, what it returned, and at least one case where the agent was wrong, how it was detected and how it was corrected, with evidence of the real session. Nothing is invented and nothing is reused from the previous logbooks. The agent drafts each entry; Franklin reviews it before it is pushed.

## Definition of done

- The invoicing module works with at least five separable features, protected by session and role, and the README explains how to provoke each one. Administrators are rejected on the operations of whoever collects.
- `docs/maquina-de-estados.md` matches the code: tables of allowed and forbidden transitions, terminal states, and the note that the machines are independent.
- The suite has one test per row of the tables and per terminal state, and passes at the tag with one command.
- Four defect tags exist, each parent is `practica-3`, each commit touches only production code, and each one makes at least one test fail. The README `Defectos` names the test of each one. Defect 4 is detected by the integration test.
- The integration test shows the operation rejected while the request is not approved and allowed after an Administrator approves it.
- Access control and Permissions work in the five fronts of section 1.5, following the README.
- Documentation updated in the same pull requests, including the requirement files and the index.
- Tags pushed and the repository URL and tag name submitted in Moodle, after Franklin's authorization.

## How it will be checked

`git clone` and `git checkout practica-3`; the command of the README `Pruebas` section; for each defect, `git diff practica-3 practica-3-defecto-N --stat`, the commit message, and the same command over the tag, which must report at least one failure; the README `Defectos` section against the tests that fail, and how to provoke each feature of the business; `docs/maquina-de-estados.md` against the code and the integration test; and Access control and Permissions started from the README, in their five fronts.

## Delivered earlier

Práctica 1 (Access control, mail queue and the structure of the business state machine), Asignación 2 (Permissions management and its first suite) and Práctica 2 (full Permissions suite and its six planted defects) are delivered. Their decisions live in `docs/adr/`; their commands in `README.md`; their briefs in `docs/assignments/`.