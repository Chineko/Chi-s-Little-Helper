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
    bool constraintConvertAnimationClips = true;
    bool constraintConvertSelectedOnly = false;

    void DrawConstraintToolkit()
    {
        BeginCard(CardIcon.Armature, "Constraint Toolkit", "Create, inspect and manage Unity animation constraints on the selected avatar.");

        DrawUnityParentConstraintConverter();

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Create Constraint", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColLavenderText },
            fontSize = 13
        });
        GUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Target", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        constraintTarget = (GameObject)DrawThemedObjectField(constraintTarget, typeof(GameObject), true);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Source", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        constraintSource = (Transform)DrawThemedObjectField(constraintSource, typeof(Transform), true);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Type", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        constraintType = GUILayout.Toolbar(constraintType,
            new[] { "Parent", "Position", "Rotation", "Scale", "Aim" },
            styleBtnDark, GUILayout.Height(28));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Weight", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        constraintWeight = EditorGUILayout.Slider(constraintWeight, 0f, 1f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Active", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        constraintActivate = EditorGUILayout.Toggle(constraintActivate);
        EditorGUILayout.EndHorizontal();

        bool targetValid = constraintTarget != null && avatar != null &&
                           constraintTarget.transform.IsChildOf(avatar.transform);
        bool sourceValid = constraintSource != null;

        GUILayout.Space(8);
        using (new EditorGUI.DisabledScope(!targetValid || !sourceValid))
        {
            if (GUILayout.Button("Create Constraint", styleBtnGrad, GUILayout.Height(38)))
                Run("Create Constraint", CreateConstraintFromToolkit);
        }

        if (constraintTarget != null && !targetValid)
            EditorGUILayout.HelpBox("Target must be part of the selected avatar.", MessageType.Warning);

        GUILayout.Space(12);
        if (IsNarrowLayout)
        {
            EditorGUILayout.LabelField("Existing Constraints", new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColLavenderText }
            });
            GUILayout.Space(3);
            constraintSearch = EditorGUILayout.TextField(constraintSearch, styleInputBox);
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Existing Constraints", new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColLavenderText }
            });
            GUILayout.FlexibleSpace();
            constraintSearch = EditorGUILayout.TextField(constraintSearch, styleInputBox, GUILayout.Width(180));
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(6);

        var constraints = GetAvatarConstraints();
        string search = (constraintSearch ?? "").Trim();

        foreach (Component c in constraints)
        {
            if (c == null) continue;
            if (search.Length > 0 &&
                c.gameObject.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                c.GetType().Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var ic = c as IConstraint;
            int sources = ic != null ? ic.sourceCount : 0;
            float weight = ic != null ? ic.weight : 0f;
            bool active = ic != null && ic.constraintActive;

            EditorGUILayout.BeginVertical(styleRowBg);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(c.gameObject.name, new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColTextPrimary }
            });
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(c.GetType().Name, styleCardSubtitle, GUILayout.Width(130));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                $"Sources: {sources}   ·   Weight: {weight:0.00}   ·   Active: {(active ? "Yes" : "No")}",
                styleCardSubtitle);

            BeginResponsiveButtonRow();
            if (GUILayout.Button("Select", styleBtnDark, GUILayout.Height(26), GUILayout.ExpandWidth(true)))
            {
                Selection.activeObject = c.gameObject;
                EditorGUIUtility.PingObject(c.gameObject);
            }

            ResponsiveButtonGap();
            if (ic != null && GUILayout.Button(active ? "Disable" : "Enable", styleBtnDark, GUILayout.Height(26), GUILayout.ExpandWidth(true)))
            {
                Run(active ? "Disable Constraint" : "Enable Constraint", () =>
                {
                    Undo.RecordObject(c, active ? "Disable Constraint" : "Enable Constraint");
                    ic.constraintActive = !active;
                    EditorUtility.SetDirty(c);
                    doctorDirty = true;
                });
            }

            ResponsiveButtonGap();
            if (GUILayout.Button("Delete", styleBtnRemove, ResponsiveButtonWidth(64), GUILayout.Height(26)))
            {
                RunDestructive(
                    "Delete Constraint",
                    "Delete " + c.GetType().Name + " from '" + c.gameObject.name + "'?",
                    () =>
                    {
                        Undo.DestroyObjectImmediate(c);
                        doctorDirty = true;
                    });
                GUIUtility.ExitGUI();
            }
            EndResponsiveButtonRow();

            EditorGUILayout.EndVertical();
            GUILayout.Space(6);
        }

        if (constraints.Count == 0)
            EditorGUILayout.HelpBox("No Unity animation constraints were found on this avatar.", MessageType.Info);

        DrawInfoBox("This page manages UnityEngine.Animations constraints. It does not rewrite or convert VRCFury/VRC constraint systems.");
        EndCard();
    }


    void DrawUnityParentConstraintConverter()
    {
        int avatarCount = avatar != null
            ? avatar.GetComponentsInChildren<ParentConstraint>(true).Length
            : 0;

        GameObject selected = Selection.activeGameObject;
        bool selectedInsideAvatar =
            avatar != null &&
            selected != null &&
            (selected == avatar.gameObject || selected.transform.IsChildOf(avatar.transform));

        int selectedCount = selectedInsideAvatar
            ? selected.GetComponents<ParentConstraint>().Length
            : 0;

        EditorGUILayout.BeginVertical(styleRowBg);

        EditorGUILayout.LabelField("Unity → VRChat Parent Constraints", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = ColTextPrimary },
            fontSize = 12
        });

        EditorGUILayout.LabelField(
            "Uses the VRChat SDK's own documented constraint converter instead of recreating its conversion logic.",
            styleInfoText);

        GUILayout.Space(7);

        if (avatar == null)
        {
            EditorGUILayout.HelpBox("Select an avatar before converting constraints.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Scope", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        constraintConvertSelectedOnly = GUILayout.Toggle(
            constraintConvertSelectedOnly,
            "Selected Object Only",
            GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Animation Clips", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        constraintConvertAnimationClips =
            EditorGUILayout.Toggle(constraintConvertAnimationClips, GUILayout.Width(20));
        EditorGUILayout.LabelField("Retarget referenced constraint animation bindings", styleCardSubtitle);
        EditorGUILayout.EndHorizontal();

        int targetCount = constraintConvertSelectedOnly ? selectedCount : avatarCount;

        GUILayout.Space(5);
        EditorGUILayout.LabelField(
            constraintConvertSelectedOnly
                ? $"{targetCount} ParentConstraint component{(targetCount == 1 ? "" : "s")} on selected object"
                : $"{targetCount} ParentConstraint component{(targetCount == 1 ? "" : "s")} on avatar",
            styleCardSubtitle);

        bool apiAvailable = HasVrchatConstraintConverter();
        if (!apiAvailable)
        {
            EditorGUILayout.HelpBox(
                "The installed VRChat SDK does not expose AvatarDynamicsSetup.DoConvertUnityConstraints. Update the VRChat Avatars SDK to use this converter.",
                MessageType.Warning);
        }

        GUILayout.Space(6);

        using (new EditorGUI.DisabledScope(
            !apiAvailable ||
            targetCount == 0 ||
            (constraintConvertSelectedOnly && !selectedInsideAvatar)))
        {
            if (GUILayout.Button(
                $"Convert {targetCount} Parent Constraint{(targetCount == 1 ? "" : "s")}",
                styleBtnGrad,
                GUILayout.Height(38)))
            {
                RunDestructive(
                    "Convert Unity Parent Constraints",
                    BuildParentConstraintConversionConfirmation(targetCount),
                    ConvertUnityParentConstraintsToVrchat);
            }
        }

        if (targetCount == 0)
        {
            EditorGUILayout.HelpBox(
                constraintConvertSelectedOnly
                    ? "The selected GameObject has no Unity ParentConstraint component."
                    : "No Unity ParentConstraint components were found on this avatar.",
                MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }

    bool HasVrchatConstraintConverter()
    {
        Type setup = FindType("VRC.SDK3.Avatars.AvatarDynamicsSetup");
        if (setup == null) return false;

        return setup.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Any(m =>
            {
                if (m.Name != "DoConvertUnityConstraints") return false;
                var p = m.GetParameters();
                return p.Length == 3 &&
                       p[0].ParameterType.IsArray &&
                       typeof(VRCAvatarDescriptor).IsAssignableFrom(p[1].ParameterType) &&
                       p[2].ParameterType == typeof(bool);
            });
    }

    string BuildParentConstraintConversionConfirmation(int count)
    {
        return
            $"Convert {count} Unity ParentConstraint component{(count == 1 ? "" : "s")} to VRChat constraints?" +
            "\n\nScope: " + (constraintConvertSelectedOnly ? "Selected GameObject" : "Entire avatar") +
            "\nAnimation bindings: " + (constraintConvertAnimationClips ? "Convert referenced clips" : "Do not convert clips") +
            "\n\nThe VRChat SDK performs the actual conversion. Existing Little Helper confirmation, Undo, and optional snapshot safety remain active.";
    }

    void ConvertUnityParentConstraintsToVrchat()
    {
        if (avatar == null)
            throw new InvalidOperationException("No avatar is selected.");

        ParentConstraint[] constraints;

        if (constraintConvertSelectedOnly)
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null ||
                (selected != avatar.gameObject && !selected.transform.IsChildOf(avatar.transform)))
                throw new InvalidOperationException("Select a GameObject inside the active avatar.");

            constraints = selected.GetComponents<ParentConstraint>();
        }
        else
        {
            constraints = avatar.GetComponentsInChildren<ParentConstraint>(true);
        }

        constraints = constraints.Where(x => x != null).ToArray();
        if (constraints.Length == 0)
            throw new InvalidOperationException("No Unity ParentConstraint components were found.");

        Type setup = FindType("VRC.SDK3.Avatars.AvatarDynamicsSetup");
        if (setup == null)
            throw new InvalidOperationException("VRChat SDK AvatarDynamicsSetup was not found.");

        MethodInfo method = setup
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(m =>
            {
                if (m.Name != "DoConvertUnityConstraints") return false;
                var p = m.GetParameters();
                return p.Length == 3 &&
                       p[0].ParameterType.IsArray &&
                       typeof(VRCAvatarDescriptor).IsAssignableFrom(p[1].ParameterType) &&
                       p[2].ParameterType == typeof(bool);
            });

        if (method == null)
            throw new MissingMethodException("VRChat SDK DoConvertUnityConstraints was not found.");

        Type elementType = method.GetParameters()[0].ParameterType.GetElementType();
        Array typedArray = Array.CreateInstance(elementType, constraints.Length);

        for (int i = 0; i < constraints.Length; i++)
        {
            if (!elementType.IsInstanceOfType(constraints[i]))
                throw new InvalidOperationException(
                    $"SDK converter expected {elementType.FullName}, but ParentConstraint could not be assigned.");

            typedArray.SetValue(constraints[i], i);
        }

        try
        {
            method.Invoke(null, new object[]
            {
                typedArray,
                avatar,
                constraintConvertAnimationClips
            });
        }
        catch (TargetInvocationException ex)
        {
            throw new InvalidOperationException(
                "VRChat SDK conversion failed: " +
                (ex.InnerException?.Message ?? ex.Message),
                ex.InnerException ?? ex);
        }

        doctorDirty = true;
        overviewDirty = true;
        Repaint();

        ShowNotification(new GUIContent("VRChat constraint conversion complete"));
    }

    List<Component> GetAvatarConstraints()
    {
        var result = new List<Component>();
        if (avatar == null) return result;

        result.AddRange(avatar.GetComponentsInChildren<ParentConstraint>(true).Cast<Component>());
        result.AddRange(avatar.GetComponentsInChildren<PositionConstraint>(true).Cast<Component>());
        result.AddRange(avatar.GetComponentsInChildren<RotationConstraint>(true).Cast<Component>());
        result.AddRange(avatar.GetComponentsInChildren<ScaleConstraint>(true).Cast<Component>());
        result.AddRange(avatar.GetComponentsInChildren<AimConstraint>(true).Cast<Component>());
        result.AddRange(avatar.GetComponentsInChildren<LookAtConstraint>(true).Cast<Component>());
        return result;
    }

    void CreateConstraintFromToolkit()
    {
        if (constraintTarget == null || constraintSource == null || avatar == null)
            return;

        if (!constraintTarget.transform.IsChildOf(avatar.transform))
            throw new Exception("Constraint target must be part of the selected avatar.");

        Type t =
            constraintType == 0 ? typeof(ParentConstraint) :
            constraintType == 1 ? typeof(PositionConstraint) :
            constraintType == 2 ? typeof(RotationConstraint) :
            constraintType == 3 ? typeof(ScaleConstraint) :
                                  typeof(AimConstraint);

        Component comp = Undo.AddComponent(constraintTarget, t);
        if (comp == null)
            throw new Exception("Unity could not create the selected constraint component.");

        var constraint = comp as IConstraint;
        if (constraint == null)
            throw new Exception("The created component does not implement IConstraint.");

        constraint.AddSource(new ConstraintSource
        {
            sourceTransform = constraintSource,
            weight = 1f
        });
        constraint.weight = Mathf.Clamp01(constraintWeight);
        constraint.constraintActive = constraintActivate;

        // Sensible defaults for newly-created Aim Constraints.
        var aim = comp as AimConstraint;
        if (aim != null)
        {
            aim.aimVector = Vector3.forward;
            aim.upVector = Vector3.up;
            aim.worldUpType = AimConstraint.WorldUpType.SceneUp;
        }

        EditorUtility.SetDirty(comp);
        doctorDirty = true;
        Selection.activeObject = comp;
        EditorGUIUtility.PingObject(comp);
    }

    void DrawPhysBoneManager()
    {
        BeginCard(CardIcon.Sparkle, "PhysBone Manager", "Inspect and edit every VRChat PhysBone on the selected avatar.");

        Type pbType = FindType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone");
        if (pbType == null)
        {
            EditorGUILayout.HelpBox("VRChat PhysBone components could not be found. Make sure the VRChat Avatars SDK is installed.", MessageType.Error);
            EndCard();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Search", styleFieldLabel, GUILayout.Width(62));
        physBoneSearch = EditorGUILayout.TextField(physBoneSearch, styleInputBox);
        GUILayout.Space(8);
        physBoneShowInactive = GUILayout.Toggle(physBoneShowInactive, "Inactive", GUILayout.Width(72));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);

        var all = avatar != null
            ? avatar.gameObject.GetComponentsInChildren(pbType, physBoneShowInactive)
            : new Component[0];

        var filtered = new List<Component>();
        string q = (physBoneSearch ?? "").Trim();
        foreach (Component c in all)
        {
            if (c == null) continue;
            if (q.Length == 0 ||
                c.gameObject.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                GetPhysBoneRootName(c).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                filtered.Add(c);
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{filtered.Count} shown · {all.Length} total", styleCardSubtitle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Select All", styleBtnDark, GUILayout.Width(82), GUILayout.Height(26)))
            Selection.objects = filtered.Select(x => x.gameObject).Distinct().ToArray();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);

        if (filtered.Count == 0)
        {
            EditorGUILayout.HelpBox(all.Length == 0
                ? "No PhysBones were found on this avatar."
                : "No PhysBones match the current search.", MessageType.Info);
        }

        physBoneScroll = EditorGUILayout.BeginScrollView(physBoneScroll, GUILayout.MinHeight(120), GUILayout.MaxHeight(500));
        foreach (Component pb in filtered)
        {
            if (pb == null) continue;

            EditorGUILayout.BeginVertical(styleRowBg);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(pb.gameObject.name, new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ColTextPrimary }
            });
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Select", styleBtnDark, GUILayout.Width(58), GUILayout.Height(24)))
            {
                Selection.activeObject = pb.gameObject;
                EditorGUIUtility.PingObject(pb.gameObject);
            }
            EditorGUILayout.EndHorizontal();

            string rootName = GetPhysBoneRootName(pb);
            int affected = CountPhysBoneTransforms(pb);
            int colliderCount = CountPhysBoneColliders(pb);
            bool allowGrabbing = GetBoolMember(pb, "allowGrabbing", false);
            bool allowPosing = GetBoolMember(pb, "allowPosing", false);

            EditorGUILayout.LabelField(
                $"Root: {rootName}   ·   Transforms: {affected}   ·   Colliders: {colliderCount}",
                styleCardSubtitle);
            EditorGUILayout.LabelField(
                $"Grab: {(allowGrabbing ? "On" : "Off")}   ·   Pose: {(allowPosing ? "On" : "Off")}",
                styleCardSubtitle);

            GUILayout.Space(5);
            BeginResponsiveButtonRow();

            if (GUILayout.Button("Copy", styleBtnDark, GUILayout.Height(26), GUILayout.ExpandWidth(true)))
                CopyPhysBoneSettings(pb);

            ResponsiveButtonGap(3f);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(physBoneClipboardJson)))
            {
                if (GUILayout.Button("Paste", styleBtnDark, GUILayout.Height(26), GUILayout.ExpandWidth(true)))
                    Run("Paste PhysBone Settings", () => PastePhysBoneSettings(pb));
            }

            ResponsiveButtonGap(3f);
            if (GUILayout.Button("Duplicate", styleBtnDark, GUILayout.Height(26), GUILayout.ExpandWidth(true)))
                Run("Duplicate PhysBone", () => DuplicatePhysBone(pb));

            ResponsiveButtonGap(3f);
            if (GUILayout.Button("Mirror L↔R", styleBtnDark, GUILayout.Height(26), GUILayout.ExpandWidth(true)))
                Run("Mirror PhysBone", () => MirrorPhysBone(pb));

            ResponsiveButtonGap(3f);
            if (GUILayout.Button("Delete", styleBtnRemove, ResponsiveButtonWidth(64), GUILayout.Height(26)))
            {
                RunDestructive(
                    "Delete PhysBone",
                    "Delete the selected PhysBone component from '" + pb.gameObject.name + "'?",
                    () =>
                    {
                        Undo.DestroyObjectImmediate(pb);
                        doctorDirty = true;
                    });
                GUIUtility.ExitGUI();
            }

            EndResponsiveButtonRow();
            EditorGUILayout.EndVertical();
            GUILayout.Space(6);
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);
        DrawInfoBox("Copy/Paste transfers serialized PhysBone settings while preserving the destination component's GameObject. Mirror searches for a matching left/right object and copies the settings there.");

        EndCard();
    }

    string GetPhysBoneRootName(Component pb)
    {
        object rootObj = GetMember(pb, "rootTransform");
        var root = rootObj as Transform;
        return root != null ? root.name : "(self)";
    }

    int CountPhysBoneTransforms(Component pb)
    {
        Transform root = GetMember(pb, "rootTransform") as Transform;
        if (root == null) root = pb.transform;
        if (root == null) return 0;

        // Approximation for quick diagnostics. Ignore lists are respected when available.
        var ignored = new HashSet<Transform>();
        object ignoreObj = GetMember(pb, "ignoreTransforms");
        if (ignoreObj is System.Collections.IEnumerable enumerable)
        {
            foreach (object x in enumerable)
                if (x is Transform t) ignored.Add(t);
        }

        int count = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (!ignored.Contains(t)) count++;
        return count;
    }

    int CountPhysBoneColliders(Component pb)
    {
        object obj = GetMember(pb, "colliders");
        if (obj is System.Collections.ICollection col) return col.Count;
        if (obj is System.Collections.IEnumerable enumerable)
        {
            int n = 0;
            foreach (var _ in enumerable) n++;
            return n;
        }
        return 0;
    }

    bool GetBoolMember(object obj, string name, bool fallback)
    {
        object v = GetMember(obj, name);
        return v is bool b ? b : fallback;
    }

    void CopyPhysBoneSettings(Component pb)
    {
        if (pb == null) return;
        physBoneClipboard = pb;
        physBoneClipboardType = pb.GetType().AssemblyQualifiedName;
        physBoneClipboardJson = EditorJsonUtility.ToJson(pb);
        ShowNotification(new GUIContent("PhysBone settings copied"));
    }

    void PastePhysBoneSettings(Component destination)
    {
        if (destination == null || string.IsNullOrEmpty(physBoneClipboardJson)) return;

        string destType = destination.GetType().AssemblyQualifiedName;
        if (!string.Equals(destType, physBoneClipboardType, StringComparison.Ordinal))
            throw new Exception("The copied component type does not match the destination PhysBone.");

        Undo.RecordObject(destination, "Paste PhysBone Settings");

        // Preserve identity references that should stay tied to the destination object.
        Transform oldRoot = GetMember(destination, "rootTransform") as Transform;
        EditorJsonUtility.FromJsonOverwrite(physBoneClipboardJson, destination);

        // A null/self root on the source should not unexpectedly retarget the destination.
        if (oldRoot != null && GetMember(destination, "rootTransform") == null)
            TrySet(destination, "rootTransform", oldRoot, false);

        EditorUtility.SetDirty(destination);
        doctorDirty = true;
    }

    void DuplicatePhysBone(Component source)
    {
        if (source == null) return;

        Component dest = Undo.AddComponent(source.gameObject, source.GetType());
        if (dest == null) throw new Exception("Could not create the duplicate PhysBone.");

        string json = EditorJsonUtility.ToJson(source);
        EditorJsonUtility.FromJsonOverwrite(json, dest);
        EditorUtility.SetDirty(dest);

        doctorDirty = true;
        Selection.activeObject = dest;
    }

    void MirrorPhysBone(Component source)
    {
        if (source == null || avatar == null) return;

        GameObject mirror = FindMirroredObject(source.gameObject);
        if (mirror == null)
            throw new Exception("No matching left/right object was found. Supported pairs include Left/Right, _L/_R, .L/.R, and L_/R_.");

        Component destination = mirror.GetComponent(source.GetType());
        if (destination == null)
            destination = Undo.AddComponent(mirror, source.GetType());

        Undo.RecordObject(destination, "Mirror PhysBone");
        string json = EditorJsonUtility.ToJson(source);
        EditorJsonUtility.FromJsonOverwrite(json, destination);

        // Retarget rootTransform when the mirrored counterpart exists.
        Transform sourceRoot = GetMember(source, "rootTransform") as Transform;
        if (sourceRoot != null)
        {
            GameObject mirroredRoot = FindMirroredObject(sourceRoot.gameObject);
            if (mirroredRoot != null)
                TrySet(destination, "rootTransform", mirroredRoot.transform, false);
        }

        EditorUtility.SetDirty(destination);
        doctorDirty = true;
        Selection.activeObject = mirror;
        EditorGUIUtility.PingObject(mirror);
    }

    GameObject FindMirroredObject(GameObject source)
    {
        if (source == null || avatar == null) return null;

        string mirroredName = MirrorName(source.name);
        if (mirroredName == source.name) return null;

        foreach (Transform t in avatar.GetComponentsInChildren<Transform>(true))
            if (t.gameObject != source && string.Equals(t.name, mirroredName, StringComparison.OrdinalIgnoreCase))
                return t.gameObject;

        return null;
    }

    string MirrorName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var pairs = new[]
        {
            new[] { "Left", "Right" },
            new[] { "left", "right" },
            new[] { "_L", "_R" },
            new[] { ".L", ".R" },
            new[] { "L_", "R_" },
            new[] { "-L", "-R" }
        };

        foreach (var p in pairs)
        {
            if (name.Contains(p[0])) return name.Replace(p[0], p[1]);
            if (name.Contains(p[1])) return name.Replace(p[1], p[0]);
        }
        return name;
    }

    void DrawContactSetup()
    {
        BeginCard(CardIcon.Sparkle, "Contact Setup Helper", "Create common VRChat Contact Senders and Receivers without repetitive Inspector setup.");
        DrawInfoBox("Contacts use the VRChat SDK directly. Sender and Receiver collision tags must match for them to interact.");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Target object", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        contactTarget = (GameObject)DrawThemedObjectField(contactTarget, typeof(GameObject), true, GUILayout.Height(24));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Contact type", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        if (GUILayout.Toggle(contactCreateSender, "Sender", styleBtnDark, GUILayout.Height(30)))
            contactCreateSender = true;
        GUILayout.Space(6);
        if (GUILayout.Toggle(!contactCreateSender, "Receiver", styleBtnDark, GUILayout.Height(30)))
            contactCreateSender = false;
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Collision tag", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        contactTag = EditorGUILayout.TextField(contactTag, styleInputBox);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Shape", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        contactShape = GUILayout.Toolbar(contactShape, new[] { "Sphere", "Capsule", "Box" }, styleBtnDark, GUILayout.Height(28));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        if (contactShape == 0 || contactShape == 1)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Radius", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
            contactRadius = Mathf.Max(0f, EditorGUILayout.FloatField(contactRadius, styleNumberField));
            EditorGUILayout.EndHorizontal();
        }
        if (contactShape == 1)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Height", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
            contactHeight = Mathf.Max(0f, EditorGUILayout.FloatField(contactHeight, styleNumberField));
            EditorGUILayout.EndHorizontal();
        }
        if (contactShape == 2)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Box size", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
            contactBoxSize = EditorGUILayout.Vector3Field(GUIContent.none, contactBoxSize);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Position", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        contactPosition = EditorGUILayout.Vector3Field(GUIContent.none, contactPosition);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Rotation", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        contactRotation = EditorGUILayout.Vector3Field(GUIContent.none, contactRotation);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Local Only", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        contactLocalOnly = EditorGUILayout.Toggle(contactLocalOnly, GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();

        if (!contactCreateSender)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Allow Self", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
            contactAllowSelf = EditorGUILayout.Toggle(contactAllowSelf, GUILayout.Width(20));
            GUILayout.Space(16);
            EditorGUILayout.LabelField("Allow Others", styleFieldLabel, GUILayout.Width(82));
            contactAllowOthers = EditorGUILayout.Toggle(contactAllowOthers, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Receiver type", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
            contactReceiverType = GUILayout.Toolbar(contactReceiverType, new[] { "Constant", "On Enter", "Proximity" }, styleBtnDark, GUILayout.Height(28));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Parameter", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
            contactParameter = EditorGUILayout.TextField(contactParameter, styleInputBox);
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(14);

        bool validTarget = contactTarget != null && avatar != null && contactTarget.transform.IsChildOf(avatar.transform);
        using (new EditorGUI.DisabledScope(!validTarget || string.IsNullOrWhiteSpace(contactTag)))
        {
            string label = contactCreateSender ? "Create Contact Sender" : "Create Contact Receiver";
            if (GUILayout.Button(label, styleBtnGrad, GUILayout.Height(42)))
                Run(label, CreateContact);
        }

        if (contactTarget != null && !validTarget)
            EditorGUILayout.HelpBox("The target object must be inside the selected avatar.", MessageType.Warning);

        EndCard();
    }

    void CreateContact()
    {
        if (avatar == null || contactTarget == null) return;
        if (!contactTarget.transform.IsChildOf(avatar.transform))
            throw new Exception("The contact target is not inside the selected avatar.");

        Type t = FindType(contactCreateSender ? T_CONTACT_SENDER : T_CONTACT_RECEIVER);
        if (t == null)
            throw new Exception("The VRChat Contact component type could not be found. Check that the current VRChat Avatars SDK is installed.");

        Component comp = Undo.AddComponent(contactTarget, t);
        if (comp == null) throw new Exception("Unity could not add the Contact component.");

        // These names match the SDK's serialized Contact fields. TrySet is deliberately
        // tolerant so a minor SDK revision does not stop the rest of the helper.
        TrySet(comp, "rootTransform", contactTarget.transform, false);
        SetEnumMember(comp, "shapeType", contactShape == 0 ? "Sphere" : contactShape == 1 ? "Capsule" : "Box");
        TrySet(comp, "radius", Mathf.Clamp(contactRadius, 0f, 3f), false);
        TrySet(comp, "height", Mathf.Clamp(contactHeight, 0f, 6f), false);
        TrySet(comp, "size", new Vector3(
            Mathf.Clamp(contactBoxSize.x, 0f, 6f),
            Mathf.Clamp(contactBoxSize.y, 0f, 6f),
            Mathf.Clamp(contactBoxSize.z, 0f, 6f)), false);
        TrySet(comp, "position", contactPosition, false);
        TrySet(comp, "rotation", Quaternion.Euler(contactRotation), false);
        TrySet(comp, "localOnly", contactLocalOnly, false);
        SetStringListMember(comp, "collisionTags", contactTag.Trim());

        if (!contactCreateSender)
        {
            TrySet(comp, "allowSelf", contactAllowSelf, false);
            TrySet(comp, "allowOthers", contactAllowOthers, false);
            TrySet(comp, "parameter", string.IsNullOrWhiteSpace(contactParameter) ? "Contact" : contactParameter.Trim(), false);
            SetEnumMember(comp, "receiverType",
                contactReceiverType == 0 ? "Constant" :
                contactReceiverType == 1 ? "OnEnter" : "Proximity");
        }

        EditorUtility.SetDirty(comp);
        Selection.activeObject = comp;
        EditorGUIUtility.PingObject(comp);
    }

    static bool SetEnumMember(object obj, string memberName, string enumName)
    {
        if (obj == null) return false;
        var m = FindMember(obj.GetType(), memberName);
        Type mt = (m as FieldInfo)?.FieldType ?? (m as PropertyInfo)?.PropertyType;
        if (mt == null || !mt.IsEnum) return false;
        try
        {
            object v = Enum.Parse(mt, enumName, true);
            return TrySet(obj, memberName, v, false);
        }
        catch { return false; }
    }

    static bool SetStringListMember(object obj, string memberName, string value)
    {
        object listObj = GetMember(obj, memberName);
        if (listObj is System.Collections.IList list)
        {
            list.Clear();
            list.Add(value);
            return true;
        }

        var m = FindMember(obj.GetType(), memberName);
        Type mt = (m as FieldInfo)?.FieldType ?? (m as PropertyInfo)?.PropertyType;
        if (mt == typeof(string[]))
            return TrySet(obj, memberName, new[] { value }, false);

        return false;
    }
}
