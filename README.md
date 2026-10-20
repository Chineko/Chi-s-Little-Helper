# Chi's Little Helper

A modular Unity Editor toolkit for VRChat avatar creation, diagnostics, optimization, VRCFury workflows, and animation management.

## Status

Chi's Little Helper is currently in the 1.0 release-candidate phase and is available as a VPM/VCC package.

Current package release: **1.0.0-rc.14**

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

### One-click installer page

**[Open the Chi's Little Helper VCC install page](https://chineko.github.io/Chi-s-Little-Helper/)**

That page includes a real **Add to VCC** button which hands off to the `vcc://` protocol.

### Manual repository URL

If the button does not launch VCC, add this repository URL manually:

`https://raw.githubusercontent.com/Chineko/Chi-s-Little-Helper/main/Website/index.json`

In the VRChat Creator Companion:

1. Open **Settings → Packages**.
2. Click **Add Repository**.
3. Paste the repository URL above and confirm.
4. Open your avatar project.
5. Go to **Manage Project → Packages** and install **Chi's Little Helper**.

## Releases and updates

The distributable package lives under:

`Packages/com.chineko.chis-little-helper`

The **Build Release** GitHub Actions workflow packages that directory as a VPM-compatible ZIP and publishes the version from `package.json`.

The **Build Repo Listing** workflow rebuilds the VPM repository listing after releases, so newer package versions can appear as updates in VCC.

The **Deploy VCC Landing Page** workflow publishes the installer page from `docs/` through GitHub Pages.

## License

MIT
