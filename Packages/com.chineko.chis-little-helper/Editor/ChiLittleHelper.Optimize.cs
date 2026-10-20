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
    void RebuildOverviewCache()
    {
        overviewDirty = false;
        overviewEntries.Clear();
        overviewToggleEdits.Clear();
        overviewMenuEdits.Clear();

        if (avatar == null)
            return;

        Type vfType = FindType(T_VRCFURY);
        Type toggleType = FindType(T_TOGGLE);
        if (vfType == null)
            return;

        foreach (Component c in avatar.GetComponentsInChildren(vfType, true).Cast<Component>())
        {
            if (c == null)
                continue;

            object feature = GetMember(c, "content");
            Type featureType = feature != null ? feature.GetType() : null;
            string featureLabel = featureType != null ? featureType.Name : "(empty)";

            bool isToggle =
                feature != null &&
                toggleType != null &&
                (toggleType.IsInstanceOfType(feature) ||
                 string.Equals(featureType.FullName, T_TOGGLE, StringComparison.Ordinal));

            string menuPath = "";
            string toggleName = "";

            if (isToggle)
            {
                string fullPath = NormalizeOverviewPath(ReadStringMember(feature, "name"));
                SplitOverviewTogglePath(fullPath, out menuPath, out toggleName);

                overviewToggleEdits[c.GetInstanceID()] = new OverviewToggleEdit
                {
                    menuPath = menuPath,
                    name = toggleName
                };
            }

            overviewEntries.Add(new OverviewEntry
            {
                comp = c,
                feature = feature,
                featureLabel = featureLabel,
                isToggle = isToggle,
                menuPath = menuPath,
                toggleName = toggleName
            });
        }
    }

    string NormalizeOverviewPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        return string.Join(
            "/",
            path.Replace('\\', '/')
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0));
    }

    void SplitOverviewTogglePath(string fullPath, out string menuPath, out string toggleName)
    {
        string normalized = NormalizeOverviewPath(fullPath);
        int slash = normalized.LastIndexOf('/');

        if (slash < 0)
        {
            menuPath = "";
            toggleName = normalized;
            return;
        }

        menuPath = normalized.Substring(0, slash);
        toggleName = normalized.Substring(slash + 1);
    }

    void SplitOverviewMenuPath(string menuPath, out string parentPath, out string menuName)
    {
        string normalized = NormalizeOverviewPath(menuPath);
        int slash = normalized.LastIndexOf('/');

        if (slash < 0)
        {
            parentPath = "";
            menuName = normalized;
            return;
        }

        parentPath = normalized.Substring(0, slash);
        menuName = normalized.Substring(slash + 1);
    }

    string BuildOverviewTogglePath(string menuPath, string toggleName)
    {
        string menu = NormalizeOverviewPath(menuPath);
        string name = (toggleName ?? "").Trim();

        return menu.Length == 0
            ? name
            : menu + "/" + name;
    }

    string BuildOverviewMenuPath(string parentPath, string menuName)
    {
        string parent = NormalizeOverviewPath(parentPath);
        string name = (menuName ?? "").Trim();

        if (name.Length == 0)
            return parent;

        return parent.Length == 0
            ? name
            : parent + "/" + name;
    }

    OverviewMenuEdit GetOverviewMenuEdit(string menuPath)
    {
        string key = NormalizeOverviewPath(menuPath);

        if (!overviewMenuEdits.TryGetValue(key, out OverviewMenuEdit edit))
        {
            SplitOverviewMenuPath(key, out string parentPath, out string menuName);
            edit = new OverviewMenuEdit
            {
                parentPath = parentPath,
                name = menuName
            };
            overviewMenuEdits[key] = edit;
        }

        return edit;
    }

    void DrawOverviewEditField(string label, ref string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, styleFieldLabel, GUILayout.Width(IsCompactLayout ? 82f : 96f));
        value = EditorGUILayout.TextField(value ?? "", styleInputBox, GUILayout.Height(24), GUILayout.MinWidth(0));
        EditorGUILayout.EndHorizontal();
    }

    bool IsOverviewMenuDescendant(string candidateMenu, string parentMenu)
    {
        string candidate = NormalizeOverviewPath(candidateMenu);
        string parent = NormalizeOverviewPath(parentMenu);

        if (parent.Length == 0)
            return candidate.Length == 0;

        return string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase);
    }

    void ApplyOverviewToggleEdit(int entryIndex, string menuPath, string toggleName)
    {
        if (entryIndex < 0 || entryIndex >= overviewEntries.Count)
            return;

        OverviewEntry entry = overviewEntries[entryIndex];
        if (entry == null || entry.comp == null || !entry.isToggle || entry.feature == null)
            return;

        string name = (toggleName ?? "").Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("Toggle Name cannot be empty.");

        string fullPath = BuildOverviewTogglePath(menuPath, name);

        Undo.RecordObject(entry.comp, "Edit VRCFury Toggle Path");

        if (!TrySet(entry.feature, "name", fullPath, warn: false))
            throw new MissingFieldException(
                "This VRCFury version does not expose the Toggle name/path member expected by Overview++.");

        EditorUtility.SetDirty(entry.comp);
        PrefabUtility.RecordPrefabInstancePropertyModifications(entry.comp);

        parameterMemoryDirty = true;
        preflightDirty = true;
    }

    void ApplyOverviewMenuEdit(string oldMenuPath, string parentPath, string menuName)
    {
        string oldPath = NormalizeOverviewPath(oldMenuPath);
        string newPath = BuildOverviewMenuPath(parentPath, menuName);

        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            return;

        List<OverviewEntry> affected = overviewEntries
            .Where(x =>
                x != null &&
                x.comp != null &&
                x.isToggle &&
                x.feature != null &&
                IsOverviewMenuDescendant(x.menuPath, oldPath))
            .ToList();

        if (affected.Count == 0)
            return;

        foreach (OverviewEntry entry in affected)
        {
            string currentMenu = NormalizeOverviewPath(entry.menuPath);
            string targetMenu;

            if (oldPath.Length == 0)
            {
                // Root is not a real named menu. Only move root-level toggles.
                if (currentMenu.Length != 0)
                    continue;

                targetMenu = newPath;
            }
            else if (string.Equals(currentMenu, oldPath, StringComparison.OrdinalIgnoreCase))
            {
                targetMenu = newPath;
            }
            else
            {
                string suffix = currentMenu.Substring(oldPath.Length).TrimStart('/');
                targetMenu =
                    newPath.Length == 0
                        ? suffix
                        : (suffix.Length == 0 ? newPath : newPath + "/" + suffix);
            }

            string fullPath = BuildOverviewTogglePath(targetMenu, entry.toggleName);

            Undo.RecordObject(entry.comp, "Edit VRCFury Menu Path");

            if (!TrySet(entry.feature, "name", fullPath, warn: false))
                throw new MissingFieldException(
                    "This VRCFury version does not expose the Toggle name/path member expected by Overview++.");

            EditorUtility.SetDirty(entry.comp);
            PrefabUtility.RecordPrefabInstancePropertyModifications(entry.comp);
        }

        parameterMemoryDirty = true;
        preflightDirty = true;
    }

    void DrawOverview()
    {
        BeginCard(
            CardIcon.Overview,
            "VRCFury Overview++",
            "Browse VRCFury toggles by their menu path, edit toggle/menu paths and names, and manage the underlying feature components.");

        DrawHelpHint(
            "Toggle menus are inferred from the VRCFury Toggle name path. Example: 'Outfits/Jacket' is grouped under menu 'Outfits' with toggle name 'Jacket'. Menu edits cascade into nested child menus.");

        BeginResponsiveButtonRow();
        if (GUILayout.Button("Refresh", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
        {
            Run("Refresh VRCFury Overview", RebuildOverviewCache);
        }

        ResponsiveButtonGap(8f);

        using (new EditorGUI.DisabledScope(overviewDirty || overviewEntries.Count == 0))
        {
            if (GUILayout.Button("Select First", styleBtnDark, GUILayout.Height(32), GUILayout.ExpandWidth(true)))
            {
                OverviewEntry first = overviewEntries.FirstOrDefault(x => x != null && x.comp != null);
                if (first != null)
                {
                    Selection.activeObject = first.comp.gameObject;
                    EditorGUIUtility.PingObject(first.comp.gameObject);
                }
            }
        }
        EndResponsiveButtonRow();

        GUILayout.Space(10);

        DrawDataFreshnessBanner(
            "Overview++ cache",
            overviewDirty,
            "Overview++ matches the current avatar hierarchy.",
            () => Run("Refresh VRCFury Overview", RebuildOverviewCache),
            "Refresh Overview");

        if (overviewDirty)
        {
            EndCard();
            return;
        }

        GUILayout.Space(8);

        int toggleCount = overviewEntries.Count(x => x != null && x.comp != null && x.isToggle);
        int otherCount = overviewEntries.Count(x => x != null && x.comp != null && !x.isToggle);

        EditorGUILayout.LabelField(
            toggleCount + " toggle" + (toggleCount == 1 ? "" : "s") +
            " · " + otherCount + " other VRCFury feature" + (otherCount == 1 ? "" : "s"),
            new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColTextPrimary }
            });

        GUILayout.Space(8);

        int removeAt = -1;
        int duplicateAt = -1;
        int applyToggleAt = -1;
        string applyToggleMenu = null;
        string applyToggleName = null;

        bool applyMenu = false;
        string applyOldMenu = null;
        string applyMenuParent = null;
        string applyMenuName = null;

        List<int> toggleIndexes = Enumerable.Range(0, overviewEntries.Count)
            .Where(i =>
                overviewEntries[i] != null &&
                overviewEntries[i].comp != null &&
                overviewEntries[i].isToggle)
            .ToList();

        if (toggleIndexes.Count > 0)
        {
            EditorGUILayout.LabelField(
                "Toggle Menus",
                new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    normal = { textColor = ColLavenderText }
                });

            GUILayout.Space(4);

            foreach (IGrouping<string, int> group in toggleIndexes
                .GroupBy(i => NormalizeOverviewPath(overviewEntries[i].menuPath), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key.Length == 0 ? "" : g.Key, StringComparer.OrdinalIgnoreCase))
            {
                string groupPath = group.Key;
                List<int> indexes = group.ToList();
                OverviewMenuEdit menuEdit = GetOverviewMenuEdit(groupPath);

                EditorGUILayout.BeginVertical(styleRowBg);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    groupPath.Length == 0 ? "Root Menu" : groupPath,
                    new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12,
                        normal = { textColor = ColLavenderText }
                    });

                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField(
                    indexes.Count + " toggle" + (indexes.Count == 1 ? "" : "s"),
                    styleCardSubtitle,
                    GUILayout.Width(74));
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(5);

                DrawOverviewEditField("Menu Name", ref menuEdit.name);
                GUILayout.Space(3);
                DrawOverviewEditField("Parent Path", ref menuEdit.parentPath);

                string previewMenuPath = BuildOverviewMenuPath(menuEdit.parentPath, menuEdit.name);
                EditorGUILayout.LabelField(
                    "Full menu path: " + (previewMenuPath.Length == 0 ? "(Root)" : previewMenuPath),
                    styleCardSubtitle);

                bool menuChanged =
                    !string.Equals(
                        NormalizeOverviewPath(groupPath),
                        NormalizeOverviewPath(previewMenuPath),
                        StringComparison.OrdinalIgnoreCase);

                using (new EditorGUI.DisabledScope(!menuChanged))
                {
                    if (GUILayout.Button(
                        groupPath.Length == 0 ? "Move Root Toggles to Menu" : "Apply Menu Path / Name",
                        styleBtnGrad,
                        GUILayout.Height(28),
                        GUILayout.ExpandWidth(true)))
                    {
                        applyMenu = true;
                        applyOldMenu = groupPath;
                        applyMenuParent = menuEdit.parentPath;
                        applyMenuName = menuEdit.name;
                    }
                }

                GUILayout.Space(7);

                foreach (int index in indexes)
                {
                    OverviewEntry entry = overviewEntries[index];
                    int id = entry.comp.GetInstanceID();

                    if (!overviewToggleEdits.TryGetValue(id, out OverviewToggleEdit edit))
                    {
                        edit = new OverviewToggleEdit
                        {
                            menuPath = entry.menuPath,
                            name = entry.toggleName
                        };
                        overviewToggleEdits[id] = edit;
                    }

                    EditorGUILayout.BeginVertical(texInfoBox != null
                        ? new GUIStyle
                        {
                            normal = { background = texInfoBox },
                            border = new RectOffset(8, 8, 8, 8),
                            padding = new RectOffset(8, 8, 7, 7),
                            margin = new RectOffset(0, 0, 2, 2)
                        }
                        : styleRowBg);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(
                        string.IsNullOrWhiteSpace(entry.toggleName) ? "(Unnamed Toggle)" : entry.toggleName,
                        new GUIStyle(EditorStyles.boldLabel)
                        {
                            normal = { textColor = ColTextPrimary }
                        });
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(entry.featureLabel, styleCardSubtitle, GUILayout.Width(78));
                    EditorGUILayout.EndHorizontal();

                    GUILayout.Space(3);
                    DrawOverviewEditField("Toggle Name", ref edit.name);
                    GUILayout.Space(3);
                    DrawOverviewEditField("Menu Path", ref edit.menuPath);

                    string previewTogglePath = BuildOverviewTogglePath(edit.menuPath, edit.name);
                    EditorGUILayout.LabelField(
                        "Full path: " + (previewTogglePath.Length == 0 ? "(empty)" : previewTogglePath),
                        styleCardSubtitle);

                    GUILayout.Space(4);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Host Object", styleFieldLabel, GUILayout.Width(IsCompactLayout ? 82f : 96f));
                    string oldHostName = entry.comp.gameObject.name;
                    string newHostName = EditorGUILayout.TextField(
                        oldHostName,
                        styleInputBox,
                        GUILayout.Height(24),
                        GUILayout.MinWidth(0));

                    if (newHostName != oldHostName && !string.IsNullOrWhiteSpace(newHostName))
                    {
                        Undo.RecordObject(entry.comp.gameObject, "Rename VRCFury Feature Host");
                        entry.comp.gameObject.name = newHostName.Trim();
                        EditorUtility.SetDirty(entry.comp.gameObject);
                    }
                    EditorGUILayout.EndHorizontal();

                    bool toggleChanged =
                        !string.Equals(
                            NormalizeOverviewPath(entry.menuPath),
                            NormalizeOverviewPath(edit.menuPath),
                            StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(
                            entry.toggleName ?? "",
                            (edit.name ?? "").Trim(),
                            StringComparison.Ordinal);

                    GUILayout.Space(5);
                    BeginResponsiveButtonRow();

                    using (new EditorGUI.DisabledScope(
                        !toggleChanged || string.IsNullOrWhiteSpace(edit.name)))
                    {
                        if (GUILayout.Button(
                            "Apply Path / Name",
                            styleBtnGrad,
                            GUILayout.Height(28),
                            GUILayout.ExpandWidth(true)))
                        {
                            applyToggleAt = index;
                            applyToggleMenu = edit.menuPath;
                            applyToggleName = edit.name;
                        }
                    }

                    ResponsiveButtonGap(4f);

                    if (GUILayout.Button("Select", styleBtnDark, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
                    {
                        Selection.activeObject = entry.comp.gameObject;
                        EditorGUIUtility.PingObject(entry.comp.gameObject);
                    }

                    ResponsiveButtonGap(4f);

                    if (GUILayout.Button("Duplicate", styleBtnDark, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
                        duplicateAt = index;

                    ResponsiveButtonGap(4f);

                    if (GUILayout.Button("Delete", styleBtnRemove, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
                        removeAt = index;

                    EndResponsiveButtonRow();

                    EditorGUILayout.EndVertical();
                    GUILayout.Space(4);
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(8);
            }
        }

        List<int> otherIndexes = Enumerable.Range(0, overviewEntries.Count)
            .Where(i =>
                overviewEntries[i] != null &&
                overviewEntries[i].comp != null &&
                !overviewEntries[i].isToggle)
            .ToList();

        if (otherIndexes.Count > 0)
        {
            EditorGUILayout.LabelField(
                "Other VRCFury Features",
                new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    normal = { textColor = ColLavenderText }
                });

            GUILayout.Space(4);

            foreach (int index in otherIndexes)
            {
                OverviewEntry entry = overviewEntries[index];

                EditorGUILayout.BeginVertical(styleRowBg);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Host Object", styleFieldLabel, GUILayout.Width(IsCompactLayout ? 82f : 96f));
                string oldName = entry.comp.gameObject.name;
                string newName = EditorGUILayout.TextField(oldName, styleInputBox, GUILayout.Height(24), GUILayout.MinWidth(0));

                if (newName != oldName && !string.IsNullOrWhiteSpace(newName))
                {
                    Undo.RecordObject(entry.comp.gameObject, "Rename VRCFury Feature");
                    entry.comp.gameObject.name = newName.Trim();
                    EditorUtility.SetDirty(entry.comp.gameObject);
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);
                EditorGUILayout.LabelField(entry.featureLabel, styleCardSubtitle);

                GUILayout.Space(6);
                BeginResponsiveButtonRow();

                if (GUILayout.Button("Select", styleBtnDark, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
                {
                    Selection.activeObject = entry.comp.gameObject;
                    EditorGUIUtility.PingObject(entry.comp.gameObject);
                }

                ResponsiveButtonGap(5f);

                if (GUILayout.Button("Duplicate", styleBtnDark, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
                    duplicateAt = index;

                ResponsiveButtonGap(5f);

                if (GUILayout.Button("Delete", styleBtnRemove, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
                    removeAt = index;

                EndResponsiveButtonRow();

                EditorGUILayout.EndVertical();
                GUILayout.Space(6);
            }
        }

        if (applyMenu)
        {
            Run("Edit VRCFury Menu Path", () =>
            {
                ApplyOverviewMenuEdit(applyOldMenu, applyMenuParent, applyMenuName);
                RebuildOverviewCache();
            });
            GUIUtility.ExitGUI();
        }

        if (applyToggleAt >= 0)
        {
            int capturedIndex = applyToggleAt;
            string capturedMenu = applyToggleMenu;
            string capturedName = applyToggleName;

            Run("Edit VRCFury Toggle Path", () =>
            {
                ApplyOverviewToggleEdit(capturedIndex, capturedMenu, capturedName);
                RebuildOverviewCache();
            });
            GUIUtility.ExitGUI();
        }

        if (duplicateAt >= 0 && duplicateAt < overviewEntries.Count)
        {
            Component source = overviewEntries[duplicateAt].comp;

            if (source != null)
            {
                Run("Duplicate VRCFury Feature", () =>
                {
                    GameObject clone = Instantiate(source.gameObject, source.transform.parent);
                    clone.name = source.gameObject.name + " Copy";
                    Undo.RegisterCreatedObjectUndo(clone, "Duplicate VRCFury Feature");
                    clone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

                    overviewDirty = true;
                    doctorDirty = true;
                    parameterMemoryDirty = true;
                    preflightDirty = true;

                    Selection.activeObject = clone;
                    EditorGUIUtility.PingObject(clone);
                });
                GUIUtility.ExitGUI();
            }
        }

        if (removeAt >= 0 && removeAt < overviewEntries.Count)
        {
            Component victim = overviewEntries[removeAt].comp;

            if (victim != null)
            {
                bool deleteObject =
                    victim.gameObject != avatar.gameObject &&
                    victim.gameObject.GetComponents<Component>().Length <= 2 &&
                    victim.transform.childCount == 0;

                RunDestructive(
                    "Delete VRCFury Feature",
                    "Delete the selected VRCFury feature" +
                    (deleteObject ? " object" : " component") +
                    " from '" + victim.gameObject.name + "'?",
                    () =>
                    {
                        if (deleteObject)
                            Undo.DestroyObjectImmediate(victim.gameObject);
                        else
                            Undo.DestroyObjectImmediate(victim);

                        overviewDirty = true;
                        doctorDirty = true;
                        parameterMemoryDirty = true;
                        preflightDirty = true;
                    });

                GUIUtility.ExitGUI();
            }
        }

        EndCard();
    }

    void DrawTextureVramAnalyzer()
    {
        BeginCard(CardIcon.Sparkle, "Texture & VRAM Analyzer", "Find the textures used by this avatar and estimate their runtime GPU memory footprint.");

        if (IsCompactLayout)
        {
            if (GUILayout.Button("Analyze Textures", styleBtnGrad, GUILayout.Height(38), GUILayout.ExpandWidth(true)))
            {
                Run("Analyze Textures", () =>
                {
                    RebuildTextureVramCache();
                    textureVramDirty = false;
                });
            }

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search", styleFieldLabel, GUILayout.Width(48));
            textureSearch = EditorGUILayout.TextField(textureSearch, styleInputBox, GUILayout.MinWidth(0));
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Analyze Textures", styleBtnGrad, GUILayout.Height(38)))
            {
                Run("Analyze Textures", () =>
                {
                    RebuildTextureVramCache();
                    textureVramDirty = false;
                });
            }
            GUILayout.Space(8);
            EditorGUILayout.LabelField("Search", styleFieldLabel, GUILayout.Width(48));
            textureSearch = EditorGUILayout.TextField(textureSearch, styleInputBox);
            EditorGUILayout.EndHorizontal();
        }

        DrawDataFreshnessBanner(
            "Texture analysis",
            textureVramDirty,
            "Texture & VRAM results match the current avatar state.",
            () =>
            {
                Run("Analyze Textures", () =>
                {
                    RebuildTextureVramCache();
                    textureVramDirty = false;
                });
            },
            "Analyze Textures");

        GUILayout.Space(10);

        float totalMb = textureTotalBytes / (1024f * 1024f);
        int over4k = textureVramEntries.Count(x => x.texture != null && (x.texture.width > 4096 || x.texture.height > 4096));
        int over2k = textureVramEntries.Count(x => x.texture != null && (x.texture.width > 2048 || x.texture.height > 2048));

        if (IsCompactLayout)
        {
            EditorGUILayout.BeginHorizontal();
            DrawTextureStat("Textures", textureVramEntries.Count.ToString());
            GUILayout.Space(3);
            DrawTextureStat("Est. VRAM", totalMb.ToString("0.0") + " MB");
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            DrawTextureStat("> 2K", over2k.ToString());
            GUILayout.Space(3);
            DrawTextureStat("> 4K", over4k.ToString());
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            DrawTextureStat("Textures", textureVramEntries.Count.ToString());
            GUILayout.Space(5);
            DrawTextureStat("Est. VRAM", totalMb.ToString("0.0") + " MB");
            GUILayout.Space(5);
            DrawTextureStat("> 2K", over2k.ToString());
            GUILayout.Space(5);
            DrawTextureStat("> 4K", over4k.ToString());
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(10);
        DrawInfoBox("VRAM is an estimate based on the texture's current Unity runtime format and mipmaps. Platform overrides and GPU-specific storage can differ.");

        string q = (textureSearch ?? "").Trim();
        var filtered = textureVramEntries
            .Where(x => x.texture != null &&
                (q.Length == 0 ||
                 x.texture.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 x.usage.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderByDescending(x => x.estimatedBytes)
            .ToList();

        textureScroll = EditorGUILayout.BeginScrollView(textureScroll, GUILayout.MinHeight(160), GUILayout.MaxHeight(520));

        foreach (var e in filtered)
        {
            Texture tex = e.texture;
            float mb = e.estimatedBytes / (1024f * 1024f);

            EditorGUILayout.BeginVertical(styleRowBg);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(tex.name, new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColTextPrimary }
            });
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{tex.width}×{tex.height}", styleCardSubtitle, GUILayout.Width(85));
            EditorGUILayout.LabelField($"{mb:0.00} MB", styleCardSubtitle, GUILayout.Width(72));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                $"{e.materialCount} material{(e.materialCount == 1 ? "" : "s")} · {e.usage}",
                styleCardSubtitle);

            if (!string.IsNullOrEmpty(e.assetPath))
                EditorGUILayout.LabelField(e.assetPath, styleCardSubtitle);

            BeginResponsiveButtonRow();
            if (GUILayout.Button("Select", styleBtnDark, GUILayout.Height(26), GUILayout.ExpandWidth(true)))
            {
                Selection.activeObject = tex;
                EditorGUIUtility.PingObject(tex);
            }

            var importer = !string.IsNullOrEmpty(e.assetPath)
                ? AssetImporter.GetAtPath(e.assetPath) as TextureImporter
                : null;

            if (importer != null)
            {
                GUILayout.Space(4);
                using (new EditorGUI.DisabledScope(importer.maxTextureSize <= 2048))
                {
                    if (GUILayout.Button("Max 2048", styleBtnDark, ResponsiveButtonWidth(78), GUILayout.Height(26)))
                    {
                        RunDestructive(
                            "Set Texture Max Size 2048",
                            "Change the texture import Max Size to 2048? This reimports the texture asset.",
                            () =>
                            {
                                SetTextureMaxSize(importer, 2048);
                                textureVramDirty = true;
                                doctorDirty = true;
                            },
                            false);
                        GUIUtility.ExitGUI();
                    }
                }

                GUILayout.Space(4);
                using (new EditorGUI.DisabledScope(importer.maxTextureSize <= 1024))
                {
                    if (GUILayout.Button("Max 1024", styleBtnDark, ResponsiveButtonWidth(78), GUILayout.Height(26)))
                    {
                        RunDestructive(
                            "Set Texture Max Size 1024",
                            "Change the texture import Max Size to 1024? This reimports the texture asset.",
                            () =>
                            {
                                SetTextureMaxSize(importer, 1024);
                                textureVramDirty = true;
                                doctorDirty = true;
                            },
                            false);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            EndResponsiveButtonRow();
            EditorGUILayout.EndVertical();
            GUILayout.Space(6);
        }

        EditorGUILayout.EndScrollView();

        if (filtered.Count == 0)
            EditorGUILayout.HelpBox("No matching avatar textures were found.", MessageType.Info);

        EndCard();
    }

    void DrawTextureStat(string label, string value)
    {
        var box = new GUIStyle
        {
            normal = { background = texInfoBox },
            border = new RectOffset(8, 8, 8, 8),
            padding = new RectOffset(6, 6, 7, 7)
        };

        EditorGUILayout.BeginVertical(box, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField(value, new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            normal = { textColor = ColTextPrimary }
        });
        EditorGUILayout.LabelField(label, new GUIStyle(styleCardSubtitle)
        {
            alignment = TextAnchor.MiddleCenter
        });
        EditorGUILayout.EndVertical();
    }

    void RebuildTextureVramCache()
    {
        textureVramEntries.Clear();
        textureTotalBytes = 0;
        if (avatar == null) return;

        var usageMap = new Dictionary<Texture, HashSet<string>>();
        var materialMap = new Dictionary<Texture, HashSet<Material>>();

        foreach (Renderer r in avatar.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r.sharedMaterials == null) continue;

            foreach (Material mat in r.sharedMaterials)
            {
                if (mat == null) continue;

                string[] props;
                try { props = mat.GetTexturePropertyNames(); }
                catch { continue; }

                foreach (string prop in props)
                {
                    Texture tex = null;
                    try { tex = mat.GetTexture(prop); }
                    catch { }
                    if (tex == null) continue;

                    if (!usageMap.TryGetValue(tex, out var usages))
                    {
                        usages = new HashSet<string>();
                        usageMap[tex] = usages;
                    }
                    usages.Add(prop);

                    if (!materialMap.TryGetValue(tex, out var mats))
                    {
                        mats = new HashSet<Material>();
                        materialMap[tex] = mats;
                    }
                    mats.Add(mat);
                }
            }
        }

        foreach (var kv in usageMap)
        {
            Texture tex = kv.Key;
            long bytes = EstimateTextureVram(tex);
            textureTotalBytes += bytes;

            textureVramEntries.Add(new TextureVramEntry
            {
                texture = tex,
                assetPath = AssetDatabase.GetAssetPath(tex),
                usage = string.Join(", ", kv.Value.Take(4).ToArray()) + (kv.Value.Count > 4 ? " …" : ""),
                materialCount = materialMap.TryGetValue(tex, out var mats) ? mats.Count : 0,
                estimatedBytes = bytes
            });
        }

        textureVramEntries.Sort((a, b) => b.estimatedBytes.CompareTo(a.estimatedBytes));
    }

    long EstimateTextureVram(Texture tex)
    {
        if (tex == null) return 0;

        long pixels = Math.Max(1L, (long)tex.width * tex.height);
        double bytesPerPixel = 4.0;
        bool mipmaps = true;

        Texture2D t2d = tex as Texture2D;
        if (t2d != null)
        {
            string f = t2d.format.ToString().ToUpperInvariant();

            // Approximate common block-compressed formats by effective bpp.
            if (f.Contains("DXT1") || f.Contains("BC1") || f.Contains("ETC_RGB") || f == "ETC_RGB4")
                bytesPerPixel = 0.5;
            else if (f.Contains("DXT5") || f.Contains("BC3") || f.Contains("BC5") ||
                     f.Contains("ETC2_RGBA") || f.Contains("EAC"))
                bytesPerPixel = 1.0;
            else if (f.Contains("BC7"))
                bytesPerPixel = 1.0;
            else if (f.Contains("ASTC"))
            {
                // Most avatar ASTC imports are around 6x6; use a conservative estimate.
                bytesPerPixel = 16.0 / 36.0;
            }
            else if (f.Contains("RGBA64") || f.Contains("RGBAHALF"))
                bytesPerPixel = 8.0;
            else if (f.Contains("RGBAFLOAT"))
                bytesPerPixel = 16.0;
            else if (f.Contains("RGB24"))
                bytesPerPixel = 3.0;
            else if (f.Contains("R8") || f.Contains("ALPHA8"))
                bytesPerPixel = 1.0;
            else if (f.Contains("RG16"))
                bytesPerPixel = 2.0;
            else
                bytesPerPixel = 4.0;

            mipmaps = t2d.mipmapCount > 1;
        }

        double total = pixels * bytesPerPixel;
        if (mipmaps) total *= 4.0 / 3.0;

        Cubemap cube = tex as Cubemap;
        if (cube != null) total *= 6.0;

        return (long)Math.Max(0.0, total);
    }

    void SetTextureMaxSize(TextureImporter importer, int size)
    {
        if (importer == null) return;

        Undo.RecordObject(importer, "Change Texture Max Size");
        importer.maxTextureSize = size;
        importer.SaveAndReimport();
        EditorUtility.SetDirty(importer);
    }
}
