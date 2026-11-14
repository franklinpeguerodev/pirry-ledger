# Current iteration — Cancel invoices on approval

## Scope

Base and pull request target: `develop`. Working branch:
`feature/cancel-invoice-on-approval`. Logbook:
`docs/bitacora-anulacion-automatica.md`.

Sources: `docs/requirements/business-module.md`,
`docs/requirements/permissions.md` and `docs/requirements/design-rules.md`.
Applicable IDs: RF-NEG-06, RF-GP-03, RF-GP-05, RF-GP-06, RF-GP-07,
RD-03, RD-04, RD-06 and RD-12.

## Acceptance criteria

- Approving a cancellation request cancels only its paid or in-debt invoice.
- The invoice is cancelled when approval returns and the request is applied.
- If cancellation fails, the request remains pending.
- Existing grants can still be consumed and unrelated permission approvals
  follow their existing flow.
- Build and tests pass. Update the README, state machine, ADR and logbook.

## Out of scope

Mobile UI changes and new invoice states.
