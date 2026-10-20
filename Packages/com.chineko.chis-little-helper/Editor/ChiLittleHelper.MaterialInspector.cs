using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public partial class ChiLittleHelperWindow
{
    sealed class MaterialDuplicateGroup
    {
        public string signature;
        public readonly List<Material> materials = new List<Material>();
    }

    readonly List<MaterialDuplicateGroup> materialInspectorDuplicates = new List<MaterialDuplicateGroup>();
    readonly List<MaterialDuplicateGroup> materialInspectorTextureGroups = new List<MaterialDuplicateGroup>();
    readonly List<Material> materialInspectorUnused = new List<Material>();
    readonly Dictionary<Material, List<Renderer>> materialInspectorUsage = new Dictionary<Material, List<Renderer>>();

    bool materialInspectorDirty = true;
    int materialInspectorNullSlots = 0;
    string materialInspectorUnusedFolder = "Assets";
    Shader materialInspectorSourceShader;
    Shader materialInspectorTargetShader;

    void DrawMaterialInspectorPlus()
    {
        BeginCard(
            CardIcon.Sparkle,
            "Material Inspector++",
            "Audit avatar materials, find exact duplicates and shared texture sets, inspect usage, and safely batch-replace shaders.");

        DrawHelpHint("Exact duplicates can be consolidated by renderer reference without deleting material assets. Batch shader replacement edits the material assets themselves.");

        if (avatar == null)
        {
            DrawInfoBox("Select an avatar to inspect its renderer materials.");
            EndCard();
            return;
        }

        BeginResponsiveButtonRow();
        if (GUILayout.Button("Scan Avatar Materials", styleBtnGrad, GUILayout.Height(38), GUILayout.ExpandWidth(true)))
        {
            Run("Scan Avatar Materials", () =>
            {
                ScanMaterialInspector();
                materialInspectorDirty = false;
            });
        }

        ResponsiveButtonGap(5f);

        if (GUILayout.Button("Select Duplicate Materials", styleBtnDark, GUILayout.Height(38), GUILayout.ExpandWidth(true)))
        {
            Selection.objects = materialInspectorDuplicates
                .SelectMany(g => g.materials)
                .Where(m => m != null)
                .Distinct()
                .Cast<UnityEngine.Object>()
                .ToArray();
        }
        EndResponsiveButtonRow();

        GUILayout.Space(8);

        DrawDataFreshnessBanner(
            "Material audit",
            materialInspectorDirty,
            "Material audit matches the current avatar state.",
            () =>
            {
                Run("Scan Avatar Materials", () =>
                {
                    ScanMaterialInspector();
                    materialInspectorDirty = false;
                });
            },
            "Scan Materials");

        if (materialInspectorDirty)
        {
            EndCard();
            return;
        }

        GUILayout.Space(8);

        int uniqueMaterials = materialInspectorUsage.Keys.Count;
        int duplicateAssets = materialInspectorDuplicates.Sum(g => Math.Max(0, g.materials.Count - 1));

        if (IsCompactLayout)
        {
            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("Unique Materials", uniqueMaterials.ToString(), styleCardSubtitle);
            GUILayout.Space(3);
            DrawDoctorStat("Duplicate Assets", duplicateAssets.ToString(), styleCardSubtitle);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("Texture-set Groups", materialInspectorTextureGroups.Count.ToString(), styleCardSubtitle);
            GUILayout.Space(3);
            DrawDoctorStat("Null Slots", materialInspectorNullSlots.ToString(), styleCardSubtitle);
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("Unique Materials", uniqueMaterials.ToString(), styleCardSubtitle);
            GUILayout.Space(4);
            DrawDoctorStat("Duplicate Assets", duplicateAssets.ToString(), styleCardSubtitle);
            GUILayout.Space(4);
            DrawDoctorStat("Texture-set Groups", materialInspectorTextureGroups.Count.ToString(), styleCardSubtitle);
            GUILayout.Space(4);
            DrawDoctorStat("Null Slots", materialInspectorNullSlots.ToString(), styleCardSubtitle);
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        DrawMaterialDuplicateGroups();

        GUILayout.Space(10);
        DrawMaterialUnusedScanner();

        GUILayout.Space(10);
        DrawMaterialBatchShader();

        EndCard();
    }

    void ScanMaterialInspector()
    {
        materialInspectorDuplicates.Clear();
        materialInspectorTextureGroups.Clear();
        materialInspectorUnused.Clear();
        materialInspectorUsage.Clear();
        materialInspectorNullSlots = 0;

        Renderer[] renderers = avatar.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials ?? new Material[0];
            foreach (Material material in materials)
            {
                if (material == null)
                {
                    materialInspectorNullSlots++;
                    continue;
                }

                if (!materialInspectorUsage.TryGetValue(material, out var users))
                {
                    users = new List<Renderer>();
                    materialInspectorUsage.Add(material, users);
                }
                users.Add(renderer);
            }
        }

        BuildMaterialGroups(
            materialInspectorUsage.Keys,
            BuildFullMaterialSignature,
            materialInspectorDuplicates);

        BuildMaterialGroups(
            materialInspectorUsage.Keys,
            BuildTextureSetSignature,
            materialInspectorTextureGroups);
    }

    void BuildMaterialGroups(
        IEnumerable<Material> materials,
        Func<Material, string> signature,
        List<MaterialDuplicateGroup> output)
    {
        output.Clear();

        foreach (var grouping in materials
            .Where(m => m != null)
            .GroupBy(signature)
            .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1))
        {
            var group = new MaterialDuplicateGroup { signature = grouping.Key };
            group.materials.AddRange(grouping.OrderBy(m => AssetDatabase.GetAssetPath(m), StringComparer.OrdinalIgnoreCase));
            output.Add(group);
        }
    }

    string BuildFullMaterialSignature(Material material)
    {
        if (material == null || material.shader == null) return "";

        var sb = new StringBuilder();
        sb.Append(material.shader.name).Append('|');
        sb.Append(material.renderQueue).Append('|');

        string[] keywords = material.shaderKeywords ?? new string[0];
        Array.Sort(keywords, StringComparer.Ordinal);
        sb.Append(string.Join(",", keywords)).Append('|');

        Shader shader = material.shader;
        int count = ShaderUtil.GetPropertyCount(shader);

        for (int i = 0; i < count; i++)
        {
            string property = ShaderUtil.GetPropertyName(shader, i);
            var type = ShaderUtil.GetPropertyType(shader, i);
            sb.Append(property).Append('=');

            try
            {
                if (type == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    Texture tex = material.GetTexture(property);
                    sb.Append(tex != null ? AssetDatabase.GetAssetPath(tex) : "null");
                    sb.Append('@').Append(material.GetTextureScale(property));
                    sb.Append('@').Append(material.GetTextureOffset(property));
                }
                else if (type == ShaderUtil.ShaderPropertyType.Color)
                    sb.Append(material.GetColor(property));
                else if (type == ShaderUtil.ShaderPropertyType.Vector)
                    sb.Append(material.GetVector(property));
                else
                    sb.Append(material.GetFloat(property).ToString("R"));
            }
            catch
            {
                sb.Append("?");
            }

            sb.Append('|');
        }

        return sb.ToString();
    }

    string BuildTextureSetSignature(Material material)
    {
        if (material == null || material.shader == null) return "";

        var textures = new List<string>();
        Shader shader = material.shader;
        int count = ShaderUtil.GetPropertyCount(shader);

        for (int i = 0; i < count; i++)
        {
            if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                continue;

            string property = ShaderUtil.GetPropertyName(shader, i);
            Texture texture = material.GetTexture(property);
            if (texture == null) continue;

            string path = AssetDatabase.GetAssetPath(texture);
            textures.Add(property + "=" + path);
        }

        textures.Sort(StringComparer.Ordinal);
        return material.shader.name + "|" + string.Join("|", textures.ToArray());
    }

    void DrawMaterialDuplicateGroups()
    {
        EditorGUILayout.LabelField("Exact Duplicate Materials", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText }
        });

        if (materialInspectorDuplicates.Count == 0)
        {
            EditorGUILayout.HelpBox("No exact duplicate material assets were found on this avatar.", MessageType.Info);
        }
        else
        {
            foreach (MaterialDuplicateGroup group in materialInspectorDuplicates)
            {
                EditorGUILayout.BeginVertical(styleRowBg);

                Material keep = group.materials[0];
                EditorGUILayout.LabelField(
                    "Keep: " + keep.name,
                    new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = ColTextPrimary } });

                EditorGUILayout.LabelField(
                    string.Join("  •  ", group.materials.Select(m => m.name).ToArray()),
                    styleCardSubtitle);

                GUILayout.Space(4);
                BeginResponsiveButtonRow();

                if (GUILayout.Button("Select Group", styleBtnDark, GUILayout.Height(26), GUILayout.ExpandWidth(true)))
                {
                    Selection.objects = group.materials.Cast<UnityEngine.Object>().ToArray();
                    EditorGUIUtility.PingObject(keep);
                }

                ResponsiveButtonGap();

                if (GUILayout.Button("Use First Everywhere", styleBtnDark, GUILayout.Height(26), GUILayout.ExpandWidth(true)))
                {
                    RunDestructive(
                        "Merge Duplicate Material References",
                        "Replace renderer references to the other exact-duplicate materials with '" + keep.name + "'? Material assets themselves will not be deleted.",
                        () => MergeMaterialDuplicateReferences(group, keep));
                }

                EndResponsiveButtonRow();
                EditorGUILayout.EndVertical();
                GUILayout.Space(4);
            }
        }

        GUILayout.Space(8);

        EditorGUILayout.LabelField("Same Texture Set", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText }
        });

        if (materialInspectorTextureGroups.Count == 0)
        {
            EditorGUILayout.HelpBox("No multiple materials using an identical shader + texture set were found.", MessageType.Info);
        }
        else
        {
            foreach (MaterialDuplicateGroup group in materialInspectorTextureGroups.Take(12))
            {
                EditorGUILayout.BeginHorizontal(styleRowBg);
                EditorGUILayout.LabelField(
                    string.Join(", ", group.materials.Select(m => m.name).ToArray()),
                    styleCardSubtitle);

                if (GUILayout.Button("Select", styleBtnDark, GUILayout.Width(70), GUILayout.Height(24)))
                    Selection.objects = group.materials.Cast<UnityEngine.Object>().ToArray();

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(3);
            }
        }
    }

    void MergeMaterialDuplicateReferences(MaterialDuplicateGroup group, Material keep)
    {
        if (group == null || keep == null) return;

        var replace = new HashSet<Material>(group.materials.Where(m => m != keep));

        foreach (Renderer renderer in avatar.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && replace.Contains(materials[i]))
                {
                    materials[i] = keep;
                    changed = true;
                }
            }

            if (changed)
            {
                Undo.RecordObject(renderer, "Merge Duplicate Material References");
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
        }

        doctorDirty = true;
        materialInspectorDirty = true;
    }

    void DrawMaterialUnusedScanner()
    {
        EditorGUILayout.LabelField("Unused Material Finder", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText }
        });

        EditorGUILayout.LabelField(
            "Scans a chosen Assets folder and lists .mat assets that are not referenced by the current avatar.",
            styleCardSubtitle);

        GUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        materialInspectorUnusedFolder = EditorGUILayout.TextField(
            materialInspectorUnusedFolder,
            styleInputBox);

        GUILayout.Space(4);
        if (GUILayout.Button("Browse", styleBtnDark, GUILayout.Width(72), GUILayout.Height(24)))
        {
            string picked = EditorUtility.OpenFolderPanel(
                "Unused Material Scan Folder",
                AssetFolderToAbsolute(materialInspectorUnusedFolder),
                "");

            if (!string.IsNullOrEmpty(picked))
            {
                string assetPath = AbsoluteFolderToAssetPath(picked);
                if (!string.IsNullOrEmpty(assetPath))
                    materialInspectorUnusedFolder = assetPath;
            }
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        if (GUILayout.Button("Find Unused Materials in Folder", styleBtnDark, GUILayout.Height(28)))
            ScanUnusedMaterials();

        if (materialInspectorUnused.Count > 0)
        {
            EditorGUILayout.HelpBox(
                materialInspectorUnused.Count + " material asset(s) in this folder are not referenced by the current avatar.",
                MessageType.Warning);

            if (GUILayout.Button("Select Unused Materials", styleBtnDark, GUILayout.Height(26)))
                Selection.objects = materialInspectorUnused.Cast<UnityEngine.Object>().ToArray();
        }
    }

    void ScanUnusedMaterials()
    {
        materialInspectorUnused.Clear();

        if (!AssetDatabase.IsValidFolder(materialInspectorUnusedFolder))
            return;

        var used = new HashSet<Material>(materialInspectorUsage.Keys);

        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { materialInspectorUnusedFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && !used.Contains(material))
                materialInspectorUnused.Add(material);
        }
    }

    void DrawMaterialBatchShader()
    {
        EditorGUILayout.LabelField("Batch Shader Replacement", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText }
        });

        EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Source Shader", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
            materialInspectorSourceShader = (Shader)DrawThemedObjectField(materialInspectorSourceShader, typeof(Shader), false);
            EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target Shader", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
            materialInspectorTargetShader = (Shader)DrawThemedObjectField(materialInspectorTargetShader, typeof(Shader), false);
            EditorGUILayout.EndHorizontal();

        int affected = materialInspectorUsage.Keys.Count(m =>
            m != null &&
            materialInspectorSourceShader != null &&
            m.shader == materialInspectorSourceShader);

        using (new EditorGUI.DisabledScope(
            materialInspectorSourceShader == null ||
            materialInspectorTargetShader == null ||
            materialInspectorSourceShader == materialInspectorTargetShader ||
            affected == 0))
        {
            if (GUILayout.Button(
                "Replace Shader on " + affected + " Material" + (affected == 1 ? "" : "s"),
                styleBtnGrad,
                GUILayout.Height(34)))
            {
                RunDestructive(
                    "Batch Replace Material Shader",
                    "Change " + affected + " material asset(s) from '" +
                    materialInspectorSourceShader.name + "' to '" +
                    materialInspectorTargetShader.name +
                    "'? Shader-specific properties may not transfer cleanly.",
                    () => BatchReplaceMaterialShader(materialInspectorSourceShader, materialInspectorTargetShader),
                    false);
            }
        }
    }

    void BatchReplaceMaterialShader(Shader source, Shader target)
    {
        foreach (Material material in materialInspectorUsage.Keys)
        {
            if (material == null || material.shader != source)
                continue;

            Undo.RecordObject(material, "Replace Material Shader");
            material.shader = target;
            EditorUtility.SetDirty(material);
        }

        AssetDatabase.SaveAssets();
        materialInspectorDirty = true;
        doctorDirty = true;
        textureVramDirty = true;
    }
}
