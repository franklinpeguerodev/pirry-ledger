# Current Iteration

Franklin replaces this file at the start of each assignment. The agent reads it before planning any work and treats it as the scope boundary. If it is missing or stale, the agent asks.

## Active assignment

**Practice 1 — Complete access control** (Programación III, Week 4, 8 points, individual).
Full brief and rubric: `docs/requirements/practice-1.md`.

## Requirement files to read

`design-rules.md`, `access-control.md`, `entities.md`, `notifications.md`, `business-module.md` and `practice-1.md`, all in `docs/requirements/`. Read the index (`docs/requirements/README.md`) first.

## Goal

Deliver the first working Core piece, Access control, from registration with email activation to password recovery and user administration, plus the declared structure (no tests) of the business state machine.

## In scope

| Area | Requirement IDs |
|---|---|
| Registration and activation | RF-CA-01, 02, 14, 15, 16, 17 |
| Session | RF-CA-03, 07, 18, 19 |
| Roles and user administration | RF-CA-04, 05, 06, 08, 20, 21 |
| Passwords (recovery, forced reset, change) | RF-CA-09, 10, 11, 12, 13, 22 |
| Minimal outgoing mail queue | RF-NOT-08, 09, 12, 13 |
| Business state machine (structure only) | RF-NEG-03, 04, 05, RD-04 |
| Cross-cutting design | RD-05, 06, 07, 08, 09, 10 |

## Out of scope (do not build)

- Mail retries, failed state, last error, admin view of the queue (Week 11).
- Audit records for RF-CA-08, 13, 20 (Week 14). Design so they can be added later, but do not implement them.
- Permission requests, documents, notifications, reports.
- Tests of the business state machine (Week 8).

## Constraints specific to this iteration

- The session credential must be invalidatable on the server. Logging out (RF-CA-18), deactivating a user (RF-CA-20) and changing a password (RF-CA-12) invalidate sessions already issued. A purely self-contained credential with no server-side check does not meet this. **Decided on 2026-09-30:** an opaque token with a `Sesion` table and a `CredencialVersion` copied per session, recorded in `docs/adr/001-session-credential.md`.
- Lockout: 5 consecutive failures block the account for 15 minutes; the counter is persisted and resets on a successful login (RF-CA-19).
- Forced password reset by an Administrator (RF-CA-13): the old password stops working and the user receives, through the queue, the email with the code to set a new one.
- Operations never send mail directly. They write a `CorreoEnCola` row as pending and finish successfully even with no SMTP server. A separate process or command sends pending mail over SMTP and marks it sent; running it twice must not duplicate sends. SMTP credentials come from environment variables.
- Business state machine: the central domain entity with a state attribute; 3 to 5 states declared in one place; allowed transitions declared in one place, with at least one explicitly forbidden transition and one terminal state; `docs/maquina-de-estados.md` (exact name required by the assignment) with the table (from, to, who executes, condition). The invoice is the candidate entity (for example Draft → Issued → Paid, with Cancelled as terminal), but the choice is Franklin's.

## Definition of done

- At least four pull requests, each with the four sections: registration and activation, session, password recovery, user administration.
- Atomic commits with an imperative subject and the requirement ID.
- README with exact run steps, environment variable names and purpose (never values), and how to trigger each acceptance criterion.
- No credentials or generated files anywhere in the history.
- Tag `practica-1` pushed, and the repo URL and tag name submitted in Moodle.

## How it will be checked

The repo is cloned, the tag checked out, and the README followed to the letter. Then: register with a real email, try logging in before activating, open the link twice, register a duplicate, try a 5-character password and a malformed email, compare wrong-password and unknown-email rejections, fail five times then use the right password, log out and reuse the credential, call an Administrator operation by hand as a Standard user, list users, change a role, deactivate a user with an open session, try to deactivate yourself, run recovery with an existing and a non-existing email, reuse the code, force a reset, change password with a wrong current one, cut SMTP access and register, run the sender twice, restart the app and confirm users persist, then inspect `git log -p` for credentials and the pull requests on GitHub.

## Regression note

From Practice 2 on, two of the eight points in each practice re-verify this work: registration with activation, login, role rejection and password recovery. Do not break what is delivered here.

## Decisions resolved for this iteration

- The business state machine is carried by `Invoice`, with `Draft`, `Issued`,
  `Paid` and `Cancelled` states. The transitions and terminal states are
  documented in `docs/maquina-de-estados.md`.
- Recovery and forced-reset codes are valid for 15 minutes.
- `Usuario.ContrasenaCambiadaUtc` records in UTC when the password was last
  replaced. It is nullable for accounts that still use their initial password
  and provides minimal credential traceability without implementing the audit
  records reserved for week 14. The decision is documented in
  `docs/adr/003-password-change-traceability.md`.
- The address the API listens on comes from `PIRRY_LEDGER_PUBLIC_BASE_URL`, the
  same environment variable that builds the activation links: one source for
  both, so they cannot drift apart. The `launchSettings.json` profiles no longer
  define a port (they only set the environment). Documented in
  `docs/adr/005-listen-address-from-public-base-url.md`.
- Local development reads its environment variables from a `.env` file at the
  repo root, loaded with `DotNetEnv` before the configuration is built. The
  environment always wins (`NoClobber`: a variable already defined in the
  terminal or in the system is never overwritten) and a missing `.env` is not an
  error. Only `.env.example` (names and descriptions, no values) is committed.
  Documented in the README, step 4.

Resolved and therefore no longer listed: the session credential mechanism (`docs/adr/001-session-credential.md`, accepted 2026-09-30), the data stack (PostgreSQL with EF Core migrations, applied in the README), and how the first Administrator is created (`docs/adr/002-first-administrator.md`, accepted 2026-10-01: a seed from environment variables that runs on startup).

Administration decisions confirmed for the `feature/user-administration` work:

- An Administrator cannot change their own role.
- An Administrator cannot deactivate their own account.
- The last active Administrator cannot be deactivated.
- Logout is idempotent and responds successfully even when its credential is
  missing, invalid or expired.