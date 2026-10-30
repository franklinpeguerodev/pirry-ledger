# Architecture Decision Records

One-line index of the ADRs in this folder. The title and date below come from each ADR's heading; this index does not modify or summarize any ADR — open the file for the context, alternatives, and consequences.

- `001-session-credential.md` (2026-09-30) — Session credential: opaque token with a session table.
- `002-first-administrator.md` (2026-10-01) — First Administrator: seed from environment variables.
- `003-password-change-traceability.md` (2026-10-02) — Password change traceability with a nullable UTC timestamp.
- `004-snake-case-column-naming.md` (2026-10-03) — Snake case column naming for PostgreSQL.
- `005-listen-address-from-public-base-url.md` (2026-10-04) — Listen address comes from `PIRRY_LEDGER_PUBLIC_BASE_URL`.
- `006-local-env-configuration.md` (2026-10-04) — Local configuration from a `.env` file loaded with `DotNetEnv`.
- `007-mail-sender-cli-command.md` (2026-10-04) — Mail sender: CLI command `--send-mail` in the Host.
- `008-permission-grants-table.md` (2026-10-20) — Permission grants live in their own table.
- `009-role-changes-invalidate-sessions.md` (2026-10-20) — Role changes invalidate sessions via CredentialVersion.
- `010-role-as-text-in-responses.md` (2026-10-24) — Role travels as text in API responses.\n- `011-consume-permission-grant.md` (2026-10-30) — Consume a permission grant: naming convention, deletion of the grant, and ordering with the invoice transition.