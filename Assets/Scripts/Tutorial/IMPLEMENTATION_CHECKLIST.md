# Tutorial System Implementation Checklist

## Phase 1: Scene Setup

### Particle Systems
- [ ] Create "Airflow Particles" GameObject with ParticleSystem
  - [ ] Position at engine intake
  - [ ] Set color to blue (0.2, 0.6, 1.0)
  - [ ] Configure emission rate (50 particles/sec base)
  - [ ] Set lifetime (2-3 seconds)
  - [ ] Set velocity direction (forward)

- [ ] Create "Combustion Particles" GameObject with ParticleSystem
  - [ ] Position at combustion chamber
  - [ ] Set color to orange (1.0, 0.5, 0.0)
  - [ ] Configure emission rate (80 particles/sec base)
  - [ ] Set lifetime (1-2 seconds)
  - [ ] Set velocity direction (radial outward)

- [ ] Create "Exhaust Particles" GameObject with ParticleSystem
  - [ ] Position at engine exhaust
  - [ ] Set color to red (1.0, 0.2, 0.2)
  - [ ] Configure emission rate (60 particles/sec base)
  - [ ] Set lifetime (2-3 seconds)
  - [ ] Set velocity direction (backward)

### Tutorial Data
- [ ] Create TutorialData ScriptableObject
  - [ ] Right-click → Create → Engine VR → Tutorial → Engine Tutorial Data
  - [ ] Name it (e.g., "RamjetTutorial")
  - [ ] Add all tutorial steps in Inspector

### Core Components
- [ ] Create "TutorialSystem" empty GameObject
- [ ] Add EngineFlowVisualizer component
  - [ ] Assign Airflow Particles
  - [ ] Assign Combustion Particles
  - [ ] Assign Exhaust Particles
  - [ ] Verify colors are correct

- [ ] Add TutorialController component
  - [ ] Assign TutorialData
  - [ ] Assign EngineFlowVisualizer
  - [ ] Configure auto-play settings (optional)

## Phase 2: UI Setup

### Top Panel
- [ ] Create Canvas Panel at top of screen
- [ ] Add TextMeshProUGUI for step title
  - [ ] Name: "StepTitleText"
  - [ ] Style and position appropriately
  
- [ ] Add TextMeshProUGUI for step description
  - [ ] Name: "StepDescriptionText"
  - [ ] Enable word wrapping
  - [ ] Set appropriate font size

### Tablet Panel
- [ ] Add TextMeshProUGUI for step counter
  - [ ] Name: "StepCounterText"
  - [ ] Position on tablet

- [ ] Add Button for "Previous"
  - [ ] Name: "PreviousButton"
  - [ ] Position on left side of tablet
  - [ ] Style appropriately

- [ ] Add Button for "Next"
  - [ ] Name: "NextButton"
  - [ ] Position on right side of tablet
  - [ ] Style appropriately

### Tutorial UI Manager
- [ ] Create empty GameObject "TutorialUIManager"
- [ ] Add TutorialUIPanel component
  - [ ] Assign TutorialController
  - [ ] Assign Top Panel GameObject
  - [ ] Assign StepTitleText
  - [ ] Assign StepDescriptionText
  - [ ] Assign Tablet Panel GameObject
  - [ ] Assign PreviousButton
  - [ ] Assign NextButton
  - [ ] Assign StepCounterText
  - [ ] Enable "Auto Hide Panels When Inactive"

## Phase 3: Button Integration

### Launch Button
- [ ] Find or create "Show Working" button on tablet
- [ ] Add TutorialLauncher component
  - [ ] Assign TutorialController
  - [ ] Wire button's OnClick → TutorialLauncher.LaunchTutorial()

### Navigation Buttons
- [ ] Wire PreviousButton's OnClick
  - [ ] Already handled by TutorialUIPanel (auto-wired)

- [ ] Wire NextButton's OnClick
  - [ ] Already handled by TutorialUIPanel (auto-wired)

## Phase 4: Testing

### Basic Functionality
- [ ] Click "Show Working" button
  - [ ] [ ] Top panel appears
  - [ ] [ ] Tablet panel appears
  - [ ] [ ] Step 1 displays correctly
  - [ ] [ ] Airflow particles start (if Step 1 has airflow)

- [ ] Click "Next" button
  - [ ] [ ] Step advances
  - [ ] [ ] Title updates
  - [ ] [ ] Description updates
  - [ ] [ ] Particles transition smoothly
  - [ ] [ ] Step counter updates

- [ ] Click "Previous" button
  - [ ] [ ] Step goes back
  - [ ] [ ] All UI updates correctly
  - [ ] [ ] Particles transition back

- [ ] Navigate through all steps
  - [ ] [ ] Each step displays correctly
  - [ ] [ ] Particles show appropriate effects
  - [ ] [ ] No console errors

### Edge Cases
- [ ] Previous button disabled on first step
- [ ] Next button disabled on last step
- [ ] Tutorial ends after last step
- [ ] Panels hide when tutorial ends
- [ ] Can restart tutorial after ending

### Visual Effects
- [ ] Airflow particles visible and blue
- [ ] Combustion particles visible and orange
- [ ] Exhaust particles visible and red
- [ ] Transitions are smooth (not jerky)
- [ ] Particle intensity increases/decreases correctly

## Phase 5: Customization (Optional)

### Adjust Colors
- [ ] Edit EngineFlowVisualizer.cs
  - [ ] Modify airflowColor
  - [ ] Modify combustionColor
  - [ ] Modify exhaustColor

### Adjust Particle Emission
- [ ] Edit EngineFlowVisualizer.cs
  - [ ] Adjust emission rates in UpdateAirflow()
  - [ ] Adjust emission rates in UpdateCombustion()
  - [ ] Adjust emission rates in UpdateExhaust()

### Adjust Transition Speed
- [ ] Edit TutorialStep data
  - [ ] Modify transitionDuration for each step

### Enable Auto-Play
- [ ] In TutorialController Inspector
  - [ ] Enable "Enable Auto Play"
  - [ ] Set "Auto Play Step Duration"

## Phase 6: Debugging (Optional)

### Add Debug Display
- [ ] Create TextMeshProUGUI for debug info
- [ ] Add TutorialDebugger component
  - [ ] Assign TutorialController
  - [ ] Assign EngineFlowVisualizer
  - [ ] Assign debug text
  - [ ] Enable "Show Debug Info"

### Test Debug Features
- [ ] Debug display shows current step
- [ ] Debug display shows particle intensities
- [ ] Console logs show correct state

## Phase 7: Documentation

- [ ] Review SETUP_GUIDE.md
- [ ] Review README.md
- [ ] Add project-specific notes
- [ ] Document any customizations made

## Troubleshooting Checklist

If particles don't show:
- [ ] Verify particle systems are assigned in EngineFlowVisualizer
- [ ] Check particle system materials are set
- [ ] Verify particle lifetime is > 0
- [ ] Check emission rate is > 0
- [ ] Verify particle system is not culled

If UI doesn't update:
- [ ] Verify TutorialUIPanel is assigned to TutorialController
- [ ] Check all TextMeshProUGUI components are assigned
- [ ] Verify buttons are wired correctly
- [ ] Check console for errors

If steps don't progress:
- [ ] Verify TutorialData has steps
- [ ] Check TutorialController has TutorialData assigned
- [ ] Verify buttons are clickable
- [ ] Check console for warnings

## Final Verification

- [ ] All particle systems visible and working
- [ ] All UI panels display correctly
- [ ] Navigation works smoothly
- [ ] No console errors or warnings
- [ ] Tutorial can be started and stopped
- [ ] All steps display correctly
- [ ] Visual effects match step descriptions
- [ ] Performance is acceptable (no lag)

## Sign-Off

- [ ] Implementation complete
- [ ] All tests passed
- [ ] Ready for user testing
- [ ] Documentation complete
