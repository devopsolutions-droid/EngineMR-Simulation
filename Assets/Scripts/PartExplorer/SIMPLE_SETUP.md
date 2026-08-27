# Simple Part Explorer - 5 Minute Setup

## What It Does

1. User clicks "Show Working" button
2. First engine part highlights (glows)
3. Other parts fade to invisible
4. Part name and description show on tablet AND wall monitor
5. Audio explanation plays
6. User clicks Next/Previous to navigate
7. When done, all parts restore

## Setup (5 Minutes)

### Step 1: Create Explorer GameObject (1 min)

1. In your scene, create empty GameObject called "SimplePartExplorer"
2. Add **SimplePartExplorer** component to it

### Step 2: Assign Data Source (1 min)

In the Inspector:
- **Engine Part Manifest**: Drag "V8HotRedManifest" from Assets/ScriptableObjects/Data/Engines/V8HotRed/
- Leave Engine Root empty (it auto-finds)

### Step 3: Assign Tablet UI (1 min)

In the Inspector, assign:
- **Tablet Part Name**: TextMeshProUGUI for part name
- **Tablet Part Description**: TextMeshProUGUI for description
- **Previous Button**: Your "Previous" button
- **Next Button**: Your "Next" button
- **Step Counter**: TextMeshProUGUI for "Part X / Y"

### Step 4: Assign Monitor UI (1 min)

In the Inspector, assign:
- **Monitor Part Name**: TextMeshProUGUI on wall monitor
- **Monitor Part Description**: TextMeshProUGUI on wall monitor
- **Audio Source**: AudioSource component (for playing audio explanations)

### Step 5: Wire "Show Working" Button (1 min)

1. Select your "Show Working" button
2. In Button component, click "+" under On Click ()
3. Drag the "SimplePartExplorer" GameObject into the object field
4. Select SimplePartExplorer → StartExplorer()

## That's It! 🎉

Now when you click "Show Working":
- ✓ First part highlights
- ✓ Name and description appear on tablet
- ✓ Name and description appear on monitor
- ✓ Audio plays
- ✓ Other parts fade
- ✓ Navigate with Next/Previous

## Customization

### Make Parts More/Less Transparent
In SimplePartExplorer Inspector:
- **Ghost Alpha**: 0.1 (very transparent) to 0.8 (mostly visible)

### Make Transitions Faster/Slower
In SimplePartExplorer Inspector:
- **Fade Duration**: 0.2 (fast) to 1.0 (slow)

## How It Works

1. Reads V8HotRedManifest (your existing data)
2. Gets all part names and descriptions
3. Finds each part in the scene
4. Shows one at a time
5. Hides others
6. Displays info on tablet and monitor
7. Plays audio explanation

## Files Used

- SimplePartExplorer.cs (the main script)
- V8HotRedManifest (your existing data)
- Your existing UI elements

## That's All!

No complex setup, no manual data entry, no extra tools.

Just:
1. Add component
2. Assign manifest
3. Assign UI elements
4. Wire button
5. Done!

**5 minutes and you're ready to go!**
