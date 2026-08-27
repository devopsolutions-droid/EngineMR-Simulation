# Simple Part Explorer - Prefab Button Setup

## Your Button Prefabs

You have button prefabs in:
```
Assets/Prefabs/Hover Panel & button Prefab/
├─ Previous.prefab
└─ Next.prefab
```

These prefabs have:
- ✓ Image component (the button visual)
- ✓ CleanButton script (hover/click effects)
- ✓ CanvasGroup (for alpha control)
- ✓ AudioSource (for button sounds)

SimplePartExplorer is now **fully compatible** with these prefabs!

---

## **3-Minute Setup**

### **Step 1: Add Component**
```
Create empty GameObject "SimplePartExplorer"
Add SimplePartExplorer component
```

### **Step 2: Assign Tablet UI Text**
```
Tablet Part Name: [Your TextMeshProUGUI]
Tablet Part Description: [Your TextMeshProUGUI]
Step Counter: [Your TextMeshProUGUI]
```

### **Step 3: Assign Button Images**
```
Previous Button Image: [Previous button's Image component]
Next Button Image: [Next button's Image component]
```

**How to find the Image components:**
1. Select your Previous button in the scene
2. In Inspector, find the **Image** component
3. Drag it into "Previous Button Image" field
4. Repeat for Next button

### **Step 4: Assign Monitor UI**
```
Monitor Part Name: [Your TextMeshProUGUI on wall monitor]
Monitor Part Description: [Your TextMeshProUGUI on wall monitor]
Audio Source: [Your AudioSource]
```

### **Step 5: Wire "Show Working" Button**
```
Select "Show Working" button
Button component → On Click () → Add
Drag SimplePartExplorer GameObject
Select SimplePartExplorer → StartExplorer()
```

---

## **That's It!**

SimplePartExplorer now:
- ✓ Works with your prefab buttons
- ✓ Uses CanvasGroup for alpha control
- ✓ Fades buttons when disabled
- ✓ Respects CleanButton hover effects
- ✓ Plays button sounds

---

## **Inspector Setup**

```
SimplePartExplorer (Component)
├─ UI - Tablet
│  ├─ Tablet Part Name: [assign]
│  ├─ Tablet Part Description: [assign]
│  ├─ Previous Button Image: [assign]
│  ├─ Next Button Image: [assign]
│  └─ Step Counter: [assign]
├─ UI - Wall Monitor
│  ├─ Monitor Part Name: [assign]
│  ├─ Monitor Part Description: [assign]
│  └─ Audio Source: [assign]
└─ Settings
   └─ Ghost Alpha: 0.2
```

**Notice:** Only 8 fields! Clean and simple!

---

## **How It Works**

### **Button State Management**
```
When disabled:
├─ CanvasGroup.alpha: 1.0 → 0.5 (fades)
├─ CanvasGroup.interactable: false
└─ User can't click

When enabled:
├─ CanvasGroup.alpha: 0.5 → 1.0 (bright)
├─ CanvasGroup.interactable: true
└─ User can click
```

### **Pointer Click Handling**
```
User clicks button
    ↓
OnPointerClick() fires
    ↓
Check which button was clicked
    ↓
Call PreviousPart() or NextPart()
```

---

## **Button Behavior**

### **First Part**
- Previous button: FADED (disabled)
- Next button: BRIGHT (enabled)

### **Middle Part**
- Previous button: BRIGHT (enabled)
- Next button: BRIGHT (enabled)

### **Last Part**
- Previous button: BRIGHT (enabled)
- Next button: FADED (disabled)

---

## **Testing**

1. Click "Show Working"
2. See first part highlighted
3. Previous button should be faded
4. Next button should be bright
5. Click Next button
6. Next part shows
7. UI updates
8. Click Previous button
9. Previous part shows
10. Navigate to last part
11. Next button should be faded

---

## **Customization**

### **Change Fade Amount**
In SimplePartExplorer.cs, UpdateButtonStates():
```csharp
previousButtonCanvasGroup.alpha = canGoPrevious ? 1f : 0.5f;
// Change 0.5f to your value (0.3f = more faded, 0.7f = less faded)
```

### **Change Transparency of Other Parts**
In SimplePartExplorer Inspector:
```
Ghost Alpha: 0.1 (very transparent) to 0.8 (mostly visible)
```

---

## **Prefab Compatibility**

Your prefabs have these components:
- ✓ **Image** - SimplePartExplorer uses this
- ✓ **CleanButton** - Provides hover effects (still works!)
- ✓ **CanvasGroup** - SimplePartExplorer uses this for alpha
- ✓ **AudioSource** - Button sounds still play
- ✓ **Animator** - Empty but doesn't interfere

Everything works together perfectly!

---

## **Summary**

**3-minute setup. Prefab buttons. Any engine. Scales to 100.**

- Add component
- Assign UI elements
- Assign button Image components
- Wire "Show Working" button
- Done!

---

## **Troubleshooting**

| Problem | Solution |
|---------|----------|
| Buttons don't respond | Check Image components are assigned |
| Buttons don't fade | Verify CanvasGroup is being updated |
| Engine not found | Make sure engine is in scene |
| UI doesn't show | Check TextMeshProUGUI components assigned |

---

**SimplePartExplorer is ready to use with your prefab buttons!**
