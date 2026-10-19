# Code signing policy

> **Status: application pending.** G-Windows has applied to the [SignPath Foundation](https://signpath.org/) free code signing program for open-source projects. Until it is approved, releases are **not signed** and Windows SmartScreen may show a warning. This page describes the policy that will apply once signing is active.

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

## What is signed

Only binaries built from this repository's source by the public GitHub Actions workflow (`.github/workflows/release.yml`): `G-Windows.exe`, `G-Windows.dll` and `GWindows.Core.dll`. Third-party and Microsoft runtime files are shipped as they are and are not re-signed.

## Roles

| Role | Who |
|---|---|
| Authors / committers | The maintainer, [@GustavoKehr](https://github.com/GustavoKehr), and contributors whose pull requests are merged |
| Reviewers | The maintainer reviews every change before merging |
| Approvers (release signing) | The maintainer approves each signing request manually |

## Privacy

G-Windows runs entirely on your computer. It does not send any data over the network: no telemetry, no analytics, no automatic update check. Settings are stored in `%AppData%\G-Windows`.