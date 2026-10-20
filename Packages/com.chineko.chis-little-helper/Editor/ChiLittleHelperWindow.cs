using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;

public partial class ChiLittleHelperWindow : EditorWindow
{
    public const string TOOL_VERSION = "1.0.0-rc.14";
    public const string RELEASE_CHANNEL = "Release Candidate";

    static bool Verbose = false;

    // Reflection lookups are hit heavily by VRCFury/SDK tools.
    // Cache both successful and failed lookups for the lifetime of the domain.
    static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();
    static readonly HashSet<string> MissingTypeCache = new HashSet<string>();
    static readonly Dictionary<string, MemberInfo> MemberCache = new Dictionary<string, MemberInfo>();
    static readonly HashSet<string> MissingMemberCache = new HashSet<string>();
    static readonly Dictionary<Assembly, string> PackageVersionCache = new Dictionary<Assembly, string>();

    bool hierarchyRefreshQueued = false;

    // Responsive breakpoints based on the actual EditorWindow width.
    bool IsCompactLayout => position.width < 700f;
    bool IsNarrowLayout => position.width < 580f;
    float ResponsiveGap => IsCompactLayout ? 3f : 5f;
    float FieldLabelWidth => IsNarrowLayout ? 108f : 145f;

    const string T_VRCFURY = "VF.Model.VRCFury";
    const string T_ARMATURELINK = "VF.Model.Feature.ArmatureLink";
    const string T_TOGGLE = "VF.Model.Feature.Toggle";
    const string T_BLENDSHAPE_ACTION = "VF.Model.StateAction.BlendShapeAction";
    const string T_MATERIAL_ACTION = "VF.Model.StateAction.MaterialPropertyAction";
    const string T_MATERIAL_SWAP_ACTION = "VF.Model.StateAction.MaterialAction";
    const string T_OBJECT_TOGGLE = "VF.Model.StateAction.ObjectToggleAction";
    const string T_CONTACT_SENDER = "VRC.SDK3.Dynamics.Contact.Components.VRCContactSender";
    const string T_CONTACT_RECEIVER = "VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver";

    static readonly string[] CategoryLabels =
    {
        "Build", "Dynamics", "Materials", "Optimize", "Doctor"
    };

    // Tool ids keep the existing feature switch stable.
    static readonly string[][] CategoryToolLabels =
    {
        new[] { "Outfit Builder", "Animation Swapper", "Multi Toggles", "Armature Link", "Preset Library", "Snapshot & Compare" },
        new[] { "PhysBone Manager", "Constraint Toolkit", "Contact Setup", "Blendshapes" },
        new[] { "Material Assembler", "Material Inspector++", "Material Swap", "Dissolve Toggle" },
        new[] { "PC / Quest", "Texture & VRAM", "VRCFury Overview++" },
        new[] { "Pre-Upload Check", "Avatar Doctor 2.0", "Animator & Params Doctor" }
    };

    static readonly int[][] CategoryToolIds =
    {
        new[] { 5, 19, 2, 0, 16, 18 },
        new[] { 9, 10, 6, 3 },
        new[] { 12, 15, 4, 1 },
        new[] { 14, 11, 7 },
        new[] { 17, 8, 13 }
    };
    static readonly string[] StateListPaths = { "content.transitionStateIn.actions", "content.state.actions" };

    [SerializeField] VRCAvatarDescriptor avatar;
    [SerializeField] VRCAvatarDescriptor cachedAvatar;
    [SerializeField] int category = 0;
    [SerializeField] int tab = 5; // Default to Outfit Builder
    [SerializeField] Vector2 scroll;
    [SerializeField] Vector2 preferencesScroll;
    [SerializeField] bool showPreferences = false;
    [SerializeField] bool showHome = true;
    [SerializeField] bool homeShowChangelog = true;
    [SerializeField] bool homeShowRoadmap = true;

    const string PREF_VRCFURY_FEATURES = "ChiLittleHelper.EnableVRCFuryFeatures";
    const string PREF_VERBOSE_LOGGING = "ChiLittleHelper.VerboseLogging";
    const string PREF_CONFIRM_DESTRUCTIVE = "ChiLittleHelper.ConfirmDestructive";
    const string PREF_AUTO_BACKUP = "ChiLittleHelper.AutoBackup";
    const string PREF_ONBOARDING_DISMISSED = "ChiLittleHelper.OnboardingDismissed";

    const string PREF_DOCTOR_TRIANGLES = "ChiLittleHelper.Doctor.Triangles";
    const string PREF_DOCTOR_MATERIAL_SLOTS = "ChiLittleHelper.Doctor.MaterialSlots";
    const string PREF_DOCTOR_RENDERERS = "ChiLittleHelper.Doctor.Renderers";
    const string PREF_DOCTOR_PHYSBONES = "ChiLittleHelper.Doctor.PhysBones";
    const string PREF_DOCTOR_TEXTURE_MB = "ChiLittleHelper.Doctor.TextureMB";
    const string PREF_DOCTOR_TEXTURE_SIZE = "ChiLittleHelper.Doctor.TextureSize";

    bool enableVRCFuryFeatures = true;
    bool confirmDestructiveActions = true;
    bool autoBackupBeforeDestructive = false;
    bool onboardingDismissed = false;

    int doctorTriangleThreshold = 70000;
    int doctorMaterialSlotThreshold = 16;
    int doctorRendererThreshold = 16;
    int doctorPhysBoneThreshold = 16;
    int doctorTextureMemoryThresholdMb = 128;
    int doctorLargeTextureThreshold = 2048;

    sealed class DoctorIssue
    {
        public MessageType severity;
        public string category;
        public string title;
        public string detail;
        public UnityEngine.Object target;
        public Action fix;
        public string fixLabel;
        public Action route;
        public string routeLabel;
        public bool safeFix;
        public string safeFixDescription;
    }

    readonly List<DoctorIssue> doctorIssues = new List<DoctorIssue>();
    bool doctorDirty = true;
    int doctorTriangles, doctorMaterialSlots, doctorRendererCount, doctorSkinnedCount;
    int doctorPhysBones, doctorContacts, doctorConstraints;
    int doctorHealthScore = 100;
    long doctorTextureBytes = 0;
    int doctorLargeTextures = 0;
    DateTime doctorLastScanUtc;

    // PhysBone Manager
    string physBoneSearch = "";
    Vector2 physBoneScroll;
    Component physBoneClipboard;
    string physBoneClipboardJson;
    string physBoneClipboardType;
    bool physBoneShowInactive = true;

    // Constraint Toolkit
    GameObject constraintTarget;
    Transform constraintSource;
    int constraintType = 0; // Parent, Position, Rotation, Scale, Aim
    float constraintWeight = 1f;
    bool constraintActivate = true;
    string constraintSearch = "";

    // Texture & VRAM Analyzer
    sealed class TextureVramEntry
    {
        public Texture texture;
        public string assetPath;
        public string usage;
        public int materialCount;
        public long estimatedBytes;
    }

    readonly List<TextureVramEntry> textureVramEntries = new List<TextureVramEntry>();
    bool textureVramDirty = true;
    string textureSearch = "";
    Vector2 textureScroll;
    long textureTotalBytes = 0;

    bool bsDirty = true, overviewDirty = true, dissolveDirty = true;

    // Contact Setup Helper
    GameObject contactTarget;
    bool contactCreateSender = true;
    string contactTag = "Hand";
    int contactShape = 0; // Sphere, Capsule, Box
    float contactRadius = 0.08f;
    float contactHeight = 0.20f;
    Vector3 contactBoxSize = new Vector3(0.12f, 0.12f, 0.12f);
    Vector3 contactPosition = Vector3.zero;
    Vector3 contactRotation = Vector3.zero;
    bool contactLocalOnly = false;
    bool contactAllowSelf = true;
    bool contactAllowOthers = true;
    int contactReceiverType = 0; // Constant, OnEnter, Proximity
    string contactParameter = "Contact";


    // ---------- Color Palette ----------
    static readonly Color ColWindowBg      = Hex("#081426");
    static readonly Color ColCardBg        = Hex("#0E1C31");
    static readonly Color ColCardBorder    = Hex("#2A3D60");
    static readonly Color ColCardInnerBg   = Hex("#0A1628");
    static readonly Color ColFieldBorder   = Hex("#31486D");

    static readonly Color ColGradPink      = Hex("#F080DC");
    static readonly Color ColGradPurple    = Hex("#8A5CFF");
    static readonly Color ColLavenderText  = Hex("#DDBBFF");
    static readonly Color ColTextPrimary   = Hex("#FFFFFF");
    static readonly Color ColTextMuted     = Hex("#9BAAC2");
    static readonly Color ColIconBadgeBg   = Hex("#17254A");

    static readonly Color ColDarkBtnBg     = Hex("#172845");
    static readonly Color ColDarkBtnBorder = Hex("#30476C");
    static readonly Color ColDarkBtnHover  = Hex("#21365A");

    static readonly Color DangerRed        = Hex("#E86262");
    static readonly Color DangerBgHover    = Hex("#3A1A22");

    static Color Lighten(Color c, float amt) => Color.Lerp(c, Color.white, amt);

    // ---------- Cached Textures & Styles ----------
    static Texture2D texCardBg, texBannerBg, texBannerIcon, texTabActive, texTabInactive;
    static Texture2D texBtnGrad, texBtnGradHover, texBtnDark, texBtnDarkHover;
    static Texture2D texBtnRemove, texBtnRemoveHover;
    static Texture2D texInfoBox, texFieldBg, texRowBg, texBoxIllustration;

    // Card badge icons
    static Texture2D texIconBone, texIconBubble, texIconCube, texIconSmile, texIconList, texIconPerson;

    static GUIStyle styleHeaderTitle, styleHeaderSubtitle, styleCardTitle, styleCardSubtitle;
    static GUIStyle styleInfoText, styleFieldLabel, styleInputBox, stylePopup, styleNumberField, styleObjectField, styleToolbarSearch, styleRowBg;
    static GUIStyle styleBtnGrad, styleBtnDark, styleBtnRemove, styleTabActive, styleTabInactive;

    // ---------- Procedural Texture Helpers ----------

    // ---------- Card Badge Icons (procedural SDF glyphs) ----------

    // --- Shared SDF helpers ---
    [MenuItem("Tools/Chi's Little Helper")]
    public static void Open()
    {
        var win = GetWindow<ChiLittleHelperWindow>("Chi's Little Helper");
        win.minSize = new Vector2(520, 700);
    }

   void OnEnable()
    {
        wantsMouseMove = true;

        enableVRCFuryFeatures = EditorPrefs.GetBool(PREF_VRCFURY_FEATURES, true);
        Verbose = EditorPrefs.GetBool(PREF_VERBOSE_LOGGING, false);
        confirmDestructiveActions = EditorPrefs.GetBool(PREF_CONFIRM_DESTRUCTIVE, true);
        autoBackupBeforeDestructive = EditorPrefs.GetBool(PREF_AUTO_BACKUP, false);
        onboardingDismissed = EditorPrefs.GetBool(PREF_ONBOARDING_DISMISSED, false);

        doctorTriangleThreshold = EditorPrefs.GetInt(PREF_DOCTOR_TRIANGLES, 70000);
        doctorMaterialSlotThreshold = EditorPrefs.GetInt(PREF_DOCTOR_MATERIAL_SLOTS, 16);
        doctorRendererThreshold = EditorPrefs.GetInt(PREF_DOCTOR_RENDERERS, 16);
        doctorPhysBoneThreshold = EditorPrefs.GetInt(PREF_DOCTOR_PHYSBONES, 16);
        doctorTextureMemoryThresholdMb = EditorPrefs.GetInt(PREF_DOCTOR_TEXTURE_MB, 128);
        doctorLargeTextureThreshold = EditorPrefs.GetInt(PREF_DOCTOR_TEXTURE_SIZE, 2048);

        CheckDependencies(enableVRCFuryFeatures);
        ScanInstallationHealth();
        OnSelectionChange();
        EnsureVisibleToolSelection();
        showHome = true;
    }

    void OnFocus()
    {
        if (deps == null || (enableVRCFuryFeatures && !deps.Ok))
        {
            CheckDependencies(false);
            Repaint();
        }
    }

    void OnHierarchyChange()
    {
        bsDirty = overviewDirty = dissolveDirty = doctorDirty = textureVramDirty = true;
        parameterMemoryDirty = true;

        if (hierarchyRefreshQueued) return;
        hierarchyRefreshQueued = true;

        EditorApplication.delayCall += () =>
        {
            hierarchyRefreshQueued = false;
            if (this == null) return;
            Repaint();
        };
    }

    void OnSelectionChange()
    {
        var sel = Selection.activeGameObject;
        if (sel == null) return;

        // Keep selection handling intentionally lightweight. Expensive avatar-wide
        // scans are no longer performed while the Hierarchy/ObjectField is changing.
        var selectedAvatar = sel.GetComponentInParent<VRCAvatarDescriptor>();
        if (selectedAvatar != null && selectedAvatar != avatar)
            SetActiveAvatar(selectedAvatar);

        if (avatar != null &&
            sel != avatar.gameObject &&
            sel.transform.IsChildOf(avatar.transform))
        {
            // Only inspect the selected object itself; avoid recursive
            // GetComponentInChildren calls on every selection event.
            if (sel.GetComponent<Renderer>() != null)
            {
                dissolveTarget = sel;
                clothing = sel;
            }
        }

        Repaint();
    }

    void SetActiveAvatar(VRCAvatarDescriptor nextAvatar)
    {
        if (avatar == nextAvatar && cachedAvatar == nextAvatar)
            return;

        avatar = nextAvatar;
        cachedAvatar = nextAvatar;

        bsDirty = true;
        overviewDirty = true;
        dissolveDirty = true;
        doctorDirty = true;
        textureVramDirty = true;
        animatorDoctorDirty = true;
        platformCompareDirty = true;
        materialInspectorDirty = true;
        parameterMemoryDirty = true;
        preflightDirty = true;
        snapshotCompareDirty = true;

        // Prevent old-avatar statistics from being shown for the newly-selected avatar.
        ResetAvatarAnalysisSummary();
        overviewEntries.Clear();
        bsEntries.Clear();
    }

    void ResetAvatarAnalysisSummary()
    {
        doctorTriangles = 0;
        doctorMaterialSlots = 0;
        doctorRendererCount = 0;
        doctorSkinnedCount = 0;
        doctorPhysBones = 0;
        doctorContacts = 0;
        doctorConstraints = 0;
        doctorTextureBytes = 0;
        doctorLargeTextures = 0;
        doctorHealthScore = 100;
        doctorLastScanUtc = default(DateTime);

        doctorIssues.Clear();
        textureVramEntries.Clear();
        textureTotalBytes = 0;
    }

    void OnGUI()
    {
        EnsureStyles();

        // Immediate hover feedback for custom buttons/tabs.
        // This is visual-only: cached avatar analysis remains untouched.
        if (Event.current.type == EventType.MouseMove)
            Repaint();

        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), ColWindowBg);

        EditorGUILayout.BeginVertical();
        GUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(16);
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        DrawBanner();
        GUILayout.Space(8);
        DrawUtilityBar();
        GUILayout.Space(8);

        BeginCard(CardIcon.Avatar, "Avatar", "Choose an avatar descriptor, or select any object inside the avatar in the Hierarchy.");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Avatar Descriptor", styleFieldLabel, GUILayout.Width(FieldLabelWidth));
        EditorGUI.BeginChangeCheck();
        var nextAvatar = (VRCAvatarDescriptor)DrawThemedObjectField(avatar, typeof(VRCAvatarDescriptor), true,
            GUILayout.Height(24));

        if (EditorGUI.EndChangeCheck())
            SetActiveAvatar(nextAvatar);
        EditorGUILayout.EndHorizontal();
        EndCard();

        if (deps == null) CheckDependencies(false);

        if (showPreferences)
        {
            DrawSurfaceSafely("preferences", "Settings & Preferences", DrawPreferences, true);
        }
        else if (showHome)
        {
            DrawSurfaceSafely("home", "Home", DrawHomeScreen, true);
        }
        else if (avatar == null)
        {
            if (tab == 12 || tab == 16)
            {
                DrawNavBar();
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
                if (tab == 12)
                    DrawToolSafely(12, "Material Assembler", DrawMaterialAssembler);
                else
                    DrawToolSafely(16, "Preset Library", DrawPresetLibrary);
                EditorGUILayout.EndScrollView();
            }
            else
            {
                BeginCard(CardIcon.Sparkle, "Get Started", "No avatar selected.");
                DrawInfoBox("Select your avatar in the Hierarchy to unlock avatar tools. Material Assembler and Preset Library can also be used without an avatar.");

                GUILayout.Space(6);
                BeginResponsiveButtonRow();
                if (GUILayout.Button("Open Material Assembler", styleBtnGrad, GUILayout.Height(34), GUILayout.ExpandWidth(true)))
                {
                    showHome = false;
                    showPreferences = false;
                    category = 2;
                    tab = 12;
                    scroll = Vector2.zero;
                }

                ResponsiveButtonGap(5f);
                if (GUILayout.Button("Open Preset Library", styleBtnDark, GUILayout.Height(34), GUILayout.ExpandWidth(true)))
                {
                    showHome = false;
                    showPreferences = false;
                    category = 0;
                    tab = 16;
                    scroll = Vector2.zero;
                }
                EndResponsiveButtonRow();

                EndCard();
            }
        }
        else
        {
            EnsureVisibleToolSelection();
            DrawNavBar();

            if (tab < 0)
            {
                BeginCard(CardIcon.Sparkle, "No tools shown", "This category currently contains only VRCFury-powered tools.");
                DrawInfoBox("VRCFury features are disabled in Preferences. Turn them back on to show this category's VRCFury tools.");
                if (GUILayout.Button("Open Preferences", styleBtnGrad, GUILayout.Height(36)))
                    showPreferences = true;
                EndCard();
            }
            else
            {
                bool currentNeedsVRCFury = IsVRCFuryTool(tab);

                if (currentNeedsVRCFury && enableVRCFuryFeatures && (!deps.vrcfuryFound || deps.error != null))
                {
                    BeginCard(CardIcon.Warning, "VRCFury Required", "This tool uses VRCFury. The rest of Chi's Little Helper remains available.");
                    DrawInfoBox("VRCFury was not found in this project. Install it to use the VRCFury-powered Build, Materials and Overview tools.");
                    if (GUILayout.Button("Add VRCFury to VCC", styleBtnGrad, GUILayout.Height(36)))
                        AddVRCFuryToVCC();
                    EndCard();
                }
                else
                {
                    if (!string.IsNullOrEmpty(lastError))
                    {
                        EditorGUILayout.BeginVertical(styleRowBg);

                        EditorGUILayout.LabelField(
                            "Action could not be completed",
                            new GUIStyle(EditorStyles.boldLabel)
                            {
                                fontSize = 13,
                                normal = { textColor = DangerRed }
                            });

                        GUILayout.Space(2);
                        EditorGUILayout.LabelField(lastError, styleInfoText);

                        if (!string.IsNullOrWhiteSpace(lastErrorHint))
                        {
                            GUILayout.Space(4);
                            EditorGUILayout.LabelField(
                                "Suggested next step",
                                new GUIStyle(EditorStyles.miniBoldLabel)
                                {
                                    normal = { textColor = ColLavenderText }
                                });
                            EditorGUILayout.LabelField(lastErrorHint, styleInfoText);
                        }

                        if (lastErrorUtc != default(DateTime))
                        {
                            GUILayout.Space(3);
                            EditorGUILayout.LabelField(
                                "Captured: " + lastErrorUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                                styleCardSubtitle);
                        }

                        GUILayout.Space(6);
                        BeginResponsiveButtonRow();
                        if (!IsCompactLayout)
                            GUILayout.FlexibleSpace();

                        if (HelpButton(
                            "Copy Support Details",
                            "Copy the friendly message, suggested next step, full exception, toolkit version and environment details.",
                            styleBtnDark,
                            ResponsiveButtonWidth(142),
                            GUILayout.Height(28)))
                        {
                            EditorGUIUtility.systemCopyBuffer = BuildActionErrorReport();
                            ShowNotification(new GUIContent("Support details copied"));
                        }

                        ResponsiveButtonGap(5f);

                        if (GUILayout.Button("Dismiss", styleBtnDark, ResponsiveButtonWidth(82), GUILayout.Height(28)))
                        {
                            lastError = null;
                            lastErrorDetails = null;
                            lastErrorHint = null;
                            lastErrorContext = null;
                            lastErrorUtc = default(DateTime);
                        }

                        EndResponsiveButtonRow();
                        EditorGUILayout.EndVertical();
                        GUILayout.Space(6);
                    }

                    scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
                    switch (tab)
                    {
                        case 0: DrawToolSafely(0, "Armature Link", DrawArmatureLink); break;
                        case 1: DrawToolSafely(1, "Dissolve Toggle", DrawDissolve); break;
                        case 2: DrawToolSafely(2, "Multi Toggles", DrawMultiToggles); break;
                        case 3: DrawToolSafely(3, "Blendshapes", DrawBlendshapes); break;
                        case 4: DrawToolSafely(4, "Material Swap", DrawMaterialSwap); break;
                        case 5: DrawToolSafely(5, "Outfit Builder", DrawOutfitBuilder); break;
                        case 6: DrawToolSafely(6, "Contact Setup", DrawContactSetup); break;
                        case 7: DrawToolSafely(7, "VRCFury Overview++", DrawOverview); break;
                        case 8: DrawToolSafely(8, "Avatar Doctor 2.0", DrawDoctor); break;
                        case 9: DrawToolSafely(9, "PhysBone Manager", DrawPhysBoneManager); break;
                        case 10: DrawToolSafely(10, "Constraint Toolkit", DrawConstraintToolkit); break;
                        case 11: DrawToolSafely(11, "Texture & VRAM Analyzer", DrawTextureVramAnalyzer); break;
                        case 12: DrawToolSafely(12, "Material Assembler", DrawMaterialAssembler); break;
                        case 13: DrawToolSafely(13, "Animator & Parameters Doctor", DrawAnimatorParametersDoctor); break;
                        case 14: DrawToolSafely(14, "PC / Quest Comparator", DrawPcQuestComparator); break;
                        case 15: DrawToolSafely(15, "Material Inspector++", DrawMaterialInspectorPlus); break;
                        case 16: DrawToolSafely(16, "Preset Library", DrawPresetLibrary); break;
                        case 17: DrawToolSafely(17, "Pre-Upload Check", DrawPreUploadCheck); break;
                        case 18: DrawToolSafely(18, "Snapshot & Compare", DrawSnapshotCompare); break;
                        case 19: DrawToolSafely(19, "Animation Swapper & Usage Map", DrawAnimationSwapper); break;
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        GUILayout.FlexibleSpace();
        DrawFooter();
        GUILayout.Space(6);

        EditorGUILayout.EndVertical();
        GUILayout.Space(16);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    // Enum identifying which icon to render inside a card badge.
    enum CardIcon { None, Avatar, Armature, Dissolve, MultiToggles, Blendshapes, Overview, Warning, Sparkle }

    static void EndCard() => EditorGUILayout.EndVertical();

    // ---------- Tab Implementations ----------

    GameObject clothing;
    GameObject dissolveTarget;
    string dissolveMenuPath = "Clothing/Dissolve";
    string dissolveProperty = "";
    float dissolveHiddenValue = 1f;
    float dissolveShownValue = 0f;
    float dissolveTime = 1.4f;
    bool dissolveDefaultOn = false;
    GameObject dissolveDetectedFor;
    readonly List<string> dissolveDetected = new List<string>();
    string[] dissolveDetectedArray = new string[0];

    sealed class ToggleEntry { public GameObject asset; public string name; public Texture2D icon; }
    string multiToggleMenuPrefix = "Clothing";
    bool multiToggleDefaultOn = false;
    readonly List<ToggleEntry> multiToggles = new List<ToggleEntry>();
    readonly HashSet<GameObject> multiToggleSet = new HashSet<GameObject>();

    Renderer materialSwapRenderer;
    int materialSwapSlot = 0;
    Material materialSwapMaterial;
    string materialSwapMenuPath = "Materials/Swap";
    bool materialSwapDefaultOn = false;

    enum OutfitActionType { Object, MaterialSwap, Blendshape, MaterialProperty }
    sealed class OutfitActionEntry
    {
        public OutfitActionType type;
        public GameObject obj;
        public bool objectOn = true;
        public Renderer renderer;
        public int materialSlot;
        public Material material;
        public SkinnedMeshRenderer smr;
        public string blendshape = "";
        public float blendshapeValue = 100f;
        public string property = "";
        public float propertyValue = 1f;
    }
    string outfitMenuPath = "Outfits/New Outfit";
    bool outfitDefaultOn = false;
    Texture2D outfitIcon;
    readonly List<OutfitActionEntry> outfitActions = new List<OutfitActionEntry>();

    sealed class BsEntry
    {
        public SkinnedMeshRenderer smr;
        public string[] names;
        public bool open;
        public string appliedFilter;
        public readonly List<int> visible = new List<int>();
        public string label;
    }
    const float BsRowHeight = 24f;
    string bsFilter = "";
    readonly List<BsEntry> bsEntries = new List<BsEntry>();
    readonly Dictionary<SkinnedMeshRenderer, bool> bsFoldouts = new Dictionary<SkinnedMeshRenderer, bool>();

    sealed class OverviewEntry
    {
        public Component comp;
        public object feature;
        public string featureLabel;
        public bool isToggle;
        public string menuPath;
        public string toggleName;
    }

    sealed class OverviewToggleEdit
    {
        public string menuPath;
        public string name;
    }

    sealed class OverviewMenuEdit
    {
        public string parentPath;
        public string name;
    }

    readonly List<OverviewEntry> overviewEntries = new List<OverviewEntry>();
    readonly Dictionary<int, OverviewToggleEdit> overviewToggleEdits = new Dictionary<int, OverviewToggleEdit>();
    readonly Dictionary<string, OverviewMenuEdit> overviewMenuEdits =
        new Dictionary<string, OverviewMenuEdit>(StringComparer.OrdinalIgnoreCase);

    static readonly List<Material> MaterialScratch = new List<Material>();
    static readonly List<Renderer> RendererScratch = new List<Renderer>();

    // ---------- Reflection & VRCFury Utils ----------

    const string VRCFURY_URL = "https://vrcfury.com/download";
    const string VRCFURY_VCC_URL = "vcc://vpm/addRepo?url=https://vrcfury.com/vpm.json";

    sealed class DependencyReport
    {
        public string unityVersion;
        public bool vrcfuryFound;
        public string vrcfuryVersion;
        public string sdkVersion;
        public string sdkAssembly;
        public string vrcfuryAssembly;
        public readonly List<string> missingTypes = new List<string>();
        public string error;
        public bool Ok => vrcfuryFound && missingTypes.Count == 0 && error == null;
    }

    static readonly string[][] TabRequirements =
    {
        new[] { T_VRCFURY, T_ARMATURELINK },                                      // 0 Armature Link
        new[] { T_VRCFURY, T_TOGGLE, T_MATERIAL_ACTION, T_OBJECT_TOGGLE },       // 1 Dissolve
        new[] { T_VRCFURY, T_TOGGLE, T_OBJECT_TOGGLE },                           // 2 Multi Toggles
        new[] { T_VRCFURY, T_TOGGLE, T_BLENDSHAPE_ACTION },                       // 3 Blendshapes
        new[] { T_VRCFURY, T_TOGGLE },                                             // 4 Material Swap
        new[] { T_VRCFURY, T_TOGGLE, T_OBJECT_TOGGLE, T_BLENDSHAPE_ACTION, T_MATERIAL_ACTION }, // 5 Outfit
        new string[0],                                                              // 6 Contact Setup
        new[] { T_VRCFURY },                                                        // 7 Overview++
        new string[0],                                                              // 8 Doctor
        new string[0],                                                              // 9 PhysBone Manager
        new string[0],                                                              // 10 Constraint Toolkit
        new string[0],                                                              // 11 Texture & VRAM
        new string[0],                                                              // 12 Material Assembler
        new string[0],                                                              // 13 Animator & Parameters Doctor
        new string[0],                                                              // 14 PC / Quest Comparator
        new string[0],                                                              // 15 Material Inspector++
        new string[0],                                                              // 16 Preset Library
        new string[0],                                                              // 17 Pre-Upload Check
        new string[0],                                                              // 18 Snapshot & Compare
        new string[0],                                                              // 19 Animation Swapper & Usage Map
    };

    DependencyReport deps;
    static string lastError;
    static string lastErrorDetails;
    static string lastErrorHint;
    static string lastErrorContext;
    static DateTime lastErrorUtc;

    // Per-surface render error boundary. A render exception is captured,
    // the current IMGUI pass is aborted safely, and the next repaint
    // shows an isolated recovery card instead of repeatedly invoking
    // the broken tool.
    string renderErrorKey;
    string renderErrorTitle;
    string renderErrorMessage;
    string renderErrorDetails;
    DateTime renderErrorUtc;

}