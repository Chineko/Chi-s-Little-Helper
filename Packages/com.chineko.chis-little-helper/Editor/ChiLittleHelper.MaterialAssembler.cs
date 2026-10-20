using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class ChiLittleHelperWindow
{
    enum MaterialAssemblerShaderPreset
    {
        Standard,
        Poiyomi,
        LilToon,
        Custom
    }

    enum MaterialAssemblerMapType
    {
        BaseColor,
        Normal,
        Metallic,
        Roughness,
        Smoothness,
        AO,
        Emission,
        Mask
    }

    sealed class MaterialAssemblerSet
    {
        public string baseName;
        public readonly Dictionary<MaterialAssemblerMapType, Texture2D> maps =
            new Dictionary<MaterialAssemblerMapType, Texture2D>();

        public readonly Dictionary<MaterialAssemblerMapType, List<Texture2D>> duplicates =
            new Dictionary<MaterialAssemblerMapType, List<Texture2D>>();

        public bool HasDuplicates => duplicates.Count > 0;
    }

    sealed class MaterialAssemblerImporterBackup
    {
        public string path;
        public TextureImporterType textureType;
    }

    [SerializeField] string assemblerTextureFolder = "";
    [SerializeField] string assemblerOutputFolder = "Assets/Materials/Assembled";
    [SerializeField] bool assemblerIncludeSubfolders = true;
    [SerializeField] bool assemblerSetNormalImportType = true;
    [SerializeField] MaterialAssemblerShaderPreset assemblerShaderPreset = MaterialAssemblerShaderPreset.LilToon;
    [SerializeField] Shader assemblerCustomShader;
    [SerializeField] bool assemblerShowNamingRules = false;

    [SerializeField] string assemblerBaseAliases = "BaseColor, Albedo, Diffuse, Base, Color, BaseColour";
    [SerializeField] string assemblerNormalAliases = "Normal, NormalMap, Normals, NRM, Norm";
    [SerializeField] string assemblerMetallicAliases = "Metallic, Metalness, Metal, MTL";
    [SerializeField] string assemblerRoughnessAliases = "Roughness, Rough, RGH";
    [SerializeField] string assemblerSmoothnessAliases = "Smoothness, Smooth, Gloss, Glossiness";
    [SerializeField] string assemblerAoAliases = "AO, AmbientOcclusion, Occlusion";
    [SerializeField] string assemblerEmissionAliases = "Emission, Emissive, Emit";
    [SerializeField] string assemblerMaskAliases = "Mask, MaskMap, Packed, ORM, RMA, MRA";

    [SerializeField] string assemblerCustomBaseProperty = "_MainTex";
    [SerializeField] string assemblerCustomNormalProperty = "_BumpMap";
    [SerializeField] string assemblerCustomMetallicProperty = "_MetallicGlossMap";
    [SerializeField] string assemblerCustomRoughnessProperty = "_RoughnessMap";
    [SerializeField] string assemblerCustomSmoothnessProperty = "_SmoothnessTex";
    [SerializeField] string assemblerCustomAoProperty = "_OcclusionMap";
    [SerializeField] string assemblerCustomEmissionProperty = "_EmissionMap";
    [SerializeField] string assemblerCustomMaskProperty = "_MaskMap";

    Vector2 assemblerPreviewScroll;
    readonly List<MaterialAssemblerSet> assemblerSets = new List<MaterialAssemblerSet>();
    readonly List<string> assemblerScanWarnings = new List<string>();
    readonly List<string> assemblerUnmatched = new List<string>();

    Shader assemblerResolvedShader;
    MaterialAssemblerShaderPreset assemblerResolvedPreset;
    bool assemblerShaderResolutionValid = false;
    bool assemblerPreviewValid = false;

    void DrawMaterialAssembler()
    {
        BeginCard(
            CardIcon.Sparkle,
            "Bulk Material Assembler",
            "Scan a texture folder, group matching PBR maps, preview the result, then create materials automatically.");

        DrawInfoBox(
            "Nothing is created during Scan. The preview shows exactly which texture sets will become materials, including duplicates, missing Base Color maps, unsupported shader slots and existing .mat collisions.");

        GUILayout.Space(8);

        DrawAssemblerFolderField(
            "Texture Folder",
            ref assemblerTextureFolder,
            "Choose Texture Folder",
            () =>
            {
                assemblerPreviewValid = false;
            });

        DrawAssemblerFolderField(
            "Output Folder",
            ref assemblerOutputFolder,
            "Choose Material Output Folder",
            () =>
            {
                assemblerPreviewValid = false;
            });

        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Include Subfolders", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        bool includeSubfolders = EditorGUILayout.Toggle(assemblerIncludeSubfolders, GUILayout.Width(20));
        if (includeSubfolders != assemblerIncludeSubfolders)
        {
            assemblerIncludeSubfolders = includeSubfolders;
            assemblerPreviewValid = false;
        }

        GUILayout.Space(20);
        EditorGUILayout.LabelField("Normal Import Fix", styleFieldLabel, GUILayout.Width(118));
        assemblerSetNormalImportType = EditorGUILayout.Toggle(assemblerSetNormalImportType, GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Shader Preset", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        var preset = (MaterialAssemblerShaderPreset)EditorGUILayout.Popup(
            (int)assemblerShaderPreset,
            Enum.GetNames(typeof(MaterialAssemblerShaderPreset)),
            stylePopup,
            GUILayout.Height(24));
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck())
        {
            assemblerShaderPreset = preset;
            assemblerShaderResolutionValid = false;
            assemblerPreviewValid = false;
        }

        if (assemblerShaderPreset == MaterialAssemblerShaderPreset.Custom)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Custom Shader", styleFieldLabel, GUILayout.Width(FieldLabelWidth));

            assemblerCustomShader = (Shader)DrawThemedObjectField(
                assemblerCustomShader,
                typeof(Shader),
                false,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(24));

            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                assemblerShaderResolutionValid = false;
                assemblerPreviewValid = false;
            }
        }

        Shader shader = ResolveAssemblerShader();
        if (shader != null)
        {
            EditorGUILayout.HelpBox(
                "Material shader: " + shader.name,
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                assemblerShaderPreset + " shader was not found. Install/select the shader or choose Custom before creating materials.",
                MessageType.Warning);
        }

        GUILayout.Space(6);

        assemblerShowNamingRules = EditorGUILayout.Foldout(
            assemblerShowNamingRules,
            "Advanced Naming Rules",
            true);

        if (assemblerShowNamingRules)
        {
            EditorGUILayout.BeginVertical(styleRowBg);
            EditorGUILayout.LabelField(
                "Comma-separated filename suffixes. A separator such as _, -, space or . is expected before each suffix.",
                styleCardSubtitle);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Base Color", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerBaseAliases = EditorGUILayout.TextField(assemblerBaseAliases, styleInputBox);
        EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Normal", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerNormalAliases = EditorGUILayout.TextField(assemblerNormalAliases, styleInputBox);
        EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Metallic", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerMetallicAliases = EditorGUILayout.TextField(assemblerMetallicAliases, styleInputBox);
        EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Roughness", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerRoughnessAliases = EditorGUILayout.TextField(assemblerRoughnessAliases, styleInputBox);
        EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Smoothness", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerSmoothnessAliases = EditorGUILayout.TextField(assemblerSmoothnessAliases, styleInputBox);
        EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("AO", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerAoAliases = EditorGUILayout.TextField(assemblerAoAliases, styleInputBox);
        EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Emission", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerEmissionAliases = EditorGUILayout.TextField(assemblerEmissionAliases, styleInputBox);
        EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Mask / Packed", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerMaskAliases = EditorGUILayout.TextField(assemblerMaskAliases, styleInputBox);
        EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
                assemblerPreviewValid = false;

            if (assemblerShaderPreset == MaterialAssemblerShaderPreset.Custom)
            {
                GUILayout.Space(8);
                EditorGUILayout.LabelField("Custom Shader Texture Properties", new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = ColLavenderText }
                });

                EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Base Color", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerCustomBaseProperty = EditorGUILayout.TextField(assemblerCustomBaseProperty, styleInputBox);
        EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Normal", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerCustomNormalProperty = EditorGUILayout.TextField(assemblerCustomNormalProperty, styleInputBox);
        EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Metallic", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerCustomMetallicProperty = EditorGUILayout.TextField(assemblerCustomMetallicProperty, styleInputBox);
        EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Roughness", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerCustomRoughnessProperty = EditorGUILayout.TextField(assemblerCustomRoughnessProperty, styleInputBox);
        EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Smoothness", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerCustomSmoothnessProperty = EditorGUILayout.TextField(assemblerCustomSmoothnessProperty, styleInputBox);
        EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("AO", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerCustomAoProperty = EditorGUILayout.TextField(assemblerCustomAoProperty, styleInputBox);
        EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Emission", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerCustomEmissionProperty = EditorGUILayout.TextField(assemblerCustomEmissionProperty, styleInputBox);
        EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Mask", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        assemblerCustomMaskProperty = EditorGUILayout.TextField(assemblerCustomMaskProperty, styleInputBox);
        EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(10);

        BeginResponsiveButtonRow();
        using (new EditorGUI.DisabledScope(!AssetDatabase.IsValidFolder(assemblerTextureFolder)))
        {
            if (GUILayout.Button("Scan Folder / Preview", styleBtnGrad, GUILayout.Height(38), GUILayout.ExpandWidth(true)))
                Run("Scan Material Textures", ScanMaterialAssemblerFolder);
        }

        ResponsiveButtonGap(6f);

        if (GUILayout.Button("Refresh Shader Detection", styleBtnDark, ResponsiveButtonWidth(168), GUILayout.Height(38)))
        {
            assemblerShaderResolutionValid = false;
            assemblerPreviewValid = false;
            ResolveAssemblerShader();
        }
        EndResponsiveButtonRow();

        if (!AssetDatabase.IsValidFolder(assemblerTextureFolder) && !string.IsNullOrEmpty(assemblerTextureFolder))
            EditorGUILayout.HelpBox("Texture Folder must be a folder inside this Unity project's Assets folder.", MessageType.Warning);

        GUILayout.Space(10);

        if (!assemblerPreviewValid)
        {
            EditorGUILayout.HelpBox(
                "Preview is not current. Choose a texture folder and click Scan Folder / Preview.",
                MessageType.Info);
            EndCard();
            return;
        }

        DrawMaterialAssemblerPreview(shader);

        int createCount = assemblerSets.Count(set =>
            !set.HasDuplicates && !AssemblerMaterialExists(set));

        GUILayout.Space(10);

        using (new EditorGUI.DisabledScope(
            shader == null ||
            createCount == 0 ||
            !IsValidAssemblerOutputPath()))
        {
            if (GUILayout.Button(
                "Create " + createCount + " Material" + (createCount == 1 ? "" : "s"),
                styleBtnGrad,
                GUILayout.Height(42)))
            {
                RunDestructive(
                    "Create Assembled Materials",
                    BuildAssemblerConfirmation(createCount),
                    CreateMaterialAssemblerMaterials,
                    false);
            }
        }

        if (!IsValidAssemblerOutputPath())
            EditorGUILayout.HelpBox("Output Folder must be inside Assets.", MessageType.Warning);

        EndCard();
    }

    void DrawAssemblerFolderField(string label, ref string path, string dialogTitle, Action changed)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, styleFieldLabel, GUILayout.Width(FieldLabelWidth));

        EditorGUI.BeginChangeCheck();
        string next = EditorGUILayout.TextField(path, styleInputBox);
        if (EditorGUI.EndChangeCheck())
        {
            path = NormalizeAssetFolderPath(next);
            changed?.Invoke();
        }

        GUILayout.Space(4);
        if (GUILayout.Button("Browse", styleBtnDark, GUILayout.Width(72), GUILayout.Height(24)))
        {
            string start = AssetFolderToAbsolute(path);
            string picked = EditorUtility.OpenFolderPanel(dialogTitle, start, "");
            if (!string.IsNullOrEmpty(picked))
            {
                string assetPath = AbsoluteFolderToAssetPath(picked);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    path = assetPath;
                    changed?.Invoke();
                    GUI.FocusControl(null);
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Folder Outside Assets",
                        "Please choose a folder inside this Unity project's Assets folder.",
                        "OK");
                }
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    void ScanMaterialAssemblerFolder()
    {
        assemblerSets.Clear();
        assemblerScanWarnings.Clear();
        assemblerUnmatched.Clear();

        assemblerTextureFolder = NormalizeAssetFolderPath(assemblerTextureFolder);
        assemblerOutputFolder = NormalizeAssetFolderPath(assemblerOutputFolder);

        if (!AssetDatabase.IsValidFolder(assemblerTextureFolder))
            throw new InvalidOperationException("Texture Folder is not a valid Assets folder.");

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { assemblerTextureFolder });
        var groups = new Dictionary<string, MaterialAssemblerSet>(StringComparer.OrdinalIgnoreCase);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;

            if (!assemblerIncludeSubfolders)
            {
                string dir = NormalizeAssetFolderPath(Path.GetDirectoryName(path));
                if (!string.Equals(dir, assemblerTextureFolder, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) continue;

            string fileName = Path.GetFileNameWithoutExtension(path);

            if (!TryClassifyAssemblerTexture(fileName, out string baseName, out MaterialAssemblerMapType mapType, out string problem))
            {
                assemblerUnmatched.Add(texture.name);
                if (!string.IsNullOrEmpty(problem))
                    assemblerScanWarnings.Add(problem);
                continue;
            }

            if (!groups.TryGetValue(baseName, out var set))
            {
                set = new MaterialAssemblerSet { baseName = baseName };
                groups.Add(baseName, set);
            }

            if (set.maps.TryGetValue(mapType, out Texture2D existing))
            {
                if (!set.duplicates.TryGetValue(mapType, out var dupes))
                {
                    dupes = new List<Texture2D> { existing };
                    set.duplicates.Add(mapType, dupes);
                }
                dupes.Add(texture);
            }
            else
            {
                set.maps.Add(mapType, texture);
            }
        }

        assemblerSets.AddRange(groups.Values.OrderBy(x => x.baseName, StringComparer.OrdinalIgnoreCase));

        if (assemblerSets.Count == 0)
            assemblerScanWarnings.Add("No recognized texture sets were found.");

        assemblerPreviewValid = true;
    }

    bool TryClassifyAssemblerTexture(
        string fileName,
        out string baseName,
        out MaterialAssemblerMapType mapType,
        out string problem)
    {
        baseName = null;
        mapType = MaterialAssemblerMapType.BaseColor;
        problem = null;

        var matches = new List<Tuple<MaterialAssemblerMapType, string, int>>();

        foreach (MaterialAssemblerMapType type in Enum.GetValues(typeof(MaterialAssemblerMapType)))
        {
            foreach (string alias in GetAssemblerAliases(type))
            {
                foreach (string separator in new[] { "_", "-", " ", "." })
                {
                    string suffix = separator + alias;
                    if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        matches.Add(Tuple.Create(type, suffix, suffix.Length));
                }
            }
        }

        if (matches.Count == 0)
            return false;

        int bestLength = matches.Max(x => x.Item3);
        var best = matches.Where(x => x.Item3 == bestLength).ToList();

        if (best.Select(x => x.Item1).Distinct().Count() > 1)
        {
            problem = "Ambiguous filename '" + fileName + "' matched more than one map type.";
            return false;
        }

        mapType = best[0].Item1;
        string matchedSuffix = best[0].Item2;
        baseName = fileName.Substring(0, fileName.Length - matchedSuffix.Length).Trim(' ', '_', '-', '.');

        if (string.IsNullOrWhiteSpace(baseName))
        {
            problem = "Could not determine a material base name for '" + fileName + "'.";
            return false;
        }

        return true;
    }

    IEnumerable<string> GetAssemblerAliases(MaterialAssemblerMapType type)
    {
        string source =
            type == MaterialAssemblerMapType.BaseColor ? assemblerBaseAliases :
            type == MaterialAssemblerMapType.Normal ? assemblerNormalAliases :
            type == MaterialAssemblerMapType.Metallic ? assemblerMetallicAliases :
            type == MaterialAssemblerMapType.Roughness ? assemblerRoughnessAliases :
            type == MaterialAssemblerMapType.Smoothness ? assemblerSmoothnessAliases :
            type == MaterialAssemblerMapType.AO ? assemblerAoAliases :
            type == MaterialAssemblerMapType.Emission ? assemblerEmissionAliases :
            assemblerMaskAliases;

        return (source ?? "")
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().TrimStart('_', '-', '.', ' '))
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Length);
    }

    void DrawMaterialAssemblerPreview(Shader shader)
    {
        int duplicateSets = assemblerSets.Count(x => x.HasDuplicates);
        int missingBase = assemblerSets.Count(x => !x.maps.ContainsKey(MaterialAssemblerMapType.BaseColor));
        int collisions = assemblerSets.Count(AssemblerMaterialExists);

        EditorGUILayout.LabelField(
            "Detected " + assemblerSets.Count + " material set" + (assemblerSets.Count == 1 ? "" : "s"),
            new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColLavenderText },
                fontSize = 13
            });

        EditorGUILayout.LabelField(
            duplicateSets + " ambiguous · " +
            missingBase + " without Base Color · " +
            collisions + " existing material collision" + (collisions == 1 ? "" : "s") + " · " +
            assemblerUnmatched.Count + " unmatched texture" + (assemblerUnmatched.Count == 1 ? "" : "s"),
            styleCardSubtitle);

        if (assemblerScanWarnings.Count > 0)
        {
            GUILayout.Space(5);
            foreach (string warning in assemblerScanWarnings.Distinct())
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        GUILayout.Space(7);

        assemblerPreviewScroll = GUILayout.BeginScrollView(
            assemblerPreviewScroll,
            true,
            false,
            GUILayout.Height(Mathf.Min(340f, 58f + assemblerSets.Count * 28f)));

        const float nameW = 150f;
        const float mapW = 72f;
        const float statusW = 220f;

        EditorGUILayout.BeginHorizontal();
        DrawAssemblerTableHeader("Material", nameW);
        DrawAssemblerTableHeader("Base", mapW);
        DrawAssemblerTableHeader("Normal", mapW);
        DrawAssemblerTableHeader("Metal", mapW);
        DrawAssemblerTableHeader("Rough", mapW);
        DrawAssemblerTableHeader("Smooth", mapW);
        DrawAssemblerTableHeader("AO", mapW);
        DrawAssemblerTableHeader("Emission", mapW);
        DrawAssemblerTableHeader("Mask", mapW);
        DrawAssemblerTableHeader("Status", statusW);
        EditorGUILayout.EndHorizontal();

        foreach (MaterialAssemblerSet set in assemblerSets)
        {
            EditorGUILayout.BeginHorizontal(styleRowBg);

            DrawAssemblerTableCell(set.baseName, nameW, true);
            DrawAssemblerMapCell(set, MaterialAssemblerMapType.BaseColor, mapW);
            DrawAssemblerMapCell(set, MaterialAssemblerMapType.Normal, mapW);
            DrawAssemblerMapCell(set, MaterialAssemblerMapType.Metallic, mapW);
            DrawAssemblerMapCell(set, MaterialAssemblerMapType.Roughness, mapW);
            DrawAssemblerMapCell(set, MaterialAssemblerMapType.Smoothness, mapW);
            DrawAssemblerMapCell(set, MaterialAssemblerMapType.AO, mapW);
            DrawAssemblerMapCell(set, MaterialAssemblerMapType.Emission, mapW);
            DrawAssemblerMapCell(set, MaterialAssemblerMapType.Mask, mapW);

            DrawAssemblerTableCell(GetAssemblerSetStatus(set, shader), statusW, false);
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        if (assemblerUnmatched.Count > 0)
        {
            GUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Unmatched textures are ignored. Examples: " +
                string.Join(", ", assemblerUnmatched.Take(5).ToArray()) +
                (assemblerUnmatched.Count > 5 ? " …" : ""),
                MessageType.Info);
        }
    }

    void DrawAssemblerTableHeader(string text, float width)
    {
        GUILayout.Label(
            text,
            new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColLavenderText }
            },
            GUILayout.Width(width),
            GUILayout.Height(22));
    }

    void DrawAssemblerTableCell(string text, float width, bool bold)
    {
        GUIStyle style = new GUIStyle(bold ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
            normal = { textColor = bold ? ColTextPrimary : ColTextMuted }
        };

        GUILayout.Label(text ?? "", style, GUILayout.Width(width), GUILayout.Height(24));
    }

    void DrawAssemblerMapCell(MaterialAssemblerSet set, MaterialAssemblerMapType type, float width)
    {
        string text =
            set.duplicates.ContainsKey(type) ? "DUP" :
            set.maps.ContainsKey(type) ? "✓" : "—";

        Color color =
            set.duplicates.ContainsKey(type) ? ColGradPink :
            set.maps.ContainsKey(type) ? ColLavenderText : ColTextMuted;

        GUILayout.Label(
            text,
            new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = color }
            },
            GUILayout.Width(width),
            GUILayout.Height(24));
    }

    string GetAssemblerSetStatus(MaterialAssemblerSet set, Shader shader)
    {
        if (set.HasDuplicates)
        {
            return "Ambiguous duplicate: " +
                   string.Join(", ", set.duplicates.Keys.Select(x => x.ToString()).ToArray());
        }

        if (AssemblerMaterialExists(set))
            return "Existing .mat — skipped";

        var unsupported = GetUnsupportedAssemblerMaps(set, shader);
        if (unsupported.Count > 0)
            return "Ready · unmapped: " + string.Join(", ", unsupported.Select(x => x.ToString()).ToArray());

        if (!set.maps.ContainsKey(MaterialAssemblerMapType.BaseColor))
            return "Ready · no Base Color";

        return "Ready";
    }

    List<MaterialAssemblerMapType> GetUnsupportedAssemblerMaps(MaterialAssemblerSet set, Shader shader)
    {
        var result = new List<MaterialAssemblerMapType>();
        if (shader == null)
        {
            result.AddRange(set.maps.Keys);
            return result;
        }

        foreach (var type in set.maps.Keys)
        {
            if (FindAssemblerShaderProperty(shader, type) == null)
                result.Add(type);
        }

        return result;
    }

    string BuildAssemblerConfirmation(int createCount)
    {
        int normalsToChange = 0;

        if (assemblerSetNormalImportType)
        {
            foreach (var set in assemblerSets)
            {
                if (set.HasDuplicates || AssemblerMaterialExists(set)) continue;
                if (!set.maps.TryGetValue(MaterialAssemblerMapType.Normal, out Texture2D normal)) continue;

                string path = AssetDatabase.GetAssetPath(normal);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                    normalsToChange++;
            }
        }

        int skippedCollisions = assemblerSets.Count(AssemblerMaterialExists);
        int skippedAmbiguous = assemblerSets.Count(x => x.HasDuplicates);

        return
            "Create " + createCount + " new material asset" + (createCount == 1 ? "" : "s") +
            " in:\n" + assemblerOutputFolder +
            "\n\nShader: " + (ResolveAssemblerShader() != null ? ResolveAssemblerShader().name : "Missing") +
            "\nNormal textures to reimport as Normal Map: " + normalsToChange +
            "\nExisting materials skipped: " + skippedCollisions +
            "\nAmbiguous sets skipped: " + skippedAmbiguous +
            "\n\nExisting material assets are never overwritten.";
    }

    void CreateMaterialAssemblerMaterials()
    {
        Shader shader = ResolveAssemblerShader();
        if (shader == null)
            throw new InvalidOperationException("The selected shader preset could not be resolved.");

        if (!IsValidAssemblerOutputPath())
            throw new InvalidOperationException("Output Folder must be inside Assets.");

        EnsureAssemblerAssetFolder(assemblerOutputFolder);

        var createdPaths = new List<string>();
        var importerBackups = new List<MaterialAssemblerImporterBackup>();

        try
        {
            foreach (MaterialAssemblerSet set in assemblerSets)
            {
                if (set.HasDuplicates || AssemblerMaterialExists(set))
                    continue;

                string assetPath = GetAssemblerMaterialPath(set);
                var material = new Material(shader)
                {
                    name = set.baseName
                };

                foreach (var kv in set.maps)
                {
                    string property = FindAssemblerShaderProperty(shader, kv.Key);
                    if (string.IsNullOrEmpty(property))
                        continue;

                    material.SetTexture(property, kv.Value);
                    ApplyAssemblerMaterialKeyword(material, kv.Key);
                }

                if (set.maps.ContainsKey(MaterialAssemblerMapType.Emission) &&
                    material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", Color.white);

                AssetDatabase.CreateAsset(material, assetPath);
                createdPaths.Add(assetPath);
            }

            AssetDatabase.SaveAssets();

            if (assemblerSetNormalImportType)
            {
                foreach (MaterialAssemblerSet set in assemblerSets)
                {
                    if (set.HasDuplicates || AssemblerMaterialExists(set)) continue;
                    if (!set.maps.TryGetValue(MaterialAssemblerMapType.Normal, out Texture2D normal)) continue;

                    string texturePath = AssetDatabase.GetAssetPath(normal);
                    var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if (importer == null || importer.textureType == TextureImporterType.NormalMap)
                        continue;

                    importerBackups.Add(new MaterialAssemblerImporterBackup
                    {
                        path = texturePath,
                        textureType = importer.textureType
                    });

                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ShowNotification(new GUIContent(
                "Created " + createdPaths.Count + " material" + (createdPaths.Count == 1 ? "" : "s")));

            assemblerPreviewValid = false;
        }
        catch
        {
            foreach (string path in createdPaths)
            {
                if (!string.IsNullOrEmpty(path))
                    AssetDatabase.DeleteAsset(path);
            }

            foreach (var backup in importerBackups)
            {
                var importer = AssetImporter.GetAtPath(backup.path) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = backup.textureType;
                importer.SaveAndReimport();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            throw;
        }
    }

    void ApplyAssemblerMaterialKeyword(Material material, MaterialAssemblerMapType type)
    {
        if (material == null) return;

        if (type == MaterialAssemblerMapType.Normal)
            material.EnableKeyword("_NORMALMAP");
        else if (type == MaterialAssemblerMapType.Metallic)
            material.EnableKeyword("_METALLICGLOSSMAP");
        else if (type == MaterialAssemblerMapType.Emission)
            material.EnableKeyword("_EMISSION");
    }

    Shader ResolveAssemblerShader()
    {
        if (assemblerShaderResolutionValid &&
            assemblerResolvedPreset == assemblerShaderPreset)
            return assemblerResolvedShader;

        assemblerResolvedPreset = assemblerShaderPreset;
        assemblerResolvedShader = null;

        if (assemblerShaderPreset == MaterialAssemblerShaderPreset.Standard)
        {
            assemblerResolvedShader = Shader.Find("Standard");
        }
        else if (assemblerShaderPreset == MaterialAssemblerShaderPreset.LilToon)
        {
            assemblerResolvedShader =
                Shader.Find("lilToon") ??
                FindAssemblerShaderAsset("lilToon");
        }
        else if (assemblerShaderPreset == MaterialAssemblerShaderPreset.Poiyomi)
        {
            assemblerResolvedShader =
                Shader.Find(".poiyomi/Poiyomi Toon") ??
                Shader.Find("Poiyomi/Poiyomi Toon") ??
                Shader.Find("poiyomi/Poiyomi Toon") ??
                FindAssemblerShaderAsset("Poiyomi Toon") ??
                FindAssemblerShaderAsset("Poiyomi");
        }
        else
        {
            assemblerResolvedShader = assemblerCustomShader;
        }

        assemblerShaderResolutionValid = true;
        return assemblerResolvedShader;
    }

    Shader FindAssemblerShaderAsset(string search)
    {
        string[] guids = AssetDatabase.FindAssets(search + " t:Shader");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader != null)
                return shader;
        }

        return null;
    }

    string FindAssemblerShaderProperty(Shader shader, MaterialAssemblerMapType type)
    {
        if (shader == null) return null;

        if (assemblerShaderPreset == MaterialAssemblerShaderPreset.Custom)
        {
            string custom =
                type == MaterialAssemblerMapType.BaseColor ? assemblerCustomBaseProperty :
                type == MaterialAssemblerMapType.Normal ? assemblerCustomNormalProperty :
                type == MaterialAssemblerMapType.Metallic ? assemblerCustomMetallicProperty :
                type == MaterialAssemblerMapType.Roughness ? assemblerCustomRoughnessProperty :
                type == MaterialAssemblerMapType.Smoothness ? assemblerCustomSmoothnessProperty :
                type == MaterialAssemblerMapType.AO ? assemblerCustomAoProperty :
                type == MaterialAssemblerMapType.Emission ? assemblerCustomEmissionProperty :
                assemblerCustomMaskProperty;

            return ShaderHasTextureProperty(shader, custom) ? custom : null;
        }

        string[] candidates =
            type == MaterialAssemblerMapType.BaseColor
                ? new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo", "_BaseColor" } :
            type == MaterialAssemblerMapType.Normal
                ? new[] { "_BumpMap", "_NormalMap", "_NormalTex" } :
            type == MaterialAssemblerMapType.Metallic
                ? new[] { "_MetallicGlossMap", "_MetallicMap", "_MetallicTex", "_MetalMap" } :
            type == MaterialAssemblerMapType.Roughness
                ? new[] { "_RoughnessMap", "_RoughnessTex", "_Roughness" } :
            type == MaterialAssemblerMapType.Smoothness
                ? new[] { "_SmoothnessTex", "_SmoothnessMap", "_GlossMap", "_GlossinessMap" } :
            type == MaterialAssemblerMapType.AO
                ? new[] { "_OcclusionMap", "_AOMap", "_AOTex", "_OcclusionTex" } :
            type == MaterialAssemblerMapType.Emission
                ? new[] { "_EmissionMap", "_EmissionTex", "_EmissiveMap" } :
                new[] { "_MaskMap", "_MaskTex", "_PackedMap", "_ORMMap" };

        foreach (string candidate in candidates)
        {
            if (ShaderHasTextureProperty(shader, candidate))
                return candidate;
        }

        return FindAssemblerPropertyByKeywords(shader, type);
    }

    string FindAssemblerPropertyByKeywords(Shader shader, MaterialAssemblerMapType type)
    {
        string[] words =
            type == MaterialAssemblerMapType.BaseColor ? new[] { "main", "base", "albedo", "diffuse" } :
            type == MaterialAssemblerMapType.Normal ? new[] { "normal", "bump" } :
            type == MaterialAssemblerMapType.Metallic ? new[] { "metallic", "metalness" } :
            type == MaterialAssemblerMapType.Roughness ? new[] { "roughness", "rough" } :
            type == MaterialAssemblerMapType.Smoothness ? new[] { "smoothness", "gloss" } :
            type == MaterialAssemblerMapType.AO ? new[] { "occlusion", "ambient occlusion", " ao" } :
            type == MaterialAssemblerMapType.Emission ? new[] { "emission", "emissive" } :
            new[] { "mask", "packed", "orm" };

        int count = ShaderUtil.GetPropertyCount(shader);
        for (int i = 0; i < count; i++)
        {
            if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                continue;

            string name = ShaderUtil.GetPropertyName(shader, i);
            string desc = ShaderUtil.GetPropertyDescription(shader, i);
            string haystack = (name + " " + desc).ToLowerInvariant();

            if (words.Any(word => haystack.Contains(word.ToLowerInvariant())))
                return name;
        }

        return null;
    }

    bool ShaderHasTextureProperty(Shader shader, string property)
    {
        if (shader == null || string.IsNullOrWhiteSpace(property))
            return false;

        int count = ShaderUtil.GetPropertyCount(shader);
        for (int i = 0; i < count; i++)
        {
            if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv &&
                string.Equals(ShaderUtil.GetPropertyName(shader, i), property, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    bool AssemblerMaterialExists(MaterialAssemblerSet set)
    {
        string path = GetAssemblerMaterialPath(set);
        return !string.IsNullOrEmpty(path) &&
               AssetDatabase.LoadAssetAtPath<Material>(path) != null;
    }

    string GetAssemblerMaterialPath(MaterialAssemblerSet set)
    {
        if (set == null || string.IsNullOrWhiteSpace(set.baseName))
            return null;

        string folder = NormalizeAssetFolderPath(assemblerOutputFolder);
        string file = SanitizeAssemblerFileName(set.baseName) + ".mat";
        return folder.TrimEnd('/') + "/" + file;
    }

    string SanitizeAssemblerFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Material";

        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value.Trim();
    }

    bool IsValidAssemblerOutputPath()
    {
        string folder = NormalizeAssetFolderPath(assemblerOutputFolder);
        return folder == "Assets" || folder.StartsWith("Assets/", StringComparison.Ordinal);
    }

    void EnsureAssemblerAssetFolder(string folder)
    {
        folder = NormalizeAssetFolderPath(folder);
        if (AssetDatabase.IsValidFolder(folder))
            return;

        if (folder == "Assets")
            return;

        string[] parts = folder.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
            throw new InvalidOperationException("Output Folder must be inside Assets.");

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    string NormalizeAssetFolderPath(string path)
    {
        return (path ?? "").Replace("\\", "/").TrimEnd('/');
    }

    string AssetFolderToAbsolute(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets", StringComparison.Ordinal))
            return Application.dataPath;

        return Path.Combine(projectRoot, assetPath).Replace("\\", "/");
    }

    string AbsoluteFolderToAssetPath(string absolute)
    {
        if (string.IsNullOrEmpty(absolute))
            return null;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/").TrimEnd('/');
        string normalized = Path.GetFullPath(absolute).Replace("\\", "/").TrimEnd('/');

        if (!normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            return null;

        string relative = normalized.Substring(projectRoot.Length + 1);
        if (relative == "Assets" || relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            return NormalizeAssetFolderPath(relative);

        return null;
    }
}
