# Simple Part Explorer - Image Button Setup

## Understanding Your Button Setup

Your Previous/Next buttons are **Image-based** (not Unity Buttons).

This means:
- They're Image components with click detection
- They use IPointerClickHandler for interaction
- SimplePartExplorer now supports both!

## Setup (3 Minutes)

### Step 1: Add Component
```
GameObject: SimplePartExplorer
Component: SimplePartExplorer
```

### Step 2: Assign Tablet UI
```
Tablet Part Name: [TextMeshProUGUI]
Tablet Part Description: [TextMeshProUGUI]
Step Counter: [TextMeshProUGUI]
```

### Step 3: Assign Button Images
```
Previous Button Image: [Your Previous Image]
Next Button Image: [Your Next Image]
```

**Important:** Assign the **Image components**, not the GameObjects!

### Step 4: Assign Monitor UI
```
Monitor Part Name: [TextMeshProUGUI]
Monitor Part Description: [TextMeshProUGUI]
Audio Source: [AudioSource]
```

### Step 5: Wire "Show Working" Button
```
"Show Working" Button
→ On Click () → Add
→ SimplePartExplorer.StartExplorer()
```

## That's It!

SimplePartExplorer automatically:
- ✓ Detects image-based buttons
- ✓ Adds GraphicRaycaster if needed
- ✓ Handles pointer clicks
- ✓ Fades buttons when disabled
- ✓ Works with both button types!

## How It Works

### Image Button Detection
```
SimplePartExplorer checks:
├─ Is previousButtonImage assigned?
├─ Is nextButtonImage assigned?
└─ If yes, enable pointer click handling
```

### Pointer Click Handling
```
User clicks on image
    ↓
OnPointerClick() fires
    ↓
Check which image was clicked
    ↓
Call PreviousPart() or NextPart()
```

### Button State Management
```
When disabled:
├─ Image alpha: 1.0 → 0.5 (fades out)
└─ User can't click

When enabled:
├─ Image alpha: 0.5 → 1.0 (fades in)
└─ User can click
```

## Inspector Setup

```
SimplePartExplorer (Component)
├─ UI - Tablet
│  ├─ Tablet Part Name: [assign]
│  ├─ Tablet Part Description: [assign]
│  ├─ Previous Button: [leave empty]
│  ├─ Next Button: [leave empty]
│  ├─ Previous Button Image: [assign your image]
│  ├─ Next Button Image: [assign your image]
│  └─ Step Counter: [assign]
├─ UI - Wall Monitor
│  ├─ Monitor Part Name: [assign]
│  ├─ Monitor Part Description: [assign]
│  └─ Audio Source: [assign]
└─ Settings
   ├─ Fade Duration: 0.5
   └─ Ghost Alpha: 0.2
```

## Button Types Supported

### Option 1: Unity Buttons
```
Assign to:
├─ Previous Button: [Button component]
└─ Next Button: [Button component]
```

### Option 2: Image-Based Buttons
```
Assign to:
├─ Previous Button Image: [Image component]
└─ Next Button Image: [Image component]
```

### Option 3: Both (Mixed)
```
Assign to:
├─ Previous Button: [Button component]
├─ Next Button Image: [Image component]
└─ Or any combination!
```

## How to Find Your Image Components

1. Select your Previous button GameObject in hierarchy
2. In Inspector, find the **Image** component
3. Drag that Image component into "Previous Button Image" field
4. Repeat for Next button

## Button Behavior

### When Enabled (Can Click)
```
Image Alpha: 1.0 (fully visible)
User can click
Next part shows
```

### When Disabled (Can't Click)
```
Image Alpha: 0.5 (faded)
User can't click
Nothing happens
```

## Testing

### Test 1: First Part
1. Click "Show Working"
2. See first part
3. Previous button should be faded (disabled)
4. Next button should be bright (enabled)

### Test 2: Middle Part
1. Click "Next" several times
2. Both buttons should be bright (enabled)
3. Can click either direction

### Test 3: Last Part
1. Click "Next" until last part
2. Next button should be faded (disabled)
3. Previous button should be bright (enabled)

## Customization

### Change Fade Amount
In SimplePartExplorer.cs, find UpdateButtonStates():
```csharp
color.a = canGoPrevious ? 1f : 0.5f;  // Change 0.5f to your value
```

### Change Button Colors
Instead of fading alpha, you can change color:
```csharp
// In UpdateButtonStates()
if (previousButtonImage != null)
{
    previousButtonImage.color = canGoPrevious ? Color.white : Color.gray;
}
```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Buttons don't respond | Check Image components are assigned |
| Buttons don't fade | Check alpha is being updated (0.5f value) |
| Buttons always enabled | Check button state logic |
| Clicks not detected | Ensure Canvas has GraphicRaycaster |

## Summary

SimplePartExplorer now supports:
- ✓ Unity Buttons
- ✓ Image-based buttons
- ✓ Mixed button types
- ✓ Automatic state management
- ✓ Visual feedback (fading)

**Just assign your image components and it works!**

---

**That's it! Your image buttons are now fully integrated!**
