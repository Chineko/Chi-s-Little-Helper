using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class ChiLittleHelperWindow
{
    void DrawHomeToolDirectory()
    {
        EditorGUILayout.LabelField("All Tools", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText },
            fontSize = 13
        });

        EditorGUILayout.LabelField(
            "Every module is available here as well as in the category tabs above.",
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

            EditorGUILayout.LabelField(CategoryLabels[categoryIndex], new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColTextPrimary },
                fontSize = 11
            });

            GUILayout.Space(3);

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
