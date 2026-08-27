using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

/// <summary>
/// Tools > Voicemaker Audio Generator
///
/// Usage:
///   1. Paste your Voicemaker.in API Key.
///   2. Select your Engine prefab.
///   3. Choose Engine type (neural / standard) and Voice ID.
///   4. Click Generate to convert descriptions to AudioClips via Voicemaker.in.
/// </summary>
public class VoicemakerAudioGenerator : EditorWindow
{
    private const string VoicemakerEndpoint = "https://developer.voicemaker.in/api/v1/voice/convert";
    private const string PrefKeyApi = "VoicemakerAPIKey";
    private const string PrefKeyVoice = "VoicemakerVoiceID";
    private const string PrefKeyEngine = "VoicemakerEngine";
    private const string PrefKeySampleRate = "VoicemakerSampleRate";
    private const string PrefKeyPartsFolder = "VoicemakerPartsFolderOverride";

    private const string DefaultVoiceId = "ai3-Jony"; // Default Voicemaker Voice ID from official API docs
    private const string DefaultEngine = "neural";
    private const string DefaultSampleRate = "48000";
    private const string DefaultLanguageCode = "en-US";

    private string _apiKey = "";
    private string _voiceId = DefaultVoiceId;
    private string _engineType = DefaultEngine;
    private string _sampleRate = DefaultSampleRate;
    private string _languageCode = DefaultLanguageCode;
    private int _outputFormatIndex = 0; // 0 = mp3, 1 = wav, 2 = ogg
    private string _masterSpeed = "0";
    private string _masterVolume = "0";
    private string _masterPitch = "0";

    private readonly string[] _outputFormats = new string[] { "mp3", "wav", "ogg" };
    private readonly string[] _outputFormatDisplayNames = new string[] { "MP3 Audio (.mp3)", "WAV Audio (.wav)", "OGG Audio (.ogg)" };
    private readonly string[] _engineTypes = new string[] { "neural", "standard" };
    private readonly string[] _engineDisplayNames = new string[] { "Neural (AI2 / AI3 / Pro Voices)", "Standard (AI1 Voices)" };

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
    private string _singleAudioFilename = "voicemaker_audio_clip";
    private string _singleSavePath = "Assets/ScriptableObjects/Data/Engines";
    private string _partsFolderOverride = "";

    [MenuItem("Tools/Voicemaker Audio Generator")]
    public static void Open() => GetWindow<VoicemakerAudioGenerator>("Voicemaker TTS Audio");

    void OnEnable()
    {
        _apiKey = EditorPrefs.GetString(PrefKeyApi, "");
        _voiceId = EditorPrefs.GetString(PrefKeyVoice, DefaultVoiceId);
        _engineType = EditorPrefs.GetString(PrefKeyEngine, DefaultEngine);
        _sampleRate = EditorPrefs.GetString(PrefKeySampleRate, DefaultSampleRate);
        _languageCode = EditorPrefs.GetString("VoicemakerLanguageCode", DefaultLanguageCode);
        _partsFolderOverride = EditorPrefs.GetString(PrefKeyPartsFolder, "");
        _outputFormatIndex = EditorPrefs.GetInt("VoicemakerOutputFormatIndex", 0);
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
        Color activeBg = new Color(0.55f, 0.35f, 0.85f, 1f); // Vibrant purple accent for Voicemaker
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
        Color bgCol = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.15f, 0.22f, 1f) : new Color(0.88f, 0.84f, 0.92f, 1f);
        EditorGUI.DrawRect(headerRect, bgCol);

        Rect accentRect = new Rect(headerRect.x, headerRect.y + headerRect.height - 3f, headerRect.width, 3f);
        Color accentCol = new Color(0.55f, 0.35f, 0.85f); // Accent purple
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

        GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 20), "VOICEMAKER.IN TTS AUDIO GENERATOR", titleStyle);
        GUI.Label(new Rect(textRect.x, textRect.y + 18, textRect.width, 16), "Generate AI voiceovers using your Voicemaker.in API subscription", subtitleStyle);

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
        formattedStatus = formattedStatus.Replace("[Voicemaker]", "<color=#a855f7><b>[Voicemaker]</b></color>");
        formattedStatus = formattedStatus.Replace("ERROR:", "<color=#ff5555><b>ERROR:</b></color>");
        formattedStatus = formattedStatus.Replace("Failed:", "<color=#ff5555><b>Failed:</b></color>");
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

        // ── Card 1: API & Credentials ──────────────────────────────────────────
        BeginCard("Voicemaker.in Credentials & Voice Settings", "🔑");

        GUILayout.Label("Voicemaker API Key", EditorStyles.boldLabel);
        string newKey = EditorGUILayout.PasswordField(_apiKey);
        if (newKey != _apiKey)
        {
            _apiKey = SanitizeKey(newKey);
            EditorPrefs.SetString(PrefKeyApi, _apiKey);
        }

        if (string.IsNullOrEmpty(_apiKey))
        {
            EditorGUILayout.HelpBox("Get your API Key from Voicemaker.in -> Developer Dashboard -> API Keys.", MessageType.Info);
        }

        GUILayout.Space(6);

        // Engine type selection (Neural / Standard)
        int selectedEngineIdx = System.Array.IndexOf(_engineTypes, _engineType.ToLower());
        if (selectedEngineIdx < 0) selectedEngineIdx = 0;
        int newEngineIdx = EditorGUILayout.Popup("Voice Engine", selectedEngineIdx, _engineDisplayNames);
        if (newEngineIdx != selectedEngineIdx)
        {
            _engineType = _engineTypes[newEngineIdx];
            EditorPrefs.SetString(PrefKeyEngine, _engineType);
        }

        // Language Code
        string newLang = EditorGUILayout.TextField("Language Code", _languageCode);
        if (newLang != _languageCode)
        {
            _languageCode = SanitizeKey(newLang);
            EditorPrefs.SetString("VoicemakerLanguageCode", _languageCode);
        }

        // Voice ID Input
        string newVoice = EditorGUILayout.TextField("Voice ID", _voiceId);
        if (newVoice != _voiceId)
        {
            _voiceId = SanitizeKey(newVoice);
            EditorPrefs.SetString(PrefKeyVoice, _voiceId);
        }

        GUILayout.Space(2);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Preset Voice IDs:", EditorStyles.miniLabel, GUILayout.Width(110));
        if (GUILayout.Button("ai3-Jony (Neural)", EditorStyles.miniButton, GUILayout.Width(110)))
        {
            _voiceId = "ai3-Jony"; _engineType = "neural"; SaveSettings();
        }
        if (GUILayout.Button("ai3-Jenny (Neural)", EditorStyles.miniButton, GUILayout.Width(110)))
        {
            _voiceId = "ai3-Jenny"; _engineType = "neural"; SaveSettings();
        }
        if (GUILayout.Button("ai1-Joanna (Standard)", EditorStyles.miniButton, GUILayout.Width(130)))
        {
            _voiceId = "ai1-Joanna"; _engineType = "standard"; SaveSettings();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        if (GUILayout.Button("Find Available Voice IDs (Fetch & Format)", GUILayout.Height(22)))
        {
            FetchAvailableVoices();
        }

        if (_fetchedVoices != null && _fetchedVoices.Count > 0 && _fetchedVoiceDisplayNames.Length > 0)
        {
            GUILayout.Space(4);
            int newIdx = EditorGUILayout.Popup("Quick Voice Select", _selectedVoiceIndex, _fetchedVoiceDisplayNames);
            if (newIdx != _selectedVoiceIndex && newIdx >= 0 && newIdx < _fetchedVoices.Count)
            {
                _selectedVoiceIndex = newIdx;
                var v = _fetchedVoices[newIdx];
                _voiceId = v.VoiceId;
                if (v.Engine == "neural" || v.Engine == "standard") _engineType = v.Engine;
                if (!string.IsNullOrEmpty(v.LanguageCode)) _languageCode = v.LanguageCode;
                SaveSettings();
            }
        }

        GUILayout.Space(6);

        // Output format selection
        int newFormatIdx = EditorGUILayout.Popup("Output Format", _outputFormatIndex, _outputFormatDisplayNames);
        if (newFormatIdx != _outputFormatIndex)
        {
            _outputFormatIndex = newFormatIdx;
            EditorPrefs.SetInt("VoicemakerOutputFormatIndex", _outputFormatIndex);
        }

        EndCard();

        GUILayout.Space(4);

        // ── Card 2: Execution Modes ─────────────────────────────────────────────
        BeginCard("Generation Modes", "⚙️");

        DrawTabs();

        bool hasValidKey = !string.IsNullOrEmpty(_apiKey);

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

            bool canRunBatch = !_running && hasValidKey && _droppedModel != null;

            Color btnColor = canRunBatch ? new Color(0.55f, 0.35f, 0.85f) : new Color(0.35f, 0.35f, 0.35f);
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
            if (GUILayout.Button("Generate Voiceover Audio via Voicemaker (Batch)", btnStyle))
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
            else if (!hasValidKey)
                EditorGUILayout.HelpBox("Please configure your Voicemaker.in API Key.", MessageType.Warning);
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

            bool canRunAssembly = !_running && hasValidKey && config != null && config.assemblySteps != null && config.assemblySteps.Length > 0;

            Color btnColor = canRunAssembly ? new Color(0.55f, 0.35f, 0.85f) : new Color(0.35f, 0.35f, 0.35f);
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
            if (GUILayout.Button("Generate Assembly Step Audio via Voicemaker (Batch)", btnStyle))
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

            bool canRunSingle = !_running && hasValidKey && !string.IsNullOrEmpty(_singleTextDescription) && !string.IsNullOrEmpty(_singleAudioFilename);

            Color btnColor = canRunSingle ? new Color(0.55f, 0.35f, 0.85f) : new Color(0.35f, 0.35f, 0.35f);
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
            if (GUILayout.Button("Generate Single Audio Clip via Voicemaker", btnStyle))
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

    // ── Batch Generation logic for Parts ──────────────────────────────────────

    async void RunGeneration()
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _status = "ERROR: API Key is empty. Please enter your Voicemaker.in API key.";
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

        _status = $"Found {parts.Count} parts. Starting Voicemaker generation...";
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
                Debug.LogWarning($"[Voicemaker] Skipping '{pd.partName}' - Description is empty or placeholder.");
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
                Debug.Log($"[Voicemaker] Reused shared audio clip for '{pd.partName}' — API call skipped!");
                continue;
            }

            EditorUtility.DisplayProgressBar("Generating Audio via Voicemaker.in", $"Processing {pd.partName} ({i + 1}/{parts.Count})...", (float)i / parts.Count);
            _status = $"Generating audio for '{pd.partName}' via Voicemaker...\nText: {pd.description}";
            Repaint();

            byte[] audioData = await CallVoicemakerAPI(pd.description, _cts.Token);
            if (audioData != null && audioData.Length > 0)
            {
                string safeName = pd.partName.Replace(":", "_").Replace("/", "_").Replace("\\", "_");
                string ext = _outputFormats[_outputFormatIndex];
                string relativePath = $"{audioFolder}/{safeName}_explanation.{ext}";
                string fullPath = Path.Combine(Application.dataPath, relativePath.Substring(7));

                try
                {
                    File.WriteAllBytes(fullPath, audioData);
                    AssetDatabase.ImportAsset(relativePath);

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
                        Debug.LogError($"[Voicemaker] Failed to load imported AudioClip at {relativePath}");
                        failedCount++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Voicemaker] Error writing file: {ex.Message}");
                    failedCount++;
                }
            }
            else
            {
                failedCount++;
            }

            try
            {
                await Task.Delay(300, _cts.Token);
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
        if (string.IsNullOrEmpty(_apiKey))
        {
            _status = "ERROR: API Key is empty. Please enter your Voicemaker.in API key.";
            Repaint();
            return;
        }

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

        _status = $"Parts folder found:\n{partsFolder}\n\nStarting assembly steps audio generation via Voicemaker...";
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
                Debug.Log($"[Voicemaker] Reused audio clip for Step {i + 1} '{step.stepName}' — API call skipped!");
                continue;
            }

            EditorUtility.DisplayProgressBar("Generating Assembly Step Audio via Voicemaker.in", $"Processing Step {i + 1}/{total}: {step.stepName}...", (float)i / total);
            _status = $"Generating audio for Step {i + 1}/{total}...\nText: {textToGenerate}";
            Repaint();

            byte[] audioData = await CallVoicemakerAPI(textToGenerate, _cts.Token);
            if (audioData != null && audioData.Length > 0)
            {
                string safeName = string.IsNullOrEmpty(step.stepName) ? $"step_{i + 1}" : step.stepName;
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    safeName = safeName.Replace(c, '_');
                }
                safeName = safeName.Replace(":", "_").Replace("/", "_").Replace("\\", "_");

                string ext = _outputFormats[_outputFormatIndex];
                string relativePath = $"{audioFolder}/Step_{i + 1}_{safeName}.{ext}";
                string fullPath = Path.Combine(Application.dataPath, relativePath.Substring(7));

                try
                {
                    File.WriteAllBytes(fullPath, audioData);
                    AssetDatabase.ImportAsset(relativePath);

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
                        Debug.LogError($"[Voicemaker] Failed to load imported AudioClip at {relativePath}");
                        failedCount++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Voicemaker] Error writing file: {ex.Message}");
                    failedCount++;
                }
            }
            else
            {
                failedCount++;
            }

            try
            {
                await Task.Delay(300, _cts.Token);
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
        if (string.IsNullOrEmpty(_apiKey))
        {
            _status = "ERROR: API Key is empty. Please enter your Voicemaker.in API key.";
            Repaint();
            return;
        }

        _cts = new CancellationTokenSource();
        _running = true;
        _status = $"Calling Voicemaker.in API for single audio: '{_singleAudioFilename}'...";
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

        EditorUtility.DisplayProgressBar("Generating Single Audio via Voicemaker.in", $"Processing {_singleAudioFilename}...", 0.5f);

        byte[] audioData = await CallVoicemakerAPI(_singleTextDescription, _cts.Token);
        EditorUtility.ClearProgressBar();

        if (audioData != null && audioData.Length > 0)
        {
            string ext = _outputFormats[_outputFormatIndex];
            string safeName = _singleAudioFilename.Replace(":", "_").Replace("/", "_").Replace("\\", "_");
            if (!safeName.ToLower().EndsWith("." + ext)) safeName += "." + ext;

            string relativePath = $"{saveFolder}/{safeName}";
            string fullPath;

            if (saveFolder == "Assets")
                fullPath = Path.Combine(Application.dataPath, safeName);
            else if (saveFolder.StartsWith("Assets/"))
                fullPath = Path.Combine(Application.dataPath, saveFolder.Substring(7), safeName);
            else
                fullPath = Path.Combine(saveFolder, safeName);

            try
            {
                File.WriteAllBytes(fullPath, audioData);
                AssetDatabase.ImportAsset(relativePath);

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
            _status = "ERROR: Failed to receive audio data from Voicemaker.in. Check console for details.";
        }

        _running = false;
        Repaint();
    }

    // ── Voicemaker.in API Core ────────────────────────────────────────────────

    [System.Serializable]
    private class VoicemakerResponse
    {
        public bool success;
        public string message;
        public string path; // URL of generated audio file
    }

    [System.Serializable]
    public class VoiceInfo
    {
        public string VoiceId;
        public string VoiceName;
        public string Engine;
        public string LanguageCode;
        public string Gender;

        public string DisplayName => $"{VoiceId} - {VoiceName} ({Engine}, {LanguageCode})";
    }

    private List<VoiceInfo> _fetchedVoices = new List<VoiceInfo>();
    private string[] _fetchedVoiceDisplayNames = new string[0];
    private int _selectedVoiceIndex = -1;

    private async void FetchAvailableVoices()
    {
        string endpoint = "https://developer.voicemaker.in/api/v1/voice/list";
        string sanitizedKey = SanitizeKey(_apiKey);
        if (string.IsNullOrEmpty(sanitizedKey))
        {
            Debug.LogError("[Voicemaker] API Key is empty or null! Please enter your Voicemaker API key first.");
            _status = "ERROR: API Key is empty. Enter your Voicemaker API key to fetch available voices.";
            Repaint();
            return;
        }

        string authHeader = sanitizedKey.StartsWith("Bearer ", System.StringComparison.OrdinalIgnoreCase)
            ? sanitizedKey
            : "Bearer " + sanitizedKey;

        Debug.Log("[Voicemaker] Fetching available voices from Voicemaker API...");
        _status = "Fetching available voices from Voicemaker API...";
        Repaint();

        try
        {
            using var req = new UnityWebRequest(endpoint, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", authHeader);
            req.SetRequestHeader("Content-Type", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Voicemaker] Failed to fetch voices: {req.error}\nResponse: {req.downloadHandler.text}");
                _status = $"ERROR: Failed to fetch voices: {req.error}\nCheck console for details.";
                Repaint();
                return;
            }

            string rawText = req.downloadHandler.text;
            ParseAndPrintVoices(rawText);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Voicemaker] Failed to fetch voices: {ex.Message}");
            _status = $"ERROR: Exception fetching voices: {ex.Message}";
            Repaint();
        }
    }

    private void ParseAndPrintVoices(string jsonText)
    {
        _fetchedVoices.Clear();

        // Extract VoiceId objects using Regex scanning across jsonText
        var matches = System.Text.RegularExpressions.Regex.Matches(jsonText,
            @"\{[^{}]*?""VoiceId""\s*:\s*""(?<id>[^""]+)""[^{}]*?\}",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (matches.Count == 0)
        {
            // Fallback scan: look for any "VoiceId": "..."
            var idMatches = System.Text.RegularExpressions.Regex.Matches(jsonText, @"""VoiceId""\s*:\s*""(?<id>[^""]+)""");
            foreach (System.Text.RegularExpressions.Match m in idMatches)
            {
                string id = m.Groups["id"].Value;
                if (!string.IsNullOrEmpty(id))
                {
                    _fetchedVoices.Add(new VoiceInfo { VoiceId = id, VoiceName = id, Engine = "unknown", LanguageCode = "en-US" });
                }
            }
        }
        else
        {
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                string block = m.Value;
                string id = ExtractJsonField(block, "VoiceId");
                string name = ExtractJsonField(block, "VoiceName");
                string engine = ExtractJsonField(block, "Engine");
                string lang = ExtractJsonField(block, "LanguageCode");
                string gender = ExtractJsonField(block, "Gender");

                if (!string.IsNullOrEmpty(id))
                {
                    _fetchedVoices.Add(new VoiceInfo
                    {
                        VoiceId = id,
                        VoiceName = string.IsNullOrEmpty(name) ? id : name,
                        Engine = string.IsNullOrEmpty(engine) ? "neural" : engine,
                        LanguageCode = string.IsNullOrEmpty(lang) ? "en-US" : lang,
                        Gender = string.IsNullOrEmpty(gender) ? "" : gender
                    });
                }
            }
        }

        if (_fetchedVoices.Count == 0)
        {
            Debug.LogWarning($"[Voicemaker] Response received but could not parse voice items.\nRaw Response:\n{jsonText}");
            _status = "WARNING: Received voice list but could not parse. Raw text printed to Console.";
            Repaint();
            return;
        }

        // Build display names for UI dropdown
        _fetchedVoiceDisplayNames = new string[_fetchedVoices.Count];
        for (int i = 0; i < _fetchedVoices.Count; i++)
            _fetchedVoiceDisplayNames[i] = _fetchedVoices[i].DisplayName;

        _selectedVoiceIndex = 0;

        // Print clean structured table to Unity Console
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("====================================================================================================");
        sb.AppendLine($"                             VOICEMAKER AVAILABLE VOICES (Total: {_fetchedVoices.Count})");
        sb.AppendLine("====================================================================================================");
        sb.AppendLine(string.Format("{0,-22} | {1,-20} | {2,-12} | {3,-12} | {4,-10}", "VOICE ID", "VOICE NAME", "ENGINE", "LANG CODE", "GENDER"));
        sb.AppendLine("----------------------------------------------------------------------------------------------------");

        foreach (var v in _fetchedVoices)
        {
            sb.AppendLine(string.Format("{0,-22} | {1,-20} | {2,-12} | {3,-12} | {4,-10}",
                v.VoiceId, v.VoiceName, v.Engine, v.LanguageCode, v.Gender));
        }

        sb.AppendLine("====================================================================================================");
        Debug.Log(sb.ToString());

        _status = $"✓ Successfully fetched {_fetchedVoices.Count} voices!\n\nA clean structured list has been printed to the Unity Console, and a new 'Quick Voice Select' dropdown is now active above.";
        Repaint();
    }

    private string ExtractJsonField(string jsonBlock, string fieldName)
    {
        var m = System.Text.RegularExpressions.Regex.Match(jsonBlock, $@"""{fieldName}""\s*:\s*""(?<val>[^""]+)""");
        return m.Success ? m.Groups["val"].Value : "";
    }

    private void SaveSettings()
    {
        EditorPrefs.SetString(PrefKeyVoice, _voiceId);
        EditorPrefs.SetString(PrefKeyEngine, _engineType);
        EditorPrefs.SetString("VoicemakerLanguageCode", _languageCode);
        Repaint();
    }

    private async Task<byte[]> CallVoicemakerAPI(string text, CancellationToken token = default)
    {
        // Auto-detect engine from voiceId prefix if needed
        string effectiveEngine = _engineType;
        if (_voiceId.StartsWith("ai1-")) effectiveEngine = "standard";
        else if (_voiceId.StartsWith("ai2-") || _voiceId.StartsWith("ai3-") || _voiceId.StartsWith("pro")) effectiveEngine = "neural";

        return await CallVoicemakerAPIInternal(text, _voiceId, effectiveEngine, _languageCode, true, token);
    }

    private async Task<byte[]> CallVoicemakerAPIInternal(string text, string voiceId, string engineType, string languageCode, bool allowFallback, CancellationToken token = default)
    {
        string sanitizedKey = SanitizeKey(_apiKey);
        string format = _outputFormats[_outputFormatIndex];

        // Format Bearer Token header
        string authHeader = sanitizedKey.StartsWith("Bearer ", System.StringComparison.OrdinalIgnoreCase)
            ? sanitizedKey
            : "Bearer " + sanitizedKey;

        // Escape JSON text
        string escapedText = text.Replace("\\", "\\\\").Replace("\"", "\\\"")
                                 .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

        StringBuilder jsonBuilder = new StringBuilder();
        jsonBuilder.Append("{");
        jsonBuilder.Append($"\"Engine\":\"{engineType}\",");
        jsonBuilder.Append($"\"VoiceId\":\"{voiceId}\",");
        jsonBuilder.Append($"\"LanguageCode\":\"{languageCode}\",");
        jsonBuilder.Append($"\"Text\":\"{escapedText}\",");
        jsonBuilder.Append($"\"OutputFormat\":\"{format}\",");
        jsonBuilder.Append($"\"SampleRate\":\"{_sampleRate}\",");
        jsonBuilder.Append($"\"MasterVolume\":\"{_masterVolume}\",");
        jsonBuilder.Append($"\"MasterSpeed\":\"{_masterSpeed}\",");
        jsonBuilder.Append($"\"MasterPitch\":\"{_masterPitch}\"");
        jsonBuilder.Append("}");

        string jsonBody = jsonBuilder.ToString();

        try
        {
            using var req = new UnityWebRequest(VoicemakerEndpoint, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", authHeader);

            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (token.IsCancellationRequested)
                {
                    req.Abort();
                    return null;
                }
                await Task.Yield();
            }

            string rawResp = req.downloadHandler != null ? req.downloadHandler.text : "";

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Voicemaker] API Request Error for Voice '{voiceId}' ({engineType}): {req.error}\nResponse: {rawResp}");

                if (allowFallback && (rawResp.Contains("not subscribed") || rawResp.Contains("You are not subscribed")))
                {
                    Debug.LogWarning("[Voicemaker] 'You are not subscribed' returned for requested voice. Retrying automatically with Standard engine fallback ('standard', 'ai1-Joanna')...");
                    return await CallVoicemakerAPIInternal(text, "ai1-Joanna", "standard", "en-US", false, token);
                }

                if (rawResp.Contains("You are not subscribed"))
                {
                    _status = "ERROR: Voicemaker returned 'You are not subscribed'.\n\n" +
                              "Troubleshooting:\n" +
                              "1. Check if your Voicemaker account has active API credits.\n" +
                              "2. If your plan is Standard, select 'Standard (AI1 Voices)' as Engine and use Voice ID 'ai1-Joanna' or 'ai1-en-US-Aria'.";
                    Repaint();
                }
                return null;
            }

            VoicemakerResponse resp = JsonUtility.FromJson<VoicemakerResponse>(rawResp);

            if (resp == null || !resp.success || string.IsNullOrEmpty(resp.path))
            {
                string msg = resp != null ? resp.message : "Invalid response JSON";
                Debug.LogWarning($"[Voicemaker] API returned error: {msg}\nRaw: {rawResp}");

                if (allowFallback && msg != null && msg.Contains("not subscribed"))
                {
                    Debug.LogWarning("[Voicemaker] 'You are not subscribed' returned in response. Retrying automatically with Standard engine fallback ('standard', 'ai1-Joanna')...");
                    return await CallVoicemakerAPIInternal(text, "ai1-Joanna", "standard", "en-US", false, token);
                }

                return null;
            }

            // Download audio file bytes from generated path URL
            using var audioReq = UnityWebRequest.Get(resp.path);
            var audioOp = audioReq.SendWebRequest();
            while (!audioOp.isDone)
            {
                if (token.IsCancellationRequested)
                {
                    audioReq.Abort();
                    return null;
                }
                await Task.Yield();
            }

            if (audioReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Voicemaker] Failed to download audio file from URL '{resp.path}': {audioReq.error}");
                return null;
            }

            return audioReq.downloadHandler.data;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Voicemaker] Exception during API call: {ex.Message}");
            return null;
        }
    }

    private string SanitizeKey(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        val = val.Trim();
        StringBuilder sb = new StringBuilder();
        foreach (char c in val)
        {
            if (c >= 32 && c < 127) sb.Append(c);
        }
        return sb.ToString();
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
