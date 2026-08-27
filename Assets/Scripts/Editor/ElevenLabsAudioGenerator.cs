using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

/// <summary>
/// Tools > ElevenLabs Audio Generator
///
/// Usage:
///   1. Paste your ElevenLabs API Key.
///   2. Select your Engine prefab.
///   3. Choose a Voice ID and adjust settings.
///   4. Click Generate to convert PartData descriptions to AudioClips.
/// </summary>
public class ElevenLabsAudioGenerator : EditorWindow
{
    private const string ElevenLabsEndpointBase = "https://api.elevenlabs.io/v1/text-to-speech";
    private const string PrefKeyApi = "ElevenLabsAPIKey";
    private const string PrefKeyVoice = "ElevenLabsVoiceID";
    private const string PrefKeyModel = "ElevenLabsModelID";
    private const string PrefKeyPartsFolder = "ElevenLabsPartsFolderOverride";

    private const string DefaultVoiceId = "FGY2WhTYpPnrIDTdsKH5";
    private const string DefaultModelId = "eleven_multilingual_v2";

    private string _apiKey = "";
    private string _voiceId = DefaultVoiceId;
    private string _modelId = DefaultModelId;
    private float _speed = 1.0f;
    private float _stability = 0.5f;
    private float _similarityBoost = 0.75f;
    private float _styleExaggeration = 0.0f;
    private bool _languageOverride = false;
    private string _languageCode = "en";
    private int _outputFormatIndex = 4; // index in the array of formats
    private bool _speakerBoost = true;
    private bool _showVoiceSettings = false;
    private Vector2 _windowScrollPos;

    private readonly string[] _outputFormats = new string[]
    {
        "mp3_22050_32",
        "mp3_44100_32",
        "mp3_44100_64",
        "mp3_44100_96",
        "mp3_44100_128",
        "mp3_44100_192"
    };

    private readonly string[] _outputFormatDisplayNames = new string[]
    {
        "MP3 22.05 kHz (32kbps)",
        "MP3 44.1 kHz (32kbps)",
        "MP3 44.1 kHz (64kbps)",
        "MP3 44.1 kHz (96kbps)",
        "MP3 44.1 kHz (128kbps)",
        "MP3 44.1 kHz (192kbps)"
    };

    private GameObject _droppedModel;
    private GameObject _lastDroppedModel; // used to detect model change for smart auto-fetch
    private bool _onlyMissing = true;
    private string _status = "";
    private bool _running = false;
    private CancellationTokenSource _cts;
    private Vector2 _scroll;
    private Vector2 _stepsScrollPos;

    private int _tabSelection = 2; // Default to Single Text Mode
    private string _singleTextDescription = "";
    private string _singleAudioFilename = "new_audio_clip";
    private string _singleSavePath = "Assets/ScriptableObjects/Data/Engines";
    private string _partsFolderOverride = "";

    private int _characterCount = -1;
    private int _characterLimit = -1;
    private bool _fetchingUsage = false;

    [MenuItem("Tools/ElevenLabs Audio Generator")]
    public static void Open() => GetWindow<ElevenLabsAudioGenerator>("ElevenLabs TTS Audio");

    void OnEnable()
    {
        _apiKey = EditorPrefs.GetString(PrefKeyApi, "");
        _voiceId = EditorPrefs.GetString(PrefKeyVoice, DefaultVoiceId);
        _modelId = EditorPrefs.GetString(PrefKeyModel, DefaultModelId);
        _partsFolderOverride = EditorPrefs.GetString(PrefKeyPartsFolder, "");

        // Auto-migrate deprecated models off the free tier
        if (_modelId == "eleven_monolingual_v1" || _modelId == "eleven_multilingual_v1")
        {
            _modelId = "eleven_multilingual_v2";
            EditorPrefs.SetString(PrefKeyModel, _modelId);
        }

        _speed = EditorPrefs.GetFloat("ElevenLabsSpeed", 1.0f);
        _stability = EditorPrefs.GetFloat("ElevenLabsStability", 0.5f);
        _similarityBoost = EditorPrefs.GetFloat("ElevenLabsSimilarity", 0.75f);
        _styleExaggeration = EditorPrefs.GetFloat("ElevenLabsStyle", 0.0f);
        _languageOverride = EditorPrefs.GetBool("ElevenLabsLanguageOverride", false);
        _languageCode = EditorPrefs.GetString("ElevenLabsLanguageCode", "en");
        _outputFormatIndex = EditorPrefs.GetInt("ElevenLabsOutputFormatIndex", 4);
        _speakerBoost = EditorPrefs.GetBool("ElevenLabsSpeakerBoost", true);
        _showVoiceSettings = EditorPrefs.GetBool("ElevenLabsShowVoiceSettings", false);

        if (!string.IsNullOrEmpty(_apiKey) && IsValidIdentifier(_apiKey, true))
        {
            FetchSubscriptionUsage();
        }
    }

    // Styles
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
        {
            return tex;
        }
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
        Color activeBg = new Color(0.15f, 0.5f, 0.8f, 1f); // Vibrant blue
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
        Color bgCol = EditorGUIUtility.isProSkin ? new Color(0.14f, 0.16f, 0.19f, 1f) : new Color(0.84f, 0.86f, 0.89f, 1f);
        EditorGUI.DrawRect(headerRect, bgCol);
        
        Rect accentRect = new Rect(headerRect.x, headerRect.y + headerRect.height - 3f, headerRect.width, 3f);
        Color accentCol = new Color(0.15f, 0.5f, 0.8f); // Accent blue
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

        GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 20), "ELEVENLABS TTS AUDIO GENERATOR", titleStyle);
        GUI.Label(new Rect(textRect.x, textRect.y + 18, textRect.width, 16), "Create premium voiceovers for parts & assembly steps", subtitleStyle);

        GUILayout.Space(8);
    }

    private void BeginCard(string title, string icon = "")
    {
        GUILayout.BeginVertical(_cardStyle);
        GUILayout.BeginHorizontal();
        if (!string.IsNullOrEmpty(icon))
        {
            GUILayout.Label(icon, GUILayout.Width(18));
        }
        GUILayout.Label(title, _cardHeaderStyle);
        GUILayout.EndHorizontal();
        DrawDivider(new Color(0.5f, 0.5f, 0.5f, 0.15f));
        GUILayout.Space(6);
    }

    private void EndCard()
    {
        GUILayout.EndVertical();
    }

    private void DrawDivider(Color col, float height = 1f)
    {
        Rect rect = GUILayoutUtility.GetRect(10, height, GUILayout.ExpandWidth(true));
        rect.height = height;
        EditorGUI.DrawRect(rect, col);
    }

    private void DrawSubscriptionUsage()
    {
        if (_characterLimit <= 0) return;

        float ratio = (float)_characterCount / _characterLimit;
        float remainingRatio = 1f - ratio;
        int remaining = _characterLimit - _characterCount;

        Color barColor = new Color(0.2f, 0.65f, 0.3f); // Green
        if (remainingRatio < 0.15f)
            barColor = new Color(0.85f, 0.2f, 0.2f); // Red
        else if (remainingRatio < 0.4f)
            barColor = new Color(0.85f, 0.55f, 0.1f); // Orange

        GUILayout.BeginVertical(_cardStyle);
        GUILayout.Label("Subscription Usage", _cardHeaderStyle);
        GUILayout.Space(2);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Used: {_characterCount:N0} / {_characterLimit:N0} chars", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"{remaining:N0} left ({remainingRatio * 100f:F1}%)", EditorStyles.miniLabel);
        GUILayout.EndHorizontal();
        GUILayout.Space(4);

        Rect rect = GUILayoutUtility.GetRect(10, 14, GUILayout.ExpandWidth(true));
        Color bgCol = EditorGUIUtility.isProSkin ? new Color(0.14f, 0.14f, 0.15f, 1f) : new Color(0.78f, 0.78f, 0.78f, 1f);
        EditorGUI.DrawRect(rect, bgCol);
        
        Rect fillRect = new Rect(rect.x + 1, rect.y + 1, (rect.width - 2) * ratio, rect.height - 2);
        EditorGUI.DrawRect(fillRect, barColor);
        
        GUILayout.EndVertical();
    }

    private void DrawSlider(string label, ref float value, float min, float max, string minLabel, string maxLabel, string defaultVal, string warningMsg = "")
    {
        GUILayout.BeginVertical();
        
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(140));
        GUILayout.FlexibleSpace();
        GUILayout.Label($"{value:F2}", EditorStyles.miniLabel);
        GUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(warningMsg))
        {
            Color originalCol = GUI.color;
            GUI.color = new Color(0.9f, 0.5f, 0.1f);
            GUILayout.Label(warningMsg, EditorStyles.miniLabel);
            GUI.color = originalCol;
        }

        value = GUILayout.HorizontalSlider(value, min, max);

        GUILayout.BeginHorizontal();
        GUILayout.Label(minLabel, EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label(maxLabel, EditorStyles.miniLabel);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.EndVertical();
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
        formattedStatus = formattedStatus.Replace("[ElevenLabs]", "<color=#45a3e5><b>[ElevenLabs]</b></color>");
        formattedStatus = formattedStatus.Replace("ERROR:", "<color=#ff5555><b>ERROR:</b></color>");
        formattedStatus = formattedStatus.Replace("Failed:", "<color=#ff5555><b>Failed:</b></color>");
        formattedStatus = formattedStatus.Replace("failed", "<color=#ff5555><b>failed</b></color>");
        formattedStatus = formattedStatus.Replace("✓ Complete!", "<color=#50fa7b><b>✓ Complete!</b></color>");
        formattedStatus = formattedStatus.Replace("✓ Success!", "<color=#50fa7b><b>✓ Success!</b></color>");
        formattedStatus = formattedStatus.Replace("Generating", "<color=#8be9fd>Generating</color>");
        formattedStatus = formattedStatus.Replace("Calling", "<color=#8be9fd>Calling</color>");

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
        BeginCard("API Credentials", "🔑");
        
        // API Key Input
        GUILayout.BeginHorizontal();
        GUILayout.Label("ElevenLabs API Key", GUILayout.Width(130));
        GUILayout.FlexibleSpace();
        if (_characterLimit > 0)
        {
            int remaining = _characterLimit - _characterCount;
            GUILayout.Label($"Chars: {remaining:N0} remaining", EditorStyles.miniLabel);
        }
        else if (_fetchingUsage)
        {
            GUILayout.Label("Checking subscription...", EditorStyles.miniLabel);
        }
        
        if (GUILayout.Button("↻", GUILayout.Width(20), GUILayout.Height(16)))
        {
            FetchSubscriptionUsage();
        }
        GUILayout.EndHorizontal();

        string newKey = EditorGUILayout.PasswordField(_apiKey);
        if (newKey != _apiKey) 
        { 
            _apiKey = SanitizeHeaderValue(newKey); 
            EditorPrefs.SetString(PrefKeyApi, _apiKey); 
            if (IsValidIdentifier(_apiKey, true))
            {
                FetchSubscriptionUsage();
            }
        }

        bool isApiValid = IsValidIdentifier(_apiKey, true);
        if (!string.IsNullOrEmpty(_apiKey) && !isApiValid)
        {
            EditorGUILayout.HelpBox("API Key contains invalid characters. Alphanumeric, underscores, or hyphens only.", MessageType.Error);
        }

        GUILayout.Space(6);

        // Voice ID Input
        string newVoice = EditorGUILayout.TextField("Voice ID", _voiceId);
        if (newVoice != _voiceId) { _voiceId = SanitizeHeaderValue(newVoice); EditorPrefs.SetString(PrefKeyVoice, _voiceId); }

        bool isVoiceValid = IsValidIdentifier(_voiceId, true);
        if (!string.IsNullOrEmpty(_voiceId) && !isVoiceValid)
        {
            EditorGUILayout.HelpBox("Voice ID contains invalid characters.", MessageType.Warning);
        }

        GUILayout.Space(2);
        if (GUILayout.Button("Find Available Voice IDs (Prints to Console)", GUILayout.Height(20)))
        {
            FetchAvailableVoices();
        }

        GUILayout.Space(6);

        // Model ID Input
        string newModel = EditorGUILayout.TextField("Model ID", _modelId);
        if (newModel != _modelId) { _modelId = SanitizeHeaderValue(newModel); EditorPrefs.SetString(PrefKeyModel, _modelId); }

        bool isModelValid = IsValidIdentifier(_modelId, true);
        if (!string.IsNullOrEmpty(_modelId) && !isModelValid)
        {
            EditorGUILayout.HelpBox("Model ID contains invalid characters.", MessageType.Warning);
        }

        EndCard();
        
        // Render Subscription Usage Bar (if loaded)
        DrawSubscriptionUsage();
        
        // ── Card 2: Voice & Generation Configuration ────────────────────────────
        GUILayout.BeginVertical(_cardStyle);
        GUILayout.BeginHorizontal();
        _showVoiceSettings = EditorGUILayout.Foldout(_showVoiceSettings, "Advanced Voice Configuration", true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
        GUILayout.EndHorizontal();

        if (_showVoiceSettings)
        {
            DrawDivider(new Color(0.5f, 0.5f, 0.5f, 0.2f));
            GUILayout.Space(6);
            
            EditorGUI.BeginChangeCheck();
            
            DrawSlider("Speed (0.7x - 1.2x)", ref _speed, 0.7f, 1.2f, "Slower", "Faster", "1.0");
            DrawSlider("Stability", ref _stability, 0f, 1f, "More variable (expressive)", "More stable (consistent)", "0.5");
            DrawSlider("Similarity Boost", ref _similarityBoost, 0f, 1f, "Low similarity", "High similarity", "0.75");
            DrawSlider("Style Exaggeration", ref _styleExaggeration, 0f, 1f, "None", "Exaggerated", "0.0", _styleExaggeration > 0.5f ? "Caution: Values > 50% can cause speech instability" : "");

            // Language Override
            GUILayout.BeginHorizontal();
            _languageOverride = EditorGUILayout.Toggle("Language Override", _languageOverride, GUILayout.Width(150));
            if (_languageOverride)
            {
                _languageCode = EditorGUILayout.TextField(_languageCode, GUILayout.Width(80));
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            // Output Format
            _outputFormatIndex = EditorGUILayout.Popup("Output Quality", _outputFormatIndex, _outputFormatDisplayNames);
            if (_outputFormatIndex == 5)
            {
                EditorGUILayout.HelpBox("192kbps MP3 requires Starter tier or higher.", MessageType.None);
            }
            GUILayout.Space(6);

            // Speaker Boost
            _speakerBoost = EditorGUILayout.Toggle("Speaker Boost", _speakerBoost);
            GUILayout.Space(8);

            // Reset
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset Settings", GUILayout.Width(110), GUILayout.Height(20)))
            {
                ResetToDefaultVoiceSettings();
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                SaveVoiceSettings();
            }
        }
        GUILayout.EndVertical();
        
        GUILayout.Space(4);
        
        // ── Card 3: Execution Modes ─────────────────────────────────────────────
        BeginCard("Generation Modes", "⚙️");
        
        DrawTabs();
        
        if (_tabSelection == 0)
        {
            // Batch Mode
            _droppedModel = (GameObject)EditorGUILayout.ObjectField(
                "3D Model Prefab / GLB", _droppedModel, typeof(GameObject), true);

            // ── Smart Auto-Fetch: detect model change and resolve Parts folder automatically ──
            if (_droppedModel != _lastDroppedModel)
            {
                _lastDroppedModel = _droppedModel;
                if (_droppedModel != null)
                {
                    string autoFound = FindPartsFolderForModel(_droppedModel);
                    if (!string.IsNullOrEmpty(autoFound))
                    {
                        _partsFolderOverride = autoFound;
                        EditorPrefs.SetString(PrefKeyPartsFolder, _partsFolderOverride);
                        _status = $"[ElevenLabs] Auto-detected Parts folder:\n{autoFound}";
                    }
                    else
                    {
                        _status = "[ElevenLabs] Could not auto-detect Parts folder. Please browse manually.";
                    }
                }
            }

            EditorGUILayout.BeginHorizontal();
            string newFolder = EditorGUILayout.TextField("Parts Folder", _partsFolderOverride);
            if (newFolder != _partsFolderOverride)
            {
                _partsFolderOverride = newFolder;
                EditorPrefs.SetString(PrefKeyPartsFolder, _partsFolderOverride);
            }
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string defaultBrowsePath = Path.Combine(Application.dataPath, "ScriptableObjects/Data/Engines").Replace("\\", "/");
                if (!Directory.Exists(defaultBrowsePath)) defaultBrowsePath = Application.dataPath;
                string path = EditorUtility.OpenFolderPanel("Select Parts Folder containing PartData", defaultBrowsePath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    string absoluteDataPath = Path.GetFullPath(Application.dataPath).Replace("\\", "/");
                    string normalizedPath = path.Replace("\\", "/");
                    if (normalizedPath.StartsWith(absoluteDataPath))
                        _partsFolderOverride = "Assets" + normalizedPath.Substring(absoluteDataPath.Length);
                    else
                        _partsFolderOverride = path;
                    EditorPrefs.SetString(PrefKeyPartsFolder, _partsFolderOverride);
                }
            }
            EditorGUILayout.EndHorizontal();

            _onlyMissing = EditorGUILayout.Toggle("Only Missing Audio", _onlyMissing);

            GUILayout.Space(12);

            bool canRunBatch = !_running && isApiValid && isVoiceValid && isModelValid && _droppedModel != null;
            
            Color btnColor = canRunBatch ? new Color(0.15f, 0.5f, 0.8f) : new Color(0.35f, 0.35f, 0.35f);
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
            if (GUILayout.Button("Generate Voiceover Audio (Batch)", btnStyle))
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
            else if (string.IsNullOrEmpty(_apiKey))
                EditorGUILayout.HelpBox("Please configure your ElevenLabs API Key.", MessageType.Warning);
        }
        else if (_tabSelection == 1)
        {
            // Assembly Steps Mode
            _droppedModel = (GameObject)EditorGUILayout.ObjectField(
                "3D Model Prefab / GLB", _droppedModel, typeof(GameObject), true);

            // ── Smart Auto-Fetch: detect model change and resolve Parts folder automatically ──
            if (_droppedModel != _lastDroppedModel)
            {
                _lastDroppedModel = _droppedModel;
                if (_droppedModel != null)
                {
                    string autoFound = FindPartsFolderForModel(_droppedModel);
                    if (!string.IsNullOrEmpty(autoFound))
                    {
                        _partsFolderOverride = autoFound;
                        EditorPrefs.SetString(PrefKeyPartsFolder, _partsFolderOverride);
                        _status = $"[ElevenLabs] Auto-detected Parts folder:\n{autoFound}";
                    }
                    else
                    {
                        _status = "[ElevenLabs] Could not auto-detect Parts folder. Please browse manually.";
                    }
                }
            }

            EditorGUILayout.BeginHorizontal();
            string newFolder = EditorGUILayout.TextField("Parts Folder", _partsFolderOverride);
            if (newFolder != _partsFolderOverride)
            {
                _partsFolderOverride = newFolder;
                EditorPrefs.SetString(PrefKeyPartsFolder, _partsFolderOverride);
            }
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string defaultBrowsePath = Path.Combine(Application.dataPath, "ScriptableObjects/Data/Engines").Replace("\\", "/");
                if (!Directory.Exists(defaultBrowsePath)) defaultBrowsePath = Application.dataPath;
                string path = EditorUtility.OpenFolderPanel("Select Parts Folder containing PartData", defaultBrowsePath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    string absoluteDataPath = Path.GetFullPath(Application.dataPath).Replace("\\", "/");
                    string normalizedPath = path.Replace("\\", "/");
                    if (normalizedPath.StartsWith(absoluteDataPath))
                        _partsFolderOverride = "Assets" + normalizedPath.Substring(absoluteDataPath.Length);
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
            {
                config = _droppedModel.GetComponentInChildren<EngineAssemblyConfig>(true);
            }

            if (_droppedModel != null && config == null)
            {
                EditorGUILayout.HelpBox("Selected model does not contain an EngineAssemblyConfig component.", MessageType.Error);
            }
            else if (config != null)
            {
                DrawAssemblyStepsList(config);
            }

            GUILayout.Space(12);

            bool canRunAssembly = !_running && isApiValid && isVoiceValid && isModelValid && config != null && config.assemblySteps != null && config.assemblySteps.Length > 0;
            
            Color btnColor = canRunAssembly ? new Color(0.15f, 0.5f, 0.8f) : new Color(0.35f, 0.35f, 0.35f);
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
            if (GUILayout.Button("Generate Assembly Step Audio (Batch)", btnStyle))
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
                        {
                            _singleSavePath = "Assets";
                        }
                        else
                        {
                            _singleSavePath = "Assets" + normalizedChosen.Substring(absoluteAssetsPath.Length);
                        }
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

            bool canRunSingle = !_running && isApiValid && isVoiceValid && isModelValid && !string.IsNullOrEmpty(_singleTextDescription) && !string.IsNullOrEmpty(_singleAudioFilename);
            
            Color btnColor = canRunSingle ? new Color(0.15f, 0.5f, 0.8f) : new Color(0.35f, 0.35f, 0.35f);
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
            if (GUILayout.Button("Generate Single Audio Clip", btnStyle))
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

    async void RunGeneration()
    {
        if (!IsValidIdentifier(_apiKey, true))
        {
            _status = "ERROR: API Key is invalid. Please correct it in the API settings.";
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

        // Auto-create Parts/Audio subfolder
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

        // Cache all EnginePart components on the prefab once for prefab-level assignment
        EnginePart[] prefabEngineParts = _droppedModel.GetComponentsInChildren<EnginePart>(true);
        bool prefabWasDirtied = false;

        _status = $"Found {parts.Count} parts. Starting generation...";
        Repaint();

        int processedCount = 0;
        int reusedCount = 0;
        int skippedCount = 0;
        int failedCount = 0;

        // Smart Deduplication: map text description -> AudioClip
        var audioCache = new Dictionary<string, AudioClip>();

        // Pre-populate cache with any existing clips already assigned
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
                Debug.LogWarning($"[ElevenLabs] Skipping '{pd.partName}' - Description is empty or placeholder.");
                skippedCount++;
                continue;
            }

            string cleanDesc = pd.description.Trim();

            // ── Smart Deduplication Check ──────────────────────────────────────
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
                Debug.Log($"[ElevenLabs] Reused shared audio clip for '{pd.partName}' (identical description) — API call skipped!");
                continue;
            }

            // Show progress bar
            EditorUtility.DisplayProgressBar(
                "Generating Audio via ElevenLabs", 
                $"Processing {pd.partName} ({i + 1}/{parts.Count})...", 
                (float)i / parts.Count
            );

            _status = $"Generating audio for '{pd.partName}'...\nText: {pd.description}";
            Repaint();

            byte[] audioData = await CallElevenLabsAPI(pd.description, _cts.Token);
            if (audioData != null && audioData.Length > 0)
            {
                // Save MP3 file
                string safeName = pd.partName.Replace(":", "_").Replace("/", "_").Replace("\\", "_");
                string relativePath = $"{audioFolder}/{safeName}_explanation.mp3";
                string fullPath = Path.Combine(Application.dataPath, relativePath.Substring(7));

                try
                {
                    File.WriteAllBytes(fullPath, audioData);
                    AssetDatabase.ImportAsset(relativePath);

                    // Load imported audio clip
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(relativePath);
                    if (clip != null)
                    {
                        pd.audioExplanation = clip;
                        EditorUtility.SetDirty(pd);

                        // Cache clip for deduplication across all parts with identical description
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
                        Debug.LogError($"[ElevenLabs] Failed to load imported AudioClip at {relativePath}");
                        failedCount++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ElevenLabs] Error writing file: {ex.Message}");
                    failedCount++;
                }
            }
            else
            {
                failedCount++;
            }

            try
            {
                await Task.Delay(250, _cts.Token);
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

        // Save the prefab asset if any EnginePart components were updated
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
        FetchSubscriptionUsage();
        Repaint();
    }

    private string SanitizeHeaderValue(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";

        // If the value contains a colon (e.g., "xi-api-key: abc123xyz"), extract the actual key after the colon
        if (val.Contains(":"))
        {
            int colonIndex = val.IndexOf(':');
            val = val.Substring(colonIndex + 1);
        }

        StringBuilder sb = new StringBuilder();
        foreach (char c in val)
        {
            if (c > 31 && c < 127 && !char.IsWhiteSpace(c)) // Only printable ASCII, no whitespaces
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private async Task<byte[]> CallElevenLabsAPI(string text, CancellationToken token = default)
    {
        return await CallElevenLabsAPIInternal(text, _voiceId, _modelId, true, token);
    }

    private async Task<byte[]> CallElevenLabsAPIInternal(string text, string voiceId, string modelId, bool allowFallback, CancellationToken token = default)
    {
        string format = _outputFormats[_outputFormatIndex];
        string endpoint = $"{ElevenLabsEndpointBase}/{SanitizeHeaderValue(voiceId)}?output_format={format}";
        
        // Escape JSON body manually to avoid external libraries
        string escapedText = text.Replace("\\", "\\\\").Replace("\"", "\\\"")
                                 .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

        string stabilityStr = _stability.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        string similarityStr = _similarityBoost.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        string styleStr = _styleExaggeration.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        string speedStr = _speed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        string speakerBoostStr = _speakerBoost ? "true" : "false";

        StringBuilder jsonBuilder = new StringBuilder();
        jsonBuilder.Append("{");
        jsonBuilder.Append($"\"text\":\"{escapedText}\",");
        jsonBuilder.Append($"\"model_id\":\"{SanitizeHeaderValue(modelId)}\",");
        
        if (_languageOverride && !string.IsNullOrEmpty(_languageCode))
        {
            jsonBuilder.Append($"\"language_code\":\"{SanitizeHeaderValue(_languageCode)}\",");
        }

        jsonBuilder.Append("\"voice_settings\":{");
        jsonBuilder.Append($"\"stability\":{stabilityStr},");
        jsonBuilder.Append($"\"similarity_boost\":{similarityStr},");
        jsonBuilder.Append($"\"style\":{styleStr},");
        jsonBuilder.Append($"\"use_speaker_boost\":{speakerBoostStr},");
        jsonBuilder.Append($"\"speed\":{speedStr}");
        jsonBuilder.Append("}");
        jsonBuilder.Append("}");

        string jsonBody = jsonBuilder.ToString();

        try
        {
            string sanitizedKey = SanitizeHeaderValue(_apiKey);
            if (string.IsNullOrEmpty(sanitizedKey))
            {
                Debug.LogError("[ElevenLabs Debug] Sanitized API Key is empty or null!");
            }

            using var req = new UnityWebRequest(endpoint, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("xi-api-key", sanitizedKey);
            req.SetRequestHeader("accept", "audio/mpeg");

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

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[ElevenLabs] API Request Error: {req.error}\nResponse: {req.downloadHandler.text}");
                
                if (allowFallback && (voiceId != DefaultVoiceId || modelId != DefaultModelId))
                {
                    Debug.LogWarning($"[ElevenLabs] API call failed with Voice ID: '{voiceId}' or Model ID: '{modelId}'. Retrying with default voice '{DefaultVoiceId}' and model '{DefaultModelId}'...");
                    return await CallElevenLabsAPIInternal(text, DefaultVoiceId, DefaultModelId, false, token);
                }
                return null;
            }

            return req.downloadHandler.data;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ElevenLabs] Exception during API call: {ex.Message}");
            return null;
        }
    }

    private async void FetchAvailableVoices()
    {
        string endpoint = "https://api.elevenlabs.io/v1/voices";
        string sanitizedKey = SanitizeHeaderValue(_apiKey);
        if (string.IsNullOrEmpty(sanitizedKey))
        {
            Debug.LogError("[ElevenLabs] API Key is empty or null!");
            return;
        }

        Debug.Log("[ElevenLabs] Fetching available voices from API...");

        try
        {
            using var req = UnityWebRequest.Get(endpoint);
            req.SetRequestHeader("xi-api-key", sanitizedKey);
            req.SetRequestHeader("accept", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ElevenLabs] Failed to fetch voices: {req.error}\nResponse: {req.downloadHandler.text}");
                return;
            }

            Debug.Log($"[ElevenLabs] Available Voices:\n{req.downloadHandler.text}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ElevenLabs] Failed to fetch voices: {ex.Message}");
        }
    }

    static string FindPartsFolderForModel(GameObject model)
    {
        string modelAssetPath = AssetDatabase.GetAssetPath(model);
        string modelName = model.name;
        string modelNorm = Normalize(modelName);

        // Search all EngineData assets
        string[] edGuids = AssetDatabase.FindAssets("t:EngineData");
        foreach (var guid in edGuids)
        {
            var ed = AssetDatabase.LoadAssetAtPath<EngineData>(AssetDatabase.GUIDToAssetPath(guid));
            if (ed == null) continue;

            bool match = false;

            // Exact prefab reference match
            if (ed.enginePrefab != null)
                match = AssetDatabase.GetAssetPath(ed.enginePrefab) == modelAssetPath;

            // Name-based fallback
            if (!match && ed.engineName != null)
                match = Normalize(ed.engineName) == modelNorm;

            if (match)
            {
                string edDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(ed)).Replace("\\", "/");
                string partsPath = edDir + "/Parts";
                if (Directory.Exists(partsPath)) return partsPath;
            }
        }

        // Fallback: search all Parts folders whose engine folder name matches the model name
        string[] allDirs = Directory.GetDirectories("Assets", "Parts", SearchOption.AllDirectories);
        foreach (var dir in allDirs)
        {
            string unityDir = dir.Replace("\\", "/");
            string engineFolder = Path.GetFileName(Path.GetDirectoryName(unityDir));
            if (Normalize(engineFolder) == modelNorm)
                return unityDir;
        }
        // Last resort: any Parts folder under an engine folder whose name contains the model name
        foreach (var dir in allDirs)
        {
            string unityDir = dir.Replace("\\", "/");
            string engineFolder = Path.GetFileName(Path.GetDirectoryName(unityDir));
            if (Normalize(engineFolder).Contains(modelNorm) || modelNorm.Contains(Normalize(engineFolder)))
                return unityDir;
        }

        return null;
    }

    async void RunAssemblyStepsGeneration()
    {
        if (!IsValidIdentifier(_apiKey, true))
        {
            _status = "ERROR: API Key is invalid. Please correct it in the API settings.";
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
            _status = "ERROR: Could not find a Parts folder for this model.\n\n" +
                       "Make sure you ran Tools → Engine Part Setup on this model first, or manually select the folder above.";
            _running = false; Repaint(); return;
        }

        // Auto-create Parts/Audio subfolder
        string audioFolder = partsFolder + "/Audio";
        if (!Directory.Exists(audioFolder))
        {
            Directory.CreateDirectory(audioFolder);
            AssetDatabase.Refresh();
        }

        _status = $"Parts folder found:\n{partsFolder}\n\nStarting assembly steps audio generation...";
        Repaint();

        var steps = config.assemblySteps;
        int total = steps.Length;
        int processedCount = 0;
        int reusedCount = 0;
        int skippedCount = 0;
        int failedCount = 0;
        bool configWasDirtied = false;

        // Cache for assembly step text deduplication
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
                Debug.LogWarning($"[ElevenLabs] Skipping Step {i + 1} - Name and Description are empty.");
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

            // ── Smart Deduplication Check ──────────────────────────────────────
            if (stepAudioCache.TryGetValue(cleanText, out AudioClip existingClip) && existingClip != null)
            {
                steps[i].stepAudio = existingClip;
                configWasDirtied = true;
                reusedCount++;
                Debug.Log($"[ElevenLabs] Reused audio clip for Step {i + 1} '{step.stepName}' (identical text) — API call skipped!");
                continue;
            }

            EditorUtility.DisplayProgressBar(
                "Generating Assembly Step Audio via ElevenLabs", 
                $"Processing Step {i + 1}/{total}: {step.stepName}...", 
                (float)i / total
            );

            _status = $"Generating audio for Step {i + 1}/{total}...\nText: {textToGenerate}";
            Repaint();

            byte[] audioData = await CallElevenLabsAPI(textToGenerate, _cts.Token);
            if (audioData != null && audioData.Length > 0)
            {
                string safeName = string.IsNullOrEmpty(step.stepName) ? $"step_{i + 1}" : step.stepName;
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    safeName = safeName.Replace(c, '_');
                }
                safeName = safeName.Replace(":", "_").Replace("/", "_").Replace("\\", "_");

                string relativePath = $"{audioFolder}/Step_{i + 1}_{safeName}.mp3";
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
                        Debug.LogError($"[ElevenLabs] Failed to load imported AudioClip at {relativePath}");
                        failedCount++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ElevenLabs] Error writing file: {ex.Message}");
                    failedCount++;
                }
            }
            else
            {
                failedCount++;
            }

            try
            {
                await Task.Delay(250, _cts.Token);
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
        FetchSubscriptionUsage();
        Repaint();
    }

    async void RunSingleGeneration()
    {
        if (!IsValidIdentifier(_apiKey, true))
        {
            _status = "ERROR: API Key is invalid. Please correct it in the API settings.";
            Repaint();
            return;
        }

        _cts = new CancellationTokenSource();
        _running = true;
        _status = $"Calling ElevenLabs API for single audio: '{_singleAudioFilename}'...";
        Repaint();

        // Auto-create Save folder
        string saveFolder = _singleSavePath.Replace("\\", "/");
        if (saveFolder.EndsWith("/")) saveFolder = saveFolder.Substring(0, saveFolder.Length - 1);
        
        string systemFolder;
        if (saveFolder == "Assets")
        {
            systemFolder = Application.dataPath;
        }
        else if (saveFolder.StartsWith("Assets/"))
        {
            systemFolder = Path.Combine(Application.dataPath, saveFolder.Substring(7));
        }
        else
        {
            systemFolder = saveFolder;
        }

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

        EditorUtility.DisplayProgressBar(
            "Generating Single Audio via ElevenLabs", 
            $"Processing {_singleAudioFilename}...", 
            0.5f
        );

        byte[] audioData = await CallElevenLabsAPI(_singleTextDescription, _cts.Token);
        EditorUtility.ClearProgressBar();

        if (audioData != null && audioData.Length > 0)
        {
            string safeName = _singleAudioFilename.Replace(":", "_").Replace("/", "_").Replace("\\", "_");
            if (!safeName.ToLower().EndsWith(".mp3")) safeName += ".mp3";

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

                // Load imported audio clip
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
            _status = "ERROR: Failed to receive audio data from ElevenLabs. Check console for API error logs.";
        }

        _running = false;
        FetchSubscriptionUsage();
        Repaint();
    }

    static string Normalize(string s) =>
        s.Replace(" ", "").Replace("_", "").Replace("-", "").ToLower();

    private bool IsValidIdentifier(string val, bool allowUnderscoreAndHyphen)
    {
        if (string.IsNullOrEmpty(val)) return false;
        foreach (char c in val)
        {
            if (char.IsLetterOrDigit(c)) continue;
            if (allowUnderscoreAndHyphen && (c == '_' || c == '-')) continue;
            return false;
        }
        return true;
    }

    private void ResetToDefaultVoiceSettings()
    {
        _speed = 1.0f;
        _stability = 0.5f;
        _similarityBoost = 0.75f;
        _styleExaggeration = 0.0f;
        _languageOverride = false;
        _languageCode = "en";
        _outputFormatIndex = 4; // mp3_44100_128
        _speakerBoost = true;

        SaveVoiceSettings();
    }

    private void SaveVoiceSettings()
    {
        EditorPrefs.SetFloat("ElevenLabsSpeed", _speed);
        EditorPrefs.SetFloat("ElevenLabsStability", _stability);
        EditorPrefs.SetFloat("ElevenLabsSimilarity", _similarityBoost);
        EditorPrefs.SetFloat("ElevenLabsStyle", _styleExaggeration);
        EditorPrefs.SetBool("ElevenLabsLanguageOverride", _languageOverride);
        EditorPrefs.SetString("ElevenLabsLanguageCode", _languageCode);
        EditorPrefs.SetInt("ElevenLabsOutputFormatIndex", _outputFormatIndex);
        EditorPrefs.SetBool("ElevenLabsSpeakerBoost", _speakerBoost);
    }

    [System.Serializable]
    private class SubscriptionResponse
    {
        public int character_count;
        public int character_limit;
    }

    private async void FetchSubscriptionUsage()
    {
        if (_fetchingUsage) return;

        string sanitizedKey = SanitizeHeaderValue(_apiKey);
        if (string.IsNullOrEmpty(sanitizedKey) || !IsValidIdentifier(sanitizedKey, true))
        {
            _characterCount = -1;
            _characterLimit = -1;
            return;
        }

        _fetchingUsage = true;
        Repaint();

        string endpoint = "https://api.elevenlabs.io/v1/user/subscription";
        try
        {
            using var req = UnityWebRequest.Get(endpoint);
            req.SetRequestHeader("xi-api-key", sanitizedKey);
            req.SetRequestHeader("accept", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                SubscriptionResponse data = JsonUtility.FromJson<SubscriptionResponse>(req.downloadHandler.text);
                _characterCount = data.character_count;
                _characterLimit = data.character_limit;
            }
            else
            {
                Debug.LogWarning($"[ElevenLabs] Failed to fetch subscription usage: {req.error}");
                _characterCount = -1;
                _characterLimit = -1;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ElevenLabs] Exception fetching usage: {ex.Message}");
            _characterCount = -1;
            _characterLimit = -1;
        }
        finally
        {
            _fetchingUsage = false;
            Repaint();
        }
    }
}
