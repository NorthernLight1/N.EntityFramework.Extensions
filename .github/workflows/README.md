# Workflows

## `dotnet.yml`
Runs on push/PR to `master`: build and test.

## `release.yml` – Publish and Release (manual)

**Use this to publish a new version to NuGet and create a GitHub release with source zip.**

### How to run
1. Open the repo **Actions** tab.
2. Select **"Publish and Release"** in the left sidebar.
3. Click **"Run workflow"**.
4. Enter the **version** (e.g. `1.9.2`). The tag will be `v1.9.2`.
5. Optionally uncheck **"Publish package to NuGet.org"** to only create the GitHub release (and source zip).
6. Click **"Run workflow"** (green button).

### What it does
- Builds and packs the library in Release.
- **Publishes** the `.nupkg` to NuGet.org (if the option is enabled).
- Creates a **source zip** of the repo at the current commit (`git archive`).
- Creates a **GitHub release** for tag `v{version}` with:
  - Generated release notes
  - **Source code zip** attached
  - **.nupkg** attached

### Required setup
- **Repository secret:** `NUGET_API_KEY`  
  Create an API key at [NuGet.org → Account → API Keys](https://www.nuget.org/account/apikeys), then add it in **Settings → Secrets and variables → Actions**.
- `GITHUB_TOKEN` is provided automatically; no extra setup for creating the release/tag.

### Before running
- Bump **Version** in `N.EntityFramework.Extensions/N.EntityFramework.Extensions.csproj` to match the release version.
- Commit and push the version change (or run from the branch/commit you want to release).
