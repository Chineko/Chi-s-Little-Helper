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
    static GameObject NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        Undo.SetTransformParent(go.transform, parent, "Parent " + name);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go;
    }

    static bool AddFeature(GameObject go, string featureTypeName, out Component comp, out object feature)
    {
        comp = null; feature = null;
        var vf = FindType(T_VRCFURY);
        var ft = FindType(featureTypeName);
        if (vf == null || ft == null) return false;
        comp = Undo.AddComponent(go, vf);
        feature = Activator.CreateInstance(ft);
        return TrySet(comp, "content", feature);
    }

    static Type FindType(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return null;

        if (TypeCache.TryGetValue(fullName, out var cached))
            return cached;
        if (MissingTypeCache.Contains(fullName))
            return null;

        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = a.GetType(fullName, false);
                if (t != null)
                {
                    TypeCache[fullName] = t;
                    return t;
                }
            }
            catch { }
        }

        MissingTypeCache.Add(fullName);
        return null;
    }

    static MemberInfo FindMember(Type t, string name)
    {
        if (t == null || string.IsNullOrEmpty(name)) return null;

        string key = t.AssemblyQualifiedName + "|" + name;
        if (MemberCache.TryGetValue(key, out var cached))
            return cached;
        if (MissingMemberCache.Contains(key))
            return null;

        MemberInfo member =
            (MemberInfo)t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
            t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (member != null)
            MemberCache[key] = member;
        else
            MissingMemberCache.Add(key);

        return member;
    }

    static object GetMember(object o, string name)
    {
        if (o == null) return null;
        var m = FindMember(o.GetType(), name);
        if (m is FieldInfo f) return f.GetValue(o);
        if (m is PropertyInfo p && p.CanRead) return p.GetValue(o);
        return null;
    }

    static object EnsureState(object feature, string name)
    {
        var existing = GetMember(feature, name);
        if (existing != null) return existing;
        var m = FindMember(feature.GetType(), name);
        var type = (m as FieldInfo)?.FieldType ?? (m as PropertyInfo)?.PropertyType;
        if (type == null) return null;
        var created = Activator.CreateInstance(type);
        TrySet(feature, name, created);
        return created;
    }

    static bool TrySet(object o, string name, object value, bool warn = true)
    {
        if (o == null) return false;
        var t = o.GetType();
        try
        {
            var m = FindMember(t, name);
            if (m is FieldInfo f) { f.SetValue(o, value); return true; }
            if (m is PropertyInfo p && p.CanWrite) { p.SetValue(o, value); return true; }
        }
        catch { }
        return false;
    }

    static void TrySetLinkTo(object armatureLink, GameObject hips)
    {
        var f = FindMember(armatureLink.GetType(), "linkTo") as FieldInfo;
        if (f == null || !(f.GetValue(armatureLink) is System.Collections.IList list)) return;
        var elemType = f.FieldType.IsGenericType ? f.FieldType.GetGenericArguments()[0] : null;
        if (elemType == null) return;
        var entry = Activator.CreateInstance(elemType);
        if (!TrySet(entry, "obj", hips, warn: false) && !TrySet(entry, "bone", hips)) return;
        list.Clear();
        list.Add(entry);
    }

    static void AddVRCFuryToVCC()
    {
        try
        {
            Application.OpenURL(VRCFURY_VCC_URL);

            EditorApplication.delayCall += () =>
            {
                bool openDownload = EditorUtility.DisplayDialog(
                    "VRCFury Added to VCC",
                    "VRCFury has been added to your VRChat Creator Companion.\n\n" +
                    "Next steps:\n" +
                    "1. Open your avatar project in VCC\n" +
                    "2. Go to Manage Project - Packages\n" +
                    "3. Find VRCFury in the list and click Add\n\n" +
                    "If VCC did not open automatically, you may need to install it first.",
                    "Open VCC Download",
                    "I'll do it manually"
                );

                if (openDownload)
                    Application.OpenURL(VRCFURY_URL);
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AvatarTools] Failed to add VRCFury to VCC: {ex.Message}");
            EditorUtility.DisplayDialog(
                "VRCFury Setup",
                "Could not automatically add VRCFury to VCC.\n\n" +
                "Please make sure the VRChat Creator Companion is installed, then add VRCFury manually from the download page.",
                "Open Download Page",
                "Cancel"
            );
            Application.OpenURL(VRCFURY_URL);
        }
    }

    void CheckDependencies(bool log)
    {
        var report = new DependencyReport();
        try
        {
            report.unityVersion = Application.unityVersion;
            report.sdkVersion = GetPackageVersion(typeof(VRCAvatarDescriptor));
            report.sdkAssembly = typeof(VRCAvatarDescriptor).Assembly.GetName().Name;

            var vf = FindType(T_VRCFURY);
            report.vrcfuryFound = vf != null;
            if (vf != null)
            {
                report.vrcfuryVersion = GetPackageVersion(vf);
                report.vrcfuryAssembly = vf.Assembly.GetName().Name;
                var seen = new HashSet<string>();
                foreach (var req in TabRequirements)
                    foreach (var typeName in req)
                        if (typeName != T_VRCFURY && seen.Add(typeName) && FindType(typeName) == null)
                            report.missingTypes.Add(typeName);
            }
        }
        catch (Exception ex)
        {
            report.error = ex.Message;
            Debug.LogException(ex);
        }
        deps = report;

        if (!log) return;
        if (report.error != null)
            Debug.LogError("[Chi's Little Helper] Dependency check failed: " + report.error);
        else if (enableVRCFuryFeatures && !report.vrcfuryFound)
            Debug.LogWarning("[Chi's Little Helper] VRCFury features are enabled, but VRCFury was not found. Non-VRCFury tools remain available.");
        else if (enableVRCFuryFeatures && report.missingTypes.Count > 0)
            Debug.LogWarning("[Chi's Little Helper] VRCFury " + (report.vrcfuryVersion ?? "(unknown version)") + " is missing classes: " + string.Join(", ", report.missingTypes));
        else if (Verbose)
            Debug.Log($"[Chi's Little Helper] Dependencies OK. VRCFury {report.vrcfuryVersion}, SDK {report.sdkVersion}.");
    }

    static string GetPackageVersion(Type type)
    {
        if (type == null) return null;
        var assembly = type.Assembly;

        if (PackageVersionCache.TryGetValue(assembly, out var cached))
            return cached;

        try
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
            string version = info != null ? info.version : null;
            PackageVersionCache[assembly] = version;
            return version;
        }
        catch
        {
            PackageVersionCache[assembly] = null;
            return null;
        }
    }

    bool ConfirmDestructiveAction(string title, string detail)
    {
        if (!confirmDestructiveActions) return true;

        return EditorUtility.DisplayDialog(
            title,
            detail + "\n\nThis operation supports Unity Undo, but reviewing the target first is recommended.",
            "Continue",
            "Cancel");
    }

    GameObject CreateAvatarSnapshot(string reason)
    {
        if (avatar == null)
            throw new InvalidOperationException("No avatar is selected.");

        var source = avatar.gameObject;
        var clone = UnityEngine.Object.Instantiate(source, source.transform.parent);
        string stamp = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
        clone.name = $"[CLH Snapshot {stamp}] {source.name}";
        clone.SetActive(false);
        clone.hideFlags |= HideFlags.DontSaveInBuild;
        try { clone.tag = "EditorOnly"; } catch { }

        Undo.RegisterCreatedObjectUndo(clone, "Create Avatar Snapshot");
        clone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
        EditorUtility.SetDirty(clone);

        if (Verbose)
            Debug.Log($"[Chi's Little Helper] Created avatar snapshot before '{reason}': {clone.name}");

        return clone;
    }

    void RunDestructive(string what, string confirmationDetail, Action action, bool snapshotAvatar = true)
    {
        if (action == null) return;
        if (!ConfirmDestructiveAction(what, confirmationDetail)) return;

        Run(what, () =>
        {
            if (snapshotAvatar && autoBackupBeforeDestructive && avatar != null)
                CreateAvatarSnapshot(what);

            action();
        });
    }

    void Run(string what, Action action)
    {
        if (action == null) return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(what);

        try
        {
            lastError = null;
            lastErrorDetails = null;

            action();

            Undo.CollapseUndoOperations(undoGroup);
        }
        catch (ExitGUIException)
        {
            Undo.CollapseUndoOperations(undoGroup);
            throw;
        }
        catch (Exception ex)
        {
            try { Undo.RevertAllDownToGroup(undoGroup); }
            catch { }

            ReportError(what, ex);
        }
    }

    void ReportError(string context, Exception ex)
    {
        Exception root = UnwrapEditorException(ex);

        lastErrorContext = string.IsNullOrWhiteSpace(context) ? "Action" : context;
        lastErrorUtc = DateTime.UtcNow;
        lastError = BuildFriendlyErrorMessage(lastErrorContext, root);
        lastErrorHint = BuildErrorHint(root);
        lastErrorDetails = root != null ? root.ToString() : lastError;

        Debug.LogError(
            "[Chi's Little Helper] " + lastError + "\n" +
            (string.IsNullOrEmpty(lastErrorHint) ? "" : "Suggestion: " + lastErrorHint + "\n") +
            lastErrorDetails);
    }

    Exception UnwrapEditorException(Exception ex)
    {
        Exception current = ex;
        while (current is TargetInvocationException target && target.InnerException != null)
            current = target.InnerException;
        return current ?? ex;
    }

    string BuildFriendlyErrorMessage(string context, Exception ex)
    {
        string action = string.IsNullOrWhiteSpace(context) ? "This action" : context;

        if (ex == null)
            return action + " could not be completed because an unknown editor error occurred.";

        if (ex is MissingMethodException || ex is MissingFieldException || ex is TypeLoadException)
            return action + " could not be completed because an installed SDK/package API does not match the version this tool expected.";

        if (ex is System.IO.FileNotFoundException || ex is System.IO.DirectoryNotFoundException)
            return action + " could not be completed because a required file or folder could not be found.";

        if (ex is UnauthorizedAccessException)
            return action + " could not be completed because Unity could not write to the requested location.";

        if (ex is NullReferenceException)
            return action + " stopped because a required avatar/object reference was missing while the operation was running.";

        if (ex is ArgumentException)
            return action + " could not be completed because one of the supplied values is not valid for the current Unity/SDK API.";

        if (ex is InvalidOperationException)
            return action + " could not be completed in the avatar/project's current state.";

        string message = (ex.Message ?? "").Trim();
        return message.Length > 0
            ? action + " could not be completed: " + message
            : action + " could not be completed because an unexpected editor error occurred.";
    }

    string BuildErrorHint(Exception ex)
    {
        if (ex == null)
            return "Retry once. If it repeats, copy the support details and include them in a bug report.";

        if (ex is MissingMethodException || ex is MissingFieldException || ex is TypeLoadException)
            return "Refresh Dependencies in Preferences and verify the installed VRChat SDK/VRCFury versions. If it still fails, copy the details so the exact API mismatch can be patched.";

        if (ex is System.IO.FileNotFoundException || ex is System.IO.DirectoryNotFoundException)
            return "Check that the selected project folder or asset still exists and is inside the Unity project when required.";

        if (ex is UnauthorizedAccessException)
            return "Choose a writable location and check whether the file is read-only, locked, or blocked by source-control permissions.";

        if (ex is NullReferenceException)
            return "Re-select the avatar/object, refresh the related tool, and check for missing scripts or deleted references.";

        if (ex is ArgumentException)
            return "Refresh the tool and verify the selected object/value. This can also indicate an SDK-version-specific API signature.";

        if (ex is InvalidOperationException)
            return "Review the current selection and the tool's requirements, then retry after refreshing its analysis.";

        return "Retry once. If it repeats, use Copy Support Details so the full exception and environment can be inspected.";
    }

    string BuildActionErrorReport()
    {
        string env;
        try
        {
            env = BuildEnvironmentReport();
        }
        catch (Exception envEx)
        {
            env =
                "Chi's Little Helper " + TOOL_VERSION + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Environment report failed: " + envEx.Message;
        }

        return
            env + "\n\n" +
            "Action: " + (lastErrorContext ?? "Unknown") + "\n" +
            "Captured: " + (lastErrorUtc == default(DateTime) ? "Unknown" : lastErrorUtc.ToString("O")) + "\n" +
            "Friendly message: " + (lastError ?? "Unknown") + "\n" +
            "Suggestion: " + (lastErrorHint ?? "None") + "\n\n" +
            "Exception:\n" +
            (lastErrorDetails ?? "No exception details available.");
    }
}
