# V8 Hot Rod - Part Explorer Implementation Guide

This guide walks you through setting up the Part Explorer for your V8 Hot Rod engine step-by-step.

## Phase 1: Prepare Your Engine Parts (5 minutes)

### Step 1: Identify All V8 Parts

First, you need to know what parts are in your V8 engine. Look at your engine hierarchy in the scene and list all the parts. Common V8 parts include:

- Cylinder Block
- Cylinder Head
- Pistons
- Crankshaft
- Connecting Rods
- Valves
- Spark Plugs
- Oil Pan
- Intake Manifold
- Exhaust Manifold
- Timing Chain
- Water Pump
- Alternator
- Starter Motor

### Step 2: Verify EnginePart Components

For each part in your engine:
1. Select it in the hierarchy
2. Check it has an **EnginePart** component
3. If not, add one (Add Component → EnginePart)

This is important because the Part Explorer uses EnginePart to highlight/fade parts.

## Phase 2: Create PartExplorerData (5 minutes)

### Step 3: Create the Data Asset

1. In your Assets folder, navigate to a good location (e.g., Assets/ScriptableObjects/)
2. Right-click → Create → Engine VR → Part Explorer → Part Explorer Data
3. Name it "V8HotRodExplorer"

### Step 4: Add Parts to PartExplorerData

Select "V8HotRodExplorer" in the Inspector and add each part:

**Part 1: Cylinder Block**
- Part Name: "Cylinder Block"
- Part Description: "The main engine block that houses the cylinders and pistons. It's the foundation of the engine."
- Engine Part: [Drag the Cylinder Block from your scene]

**Part 2: Cylinder Head**
- Part Name: "Cylinder Head"
- Part Description: "Sits on top of the block and contains the valves and spark plugs. It seals the combustion chambers."
- Engine Part: [Drag the Cylinder Head from your scene]

**Part 3: Pistons**
- Part Name: "Pistons"
- Part Description: "Move up and down inside cylinders, converting fuel combustion into motion. There are 8 pistons in a V8."
- Engine Part: [Drag the Pistons from your scene]

**Part 4: Crankshaft**
- Part Name: "Crankshaft"
- Part Description: "Converts the linear motion of pistons into rotational motion. It's the main rotating shaft of the engine."
- Engine Part: [Drag the Crankshaft from your scene]

**Part 5: Connecting Rods**
- Part Name: "Connecting Rods"
- Part Description: "Connect pistons to the crankshaft, transferring motion. There are 8 connecting rods in a V8."
- Engine Part: [Drag the Connecting Rods from your scene]

**Part 6: Valves**
- Part Name: "Valves"
- Part Description: "Control the flow of air and fuel into and exhaust out of cylinders. They open and close precisely."
- Engine Part: [Drag the Valves from your scene]

**Part 7: Spark Plugs**
- Part Name: "Spark Plugs"
- Part Description: "Ignite the fuel-air mixture to start combustion. There are 8 spark plugs in a V8."
- Engine Part: [Drag the Spark Plugs from your scene]

**Part 8: Oil Pan**
- Part Name: "Oil Pan"
- Part Description: "Stores engine oil for lubrication. It sits at the bottom of the engine."
- Engine Part: [Drag the Oil Pan from your scene]

**Part 9: Intake Manifold**
- Part Name: "Intake Manifold"
- Part Description: "Distributes air-fuel mixture to each cylinder. It connects to the carburetor or fuel injector."
- Engine Part: [Drag the Intake Manifold from your scene]

**Part 10: Exhaust Manifold**
- Part Name: "Exhaust Manifold"
- Part Description: "Collects exhaust gases from cylinders and directs them out. It connects to the exhaust system."
- Engine Part: [Drag the Exhaust Manifold from your scene]

## Phase 3: Add Scene Components (5 minutes)

### Step 5: Create PartExplorerSystem GameObject

1. In your engine scene, create a new empty GameObject
2. Name it "PartExplorerSystem"
3. Add the **PartExplorerController** component to it

### Step 6: Configure PartExplorerController

In the Inspector:
- **Explorer Data**: Drag "V8HotRodExplorer" here
- **Fade Duration**: 0.5 (seconds for transition)
- **Ghost Alpha**: 0.2 (0 = invisible, 1 = fully visible)

The Ghost Alpha of 0.2 means other parts will be 20% visible (80% transparent).

## Phase 4: Create UI Elements (10 minutes)

### Step 7: Add UI to Your Tablet

On your existing tablet UI Canvas, add these elements:

**Element 1: Part Name Text**
1. Right-click on your tablet panel → TextMeshPro → Text
2. Name it "PartNameText"
3. In Inspector:
   - Text: "Part Name"
   - Font Size: 28
   - Color: White
   - Alignment: Top-Left
   - Position: Top of tablet (e.g., 0, -20)
   - Size: Full width, 50 pixels height

**Element 2: Part Description Text**
1. Right-click on your tablet panel → TextMeshPro → Text
2. Name it "PartDescriptionText"
3. In Inspector:
   - Text: "Part description goes here"
   - Font Size: 18
   - Color: Light gray (200, 200, 200)
   - Alignment: Top-Left
   - Enable "Word Wrapping"
   - Position: Below part name (e.g., 0, -80)
   - Size: Full width, 150 pixels height

**Element 3: Part Counter Text**
1. Right-click on your tablet panel → TextMeshPro → Text
2. Name it "PartCounterText"
3. In Inspector:
   - Text: "Part 1 / 10"
   - Font Size: 16
   - Color: Gray (150, 150, 150)
   - Alignment: Top-Right
   - Position: Top-right corner (e.g., -10, -20)
   - Size: 100 pixels width, 30 pixels height

**Element 4: Previous Button**
1. Right-click on your tablet panel → Button - TextMeshPro
2. Name it "PreviousButton"
3. In Inspector:
   - Text: "← Previous"
   - Font Size: 18
   - Position: Bottom-left (e.g., 10, 10)
   - Size: 100 pixels width, 50 pixels height
   - Color: Blue or your preferred color

**Element 5: Next Button**
1. Right-click on your tablet panel → Button - TextMeshPro
2. Name it "NextButton"
3. In Inspector:
   - Text: "Next →"
   - Font Size: 18
   - Position: Bottom-right (e.g., -110, 10)
   - Size: 100 pixels width, 50 pixels height
   - Color: Blue or your preferred color

## Phase 5: Wire UI Panel (5 minutes)

### Step 8: Create PartExplorerUIManager

1. Create a new empty GameObject in your scene
2. Name it "PartExplorerUIManager"
3. Add the **PartExplorerUIPanel** component to it

### Step 9: Assign UI Elements

In the Inspector of PartExplorerUIPanel:
- **Explorer Controller**: Drag "PartExplorerSystem" (the one with PartExplorerController)
- **Part Name Text**: Drag "PartNameText"
- **Part Description Text**: Drag "PartDescriptionText"
- **Part Counter Text**: Drag "PartCounterText"
- **Previous Button**: Drag "PreviousButton"
- **Next Button**: Drag "NextButton"
- Check "Auto Hide Panels When Inactive"

## Phase 6: Add Launch Button (5 minutes)

### Step 10: Find or Create "Show Working" Button

On your tablet UI, find the "Show Working" button (or create one if it doesn't exist).

### Step 11: Add PartExplorerLauncher

1. Select the "Show Working" button
2. Add the **PartExplorerLauncher** component to it
3. In Inspector:
   - **Explorer Controller**: Drag "PartExplorerSystem"

### Step 12: Wire Button Click Event

1. Select the "Show Working" button
2. In the Inspector, find the Button component
3. Under "On Click ()", click "+"
4. Drag the "Show Working" button into the object field
5. In the dropdown, select PartExplorerLauncher → LaunchExplorer()

## Phase 7: Test! (5 minutes)

### Step 13: Play and Test

1. Press Play in Unity
2. Click the "Show Working" button
3. You should see:
   - First part (Cylinder Block) highlights with a glow
   - All other parts fade to semi-transparent
   - "Cylinder Block" appears as the part name
   - Description appears below
   - "Part 1 / 10" shows in the counter
   - Previous button is disabled (grayed out)
   - Next button is enabled (bright)

4. Click "Next" button
   - Cylinder Block fades back to normal
   - Cylinder Head highlights
   - UI updates with new part info
   - Counter shows "Part 2 / 10"

5. Keep clicking Next to see all parts
6. Click Previous to go back
7. At the last part, click Next to end the explorer
   - All parts restore to normal
   - UI hides

## Phase 8: Customize (Optional)

### Make Parts More/Less Transparent

In PartExplorerController Inspector:
- **Ghost Alpha**: 
  - 0.1 = Very transparent (barely visible)
  - 0.2 = Moderately transparent (default)
  - 0.5 = Somewhat visible
  - 0.8 = Mostly visible

### Make Transitions Faster/Slower

In PartExplorerController Inspector:
- **Fade Duration**:
  - 0.2 = Very fast
  - 0.5 = Normal (default)
  - 1.0 = Slow and smooth

### Add More Parts

If you have more parts:
1. Select "V8HotRodExplorer"
2. Click "+" in the Parts list
3. Add the new part

### Remove Parts

If you want to remove a part:
1. Select "V8HotRodExplorer"
2. Select the part in the list
3. Click "-" to remove

## Troubleshooting

### Parts don't highlight
**Problem**: When you click Next, parts don't glow
**Solution**: 
- Check that EnginePart components are on all parts
- Verify parts are assigned in PartExplorerData
- Check console for errors

### UI doesn't show
**Problem**: Part name and description don't appear
**Solution**:
- Verify all TextMeshProUGUI components are assigned in PartExplorerUIPanel
- Check that text components are active in the scene
- Verify PartExplorerUIPanel is assigned to PartExplorerController

### Parts don't fade
**Problem**: Other parts stay fully visible instead of fading
**Solution**:
- Check Ghost Alpha is > 0 (not 0)
- Verify EnginePart.SetGhost() is working
- Check that materials support transparency

### Buttons don't work
**Problem**: Clicking Next/Previous does nothing
**Solution**:
- Verify buttons are wired to PartExplorerUIPanel
- Check that PartExplorerController is in the scene
- Verify PartExplorerData has parts

### Can't find parts in scene
**Problem**: When trying to drag parts, they don't appear
**Solution**:
- Make sure your engine is instantiated in the scene
- Check that parts are children of the engine
- Verify parts have EnginePart components

## Summary

You now have a working Part Explorer for your V8 Hot Rod! Users can:
- Click "Show Working" to start exploring
- See one part highlighted at a time
- Read the part name and description
- Navigate with Previous/Next buttons
- See all 10 parts of the engine

The system is complete and ready to use!

## Next Steps

1. Test thoroughly with all parts
2. Adjust Ghost Alpha if needed (more/less transparent)
3. Adjust Fade Duration if needed (faster/slower transitions)
4. Add more parts if you have them
5. Customize descriptions to be more detailed if desired

Enjoy your Part Explorer! 🚀
