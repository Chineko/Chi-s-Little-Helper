using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;

public partial class ChiLittleHelperWindow
{
    void DrawUtilityBar()
    {
        EnsureStyles();

        var barStyle = new GUIStyle
        {
            normal = { background = texCardBg },
            border = new RectOffset(8, 8, 8, 8),
            padding = new RectOffset(8, 8, 6, 6),
            margin = new RectOffset(0, 0, 0, 0)
        };

        bool requirementsReady = SessionRequirementsReady();

        EditorGUILayout.BeginVertical(barStyle);

        if (IsCompactLayout)
        {
            EditorGUILayout.LabelField(
                requirementsReady ? "Requirements: ALL READY" : "Requirements: ATTENTION",
                new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = requirementsReady ? ColLavenderText : ColGradPink },
                    fontSize = 11
                });

            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Home", showHome && !showPreferences ? styleBtnGrad : styleBtnDark,
                GUILayout.Height(28), GUILayout.ExpandWidth(true)))
            {
                ClearRenderError();
                showPreferences = false;
                showHome = true;
                GUI.FocusControl(null);
                scroll = Vector2.zero;
            }

            GUILayout.Space(5);

            string compactPrefLabel = showPreferences ? "← Back" : "Preferences";
            if (GUILayout.Button(compactPrefLabel, showPreferences ? styleBtnGrad : styleBtnDark,
                GUILayout.Height(28), GUILayout.ExpandWidth(true)))
            {
                ClearRenderError();
                bool openingPreferences = !showPreferences;
                showPreferences = !showPreferences;
                if (showPreferences)
                {
                    showHome = false;
                    if (openingPreferences)
                        preferencesScroll = Vector2.zero;
                }
                else
                    showHome = true;
                GUI.FocusControl(null);
                scroll = Vector2.zero;
            }

            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(
                requirementsReady ? "Requirements: ALL READY" : "Requirements: ATTENTION",
                new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = requirementsReady ? ColLavenderText : ColGradPink },
                    fontSize = 11
                });

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Home", showHome && !showPreferences ? styleBtnGrad : styleBtnDark,
                GUILayout.Width(72), GUILayout.Height(28)))
            {
                ClearRenderError();
                showPreferences = false;
                showHome = true;
                GUI.FocusControl(null);
                scroll = Vector2.zero;
            }

            GUILayout.Space(5);

            string prefLabel = showPreferences ? "← Back" : "Preferences";
            if (GUILayout.Button(prefLabel, showPreferences ? styleBtnGrad : styleBtnDark,
                GUILayout.Width(showPreferences ? 82 : 96), GUILayout.Height(28)))
            {
                ClearRenderError();
                bool openingPreferences = !showPreferences;
                showPreferences = !showPreferences;
                if (showPreferences)
                {
                    showHome = false;
                    if (openingPreferences)
                        preferencesScroll = Vector2.zero;
                }
                else
                    showHome = true;
                GUI.FocusControl(null);
                scroll = Vector2.zero;
            }

            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(4);

        bool sdkReady = deps != null && !string.IsNullOrEmpty(deps.sdkAssembly);
        bool vfReady = !enableVRCFuryFeatures ||
                       (deps != null && deps.vrcfuryFound &&
                        deps.missingTypes.Count == 0 && deps.error == null);

        string vfText = !enableVRCFuryFeatures
            ? "VRCFURY OFF"
            : (vfReady ? "VRCFURY READY" : "VRCFURY CHECK");

        string analysisText = avatar == null
            ? "ANALYSIS N/A"
            : (IsCoreAnalysisStale() ? "ANALYSIS STALE" : "ANALYSIS READY");

        if (IsCompactLayout)
        {
            EditorGUILayout.BeginHorizontal();
            DrawSessionPill(sdkReady ? "SDK READY" : "SDK CHECK", sdkReady, true);
            GUILayout.Space(ResponsiveGap);
            DrawSessionPill(vfText, vfReady, true);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            DrawSessionPill(avatar != null ? "AVATAR READY" : "NO AVATAR", avatar != null, true);
            GUILayout.Space(ResponsiveGap);
            DrawSessionPill(analysisText, avatar == null || !IsCoreAnalysisStale(), true);
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            DrawSessionPill(sdkReady ? "SDK READY" : "SDK CHECK", sdkReady);
            GUILayout.Space(4);
            DrawSessionPill(vfText, vfReady);
            GUILayout.Space(4);
            DrawSessionPill(avatar != null ? "AVATAR READY" : "NO AVATAR", avatar != null);
            GUILayout.Space(4);
            DrawSessionPill(analysisText, avatar == null || !IsCoreAnalysisStale());
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    bool SessionRequirementsReady()
    {
        bool sdkReady = deps != null &&
                        !string.IsNullOrEmpty(deps.sdkAssembly) &&
                        deps.error == null;

        bool vrcfuryReady =
            !enableVRCFuryFeatures ||
            (deps != null &&
             deps.vrcfuryFound &&
             deps.missingTypes.Count == 0 &&
             deps.error == null);

        return sdkReady && vrcfuryReady;
    }

    bool IsCoreAnalysisStale()
    {
        return avatar != null && (doctorDirty || textureVramDirty);
    }

    void DrawSessionPill(string text, bool ready, bool expand = false)
    {
        // texInfoBox / texBtnDark are 9-sliced rounded textures. Supplying a
        // border here is important: without it Unity stretches the whole
        // texture when the window becomes narrow, which distorts the pill ends.
        var style = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            border = new RectOffset(8, 8, 8, 8),
            padding = new RectOffset(8, 8, 3, 3),
            margin = new RectOffset(0, 0, 0, 0),
            clipping = TextClipping.Clip,
            stretchWidth = expand,
            fixedHeight = 24,
            normal =
            {
                background = ready ? texInfoBox : texBtnDark,
                textColor = ready ? ColLavenderText : ColGradPink
            }
        };

        if (expand)
            GUILayout.Label(
                text,
                style,
                GUILayout.MinWidth(96),
                GUILayout.Height(24),
                GUILayout.ExpandWidth(true));
        else
            GUILayout.Label(
                text,
                style,
                GUILayout.MinWidth(96),
                GUILayout.Height(24));
    }

    void DrawDataFreshnessBanner(string label, bool stale, string readyText, Action refresh, string refreshLabel)
    {
        if (IsNarrowLayout)
            EditorGUILayout.BeginVertical(styleRowBg);
        else
            EditorGUILayout.BeginHorizontal(styleRowBg);

        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(
            stale ? label + ": STALE" : label + ": READY",
            new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = stale ? ColGradPink : ColLavenderText }
            });
        EditorGUILayout.LabelField(
            stale
                ? "The avatar changed after this data was generated. Refresh when you are ready."
                : readyText,
            styleCardSubtitle);
        EditorGUILayout.EndVertical();

        if (stale && refresh != null)
        {
            GUILayout.Space(8);
            if (HelpButton(
                string.IsNullOrEmpty(refreshLabel) ? "Refresh" : refreshLabel,
                "Refresh this cached analysis now.",
                styleBtnDark,
                IsNarrowLayout ? GUILayout.ExpandWidth(true) : GUILayout.Width(118),
                GUILayout.Height(30)))
            {
                refresh();
            }
        }

        if (IsNarrowLayout)
            EditorGUILayout.EndVertical();
        else
            EditorGUILayout.EndHorizontal();
    }


    void DrawHomeScreen()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));

        if (!onboardingDismissed)
        {
            DrawFirstRunOnboarding();
            GUILayout.Space(10);
        }

        BeginCard(CardIcon.Sparkle, "Welcome to Chi's Little Helper", "Avatar building, dynamics, optimization and diagnostics in one editor toolkit.");

        if (avatar != null)
        {
            if (IsNarrowLayout)
            {
                EditorGUILayout.LabelField("Current Avatar", styleCardSubtitle);
                EditorGUILayout.LabelField(avatar.gameObject.name, new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = ColTextPrimary },
                    fontSize = 15,
                    wordWrap = true
                });

                GUILayout.Space(5);
                if (GUILayout.Button("Pre-Upload Check", styleBtnGrad, GUILayout.Height(34)))
                    RouteToTool(4, 17);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Current Avatar", styleCardSubtitle);
                EditorGUILayout.LabelField(avatar.gameObject.name, new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = ColTextPrimary },
                    fontSize = 15,
                    wordWrap = true
                });
                EditorGUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Pre-Upload Check", styleBtnGrad, GUILayout.Width(138), GUILayout.Height(34)))
                    RouteToTool(4, 17);

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(8);

            EnsureParameterMemoryCache();

            EditorGUILayout.LabelField(
                GetParameterMemorySummary(),
                new GUIStyle(styleCardSubtitle)
                {
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10
                });

            GUILayout.Space(5);

            DrawHelpHint("Click a readiness chip to open its related tool. STALE means that analysis has not been refreshed since the avatar/project changed.");
            GUILayout.Space(5);
            DrawHomeReadinessDashboard();

            GUILayout.Space(8);

            if (doctorDirty)
            {
                EditorGUILayout.HelpBox(
                    "Avatar analysis is waiting. This is intentional so selecting or dragging an Avatar Descriptor stays responsive.",
                    MessageType.Info);

                if (GUILayout.Button("Analyze Avatar", styleBtnGrad, GUILayout.Height(32)))
                {
                    Run("Analyze Avatar", () =>
                    {
                        RunDoctorScan();
                        doctorDirty = false;
                    });
                }

                GUILayout.Space(6);
                DrawHomeStat(
                    "Params Left",
                    GetParameterMemoryHomeValue(),
                    GetParameterMemoryTooltip());

            }
            else
            {
                if (IsCompactLayout)
                {
                    EditorGUILayout.BeginHorizontal();
                    DrawHomeStat("Health", doctorHealthScore + "/100");
                    GUILayout.Space(3);
                    DrawHomeStat("Triangles", doctorTriangles.ToString("N0"));
                    EditorGUILayout.EndHorizontal();

                    GUILayout.Space(3);

                    EditorGUILayout.BeginHorizontal();
                    DrawHomeStat("Texture VRAM", FormatBytes(doctorTextureBytes));
                    GUILayout.Space(3);
                    DrawHomeStat("PhysBones", doctorPhysBones.ToString());
                    EditorGUILayout.EndHorizontal();

                    GUILayout.Space(3);

                    EditorGUILayout.BeginHorizontal();
                    DrawHomeStat("Contacts", doctorContacts.ToString());
                    GUILayout.Space(3);
                    DrawHomeStat(
                        "Params Left",
                        GetParameterMemoryHomeValue(),
                        GetParameterMemoryTooltip());
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    DrawHomeStat("Health", doctorHealthScore + "/100");
                    GUILayout.Space(4);
                    DrawHomeStat("Triangles", doctorTriangles.ToString("N0"));
                    GUILayout.Space(4);
                    DrawHomeStat("Texture VRAM", FormatBytes(doctorTextureBytes));
                    GUILayout.Space(4);
                    DrawHomeStat("PhysBones", doctorPhysBones.ToString());
                    GUILayout.Space(4);
                    DrawHomeStat("Contacts", doctorContacts.ToString());
                    GUILayout.Space(4);
                    DrawHomeStat(
                        "Params Left",
                        GetParameterMemoryHomeValue(),
                        GetParameterMemoryTooltip());
                    EditorGUILayout.EndHorizontal();
                }


            }
        }
        else
        {
            DrawInfoBox("Select an avatar in the Hierarchy to unlock avatar-specific tools and health information.");
        }

        GUILayout.Space(10);

        EditorGUILayout.LabelField("Quick Actions", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText },
            fontSize = 13
        });
        GUILayout.Space(5);

        BeginResponsiveButtonRow();
        using (new EditorGUI.DisabledScope(avatar == null))
        {
            if (GUILayout.Button("Outfit Builder", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
                RouteToTool(0, 5);
            ResponsiveButtonGap();
            if (GUILayout.Button("PhysBone Manager", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
                RouteToTool(1, 9);
            ResponsiveButtonGap();
            if (GUILayout.Button("Texture & VRAM", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
                RouteToTool(3, 11);
        }
        EndResponsiveButtonRow();

        GUILayout.Space(5);

        BeginResponsiveButtonRow();
        using (new EditorGUI.DisabledScope(avatar == null))
        {
            if (GUILayout.Button("Contact Setup", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
                RouteToTool(1, 6);
            ResponsiveButtonGap();
            if (GUILayout.Button("Constraint Toolkit", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
                RouteToTool(1, 10);
            ResponsiveButtonGap();
            if (GUILayout.Button("Avatar Doctor 2.0", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
                RouteToTool(4, 8);
        }
        EndResponsiveButtonRow();

        GUILayout.Space(5);

        if (GUILayout.Button(IsCompactLayout ? "Material Assembler" : "Material Assembler — Build Materials from a Texture Folder", styleBtnDark, GUILayout.Height(32), GUILayout.MinWidth(0), GUILayout.ExpandWidth(true)))
            RouteToTool(2, 12);

        GUILayout.Space(5);

        BeginResponsiveButtonRow();
        using (new EditorGUI.DisabledScope(avatar == null))
        {
            if (GUILayout.Button("Animator & Params Doctor", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
                RouteToTool(4, 13);
            ResponsiveButtonGap();
            if (GUILayout.Button("PC / Quest Comparator", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
                RouteToTool(3, 14);
            ResponsiveButtonGap();
            if (GUILayout.Button("Material Inspector++", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
                RouteToTool(2, 15);
        }
        EndResponsiveButtonRow();

        GUILayout.Space(5);

        using (new EditorGUI.DisabledScope(avatar == null))
        {
            if (GUILayout.Button("Snapshot & Compare", styleBtnDark, GUILayout.Height(32)))
                RouteToTool(0, 18);
        }

        GUILayout.Space(5);

        using (new EditorGUI.DisabledScope(avatar == null))
        {
            if (GUILayout.Button("Animation Swapper & Usage Map", styleBtnDark, GUILayout.Height(32)))
                RouteToTool(0, 19);
        }

        GUILayout.Space(5);
        if (GUILayout.Button("Preset Library v2", styleBtnDark, GUILayout.Height(32)))
            RouteToTool(0, 16);

        GUILayout.Space(12);
        DrawHomeToolDirectory();

        EndCard();

        GUILayout.Space(10);

        BeginCard(CardIcon.Sparkle, "Beta Feedback", "Copy a ready-to-paste report when something feels wrong, slow, confusing, or inconsistent.");

        EditorGUILayout.LabelField(
            "The report includes toolkit/Unity/SDK/VRCFury status, current avatar, current view, analysis freshness, and available error details.",
            styleInfoText);

        GUILayout.Space(6);

        if (IsCompactLayout)
        {
            if (GUILayout.Button("Copy Beta Report", styleBtnGrad, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildBetaFeedbackReport();
                ShowNotification(new GUIContent("Beta report copied"));
            }

            GUILayout.Space(4);

            if (GUILayout.Button("Copy Environment Only", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildEnvironmentReport();
                ShowNotification(new GUIContent("Environment info copied"));
            }
        }
        else
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Copy Beta Report", styleBtnGrad, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildBetaFeedbackReport();
                ShowNotification(new GUIContent("Beta report copied"));
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Copy Environment Only", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildEnvironmentReport();
                ShowNotification(new GUIContent("Environment info copied"));
            }

            EditorGUILayout.EndHorizontal();
        }

        EndCard();

        GUILayout.Space(10);

        BeginCard(CardIcon.Overview, "Current Changelog", "Current changes included in the " + TOOL_VERSION + " release candidate build.");

        if (GUILayout.Button(homeShowChangelog ? "▼  Hide Changelog" : "▶  Show Changelog",
            styleBtnDark, GUILayout.Height(30)))
            homeShowChangelog = !homeShowChangelog;

        if (homeShowChangelog)
        {
            GUILayout.Space(8);

            DrawHomeChange("Animation Swapper & Usage Map",
                "Finds AnimationClip references across avatar Animator Controllers and Blend Trees, then safely replaces selected or all matching references with preview and Undo.");
            DrawHomeChange("VRCFury Overview++ Menu Groups",
                "Groups VRCFury toggles by their menu paths and lets you edit toggle names, toggle paths, menu names and parent menu paths with Undo support.");
            DrawHomeChange("Responsive Layout Fixes",
                "Compact layouts now stack action buttons and reorganize dashboard/stat cards to avoid horizontal overflow at narrow window widths.");
            DrawHomeChange("Status Indicator Scaling",
                "SDK, VRCFury, Avatar and Analysis indicators now keep their rounded shape and readable text at compact widths.");
            DrawHomeChange("Readable Diagnostic Cards",
                "Animator Doctor and Material Inspector summary cards now use explicit light labels and larger values instead of inherited dark text.");
            DrawHomeChange("Support Bundle",
                "Exports environment details, installation health, known limitations, fresh Pre-Upload reports and recent errors for easier support.");
            DrawHomeChange("Installation Health",
                "Detects likely duplicate Chi's Little Helper installs and warns against keeping older versions beside the current package.");
            DrawHomeChange("Pre-Upload Check",
                "Runs Doctor, Animator/Parameters, PC/Quest, Material Inspector and parameter-memory checks in one readiness pass with Fix in… routing.");
            DrawHomeChange("Snapshot & Compare",
                "Captures lightweight avatar baselines and compares hierarchy, materials, parameters and dynamics changes later.");
            DrawHomeChange("Preset Library v2",
                "Adds search, categories, favorites, descriptions, previews, rename, duplicate and single-preset export.");
            DrawHomeChange("Parameter Memory",
                "Shows remaining synced parameter memory and estimates VRCFury Toggle parameters generated during upload.");
            DrawHomeChange("Animator & Parameters Doctor",
                "Cross-checks expression parameters against Animator Controllers, finds duplicate/type issues and reports synced parameter memory.");
            DrawHomeChange("PC / Quest Comparator",
                "Provides side-by-side mobile compatibility and performance checks with Quest shader/component warnings.");
            DrawHomeChange("Material Inspector++",
                "Finds duplicate materials, shared texture sets, unused folder materials and supports batch shader replacement.");
            DrawHomeChange("Material Assembler",
                "Scans texture folders, groups PBR maps by filename, previews matches and creates shader-aware materials in bulk.");
            DrawHomeChange("Avatar Doctor 2.0",
                "Health score, categorized diagnostics, guided issue routing, texture checks, PhysBone warnings and safe fixes.");
            DrawHomeChange("Dynamics Tooling",
                "PhysBone Manager, Contact Setup and Constraint Toolkit provide focused avatar dynamics workflows.");
            DrawHomeChange("Release Candidate Polish",
                "Error messages, responsive buttons, compact layouts, upgrade safety and diagnostics have been hardened for the 1.0 release.");
        }

        EndCard();

        GUILayout.Space(10);

        BeginCard(CardIcon.Sparkle, "Potential Future Features", "Ideas for future versions — these are roadmap candidates, not promised features.");

        if (GUILayout.Button(homeShowRoadmap ? "▼  Hide Ideas" : "▶  Show Ideas",
            styleBtnDark, GUILayout.Height(30)))
            homeShowRoadmap = !homeShowRoadmap;

        if (homeShowRoadmap)
        {
            GUILayout.Space(8);

            DrawRoadmapItem("VCC Updater",
                "Provide a VCC-compatible package/update flow for checking and applying future Chi's Little Helper releases more safely.");
            DrawRoadmapItem("Blendshape Manager",
                "Search every mesh, preview shapes, save presets and create expression snapshots.");
            DrawRoadmapItem("Hierarchy Cleaner",
                "Find empty objects, broken references, duplicate names, unused leftovers and suspicious hierarchy structure.");
            DrawRoadmapItem("Setup Wizard",
                "Guided avatar setup covering descriptor, view position, lip sync, eye look, dynamics and performance.");
            DrawRoadmapItem("Quick Actions Palette",
                "Searchable command launcher for common Little Helper operations.");
            DrawRoadmapItem("Avatar Variant Manager",
                "Manage PC/Quest or alternate avatar variants with shared setup checks and side-by-side differences.");
        }

        EndCard();

        EditorGUILayout.EndScrollView();
    }

    void DrawFirstRunOnboarding()
    {
        BeginCard(CardIcon.Sparkle, "First Run — 3 Quick Steps", "A short guide to the release candidate. You can reopen it later from Preferences.");

        DrawOnboardingStep(
            "1",
            "Check requirements",
            SessionRequirementsReady()
                ? "The SDK and all enabled integrations are ready."
                : "The session bar shows ATTENTION. Open Preferences to see which enabled integration needs attention.");

        DrawOnboardingStep(
            "2",
            "Choose your avatar",
            avatar != null
                ? "An Avatar Descriptor is selected and ready."
                : "Select an Avatar Descriptor above, or select any object inside an avatar in the Hierarchy.");

        DrawOnboardingStep(
            "3",
            "Analyze only when needed",
            "Expensive Doctor/VRAM scans are explicit. A STALE label means cached data should be refreshed, not that the avatar is broken.");

        GUILayout.Space(6);
        BeginResponsiveButtonRow();

        if (GUILayout.Button("Open Preferences", styleBtnDark, GUILayout.Height(30), GUILayout.ExpandWidth(true)))
        {
            showPreferences = true;
            showHome = false;
            scroll = Vector2.zero;
        }

        ResponsiveButtonGap(5f);

        if (GUILayout.Button("Got it — Don't Show Again", styleBtnGrad, GUILayout.Height(30), GUILayout.ExpandWidth(true)))
        {
            onboardingDismissed = true;
            EditorPrefs.SetBool(PREF_ONBOARDING_DISMISSED, true);
        }

        EndResponsiveButtonRow();
        EndCard();
    }

    void DrawOnboardingStep(string number, string title, string detail)
    {
        EditorGUILayout.BeginVertical(styleRowBg);
        EditorGUILayout.BeginHorizontal();

        GUILayout.Label(number, new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = ColGradPink },
            fontSize = 14
        }, GUILayout.Width(24));

        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(title, new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColTextPrimary }
        });
        EditorGUILayout.LabelField(detail, styleInfoText);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    string GetCurrentSurfaceName()
    {
        if (showPreferences) return "Settings & Preferences";
        if (showHome) return "Home";

        switch (tab)
        {
            case 0: return "Armature Link";
            case 1: return "Dissolve Toggle";
            case 2: return "Multi Toggles";
            case 3: return "Blendshapes";
            case 4: return "Material Swap";
            case 5: return "Outfit Builder";
            case 6: return "Contact Setup";
            case 7: return "VRCFury Overview++";
            case 8: return "Avatar Doctor 2.0";
            case 9: return "PhysBone Manager";
            case 10: return "Constraint Toolkit";
            case 11: return "Texture & VRAM Analyzer";
            case 12: return "Material Assembler";
            case 13: return "Animator & Parameters Doctor";
            case 14: return "PC / Quest Comparator";
            case 15: return "Material Inspector++";
            case 16: return "Preset Library";
            default: return "Unknown";
        }
    }

    string BuildBetaFeedbackReport()
    {
        var lines = new List<string>
        {
            "=== Chi's Little Helper Beta Report ===",
            BuildEnvironmentReport(),
            "",
            "Current View: " + GetCurrentSurfaceName(),
            "Requirements: " + (SessionRequirementsReady() ? "ALL READY" : "ATTENTION"),
            "Core Analysis: " + (avatar == null ? "N/A" : (IsCoreAnalysisStale() ? "STALE" : "READY")),
            "Doctor Cache: " + (avatar == null ? "N/A" : (doctorDirty ? "STALE" : "READY")),
            "Texture/VRAM Cache: " + (avatar == null ? "N/A" : (textureVramDirty ? "STALE" : "READY")),
            "Overview++ Cache: " + (avatar == null ? "N/A" : (overviewDirty ? "STALE" : "READY"))
        };

        if (avatar != null && !doctorDirty)
        {
            lines.Add("");
            lines.Add("Doctor Health: " + doctorHealthScore + "/100");
            lines.Add("Triangles: " + doctorTriangles.ToString("N0"));
            lines.Add("Material Slots: " + doctorMaterialSlots);
            lines.Add("Texture VRAM Estimate: " + FormatBytes(doctorTextureBytes));
            lines.Add("PhysBones: " + doctorPhysBones);
            lines.Add("Contacts: " + doctorContacts);
            lines.Add("Constraints: " + doctorConstraints);
        }

        if (!string.IsNullOrEmpty(lastError))
        {
            lines.Add("");
            lines.Add("Last Action Error: " + lastError);
            if (!string.IsNullOrEmpty(lastErrorDetails))
                lines.Add(lastErrorDetails);
        }

        if (!string.IsNullOrEmpty(renderErrorMessage))
        {
            lines.Add("");
            lines.Add("Last Render Error: " + renderErrorMessage);
            if (!string.IsNullOrEmpty(renderErrorDetails))
                lines.Add(renderErrorDetails);
        }

        lines.Add("");
        lines.Add("=== Tester Notes ===");
        lines.Add("What were you doing?");
        lines.Add("");
        lines.Add("What did you expect?");
        lines.Add("");
        lines.Add("What happened instead?");
        lines.Add("");

        return string.Join("\\n", lines);
    }

    void DrawHomeReadinessDashboard()
    {
        EditorGUILayout.LabelField("Readiness", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText },
            fontSize = 12
        });

        GUILayout.Space(4);

        if (IsCompactLayout)
        {
            EditorGUILayout.BeginHorizontal();
            DrawHomeStatusChip("Preflight", GetHomePreflightStatus(), GetHomePreflightSeverity(), () => RouteToTool(4, 17));
            GUILayout.Space(3);
            DrawHomeStatusChip(
                "Animator",
                animatorDoctorDirty ? "STALE" : (animatorParamIssues.Any(x => x.severity == MessageType.Error) ? "ERROR" :
                    animatorParamIssues.Any(x => x.severity == MessageType.Warning) ? "WARN" : "READY"),
                animatorDoctorDirty ? MessageType.None :
                    animatorParamIssues.Any(x => x.severity == MessageType.Error) ? MessageType.Error :
                    animatorParamIssues.Any(x => x.severity == MessageType.Warning) ? MessageType.Warning : MessageType.Info,
                () => RouteToTool(4, 13));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            DrawHomeStatusChip(
                "Quest",
                platformCompareDirty ? "STALE" : (platformQuestWarnings.Count > 0 || platformMetrics.Any(x => x.questSeverity != MessageType.Info) ? "WARN" : "READY"),
                platformCompareDirty ? MessageType.None :
                    platformMetrics.Any(x => x.questSeverity == MessageType.Error) ? MessageType.Error :
                    (platformQuestWarnings.Count > 0 || platformMetrics.Any(x => x.questSeverity == MessageType.Warning)) ? MessageType.Warning : MessageType.Info,
                () => RouteToTool(3, 14));
            GUILayout.Space(3);
            DrawHomeStatusChip(
                "Materials",
                materialInspectorDirty ? "STALE" : (materialInspectorNullSlots > 0 ? "ERROR" :
                    materialInspectorDuplicates.Count > 0 ? "WARN" : "READY"),
                materialInspectorDirty ? MessageType.None :
                    materialInspectorNullSlots > 0 ? MessageType.Error :
                    materialInspectorDuplicates.Count > 0 ? MessageType.Warning : MessageType.Info,
                () => RouteToTool(2, 15));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);
            DrawHomeStatusChip(
                "Params",
                ParameterMemoryRemainingBits < 0 ? "OVER" :
                ParameterMemoryRemainingBits <= 16 ? "LOW" : "READY",
                ParameterMemoryRemainingBits < 0 ? MessageType.Error :
                ParameterMemoryRemainingBits <= 16 ? MessageType.Warning : MessageType.Info,
                () => RouteToTool(4, 13));
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            DrawHomeStatusChip("Preflight", GetHomePreflightStatus(), GetHomePreflightSeverity(), () => RouteToTool(4, 17));
            GUILayout.Space(4);
            DrawHomeStatusChip(
                "Quest",
                platformCompareDirty ? "STALE" : (platformQuestWarnings.Count > 0 || platformMetrics.Any(x => x.questSeverity != MessageType.Info) ? "WARN" : "READY"),
                platformCompareDirty ? MessageType.None :
                    platformMetrics.Any(x => x.questSeverity == MessageType.Error) ? MessageType.Error :
                    (platformQuestWarnings.Count > 0 || platformMetrics.Any(x => x.questSeverity == MessageType.Warning)) ? MessageType.Warning : MessageType.Info,
                () => RouteToTool(3, 14));
            GUILayout.Space(4);
            DrawHomeStatusChip(
                "Animator",
                animatorDoctorDirty ? "STALE" : (animatorParamIssues.Any(x => x.severity == MessageType.Error) ? "ERROR" :
                    animatorParamIssues.Any(x => x.severity == MessageType.Warning) ? "WARN" : "READY"),
                animatorDoctorDirty ? MessageType.None :
                    animatorParamIssues.Any(x => x.severity == MessageType.Error) ? MessageType.Error :
                    animatorParamIssues.Any(x => x.severity == MessageType.Warning) ? MessageType.Warning : MessageType.Info,
                () => RouteToTool(4, 13));
            GUILayout.Space(4);
            DrawHomeStatusChip(
                "Materials",
                materialInspectorDirty ? "STALE" : (materialInspectorNullSlots > 0 ? "ERROR" :
                    materialInspectorDuplicates.Count > 0 ? "WARN" : "READY"),
                materialInspectorDirty ? MessageType.None :
                    materialInspectorNullSlots > 0 ? MessageType.Error :
                    materialInspectorDuplicates.Count > 0 ? MessageType.Warning : MessageType.Info,
                () => RouteToTool(2, 15));
            GUILayout.Space(4);
            DrawHomeStatusChip(
                "Params",
                ParameterMemoryRemainingBits < 0 ? "OVER" :
                ParameterMemoryRemainingBits <= 16 ? "LOW" : "READY",
                ParameterMemoryRemainingBits < 0 ? MessageType.Error :
                ParameterMemoryRemainingBits <= 16 ? MessageType.Warning : MessageType.Info,
                () => RouteToTool(4, 13));
            EditorGUILayout.EndHorizontal();
        }
    }

    void DrawHomeStatusChip(string label, string value, MessageType severity, Action route)
    {
        Color color =
            severity == MessageType.Error ? DangerRed :
            severity == MessageType.Warning ? ColGradPink :
            severity == MessageType.Info ? ColLavenderText :
            ColTextMuted;

        var box = new GUIStyle
        {
            normal = { background = texInfoBox },
            border = new RectOffset(8, 8, 8, 8),
            padding = new RectOffset(8, 8, 7, 8)
        };

        var labelStyle = new GUIStyle(styleCardSubtitle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            clipping = TextClipping.Clip,
            margin = new RectOffset(0, 0, 0, 4)
        };

        var valueStyle = new GUIStyle(styleBtnDark)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter,
            contentOffset = Vector2.zero,
            padding = new RectOffset(8, 8, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            fixedHeight = 28
        };
        valueStyle.normal.background = texBtnDark;
        valueStyle.normal.textColor = color;
        valueStyle.hover.background = texBtnDarkHover;
        valueStyle.hover.textColor = Color.white;
        valueStyle.active.background = texBtnDarkHover;
        valueStyle.active.textColor = Color.white;
        valueStyle.focused.background = texBtnDark;
        valueStyle.focused.textColor = color;

        EditorGUILayout.BeginVertical(box, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));

        // Tool name on top.
        EditorGUILayout.LabelField(
            new GUIContent(label, label + " readiness status"),
            labelStyle,
            GUILayout.Height(16));

        // Current state on the bottom.
        if (GUILayout.Button(
            new GUIContent(
                value,
                label + " status: " + value + ". Click to open the related diagnostic tool."),
            valueStyle,
            GUILayout.Height(28)))
        {
            route?.Invoke();
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.EndVertical();
    }

    void DrawHomeStat(string label, string value, string tooltip = null)
    {
        var style = new GUIStyle
        {
            normal = { background = texInfoBox },
            border = new RectOffset(8, 8, 8, 8),
            padding = new RectOffset(7, 7, 7, 7)
        };

        var labelContent = new GUIContent(label, tooltip ?? "");
        var valueContent = new GUIContent(value, tooltip ?? "");

        var labelStyle = new GUIStyle(styleCardSubtitle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            margin = new RectOffset(0, 0, 0, 3)
        };

        var valueStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = ColTextPrimary },
            fontSize = 12,
            margin = new RectOffset(0, 0, 0, 0)
        };

        EditorGUILayout.BeginVertical(style, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));

        // Metric label on top.
        EditorGUILayout.LabelField(
            labelContent,
            labelStyle,
            GUILayout.Height(16));

        // Current value underneath.
        EditorGUILayout.LabelField(
            valueContent,
            valueStyle,
            GUILayout.Height(19));

        EditorGUILayout.EndVertical();
    }

    void DrawHomeChange(string title, string text)
    {
        EditorGUILayout.BeginVertical(styleRowBg);
        EditorGUILayout.LabelField(title, new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColGradPink }
        });
        EditorGUILayout.LabelField(text, styleInfoText);
        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    void DrawRoadmapItem(string title, string text)
    {
        EditorGUILayout.BeginVertical(styleRowBg);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(title, new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText }
        });
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("IDEA", styleCardSubtitle, GUILayout.Width(34));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(text, styleInfoText);
        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    void DrawPreferences()
    {
        preferencesScroll = EditorGUILayout.BeginScrollView(
            preferencesScroll,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true));

        BeginCard(CardIcon.Sparkle, "Settings & Preferences", "Choose which integrations and diagnostics Chi's Little Helper exposes.");
        DrawHelpHint("Hover controls for context. Destructive confirmations are recommended during beta testing; automatic avatar snapshots are heavier and remain opt-in.");

        EditorGUILayout.LabelField("Integrations", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText },
            fontSize = 13
        });
        GUILayout.Space(4);

        EditorGUILayout.BeginVertical(styleRowBg);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Show VRCFury Features", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColTextPrimary }
        });
        EditorGUILayout.LabelField(
            "Show Outfit Builder, toggles, Armature Link, VRCFury blendshape/material tools and Overview++.",
            styleCardSubtitle);
        EditorGUILayout.EndVertical();
        GUILayout.Space(12);

        bool newVf = DrawPreferenceSwitch(enableVRCFuryFeatures);
        if (newVf != enableVRCFuryFeatures)
        {
            enableVRCFuryFeatures = newVf;
            EditorPrefs.SetBool(PREF_VRCFURY_FEATURES, enableVRCFuryFeatures);
            CheckDependencies(enableVRCFuryFeatures);
            EnsureVisibleToolSelection();
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);
        if (enableVRCFuryFeatures)
        {
            string status = deps != null && deps.vrcfuryFound
                ? "VRCFury detected" + (!string.IsNullOrEmpty(deps.vrcfuryVersion) ? " · " + deps.vrcfuryVersion : "")
                : "VRCFury is not currently detected.";

            EditorGUILayout.LabelField(status, styleCardSubtitle);

            if (deps == null || !deps.vrcfuryFound)
            {
                GUILayout.Space(4);
                if (GUILayout.Button("Add VRCFury to VCC", styleBtnDark, GUILayout.Height(28)))
                    AddVRCFuryToVCC();
            }
        }
        else
        {
            EditorGUILayout.LabelField(
                "VRCFury-only tools are hidden from navigation. SDK-native tools, Optimize and Doctor remain available.",
                styleCardSubtitle);
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Safety", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText },
            fontSize = 13
        });
        GUILayout.Space(4);

        EditorGUILayout.BeginVertical(styleRowBg);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Confirm Destructive Actions", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColTextPrimary }
        });
        EditorGUILayout.LabelField("Ask before deletes, missing-script removal and other destructive operations.", styleCardSubtitle);
        EditorGUILayout.EndVertical();
        GUILayout.Space(12);
        bool newConfirm = DrawPreferenceSwitch(confirmDestructiveActions);
        if (newConfirm != confirmDestructiveActions)
        {
            confirmDestructiveActions = newConfirm;
            EditorPrefs.SetBool(PREF_CONFIRM_DESTRUCTIVE, confirmDestructiveActions);
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Automatic Avatar Snapshot", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColTextPrimary }
        });
        EditorGUILayout.LabelField("Create a disabled scene duplicate before destructive avatar edits. Disabled by default because large avatars can be expensive to duplicate.", styleCardSubtitle);
        EditorGUILayout.EndVertical();
        GUILayout.Space(12);
        bool newBackup = DrawPreferenceSwitch(autoBackupBeforeDestructive);
        if (newBackup != autoBackupBeforeDestructive)
        {
            autoBackupBeforeDestructive = newBackup;
            EditorPrefs.SetBool(PREF_AUTO_BACKUP, autoBackupBeforeDestructive);
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);
        using (new EditorGUI.DisabledScope(avatar == null))
        {
            if (GUILayout.Button("Create Avatar Snapshot Now", styleBtnDark, GUILayout.Height(30)))
                Run("Create Avatar Snapshot", () => CreateAvatarSnapshot("Manual"));
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Onboarding", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText },
            fontSize = 13
        });
        GUILayout.Space(4);

        EditorGUILayout.BeginVertical(styleRowBg);
        EditorGUILayout.LabelField(
            onboardingDismissed ? "First-run guide is hidden." : "First-run guide will be shown on Home.",
            styleCardSubtitle);

        if (GUILayout.Button("Show First-Run Guide on Home", styleBtnDark, GUILayout.Height(28)))
        {
            onboardingDismissed = false;
            EditorPrefs.SetBool(PREF_ONBOARDING_DISMISSED, false);
            showPreferences = false;
            showHome = true;
            scroll = Vector2.zero;
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Avatar Doctor 2.0 Thresholds", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText },
            fontSize = 13
        });
        GUILayout.Space(4);

        EditorGUILayout.BeginVertical(styleRowBg);
        DrawThresholdInt("Triangle review threshold", ref doctorTriangleThreshold, PREF_DOCTOR_TRIANGLES, 1000, 1000000);
        DrawThresholdInt("Material slot warning", ref doctorMaterialSlotThreshold, PREF_DOCTOR_MATERIAL_SLOTS, 1, 256);
        DrawThresholdInt("Renderer warning", ref doctorRendererThreshold, PREF_DOCTOR_RENDERERS, 1, 256);
        DrawThresholdInt("PhysBone warning", ref doctorPhysBoneThreshold, PREF_DOCTOR_PHYSBONES, 1, 256);
        DrawThresholdInt("Texture memory warning (MB)", ref doctorTextureMemoryThresholdMb, PREF_DOCTOR_TEXTURE_MB, 1, 4096);
        DrawThresholdInt("Large texture threshold", ref doctorLargeTextureThreshold, PREF_DOCTOR_TEXTURE_SIZE, 256, 16384);

        GUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset Doctor Thresholds", styleBtnDark, GUILayout.Width(160), GUILayout.Height(28)))
        {
            ResetDoctorThresholds();
            doctorDirty = true;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Version & Dependencies", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText },
            fontSize = 13
        });
        GUILayout.Space(4);

        EditorGUILayout.BeginVertical(styleRowBg);
        DrawDiagnosticRow("Toolkit", TOOL_VERSION + " · " + RELEASE_CHANNEL);
        DrawDiagnosticRow("Unity", deps != null ? deps.unityVersion : Application.unityVersion);
        DrawDiagnosticRow("VRChat SDK", deps != null && !string.IsNullOrEmpty(deps.sdkVersion) ? deps.sdkVersion : "Unknown");
        DrawDiagnosticRow("SDK Assembly", deps != null && !string.IsNullOrEmpty(deps.sdkAssembly) ? deps.sdkAssembly : "Unknown");

        string vfVersion =
            deps != null && deps.vrcfuryFound
                ? (string.IsNullOrEmpty(deps.vrcfuryVersion) ? "Detected · version unknown" : deps.vrcfuryVersion)
                : "Not detected";
        DrawDiagnosticRow("VRCFury", vfVersion);

        if (deps != null && !string.IsNullOrEmpty(deps.vrcfuryAssembly))
            DrawDiagnosticRow("VRCFury Assembly", deps.vrcfuryAssembly);

        if (deps != null && deps.missingTypes.Count > 0)
            EditorGUILayout.HelpBox("VRCFury compatibility check is missing: " + string.Join(", ", deps.missingTypes), MessageType.Warning);
        else if (deps != null && deps.vrcfuryFound)
            EditorGUILayout.HelpBox("VRCFury reflection compatibility check passed for the feature types used by this build.", MessageType.Info);

        BeginResponsiveButtonRow();
        if (GUILayout.Button("Refresh Dependencies", styleBtnDark, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
            CheckDependencies(false);
        ResponsiveButtonGap(5f);
        if (GUILayout.Button("Copy Environment Info", styleBtnDark, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
            EditorGUIUtility.systemCopyBuffer = BuildEnvironmentReport();
        EndResponsiveButtonRow();
        EditorGUILayout.EndVertical();

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Diagnostics", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText },
            fontSize = 13
        });
        GUILayout.Space(4);

        EditorGUILayout.BeginVertical(styleRowBg);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Verbose Console Logging", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColTextPrimary }
        });
        EditorGUILayout.LabelField("Print additional dependency and toolkit diagnostics to the Unity Console.", styleCardSubtitle);
        EditorGUILayout.EndVertical();
        GUILayout.Space(12);

        bool newVerbose = DrawPreferenceSwitch(Verbose);
        if (newVerbose != Verbose)
        {
            Verbose = newVerbose;
            EditorPrefs.SetBool(PREF_VERBOSE_LOGGING, Verbose);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Installation Health", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText },
            fontSize = 13
        });
        GUILayout.Space(4);

        EditorGUILayout.BeginVertical(styleRowBg);

        if (!installationHealthScanned)
            ScanInstallationHealth();

        EditorGUILayout.LabelField(
            HasLikelyDuplicateInstallation
                ? "Potential duplicate installation detected"
                : "Installation scan looks clean",
            new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = HasLikelyDuplicateInstallation ? ColGradPink : ColLavenderText }
            });

        EditorGUILayout.LabelField(installationHealthNote, styleInfoText);

        if (HasLikelyDuplicateInstallation)
        {
            GUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "For upgrades, replace the previous Chi's Little Helper folder instead of installing versions side-by-side. Duplicate Editor scripts can cause duplicate classes, menu items, or stale code to compile.",
                MessageType.Warning);
        }

        GUILayout.Space(5);
        BeginResponsiveButtonRow();
        if (GUILayout.Button("Rescan Installation", styleBtnDark, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
            ScanInstallationHealth();

        ResponsiveButtonGap(5f);

        if (GUILayout.Button("Copy Install Report", styleBtnDark, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
        {
            EditorGUIUtility.systemCopyBuffer = BuildInstallationHealthReport();
            ShowNotification(new GUIContent("Installation report copied"));
        }
        EndResponsiveButtonRow();

        EditorGUILayout.EndVertical();

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Support & Diagnostics", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText },
            fontSize = 13
        });
        GUILayout.Space(4);

        EditorGUILayout.BeginVertical(styleRowBg);
        EditorGUILayout.LabelField(
            "Export Support Bundle",
            new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColTextPrimary }
            });
        EditorGUILayout.LabelField(
            "Creates a shareable bundle containing environment details, installation health, known limitations, current Pre-Upload results when fresh, and the latest action/render errors when present.",
            styleInfoText);

        GUILayout.Space(6);

        if (HelpButton(
            "Export Support Bundle",
            "Export a ZIP when supported by the Unity runtime. If ZIP support is unavailable, the same files are exported as a folder.",
            styleBtnGrad,
            GUILayout.Height(32)))
        {
            Run("Export Support Bundle", ExportSupportBundle);
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(12);
        EditorGUILayout.LabelField("About & Known Limitations", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText },
            fontSize = 13
        });
        GUILayout.Space(4);

        EditorGUILayout.BeginVertical(styleRowBg);
        DrawDiagnosticRow("Toolkit", TOOL_VERSION + " · " + RELEASE_CHANNEL);
        DrawDiagnosticRow("Purpose", "VRChat avatar workflow helper");
        GUILayout.Space(5);

        EditorGUILayout.LabelField(
            "Known limitations",
            new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColTextPrimary }
            });

        foreach (string limitation in KnownLimitations)
        {
            GUILayout.Space(3);
            EditorGUILayout.LabelField("• " + limitation, styleInfoText);
        }

        GUILayout.Space(6);
        if (GUILayout.Button("Copy Known Limitations", styleBtnDark, GUILayout.Height(28)))
        {
            EditorGUIUtility.systemCopyBuffer = BuildKnownLimitationsReport();
            ShowNotification(new GUIContent("Known limitations copied"));
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(12);
        DrawInfoBox("Preferences are stored in the Unity Editor and persist between sessions.");

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset Preferences", styleBtnRemove, GUILayout.Width(130), GUILayout.Height(30)))
        {
            EditorPrefs.DeleteKey(PREF_VRCFURY_FEATURES);
            EditorPrefs.DeleteKey(PREF_VERBOSE_LOGGING);
            EditorPrefs.DeleteKey(PREF_CONFIRM_DESTRUCTIVE);
            EditorPrefs.DeleteKey(PREF_AUTO_BACKUP);
            EditorPrefs.DeleteKey(PREF_ONBOARDING_DISMISSED);

            enableVRCFuryFeatures = true;
            Verbose = false;
            confirmDestructiveActions = true;
            autoBackupBeforeDestructive = false;
            onboardingDismissed = false;
            ResetDoctorThresholds();

            CheckDependencies(false);
            EnsureVisibleToolSelection();
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        EndCard();

        EditorGUILayout.EndScrollView();
    }

    void DrawThresholdInt(string label, ref int value, string prefKey, int min, int max)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, styleFieldLabel);
        EditorGUI.BeginChangeCheck();
        int next = EditorGUILayout.IntField(value, styleNumberField, GUILayout.Width(92));
        if (EditorGUI.EndChangeCheck())
        {
            value = Mathf.Clamp(next, min, max);
            EditorPrefs.SetInt(prefKey, value);
            doctorDirty = true;
        }
        EditorGUILayout.EndHorizontal();
    }

    void ResetDoctorThresholds()
    {
        doctorTriangleThreshold = 70000;
        doctorMaterialSlotThreshold = 16;
        doctorRendererThreshold = 16;
        doctorPhysBoneThreshold = 16;
        doctorTextureMemoryThresholdMb = 128;
        doctorLargeTextureThreshold = 2048;

        EditorPrefs.DeleteKey(PREF_DOCTOR_TRIANGLES);
        EditorPrefs.DeleteKey(PREF_DOCTOR_MATERIAL_SLOTS);
        EditorPrefs.DeleteKey(PREF_DOCTOR_RENDERERS);
        EditorPrefs.DeleteKey(PREF_DOCTOR_PHYSBONES);
        EditorPrefs.DeleteKey(PREF_DOCTOR_TEXTURE_MB);
        EditorPrefs.DeleteKey(PREF_DOCTOR_TEXTURE_SIZE);
    }

    void DrawDiagnosticRow(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, styleFieldLabel, GUILayout.Width(118));
        EditorGUILayout.LabelField(string.IsNullOrEmpty(value) ? "Unknown" : value, styleCardSubtitle);
        EditorGUILayout.EndHorizontal();
    }

    string BuildEnvironmentReport()
    {
        if (deps == null) CheckDependencies(false);

        var lines = new List<string>
        {
            "Chi's Little Helper " + TOOL_VERSION + " (" + RELEASE_CHANNEL + ")",
            "Unity: " + (deps != null ? deps.unityVersion : Application.unityVersion),
            "VRChat SDK: " + (deps != null ? (deps.sdkVersion ?? "Unknown") : "Unknown"),
            "SDK Assembly: " + (deps != null ? (deps.sdkAssembly ?? "Unknown") : "Unknown"),
            "VRCFury Enabled in Preferences: " + enableVRCFuryFeatures,
            "VRCFury Detected: " + (deps != null && deps.vrcfuryFound),
            "VRCFury Version: " + (deps != null ? (deps.vrcfuryVersion ?? "Unknown") : "Unknown"),
            "VRCFury Assembly: " + (deps != null ? (deps.vrcfuryAssembly ?? "Unknown") : "Unknown"),
            "Missing Compatibility Types: " + (deps != null && deps.missingTypes.Count > 0 ? string.Join(", ", deps.missingTypes) : "None"),
            "Selected Avatar: " + (avatar != null ? avatar.gameObject.name : "None"),
            "Confirm Destructive Actions: " + confirmDestructiveActions,
            "Automatic Avatar Snapshot: " + autoBackupBeforeDestructive
        };

        if (deps != null && !string.IsNullOrEmpty(deps.error))
            lines.Add("Dependency Error: " + deps.error);

        return string.Join("\n", lines);
    }

    bool IsVRCFuryTool(int toolId)
    {
        return toolId == 0 || toolId == 1 || toolId == 2 || toolId == 3 ||
               toolId == 4 || toolId == 5 || toolId == 7;
    }

    bool IsToolVisible(int toolId)
    {
        return enableVRCFuryFeatures || !IsVRCFuryTool(toolId);
    }

    int FirstVisibleToolInCategory(int categoryIndex)
    {
        if (categoryIndex < 0 || categoryIndex >= CategoryToolIds.Length)
            return -1;

        foreach (int id in CategoryToolIds[categoryIndex])
            if (IsToolVisible(id))
                return id;

        return -1;
    }

    void EnsureVisibleToolSelection()
    {
        category = Mathf.Clamp(category, 0, CategoryToolIds.Length - 1);

        bool belongsToCategory = CategoryToolIds[category].Contains(tab);
        if (!belongsToCategory || !IsToolVisible(tab))
            tab = FirstVisibleToolInCategory(category);
    }

    bool DrawPreferenceSwitch(bool value)
    {
        const float width = 46f;
        const float height = 24f;
        const float pad = 3f;

        Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));

        // Track
        Color track = value ? ColGradPurple : ColDarkBtnBg;
        Color border = value ? ColGradPink : ColDarkBtnBorder;

        EditorGUI.DrawRect(rect, border);

        Rect inner = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
        EditorGUI.DrawRect(inner, track);

        // Knob
        float knobSize = height - pad * 2f;
        float knobX = value
            ? rect.xMax - pad - knobSize
            : rect.x + pad;

        Rect knob = new Rect(knobX, rect.y + pad, knobSize, knobSize);
        EditorGUI.DrawRect(knob, value ? ColTextPrimary : ColTextMuted);

        // Small ON/OFF label for extra clarity.
        var labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = value ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight,
            normal = { textColor = ColTextPrimary },
            fontSize = 8,
            padding = value ? new RectOffset(4, 0, 0, 0) : new RectOffset(0, 4, 0, 0)
        };
        GUI.Label(inner, value ? "ON" : "OFF", labelStyle);

        EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

        if (Event.current.type == EventType.MouseDown &&
            Event.current.button == 0 &&
            rect.Contains(Event.current.mousePosition))
        {
            value = !value;
            Event.current.Use();
            GUI.changed = true;
        }

        return value;
    }

    void DrawNavBar()
    {
        EnsureStyles();
        EnsureVisibleToolSelection();

        var barStyle = new GUIStyle
        {
            normal = { background = texCardBg },
            border = new RectOffset(10, 10, 10, 10),
            padding = new RectOffset(4, 4, 4, 4),
            margin = new RectOffset(0, 0, 6, 5)
        };

        EditorGUILayout.BeginHorizontal(barStyle);
        for (int i = 0; i < CategoryLabels.Length; i++)
        {
            bool active = category == i;
            var style = active ? styleTabActive : styleTabInactive;

            if (GUILayout.Button(CategoryLabels[i], style, GUILayout.Height(36)))
            {
                ClearRenderError();
                showHome = false;
                showPreferences = false;
                category = i;
                tab = FirstVisibleToolInCategory(i);
                scroll = Vector2.zero;
                GUI.FocusControl(null);
            }

            if (i < CategoryLabels.Length - 1)
                GUILayout.Space(4);
        }
        EditorGUILayout.EndHorizontal();

        var labels = CategoryToolLabels[Mathf.Clamp(category, 0, CategoryToolLabels.Length - 1)];
        var ids = CategoryToolIds[Mathf.Clamp(category, 0, CategoryToolIds.Length - 1)];

        var visible = new List<int>();
        for (int i = 0; i < labels.Length && i < ids.Length; i++)
            if (IsToolVisible(ids[i]))
                visible.Add(i);

        if (visible.Count == 0)
        {
            EditorGUILayout.BeginHorizontal(barStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("VRCFury features hidden in Preferences", styleCardSubtitle,
                GUILayout.Height(28));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            int perRow = IsCompactLayout && visible.Count > 3
                ? Mathf.CeilToInt(visible.Count / 2f)
                : visible.Count;

            for (int rowStart = 0; rowStart < visible.Count; rowStart += perRow)
            {
                EditorGUILayout.BeginHorizontal(barStyle);

                int rowEnd = Mathf.Min(rowStart + perRow, visible.Count);
                for (int v = rowStart; v < rowEnd; v++)
                {
                    if (v > rowStart)
                        GUILayout.Space(4);

                    int i = visible[v];
                    int id = ids[i];
                    bool active = tab == id;
                    var style = active ? styleTabActive : styleTabInactive;

                    if (GUILayout.Button(labels[i], style, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
                    {
                        ClearRenderError();
                        showHome = false;
                        showPreferences = false;
                        tab = id;
                        scroll = Vector2.zero;
                        GUI.FocusControl(null);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
