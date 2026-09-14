# Validation record — 2026-09-14

## Completed locally

- .NET SDK 10.0.401: Release solution build succeeded with zero warnings/errors.
- Default executable regression/HTTP suite: all 61 checks passed.
- Optional SQL Server integration against a dedicated, initially empty local SQL Server database: all 77 checks passed, including accounting, rollback and concurrent version conflicts across JSON, SQL Server and SQLite stores.
- SQL Server and SQLite migration snapshots match their models; SQL Server DDL generation succeeded.
- A clean Git clone independently restored, built, passed all 61 default checks and published successfully. This verifies that required source and migrations are committed.
- Publishing succeeded to `artifacts/publish`.
- Headless Microsoft Edge: connect, create, edit, soft-delete and API explorer request passed without page errors. Desktop screenshot reviewed; a 390-pixel mobile viewport passed the horizontal-overflow check. Screenshots are in ignored `.local/`.
- Both browser JavaScript files passed `node --check`.
- `docker compose config --quiet` passed with a locally supplied secret.
- Linux Docker image built successfully. `scripts/container-smoke.ps1` passed startup/migrations, documentation downloads, authenticated portfolio creation and persistence across container restart. The script refreshes Docker's ephemeral host-port mapping after restart; the first attempt identified that test-harness requirement, which was corrected before the successful run.
- Local startup script generated an ignored development key/database; `http://localhost:5080/health` reports healthy SQLite storage.
- Git ignore checks confirm that development credentials, databases and published binaries are excluded. Committed whitespace checks passed.

## GitHub Actions blocker

The initial push successfully reached `origin/main`. The configured workflow could not start either verification job because GitHub reported: "The job was not started because your account is locked due to a billing issue."

[Affected workflow run](https://github.com/1rinda/FinancialPortfolioAPI/actions/runs/34878099482).

Resolve the GitHub account billing lock, then rerun the workflow. No hosted test result is claimed. Local verification above completed independently of GitHub Actions.

## Runtime scope

This verifies the documented demonstration/test-server application, not production trading, tenant isolation, live market pricing or high-volume performance. Browser fixtures are soft-deleted and retain audit/change records. Temporary database fixtures and the clean checkout remain available for inspection; see [operations](operations.md) for locations and repeatable commands.
