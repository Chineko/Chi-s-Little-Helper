using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class ChiLittleHelperWindow
{
    enum PreflightTarget
    {
        PC,
        Quest
    }

    sealed class PreflightFinding
    {
        public MessageType severity;
        public string area;
        public string title;
        public string detail;
        public Action route;
        public string routeLabel;
    }

    [Serializable]
    sealed class PreflightReportFinding
    {
        public string severity;
        public string area;
        public string title;
        public string detail;
    }

    [Serializable]
    sealed class PreflightReportData
    {
        public string toolkitVersion;
        public string generatedUtc;
        public string avatarName;
        public string target;
        public string result;
        public int blockers;
        public int warnings;
        public int infos;
        public int parameterUsedBits;
        public int parameterRemainingBits;
        public int expressionParameterBits;
        public int estimatedVrcfuryToggleBits;
        public int triangles;
        public long estimatedTextureBytes;
        public int materialSlots;
        public int physBones;
        public int contacts;
        public int constraints;
        public List<PreflightReportFinding> findings = new List<PreflightReportFinding>();
    }

    readonly List<PreflightFinding> preflightFindings = new List<PreflightFinding>();
    bool preflightDirty = true;
    PreflightTarget preflightTarget = PreflightTarget.PC;
    DateTime preflightLastRunUtc;
    int preflightErrors;
    int preflightWarnings;
    int preflightInfos;

    string PreflightStateLabel
    {
        get
        {
            if (preflightDirty) return "STALE";
            if (preflightErrors > 0) return "BLOCKED";
            if (preflightWarnings > 0) return "WARNINGS";
            return "READY";
        }
    }

    void DrawPreUploadCheck()
    {
        BeginCard(
            CardIcon.Warning,
            "Pre-Upload Check",
            "One pass across Doctor, Animator/Parameters, platform compatibility, materials and synced parameter memory.");

        if (avatar == null)
        {
            DrawInfoBox("Select an avatar before running the pre-upload check.");
            EndCard();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Upload Target", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        preflightTarget = (PreflightTarget)EditorGUILayout.Popup(
            (int)preflightTarget,
            Enum.GetNames(typeof(PreflightTarget)),
            stylePopup,
            GUILayout.Height(24));
        EditorGUILayout.EndHorizontal();
        DrawHelpHint("PC ignores Quest-only restrictions. Quest adds mobile shaders/components and mobile performance thresholds.");

        GUILayout.Space(6);

        if (HelpButton(
            "Run Full Pre-Upload Check",
            "Refresh all readiness diagnostics and build a combined upload result.",
            styleBtnGrad,
            GUILayout.Height(38)))
        {
            Run("Run Pre-Upload Check", RunPreUploadCheck);
        }

        GUILayout.Space(5);

        BeginResponsiveButtonRow();
        using (new EditorGUI.DisabledScope(preflightDirty))
        {
            if (HelpButton(
                "Copy Report",
                "Copy a human-readable version of the current report.",
                styleBtnDark,
                GUILayout.Height(30),
                GUILayout.ExpandWidth(true)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildPreflightTextReport();
                ShowNotification(new GUIContent("Pre-upload report copied"));
            }

            ResponsiveButtonGap();

            if (HelpButton(
                "Export TXT",
                "Save a readable text report for sharing or bug reports.",
                styleBtnDark,
                GUILayout.Height(30),
                GUILayout.ExpandWidth(true)))
            {
                ExportPreflightTextReport();
            }

            ResponsiveButtonGap();

            if (HelpButton(
                "Export JSON",
                "Save structured report data for automation or issue tracking.",
                styleBtnDark,
                GUILayout.Height(30),
                GUILayout.ExpandWidth(true)))
            {
                ExportPreflightJsonReport();
            }
        }
        EndResponsiveButtonRow();

        GUILayout.Space(8);

        DrawDataFreshnessBanner(
            "Pre-upload check",
            preflightDirty,
            preflightDirty
                ? "The combined result is stale because the avatar or project changed."
                : "The combined result matches the current cached avatar state.",
            () => Run("Run Pre-Upload Check", RunPreUploadCheck),
            "Run Check");

        if (preflightDirty)
        {
            EndCard();
            return;
        }

        GUILayout.Space(8);

        if (IsCompactLayout)
        {
            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("Result", PreflightStateLabel, styleCardSubtitle);
            GUILayout.Space(3);
            DrawDoctorStat("Blockers", preflightErrors.ToString(), styleCardSubtitle);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("Warnings", preflightWarnings.ToString(), styleCardSubtitle);
            GUILayout.Space(3);
            DrawDoctorStat("Params Left", GetParameterMemoryHomeValue(), styleCardSubtitle);
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("Result", PreflightStateLabel, styleCardSubtitle);
            GUILayout.Space(4);
            DrawDoctorStat("Blockers", preflightErrors.ToString(), styleCardSubtitle);
            GUILayout.Space(4);
            DrawDoctorStat("Warnings", preflightWarnings.ToString(), styleCardSubtitle);
            GUILayout.Space(4);
            DrawDoctorStat("Params Left", GetParameterMemoryHomeValue(), styleCardSubtitle);
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(6);
        EditorGUILayout.LabelField(
            "Last run: " + preflightLastRunUtc.ToLocalTime().ToString("g") +
            " · Target: " + preflightTarget,
            styleCardSubtitle);

        GUILayout.Space(8);

        if (preflightFindings.Count == 0)
        {
            EditorGUILayout.HelpBox("No blockers or warnings were found by the current pre-upload checks.", MessageType.Info);
            EndCard();
            return;
        }

        foreach (var group in preflightFindings
            .OrderByDescending(x => PreflightSeverityRank(x.severity))
            .ThenBy(x => x.area)
            .GroupBy(x => x.area))
        {
            EditorGUILayout.LabelField(group.Key, new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColLavenderText },
                fontSize = 12
            });

            foreach (PreflightFinding finding in group)
            {
                EditorGUILayout.BeginVertical(styleRowBg);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(finding.title, new GUIStyle(EditorStyles.boldLabel)
                {
                    normal =
                    {
                        textColor = finding.severity == MessageType.Error
                            ? DangerRed
                            : finding.severity == MessageType.Warning
                                ? ColGradPink
                                : ColLavenderText
                    }
                });
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    finding.severity == MessageType.Error ? "BLOCKER" :
                    finding.severity == MessageType.Warning ? "WARN" : "INFO",
                    styleCardSubtitle,
                    GUILayout.Width(62));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField(finding.detail, styleInfoText);

                if (finding.route != null)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    string label = string.IsNullOrEmpty(finding.routeLabel)
                        ? "Fix in Tool"
                        : finding.routeLabel;
                    float width = Mathf.Max(118f, styleBtnDark.CalcSize(new GUIContent(label)).x + 24f);
                    if (GUILayout.Button(label, styleBtnDark, GUILayout.Width(width), GUILayout.Height(28)))
                    {
                        finding.route();
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(5);
            }

            GUILayout.Space(4);
        }

        EndCard();
    }

    void RunPreUploadCheck()
    {
        if (avatar == null)
            throw new InvalidOperationException("Select an avatar first.");

        preflightFindings.Clear();

        // Refresh the major caches once, deliberately and explicitly.
        RunDoctorScan();
        doctorDirty = false;

        ScanAnimatorParameters();
        animatorDoctorDirty = false;

        ScanMaterialInspector();
        materialInspectorDirty = false;

        ScanPcQuestCompatibility();
        platformCompareDirty = false;

        RebuildParameterMemoryCache();

        // Doctor findings become routed preflight findings.
        foreach (DoctorIssue issue in doctorIssues)
        {
            AddPreflight(
                issue.severity,
                "Avatar Doctor",
                issue.title,
                issue.detail,
                issue.route,
                !string.IsNullOrEmpty(issue.routeLabel)
                    ? "Fix in " + issue.routeLabel.Replace("Open ", "")
                    : null);
        }

        // Animator / parameter findings.
        foreach (AnimatorParamIssue issue in animatorParamIssues)
        {
            AddPreflight(
                issue.severity,
                "Animator & Parameters",
                issue.title,
                issue.detail,
                () => RouteToTool(4, 13),
                "Fix in Animator Doctor");
        }

        // Parameter memory is an upload blocker only if over budget.
        if (ParameterMemoryRemainingBits < 0)
        {
            AddPreflight(
                MessageType.Error,
                "Parameters",
                "Synced parameter memory exceeds budget",
                GetParameterMemorySummary(),
                () => RouteToTool(4, 13),
                "Fix in Animator Doctor");
        }
        else if (ParameterMemoryRemainingBits <= 16)
        {
            AddPreflight(
                MessageType.Warning,
                "Parameters",
                "Very little synced parameter memory remains",
                GetParameterMemorySummary(),
                () => RouteToTool(4, 13),
                "Review Parameters");
        }

        if (parameterVrcfuryCompressorDetected)
        {
            AddPreflight(
                MessageType.Info,
                "Parameters",
                "VRCFury Parameter Compressor detected",
                "The Home/preflight number is intentionally a conservative pre-compression estimate.",
                () => RouteToTool(0, 16),
                "Review Presets / Setup");
        }

        // Materials.
        if (materialInspectorNullSlots > 0)
        {
            AddPreflight(
                MessageType.Error,
                "Materials",
                "Null material slots",
                materialInspectorNullSlots + " empty renderer material slot(s) were found.",
                () => RouteToTool(2, 15),
                "Fix in Material Inspector++");
        }

        if (materialInspectorDuplicates.Count > 0)
        {
            int duplicateCount = materialInspectorDuplicates.Sum(g => Math.Max(0, g.materials.Count - 1));
            AddPreflight(
                MessageType.Warning,
                "Materials",
                "Exact duplicate materials",
                duplicateCount + " duplicate material asset(s) are referenced by the avatar.",
                () => RouteToTool(2, 15),
                "Fix in Material Inspector++");
        }

        // Platform target.
        if (preflightTarget == PreflightTarget.Quest)
        {
            foreach (PlatformMetric metric in platformMetrics)
            {
                if (metric.questSeverity == MessageType.Error)
                {
                    AddPreflight(
                        MessageType.Error,
                        "Quest",
                        metric.label + " is Very Poor",
                        metric.label + ": " + metric.value + " · Quest rank: " + metric.questStatus,
                        () => RouteToTool(3, 14),
                        "Fix in PC / Quest");
                }
                else if (metric.questSeverity == MessageType.Warning)
                {
                    AddPreflight(
                        MessageType.Warning,
                        "Quest",
                        metric.label + " needs review",
                        metric.label + ": " + metric.value + " · Quest rank: " + metric.questStatus,
                        () => RouteToTool(3, 14),
                        "Review PC / Quest");
                }
            }

            foreach (string warning in platformQuestWarnings)
            {
                AddPreflight(
                    MessageType.Warning,
                    "Quest",
                    "Quest compatibility warning",
                    warning,
                    () => RouteToTool(3, 14),
                    "Fix in PC / Quest");
            }
        }

        preflightErrors = preflightFindings.Count(x => x.severity == MessageType.Error);
        preflightWarnings = preflightFindings.Count(x => x.severity == MessageType.Warning);
        preflightInfos = preflightFindings.Count(x => x.severity == MessageType.Info);
        preflightLastRunUtc = DateTime.UtcNow;
        preflightDirty = false;

        ShowNotification(new GUIContent(
            preflightErrors > 0 ? "Pre-upload check: BLOCKED" :
            preflightWarnings > 0 ? "Pre-upload check: warnings" :
            "Pre-upload check: READY"));
    }


    PreflightReportData BuildPreflightReportData()
    {
        var data = new PreflightReportData
        {
            toolkitVersion = TOOL_VERSION,
            generatedUtc = DateTime.UtcNow.ToString("o"),
            avatarName = avatar != null ? avatar.gameObject.name : "None",
            target = preflightTarget.ToString(),
            result = PreflightStateLabel,
            blockers = preflightErrors,
            warnings = preflightWarnings,
            infos = preflightInfos,
            parameterUsedBits = ParameterMemoryUsedBits,
            parameterRemainingBits = ParameterMemoryRemainingBits,
            expressionParameterBits = parameterExpressionBits,
            estimatedVrcfuryToggleBits = parameterVrcfuryToggleBits,
            triangles = doctorTriangles,
            estimatedTextureBytes = doctorTextureBytes,
            materialSlots = doctorMaterialSlots,
            physBones = doctorPhysBones,
            contacts = doctorContacts,
            constraints = doctorConstraints
        };

        foreach (PreflightFinding finding in preflightFindings)
        {
            data.findings.Add(new PreflightReportFinding
            {
                severity = finding.severity.ToString(),
                area = finding.area,
                title = finding.title,
                detail = finding.detail
            });
        }

        return data;
    }

    string BuildPreflightTextReport()
    {
        PreflightReportData data = BuildPreflightReportData();

        var lines = new List<string>
        {
            "=== Chi's Little Helper — Pre-Upload Check ===",
            "Toolkit: " + data.toolkitVersion,
            "Generated: " + DateTime.Parse(data.generatedUtc).ToLocalTime().ToString("g"),
            "Avatar: " + data.avatarName,
            "Target: " + data.target,
            "Result: " + data.result,
            "",
            "Blockers: " + data.blockers,
            "Warnings: " + data.warnings,
            "Info: " + data.infos,
            "",
            "Parameters: " + data.parameterUsedBits + " / 256 bits (" + data.parameterRemainingBits + " left)",
            "  Expression Parameters: " + data.expressionParameterBits + " bits",
            "  Estimated VRCFury Toggles: +" + data.estimatedVrcfuryToggleBits + " bits",
            "Triangles: " + data.triangles.ToString("N0"),
            "Estimated Texture Memory: " + FormatBytes(data.estimatedTextureBytes),
            "Material Slots: " + data.materialSlots,
            "PhysBones: " + data.physBones,
            "Contacts: " + data.contacts,
            "Constraints: " + data.constraints,
            "",
            "Findings"
        };

        if (data.findings.Count == 0)
        {
            lines.Add("No blockers or warnings were found.");
        }
        else
        {
            foreach (PreflightReportFinding finding in data.findings)
            {
                lines.Add("");
                lines.Add("[" + finding.severity.ToUpperInvariant() + "] [" + finding.area + "] " + finding.title);
                lines.Add(finding.detail);
            }
        }

        lines.Add("");
        lines.Add("Notes:");
        lines.Add("- Parameter memory includes synced Expression Parameters plus the toolkit's VRCFury Toggle build estimate.");
        lines.Add("- Quest checks are a preflight assistant; the installed VRChat SDK Build panel remains authoritative.");
        lines.Add("- Toolkit Doctor thresholds are local diagnostics unless a result explicitly states it is a VRChat rank.");

        return string.Join("\n", lines);
    }

    void ExportPreflightTextReport()
    {
        string path = EditorUtility.SaveFilePanel(
            "Export Pre-Upload Report",
            "",
            (avatar != null ? avatar.gameObject.name : "Avatar") + "_PreUpload_" + preflightTarget + ".txt",
            "txt");

        if (string.IsNullOrEmpty(path))
            return;

        System.IO.File.WriteAllText(path, BuildPreflightTextReport());
        ShowNotification(new GUIContent("Pre-upload TXT exported"));
    }

    void ExportPreflightJsonReport()
    {
        string path = EditorUtility.SaveFilePanel(
            "Export Pre-Upload JSON",
            "",
            (avatar != null ? avatar.gameObject.name : "Avatar") + "_PreUpload_" + preflightTarget + ".json",
            "json");

        if (string.IsNullOrEmpty(path))
            return;

        System.IO.File.WriteAllText(
            path,
            JsonUtility.ToJson(BuildPreflightReportData(), true));

        ShowNotification(new GUIContent("Pre-upload JSON exported"));
    }

    int PreflightSeverityRank(MessageType severity)
    {
        if (severity == MessageType.Error) return 3;
        if (severity == MessageType.Warning) return 2;
        if (severity == MessageType.Info) return 1;
        return 0;
    }

    void AddPreflight(
        MessageType severity,
        string area,
        string title,
        string detail,
        Action route = null,
        string routeLabel = null)
    {
        preflightFindings.Add(new PreflightFinding
        {
            severity = severity,
            area = area,
            title = title,
            detail = detail,
            route = route,
            routeLabel = routeLabel
        });
    }

    string GetHomePreflightStatus()
    {
        if (preflightDirty) return "STALE";
        if (preflightErrors > 0) return "BLOCKED";
        if (preflightWarnings > 0) return "WARN";
        return "READY";
    }

    MessageType GetHomePreflightSeverity()
    {
        if (preflightDirty) return MessageType.None;
        if (preflightErrors > 0) return MessageType.Error;
        if (preflightWarnings > 0) return MessageType.Warning;
        return MessageType.Info;
    }
}
