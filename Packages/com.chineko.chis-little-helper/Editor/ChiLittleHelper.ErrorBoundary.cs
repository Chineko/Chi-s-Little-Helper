using System;
using UnityEditor;
using UnityEngine;

public partial class ChiLittleHelperWindow
{
    void DrawToolSafely(int toolId, string title, Action draw)
    {
        DrawSurfaceSafely("tool:" + toolId, title, draw, false);
    }

    void DrawSurfaceSafely(string key, string title, Action draw, bool allowHomeRecovery)
    {
        if (draw == null)
            return;

        // If this exact surface failed previously, do not call it again until
        // the user explicitly retries. This prevents an exception-on-repaint loop.
        if (renderErrorKey == key)
        {
            DrawRenderErrorCard(title, allowHomeRecovery);
            return;
        }

        try
        {
            draw();
        }
        catch (ExitGUIException)
        {
            throw;
        }
        catch (Exception ex)
        {
            renderErrorKey = key;
            renderErrorTitle = title;
            renderErrorMessage = ex.Message;
            renderErrorDetails = ex.ToString();
            renderErrorUtc = DateTime.UtcNow;

            Debug.LogError(
                "[Chi's Little Helper] '" + title + "' failed while rendering.\n" +
                renderErrorDetails);

            // An exception may have interrupted a GUILayout Begin/End pair.
            // Do not continue drawing this IMGUI frame; render the recovery
            // card cleanly on the next repaint instead.
            Repaint();
            GUIUtility.ExitGUI();
        }
    }

    void DrawRenderErrorCard(string fallbackTitle, bool allowHomeRecovery)
    {
        string title = string.IsNullOrEmpty(renderErrorTitle)
            ? fallbackTitle
            : renderErrorTitle;

        EditorGUILayout.BeginVertical(styleRowBg);
        GUILayout.Space(4);

        EditorGUILayout.LabelField(
            "This section could not be rendered",
            new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = new Color(1f, 0.55f, 0.65f) }
            });

        GUILayout.Space(4);

        EditorGUILayout.LabelField(
            title + " could not be drawn. The failure has been isolated, so the rest of Chi's Little Helper remains available.",
            styleInfoText);

        if (!string.IsNullOrEmpty(renderErrorMessage))
        {
            GUILayout.Space(5);
            EditorGUILayout.LabelField(
                "What Unity reported",
                new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    normal = { textColor = ColLavenderText }
                });
            EditorGUILayout.LabelField(renderErrorMessage, styleInfoText);
        }

        GUILayout.Space(5);
        EditorGUILayout.LabelField(
            "Suggested next step",
            new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = ColLavenderText }
            });
        EditorGUILayout.LabelField(
            "Retry this section once. If it fails again, copy the support details so the exact render exception and environment can be inspected.",
            styleInfoText);

        if (renderErrorUtc != default(DateTime))
        {
            EditorGUILayout.LabelField(
                "Captured: " + renderErrorUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                EditorStyles.miniLabel);
        }

        GUILayout.Space(8);

        BeginResponsiveButtonRow();

        if (HelpButton("Copy Support Details", "Copy the render exception plus Unity/SDK/VRCFury environment details.", styleBtnDark, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
        {
            EditorGUIUtility.systemCopyBuffer = BuildRenderErrorReport();
            ShowNotification(new GUIContent("Support details copied"));
        }

        ResponsiveButtonGap(5f);

        if (HelpButton("Retry", "Clear the isolated error and draw this section again.", styleBtnGrad, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
        {
            ClearRenderError();
            Repaint();
            GUIUtility.ExitGUI();
        }

        ResponsiveButtonGap(5f);

        if (!allowHomeRecovery && GUILayout.Button("Go Home", styleBtnDark, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
        {
            ClearRenderError();
            showPreferences = false;
            showHome = true;
            scroll = Vector2.zero;
            Repaint();
            GUIUtility.ExitGUI();
        }

        if (allowHomeRecovery && GUILayout.Button("Back to Tools", styleBtnDark, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
        {
            ClearRenderError();
            showPreferences = false;
            showHome = false;
            scroll = Vector2.zero;
            EnsureVisibleToolSelection();
            Repaint();
            GUIUtility.ExitGUI();
        }

        EndResponsiveButtonRow();

        GUILayout.Space(4);

        EditorGUILayout.LabelField(
            "Retry only re-runs this section. Copy Support Details includes the toolkit, Unity, SDK and VRCFury environment details when available.",
            styleCardSubtitle);

        GUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    string BuildRenderErrorReport()
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
            "Render Surface: " + (renderErrorTitle ?? "Unknown") + "\n" +
            "Captured: " + (renderErrorUtc == default(DateTime)
                ? "Unknown"
                : renderErrorUtc.ToString("O")) + "\n\n" +
            "Exception:\n" +
            (string.IsNullOrEmpty(renderErrorDetails)
                ? (renderErrorMessage ?? "Unknown render error")
                : renderErrorDetails);
    }

    void ClearRenderError()
    {
        renderErrorKey = null;
        renderErrorTitle = null;
        renderErrorMessage = null;
        renderErrorDetails = null;
        renderErrorUtc = default(DateTime);
    }
}
