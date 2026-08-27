// using UnityEngine;
// using UnityEditor;
// using System.IO;
// using System.Collections.Generic;
// using System.Text;
// using System.Threading.Tasks;
// using UnityEngine.Networking;

// /// <summary>
// /// Tools > Groq Part Description Generator
// ///
// /// Usage:
// ///   1. Drop your GLB / prefab into the "3D Model" field
// ///   2. Type what it is (e.g. "V8 Twin Turbo engine", "Boeing CFM56 jet engine")
// ///   3. Click Generate
// ///
// /// The tool auto-finds the matching EngineData + Parts folder,
// /// extracts real mesh geometry (position, size, shape, vertices, material),
// /// sends everything to Groq llama-3.3-70b, and writes accurate names +
// /// descriptions back into every PartData asset automatically.
// /// </summary>
// public class GroqPartDescriptionGenerator : EditorWindow
// {
//     private const string GroqEndpoint   = "https://api.groq.com/openai/v1/chat/completions";
//     private const string Model          = "llama-3.3-70b-versatile";
//     private const string PrefKeyApi     = "GroqAPIKey";
//     private const string PrefKeyType    = "GroqModelType";

//     private string      _apiKey     = "";
//     private string      _modelType  = "";
//     private GameObject  _droppedModel;
//     private string      _status     = "";
//     private bool        _running    = false;
//     private Vector2     _scroll;

//     [MenuItem("Tools/Groq Part Description Generator")]
//     public static void Open() => GetWindow<GroqPartDescriptionGenerator>("Groq AI Descriptions");

//     void OnEnable()
//     {
//         _apiKey    = EditorPrefs.GetString(PrefKeyApi,  "");
//         _modelType = EditorPrefs.GetString(PrefKeyType, "");
//     }

//     void OnGUI()
//     {
//         GUILayout.Label("Groq AI — Part Description Generator", EditorStyles.boldLabel);
//         EditorGUILayout.Space(6);

//         // ── API Key ───────────────────────────────────────────────────────────
//         EditorGUILayout.LabelField("Groq API Key  (saved locally, never committed)");
//         string newKey = EditorGUILayout.PasswordField(_apiKey);
//         if (newKey != _apiKey) { _apiKey = newKey; EditorPrefs.SetString(PrefKeyApi, _apiKey); }

//         EditorGUILayout.Space(6);

//         // ── Drop model ────────────────────────────────────────────────────────
//         var newModel = (GameObject)EditorGUILayout.ObjectField(
//             "3D Model (GLB / Prefab)", _droppedModel, typeof(GameObject), false);
//         if (newModel != _droppedModel)
//         {
//             _droppedModel = newModel;
//             // Auto-fill model type from asset name if field is empty
//             if (_droppedModel != null && string.IsNullOrEmpty(_modelType))
//             {
//                 _modelType = _droppedModel.name.Replace("_", " ").Replace("-", " ");
//                 EditorPrefs.SetString(PrefKeyType, _modelType);
//             }
//         }

//         // ── Model type ────────────────────────────────────────────────────────
//         EditorGUILayout.Space(2);
//         string newType = EditorGUILayout.TextField("What is this model?", _modelType);
//         if (newType != _modelType) { _modelType = newType; EditorPrefs.SetString(PrefKeyType, _modelType); }

//         EditorGUILayout.HelpBox(
//             "Be specific for best accuracy.\n" +
//             "Examples:\n" +
//             "  • V8 Twin Turbo gasoline engine\n" +
//             "  • F6 Boxer engine (Porsche 911)\n" +
//             "  • CFM56 Turbofan jet engine\n" +
//             "  • Ferrari 458 Italia V8 engine\n" +
//             "  • Diesel truck engine",
//             MessageType.Info);

//         EditorGUILayout.Space(8);

//         // ── Generate button ───────────────────────────────────────────────────
//         bool canRun = !_running
//                    && !string.IsNullOrEmpty(_apiKey)
//                    && _droppedModel != null
//                    && !string.IsNullOrEmpty(_modelType);

//         GUI.enabled = canRun;
//         if (GUILayout.Button("Generate All Descriptions  (AI)", GUILayout.Height(44)))
//             RunGeneration();
//         GUI.enabled = true;

//         if (_droppedModel == null)
//             EditorGUILayout.HelpBox("Drop a GLB or Prefab above to begin.", MessageType.Warning);
//         else if (string.IsNullOrEmpty(_modelType))
//             EditorGUILayout.HelpBox("Describe what this model is.", MessageType.Warning);

//         EditorGUILayout.Space(6);

//         // ── Status ────────────────────────────────────────────────────────────
//         if (!string.IsNullOrEmpty(_status))
//         {
//             _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(160));
//             EditorGUILayout.HelpBox(_status, _running ? MessageType.Info : MessageType.None);
//             EditorGUILayout.EndScrollView();
//         }

//         EditorGUILayout.Space(2);
//         EditorGUILayout.LabelField("Get free API key → console.groq.com", EditorStyles.miniLabel);
//     }

//     // ─────────────────────────────────────────────────────────────────────────

//     async void RunGeneration()
//     {
//         _running = true;
//         _status  = "Locating Parts folder for this model...";
//         Repaint();

//         // ── Find the Parts folder that belongs to this prefab ─────────────────
//         string partsFolder = FindPartsFolderForModel(_droppedModel);
//         if (partsFolder == null)
//         {
//             _status  = "ERROR: Could not find a Parts folder for this model.\n\n" +
//                        "Make sure you ran  Tools → Engine Part Setup  on this model first.\n" +
//                        "That tool creates the Parts/ folder and PartData assets automatically.";
//             _running = false; Repaint(); return;
//         }

//         _status = $"Parts folder found:\n{partsFolder}\n\nLoading PartData assets...";
//         Repaint();

//         // ── Load PartData assets ──────────────────────────────────────────────
//         var guids = AssetDatabase.FindAssets("t:PartData", new[] { partsFolder });
//         if (guids.Length == 0)
//         {
//             _status  = $"No PartData assets found in:\n{partsFolder}\n\nRun Engine Part Setup first.";
//             _running = false; Repaint(); return;
//         }

//         var parts = new List<PartData>();
//         foreach (var g in guids)
//         {
//             var pd = AssetDatabase.LoadAssetAtPath<PartData>(AssetDatabase.GUIDToAssetPath(g));
//             if (pd != null) parts.Add(pd);
//         }

//         _status = $"Found {parts.Count} parts.\nExtracting mesh geometry...";
//         Repaint();

//         // ── Extract geometry from the prefab ──────────────────────────────────
//         var geoData = ExtractGeometry(_droppedModel, parts);

//         _status = $"Geometry extracted.\nSending {parts.Count} parts to Groq AI...";
//         Repaint();

//         // ── Call Groq ─────────────────────────────────────────────────────────
//         string prompt  = BuildPrompt(geoData, _modelType);
//         var    results = await CallGroq(prompt);

//         if (results == null || results.Count == 0)
//         {
//             _status  = "ERROR: Groq request failed or returned empty response.\n" +
//                        "Check your API key and internet connection.\nSee Console for details.";
//             _running = false; Repaint(); return;
//         }

//         // ── Write results back ────────────────────────────────────────────────
//         int applied = 0;
//         for (int i = 0; i < parts.Count && i < results.Count; i++)
//         {
//             if (!string.IsNullOrEmpty(results[i].name))
//                 parts[i].partName = results[i].name;
//             if (!string.IsNullOrEmpty(results[i].description))
//                 parts[i].description = results[i].description;
//             EditorUtility.SetDirty(parts[i]);
//             applied++;
//         }

//         AssetDatabase.SaveAssets();
//         AssetDatabase.Refresh();

//         _status  = $"✓ Done!  {applied} / {parts.Count} parts updated.\n\n" +
//                    $"All PartData assets saved to:\n{partsFolder}";
//         _running = false;
//         Repaint();
//     }

//     // ── Auto-find Parts folder ────────────────────────────────────────────────

//     /// <summary>
//     /// Searches all EngineData assets in the project for one whose enginePrefab
//     /// matches the dropped model, then returns its sibling Parts/ folder.
//     /// Falls back to searching by prefab name if no exact match found.
//     /// </summary>
//     static string FindPartsFolderForModel(GameObject model)
//     {
//         string modelAssetPath = AssetDatabase.GetAssetPath(model);
//         string modelName      = model.name;
//         string modelNorm      = Normalize(modelName);

//         // Search all EngineData assets
//         string[] edGuids = AssetDatabase.FindAssets("t:EngineData");
//         foreach (var guid in edGuids)
//         {
//             var ed = AssetDatabase.LoadAssetAtPath<EngineData>(AssetDatabase.GUIDToAssetPath(guid));
//             if (ed == null) continue;

//             bool match = false;

//             // Exact prefab reference match
//             if (ed.enginePrefab != null)
//                 match = AssetDatabase.GetAssetPath(ed.enginePrefab) == modelAssetPath;

//             // Name-based fallback — normalize spaces, underscores, hyphens
//             if (!match && ed.engineName != null)
//                 match = Normalize(ed.engineName) == modelNorm;

//             if (match)
//             {
//                 string edDir     = Path.GetDirectoryName(AssetDatabase.GetAssetPath(ed)).Replace("\\", "/");
//                 string partsPath = edDir + "/Parts";
//                 if (Directory.Exists(partsPath)) return partsPath;
//             }
//         }

//         // Fallback: search all Parts folders whose engine folder name matches the model name
//         string[] allDirs = Directory.GetDirectories("Assets", "Parts", SearchOption.AllDirectories);
//         foreach (var dir in allDirs)
//         {
//             string unityDir    = dir.Replace("\\", "/");
//             string engineFolder = Path.GetFileName(Path.GetDirectoryName(unityDir));
//             if (Normalize(engineFolder) == modelNorm)
//                 return unityDir;
//         }

//         // Last resort: any Parts folder under an engine folder whose name contains the model name
//         foreach (var dir in allDirs)
//         {
//             string unityDir     = dir.Replace("\\", "/");
//             string engineFolder = Path.GetFileName(Path.GetDirectoryName(unityDir));
//             if (Normalize(engineFolder).Contains(modelNorm) || modelNorm.Contains(Normalize(engineFolder)))
//                 return unityDir;
//         }

//         return null;
//     }

//     /// <summary>Lowercases and strips spaces, underscores, and hyphens for fuzzy name matching.</summary>
//     static string Normalize(string s) =>
//         s.Replace(" ", "").Replace("_", "").Replace("-", "").ToLower();

//     // ── Geometry Extraction ───────────────────────────────────────────────────

//     struct PartGeo
//     {
//         public string rawName;
//         public string position;   // e.g. "top-center-front"
//         public string size;       // large / medium / small / tiny
//         public string shape;      // complex / elongated / thin-flat / compact
//         public int    vertices;
//         public string material;
//     }

//     static List<PartGeo> ExtractGeometry(GameObject prefab, List<PartData> parts)
//     {
//         var result   = new List<PartGeo>();
//         var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
//         instance.hideFlags = HideFlags.HideAndDontSave;

//         try
//         {
//             // Overall engine bounds
//             var allR = instance.GetComponentsInChildren<Renderer>(true);
//             var engBounds = new Bounds(instance.transform.position, Vector3.zero);
//             foreach (var r in allR) engBounds.Encapsulate(r.bounds);
//             Vector3 ec = engBounds.center;
//             Vector3 es = engBounds.size;

//             // Name → (MeshFilter, Renderer) lookup
//             var lookup = new Dictionary<string, (MeshFilter mf, Renderer rend)>();
//             foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
//                 if (!lookup.ContainsKey(mf.gameObject.name))
//                     lookup[mf.gameObject.name] = (mf, mf.GetComponent<Renderer>());

//             foreach (var pd in parts)
//             {
//                 var geo = new PartGeo { rawName = pd.partName };

//                 if (lookup.TryGetValue(pd.partName, out var entry)
//                     && entry.mf != null && entry.mf.sharedMesh != null)
//                 {
//                     var mesh = entry.mf.sharedMesh;
//                     var rend = entry.rend;
//                     var b    = rend != null ? rend.bounds : new Bounds(entry.mf.transform.position, Vector3.zero);

//                     // Relative position
//                     Vector3 rel = b.center - ec;
//                     float rx = es.x > 0 ? rel.x / (es.x * 0.5f) : 0;
//                     float ry = es.y > 0 ? rel.y / (es.y * 0.5f) : 0;
//                     float rz = es.z > 0 ? rel.z / (es.z * 0.5f) : 0;
//                     string px = rx >  0.3f ? "right"  : rx < -0.3f ? "left"   : "center";
//                     string py = ry >  0.3f ? "top"    : ry < -0.3f ? "bottom" : "middle";
//                     string pz = rz >  0.3f ? "front"  : rz < -0.3f ? "rear"   : "center";
//                     geo.position = $"{py}-{px}-{pz}";

//                     // Relative size
//                     float pv   = b.size.x * b.size.y * b.size.z;
//                     float ev   = es.x * es.y * es.z;
//                     float rat  = ev > 0 ? pv / ev : 0;
//                     geo.size   = rat > 0.15f ? "large" : rat > 0.04f ? "medium" : rat > 0.008f ? "small" : "tiny";

//                     // Shape
//                     float maxD = Mathf.Max(b.size.x, b.size.y, b.size.z);
//                     float minD = Mathf.Min(b.size.x, b.size.y, b.size.z);
//                     float asp  = maxD > 0 ? minD / maxD : 1;
//                     geo.shape  = asp < 0.15f ? "thin-flat"
//                                : asp < 0.4f  ? "elongated"
//                                : mesh.vertexCount > 2000 ? "complex" : "compact";

//                     geo.vertices = mesh.vertexCount;

//                     // Material hint
//                     if (rend != null && rend.sharedMaterial != null)
//                     {
//                         string mn = rend.sharedMaterial.name
//                             .Replace("(Instance)", "").Replace("_", " ").Trim();
//                         geo.material = mn.Length > 35 ? mn.Substring(0, 35) : mn;
//                     }
//                 }
//                 else
//                 {
//                     geo.position = "unknown";
//                     geo.size     = "unknown";
//                     geo.shape    = "unknown";
//                 }

//                 result.Add(geo);
//             }
//         }
//         finally { DestroyImmediate(instance); }

//         return result;
//     }

//     // ── Prompt ────────────────────────────────────────────────────────────────

//     static string BuildPrompt(List<PartGeo> geoData, string modelType)
//     {
//         var sb = new StringBuilder();
//         sb.AppendLine("You are an expert mechanical engineer and technical writer.");
//         sb.AppendLine($"You are analyzing a 3D model of: {modelType}");
//         sb.AppendLine();
//         sb.AppendLine("Each part below has geometric data extracted from the actual 3D mesh:");
//         sb.AppendLine("  position = location relative to model center (vertical-horizontal-depth)");
//         sb.AppendLine("  size     = relative to whole model (large/medium/small/tiny)");
//         sb.AppendLine("  shape    = mesh shape classification");
//         sb.AppendLine("  vertices = mesh complexity");
//         sb.AppendLine("  material = material name hint from the 3D file");
//         sb.AppendLine();
//         sb.AppendLine("PARTS:");

//         for (int i = 0; i < geoData.Count; i++)
//         {
//             var g = geoData[i];
//             sb.Append($"{i + 1}.");
//             sb.Append($" position={g.position}");
//             sb.Append($" size={g.size}");
//             sb.Append($" shape={g.shape}");
//             sb.Append($" vertices={g.vertices}");
//             if (!string.IsNullOrEmpty(g.material))
//                 sb.Append($" material=\"{g.material}\"");
//             sb.AppendLine();
//         }

//         sb.AppendLine();
//         sb.AppendLine($"Return a JSON array of exactly {geoData.Count} objects.");
//         sb.AppendLine("Each object: {\"name\": \"Part Name\", \"description\": \"2-3 sentence technical description.\"}");
//         sb.AppendLine();
//         sb.AppendLine("Rules:");
//         sb.AppendLine("- Every part name must be UNIQUE — no duplicates");
//         sb.AppendLine("- Names must be real mechanical part names for this specific model type");
//         sb.AppendLine("- Descriptions must be technically accurate and educational");
//         sb.AppendLine("- Use geometry data to reason: large+top+complex = main block/head, tiny+scattered = bolts/sensors, thin-flat = gaskets/shields, bottom = oil pan/sump, rear = flywheel/clutch");
//         sb.AppendLine("- Return ONLY the raw JSON array, no markdown, no explanation");

//         return sb.ToString();
//     }

//     // ── Groq API ──────────────────────────────────────────────────────────────

//     async Task<List<(string name, string description)>> CallGroq(string prompt)
//     {
//         string body = "{\"model\":\"" + Model + "\"," +
//                       "\"messages\":[{\"role\":\"user\",\"content\":" + JsonEscape(prompt) + "}]," +
//                       "\"temperature\":0.2,\"max_tokens\":6000}";
//         try
//         {
//             using var req = new UnityWebRequest(GroqEndpoint, "POST");
//             req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
//             req.downloadHandler = new DownloadHandlerBuffer();
//             req.SetRequestHeader("Content-Type",  "application/json");
//             req.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

//             var op = req.SendWebRequest();
//             while (!op.isDone) await Task.Yield();

//             if (req.result != UnityWebRequest.Result.Success)
//             {
//                 Debug.LogError($"[Groq] {req.error}\n{req.downloadHandler.text}");
//                 return null;
//             }

//             string content = ExtractContent(req.downloadHandler.text);
//             Debug.Log($"[Groq] Response:\n{content}");
//             return string.IsNullOrEmpty(content) ? null : ParseArray(content);
//         }
//         catch (System.Exception e) { Debug.LogError($"[Groq] {e.Message}"); return null; }
//     }

//     // ── JSON Helpers ──────────────────────────────────────────────────────────

//     static List<(string, string)> ParseArray(string content)
//     {
//         var list  = new List<(string, string)>();
//         int start = content.IndexOf('[');
//         int end   = content.LastIndexOf(']');
//         if (start < 0 || end <= start) return null;

//         string json = content.Substring(start, end - start + 1);
//         int i = 0;
//         while (i < json.Length)
//         {
//             int os = json.IndexOf('{', i); if (os < 0) break;
//             int oe = FindBrace(json, os);  if (oe < 0) break;
//             string obj = json.Substring(os, oe - os + 1);
//             list.Add((ReadStr(obj, "name") ?? "", ReadStr(obj, "description") ?? ""));
//             i = oe + 1;
//         }
//         return list;
//     }

//     static int FindBrace(string s, int from)
//     {
//         int d = 0; bool inS = false;
//         for (int i = from; i < s.Length; i++)
//         {
//             char c = s[i];
//             if (c == '\\' && inS) { i++; continue; }
//             if (c == '"') { inS = !inS; continue; }
//             if (inS) continue;
//             if (c == '{') d++; else if (c == '}') { d--; if (d == 0) return i; }
//         }
//         return -1;
//     }

//     static string ExtractContent(string resp)
//     {
//         int idx = resp.IndexOf("\"content\":");
//         if (idx < 0) return null;
//         idx += 10;
//         while (idx < resp.Length && resp[idx] != '"') idx++;
//         if (idx >= resp.Length) return null;
//         idx++;
//         var sb = new StringBuilder();
//         while (idx < resp.Length)
//         {
//             char c = resp[idx];
//             if (c == '\\' && idx + 1 < resp.Length)
//             {
//                 char n = resp[++idx];
//                 sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
//                 idx++; continue;
//             }
//             if (c == '"') break;
//             sb.Append(c); idx++;
//         }
//         return sb.ToString();
//     }

//     static string ReadStr(string json, string key)
//     {
//         int idx = json.IndexOf($"\"{key}\"");
//         if (idx < 0) return null;
//         idx += key.Length + 2;
//         while (idx < json.Length && json[idx] != '"') idx++;
//         if (idx >= json.Length) return null;
//         idx++;
//         var sb = new StringBuilder();
//         while (idx < json.Length)
//         {
//             char c = json[idx];
//             if (c == '\\' && idx + 1 < json.Length)
//             {
//                 char n = json[++idx];
//                 sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
//                 idx++; continue;
//             }
//             if (c == '"') break;
//             sb.Append(c); idx++;
//         }
//         return sb.ToString().Trim();
//     }

//     static string JsonEscape(string s) =>
//         "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
//                 .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
// }



// using UnityEngine;
// using UnityEditor;
// using System.IO;
// using System.Collections.Generic;
// using System.Text;
// using System.Threading.Tasks;
// using UnityEngine.Networking;

// /// <summary>
// /// Tools > Groq Part Description Generator
// ///
// /// Usage:
// ///   1. Drop your GLB / prefab into the "3D Model" field
// ///   2. Type what it is (e.g. "V8 Twin Turbo engine", "Boeing CFM56 jet engine")
// ///   3. Click Generate
// ///
// /// The tool auto-finds the matching EngineData + Parts folder,
// /// extracts real mesh geometry (position, size, shape, vertices, material),
// /// sends everything to Groq llama-3.3-70b, and writes accurate names +
// /// descriptions back into every PartData asset automatically.
// /// </summary>
// public class GroqPartDescriptionGenerator : EditorWindow
// {
//     private const string GroqEndpoint   = "https://api.groq.com/openai/v1/chat/completions";
//     private const string Model          = "llama-3.3-70b-versatile";
//     private const string PrefKeyApi     = "GroqAPIKey";
//     private const string PrefKeyType    = "GroqModelType";

//     private string      _apiKey     = "";
//     private string      _modelType  = "";
//     private GameObject  _droppedModel;
//     private string      _status     = "";
//     private bool        _running    = false;
//     private Vector2     _scroll;

//     [MenuItem("Tools/Groq Part Description Generator")]
//     public static void Open() => GetWindow<GroqPartDescriptionGenerator>("Groq AI Descriptions");

//     void OnEnable()
//     {
//         _apiKey    = EditorPrefs.GetString(PrefKeyApi,  "");
//         _modelType = EditorPrefs.GetString(PrefKeyType, "");
//     }

//     void OnGUI()
//     {
//         GUILayout.Label("Groq AI — Part Description Generator", EditorStyles.boldLabel);
//         EditorGUILayout.Space(6);

//         // ── API Key ───────────────────────────────────────────────────────────
//         EditorGUILayout.LabelField("Groq API Key  (saved locally, never committed)");
//         string newKey = EditorGUILayout.PasswordField(_apiKey);
//         if (newKey != _apiKey) { _apiKey = newKey; EditorPrefs.SetString(PrefKeyApi, _apiKey); }

//         EditorGUILayout.Space(6);

//         // ── Drop model ────────────────────────────────────────────────────────
//         var newModel = (GameObject)EditorGUILayout.ObjectField(
//             "3D Model (GLB / Prefab)", _droppedModel, typeof(GameObject), false);
//         if (newModel != _droppedModel)
//         {
//             _droppedModel = newModel;
//             // Auto-fill model type from asset name if field is empty
//             if (_droppedModel != null && string.IsNullOrEmpty(_modelType))
//             {
//                 _modelType = _droppedModel.name.Replace("_", " ").Replace("-", " ");
//                 EditorPrefs.SetString(PrefKeyType, _modelType);
//             }
//         }

//         // ── Model type ────────────────────────────────────────────────────────
//         EditorGUILayout.Space(2);
//         string newType = EditorGUILayout.TextField("What is this model?", _modelType);
//         if (newType != _modelType) { _modelType = newType; EditorPrefs.SetString(PrefKeyType, _modelType); }

//         EditorGUILayout.HelpBox(
//             "Be specific for best accuracy.\n" +
//             "Examples:\n" +
//             "  • V8 Twin Turbo gasoline engine\n" +
//             "  • F6 Boxer engine (Porsche 911)\n" +
//             "  • CFM56 Turbofan jet engine\n" +
//             "  • Ferrari 458 Italia V8 engine\n" +
//             "  • Diesel truck engine",
//             MessageType.Info);

//         EditorGUILayout.Space(8);

//         // ── Generate button ───────────────────────────────────────────────────
//         bool canRun = !_running
//                    && !string.IsNullOrEmpty(_apiKey)
//                    && _droppedModel != null
//                    && !string.IsNullOrEmpty(_modelType);

//         GUI.enabled = canRun;
//         if (GUILayout.Button("Generate All Descriptions  (AI)", GUILayout.Height(44)))
//             RunGeneration();
//         GUI.enabled = true;

//         if (_droppedModel == null)
//             EditorGUILayout.HelpBox("Drop a GLB or Prefab above to begin.", MessageType.Warning);
//         else if (string.IsNullOrEmpty(_modelType))
//             EditorGUILayout.HelpBox("Describe what this model is.", MessageType.Warning);

//         EditorGUILayout.Space(6);

//         // ── Status ────────────────────────────────────────────────────────────
//         if (!string.IsNullOrEmpty(_status))
//         {
//             _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(160));
//             EditorGUILayout.HelpBox(_status, _running ? MessageType.Info : MessageType.None);
//             EditorGUILayout.EndScrollView();
//         }

//         EditorGUILayout.Space(2);
//         EditorGUILayout.LabelField("Get free API key → console.groq.com", EditorStyles.miniLabel);
//     }

//     // ─────────────────────────────────────────────────────────────────────────

//     async void RunGeneration()
//     {
//         _running = true;
//         _status  = "Locating Parts folder for this model...";
//         Repaint();

//         // ── Find the Parts folder that belongs to this prefab ─────────────────
//         string partsFolder = FindPartsFolderForModel(_droppedModel);
//         if (partsFolder == null)
//         {
//             _status  = "ERROR: Could not find a Parts folder for this model.\n\n" +
//                        "Make sure you ran  Tools → Engine Part Setup  on this model first.\n" +
//                        "That tool creates the Parts/ folder and PartData assets automatically.";
//             _running = false; Repaint(); return;
//         }

//         _status = $"Parts folder found:\n{partsFolder}\n\nLoading PartData assets...";
//         Repaint();

//         // ── Load PartData assets ──────────────────────────────────────────────
//         var guids = AssetDatabase.FindAssets("t:PartData", new[] { partsFolder });
//         if (guids.Length == 0)
//         {
//             _status  = $"No PartData assets found in:\n{partsFolder}\n\nRun Engine Part Setup first.";
//             _running = false; Repaint(); return;
//         }

//         var parts = new List<PartData>();
//         foreach (var g in guids)
//         {
//             var pd = AssetDatabase.LoadAssetAtPath<PartData>(AssetDatabase.GUIDToAssetPath(g));
//             if (pd != null) parts.Add(pd);
//         }

//         _status = $"Found {parts.Count} parts.\nExtracting mesh geometry...";
//         Repaint();

//         // ── Extract geometry from the prefab ──────────────────────────────────
//         var geoData = ExtractGeometry(_droppedModel, parts);

//         _status = $"Geometry extracted.\nSending {parts.Count} parts to Groq AI...";
//         Repaint();

//         // ── Call Groq ─────────────────────────────────────────────────────────
//         string prompt  = BuildPrompt(geoData, _modelType);
//         var    results = await CallGroq(prompt);

//         if (results == null || results.Count == 0)
//         {
//             _status  = "ERROR: Groq request failed or returned empty response.\n" +
//                        "Check your API key and internet connection.\nSee Console for details.";
//             _running = false; Repaint(); return;
//         }

//         // ── Write results back ────────────────────────────────────────────────
//         int applied = 0;
//         for (int i = 0; i < parts.Count && i < results.Count; i++)
//         {
//             if (!string.IsNullOrEmpty(results[i].name))
//                 parts[i].partName = results[i].name;
//             if (!string.IsNullOrEmpty(results[i].description))
//                 parts[i].description = results[i].description;
//             EditorUtility.SetDirty(parts[i]);
//             applied++;
//         }

//         AssetDatabase.SaveAssets();
//         AssetDatabase.Refresh();

//         _status  = $"✓ Done!  {applied} / {parts.Count} parts updated.\n\n" +
//                    $"All PartData assets saved to:\n{partsFolder}";
//         _running = false;
//         Repaint();
//     }

//     // ── Auto-find Parts folder ────────────────────────────────────────────────

//     /// <summary>
//     /// Searches all EngineData assets in the project for one whose enginePrefab
//     /// matches the dropped model, then returns its sibling Parts/ folder.
//     /// Falls back to searching by prefab name if no exact match found.
//     /// </summary>
//     static string FindPartsFolderForModel(GameObject model)
//     {
//         string modelAssetPath = AssetDatabase.GetAssetPath(model);
//         string modelName      = model.name;
//         string modelNorm      = Normalize(modelName);

//         // Search all EngineData assets
//         string[] edGuids = AssetDatabase.FindAssets("t:EngineData");
//         foreach (var guid in edGuids)
//         {
//             var ed = AssetDatabase.LoadAssetAtPath<EngineData>(AssetDatabase.GUIDToAssetPath(guid));
//             if (ed == null) continue;

//             bool match = false;

//             // Exact prefab reference match
//             if (ed.enginePrefab != null)
//                 match = AssetDatabase.GetAssetPath(ed.enginePrefab) == modelAssetPath;

//             // Name-based fallback — normalize spaces, underscores, hyphens
//             if (!match && ed.engineName != null)
//                 match = Normalize(ed.engineName) == modelNorm;

//             if (match)
//             {
//                 string edDir     = Path.GetDirectoryName(AssetDatabase.GetAssetPath(ed)).Replace("\\", "/");
//                 string partsPath = edDir + "/Parts";
//                 if (Directory.Exists(partsPath)) return partsPath;
//             }
//         }

//         // Fallback: search all Parts folders whose engine folder name matches the model name
//         string[] allDirs = Directory.GetDirectories("Assets", "Parts", SearchOption.AllDirectories);
//         foreach (var dir in allDirs)
//         {
//             string unityDir    = dir.Replace("\\", "/");
//             string engineFolder = Path.GetFileName(Path.GetDirectoryName(unityDir));
//             if (Normalize(engineFolder) == modelNorm)
//                 return unityDir;
//         }

//         // Last resort: any Parts folder under an engine folder whose name contains the model name
//         foreach (var dir in allDirs)
//         {
//             string unityDir     = dir.Replace("\\", "/");
//             string engineFolder = Path.GetFileName(Path.GetDirectoryName(unityDir));
//             if (Normalize(engineFolder).Contains(modelNorm) || modelNorm.Contains(Normalize(engineFolder)))
//                 return unityDir;
//         }

//         return null;
//     }

//     /// <summary>Lowercases and strips spaces, underscores, and hyphens for fuzzy name matching.</summary>
//     static string Normalize(string s) =>
//         s.Replace(" ", "").Replace("_", "").Replace("-", "").ToLower();

//     // ── Geometry Extraction ───────────────────────────────────────────────────

//     struct PartGeo
//     {
//         public string rawName;
//         public string position;   // e.g. "top-center-front"
//         public string size;       // large / medium / small / tiny
//         public string shape;      // complex / elongated / thin-flat / compact
//         public int    vertices;
//         public string material;
//     }

//     static List<PartGeo> ExtractGeometry(GameObject prefab, List<PartData> parts)
//     {
//         var result   = new List<PartGeo>();
//         var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
//         instance.hideFlags = HideFlags.HideAndDontSave;

//         try
//         {
//             // Overall engine bounds
//             var allR = instance.GetComponentsInChildren<Renderer>(true);
//             var engBounds = new Bounds(instance.transform.position, Vector3.zero);
//             foreach (var r in allR) engBounds.Encapsulate(r.bounds);
//             Vector3 ec = engBounds.center;
//             Vector3 es = engBounds.size;

//             // Name → (MeshFilter, Renderer) lookup
//             var lookup = new Dictionary<string, (MeshFilter mf, Renderer rend)>();
//             foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
//                 if (!lookup.ContainsKey(mf.gameObject.name))
//                     lookup[mf.gameObject.name] = (mf, mf.GetComponent<Renderer>());

//             foreach (var pd in parts)
//             {
//                 var geo = new PartGeo { rawName = pd.partName };

//                 if (lookup.TryGetValue(pd.partName, out var entry)
//                     && entry.mf != null && entry.mf.sharedMesh != null)
//                 {
//                     var mesh = entry.mf.sharedMesh;
//                     var rend = entry.rend;
//                     var b    = rend != null ? rend.bounds : new Bounds(entry.mf.transform.position, Vector3.zero);

//                     // Relative position
//                     Vector3 rel = b.center - ec;
//                     float rx = es.x > 0 ? rel.x / (es.x * 0.5f) : 0;
//                     float ry = es.y > 0 ? rel.y / (es.y * 0.5f) : 0;
//                     float rz = es.z > 0 ? rel.z / (es.z * 0.5f) : 0;
//                     string px = rx >  0.3f ? "right"  : rx < -0.3f ? "left"   : "center";
//                     string py = ry >  0.3f ? "top"    : ry < -0.3f ? "bottom" : "middle";
//                     string pz = rz >  0.3f ? "front"  : rz < -0.3f ? "rear"   : "center";
//                     geo.position = $"{py}-{px}-{pz}";

//                     // Relative size
//                     float pv   = b.size.x * b.size.y * b.size.z;
//                     float ev   = es.x * es.y * es.z;
//                     float rat  = ev > 0 ? pv / ev : 0;
//                     geo.size   = rat > 0.15f ? "large" : rat > 0.04f ? "medium" : rat > 0.008f ? "small" : "tiny";

//                     // Shape
//                     float maxD = Mathf.Max(b.size.x, b.size.y, b.size.z);
//                     float minD = Mathf.Min(b.size.x, b.size.y, b.size.z);
//                     float asp  = maxD > 0 ? minD / maxD : 1;
//                     geo.shape  = asp < 0.15f ? "thin-flat"
//                                : asp < 0.4f  ? "elongated"
//                                : mesh.vertexCount > 2000 ? "complex" : "compact";

//                     geo.vertices = mesh.vertexCount;

//                     // Material hint
//                     if (rend != null && rend.sharedMaterial != null)
//                     {
//                         string mn = rend.sharedMaterial.name
//                             .Replace("(Instance)", "").Replace("_", " ").Trim();
//                         geo.material = mn.Length > 35 ? mn.Substring(0, 35) : mn;
//                     }
//                 }
//                 else
//                 {
//                     geo.position = "unknown";
//                     geo.size     = "unknown";
//                     geo.shape    = "unknown";
//                 }

//                 result.Add(geo);
//             }
//         }
//         finally { DestroyImmediate(instance); }

//         return result;
//     }

//     // ── Prompt ────────────────────────────────────────────────────────────────

//     static string BuildPrompt(List<PartGeo> geoData, string modelType)
//     {
//         var sb = new StringBuilder();
//         sb.AppendLine("You are an expert mechanical engineer and technical writer.");
//         sb.AppendLine($"You are analyzing a 3D model of: {modelType}");
//         sb.AppendLine();
//         sb.AppendLine("Each part below has geometric data extracted from the actual 3D mesh:");
//         sb.AppendLine("  position = location relative to model center (vertical-horizontal-depth)");
//         sb.AppendLine("  size     = relative to whole model (large/medium/small/tiny)");
//         sb.AppendLine("  shape    = mesh shape classification");
//         sb.AppendLine("  vertices = mesh complexity");
//         sb.AppendLine("  material = material name hint from the 3D file");
//         sb.AppendLine();
//         sb.AppendLine("PARTS:");

//         for (int i = 0; i < geoData.Count; i++)
//         {
//             var g = geoData[i];
//             sb.Append($"{i + 1}.");
//             sb.Append($" position={g.position}");
//             sb.Append($" size={g.size}");
//             sb.Append($" shape={g.shape}");
//             sb.Append($" vertices={g.vertices}");
//             if (!string.IsNullOrEmpty(g.material))
//                 sb.Append($" material=\"{g.material}\"");
//             sb.AppendLine();
//         }

//         sb.AppendLine();
//         sb.AppendLine($"Return a JSON array of exactly {geoData.Count} objects.");
//         sb.AppendLine("Each object: {\"name\": \"Part Name\", \"description\": \"2-3 sentence technical description.\"}");
//         sb.AppendLine();
//         sb.AppendLine("Rules:");
//         sb.AppendLine("- Every part name must be UNIQUE — no duplicates");
//         sb.AppendLine("- Names must be real mechanical part names for this specific model type");
//         sb.AppendLine("- Descriptions must be technically accurate and educational");
//         sb.AppendLine("- Use geometry data to reason: large+top+complex = main block/head, tiny+scattered = bolts/sensors, thin-flat = gaskets/shields, bottom = oil pan/sump, rear = flywheel/clutch");
//         sb.AppendLine("- Return ONLY the raw JSON array, no markdown, no explanation");

//         return sb.ToString();
//     }

//     // ── Groq API ──────────────────────────────────────────────────────────────

//     async Task<List<(string name, string description)>> CallGroq(string prompt)
//     {
//         string body = "{\"model\":\"" + Model + "\"," +
//                       "\"messages\":[{\"role\":\"user\",\"content\":" + JsonEscape(prompt) + "}]," +
//                       "\"temperature\":0.2,\"max_tokens\":6000}";
//         try
//         {
//             using var req = new UnityWebRequest(GroqEndpoint, "POST");
//             req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
//             req.downloadHandler = new DownloadHandlerBuffer();
//             req.SetRequestHeader("Content-Type",  "application/json");
//             req.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

//             var op = req.SendWebRequest();
//             while (!op.isDone) await Task.Yield();

//             if (req.result != UnityWebRequest.Result.Success)
//             {
//                 Debug.LogError($"[Groq] {req.error}\n{req.downloadHandler.text}");
//                 return null;
//             }

//             string content = ExtractContent(req.downloadHandler.text);
//             Debug.Log($"[Groq] Response:\n{content}");
//             return string.IsNullOrEmpty(content) ? null : ParseArray(content);
//         }
//         catch (System.Exception e) { Debug.LogError($"[Groq] {e.Message}"); return null; }
//     }

//     // ── JSON Helpers ──────────────────────────────────────────────────────────

//     static List<(string, string)> ParseArray(string content)
//     {
//         var list  = new List<(string, string)>();
//         int start = content.IndexOf('[');
//         int end   = content.LastIndexOf(']');
//         if (start < 0 || end <= start) return null;

//         string json = content.Substring(start, end - start + 1);
//         int i = 0;
//         while (i < json.Length)
//         {
//             int os = json.IndexOf('{', i); if (os < 0) break;
//             int oe = FindBrace(json, os);  if (oe < 0) break;
//             string obj = json.Substring(os, oe - os + 1);
//             list.Add((ReadStr(obj, "name") ?? "", ReadStr(obj, "description") ?? ""));
//             i = oe + 1;
//         }
//         return list;
//     }

//     static int FindBrace(string s, int from)
//     {
//         int d = 0; bool inS = false;
//         for (int i = from; i < s.Length; i++)
//         {
//             char c = s[i];
//             if (c == '\\' && inS) { i++; continue; }
//             if (c == '"') { inS = !inS; continue; }
//             if (inS) continue;
//             if (c == '{') d++; else if (c == '}') { d--; if (d == 0) return i; }
//         }
//         return -1;
//     }

//     static string ExtractContent(string resp)
//     {
//         int idx = resp.IndexOf("\"content\":");
//         if (idx < 0) return null;
//         idx += 10;
//         while (idx < resp.Length && resp[idx] != '"') idx++;
//         if (idx >= resp.Length) return null;
//         idx++;
//         var sb = new StringBuilder();
//         while (idx < resp.Length)
//         {
//             char c = resp[idx];
//             if (c == '\\' && idx + 1 < resp.Length)
//             {
//                 char n = resp[++idx];
//                 sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
//                 idx++; continue;
//             }
//             if (c == '"') break;
//             sb.Append(c); idx++;
//         }
//         return sb.ToString();
//     }

//     static string ReadStr(string json, string key)
//     {
//         int idx = json.IndexOf($"\"{key}\"");
//         if (idx < 0) return null;
//         idx += key.Length + 2;
//         while (idx < json.Length && json[idx] != '"') idx++;
//         if (idx >= json.Length) return null;
//         idx++;
//         var sb = new StringBuilder();
//         while (idx < json.Length)
//         {
//             char c = json[idx];
//             if (c == '\\' && idx + 1 < json.Length)
//             {
//                 char n = json[++idx];
//                 sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
//                 idx++; continue;
//             }
//             if (c == '"') break;
//             sb.Append(c); idx++;
//         }
//         return sb.ToString().Trim();
//     }

//     static string JsonEscape(string s) =>
//         "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
//                 .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
// }





// Updated Claude code
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

/// <summary>
/// Tools > Groq Part Description Generator
///
/// Usage:
///   1. Drop your GLB / prefab into the "3D Model" field
///   2. Type what it is (e.g. "V8 Twin Turbo engine", "Boeing CFM56 jet engine")
///   3. Click Generate
///
/// Improvements over v1:
///   - Two-pass generation: Pass 1 names parts, Pass 2 writes descriptions
///   - Raw mesh name sent to AI as strong hint (biggest accuracy win)
///   - Mesh names cleaned/normalized before sending
///   - Neighbor proximity context (nearby tiny parts = bolts/studs)
///   - Duplicate name detection and auto-suffix resolution
///   - Markdown fence stripping before JSON parse
///   - Result count validation before any asset write (no partial corruption)
///   - 30-second request timeout
///   - Cancellation support (Cancel button during generation)
///   - Chunked batching for models with 30+ parts
///   - Pre-write backup to ProjectSettings/GroqBackup/
///   - Per-part progress reporting in status log
/// </summary>
public class GroqPartDescriptionGenerator : EditorWindow
{
    private const string GroqEndpoint  = "https://api.groq.com/openai/v1/chat/completions";
    private const string Model         = "llama-3.3-70b-versatile";
    private const string VisionModel   = "qwen/qwen3.6-27b";
    private const string PrefKeyApi    = "GroqAPIKey";
    private const string PrefKeyType   = "GroqModelType";
    private const int    BatchSize         = 5;     // max parts per API call — keeps each request under ~2500 tokens
    private const int    TimeoutSecs       = 30;
    private const int    MinBatchDelayMs   = 5000;  // minimum inter-batch wait (text, few batches)
    private const int    MaxBatchDelayMs   = 25000; // cap for text mode
    private const int    MinVisionDelayMs  = 15000; // minimum inter-batch wait (vision)
    private const int    MaxVisionDelayMs  = 60000; // cap for vision mode
    private const float  MinResultRatio    = 0.8f;

    // ── State ─────────────────────────────────────────────────────────────────
    private string     _apiKey             = "";
    private string     _modelType          = "";
    private string     _partsFolderOverride = "";
    private GameObject _droppedModel;
    private string     _status             = "";
    private bool       _running            = false;
    private Vector2    _scroll;
    private bool       _useVision          = false;
    private bool       _smartChunking      = true;
    private int        _chunkSize          = 20;
    private bool       _smartSkipCompleted = true;

    // Progress tracking
    private float      _generationProgress = 0f;
    private string     _progressText       = "";

    // Caching styles
    private GUIStyle   _titleStyle;
    private GUIStyle   _boldHeaderStyle;
    private GUIStyle   _boxStyle;

    // Part selection via hierarchy
    private List<HierarchyNode> _hierarchyNodes = new List<HierarchyNode>();
    private HashSet<string>     _selectedPartNames = new HashSet<string>();
    private string              _additionalContext = "";
    private Vector2             _hierarchyScroll;
    private bool                _hierarchyExpanded = true;
    private bool                _contextExpanded   = true;

    private CancellationTokenSource _cts;

    /// <summary>
    /// Represents one transform node in the prefab hierarchy for the selection UI.
    /// Mirrors the Unity Hierarchy window structure.
    /// </summary>
    private class HierarchyNode
    {
        public string              name;
        public int                 depth;           // 0 = root, 1 = first-level child, etc.
        public bool                isSelected;      // checkbox state
        public bool                hasPartData;     // true = this node maps to a PartData asset
        public bool                isLeaf;          // no children
        public List<HierarchyNode> children = new List<HierarchyNode>();
    }

    // ── Menu ──────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Groq Part Description Generator")]
    public static void Open() => GetWindow<GroqPartDescriptionGenerator>("Groq AI Descriptions");

    void OnEnable()
    {
        _apiKey    = EditorPrefs.GetString(PrefKeyApi,  "");
        _modelType = EditorPrefs.GetString(PrefKeyType, "");
        _partsFolderOverride = EditorPrefs.GetString("GroqPartsFolderOverride", "");
        _useVision          = EditorPrefs.GetBool("GroqUseVision", false);
        _smartChunking      = EditorPrefs.GetBool("GroqSmartChunking", true);
        _chunkSize          = EditorPrefs.GetInt("GroqChunkSize", 20);
        _smartSkipCompleted = EditorPrefs.GetBool("GroqSmartSkipCompleted", true);
    }

    void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    // ── Hierarchy builder ────────────────────────────────────────────────────
    /// <summary>
    /// Builds a tree of HierarchyNode from the prefab's transform hierarchy,
    /// marking nodes that match PartData names. Only leaf transforms with
    /// mesh renderers are checkable.
    /// </summary>
    void BuildHierarchyFromPrefab()
    {
        _hierarchyNodes.Clear();
        _selectedPartNames.Clear();

        if (_droppedModel == null) return;

        // Instantiate to inspect children
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(_droppedModel);
        instance.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            // Collect all renderers — these are the "real" parts
            var renderers = new HashSet<Transform>();
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                renderers.Add(r.transform);

            // Collect all PartData names from the asset folder for this model
            var partDataNames = new HashSet<string>();
            string partsFolder = !string.IsNullOrEmpty(_partsFolderOverride) && Directory.Exists(_partsFolderOverride)
                ? _partsFolderOverride
                : FindPartsFolderForModel(_droppedModel);

            if (partsFolder != null && Directory.Exists(partsFolder))
            {
                var guids = AssetDatabase.FindAssets("t:PartData", new[] { partsFolder });
                foreach (var g in guids)
                {
                    var pd = AssetDatabase.LoadAssetAtPath<PartData>(AssetDatabase.GUIDToAssetPath(g));
                    if (pd != null) partDataNames.Add(pd.partName);
                }
            }

            // Recursively build the tree
            Transform root = instance.transform;
            var rootNode = new HierarchyNode
            {
                name      = root.name,
                depth     = 0,
                isLeaf    = root.childCount == 0,
                hasPartData = renderers.Contains(root) || partDataNames.Contains(root.name)
            };
            rootNode.isSelected = rootNode.hasPartData; // pre-select if it has part data
            BuildNodeChildren(rootNode, root, 1, renderers, partDataNames);
            _hierarchyNodes.Add(rootNode);

            // Populate _selectedPartNames from initial selections
            UpdateSelectionFromHierarchy();
        }
        finally { DestroyImmediate(instance); }
    }

    void BuildNodeChildren(HierarchyNode parentNode, Transform parentT, int depth,
        HashSet<Transform> renderers, HashSet<string> partDataNames)
    {
        foreach (Transform child in parentT)
        {
            bool isLeaf   = child.childCount == 0;
            bool hasMesh  = renderers.Contains(child);
            var node = new HierarchyNode
            {
                name      = child.name,
                depth     = depth,
                isLeaf    = isLeaf,
                hasPartData = hasMesh || partDataNames.Contains(child.name)
            };

            // Auto-select if this node has PartData
            node.isSelected = node.hasPartData;

            // Recurse children
            if (child.childCount > 0)
                BuildNodeChildren(node, child, depth + 1, renderers, partDataNames);

            parentNode.children.Add(node);
        }
    }

    /// <summary>
    /// Walks the hierarchy tree and syncs _selectedPartNames from selected leaf nodes.
    /// </summary>
    void UpdateSelectionFromHierarchy()
    {
        _selectedPartNames.Clear();
        CollectSelectedLeaves(_hierarchyNodes, _selectedPartNames);
    }

    void CollectSelectedLeaves(List<HierarchyNode> nodes, HashSet<string> selected)
    {
        foreach (var node in nodes)
        {
            if (node.isSelected && node.hasPartData)
            {
                selected.Add(node.name);
            }
            if (node.children.Count > 0)
            {
                CollectSelectedLeaves(node.children, selected);
            }
        }
    }

    // ── Hierarchy tree drawing ──────────────────────────────────────────────
    void DrawHierarchyNode(List<HierarchyNode> nodes, int depth)
    {
        foreach (var node in nodes)
        {
            if (node.children.Count > 0)
            {
                // Group node — draw as foldout header with optional checkbox
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(depth * 16);
                bool hasSelectableLeaf = HasSelectableLeaf(node);
                if (hasSelectableLeaf)
                {
                    bool newSel = EditorGUILayout.Toggle(node.isSelected, GUILayout.Width(16));
                    if (newSel != node.isSelected)
                    {
                        node.isSelected = newSel;
                        SetChildrenSelection(node, newSel);
                        UpdateSelectionFromHierarchy();
                    }
                }
                else
                {
                    GUILayout.Space(20);
                }
                string icon = node.hasPartData ? "📦 " : "📁 ";
                string label = node.name;
                if (node.hasPartData)
                    label += "  (PartData)";
                GUILayout.Label(icon + label);
                EditorGUILayout.EndHorizontal();

                DrawHierarchyNode(node.children, depth + 1);
            }
            else
            {
                // Leaf node — draw as checkbox
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(depth * 16);

                Color orig = GUI.contentColor;
                if (node.hasPartData)
                    GUI.contentColor = node.isSelected ? Color.green : Color.gray;
                else
                    GUI.contentColor = Color.gray;

                bool prev = node.isSelected;
                bool next = EditorGUILayout.Toggle(prev, GUILayout.Width(16));
                if (next != prev)
                {
                    node.isSelected = next;
                    UpdateSelectionFromHierarchy();
                }

                string label = node.name;
                if (node.hasPartData)
                    label += "  (PartData)";
                GUILayout.Label(label);
                GUI.contentColor = orig;
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    bool HasSelectableLeaf(HierarchyNode node)
    {
        if (node.hasPartData) return true;
        foreach (var c in node.children)
            if (HasSelectableLeaf(c)) return true;
        return false;
    }

    void SetChildrenSelection(HierarchyNode node, bool selected)
    {
        node.isSelected = selected;
        foreach (var c in node.children)
        {
            SetChildrenSelection(c, selected);
        }
    }

    void SetAllHierarchySelection(bool selected)
    {
        foreach (var node in _hierarchyNodes)
            SetChildrenSelection(node, selected);
        UpdateSelectionFromHierarchy();
    }

    // ── GUI ───────────────────────────────────────────────────────────────────
    // ── GUI ───────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        // Initialize Custom Styling
        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 10, 10)
            };
            _titleStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.7f, 1f) : new Color(0f, 0.3f, 0.6f);

            _boldHeaderStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };

            _boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };
        }

        // Draw Title Banner
        EditorGUILayout.Space(4);
        GUILayout.Label("🚀 GROQ AI — Part Description Generator", _titleStyle);
        EditorGUILayout.Space(6);

        // Section 1: Credentials & Model Configuration
        EditorGUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("⚙️ Configuration", _boldHeaderStyle);
        EditorGUILayout.Space(4);

        // API Key (Password style field)
        _apiKey = EditorGUILayout.PasswordField("Groq API Key", _apiKey);
        if (GUI.changed) EditorPrefs.SetString(PrefKeyApi, _apiKey);
        EditorGUILayout.Space(2);

        // Model Category / Description
        string newType = EditorGUILayout.TextField("Model Type/Category", _modelType);
        if (newType != _modelType) { _modelType = newType; EditorPrefs.SetString(PrefKeyType, _modelType); }
        EditorGUILayout.Space(2);

        // Vision Toggle
        bool newUseVision = EditorGUILayout.Toggle("Use Vision (AI Screenshots)", _useVision);
        if (newUseVision != _useVision)
        {
            _useVision = newUseVision;
            EditorPrefs.SetBool("GroqUseVision", _useVision);
        }
        EditorGUILayout.Space(2);

        // Smart Chunking & Smart Resume Controls
        EditorGUILayout.BeginHorizontal();
        bool newChunking = EditorGUILayout.Toggle("Smart Chunking (Auto-split)", _smartChunking);
        if (newChunking != _smartChunking)
        {
            _smartChunking = newChunking;
            EditorPrefs.SetBool("GroqSmartChunking", _smartChunking);
        }
        if (_smartChunking)
        {
            int newChunkSize = EditorGUILayout.IntSlider("Chunk Size", _chunkSize, 10, 50);
            if (newChunkSize != _chunkSize)
            {
                _chunkSize = newChunkSize;
                EditorPrefs.SetInt("GroqChunkSize", _chunkSize);
            }
        }
        EditorGUILayout.EndHorizontal();

        bool newSkip = EditorGUILayout.Toggle("Smart Resume (Skip Completed)", _smartSkipCompleted);
        if (newSkip != _smartSkipCompleted)
        {
            _smartSkipCompleted = newSkip;
            EditorPrefs.SetBool("GroqSmartSkipCompleted", _smartSkipCompleted);
        }
        EditorGUILayout.Space(2);

        // Drop Model Field
        var newModel = (GameObject)EditorGUILayout.ObjectField("3D Model Prefab/GLB", _droppedModel, typeof(GameObject), false);
        if (newModel != _droppedModel)
        {
            _droppedModel = newModel;
            if (_droppedModel != null)
            {
                if (string.IsNullOrEmpty(_modelType))
                {
                    _modelType = _droppedModel.name.Replace("_", " ").Replace("-", " ");
                    EditorPrefs.SetString(PrefKeyType, _modelType);
                }

                // Auto-detect parts folder and set the override field immediately
                string detectedFolder = FindPartsFolderForModel(_droppedModel);
                if (!string.IsNullOrEmpty(detectedFolder))
                {
                    _partsFolderOverride = detectedFolder;
                    EditorPrefs.SetString("GroqPartsFolderOverride", _partsFolderOverride);
                }
                else
                {
                    _partsFolderOverride = "";
                    EditorPrefs.SetString("GroqPartsFolderOverride", "");
                }
            }
            BuildHierarchyFromPrefab();
        }
        EditorGUILayout.Space(2);

        // Folder Override
        EditorGUILayout.BeginHorizontal();
        string newFolder = EditorGUILayout.TextField("Parts Folder Override", _partsFolderOverride);
        if (newFolder != _partsFolderOverride)
        {
            _partsFolderOverride = newFolder;
            EditorPrefs.SetString("GroqPartsFolderOverride", _partsFolderOverride);
        }
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("Select Parts Folder containing PartData", "C:/Users/ADMIN/Desktop/Debojit/EngineVR Simulation/EngineVRSimulation/Assets/ScriptableObjects/Data/Engines", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                    _partsFolderOverride = "Assets" + path.Substring(Application.dataPath.Length);
                else
                    _partsFolderOverride = path;
                EditorPrefs.SetString("GroqPartsFolderOverride", _partsFolderOverride);
                BuildHierarchyFromPrefab(); // Rebuild hierarchy when folder changes
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // Section 2: Generation Setup & Controls
        EditorGUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("🤖 Run Generator", _boldHeaderStyle);
        EditorGUILayout.Space(4);

        // Status Warnings
        if (_droppedModel == null)
            EditorGUILayout.HelpBox("Please drag and drop your 3D Model asset to populate the parts hierarchy.", MessageType.Warning);
        else if (string.IsNullOrEmpty(_partsFolderOverride) || !Directory.Exists(_partsFolderOverride))
            EditorGUILayout.HelpBox("Could not auto-detect a valid Parts folder. Please locate it manually using the field below.", MessageType.Warning);
        else if (string.IsNullOrEmpty(_modelType))
            EditorGUILayout.HelpBox("Please enter a category name (e.g. 'Jet Engine' or 'V8 Engine') to guide the AI.", MessageType.Warning);
        else if (string.IsNullOrEmpty(_apiKey))
            EditorGUILayout.HelpBox("Please enter your Groq API key to enable generation.", MessageType.Warning);
        else
            EditorGUILayout.HelpBox($"Ready to process! Select parts below and click Generate. It will request descriptions for {_selectedPartNames.Count} parts.", MessageType.Info);

        EditorGUILayout.Space(4);

        // Buttons Row
        bool canRun = !_running
                   && !string.IsNullOrEmpty(_apiKey)
                   && _droppedModel != null
                   && !string.IsNullOrEmpty(_partsFolderOverride)
                   && Directory.Exists(_partsFolderOverride)
                   && !string.IsNullOrEmpty(_modelType);

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = canRun;
        if (GUILayout.Button("⚡ Generate All Descriptions", GUILayout.Height(36)))
        {
            _ = RunGeneration();
        }
        GUI.enabled = true;

        if (GUILayout.Button("🏷️ Rename GameObjects", GUILayout.Height(36), GUILayout.Width(170)))
        {
            RenameGameObjectsToPartNamesTool.Open();
        }

        if (_running)
        {
            if (GUILayout.Button("🛑 Cancel", GUILayout.Height(36), GUILayout.Width(100)))
            {
                _cts?.Cancel();
                _status = "Cancelling...";
                _running = false;
                Repaint();
            }
        }
        EditorGUILayout.EndHorizontal();

        // Progress Bar
        if (_running)
        {
            EditorGUILayout.Space(6);
            Rect r = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(r, _generationProgress, _progressText);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // Section 3: Hierarchy Part Selection Tree
        if (_droppedModel != null && _hierarchyNodes.Count > 0)
        {
            EditorGUILayout.BeginVertical(_boxStyle);
            _hierarchyExpanded = EditorGUILayout.Foldout(_hierarchyExpanded,
                $"📋 Part Selection  ({_selectedPartNames.Count} selected)", true, EditorStyles.foldoutHeader);
            if (_hierarchyExpanded)
            {
                EditorGUILayout.Space(2);
                _hierarchyScroll = EditorGUILayout.BeginScrollView(_hierarchyScroll,
                    GUILayout.MaxHeight(220), GUILayout.ExpandHeight(false));
                EditorGUI.indentLevel = 0;
                DrawHierarchyNode(_hierarchyNodes, 0);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space(4);

                // Select/Deselect buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All", EditorStyles.miniButtonLeft))
                    SetAllHierarchySelection(true);
                if (GUILayout.Button("Deselect All", EditorStyles.miniButtonRight))
                    SetAllHierarchySelection(false);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        // Section 4: Additional Context Notes
        EditorGUILayout.BeginVertical(_boxStyle);
        _contextExpanded = EditorGUILayout.Foldout(_contextExpanded, "💬 Additional Context / Notes", true, EditorStyles.foldoutHeader);
        if (_contextExpanded)
        {
            EditorGUILayout.Space(2);
            EditorGUI.BeginChangeCheck();
            string newCtx = EditorGUILayout.TextArea(_additionalContext, GUILayout.MinHeight(44));
            if (EditorGUI.EndChangeCheck()) _additionalContext = newCtx;
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // Section 5: Status Console / Logs
        if (!string.IsNullOrEmpty(_status))
        {
            EditorGUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("💻 Console Status Log", _boldHeaderStyle);
            EditorGUILayout.Space(2);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(120));
            EditorGUILayout.HelpBox(_status, _running ? MessageType.Info : MessageType.None);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("🔑 Get free API key → console.groq.com", EditorStyles.miniLabel);
    }

    // ── Main pipeline ─────────────────────────────────────────────────────────
    async Task RunGeneration()
    {
        _cts     = new CancellationTokenSource();
        _running = true;
        _generationProgress = 0f;
        _progressText = "Initializing...";
        _status  = "Locating Parts folder for this model...";
        Repaint();

        try
        {
            // Step 1 — Find Parts folder
            string partsFolder = !string.IsNullOrEmpty(_partsFolderOverride) && Directory.Exists(_partsFolderOverride)
                ? _partsFolderOverride
                : FindPartsFolderForModel(_droppedModel);

            if (partsFolder == null || !Directory.Exists(partsFolder))
            {
                // Prompt user to locate manually
                bool locate = EditorUtility.DisplayDialog(
                    "Parts Folder Required",
                    "Could not auto-detect a Parts folder for this model. Would you like to locate it manually?",
                    "Locate Folder", "Cancel"
                );

                if (locate)
                {
                    string path = EditorUtility.OpenFolderPanel("Select Parts Folder containing PartData", "C:/Users/ADMIN/Desktop/Debojit/EngineVR Simulation/EngineVRSimulation/Assets/ScriptableObjects/Data/Engines", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (path.StartsWith(Application.dataPath))
                            partsFolder = "Assets" + path.Substring(Application.dataPath.Length);
                        else
                            partsFolder = path;
                        
                        _partsFolderOverride = partsFolder;
                        EditorPrefs.SetString("GroqPartsFolderOverride", _partsFolderOverride);
                        
                        BuildHierarchyFromPrefab();
                    }
                }

                if (partsFolder == null || !Directory.Exists(partsFolder))
                {
                    _status = "ERROR: Could not find or assign a valid Parts folder. Operation aborted.";
                    _running = false;
                    _generationProgress = 0f;
                    _progressText = "";
                    Repaint();
                    return;
                }
            }

            _generationProgress = 0.02f;
            _progressText = "Loading PartData assets...";
            Log($"Parts folder:\n{partsFolder}\n\nLoading PartData assets...");

            // Step 2 — Load PartData assets
            var guids = AssetDatabase.FindAssets("t:PartData", new[] { partsFolder });
            if (guids.Length == 0)
            {
                _status = $"No PartData assets found in:\n{partsFolder}";
                return;
            }

            var allParts = new List<PartData>();
            foreach (var g in guids)
            {
                var pd = AssetDatabase.LoadAssetAtPath<PartData>(AssetDatabase.GUIDToAssetPath(g));
                if (pd != null) allParts.Add(pd);
            }

            // Filter to user-selected parts only
            List<PartData> parts;
            if (_selectedPartNames.Count > 0)
            {
                var normalizedSelected = new HashSet<string>();
                foreach (var name in _selectedPartNames)
                {
                    normalizedSelected.Add(Normalize(name));
                }
                parts = allParts.Where(p => p != null && normalizedSelected.Contains(Normalize(p.partName))).ToList();
                int skipped = allParts.Count - parts.Count;
                Log($"Found {allParts.Count} parts total. Using {parts.Count} selected parts ({skipped} skipped via hierarchy).");
            }
            else
            {
                parts = allParts;
                Log($"Found {parts.Count} parts.\n(Tip: deselect unwanted parts in the hierarchy panel above)");
            }

            if (parts.Count == 0)
            {
                _status = "No parts selected. Check some parts in the Part Selection tree and try again.";
                return;
            }

            int totalSelectedCount = parts.Count;

            // ── Smart Resume Filter ──────────────────────────────────────────────
            if (_smartSkipCompleted)
            {
                var uncompletedParts = parts.Where(p => !IsPartCompleted(p)).ToList();
                int alreadyDoneCount = parts.Count - uncompletedParts.Count;
                if (alreadyDoneCount > 0)
                {
                    Log($"⚡ [Smart Resume] Found {alreadyDoneCount} / {totalSelectedCount} parts already generated (skipped). Processing {uncompletedParts.Count} remaining un-generated parts.");
                    parts = uncompletedParts;
                }
            }

            if (parts.Count == 0)
            {
                _status = $"🎉 All {totalSelectedCount} selected parts already have completed names & descriptions!\n\n" +
                          $"(Uncheck 'Smart Resume' in configuration above if you want to overwrite existing descriptions).";
                _running = false;
                _generationProgress = 1.0f;
                _progressText = "Complete!";
                Repaint();
                return;
            }

            // Backup existing data before writing
            SaveBackup(parts, partsFolder);

            // ── Smart Chunking Execution ─────────────────────────────────────────
            int chunkSize = _smartChunking ? Mathf.Clamp(_chunkSize, 5, 50) : parts.Count;
            int totalChunks = Mathf.CeilToInt((float)parts.Count / chunkSize);

            Log($"🧩 [Smart Chunking] Processing {parts.Count} parts across {totalChunks} chunk(s) (Max {chunkSize} parts per chunk)...");

            int completedChunksCount = 0;
            int totalPartsSavedCount = 0;

            for (int c = 0; c < totalChunks; c++)
            {
                if (_cts.Token.IsCancellationRequested)
                {
                    _status = $"Cancelled. Saved {totalPartsSavedCount} / {parts.Count} parts generated so far.";
                    _running = false;
                    Repaint();
                    return;
                }

                int chunkStart = c * chunkSize;
                int chunkEnd = Mathf.Min(chunkStart + chunkSize, parts.Count);
                var chunkParts = parts.GetRange(chunkStart, chunkEnd - chunkStart);

                bool chunkSuccess = await ProcessChunkAsync(chunkParts, c, totalChunks, chunkStart, parts.Count);

                if (!chunkSuccess)
                {
                    _status = $"⚠️ Smart Chunking paused at Chunk {c + 1}/{totalChunks}.\n" +
                              $"Saved {totalPartsSavedCount} / {parts.Count} parts generated so far!\n\n" +
                              $"Simply click Generate again to resume remaining chunks seamlessly.";
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    _running = false;
                    Repaint();
                    return;
                }

                completedChunksCount++;
                totalPartsSavedCount += chunkParts.Count;

                if (c + 1 < totalChunks)
                {
                    Log($"Cooling down 3s before starting Chunk {c + 2}/{totalChunks}...");
                    await Task.Delay(3000, _cts.Token);
                }
            }

            // Write back to the prefab's EnginePart components directly as well
            string prefabPath = AssetDatabase.GetAssetPath(_droppedModel);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                Log("Updating prefab GameObjects with generated names and descriptions...");
                using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
                {
                    var root = scope.prefabContentsRoot;
                    var engineParts = root.GetComponentsInChildren<EnginePart>(true);
                    foreach (var ep in engineParts)
                    {
                        if (ep == null) continue;

                        int matchIdx = parts.FindIndex(p => p != null && (p.name == ep.partData?.name || Normalize(p.partName) == Normalize(ep.gameObject.name)));
                        if (matchIdx >= 0 && parts[matchIdx] != null)
                        {
                            if (!string.IsNullOrEmpty(parts[matchIdx].partName))
                                ep.partName = parts[matchIdx].partName;
                            if (!string.IsNullOrEmpty(parts[matchIdx].description))
                                ep.description = parts[matchIdx].description;
                            EditorUtility.SetDirty(ep);
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _generationProgress = 1.0f;
            _progressText = "Done!";

            _status = $"🎉 DONE! All {parts.Count} parts processed across {totalChunks} smart chunks.\n\n" +
                      $"Saved to:\n{partsFolder}\n\n" +
                      $"Backup at:\nProjectSettings/GroqBackup/";
        }
        catch (System.OperationCanceledException)
        {
            _status = "Generation cancelled by user.";
        }
        catch (System.Exception e)
        {
            _status = $"Unexpected error:\n{e.Message}";
            Debug.LogException(e);
        }
        finally
        {
            _running = false;
            _generationProgress = 0f;
            _progressText = "";
            _cts?.Dispose();
            _cts = null;
            Repaint();
        }
    }

    private bool IsPartCompleted(PartData pd)
    {
        if (pd == null) return false;
        if (string.IsNullOrEmpty(pd.description)) return false;
        string desc = pd.description.Trim();
        if (desc == "Part description here." || desc == "Engine description." || desc.StartsWith("Description for ")) return false;
        return desc.Length > 25;
    }

    private async Task<bool> ProcessChunkAsync(List<PartData> chunkParts, int chunkIndex, int totalChunks, int globalStartOffset, int globalTotalParts)
    {
        Log($"\n=======================================================");
        Log($"🧩 [Smart Chunking] Starting Chunk {chunkIndex + 1}/{totalChunks} ({chunkParts.Count} parts: #{globalStartOffset + 1} to #{globalStartOffset + chunkParts.Count} of {globalTotalParts})");
        Log($"=======================================================");

        var chunkGeo = ExtractGeometry(_droppedModel, chunkParts);
        AnnotateNeighbors(chunkGeo);

        int currentBatchSize = _useVision ? 3 : BatchSize;
        int totalBatches = Mathf.CeilToInt((float)chunkParts.Count / currentBatchSize);
        int batchDelayMs = ComputeBatchDelayMs(totalBatches, _useVision);
        int passTransitionDelayMs = _useVision ? batchDelayMs + 5000 : batchDelayMs;

        // Pass 1 — Naming
        var chunkNames = new List<string>();
        for (int b = 0; b < chunkParts.Count; b += currentBatchSize)
        {
            if (_cts.Token.IsCancellationRequested) return false;

            int batchNum = b / currentBatchSize + 1;
            int batchEnd = Mathf.Min(b + currentBatchSize, chunkParts.Count);
            var batchGeo = chunkGeo.GetRange(b, batchEnd - b);

            float chunkBaseProgress = (float)chunkIndex / totalChunks;
            float chunkWeight = 1f / totalChunks;
            float batchProgress = (float)b / chunkParts.Count;

            _generationProgress = chunkBaseProgress + chunkWeight * (0.05f + 0.43f * batchProgress);
            _progressText = $"Chunk {chunkIndex + 1}/{totalChunks} | Pass 1: Naming batch {batchNum}/{totalBatches}...";

            Log($"Chunk {chunkIndex + 1}/{totalChunks} — Pass 1 Batch {batchNum}/{totalBatches} (parts {globalStartOffset + b + 1}–{globalStartOffset + batchEnd})...");

            List<string> images = null;
            if (_useVision)
            {
                images = new List<string>();
                var batchParts = chunkParts.GetRange(b, batchEnd - b);
                for (int i = 0; i < batchParts.Count; i++)
                {
                    if (_cts.Token.IsCancellationRequested) return false;
                    _progressText = $"Chunk {chunkIndex + 1}/{totalChunks} | Rendering part {b + i + 1}...";
                    Repaint();
                    string b64 = CapturePartScreenshot(_droppedModel, batchParts[i].partName);
                    images.Add(b64);
                }
            }

            string namePrompt = BuildNameOnlyPrompt(batchGeo, _modelType, b, _additionalContext, _useVision);
            var names = await CallGroq(namePrompt, _cts.Token, images);

            if (names == null)
            {
                Log($"⚠️ Pass 1 batch {batchNum} in Chunk {chunkIndex + 1} failed or timed out.");
                return false;
            }

            chunkNames.AddRange(ExtractNames(names));

            if (b + currentBatchSize < chunkParts.Count)
            {
                Log($"Pass 1 Batch {batchNum}/{totalBatches} done. Cooling down {batchDelayMs / 1000}s...");
                for (int wait = 0; wait < batchDelayMs; wait += 500)
                {
                    if (_cts.Token.IsCancellationRequested) return false;
                    _progressText = $"Chunk {chunkIndex + 1}/{totalChunks} | Cooling down ({ (batchDelayMs - wait) / 1000 }s)...";
                    await Task.Delay(500, _cts.Token);
                }
            }
        }

        chunkNames = DeduplicateNames(chunkNames);

        for (int wait = 0; wait < passTransitionDelayMs; wait += 500)
        {
            if (_cts.Token.IsCancellationRequested) return false;
            _progressText = $"Chunk {chunkIndex + 1}/{totalChunks} | Pass transition cooldown ({ (passTransitionDelayMs - wait) / 1000 }s)...";
            await Task.Delay(500, _cts.Token);
        }

        // Pass 2 — Descriptions
        var chunkDescriptions = new List<string>();
        for (int b = 0; b < chunkParts.Count; b += currentBatchSize)
        {
            if (_cts.Token.IsCancellationRequested) return false;

            int batchNum = b / currentBatchSize + 1;
            int batchEnd = Mathf.Min(b + currentBatchSize, chunkParts.Count);
            var batchGeo = chunkGeo.GetRange(b, batchEnd - b);

            int nameEnd = Mathf.Min(b + currentBatchSize, chunkNames.Count);
            var batchNames = b < chunkNames.Count ? chunkNames.GetRange(b, nameEnd - b) : new List<string>();

            float chunkBaseProgress = (float)chunkIndex / totalChunks;
            float chunkWeight = 1f / totalChunks;
            float batchProgress = (float)b / chunkParts.Count;

            _generationProgress = chunkBaseProgress + chunkWeight * (0.50f + 0.45f * batchProgress);
            _progressText = $"Chunk {chunkIndex + 1}/{totalChunks} | Pass 2: Descriptions batch {batchNum}/{totalBatches}...";

            Log($"Chunk {chunkIndex + 1}/{totalChunks} — Pass 2 Batch {batchNum}/{totalBatches} (parts {globalStartOffset + b + 1}–{globalStartOffset + batchEnd})...");

            List<string> images = null;
            if (_useVision)
            {
                images = new List<string>();
                var batchParts = chunkParts.GetRange(b, batchEnd - b);
                for (int i = 0; i < batchParts.Count; i++)
                {
                    if (_cts.Token.IsCancellationRequested) return false;
                    _progressText = $"Chunk {chunkIndex + 1}/{totalChunks} | Rendering part {b + i + 1}...";
                    Repaint();
                    string b64 = CapturePartScreenshot(_droppedModel, batchParts[i].partName);
                    images.Add(b64);
                }
            }

            string descPrompt = BuildDescriptionPrompt(batchGeo, batchNames, _modelType, _additionalContext, _useVision);
            var results = await CallGroq(descPrompt, _cts.Token, images);

            if (results == null)
            {
                Log($"⚠️ Pass 2 batch {batchNum} in Chunk {chunkIndex + 1} failed or timed out.");
                return false;
            }

            var batchDescs = ExtractDescriptions(results);
            chunkDescriptions.AddRange(batchDescs);

            // Immediate Batch Auto-Save to Disk
            var batchPartsToSave = chunkParts.GetRange(b, batchEnd - b);
            for (int i = 0; i < batchPartsToSave.Count; i++)
            {
                int localIndex = b + i;
                if (localIndex < chunkNames.Count && !string.IsNullOrEmpty(chunkNames[localIndex]))
                    batchPartsToSave[i].partName = chunkNames[localIndex];
                if (i < batchDescs.Count && !string.IsNullOrEmpty(batchDescs[i]))
                    batchPartsToSave[i].description = batchDescs[i];
                EditorUtility.SetDirty(batchPartsToSave[i]);
            }
            AssetDatabase.SaveAssets();
            Log($"[Auto-Save] Chunk {chunkIndex + 1}/{totalChunks} — Batch {batchNum} saved ({chunkDescriptions.Count}/{chunkParts.Count} parts in chunk written to disk).");

            if (b + currentBatchSize < chunkParts.Count)
            {
                Log($"Pass 2 Batch {batchNum}/{totalBatches} done. Cooling down {batchDelayMs / 1000}s...");
                for (int wait = 0; wait < batchDelayMs; wait += 500)
                {
                    if (_cts.Token.IsCancellationRequested) return false;
                    _progressText = $"Chunk {chunkIndex + 1}/{totalChunks} | Cooling down ({ (batchDelayMs - wait) / 1000 }s)...";
                    await Task.Delay(500, _cts.Token);
                }
            }
        }

        Log($"✅ [Smart Chunking] Chunk {chunkIndex + 1}/{totalChunks} FULLY COMPLETED & COMMITTED TO DISK!");
        return true;
    }

    void Log(string msg) { _status = msg; Repaint(); }

    // ── Parts folder search ───────────────────────────────────────────────────
    static string FindPartsFolderForModel(GameObject model)
    {
        string modelAssetPath = AssetDatabase.GetAssetPath(model);
        string modelNorm      = Normalize(model.name);

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
                string edDir     = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guid)).Replace("\\", "/");
                string partsPath = edDir + "/Parts";
                if (Directory.Exists(partsPath)) return partsPath;
            }
        }

        string[] allDirs = Directory.GetDirectories("Assets", "Parts", SearchOption.AllDirectories);
        foreach (var dir in allDirs)
        {
            string ud = dir.Replace("\\", "/");
            string ef = Path.GetFileName(Path.GetDirectoryName(ud));
            if (Normalize(ef) == modelNorm) return ud;
        }
        foreach (var dir in allDirs)
        {
            string ud = dir.Replace("\\", "/");
            string ef = Path.GetFileName(Path.GetDirectoryName(ud));
            if (Normalize(ef).Contains(modelNorm) || modelNorm.Contains(Normalize(ef))) return ud;
        }

        return null;
    }

    static string Normalize(string s) =>
        s.Replace(" ", "").Replace("_", "").Replace("-", "").ToLower();

    /// <summary>
    /// Computes an adaptive inter-batch cooldown delay that scales with the number of batches.
    /// Rationale: the more batches in a run, the more cumulative API calls are made,
    /// so later batches need a longer breathing window to avoid rate limiting.
    ///
    /// Formula: base + (totalBatches - 1) * perBatchExtra, clamped to [min, max].
    ///
    /// Text mode  — 1 batch: 5s | 4 batches: ~9.5s | 8 batches: ~15.5s | 12+ batches: 25s
    /// Vision mode — 1 batch: 15s | 4 batches: ~24s | 6 batches: ~30s | 12+ batches: 60s
    /// </summary>
    static int ComputeBatchDelayMs(int totalBatches, bool useVision)
    {
        if (useVision)
        {
            int raw = MinVisionDelayMs + (totalBatches - 1) * 3000;
            return Mathf.Clamp(raw, MinVisionDelayMs, MaxVisionDelayMs);
        }
        else
        {
            int raw = MinBatchDelayMs + (totalBatches - 1) * 1500;
            return Mathf.Clamp(raw, MinBatchDelayMs, MaxBatchDelayMs);
        }
    }

    // ── Geometry extraction ───────────────────────────────────────────────────
    struct PartGeo
    {
        public string rawName;       // original mesh node name
        public string cleanedName;   // normalized for AI hint
        public string position;
        public string size;
        public string shape;
        public int    vertices;
        public string material;
        public int    nearbyTinyCount; // how many tiny parts share the same position zone
    }

    static List<PartGeo> ExtractGeometry(GameObject prefab, List<PartData> parts)
    {
        var result   = new List<PartGeo>();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            var allR      = instance.GetComponentsInChildren<Renderer>(true);
            var engBounds = new Bounds(instance.transform.position, Vector3.zero);
            foreach (var r in allR) engBounds.Encapsulate(r.bounds);

            Vector3 ec = engBounds.center;
            Vector3 es = engBounds.size;

            var lookup = new Dictionary<string, (MeshFilter mf, Renderer rend)>();
            foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
                if (!lookup.ContainsKey(mf.gameObject.name))
                    lookup[mf.gameObject.name] = (mf, mf.GetComponent<Renderer>());

            foreach (var pd in parts)
            {
                var geo = new PartGeo
                {
                    rawName     = pd.partName,
                    cleanedName = CleanMeshName(pd.partName)
                };

                if (lookup.TryGetValue(pd.partName, out var entry)
                    && entry.mf != null && entry.mf.sharedMesh != null)
                {
                    var mesh = entry.mf.sharedMesh;
                    var rend = entry.rend;
                    var b    = rend != null ? rend.bounds
                                           : new Bounds(entry.mf.transform.position, Vector3.zero);

                    // Position
                    Vector3 rel = b.center - ec;
                    float rx = es.x > 0 ? rel.x / (es.x * 0.5f) : 0;
                    float ry = es.y > 0 ? rel.y / (es.y * 0.5f) : 0;
                    float rz = es.z > 0 ? rel.z / (es.z * 0.5f) : 0;
                    string px = rx >  0.3f ? "right"  : rx < -0.3f ? "left"   : "center";
                    string py = ry >  0.3f ? "top"    : ry < -0.3f ? "bottom" : "middle";
                    string pz = rz >  0.3f ? "front"  : rz < -0.3f ? "rear"   : "center";
                    geo.position = $"{py}-{px}-{pz}";

                    // Size
                    float pv  = b.size.x * b.size.y * b.size.z;
                    float ev  = es.x * es.y * es.z;
                    float rat = ev > 0 ? pv / ev : 0;
                    geo.size  = rat > 0.15f ? "large"
                              : rat > 0.04f ? "medium"
                              : rat > 0.008f ? "small"
                              : "tiny";

                    // Shape
                    float maxD = Mathf.Max(b.size.x, b.size.y, b.size.z);
                    float minD = Mathf.Min(b.size.x, b.size.y, b.size.z);
                    float asp  = maxD > 0 ? minD / maxD : 1;
                    geo.shape  = asp < 0.15f ? "thin-flat"
                               : asp < 0.4f  ? "elongated"
                               : mesh.vertexCount > 2000 ? "complex"
                               : "compact";

                    geo.vertices = mesh.vertexCount;

                    // Material
                    if (rend != null && rend.sharedMaterial != null)
                    {
                        string mn = rend.sharedMaterial.name
                            .Replace("(Instance)", "").Replace("_", " ").Trim();
                        geo.material = mn.Length > 35 ? mn.Substring(0, 35) : mn;
                    }
                }
                else
                {
                    geo.position = "unknown";
                    geo.size     = "unknown";
                    geo.shape    = "unknown";
                }

                result.Add(geo);
            }
        }
        finally { DestroyImmediate(instance); }

        return result;
    }

    /// <summary>
    /// Cleans up raw Unity/GLB mesh node names into readable hints.
    /// e.g. "SM_exhaust_manifold_L_LOD0" → "exhaust manifold L"
    /// </summary>
    static string CleanMeshName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        // Strip common prefixes/suffixes from 3D tools
        string s = Regex.Replace(raw,
            @"(?i)\b(SM_|UCX_|LOD\d|_LOD\d|Mesh_?|mesh_?|Object_?|Cube\.?\d*|Plane\.?\d*|Cylinder\.?\d*)\b",
            " ");

        // Strip ALL trailing digits (e.g. 1, 2, 01, 100)
        s = Regex.Replace(s, @"\d+$", "");

        // Strip trailing numbers like _001, .001
        s = Regex.Replace(s, @"[._]\d+$", "");

        // Replace separators with spaces
        s = s.Replace("_", " ").Replace("-", " ").Replace(".", " ");

        // Strip any remaining standalone digits from the string
        s = Regex.Replace(s, @"\b\d+\b", "");

        // Collapse multiple spaces
        s = Regex.Replace(s, @"\s+", " ").Trim();

        return s;
    }

    /// <summary>
    /// Counts how many tiny parts share the same positional zone as each part.
    /// Large parts with many tiny neighbours → mounting bolts/studs are nearby.
    /// </summary>
    static void AnnotateNeighbors(List<PartGeo> geoData)
    {
        for (int i = 0; i < geoData.Count; i++)
        {
            int count = 0;
            for (int j = 0; j < geoData.Count; j++)
            {
                if (i == j) continue;
                if (geoData[j].size == "tiny" && geoData[j].position == geoData[i].position)
                    count++;
            }
            var g = geoData[i];
            g.nearbyTinyCount = count;
            geoData[i] = g;
        }
    }

    // ── Prompt builders ───────────────────────────────────────────────────────

    /// <summary>
    /// Pass 1: asks the AI to identify part names ONLY.
    /// Includes raw mesh hint as the strongest signal.
    /// </summary>
    static string BuildNameOnlyPrompt(List<PartGeo> batch, string modelType, int globalOffset, string additionalContext = "", bool useVision = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert mechanical engineer specializing in automotive and aerospace powertrains.");
        sb.AppendLine($"You are analyzing 3D mesh metadata from: {modelType}");
        if (!string.IsNullOrWhiteSpace(additionalContext))
        {
            sb.AppendLine();
            sb.AppendLine("ADDITIONAL CONTEXT (provided by the engineer running this tool — use it to disambiguate part identities):");
            sb.AppendLine(additionalContext.Trim());
        }
        sb.AppendLine();
        sb.AppendLine("Your task: identify the most accurate REAL mechanical component name for each mesh.");
        sb.AppendLine();
        sb.AppendLine("INPUT FIELDS");
        sb.AppendLine("Each mesh entry contains:");
        sb.AppendLine("  mesh_hint     = cleaned-up node name from the 3D source file (STRONGEST signal)");
        sb.AppendLine("  position      = location relative to engine center (vertical-horizontal-depth)");
        sb.AppendLine("  size          = relative scale in the assembly (large/medium/small/tiny)");
        sb.AppendLine("  shape         = bounding-box classification");
        sb.AppendLine("  vertices      = polygon complexity estimate");
        sb.AppendLine("  material      = material hint from source file");
        sb.AppendLine("  nearby_tiny   = count of nearby tiny meshes");
        if (useVision)
        {
            sb.AppendLine("  (You are also provided with an image of each part in order. Match each image index to the part list below.)");
        }
        sb.AppendLine();
        sb.AppendLine("PRIMARY IDENTIFICATION RULES");
        if (useVision)
        {
            sb.AppendLine("  1. Use the attached images in order to visually recognize the component's mechanical function, shape, and connection points.");
            sb.AppendLine("  2. Combine the visual evidence with the mesh_hint. The mesh_hint is a strong starting signal, but if the image reveals it is a different component (e.g. hint is generic or misleading), prioritize visual identification.");
        }
        else
        {
            sb.AppendLine("  1. mesh_hint is the STRONGEST signal. Clean and normalize it into a professional");
            sb.AppendLine("     mechanical part name. Use geometry reasoning ONLY if the hint is generic.");
        }
        sb.AppendLine("  2. IGNORE useless mesh hints entirely — reason from geometry instead:");
        sb.AppendLine("     Mesh, Object, Part, Cube, Cylinder, Plane, SM_, UCX_, LOD, lambert, default, unnamed");
        sb.AppendLine("  3. NEVER output placeholder-style names like Blade1, Turbine3, Gear_07, Part_A, Object12.");
        sb.AppendLine("  4. NEVER include numbers, digits (0-9), underscores, file suffixes, mesh IDs, or CAD labels. Absolutely no digit characters (0-9) are allowed in the output.");
        sb.AppendLine("  5. Output ONLY clean professional engineering names using words. If there are multiple stages or versions of a component, use words instead of digits (e.g., write 'First Stage Compressor' instead of 'Compressor 1', 'Second Stage' instead of 'Stage 2').");
        sb.AppendLine("  6. Use directional or ordinal word suffixes/prefixes when appropriate to make names unique: Left/Right, Upper/Lower, Front/Rear, Inner/Outer, First/Second/Third.");
        sb.AppendLine("  7. Every part name MUST be UNIQUE across all parts. If multiple parts are identical (e.g., bolts or blades), differentiate them using written ordinal words (e.g. 'First Fan Blade', 'Second Fan Blade', 'Third Fan Blade') rather than digits.");
        sb.AppendLine();
        sb.AppendLine("GEOMETRY REASONING FALLBACKS (use only when mesh_hint is weak or generic):");
        sb.AppendLine("  large + top + complex         → Engine Block, Cylinder Head");
        sb.AppendLine("  medium + top + complex        → Valve Cover, Intake Manifold");
        sb.AppendLine("  elongated + side              → Exhaust Manifold, Intake Runner");
        sb.AppendLine("  thin-flat                     → Heat Shield, Gasket, Cover Plate");
        sb.AppendLine("  bottom + medium + compact     → Oil Pan, Oil Sump");
        sb.AppendLine("  rear + large + circular       → Flywheel, Flexplate");
        sb.AppendLine("  tiny + metallic               → Bolt, Stud, Fastener, Retaining Clip, Sensor");
        sb.AppendLine("  nearby_tiny >= 6 around large → likely mounted housing or cover with fasteners");
        sb.AppendLine();
        sb.AppendLine("PARTS:");

        for (int i = 0; i < batch.Count; i++)
        {
            var g = batch[i];
            sb.Append($"{globalOffset + i + 1}.");
            if (!string.IsNullOrEmpty(g.cleanedName) && g.cleanedName.Length > 2)
                sb.Append($" mesh_hint=\"{g.cleanedName}\"");
            else
                sb.Append($" mesh_hint=GENERIC");
            sb.Append($" position={g.position}");
            sb.Append($" size={g.size}");
            sb.Append($" shape={g.shape}");
            sb.Append($" vertices={g.vertices}");
            if (!string.IsNullOrEmpty(g.material))
                sb.Append($" material=\"{g.material}\"");
            if (g.nearbyTinyCount > 0)
                sb.Append($" nearby_tiny={g.nearbyTinyCount}");
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine($"Return a JSON array of exactly {batch.Count} objects.");
        sb.AppendLine("Format: [{\"name\": \"Part Name\"}, {\"name\": \"Part Name\"}, ...]");
        sb.AppendLine("Return ONLY the raw JSON array. No markdown, no explanation, no extra text.");

        return sb.ToString();
    }

    /// <summary>
    /// Pass 2: given confirmed part names from Pass 1, generates accurate descriptions.
    /// </summary>
    static string BuildDescriptionPrompt(List<PartGeo> batch, List<string> confirmedNames, string modelType, string additionalContext = "", bool useVision = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert mechanical engineer writing concise technical documentation.");
        sb.AppendLine($"Engine type: {modelType}");
        if (!string.IsNullOrWhiteSpace(additionalContext))
        {
            sb.AppendLine();
            sb.AppendLine("ADDITIONAL CONTEXT (provided by the engineer — incorporate this into descriptions where relevant):");
            sb.AppendLine(additionalContext.Trim());
        }
        sb.AppendLine();
        sb.AppendLine("The part names below have been confirmed. Write a ONE-sentence description (exactly 25-30 words) for each.");
        if (useVision)
        {
            sb.AppendLine("Use the attached images of each component in order to visually inspect its design, details, and function, and write an accurate description based on the image.");
        }
        sb.AppendLine();
        sb.AppendLine("DESCRIPTION RULES:");
        sb.AppendLine("  - The description MUST start with \"This is a {part_name}\" or \"This is the {part_name}\" where {part_name} is the confirmed_name.");
        sb.AppendLine("  - The description MUST focus on explaining its main purpose and mechanical function within the engine.");
        sb.AppendLine("  - The description length MUST be strictly between 25 and 30 words (inclusive). Count your words carefully!");
        sb.AppendLine("  - Mention sealing, airflow, rotation, mounting, lubrication, or structural support when relevant.");
        sb.AppendLine("  - Be factual, direct, engineering-tone. Avoid marketing language or educational filler.");
        sb.AppendLine();
        sb.AppendLine("  DO NOT:");
        sb.AppendLine("    • Invent materials, thermal properties, or manufacturing methods.");
        sb.AppendLine("    • Say \"interestingly\", \"notably\", \"plays a crucial role\", \"typically made from\".");
        sb.AppendLine("    • Add trivia, storytelling, or speculative details.");
        sb.AppendLine();
        sb.AppendLine("  GOOD EXAMPLES:");
        sb.AppendLine("    • For confirmed_name \"cylinder head gasket\": \"This is the cylinder head gasket which seals the gap between the cylinder head and engine block to prevent oil, coolant, and combustion gas leakage.\" (25 words)");
        sb.AppendLine("    • For confirmed_name \"spark plug\": \"This is a spark plug that provides the electrical spark necessary to ignite the compressed air-fuel mixture within the combustion chamber of the engine cylinders.\" (25 words)");
        sb.AppendLine("  BAD EXAMPLE:");
        sb.AppendLine("    \"Seals the gap between the cylinder head and engine block to prevent oil and gas leakage.\" (Does not start with \"This is the cylinder head gasket\" and is too short)");
        sb.AppendLine();
        sb.AppendLine("PARTS:");

        for (int i = 0; i < batch.Count; i++)
        {
            var    g    = batch[i];
            string name = i < confirmedNames.Count ? confirmedNames[i] : g.cleanedName;
            sb.Append($"{i + 1}. confirmed_name=\"{name}\"");
            sb.Append($" position={g.position}");
            sb.Append($" size={g.size}");
            sb.Append($" shape={g.shape}");
            if (!string.IsNullOrEmpty(g.material))
                sb.Append($" material=\"{g.material}\"");
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine($"Return a JSON array of exactly {batch.Count} objects.");
        sb.AppendLine("Format: [{\"description\": \"25-30 word technical description starting with 'This is a/the {part_name}'\"}, ...]");
        sb.AppendLine("Return ONLY the raw JSON array. No markdown, no explanation, no extra text.");

        return sb.ToString();
    }

    // ── Groq API call ─────────────────────────────────────────────────────────
    async Task<List<Dictionary<string, string>>> CallGroq(string prompt, CancellationToken ct, List<string> base64Images = null)
    {
        return await CallGroqWithRetry(prompt, ct, 3, base64Images);
    }

    async Task<List<Dictionary<string, string>>> CallGroqWithRetry(string prompt, CancellationToken ct, int maxRetries, List<string> base64Images = null)
    {
        // Clamp base64Images to max 3 images (Groq API limit for vision models)
        if (base64Images != null && base64Images.Count > 3)
        {
            Log($"[Groq] Clamping image count from {base64Images.Count} to 3 (Groq API limit).");
            base64Images = base64Images.GetRange(0, 3);
        }

        // Define fallback candidates
        List<string> candidateModels = new List<string>();
        if (base64Images != null && base64Images.Count > 0)
        {
            candidateModels.Add(VisionModel);
            candidateModels.Add("qwen/qwen3.6-27b");
            candidateModels.Add("meta-llama/llama-4-scout-17b-16e-instruct");
            candidateModels.Add("llama-3.2-90b-vision-preview");
            candidateModels.Add("llama-3.2-11b-vision-preview");
        }
        else
        {
            candidateModels.Add(Model);
            candidateModels.Add("llama-3.3-70b-versatile");
            candidateModels.Add("llama-3.1-70b-versatile");
            candidateModels.Add("llama3-70b-8192");
            candidateModels.Add("llama-3.1-8b-instant");
            candidateModels.Add("mixtral-8x7b-32768");
        }

        // Deduplicate candidates while preserving order
        List<string> uniqueModels = new List<string>();
        foreach (var m in candidateModels)
        {
            if (!string.IsNullOrEmpty(m) && !uniqueModels.Contains(m))
                uniqueModels.Add(m);
        }

        foreach (var activeModel in uniqueModels)
        {
            StringBuilder jsonBuilder = new StringBuilder();
            jsonBuilder.Append("{");
            jsonBuilder.Append($"\"model\":\"{activeModel}\",");
            jsonBuilder.Append("\"messages\":[{");
            jsonBuilder.Append("\"role\":\"user\",");
            
            if (base64Images != null && base64Images.Count > 0)
            {
                jsonBuilder.Append("\"content\":[");
                jsonBuilder.Append("{");
                jsonBuilder.Append("\"type\":\"text\",");
                jsonBuilder.Append($"\"text\":\"{JsonEscapeContent(prompt)}\"");
                jsonBuilder.Append("}");
                
                foreach (var img in base64Images)
                {
                    if (string.IsNullOrEmpty(img)) continue;
                    jsonBuilder.Append(",{");
                    jsonBuilder.Append("\"type\":\"image_url\",");
                    jsonBuilder.Append("\"image_url\":{");
                    jsonBuilder.Append($"\"url\":\"data:image/jpeg;base64,{img}\"");
                    jsonBuilder.Append("}");
                    jsonBuilder.Append("}");
                }
                
                jsonBuilder.Append("]");
            }
            else
            {
                jsonBuilder.Append($"\"content\":{JsonEscape(prompt)}");
            }
            
            jsonBuilder.Append("}],");
            jsonBuilder.Append("\"temperature\":0.15,");
            jsonBuilder.Append("\"max_tokens\":4000");
            jsonBuilder.Append("}");
            
            string body = jsonBuilder.ToString();
                          
            int retryCount = 0;
            int delayMs = 5000;
            
            while (retryCount <= maxRetries)
            {
                try
                {
                    using var req = new UnityWebRequest(GroqEndpoint, "POST");
                    req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type",  "application/json");
                    req.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
                    req.timeout = TimeoutSecs;

                    var op = req.SendWebRequest();
                    while (!op.isDone)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            req.Abort();
                            return null;
                        }
                        await Task.Yield();
                    }

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        long responseCode = req.responseCode;
                        string errorText = req.downloadHandler.text;
                        
                        if (responseCode == 429)
                        {
                            retryCount++;
                            if (retryCount <= maxRetries)
                            {
                                int waitMs = delayMs * retryCount;
                                Match match = Regex.Match(errorText, @"Please try again in (\d+)(ms|s)");
                                if (match.Success)
                                {
                                    if (int.TryParse(match.Groups[1].Value, out int parsedVal))
                                    {
                                        if (match.Groups[2].Value == "s")
                                            waitMs = parsedVal * 1000 + 500;
                                        else
                                            waitMs = parsedVal + 500;
                                    }
                                }
                                
                                Log($"[Groq] Rate limit (429) reached for model '{activeModel}'. Waiting {waitMs / 1000f:F1}s before retry {retryCount}/{maxRetries}...");
                                await Task.Delay(waitMs, ct);
                                continue;
                            }
                        }
                        
                        // Check if model error (not found, decommissioned, payload too large, or rate limited) -> try next fallback model
                        if (responseCode == 400 || responseCode == 404 || responseCode == 413)
                        {
                            Log($"[Groq] Model '{activeModel}' returned HTTP {responseCode}: {errorText}. Trying next fallback model / payload reduction...");
                            break;
                        }

                        Debug.LogError($"[Groq] {req.error}\n{errorText}");
                        return null;
                    }

                    string raw     = req.downloadHandler.text;
                    string content = ExtractContent(raw);
                    if (string.IsNullOrEmpty(content)) { Debug.LogError("[Groq] Empty content in response."); return null; }

                    content = StripMarkdownFences(content);
                    Debug.Log($"[Groq] Model '{activeModel}' response:\n{content}");
                    return ParseObjectArray(content);
                }
                catch (System.Exception e) 
                { 
                    Debug.LogError($"[Groq] {e.Message}"); 
                    return null; 
                }
            }
        }

        // Final fallback: If all vision models failed or produced errors with images, try text-only mode
        if (base64Images != null && base64Images.Count > 0)
        {
            Log("[Groq] All vision models failed. Automatically falling back to Text-Only mode...");
            return await CallGroqWithRetry(prompt, ct, maxRetries, base64Images: null);
        }

        return null;
    }

    // ── Result extraction ─────────────────────────────────────────────────────
    static List<string> ExtractNames(List<Dictionary<string, string>> parsed)
    {
        var list = new List<string>();
        if (parsed == null) return list;
        foreach (var d in parsed)
        {
            if (d.TryGetValue("name", out var v)) list.Add(v.Trim());
            else if (d.TryGetValue("partName", out v)) list.Add(v.Trim());
            else if (d.TryGetValue("confirmed_name", out v)) list.Add(v.Trim());
            else list.Add("");
        }
        return list;
    }

    static List<string> ExtractDescriptions(List<Dictionary<string, string>> parsed)
    {
        var list = new List<string>();
        if (parsed == null) return list;
        foreach (var d in parsed)
        {
            if (d.TryGetValue("description", out var v)) list.Add(v.Trim());
            else if (d.TryGetValue("desc", out v)) list.Add(v.Trim());
            else list.Add("");
        }
        return list;
    }

    /// <summary>
    /// Ensures no two parts share the same name.
    /// Appends (2), (3), etc. to duplicates rather than leaving them identical.
    /// </summary>
    static List<string> DeduplicateNames(List<string> names)
    {
        var seen  = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(names.Count);

        foreach (var n in names)
        {
            if (string.IsNullOrEmpty(n)) { result.Add(n); continue; }

            if (!seen.ContainsKey(n))
            {
                seen[n] = 1;
                result.Add(n);
            }
            else
            {
                seen[n]++;
                result.Add($"{n} ({seen[n]})");
            }
        }
        return result;
    }

    // ── Backup ────────────────────────────────────────────────────────────────
    static void SaveBackup(List<PartData> parts, string partsFolder)
    {
        try
        {
            string backupDir = "ProjectSettings/GroqBackup";
            Directory.CreateDirectory(backupDir);

            string folderLabel = partsFolder.Replace("/", "_").Replace(":", "");
            string timestamp   = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path        = $"{backupDir}/{folderLabel}_{timestamp}.json";

            var sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < parts.Count; i++)
            {
                sb.Append($"  {{\"partName\":{JsonQuote(parts[i].partName)}," +
                           $"\"description\":{JsonQuote(parts[i].description)}}}");
                if (i < parts.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("]");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[Groq] Backup saved to {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Groq] Backup failed (non-fatal): {e.Message}");
        }
    }

    static string JsonQuote(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    // ── JSON helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Strips markdown code fences that the model sometimes wraps around JSON.
    /// e.g. ```json [...] ``` → [...]
    /// </summary>
    static string StripMarkdownFences(string s)
    {
        s = s.Trim();
        if (!s.StartsWith("```")) return s;
        int firstNewline = s.IndexOf('\n');
        int lastFence    = s.LastIndexOf("```");
        if (firstNewline > 0 && lastFence > firstNewline)
            s = s.Substring(firstNewline, lastFence - firstNewline).Trim();
        return s;
    }

    /// <summary>
    /// Parses a JSON array of objects into a list of string dictionaries.
    /// Handles arbitrary key/value string pairs per object.
    /// </summary>
    static List<Dictionary<string, string>> ParseObjectArray(string content)
    {
        var list  = new List<Dictionary<string, string>>();
        int start = content.IndexOf('[');
        int end   = content.LastIndexOf(']');
        
        // If content is truncated and missing the closing ']', try to parse whatever we have
        if (start < 0) return null;
        if (end <= start) end = content.Length - 1;

        string json = content.Substring(start, end - start + 1);
        int i = 0;
        while (i < json.Length)
        {
            int os = json.IndexOf('{', i); if (os < 0) break;
            int oe = FindClosingBrace(json, os);
            string obj;
            if (oe < 0)
            {
                // Truncated object at the end: parse from '{' to the end of string
                obj = json.Substring(os);
                i = json.Length; // break loop
            }
            else
            {
                obj = json.Substring(os, oe - os + 1);
                i = oe + 1;
            }

            var dict = ParseObject(obj);
            if (dict != null) list.Add(dict);
        }
        return list;
    }

    /// <summary>Parses all string key-value pairs from a single JSON object string.</summary>
    static Dictionary<string, string> ParseObject(string obj)
    {
        var dict = new Dictionary<string, string>();
        int i = 1; // skip opening {

        while (i < obj.Length)
        {
            // Find next key
            int ks = obj.IndexOf('"', i); if (ks < 0) break;
            int ke = FindClosingQuote(obj, ks + 1); if (ke < 0) break;
            string key = obj.Substring(ks + 1, ke - ks - 1);
            i = ke + 1;

            // Skip colon
            int colon = obj.IndexOf(':', i); if (colon < 0) break;
            i = colon + 1;

            // Skip whitespace
            while (i < obj.Length && (obj[i] == ' ' || obj[i] == '\n' || obj[i] == '\r' || obj[i] == '\t')) i++;
            if (i >= obj.Length) break;

            // Read value (only string values for our use case)
            if (obj[i] == '"')
            {
                string val;
                int ve = FindClosingQuote(obj, i + 1);
                if (ve < 0)
                {
                    // Truncated string value at the end!
                    int len = obj.Length - (i + 1);
                    string rawVal = obj.Substring(i + 1, len);
                    // Clean trailing brackets, braces, quotes, spaces
                    rawVal = rawVal.TrimEnd(' ', '\n', '\r', '\t', ']', '}', '"');
                    val = UnescapeJson(rawVal);
                    dict[key] = val;
                    break;
                }
                else
                {
                    val = UnescapeJson(obj.Substring(i + 1, ve - i - 1));
                    dict[key] = val;
                    i = ve + 1;
                }
            }
            else
            {
                // Non-string value — skip to next comma or closing brace
                while (i < obj.Length && obj[i] != ',' && obj[i] != '}') i++;
            }

            // Skip comma and trailing whitespaces
            while (i < obj.Length && (obj[i] == ',' || obj[i] == ' ' || obj[i] == '\n' || obj[i] == '\r' || obj[i] == '\t')) i++;
        }

        return dict.Count > 0 ? dict : null;
    }

    static int FindClosingBrace(string s, int from)
    {
        int  depth = 0;
        bool inStr = false;
        for (int i = from; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && inStr) { i++; continue; }
            if (c == '"') { inStr = !inStr; continue; }
            if (inStr) continue;
            if (c == '{') depth++;
            else if (c == '}') { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    static int FindClosingQuote(string s, int from)
    {
        for (int i = from; i < s.Length; i++)
        {
            if (s[i] == '\\') { i++; continue; }
            if (s[i] == '"') return i;
        }
        return -1;
    }

    static string UnescapeJson(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n == 'r' ? '\r' : n);
                continue;
            }
            sb.Append(s[i]);
        }
        return sb.ToString().Trim();
    }

    static string ExtractContent(string resp)
    {
        int idx = resp.IndexOf("\"content\":");
        if (idx < 0) return null;
        idx += 10;
        while (idx < resp.Length && resp[idx] != '"') idx++;
        if (idx >= resp.Length) return null;
        idx++;
        var sb = new StringBuilder();
        while (idx < resp.Length)
        {
            char c = resp[idx];
            if (c == '\\' && idx + 1 < resp.Length)
            {
                char n = resp[++idx];
                sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n == 'r' ? '\r' : n);
                idx++; continue;
            }
            if (c == '"') break;
            sb.Append(c); idx++;
        }
        return sb.ToString();
    }

    static string JsonEscape(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";

    static string JsonEscapeContent(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"")
         .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    private static string CapturePartScreenshot(GameObject prefab, string partName)
    {
        if (prefab == null || string.IsNullOrEmpty(partName)) return null;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;

        GameObject camObj = null;
        RenderTexture rt = null;
        Texture2D tex = null;
        string base64Result = null;

        try
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            Renderer targetRenderer = null;
            foreach (var r in renderers)
            {
                if (Normalize(r.gameObject.name) == Normalize(partName))
                {
                    targetRenderer = r;
                    break;
                }
            }

            if (targetRenderer == null)
            {
                foreach (var r in renderers)
                {
                    if (Normalize(r.gameObject.name).Contains(Normalize(partName)) ||
                        Normalize(partName).Contains(Normalize(r.gameObject.name)))
                    {
                        targetRenderer = r;
                        break;
                    }
                }
            }

            if (targetRenderer == null)
            {
                Debug.LogWarning($"[Groq Vision] Could not find renderer for part '{partName}' to capture screenshot.");
                return null;
            }

            foreach (var r in renderers)
            {
                r.enabled = (r == targetRenderer);
            }

            camObj = new GameObject("TempVisionCamera");
            camObj.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = camObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;

            Bounds bounds = targetRenderer.bounds;
            Vector3 center = bounds.center;
            float boundsSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

            Vector3 viewDir = new Vector3(1f, 1f, -1f).normalized;
            camera.transform.position = center + viewDir * (boundsSize * 3.0f + 1f);
            camera.transform.LookAt(center);
            camera.orthographicSize = (boundsSize * 0.5f) * 1.2f;

            GameObject lightObj = new GameObject("TempVisionLight");
            lightObj.hideFlags = HideFlags.HideAndDontSave;
            lightObj.transform.parent = camObj.transform;
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = Color.white;
            light.transform.forward = camera.transform.forward;

            rt = RenderTexture.GetTemporary(128, 128, 16, RenderTextureFormat.ARGB32);
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = rt;
            camera.targetTexture = rt;
            camera.Render();

            tex = new Texture2D(128, 128, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
            tex.Apply();

            RenderTexture.active = currentRT;
            camera.targetTexture = null;

            byte[] bytes = tex.EncodeToJPG(70);
            base64Result = System.Convert.ToBase64String(bytes);
            
            DestroyImmediate(lightObj);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Groq Vision] Error rendering screenshot for '{partName}': {ex.Message}");
        }
        finally
        {
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
            if (tex != null) DestroyImmediate(tex);
            if (camObj != null) DestroyImmediate(camObj);
            if (instance != null) DestroyImmediate(instance);
        }

        return base64Result;
    }
}