# Current iteration — Assignment 3: Audit and delegation

## Current work

Planning requested by Franklin on 2026-11-28. Preparation is authorized;
execution follows Franklin's review of docs/plan-ejecucion-asignacion-3.md.
Franklin authorized publishing this planning branch and opening its PR on 2026-11-28.
No implementation decisions are currently open for starting this audit.
Any important choice exposed by a finding follows AGENTS.md approval before coding.

Franklin reconfirmed that ADR 023 and ADR 024 channel policies are approved.
Audit operational channels against that policy. Preserve RF-NOT-06 original
academic wording and the documented difference as a historical antecedent.

## Scope and sources

Assignment: docs/assignments/asignacion-3.md, transcribed from Franklin's PDF.
Audit Practice 5 notifications, email queue and reports, then fix confirmed findings.
Plan: docs/plan-ejecucion-asignacion-3.md.
Audit evidence, created during execution: docs/auditoria-asignacion-3.md.
Required logbook: docs/bitacora-asignacion-3.md.

Read docs/requirements/README.md first. Requirement sources in that directory:
notifications.md, reports.md, design-rules.md, permissions.md, access-control.md,
business-module.md and entities.md.
Audit RF-NOT-01 through 13 and RF-REP-01 through 05, especially RD-02, RD-03,
RD-06, RD-07, RD-08, RD-10 and RD-11. Regression tests follow RD-12.
Preserve previous requirements through the six accumulated regression fronts.

Original criteria: docs/criterios-notificaciones-reportes.md.
Business rules: docs/reglas-operativas-practica-5.md.
Report definitions: docs/business-report-contract.md.
Previous assignment: docs/assignments/practica-5.md.
Relevant decisions: ADR 023 through 034 and earlier ADRs used by a tested flow.
Historical evidence: docs/aceptacion-practica-5.md and
docs/verificacion-docker-practica-5.md. Recheck behavior during this assignment.

## Baseline and Git workflow

Local baseline: develop 860d0fd10f887d4a49c0ef4691737883c8d792c2.
Main 1fa48d2 has the same tree. Confirm remote state before audit execution.
Preparation branch: docs/assignment-3-planning, based on develop.
Subsequent branches target develop after their dependencies are integrated.
One correction branch and one fix commit per confirmed finding.
Fix subjects cite H-XX and applicable requirement IDs. Include regression and
affected documentation with the fix; logbook entries have separate commits.

Franklin reviews the plan before execution, each correction plan before coding,
and commits and logbook before publication. This planning PR is authorized.
Publication of later branches/PRs requires his authorization. Merge and delivery tag require explicit
authorization. PRs retain the existing four-section template.

## Acceptance criteria

- At least three reproducible findings including notifications and reports.
  Each has a number, justified severity, violated criterion/requirement, exact
  baseline, setup, executed command and observed result.
- Each finding declared fixed is corrected in its own commit citing its number.
  Review recurrence of the root cause. Low-severity deferrals are explicit,
  justified and reviewed by Franklin.
- Each fixed finding has a new independent xUnit regression with Spanish name,
  identifiable arrange/act/assert and H-XX in its name or comment. It fails for
  expected behavior on exact parent code and passes on corrected code.
  Compilation errors do not demonstrate the defect. Use doubles and fake clock.
- README contains one full-suite command in its tests section, executed from a
  clean clone. Verify build and dotnet test pirry-ledger.slnx --configuration Release.
- Verify README startup with docker compose up --build and the six inherited
  fronts: registration/activation, sessions/roles, recovery, permissions,
  Billing/business days and document persistence after container recreation.
- Verify real SQL, persistence, SMTP and concurrency where applicable, with safe
  evidence. An environment limitation leaves that criterion unverified.
- Logbook is organized by finding with all six assignment fields, real delegation
  evidence, explanation of escaped defects, correction instructions and commits,
  and preventive changes visible in context or criteria. Attribute agent and
  Franklin checks accurately. Keep original criteria traceable by baseline SHA.
- Final candidate replays defects on parent commits and verifies all regressions
  and corrected behavior on delivery tree. Tag asignacion-3 only when authorized.

## Out of scope

New business features, mobile changes, a general audit module, infrastructure
changes and operational data resets. Use existing infrastructure and isolated
test data. Preserve module boundaries, approved channel policy and report contracts.
