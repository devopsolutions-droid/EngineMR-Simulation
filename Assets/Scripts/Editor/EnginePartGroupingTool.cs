using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tools > Engine Part Grouping Tool
///
/// Workflow:
///   1. Drag any engine root GameObject (scene object or prefab instance) into "Engine Root"
///   2. ALL direct children are listed on the left — no EnginePart required
///   3. Click to select (Ctrl = toggle, Shift = range), or use All / None buttons
///   4. Click "+ Add Group", name it, then drag selected rows into the drop zone
///   5. Click "✓ Apply Groups" — a new parent GameObject is created for each group,
///      all selected children are reparented under it, and the result is saved as a
///      new prefab in the same folder as the original (or next to the scene root).
/// </summary>
public class EnginePartGroupingTool : EditorWindow
{
    // ── Engine root ───────────────────────────────────────────────────────────
    private GameObject _engineRoot;

    // ── All direct children of the engine root ────────────────────────────────
    private List<GameObject> _allChildren = new List<GameObject>();

    // ── Groups the user is building ───────────────────────────────────────────
    private List<PartGroup> _groups = new List<PartGroup>();

    // ── Scroll positions ──────────────────────────────────────────────────────
    private Vector2 _leftScroll;
    private Vector2 _rightScroll;

    // ── Multi-selection ───────────────────────────────────────────────────────
    private HashSet<GameObject> _selectedObjects = new HashSet<GameObject>();
    private int _lastClickedIndex = -1;

    // ── Drag state ────────────────────────────────────────────────────────────
    private List<GameObject> _draggingObjects = new List<GameObject>();
    private bool _isDragging = false;

    // ── Save-as-new-prefab path ───────────────────────────────────────────────
    private string _savePath = "Assets/Prefabs/Engine Prefabs/GroupedEngine.prefab";

    private class PartGroup
    {
        public string           name    = "New Group";
        public List<GameObject> objects = new List<GameObject>();
        public bool             foldout = true;
    }

    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Engine Part Grouping Tool")]
    public static void Open() => GetWindow<EnginePartGroupingTool>("Part Grouping");

    // ─────────────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        GUILayout.Label("Engine Part Grouping Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // Engine root field
        EditorGUI.BeginChangeCheck();
        _engineRoot = (GameObject)EditorGUILayout.ObjectField(
            "Engine Root", _engineRoot, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
            ScanChildren();

        if (_engineRoot == null)
        {
            EditorGUILayout.HelpBox("Drag any engine root GameObject from the Hierarchy here.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(4);

        // Save path
        EditorGUILayout.BeginHorizontal();
        _savePath = EditorGUILayout.TextField("Save Prefab As", _savePath);
        if (GUILayout.Button("…", GUILayout.Width(26)))
        {
            string chosen = EditorUtility.SaveFilePanelInProject(
                "Save Grouped Prefab", System.IO.Path.GetFileNameWithoutExtension(_savePath),
                "prefab", "Choose where to save the new prefab", System.IO.Path.GetDirectoryName(_savePath));
            if (!string.IsNullOrEmpty(chosen)) _savePath = chosen;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Two-column layout
        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();
        GUILayout.Space(8);
        DrawRightPanel();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // Bottom buttons
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
        if (GUILayout.Button("+ Add Group", GUILayout.Height(30)))
            _groups.Add(new PartGroup { name = $"Group {_groups.Count + 1}" });

        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        GUI.enabled = _groups.Count > 0 && _groups.Any(g => g.objects.Count >= 1);
        if (GUILayout.Button("✓ Apply Groups", GUILayout.Height(30)))
            ApplyGroups();

        GUI.enabled = true;
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "1. Drag engine root into the field above\n" +
            "2. Click '+ Add Group', name it\n" +
            "3. Select objects on the left (Ctrl = toggle, Shift = range)\n" +
            "4. Drag selected rows into a group drop zone on the right\n" +
            "5. Click '✓ Apply Groups' — saves a new prefab at the path above",
            MessageType.Info);
    }

    // ── Scan all descendants ──────────────────────────────────────────────────
    void ScanChildren()
    {
        _allChildren.Clear();
        _groups.Clear();
        _selectedObjects.Clear();
        _draggingObjects.Clear();
        _isDragging       = false;
        _lastClickedIndex = -1;

        if (_engineRoot == null) return;

        // Collect every descendant that has a MeshRenderer or SkinnedMeshRenderer
        // (i.e. actual visible mesh objects), excluding the root itself.
        var allTransforms = _engineRoot.GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            if (t.gameObject == _engineRoot) continue; // skip root itself
            // Only include leaf-level mesh objects (has a renderer)
            if (t.GetComponent<MeshRenderer>() != null || t.GetComponent<SkinnedMeshRenderer>() != null)
                _allChildren.Add(t.gameObject);
        }

        // Fallback: if no mesh renderers found, just list all descendants
        if (_allChildren.Count == 0)
        {
            foreach (var t in allTransforms)
            {
                if (t.gameObject == _engineRoot) continue;
                _allChildren.Add(t.gameObject);
            }
        }

        // Auto-suggest save path next to the original asset (if it is a prefab)
        string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(_engineRoot);
        if (!string.IsNullOrEmpty(assetPath))
        {
            string dir  = System.IO.Path.GetDirectoryName(assetPath);
            string stem = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            _savePath = $"{dir}/{stem}_Grouped.prefab";
        }

        Debug.Log($"[PartGrouping] Found {_allChildren.Count} objects in '{_engineRoot.name}'");
    }

    // ── Left panel ────────────────────────────────────────────────────────────
    void DrawLeftPanel()
    {
        var ungrouped = GetUngroupedObjects();

        EditorGUILayout.BeginVertical(GUILayout.Width(230));

        // Header row
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"Children ({ungrouped.Count} ungrouped)", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        GUI.backgroundColor = new Color(0.7f, 0.7f, 1f);
        if (GUILayout.Button("All", GUILayout.Width(32), GUILayout.Height(18)))
        { foreach (var o in ungrouped) _selectedObjects.Add(o); Repaint(); }

        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
        if (GUILayout.Button("None", GUILayout.Width(36), GUILayout.Height(18)))
        { _selectedObjects.Clear(); Repaint(); }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (_selectedObjects.Count > 0)
        {
            var hint = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.3f, 0.85f, 1f) } };
            GUILayout.Label($"  {_selectedObjects.Count} selected — drag any row to move all", hint);
        }

        _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUILayout.Height(320));

        for (int i = 0; i < ungrouped.Count; i++)
        {
            var obj = ungrouped[i];
            if (obj == null) continue;

            bool isSelected     = _selectedObjects.Contains(obj);
            bool isDraggingThis = _isDragging && _draggingObjects.Contains(obj);

            GUI.backgroundColor = isDraggingThis ? new Color(0.2f, 1f, 0.6f)
                                 : isSelected     ? new Color(0.3f, 0.6f, 1f)
                                 :                  Color.white;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Box(obj.name, GUILayout.ExpandWidth(true), GUILayout.Height(22));
            Rect rowRect = GUILayoutUtility.GetLastRect();
            GUI.backgroundColor = Color.white;

            // Mouse events
            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                bool ctrl  = Event.current.control || Event.current.command;
                bool shift = Event.current.shift;

                if (shift && _lastClickedIndex >= 0)
                {
                    int lo = Mathf.Min(_lastClickedIndex, i);
                    int hi = Mathf.Max(_lastClickedIndex, i);
                    for (int r = lo; r <= hi; r++)
                        if (r < ungrouped.Count) _selectedObjects.Add(ungrouped[r]);
                }
                else if (ctrl)
                {
                    if (isSelected) _selectedObjects.Remove(obj);
                    else            _selectedObjects.Add(obj);
                    _lastClickedIndex = i;
                }
                else
                {
                    if (!isSelected)
                    {
                        _selectedObjects.Clear();
                        _selectedObjects.Add(obj);
                        _lastClickedIndex = i;
                    }
                    // keep existing selection if clicking an already-selected row (for drag)
                }

                // Start drag with full selection
                _draggingObjects.Clear();
                _draggingObjects.AddRange(_selectedObjects.Count > 0 ? _selectedObjects : new HashSet<GameObject> { obj });
                _isDragging = true;
                Event.current.Use();
                Repaint();
            }

            // Ping button
            if (GUILayout.Button("→", GUILayout.Width(22), GUILayout.Height(22)))
                EditorGUIUtility.PingObject(obj);

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ── Right panel ───────────────────────────────────────────────────────────
    void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical();
        GUILayout.Label("Groups", EditorStyles.boldLabel);

        _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll, GUILayout.Height(320));

        for (int gi = _groups.Count - 1; gi >= 0; gi--)
        {
            var group = _groups[gi];

            EditorGUILayout.BeginVertical(GUI.skin.box);

            // Header
            EditorGUILayout.BeginHorizontal();
            group.foldout = EditorGUILayout.Foldout(group.foldout, "", true);
            group.name    = EditorGUILayout.TextField(group.name, GUILayout.ExpandWidth(true));

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                _groups.RemoveAt(gi);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                continue;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (group.foldout)
            {
                bool canDrop = _isDragging && _draggingObjects.Count > 0;

                string dropLabel = canDrop
                    ? (_draggingObjects.Count > 1 ? $"Drop {_draggingObjects.Count} objects here" : "Drop here")
                    : "← Drag objects here";

                var dropStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = canDrop ? Color.green : Color.grey }
                };

                GUI.backgroundColor = canDrop ? new Color(0.2f, 0.55f, 0.2f, 0.35f) : Color.white;
                Rect dropRect = GUILayoutUtility.GetRect(0, 34, GUILayout.ExpandWidth(true));
                GUI.Box(dropRect, dropLabel, dropStyle);
                GUI.backgroundColor = Color.white;

                // Handle drop
                if (Event.current.type == EventType.MouseUp &&
                    dropRect.Contains(Event.current.mousePosition) &&
                    _isDragging && _draggingObjects.Count > 0)
                {
                    foreach (var obj in _draggingObjects)
                        if (obj != null && !group.objects.Contains(obj))
                            group.objects.Add(obj);

                    _selectedObjects.Clear();
                    _draggingObjects.Clear();
                    _isDragging = false;
                    Event.current.Use();
                    Repaint();
                }

                // List objects in group
                for (int pi = group.objects.Count - 1; pi >= 0; pi--)
                {
                    var obj = group.objects[pi];
                    if (obj == null) { group.objects.RemoveAt(pi); continue; }

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"  • {obj.name}", GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                        group.objects.RemoveAt(pi);
                    EditorGUILayout.EndHorizontal();
                }

                if (group.objects.Count == 0)
                    EditorGUILayout.LabelField("  (empty)", EditorStyles.miniLabel);

                EditorGUILayout.LabelField($"  {group.objects.Count} object(s)", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // Cancel drag on mouse-up outside any drop zone
        if (Event.current.type == EventType.MouseUp && _isDragging)
        {
            _draggingObjects.Clear();
            _isDragging = false;
            Repaint();
        }
    }

    // ── Apply: reparent + save new prefab ─────────────────────────────────────
    void ApplyGroups()
    {
        if (_engineRoot == null) return;

        // Work on a duplicate in the scene so we never touch the original asset
        GameObject workCopy = Instantiate(_engineRoot);
        workCopy.name = _engineRoot.name;

        // Build instanceID → Transform lookup on the copy.
        // We match by sibling index path so duplicate names aren't a problem.
        // Simpler: build a parallel list — copy was instantiated in the same order,
        // so we can match by the object's path relative to the root.
        var srcAllTransforms  = _engineRoot.GetComponentsInChildren<Transform>(true);
        var copyAllTransforms = workCopy.GetComponentsInChildren<Transform>(true);

        // Map: source Transform instanceID → copy Transform
        var srcToCopy = new Dictionary<int, Transform>();
        for (int i = 0; i < srcAllTransforms.Length && i < copyAllTransforms.Length; i++)
            srcToCopy[srcAllTransforms[i].GetInstanceID()] = copyAllTransforms[i];

        int groupsCreated = 0;

        foreach (var group in _groups)
        {
            if (group.objects.Count == 0) continue;
            if (string.IsNullOrEmpty(group.name)) continue;

            // Create the group parent inside the copy
            GameObject groupGO = new GameObject(group.name);
            groupGO.transform.SetParent(workCopy.transform, worldPositionStays: false);

            // Position at average world position of the members
            Vector3 avg = Vector3.zero;
            int found = 0;
            foreach (var srcObj in group.objects)
            {
                if (srcObj == null) continue;
                if (srcToCopy.TryGetValue(srcObj.transform.GetInstanceID(), out Transform ct))
                { avg += ct.position; found++; }
            }
            if (found > 0) groupGO.transform.position = avg / found;

            // Add EnginePart + sibling components on the group parent
            var ep = groupGO.AddComponent<EnginePart>();
            ep.partName = group.name;
            groupGO.AddComponent<EnginePartVisuals>();
            groupGO.AddComponent<EnginePartExplode>();

            // Set layer
            int layer = LayerMask.NameToLayer("EngineParts");
            if (layer >= 0) groupGO.layer = layer;

            // Reparent matching children from the copy under this group
            foreach (var srcObj in group.objects)
            {
                if (srcObj == null) continue;
                if (!srcToCopy.TryGetValue(srcObj.transform.GetInstanceID(), out Transform ct)) continue;

                ct.SetParent(groupGO.transform, worldPositionStays: true);
                if (layer >= 0) ct.gameObject.layer = layer;
            }

            groupsCreated++;
            Debug.Log($"[PartGrouping] Group '{group.name}' — {group.objects.Count} children.");
        }

        // Ensure save directory exists
        string saveDir = System.IO.Path.GetDirectoryName(_savePath);
        if (!AssetDatabase.IsValidFolder(saveDir))
        {
            string[] parts = saveDir.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // Save as new prefab asset
        bool success = false;
        try
        {
            PrefabUtility.SaveAsPrefabAsset(workCopy, _savePath, out success);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PartGrouping] Failed to save prefab: {ex.Message}");
        }

        DestroyImmediate(workCopy);

        if (success)
        {
            AssetDatabase.Refresh();
            var newAsset = AssetDatabase.LoadAssetAtPath<GameObject>(_savePath);
            EditorGUIUtility.PingObject(newAsset);

            EditorUtility.DisplayDialog("Done ✓",
                $"{groupsCreated} group(s) created.\n\n" +
                $"New prefab saved to:\n{_savePath}\n\n" +
                "Drag it into your scene to use it.",
                "OK");

            _groups.Clear();
            _selectedObjects.Clear();
            ScanChildren();
        }
        else
        {
            EditorUtility.DisplayDialog("Save Failed",
                $"Could not save prefab to:\n{_savePath}\n\nCheck the path and try again.",
                "OK");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    List<GameObject> GetUngroupedObjects()
    {
        var grouped = new HashSet<GameObject>();
        foreach (var g in _groups)
            foreach (var o in g.objects)
                grouped.Add(o);

        return _allChildren.Where(o => o != null && !grouped.Contains(o)).ToList();
    }
}
