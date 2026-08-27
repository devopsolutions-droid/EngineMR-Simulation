# Simple Part Explorer - Final Setup Guide

## Complete Setup (3 Minutes)

### Step 1: Add Component
```
Create empty GameObject "SimplePartExplorer"
Add SimplePartExplorer component
```

### Step 2: Assign Tablet UI Text
```
Tablet Part Name: [Your TextMeshProUGUI for part name]
Tablet Part Description: [Your TextMeshProUGUI for description]
Step Counter: [Your TextMeshProUGUI for "Part X / Y"]
```

### Step 3: Assign Button Images
```
Previous Button Image: [Your Previous Image component]
Next Button Image: [Your Next Image component]
```

**How to find Image components:**
1. Select Previous button GameObject
2. In Inspector, find Image component
3. Drag it into "Previous Button Image" field
4. Repeat for Next button

### Step 4: Assign Monitor UI
```
Monitor Part Name: [Your TextMeshProUGUI on wall monitor]
Monitor Part Description: [Your TextMeshProUGUI on wall monitor]
Audio Source: [Your AudioSource component]
```

### Step 5: Wire "Show Working" Button
```
Select "Show Working" button
Button component → On Click () → Add
Drag SimplePartExplorer GameObject
Select SimplePartExplorer → StartExplorer()
```

## That's It! 🎉

## What Happens

```
User clicks "Show Working"
    ↓
First engine part highlights
Other parts fade (invisible)
    ↓
Part name shows on tablet
Part description shows on tablet
Part name shows on monitor
Part description shows on monitor
Audio explanation plays
    ↓
Previous button: FADED (disabled)
Next button: BRIGHT (enabled)
    ↓
User clicks Next
    ↓
Next part highlights
(repeat)
    ↓
At last part:
Previous button: BRIGHT (enabled)
Next button: FADED (disabled)
```

## Inspector Checklist

```
SimplePartExplorer (Component)
├─ UI - Tablet
│  ├─ Tablet Part Name: ✓ Assigned
│  ├─ Tablet Part Description: ✓ Assigned
│  ├─ Previous Button: [Leave empty]
│  ├─ Next Button: [Leave empty]
│  ├─ Previous Button Image: ✓ Assigned
│  ├─ Next Button Image: ✓ Assigned
│  └─ Step Counter: ✓ Assigned
├─ UI - Wall Monitor
│  ├─ Monitor Part Name: ✓ Assigned
│  ├─ Monitor Part Description: ✓ Assigned
│  └─ Audio Source: ✓ Assigned
└─ Settings
   ├─ Fade Duration: 0.5
   └─ Ghost Alpha: 0.2
```

## Features

✓ **Auto-detects engine** - Works with any engine  
✓ **Image buttons** - Supports your image-based buttons  
✓ **Dual display** - Tablet + wall monitor  
✓ **Audio support** - Plays explanations  
✓ **Smart buttons** - Fade when disabled  
✓ **Multi-engine** - Scales to 100 engines  

## Button Behavior

### Enabled (Can Click)
- Image alpha: 1.0 (fully visible)
- User can click
- Next part shows

### Disabled (Can't Click)
- Image alpha: 0.5 (faded)
- User can't click
- Nothing happens

## Testing Checklist

- [ ] Click "Show Working"
- [ ] First part highlights
- [ ] Other parts fade
- [ ] Part name shows on tablet
- [ ] Part description shows on tablet
- [ ] Part name shows on monitor
- [ ] Part description shows on monitor
- [ ] Audio plays
- [ ] Previous button is faded
- [ ] Next button is bright
- [ ] Click "Next"
- [ ] Next part highlights
- [ ] UI updates
- [ ] Audio plays
- [ ] Click "Previous"
- [ ] Previous part shows
- [ ] UI updates
- [ ] Navigate to last part
- [ ] Next button is faded
- [ ] Previous button is bright

## Multi-Engine Support

Works with:
- ✓ V8 Hot Rod
- ✓ Ramjet Engine
- ✓ Any engine you have
- ✓ 100 engines tomorrow

No changes needed!

## Customization

### Change Button Fade Amount
In SimplePartExplorer.cs, UpdateButtonStates():
```csharp
color.a = canGoPrevious ? 1f : 0.5f;  // Change 0.5f
```

### Change Transparency of Other Parts
In SimplePartExplorer Inspector:
```
Ghost Alpha: 0.1 (very transparent) to 0.8 (mostly visible)
```

### Change Transition Speed
In SimplePartExplorer Inspector:
```
Fade Duration: 0.2 (fast) to 1.0 (slow)
```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Buttons don't respond | Check Image components are assigned |
| Buttons don't fade | Verify alpha is updating |
| Engine not found | Make sure engine is in scene |
| UI doesn't show | Check TextMeshProUGUI components assigned |
| Audio doesn't play | Check AudioSource assigned and audio clips in manifest |

## Summary

**3-minute setup. Image buttons. Any engine. Scales to 100.**

- Add component
- Assign UI elements
- Assign image buttons
- Wire "Show Working" button
- Done!

---

**SimplePartExplorer is ready to use!**

**See IMAGE_BUTTON_SETUP.md for detailed image button guide.**
