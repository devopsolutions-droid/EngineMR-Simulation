# Scene Wiring Guide: Home Scene & Main Scene

## Overview
This document explains how the **Home Scene** and **Main Scene** are connected in the EngineVR Simulation project, and what changes are needed if you want to replace the Home Scene with a new scene.

---

## Current Scene Architecture

### 1. **Home Scene** (`Assets/Scenes/Home Scene.unity`)
**Purpose**: The starting scene where users browse and select engines.

**Key Components**:
- **HomeSceneUIController**: Controls the two-panel flow
  - Start Page (initial landing)
  - Engine Scroll Panel (engine selection grid)
- **HomeSceneManager**: Populates the 3x3 engine card grid from `EngineRegistry`
- **EngineCardUI**: Individual engine cards that load the Main Scene when clicked
- **SceneTransitionManager**: Handles fade transitions between scenes (initialized here with `DontDestroyOnLoad`)

**Data Flow**:
1. User selects an engine card
2. Selection is stored in `EngineSessionData` (ScriptableObject)
3. Scene transitions to "Main Scene" using `SceneTransitionManager.LoadScene()`

---

### 2. **Main Scene** (`Assets/Scenes/Main Scene.unity`)
**Purpose**: The engine viewing/interaction scene where users explore selected engines.

**Key Components**:
- **EngineSceneLoader**: Core scene manager
  - Reads selected engine from `EngineSessionData`
  - Activates the appropriate engine model from `engineEntries` array
  - Deactivates all other engines
  - Configures all engine-related systems (ViewManager, Interactor, UI, etc.)
- **EngineViewManager**: Controls view modes (Normal, Exploded, X-Ray, etc.)
- **EngineInteractor**: Handles part selection and interaction
- **TabletUIController**: In-scene UI controls
- **TutorialPlayerDisplay**: Displays tutorial content for the active engine

**Back Button Flow**:
- Sets `HomeSceneUIController.ReturnToScroll = true`
- Loads scene by name from `homeSceneName` field (default: "HomeScene")
- Uses `SceneTransitionManager.LoadScene()` for smooth fade

---

## Scene Configuration Files

### **Build Settings** (`ProjectSettings/EditorBuildSettings.asset`)
```yaml
m_Scenes:
  - enabled: 1
    path: Assets/Scenes/Home Scene.unity
    guid: 806c0300040ca6b4499d28ea2df7774c
  - enabled: 1
    path: Assets/Scenes/Main Scene.unity
    guid: 428653eb402fe514cb94556afb3934bd
```

---

## How Scenes Are Wired Together

### **Home → Main Scene Flow**

```
[Home Scene]
    ↓
HomeSceneManager
    ↓
EngineCardUI.OnCardSelected()
    ↓
EngineSessionData.Select(engineData)  ← stores selection
    ↓
SceneTransitionManager.LoadScene("Main Scene")
    ↓
[Main Scene]
    ↓
EngineSceneLoader.Start()
    ↓
Reads EngineSessionData.selectedEngine
    ↓
ActivateEngine() - shows selected engine model
```

### **Main → Home Scene Flow**

```
[Main Scene]
    ↓
Back Button clicked
    ↓
EngineSceneLoader.GoHome()
    ↓
Sets HomeSceneUIController.ReturnToScroll = true
    ↓
SceneTransitionManager.LoadScene(homeSceneName)  ← "HomeScene" by default
    ↓
[Home Scene]
    ↓
HomeSceneUIController.Start()
    ↓
Checks ReturnToScroll flag
    ↓
If true: Shows Engine Scroll Panel (skips Start Page)
If false: Shows Start Page
```

---

## Replacing the Home Scene: Required Changes

If you want to replace the Home Scene with a new scene (let's call it "NewHomeScene"), here's what needs to be updated:

### 1. **Unity Build Settings**
**File**: `ProjectSettings/EditorBuildSettings.asset`

**Action**: Add your new scene and optionally remove the old one
```yaml
m_Scenes:
  - enabled: 1
    path: Assets/Scenes/NewHomeScene.unity  # Your new scene
    guid: [auto-generated-guid]
  - enabled: 1
    path: Assets/Scenes/Main Scene.unity
    guid: 428653eb402fe514cb94556afb3934bd
```

**How**: In Unity Editor, go to `File → Build Settings → Scenes in Build`

---

### 2. **EngineSceneLoader.cs**
**File**: `Assets/Scripts/Engine/EngineSceneLoader.cs`

**Change**: Update the `homeSceneName` field (line 48)
```csharp
[Header("Back Button")]
public string homeSceneName = "NewHomeScene";  // ← Change this
```

**Alternative**: You can also change this value in the Unity Inspector on the EngineSceneLoader component in the Main Scene.

---

### 3. **HomeSceneManager.cs** (if keeping similar structure)
**File**: `Assets/Scripts/Home/HomeSceneManager.cs`

**Change**: Update the `engineSceneName` field (line 17)
```csharp
[Header("Scene")]
[Tooltip("Exact name of the engine view scene in Build Settings.")]
public string engineSceneName = "Main Scene";  // ← This should stay "Main Scene"
```

**Note**: Only change this if you're also renaming the Main Scene.

---

### 4. **SceneTransitionManager Prefab**
**File**: Referenced in `HomeSceneManager.sceneTransitionPrefab`

**Action**: Ensure your new Home Scene has the SceneTransitionManager
- Either place the prefab in your new scene
- Or ensure `HomeSceneManager` instantiates it via `sceneTransitionPrefab` reference

**Why**: SceneTransitionManager must be initialized with `DontDestroyOnLoad` from the first scene that loads.

---

### 5. **XROriginScenePose.cs** (Optional - for VR positioning)
**File**: `Assets/Scripts/XROriginScenePose.cs`

**Change**: Update scene-specific pose data if your scene has custom camera positioning
```csharp
public struct ScenePoseData
{
    [Tooltip("Exact scene name (e.g. 'Home Scene' or 'Main Scene').")]
    public string sceneName;  // ← Update any references to "Home Scene"
    // ...
}
```

---

### 6. **Editor Tools** (Optional - only if using them)
**File**: `Assets/Scripts/Editor/AddVRKeyboardToHomeScene.cs`

**Change**: Update the scene path (line 13)
```csharp
string scenePath = "Assets/Scenes/NewHomeScene.unity";  // ← Change this
```

---

## Required Components in Your New Home Scene

Your new Home Scene must include these components to maintain functionality:

### **Minimum Required**:
1. **EngineSessionData** (ScriptableObject reference) - for passing selected engine to Main Scene
2. **EngineRegistry** (ScriptableObject reference) - contains list of all engines
3. **SceneTransitionManager** (prefab or component) - handles fade transitions
4. **A way to select engines and call**:
   ```csharp
   sessionData.Select(engineData);
   SceneTransitionManager.Instance.LoadScene("Main Scene");
   ```

### **Optional but Recommended**:
- **HomeSceneUIController** - for two-panel flow and return-from-engine behavior
- **HomeSceneManager** - for automatic grid population from EngineRegistry
- **EngineCardUI prefabs** - for displaying engine cards

---

## Key Data Objects (ScriptableObjects)

### **EngineSessionData**
**Location**: `Assets/ScriptableObjects/EngineSessionData.asset`

**Purpose**: Runtime messenger between scenes
- Home Scene writes `selectedEngine` before loading Main Scene
- Main Scene reads `selectedEngine` in `EngineSceneLoader.Start()`
- Automatically persists across scene loads (ScriptableObject behavior)

### **EngineRegistry**
**Location**: `Assets/ScriptableObjects/EngineRegistry.asset`

**Purpose**: Master list of all available engines
- Read by HomeSceneManager to populate engine cards
- Contains references to all EngineData assets

---

## Common Pitfalls When Replacing Home Scene

### ❌ **Scene Name Mismatch**
- The string in `EngineSceneLoader.homeSceneName` must **exactly** match the scene name in Build Settings
- Scene names are case-sensitive: "HomeScene" ≠ "homescene"

### ❌ **Missing SceneTransitionManager**
- If your new scene doesn't initialize SceneTransitionManager, transitions will fall back to instant `SceneManager.LoadScene()` calls (no fade)
- Make sure the prefab is instantiated in your new scene's Start/Awake

### ❌ **Not in Build Settings**
- Your new scene must be added to Build Settings (`File → Build Settings`)
- Scenes not in build settings cannot be loaded at runtime

### ❌ **Missing EngineSessionData Reference**
- If your new scene doesn't pass the selected engine via `EngineSessionData.Select()`, the Main Scene won't know which engine to display
- EngineSceneLoader will fall back to `fallbackEngine` (if set) or show an error

### ❌ **ReturnToScroll Flag**
- If you don't handle `HomeSceneUIController.ReturnToScroll`, returning from Main Scene might show the wrong panel
- Or implement your own state management for return behavior

---

## Testing Your Changes

1. **Forward Flow**: Home Scene → Main Scene
   - Select an engine card in your new Home Scene
   - Verify the correct engine appears in Main Scene
   - Check that transitions are smooth (fade in/out)

2. **Backward Flow**: Main Scene → Home Scene
   - Click the Back button in Main Scene
   - Verify it returns to your new Home Scene
   - Check that it shows the correct panel (Engine Scroll, not Start Page)

3. **Scene Names**
   - Verify all scene name strings match exactly
   - Check Build Settings has both scenes enabled
   - Test build to ensure scenes load correctly outside Editor

---

## Summary Checklist

When replacing Home Scene with a new scene:

- [ ] Create your new scene file
- [ ] Add new scene to Build Settings (remove old if desired)
- [ ] Update `EngineSceneLoader.homeSceneName` to match new scene name
- [ ] Ensure new scene has SceneTransitionManager
- [ ] Ensure new scene has EngineSessionData reference
- [ ] Ensure new scene has EngineRegistry reference
- [ ] Implement engine selection logic that calls `sessionData.Select()` and `LoadScene("Main Scene")`
- [ ] (Optional) Update XROriginScenePose if using custom camera positions
- [ ] (Optional) Update editor tools if they reference the old scene path
- [ ] Test both forward (Home→Main) and backward (Main→Home) navigation
- [ ] Test in both Editor and Build

---

## Questions?

- **Where is the scene name hardcoded?** 
  - Main answer: `EngineSceneLoader.homeSceneName` field
  - Also check: XROriginScenePose data, editor tools

- **Can I rename both scenes?**
  - Yes, just update all references to match the new names
  - Main Scene name goes in `HomeSceneManager.engineSceneName`
  - Home Scene name goes in `EngineSceneLoader.homeSceneName`

- **Do I need to keep the same UI structure?**
  - No, as long as you maintain the core functionality:
    1. Select an engine
    2. Store in EngineSessionData
    3. Load Main Scene via SceneTransitionManager

---

*Last Updated: Based on current project structure*
