# AGENTS.md — Pirry Ledger

Repository: `franklinpeguerodev/pirry-ledger`. This file is loaded every session. It holds permanent rules only. If something here is no longer true, tell Franklin instead of following it blindly.

## Project

- ERP for Pirri, a fast-food business with 2 branches and 2–3 employees per branch. It is real software (daily use from a tablet) and also a university project (Programación III, ITLA).
- Business module (Billing): invoicing (including the physical invoice given to the customer), daily sales, investment (purchases and expenses) and profit. An owner view shows income and how the business is doing. No delivery for now.
- Possible future ideas, do not build them unless asked: AI invoice scanning to feed investment, Excel or desktop-web versions, full inventory.
- Architecture: **modular monolith in C#/.NET**, single deployment, strict module boundaries. Do not propose microservices.
- The point of the course is learning to build with AI. Franklin directs and decides; you propose, execute and verify. He must be able to explain and defend every decision, so never decide silently on his behalf.

## Sources of truth

- `docs/current-iteration.md` defines what we are working on now: scope, requirement IDs, out-of-scope items and definition of done. Work only inside that scope. If the file is missing or looks stale, ask.
- Requirements live in `docs/requirements/`. Read its `README.md` (the index) first, then open only the files that `docs/current-iteration.md` lists. Cite requirements by ID (`RD-03`, `RF-CA-19`) in commits and PRs. Their text is the professor's original Spanish; do not translate or paraphrase the IDs.
- If a requirement you need is not in `docs/`, ask for it. Never reconstruct it from memory.
- An acceptance criterion that is not met means the requirement is not done, even if the code runs.
- If two documents contradict each other, say so and ask. Do not pick one yourself.
- The Core is built piece by piece. Do not build pieces that are not in the current iteration. Design so later pieces can be added (audit, notifications, documents, reports), but do not implement them early.

## Language

All development is in English: code, identifiers, comments, commit messages, branch names, technical docs and files for the agent. Course deliverables written for the professor (README, PR descriptions, logbooks in `docs/bitacoras/` named `bitacora-*.md`, architecture decision records in `docs/adr/`) stay in Spanish, following the existing PR template, unless Franklin says otherwise. ADRs are a deliberate exception to the "technical docs in English" rule: Franklin decided on 2026-09-30 that they are written in Spanish because they are read as course deliverables.

## Where the state lives

This file holds permanent rules only. It goes stale as soon as code is written, so it does not describe the repo:

- What we are building right now, the scope and how it is graded: `docs/current-iteration.md`. Franklin replaces it at the start of each assignment.
- What is already built and which commands actually run: `README.md`. It is verified by running them, not by reading.
- The decisions already taken, with their reasoning and the alternatives that were discarded: `docs/adr/`.

If the agent needs state that is not in those three files, it asks. It never reconstructs it from memory and never writes it here.

Franklin's machine: Windows, PowerShell 5.1, `dotnet` SDK 10.0.302, Node 24. Write commands for PowerShell. Do not use bash syntax (`&&`, `export`, `rm -rf`).

## Truthfulness rules

- **Never invent commands, paths, packages, versions or results.** Past error in this repo: a README documented `npm install` and `npm run dev` with no `package.json`. If a task needs a run command: create the project or manifest first, then document it, then run it.
- Verify with a command, not by eye: `Test-Path`, `git status`, `git check-ignore -v`, `dotnet build`.
- Run every command you document before proposing the commit, and show the real output.
- Cross-check every new file against the rest of the repo. If something contradicts, flag it before continuing.
- Never claim something works if you did not run it. If you could not verify it, say so.

## Architecture rules

- `Src/Core`: one module per Core piece. `Src/Business`: the business module (Billing). `Src/Host`: composition only (startup, module registration, configuration) plus background processes such as the mail sender. The Host has no controllers and no logic.
- One-way dependency (RD-03): the Core never references Business. Business references only the Core's contracts project. Mental test: delete Business and the Core must still build.
- Modules talk only through public interfaces. No module touches another module's internal classes or tables. Each module owns its data.
- Each module is layered (Domain, Application, Infrastructure, Api). Business logic lives in Domain and Application, never in controllers or presentation (RD-02). A module's controllers belong to that module (RD-01).
- Every piece must be testable without starting the full application (RD-12). Each module has its own test project.
- The Core connects to the business module through three contact points only: notify and feed reports, attach documents (optional), and protect endpoints with Core roles.
- Cross-cutting rules:
  - Authorization is enforced on the server for every operation (RD-06). Hiding something in the UI does not count.
  - State transitions are resolved in a single component (RD-04). Role requirements are declared in a single readable place (RF-CA-05).
  - All external input is validated; errors never expose stack traces, file paths or queries (RD-07, RD-08).
  - Dates and times in UTC through one injectable clock (RD-11).
  - Data persists outside the process (RD-09).
  - Secrets and keys come from environment variables only (RD-10).

## Security baselines

- Passwords are stored with a hash and per-user salt, never in plain text. Use a standard, proven implementation; never write your own cryptography.
- Tokens and codes are single-use and expire.
- Flows that involve an email address (activation resend, password recovery) respond identically whether or not the email exists.
- Outgoing email is never sent inside the operation that triggers it: it is queued and a separate process sends it. Sending twice must not duplicate emails.

## Git

- Branches: `type/short-description`, lowercase with hyphens. Types: `feature`, `chore`, `fix`, `docs`. One branch per feature.
- Never commit directly to `main` or `develop`. Never `push`, force-push or merge without Franklin's explicit approval. Confirm the base branch before creating the first branch.
- Commits: English subject, imperative mood, one change per commit, with the requirement ID when one applies. Example: `Add salted password hashing (RF-CA-02)`. Do not mix unrelated changes.
- Before proposing a commit, run `git status` and `git diff --staged`. No `bin/`, `obj/`, generated files or secrets. A credential in history is penalized even if deleted later, so check before committing, not after.
- Pull requests use `.github/PULL_REQUEST_TEMPLATE.md` with exactly its four sections (Qué cambia, Por qué, Cómo probarlo, Qué NO incluye). Do not add sections. Cite requirement IDs. Franklin opens the PR.

## Documentation

Three rules. They exist because this repo already paid for breaking them.

- **Documentation ships in the same pull request.** Every pull request that changes behaviour updates `README.md`, `docs/current-iteration.md` and the ADR in `docs/adr/` in the same PR. Documentation that a change makes wrong is part of that change, not a follow-up. The README section below is the same rule seen from the grading side: what the README does not say will not be looked for.
- **ADRs need authorization first.** An important decision is documented in an ADR under `docs/adr/`, following the format of `001-credencial-de-sesion.md`. **The agent never writes an ADR on its own initiative.** When it detects a decision that meets the bar, it stops, presents the alternatives with their trade-offs, and waits for Franklin's explicit authorization. Only then does it write the file. A decision is *important* when at least one of these is true: it chooses between alternatives that affect structure, persistence, the protocol or the security model; it contradicts or replaces an earlier decision; or it is the kind of question a reviewer asks as *"why not X?"*. A decision below that bar is explained in the pull request description instead.
- **The logbook gets an entry in the same pull request.** Every pull request appends its entry to the current logbook in `docs/bitacoras/` (`bitacora-*.md`) in the same PR, as its own commit so the code commit stays atomic: what was asked, what the agent returned, what Franklin verified and with which command, and what changed or was corrected. The agent drafts; Franklin reviews before it is pushed.

## Workflow

One requirement (or a small group) at a time:

1. Read the requirement and its acceptance criterion.
2. Propose a plan before writing code: files you will touch, design decisions with alternatives, how you will verify each criterion, and what you will NOT do. Wait for Franklin's approval.
3. Implement the minimum that meets the criterion.
4. Build, run the tests and show the real output.
5. Check the acceptance criterion point by point with evidence (command and result). Test rejections too, not only the happy path.
6. Propose the commit and wait.

After each step report what you did, what you verified and what you could not verify. If something is ambiguous, ask before assuming. Explain each design decision in a few lines. Franklin does the final review: well-written code can still be wrong.

## README

Any change that affects how the project runs must update the README with exact run steps, environment variable names and what each is for (never the values), and how to trigger each acceptance criterion. What the README does not say will not be looked for when grading, so verify each instruction by running it.

## Prohibitions

- Do not commit credentials, passwords, tokens or real connection strings, and do not use them as examples.
- Do not touch code outside the current scope. No opportunistic refactors.
- Do not install packages or tools without asking.
- Do not delete files or modify `docs/`, `.github/` or `.gitignore` unless Franklin asks.
- Do not add infrastructure nobody asked for (Docker, CI, microservices).

## Open decisions (ask, do not assume)

Franklin will resolve these and update the list. Until then propose options with trade-offs and wait:

1. Frontend (undefined; do not assume Angular). Until decided, the app is consumed as an API.
2. How the first Administrator is created. Self-registration only produces an inactive Estándar (`Usuario.Crear` fixes `Rol.Estandar`), and RF-CA-08, 20 and 21 cannot be exercised until an Administrador exists. Options: seed one from environment variables, or promote the first activated user.
3. The business entity that carries the state machine (RF-NEG-03; the invoice is a candidate, Franklin's choice).
4. Whether a `Shared` project exists. Do not create one without approval.

Resolved and therefore no longer listed: the session credential mechanism (`docs/adr/001-credencial-de-sesion.md`, accepted 2026-09-30) and the shape of the mail sender (a CLI command, `--send-mail`, not a hosted service).

## Known issues (temporary, delete when fixed)

- The `.gitignore` header says ".NET (microservices) + Angular", which contradicts the modular monolith and the undefined frontend. Do not assume that stack. Franklin will fix it in its own PR.
- The `.gitignore` has unanchored patterns that match at any depth. Before adding a file, run `git check-ignore -v <path>`:
  - `server/` ignores any directory with that name.
  - `Debug/`, `Release/`, `logs/`, `dist/`, `artifacts/`, `publish/`, `output/` are ignored at any level.
  - `appsettings.Development.json`, `appsettings.Staging.json` and `appsettings.*.local.json` are ignored on purpose. Do not "fix" that.
  - `.vscode/*` uses an allow list; if an allowed file does not show in `git status`, force it with `git add -f`.