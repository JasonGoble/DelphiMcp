# chore: add low-cost CI workflow, README badges, and release process docs
## Summary
Add a lightweight GitHub Actions workflow and README status/version indicators suitable for a public project while keeping Actions usage low-cost.
## Scope
1. CI workflow
- Add a single low-cost workflow for build/test on push and pull_request.\n- Keep scope limited to restore/build/test; do not run heavy indexing jobs in CI.\n- Add concurrency cancellation to avoid duplicate runs.\n
2. README status indicators
- Add CI status badge tied to the workflow.\n- Add latest release badge.\n- Add short note that test/build status is automated via Actions.\n
3. Release process docs
- Add a short release process section to README with tag/release steps and expectations.\n
## Acceptance Criteria
- Workflow runs successfully on PRs and pushes.\n- README displays working CI and release badges.\n- README includes concise release process instructions.\n- No heavyweight indexing commands are run in CI.\n
