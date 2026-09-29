# Business module: state machine structure

Source: Requerimientos del Core, sección 9, and Práctica 1, sección 1.6. Only the requirements Practice 1 uses are included. The remaining RF-NEG requirements are added when an iteration needs them.

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-NEG-03 | Máquina de estados propia, entre 3 y 5 estados. | Los estados están declarados en un solo lugar del código. |
| RF-NEG-04 | Al menos una transición prohibida de forma explícita. | Intentarla se rechaza y el estado no cambia. |
| RF-NEG-05 | Al menos un estado terminal del que no se puede salir. | Ninguna transición parte de ese estado. |

Related design rule: RD-04 (transitions are resolved in a single point of the code; see `design-rules.md`).

## What Practice 1 delivers (structure only, no tests yet)

Tests arrive in week 8. What is delivered now:

- The central entity of the domain exists in the data model, with its state attribute.
- The states are declared in one place in the code (RF-NEG-03), between 3 and 5.
- The allowed transitions are declared in one place (RD-04), with at least one explicitly forbidden transition (RF-NEG-04) and at least one terminal state (RF-NEG-05).
- A file `docs/maquina-de-estados.md` (exact name required by the assignment) with the transitions table: from, to, who executes it, condition. Like the Gestión de permisos table in the Core requirements.

## Context

The business module has its own state machine, independent from the Core's permission requests one. From week 8 the project has two state machines tested separately, so there are two test suites, not one.