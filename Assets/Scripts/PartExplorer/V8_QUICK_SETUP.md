# V8 Hot Rod - Part Explorer Quick Setup (15 Minutes)

## Great News! 🎉

You already have all the part data! We just need to:
1. Create PartExplorerData
2. Auto-populate it from your existing manifest
3. Add UI and buttons
4. Done!

## Step-by-Step Setup

### Step 1: Create PartExplorerData (1 min)

1. Right-click in Assets
2. Create → Engine VR → Part Explorer → Part Explorer Data
3. Name it "V8HotRodExplorer"

### Step 2: Auto-Populate from Existing Data (2 min)

1. Create empty GameObject in your scene called "PopulatorHelper"
2. Add **PartExplorerAutoPopulator** component
3. In Inspector, assign:
   - **Engine Part Manifest**: Drag "V8HotRedManifest" from Assets/ScriptableObjects/Data/Engines/V8HotRed/
   - **Explorer Data**: Drag "V8HotRodExplorer"
   - **Engine Root**: Drag your V8 engine from the scene

4. Click the "Populate Explorer Data" button (in the script)
   - Or call it from code: `populator.PopulateExplorerData();`

5. Check the console - you should see:
   ```
   Added part: Cylinder Head
   Added part: Crankshaft
   ... (all parts)
   Successfully added X parts to explorer data!
   ```

6. Delete the PopulatorHelper GameObject (you don't need it anymore)

### Step 3: Add PartExplorerSystem (2 min)

1. Create empty GameObject "PartExplorerSystem"
2. Add **PartExplorerController** component
3. Assign:
   - **Explorer Data**: Drag "V8HotRodExplorer"
   - Leave other settings as default

### Step 4: Create UI Elements (5 min)

On your tablet, add these 5 UI elements:

**1. Part Name Text**
- Right-click tablet → TextMeshPro → Text
- Name: "PartNameText"
- Font Size: 28, Color: White
- Position: Top

**2. Part Description Text**
- Right-click tablet → TextMeshPro → Text
- Name: "PartDescriptionText"
- Font Size: 18, Color: Light gray
- Enable "Word Wrapping"
- Position: Below name

**3. Part Counter Text**
- Right-click tablet → TextMeshPro → Text
- Name: "PartCounterText"
- Font Size: 16, Color: Gray
- Position: Top-right

**4. Previous Button**
- Right-click tablet → Button - TextMeshPro
- Name: "PreviousButton"
- Text: "← Previous"
- Position: Bottom-left

**5. Next Button**
- Right-click tablet → Button - TextMeshPro
- Name: "NextButton"
- Text: "Next →"
- Position: Bottom-right

### Step 5: Add PartExplorerUIPanel (2 min)

1. Create empty GameObject "PartExplorerUIManager"
2. Add **PartExplorerUIPanel** component
3. Assign all UI elements:
   - Explorer Controller: [Drag PartExplorerSystem]
   - Part Name Text: [Drag PartNameText]
   - Part Description Text: [Drag PartDescriptionText]
   - Part Counter Text: [Drag PartCounterText]
   - Previous Button: [Drag PreviousButton]
   - Next Button: [Drag NextButton]
   - Check "Auto Hide Panels When Inactive"

### Step 6: Wire "Show Working" Button (2 min)

1. Select your "Show Working" button
2. Add **PartExplorerLauncher** component
3. Assign:
   - **Explorer Controller**: [Drag PartExplorerSystem]
4. Wire button's OnClick:
   - Click "+"
   - Drag the button itself
   - Select PartExplorerLauncher → LaunchExplorer()

### Step 7: Test! (1 min)

1. Play the scene
2. Click "Show Working"
3. See first part highlight with its name and description
4. Click Next/Previous to navigate
5. All part data comes from your existing manifest!

## That's It! 🚀

Your Part Explorer is now using all the data you already generated with Groq AI!

## What Happened

- Your V8HotRedManifest had all the part names and descriptions
- PartExplorerAutoPopulator read that data
- Created PartExplorerData with all parts automatically
- No manual data entry needed!

## Customization

### Make Parts More/Less Transparent
PartExplorerController → Ghost Alpha: 0.1 to 0.8

### Make Transitions Faster/Slower
PartExplorerController → Fade Duration: 0.2 to 1.0

### Add More Parts
If you add more parts to the manifest, just run PopulateExplorerData again!

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Parts don't populate | Check manifest and engine root are assigned |
| UI doesn't show | Verify TextMeshProUGUI components assigned |
| Parts don't highlight | Check EnginePart components on all parts |
| Buttons don't work | Verify button wiring in OnClick |

## Files Used

- V8HotRedManifest (your existing data)
- V8HotRodExplorer (new, auto-populated)
- PartExplorerController
- PartExplorerUIPanel
- PartExplorerLauncher
- PartExplorerAutoPopulator

## Next Steps

1. Follow steps 1-7 above
2. Test in your scene
3. Customize as needed
4. Done!

---

**That's all! Your existing data is now powering the Part Explorer!**
