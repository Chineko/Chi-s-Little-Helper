using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

public partial class ChiLittleHelperWindow
{
    sealed class PlatformMetric
    {
        public string label;
        public string value;
        public string pcStatus;
        public string questStatus;
        public MessageType questSeverity;
    }

    readonly List<PlatformMetric> platformMetrics = new List<PlatformMetric>();
    readonly List<string> platformQuestWarnings = new List<string>();
    bool platformCompareDirty = true;

    int platformTriangles;
    long platformTextureBytes;
    int platformSkinnedMeshes;
    int platformBasicMeshes;
    int platformMaterialSlots;
    int platformAnimators;
    int platformBones;
    int platformPhysBones;
    int platformPhysBoneTransforms;
    int platformPhysBoneColliders;
    int platformContacts;
    int platformConstraints;
    int platformParticles;

    void DrawPcQuestComparator()
    {
        BeginCard(
            CardIcon.Overview,
            "PC / Quest Compatibility Comparator",
            "Compare the current avatar against PC-friendly guidance and VRChat mobile/Quest performance and compatibility limits.");

        DrawHelpHint("Quest/mobile results are a preflight assistant. The VRChat SDK Build panel remains authoritative after SDK updates.");

        if (avatar == null)
        {
            DrawInfoBox("Select an avatar before running the platform comparison.");
            EndCard();
            return;
        }

        EditorGUILayout.HelpBox(
            "Quest checks use VRChat's current mobile performance thresholds embedded in this beta. Treat this as a preflight assistant; the VRChat SDK Build panel remains authoritative after SDK updates.",
            MessageType.Info);

        GUILayout.Space(6);

        if (GUILayout.Button("Analyze PC / Quest Compatibility", styleBtnGrad, GUILayout.Height(38)))
        {
            Run("Analyze PC / Quest Compatibility", () =>
            {
                ScanPcQuestCompatibility();
                platformCompareDirty = false;
            });
        }

        GUILayout.Space(8);

        DrawDataFreshnessBanner(
            "Platform comparison",
            platformCompareDirty,
            "Platform compatibility results match the current avatar state.",
            () =>
            {
                Run("Analyze PC / Quest Compatibility", () =>
                {
                    ScanPcQuestCompatibility();
                    platformCompareDirty = false;
                });
            },
            "Analyze");

        if (platformCompareDirty)
        {
            EndCard();
            return;
        }

        GUILayout.Space(8);

        if (IsCompactLayout)
        {
            EditorGUILayout.BeginHorizontal();
            DrawPlatformStat("Triangles", platformTriangles.ToString("N0"));
            GUILayout.Space(3);
            DrawPlatformStat("Texture VRAM", FormatBytes(platformTextureBytes));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            DrawPlatformStat("Materials", platformMaterialSlots.ToString());
            GUILayout.Space(3);
            DrawPlatformStat("Constraints", platformConstraints.ToString());
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            DrawPlatformStat("Triangles", platformTriangles.ToString("N0"));
            GUILayout.Space(4);
            DrawPlatformStat("Texture VRAM", FormatBytes(platformTextureBytes));
            GUILayout.Space(4);
            DrawPlatformStat("Materials", platformMaterialSlots.ToString());
            GUILayout.Space(4);
            DrawPlatformStat("Constraints", platformConstraints.ToString());
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        var headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = ColLavenderText }
        };

        var metricNameStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = ColTextPrimary }
        };

        var metricValueStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = ColTextMuted }
        };

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Metric", headerStyle, GUILayout.Width(155));
        GUILayout.Label("Current", headerStyle, GUILayout.Width(95));
        GUILayout.Label("PC", headerStyle, GUILayout.Width(125));
        GUILayout.Label("Quest / Mobile", headerStyle);
        EditorGUILayout.EndHorizontal();

        foreach (PlatformMetric metric in platformMetrics)
        {
            EditorGUILayout.BeginHorizontal(styleRowBg);

            GUILayout.Label(metric.label, metricNameStyle, GUILayout.Width(155));
            GUILayout.Label(metric.value, metricValueStyle, GUILayout.Width(95));
            GUILayout.Label(metric.pcStatus, metricValueStyle, GUILayout.Width(125));

            var questStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal =
                {
                    textColor = metric.questSeverity == MessageType.Error
                        ? DangerRed
                        : metric.questSeverity == MessageType.Warning
                            ? ColGradPink
                            : ColLavenderText
                }
            };

            GUILayout.Label(metric.questStatus, questStyle);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }

        if (platformQuestWarnings.Count > 0)
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField("Quest Compatibility Warnings", new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColLavenderText }
            });

            foreach (string warning in platformQuestWarnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        EndCard();
    }

    void DrawPlatformStat(string label, string value)
    {
        var boxStyle = new GUIStyle
        {
            normal = { background = texInfoBox },
            border = new RectOffset(8, 8, 8, 8),
            padding = new RectOffset(6, 6, 7, 7)
        };

        var valueStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            normal = { textColor = ColTextPrimary }
        };

        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            normal = { textColor = ColTextMuted }
        };

        EditorGUILayout.BeginVertical(boxStyle, GUILayout.MinWidth(88), GUILayout.Height(58));
        GUILayout.FlexibleSpace();
        GUILayout.Label(label, new GUIStyle(labelStyle)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 10
        }, GUILayout.Height(16));
        GUILayout.Label(value, valueStyle, GUILayout.Height(19));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();
    }

    void ScanPcQuestCompatibility()
    {
        platformMetrics.Clear();
        platformQuestWarnings.Clear();

        var renderers = avatar.GetComponentsInChildren<Renderer>(true);
        var skinned = renderers.OfType<SkinnedMeshRenderer>().ToArray();
        var meshes = renderers
            .Where(r => !(r is SkinnedMeshRenderer))
            .Select(r => r.GetComponent<MeshFilter>())
            .Where(mf => mf != null && mf.sharedMesh != null)
            .ToArray();

        platformTriangles = 0;
        foreach (SkinnedMeshRenderer smr in skinned)
        {
            if (smr.sharedMesh != null)
                platformTriangles += CountMeshTriangles(smr.sharedMesh);
        }
        foreach (MeshFilter mf in meshes)
            platformTriangles += CountMeshTriangles(mf.sharedMesh);

        platformSkinnedMeshes = skinned.Length;
        platformBasicMeshes = meshes.Length;
        platformMaterialSlots = renderers.Sum(r => r.sharedMaterials != null ? r.sharedMaterials.Length : 0);
        platformAnimators = avatar.GetComponentsInChildren<Animator>(true).Length;

        var boneSet = new HashSet<Transform>();
        foreach (SkinnedMeshRenderer smr in skinned)
        {
            if (smr.bones == null) continue;
            foreach (Transform bone in smr.bones)
                if (bone != null) boneSet.Add(bone);
        }
        platformBones = boneSet.Count;

        if (textureVramDirty)
        {
            RebuildTextureVramCache();
            textureVramDirty = false;
        }
        platformTextureBytes = textureTotalBytes;

        Type pbType = FindType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone");
        Component[] physBones = pbType != null
            ? avatar.GetComponentsInChildren(pbType, true).Cast<Component>().ToArray()
            : new Component[0];

        platformPhysBones = physBones.Length;
        platformPhysBoneTransforms = physBones.Sum(CountPhysBoneTransforms);
        platformPhysBoneColliders = physBones.Sum(CountPhysBoneColliders);

        Type sender = FindType(T_CONTACT_SENDER);
        Type receiver = FindType(T_CONTACT_RECEIVER);
        platformContacts =
            (sender != null ? avatar.GetComponentsInChildren(sender, true).Length : 0) +
            (receiver != null ? avatar.GetComponentsInChildren(receiver, true).Length : 0);

        platformConstraints = GetAvatarConstraints().Count;
        platformParticles = avatar.GetComponentsInChildren<ParticleSystem>(true).Length;

        AddPlatformMetric("Triangles", platformTriangles.ToString("N0"),
            platformTriangles <= doctorTriangleThreshold ? "Within local target" : "High",
            MobileRankText(platformTriangles, 7500, 10000, 15000, 20000),
            MobileSeverity(platformTriangles, 10000, 20000));

        AddPlatformMetric("Texture Memory", FormatBytes(platformTextureBytes),
            platformTextureBytes <= doctorTextureMemoryThresholdMb * 1024L * 1024L ? "Within local target" : "High",
            MobileRankTextBytes(platformTextureBytes, 10, 18, 25, 40),
            MobileSeverityBytes(platformTextureBytes, 18, 40));

        AddPlatformMetric("Skinned Meshes", platformSkinnedMeshes.ToString(),
            platformSkinnedMeshes <= 8 ? "Reasonable" : "High",
            MobileRankText(platformSkinnedMeshes, 1, 1, 2, 2),
            MobileSeverity(platformSkinnedMeshes, 1, 2));

        AddPlatformMetric("Basic Meshes", platformBasicMeshes.ToString(),
            platformBasicMeshes <= 8 ? "Reasonable" : "High",
            MobileRankText(platformBasicMeshes, 1, 1, 2, 2),
            MobileSeverity(platformBasicMeshes, 1, 2));

        AddPlatformMetric("Material Slots", platformMaterialSlots.ToString(),
            platformMaterialSlots <= doctorMaterialSlotThreshold ? "Within local target" : "High",
            MobileRankText(platformMaterialSlots, 1, 1, 2, 4),
            MobileSeverity(platformMaterialSlots, 1, 4));

        AddPlatformMetric("Animators", platformAnimators.ToString(),
            platformAnimators <= 2 ? "Reasonable" : "High",
            MobileRankText(platformAnimators, 1, 1, 1, 2),
            MobileSeverity(platformAnimators, 1, 2));

        AddPlatformMetric("Bones", platformBones.ToString(),
            platformBones <= 256 ? "Reasonable" : "High",
            MobileRankText(platformBones, 75, 90, 150, 150),
            MobileSeverity(platformBones, 90, 150));

        AddPlatformMetric("PhysBones", platformPhysBones.ToString(),
            platformPhysBones <= doctorPhysBoneThreshold ? "Within local target" : "High",
            MobileRankText(platformPhysBones, 0, 4, 6, 8),
            platformPhysBones > 8 ? MessageType.Error : platformPhysBones > 4 ? MessageType.Warning : MessageType.Info);

        AddPlatformMetric("PB Transforms", platformPhysBoneTransforms.ToString(),
            "Advisory",
            MobileRankText(platformPhysBoneTransforms, 0, 16, 32, 64),
            platformPhysBoneTransforms > 64 ? MessageType.Error : platformPhysBoneTransforms > 16 ? MessageType.Warning : MessageType.Info);

        AddPlatformMetric("PB Colliders", platformPhysBoneColliders.ToString(),
            "Advisory",
            MobileRankText(platformPhysBoneColliders, 0, 4, 8, 16),
            platformPhysBoneColliders > 16 ? MessageType.Error : platformPhysBoneColliders > 4 ? MessageType.Warning : MessageType.Info);

        AddPlatformMetric("Contacts", platformContacts.ToString(),
            "Supported",
            MobileRankText(platformContacts, 2, 4, 8, 16),
            platformContacts > 16 ? MessageType.Error : platformContacts > 4 ? MessageType.Warning : MessageType.Info);

        AddPlatformMetric("Constraints", platformConstraints.ToString(),
            "Supported",
            MobileRankText(platformConstraints, 30, 60, 120, 150),
            platformConstraints > 150 ? MessageType.Error : platformConstraints > 60 ? MessageType.Warning : MessageType.Info);

        AddPlatformMetric("Particles", platformParticles.ToString(),
            platformParticles == 0 ? "None" : "Supported with limits",
            MobileRankText(platformParticles, 0, 0, 0, 2),
            platformParticles > 2 ? MessageType.Error : platformParticles > 0 ? MessageType.Warning : MessageType.Info);

        ScanQuestUnsupportedComponents();
        ScanQuestShaders(renderers);
    }

    int CountMeshTriangles(Mesh mesh)
    {
        if (mesh == null) return 0;
        long indices = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
            indices += (long)mesh.GetIndexCount(i);
        return (int)Math.Min(int.MaxValue, indices / 3L);
    }

    void ScanQuestUnsupportedComponents()
    {
        AddQuestUnsupportedIfAny<Cloth>("Cloth is disabled on VRChat mobile avatars.");
        AddQuestUnsupportedIfAny<Light>("Lights are disabled on VRChat mobile avatars.");
        AddQuestUnsupportedIfAny<Camera>("Cameras are disabled on VRChat mobile avatars.");
        AddQuestUnsupportedIfAny<AudioSource>("Audio Sources are disabled on VRChat mobile avatars.");
        AddQuestUnsupportedIfAny<Rigidbody>("Rigidbodies are disabled on VRChat mobile avatars.");

        int unityConstraints = GetAvatarConstraints().Count;
        if (unityConstraints > 0)
            platformQuestWarnings.Add($"{unityConstraints} Unity constraint(s) found. VRChat mobile disables Unity constraints; convert them to VRChat Constraints.");
    }

    void AddQuestUnsupportedIfAny<T>(string detail) where T : Component
    {
        int count = avatar.GetComponentsInChildren<T>(true).Length;
        if (count > 0)
            platformQuestWarnings.Add(count + " " + typeof(T).Name + " component(s): " + detail);
    }

    void ScanQuestShaders(Renderer[] renderers)
    {
        var badShaders = new HashSet<string>(StringComparer.Ordinal);

        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || material.shader == null) continue;

                string shader = material.shader.name ?? "";
                if (!shader.StartsWith("VRChat/Mobile", StringComparison.OrdinalIgnoreCase))
                    badShaders.Add(shader);
            }
        }

        if (badShaders.Count > 0)
        {
            string preview = string.Join(", ", badShaders.Take(6).ToArray());
            if (badShaders.Count > 6) preview += " …";

            platformQuestWarnings.Add(
                badShaders.Count + " non-mobile shader(s) detected: " + preview +
                ". Quest/mobile avatars should use VRChat/Mobile shaders from the SDK.");
        }
    }

    void AddPlatformMetric(string label, string value, string pc, string quest, MessageType questSeverity)
    {
        platformMetrics.Add(new PlatformMetric
        {
            label = label,
            value = value,
            pcStatus = pc,
            questStatus = quest,
            questSeverity = questSeverity
        });
    }

    string MobileRankText(long value, long excellent, long good, long medium, long poor)
    {
        if (value <= excellent) return "Excellent";
        if (value <= good) return "Good";
        if (value <= medium) return "Medium";
        if (value <= poor) return "Poor";
        return "Very Poor";
    }

    string MobileRankTextBytes(long bytes, long excellentMb, long goodMb, long mediumMb, long poorMb)
    {
        long mb = 1024L * 1024L;
        if (bytes <= excellentMb * mb) return "Excellent";
        if (bytes <= goodMb * mb) return "Good";
        if (bytes <= mediumMb * mb) return "Medium";
        if (bytes <= poorMb * mb) return "Poor";
        return "Very Poor";
    }

    MessageType MobileSeverity(long value, long good, long poor)
    {
        if (value > poor) return MessageType.Error;
        if (value > good) return MessageType.Warning;
        return MessageType.Info;
    }

    MessageType MobileSeverityBytes(long bytes, long goodMb, long poorMb)
    {
        long mb = 1024L * 1024L;
        if (bytes > poorMb * mb) return MessageType.Error;
        if (bytes > goodMb * mb) return MessageType.Warning;
        return MessageType.Info;
    }
}
