# Current iteration — Práctica 4: Document Manager, container, external API

> Replaces the Práctica 3 version. Scope source: `docs/assignments/practica-4.md`
> (Week 9, 8 pts, individual). If this file and the brief disagree, the brief wins.
>
> **RF-DOC-01 to RF-DOC-05 are not in `docs/requirements/`.** Verified 2026-11-10:
> the index lists access-control, business-module, design-rules, entities,
> notifications and permissions only. For this iteration the brief is the only
> source for RF-DOC-01..05, and nothing is reconstructed from memory.

## Goal

Anyone who clones the repo, copies `.env.example` to `.env` and runs
`docker compose up --build` gets the whole project running, with documents and
application data surviving `docker compose down` (no `-v`), plus one real
external API (SeaweedFS) whose key never enters the repo.

## Decided by Franklin (2026-11-10)

Not open anymore. Do not re-ask; build the plan on these.

- **The external API is SeaweedFS** (open source, Apache-2.0), self-hosted in the
  composition: it is where the Document Manager writes and reads the bytes, over
  the S3 API.
- **Open source version only**, from `chrislusf/seaweedfs`, with the version
  pinned. No enterprise features. This iteration is Docker only: nothing is
  deployed to the cloud.
- **Documents are generic**: any file type, up to **5 MiB** (5 242 880 bytes).
  No PDF-only rule, no image handling, no content is ever decoded.
- **Standard users see only their own documents; an Administrator sees all.**
- **The host port is 8090**, the same number inside and outside the container.
- **Migrations run inside the composition**: a `migrate` service applies them
  with a `--migrate` flag and exits; the app waits for it.
- **Email in the composition**: `mailpit` as the SMTP sink with no published
  port, plus a `mailer` service that runs `--send-mail` in a loop.
- **The listen address**: `PIRRY_LEDGER_PUBLIC_BASE_URL` stays the public
  address (activation links) and the port, and the process binds `0.0.0.0` on
  that port, because a Docker published port does not reach a process bound to
  loopback (ADR 020, which amends ADR 005).
- **Product images keep R2** (ADR 015 unchanged): `.env.example` carries the
  seven variables with example values, the app starts, and image upload answers
  a controlled 503. The README says so.
- **Verification happens in a GitHub Codespace** (Franklin has no local Docker).
  README commands must work on Linux (bash, case-sensitive paths) and on his
  Windows machine.
- **First Administrator variables** (`PIRRY_LEDGER_FIRST_ADMIN_EMAIL` /
  `..._PASSWORD`) go in `.env.example` with example values.
- **The logbook is `docs/bitacora-practica-4.md`.**
- **Franklin decides at the end** where `practica-4` is tagged. The agent does
  not tag.

## In scope

### A. Document Manager (Core piece 3) — 2.0 pts + 1.0 pt (volume)

Requirements: RF-DOC-01 to RF-DOC-05. Also RD-06, RD-07, RD-09.
All operations require a valid session.

| ID | Acceptance criterion (from the brief) |
|---|---|
| RF-DOC-01 | "Subir un documento registrando nombre, tipo, tamaño, fecha y quién lo subió." / "El registro queda consultable con todos esos datos." |
| RF-DOC-02 | "Listar los documentos que el usuario tiene permitido ver." / "El listado no incluye documentos eliminados ni de otros usuarios, salvo para el Administrador." |
| RF-DOC-03 | "Descargar un documento." / "Solo el dueño o un Administrador lo logran; cualquier otro usuario recibe un rechazo." |
| RF-DOC-04 | "Eliminar un documento de forma lógica, no física." / "Deja de aparecer en el listado, pero el registro sigue existiendo en el almacenamiento." |
| RF-DOC-05 | The bytes live in a named volume, outside the image. After `docker compose down` (no `-v`) and `up`, the document is listed and downloads. |

- RF-DOC-05 appears in the brief's section 1.3, not in its table; it is listed
  here so it is planned and verified like the others.
- The brief states who may delete only in the rubric: owner or Administrator,
  and another Standard user is rejected. Apply that.
- **The record** (`doc_documents`): id, original name, declared type, size in
  bytes, uploader, branch, upload date, logical-delete date, deleter and the
  storage key. Dates come from `IClock` (RD-11). The branch is the uploader's
  branch from the session, never from the request body; for an Administrator it
  is the global branch `Branch.AllId` ("Todas"), the row the seed already
  creates. No foreign key to another piece's table: each module owns its data.
- **Size limit: 5 MiB** (`PIRRY_LEDGER_DOCUMENT_MAX_BYTES`). The application's
  general body limit is 1 MiB (ADR 015), so the upload route raises it the way
  the image route already does (route limit = 5 MiB + 64 KiB of multipart
  overhead). An absent or empty file is rejected in a controlled way (RD-07).
- **Documents are generic**: no file type is inspected and nothing is decoded.
  The declared type is stored as data and is not trusted for anything else;
  downloads are always served as an attachment.
- The server generates the storage key (a new `Guid`); the original name is
  kept in the database only.
- RD-06: authorization is enforced on the server. Declare the operations with
  `.WithAccess(Operation.X)`; endpoints do not compare roles. Ownership is a
  use-case rule in Application, compared by identity (like RF-GP-07).
- Fits the existing architecture: a new Core piece with Domain / Application /
  Infrastructure / Api, its own `DbContext` and migrations, its own table
  prefix (`doc_`), `app.MapDocuments()` in the Host and no controllers there
  (RD-01, RD-02). The Core must still compile without `Src/Business` (RD-03).
- New module -> **its own test project from day one**: no app, no DB, no
  network, in-memory doubles and a fake clock (RD-12). Test names in Spanish,
  three phases each, one behavior each. A rejected operation also verifies that
  nothing changed.

### B. One-command container — 1.5 pts

- `Dockerfile` (exists), `.dockerignore` (exists), `docker-compose.yml` (to
  write), `.env.example` (to extend).
- `docker compose up --build` from a clean clone, with nothing installed but
  Docker and no manual steps (no creating a database, no running scripts, no
  `dotnet ef`).
- Services: `db`, `seaweedfs`, `migrate`, `mailer`, `mailpit`, `app`. They
  reach each other by service name, not `localhost`.
- Only the app publishes a port (`8090`); the database and SeaweedFS are not
  reachable from outside the composition.
- README section **Contenedor**: the command, the full address
  (`http://localhost:8090`) and how to stop it. The README is verified by
  running it, not by reading it.
- `.dockerignore` leaves out dependencies, build folders (`bin/`, `obj/`) and
  `.git`.

### C. Data survives — 1.0 pt

- The document bytes live in the named volume `pirry-ledger-documents`, mounted
  at `/data` in the SeaweedFS service, outside the image (RF-DOC-05).
- Application data (users, requests, document records) survives too (RD-09);
  the database data folder goes in the second volume, `pirry-ledger-db-data`.
- Acceptance test: upload -> `docker compose down` (no `-v`) -> `docker compose
  up` -> the document is listed and downloads, and the registered user still
  logs in.
- The README names both volumes and shows how to see the bytes inside the
  SeaweedFS volume, so the criterion is demonstrable and not only asserted.

### D. External API with its key as a secret — 1.5 pts (SeaweedFS)

**The operation (decided):** the Document Manager stores and serves the bytes
through SeaweedFS's S3 API. The app is not a relay: it decides what is
accepted, generates the key, and decides who may see the document.

- Code in English, following the project's conventions; text the user sees in
  Spanish; test names in Spanish.
- The port lives in Application (`IDocumentStore`); the adapter
  (`SeaweedDocumentStore`) lives in Infrastructure and talks S3 with the
  existing `AmazonS3Client` pattern of ADR 015/016: `ServiceURL`,
  `ForcePathStyle = true`, `UseChunkEncoding = false`. Tests run without
  network using a fake (RD-12).
- **The key is the SeaweedFS access key and secret key.** They travel in
  environment variables from the unversioned `.env`; `.gitignore` excludes it.
  The SeaweedFS service and the application read the same `.env` values, so the
  bucket and the client always agree. The bucket is created by SeaweedFS at
  startup (`S3_BUCKET`); the app never creates buckets.
- `.env.example` is versioned with **every** variable the project needs and an
  example value for each. Copying it to `.env` is the only step before
  starting.
- The key is not in code, `Dockerfile`, `docker-compose.yml`, the image, or any
  commit of history. The compose file only references `${VARIABLE}`; the values
  live in `.env`.
- **No key or SeaweedFS down -> controlled rejection** with an understandable
  message (RD-07, RD-08) and the rest of the project keeps working. The
  application starts without the storage key: this differs from ADR 015's "all
  variables are mandatory", because the brief requires the rest of the project
  to keep working.
- README section **API externa**: what SeaweedFS is (open source, Apache-2.0),
  which operation uses it, the variable names, and a real example response
  (without the key).
- **Known risk, and how the README answers it.** The brief's examples are
  third-party data APIs (weather, exchange rate, geolocation); SeaweedFS is a
  real external service with a key, but it runs inside the same composition.
  The brief's "with the example key (controlled rejection)" therefore does not
  happen literally: the example key works, because the same example key
  configures the server. The README shows the two rejection paths with the
  exact commands: the key removed from `.env` (503, storage not configured) and
  `docker compose stop seaweedfs` (503, storage not available). The storage
  port is the seam where a third-party service would be added, without touching
  the domain.

### E. Cumulative review — 2.0 pts

Started from the tag, following the README to the letter, six fronts must work:
registration with email activation; login and logout; a Standard user rejected
on an Administrator operation; password recovery; permission requests (create,
approve, reject, out-of-table transitions rejected); and Business (a valid
operation works, the prohibited invoice transition is rejected).

## Out of scope (do not build)

- RF-DOC-06, audit of uploads and deletions: arrives with Piece 6 in week 12,
  not graded. No hooks or half-built audit tables.
- RF-NEG-10, attaching documents to a business entity (optional, not asked
  here). The Document Manager has no reference to Business.
- Any file-type inspection, image processing, PDF parsing and the information
  card (mobile client).
- Mobile client (`pirry-ledger-mobile`), reports, administrator notice
  (RF-NEG-07), investment, full inventory, AI invoice scanning.
- Cloud deployment (Render/Railway/Neon) and R2 for documents.
- Changing the existing state machines or weakening any existing test.

## Deferred

| Item | What blocks it | Why it is not resolved now |
|---|---|---|
| Audit of document operations (RF-DOC-06) | Piece 6 does not exist yet | Scheduled for week 12; building it now would be a guess |
| Documents attached to entities (RF-NEG-10) | Optional requirement, no decision to use it | Would create a Core -> Business coupling nobody asked for |
| Third-party API with a real account key | The brief is satisfied by SeaweedFS | Only if the professor rejects a self-hosted external API |
| Cloud deploy / R2 for documents | Needs a hosting decision | The brief only asks for a local composition |

## Decisions already taken (authorized)

These are closed. The agent writes the ADR, drafts it and waits for Franklin's
review before pushing; it does not re-open them.

1. **ADR 017** — Document storage in SeaweedFS over S3.
2. **ADR 018** — Migrations run inside the composition.
3. **ADR 019** — Email in the composition.
4. **ADR 020** — Listen address inside a container (amends ADR 005).

## Definition of done

Not done until each point has evidence (real output, rejections included).
Last verification: GitHub Codespace, 2026-11-11.

- [x] RF-DOC-01 verified: `POST /api/documents` answered `201` with the record —
      name, declared type, size, date, uploader and branch — and the listing
      returned the same data. The real JSON is in the README.
- [x] RF-DOC-02 verified for the Administrator: `GET /api/documents` answered
      `200` with the documents of every user. The branch recorded was `Todas`
      because the uploader was the Administrator.
- [x] RF-DOC-03 verified for the owner: the download answered `200` with
      `Content-Type: application/octet-stream`,
      `Content-Disposition: attachment` and the same bytes that were uploaded.
- [x] RF-DOC-04 verified: `DELETE` answered `204`, the document disappeared from
      the listing (`200` with the other documents) and the row stayed in
      `doc_documents` with `deleted_at_utc` and `deleted_by` set. The `psql`
      query showed it as `eliminado = t` and `quien = t`.
- [x] RF-DOC-03 for another Standard (rejected): with two Standards, one got
      `403` on download and `403` on delete of the other's document, while the
      Administrator got `200`. The Standard's listing showed only its own
      document.
- [x] RF-DOC-05 verified: after `docker compose down` (no `-v`) and
      `docker compose up`, the download answered `200` and returned the same
      content (`sobreviviente`) that had been uploaded before the restart.
- [x] SeaweedFS integration: the round trip works against the real service;
      missing key -> `503` configured; SeaweedFS stopped -> `503` unavailable;
      the rest of the project keeps working. Both rejections were repeated in
      the closing run, after the insert fix.
- [x] Document test suite passes with `dotnet test pirry-ledger.slnx`: 483
      tests, 0 failures. The insert bug was reproduced by a test that failed
      with the same `DbUpdateConcurrencyException` before the fix.
- [x] From a clean state (Codespace): `docker compose down -v`, copy
      `.env.example` to `.env`, then `docker compose up --build -d`; the app
      answers on `http://localhost:8090` and every service comes up.
- [x] `docker compose config` shows only example values; only the app publishes
      a port (`8090`), and neither the database nor SeaweedFS is reachable from
      outside.
- [x] Upload -> `docker compose down` -> `docker compose up` -> document listed
      and downloaded, user logs in. Never `-v`. Verified: the download answered
      `200` with the same bytes after the restart.
- [x] `git check-ignore -v .env` matches; `git log --all -- .env` is empty; no
      real key appears in any commit (only the example values of
      `.env.example`).
- [x] Six cumulative fronts walked through from the README commands, each with
      its real output: activation `201` + `200`, session `200`/`204`/`401`,
      Standard rejected `403`, recovery `202` + `204` + `204`, permissions `202`/
      `204`/`400`/`204`, business `Issued` -> `Paid` + `400`.
- [x] README has **Contenedor** and **API externa**, and every command that is
      documented was executed, including reading the activation email from
      Mailpit.
- [x] Closing run: 49 checks, 0 failures (the logbook has the detail).
- [x] `docs/current-iteration.md`, README and the four authorized ADRs in the
      same PR. The PR is Franklin's; the branch has all of it.
- [x] Bitácora entry: `docs/bitacora-practica-4.md`, as its own commit.

## Status: concluded

Práctica 4 is delivered (2026-11-11, GitHub Codespace). The evidence for every
acceptance criterion is in the checklist above and in
`docs/bitacora-practica-4.md`.

What remains is Franklin's, and it is not code:

- The pull request `develop` -> `main` that promotes the work.
- The tag `practica-4`, on the merge commit in `main`. The agent does not tag.

This file stays as the record of what the iteration asked and what it
delivered. The next iteration replaces it.

## How to work on this iteration

- Plan first: files touched, design decisions and alternatives, how each
  criterion is verified, what will **not** be done. Wait for Franklin's
  approval.
- Branch `feature/practica-4-documents-and-container`; never commit to `main`
  or `develop`; no push, force-push, merge or tag without Franklin's
  authorization.
- One change per commit, English imperative subject, requirement ID when it
  applies (e.g. `Add logical delete for documents (RF-DOC-04)`). Before
  proposing a commit: `git status` and `git diff --staged`; no `bin/`, `obj/`,
  generated files or secrets.
- `git check-ignore -v <path>` before adding any file. Do not "fix" the ignored
  `appsettings.*.local.json`. Never read or print `.env`; use `.env.example`.
- PR uses `.github/PULL_REQUEST_TEMPLATE.md` with exactly its four sections.
- Local shell is PowerShell; the Codespace is bash. Give both forms when a
  command differs.

## Authorized deployment adjustment (2026-11-12)

Franklin approved separating the public activation URL from the HTTP listener
port (ADR 020, RD-08, RD-10). The Host requires both
`PIRRY_LEDGER_PUBLIC_BASE_URL` and `PORT`. The acceptance checks cover HTTP and
HTTPS public URLs, explicit listener port selection, invalid configuration, and
an activation link using the public origin. Compose and Render use port 8090
in their standard configuration.
