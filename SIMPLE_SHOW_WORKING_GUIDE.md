# Simplified "Show Working" Setup Guide

We have simplified the "Show Working" system to be completely automatic and use the **pre-existing Engine data** and manifests (`Assets > ScriptableObjects > Data > Engines`).

---

## 🌟 Expected Behavior
1. The user clicks **Show Working** on the tablet.
2. The explorer mode starts, disabling normal pointer/hover raycasts so the user can focus.
3. **Part 1 is fully visible, textured, and opaque**; all other engine parts are made **transparent/ghosted** (alpha ~0.1 - 0.3).
4. Part 1's name and description are displayed on **both the tablet and the wall monitor**.
5. The corresponding **audio explanation** starts playing.
6. The user clicks **Next** or **Previous** to navigate.
7. Only the current part is highlighted/opaque at any given step, with all other parts ghosted.
8. The **Stop Show Working** button/panel (which contains the Next and Previous buttons as children) is automatically **activated**, and all other main tablet buttons (X-Ray, Exploded, Grab, Show Working) are **deactivated**.
9. When the user clicks the **Stop Show Working** button, the explorer stops, the engine parts restore to their fully visible look, the main buttons are **reactivated**, and the **Stop Show Working** button/panel is **deactivated**.

---

## 🛠️ Step-by-Step Scene Setup (2 Minutes)

### 1. Add the Explorer Component
1. In your Unity hierarchy, find or create an empty GameObject named **`SimplePartExplorer`**.
2. Add the **`SimplePartExplorer`** component to it.

### 2. Auto-Wire the Reference Slots (One-Click)
1. Right-click the **`SimplePartExplorer`** component header in the Inspector.
2. Select **`Auto Wire UI References`** from the context menu.
3. It will scan the scene and automatically link:
   - **Tablet UI Buttons or Images** (Name, Description, Step Counter, Buttons/Images)
   - **Wall Monitor UI** (Name, Description)
   - **Audio Source**
4. *(Optional)* If you have a parent panel GameObject that wraps the entire explorer UI (Next/Prev buttons, text overlays, etc.), drag it into the **`Explorer Panel`** slot. It will be turned ON when the explorer starts, and OFF when it stops.

### 3. Assign References in EngineViewManager
1. Select the GameObject with the **`EngineViewManager`** component (usually the `EngineViewManager` empty object).
2. Look at the Inspector and assign:
   - **`Simple Part Explorer`**: Drag the `SimplePartExplorer` GameObject here.
   - **`Show Working Button`**: Drag your tablet's Show Working button GameObject here.
   - **`Stop Show Working Button`**: Drag your tablet's **Stop Show Working** button/panel (the one containing the Next and Previous buttons as children) here.
   - **`Default View Button`**: (Ensure this is already assigned to the Reset/Default view button).

### 4. Wire the "Show Working" Button in the Tablet
1. Select your **Show Working** button in the canvas hierarchy.
2. In the Inspector's **Button** component, look at the **`On Click ()`** list and click **`+`**.
3. Drag the **`Tablet`** GameObject (with the `TabletUIController` component) into the Object field.
4. Select **`TabletUIController -> OnShowWorkingClicked`** from the dropdown.

### 5. Wire the "Stop Show Working" Button in the Tablet
1. Select your **Stop Show Working** button in the canvas hierarchy.
2. In the Inspector's **Button** component, look at the **`On Click ()`** list and click **`+`**.
3. Drag the **`Tablet`** GameObject into the Object field.
4. Select **`TabletUIController -> OnStopShowWorkingClicked`** from the dropdown.

### 6. Wire the Next and Previous Buttons in the Tablet
1. Select your **Next** button in the canvas hierarchy.
2. In the Inspector's **Button** component, look at the **`On Click ()`** list and click **`+`**.
3. Drag the **`Tablet`** GameObject into the Object field.
4. Select **`TabletUIController -> OnNextClicked`** from the dropdown.
5. Select your **Previous** button in the canvas hierarchy.
6. In its `OnClick()` list, click **`+`**, drag the **`Tablet`** GameObject into the Object field, and select **`TabletUIController -> OnPreviousClicked`** from the dropdown.

---

## ⚙️ How Indexing & Steps Work
* The steps are indexed **directly by the order** of the parts listed inside each engine's `EnginePartManifest` asset (e.g. `CarEngineManifest`, `F6BoxerEngineManifest`, `V8HotRedManifest`, etc.).
* No manual indexing setup is required; the tool dynamically reads the active engine's manifest at runtime when the user clicks the button. If you want to change the order of steps, simply re-order the list of parts in the Engine's manifest asset in the inspector.

---

## 💡 Code Details
* **Fading & Ghosting/Transparency**: When navigating steps, the active part calls `RestoreOriginal()` to display its original material and textures. All other parts are set to `SetGhost()` to make them semi-transparent (alpha ~0.1 - 0.3) so the user maintains visual context of where the active part fits in the engine.
* **Standard Interactions Lock**: The system automatically calls `EngineInteractor.DisableInteraction()` when explorer starts (preventing hover popups or manual click-isolation while in step mode), and calls `EngineInteractor.EnableInteraction()` when stopping.
