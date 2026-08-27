using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scans all scenes and prefabs to find which 3D model assets (GLB, FBX, OBJ)
/// are never referenced anywhere in the project.
/// 
/// Usage: Top menu → Tools → Find Unused 3D Models
/// Results are printed to Console and saved to Assets/UnusedAssets.txt
/// </summary>
public class FindUnusedAssets : EditorWindow
{
    private static readonly string[] ModelExtensions = { ".glb", ".fbx", ".obj", ".gltf" };
    private static readonly string[] LargeExtensions = { ".glb", ".fbx", ".obj", ".gltf", ".mp4", ".mp3", ".wav", ".png", ".jpg", ".exr", ".psd" };
    private const string RecycleBinRoot = "ProjectSettings/UnusedAssetsRecycleBin";
    private const string RecycleBinManifest = "ProjectSettings/UnusedAssetsRecycleBin/manifest.txt";

    private Vector2 _scroll;
    private List<string> _unusedPaths = new();
    private List<string> _unusedLargePaths = new();
    private readonly List<DeletedEntry> _deletedEntries = new();
    private bool _scanned = false;
    private bool _showLarge = false;

    [MenuItem("Tools/Find Unused 3D Models")]
    public static void ShowWindow()
    {
        GetWindow<FindUnusedAssets>("Unused Assets");
    }

    void OnEnable()
    {
        LoadDeletedManifest();
    }

    void OnGUI()
    {
        GUILayout.Label("Find Unused Assets", EditorStyles.boldLabel);
        GUILayout.Space(5);

        _showLarge = GUILayout.Toggle(_showLarge, "Include all large files (audio, textures, video)");
        GUILayout.Space(5);

        if (GUILayout.Button("Scan Project", GUILayout.Height(30)))
            Scan(_showLarge ? LargeExtensions : ModelExtensions);

        if (_scanned)
        {
            var list = _showLarge ? _unusedLargePaths : _unusedPaths;
            GUILayout.Space(5);

            long totalBytes = list.Sum(p =>
            {
                var fi = new FileInfo(p);
                return fi.Exists ? fi.Length : 0;
            });

            GUILayout.Label($"Found {list.Count} unused files  ({totalBytes / 1024 / 1024} MB that can be removed)",
                EditorStyles.boldLabel);

            GUILayout.Space(5);

            if (GUILayout.Button("Save list to Assets/UnusedAssets.txt"))
                SaveToFile(list);

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUI.enabled = list.Count > 0;
            if (GUILayout.Button("Delete All Listed (Move to Recycle Bin)", GUILayout.Height(24)))
                DeleteAllListed(list);
            GUI.enabled = true;

            GUI.enabled = _deletedEntries.Count > 0;
            if (GUILayout.Button("Restore Last Deleted", GUILayout.Height(24)))
                RestoreLastDeleted();
            if (GUILayout.Button("Restore All From Recycle Bin", GUILayout.Height(24)))
                RestoreAllDeleted();
            if (GUILayout.Button("Empty Recycle Bin", GUILayout.Height(24)))
                EmptyRecycleBin();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (_deletedEntries.Count > 0)
                GUILayout.Label($"Recycle Bin items: {_deletedEntries.Count}", EditorStyles.miniLabel);

            GUILayout.Space(5);
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var path in list)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(path, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Ping", GUILayout.Width(45)))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    EditorGUIUtility.PingObject(obj);
                }
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    DeleteToRecycleBin(path);
                    GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }
    }

    void Scan(string[] extensions)
    {
        _unusedPaths.Clear();
        _unusedLargePaths.Clear();

        // ── Step 1: Collect all asset paths with target extensions ────────────
        string[] allAssets = AssetDatabase.GetAllAssetPaths();
        var candidates = allAssets
            .Where(p => p.StartsWith("Assets/") &&
                        extensions.Contains(Path.GetExtension(p).ToLower()))
            .ToList();

        Debug.Log($"[FindUnusedAssets] Scanning {candidates.Count} candidate files...");

        // ── Step 2: Collect all GUIDs referenced by scenes + prefabs ─────────
        var referencedGuids = new HashSet<string>();

        // All scenes in build settings + all scenes in project
        var scenePaths = allAssets.Where(p => p.EndsWith(".unity")).ToList();
        // All prefabs
        var prefabPaths = allAssets.Where(p => p.EndsWith(".prefab")).ToList();
        // All scriptable objects
        var soPaths = allAssets.Where(p => p.EndsWith(".asset")).ToList();

        var filesToSearch = scenePaths
            .Concat(prefabPaths)
            .Concat(soPaths)
            .ToList();

        int total = filesToSearch.Count;
        int done  = 0;

        foreach (var filePath in filesToSearch)
        {
            done++;
            if (done % 50 == 0)
                EditorUtility.DisplayProgressBar("Scanning...", filePath, (float)done / total);

            // Read raw file text and extract GUIDs
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
            if (!File.Exists(fullPath)) continue;

            string content;
            try { content = File.ReadAllText(fullPath); }
            catch { continue; }

            // GUIDs appear as: guid: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
            int idx = 0;
            while ((idx = content.IndexOf("guid: ", idx)) != -1)
            {
                idx += 6;
                if (idx + 32 <= content.Length)
                {
                    string guid = content.Substring(idx, 32);
                    if (guid.All(c => "0123456789abcdefABCDEF".Contains(c)))
                        referencedGuids.Add(guid);
                }
            }
        }

        EditorUtility.ClearProgressBar();

        // ── Step 3: Check which candidates are NOT referenced ─────────────────
        var result = new List<string>();
        foreach (var assetPath in candidates)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!referencedGuids.Contains(guid))
                result.Add(assetPath);
        }

        result.Sort();

        if (_showLarge)
            _unusedLargePaths = result;
        else
            _unusedPaths = result;

        _scanned = true;

        long totalMB = result.Sum(p =>
        {
            var fi = new FileInfo(p);
            return fi.Exists ? fi.Length : 0L;
        }) / 1024 / 1024;

        Debug.Log($"[FindUnusedAssets] Done. {result.Count} unused files found ({totalMB} MB).");
    }

    void SaveToFile(List<string> list)
    {
        string output = string.Join("\n", list);
        File.WriteAllText("Assets/UnusedAssets.txt", output);
        AssetDatabase.Refresh();
        Debug.Log("[FindUnusedAssets] Saved to Assets/UnusedAssets.txt");
    }

    void DeleteAllListed(List<string> list)
    {
        if (!EditorUtility.DisplayDialog(
                "Delete all listed assets?",
                $"This will move {list.Count} assets (and their .meta files) to:\n{RecycleBinRoot}\n\nYou can restore them later.",
                "Move to Recycle Bin",
                "Cancel"))
            return;

        int moved = 0;
        // Iterate over a copy, because original list is modified after each delete.
        foreach (var path in list.ToList())
        {
            if (DeleteToRecycleBin(path, false))
                moved++;
        }

        AssetDatabase.Refresh();
        SaveDeletedManifest();
        Debug.Log($"[FindUnusedAssets] Moved {moved} assets to recycle bin.");
    }

    bool DeleteToRecycleBin(string assetPath, bool refreshAfter = true)
    {
        string projectRoot = Directory.GetCurrentDirectory().Replace("\\", "/");
        string sourceAbs = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        if (!File.Exists(sourceAbs))
            return false;

        Directory.CreateDirectory(RecycleBinRoot);

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        string bucket = string.IsNullOrEmpty(guid) ? "noguid" : guid;
        string relNoAssets = assetPath.StartsWith("Assets/") ? assetPath.Substring("Assets/".Length) : assetPath;
        string recycleFolder = Path.Combine(RecycleBinRoot, $"{stamp}_{bucket}");

        string destAssetAbs = Path.Combine(recycleFolder, relNoAssets);
        string sourceMetaAbs = sourceAbs + ".meta";
        string destMetaAbs = destAssetAbs + ".meta";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destAssetAbs) ?? recycleFolder);
            File.Move(sourceAbs, destAssetAbs);
            if (File.Exists(sourceMetaAbs))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destMetaAbs) ?? recycleFolder);
                File.Move(sourceMetaAbs, destMetaAbs);
            }

            _deletedEntries.Add(new DeletedEntry
            {
                originalAssetPath = assetPath,
                recycleAssetPath = destAssetAbs.Replace("\\", "/"),
                deletedAtIso = DateTime.Now.ToString("O")
            });
            SaveDeletedManifest();

            _unusedPaths.Remove(assetPath);
            _unusedLargePaths.Remove(assetPath);

            if (refreshAfter)
                AssetDatabase.Refresh();

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FindUnusedAssets] Failed to move '{assetPath}' to recycle bin: {e.Message}");
            return false;
        }
    }

    void RestoreLastDeleted()
    {
        if (_deletedEntries.Count == 0)
            return;

        var entry = _deletedEntries[_deletedEntries.Count - 1];
        if (RestoreEntry(entry))
        {
            _deletedEntries.RemoveAt(_deletedEntries.Count - 1);
            SaveDeletedManifest();
            AssetDatabase.Refresh();
        }
    }

    void RestoreAllDeleted()
    {
        if (_deletedEntries.Count == 0)
            return;

        int restored = 0;
        // Restore newest first.
        for (int i = _deletedEntries.Count - 1; i >= 0; i--)
        {
            if (RestoreEntry(_deletedEntries[i]))
            {
                _deletedEntries.RemoveAt(i);
                restored++;
            }
        }

        SaveDeletedManifest();
        AssetDatabase.Refresh();
        Debug.Log($"[FindUnusedAssets] Restored {restored} assets from recycle bin.");
    }

    void EmptyRecycleBin()
    {
        if (_deletedEntries.Count == 0)
            return;

        if (!EditorUtility.DisplayDialog(
                "Empty recycle bin?",
                $"This will permanently delete {_deletedEntries.Count} recycled asset entries.\n\nThis action cannot be undone.",
                "Empty Recycle Bin",
                "Cancel"))
            return;

        try
        {
            if (Directory.Exists(RecycleBinRoot))
                Directory.Delete(RecycleBinRoot, true);

            Directory.CreateDirectory(RecycleBinRoot);
            _deletedEntries.Clear();
            SaveDeletedManifest();
            AssetDatabase.Refresh();
            Debug.Log("[FindUnusedAssets] Recycle bin emptied.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[FindUnusedAssets] Failed to empty recycle bin: {e.Message}");
        }
    }

    bool RestoreEntry(DeletedEntry entry)
    {
        string projectRoot = Directory.GetCurrentDirectory().Replace("\\", "/");
        string targetAssetAbs = Path.GetFullPath(Path.Combine(projectRoot, entry.originalAssetPath));
        string sourceAssetAbs = entry.recycleAssetPath.Replace("\\", "/");

        if (!File.Exists(sourceAssetAbs))
        {
            Debug.LogWarning($"[FindUnusedAssets] Recycle file missing, skipping restore: {entry.originalAssetPath}");
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetAssetAbs) ?? projectRoot);

            if (File.Exists(targetAssetAbs))
            {
                Debug.LogWarning($"[FindUnusedAssets] Target already exists, skipping: {entry.originalAssetPath}");
                return false;
            }

            File.Move(sourceAssetAbs, targetAssetAbs);

            string sourceMetaAbs = sourceAssetAbs + ".meta";
            string targetMetaAbs = targetAssetAbs + ".meta";
            if (File.Exists(sourceMetaAbs))
            {
                if (File.Exists(targetMetaAbs))
                    File.Delete(targetMetaAbs);
                File.Move(sourceMetaAbs, targetMetaAbs);
            }

            // Try to clean empty recycle directories.
            var recycleDir = Path.GetDirectoryName(sourceAssetAbs);
            if (!string.IsNullOrEmpty(recycleDir) && Directory.Exists(recycleDir))
            {
                if (!Directory.EnumerateFileSystemEntries(recycleDir).Any())
                    Directory.Delete(recycleDir, false);
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FindUnusedAssets] Failed to restore '{entry.originalAssetPath}': {e.Message}");
            return false;
        }
    }

    void SaveDeletedManifest()
    {
        Directory.CreateDirectory(RecycleBinRoot);
        var lines = _deletedEntries
            .Select(e => $"{Escape(e.originalAssetPath)}|{Escape(e.recycleAssetPath)}|{Escape(e.deletedAtIso)}");
        File.WriteAllLines(RecycleBinManifest, lines);
    }

    void LoadDeletedManifest()
    {
        _deletedEntries.Clear();
        if (!File.Exists(RecycleBinManifest))
            return;

        foreach (var line in File.ReadAllLines(RecycleBinManifest))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = SplitEscaped(line, '|');
            if (parts.Count < 2)
                continue;

            _deletedEntries.Add(new DeletedEntry
            {
                originalAssetPath = Unescape(parts[0]),
                recycleAssetPath = Unescape(parts[1]),
                deletedAtIso = parts.Count > 2 ? Unescape(parts[2]) : ""
            });
        }
    }

    static string Escape(string value)
    {
        return (value ?? "")
            .Replace("\\", "\\\\")
            .Replace("|", "\\|")
            .Replace("\n", "\\n");
    }

    static string Unescape(string value)
    {
        return (value ?? "")
            .Replace("\\n", "\n")
            .Replace("\\|", "|")
            .Replace("\\\\", "\\");
    }

    static List<string> SplitEscaped(string input, char separator)
    {
        var parts = new List<string>();
        var current = "";
        bool escape = false;
        foreach (char c in input)
        {
            if (escape)
            {
                current += c;
                escape = false;
                continue;
            }
            if (c == '\\')
            {
                escape = true;
                current += c;
                continue;
            }
            if (c == separator)
            {
                parts.Add(current);
                current = "";
                continue;
            }
            current += c;
        }
        parts.Add(current);
        return parts;
    }

    class DeletedEntry
    {
        public string originalAssetPath;
        public string recycleAssetPath;
        public string deletedAtIso;
    }
}
