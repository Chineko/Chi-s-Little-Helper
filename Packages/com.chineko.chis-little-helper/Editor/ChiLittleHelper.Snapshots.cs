using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

public partial class ChiLittleHelperWindow
{
    [Serializable]
    sealed class AvatarSnapshot
    {
        public string id;
        public string name;
        public string avatarName;
        public string createdUtc;
        public int triangles;
        public int rendererCount;
        public int materialSlots;
        public int physBones;
        public int contacts;
        public int constraints;
        public int parameterBits;
        public List<string> objectPaths = new List<string>();
        public List<string> materialAssets = new List<string>();
        public List<string> parameters = new List<string>();
        public List<string> componentKeys = new List<string>();
    }

    [Serializable]
    sealed class AvatarSnapshotFile
    {
        public List<AvatarSnapshot> snapshots = new List<AvatarSnapshot>();
    }

    sealed class SnapshotDiff
    {
        public string label;
        public readonly List<string> added = new List<string>();
        public readonly List<string> removed = new List<string>();
    }

    AvatarSnapshotFile snapshotLibrary = new AvatarSnapshotFile();
    bool snapshotLibraryLoaded = false;
    bool snapshotCompareDirty = true;
    string snapshotNewName = "Baseline";
    string snapshotSelectedId = "";
    readonly List<SnapshotDiff> snapshotDiffs = new List<SnapshotDiff>();

    string SnapshotLibraryPath
    {
        get
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, "ProjectSettings", "ChiLittleHelperSnapshots.json");
        }
    }

    void DrawSnapshotCompare()
    {
        EnsureSnapshotLibraryLoaded();

        BeginCard(
            CardIcon.Overview,
            "Avatar Snapshot & Compare",
            "Capture a lightweight structural baseline, then see exactly what changed later.");

        DrawHelpHint("Snapshots are structural baselines, not scene backups. They report differences but do not restore the avatar.");

        if (avatar == null)
        {
            DrawInfoBox("Select an avatar before capturing or comparing snapshots.");
            EndCard();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Snapshot Name", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        snapshotNewName = EditorGUILayout.TextField(snapshotNewName, styleInputBox);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        if (GUILayout.Button("Capture Current Avatar Snapshot", styleBtnGrad, GUILayout.Height(34)))
            CaptureAvatarSnapshot();

        GUILayout.Space(10);

        if (snapshotLibrary.snapshots.Count == 0)
        {
            EditorGUILayout.HelpBox("No snapshots saved yet.", MessageType.Info);
            EndCard();
            return;
        }

        string[] labels = snapshotLibrary.snapshots
            .Select(s => s.name + " · " + s.avatarName + " · " + ParseSnapshotTime(s.createdUtc))
            .ToArray();

        int selectedIndex = Mathf.Max(0, snapshotLibrary.snapshots.FindIndex(x => x.id == snapshotSelectedId));
        if (selectedIndex >= snapshotLibrary.snapshots.Count) selectedIndex = 0;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Compare Against", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        int nextIndex = EditorGUILayout.Popup(selectedIndex, labels, stylePopup, GUILayout.Height(24));
        EditorGUILayout.EndHorizontal();

        AvatarSnapshot selected = snapshotLibrary.snapshots[nextIndex];
        snapshotSelectedId = selected.id;

        GUILayout.Space(6);

        BeginResponsiveButtonRow();
        if (GUILayout.Button("Compare Current to Snapshot", styleBtnDark, GUILayout.Height(30), GUILayout.ExpandWidth(true)))
            CompareCurrentToSnapshot(selected);

        ResponsiveButtonGap();

        if (GUILayout.Button("Export Snapshot JSON", styleBtnDark, GUILayout.Height(30), GUILayout.ExpandWidth(true)))
            ExportSingleSnapshot(selected);

        ResponsiveButtonGap();

        if (GUILayout.Button("Delete", styleBtnRemove, ResponsiveButtonWidth(78), GUILayout.Height(30)))
        {
            if (!confirmDestructiveActions ||
                EditorUtility.DisplayDialog(
                    "Delete Snapshot",
                    "Delete snapshot '" + selected.name + "'?",
                    "Delete",
                    "Cancel"))
            {
                snapshotLibrary.snapshots.Remove(selected);
                SaveSnapshotLibrary();
                snapshotSelectedId = "";
                snapshotDiffs.Clear();
                snapshotCompareDirty = true;
                GUIUtility.ExitGUI();
            }
        }
        EndResponsiveButtonRow();

        GUILayout.Space(8);

        if (snapshotCompareDirty)
        {
            EditorGUILayout.HelpBox("Choose Compare Current to Snapshot to calculate changes.", MessageType.Info);
            EndCard();
            return;
        }

        AvatarSnapshot current = BuildCurrentAvatarSnapshot("Current");

        EditorGUILayout.BeginHorizontal();
        DrawDoctorStat("Triangles Δ", Signed(current.triangles - selected.triangles), styleCardSubtitle);
        GUILayout.Space(4);
        DrawDoctorStat("Renderers Δ", Signed(current.rendererCount - selected.rendererCount), styleCardSubtitle);
        GUILayout.Space(4);
        DrawDoctorStat("Materials Δ", Signed(current.materialSlots - selected.materialSlots), styleCardSubtitle);
        GUILayout.Space(4);
        DrawDoctorStat("Param Bits Δ", Signed(current.parameterBits - selected.parameterBits), styleCardSubtitle);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);

        bool any = snapshotDiffs.Any(d => d.added.Count > 0 || d.removed.Count > 0);
        if (!any)
        {
            EditorGUILayout.HelpBox("No structural differences detected by this snapshot format.", MessageType.Info);
        }
        else
        {
            foreach (SnapshotDiff diff in snapshotDiffs)
            {
                if (diff.added.Count == 0 && diff.removed.Count == 0)
                    continue;

                EditorGUILayout.LabelField(diff.label, new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = ColLavenderText }
                });

                foreach (string added in diff.added.Take(20))
                    EditorGUILayout.LabelField("+ " + added, styleInfoText);

                foreach (string removed in diff.removed.Take(20))
                    EditorGUILayout.LabelField("− " + removed, styleInfoText);

                int hidden = Math.Max(0, diff.added.Count - 20) + Math.Max(0, diff.removed.Count - 20);
                if (hidden > 0)
                    EditorGUILayout.LabelField("… " + hidden + " more change(s)", styleCardSubtitle);

                GUILayout.Space(6);
            }
        }

        EndCard();
    }

    void CaptureAvatarSnapshot()
    {
        if (avatar == null)
            throw new InvalidOperationException("Select an avatar first.");

        AvatarSnapshot snapshot = BuildCurrentAvatarSnapshot(
            string.IsNullOrWhiteSpace(snapshotNewName) ? "Snapshot" : snapshotNewName.Trim());

        snapshot.id = Guid.NewGuid().ToString("N");
        snapshot.createdUtc = DateTime.UtcNow.ToString("o");
        snapshotLibrary.snapshots.Add(snapshot);
        snapshotSelectedId = snapshot.id;
        SaveSnapshotLibrary();
        snapshotCompareDirty = true;

        ShowNotification(new GUIContent("Avatar snapshot captured"));
    }

    AvatarSnapshot BuildCurrentAvatarSnapshot(string name)
    {
        EnsureParameterMemoryCache();

        AvatarSnapshot result = new AvatarSnapshot
        {
            name = name,
            avatarName = avatar != null ? avatar.gameObject.name : "None",
            parameterBits = ParameterMemoryUsedBits
        };

        if (avatar == null)
            return result;

        Transform root = avatar.transform;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            result.objectPaths.Add(GetRelativeTransformPath(root, t));

        foreach (Renderer renderer in avatar.GetComponentsInChildren<Renderer>(true))
        {
            result.rendererCount++;
            Material[] materials = renderer.sharedMaterials ?? new Material[0];
            result.materialSlots += materials.Length;

            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer smr)
                mesh = smr.sharedMesh;
            else
            {
                MeshFilter mf = renderer.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }

            if (mesh != null)
                result.triangles += CountMeshTriangles(mesh);

            foreach (Material material in materials)
            {
                if (material == null) continue;
                string path = AssetDatabase.GetAssetPath(material);
                result.materialAssets.Add(
                    string.IsNullOrEmpty(path) ? material.name : path);
            }
        }

        Type pbType = FindType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone");
        if (pbType != null)
        {
            foreach (Component pb in avatar.GetComponentsInChildren(pbType, true).Cast<Component>())
            {
                result.physBones++;
                result.componentKeys.Add("PhysBone:" + GetRelativeTransformPath(root, pb.transform));
            }
        }

        Type sender = FindType(T_CONTACT_SENDER);
        Type receiver = FindType(T_CONTACT_RECEIVER);

        if (sender != null)
        {
            foreach (Component c in avatar.GetComponentsInChildren(sender, true).Cast<Component>())
            {
                result.contacts++;
                result.componentKeys.Add("ContactSender:" + GetRelativeTransformPath(root, c.transform));
            }
        }

        if (receiver != null)
        {
            foreach (Component c in avatar.GetComponentsInChildren(receiver, true).Cast<Component>())
            {
                result.contacts++;
                result.componentKeys.Add("ContactReceiver:" + GetRelativeTransformPath(root, c.transform));
            }
        }

        foreach (Component component in GetAvatarConstraints())
        {
            if (component == null) continue;
            result.constraints++;
            result.componentKeys.Add(component.GetType().Name + ":" + GetRelativeTransformPath(root, component.transform));
        }

        UnityEngine.Object expressionAsset = GetExpressionParametersAsset();
        if (expressionAsset != null)
        {
            SerializedObject so = new SerializedObject(expressionAsset);
            SerializedProperty parameters = so.FindProperty("parameters");

            if (parameters != null && parameters.isArray)
            {
                for (int i = 0; i < parameters.arraySize; i++)
                {
                    SerializedProperty element = parameters.GetArrayElementAtIndex(i);
                    string paramName = element.FindPropertyRelative("name")?.stringValue ?? "";
                    string paramType = ReadParameterMemoryType(element.FindPropertyRelative("valueType"));
                    if (!string.IsNullOrWhiteSpace(paramName))
                        result.parameters.Add(paramName + ":" + paramType);
                }
            }
        }

        result.objectPaths = result.objectPaths.Distinct().OrderBy(x => x).ToList();
        result.materialAssets = result.materialAssets.Distinct().OrderBy(x => x).ToList();
        result.parameters = result.parameters.Distinct().OrderBy(x => x).ToList();
        result.componentKeys = result.componentKeys.Distinct().OrderBy(x => x).ToList();

        return result;
    }

    string GetRelativeTransformPath(Transform root, Transform target)
    {
        if (target == root) return "<root>";

        var parts = new List<string>();
        Transform current = target;

        while (current != null && current != root)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts.ToArray());
    }

    void CompareCurrentToSnapshot(AvatarSnapshot baseline)
    {
        AvatarSnapshot current = BuildCurrentAvatarSnapshot("Current");
        snapshotDiffs.Clear();

        AddSnapshotDiff("Hierarchy", baseline.objectPaths, current.objectPaths);
        AddSnapshotDiff("Materials", baseline.materialAssets, current.materialAssets);
        AddSnapshotDiff("Parameters", baseline.parameters, current.parameters);
        AddSnapshotDiff("Dynamics / Constraints", baseline.componentKeys, current.componentKeys);

        snapshotCompareDirty = false;
    }

    void AddSnapshotDiff(string label, List<string> before, List<string> after)
    {
        var diff = new SnapshotDiff { label = label };
        var beforeSet = new HashSet<string>(before ?? new List<string>(), StringComparer.Ordinal);
        var afterSet = new HashSet<string>(after ?? new List<string>(), StringComparer.Ordinal);

        diff.added.AddRange(afterSet.Except(beforeSet).OrderBy(x => x));
        diff.removed.AddRange(beforeSet.Except(afterSet).OrderBy(x => x));
        snapshotDiffs.Add(diff);
    }

    string Signed(int value)
    {
        return value > 0 ? "+" + value : value.ToString();
    }

    string ParseSnapshotTime(string utc)
    {
        if (DateTime.TryParse(utc, out DateTime dt))
            return dt.ToLocalTime().ToString("g");
        return "unknown time";
    }

    void EnsureSnapshotLibraryLoaded()
    {
        if (snapshotLibraryLoaded) return;

        snapshotLibrary = new AvatarSnapshotFile();

        try
        {
            if (File.Exists(SnapshotLibraryPath))
            {
                string json = File.ReadAllText(SnapshotLibraryPath);
                AvatarSnapshotFile loaded = JsonUtility.FromJson<AvatarSnapshotFile>(json);
                if (loaded != null)
                    snapshotLibrary = loaded;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Chi's Little Helper] Could not load snapshot library: " + ex.Message);
        }

        if (snapshotLibrary.snapshots == null)
            snapshotLibrary.snapshots = new List<AvatarSnapshot>();

        snapshotLibraryLoaded = true;
    }

    void SaveSnapshotLibrary()
    {
        string directory = Path.GetDirectoryName(SnapshotLibraryPath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(
            SnapshotLibraryPath,
            JsonUtility.ToJson(snapshotLibrary, true));
    }

    void ExportSingleSnapshot(AvatarSnapshot snapshot)
    {
        string path = EditorUtility.SaveFilePanel(
            "Export Avatar Snapshot",
            "",
            snapshot.name + ".json",
            "json");

        if (string.IsNullOrEmpty(path)) return;

        File.WriteAllText(path, JsonUtility.ToJson(snapshot, true));
        ShowNotification(new GUIContent("Snapshot exported"));
    }
}
