# Contributing

Before contributing:

- read the architecture overview in [README.md](README.md) and the decisions in
  [docs/adr/](docs/adr/)
- keep module boundaries explicit and make pragmatic exceptions visible
- avoid generic repositories and generic service layers
- keep framework-specific concerns out of `SharedKernel`

Run the complete public verification baseline before opening a pull request:

```powershell
./scripts/verify.ps1 -StartDatabase
```

If PostgreSQL is already available, omit `-StartDatabase`. The script is the primary
verification contract; hosted CI is only optional automation of the same checks.
