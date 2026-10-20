using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public partial class ChiLittleHelperWindow
{
    sealed class AnimatorParamIssue
    {
        public MessageType severity;
        public string title;
        public string detail;
        public UnityEngine.Object target;
    }

    sealed class ExpressionParamInfo
    {
        public string name;
        public string type;
        public bool synced;
    }

    readonly List<AnimatorParamIssue> animatorParamIssues = new List<AnimatorParamIssue>();
    readonly List<AnimatorController> animatorDoctorControllers = new List<AnimatorController>();
    readonly List<ExpressionParamInfo> animatorDoctorExpressionParams = new List<ExpressionParamInfo>();
    bool animatorDoctorDirty = true;
    int animatorDoctorSyncedBits = 0;
    int animatorDoctorExpressionCount = 0;
    int animatorDoctorControllerParamCount = 0;

    void DrawAnimatorParametersDoctor()
    {
        BeginCard(
            CardIcon.Warning,
            "Animator & Parameters Doctor",
            "Cross-check Expression Parameters against avatar Animator Controllers and catch common setup mistakes before upload.");

        DrawHelpHint("Synced Bool parameters cost 1 bit; Int/Float cost 8 bits. Type mismatches are warnings because matching types is clearer and safer to maintain.");

        if (avatar == null)
        {
            DrawInfoBox("Select an avatar to scan its playable layers and Expression Parameters.");
            EndCard();
            return;
        }

        BeginResponsiveButtonRow();
        if (GUILayout.Button("Run Animator / Parameter Scan", styleBtnGrad, GUILayout.Height(38), GUILayout.ExpandWidth(true)))
        {
            Run("Scan Animator & Parameters", () =>
            {
                ScanAnimatorParameters();
                animatorDoctorDirty = false;
            });
        }

        ResponsiveButtonGap(5f);
        using (new EditorGUI.DisabledScope(animatorDoctorDirty))
        {
            if (GUILayout.Button("Copy Report", styleBtnDark, ResponsiveButtonWidth(110), GUILayout.Height(38)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildAnimatorDoctorReport();
                ShowNotification(new GUIContent("Animator report copied"));
            }
        }
        EndResponsiveButtonRow();

        GUILayout.Space(8);

        DrawDataFreshnessBanner(
            "Animator / parameter scan",
            animatorDoctorDirty,
            "Animator and Expression Parameter diagnostics match the current avatar state.",
            () =>
            {
                Run("Scan Animator & Parameters", () =>
                {
                    ScanAnimatorParameters();
                    animatorDoctorDirty = false;
                });
            },
            "Run Scan");

        if (animatorDoctorDirty)
        {
            EndCard();
            return;
        }

        GUILayout.Space(8);

        if (IsCompactLayout)
        {
            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("Controllers", animatorDoctorControllers.Count.ToString(), styleCardSubtitle);
            GUILayout.Space(3);
            DrawDoctorStat("Animator Params", animatorDoctorControllerParamCount.ToString(), styleCardSubtitle);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("Expression Params", animatorDoctorExpressionCount.ToString(), styleCardSubtitle);
            GUILayout.Space(3);
            DrawDoctorStat("Synced Memory", animatorDoctorSyncedBits + " / 256 bits", styleCardSubtitle);
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("Controllers", animatorDoctorControllers.Count.ToString(), styleCardSubtitle);
            GUILayout.Space(4);
            DrawDoctorStat("Animator Params", animatorDoctorControllerParamCount.ToString(), styleCardSubtitle);
            GUILayout.Space(4);
            DrawDoctorStat("Expression Params", animatorDoctorExpressionCount.ToString(), styleCardSubtitle);
            GUILayout.Space(4);
            DrawDoctorStat("Synced Memory", animatorDoctorSyncedBits + " / 256 bits", styleCardSubtitle);
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(8);

        if (animatorDoctorSyncedBits > 256)
            EditorGUILayout.HelpBox("Synced Expression Parameters exceed VRChat's 256-bit synchronization budget.", MessageType.Error);
        else if (animatorDoctorExpressionCount > 8192)
            EditorGUILayout.HelpBox("Expression Parameter count exceeds VRChat's 8192 custom-parameter limit.", MessageType.Error);

        if (animatorParamIssues.Count == 0)
        {
            EditorGUILayout.HelpBox("No issues found by this scan.", MessageType.Info);
            EndCard();
            return;
        }

        foreach (var group in animatorParamIssues.GroupBy(x => x.severity).OrderByDescending(g => g.Key == MessageType.Error ? 2 : g.Key == MessageType.Warning ? 1 : 0))
        {
            string heading =
                group.Key == MessageType.Error ? "Errors" :
                group.Key == MessageType.Warning ? "Warnings" : "Info";

            EditorGUILayout.LabelField(heading, new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = group.Key == MessageType.Error ? DangerRed : ColLavenderText }
            });

            foreach (var issue in group)
            {
                EditorGUILayout.BeginVertical(styleRowBg);
                EditorGUILayout.LabelField(issue.title, new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = ColTextPrimary }
                });
                EditorGUILayout.LabelField(issue.detail, styleInfoText);

                if (issue.target != null)
                {
                    GUILayout.Space(3);
                    if (GUILayout.Button("Select Asset", styleBtnDark, GUILayout.Width(100), GUILayout.Height(25)))
                    {
                        Selection.activeObject = issue.target;
                        EditorGUIUtility.PingObject(issue.target);
                    }
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(4);
            }
        }

        EndCard();
    }

    void ScanAnimatorParameters()
    {
        animatorParamIssues.Clear();
        animatorDoctorControllers.Clear();
        animatorDoctorExpressionParams.Clear();
        animatorDoctorSyncedBits = 0;
        animatorDoctorExpressionCount = 0;
        animatorDoctorControllerParamCount = 0;

        CollectAvatarAnimatorControllers(animatorDoctorControllers);
        ScanExpressionParameterAsset();

        var animatorTypes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (AnimatorController controller in animatorDoctorControllers)
        {
            if (controller == null) continue;

            var duplicateParams = controller.parameters
                .GroupBy(p => p.name)
                .Where(g => g.Count() > 1);

            foreach (var duplicate in duplicateParams)
            {
                AddAnimatorIssue(
                    MessageType.Error,
                    "Duplicate Animator Parameter",
                    $"'{duplicate.Key}' appears {duplicate.Count()} times in {controller.name}.",
                    controller);
            }

            var duplicateLayers = controller.layers
                .GroupBy(l => l.name)
                .Where(g => g.Count() > 1);

            foreach (var duplicate in duplicateLayers)
            {
                AddAnimatorIssue(
                    MessageType.Warning,
                    "Duplicate Animator Layer Name",
                    $"'{duplicate.Key}' appears {duplicate.Count()} times in {controller.name}. Duplicate layer names make debugging and merging harder.",
                    controller);
            }

            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                animatorDoctorControllerParamCount++;

                string type = NormalizeAnimatorParamType(parameter.type);
                if (!animatorTypes.TryGetValue(parameter.name, out var types))
                {
                    types = new HashSet<string>(StringComparer.Ordinal);
                    animatorTypes.Add(parameter.name, types);
                }
                types.Add(type);

                if (parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    AddAnimatorIssue(
                        MessageType.Warning,
                        "Trigger Parameter",
                        $"'{parameter.name}' in {controller.name} uses Trigger. VRChat recommends Bool/Int/Float for avatar state because Trigger values can desynchronize.",
                        controller);
                }
            }

            for (int i = 1; i < controller.layers.Length; i++)
            {
                if (controller.layers[i].defaultWeight <= 0f)
                {
                    AddAnimatorIssue(
                        MessageType.Warning,
                        "Layer Weight is Zero",
                        $"Layer '{controller.layers[i].name}' in {controller.name} has a default weight of 0.",
                        controller);
                }
            }
        }

        foreach (var pair in animatorTypes)
        {
            if (pair.Value.Count > 1)
            {
                AddAnimatorIssue(
                    MessageType.Warning,
                    "Animator Parameter Type Mismatch",
                    $"'{pair.Key}' uses multiple Animator types across controllers: {string.Join(", ", pair.Value.ToArray())}.",
                    avatar);
            }
        }

        var expressionByName = animatorDoctorExpressionParams
            .GroupBy(x => x.name)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        foreach (var duplicate in expressionByName.Where(x => x.Value.Count > 1))
        {
            AddAnimatorIssue(
                MessageType.Error,
                "Duplicate Expression Parameter",
                $"'{duplicate.Key}' appears {duplicate.Value.Count} times in the Expression Parameters asset.",
                GetExpressionParametersAsset());
        }

        foreach (ExpressionParamInfo expression in animatorDoctorExpressionParams)
        {
            if (!animatorTypes.TryGetValue(expression.name, out var controllerTypes))
                continue;

            if (!controllerTypes.Contains(expression.type))
            {
                AddAnimatorIssue(
                    MessageType.Warning,
                    "Expression / Animator Type Mismatch",
                    $"'{expression.name}' is {expression.type} in Expression Parameters but Animator Controllers use {string.Join(", ", controllerTypes.ToArray())}. VRChat can convert mismatched values, but matching types is safer and clearer.",
                    GetExpressionParametersAsset());
            }
        }

        if (GetExpressionParametersAsset() == null)
        {
            AddAnimatorIssue(
                MessageType.Warning,
                "No Expression Parameters Asset",
                "The Avatar Descriptor does not reference an Expression Parameters asset.",
                avatar);
        }

        if (animatorDoctorControllers.Count == 0)
        {
            AddAnimatorIssue(
                MessageType.Warning,
                "No Custom Animator Controllers Found",
                "No AnimatorController assets were found on the avatar's descriptor or Animator component.",
                avatar);
        }
    }

    void CollectAvatarAnimatorControllers(List<AnimatorController> output)
    {
        output.Clear();
        var seen = new HashSet<AnimatorController>();

        void AddController(RuntimeAnimatorController runtime)
        {
            if (runtime == null) return;

            AnimatorController controller = runtime as AnimatorController;
            if (controller == null && runtime is AnimatorOverrideController overrideController)
                controller = overrideController.runtimeAnimatorController as AnimatorController;

            if (controller != null && seen.Add(controller))
                output.Add(controller);
        }

        Animator animator = avatar.GetComponent<Animator>();
        if (animator != null)
            AddController(animator.runtimeAnimatorController);

        var serializedAvatar = new SerializedObject(avatar);
        SerializedProperty iterator = serializedAvatar.GetIterator();
        bool enterChildren = true;

        while (iterator.Next(enterChildren))
        {
            enterChildren = true;
            if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                iterator.objectReferenceValue is RuntimeAnimatorController runtime)
            {
                AddController(runtime);
            }
        }
    }

    UnityEngine.Object GetExpressionParametersAsset()
    {
        if (avatar == null) return null;

        var so = new SerializedObject(avatar);
        SerializedProperty prop =
            so.FindProperty("expressionParameters") ??
            so.FindProperty("expressionsParameters");

        return prop != null ? prop.objectReferenceValue : null;
    }

    void ScanExpressionParameterAsset()
    {
        UnityEngine.Object asset = GetExpressionParametersAsset();
        if (asset == null) return;

        var so = new SerializedObject(asset);
        SerializedProperty parameters = so.FindProperty("parameters");
        if (parameters == null || !parameters.isArray)
            return;

        for (int i = 0; i < parameters.arraySize; i++)
        {
            SerializedProperty element = parameters.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = element.FindPropertyRelative("name");
            SerializedProperty typeProp = element.FindPropertyRelative("valueType");
            SerializedProperty syncedProp =
                element.FindPropertyRelative("networkSynced") ??
                element.FindPropertyRelative("synced");

            string name = nameProp != null ? nameProp.stringValue : "";
            string type = ReadExpressionType(typeProp);
            bool synced = syncedProp == null || syncedProp.boolValue;

            if (string.IsNullOrWhiteSpace(name))
            {
                AddAnimatorIssue(
                    MessageType.Warning,
                    "Unnamed Expression Parameter",
                    $"Expression parameter slot {i} has no name.",
                    asset);
                continue;
            }

            animatorDoctorExpressionParams.Add(new ExpressionParamInfo
            {
                name = name,
                type = type,
                synced = synced
            });

            animatorDoctorExpressionCount++;

            if (synced)
                animatorDoctorSyncedBits += type == "Bool" ? 1 : 8;
        }
    }

    string ReadExpressionType(SerializedProperty typeProp)
    {
        if (typeProp == null) return "Unknown";

        if (typeProp.propertyType == SerializedPropertyType.Enum)
        {
            string[] names = typeProp.enumNames;
            int index = typeProp.enumValueIndex;
            if (names != null && index >= 0 && index < names.Length)
                return NormalizeExpressionTypeName(names[index]);
        }

        return NormalizeExpressionTypeName(typeProp.displayName);
    }

    string NormalizeExpressionTypeName(string value)
    {
        value = value ?? "";
        if (value.IndexOf("Bool", StringComparison.OrdinalIgnoreCase) >= 0) return "Bool";
        if (value.IndexOf("Int", StringComparison.OrdinalIgnoreCase) >= 0) return "Int";
        if (value.IndexOf("Float", StringComparison.OrdinalIgnoreCase) >= 0) return "Float";
        return value;
    }

    string NormalizeAnimatorParamType(AnimatorControllerParameterType type)
    {
        if (type == AnimatorControllerParameterType.Bool) return "Bool";
        if (type == AnimatorControllerParameterType.Int) return "Int";
        if (type == AnimatorControllerParameterType.Float) return "Float";
        if (type == AnimatorControllerParameterType.Trigger) return "Trigger";
        return type.ToString();
    }

    void AddAnimatorIssue(MessageType severity, string title, string detail, UnityEngine.Object target)
    {
        animatorParamIssues.Add(new AnimatorParamIssue
        {
            severity = severity,
            title = title,
            detail = detail,
            target = target
        });
    }

    string BuildAnimatorDoctorReport()
    {
        var lines = new List<string>
        {
            "=== Chi's Little Helper — Animator & Parameters Doctor ===",
            "Avatar: " + (avatar != null ? avatar.gameObject.name : "None"),
            "Controllers: " + animatorDoctorControllers.Count,
            "Animator Parameters: " + animatorDoctorControllerParamCount,
            "Expression Parameters: " + animatorDoctorExpressionCount,
            "Synced Memory: " + animatorDoctorSyncedBits + " / 256 bits",
            ""
        };

        foreach (AnimatorParamIssue issue in animatorParamIssues)
        {
            lines.Add("[" + issue.severity.ToString().ToUpperInvariant() + "] " + issue.title);
            lines.Add(issue.detail);
            lines.Add("");
        }

        if (animatorParamIssues.Count == 0)
            lines.Add("No issues found.");

        return string.Join("\n", lines);
    }
}
