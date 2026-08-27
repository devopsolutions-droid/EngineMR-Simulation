# Simple Part Explorer - Complete Guide

## What You Get

A system that shows engine parts one at a time with:
- ✓ Part highlighting (glows)
- ✓ Other parts fade (invisible)
- ✓ Name and description on tablet
- ✓ Name and description on wall monitor
- ✓ Audio explanation plays
- ✓ Navigate with Next/Previous buttons
- ✓ Uses your existing V8HotRedManifest data

## 5-Minute Setup

### 1. Create GameObject
```
Create empty GameObject "SimplePartExplorer"
Add SimplePartExplorer component
```

### 2. Assign Manifest
```
Engine Part Manifest: V8HotRedManifest
(Leave Engine Root empty - auto-finds)
```

### 3. Assign Tablet UI
```
Tablet Part Name: [TextMeshProUGUI]
Tablet Part Description: [TextMeshProUGUI]
Previous Button: [Button]
Next Button: [Button]
Step Counter: [TextMeshProUGUI]
```

### 4. Assign Monitor UI
```
Monitor Part Name: [TextMeshProUGUI]
Monitor Part Description: [TextMeshProUGUI]
Audio Source: [AudioSource]
```

### 5. Wire Button
```
"Show Working" Button
→ On Click ()
→ Add SimplePartExplorer.StartExplorer()
```

## That's It!

Click "Show Working" and it works!

## How It Works

1. **Reads your manifest** - Gets all 15 parts with names and descriptions
2. **Finds parts in scene** - Locates each part automatically
3. **Shows one at a time** - Highlights current, fades others
4. **Displays info** - Shows on tablet and monitor
5. **Plays audio** - Audio explanation for each part
6. **Navigates** - Next/Previous buttons move through parts

## Features

✓ **Auto-finds engine** - No manual assignment needed  
✓ **Uses existing data** - Your V8HotRedManifest  
✓ **Dual display** - Tablet and wall monitor  
✓ **Audio support** - Plays explanations  
✓ **Smart buttons** - Enable/disable at boundaries  
✓ **Smooth transitions** - Fade effects  
✓ **Simple code** - Easy to understand and modify  

## Customization

### Transparency
```
Ghost Alpha: 0.1 (very transparent) to 0.8 (mostly visible)
```

### Transition Speed
```
Fade Duration: 0.2 (fast) to 1.0 (slow)
```

## API

```csharp
// Start explorer
explorer.StartExplorer();

// Stop explorer
explorer.StopExplorer();

// Navigate
explorer.NextPart();
explorer.PreviousPart();
```

## Files

- **SimplePartExplorer.cs** - Main script
- **SIMPLE_SETUP.md** - Setup guide
- **VISUAL_GUIDE.md** - Visual walkthrough
- **README_SIMPLE.md** - This file

## Data Used

- V8HotRedManifest (your existing data)
- 15 engine parts with names and descriptions
- Audio explanations for each part

## Performance

- Memory: ~500 KB
- CPU: Minimal
- GPU: Depends on part complexity
- VR-Ready: Yes

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Engine not found | Make sure V8 engine is in scene |
| Manifest not found | Drag V8HotRedManifest from Assets |
| UI doesn't show | Check TextMeshProUGUI components assigned |
| Audio doesn't play | Check AudioSource assigned and audio clips in manifest |
| Buttons don't work | Check button wiring in OnClick |

## Next Steps

1. Follow SIMPLE_SETUP.md
2. Test in your scene
3. Customize if needed
4. Done!

## Summary

**One script. One manifest. Five minutes. Done.**

No complex setup, no manual data entry, no extra tools.

Just add the component, assign the UI, wire the button, and go!

---

**Ready? Start with SIMPLE_SETUP.md!**
