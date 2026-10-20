using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public partial class ChiLittleHelperWindow
{
    sealed class AnimationClipUsage
    {
        public bool selected = true;
        public AnimationClip clip;
        public AnimatorController controller;
        public string layerName;
        public string statePath;
        public AnimatorState state;
        public BlendTree blendTree;
        public int blendTreeChildIndex = -1;
        public string location;
    }

    AnimationClip animationSwapSource;
    AnimationClip animationSwapReplacement;
    string animationSwapSearch = "";
    bool animationSwapDirty = true;
    readonly List<AnimationClipUsage> animationSwapUsages = new List<AnimationClipUsage>();

    void DrawAnimationSwapper()
    {
        BeginCard(
            CardIcon.Overview,
            "Animation Swapper & Usage Map",
            "Find where animation clips are used across the avatar's Animator Controllers, then safely replace selected references.");

        DrawHelpHint(
            "Source Clip is optional for scanning. Leave it empty to build a usage map of every AnimationClip. " +
            "Replacement changes Animator State and Blend Tree references only; animation assets themselves are not modified or deleted.");

        if (avatar == null)
        {
            DrawInfoBox("Select an avatar to scan its Animator Controllers.");
            EndCard();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Source Clip", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        EditorGUI.BeginChangeCheck();
        AnimationClip nextSource = (AnimationClip)DrawThemedObjectField(
            animationSwapSource,
            typeof(AnimationClip),
            false,
            GUILayout.Height(24));
        if (EditorGUI.EndChangeCheck())
        {
            animationSwapSource = nextSource;
            animationSwapDirty = true;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Replacement", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        animationSwapReplacement = (AnimationClip)DrawThemedObjectField(
            animationSwapReplacement,
            typeof(AnimationClip),
            false,
            GUILayout.Height(24));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        BeginResponsiveButtonRow();

        if (GUILayout.Button(
            animationSwapSource == null ? "Scan All Clip Usage" : "Find Source Clip Usage",
            styleBtnGrad,
            GUILayout.Height(36),
            GUILayout.ExpandWidth(true)))
        {
            Run("Scan Animation Clip Usage", ScanAnimationClipUsage);
        }

        ResponsiveButtonGap(5f);

        using (new EditorGUI.DisabledScope(animationSwapUsages.Count == 0))
        {
            if (GUILayout.Button("Select All", styleBtnDark, GUILayout.Height(36), GUILayout.ExpandWidth(true)))
                SetAnimationUsageSelection(true);
        }

        ResponsiveButtonGap(5f);

        using (new EditorGUI.DisabledScope(animationSwapUsages.Count == 0))
        {
            if (GUILayout.Button("Select None", styleBtnDark, GUILayout.Height(36), GUILayout.ExpandWidth(true)))
                SetAnimationUsageSelection(false);
        }

        EndResponsiveButtonRow();

        GUILayout.Space(8);

        DrawDataFreshnessBanner(
            "Animation usage map",
            animationSwapDirty,
            "Animation usage results match the current scan settings.",
            () => Run("Scan Animation Clip Usage", ScanAnimationClipUsage),
            "Rescan");

        if (animationSwapDirty)
        {
            EndCard();
            return;
        }

        GUILayout.Space(8);

        int selectedCount = animationSwapUsages.Count(x => x.selected);
        int controllerCount = animationSwapUsages
            .Where(x => x.controller != null)
            .Select(x => x.controller)
            .Distinct()
            .Count();
        int clipCount = animationSwapUsages
            .Where(x => x.clip != null)
            .Select(x => x.clip)
            .Distinct()
            .Count();

        if (IsCompactLayout)
        {
            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("References", animationSwapUsages.Count.ToString(), styleCardSubtitle);
            GUILayout.Space(3);
            DrawDoctorStat("Selected", selectedCount.ToString(), styleCardSubtitle);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("Controllers", controllerCount.ToString(), styleCardSubtitle);
            GUILayout.Space(3);
            DrawDoctorStat("Clips", clipCount.ToString(), styleCardSubtitle);
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            DrawDoctorStat("References", animationSwapUsages.Count.ToString(), styleCardSubtitle);
            GUILayout.Space(4);
            DrawDoctorStat("Selected", selectedCount.ToString(), styleCardSubtitle);
            GUILayout.Space(4);
            DrawDoctorStat("Controllers", controllerCount.ToString(), styleCardSubtitle);
            GUILayout.Space(4);
            DrawDoctorStat("Clips", clipCount.ToString(), styleCardSubtitle);
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(8);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Filter", styleFieldLabel, GUILayout.Width(IsCompactLayout ? 52f : 72f));
        animationSwapSearch = EditorGUILayout.TextField(
            animationSwapSearch ?? "",
            styleInputBox,
            GUILayout.Height(24),
            GUILayout.MinWidth(0));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        string q = (animationSwapSearch ?? "").Trim();

        List<AnimationClipUsage> visible = animationSwapUsages
            .Where(x =>
                q.Length == 0 ||
                (x.clip != null && x.clip.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (x.controller != null && x.controller.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (x.layerName ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (x.statePath ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (x.location ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(x => x.controller != null ? x.controller.name : "")
            .ThenBy(x => x.layerName)
            .ThenBy(x => x.statePath)
            .ThenBy(x => x.location)
            .ToList();

        if (visible.Count == 0)
        {
            DrawInfoBox(
                animationSwapUsages.Count == 0
                    ? "No matching AnimationClip references were found in the avatar's Animator Controllers."
                    : "No usage-map entries match the current filter.");
        }
        else
        {
            foreach (IGrouping<AnimatorController, AnimationClipUsage> controllerGroup in visible
                .GroupBy(x => x.controller)
                .OrderBy(g => g.Key != null ? g.Key.name : ""))
            {
                AnimatorController controller = controllerGroup.Key;

                EditorGUILayout.BeginVertical(styleRowBg);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    controller != null ? controller.name : "(Missing Controller)",
                    new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12,
                        normal = { textColor = ColLavenderText }
                    });

                GUILayout.FlexibleSpace();

                if (controller != null &&
                    GUILayout.Button("Select Controller", styleBtnDark, GUILayout.Width(120), GUILayout.Height(24)))
                {
                    Selection.activeObject = controller;
                    EditorGUIUtility.PingObject(controller);
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);

                foreach (AnimationClipUsage usage in controllerGroup)
                {
                    EditorGUILayout.BeginVertical(new GUIStyle
                    {
                        normal = { background = texInfoBox },
                        border = new RectOffset(8, 8, 8, 8),
                        padding = new RectOffset(7, 7, 6, 6),
                        margin = new RectOffset(0, 0, 2, 2)
                    });

                    EditorGUILayout.BeginHorizontal();
                    usage.selected = EditorGUILayout.Toggle(usage.selected, GUILayout.Width(18));

                    EditorGUILayout.LabelField(
                        usage.clip != null ? usage.clip.name : "(Missing Clip)",
                        new GUIStyle(EditorStyles.boldLabel)
                        {
                            normal = { textColor = ColTextPrimary }
                        });

                    GUILayout.FlexibleSpace();

                    if (usage.blendTree != null)
                        EditorGUILayout.LabelField("Blend Tree", styleCardSubtitle, GUILayout.Width(66));
                    else
                        EditorGUILayout.LabelField("State", styleCardSubtitle, GUILayout.Width(42));

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField(
                        usage.layerName + "  ›  " + usage.statePath,
                        styleCardSubtitle);

                    EditorGUILayout.LabelField(
                        usage.location,
                        styleInfoText);

                    GUILayout.Space(3);

                    BeginResponsiveButtonRow();

                    if (usage.clip != null &&
                        GUILayout.Button("Use as Source", styleBtnDark, GUILayout.Height(26), GUILayout.ExpandWidth(true)))
                    {
                        animationSwapSource = usage.clip;
                        animationSwapDirty = true;
                    }

                    ResponsiveButtonGap(3f);

                    if (usage.clip != null &&
                        GUILayout.Button("Select Clip", styleBtnDark, GUILayout.Height(26), GUILayout.ExpandWidth(true)))
                    {
                        Selection.activeObject = usage.clip;
                        EditorGUIUtility.PingObject(usage.clip);
                    }

                    EndResponsiveButtonRow();

                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(7);
            }
        }

        GUILayout.Space(8);

        bool hasSource = animationSwapSource != null;
        bool hasReplacement = animationSwapReplacement != null;
        bool sameClip = hasSource && animationSwapSource == animationSwapReplacement;

        int matchingSelected = animationSwapUsages.Count(x =>
            x.selected &&
            x.clip == animationSwapSource);

        if (sameClip)
            DrawInfoBox("Source Clip and Replacement are the same asset. Choose a different replacement.");

        using (new EditorGUI.DisabledScope(
            !hasSource ||
            !hasReplacement ||
            sameClip ||
            matchingSelected == 0))
        {
            if (GUILayout.Button(
                "Replace Selected (" + matchingSelected + ")",
                styleBtnGrad,
                GUILayout.Height(40),
                GUILayout.ExpandWidth(true)))
            {
                ReplaceAnimationUsages(false);
            }
        }

        GUILayout.Space(4);

        int matchingAll = animationSwapUsages.Count(x => x.clip == animationSwapSource);

        using (new EditorGUI.DisabledScope(
            !hasSource ||
            !hasReplacement ||
            sameClip ||
            matchingAll == 0))
        {
            if (GUILayout.Button(
                "Replace Everywhere (" + matchingAll + ")",
                styleBtnDark,
                GUILayout.Height(34),
                GUILayout.ExpandWidth(true)))
            {
                ReplaceAnimationUsages(true);
            }
        }

        EndCard();
    }

    void ScanAnimationClipUsage()
    {
        animationSwapUsages.Clear();

        if (avatar == null)
        {
            animationSwapDirty = false;
            return;
        }

        var controllers = new List<AnimatorController>();
        CollectAvatarAnimatorControllers(controllers);

        foreach (AnimatorController controller in controllers)
        {
            if (controller == null)
                continue;

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer == null || layer.stateMachine == null)
                    continue;

                ScanAnimationStateMachine(
                    controller,
                    layer.name,
                    layer.stateMachine,
                    "",
                    new HashSet<BlendTree>());
            }
        }

        animationSwapDirty = false;
    }

    void ScanAnimationStateMachine(
        AnimatorController controller,
        string layerName,
        AnimatorStateMachine machine,
        string parentPath,
        HashSet<BlendTree> blendTreeStack)
    {
        if (machine == null)
            return;

        string machinePath = string.IsNullOrEmpty(parentPath)
            ? machine.name
            : parentPath + "/" + machine.name;

        foreach (ChildAnimatorState child in machine.states)
        {
            AnimatorState state = child.state;
            if (state == null || state.motion == null)
                continue;

            string statePath = machinePath + "/" + state.name;
            ScanAnimationMotion(
                controller,
                layerName,
                statePath,
                state,
                state.motion,
                null,
                -1,
                "",
                blendTreeStack);
        }

        foreach (ChildAnimatorStateMachine childMachine in machine.stateMachines)
        {
            if (childMachine.stateMachine != null)
            {
                ScanAnimationStateMachine(
                    controller,
                    layerName,
                    childMachine.stateMachine,
                    machinePath,
                    blendTreeStack);
            }
        }
    }

    void ScanAnimationMotion(
        AnimatorController controller,
        string layerName,
        string statePath,
        AnimatorState state,
        Motion motion,
        BlendTree owningTree,
        int childIndex,
        string treePath,
        HashSet<BlendTree> blendTreeStack)
    {
        if (motion == null)
            return;

        if (motion is AnimationClip clip)
        {
            if (animationSwapSource != null && clip != animationSwapSource)
                return;

            animationSwapUsages.Add(new AnimationClipUsage
            {
                selected = true,
                clip = clip,
                controller = controller,
                layerName = layerName,
                statePath = statePath,
                state = owningTree == null ? state : null,
                blendTree = owningTree,
                blendTreeChildIndex = childIndex,
                location = owningTree == null
                    ? "Animator State motion"
                    : "Blend Tree: " + treePath + " · child " + (childIndex + 1)
            });

            return;
        }

        BlendTree tree = motion as BlendTree;
        if (tree == null || blendTreeStack.Contains(tree))
            return;

        blendTreeStack.Add(tree);

        ChildMotion[] children = tree.children;
        for (int i = 0; i < children.Length; i++)
        {
            Motion childMotion = children[i].motion;
            string nextTreePath = string.IsNullOrEmpty(treePath)
                ? tree.name
                : treePath + "/" + tree.name;

            ScanAnimationMotion(
                controller,
                layerName,
                statePath,
                state,
                childMotion,
                tree,
                i,
                nextTreePath,
                blendTreeStack);
        }

        blendTreeStack.Remove(tree);
    }

    void SetAnimationUsageSelection(bool value)
    {
        foreach (AnimationClipUsage usage in animationSwapUsages)
            usage.selected = value;
    }

    void ReplaceAnimationUsages(bool replaceEverywhere)
    {
        if (animationSwapSource == null || animationSwapReplacement == null)
            return;

        List<AnimationClipUsage> targets = animationSwapUsages
            .Where(x =>
                x.clip == animationSwapSource &&
                (replaceEverywhere || x.selected))
            .ToList();

        if (targets.Count == 0)
            return;

        string scope = replaceEverywhere ? "all" : "the selected";
        string message =
            "Replace " + targets.Count + " reference" + (targets.Count == 1 ? "" : "s") +
            " to '" + animationSwapSource.name + "' with '" + animationSwapReplacement.name + "'?\n\n" +
            "This changes " + scope + " matching Animator State / Blend Tree references shown by the current scan. " +
            "The original AnimationClip asset is not deleted.";

        if (confirmDestructiveActions &&
            !EditorUtility.DisplayDialog(
                "Replace Animation References",
                message,
                "Replace",
                "Cancel"))
        {
            return;
        }

        Run("Replace Animation References", () =>
        {
            int changed = 0;

            foreach (AnimationClipUsage usage in targets)
            {
                if (usage.blendTree != null && usage.blendTreeChildIndex >= 0)
                {
                    ChildMotion[] children = usage.blendTree.children;
                    int index = usage.blendTreeChildIndex;

                    if (index < 0 || index >= children.Length || children[index].motion != animationSwapSource)
                        continue;

                    Undo.RecordObject(usage.blendTree, "Replace Animation Clip");
                    ChildMotion child = children[index];
                    child.motion = animationSwapReplacement;
                    children[index] = child;
                    usage.blendTree.children = children;
                    EditorUtility.SetDirty(usage.blendTree);
                    changed++;
                }
                else if (usage.state != null && usage.state.motion == animationSwapSource)
                {
                    Undo.RecordObject(usage.state, "Replace Animation Clip");
                    usage.state.motion = animationSwapReplacement;
                    EditorUtility.SetDirty(usage.state);
                    changed++;
                }
            }

            AssetDatabase.SaveAssets();

            if (changed == 0)
                throw new InvalidOperationException(
                    "No references were changed. The Animator Controllers may have changed since the last scan.");

            animatorDoctorDirty = true;
            preflightDirty = true;
            animationSwapDirty = true;

            ShowNotification(new GUIContent(
                "Replaced " + changed + " animation reference" + (changed == 1 ? "" : "s")));
        });

        ScanAnimationClipUsage();
    }
}
