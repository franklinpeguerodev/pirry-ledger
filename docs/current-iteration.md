# Current iteration — Practice 5: Notifications and reports

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
Preparation authorized 2026-11-19; implementation not yet delivered.
Operational reset: read-only database inventory completed; deletion is pending.
See `docs/limpieza-datos-practica-5.md`. Franklin approved preserving sessions and access codes.
Rollback-only deletion preview passed; no rows or external objects were removed.

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
