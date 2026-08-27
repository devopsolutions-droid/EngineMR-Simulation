# Back Button Setup Guide

## Good News!
The back button functionality is **already fully implemented** in code. You just need to wire it up in the Unity scene.

---

## What's Already Done

✅ **TabletUIController.OnBackClicked()** — calls EngineSceneLoader.GoHome()  
✅ **EngineSceneLoader.GoHome()** — sets ReturnToScroll flag and loads Home Scene  
✅ **HomeSceneUIController** — detects the flag and skips the start page, going straight to engine selection  
✅ **SceneTransitionManager** — provides smooth fade transitions between scenes

---

## How to Add the Back Button to Your Scene

### Option 1: Use Existing Button (Recommended)

If you already have a BACK button in your tablet UI:

1. Open the **Main Scene** (Engine View Scene)
2. Find your **Tablet Canvas** in the hierarchy
3. Locate the **BACK button** GameObject
4. In the Inspector, find the **Button** component
5. Scroll to **On Click ()** events
6. Click **+** to add a new event
7. Drag the **TabletUIController** GameObject into the object field
8. Select **TabletUIController → OnBackClicked** from the dropdown

Done! The button will now return to the home scene.

---

### Option 2: Create a New Back Button

If you don't have a back button yet:

1. Open the **Main Scene**
2. Find your **Tablet Canvas** in the hierarchy
3. Right-click on the canvas → **UI → Button - TextMeshPro**
4. Rename it to **"Back Button"**
5. Position it in the top-left or bottom-left corner of the tablet
6. Select the button's **Text (TMP)** child
7. Change the text to **"← BACK"** or **"HOME"**
8. Style it to match your other buttons (font, color, size)

**Wire it up:**
1. Select the **Back Button** GameObject
2. In the Inspector, find the **Button** component
3. Scroll to **On Click ()** events
4. Click **+** to add a new event
5. Drag the **TabletUIController** GameObject into the object field
6. Select **TabletUIController → OnBackClicked** from the dropdown

---

### Option 3: Use a Sci-Fi Button Prefab

If you want to match the existing sci-fi style:

1. In the Project window, navigate to **Assets/Sci-fi Prefabs/**
2. Drag one of these prefabs into your Tablet Canvas:
   - **Blue Button.prefab**
   - **Purple Button.prefab**
   - **Button - Profile Variant.prefab** (has an icon)
3. Position it in your tablet UI
4. Rename it to **"Back Button"**
5. Change the button text to **"← BACK"** or **"HOME"**
6. Wire it up (same as Option 2 steps 1-6)

---

## Testing

1. Play the scene
2. Click **START** on the loading screen
3. Click the **BACK** button
4. You should see a fade transition
5. You should land on the **Engine Selection** panel (not the start page)
6. Select a different engine
7. Verify it loads correctly

---

## Troubleshooting

### Button doesn't work
- Check that **TabletUIController** is assigned in the scene
- Check that **EngineSceneLoader** exists in the scene
- Check the Console for errors

### Returns to start page instead of engine selection
- Check that **HomeSceneUIController.ReturnToScroll** is being set to `true`
- Check that the **Home Scene** has a **HomeSceneUIController** component

### No fade transition
- Check that **SceneTransitionManager** prefab is in the scene
- Or check that **HomeSceneManager** has the **sceneTransitionPrefab** assigned

### Wrong scene name
- Check **EngineSceneLoader → homeSceneName** field (should be "Home Scene" or whatever your home scene is called)
- Check that the scene name matches exactly in **Build Settings**

---

## Code Reference

If you need to call the back button from code:

```csharp
// From any script in the engine scene:
var tabletUI = FindFirstObjectByType<TabletUIController>();
tabletUI?.OnBackClicked();

// Or directly:
var loader = FindFirstObjectByType<EngineSceneLoader>();
loader?.GoHome();
```

---

## What Happens When You Click Back

1. **TabletUIController.OnBackClicked()** is called
2. Finds **EngineSceneLoader** in the scene
3. Calls **EngineSceneLoader.GoHome()**
4. Sets **HomeSceneUIController.ReturnToScroll = true** (static flag)
5. Loads the **Home Scene** via **SceneTransitionManager** (with fade)
6. **HomeSceneUIController.Start()** runs in the new scene
7. Detects **ReturnToScroll == true**
8. Skips the start page and shows the **Engine Selection** panel
9. Resets the flag to `false`

---

## Additional Features You Can Add

### Add a confirmation dialog
```csharp
public void OnBackClicked()
{
    // Show a "Are you sure?" dialog before going back
    if (EditorUtility.DisplayDialog("Return to Home", 
        "Return to engine selection?", "Yes", "Cancel"))
    {
        var loader = FindFirstObjectByType<EngineSceneLoader>();
        loader?.GoHome();
    }
}
```

### Add a keyboard shortcut
```csharp
void Update()
{
    if (Input.GetKeyDown(KeyCode.Escape))
    {
        OnBackClicked();
    }
}
```

### Add a VR controller button
```csharp
// In TabletUIController.Update()
if (OVRInput.GetDown(OVRInput.Button.Two)) // B button on right controller
{
    OnBackClicked();
}
```
