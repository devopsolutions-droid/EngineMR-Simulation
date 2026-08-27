using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine.Networking;

/// <summary>
/// Tools > AI Engine Part Auto-Grouping Tool
///
/// Automatically analyzes engine 3D model parts (names, spatial coordinates, mesh hierarchy)
/// using Groq AI (llama-3.3-70b) or offline spatial heuristics to decide how to group parts
/// into mechanical sub-assemblies and populates EngineAssemblyConfig.partGroups automatically.
/// </summary>
public class AIAutoPartGroupingTool : EditorWindow
{
    private const string GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    private const string DefaultModel = "llama-3.3-70b-versatile";
    private const string PrefKeyApi    = "GroqAPIKey";

    private string _apiKey = "";
    private GameObject _engineRoot;
    private string _engineType = "";
    private bool _reparentObjects = false;
    private string _status = "";
    private bool _running = false;
    private Vector2 _scroll;

    [MenuItem("Tools/AI Engine Part Auto-Grouping Tool")]
    public static void Open()
    {
        GetWindow<AIAutoPartGroupingTool>("AI Part Grouping");
    }

    private void OnEnable()
    {
        _apiKey = EditorPrefs.GetString(PrefKeyApi, "");
    }

    private void OnGUI()
    {
        GUILayout.Label("AI Engine Part Auto-Grouping Tool", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Automatically decides and creates engine part groups using AI.", EditorStyles.miniLabel);
        EditorGUILayout.Space(6);

        // ── Groq API Key ──────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Groq API Key (saved locally)");
        string newKey = EditorGUILayout.PasswordField(_apiKey);
        if (newKey != _apiKey)
        {
            _apiKey = newKey;
            EditorPrefs.SetString(PrefKeyApi, _apiKey);
        }

        EditorGUILayout.Space(6);

        // ── Engine Root Field ─────────────────────────────────────────────────
        EditorGUI.BeginChangeCheck();
        _engineRoot = (GameObject)EditorGUILayout.ObjectField(
            "Engine Root GameObject", _engineRoot, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && _engineRoot != null)
        {
            if (string.IsNullOrEmpty(_engineType))
            {
                _engineType = _engineRoot.name.Replace("_", " ").Replace("-", " ");
            }
        }

        // ── Engine Type / Context ─────────────────────────────────────────────
        _engineType = EditorGUILayout.TextField("Engine Type / Context", _engineType);
        EditorGUILayout.HelpBox(
            "Provide context for higher AI grouping accuracy (e.g. 'Jet Engine', 'V8 Twin Turbo', 'Steam Turbine').",
            MessageType.Info);

        EditorGUILayout.Space(4);

        // ── Options ───────────────────────────────────────────────────────────
        _reparentObjects = EditorGUILayout.ToggleLeft(
            "Physical Reparenting: Create parent GameObjects in scene for each group", _reparentObjects);

        EditorGUILayout.Space(8);

        // ── Action Buttons ────────────────────────────────────────────────────
        bool canRunAI = !_running && !string.IsNullOrEmpty(_apiKey) && _engineRoot != null;

        GUI.enabled = canRunAI;
        if (GUILayout.Button("🤖 Auto-Group Engine Parts (AI - Groq)", GUILayout.Height(36)))
        {
            RunAIGrouping();
        }
        GUI.enabled = !_running && _engineRoot != null;

        if (GUILayout.Button("⚡ Offline Smart Grouping (Rule & Pattern Based)", GUILayout.Height(30)))
        {
            RunOfflineGrouping();
        }
        GUI.enabled = true;

        if (_engineRoot != null)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Hierarchy Tools:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📁 Organize Hierarchy Nodes (1-Click)", GUILayout.Height(26)))
            {
                EngineAssemblyConfig config = _engineRoot.GetComponent<EngineAssemblyConfig>();
                if (config != null && config.partGroups != null)
                {
                    ReparentGameObjects(config.partGroups);
                    _status += "Organized all groups into Hierarchy folder GameObjects.\n";
                }
            }
            if (GUILayout.Button("🔍 Select All Grouped Parts", GUILayout.Height(26)))
            {
                EngineAssemblyConfig config = _engineRoot.GetComponent<EngineAssemblyConfig>();
                if (config != null && config.partGroups != null)
                {
                    List<GameObject> all = new List<GameObject>();
                    foreach (var g in config.partGroups)
                        if (g.parts != null) foreach (var p in g.parts) if (p != null) all.Add(p.gameObject);
                    Selection.objects = all.ToArray();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        if (_engineRoot == null)
            EditorGUILayout.HelpBox("Select or drag an Engine Root GameObject above.", MessageType.Warning);
        else if (string.IsNullOrEmpty(_apiKey))
            EditorGUILayout.HelpBox("Enter your Groq API Key above to enable AI mode (or use Offline mode below). Get a key at console.groq.com", MessageType.Info);

        EditorGUILayout.Space(6);

        // ── Status Console ────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(_status))
        {
            EditorGUILayout.LabelField("Status Log:", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(140));
            EditorGUILayout.HelpBox(_status, _running ? MessageType.Info : MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AI Grouping Logic via Groq API
    // ─────────────────────────────────────────────────────────────────────────
    private async void RunAIGrouping()
    {
        _running = true;
        _status = "Analyzing engine parts geometry and hierarchy...\n";

        List<EnginePart> parts = GetOrAddEngineParts(_engineRoot);
        if (parts.Count == 0)
        {
            _status += "Error: No parts found under Engine Root.";
            _running = false;
            return;
        }

        _status += $"Found {parts.Count} parts. Building prompt for Groq AI...\n";

        // Build list of part metadata
        StringBuilder partListBuilder = new StringBuilder();
        foreach (var p in parts)
        {
            Vector3 pos = p.transform.localPosition;
            partListBuilder.AppendLine($"- Name: \"{p.gameObject.name}\", Pos: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})");
        }

        string systemPrompt =
            "You are a master automotive and aerospace mechanical engineer designing sub-assemblies for an engine VR simulation.\n\n" +
            "CRITICAL RULES FOR GROUPING:\n" +
            "1. TOTAL GROUP COUNT: Create EXACTLY between 4 and 7 broad, major functional groups total for the entire engine model. DO NOT create more than 7 groups under any circumstances.\n" +
            "2. STRUCTURAL SUBSYSTEM CLUSTERING: Group parts by macro engineering subsystem, NOT individually or sequentially.\n" +
            "   - VALVES & VALVETRAIN: Put ALL valves (intake, exhaust, first, second, third, etc.), valve springs, and camshafts together into ONE single group (e.g. 'Valvetrain Assembly').\n" +
            "   - AIR INTAKE SYSTEM: Put ALL air filters, intake manifolds, and intake tubes together into ONE group (e.g. 'Air Intake System').\n" +
            "   - ENGINE BLOCK & CYLINDER HEAD: Put cylinder heads, head covers, and main engine block into ONE group (e.g. 'Engine Block & Cylinder Head').\n" +
            "   - EXHAUST SYSTEM: Put exhaust manifolds, exhaust piping, and turbochargers into ONE group (e.g. 'Exhaust System').\n" +
            "   - DRIVETRAIN & ROTATING ASSEMBLY: Put crankshafts, flywheels, clutches, pistons, and gears into ONE group (e.g. 'Crankshaft & Drivetrain').\n" +
            "3. DO NOT GROUP SEQUENTIALLY: Do not group parts line-by-line. Understand the physical structure of the entire engine as a whole.\n" +
            "4. SELECTIVE COVERAGE: Focus on the 4 to 7 primary functional assemblies of the engine. Do not create separate tiny groups for 1 or 2 parts.\n" +
            "5. EXACT NAMES: The 'partGameObjectNames' array MUST contain exact string GameObject names from the provided list.\n\n" +
            "Return ONLY a valid JSON array of 4 to 7 objects:\n" +
            "[\n" +
            "  {\n" +
            "    \"groupName\": \"Valvetrain Assembly\",\n" +
            "    \"partGameObjectNames\": [\"First Exhaust Valve\", \"Second Exhaust Valve\", \"Intake Camshaft\", \"Exhaust Valve Spring\"]\n" +
            "  }\n" +
            "]";

        string userPrompt = $"Engine Model Context: {_engineType}\n\nEngine Parts List ({parts.Count} parts):\n{partListBuilder}";

        _status += "Sending request to Groq LLM (llama-3.3-70b)...\n";
        Repaint();

        string jsonPayload = BuildGroqJsonPayload(systemPrompt, userPrompt);
        string response = await SendGroqRequest(jsonPayload);

        if (string.IsNullOrEmpty(response))
        {
            _status += "API Error: No response received from Groq.\nFalling back to local offline smart grouping...\n";
            RunOfflineGrouping();
            _running = false;
            return;
        }

        _status += "Received AI response. Processing groups...\n";
        ProcessAIGroupsResponse(response, parts);
        _running = false;
        Repaint();
    }

    private void ProcessAIGroupsResponse(string rawJson, List<EnginePart> allParts)
    {
        try
        {
            // Clean potential markdown wrappers
            string cleanedJson = rawJson.Trim();
            if (cleanedJson.StartsWith("```json")) cleanedJson = cleanedJson.Substring(7);
            if (cleanedJson.StartsWith("```")) cleanedJson = cleanedJson.Substring(3);
            if (cleanedJson.EndsWith("```")) cleanedJson = cleanedJson.Substring(0, cleanedJson.Length - 3);
            cleanedJson = cleanedJson.Trim();

            List<ParsedGroup> parsedGroups = ParseGroupsJson(cleanedJson);

            if (parsedGroups == null || parsedGroups.Count == 0)
            {
                _status += "Warning: Could not parse AI groups JSON format. Output:\n" + rawJson + "\nExecuting offline grouping fallback...\n";
                RunOfflineGrouping();
                return;
            }

            ApplyGroupsToEngineConfig(parsedGroups, allParts);
        }
        catch (Exception ex)
        {
            _status += $"Error parsing AI response: {ex.Message}\nRaw:\n{rawJson}\n";
            Debug.LogError($"[AIAutoPartGrouping] Exception: {ex}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Offline Rule & Spatial Grouping Logic
    // ─────────────────────────────────────────────────────────────────────────
    private void RunOfflineGrouping()
    {
        _status += "Executing Offline Structural Subsystem Grouping (4-6 Major Assemblies)...\n";

        List<EnginePart> parts = GetOrAddEngineParts(_engineRoot);
        if (parts.Count == 0)
        {
            _status += "Error: No parts found under Engine Root.";
            return;
        }

        Dictionary<string, List<string>> categoryMap = new Dictionary<string, List<string>>
        {
            { "Valvetrain Assembly", new List<string>() },
            { "Air Intake System", new List<string>() },
            { "Exhaust System", new List<string>() },
            { "Engine Block & Cylinder Head", new List<string>() },
            { "Drivetrain & Crankshaft Assembly", new List<string>() },
            { "Auxiliary & Electrical Components", new List<string>() }
        };

        foreach (var part in parts)
        {
            string n = part.gameObject.name.ToLower();

            if (n.Contains("valve") || n.Contains("camshaft") || n.Contains("spring") || n.Contains("rocker") || n.Contains("lifter"))
            {
                categoryMap["Valvetrain Assembly"].Add(part.gameObject.name);
            }
            else if (n.Contains("air") || n.Contains("filter") || n.Contains("intake") || n.Contains("plenum") || n.Contains("throttle"))
            {
                categoryMap["Air Intake System"].Add(part.gameObject.name);
            }
            else if (n.Contains("exhaust") || n.Contains("manifold") || n.Contains("turbo") || n.Contains("header") || n.Contains("pipe"))
            {
                categoryMap["Exhaust System"].Add(part.gameObject.name);
            }
            else if (n.Contains("block") || n.Contains("cylinder") || n.Contains("head") || n.Contains("cover") || n.Contains("case") || n.Contains("housing"))
            {
                categoryMap["Engine Block & Cylinder Head"].Add(part.gameObject.name);
            }
            else if (n.Contains("crank") || n.Contains("flywheel") || n.Contains("clutch") || n.Contains("piston") || n.Contains("gear") || n.Contains("distributor") || n.Contains("pulley") || n.Contains("belt") || n.Contains("drum"))
            {
                categoryMap["Drivetrain & Crankshaft Assembly"].Add(part.gameObject.name);
            }
            else
            {
                categoryMap["Auxiliary & Electrical Components"].Add(part.gameObject.name);
            }
        }

        List<ParsedGroup> groups = new List<ParsedGroup>();
        foreach (var kvp in categoryMap)
        {
            if (kvp.Value.Count > 0)
            {
                groups.Add(new ParsedGroup
                {
                    groupName = kvp.Key,
                    partGameObjectNames = kvp.Value
                });
            }
        }

        ApplyGroupsToEngineConfig(groups, parts);
    }

    private string NormalizePartName(string rawName)
    {
        string s = Regex.Replace(rawName, @"[\(\[].*?[\)\]]", "");
        s = Regex.Replace(s, @"[\._\-\s]*\d+$", "");
        s = s.Trim();
        return string.IsNullOrEmpty(s) ? rawName : s;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Apply Groups to EngineAssemblyConfig
    // ─────────────────────────────────────────────────────────────────────────
    private void ApplyGroupsToEngineConfig(List<ParsedGroup> parsedGroups, List<EnginePart> allParts)
    {
        EngineAssemblyConfig config = _engineRoot.GetComponent<EngineAssemblyConfig>();
        if (config == null)
        {
            config = _engineRoot.AddComponent<EngineAssemblyConfig>();
            Undo.RegisterCreatedObjectUndo(config, "Add EngineAssemblyConfig");
        }

        Undo.RecordObject(config, "Apply AI Engine Part Groups");

        if (config.partGroups == null)
            config.partGroups = new List<EnginePartGroupData>();
        else
            config.partGroups.Clear();

        // Use ToLookup to safely handle duplicate GameObject names in the hierarchy
        ILookup<string, EnginePart> partLookup = allParts.ToLookup(p => p.gameObject.name, p => p);

        int totalApplied = 0;
        foreach (var pg in parsedGroups)
        {
            EnginePartGroupData groupData = new EnginePartGroupData
            {
                groupName = pg.groupName,
                parts = new List<EnginePart>()
            };

            foreach (var pName in pg.partGameObjectNames)
            {
                if (partLookup.Contains(pName))
                {
                    foreach (var ep in partLookup[pName])
                    {
                        if (ep != null && !groupData.parts.Contains(ep))
                        {
                            groupData.parts.Add(ep);
                            totalApplied++;
                        }
                    }
                }
            }

            if (groupData.parts.Count > 0)
            {
                config.partGroups.Add(groupData);
            }
        }

        if (_reparentObjects)
        {
            ReparentGameObjects(config.partGroups);
        }

        EditorUtility.SetDirty(config);
        EditorUtility.SetDirty(_engineRoot);

        _status += $"SUCCESS ✓\nCreated {config.partGroups.Count} groups with {totalApplied} total engine parts assigned to EngineAssemblyConfig.\n";
        Debug.Log($"[AIAutoPartGrouping] Successfully generated {config.partGroups.Count} groups on '{_engineRoot.name}'.");

        EditorUtility.DisplayDialog("Done ✓",
            $"Successfully created {config.partGroups.Count} part groups for '{_engineRoot.name}'!\n\nCheck EngineAssemblyConfig in Inspector.", "OK");
    }

    private void ReparentGameObjects(List<EnginePartGroupData> groups)
    {
        foreach (var g in groups)
        {
            if (g.parts == null || g.parts.Count == 0) continue;

            Transform existingParent = _engineRoot.transform.Find(g.groupName);
            GameObject parentGO = existingParent != null ? existingParent.gameObject : new GameObject(g.groupName);

            Undo.RegisterCreatedObjectUndo(parentGO, "Create Group Parent");
            parentGO.transform.SetParent(_engineRoot.transform, worldPositionStays: false);

            Vector3 avgPos = Vector3.zero;
            foreach (var p in g.parts) avgPos += p.transform.position;
            avgPos /= g.parts.Count;
            parentGO.transform.position = avgPos;

            foreach (var p in g.parts)
            {
                Undo.SetTransformParent(p.transform, parentGO.transform, "Reparent Part to Group");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper Methods
    // ─────────────────────────────────────────────────────────────────────────
    private List<EnginePart> GetOrAddEngineParts(GameObject root)
    {
        List<EnginePart> parts = new List<EnginePart>(root.GetComponentsInChildren<EnginePart>(true));

        if (parts.Count == 0)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                EnginePart ep = r.gameObject.GetComponent<EnginePart>();
                if (ep == null)
                {
                    ep = r.gameObject.AddComponent<EnginePart>();
                    ep.partName = r.gameObject.name;
                    Undo.RegisterCreatedObjectUndo(ep, "Auto Add EnginePart");
                }
                parts.Add(ep);
            }
        }
        return parts;
    }

    private string BuildGroqJsonPayload(string systemPrompt, string userPrompt)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        sb.Append($"\"model\":\"{DefaultModel}\",");
        sb.Append("\"temperature\":0.2,");
        sb.Append("\"messages\":[");
        sb.Append("{\"role\":\"system\",\"content\":\"" + EscapeJson(systemPrompt) + "\"},");
        sb.Append("{\"role\":\"user\",\"content\":\"" + EscapeJson(userPrompt) + "\"}");
        sb.Append("]");
        sb.Append("}");
        return sb.ToString();
    }

    [Serializable]
    private class GroqApiResponse
    {
        public GroqChoice[] choices;
    }

    [Serializable]
    private class GroqChoice
    {
        public GroqMessage message;
    }

    [Serializable]
    private class GroqMessage
    {
        public string content;
    }

    private async Task<string> SendGroqRequest(string jsonBody)
    {
        using (UnityWebRequest req = new UnityWebRequest(GroqEndpoint, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {_apiKey.Trim()}");

            var operation = req.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Delay(50);
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AIAutoPartGrouping] Groq API Error: {req.error}\n{req.downloadHandler.text}");
                return null;
            }

            string jsonResp = req.downloadHandler.text;
            try
            {
                GroqApiResponse respObj = JsonUtility.FromJson<GroqApiResponse>(jsonResp);
                if (respObj != null && respObj.choices != null && respObj.choices.Length > 0 && respObj.choices[0].message != null)
                {
                    return respObj.choices[0].message.content;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIAutoPartGrouping] JsonUtility parse notice: {ex.Message}");
            }

            // Fallback manual parse if needed
            int choicesIdx = jsonResp.IndexOf("\"content\":");
            if (choicesIdx < 0) return null;

            int start = jsonResp.IndexOf("\"", choicesIdx + 10) + 1;
            int end = jsonResp.IndexOf("\",", start);
            if (end < 0) end = jsonResp.IndexOf("\"}", start);

            if (start > 0 && end > start)
            {
                string rawContent = jsonResp.Substring(start, end - start);
                return UnescapeJson(rawContent);
            }

            return null;
        }
    }

    private string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    private string UnescapeJson(string s) => s.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");

    [Serializable]
    private class ParsedGroup
    {
        public string groupName;
        public List<string> partGameObjectNames;
    }

    [Serializable]
    private class GroupArrayWrapper
    {
        public ParsedGroup[] groups;
    }

    private List<ParsedGroup> ParseGroupsJson(string json)
    {
        List<ParsedGroup> result = new List<ParsedGroup>();
        try
        {
            // 1. Try JsonUtility wrapper first
            string wrapped = "{\"groups\":" + json + "}";
            GroupArrayWrapper wrapper = JsonUtility.FromJson<GroupArrayWrapper>(wrapped);
            if (wrapper != null && wrapper.groups != null && wrapper.groups.Length > 0)
            {
                return wrapper.groups.Where(g => g != null && g.partGameObjectNames != null && g.partGameObjectNames.Count > 0).ToList();
            }
        }
        catch {}

        // 2. Fallback robust Regex block matcher
        try
        {
            MatchCollection blocks = Regex.Matches(json, @"\{[^{}]*\}", RegexOptions.Singleline);
            foreach (Match b in blocks)
            {
                string blockText = b.Value;
                Match nameMatch = Regex.Match(blockText, @"""groupName""\s*:\s*""([^""]+)""");
                Match partsMatch = Regex.Match(blockText, @"""partGameObjectNames""\s*:\s*\[([^\]]*)\]", RegexOptions.Singleline);

                if (nameMatch.Success && partsMatch.Success)
                {
                    string gName = nameMatch.Groups[1].Value;
                    string partsText = partsMatch.Groups[1].Value;
                    List<string> partsList = new List<string>();
                    MatchCollection pm = Regex.Matches(partsText, @"""([^""]+)""");
                    foreach (Match p in pm) partsList.Add(p.Groups[1].Value);

                    if (partsList.Count > 0)
                    {
                        result.Add(new ParsedGroup { groupName = gName, partGameObjectNames = partsList });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AIAutoPartGrouping] Regex parser notice: {ex.Message}");
        }
        return result;
    }
}
