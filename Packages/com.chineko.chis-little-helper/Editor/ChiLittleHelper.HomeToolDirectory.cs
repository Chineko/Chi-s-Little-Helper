using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class ChiLittleHelperWindow
{
    void DrawHomeToolDirectory()
    {
        if (GUILayout.Button(homeShowAllTools ? "▼  Hide All Tools" : "▶  Show All Tools",
            styleBtnDark, GUILayout.Height(30), GUILayout.ExpandWidth(true)))
            homeShowAllTools = !homeShowAllTools;

        if (!homeShowAllTools)
            return;

        GUILayout.Space(6);

        EditorGUILayout.LabelField(
            "Every module is available here as well as in the category tabs above. Collapse any category to reduce clutter.",
            styleCardSubtitle);

        GUILayout.Space(6);

        for (int categoryIndex = 0; categoryIndex < CategoryLabels.Length; categoryIndex++)
        {
            var visible = new List<int>();
            int[] ids = CategoryToolIds[categoryIndex];
            string[] labels = CategoryToolLabels[categoryIndex];

            for (int i = 0; i < ids.Length; i++)
            {
                int id = ids[i];
                if (!enableVRCFuryFeatures && IsVRCFuryTool(id))
                    continue;
                visible.Add(i);
            }

            if (visible.Count == 0)
                continue;

            bool expanded = GetHomeToolCategoryExpanded(categoryIndex);
            if (GUILayout.Button(
                (expanded ? "▼  " : "▶  ") + CategoryLabels[categoryIndex] + "  (" + visible.Count + ")",
                styleBtnDark,
                GUILayout.Height(28),
                GUILayout.ExpandWidth(true)))
            {
                SetHomeToolCategoryExpanded(categoryIndex, !expanded);
                expanded = !expanded;
            }

            if (!expanded)
            {
                GUILayout.Space(4);
                continue;
            }

            GUILayout.Space(4);

            for (int row = 0; row < visible.Count; row += 2)
            {
                BeginResponsiveButtonRow();

                int firstIndex = visible[row];
                DrawHomeToolDirectoryButton(
                    categoryIndex,
                    ids[firstIndex],
                    labels[firstIndex]);

                if (row + 1 < visible.Count)
                {
                    ResponsiveButtonGap();
                    int secondIndex = visible[row + 1];
                    DrawHomeToolDirectoryButton(
                        categoryIndex,
                        ids[secondIndex],
                        labels[secondIndex]);
                }

                EndResponsiveButtonRow();
                GUILayout.Space(4);
            }

            GUILayout.Space(5);
        }
    }

    bool GetHomeToolCategoryExpanded(int categoryIndex)
    {
        switch (categoryIndex)
        {
            case 0: return homeShowBuildTools;
            case 1: return homeShowDynamicsTools;
            case 2: return homeShowMaterialsTools;
            case 3: return homeShowOptimizeTools;
            case 4: return homeShowDoctorTools;
            default: return true;
        }
    }

    void SetHomeToolCategoryExpanded(int categoryIndex, bool value)
    {
        switch (categoryIndex)
        {
            case 0: homeShowBuildTools = value; break;
            case 1: homeShowDynamicsTools = value; break;
            case 2: homeShowMaterialsTools = value; break;
            case 3: homeShowOptimizeTools = value; break;
            case 4: homeShowDoctorTools = value; break;
        }
    }

    void DrawHomeToolDirectoryButton(int categoryIndex, int toolId, string label)
    {
        bool requiresAvatar = toolId != 12 && toolId != 16;

        using (new EditorGUI.DisabledScope(requiresAvatar && avatar == null))
        {
            if (GUILayout.Button(
                label,
                styleBtnDark,
                GUILayout.Height(30),
                GUILayout.MinWidth(0),
                GUILayout.ExpandWidth(true)))
            {
                RouteToTool(categoryIndex, toolId);
            }
        }
    }
}
