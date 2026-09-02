## [1.0.1](https://github.com/JellyUX/Keep_or_Remove/compare/v1.0.0...v1.0.1) (2026-09-02)

### Bug Fixes

* lower the minimum Jellyfin version to 10.11.10 ([98c21d0](https://github.com/JellyUX/Keep_or_Remove/commit/98c21d008e9bab184aca8b2b2a84dff48b6e1d6d))

## [1.0.0](https://github.com/JellyUX/Keep_or_Remove/compare/v0.3.0...v1.0.0) (2026-09-02)

### ⚠ BREAKING CHANGES

* First stable release. The HTTP API under /KeepOrRemove
and the votes.json schema (schema: 1) are now covered by SemVer; any
incompatible change bumps the major version.

### Features

* stabilise the public HTTP API and votes.json schema for 1.0.0 ([cbfd6c4](https://github.com/JellyUX/Keep_or_Remove/commit/cbfd6c4cf19bfac3fbc93c7a651a685552c177bf))

## [0.3.0](https://github.com/JellyUX/Keep_or_Remove/compare/v0.2.0...v0.3.0) (2026-09-01)

### Features

* add sort and type filter controls to the admin table ([287126f](https://github.com/JellyUX/Keep_or_Remove/commit/287126f5a13094d73a5a0a27ed06f8182c0517b4))
* render the aggregated results table on the config page ([fc94885](https://github.com/JellyUX/Keep_or_Remove/commit/fc94885e460e40bab50907860ddcc56bedde14f9))
* surface the startup warning on the config page ([c6a4ec0](https://github.com/JellyUX/Keep_or_Remove/commit/c6a4ec04729e13dad0a03d66643bad7a7ed6a491))
* wire the orphan vote purge button ([f9c3dc9](https://github.com/JellyUX/Keep_or_Remove/commit/f9c3dc97cff03dbf4f18de5c833f7b9740d68bb5))

## [0.2.0](https://github.com/JellyUX/Keep_or_Remove/compare/v0.1.0...v0.2.0) (2026-08-29)

### Features

* add the vote state transition helper ([729fa29](https://github.com/JellyUX/Keep_or_Remove/commit/729fa29460ced744d245cc2a7eb91498d32237d3))
* cast, change and clear votes from the detail page ([07f77dd](https://github.com/JellyUX/Keep_or_Remove/commit/07f77dde7f5f5638015657caab63d1de12b6133e))

## [0.1.0](https://github.com/JellyUX/Keep_or_Remove/compare/v0.0.2...v0.1.0) (2026-08-29)

### Features

* honour the plugin enabled toggle in the web client ([d909ef4](https://github.com/JellyUX/Keep_or_Remove/commit/d909ef44205dea9f944ad59019cf4fe8213f758c))
* render the keep and remove buttons on the detail page ([2ecb8bd](https://github.com/JellyUX/Keep_or_Remove/commit/2ecb8bda6ffcc42700c54f4acd769e4319fa6827))

## [0.0.2](https://github.com/JellyUX/Keep_or_Remove/compare/v0.0.1...v0.0.2) (2026-08-29)

### Bug Fixes

* reject vote payloads outside the two allowed values ([a8435f6](https://github.com/JellyUX/Keep_or_Remove/commit/a8435f6ce607c0c51e4e9baa8b90c7b4fe32e784))
* return 503 when vote storage cannot be read ([11caec5](https://github.com/JellyUX/Keep_or_Remove/commit/11caec59761c5588c181b2b8c951393574505c48))
