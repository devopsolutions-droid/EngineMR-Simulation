# Scene Update Complete: "Home Scene" → "EngineButtons HomeScene"

## ✅ Changes Made

All references to "Home Scene" have been updated to "EngineButtons HomeScene".

---

## Files Modified

### 1. **EngineSceneLoader.cs** ✅
**Path**: `Assets/Scripts/Engine/EngineSceneLoader.cs`
**Change**: Line 48
```csharp
public string homeSceneName = "EngineButtons HomeScene";  // ← Updated
```
**Purpose**: This is the main change - when you click "Back" in Main Scene, it now loads "EngineButtons HomeScene"

---

### 2. **XROriginScenePose.cs** ✅
**Path**: `Assets/Scripts/XROriginScenePose.cs`
**Change**: Line 17 tooltip
```csharp
[Tooltip("Exact scene name (e.g. 'EngineButtons HomeScene' or 'Main Scene').")]
```
**Purpose**: Updated documentation to reflect new scene name

---

### 3. **HomeSceneUIController.cs** ✅
**Path**: `Assets/Scripts/Home/HomeSceneUIController.cs`
**Changes**: 
- Line 6: Updated class documentation comment
- Line 32: Updated comment about scene loading

**Purpose**: Updated documentation for clarity

---

### 4. **SceneTransitionManager.cs** ✅
**Path**: `Assets/Scripts/Engine/SceneTransitionManager.cs`
**Change**: Line 8 comment
```csharp
/// IMPORTANT: Place this in the EngineButtons HomeScene (the first scene that loads).
```
**Purpose**: Updated documentation

---

### 5. **AddVRKeyboardToHomeScene.cs** ✅
**Path**: `Assets/Scripts/Editor/AddVRKeyboardToHomeScene.cs`
**Change**: Line 13
```csharp
string scenePath = "Assets/Scenes/EngineButtons HomeScene.unity";
```
**Purpose**: Updated editor tool to target the correct scene

---

### 6. **EditorBuildSettings.asset** ✅ (Already configured)
**Path**: `ProjectSettings/EditorBuildSettings.asset`
**Status**: Already has "EngineButtons HomeScene" enabled and "Home Scene" disabled
```yaml
m_Scenes:
  - enabled: 1
    path: Assets/Scenes/EngineButtons HomeScene.unity
  - enabled: 0
    path: Assets/Scenes/Home Scene.unity  # ← Old scene (disabled)
  - enabled: 1
    path: Assets/Scenes/Main Scene.unity
```

---

## Scene Flow (Updated)

### Forward: EngineButtons HomeScene → Main Scene
```
1. User clicks engine card
2. EngineCardUI stores selection in EngineSessionData
3. Loads "Main Scene"
4. EngineSceneLoader activates selected engine
```

### Backward: Main Scene → EngineButtons HomeScene
```
1. User clicks Back button
2. EngineSceneLoader.GoHome() is called
3. Sets HomeSceneUIController.ReturnToScroll = true
4. Loads "EngineButtons HomeScene"  ← Now using correct scene name
5. HomeSceneUIController skips Start Page, shows Engine Selection
```

---

## Testing Checklist

Before running the project, verify:

- [ ] "EngineButtons HomeScene" exists at `Assets/Scenes/EngineButtons HomeScene.unity`
- [ ] Build Settings has "EngineButtons HomeScene" enabled (File → Build Settings)
- [ ] Scene name matches exactly (case-sensitive)
- [ ] SceneTransitionManager prefab exists in EngineButtons HomeScene
- [ ] EngineSessionData reference is set in EngineButtons HomeScene
- [ ] EngineRegistry reference is set in EngineButtons HomeScene

---

## What to Test

1. **Launch the app** - Should start in "EngineButtons HomeScene"
2. **Select an engine** - Should load Main Scene with correct engine
3. **Click Back button** - Should return to "EngineButtons HomeScene" and show engine selection grid
4. **Test scene transitions** - Should have smooth fade in/out

---

## Notes

- The old "Home Scene" is still in the project but **disabled** in Build Settings
- All code now references "EngineButtons HomeScene"
- Class names like `HomeSceneManager` and `HomeSceneUIController` remain unchanged (they're just class names, not scene references)

---

## If Something Goes Wrong

**Scene not found error?**
- Check the scene name in Build Settings matches exactly: "EngineButtons HomeScene"
- Verify the scene file exists at `Assets/Scenes/EngineButtons HomeScene.unity`

**Back button doesn't work?**
- Open Main Scene in Unity Editor
- Find the EngineSceneLoader component
- Verify `homeSceneName` field shows "EngineButtons HomeScene"

**No fade transition?**
- Make sure SceneTransitionManager prefab is in EngineButtons HomeScene
- Check that `HomeSceneManager` has the `sceneTransitionPrefab` reference set

---

✅ **Update Complete!** All references have been updated to use "EngineButtons HomeScene".
