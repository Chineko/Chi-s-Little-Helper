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
    void DrawDoctor()
    {
        BeginCard(CardIcon.Warning, "Avatar Doctor 2.0", "Health score, guided diagnostics and direct hand-off into the tool that can help.");

        BeginResponsiveButtonRow();
        if (HelpButton(
            "Run Full Scan",
            "Scan the selected avatar for common hierarchy, material, texture, dynamics and performance issues.",
            styleBtnGrad,
            GUILayout.Height(40),
            GUILayout.ExpandWidth(true)))
        {
            Run("Run Avatar Doctor", () =>
            {
                RunDoctorScan();
                doctorDirty = false;
            });
        }

        ResponsiveButtonGap(8f);

        if (HelpButton(
            "Refresh",
            "Re-run Avatar Doctor and replace the current cached results.",
            styleBtnDark,
            GUILayout.Height(40),
            GUILayout.ExpandWidth(true)))
        {
            Run("Refresh Avatar Doctor", () =>
            {
                RunDoctorScan();
                doctorDirty = false;
            });
        }

        int safeFixCount = doctorIssues.Count(x => x.safeFix && x.fix != null);

        ResponsiveButtonGap(8f);

        using (new EditorGUI.DisabledScope(doctorDirty || safeFixCount == 0))
        {
            if (HelpButton(
                "Fix All Safe (" + safeFixCount + ")",
                "Preview and apply only deterministic, Undo-supported fixes. Ambiguous setup or optimization changes are excluded.",
                styleBtnGrad,
                GUILayout.Height(40),
                GUILayout.ExpandWidth(true)))
            {
                PreviewAndFixAllSafeDoctorIssues();
            }
        }

        EndResponsiveButtonRow();

        GUILayout.Space(8);
        DrawInfoBox("Doctor 2.0 is diagnostic-first. Fix All Safe only includes deterministic, Undo-supported cleanup. It never guesses at optimization choices, deletes assets, changes shaders, rewrites parameters, or alters ambiguous setup.");

        DrawDataFreshnessBanner(
            "Doctor analysis",
            doctorDirty,
            "Doctor results match the current avatar state.",
            () =>
            {
                Run("Refresh Avatar Doctor", () =>
                {
                    RunDoctorScan();
                    doctorDirty = false;
                });
            },
            "Refresh Doctor");

        GUILayout.Space(8);

        int errors = doctorIssues.Count(x => x.severity == MessageType.Error);
        int warnings = doctorIssues.Count(x => x.severity == MessageType.Warning);
        int infos = doctorIssues.Count(x => x.severity == MessageType.Info);

        GUILayout.Space(10);

        // Health header
        var healthBox = new GUIStyle
        {
            normal = { background = texInfoBox },
            border = new RectOffset(8, 8, 8, 8),
            padding = new RectOffset(12, 12, 10, 10)
        };

        EditorGUILayout.BeginHorizontal(healthBox);

        Rect healthRect = GUILayoutUtility.GetRect(
            110f,
            56f,
            GUILayout.Width(110f),
            GUILayout.Height(56f));

        var healthValueStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 28,
            normal =
            {
                textColor = doctorHealthScore >= 90 ? ColLavenderText :
                            doctorHealthScore >= 70 ? ColGradPink : DangerRed
            }
        };

        var healthLabelStyle = new GUIStyle(styleCardSubtitle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9
        };

        Rect valueRect = new Rect(healthRect.x, healthRect.y - 1f, healthRect.width, 34f);
        Rect labelRect = new Rect(healthRect.x, healthRect.y + 31f, healthRect.width, 18f);

        GUI.Label(valueRect, doctorHealthScore.ToString(), healthValueStyle);
        GUI.Label(labelRect, "HEALTH / 100", healthLabelStyle);

        GUILayout.Space(10);
        EditorGUILayout.BeginVertical();
        string healthText = doctorHealthScore >= 90 ? "Healthy" :
                            doctorHealthScore >= 70 ? "Needs Attention" :
                            doctorHealthScore >= 50 ? "Needs Work" : "Critical Review";
        EditorGUILayout.LabelField(healthText, new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            normal = { textColor = ColTextPrimary }
        });
        EditorGUILayout.LabelField($"{errors} errors · {warnings} warnings · {infos} notes", styleCardSubtitle);
        if (doctorLastScanUtc != default(DateTime))
            EditorGUILayout.LabelField("Last scan: " + doctorLastScanUtc.ToLocalTime().ToString("HH:mm:ss"), styleCardSubtitle);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);

        var statStyle = new GUIStyle(styleCardSubtitle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10
        };

        if (IsCompactLayout)
        {
            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("Triangles", doctorTriangles.ToString("N0"), statStyle);
            GUILayout.Space(3);
            DrawDoctorStat("Mat Slots", doctorMaterialSlots.ToString(), statStyle);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("Textures", FormatBytes(doctorTextureBytes), statStyle);
            GUILayout.Space(3);
            DrawDoctorStat("PhysBones", doctorPhysBones.ToString(), statStyle);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("Contacts", doctorContacts.ToString(), statStyle);
            GUILayout.Space(3);
            DrawDoctorStat("Constraints", doctorConstraints.ToString(), statStyle);
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("Triangles", doctorTriangles.ToString("N0"), statStyle);
            GUILayout.Space(4);
            DrawDoctorStat("Mat Slots", doctorMaterialSlots.ToString(), statStyle);
            GUILayout.Space(4);
            DrawDoctorStat("Textures", FormatBytes(doctorTextureBytes), statStyle);
            GUILayout.Space(4);
            DrawDoctorStat("PhysBones", doctorPhysBones.ToString(), statStyle);
            GUILayout.Space(4);
            DrawDoctorStat("Contacts", doctorContacts.ToString(), statStyle);
            GUILayout.Space(4);
            DrawDoctorStat("Constraints", doctorConstraints.ToString(), statStyle);
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        // Quick hand-off row
        BeginResponsiveButtonRow();
        if (GUILayout.Button("Texture & VRAM", styleBtnDark, GUILayout.Height(30), GUILayout.ExpandWidth(true)))
            RouteToTool(3, 11);
        ResponsiveButtonGap();
        if (GUILayout.Button("PhysBones", styleBtnDark, GUILayout.Height(30), GUILayout.ExpandWidth(true)))
            RouteToTool(1, 9);
        ResponsiveButtonGap();
        if (GUILayout.Button("Constraints", styleBtnDark, GUILayout.Height(30), GUILayout.ExpandWidth(true)))
            RouteToTool(1, 10);
        EndResponsiveButtonRow();

        GUILayout.Space(12);

        if (doctorIssues.Count == 0)
        {
            EditorGUILayout.HelpBox("No common avatar setup problems were found by this scan.", MessageType.Info);
        }
        else
        {
            foreach (var group in doctorIssues
                .OrderByDescending(x => DoctorSeverityRank(x.severity))
                .ThenBy(x => x.category)
                .GroupBy(x => string.IsNullOrEmpty(x.category) ? "General" : x.category))
            {
                EditorGUILayout.LabelField(group.Key, new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = ColLavenderText },
                    fontSize = 12
                });
                GUILayout.Space(3);

                foreach (var issue in group)
                {
                    EditorGUILayout.BeginVertical(styleRowBg);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(issue.title,
                        new GUIStyle(EditorStyles.boldLabel)
                        {
                            normal =
                            {
                                textColor = issue.severity == MessageType.Error
                                    ? DangerRed
                                    : issue.severity == MessageType.Warning ? ColGradPink : ColLavenderText
                            }
                        });
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(DoctorSeverityLabel(issue.severity),
                        styleCardSubtitle, GUILayout.Width(58));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField(issue.detail, styleInfoText);
                    GUILayout.Space(4);

                    BeginResponsiveButtonRow();
                    if (!IsCompactLayout)
                        GUILayout.FlexibleSpace();

                    if (issue.target != null && GUILayout.Button("Select", styleBtnDark, ResponsiveButtonWidth(72), GUILayout.Height(28)))
                    {
                        Selection.activeObject = issue.target;
                        EditorGUIUtility.PingObject(issue.target);
                    }

                    if (issue.route != null)
                    {
                        GUILayout.Space(5);
                        string routeText = string.IsNullOrEmpty(issue.routeLabel) ? "Open Tool" : issue.routeLabel;
                        float routeWidth = Mathf.Max(112f, styleBtnDark.CalcSize(new GUIContent(routeText)).x + 22f);
                        if (GUILayout.Button(routeText,
                            styleBtnDark, ResponsiveButtonWidth(routeWidth), GUILayout.Height(28)))
                        {
                            issue.route();
                            GUIUtility.ExitGUI();
                        }
                    }

                    if (issue.fix != null)
                    {
                        GUILayout.Space(5);
                        string fixText = string.IsNullOrEmpty(issue.fixLabel) ? "Fix" : issue.fixLabel;
                        string fixTooltip = issue.safeFix
                            ? "Classified as a safe, deterministic fix and eligible for Fix All Safe."
                            : "Apply this issue-specific fix.";

                        if (HelpButton(
                            fixText,
                            fixTooltip,
                            styleBtnGrad,
                            ResponsiveButtonWidth(98),
                            GUILayout.Height(28)))
                        {
                            if (issue.safeFix)
                            {
                                RunDestructive(
                                    "Doctor fix: " + issue.title,
                                    "Apply the safe fix for '" + issue.title + "'?",
                                    () =>
                                    {
                                        issue.fix();
                                        doctorDirty = true;
                                        preflightDirty = true;
                                    });
                            }
                            else
                            {
                                Run("Doctor fix: " + issue.title, () =>
                                {
                                    issue.fix();
                                    doctorDirty = true;
                                    preflightDirty = true;
                                });
                            }

                            GUIUtility.ExitGUI();
                        }
                    }

                    EndResponsiveButtonRow();
                    EditorGUILayout.EndVertical();
                    GUILayout.Space(6);
                }

                GUILayout.Space(4);
            }
        }

        EndCard();
    }


    void PreviewAndFixAllSafeDoctorIssues()
    {
        List<DoctorIssue> safeIssues = doctorIssues
            .Where(x => x.safeFix && x.fix != null)
            .ToList();

        if (safeIssues.Count == 0)
        {
            ShowNotification(new GUIContent("No safe fixes available"));
            return;
        }

        string preview = BuildSafeFixPreview(safeIssues);

        if (!EditorUtility.DisplayDialog(
            "Fix All Safe Issues",
            preview + "\n\nOnly the listed fixes will run. Ambiguous issues are excluded.",
            "Apply " + safeIssues.Count + " Safe Fix" + (safeIssues.Count == 1 ? "" : "es"),
            "Cancel"))
            return;

        RunDestructive(
            "Fix All Safe Doctor Issues",
            "Apply the previewed safe fixes?",
            () =>
            {
                int applied = 0;

                foreach (DoctorIssue issue in safeIssues)
                {
                    if (!issue.safeFix || issue.fix == null)
                        continue;

                    issue.fix();
                    applied++;
                }

                doctorDirty = true;
                preflightDirty = true;
                materialInspectorDirty = true;
                textureVramDirty = true;
                parameterMemoryDirty = true;

                ShowNotification(new GUIContent(
                    "Applied " + applied + " safe fix" + (applied == 1 ? "" : "es")));
            });
    }

    string BuildSafeFixPreview(List<DoctorIssue> safeIssues)
    {
        var lines = new List<string>
        {
            "Chi's Little Helper will apply:",
            ""
        };

        foreach (DoctorIssue issue in safeIssues)
        {
            lines.Add("• " + issue.title);
            lines.Add("  " + (
                string.IsNullOrWhiteSpace(issue.safeFixDescription)
                    ? issue.detail
                    : issue.safeFixDescription));
        }

        return string.Join("\n", lines);
    }

    int DoctorSeverityRank(MessageType severity)
    {
        if (severity == MessageType.Error) return 3;
        if (severity == MessageType.Warning) return 2;
        if (severity == MessageType.Info) return 1;
        return 0;
    }

    string DoctorSeverityLabel(MessageType severity)
    {
        if (severity == MessageType.Error) return "ERROR";
        if (severity == MessageType.Warning) return "WARN";
        if (severity == MessageType.Info) return "INFO";
        return "NOTE";
    }

    string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 MB";
        return (bytes / (1024f * 1024f)).ToString("0.0") + " MB";
    }

    void RouteToTool(int categoryIndex, int toolId)
    {
        showPreferences = false;
        showHome = false;
        category = Mathf.Clamp(categoryIndex, 0, CategoryToolIds.Length - 1);
        tab = toolId;
        scroll = Vector2.zero;
        GUI.FocusControl(null);
        Repaint();
    }

    void DrawDoctorStat(string label, string value, GUIStyle textStyle)
    {
        var style = new GUIStyle
        {
            normal = { background = texInfoBox },
            border = new RectOffset(8, 8, 8, 8),
            padding = new RectOffset(6, 6, 7, 7)
        };

        var labelStyle = new GUIStyle(textStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            fontStyle = FontStyle.Bold
        };
        labelStyle.normal.textColor = ColLavenderText;
        labelStyle.hover.textColor = ColLavenderText;
        labelStyle.focused.textColor = ColLavenderText;
        labelStyle.active.textColor = ColLavenderText;

        var valueStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            normal = { textColor = ColTextPrimary }
        };

        EditorGUILayout.BeginVertical(style, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField(label, labelStyle, GUILayout.Height(16));
        EditorGUILayout.LabelField(value, valueStyle, GUILayout.Height(20));
        EditorGUILayout.EndVertical();
    }

    void RunDoctorScan()
    {
        doctorIssues.Clear();
        doctorTriangles = doctorMaterialSlots = doctorRendererCount = doctorSkinnedCount = 0;
        doctorPhysBones = doctorContacts = doctorConstraints = 0;
        doctorTextureBytes = 0;
        doctorLargeTextures = 0;
        doctorHealthScore = 100;

        if (avatar == null) return;

        var root = avatar.gameObject;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        doctorRendererCount = renderers.Length;

        var seenMaterials = new Dictionary<Material, int>();

        foreach (var r in renderers)
        {
            if (r == null) continue;

            if (r is SkinnedMeshRenderer smr)
            {
                doctorSkinnedCount++;

                if (smr.sharedMesh != null)
                {
                    long indices = 0;
                    for (int sm = 0; sm < smr.sharedMesh.subMeshCount; sm++)
                        indices += (long)smr.sharedMesh.GetIndexCount(sm);
                    doctorTriangles += (int)Math.Min(int.MaxValue, indices / 3L);
                }
                else
                {
                    AddDoctorIssue(
                        MessageType.Warning, "Meshes",
                        "Skinned mesh has no mesh",
                        smr.name + " has a SkinnedMeshRenderer with no Shared Mesh assigned.",
                        smr, null, null, null, null);
                }
            }
            else
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    long indices = 0;
                    for (int sm = 0; sm < mf.sharedMesh.subMeshCount; sm++)
                        indices += (long)mf.sharedMesh.GetIndexCount(sm);
                    doctorTriangles += (int)Math.Min(int.MaxValue, indices / 3L);
                }
            }

            var mats = r.sharedMaterials;
            doctorMaterialSlots += mats != null ? mats.Length : 0;
            if (mats == null) continue;

            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null)
                {
                    Renderer capturedRenderer = r;
                    AddDoctorIssue(
                        MessageType.Error, "Materials",
                        "Missing material",
                        $"{r.name} has an empty material slot at index {i}.",
                        r, null, null,
                        () =>
                        {
                            Selection.activeObject = capturedRenderer;
                            EditorGUIUtility.PingObject(capturedRenderer);
                        },
                        "Inspect");
                    continue;
                }

                seenMaterials.TryGetValue(mat, out int count);
                seenMaterials[mat] = count + 1;
            }
        }

        // Texture diagnostics share the analyzer cache. Hierarchy/avatar changes mark it dirty,
        // so Doctor does not repeatedly rescan every material/texture on every repaint.
        if (textureVramDirty)
        {
            RebuildTextureVramCache();
            textureVramDirty = false;
        }
        doctorTextureBytes = textureTotalBytes;
        doctorLargeTextures = textureVramEntries.Count(x =>
            x.texture != null && (x.texture.width > doctorLargeTextureThreshold || x.texture.height > doctorLargeTextureThreshold));

        if (doctorLargeTextures > 0)
        {
            AddDoctorIssue(
                MessageType.Warning, "Textures",
                "Large textures detected",
                $"{doctorLargeTextures} texture{(doctorLargeTextures == 1 ? "" : "s")} exceed {doctorLargeTextureThreshold}px on at least one axis. Review them in Texture & VRAM before changing import limits.",
                root, null, null,
                () =>
                {
                    textureSearch = "";
                    RouteToTool(3, 11);
                },
                "Open Analyzer");
        }

        // This is deliberately a toolkit heuristic rather than a claimed VRChat performance rank.
        if (doctorTextureBytes > (long)doctorTextureMemoryThresholdMb * 1024L * 1024L)
        {
            AddDoctorIssue(
                MessageType.Warning, "Textures",
                "High estimated texture memory",
                $"The analyzer estimates about {FormatBytes(doctorTextureBytes)} of texture memory. Treat this as a review signal; actual platform/GPU memory can differ.",
                root, null, null,
                () => RouteToTool(3, 11),
                "Open Analyzer");
        }

        // Missing scripts: Unity-supported cleanup with Undo.
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var go = t.gameObject;
            var components = go.GetComponents<Component>();
            int missing = components.Count(c => c == null);
            if (missing <= 0) continue;

            GameObject captured = go;
            AddDoctorIssue(
                MessageType.Error, "Hierarchy",
                "Missing script",
                $"{go.name} contains {missing} missing component reference{(missing == 1 ? "" : "s")}.",
                go,
                () =>
                {
                    Undo.RegisterCompleteObjectUndo(captured, "Remove Missing Scripts");
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(captured);
                    EditorUtility.SetDirty(captured);
                },
                "Remove",
                null, null,
                true,
                "Remove broken MonoBehaviour references while preserving the GameObject and its remaining components.");
        }

        // Duplicate sibling names.
        foreach (var parent in root.GetComponentsInChildren<Transform>(true))
        {
            var names = new HashSet<string>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!names.Add(child.name))
                {
                    AddDoctorIssue(
                        MessageType.Warning, "Hierarchy",
                        "Duplicate sibling name",
                        $"'{child.name}' appears more than once under '{parent.name}'. This can make hierarchy and animation-path debugging harder.",
                        child.gameObject, null, null, null, null);
                }
            }
        }

        Type physBoneType = FindType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone");
        Type senderType = FindType(T_CONTACT_SENDER);
        Type receiverType = FindType(T_CONTACT_RECEIVER);

        if (physBoneType != null)
        {
            var pbs = root.GetComponentsInChildren(physBoneType, true);
            doctorPhysBones = pbs.Length;

            foreach (Component pb in pbs)
            {
                object pbRoot = GetMember(pb, "rootTransform");
                if (pbRoot == null)
                {
                    Component capturedPb = pb;
                    AddDoctorIssue(
                        MessageType.Warning, "Dynamics",
                        "PhysBone has no explicit root",
                        $"{pb.gameObject.name} has a PhysBone with no Root Transform assigned. Verify that this is intentional.",
                        pb, null, null,
                        () =>
                        {
                            physBoneSearch = capturedPb.gameObject.name;
                            RouteToTool(1, 9);
                        },
                        "Open PhysBones");
                }
            }

            if (doctorPhysBones > doctorPhysBoneThreshold)
            {
                AddDoctorIssue(
                    MessageType.Warning, "Dynamics",
                    "Many PhysBone components",
                    $"Found {doctorPhysBones} PhysBone components. Review the list for redundant chains or duplicated setups.",
                    root, null, null,
                    () =>
                    {
                        physBoneSearch = "";
                        RouteToTool(1, 9);
                    },
                    "Open PhysBones");
            }
        }

        if (senderType != null)
            doctorContacts += root.GetComponentsInChildren(senderType, true).Length;
        if (receiverType != null)
            doctorContacts += root.GetComponentsInChildren(receiverType, true).Length;

        doctorConstraints =
            root.GetComponentsInChildren<ParentConstraint>(true).Length +
            root.GetComponentsInChildren<PositionConstraint>(true).Length +
            root.GetComponentsInChildren<RotationConstraint>(true).Length +
            root.GetComponentsInChildren<ScaleConstraint>(true).Length +
            root.GetComponentsInChildren<AimConstraint>(true).Length +
            root.GetComponentsInChildren<LookAtConstraint>(true).Length;

        if (doctorMaterialSlots > doctorMaterialSlotThreshold)
        {
            AddDoctorIssue(
                MessageType.Warning, "Materials",
                "High material-slot count",
                $"The avatar currently uses {doctorMaterialSlots} renderer material slots. Review renderers and textures for consolidation opportunities.",
                root, null, null,
                () => RouteToTool(2, 15),
                "Open Material Inspector++");
        }

        if (doctorRendererCount > doctorRendererThreshold)
        {
            AddDoctorIssue(
                MessageType.Warning, "Performance",
                "Many renderers",
                $"The avatar contains {doctorRendererCount} renderers ({doctorSkinnedCount} skinned). Review whether very small or redundant renderer objects can be consolidated.",
                root, null, null,
                () => RouteToTool(3, 14),
                "Open PC / Quest");
        }

        if (doctorTriangles > doctorTriangleThreshold)
        {
            AddDoctorIssue(
                MessageType.Warning, "Performance",
                "High triangle count",
                $"Approximate rendered triangle count is {doctorTriangles:N0}. This is a toolkit review threshold, not an automatic optimization target.",
                root, null, null,
                () => RouteToTool(3, 14),
                "Open PC / Quest");
        }

        if (doctorConstraints > 0)
        {
            AddDoctorIssue(
                MessageType.Info, "Dynamics",
                "Constraints detected",
                $"Found {doctorConstraints} Unity constraint component{(doctorConstraints == 1 ? "" : "s")}. Constraint Toolkit can inspect, enable, disable and remove them.",
                root, null, null,
                () =>
                {
                    constraintSearch = "";
                    RouteToTool(1, 10);
                },
                "Open Constraints");
        }

        // Health score: transparent heuristic, based only on Doctor findings.
        int errorCount = doctorIssues.Count(x => x.severity == MessageType.Error);
        int warningCount = doctorIssues.Count(x => x.severity == MessageType.Warning);
        int infoCount = doctorIssues.Count(x => x.severity == MessageType.Info);

        doctorHealthScore = Mathf.Clamp(
            100 - errorCount * 15 - warningCount * 6 - Math.Min(infoCount, 5) * 1,
            0, 100);

        doctorLastScanUtc = DateTime.UtcNow;
    }

    void AddDoctorIssue(
        MessageType severity,
        string category,
        string title,
        string detail,
        UnityEngine.Object target,
        Action fix,
        string fixLabel,
        Action route,
        string routeLabel,
        bool safeFix = false,
        string safeFixDescription = null)
    {
        doctorIssues.Add(new DoctorIssue
        {
            severity = severity,
            category = category,
            title = title,
            detail = detail,
            target = target,
            fix = fix,
            fixLabel = fixLabel,
            route = route,
            routeLabel = routeLabel,
            safeFix = safeFix,
            safeFixDescription = safeFixDescription
        });
    }
}
