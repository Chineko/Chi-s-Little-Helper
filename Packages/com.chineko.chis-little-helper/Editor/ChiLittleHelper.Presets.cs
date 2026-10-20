using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class ChiLittleHelperWindow
{
    enum LittleHelperPresetKind
    {
        DoctorThresholds,
        ContactSetup,
        ConstraintSetup,
        MaterialAssemblerRules,
        PhysBone
    }

    [Serializable]
    sealed class LittleHelperPresetEntry
    {
        public string id;
        public string name;
        public LittleHelperPresetKind kind;
        public string payload;
        public string category;
        public string description;
        public bool favorite;
        public string createdUtc;
    }

    [Serializable]
    sealed class LittleHelperPresetFile
    {
        public List<LittleHelperPresetEntry> presets = new List<LittleHelperPresetEntry>();
    }

    [Serializable]
    sealed class DoctorPresetData
    {
        public int triangles;
        public int materialSlots;
        public int renderers;
        public int physBones;
        public int textureMb;
        public int textureSize;
    }

    [Serializable]
    sealed class ContactPresetData
    {
        public bool createSender;
        public string collisionTag;
        public int shape;
        public float radius;
        public float height;
        public Vector3 boxSize;
        public Vector3 position;
        public Vector3 rotation;
        public bool localOnly;
        public bool allowSelf;
        public bool allowOthers;
        public int receiverType;
        public string parameter;
    }

    [Serializable]
    sealed class ConstraintPresetData
    {
        public int type;
        public float weight;
        public bool active;
    }

    [Serializable]
    sealed class MaterialAssemblerPresetData
    {
        public bool includeSubfolders;
        public bool setNormalImportType;
        public int shaderPreset;
        public string baseAliases;
        public string normalAliases;
        public string metallicAliases;
        public string roughnessAliases;
        public string smoothnessAliases;
        public string aoAliases;
        public string emissionAliases;
        public string maskAliases;
        public string customBase;
        public string customNormal;
        public string customMetallic;
        public string customRoughness;
        public string customSmoothness;
        public string customAo;
        public string customEmission;
        public string customMask;
    }

    [Serializable]
    sealed class PhysBonePresetData
    {
        public string componentType;
        public string json;
    }

    LittleHelperPresetFile presetLibrary = new LittleHelperPresetFile();
    bool presetLibraryLoaded = false;
    string presetNewName = "My Preset";
    LittleHelperPresetKind presetNewKind = LittleHelperPresetKind.DoctorThresholds;
    string presetNewCategory = "General";
    string presetNewDescription = "";
    string presetSearch = "";
    string presetCategoryFilter = "All";
    bool presetFavoritesOnly = false;

    string PresetLibraryPath
    {
        get
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, "ProjectSettings", "ChiLittleHelperPresets.json");
        }
    }

    void DrawPresetLibrary()
    {
        EnsurePresetLibraryLoaded();

        BeginCard(
            CardIcon.Sparkle,
            "Preset Library v2",
            "Organize, search, preview, duplicate, rename, favorite, import and export reusable toolkit presets.");

        DrawHelpHint("Review the preset preview before Apply. PhysBone presets may contain project-specific object references when moved between avatars/projects.");

        EditorGUILayout.HelpBox(
            "Project presets are stored in ProjectSettings/ChiLittleHelperPresets.json. PhysBone presets may contain project-specific object references.",
            MessageType.Info);

        GUILayout.Space(6);

        EditorGUILayout.BeginVertical(styleRowBg);

        DrawPresetTextField("Preset Name", ref presetNewName);
        GUILayout.Space(3);
        DrawPresetTextField("Category", ref presetNewCategory);
        GUILayout.Space(3);
        DrawPresetTextField("Description", ref presetNewDescription);
        GUILayout.Space(3);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Preset Type", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        presetNewKind = (LittleHelperPresetKind)EditorGUILayout.Popup(
            (int)presetNewKind,
            Enum.GetNames(typeof(LittleHelperPresetKind)),
            stylePopup,
            GUILayout.Height(24));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(7);

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(presetNewName) || !CanCapturePreset(presetNewKind)))
        {
            if (GUILayout.Button("Capture Current Settings", styleBtnGrad, GUILayout.Height(34)))
                CapturePreset(presetNewKind, presetNewName.Trim());
        }

        if (!CanCapturePreset(presetNewKind))
            EditorGUILayout.HelpBox(GetPresetCaptureRequirement(presetNewKind), MessageType.Info);

        EditorGUILayout.EndVertical();

        GUILayout.Space(8);

        BeginResponsiveButtonRow();
        if (GUILayout.Button("Export Library JSON", styleBtnDark, GUILayout.Height(30), GUILayout.ExpandWidth(true)))
            ExportPresetLibrary();
        ResponsiveButtonGap();
        if (GUILayout.Button("Import Library JSON", styleBtnDark, GUILayout.Height(30), GUILayout.ExpandWidth(true)))
            ImportPresetLibrary();
        ResponsiveButtonGap();
        if (GUILayout.Button("Reload", styleBtnDark, ResponsiveButtonWidth(80), GUILayout.Height(30)))
        {
            presetLibraryLoaded = false;
            EnsurePresetLibraryLoaded();
        }
        EndResponsiveButtonRow();

        GUILayout.Space(10);

        EditorGUILayout.BeginVertical(styleRowBg);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Search", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        presetSearch = EditorGUILayout.TextField(presetSearch, styleInputBox);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(3);

        string[] categories = new[] { "All" }
            .Concat(presetLibrary.presets
                .Select(p => string.IsNullOrWhiteSpace(p.category) ? "General" : p.category)
                .Distinct()
                .OrderBy(x => x))
            .ToArray();

        int categoryIndex = Mathf.Max(0, Array.IndexOf(categories, presetCategoryFilter));
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Category", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        categoryIndex = EditorGUILayout.Popup(categoryIndex, categories, stylePopup, GUILayout.Height(24));
        EditorGUILayout.EndHorizontal();
        presetCategoryFilter = categories[Mathf.Clamp(categoryIndex, 0, categories.Length - 1)];

        GUILayout.Space(3);
        presetFavoritesOnly = EditorGUILayout.ToggleLeft("Favorites only", presetFavoritesOnly);

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        List<LittleHelperPresetEntry> visible = presetLibrary.presets
            .Where(PresetMatchesFilters)
            .OrderByDescending(p => p.favorite)
            .ThenBy(p => string.IsNullOrWhiteSpace(p.category) ? "General" : p.category)
            .ThenBy(p => p.name)
            .ToList();

        if (visible.Count == 0)
        {
            EditorGUILayout.HelpBox(
                presetLibrary.presets.Count == 0
                    ? "No presets saved yet."
                    : "No presets match the current filters.",
                MessageType.Info);
            EndCard();
            return;
        }

        foreach (LittleHelperPresetEntry preset in visible)
        {
            EditorGUILayout.BeginVertical(styleRowBg);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(preset.favorite ? "★" : "☆", styleBtnDark, GUILayout.Width(34), GUILayout.Height(26)))
            {
                preset.favorite = !preset.favorite;
                SavePresetLibrary();
            }

            GUILayout.Space(4);

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(preset.name, new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColTextPrimary }
            });

            EditorGUILayout.LabelField(
                (string.IsNullOrWhiteSpace(preset.category) ? "General" : preset.category) +
                " · " + preset.kind,
                styleCardSubtitle);

            if (!string.IsNullOrWhiteSpace(preset.description))
                EditorGUILayout.LabelField(preset.description, styleInfoText);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            EditorGUILayout.HelpBox(BuildPresetPreview(preset), MessageType.None);

            GUILayout.Space(4);

            if (IsNarrowLayout)
            {
                if (GUILayout.Button("Apply", styleBtnGrad, GUILayout.Height(28)))
                    ApplyPreset(preset);

                GUILayout.Space(3);
                DrawPresetManagementRow(preset);
            }
            else
            {
                BeginResponsiveButtonRow();

                if (GUILayout.Button("Apply", styleBtnGrad, ResponsiveButtonWidth(74), GUILayout.Height(28)))
                    ApplyPreset(preset);

                ResponsiveButtonGap();
                if (GUILayout.Button("Rename", styleBtnDark, ResponsiveButtonWidth(74), GUILayout.Height(28)))
                    RenamePreset(preset);

                ResponsiveButtonGap();
                if (GUILayout.Button("Duplicate", styleBtnDark, ResponsiveButtonWidth(82), GUILayout.Height(28)))
                    DuplicatePreset(preset);

                ResponsiveButtonGap();
                if (GUILayout.Button("Export", styleBtnDark, ResponsiveButtonWidth(70), GUILayout.Height(28)))
                    ExportSinglePreset(preset);

                if (!IsCompactLayout)
                    GUILayout.FlexibleSpace();
                else
                    ResponsiveButtonGap();

                if (GUILayout.Button("Delete", styleBtnRemove, ResponsiveButtonWidth(70), GUILayout.Height(28)))
                {
                    DeletePreset(preset);
                    GUIUtility.ExitGUI();
                }

                EndResponsiveButtonRow();
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(6);
        }

        EndCard();
    }

    void DrawPresetTextField(string label, ref string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        value = EditorGUILayout.TextField(value ?? "", styleInputBox);
        EditorGUILayout.EndHorizontal();
    }

    void DrawPresetManagementRow(LittleHelperPresetEntry preset)
    {
        BeginResponsiveButtonRow();
        if (GUILayout.Button("Rename", styleBtnDark, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
            RenamePreset(preset);
        ResponsiveButtonGap(3f);
        if (GUILayout.Button("Duplicate", styleBtnDark, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
            DuplicatePreset(preset);
        ResponsiveButtonGap(3f);
        if (GUILayout.Button("Export", styleBtnDark, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
            ExportSinglePreset(preset);
        ResponsiveButtonGap(3f);
        if (GUILayout.Button("Delete", styleBtnRemove, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
        {
            DeletePreset(preset);
            GUIUtility.ExitGUI();
        }
        EndResponsiveButtonRow();
    }

    bool PresetMatchesFilters(LittleHelperPresetEntry preset)
    {
        if (preset == null) return false;
        if (presetFavoritesOnly && !preset.favorite) return false;

        string category = string.IsNullOrWhiteSpace(preset.category) ? "General" : preset.category;
        if (!string.Equals(presetCategoryFilter, "All", StringComparison.Ordinal) &&
            !string.Equals(category, presetCategoryFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        string search = (presetSearch ?? "").Trim();
        if (search.Length == 0) return true;

        string haystack =
            (preset.name ?? "") + "\n" +
            category + "\n" +
            (preset.description ?? "") + "\n" +
            preset.kind;

        return haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    string BuildPresetPreview(LittleHelperPresetEntry preset)
    {
        if (preset == null) return "No preset.";

        if (preset.kind == LittleHelperPresetKind.DoctorThresholds)
        {
            var d = JsonUtility.FromJson<DoctorPresetData>(preset.payload);
            return "Will set Doctor thresholds: triangles " + d.triangles.ToString("N0") +
                   ", materials " + d.materialSlots +
                   ", renderers " + d.renderers +
                   ", PhysBones " + d.physBones +
                   ", texture " + d.textureMb + " MB / " + d.textureSize + "px.";
        }

        if (preset.kind == LittleHelperPresetKind.ContactSetup)
        {
            var d = JsonUtility.FromJson<ContactPresetData>(preset.payload);
            return "Will load Contact Setup: " + (d.createSender ? "Sender" : "Receiver") +
                   ", tag '" + d.collisionTag + "', radius " + d.radius.ToString("0.###") +
                   ", parameter '" + d.parameter + "'.";
        }

        if (preset.kind == LittleHelperPresetKind.ConstraintSetup)
        {
            var d = JsonUtility.FromJson<ConstraintPresetData>(preset.payload);
            return "Will load Constraint Setup: type " + d.type +
                   ", weight " + d.weight.ToString("0.##") +
                   ", active " + d.active + ".";
        }

        if (preset.kind == LittleHelperPresetKind.MaterialAssemblerRules)
        {
            var d = JsonUtility.FromJson<MaterialAssemblerPresetData>(preset.payload);
            return "Will load Material Assembler naming rules and shader preset " +
                   ((MaterialAssemblerShaderPreset)d.shaderPreset) +
                   (d.includeSubfolders ? " with subfolder scanning." : ".");
        }

        return "Will overwrite serialized settings on the currently selected PhysBone. Review object references after applying across avatars/projects.";
    }

    void RenamePreset(LittleHelperPresetEntry preset)
    {
        string next = EditorUtility.DisplayDialogComplex(
            "Rename Preset",
            "Use the current 'Preset Name' field ('" + presetNewName + "') as the new name for '" + preset.name + "'?",
            "Rename",
            "Cancel",
            "Copy Current Name") == 0
            ? presetNewName
            : null;

        if (string.IsNullOrWhiteSpace(next))
            return;

        preset.name = next.Trim();
        SavePresetLibrary();
    }

    void DuplicatePreset(LittleHelperPresetEntry preset)
    {
        var copy = new LittleHelperPresetEntry
        {
            id = Guid.NewGuid().ToString("N"),
            name = preset.name + " Copy",
            kind = preset.kind,
            payload = preset.payload,
            category = preset.category,
            description = preset.description,
            favorite = false,
            createdUtc = DateTime.UtcNow.ToString("o")
        };
        presetLibrary.presets.Add(copy);
        SavePresetLibrary();
    }

    void ExportSinglePreset(LittleHelperPresetEntry preset)
    {
        string path = EditorUtility.SaveFilePanel(
            "Export Preset",
            "",
            preset.name + ".json",
            "json");

        if (string.IsNullOrEmpty(path)) return;

        var wrapper = new LittleHelperPresetFile();
        wrapper.presets.Add(preset);
        File.WriteAllText(path, JsonUtility.ToJson(wrapper, true));
        ShowNotification(new GUIContent("Preset exported"));
    }

    void DeletePreset(LittleHelperPresetEntry preset)
    {
        if (!confirmDestructiveActions ||
            EditorUtility.DisplayDialog(
                "Delete Preset",
                "Delete preset '" + preset.name + "'?",
                "Delete",
                "Cancel"))
        {
            presetLibrary.presets.Remove(preset);
            SavePresetLibrary();
        }
    }

    bool CanCapturePreset(LittleHelperPresetKind kind)
    {
        if (kind != LittleHelperPresetKind.PhysBone)
            return true;

        return GetSelectedPhysBoneForPreset() != null;
    }

    string GetPresetCaptureRequirement(LittleHelperPresetKind kind)
    {
        if (kind == LittleHelperPresetKind.PhysBone)
            return "Select a GameObject containing a VRChat PhysBone to capture this preset.";
        return "";
    }

    void CapturePreset(LittleHelperPresetKind kind, string name)
    {
        string payload;

        if (kind == LittleHelperPresetKind.DoctorThresholds)
        {
            payload = JsonUtility.ToJson(new DoctorPresetData
            {
                triangles = doctorTriangleThreshold,
                materialSlots = doctorMaterialSlotThreshold,
                renderers = doctorRendererThreshold,
                physBones = doctorPhysBoneThreshold,
                textureMb = doctorTextureMemoryThresholdMb,
                textureSize = doctorLargeTextureThreshold
            });
        }
        else if (kind == LittleHelperPresetKind.ContactSetup)
        {
            payload = JsonUtility.ToJson(new ContactPresetData
            {
                createSender = contactCreateSender,
                collisionTag = contactTag,
                shape = contactShape,
                radius = contactRadius,
                height = contactHeight,
                boxSize = contactBoxSize,
                position = contactPosition,
                rotation = contactRotation,
                localOnly = contactLocalOnly,
                allowSelf = contactAllowSelf,
                allowOthers = contactAllowOthers,
                receiverType = contactReceiverType,
                parameter = contactParameter
            });
        }
        else if (kind == LittleHelperPresetKind.ConstraintSetup)
        {
            payload = JsonUtility.ToJson(new ConstraintPresetData
            {
                type = constraintType,
                weight = constraintWeight,
                active = constraintActivate
            });
        }
        else if (kind == LittleHelperPresetKind.MaterialAssemblerRules)
        {
            payload = JsonUtility.ToJson(new MaterialAssemblerPresetData
            {
                includeSubfolders = assemblerIncludeSubfolders,
                setNormalImportType = assemblerSetNormalImportType,
                shaderPreset = (int)assemblerShaderPreset,
                baseAliases = assemblerBaseAliases,
                normalAliases = assemblerNormalAliases,
                metallicAliases = assemblerMetallicAliases,
                roughnessAliases = assemblerRoughnessAliases,
                smoothnessAliases = assemblerSmoothnessAliases,
                aoAliases = assemblerAoAliases,
                emissionAliases = assemblerEmissionAliases,
                maskAliases = assemblerMaskAliases,
                customBase = assemblerCustomBaseProperty,
                customNormal = assemblerCustomNormalProperty,
                customMetallic = assemblerCustomMetallicProperty,
                customRoughness = assemblerCustomRoughnessProperty,
                customSmoothness = assemblerCustomSmoothnessProperty,
                customAo = assemblerCustomAoProperty,
                customEmission = assemblerCustomEmissionProperty,
                customMask = assemblerCustomMaskProperty
            });
        }
        else
        {
            Component pb = GetSelectedPhysBoneForPreset();
            if (pb == null)
                throw new InvalidOperationException("Select a GameObject containing a VRChat PhysBone.");

            payload = JsonUtility.ToJson(new PhysBonePresetData
            {
                componentType = pb.GetType().AssemblyQualifiedName,
                json = EditorJsonUtility.ToJson(pb)
            });
        }

        presetLibrary.presets.Add(new LittleHelperPresetEntry
        {
            id = Guid.NewGuid().ToString("N"),
            name = name,
            kind = kind,
            payload = payload,
            category = string.IsNullOrWhiteSpace(presetNewCategory) ? "General" : presetNewCategory.Trim(),
            description = presetNewDescription ?? "",
            favorite = false,
            createdUtc = DateTime.UtcNow.ToString("o")
        });

        SavePresetLibrary();
        ShowNotification(new GUIContent("Preset saved"));
    }

    void ApplyPreset(LittleHelperPresetEntry preset)
    {
        if (preset == null) return;

        Run("Apply Preset", () =>
        {
            if (preset.kind == LittleHelperPresetKind.DoctorThresholds)
            {
                var data = JsonUtility.FromJson<DoctorPresetData>(preset.payload);
                doctorTriangleThreshold = data.triangles;
                doctorMaterialSlotThreshold = data.materialSlots;
                doctorRendererThreshold = data.renderers;
                doctorPhysBoneThreshold = data.physBones;
                doctorTextureMemoryThresholdMb = data.textureMb;
                doctorLargeTextureThreshold = data.textureSize;

                EditorPrefs.SetInt(PREF_DOCTOR_TRIANGLES, doctorTriangleThreshold);
                EditorPrefs.SetInt(PREF_DOCTOR_MATERIAL_SLOTS, doctorMaterialSlotThreshold);
                EditorPrefs.SetInt(PREF_DOCTOR_RENDERERS, doctorRendererThreshold);
                EditorPrefs.SetInt(PREF_DOCTOR_PHYSBONES, doctorPhysBoneThreshold);
                EditorPrefs.SetInt(PREF_DOCTOR_TEXTURE_MB, doctorTextureMemoryThresholdMb);
                EditorPrefs.SetInt(PREF_DOCTOR_TEXTURE_SIZE, doctorLargeTextureThreshold);
                doctorDirty = true;
            }
            else if (preset.kind == LittleHelperPresetKind.ContactSetup)
            {
                var data = JsonUtility.FromJson<ContactPresetData>(preset.payload);
                contactCreateSender = data.createSender;
                contactTag = data.collisionTag;
                contactShape = data.shape;
                contactRadius = data.radius;
                contactHeight = data.height;
                contactBoxSize = data.boxSize;
                contactPosition = data.position;
                contactRotation = data.rotation;
                contactLocalOnly = data.localOnly;
                contactAllowSelf = data.allowSelf;
                contactAllowOthers = data.allowOthers;
                contactReceiverType = data.receiverType;
                contactParameter = data.parameter;
            }
            else if (preset.kind == LittleHelperPresetKind.ConstraintSetup)
            {
                var data = JsonUtility.FromJson<ConstraintPresetData>(preset.payload);
                constraintType = data.type;
                constraintWeight = data.weight;
                constraintActivate = data.active;
            }
            else if (preset.kind == LittleHelperPresetKind.MaterialAssemblerRules)
            {
                var data = JsonUtility.FromJson<MaterialAssemblerPresetData>(preset.payload);
                assemblerIncludeSubfolders = data.includeSubfolders;
                assemblerSetNormalImportType = data.setNormalImportType;
                assemblerShaderPreset = (MaterialAssemblerShaderPreset)data.shaderPreset;
                assemblerBaseAliases = data.baseAliases;
                assemblerNormalAliases = data.normalAliases;
                assemblerMetallicAliases = data.metallicAliases;
                assemblerRoughnessAliases = data.roughnessAliases;
                assemblerSmoothnessAliases = data.smoothnessAliases;
                assemblerAoAliases = data.aoAliases;
                assemblerEmissionAliases = data.emissionAliases;
                assemblerMaskAliases = data.maskAliases;
                assemblerCustomBaseProperty = data.customBase;
                assemblerCustomNormalProperty = data.customNormal;
                assemblerCustomMetallicProperty = data.customMetallic;
                assemblerCustomRoughnessProperty = data.customRoughness;
                assemblerCustomSmoothnessProperty = data.customSmoothness;
                assemblerCustomAoProperty = data.customAo;
                assemblerCustomEmissionProperty = data.customEmission;
                assemblerCustomMaskProperty = data.customMask;
                assemblerShaderResolutionValid = false;
                assemblerPreviewValid = false;
            }
            else if (preset.kind == LittleHelperPresetKind.PhysBone)
            {
                var data = JsonUtility.FromJson<PhysBonePresetData>(preset.payload);
                Component pb = GetSelectedPhysBoneForPreset();

                if (pb == null)
                    throw new InvalidOperationException("Select a GameObject containing a VRChat PhysBone before applying this preset.");

                Type expected = Type.GetType(data.componentType);
                if (expected != null && pb.GetType() != expected)
                    throw new InvalidOperationException("Selected PhysBone type does not match this preset.");

                Undo.RecordObject(pb, "Apply PhysBone Preset");
                EditorJsonUtility.FromJsonOverwrite(data.json, pb);
                EditorUtility.SetDirty(pb);
                doctorDirty = true;
            }

            ShowNotification(new GUIContent("Preset applied"));
        });
    }

    Component GetSelectedPhysBoneForPreset()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null) return null;

        Type pbType = FindType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone");
        return pbType != null ? selected.GetComponent(pbType) : null;
    }

    void EnsurePresetLibraryLoaded()
    {
        if (presetLibraryLoaded) return;

        presetLibrary = new LittleHelperPresetFile();

        try
        {
            if (File.Exists(PresetLibraryPath))
            {
                string json = File.ReadAllText(PresetLibraryPath);
                var loaded = JsonUtility.FromJson<LittleHelperPresetFile>(json);
                if (loaded != null)
                    presetLibrary = loaded;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Chi's Little Helper] Could not load preset library: " + ex.Message);
        }

        if (presetLibrary.presets == null)
            presetLibrary.presets = new List<LittleHelperPresetEntry>();

        foreach (LittleHelperPresetEntry entry in presetLibrary.presets)
        {
            if (string.IsNullOrWhiteSpace(entry.category))
                entry.category = "General";
            if (entry.description == null)
                entry.description = "";
        }

        presetLibraryLoaded = true;
    }

    void SavePresetLibrary()
    {
        string directory = Path.GetDirectoryName(PresetLibraryPath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(
            PresetLibraryPath,
            JsonUtility.ToJson(presetLibrary, true));
    }

    void ExportPresetLibrary()
    {
        EnsurePresetLibraryLoaded();

        string path = EditorUtility.SaveFilePanel(
            "Export Chi's Little Helper Presets",
            "",
            "ChiLittleHelperPresets.json",
            "json");

        if (string.IsNullOrEmpty(path)) return;

        File.WriteAllText(path, JsonUtility.ToJson(presetLibrary, true));
        ShowNotification(new GUIContent("Preset library exported"));
    }

    void ImportPresetLibrary()
    {
        string path = EditorUtility.OpenFilePanel(
            "Import Chi's Little Helper Presets",
            "",
            "json");

        if (string.IsNullOrEmpty(path)) return;

        string json = File.ReadAllText(path);
        var incoming = JsonUtility.FromJson<LittleHelperPresetFile>(json);

        if (incoming == null || incoming.presets == null)
            throw new InvalidOperationException("The selected file is not a valid Little Helper preset library.");

        foreach (LittleHelperPresetEntry entry in incoming.presets)
        {
            if (string.IsNullOrWhiteSpace(entry.category))
                entry.category = "General";
            if (entry.description == null)
                entry.description = "";
            if (string.IsNullOrWhiteSpace(entry.createdUtc))
                entry.createdUtc = DateTime.UtcNow.ToString("o");

            if (string.IsNullOrEmpty(entry.id) ||
                presetLibrary.presets.Any(p => p.id == entry.id))
                entry.id = Guid.NewGuid().ToString("N");

            presetLibrary.presets.Add(entry);
        }

        SavePresetLibrary();
        ShowNotification(new GUIContent("Preset library imported"));
    }
}
