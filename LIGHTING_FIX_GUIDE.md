# Lighting Fix Guide — Scene Transition Lighting Loss

## Problem Diagnosis

When you open the **Main Scene** directly in the Editor and press Play, the lighting looks perfect — warm, well-lit, with proper baked lightmaps and ambient lighting.

When you arrive at the **Main Scene** via scene transition (Home Scene → select engine → Main Scene), the lighting is flat, dark, and the baked lightmaps appear to be missing.

---

## Root Cause

Unity's baked lighting data (lightmaps, ambient settings, skybox, reflection probes) is stored per-scene in a `LightingData.asset` file. When you load a scene via `SceneManager.LoadSceneAsync()`, Unity **does not always re-apply** the scene's lighting environment correctly, especially when:

1. **DontDestroyOnLoad objects exist** — `SceneTransitionManager` uses `DontDestroyOnLoad`, which keeps it alive across scene loads. This creates an ambiguous "active scene" context for one frame, causing Unity to skip re-applying the incoming scene's `RenderSettings`.

2. **Lightmap indices aren't re-linked** — After an async scene load, Unity may not re-process the per-renderer lightmap indices, leaving renderers without their baked lightmap textures.

3. **Ambient probe isn't rebuilt** — The spherical harmonics ambient probe (which provides ambient lighting from the skybox) isn't automatically updated after a scene transition, so it stays at whatever the previous scene left it at (often flat grey).

---

## The Fix

I've created two fixes:

### 1. **LightingRestorer.cs** (NEW)
A script that runs on `Awake()` (the earliest safe point after scene load) and:
- Forces Unity to re-apply baked lightmap data
- Rebuilds the ambient probe from the skybox
- Optionally restores specific `RenderSettings` values (ambient intensity, skybox material, etc.)

### 2. **SceneTransitionManager.cs** (UPDATED)
Now waits **two frames** after scene activation instead of one, giving `LightingRestorer.Awake()` time to run before the fade-in starts.

---

## Setup Instructions

### Step 1: Add LightingRestorer to Main Scene

1. Open the **Main Scene** in Unity
2. Create a new empty GameObject (Right-click in Hierarchy → Create Empty)
3. Rename it to **"[LightingRestorer]"**
4. Add the **LightingRestorer** component to it:
   - In the Inspector, click **Add Component**
   - Search for **LightingRestorer**
   - Click to add it

### Step 2: Configure LightingRestorer

The script has several optional overrides. Here's what to set:

#### **Ambient Light Override**
1. In Unity, go to **Window → Rendering → Lighting**
2. Click the **Environment** tab
3. Note your settings:
   - **Ambient Mode** (Skybox, Flat, or Trilight)
   - **Ambient Intensity** (usually 1.0)
   - **Ambient Color** (if using Flat mode)

4. In the **LightingRestorer** Inspector:
   - Check **Override Ambient**
   - Set **Ambient Mode** to match your Lighting window
   - Set **Ambient Intensity** to match (usually 1.0)
   - If using Flat mode, set **Ambient Color** to match

#### **Skybox Override** (Recommended)
1. In the Lighting window → Environment tab, note your **Skybox Material**
2. In the **LightingRestorer** Inspector:
   - Check **Override Skybox**
   - Drag your **Skybox Material** into the **Skybox Material** slot
   - (If you don't have a skybox, leave this unchecked)

#### **Reflection Override** (Optional)
1. In the Lighting window → Environment tab, note your **Reflection Intensity**
2. In the **LightingRestorer** Inspector:
   - Check **Override Reflections**
   - Set **Reflection Intensity** to match (usually 1.0)

#### **Debug**
- Leave **Log On Restore** checked — this will print a console message when lighting is restored, so you can verify it's working

### Step 3: Test

1. **Save the Main Scene**
2. Open the **Home Scene**
3. Press **Play**
4. Select an engine
5. Wait for the scene transition
6. Check the Console — you should see:
   ```
   [LightingRestorer] Lighting restored in 'Main Scene'. Lightmaps: X | Ambient mode: Skybox | Ambient intensity: 1.00 | Skybox: YourSkyboxName
   ```
7. **Verify the lighting looks correct** — it should now match the Editor view

---

## If Lighting Still Looks Wrong

### Check 1: Lightmaps are baked
1. Open **Main Scene**
2. Go to **Window → Rendering → Lighting**
3. Click the **Baked Lightmaps** tab at the bottom
4. You should see lightmap textures listed
5. If empty, you need to bake lighting:
   - Click **Generate Lighting** at the bottom of the Lighting window
   - Wait for the bake to complete (can take several minutes)

### Check 2: Renderers have lightmap indices
1. Select any static object in the scene (walls, floor, ceiling)
2. In the Inspector, check the **Mesh Renderer** component
3. Expand **Lightmapping** section
4. **Lightmap Index** should be a number (0, 1, 2, etc.), not -1
5. If it's -1, the object isn't using lightmaps:
   - Make sure the object is marked **Static** (checkbox at top of Inspector)
   - Re-bake lighting

### Check 3: Scene has LightingData.asset
1. In the Project window, navigate to **Assets/Scenes/Main Scene/**
2. You should see **LightingData.asset** and **ReflectionProbe-0.exr**
3. If missing, you need to bake lighting (see Check 1)

### Check 4: Ambient mode matches
1. Open **Main Scene**
2. Go to **Window → Rendering → Lighting → Environment**
3. Note the **Ambient Mode** (Skybox, Flat, or Trilight)
4. Make sure **LightingRestorer → Ambient Mode** matches exactly

### Check 5: SceneTransitionManager is updated
1. In the Project window, open **Assets/Scripts/Engine/SceneTransitionManager.cs**
2. Find the `FadeAndLoad` method
3. After `op.allowSceneActivation = true;`, you should see:
   ```csharp
   yield return null;
   yield return null;
   ```
   (Two `yield return null` lines, not one)
4. If you only see one, the fix wasn't applied — re-apply the changes

---

## Advanced: Manual Lighting Refresh

If you need to force a lighting refresh at runtime (e.g., after spawning a new engine prefab), you can call:

```csharp
var restorer = FindFirstObjectByType<LightingRestorer>();
restorer?.ForceRefresh();
```

---

## Technical Details

### What DynamicGI.UpdateEnvironment() does
- Re-bakes the spherical harmonics ambient probe from the current skybox
- Updates the ambient lighting contribution to all renderers
- Forces Unity to re-process lightmap assignments

### Why we wait two frames in SceneTransitionManager
- **Frame 1**: Scene GameObjects are created, `Awake()` runs (LightingRestorer restores lighting)
- **Frame 2**: `Start()` runs, lighting is fully applied
- This ensures the new scene is fully set as the active scene before we re-attach the fade quad

### Why DontDestroyOnLoad causes issues
- Objects marked `DontDestroyOnLoad` exist in a "no scene" limbo
- Unity uses the **active scene** to determine which `RenderSettings` to apply
- When the active scene context is ambiguous (because a DontDestroyOnLoad object is still processing), Unity may skip re-applying the incoming scene's lighting environment

---

## Alternative Fix (If Above Doesn't Work)

If the above fix doesn't work, you can try removing `DontDestroyOnLoad` from `SceneTransitionManager`:

1. Open **Assets/Scripts/Engine/SceneTransitionManager.cs**
2. Find the `Awake()` method
3. Comment out or remove the `DontDestroyOnLoad(gameObject);` line
4. Instead, place a **SceneTransitionManager** prefab in **both** the Home Scene and Main Scene
5. This way each scene has its own instance, avoiding the DontDestroyOnLoad issue

**Downside**: The fade quad will be recreated on each scene load, which may cause a brief flicker.

---

## Summary

✅ **LightingRestorer.cs** — Forces Unity to re-apply baked lighting on scene load  
✅ **SceneTransitionManager.cs** — Waits two frames to let lighting restore before fade-in  
✅ **Setup** — Add LightingRestorer to Main Scene, configure ambient/skybox overrides  
✅ **Test** — Scene transition should now preserve lighting correctly

If you still have issues after following this guide, check the Console for errors and verify your lightmaps are baked correctly.
