# Changelog

## 1.0.0-rc.14
- Added an **All Tools** directory to Home so every module is visible from the main page, not only the curated Quick Actions.
- Tools are grouped under Build, Dynamics, Materials, Optimize and Doctor and route to the same module pages as the category navigation.
- VRCFury-only tools respect the existing **Show VRCFury Features** preference.
- Avatar-dependent tools are disabled when no avatar is selected; Material Assembler and Preset Library remain available without an avatar.
- The tool directory uses the responsive button layout so compact windows stack safely without horizontal overflow.

## 1.0.0-rc.13
- Combined the planned **Animation Swapper** and **Animator Clip Usage Map** into one implemented Build tool: **Animation Swapper & Usage Map**.
- The tool scans the selected avatar's Animator Controllers and reports AnimationClip references by controller, layer, state/state-machine path, and Blend Tree location.
- Source Clip is optional while scanning: leave it empty to map all clip usage, or choose a clip to find only its references.
- Usage results support filtering, per-reference selection, Select All / None, Select Clip, and Use as Source.
- Added **Replace Selected** and **Replace Everywhere** with a confirmation preview, Undo recording, and no deletion/modification of the source AnimationClip asset.
- Replacement supports direct Animator State motions and clips nested inside Blend Trees.
- Animator Doctor and Pre-Upload data are marked stale after replacements.
- Removed the old Animation Swapper and Animator Clip Usage Map entries from Potential Future Features because they are now one implemented tool.

## 1.0.0-rc.12
- Refreshed the Home **Potential Future Features** roadmap.
- Removed **Avatar Snapshot & Compare** from future ideas because it is already implemented.
- Removed the old **Doctor Report Export** roadmap item because reporting/export support is now covered by Pre-Upload reporting and the Support Bundle.
- Kept **Animation Swapper**, **VCC Updater**, **Blendshape Manager**, **Hierarchy Cleaner**, **Setup Wizard**, and **Quick Actions Palette** as future candidates.
- Added **Animator Clip Usage Map** and **Avatar Variant Manager** as new future ideas.

## 1.0.0-rc.11
- Refreshed the **Current Changelog** shown on Home so it reflects the actual Release Candidate work instead of older beta-era highlights.
- Added visible entries for VRCFury Overview++ menu grouping/editing, responsive-layout fixes, status-indicator scaling, readable diagnostic cards, Support Bundle, Installation Health, and RC hardening.
- Updated the first-run onboarding wording from **beta** to **release candidate**.

## 1.0.0-rc.10
- Reworked **VRCFury Overview++** so VRCFury Toggle features are grouped by their actual menu path.
- Toggle paths such as `Outfits/Jacket` are displayed under the `Outfits` menu with a separate editable **Toggle Name** and **Menu Path**.
- Each menu group now exposes editable **Menu Name** and **Parent Path** fields.
- Applying a menu rename/path change updates all toggles in that menu and cascades the changed prefix into nested child menus.
- Root-level toggles can be moved into a menu from the Root Menu group.
- Toggle edits use explicit Apply buttons, Undo, prefab-modification recording, and invalidate parameter/pre-upload caches when menu paths may affect generated parameters.
- Non-Toggle VRCFury features remain available under **Other VRCFury Features** with select/duplicate/delete and host-object rename controls.

## 1.0.0-rc.9
- Fixed compact-width overflow in Animator & Parameters Doctor, Texture & VRAM, Avatar Doctor 2.0, Pre-Upload Check, Material Inspector++, and the analyzed Home dashboard.
- Four-card diagnostic summaries now switch to 2×2 layouts in compact mode.
- Avatar Doctor and Home six-card summaries now switch to three rows of two cards.
- Texture & VRAM stacks Analyze/Search in compact mode and uses a 2×2 metric layout.
- Shared diagnostic/home stat cards can now shrink to the available viewport instead of forcing horizontal overflow.
- The long Home Material Assembler quick-action label is shortened in compact mode.

## 1.0.0-rc.8
- Applied the compact button-scaling fix across the rest of the toolkit modules.
- Added shared responsive action-row helpers: multi-button rows stack vertically in compact layouts and remain horizontal on wider layouts.
- Updated Home quick actions and utility header, onboarding, Preferences actions, Animator Doctor, Avatar Doctor, Multi Toggles, Outfit Builder, Constraint Toolkit, PhysBone Manager, Material Assembler, Material Inspector, Optimize/VRCFury Overview, texture actions, Pre-Upload report actions, Preset Library, Snapshot & Compare, no-avatar actions, and error/recovery controls.
- Fixed-width action buttons now switch to full-width expansion in compact layouts where appropriate.
- Wider layouts retain the original balanced horizontal button groups.

## 1.0.0-rc.7
- Fixed Home **Beta Feedback** buttons clipping/overflowing at compact window widths.
- `Copy Beta Report` and `Copy Environment Only` now stack vertically in compact layouts and remain full-width.
- Wider layouts keep the two-button horizontal row with even expansion.

## 1.0.0-rc.6
- Fixed deformation of the top session/status indicators at narrow window widths.
- Session pills now use the correct 9-slice border instead of stretching the entire rounded background texture.
- Added a stable 24px pill height, 10pt bold text, zero outer margin, and a minimum width to keep SDK/VRCFury/Avatar/Analysis indicators readable at compact scales.
- Compact two-column indicators continue to expand evenly without warping their rounded ends.

## 1.0.0-rc.5
- Added **VCC Updater** to the Home **Potential Future Features** roadmap.
- The roadmap item describes a VCC-compatible update flow for checking and applying future Chi's Little Helper releases more safely.
- This remains roadmap-only; no updater or package-distribution behavior is included in the RC.

## 1.0.0-rc.4
- Added **Animation Swapper** to the Home **Potential Future Features** roadmap.
- Roadmap description calls out quick animation-clip replacement across avatar Animator Controllers with preview, filtering, and safe rollback.
- This is a roadmap-only addition; no new runtime/editor feature behavior was added to the RC.

## 1.0.0-rc.3
- Fixed the same unreadable black-label regression in **Material Audit** summary cards.
- `Unique Materials`, `Duplicate Assets`, `Texture-set Groups`, and `Null Slots` now use the proper subtitle text style instead of the row-background style.
- Material Audit summary labels now match the RC2 diagnostic stat-card fix: readable light/lavender labels with the shared stat-card sizing.

## 1.0.0-rc.2
- Fixed unreadable black labels in the Animator & Parameters Doctor summary cards.
- Animator summary cards now pass the correct subtitle text style instead of the row-background style.
- Shared diagnostic stat labels now explicitly use lavender text at 11pt bold, preventing inherited black text from Unity GUI styles.
- Shared diagnostic stat values increased to 14pt for better readability.

## 1.0.0-rc.1
- First **Release Candidate** for Chi's Little Helper 1.0.
- Feature set is now frozen for release validation; further RC builds should focus on bug fixes, compatibility, regressions, and release blockers.
- Carries forward beta.29 installation-health checks, known-limitations panel, support bundle export, UI consistency work, error-message polish, Pre-Upload reporting, snapshots, presets, diagnostics, optimization tools, and VRCFury integration.
- Release channel is now reported as **Release Candidate** in diagnostics and support reports.
- RC validation should cover every tool with no-avatar, simple-avatar, heavy-avatar, VRCFury-enabled/disabled, PC/Quest, narrow/wide window, destructive-action Undo, and support-bundle scenarios.

## 1.0.0-beta.29
- Added **Installation Health** scanning for RC upgrade safety.
- Preferences now warns when multiple Chi's Little Helper-style Editor script roots or multiple Tools menu declarations are detected.
- Added clear upgrade guidance: replace the previous package folder instead of installing versions side-by-side.
- Added **About & Known Limitations** in Preferences covering VRCFury parameter estimates, Quest advisory checks, shader-version variance, SDK constraint conversion, snapshots and PhysBone preset references.
- Added **Export Support Bundle**.
- Support bundles contain environment info, installation-health report, known limitations, fresh Pre-Upload TXT/JSON when available, current preferences summary, and the latest action/render errors when present.
- ZIP creation is runtime-detected via reflection; if unavailable, the same support bundle is exported as a folder instead of failing.

## 1.0.0-beta.28
- Fixed CS0120 in Outfit Builder introduced by the responsive field-label consistency pass.
- `DrawOutfitBlendshapePicker` is now an instance method so it can safely use the window's responsive `FieldLabelWidth`.
- Audited all `FieldLabelWidth` usages for static-context conflicts; no other static call sites remain.

## 1.0.0-beta.27
- Completed an **error-message polish** pass for RC preparation.
- Action failures now show a friendly summary, suggested next step, capture timestamp, and **Copy Support Details**.
- Common SDK/reflection, missing-file, permission, null-reference, invalid-argument, and invalid-state failures now receive clearer guidance.
- Full exception/environment details are preserved for debugging.
- Render-boundary recovery cards now use the toolkit theme and consistent Retry/recovery buttons.
- Completed a **UI consistency sweep**.
- Standard form labels now use one responsive field-label width.
- Doctor/Preflight/shared stat cards and PC/Quest summary cards now follow the same **label-above-value** hierarchy as Home.
- Freshness banners use consistent heading typography and refresh-button behavior.
- Added `UI_CONSISTENCY_AUDIT.md` to document the RC visual conventions and remaining native Unity controls.

## 1.0.0-beta.26
- Removed the duplicate **Run Pre-Upload Check** button from Home Quick Actions.
- The primary Pre-Upload Check button at the top of Home remains the single Home entry point.
- Snapshot & Compare remains available in Quick Actions.

## 1.0.0-beta.25
- Applied the same label-first layout to the Home avatar stat cards.
- **Health, Triangles, Texture VRAM, PhysBones, Contacts, and Params Left** now appear at the top of each card.
- Their current values now appear underneath for consistency with the Readiness cards.
- Increased the stat-label size/weight slightly to keep the dashboard readable and visually coherent.

## 1.0.0-beta.24
- Corrected the internal ordering of Home Readiness chips.
- Tool labels such as **Preflight**, **Quest**, **Animator**, **Materials**, and **Params** now appear above their status.
- Status values such as **STALE**, **READY**, **WARN**, or **BLOCKED** now sit at the bottom inside the clickable status area.
- Adjusted chip spacing to keep the two lines visually balanced.

## 1.0.0-beta.23
- Corrected the Home readiness layout requested in beta.22.
- The actual parameter-memory helper summary now appears above the Readiness status cards.
- Removed the duplicate parameter-memory summary from underneath the avatar stat cards.
- Readiness status chips now follow the helper information instead of preceding it.

## 1.0.0-beta.22
- Reordered the Home **Readiness** section so the explanatory helper text appears above the status chips.
- The readiness status cards now sit at the bottom of the section, making the explanation read first and the current state follow naturally.

## 1.0.0-beta.21
- Refined the Home **Readiness** status chips for better visual balance.
- Centered the top status value more cleanly inside each chip so it no longer sits too close to the top edge.
- Increased the bottom chip label text size and weight to improve readability.
- Added a bit more spacing below the readiness chips before the explanatory help hint.

## 1.0.0-beta.20
- Added **Fix All Safe Issues** to Avatar Doctor with a preview-before-apply flow.
- Bulk fixes only include actions explicitly classified as deterministic, Undo-supported and non-ambiguous.
- Missing-script cleanup is the first safe bulk fix; subjective optimization/setup changes remain manual.
- Added **Pre-Upload Check report export**: Copy Report, Export TXT and Export JSON.
- Reports include upload target, readiness result, counts, parameter-memory breakdown, avatar stats and grouped findings.
- Added a **tooltips/help pass** across Home, Preferences, Doctor, Pre-Upload, PC/Quest, Animator Doctor, Material Inspector++, Material Assembler, Snapshot & Compare and Preset Library v2.
- Added shared tooltip/help helpers so future tools can maintain the same UX.

## 1.0.0-beta.19
- Fixed CS1501 in the shared themed ObjectField helper.
- Replaced the unsupported six-argument EditorGUI.ObjectField call with Unity's stable five-argument overload.
- The helper now pre-draws the custom navy field background, then lets Unity render the object picker/content on top.
- All tools using themed object selectors inherit the fix automatically.

## 1.0.0-beta.18
- Fixed Material Assembler Custom Shader field compile error on Unity EditorGUILayout.ObjectField overloads.
- Replaced all themed EditorGUILayout.ObjectField calls with a shared Rect-based EditorGUI helper so the same overload issue cannot recur in other tools.
- The themed Custom Shader selector now uses a manually allocated Rect with EditorGUI.ObjectField, preserving the custom style without passing GUIStyle into an unsupported EditorGUILayout overload.

## 1.0.0-beta.17
- Added **Pre-Upload Check** under Doctor.
- Pre-Upload Check refreshes Doctor, Animator/Parameters, Material Inspector, PC/Quest and parameter-memory diagnostics in one pass.
- Added READY / WARNINGS / BLOCKED result with routed **Fix in…** actions.
- Added PC vs Quest upload target selection.
- Expanded Doctor routing so material, renderer and triangle findings jump directly to the most relevant tool.
- Added **Avatar Snapshot & Compare** under Build.
- Snapshots track hierarchy paths, material assets, Expression Parameters, dynamics/constraint components and headline counts.
- Snapshot comparison shows added/removed items and count deltas.
- Added **Preset Library v2** with categories, descriptions, search, favorites, preview-before-apply, rename, duplicate and single-preset export.
- Legacy beta.11 preset JSON remains loadable and is normalized to the new schema.
- Added a **Better Home Dashboard** with clickable readiness chips for Preflight, Quest, Animator, Materials and Params.
- Home primary action is now Pre-Upload Check, with Snapshot & Compare exposed in Quick Actions.

## 1.0.0-beta.16
- Added **Parameter Memory** to the Home dashboard.
- Home now shows remaining synced parameter memory out of VRChat's 256-bit budget.
- The estimate includes the avatar's current synced Expression Parameters plus VRCFury Toggle parameters that VRCFury generates during build/upload.
- Mirrors VRCFury Toggle behavior for Bool (1 bit), Int (8 bits), Slider (8 bits), shared/global parameters, and VRChat built-in global parameters.
- Shared VRCFury global/override parameters already present in the synced Expression Parameters asset are not double-counted.
- Detects VRCFury Parameter Compressor and labels the result as a conservative pre-compression estimate.
- Parameter memory remains visible even while the heavier Avatar Doctor scan is stale.
- Added hover details showing Expression bits, VRCFury Toggle bits, toggle type counts, estimated total, and remaining memory.

## 1.0.0-beta.15
- Completed a coherent input-control theming pass across the toolkit.
- Text fields now consistently use the dark navy custom input style.
- Numeric Int/Float fields use a matching themed number-field style.
- Dropdowns/popups use the lavender-on-navy popup style introduced in beta.14.
- Object selectors use a matching themed ObjectField style.
- Search fields use a matching dark themed search style.
- Updated Material Assembler naming/shader controls, Contact/Constraint dropdowns, Preferences thresholds, Material Inspector shader selectors, and Avatar Descriptor selector.
- Added FIELD_THEME_AUDIT.md for remaining native controls that intentionally keep Unity rendering.
- Follow-up audit themed all remaining standard ObjectField/TextField/IntField/FloatField usages found across Build, Dynamics, Preferences, and Material Assembler.

## 1.0.0-beta.14
- Restyled Preset Library selection controls to match the plugin theme.
- Preset Type now uses a dark navy field with lavender text and themed hover/active states instead of Unity's default gray popup.
- Preset Name now uses the same custom input style and aligned label spacing as the rest of the toolkit.
- Added a reusable themed popup style for future dropdown controls.

## 1.0.0-beta.13
- Increased main category and secondary tool navigation text from 11pt to 12pt.
- Navigation labels now match the Home-page action-button typography.
- Active and inactive navigation tabs retain their existing responsive sizing and centered alignment.

## 1.0.0-beta.12
- Fixed PC / Quest Comparator summary cards using a dedicated themed stat layout.
- Summary labels no longer inherit a dark row background or black editor text.
- Metric values and labels are vertically centered inside each card.
- Comparator summary cards wrap into 2×2 rows in compact layouts.
- Table headers and metric rows now use explicit lavender/white/muted theme colors for readability.

## 1.0.0-beta.11
- Added **Animator & Parameters Doctor** under Doctor.
- Cross-checks Expression Parameters against Animator Controllers, detects duplicates/type mismatches, Trigger parameters and zero-weight layers.
- Reports synced Expression Parameter memory against VRChat's 256-bit budget and total custom-parameter count.
- Added **PC / Quest Comparator** under Optimize with current mobile performance thresholds, mobile shader checks and unsupported-component warnings.
- Added **Material Inspector++** under Materials with exact duplicate detection, shared texture-set grouping, unused-folder material scanning, duplicate-reference merging and batch shader replacement.
- Added **Preset Library** under Build with project-local save/apply plus JSON import/export.
- Presets currently support Doctor thresholds, Contact Setup, Constraint Setup, Material Assembler naming/shader mappings, and selected PhysBone serialized settings.
- New avatar-dependent tools invalidate their caches when the selected avatar changes.

## 1.0.0-beta.10
- Added Unity `ParentConstraint` → VRChat Constraint conversion in Dynamics > Constraint Toolkit.
- Uses VRChat SDK's documented `AvatarDynamicsSetup.DoConvertUnityConstraints(...)` API.
- Supports whole-avatar or selected-GameObject scope.
- Optional referenced animation-clip rebinding.
- Includes SDK API detection plus existing confirmation/Undo/snapshot safety.

## 1.0.0-beta.9
- Added **Materials > Material Assembler**.
- Scans a texture folder and groups PBR maps by shared filename base.
- Supports Base Color/Albedo/Diffuse, Normal, Metallic, Roughness, Smoothness, AO, Emission and Mask/Packed naming conventions.
- Added editable naming suffix rules for custom export conventions.
- Added Standard, Poiyomi, lilToon and Custom shader modes.
- Shader mapping first checks known texture properties, then safely inspects texture-property names/descriptions for compatible slots.
- Added a horizontal preview table before material creation.
- Preview reports missing Base Color maps, duplicate/ambiguous maps, unsupported shader slots, unmatched textures and existing material collisions.
- Existing `.mat` assets are never overwritten.
- Optional Normal Map importer correction is applied during creation.
- Material creation has confirmation plus cleanup/rollback for newly-created assets if an error occurs.
- Material Assembler can be used without selecting an avatar.

## 1.0.0-beta.8
- Added a dedicated vertical scrollbar to Settings & Preferences.
- Preferences now remain fully usable at minimum window size and smaller/high-DPI editor layouts.
- Preferences keeps its own scroll position instead of sharing Home/tool scrolling state.
- Opening Preferences resets that page to the top for predictable navigation.

## 1.0.0-beta.7
- Completed a responsive UI pass for minimum-width and wide EditorWindow layouts.
- Session status wraps into a balanced 2×2 layout in compact mode.
- Long avatar names and the Home Doctor action stack cleanly at narrow widths.
- Home and Doctor metric cards use responsive multi-row layouts.
- Secondary tool navigation wraps instead of compressing long labels.
- PhysBone actions and Outfit Builder action controls wrap in compact mode.
- Freshness banners place their refresh action below text at narrow widths.
- Constraint search stacks vertically at narrow widths.
- Standardized button padding and centered text across shared button styles.

## 1.0.0-beta.6
- Fixed the Avatar Doctor health score alignment.
- Health value and "HEALTH / 100" now render inside a dedicated fixed-width rect for true horizontal centering.
- Avoids GUILayout label padding differences that could visually shift the score.

## 1.0.0-beta.5
- Added first-run onboarding with a persistent dismiss/reopen preference.
- Added a global Session Status indicator showing SDK, VRCFury, Avatar, Analysis, and overall requirement readiness.
- Added explicit READY/STALE states for Doctor, Texture & VRAM, and VRCFury Overview++ instead of silently refreshing expensive caches.
- Added one-click **Copy Beta Report** with environment, current tool, cache freshness, avatar stats, and available error details.
- Added a tester-notes template directly to the copied beta report.
- Resetting Preferences now also resets the first-run onboarding state.

## 1.0.0-beta.4
- Removed automatic full Avatar Doctor scans when selecting/changing the Avatar Descriptor.
- Home now waits for an explicit **Analyze Avatar** action before running expensive avatar-wide analysis.
- Reworked Avatar Descriptor changes through one lightweight `SetActiveAvatar` path.
- Removed recursive renderer searches from `OnSelectionChange`; only the selected GameObject itself is inspected.
- Clears stale analysis results immediately when switching avatars.
- Keeps instant hover repainting without tying mouse movement to avatar analysis.

## 1.0.0-beta.3
- Restored immediate mouse-move repainting for responsive hover states.
- Hover repainting is visual-only and does not invalidate Doctor, texture, reflection, or hierarchy caches.
- Keeps the performance/caching improvements from beta.2 while removing delayed button/tab hover feedback.

## 1.0.0-beta.2
- Added a true per-tool and per-view render error boundary.
- A rendering exception now aborts only the current IMGUI pass and is isolated on the next repaint.
- Broken tools show a recovery card with **Copy Error**, **Retry**, and **Go Home**.
- Home and Preferences are also isolated so a failure there does not permanently break the editor window.
- Copy Error includes environment/version information plus the complete exception.
- Switching tools/views clears the stale render boundary.
- Preserved action-level Undo/error rollback from beta.1.

## 1.0.0-beta.1
- Added centralized versioning.
- Added Version & Dependencies diagnostics and Copy Environment Info.
- Added configurable Avatar Doctor 2.0 thresholds.
- Added destructive-action confirmations.
- Added optional automatic avatar snapshots and manual snapshots.
- Added `ChiLittleHelper.Editor.asmdef`.
- Added a formal beta release/test checklist.
- Restored the modular build's `Tools > Chi's Little Helper` menu entry.
- Retained the performance caching, modular split, and shared Undo/error boundary from the hardening pass.
