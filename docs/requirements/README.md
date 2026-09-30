# Requirements

Index of the requirement files. Read this file first, then open only the files that `docs/current-iteration.md` lists. Requirement text is the professor's original Spanish, kept verbatim so that IDs and citations match exactly.

| File | Contains | IDs |
|---|---|---|
| `design-rules.md` | Cross-cutting design rules that apply to the whole system | RD-01 to RD-12 |
| `access-control.md` | Registration, activation, session, roles, user administration, passwords | RF-CA-01 to RF-CA-22 |
| `entities.md` | Minimum attributes of the entities in use | Usuario, Rol, CodigoRecuperacion, CorreoEnCola |
| `notifications.md` | Minimal outgoing mail queue | RF-NOT-08, 09, 12, 13 |
| `business-module.md` | Structure of the business state machine | RF-NEG-03, 04, 05 |
| `practice-1.md` | Assignment brief, rubric, delivery and how it is checked | rubric criteria |

## Precedence

If a `practice-N.md` file and another requirements file disagree, the practice file wins because it is newer and more specific. Example: the Core document says RF-CA-13 must be audited, but Practice 1 says audit records are not graded until week 14.

## Adding files

A new file is added only when an iteration needs it, and this index is updated in the same commit. Requirements that no iteration uses yet are not stored here.