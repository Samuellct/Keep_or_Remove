# Contributing to Keep or Remove

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (x64)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Node.js LTS (for the front-end tests and semantic-release)

## Environment setup

1. Clone, then start the test server:
   ```bash
   cp docker/.env.example docker/.env   # adjust the media paths
   docker compose -f docker/docker-compose.yml up -d
   ```
   Install the File Transformation plugin in that container (it is required for the buttons).

2. Build and deploy the current source to the container (PowerShell):
   ```powershell
   .\docker\scripts\deploy-plugin.ps1
   ```

3. Open `http://localhost:8098`.

## Conventions

- **Language**: all code, identifiers, comments, commit messages, and docs in **English**.
- **Commits**: [Conventional Commits](https://www.conventionalcommits.org/) (`feat`, `fix`,
  `refactor`, `style`, `docs`, `chore`, `test`). One atomic commit per logical change. No
  `Co-Authored-By` trailers - Samuel is the sole author.
- **No commented-out / dead code** in commits.
- **CSS**: every class prefixed `kor-`; reuse native Jellyfin CSS variables instead of hardcoding colours.
- **No AI slop**: banned fonts (Inter, Poppins, Manrope, Outfit, Plus Jakarta Sans, Space Grotesk);
  banned colours/patterns (blue-violet or violet-cyan gradients, Tailwind Blue `#3B82F6`, Gray-50
  `#F9FAFB` backgrounds, black + neon-violet, turquoise on dark); no em dashes in user-facing text.
- **Temporary by design**: no hard-coded paths/GUIDs/ports; all persistent state stays in the single
  `Jellyfin.Plugin.KeepOrRemove/` data directory. See `CLAUDE.md`.

## Testing

```bash
dotnet test          # backend (xUnit)
npm test             # front-end pure helpers (vitest + jsdom)
```

Single backend test: `dotnet test --filter "FullyQualifiedName~VoteServiceTests.Upsert_ReplacesExistingVote"`.

## Branching & releases

`main` is the only permanent branch (feature branches live in forks only). Pushing to `main` runs
CI; on success `semantic-release` cuts the version, builds the plugin zip with `jprm`, updates
`manifest.json`, and publishes the GitHub release. Open PRs against `main` and make sure CI is green.
