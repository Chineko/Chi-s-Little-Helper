using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public partial class ChiLittleHelperWindow
{
    readonly List<string> installationCandidateScripts = new List<string>();
    readonly List<string> duplicateMenuScripts = new List<string>();
    bool installationHealthScanned = false;
    string installationHealthNote = "";

    string[] KnownLimitations =>
        new[]
        {
            "VRCFury parameter memory is an estimate before upload/build. Toggle-generated parameters are included, but other VRCFury systems may create additional parameters.",
            "If VRCFury Parameter Compressor is present, parameter memory is intentionally shown as a conservative pre-compression estimate.",
            "PC / Quest compatibility checks are advisory. The installed VRChat SDK Build panel remains authoritative.",
            "Poiyomi and lilToon property layouts can vary by shader/version. Material Assembler and Material Inspector mappings should be reviewed before bulk asset edits.",
            "Unity → VRChat ParentConstraint conversion delegates to the converter API exposed by the installed VRChat SDK and may vary between SDK versions.",
            "Avatar Snapshot & Compare stores a structural comparison baseline. It is not a scene backup and cannot restore an avatar.",
            "PhysBone presets can contain project/avatar-specific serialized references. Review them when applying across different avatars or projects."
        };

    void ScanInstallationHealth()
    {
        installationCandidateScripts.Clear();
        duplicateMenuScripts.Clear();
        installationHealthNote = "";

        try
        {
            string[] guids = AssetDatabase.FindAssets("t:MonoScript");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string fileName = Path.GetFileName(path);
                bool looksRelevant =
                    fileName.IndexOf("ChiLittleHelper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fileName.IndexOf("Chi_Little_Helper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fileName.IndexOf("Chi's Little Helper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fileName.IndexOf("Chi`s little Helper", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!looksRelevant)
                    continue;

                installationCandidateScripts.Add(path);

                try
                {
                    string absolute = Path.GetFullPath(path);
                    if (!File.Exists(absolute))
                        continue;

                    string source = File.ReadAllText(absolute);

                    if (source.IndexOf("[MenuItem(\"Tools/Chi's Little Helper\")]", StringComparison.Ordinal) >= 0 ||
                        source.IndexOf("[MenuItem(\"Tools/Chi\\'s Little Helper\")]", StringComparison.Ordinal) >= 0)
                    {
                        duplicateMenuScripts.Add(path);
                    }
                }
                catch
                {
                    // Installation-health scanning must never break the editor window.
                }
            }

            installationCandidateScripts.Sort(StringComparer.OrdinalIgnoreCase);
            duplicateMenuScripts.Sort(StringComparer.OrdinalIgnoreCase);

            if (duplicateMenuScripts.Count > 1)
            {
                installationHealthNote =
                    "Multiple scripts declare the Chi's Little Helper Tools menu item. Remove older copies and keep only the current modular package.";
            }
            else
            {
                var roots = installationCandidateScripts
                    .Select(GetLikelyPackageRoot)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (roots.Count > 1)
                {
                    installationHealthNote =
                        "More than one Chi's Little Helper-style script folder was found. This can be intentional for archived sources, but installed Editor copies should not coexist.";
                }
                else
                {
                    installationHealthNote =
                        "No obvious duplicate installed copy was detected by the project script scan.";
                }
            }
        }
        catch (Exception ex)
        {
            installationHealthNote =
                "Installation scan could not complete: " + ex.Message;
        }

        installationHealthScanned = true;
    }

    string GetLikelyPackageRoot(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "";

        string normalized = assetPath.Replace('\\', '/');
        int editorIndex = normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase);
        if (editorIndex > 0)
            return normalized.Substring(0, editorIndex);

        return Path.GetDirectoryName(normalized)?.Replace('\\', '/') ?? "";
    }

    bool HasLikelyDuplicateInstallation =>
        duplicateMenuScripts.Count > 1 ||
        installationCandidateScripts
            .Select(GetLikelyPackageRoot)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() > 1;

    string BuildInstallationHealthReport()
    {
        if (!installationHealthScanned)
            ScanInstallationHealth();

        var lines = new List<string>
        {
            "=== Installation Health ===",
            installationHealthNote,
            "",
            "Potential Chi's Little Helper scripts:"
        };

        if (installationCandidateScripts.Count == 0)
            lines.Add("- None found by filename scan.");
        else
            foreach (string path in installationCandidateScripts)
                lines.Add("- " + path);

        lines.Add("");
        lines.Add("Scripts declaring Tools/Chi's Little Helper:");

        if (duplicateMenuScripts.Count == 0)
            lines.Add("- None found.");
        else
            foreach (string path in duplicateMenuScripts)
                lines.Add("- " + path);

        lines.Add("");
        lines.Add("Upgrade guidance:");
        lines.Add("- Replace the previous Chi's Little Helper folder instead of installing a new version beside it.");
        lines.Add("- Keep the entire modular Editor folder together.");
        lines.Add("- If Unity reports duplicate classes or menu items, search the project for older Chi's Little Helper scripts and remove them.");

        return string.Join("\n", lines);
    }

    string BuildKnownLimitationsReport()
    {
        var lines = new List<string>
        {
            "=== Known Limitations ==="
        };

        for (int i = 0; i < KnownLimitations.Length; i++)
            lines.Add((i + 1) + ". " + KnownLimitations[i]);

        return string.Join("\n", lines);
    }

    string BuildSupportBundleSummary()
    {
        return
            "Chi's Little Helper Support Bundle\n" +
            "Toolkit: " + TOOL_VERSION + " (" + RELEASE_CHANNEL + ")\n" +
            "Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" +
            "Selected Avatar: " + (avatar != null ? avatar.gameObject.name : "None") + "\n" +
            "Pre-Upload: " + (preflightDirty ? "STALE / not included as authoritative" : PreflightStateLabel) + "\n" +
            "Action Error: " + (string.IsNullOrEmpty(lastError) ? "None" : "Included") + "\n" +
            "Render Error: " + (string.IsNullOrEmpty(renderErrorKey) ? "None" : "Included") + "\n" +
            "Duplicate Install Warning: " + (HasLikelyDuplicateInstallation ? "YES" : "No obvious duplicate detected");
    }

    void ExportSupportBundle()
    {
        if (!installationHealthScanned)
            ScanInstallationHealth();

        string defaultName =
            "ChiLittleHelper_Support_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".zip";

        string zipPath = EditorUtility.SaveFilePanel(
            "Export Chi's Little Helper Support Bundle",
            "",
            defaultName,
            "zip");

        if (string.IsNullOrEmpty(zipPath))
            return;

        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "ChiLittleHelperSupport_" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempRoot);

        try
        {
            File.WriteAllText(
                Path.Combine(tempRoot, "00_README.txt"),
                BuildSupportBundleSummary());

            File.WriteAllText(
                Path.Combine(tempRoot, "01_environment.txt"),
                BuildEnvironmentReport());

            File.WriteAllText(
                Path.Combine(tempRoot, "02_installation_health.txt"),
                BuildInstallationHealthReport());

            File.WriteAllText(
                Path.Combine(tempRoot, "03_known_limitations.txt"),
                BuildKnownLimitationsReport());

            if (!preflightDirty)
            {
                File.WriteAllText(
                    Path.Combine(tempRoot, "04_preupload_report.txt"),
                    BuildPreflightTextReport());

                File.WriteAllText(
                    Path.Combine(tempRoot, "04_preupload_report.json"),
                    JsonUtility.ToJson(BuildPreflightReportData(), true));
            }
            else
            {
                File.WriteAllText(
                    Path.Combine(tempRoot, "04_preupload_report_STALE.txt"),
                    "Pre-Upload Check was stale when the bundle was created. Run the Pre-Upload Check and export another bundle for current readiness results.");
            }

            if (!string.IsNullOrEmpty(lastError))
            {
                File.WriteAllText(
                    Path.Combine(tempRoot, "05_last_action_error.txt"),
                    BuildActionErrorReport());
            }

            if (!string.IsNullOrEmpty(renderErrorKey))
            {
                File.WriteAllText(
                    Path.Combine(tempRoot, "06_last_render_error.txt"),
                    BuildRenderErrorReport());
            }

            File.WriteAllText(
                Path.Combine(tempRoot, "07_preferences_summary.txt"),
                "VRCFury features enabled: " + enableVRCFuryFeatures + "\n" +
                "Verbose logging: " + Verbose + "\n" +
                "Confirm destructive actions: " + confirmDestructiveActions + "\n" +
                "Automatic avatar snapshot: " + autoBackupBeforeDestructive + "\n" +
                "Doctor triangle threshold: " + doctorTriangleThreshold + "\n" +
                "Doctor material-slot threshold: " + doctorMaterialSlotThreshold + "\n" +
                "Doctor renderer threshold: " + doctorRendererThreshold + "\n" +
                "Doctor PhysBone threshold: " + doctorPhysBoneThreshold + "\n" +
                "Doctor texture-memory threshold MB: " + doctorTextureMemoryThresholdMb + "\n" +
                "Doctor large-texture threshold: " + doctorLargeTextureThreshold);

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            if (!TryCreateZipWithRuntime(tempRoot, zipPath, out string zipError))
            {
                string fallback = Path.Combine(
                    Path.GetDirectoryName(zipPath) ?? "",
                    Path.GetFileNameWithoutExtension(zipPath) + "_folder");

                if (Directory.Exists(fallback))
                    Directory.Delete(fallback, true);

                CopyDirectoryRecursive(tempRoot, fallback);

                ShowNotification(new GUIContent("Support folder exported"));
                EditorUtility.DisplayDialog(
                    "Support Bundle Exported as Folder",
                    "This Unity/.NET runtime did not expose the ZIP helper, so the support bundle was exported as a folder instead:\n\n" +
                    fallback +
                    "\n\nZIP detail: " + zipError,
                    "OK");
                return;
            }

            ShowNotification(new GUIContent("Support bundle exported"));
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
            catch
            {
            }
        }
    }

    bool TryCreateZipWithRuntime(string sourceDirectory, string destinationZip, out string error)
    {
        error = null;

        try
        {
            Type zipFileType =
                Type.GetType("System.IO.Compression.ZipFile, System.IO.Compression.FileSystem", false) ??
                Type.GetType("System.IO.Compression.ZipFile, System.IO.Compression.ZipFile", false);

            if (zipFileType == null)
            {
                error = "System.IO.Compression.ZipFile type was not available.";
                return false;
            }

            MethodInfo create =
                zipFileType.GetMethod(
                    "CreateFromDirectory",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string) },
                    null);

            if (create == null)
            {
                error = "CreateFromDirectory(string,string) was not available.";
                return false;
            }

            create.Invoke(null, new object[] { sourceDirectory, destinationZip });
            return File.Exists(destinationZip);
        }
        catch (TargetInvocationException ex)
        {
            error = ex.InnerException != null
                ? ex.InnerException.Message
                : ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    void CopyDirectoryRecursive(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(
                file,
                Path.Combine(destination, Path.GetFileName(file)),
                true);
        }

        foreach (string directory in Directory.GetDirectories(source))
        {
            CopyDirectoryRecursive(
                directory,
                Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
