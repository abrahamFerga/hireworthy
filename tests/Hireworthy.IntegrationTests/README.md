# Hireworthy.IntegrationTests

**This project is a deliberate shell.** The scaffold creates it; `/deliver:install-runbook` owns
its contents — the Testcontainers fixture, the AG-UI stream helper, the approval-gate tests and the
golden evals. Keeping one source for that contract is why the scaffold does not hand-roll them.

Until the runbook is installed, this project builds and runs **zero tests**. A green
`dotnet test` here therefore proves nothing about runtime behaviour, and must not be reported as if
it did.

<!-- merge-gate proof branch; deleted after the check -->
