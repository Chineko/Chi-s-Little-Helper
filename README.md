# Chi's Little Helper

A modular Unity Editor toolkit for VRChat avatar creation, diagnostics, optimization, VRCFury workflows, and animation management.

## Status

Chi's Little Helper is currently in the 1.0 release-candidate phase. The repository is being prepared as a VPM/VCC package so future releases can be installed and updated through the VRChat Creator Companion.

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

## VCC / VPM

After the first package release is published, this repository will expose a VPM listing through GitHub Pages. Users will be able to add the listing to VCC and install/update Chi's Little Helper from **Manage Project → Packages**.

## Development

The distributable package lives under:

`Packages/com.chineko.chis-little-helper`

The release workflow packages that directory as a VPM-compatible ZIP and publishes the version from `package.json`.

## License

MIT
