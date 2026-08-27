using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Tools > Engine List Generator
///
/// Automatically discovers all engine models, generates sequential indexed lists,
/// syncs with EngineRegistry, and exports formatted lists (Markdown/Text/Clipboard).
/// </summary>
public class EngineListGenerator : EditorWindow
{
    private const string DefaultRegistryPath = "Assets/ScriptableObjects/Data/EngineRegistry.asset";
    private const string PrefKeySortMode = "EngineListGenerator_SortMode";
    private const string PrefKeyExportFormat = "EngineListGenerator_ExportFormat";

    public enum SortMode
    {
        Alphabetical,
        RegistryOrder,
        ByCategory,
        ByFolderPath
    }

    public enum ExportFormat
    {
        MarkdownTable,
        NumberedList,
        SimpleNames,
        JSON
    }

    [System.Serializable]
    public class EngineItem
    {
        public int index;
        public EngineData data;
        public string assetPath;
        public string folderName;
        public bool isRegistered;
        public int partCount;
        public bool hasPrefab;
        public bool hasThumbnail;
    }

    private EngineRegistry _registry;
    private SortMode _sortMode = SortMode.Alphabetical;
    private ExportFormat _exportFormat = ExportFormat.NumberedList;
    private List<EngineItem> _enginesList = new List<EngineItem>();
    private string _searchFilter = "";
    private Vector2 _scrollPos;
    private string _statusMessage = "";
    private bool _includeUnregistered = true;

    // Styles
    private GUIStyle _cardStyle;
    private GUIStyle _cardHeaderStyle;
    private GUIStyle _badgeStyle;
    private GUIStyle _indexBadgeStyle;
    private bool _stylesInitialized = false;

    [MenuItem("Tools/Engine List Generator")]
    public static void Open() => GetWindow<EngineListGenerator>("Engine List Generator");

    void OnEnable()
    {
        _sortMode = (SortMode)EditorPrefs.GetInt(PrefKeySortMode, (int)SortMode.Alphabetical);
        _exportFormat = (ExportFormat)EditorPrefs.GetInt(PrefKeyExportFormat, (int)ExportFormat.NumberedList);

        if (_registry == null)
        {
            _registry = AssetDatabase.LoadAssetAtPath<EngineRegistry>(DefaultRegistryPath);
        }

        RefreshEngineList();
    }

    private void InitializeStyles()
    {
        if (_stylesInitialized) return;

        Color cardBg = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 1f) : new Color(0.94f, 0.94f, 0.94f, 1f);
        _cardStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 10, 10),
            margin = new RectOffset(4, 4, 4, 4)
        };
        _cardStyle.normal.background = MakeTex(2, 2, cardBg);

        _cardHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            margin = new RectOffset(0, 0, 0, 4)
        };

        _badgeStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(4, 4, 1, 1)
        };

        _indexBadgeStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(6, 6, 2, 2)
        };
        _indexBadgeStyle.normal.background = MakeTex(2, 2, new Color(0.15f, 0.5f, 0.8f, 1f));
        _indexBadgeStyle.normal.textColor = Color.white;

        _stylesInitialized = true;
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    void OnGUI()
    {
        InitializeStyles();

        DrawHeader();

        // ── Controls Card ──────────────────────────────────────────────────────
        GUILayout.BeginVertical(_cardStyle);
        
        // Registry Field
        GUILayout.BeginHorizontal();
        _registry = (EngineRegistry)EditorGUILayout.ObjectField("Engine Registry", _registry, typeof(EngineRegistry), false);
        if (GUILayout.Button("↻ Refresh", GUILayout.Width(80)))
        {
            RefreshEngineList();
        }
        GUILayout.EndHorizontal();

        // Settings & Options
        GUILayout.BeginHorizontal();
        
        EditorGUI.BeginChangeCheck();
        _sortMode = (SortMode)EditorGUILayout.EnumPopup("Sort Order", _sortMode);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetInt(PrefKeySortMode, (int)_sortMode);
            RefreshEngineList();
        }

        EditorGUI.BeginChangeCheck();
        _includeUnregistered = EditorGUILayout.ToggleLeft("Include Unregistered", _includeUnregistered, GUILayout.Width(150));
        if (EditorGUI.EndChangeCheck())
        {
            RefreshEngineList();
        }

        GUILayout.EndHorizontal();

        // Search Filter
        GUILayout.BeginHorizontal();
        GUILayout.Label("Filter:", GUILayout.Width(45));
        _searchFilter = EditorGUILayout.TextField(_searchFilter);
        if (GUILayout.Button("Clear", GUILayout.Width(50)))
        {
            _searchFilter = "";
            GUI.FocusControl(null);
        }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUILayout.Space(4);

        // ── Quick Actions Card ─────────────────────────────────────────────────
        GUILayout.BeginVertical(_cardStyle);
        GUILayout.Label("Actions & Export", _cardHeaderStyle);

        GUILayout.BeginHorizontal();
        
        if (GUILayout.Button("📋 Copy Sequential List", GUILayout.Height(28)))
        {
            CopyListToClipboard();
        }

        if (GUILayout.Button("💾 Export ENGINES_LIST.md", GUILayout.Height(28)))
        {
            ExportMarkdownFile();
        }

        if (GUILayout.Button("⚡ Sync & Sort EngineRegistry", GUILayout.Height(28)))
        {
            SyncAndSortRegistry();
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        _exportFormat = (ExportFormat)EditorGUILayout.EnumPopup("Copy/Export Format", _exportFormat);
        if (GUI.changed)
        {
            EditorPrefs.SetInt(PrefKeyExportFormat, (int)_exportFormat);
        }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUILayout.Space(4);

        // ── Status Message ─────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(_statusMessage))
        {
            EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            GUILayout.Space(4);
        }

        // ── Engine List ────────────────────────────────────────────────────────
        var filteredList = GetFilteredList();
        
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Engines Found: {filteredList.Count} (Total on disk: {_enginesList.Count})", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        for (int i = 0; i < filteredList.Count; i++)
        {
            var item = filteredList[i];
            DrawEngineRow(item, i);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        Rect headerRect = GUILayoutUtility.GetRect(10, 48, GUILayout.ExpandWidth(true));
        Color bgCol = EditorGUIUtility.isProSkin ? new Color(0.14f, 0.16f, 0.19f, 1f) : new Color(0.84f, 0.86f, 0.89f, 1f);
        EditorGUI.DrawRect(headerRect, bgCol);

        Rect accentRect = new Rect(headerRect.x, headerRect.y + headerRect.height - 3f, headerRect.width, 3f);
        Color accentCol = new Color(0.15f, 0.5f, 0.8f);
        EditorGUI.DrawRect(accentRect, accentCol);

        Rect textRect = new Rect(headerRect.x + 12, headerRect.y + 8, headerRect.width - 24, 34);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
        titleStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.1f, 0.1f, 0.1f);

        GUIStyle subtitleStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
        subtitleStyle.normal.textColor = Color.gray;

        GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 18), "ENGINE LIST GENERATOR (SEQUENTIAL & INDEXED)", titleStyle);
        GUI.Label(new Rect(textRect.x, textRect.y + 18, textRect.width, 14), "Scans, indexes, sorts, and exports all project engines sequentially", subtitleStyle);

        GUILayout.Space(6);
    }

    private void DrawEngineRow(EngineItem item, int displayIndex)
    {
        Color rowBg = (displayIndex % 2 == 0)
            ? (EditorGUIUtility.isProSkin ? new Color(0.24f, 0.24f, 0.24f, 1f) : new Color(0.92f, 0.92f, 0.92f, 1f))
            : (EditorGUIUtility.isProSkin ? new Color(0.2f, 0.2f, 0.2f, 1f) : new Color(0.88f, 0.88f, 0.88f, 1f));

        Rect rowRect = GUILayoutUtility.GetRect(10, 42, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rowRect, rowBg);

        // Index Badge (#01, #02...)
        Rect indexRect = new Rect(rowRect.x + 6, rowRect.y + 8, 42, 24);
        GUI.Label(indexRect, $"#{displayIndex:D2}", _indexBadgeStyle);

        // Engine Name & Details
        float textX = indexRect.xMax + 10;
        float textWidth = rowRect.width - textX - 160;

        Rect nameRect = new Rect(textX, rowRect.y + 4, textWidth, 18);
        string nameText = item.data != null ? item.data.engineName : item.folderName;
        GUI.Label(nameRect, nameText, EditorStyles.boldLabel);

        Rect detailRect = new Rect(textX, rowRect.y + 22, textWidth, 16);
        string categoryText = item.data != null ? item.data.engineCategory : "Unknown";
        string detailStr = $"Category: {categoryText} | Folder: {item.folderName} | Parts: {item.partCount}";
        GUI.Label(detailRect, detailStr, EditorStyles.miniLabel);

        // Status Badges
        float badgeX = rowRect.x + rowRect.width - 150;
        
        // Registration Badge
        Rect regBadgeRect = new Rect(badgeX, rowRect.y + 12, 70, 18);
        Color regColor = item.isRegistered ? new Color(0.15f, 0.55f, 0.25f) : new Color(0.7f, 0.4f, 0.1f);
        GUIStyle bStyle = new GUIStyle(_badgeStyle);
        bStyle.normal.background = MakeTex(2, 2, regColor);
        bStyle.normal.textColor = Color.white;
        GUI.Label(regBadgeRect, item.isRegistered ? "REGISTERED" : "UNREGIST.", bStyle);

        // Ping Button
        Rect pingBtnRect = new Rect(badgeX + 75, rowRect.y + 10, 65, 22);
        if (GUI.Button(pingBtnRect, "Select"))
        {
            if (item.data != null)
            {
                Selection.activeObject = item.data;
                EditorGUIUtility.PingObject(item.data);
            }
            else
            {
                Object folderObj = AssetDatabase.LoadAssetAtPath<Object>(item.assetPath);
                if (folderObj != null) EditorGUIUtility.PingObject(folderObj);
            }
        }
    }

    private void RefreshEngineList()
    {
        _enginesList.Clear();

        // 1. Scan disk for all EngineData assets
        string[] guids = AssetDatabase.FindAssets("t:EngineData");
        var diskEngineMap = new Dictionary<string, EngineData>();

        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var ed = AssetDatabase.LoadAssetAtPath<EngineData>(path);
            if (ed != null)
            {
                diskEngineMap[path] = ed;
            }
        }

        // Load Registry items map
        var registeredPaths = new HashSet<string>();
        var registeredData = new HashSet<EngineData>();

        if (_registry != null && _registry.engines != null)
        {
            foreach (var ed in _registry.engines)
            {
                if (ed != null)
                {
                    registeredData.Add(ed);
                    string path = AssetDatabase.GetAssetPath(ed);
                    if (!string.IsNullOrEmpty(path)) registeredPaths.Add(path);
                }
            }
        }

        // Populate items
        int counter = 0;

        if (_sortMode == SortMode.RegistryOrder && _registry != null && _registry.engines != null)
        {
            // First add registered engines in registry order
            foreach (var ed in _registry.engines)
            {
                if (ed == null) continue;
                string path = AssetDatabase.GetAssetPath(ed);

                var item = CreateItem(ed, path, true, counter++);
                _enginesList.Add(item);
            }

            // Then add unregistered if enabled
            if (_includeUnregistered)
            {
                foreach (var kvp in diskEngineMap)
                {
                    if (!registeredData.Contains(kvp.Value))
                    {
                        var item = CreateItem(kvp.Value, kvp.Key, false, counter++);
                        _enginesList.Add(item);
                    }
                }
            }
        }
        else
        {
            // Gather all candidate EngineData assets
            List<EngineData> candidates = new List<EngineData>(diskEngineMap.Values);

            // Sort candidates
            if (_sortMode == SortMode.Alphabetical)
            {
                candidates = candidates.OrderBy(e => e.engineName).ThenBy(e => e.name).ToList();
            }
            else if (_sortMode == SortMode.ByCategory)
            {
                candidates = candidates.OrderBy(e => e.engineCategory).ThenBy(e => e.engineName).ToList();
            }
            else if (_sortMode == SortMode.ByFolderPath)
            {
                candidates = candidates.OrderBy(e => AssetDatabase.GetAssetPath(e)).ToList();
            }

            foreach (var ed in candidates)
            {
                string path = AssetDatabase.GetAssetPath(ed);
                bool isReg = registeredData.Contains(ed);

                if (!isReg && !_includeUnregistered) continue;

                var item = CreateItem(ed, path, isReg, counter++);
                _enginesList.Add(item);
            }
        }

        // Re-index sequentially (starting from 0)
        for (int i = 0; i < _enginesList.Count; i++)
        {
            _enginesList[i].index = i;
        }

        _statusMessage = $"Scanned {_enginesList.Count} engines across the project.";
    }

    private EngineItem CreateItem(EngineData ed, string path, bool isRegistered, int index)
    {
        string dirName = Path.GetFileName(Path.GetDirectoryName(path));

        // Count parts in PartManifest if available
        int partCount = 0;
        if (ed.partManifest != null && ed.partManifest.parts != null)
        {
            partCount = ed.partManifest.parts.Count;
        }
        else
        {
            // Count PartData assets in Parts folder
            string partsFolder = Path.GetDirectoryName(path).Replace("\\", "/") + "/Parts";
            if (Directory.Exists(partsFolder))
            {
                partCount = AssetDatabase.FindAssets("t:PartData", new[] { partsFolder }).Length;
            }
        }

        return new EngineItem
        {
            index = index,
            data = ed,
            assetPath = path,
            folderName = dirName,
            isRegistered = isRegistered,
            partCount = partCount,
            hasPrefab = ed.enginePrefab != null,
            hasThumbnail = ed.thumbnail != null
        };
    }

    private List<EngineItem> GetFilteredList()
    {
        if (string.IsNullOrEmpty(_searchFilter)) return _enginesList;

        string filter = _searchFilter.Trim().ToLower();
        return _enginesList.Where(e =>
            (e.data != null && e.data.engineName.ToLower().Contains(filter)) ||
            (e.data != null && e.data.engineCategory.ToLower().Contains(filter)) ||
            e.folderName.ToLower().Contains(filter)
        ).ToList();
    }

    private void SyncAndSortRegistry()
    {
        if (_registry == null)
        {
            _registry = AssetDatabase.LoadAssetAtPath<EngineRegistry>(DefaultRegistryPath);
        }

        if (_registry == null)
        {
            EditorUtility.DisplayDialog("Error", "No EngineRegistry asset found at " + DefaultRegistryPath, "OK");
            return;
        }

        Undo.RecordObject(_registry, "Sync & Sort Engine Registry");

        // Clear and rebuild list sequentially
        _registry.engines.Clear();

        var filtered = GetFilteredList();
        foreach (var item in filtered)
        {
            if (item.data != null && !_registry.engines.Contains(item.data))
            {
                _registry.engines.Add(item.data);
            }
        }

        EditorUtility.SetDirty(_registry);
        AssetDatabase.SaveAssets();

        RefreshEngineList();
        _statusMessage = $"✓ EngineRegistry updated! {_registry.engines.Count} engines saved in sequential order.";
        Debug.Log($"[EngineListGenerator] {_statusMessage}");
    }

    private void CopyListToClipboard()
    {
        string text = BuildFormattedListText();
        EditorGUIUtility.systemCopyBuffer = text;
        _statusMessage = $"✓ Copied {GetFilteredList().Count} engine entries to clipboard!";
        Debug.Log($"[EngineListGenerator] Copied engine list:\n{text}");
    }

    private void ExportMarkdownFile()
    {
        string savePath = "Assets/ScriptableObjects/Data/ENGINES_LIST.md";
        string content = BuildFormattedListText();

        try
        {
            string fullPath = Path.Combine(Application.dataPath, savePath.Substring(7));
            File.WriteAllText(fullPath, content, Encoding.UTF8);
            AssetDatabase.Refresh();

            var assetObj = AssetDatabase.LoadAssetAtPath<Object>(savePath);
            if (assetObj != null) EditorGUIUtility.PingObject(assetObj);

            _statusMessage = $"✓ Exported engine list to {savePath}";
            Debug.Log($"[EngineListGenerator] List exported to {savePath}");
        }
        catch (System.Exception ex)
        {
            _statusMessage = $"ERROR exporting file: {ex.Message}";
            Debug.LogError($"[EngineListGenerator] {ex.Message}");
        }
    }

    private string BuildFormattedListText()
    {
        var items = GetFilteredList();
        StringBuilder sb = new StringBuilder();

        if (_exportFormat == ExportFormat.NumberedList)
        {
            sb.AppendLine($"# Engine VR Simulation - Engine List ({items.Count} Engines)");
            sb.AppendLine($"Generated on: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string name = item.data != null ? item.data.engineName : item.folderName;
                string cat = item.data != null ? item.data.engineCategory : "General";
                sb.AppendLine($"[{i}] {name} (Category: {cat}, Parts: {item.partCount})");
            }
        }
        else if (_exportFormat == ExportFormat.MarkdownTable)
        {
            sb.AppendLine("# Engine VR - Sequential Engine Registry\n");
            sb.AppendLine("| Index | Engine Name | Category | Folder | Parts | Prefab | Registered |");
            sb.AppendLine("|-------|-------------|----------|--------|-------|--------|------------|");

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string name = item.data != null ? item.data.engineName : item.folderName;
                string cat = item.data != null ? item.data.engineCategory : "General";
                string hasPrefab = item.hasPrefab ? "Yes" : "No";
                string isReg = item.isRegistered ? "Yes" : "No";

                sb.AppendLine($"| {i:D2} | **{name}** | {cat} | `{item.folderName}` | {item.partCount} | {hasPrefab} | {isReg} |");
            }
        }
        else if (_exportFormat == ExportFormat.SimpleNames)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string name = item.data != null ? item.data.engineName : item.folderName;
                sb.AppendLine($"[{i}] {name}");
            }
        }
        else if (_exportFormat == ExportFormat.JSON)
        {
            sb.AppendLine("[");
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string name = item.data != null ? item.data.engineName : item.folderName;
                string cat = item.data != null ? item.data.engineCategory : "General";
                
                sb.AppendLine("  {");
                sb.AppendLine($"    \"index\": {i},");
                sb.AppendLine($"    \"name\": \"{name}\",");
                sb.AppendLine($"    \"category\": \"{cat}\",");
                sb.AppendLine($"    \"folder\": \"{item.folderName}\",");
                sb.AppendLine($"    \"partCount\": {item.partCount},");
                sb.AppendLine($"    \"isRegistered\": {item.isRegistered.ToString().ToLower()}");
                sb.Append("  }");
                if (i < items.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("]");
        }

        return sb.ToString();
    }
}
