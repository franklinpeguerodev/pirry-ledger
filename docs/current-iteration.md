# Current iteration — Practice 5: Notifications and reports

## Current feature — Business reports

Core report integrated through PR 113 at 37f0a76.
feature/business-sales-reports implements the seven business report families and
the separate open-day sales view under approved ADR 034 and decisions 1–8.
See business-report-contract.md for exact filters, endpoints, fields and paging.
Each response uses a read-only RepeatableRead snapshot. Pending and settled
debts have separate queries. Reopened closing snapshots remain informational.
Reports use only contracts and preserve module-owned queries. No new tables.
The feature remains subject to PR review; accumulated acceptance follows.

## Scope

Base and PR target: `develop`. Preparation: `docs/practice-5-planning`.
One branch per major feature; sequence: `docs/plan-ejecucion-practica-5.md`.
Logbook: `docs/bitacora-practica-5.md`.

Assignment: `docs/assignments/practica-5.md` (takes precedence).
Read `docs/requirements/README.md` first. Requirement sources:
`notifications.md`, `reports.md`, `business-module.md`, `permissions.md`,
`design-rules.md`, `access-control.md` and `entities.md` in that directory.
Applicable: RF-NOT-01 through 13, RF-REP-01 through 05,
RF-NEG-02 through 06 and RF-NEG-09, RF-GP-01 through 07, RD-01 through 12.
Preserve the access-control baseline.

Business decisions: `docs/reglas-operativas-practica-5.md`.
Acceptance scenarios: `docs/criterios-notificaciones-reportes.md`.
Preparation authorized 2026-11-19; implementation is being delivered by feature.
Invoice ownership integrated in develop (ADR 025).
Scoped repository combines branch and issuer; no standard branch transfers.
Debt-specific cross-issuer queries and verified payments integrated in develop
(ADR 027, PR 105). Ordinary invoice queries
remain private; Standard responses hide debt payment history.
Branch business days integrated in develop (ADR 026).
Close/reopen and first-invoice opening are coordinated per
branch; closed snapshots include pending cancellation exclusions through a Core
contract. Dedicated undelivered recovery and shared sales calculations are
integrated in develop (ADR 028, PR 106).
Only the Standard issuer recovers, retaining the original invoice/day and
leaving saved closings unchanged. Pending cancellation exclusions and resolution
are covered across Billing and Permissions. Reports remain in subsequent
features; notification delivery status follows below. No closing revision history is implemented.
Notification-center prerequisite integrated in develop (ADR 029, PR 107).
Own inbox integrated in develop (PR 108):
list without automatic reading, mark one/all with server-side ownership.
Notification producers, active-recipient selection and administrator email preferences
integrated in develop (ADR 030, PR 109).
Billing and Permissions operations join the explicit common transaction; source
operation, internal notice and linked mail commit or roll back together.
Stable event/recipient keys prevent duplicate deliveries without a general events table.
Five-attempt queue and administrative inspection integrated in develop
(ADR 031, PR 110). Session-lock exclusion
failed in the configured environment; Franklin authorized an exclusive coordination
transaction, verified with concurrent connections. Interrupted and uncertain
deliveries are not retried; safe metadata inspection is administrative only.
Overdue reminders integrated in develop through PR 111
(ADR 032): Notifications owns one durable control table; Billing
supplies candidates and locked revalidation through Core.Contracts. Transfers
strictly older than 96 hours and open days at their 16:00 RD deadline generate
at most 50 newly recorded cases per execution, once per source even with no
active administrators. Each case commits or rolls back independently. Combined
CLI scans reminders then sends mail; Docker waits 30 seconds after completion.
Resolved overdue emails are cancelled before claiming without an attempt;
validation errors leave them pending. No push or changes to business states.
Operational reset: reviewed PostgreSQL data and referenced R2 images removed.
See `docs/limpieza-datos-practica-5.md`. Franklin approved preserving sessions and access codes.
Identity/access rows were preserved and operational counts verified as zero.
Five unreferenced R2 objects remain for ownership review; unregistered SeaweedFS
objects were not inventoried. Do not claim all external storage is empty.

- Invoice ownership, branch business days, closing snapshots and explicit reopening.
- Branch debt payments with verified transfers and role-specific views.
- Pending cancellation exclusions and recoverable undelivered invoices.
- Internal notifications and approved selective email policy.
- Five-attempt mail queue and administrative inspection; uncertain sends not retried.
- Individually timed, once-only reminders through CLI.
- Reports module, permission aggregation, seven business reports and open-day totals.
- Reviewed operational data reset preserving users, branches and assignments.

## Acceptance criteria

- Approved scenarios pass, including rejections without state/data changes.
- Build and tests pass; unit tests use doubles and fake clock.
- Verify real transactions, concurrency, persistence and email separately.
- Context and criteria committed before new notification engine implementation.
- README commands executed; documentation and logbook ship with each behavior.
- No Core-to-Business reference or cross-module table access.
- RF-NOT-06 remains a known deviation (ADR 023 and 024), not professor-approved.
- Complete accumulated review and Franklin's final review.
- Delivery tag practica-5 only with explicit authorization.

## Workflow

Commits are atomic; 100–200 changed lines is a guideline, not a hard limit.
Larger cohesive changes are allowed with a stated reason; do not split solely by size.
Agent is authorized to push and create PRs; Franklin reviews and approves.
No merging or tagging without explicit approval. Branches start from develop
after their prerequisites are integrated. Important decisions need alternatives
and approval before implementation. Reset target and exact inventory must be
reviewed before execution. Previous iteration closed by Franklin; its historical
PostgreSQL verification limitation remains in its logbook.

## Out of scope

Mobile work, exports, inventory, purchases, expenses, profit, cash counting,
initial cash funds, withdrawals, monetary refunds and general audit module.
No historical closing revisions; keep latest closing only. Debt payment history
is still in scope. New invoice states are not planned.
