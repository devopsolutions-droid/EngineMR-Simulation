using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

/// <summary>
/// Tools > Windows Native Free TTS Generator
///
/// 100% FREE, OFFLINE, NO API KEYS REQUIRED.
/// Uses Windows built-in Speech Synthesizer to generate voiceovers
/// and save WAV audio clips directly into your Unity project.
/// </summary>
public class WindowsTTSAudioGenerator : EditorWindow
{
    private const string PrefKeyVoice = "WindowsTTSVoice";
    private const string PrefKeyRate = "WindowsTTSRate";
    private const string PrefKeyVolume = "WindowsTTSVolume";
    private const string PrefKeyPartsFolder = "WindowsTTSPartsFolderOverride";

    private string _selectedVoice = "";
    private int _speechRate = 0; // -10 (slowest) to +10 (fastest)
    private int _speechVolume = 100; // 0 to 100

    private List<string> _installedVoices = new List<string>();
    private List<string> _installedVoiceGenders = new List<string>(); // "Female" or "Male" per voice
    private string[] _installedVoicesArray = new string[0];
    private string[] _installedVoicesDisplayArray = new string[0]; // with gender tag
    private int _selectedVoiceIndex = 0;
    private bool _isPreviewing = false;

    private GameObject _droppedModel;
    private bool _onlyMissing = true;
    private string _status = "";
    private bool _running = false;
    private CancellationTokenSource _cts;
    private Vector2 _windowScrollPos;
    private Vector2 _scroll;
    private Vector2 _stepsScrollPos;

    private int _tabSelection = 2; // Default to Single Text Mode
    private string _singleTextDescription = "";
    private string _singleAudioFilename = "free_tts_audio_clip";
    private string _singleSavePath = "Assets/ScriptableObjects/Data/Engines";
    private string _partsFolderOverride = "";

    [MenuItem("Tools/Windows Native Free TTS Generator")]
    public static void Open() => GetWindow<WindowsTTSAudioGenerator>("Windows Native Free TTS");

    void OnEnable()
    {
        _selectedVoice = EditorPrefs.GetString(PrefKeyVoice, "");
        _speechRate = EditorPrefs.GetInt(PrefKeyRate, 0);
        _speechVolume = EditorPrefs.GetInt(PrefKeyVolume, 100);
        _partsFolderOverride = EditorPrefs.GetString(PrefKeyPartsFolder, "");

        FetchInstalledWindowsVoices();
    }

    // Styles & GUI elements
    private GUIStyle _cardStyle;
    private GUIStyle _cardHeaderStyle;
    private GUIStyle _consoleStyle;
    private GUIStyle _tabButtonStyle;
    private GUIStyle _tabActiveButtonStyle;
    private bool _stylesInitialized = false;

    private Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    private Texture2D GetTexture(string key, Color col)
    {
        if (_textureCache.TryGetValue(key, out Texture2D tex) && tex != null)
            return tex;
        tex = MakeTex(2, 2, col);
        _textureCache[key] = tex;
        return tex;
    }

    private void ClearTextureCache()
    {
        foreach (var tex in _textureCache.Values)
        {
            if (tex != null) DestroyImmediate(tex);
        }
        _textureCache.Clear();
    }

    void OnDisable()
    {
        ClearTextureCache();
        _stylesInitialized = false;
    }

    private void InitializeStyles()
    {
        if (_stylesInitialized) return;

        Color cardBg = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 1f) : new Color(0.94f, 0.94f, 0.94f, 1f);
        _cardStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 12, 12, 12),
            margin = new RectOffset(4, 4, 6, 6)
        };
        _cardStyle.normal.background = GetTexture("card_bg", cardBg);

        _cardHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            margin = new RectOffset(0, 0, 0, 4)
        };
        _cardHeaderStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.95f) : new Color(0.1f, 0.1f, 0.15f);

        Color consoleBg = new Color(0.09f, 0.1f, 0.12f, 1f);
        _consoleStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(8, 8, 8, 8),
            margin = new RectOffset(4, 4, 4, 4)
        };
        _consoleStyle.normal.background = GetTexture("console_bg", consoleBg);

        Color normalBg = EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f, 1f) : new Color(0.85f, 0.85f, 0.85f, 1f);
        Color activeBg = new Color(0.15f, 0.65f, 0.45f, 1f); // Vibrant emerald green accent for Free Windows TTS
        Color hoverBg = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f, 1f) : new Color(0.9f, 0.9f, 0.9f, 1f);

        _tabButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 30
        };
        _tabButtonStyle.normal.background = GetTexture("tab_normal", normalBg);
        _tabButtonStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : Color.black;
        _tabButtonStyle.hover.background = GetTexture("tab_hover", hoverBg);
        _tabButtonStyle.hover.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;

        _tabActiveButtonStyle = new GUIStyle(_tabButtonStyle);
        _tabActiveButtonStyle.normal.background = GetTexture("tab_active", activeBg);
        _tabActiveButtonStyle.normal.textColor = Color.white;
        _tabActiveButtonStyle.hover.background = GetTexture("tab_active", activeBg);
        _tabActiveButtonStyle.hover.textColor = Color.white;

        _stylesInitialized = true;
    }

    private void DrawHeader()
    {
        Rect headerRect = GUILayoutUtility.GetRect(10, 54, GUILayout.ExpandWidth(true));
        Color bgCol = EditorGUIUtility.isProSkin ? new Color(0.14f, 0.2f, 0.17f, 1f) : new Color(0.84f, 0.92f, 0.88f, 1f);
        EditorGUI.DrawRect(headerRect, bgCol);

        Rect accentRect = new Rect(headerRect.x, headerRect.y + headerRect.height - 3f, headerRect.width, 3f);
        Color accentCol = new Color(0.15f, 0.65f, 0.45f); // Accent emerald green
        EditorGUI.DrawRect(accentRect, accentCol);

        Rect textRect = new Rect(headerRect.x + 12, headerRect.y + 10, headerRect.width - 24, 34);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleLeft
        };
        titleStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.1f, 0.1f, 0.1f, 1f);

        GUIStyle subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft
        };
        subtitleStyle.normal.textColor = Color.gray;

        GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 20), "WINDOWS NATIVE FREE TTS GENERATOR", titleStyle);
        GUI.Label(new Rect(textRect.x, textRect.y + 18, textRect.width, 16), "100% Free & Offline • Zero API keys or subscriptions required", subtitleStyle);

        GUILayout.Space(8);
    }

    private void BeginCard(string title, string icon = "")
    {
        GUILayout.BeginVertical(_cardStyle);
        GUILayout.BeginHorizontal();
        if (!string.IsNullOrEmpty(icon))
            GUILayout.Label(icon, GUILayout.Width(18));
        GUILayout.Label(title, _cardHeaderStyle);
        GUILayout.EndHorizontal();
        DrawDivider(new Color(0.5f, 0.5f, 0.5f, 0.15f));
        GUILayout.Space(6);
    }

    private void EndCard() => GUILayout.EndVertical();

    private void DrawDivider(Color col, float height = 1f)
    {
        Rect rect = GUILayoutUtility.GetRect(10, height, GUILayout.ExpandWidth(true));
        rect.height = height;
        EditorGUI.DrawRect(rect, col);
    }

    private void DrawTabs()
    {
        string[] tabNames = new string[] { "Batch Prefab Mode", "Assembly Steps Mode", "Single Text Mode" };

        GUILayout.BeginHorizontal();
        for (int i = 0; i < tabNames.Length; i++)
        {
            bool isActive = _tabSelection == i;
            GUIStyle style = isActive ? _tabActiveButtonStyle : _tabButtonStyle;

            if (GUILayout.Button(tabNames[i], style, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
            {
                _tabSelection = i;
                GUI.FocusControl(null);
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
    }

    private void DrawAssemblyStepsList(EngineAssemblyConfig config)
    {
        int stepCount = config.assemblySteps != null ? config.assemblySteps.Length : 0;
        GUILayout.Label($"Steps configured on model: {stepCount}", EditorStyles.boldLabel);
        GUILayout.Space(4);

        if (stepCount > 0)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            _stepsScrollPos = EditorGUILayout.BeginScrollView(_stepsScrollPos, GUILayout.Height(180));

            for (int i = 0; i < config.assemblySteps.Length; i++)
            {
                var step = config.assemblySteps[i];
                bool hasAudio = step.stepAudio != null;

                Color rowBg = (i % 2 == 0)
                    ? (EditorGUIUtility.isProSkin ? new Color(0.24f, 0.24f, 0.24f, 1f) : new Color(0.92f, 0.92f, 0.92f, 1f))
                    : (EditorGUIUtility.isProSkin ? new Color(0.2f, 0.2f, 0.2f, 1f) : new Color(0.88f, 0.88f, 0.88f, 1f));

                Rect rowRect = GUILayoutUtility.GetRect(10, 36, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(rowRect, rowBg);

                float badgeWidth = 70f;
                float padding = 6f;
                Rect badgeRect = new Rect(rowRect.x + rowRect.width - badgeWidth - padding, rowRect.y + (rowRect.height - 18) / 2f, badgeWidth, 18);
                Rect textRect = new Rect(rowRect.x + padding, rowRect.y + 2, rowRect.width - badgeWidth - padding * 3, 16);
                Rect descRect = new Rect(rowRect.x + padding, rowRect.y + 18, rowRect.width - badgeWidth - padding * 3, 16);

                GUI.Label(textRect, $"Step {i + 1}: {step.stepName}", EditorStyles.boldLabel);

                string descText = !string.IsNullOrEmpty(step.stepDescription) ? step.stepDescription : "<i>No description.</i>";
                GUIStyle descStyle = new GUIStyle(EditorStyles.miniLabel) { richText = true };
                GUI.Label(descRect, descText, descStyle);

                Color badgeBg = hasAudio ? new Color(0.15f, 0.5f, 0.25f, 1f) : new Color(0.7f, 0.2f, 0.2f, 1f);
                GUIStyle badgeStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 8,
                    fontStyle = FontStyle.Bold
                };
                badgeStyle.normal.background = GetTexture(hasAudio ? "badge_green" : "badge_red", badgeBg);
                badgeStyle.normal.textColor = Color.white;

                GUI.Label(badgeRect, hasAudio ? "AUDIO" : "MISSING", badgeStyle);
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
    }

    private void DrawConsoleLog()
    {
        if (string.IsNullOrEmpty(_status)) return;

        GUILayout.BeginVertical(_cardStyle);
        GUILayout.Label("Console Logs", _cardHeaderStyle);
        GUILayout.Space(2);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, _consoleStyle, GUILayout.Height(140));

        string formattedStatus = _status;
        formattedStatus = formattedStatus.Replace("[WindowsTTS]", "<color=#50fa7b><b>[WindowsTTS]</b></color>");
        formattedStatus = formattedStatus.Replace("ERROR:", "<color=#ff5555><b>ERROR:</b></color>");
        formattedStatus = formattedStatus.Replace("✓ Complete!", "<color=#50fa7b><b>✓ Complete!</b></color>");
        formattedStatus = formattedStatus.Replace("✓ Success!", "<color=#50fa7b><b>✓ Success!</b></color>");
        formattedStatus = formattedStatus.Replace("Generating", "<color=#8be9fd>Generating</color>");

        GUIStyle consoleTextStyle = new GUIStyle(EditorStyles.label)
        {
            richText = true,
            wordWrap = true,
            fontSize = 11
        };
        consoleTextStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        GUILayout.Label(formattedStatus, consoleTextStyle);

        EditorGUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    void OnGUI()
    {
        InitializeStyles();

        _windowScrollPos = EditorGUILayout.BeginScrollView(_windowScrollPos, GUIStyle.none, GUI.skin.verticalScrollbar);

        DrawHeader();

        // ── Card 1: Windows Voice Settings ────────────────────────────────────
        BeginCard("Windows Voice & Speech Settings", "🎤");

        // Voice selector with gender tags
        if (_installedVoicesDisplayArray.Length > 0)
        {
            int newVoiceIdx = EditorGUILayout.Popup("Select Voice", _selectedVoiceIndex, _installedVoicesDisplayArray);
            if (newVoiceIdx != _selectedVoiceIndex)
            {
                _selectedVoiceIndex = newVoiceIdx;
                _selectedVoice = _installedVoices[_selectedVoiceIndex];
                EditorPrefs.SetString(PrefKeyVoice, _selectedVoice);
            }

            // Show gender badge for selected voice
            if (_installedVoiceGenders.Count > _selectedVoiceIndex)
            {
                string gender = _installedVoiceGenders[_selectedVoiceIndex];
                bool isFemale = gender == "Female";
                Color badgeBg = isFemale ? new Color(0.8f, 0.2f, 0.6f, 1f) : new Color(0.2f, 0.45f, 0.8f, 1f);
                string genderIcon = isFemale ? "♀ Female Voice" : "♂ Male Voice";

                GUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUIUtility.labelWidth + 4);
                GUIStyle genderBadge = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
                genderBadge.normal.textColor = isFemale ? new Color(0.95f, 0.5f, 0.85f) : new Color(0.5f, 0.75f, 1f);
                GUILayout.Label(genderIcon, genderBadge);
                GUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Searching for installed Windows voices...", MessageType.Info);
        }

        GUILayout.Space(6);

        // Preview & Install row
        GUILayout.BeginHorizontal();
        GUI.enabled = !_running && !_isPreviewing && _installedVoicesArray.Length > 0;
        if (GUILayout.Button("▶ Preview Selected Voice", GUILayout.Height(26)))
        {
            PreviewVoice();
        }
        GUI.enabled = true;

        if (GUILayout.Button("+ Install More Voices", GUILayout.Height(26)))
        {
            // Open Windows 11/10 voice settings
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:speech") { UseShellExecute = true });
            }
            catch
            {
                try { Process.Start(new ProcessStartInfo("control") { Arguments = "speech", UseShellExecute = true }); }
                catch (System.Exception ex) { Debug.LogWarning($"[WindowsTTS] Could not open voice settings: {ex.Message}"); }
            }
        }
        GUILayout.EndHorizontal();

        if (_isPreviewing)
        {
            EditorGUILayout.HelpBox("Previewing voice... please wait.", MessageType.Info);
        }

        GUILayout.Space(4);
        // Info box for female voices
        bool hasFemalVoice = _installedVoiceGenders.Contains("Female");
        if (!hasFemalVoice && _installedVoicesArray.Length > 0)
        {
            EditorGUILayout.HelpBox(
                "⚠ No female voices installed on this PC.\n" +
                "Click '+ Install More Voices' → go to Settings → Speech → Add Voices.\n" +
                "Recommended female voices: Microsoft Zira (EN-US), Microsoft Hazel (EN-GB), Microsoft Susan (EN-GB).",
                MessageType.Warning);
        }

        DrawDivider(new Color(0.5f, 0.5f, 0.5f, 0.1f));
        GUILayout.Space(4);

        int newRate = EditorGUILayout.IntSlider("Speech Speed Rate", _speechRate, -10, 10);
        if (newRate != _speechRate)
        {
            _speechRate = newRate;
            EditorPrefs.SetInt(PrefKeyRate, _speechRate);
        }

        int newVol = EditorGUILayout.IntSlider("Volume", _speechVolume, 0, 100);
        if (newVol != _speechVolume)
        {
            _speechVolume = newVol;
            EditorPrefs.SetInt(PrefKeyVolume, _speechVolume);
        }

        GUILayout.Space(2);
        if (GUILayout.Button("↻ Refresh Installed Voices", GUILayout.Height(20)))
        {
            FetchInstalledWindowsVoices();
        }

        EndCard();

        GUILayout.Space(4);

        // ── Card 2: Execution Modes ─────────────────────────────────────────────
        BeginCard("Generation Modes", "⚙️");

        DrawTabs();

        if (_tabSelection == 0)
        {
            // Batch Prefab Mode
            _droppedModel = (GameObject)EditorGUILayout.ObjectField("3D Model Prefab / GLB", _droppedModel, typeof(GameObject), true);

            EditorGUILayout.BeginHorizontal();
            string newFolder = EditorGUILayout.TextField("Parts Folder Override", _partsFolderOverride);
            if (newFolder != _partsFolderOverride)
            {
                _partsFolderOverride = newFolder;
                EditorPrefs.SetString(PrefKeyPartsFolder, _partsFolderOverride);
            }
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Parts Folder containing PartData", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        _partsFolderOverride = "Assets" + path.Substring(Application.dataPath.Length);
                    else
                        _partsFolderOverride = path;
                    EditorPrefs.SetString(PrefKeyPartsFolder, _partsFolderOverride);
                }
            }
            EditorGUILayout.EndHorizontal();

            _onlyMissing = EditorGUILayout.Toggle("Only Missing Audio", _onlyMissing);

            GUILayout.Space(12);

            bool canRunBatch = !_running && _droppedModel != null;

            Color btnColor = canRunBatch ? new Color(0.15f, 0.65f, 0.45f) : new Color(0.35f, 0.35f, 0.35f);
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40
            };
            btnStyle.normal.background = GetTexture(canRunBatch ? "batch_btn" : "batch_btn_disabled", btnColor);
            btnStyle.normal.textColor = Color.white;
            btnStyle.hover.background = GetTexture("batch_btn_hover", canRunBatch ? btnColor * 1.15f : btnColor);
            btnStyle.hover.textColor = Color.white;

            GUI.enabled = canRunBatch;
            if (GUILayout.Button("Generate Free Voiceover Audio (Batch)", btnStyle))
            {
                RunGeneration();
            }
            GUI.enabled = true;

            if (_running)
            {
                GUILayout.Space(6);
                if (GUILayout.Button("🛑 Cancel Generation", GUILayout.Height(32)))
                {
                    _cts?.Cancel();
                    _status = "Cancelling generation...";
                    Repaint();
                }
            }

            GUILayout.Space(6);
            if (_droppedModel == null)
                EditorGUILayout.HelpBox("Please drag and drop a 3D Model Prefab to analyze parts.", MessageType.Info);
        }
        else if (_tabSelection == 1)
        {
            // Assembly Steps Mode
            _droppedModel = (GameObject)EditorGUILayout.ObjectField("3D Model Prefab / GLB", _droppedModel, typeof(GameObject), true);

            EditorGUILayout.BeginHorizontal();
            string newFolder = EditorGUILayout.TextField("Parts Folder Override", _partsFolderOverride);
            if (newFolder != _partsFolderOverride)
            {
                _partsFolderOverride = newFolder;
                EditorPrefs.SetString(PrefKeyPartsFolder, _partsFolderOverride);
            }
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Parts Folder containing PartData", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        _partsFolderOverride = "Assets" + path.Substring(Application.dataPath.Length);
                    else
                        _partsFolderOverride = path;
                    EditorPrefs.SetString(PrefKeyPartsFolder, _partsFolderOverride);
                }
            }
            EditorGUILayout.EndHorizontal();

            _onlyMissing = EditorGUILayout.Toggle("Only Missing Audio", _onlyMissing);

            GUILayout.Space(6);

            EngineAssemblyConfig config = null;
            if (_droppedModel != null)
                config = _droppedModel.GetComponentInChildren<EngineAssemblyConfig>(true);

            if (_droppedModel != null && config == null)
                EditorGUILayout.HelpBox("Selected model does not contain an EngineAssemblyConfig component.", MessageType.Error);
            else if (config != null)
                DrawAssemblyStepsList(config);

            GUILayout.Space(12);

            bool canRunAssembly = !_running && config != null && config.assemblySteps != null && config.assemblySteps.Length > 0;

            Color btnColor = canRunAssembly ? new Color(0.15f, 0.65f, 0.45f) : new Color(0.35f, 0.35f, 0.35f);
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40
            };
            btnStyle.normal.background = GetTexture(canRunAssembly ? "assembly_btn" : "assembly_btn_disabled", btnColor);
            btnStyle.normal.textColor = Color.white;
            btnStyle.hover.background = GetTexture("assembly_btn_hover", canRunAssembly ? btnColor * 1.15f : btnColor);
            btnStyle.hover.textColor = Color.white;

            GUI.enabled = canRunAssembly;
            if (GUILayout.Button("Generate Free Assembly Step Audio (Batch)", btnStyle))
            {
                RunAssemblyStepsGeneration();
            }
            GUI.enabled = true;

            if (_running)
            {
                GUILayout.Space(6);
                if (GUILayout.Button("🛑 Cancel Generation", GUILayout.Height(32)))
                {
                    _cts?.Cancel();
                    _status = "Cancelling generation...";
                    Repaint();
                }
            }

            GUILayout.Space(6);
            if (_droppedModel == null)
                EditorGUILayout.HelpBox("Please drag and drop a 3D Model Prefab.", MessageType.Info);
        }
        else
        {
            // Single Text Mode
            _singleAudioFilename = EditorGUILayout.TextField("Output Filename", _singleAudioFilename);

            GUILayout.BeginHorizontal();
            _singleSavePath = EditorGUILayout.TextField("Save Folder", _singleSavePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60), GUILayout.Height(18)))
            {
                string startDir = Path.Combine(Application.dataPath, "ScriptableObjects/Data/Engines").Replace("\\", "/");
                string chosenFolder = EditorUtility.OpenFolderPanel("Select Save Folder", startDir, "");
                if (!string.IsNullOrEmpty(chosenFolder))
                {
                    string absoluteAssetsPath = Path.GetFullPath(Application.dataPath).Replace("\\", "/");
                    string normalizedChosen = chosenFolder.Replace("\\", "/");
                    if (normalizedChosen.StartsWith(absoluteAssetsPath))
                    {
                        if (normalizedChosen == absoluteAssetsPath)
                            _singleSavePath = "Assets";
                        else
                            _singleSavePath = "Assets" + normalizedChosen.Substring(absoluteAssetsPath.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid Folder", "Please select a folder inside your Unity project's Assets directory.", "OK");
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("Speech text description:", EditorStyles.boldLabel);
            GUIStyle wordWrappedTextArea = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            _singleTextDescription = EditorGUILayout.TextArea(_singleTextDescription, wordWrappedTextArea, GUILayout.Height(100));

            GUILayout.Space(12);

            bool canRunSingle = !_running && !string.IsNullOrEmpty(_singleTextDescription) && !string.IsNullOrEmpty(_singleAudioFilename);

            Color btnColor = canRunSingle ? new Color(0.15f, 0.65f, 0.45f) : new Color(0.35f, 0.35f, 0.35f);
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40
            };
            btnStyle.normal.background = GetTexture(canRunSingle ? "single_btn" : "single_btn_disabled", btnColor);
            btnStyle.normal.textColor = Color.white;
            btnStyle.hover.background = GetTexture("single_btn_hover", canRunSingle ? btnColor * 1.15f : btnColor);
            btnStyle.hover.textColor = Color.white;

            GUI.enabled = canRunSingle;
            if (GUILayout.Button("Generate Free Single Audio Clip", btnStyle))
            {
                RunSingleGeneration();
            }
            GUI.enabled = true;

            if (_running)
            {
                GUILayout.Space(6);
                if (GUILayout.Button("🛑 Cancel Generation", GUILayout.Height(32)))
                {
                    _cts?.Cancel();
                    _status = "Cancelling generation...";
                    Repaint();
                }
            }
        }

        EndCard();

        // ── Console Output Log ──────────────────────────────────────────────────
        DrawConsoleLog();

        GUILayout.Space(8);
        EditorGUILayout.EndScrollView();
    }

    // ── Windows Speech Synthesis Core ─────────────────────────────────────────

    private void FetchInstalledWindowsVoices()
    {
        _installedVoices.Clear();
        _installedVoiceGenders.Clear();

        try
        {
            // Fetch name AND gender together, separated by a pipe delimiter
            string psScript = "Add-Type -AssemblyName System.Speech; $s = New-Object System.Speech.Synthesis.SpeechSynthesizer; $s.GetInstalledVoices() | ForEach-Object { $_.VoiceInfo.Name + '|' + $_.VoiceInfo.Gender }";
            byte[] scriptBytes = System.Text.Encoding.Unicode.GetBytes(psScript);
            string encoded = System.Convert.ToBase64String(scriptBytes);

            ProcessStartInfo psi = new ProcessStartInfo("powershell", $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            using Process p = Process.Start(psi);
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);

            string[] lines = output.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                string[] parts = trimmed.Split('|');
                string voiceName = parts[0].Trim();
                string gender = parts.Length > 1 ? parts[1].Trim() : "Unknown";

                // Normalize PowerShell enum output (Male/Female/Neutral)
                if (gender == "Male") gender = "Male";
                else if (gender == "Female") gender = "Female";
                else gender = "Neutral";

                if (!string.IsNullOrEmpty(voiceName) && !_installedVoices.Contains(voiceName))
                {
                    _installedVoices.Add(voiceName);
                    _installedVoiceGenders.Add(gender);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[WindowsTTS] Warning scanning installed voices: {ex.Message}");
        }

        if (_installedVoices.Count == 0)
        {
            _installedVoices.Add("Default Windows Voice");
            _installedVoiceGenders.Add("Unknown");
        }

        _installedVoicesArray = _installedVoices.ToArray();

        // Build display names with gender icon
        _installedVoicesDisplayArray = new string[_installedVoices.Count];
        for (int i = 0; i < _installedVoices.Count; i++)
        {
            string g = i < _installedVoiceGenders.Count ? _installedVoiceGenders[i] : "Unknown";
            string icon = g == "Female" ? "♀" : (g == "Male" ? "♂" : "◉");
            _installedVoicesDisplayArray[i] = $"{icon}  {_installedVoices[i]}  [{g}]";
        }

        _selectedVoiceIndex = System.Array.IndexOf(_installedVoicesArray, _selectedVoice);
        if (_selectedVoiceIndex < 0) _selectedVoiceIndex = 0;
        _selectedVoice = _installedVoicesArray[_selectedVoiceIndex];

        Debug.Log($"[WindowsTTS] Found {_installedVoices.Count} installed voice(s):");
        for (int i = 0; i < _installedVoices.Count; i++)
        {
            string g = i < _installedVoiceGenders.Count ? _installedVoiceGenders[i] : "?";
            Debug.Log($"  [{g}] {_installedVoices[i]}");
        }
    }

    private async void PreviewVoice()
    {
        if (_isPreviewing) return;
        _isPreviewing = true;
        Repaint();

        string previewText = "Hello! This is a preview of the selected voice. I will now narrate your engine parts.";
        string tempPath = Path.Combine(System.IO.Path.GetTempPath(), "WindowsTTS_Preview.wav");

        bool ok = await SynthesizeToWav(previewText, tempPath);

        if (ok && File.Exists(tempPath))
        {
            // Play via Windows Media Player or default audio player
            try
            {
                Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[WindowsTTS] Could not auto-play preview: {ex.Message}. File saved to: {tempPath}");
            }
        }
        else
        {
            Debug.LogError("[WindowsTTS] Preview synthesis failed.");
        }

        _isPreviewing = false;
        Repaint();
    }

    /// <summary>
    /// Sets Unity AudioImporter settings for a WAV file to preserve full quality:
    /// - No compression (PCM)
    /// - 44100 Hz sample rate preserved
    /// - No force-to-mono
    /// This prevents Unity from silently downsampling or compressing the imported audio.
    /// </summary>
    private static void SetHighQualityAudioImportSettings(string assetPath)
    {
        AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
        if (importer == null) return;

        importer.forceToMono = false;
        importer.loadInBackground = true;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.preloadAudioData = false;
        settings.compressionFormat = AudioCompressionFormat.PCM;   // Lossless — no quality loss
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate; // Keep 44100 Hz
        settings.quality = 1.0f;
        importer.defaultSampleSettings = settings;

        importer.SaveAndReimport();
    }

    private async Task<bool> SynthesizeToWav(string text, string outputPath, CancellationToken token = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Ensure absolute path with Windows backslashes
                string fullWindowsPath = Path.GetFullPath(outputPath).Replace("/", "\\");
                string parentDir = Path.GetDirectoryName(fullWindowsPath);

                if (!Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                // Clean text for speech
                string safeText = text.Replace("\"", "'").Replace("\n", " ").Replace("\r", " ").Trim();
                if (string.IsNullOrEmpty(safeText)) return false;

                // Build clean PowerShell script
                StringBuilder psScript = new StringBuilder();
                psScript.AppendLine("Add-Type -AssemblyName System.Speech;");
                psScript.AppendLine("$s = New-Object System.Speech.Synthesis.SpeechSynthesizer;");

                if (!string.IsNullOrEmpty(_selectedVoice) && _selectedVoice != "Default Windows Voice")
                {
                    string safeVoice = _selectedVoice.Replace("'", "''");
                    psScript.AppendLine($"try {{ $s.SelectVoice('{safeVoice}') }} catch {{ }};");
                }

                psScript.AppendLine($"$s.Rate = {_speechRate};");
                psScript.AppendLine($"$s.Volume = {_speechVolume};");

                // ─── HIGH QUALITY AUDIO FORMAT ───────────────────────────────────────────
                // Force 44100 Hz, 16-bit, Mono (CD quality) instead of the default 8kHz 8-bit
                psScript.AppendLine("$fmt = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(44100, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono);");
                psScript.AppendLine($"$s.SetOutputToWaveFile('{fullWindowsPath}', $fmt);");
                // ─────────────────────────────────────────────────────────────────────────

                psScript.AppendLine($"$s.Speak(\"{safeText}\");");
                psScript.AppendLine("Start-Sleep -Milliseconds 250;"); // small delay to flush audio before disposal
                psScript.AppendLine("$s.Dispose();");;

                // Encode script to Unicode Base64 for PowerShell -EncodedCommand (prevents all CLI quoting/escaping bugs)
                byte[] bytes = System.Text.Encoding.Unicode.GetBytes(psScript.ToString());
                string encodedCommand = System.Convert.ToBase64String(bytes);

                ProcessStartInfo psi = new ProcessStartInfo("powershell", $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using Process p = Process.Start(psi);
                string errorOutput = p.StandardError.ReadToEnd();
                
                while (!p.HasExited)
                {
                    if (token.IsCancellationRequested)
                    {
                        try { p.Kill(); } catch { }
                        return false;
                    }
                    Thread.Sleep(50);
                }

                if (p.ExitCode != 0 || !string.IsNullOrEmpty(errorOutput))
                {
                    if (!string.IsNullOrEmpty(errorOutput))
                        Debug.LogWarning($"[WindowsTTS] PowerShell Warning/Error: {errorOutput.Trim()}");
                }

                bool fileCreated = File.Exists(fullWindowsPath) && new FileInfo(fullWindowsPath).Length > 0;
                if (!fileCreated)
                {
                    Debug.LogError($"[WindowsTTS] Failed to create WAV file at path: {fullWindowsPath}");
                }

                return fileCreated;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WindowsTTS] Exception during TTS synthesis: {ex.Message}");
                return false;
            }
        });
    }

    // ── Batch Generation logic for Parts ──────────────────────────────────────

    async void RunGeneration()
    {
        _cts = new CancellationTokenSource();
        _running = true;
        _status = "Locating Parts folder for this model...";
        Repaint();

        string partsFolder = !string.IsNullOrEmpty(_partsFolderOverride) && Directory.Exists(_partsFolderOverride)
            ? _partsFolderOverride
            : FindPartsFolderForModel(_droppedModel);

        if (string.IsNullOrEmpty(partsFolder))
        {
            _status = "ERROR: Could not find a Parts folder for this model.\n\n" +
                       "Make sure you ran Tools → Engine Part Setup on this model first, or manually select the folder above.";
            _running = false; Repaint(); return;
        }

        string audioFolder = partsFolder + "/Audio";
        if (!Directory.Exists(audioFolder))
        {
            Directory.CreateDirectory(audioFolder);
            AssetDatabase.Refresh();
        }

        _status = $"Parts folder found:\n{partsFolder}\n\nLoading PartData assets...";
        Repaint();

        var guids = AssetDatabase.FindAssets("t:PartData", new[] { partsFolder });
        if (guids.Length == 0)
        {
            _status = $"No PartData assets found in:\n{partsFolder}";
            _running = false; Repaint(); return;
        }

        var parts = new List<PartData>();
        foreach (var g in guids)
        {
            var pd = AssetDatabase.LoadAssetAtPath<PartData>(AssetDatabase.GUIDToAssetPath(g));
            if (pd != null) parts.Add(pd);
        }

        EnginePart[] prefabEngineParts = _droppedModel.GetComponentsInChildren<EnginePart>(true);
        bool prefabWasDirtied = false;

        _status = $"Found {parts.Count} parts. Starting Windows TTS generation...";
        Repaint();

        int processedCount = 0;
        int reusedCount = 0;
        int skippedCount = 0;
        int failedCount = 0;

        var audioCache = new Dictionary<string, AudioClip>();

        foreach (var pd in parts)
        {
            if (pd != null && pd.audioExplanation != null && !string.IsNullOrEmpty(pd.description))
            {
                string key = pd.description.Trim();
                if (!audioCache.ContainsKey(key))
                    audioCache[key] = pd.audioExplanation;
            }
        }

        for (int i = 0; i < parts.Count; i++)
        {
            if (_cts != null && _cts.IsCancellationRequested)
            {
                EditorUtility.ClearProgressBar();
                _status = "Generation cancelled by user.";
                _running = false;
                Repaint();
                return;
            }

            var pd = parts[i];
            if (pd == null) continue;

            if (_onlyMissing && pd.audioExplanation != null)
            {
                skippedCount++;
                continue;
            }

            if (string.IsNullOrEmpty(pd.description) || pd.description == "Part description here.")
            {
                Debug.LogWarning($"[WindowsTTS] Skipping '{pd.partName}' - Description is empty or placeholder.");
                skippedCount++;
                continue;
            }

            string cleanDesc = pd.description.Trim();

            // Deduplication check
            if (audioCache.TryGetValue(cleanDesc, out AudioClip existingClip) && existingClip != null)
            {
                pd.audioExplanation = existingClip;
                EditorUtility.SetDirty(pd);

                foreach (var ep in prefabEngineParts)
                {
                    if (ep.partData == pd || (ep.partData != null && !string.IsNullOrEmpty(ep.partData.description) && ep.partData.description.Trim() == cleanDesc))
                    {
                        ep.audioExplanation = existingClip;
                        EditorUtility.SetDirty(ep);
                        prefabWasDirtied = true;
                    }
                }

                reusedCount++;
                Debug.Log($"[WindowsTTS] Reused shared audio clip for '{pd.partName}' — synthesis skipped!");
                continue;
            }

            EditorUtility.DisplayProgressBar("Generating Free Audio via Windows Native TTS", $"Processing {pd.partName} ({i + 1}/{parts.Count})...", (float)i / parts.Count);
            _status = $"Synthesizing audio for '{pd.partName}' via Windows Speech...\nText: {pd.description}";
            Repaint();

            string safeName = pd.partName.Replace(":", "_").Replace("/", "_").Replace("\\", "_");
            string relativePath = $"{audioFolder}/{safeName}_explanation.wav";
            string fullPath = Path.Combine(Application.dataPath, relativePath.Substring(7)).Replace("\\", "/");

            bool success = await SynthesizeToWav(pd.description, fullPath, _cts.Token);
            if (success)
            {
                try
                {
                    SetHighQualityAudioImportSettings(relativePath);
                    AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);

                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(relativePath);
                    if (clip != null)
                    {
                        pd.audioExplanation = clip;
                        EditorUtility.SetDirty(pd);

                        audioCache[cleanDesc] = clip;

                        foreach (var ep in prefabEngineParts)
                        {
                            if (ep.partData == pd || (ep.partData != null && !string.IsNullOrEmpty(ep.partData.description) && ep.partData.description.Trim() == cleanDesc))
                            {
                                ep.audioExplanation = clip;
                                EditorUtility.SetDirty(ep);
                                prefabWasDirtied = true;
                            }
                        }

                        processedCount++;
                    }
                    else
                    {
                        Debug.LogError($"[WindowsTTS] Failed to load imported AudioClip at {relativePath}");
                        failedCount++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[WindowsTTS] Error importing WAV asset: {ex.Message}");
                    failedCount++;
                }
            }
            else
            {
                failedCount++;
            }

            try
            {
                await Task.Delay(100, _cts.Token);
            }
            catch (TaskCanceledException)
            {
                EditorUtility.ClearProgressBar();
                _status = "Generation cancelled by user.";
                _running = false;
                Repaint();
                return;
            }
        }

        EditorUtility.ClearProgressBar();

        if (prefabWasDirtied)
        {
            string prefabPath = AssetDatabase.GetAssetPath(_droppedModel);
            if (!string.IsNullOrEmpty(prefabPath))
                PrefabUtility.SavePrefabAsset(_droppedModel);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _status = $"✓ Complete!\n\n" +
                   $"New Generated: {processedCount}\n" +
                   $"Reused (Deduplicated): {reusedCount}\n" +
                   $"Skipped: {skippedCount}\n" +
                   $"Failed: {failedCount}\n\n" +
                   $"Audio files saved in:\n{audioFolder}";
        _running = false;
        Repaint();
    }

    // ── Batch Generation logic for Assembly Steps ─────────────────────────────

    async void RunAssemblyStepsGeneration()
    {
        var config = _droppedModel.GetComponentInChildren<EngineAssemblyConfig>(true);
        if (config == null || config.assemblySteps == null || config.assemblySteps.Length == 0)
        {
            _status = "ERROR: No EngineAssemblyConfig or assembly steps found on the model.";
            Repaint();
            return;
        }

        _cts = new CancellationTokenSource();
        _running = true;
        _status = "Locating Parts folder for this model...";
        Repaint();

        string partsFolder = !string.IsNullOrEmpty(_partsFolderOverride) && Directory.Exists(_partsFolderOverride)
            ? _partsFolderOverride
            : FindPartsFolderForModel(_droppedModel);

        if (string.IsNullOrEmpty(partsFolder))
        {
            _status = "ERROR: Could not find a Parts folder for this model.";
            _running = false; Repaint(); return;
        }

        string audioFolder = partsFolder + "/Audio";
        if (!Directory.Exists(audioFolder))
        {
            Directory.CreateDirectory(audioFolder);
            AssetDatabase.Refresh();
        }

        _status = $"Parts folder found:\n{partsFolder}\n\nStarting assembly steps audio generation via Windows Speech...";
        Repaint();

        var steps = config.assemblySteps;
        int total = steps.Length;
        int processedCount = 0;
        int reusedCount = 0;
        int skippedCount = 0;
        int failedCount = 0;
        bool configWasDirtied = false;

        var stepAudioCache = new Dictionary<string, AudioClip>();
        for (int i = 0; i < total; i++)
        {
            if (steps[i].stepAudio != null)
            {
                string textKey = $"{steps[i].stepName}.! {steps[i].stepDescription}".Trim();
                if (!stepAudioCache.ContainsKey(textKey))
                    stepAudioCache[textKey] = steps[i].stepAudio;
            }
        }

        for (int i = 0; i < total; i++)
        {
            if (_cts != null && _cts.IsCancellationRequested)
            {
                EditorUtility.ClearProgressBar();
                _status = "Generation cancelled by user.";
                _running = false;
                Repaint();
                return;
            }

            AssemblyStep step = steps[i];

            if (_onlyMissing && step.stepAudio != null)
            {
                skippedCount++;
                continue;
            }

            if (string.IsNullOrEmpty(step.stepName) && string.IsNullOrEmpty(step.stepDescription))
            {
                skippedCount++;
                continue;
            }

            string textToGenerate = "";
            if (!string.IsNullOrEmpty(step.stepName) && !string.IsNullOrEmpty(step.stepDescription))
                textToGenerate = $"{step.stepName}.! {step.stepDescription}";
            else if (!string.IsNullOrEmpty(step.stepName))
                textToGenerate = step.stepName;
            else if (!string.IsNullOrEmpty(step.stepDescription))
                textToGenerate = step.stepDescription;

            string cleanText = textToGenerate.Trim();

            if (stepAudioCache.TryGetValue(cleanText, out AudioClip existingClip) && existingClip != null)
            {
                steps[i].stepAudio = existingClip;
                configWasDirtied = true;
                reusedCount++;
                Debug.Log($"[WindowsTTS] Reused audio clip for Step {i + 1} '{step.stepName}' — synthesis skipped!");
                continue;
            }

            EditorUtility.DisplayProgressBar("Generating Free Assembly Step Audio via Windows Native TTS", $"Processing Step {i + 1}/{total}: {step.stepName}...", (float)i / total);
            _status = $"Synthesizing audio for Step {i + 1}/{total}...\nText: {textToGenerate}";
            Repaint();

            string safeName = string.IsNullOrEmpty(step.stepName) ? $"step_{i + 1}" : step.stepName;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(c, '_');
            }
            safeName = safeName.Replace(":", "_").Replace("/", "_").Replace("\\", "_");

            string relativePath = $"{audioFolder}/Step_{i + 1}_{safeName}.wav";
            string fullPath = Path.Combine(Application.dataPath, relativePath.Substring(7)).Replace("\\", "/");

            bool success = await SynthesizeToWav(textToGenerate, fullPath, _cts.Token);
            if (success)
            {
                try
                {
                    SetHighQualityAudioImportSettings(relativePath);
                    AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);

                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(relativePath);
                    if (clip != null)
                    {
                        steps[i].stepAudio = clip;
                        stepAudioCache[cleanText] = clip;
                        configWasDirtied = true;
                        processedCount++;
                    }
                    else
                    {
                        Debug.LogError($"[WindowsTTS] Failed to load imported AudioClip at {relativePath}");
                        failedCount++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[WindowsTTS] Error importing WAV asset: {ex.Message}");
                    failedCount++;
                }
            }
            else
            {
                failedCount++;
            }

            try
            {
                await Task.Delay(100, _cts.Token);
            }
            catch (TaskCanceledException)
            {
                EditorUtility.ClearProgressBar();
                _status = "Generation cancelled by user.";
                _running = false;
                Repaint();
                return;
            }
        }

        EditorUtility.ClearProgressBar();

        if (configWasDirtied)
        {
            config.assemblySteps = steps;
            EditorUtility.SetDirty(config);

            string prefabPath = AssetDatabase.GetAssetPath(_droppedModel);
            if (!string.IsNullOrEmpty(prefabPath))
                PrefabUtility.SavePrefabAsset(_droppedModel);

            AssetDatabase.SaveAssets();
        }

        AssetDatabase.Refresh();

        _status = $"✓ Complete!\n\n" +
                   $"New Generated: {processedCount}\n" +
                   $"Reused (Deduplicated): {reusedCount}\n" +
                   $"Skipped: {skippedCount}\n" +
                   $"Failed: {failedCount}\n\n" +
                   $"Audio files saved in:\n{audioFolder}";
        _running = false;
        Repaint();
    }

    // ── Single Generation logic ───────────────────────────────────────────────

    async void RunSingleGeneration()
    {
        _cts = new CancellationTokenSource();
        _running = true;
        _status = $"Synthesizing single audio via Windows Speech: '{_singleAudioFilename}'...";
        Repaint();

        string saveFolder = _singleSavePath.Replace("\\", "/");
        if (saveFolder.EndsWith("/")) saveFolder = saveFolder.Substring(0, saveFolder.Length - 1);

        string systemFolder;
        if (saveFolder == "Assets")
            systemFolder = Application.dataPath;
        else if (saveFolder.StartsWith("Assets/"))
            systemFolder = Path.Combine(Application.dataPath, saveFolder.Substring(7));
        else
            systemFolder = saveFolder;

        if (!Directory.Exists(systemFolder))
        {
            try
            {
                Directory.CreateDirectory(systemFolder);
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                _status = $"ERROR: Failed to create save folder '{saveFolder}': {ex.Message}";
                _running = false; Repaint(); return;
            }
        }

        EditorUtility.DisplayProgressBar("Generating Free Single Audio via Windows Native TTS", $"Processing {_singleAudioFilename}...", 0.5f);

        string safeName = _singleAudioFilename.Replace(":", "_").Replace("/", "_").Replace("\\", "_");
        if (!safeName.ToLower().EndsWith(".wav")) safeName += ".wav";

        string relativePath = $"{saveFolder}/{safeName}";
        string fullPath;

        if (saveFolder == "Assets")
            fullPath = Path.Combine(Application.dataPath, safeName);
        else if (saveFolder.StartsWith("Assets/"))
            fullPath = Path.Combine(Application.dataPath, saveFolder.Substring(7), safeName);
        else
            fullPath = Path.Combine(saveFolder, safeName);

        fullPath = fullPath.Replace("\\", "/");

        bool success = await SynthesizeToWav(_singleTextDescription, fullPath, _cts.Token);
        EditorUtility.ClearProgressBar();

        if (success)
        {
            try
            {
                SetHighQualityAudioImportSettings(relativePath);
                AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(relativePath);
                if (clip != null)
                {
                    _status = $"✓ Success!\n\nAudio Clip successfully generated and saved to:\n{relativePath}\n\nYou can now assign it to any component.";
                    EditorGUIUtility.PingObject(clip);
                }
                else
                {
                    _status = $"ERROR: Failed to load imported AudioClip at:\n{relativePath}";
                }
            }
            catch (System.Exception ex)
            {
                _status = $"ERROR: Writing file failed: {ex.Message}";
            }
        }
        else
        {
            _status = "ERROR: Failed to synthesize audio. Check console for details.";
        }

        _running = false;
        Repaint();
    }

    static string FindPartsFolderForModel(GameObject model)
    {
        if (model == null) return null;
        string modelAssetPath = AssetDatabase.GetAssetPath(model);
        string modelName = model.name;
        string modelNorm = Normalize(modelName);

        string[] edGuids = AssetDatabase.FindAssets("t:EngineData");
        foreach (var guid in edGuids)
        {
            var ed = AssetDatabase.LoadAssetAtPath<EngineData>(AssetDatabase.GUIDToAssetPath(guid));
            if (ed == null) continue;

            bool match = false;
            if (ed.enginePrefab != null)
                match = AssetDatabase.GetAssetPath(ed.enginePrefab) == modelAssetPath;

            if (!match && ed.engineName != null)
                match = Normalize(ed.engineName) == modelNorm;

            if (match)
            {
                string edDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(ed)).Replace("\\", "/");
                string partsPath = edDir + "/Parts";
                if (Directory.Exists(partsPath)) return partsPath;
            }
        }

        string[] allDirs = Directory.GetDirectories("Assets", "Parts", SearchOption.AllDirectories);
        foreach (var dir in allDirs)
        {
            string unityDir = dir.Replace("\\", "/");
            string engineFolder = Path.GetFileName(Path.GetDirectoryName(unityDir));
            if (Normalize(engineFolder) == modelNorm)
                return unityDir;
        }
        foreach (var dir in allDirs)
        {
            string unityDir = dir.Replace("\\", "/");
            string engineFolder = Path.GetFileName(Path.GetDirectoryName(unityDir));
            if (Normalize(engineFolder).Contains(modelNorm) || modelNorm.Contains(Normalize(engineFolder)))
                return unityDir;
        }

        return null;
    }

    static string Normalize(string s) =>
        s.Replace(" ", "").Replace("_", "").Replace("-", "").ToLower();
}
