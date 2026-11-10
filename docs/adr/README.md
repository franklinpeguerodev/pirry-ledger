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
- `010-role-as-text-in-responses.md` (2026-10-24) — Role travels as text in API responses.
- `011-consume-permission-grant.md` (2026-10-30) — Consume a permission grant: naming convention, deletion of the grant, and ordering with the invoice transition.
- `012-request-permission-from-business.md` (2026-10-31) — Request a permission from a business operation.
- `013-admin-created-users.md` (2026-11-04) — Users are created by an Administrator, not by a public registration.
- `014-password-change-permission.md` (2026-11-04) — Every password change of a Standard requires an Administrator's approval.
- `015-imagenes-del-catalogo.md` (2026-11-09) — Product images: private storage in Cloudflare R2, signed URLs, and synchronization by a revision cursor.
- `016-full-payload-signing-for-r2-uploads.md` (2026-11-10) — R2 uploads sign the full payload instead of using chunked signing.
- `017-document-storage-in-seaweedfs.md` (2026-11-10) — Document storage in SeaweedFS over S3.
- `018-migrations-inside-the-composition.md` (2026-11-10) — Migrations run inside the composition.
- `019-email-inside-the-composition.md` (2026-11-10) — Email in the composition: Mailpit as SMTP sink and a mailer service.
- `020-listen-address-inside-a-container.md` (2026-11-10) — Listen address inside a container (amends ADR 005).`