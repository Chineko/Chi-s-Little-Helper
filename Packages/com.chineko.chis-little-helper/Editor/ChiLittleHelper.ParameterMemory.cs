using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class ChiLittleHelperWindow
{
    bool parameterMemoryDirty = true;
    int parameterExpressionBits = 0;
    int parameterVrcfuryToggleBits = 0;
    int parameterVrcfuryToggleCount = 0;
    int parameterVrcfurySliderCount = 0;
    int parameterVrcfuryIntCount = 0;
    int parameterVrcfuryBoolCount = 0;
    bool parameterVrcfuryCompressorDetected = false;

    static readonly HashSet<string> VrchatBuiltInParameterNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "IsLocal",
            "PreviewMode",
            "Viseme",
            "Voice",
            "GestureLeft",
            "GestureRight",
            "GestureLeftWeight",
            "GestureRightWeight",
            "AngularY",
            "VelocityX",
            "VelocityY",
            "VelocityZ",
            "VelocityMagnitude",
            "Upright",
            "Grounded",
            "Seated",
            "AFK",
            "TrackingType",
            "VRMode",
            "MuteSelf",
            "InStation",
            "Earmuffs",
            "IsOnFriendsList",
            "AvatarVersion",
            "IsAnimatorEnabled",
            "ScaleModified",
            "ScaleFactor",
            "ScaleFactorInverse",
            "EyeHeightAsMeters",
            "EyeHeightAsPercent"
        };

    void OnProjectChange()
    {
        parameterMemoryDirty = true;
        animatorDoctorDirty = true;
        Repaint();
    }

    void EnsureParameterMemoryCache()
    {
        if (!parameterMemoryDirty)
            return;

        RebuildParameterMemoryCache();
    }

    void RebuildParameterMemoryCache()
    {
        parameterMemoryDirty = false;
        parameterExpressionBits = 0;
        parameterVrcfuryToggleBits = 0;
        parameterVrcfuryToggleCount = 0;
        parameterVrcfurySliderCount = 0;
        parameterVrcfuryIntCount = 0;
        parameterVrcfuryBoolCount = 0;
        parameterVrcfuryCompressorDetected = false;

        if (avatar == null)
            return;

        var existingSyncedNames = new HashSet<string>(StringComparer.Ordinal);
        CountExpressionParameterMemory(existingSyncedNames);
        CountVrcfuryToggleMemory(existingSyncedNames);
    }

    void CountExpressionParameterMemory(HashSet<string> existingSyncedNames)
    {
        UnityEngine.Object asset = GetExpressionParametersAsset();
        if (asset == null)
            return;

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
            bool synced = syncedProp == null || syncedProp.boolValue;

            if (!synced)
                continue;

            string type = ReadParameterMemoryType(typeProp);
            int bits = type == "Bool" ? 1 : 8;

            parameterExpressionBits += bits;

            if (!string.IsNullOrWhiteSpace(name))
                existingSyncedNames.Add(name);
        }
    }

    void CountVrcfuryToggleMemory(HashSet<string> existingSyncedNames)
    {
        Type vrcfuryType = FindType(T_VRCFURY);
        Type toggleType = FindType(T_TOGGLE);

        if (vrcfuryType == null || toggleType == null)
            return;

        var countedSharedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (Component component in avatar.GetComponentsInChildren(vrcfuryType, true).Cast<Component>())
        {
            object feature = GetMember(component, "content");
            if (feature == null)
                continue;

            Type featureType = feature.GetType();

            if (featureType.Name.IndexOf("ParameterCompressor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (featureType.FullName ?? "").IndexOf("ParameterCompressor", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                parameterVrcfuryCompressorDetected = true;
            }

            if (!toggleType.IsInstanceOfType(feature) &&
                !string.Equals(featureType.FullName, T_TOGGLE, StringComparison.Ordinal))
                continue;

            bool slider = ReadBoolMember(feature, "slider");
            bool useInt = ReadBoolMember(feature, "useInt");
            bool useGlobalParam = ReadBoolMember(feature, "useGlobalParam");
            bool usePrefixOnParam = ReadBoolMember(feature, "usePrefixOnParam");

            string globalParam = ReadStringMember(feature, "globalParam");
            string paramOverride = ReadStringMember(feature, "paramOverride");
            string menuName = ReadStringMember(feature, "name");

            bool synced = true;

            // This mirrors VRCFury ToggleBuilder:
            // slider/bool global VRChat built-ins are not synced.
            // The useInt branch is always created as synced by VRCFury.
            if (!useInt &&
                useGlobalParam &&
                !string.IsNullOrWhiteSpace(globalParam) &&
                VrchatBuiltInParameterNames.Contains(globalParam))
            {
                synced = false;
            }

            if (!synced)
                continue;

            int bits = (slider || useInt) ? 8 : 1;

            string sharedName = null;

            if (paramOverride != null)
                sharedName = paramOverride;
            else if (useGlobalParam && !string.IsNullOrWhiteSpace(globalParam))
                sharedName = globalParam;
            else if (!usePrefixOnParam && !string.IsNullOrWhiteSpace(menuName))
                sharedName = menuName;

            // If the final parameter intentionally shares a name with an already
            // synced descriptor parameter, that memory is already counted.
            if (!string.IsNullOrWhiteSpace(sharedName))
            {
                if (existingSyncedNames.Contains(sharedName))
                    continue;

                if (!countedSharedNames.Add(sharedName))
                    continue;
            }

            parameterVrcfuryToggleBits += bits;
            parameterVrcfuryToggleCount++;

            if (slider)
                parameterVrcfurySliderCount++;
            else if (useInt)
                parameterVrcfuryIntCount++;
            else
                parameterVrcfuryBoolCount++;
        }
    }

    string ReadParameterMemoryType(SerializedProperty typeProp)
    {
        if (typeProp == null)
            return "Unknown";

        if (typeProp.propertyType == SerializedPropertyType.Enum)
        {
            string[] names = typeProp.enumNames;
            int index = typeProp.enumValueIndex;

            if (names != null && index >= 0 && index < names.Length)
            {
                string value = names[index] ?? "";
                if (value.IndexOf("Bool", StringComparison.OrdinalIgnoreCase) >= 0) return "Bool";
                if (value.IndexOf("Int", StringComparison.OrdinalIgnoreCase) >= 0) return "Int";
                if (value.IndexOf("Float", StringComparison.OrdinalIgnoreCase) >= 0) return "Float";
            }
        }

        return "Unknown";
    }

    bool ReadBoolMember(object obj, string name)
    {
        object value = GetMember(obj, name);
        return value is bool b && b;
    }

    string ReadStringMember(object obj, string name)
    {
        object value = GetMember(obj, name);
        return value as string;
    }

    int ParameterMemoryUsedBits =>
        parameterExpressionBits + parameterVrcfuryToggleBits;

    int ParameterMemoryRemainingBits =>
        256 - ParameterMemoryUsedBits;

    string GetParameterMemoryHomeValue()
    {
        int remaining = ParameterMemoryRemainingBits;

        if (remaining >= 0)
            return remaining + " bits";

        return Math.Abs(remaining) + " over";
    }

    string GetParameterMemoryTooltip()
    {
        var lines = new List<string>
        {
            "VRChat synced parameter budget: 256 bits",
            "Expression Parameters: " + parameterExpressionBits + " bits",
            "Estimated VRCFury Toggle upload cost: +" + parameterVrcfuryToggleBits + " bits",
            "Estimated total: " + ParameterMemoryUsedBits + " / 256 bits",
            "Remaining: " + ParameterMemoryRemainingBits + " bits",
            "",
            "VRCFury Toggle estimate:",
            "• Bool toggles: " + parameterVrcfuryBoolCount + " × 1 bit",
            "• Int toggles: " + parameterVrcfuryIntCount + " × 8 bits",
            "• Sliders: " + parameterVrcfurySliderCount + " × 8 bits"
        };

        if (parameterVrcfuryCompressorDetected)
        {
            lines.Add("");
            lines.Add("VRCFury Parameter Compressor detected.");
            lines.Add("This display is a conservative pre-compression estimate because final compression depends on all eligible menu-controlled Int/Float parameters.");
        }

        lines.Add("");
        lines.Add("This estimate covers the avatar's current synced Expression Parameters plus parameters generated by VRCFury Toggle features. Other VRCFury features such as Full Controller or SPS can add additional parameters during build.");

        return string.Join("\n", lines);
    }

    string GetParameterMemorySummary()
    {
        string remaining =
            ParameterMemoryRemainingBits >= 0
                ? ParameterMemoryRemainingBits + " bits left"
                : Math.Abs(ParameterMemoryRemainingBits) + " bits over budget";

        string summary =
            "Parameter memory: " +
            ParameterMemoryUsedBits + " / 256 bits · " +
            remaining +
            " · Expression: " + parameterExpressionBits + " bits";

        if (parameterVrcfuryToggleBits > 0)
        {
            summary +=
                " · VRCFury toggles: ~" +
                parameterVrcfuryToggleBits +
                " bits (" +
                parameterVrcfuryToggleCount +
                ")";
        }

        if (parameterVrcfuryCompressorDetected)
            summary += " · pre-compression estimate";

        return summary;
    }
}
