# Part Explorer - Simplified Setup (10 Minutes)

## The Problem You're Facing

- Engine Data shows "None" in the dropdown
- Engine Root isn't visible in the inspector
- The engine is loaded dynamically by EngineSceneLoader

## The Solution

The auto-populator now **auto-finds** the engine! You don't need to manually assign anything.

---

## **Super Simple Setup (10 Minutes)**

### **Step 1: Create PartExplorerData (1 min)**

1. Right-click in Assets
2. Create → Engine VR → Part Explorer → Part Explorer Data
3. Name it "V8HotRodExplorer"

### **Step 2: Create PopulatorHelper (2 min)**

1. In your scene, create empty GameObject "PopulatorHelper"
2. Add **PartExplorerAutoPopulator** component
3. In Inspector, assign ONLY:
   - **Engine Part Manifest**: Drag "V8HotRedManifest"
   - **Explorer Data**: Drag "V8HotRodExplorer"
   - **Leave Engine Root EMPTY** (it will auto-find!)
   - Check "Auto Find Engine Root" ✓
   - Engine Root Search Name: "V8HotRed" (or leave default)

4. Click the "Populate Explorer Data" button
5. Check console - should say "Successfully added 15 parts!"
6. Delete PopulatorHelper

### **Step 3: Add PartExplorerSystem (2 min)**

1. Create empty GameObject "PartExplorerSystem"
2. Add **PartExplorerController** component
3. Assign:
   - **Explorer Data**: Drag "V8HotRodExplorer"
   - Leave other settings as default

### **Step 4: Create UI Elements (3 min)**

On your tablet, add these 5 elements:

**1. Part Name Text**
- Right-click tablet → TextMeshPro → Text
- Name: "PartNameText"
- Font Size: 28, Color: White

**2. Part Description Text**
- Right-click tablet → TextMeshPro → Text
- Name: "PartDescriptionText"
- Font Size: 18, Color: Light gray
- Enable "Word Wrapping"

**3. Part Counter Text**
- Right-click tablet → TextMeshPro → Text
- Name: "PartCounterText"
- Font Size: 16, Color: Gray

**4. Previous Button**
- Right-click tablet → Button - TextMeshPro
- Name: "PreviousButton"
- Text: "← Previous"

**5. Next Button**
- Right-click tablet → Button - TextMeshPro
- Name: "NextButton"
- Text: "Next →"

### **Step 5: Add PartExplorerUIPanel (1 min)**

1. Create empty GameObject "PartExplorerUIManager"
2. Add **PartExplorerUIPanel** component
3. Assign:
   - Explorer Controller: [Drag PartExplorerSystem]
   - Part Name Text: [Drag PartNameText]
   - Part Description Text: [Drag PartDescriptionText]
   - Part Counter Text: [Drag PartCounterText]
   - Previous Button: [Drag PreviousButton]
   - Next Button: [Drag NextButton]
   - Check "Auto Hide Panels When Inactive"

### **Step 6: Wire "Show Working" Button (1 min)**

1. Select your "Show Working" button
2. Add **PartExplorerLauncher** component
3. Assign:
   - **Explorer Controller**: [Drag PartExplorerSystem]
4. Wire button's OnClick:
   - Click "+"
   - Drag the button
   - Select PartExplorerLauncher → LaunchExplorer()

### **Step 7: Test! (1 min)**

1. Play scene
2. Click "Show Working"
3. See first part highlight with name and description
4. Click Next/Previous to navigate

---

## **What Changed**

**Before:**
- ❌ Had to manually find and assign engine root
- ❌ Engine Data dropdown showed "None"
- ❌ Confusing and error-prone

**Now:**
- ✓ Auto-populator finds engine automatically
- ✓ No manual engine root assignment needed
- ✓ Just assign manifest and explorer data
- ✓ Click one button and done!

---

## **How Auto-Find Works**

The auto-populator:
1. Looks for GameObject named "V8HotRed" (or your search name)
2. If not found, searches for any object with EnginePart components
3. Automatically uses that as the engine root
4. Populates all 15 parts from your manifest

---

## **Troubleshooting**

| Problem | Solution |
|---------|----------|
| "Could not find engine root" | Make sure your V8 engine is in the scene and active |
| "No parts found in manifest" | Check V8HotRedManifest is assigned correctly |
| "Part has no EnginePart component" | Verify all parts have EnginePart components |
| UI doesn't show | Check all TextMeshProUGUI components are assigned |
| Buttons don't work | Verify button wiring in OnClick |

---

## **That's It!**

You now have a working Part Explorer with:
- ✓ Auto-populated data from your manifest
- ✓ All 15 V8 parts with descriptions
- ✓ Highlighting and fading effects
- ✓ Navigation buttons
- ✓ Part info display

**No manual data entry needed!**

---

## **Next Steps**

1. Follow the 7 steps above
2. Test in your scene
3. Customize if needed (colors, transparency, etc.)
4. Done!

**Start now! It takes 10 minutes!**
