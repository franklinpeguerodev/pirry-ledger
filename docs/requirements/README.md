# Requirements

Index of the requirement files. Read this file first, then open only the files that `docs/current-iteration.md` lists. Requirement text is the professor's original Spanish, kept verbatim so that IDs and citations match exactly.

The assignment briefs (what each practice or assignment asks, its rubric and how it is checked) live in `docs/assignments/`, not here.

| File | Contains | IDs | Status |
|---|---|---|---|
| `design-rules.md` | Cross-cutting design rules that apply to the whole system | RD-01 to RD-12 | Always applies |
| `access-control.md` | Registration, activation, session, roles, user administration, passwords | RF-CA-01 to RF-CA-22 | Delivered (Practice 1). Kept as the baseline that must not break |
| `notifications.md` | Minimal outgoing mail queue | RF-NOT-08, 09, 12, 13 | Delivered (Practice 1). Kept as the baseline that must not break |
| `business-module.md` | State machine, its conditions and the operations of the invoicing module | RF-NEG-02 to 06, RF-NEG-09 | Delivered (Practice 1 structure). Extended in Practice 3 |
| `permissions.md` | Permission requests and their state machine | RF-GP-01 to RF-GP-07 | Delivered (Asignación 2). Its suite is extended in Práctica 3 |
| `entities.md` | Minimum attributes of the entities in use | Usuario, Rol, CodigoRecuperacion, CorreoEnCola, SolicitudPermiso | Always applies |

A file marked Delivered is not read for new work unless `docs/current-iteration.md` lists it. It stays because later graded reviews re-verify it.

## Precedence

If a file in `docs/assignments/` and a requirements file disagree, the assignment file wins because it is newer and more specific. Example: the Core document says RF-CA-13 must be audited, but Practice 1 says audit records are not graded until week 14.

## Adding files

A new file is added only when an iteration needs it, and this index is updated in the same commit. Requirements that no iteration uses yet are not stored here.