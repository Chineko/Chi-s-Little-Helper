# Chi's Little Helper

A modular Unity Editor toolkit for VRChat avatar creation, diagnostics, optimization, VRCFury workflows, and animation management.

## Status

Chi's Little Helper is currently in the 1.0 release-candidate phase and is available as a VPM/VCC package.

Current package release: **1.0.0-rc.13**

## Package ID

`com.chineko.chis-little-helper`

## Features

- Avatar Doctor 2.0 and Pre-Upload checks
- Animator & Parameters diagnostics
- Animation Swapper & Usage Map
- Texture / VRAM analysis
- PC / Quest comparison
- Material Inspector++ and Material Assembler
- PhysBone, Contact, and Constraint tooling
- VRCFury workflow helpers and Overview++
- Presets, snapshots, reports, support bundles, and installation-health checks

## Install with VCC

Before switching to the VCC-managed package, remove any older manually copied Chi's Little Helper folder from your Unity project. Do not keep manual and VPM installations side-by-side.

### Add the repository

GitHub does not make `vcc://` protocol links clickable in README files, so use the repository URL below with VCC.

**[Open Chi's Little Helper VPM repository listing](https://raw.githubusercontent.com/Chineko/Chi-s-Little-Helper/main/Website/index.json)**

Repository URL:

`https://raw.githubusercontent.com/Chineko/Chi-s-Little-Helper/main/Website/index.json`

In the VRChat Creator Companion:

1. Open **Settings → Packages**.
2. Click **Add Repository**.
3. Paste the repository URL above and confirm.
4. Open your avatar project.
5. Go to **Manage Project → Packages** and install **Chi's Little Helper**.

> A true one-click **Add to VCC** button can be added once a small HTTPS landing page is hosted for the repository; browsers can then hand off from that page to the `vcc://` protocol.

## Releases and updates

The distributable package lives under:

`Packages/com.chineko.chis-little-helper`

The **Build Release** GitHub Actions workflow packages that directory as a VPM-compatible ZIP and publishes the version from `package.json`.

The **Build Repo Listing** workflow rebuilds the VPM repository listing after releases, so newer package versions can appear as updates in VCC.

## License

MIT
