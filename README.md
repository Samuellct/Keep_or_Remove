# Keep or Remove

<p align="center">
  <img src="https://github.com/Samuellct/Keep_or_Remove/actions/workflows/ci.yml/badge.svg" alt="Build">
  <img src="https://img.shields.io/github/v/release/Samuellct/Keep_or_Remove" alt="Version">
  <img src="https://img.shields.io/badge/Jellyfin-10.11.11-orange" alt="Jellyfin">
  <img src="https://img.shields.io/badge/license-GPL--3.0-green" alt="License">
</p>

A small, **temporary** Jellyfin plugin. While server storage is limited, it lets a handful of users
say which movies and series they still want kept and which can be removed, and gives the admin a
plain aggregated table to decide the rotation manually.

The plugin is **decision-support only**. It never deletes, adds, moves, or modifies any media,
metadata, or file. It never auto-rotates and never enforces a majority. The admin keeps full control.

Part of the [JellyUX](https://github.com/Samuellct/JellyUX-Homepage) plugin family.

---

## Prerequisites

- **Jellyfin 10.11.11** (not tested against other versions - this plugin targets that release only).
- **[File Transformation plugin](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation)**
  - required, used to inject the vote buttons into the web client.

## Installation

1. Jellyfin dashboard: **Administration > Plugins > Repositories > Add**, paste:
   ```
   https://raw.githubusercontent.com/Samuellct/Keep_or_Remove/main/manifest.json
   ```
2. **Administration > Plugins > Catalog**, install **Keep or Remove**.
3. Restart Jellyfin.

## Usage

- On any movie or series page, users see two buttons: **Keep** and **Remove**. One vote per user per
  title; clicking the other choice replaces it. Season and episode pages vote for the parent series.
- The admin sees the aggregated results (Media / Keep / Remove / Total, sortable and filterable) on
  **Administration > Plugins > Keep or Remove**.

## Clean removal

All data lives in one directory: `<jellyfin-data>/Jellyfin.Plugin.KeepOrRemove/`. Uninstall the
plugin and delete that directory - the server is left exactly as it was.

## Development

See [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`PLAN.md`](PLAN.md).

## License

GPL-3.0. See [`LICENSE.md`](LICENSE.md).
