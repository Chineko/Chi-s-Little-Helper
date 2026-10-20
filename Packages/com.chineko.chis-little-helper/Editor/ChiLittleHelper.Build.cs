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
    void DrawMultiToggles()
    {
        BeginCard(CardIcon.MultiToggles, "Multi Toggles", "Build several object toggles in one pass.");
        DrawInfoBox("Add avatar assets, customize each menu name and optional icon, then create them together. Icon assignment depends on the installed VRCFury version.");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Menu path prefix", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        multiToggleMenuPrefix = EditorGUILayout.TextField(multiToggleMenuPrefix, styleInputBox);
        GUILayout.Space(14);
        EditorGUILayout.LabelField("Default On", styleFieldLabel, GUILayout.Width(68));
        multiToggleDefaultOn = EditorGUILayout.Toggle(multiToggleDefaultOn, GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(12);

        BeginResponsiveButtonRow();
        if (GUILayout.Button("+ Add Selected Objects", styleBtnGrad, GUILayout.Height(42), GUILayout.ExpandWidth(true)))
            Run("Add selected objects", AddSelectedMultiToggleAssets);
        ResponsiveButtonGap(8f);
        if (GUILayout.Button("Add All with Renderers", styleBtnDark, GUILayout.Height(42), GUILayout.ExpandWidth(true)))
            Run("Add all with renderers", AddRendererRootsToMultiToggleAssets);
        EndResponsiveButtonRow();

        GUILayout.Space(14);

        var iconStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = ColLavenderText }, fontSize = 12 };
        EditorGUILayout.LabelField("Assets to configure - " + multiToggles.Count, iconStyle);

        GUILayout.Space(8);

        BeginResponsiveButtonRow();
        using (new EditorGUI.DisabledScope(multiToggles.Count == 0))
        {
            if (GUILayout.Button("Clear List", styleBtnDark, GUILayout.Height(40), GUILayout.ExpandWidth(true)))
            {
                multiToggles.Clear();
                multiToggleSet.Clear();
            }
        }
        ResponsiveButtonGap(8f);
        using (new EditorGUI.DisabledScope(multiToggles.Count == 0))
        {
            if (GUILayout.Button("Create Toggles for All", styleBtnGrad, GUILayout.Height(40), GUILayout.ExpandWidth(true)))
                Run("Create Toggles", CreateMultiToggles);
        }
        EndResponsiveButtonRow();

        GUILayout.Space(14);

        if (multiToggles.Count == 0)
        {
            GUILayout.Space(10);
            var boxRect = GUILayoutUtility.GetRect(0f, 92f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                float boxX = boxRect.x + (boxRect.width - 150f) * 0.5f;
                GUI.DrawTexture(new Rect(boxX, boxRect.y, 150f, 92f), texBoxIllustration, ScaleMode.ScaleToFit);
            }

            var emptyTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Hex("#DDBBFF") }
            };
            EditorGUILayout.LabelField("No assets added yet", emptyTitleStyle);

            var emptySubtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColTextMuted }
            };
            EditorGUILayout.LabelField("Add objects to start creating multi toggles.", emptySubtitleStyle);
            GUILayout.Space(12);
        }
        else
        {
            int removeAt = -1;
            for (int i = 0; i < multiToggles.Count; i++)
            {
                var e = multiToggles[i];

                EditorGUILayout.BeginVertical(styleRowBg);
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical();
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();

                var newAsset = (GameObject)DrawThemedObjectField(e.asset, typeof(GameObject), true, GUILayout.Height(24));
                if (newAsset != e.asset)
                {
                    if (e.asset != null) multiToggleSet.Remove(e.asset);
                    if (newAsset != null) multiToggleSet.Add(newAsset);
                    e.asset = newAsset;
                }

                GUILayout.Space(8);

                e.name = EditorGUILayout.TextField(e.name, styleInputBox, GUILayout.Height(24));

                EditorGUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();

                GUILayout.Space(8);

                e.icon = (Texture2D)DrawThemedObjectField(e.icon, typeof(Texture2D), false,
                    GUILayout.Width(58),
                    GUILayout.Height(58));

                GUILayout.Space(8);

                EditorGUILayout.BeginVertical(GUILayout.Width(28));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("x", styleBtnRemove, GUILayout.Width(28), GUILayout.Height(28)))
                    removeAt = i;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                GUILayout.Space(6);
            }

            if (removeAt >= 0)
            {
                if (multiToggles[removeAt].asset != null) multiToggleSet.Remove(multiToggles[removeAt].asset);
                multiToggles.RemoveAt(removeAt);
            }
        }

        EndCard();
    }

    void AddMultiToggleAsset(GameObject go)
    {
        if (go == null || go == avatar.gameObject || !go.transform.IsChildOf(avatar.transform) || !multiToggleSet.Add(go)) return;
        multiToggles.Add(new ToggleEntry { asset = go, name = go.name });
    }

    void AddSelectedMultiToggleAssets()
    {
        foreach (var go in Selection.gameObjects) AddMultiToggleAsset(go);
    }

    void AddRendererRootsToMultiToggleAssets()
    {
        foreach (var renderer in avatar.GetComponentsInChildren<Renderer>(true))
            if (renderer != null) AddMultiToggleAsset(renderer.gameObject);
    }

    void CreateMultiToggles()
    {
        int created = 0;
        GameObject first = null;
        string prefix = string.IsNullOrWhiteSpace(multiToggleMenuPrefix) ? null : multiToggleMenuPrefix.Trim().TrimEnd('/');

        foreach (var e in multiToggles)
        {
            var asset = e.asset;
            if (asset == null || asset == avatar.gameObject || !asset.transform.IsChildOf(avatar.transform)) continue;

            string toggleName = string.IsNullOrWhiteSpace(e.name) ? asset.name : e.name.Trim();
            var toggleObject = NewChild(asset.transform, "Toggle - " + toggleName);
            if (!AddFeature(toggleObject, T_TOGGLE, out var comp, out var feature)) continue;

            TrySet(feature, "name", prefix == null ? toggleName : prefix + "/" + toggleName);
            TrySet(feature, "defaultOn", multiToggleDefaultOn);
            if (e.icon != null)
            {
                bool iconSet = TrySet(feature, "icon", e.icon, warn: false)
                            || TrySet(feature, "menuIcon", e.icon, warn: false)
                            || TrySet(feature, "iconTexture", e.icon, warn: false);
                if (!iconSet) Debug.LogWarning($"[AvatarTools] Could not find icon field for '{toggleName}'.");
            }
            var state = EnsureState(feature, "state");
            if (state != null) AddObjectOnAction(state, asset);

            EditorUtility.SetDirty(comp);
            if (first == null) first = asset;
            created++;
        }

        overviewDirty = true;
        Selection.activeObject = first;
    }

    void DrawArmatureLink()
    {
        BeginCard(CardIcon.Armature, "Armature Link", "Attach clothing or props to the avatar's armature and link them to the hips.");
        DrawInfoBox("Choose the clothing or prop root. The tool creates a VRCFury Armature Link component on the target.");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Clothing root", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        clothing = (GameObject)DrawThemedObjectField(clothing, typeof(GameObject), true, GUILayout.Height(24));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(14);
        using (new EditorGUI.DisabledScope(clothing == null))
        {
            if (GUILayout.Button("Add Armature Link", styleBtnGrad, GUILayout.Height(38)))
                Run("Add Armature Link", AddArmatureLink);
        }
        EndCard();
    }

    void AddArmatureLink()
    {
        var propBone = FindArmatureRoot(clothing.transform);
        var animator = avatar.GetComponent<Animator>();
        var hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;

        if (!AddFeature(clothing, T_ARMATURELINK, out var comp, out var feature)) return;

        if (propBone != null) TrySet(feature, "propBone", propBone.gameObject);
        if (hips != null) TrySetLinkTo(feature, hips.gameObject);
        EditorUtility.SetDirty(comp);
        overviewDirty = true;
        Selection.activeObject = clothing;
    }

    static Transform FindArmatureRoot(Transform root)
    {
        for (int i = 0, n = root.childCount; i < n; i++)
        {
            var t = root.GetChild(i);
            var name = t.name;
            if (name.IndexOf("armature", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("hips", StringComparison.OrdinalIgnoreCase) >= 0) return t;
        }
        return null;
    }

    void DrawDissolve()
    {
        BeginCard(CardIcon.Dissolve, "Dissolve Toggle", "Create a menu toggle that animates a material property over time.");
        DrawInfoBox("Supports Poiyomi _DissolveAlpha and other float/range shader properties with smooth transition states.");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Target object", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        dissolveTarget = (GameObject)DrawThemedObjectField(dissolveTarget, typeof(GameObject), true, GUILayout.Height(24));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Menu path", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        dissolveMenuPath = EditorGUILayout.TextField(dissolveMenuPath, styleInputBox);
        EditorGUILayout.EndHorizontal();

        if (dissolveTarget != dissolveDetectedFor || dissolveDirty)
        {
            dissolveDetectedFor = dissolveTarget;
            dissolveDirty = false;
            FindDissolveProperties(dissolveTarget, dissolveDetected);
            dissolveDetectedArray = dissolveDetected.ToArray();
        }

        if (dissolveDetectedArray.Length > 0)
        {
            GUILayout.Space(4);
            int cur = Mathf.Max(0, dissolveDetected.IndexOf(dissolveProperty));
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Detected property", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
            int sel = EditorGUILayout.Popup(
                cur,
                dissolveDetectedArray,
                stylePopup,
                GUILayout.Height(24));
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck() || string.IsNullOrEmpty(dissolveProperty)) dissolveProperty = dissolveDetectedArray[sel];
        }

        GUILayout.Space(4);
        dissolveProperty = EditorGUILayout.TextField("Shader property", dissolveProperty, styleInputBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Value when dissolved", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        dissolveHiddenValue = EditorGUILayout.FloatField(dissolveHiddenValue, styleNumberField);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Value when visible", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        dissolveShownValue = EditorGUILayout.FloatField(dissolveShownValue, styleNumberField);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Blend time (seconds)", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        dissolveTime = EditorGUILayout.FloatField(dissolveTime, styleNumberField);
        EditorGUILayout.EndHorizontal();
        dissolveDefaultOn = EditorGUILayout.Toggle("Default On", dissolveDefaultOn);

        GUILayout.Space(14);
        using (new EditorGUI.DisabledScope(dissolveTarget == null || string.IsNullOrEmpty(dissolveProperty)))
        {
            if (GUILayout.Button("Create Dissolve Toggle", styleBtnGrad, GUILayout.Height(38)))
                Run("Create Dissolve Toggle", CreateDissolveToggle);
        }
        EndCard();
    }

    static void FindDissolveProperties(GameObject target, List<string> result)
    {
        result.Clear();
        if (target == null) return;
        var seenShaders = new HashSet<Shader>();
        target.GetComponentsInChildren(true, RendererScratch);
        foreach (var r in RendererScratch)
        {
            r.GetSharedMaterials(MaterialScratch);
            foreach (var m in MaterialScratch)
            {
                if (m == null || m.shader == null || !seenShaders.Add(m.shader)) continue;
                var shader = m.shader;
                int count = ShaderUtil.GetPropertyCount(shader);
                for (int i = 0; i < count; i++)
                {
                    var type = ShaderUtil.GetPropertyType(shader, i);
                    if (type != ShaderUtil.ShaderPropertyType.Float && type != ShaderUtil.ShaderPropertyType.Range) continue;
                    var n = ShaderUtil.GetPropertyName(shader, i);
                    if (n.IndexOf("dissolve", StringComparison.OrdinalIgnoreCase) >= 0 && !result.Contains(n)) result.Add(n);
                }
            }
        }
        RendererScratch.Clear();
        MaterialScratch.Clear();
    }

    static bool HasProperty(Renderer r, string property)
    {
        r.GetSharedMaterials(MaterialScratch);
        foreach (var m in MaterialScratch)
            if (m != null && m.HasProperty(property)) { MaterialScratch.Clear(); return true; }
        MaterialScratch.Clear();
        return false;
    }

    void CreateDissolveToggle()
    {
        var renderers = new List<Renderer>();
        foreach (var r in dissolveTarget.GetComponentsInChildren<Renderer>(true))
            if (HasProperty(r, dissolveProperty)) renderers.Add(r);

        if (renderers.Count == 0) return;

        var go = NewChild(dissolveTarget.transform, "Dissolve Toggle");
        if (!AddFeature(go, T_TOGGLE, out var comp, out var feature)) return;
        TrySet(feature, "name", dissolveMenuPath);
        TrySet(feature, "defaultOn", dissolveDefaultOn);
        TrySet(feature, "hasTransition", true);
        TrySet(feature, "simpleOutTransition", true);
        TrySet(feature, "expandIntoTransition", true);
        TrySet(feature, "transitionTimeIn", dissolveTime);
        TrySet(feature, "transitionTimeOut", dissolveTime);

        var transitionIn = EnsureState(feature, "transitionStateIn");
        var mainState = EnsureState(feature, "state");

        AddMaterialActions(transitionIn, renderers, dissolveHiddenValue);
        AddObjectOnAction(mainState, dissolveTarget);
        AddMaterialActions(mainState, renderers, dissolveShownValue);
        AssignRenderers(comp, renderers);

        EditorUtility.SetDirty(comp);
        overviewDirty = true;
        Selection.activeObject = go;
    }

    void AssignRenderers(Component comp, List<Renderer> renderers)
    {
        var so = new SerializedObject(comp);
        so.Update();
        foreach (var listPath in StateListPaths)
        {
            var list = so.FindProperty(listPath);
            if (list == null || !list.isArray) continue;
            int next = 0;
            for (int i = 0; i < list.arraySize; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                var rp = el.FindPropertyRelative("renderer");
                var pp = el.FindPropertyRelative("propertyName");
                if (rp == null || pp == null || pp.stringValue != dissolveProperty) continue;
                if (next < renderers.Count) rp.objectReferenceValue = renderers[next++];
            }
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(comp);
    }

    void AddMaterialActions(object state, List<Renderer> renderers, float value)
    {
        var actionType = FindType(T_MATERIAL_ACTION);
        var actions = GetMember(state, "actions") as System.Collections.IList;
        if (actionType == null || actions == null) return;
        for (int i = 0; i < renderers.Count; i++)
        {
            var a = Activator.CreateInstance(actionType);
            TrySet(a, "renderer", renderers[i]);
            TrySet(a, "propertyName", dissolveProperty);
            TrySet(a, "value", value);
            actions.Add(a);
        }
    }

    static void AddObjectOnAction(object state, GameObject obj)
    {
        var t = FindType(T_OBJECT_TOGGLE);
        var actions = GetMember(state, "actions") as System.Collections.IList;
        if (t == null || actions == null) return;
        var a = Activator.CreateInstance(t);
        TrySet(a, "obj", obj);
        if (FindMember(t, "mode") is FieldInfo modeField && modeField.FieldType.IsEnum)
        {
            try { modeField.SetValue(a, Enum.Parse(modeField.FieldType, "TurnOn")); } catch { }
        }
        actions.Add(a);
    }

    void RebuildBlendshapeCache()
    {
        bsDirty = false;
        foreach (var e in bsEntries) if (e.smr != null) bsFoldouts[e.smr] = e.open;
        bsEntries.Clear();

        foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh = smr.sharedMesh;
            if (mesh == null || mesh.blendShapeCount == 0) continue;
            var names = new string[mesh.blendShapeCount];
            for (int i = 0; i < names.Length; i++) names[i] = mesh.GetBlendShapeName(i);
            bsFoldouts.TryGetValue(smr, out var open);
            bsEntries.Add(new BsEntry { smr = smr, names = names, open = open });
        }
    }

    static void ApplyBlendshapeFilter(BsEntry e, string filter)
    {
        e.appliedFilter = filter;
        e.visible.Clear();
        bool all = string.IsNullOrEmpty(filter);
        for (int i = 0; i < e.names.Length; i++)
            if (all || e.names[i].IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) e.visible.Add(i);
        e.label = e.smr.name + " (" + e.visible.Count + ")";
    }

    void DrawBlendshapes()
    {
        BeginCard(CardIcon.Blendshapes, "Blendshapes", "Search mesh blendshapes, preview weights, and generate toggles.");
        bsFilter = EditorGUILayout.TextField(bsFilter, styleInputBox) ?? "";
        DrawInfoBox("Adjust a slider to preview the shape in Scene view. Create a toggle from the current weight with the Toggle button.");

        if (bsDirty) RebuildBlendshapeCache();

        bool filtering = bsFilter.Length > 0;
        var evt = Event.current;
        float viewTop = scroll.y, viewBottom = scroll.y + position.height;

        foreach (var e in bsEntries)
        {
            var smr = e.smr;
            if (smr == null) { bsDirty = true; continue; }
            if (!ReferenceEquals(e.appliedFilter, bsFilter) && e.appliedFilter != bsFilter) ApplyBlendshapeFilter(e, bsFilter);
            int count = e.visible.Count;
            if (count == 0) continue;

            bool shown = EditorGUILayout.Foldout(e.open || filtering, e.label, true);
            if (!filtering) e.open = shown;
            if (!shown) continue;

            var block = GUILayoutUtility.GetRect(0f, count * BsRowHeight, GUILayout.ExpandWidth(true));
            if (evt.type != EventType.Layout)
            {
                int first = Mathf.Max(0, Mathf.FloorToInt((viewTop - block.y) / BsRowHeight));
                int last = Mathf.Min(count - 1, Mathf.CeilToInt((viewBottom - block.y) / BsRowHeight));
                for (int k = first; k <= last; k++)
                {
                    int idx = e.visible[k];
                    float y = block.y + k * BsRowHeight;
                    var sliderRect = new Rect(block.x, y, block.width - 64f, BsRowHeight - 2f);
                    var buttonRect = new Rect(block.xMax - 60f, y, 60f, BsRowHeight - 2f);

                    EditorGUI.BeginChangeCheck();
                    float w = EditorGUI.Slider(sliderRect, e.names[idx], smr.GetBlendShapeWeight(idx), 0f, 100f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(smr, "Blendshape preview");
                        smr.SetBlendShapeWeight(idx, w);
                    }
                    if (GUI.Button(buttonRect, "Toggle", styleBtnGrad))
                    {
                        string shapeName = e.names[idx];
                        float shapeValue = w <= 0f ? 100f : w;
                        Run("Create blendshape toggle", () => CreateBlendshapeToggle(smr, shapeName, shapeValue));
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }
        EndCard();
    }

    void CreateBlendshapeToggle(SkinnedMeshRenderer smr, string shape, float value)
    {
        var go = NewChild(smr.transform, $"Toggle {shape}");
        if (!AddFeature(go, T_TOGGLE, out var comp, out var feature)) return;
        TrySet(feature, "name", $"Blendshapes/{shape}");

        try
        {
            var state = GetMember(feature, "state");
            var actions = GetMember(state, "actions") as System.Collections.IList;
            var at = FindType(T_BLENDSHAPE_ACTION);
            if (actions != null && at != null)
            {
                var action = Activator.CreateInstance(at);
                TrySet(action, "blendShape", shape);
                TrySet(action, "blendShapeValue", value);
                TrySet(action, "renderer", smr);
                actions.Add(action);
            }
        }
        catch { }

        EditorUtility.SetDirty(comp);
        overviewDirty = true;
        Selection.activeObject = go;
    }

    void DrawMaterialSwap()
    {
        BeginCard(CardIcon.Sparkle, "Material Swap", "Create a VRCFury menu toggle that swaps one renderer material slot.");
        DrawInfoBox("Choose a renderer, material slot and replacement material. VRCFury will restore the avatar's original material when the toggle is off.");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Renderer", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        materialSwapRenderer = (Renderer)DrawThemedObjectField(materialSwapRenderer, typeof(Renderer), true, GUILayout.Height(24));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        int materialCount = materialSwapRenderer != null && materialSwapRenderer.sharedMaterials != null
            ? materialSwapRenderer.sharedMaterials.Length : 0;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Material slot", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        materialSwapSlot = EditorGUILayout.IntField(materialSwapSlot, styleNumberField);
        if (materialCount > 0)
        {
            materialSwapSlot = Mathf.Clamp(materialSwapSlot, 0, materialCount - 1);
            EditorGUILayout.LabelField("0 - " + (materialCount - 1), styleCardSubtitle, GUILayout.Width(54));
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Swap material", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        materialSwapMaterial = (Material)DrawThemedObjectField(materialSwapMaterial, typeof(Material), false, GUILayout.Height(24));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Menu path", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        materialSwapMenuPath = EditorGUILayout.TextField(materialSwapMenuPath, styleInputBox);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Default On", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        materialSwapDefaultOn = EditorGUILayout.Toggle(materialSwapDefaultOn, GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(14);

        bool rendererValid = materialSwapRenderer != null &&
                             avatar != null &&
                             materialSwapRenderer.transform.IsChildOf(avatar.transform);
        bool slotValid = materialCount > 0 && materialSwapSlot >= 0 && materialSwapSlot < materialCount;

        using (new EditorGUI.DisabledScope(!rendererValid || !slotValid || materialSwapMaterial == null))
        {
            if (GUILayout.Button("Create Material Swap Toggle", styleBtnGrad, GUILayout.Height(42)))
                Run("Create Material Swap Toggle", CreateMaterialSwapToggle);
        }

        if (materialSwapRenderer != null && !rendererValid)
            EditorGUILayout.HelpBox("The renderer must be inside the selected avatar.", MessageType.Warning);
        else if (materialSwapRenderer != null && materialCount == 0)
            EditorGUILayout.HelpBox("The selected renderer has no material slots.", MessageType.Warning);

        EndCard();
    }

    void CreateMaterialSwapToggle()
    {
        if (avatar == null || materialSwapRenderer == null || materialSwapMaterial == null) return;
        if (!materialSwapRenderer.transform.IsChildOf(avatar.transform))
            throw new Exception("The selected renderer is not inside the selected avatar.");

        var mats = materialSwapRenderer.sharedMaterials;
        if (mats == null || materialSwapSlot < 0 || materialSwapSlot >= mats.Length)
            throw new Exception("The selected material slot does not exist.");

        string menuPath = string.IsNullOrWhiteSpace(materialSwapMenuPath)
            ? "Materials/Swap"
            : materialSwapMenuPath.Trim();

        var host = NewChild(materialSwapRenderer.transform, "Material Swap - " + materialSwapMaterial.name);
        if (!AddFeature(host, T_TOGGLE, out var comp, out var feature))
            throw new Exception("Could not create the VRCFury Toggle component.");

        TrySet(feature, "name", menuPath);
        TrySet(feature, "defaultOn", materialSwapDefaultOn);

        var state = EnsureState(feature, "state");
        if (state == null)
            throw new Exception("Could not access the VRCFury toggle state.");

        if (!AddMaterialSwapAction(state, materialSwapRenderer, materialSwapSlot, materialSwapMaterial))
        {
            Undo.DestroyObjectImmediate(host);
            throw new Exception(
                "This VRCFury version's Material Swap action could not be configured. " +
                "Update VRCFury and try again.");
        }

        EditorUtility.SetDirty(comp);
        overviewDirty = true;
        Selection.activeObject = host;
        EditorGUIUtility.PingObject(host);
    }

    static bool AddMaterialSwapAction(object state, Renderer renderer, int slot, Material material)
    {
        // VRCFury has used MaterialAction for the built-in Material Swap action.
        // Keep fallbacks so this helper remains usable across package revisions.
        string[] candidates =
        {
            T_MATERIAL_SWAP_ACTION,
            "VF.Model.StateAction.MaterialSwapAction",
            "VF.Model.StateAction.MaterialReplacementAction"
        };

        Type actionType = null;
        foreach (var name in candidates)
        {
            actionType = FindType(name);
            if (actionType != null) break;
        }

        var actions = GetMember(state, "actions") as System.Collections.IList;
        if (actionType == null || actions == null) return false;

        var action = Activator.CreateInstance(actionType);

        bool rendererSet =
            TrySet(action, "renderer", renderer, warn: false) ||
            TrySet(action, "render", renderer, warn: false);

        bool slotSet =
            TrySet(action, "slot", slot, warn: false) ||
            TrySet(action, "materialSlot", slot, warn: false) ||
            TrySet(action, "index", slot, warn: false);

        bool materialSet =
            TrySet(action, "material", material, warn: false) ||
            TrySet(action, "mat", material, warn: false) ||
            TrySet(action, "replacement", material, warn: false);

        if (!rendererSet || !slotSet || !materialSet) return false;
        actions.Add(action);
        return true;
    }

    void DrawOutfitBuilder()
    {
        BeginCard(CardIcon.Sparkle, "Outfit Builder", "Combine outfit objects, material swaps, blendshapes and shader values into one VRCFury toggle.");
        DrawInfoBox("Everything in this list is applied by one VRCFury menu toggle. Add as many actions as you need for a complete outfit preset.");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Menu path", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        outfitMenuPath = EditorGUILayout.TextField(outfitMenuPath, styleInputBox);
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Default On", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        outfitDefaultOn = EditorGUILayout.Toggle(outfitDefaultOn, GUILayout.Width(20));
        GUILayout.Space(18);
        EditorGUILayout.LabelField("Menu icon", styleFieldLabel, GUILayout.Width(70));
        outfitIcon = (Texture2D)DrawThemedObjectField(outfitIcon, typeof(Texture2D), false, GUILayout.Width(58), GUILayout.Height(58));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Add Action", new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = ColLavenderText } });
        BeginResponsiveButtonRow();
        if (GUILayout.Button("+ Object", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
            outfitActions.Add(new OutfitActionEntry { type = OutfitActionType.Object });
        ResponsiveButtonGap();
        if (GUILayout.Button("+ Material Swap", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
            outfitActions.Add(new OutfitActionEntry { type = OutfitActionType.MaterialSwap });
        ResponsiveButtonGap();
        if (GUILayout.Button("+ Blendshape", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
            outfitActions.Add(new OutfitActionEntry { type = OutfitActionType.Blendshape });
        ResponsiveButtonGap();
        if (GUILayout.Button("+ Material Value", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
            outfitActions.Add(new OutfitActionEntry { type = OutfitActionType.MaterialProperty });
        EndResponsiveButtonRow();

        GUILayout.Space(12);
        int remove = -1;
        for (int i = 0; i < outfitActions.Count; i++)
        {
            var a = outfitActions[i];
            EditorGUILayout.BeginVertical(styleRowBg);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField((i + 1) + ". " + OutfitActionLabel(a.type), styleFieldLabel);
            if (GUILayout.Button("x", styleBtnRemove, GUILayout.Width(28), GUILayout.Height(24))) remove = i;
            EditorGUILayout.EndHorizontal();

            switch (a.type)
            {
                case OutfitActionType.Object:
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Object", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
                    a.obj = (GameObject)DrawThemedObjectField(a.obj, typeof(GameObject), true);
                    EditorGUILayout.EndHorizontal();
                    a.objectOn = EditorGUILayout.Toggle("Turn On", a.objectOn);
                    break;
                case OutfitActionType.MaterialSwap:
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Renderer", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
                    a.renderer = (Renderer)DrawThemedObjectField(a.renderer, typeof(Renderer), true);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Material slot", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
                    a.materialSlot = EditorGUILayout.IntField(a.materialSlot, styleNumberField);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("New material", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
                    a.material = (Material)DrawThemedObjectField(a.material, typeof(Material), false);
                    EditorGUILayout.EndHorizontal();
                    break;
                case OutfitActionType.Blendshape:
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Renderer", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
                    a.smr = (SkinnedMeshRenderer)DrawThemedObjectField(a.smr, typeof(SkinnedMeshRenderer), true);
                    EditorGUILayout.EndHorizontal();
                    DrawOutfitBlendshapePicker(a);
                    a.blendshapeValue = EditorGUILayout.Slider("Value", a.blendshapeValue, 0f, 100f);
                    break;
                case OutfitActionType.MaterialProperty:
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Renderer", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
                    a.renderer = (Renderer)DrawThemedObjectField(a.renderer, typeof(Renderer), true);
                    EditorGUILayout.EndHorizontal();
                    a.property = EditorGUILayout.TextField("Shader property", a.property, styleInputBox);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Value", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
                    a.propertyValue = EditorGUILayout.FloatField(a.propertyValue, styleNumberField);
                    EditorGUILayout.EndHorizontal();
                    break;
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(6);
        }
        if (remove >= 0) outfitActions.RemoveAt(remove);

        if (outfitActions.Count == 0)
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField("No actions added yet", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = ColLavenderText } });
            EditorGUILayout.LabelField("Add outfit objects or effects above.", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = ColTextMuted } });
        }

        GUILayout.Space(14);
        BeginResponsiveButtonRow();
        using (new EditorGUI.DisabledScope(outfitActions.Count == 0))
        {
            if (GUILayout.Button("Clear", styleBtnDark, GUILayout.Height(40), GUILayout.ExpandWidth(true))) outfitActions.Clear();
        }
        ResponsiveButtonGap(8f);
        using (new EditorGUI.DisabledScope(outfitActions.Count == 0 || string.IsNullOrWhiteSpace(outfitMenuPath)))
        {
            if (GUILayout.Button("Create Outfit Toggle", styleBtnGrad, GUILayout.Height(40), GUILayout.ExpandWidth(true)))
                Run("Create Outfit Toggle", CreateOutfitToggle);
        }
        EndResponsiveButtonRow();
        EndCard();
    }

    static string OutfitActionLabel(OutfitActionType type)
    {
        switch (type)
        {
            case OutfitActionType.Object: return "Object Toggle";
            case OutfitActionType.MaterialSwap: return "Material Swap";
            case OutfitActionType.Blendshape: return "Blendshape";
            default: return "Material Value";
        }
    }

    void DrawOutfitBlendshapePicker(OutfitActionEntry a)
    {
        if (a.smr == null || a.smr.sharedMesh == null || a.smr.sharedMesh.blendShapeCount == 0)
        {
            EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Blendshape", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
                    a.blendshape = EditorGUILayout.TextField(a.blendshape ?? "", styleInputBox);
                    EditorGUILayout.EndHorizontal();
            return;
        }
        var mesh = a.smr.sharedMesh;
        string[] names = new string[mesh.blendShapeCount];
        int selected = 0;
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = mesh.GetBlendShapeName(i);
            if (names[i] == a.blendshape) selected = i;
        }
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Blendshape", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        selected = EditorGUILayout.Popup(
            selected,
            names,
            stylePopup,
            GUILayout.Height(24));
        EditorGUILayout.EndHorizontal();
        a.blendshape = names[selected];
    }

    void CreateOutfitToggle()
    {
        if (avatar == null) throw new Exception("Select an avatar first.");
        string path = outfitMenuPath.Trim();
        string leaf = path;
        int slash = leaf.LastIndexOf('/');
        if (slash >= 0 && slash + 1 < leaf.Length) leaf = leaf.Substring(slash + 1);

        var host = NewChild(avatar.transform, "Outfit Toggle - " + leaf);
        if (!AddFeature(host, T_TOGGLE, out var comp, out var feature))
            throw new Exception("Could not create the VRCFury Toggle component.");
        TrySet(feature, "name", path);
        TrySet(feature, "defaultOn", outfitDefaultOn);
        if (outfitIcon != null)
        {
            bool iconSet = TrySet(feature, "icon", outfitIcon, false)
                        || TrySet(feature, "menuIcon", outfitIcon, false)
                        || TrySet(feature, "iconTexture", outfitIcon, false);
            if (!iconSet) Debug.LogWarning("[AvatarTools] Could not find the VRCFury menu icon field for the Outfit Builder toggle.");
        }

        var state = EnsureState(feature, "state");
        if (state == null) { Undo.DestroyObjectImmediate(host); throw new Exception("Could not access the VRCFury toggle state."); }

        foreach (var a in outfitActions)
        {
            switch (a.type)
            {
                case OutfitActionType.Object:
                    if (a.obj == null) continue;
                    if (!a.obj.transform.IsChildOf(avatar.transform) && a.obj != avatar.gameObject) continue;
                    AddObjectAction(state, a.obj, a.objectOn);
                    break;
                case OutfitActionType.MaterialSwap:
                    if (a.renderer == null || a.material == null) continue;
                    if (!AddMaterialSwapAction(state, a.renderer, a.materialSlot, a.material))
                        Debug.LogWarning("[AvatarTools] Outfit Builder could not add one Material Swap action.");
                    break;
                case OutfitActionType.Blendshape:
                    AddOutfitBlendshapeAction(state, a.smr, a.blendshape, a.blendshapeValue);
                    break;
                case OutfitActionType.MaterialProperty:
                    AddOutfitMaterialPropertyAction(state, a.renderer, a.property, a.propertyValue);
                    break;
            }
        }

        EditorUtility.SetDirty(comp);
        overviewDirty = true;
        Selection.activeObject = host;
        EditorGUIUtility.PingObject(host);
    }

    static void AddObjectAction(object state, GameObject obj, bool turnOn)
    {
        var t = FindType(T_OBJECT_TOGGLE);
        var actions = GetMember(state, "actions") as System.Collections.IList;
        if (t == null || actions == null || obj == null) return;
        var action = Activator.CreateInstance(t);
        TrySet(action, "obj", obj);
        var modeMember = FindMember(t, "mode");
        Type modeType = (modeMember as FieldInfo)?.FieldType ?? (modeMember as PropertyInfo)?.PropertyType;
        if (modeType != null && modeType.IsEnum)
        {
            try { TrySet(action, "mode", Enum.Parse(modeType, turnOn ? "TurnOn" : "TurnOff"), false); } catch { }
        }
        actions.Add(action);
    }

    static void AddOutfitBlendshapeAction(object state, SkinnedMeshRenderer smr, string shape, float value)
    {
        if (smr == null || string.IsNullOrEmpty(shape)) return;
        var t = FindType(T_BLENDSHAPE_ACTION);
        var actions = GetMember(state, "actions") as System.Collections.IList;
        if (t == null || actions == null) return;
        var action = Activator.CreateInstance(t);
        TrySet(action, "blendShape", shape);
        TrySet(action, "blendShapeValue", value);
        TrySet(action, "renderer", smr);
        actions.Add(action);
    }

    static void AddOutfitMaterialPropertyAction(object state, Renderer renderer, string property, float value)
    {
        if (renderer == null || string.IsNullOrWhiteSpace(property)) return;
        var t = FindType(T_MATERIAL_ACTION);
        var actions = GetMember(state, "actions") as System.Collections.IList;
        if (t == null || actions == null) return;
        var action = Activator.CreateInstance(t);
        TrySet(action, "renderer", renderer);
        TrySet(action, "propertyName", property.Trim());
        TrySet(action, "value", value);
        actions.Add(action);
    }
}
