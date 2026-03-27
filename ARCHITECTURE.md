# Sol System Map — Full Architecture Document

This document covers every file, every class, every method, every trick, and every data flow in the application. Written for someone who has never seen the codebase before.

---

## Table of Contents

1. [What This App Does](#what-this-app-does)
2. [Project Structure](#project-structure)
3. [Design Principles](#design-principles)
4. [Data Files (JSON)](#data-files-json)
5. [C# Source Files](#c-source-files)
6. [3D Model Assets](#3d-model-assets)
7. [Application Flow](#application-flow)
8. [The Radiation Algorithm](#the-radiation-algorithm)
9. [UI System — How It All Gets Built](#ui-system--how-it-all-gets-built)
10. [The VR Preview System](#the-vr-preview-system)
11. [The Dropdown Animation System](#the-dropdown-animation-system)
12. [Coordinate Conversion Trick](#coordinate-conversion-trick)
13. [Event-Driven Architecture](#event-driven-architecture)
14. [Unity-Specific Tricks](#unity-specific-tricks)
15. [What's Good About This Codebase](#whats-good-about-this-codebase)
16. [What Could Be Improved](#what-could-be-improved)
17. [How to Modify Things](#how-to-modify-things)
18. [WebGL Deployment](#webgl-deployment)

---

## What This App Does

An interactive 3D solar system map inspired by Elite Dangerous. You click on planets and moons (actual 3D spheres in the scene) to see radiation feasibility data for running VR hardware in space. A left sidebar lets you tune parameters — shielding level, mission duration, hardware class, and a VR preview toggle. A bottom-right info panel shows computed fidelity results. A top bar shows the current location and hardware lifespan.

The core question the app answers: "If I put a specific computer at a specific place in the solar system, with a specific amount of shielding, for a specific number of years — what level of VR could it run before radiation kills it?"

---

## Project Structure

```
Assets/
  GameManager.cs              Central state manager (singleton)
  RadiationCalculator.cs      Static algorithm engine + JSON loader
  SpaceLocation.cs            Enum, data classes, MonoBehaviour markers
  SolarSystemBuilder.cs       Builds 3D planets/moons from JSON (singleton)
  SystemMapUI.cs              Entire UI built programmatically in code
  ClickDetector.cs            Raycasts mouse clicks onto 3D objects
  LocationPanelUI.cs          (Legacy file — may still exist but unused)
  Resources/
    RadData/
      solar_system.json       Planet and moon definitions
      dosimetry_table.json    Radiation levels per location
      shielding_table.json    Shielding reduction factors
      hardware_classes.json   Hardware specs (4 classes)
      fidelity_tiers.json     VR tier thresholds
      overhead_factors.json   Reference only — not loaded by code
    Models/
      Mario/                  Mario model (.obj + textures) — Tier 1 (LOW)
      CounterStrike/          CS:GIGN model (.obj + textures) — Tier 2 (MED)
      DoomSlayer/             Doom Marine model (.obj + textures) — Tier 3 (HIGH)
      ErrorText/              ERROR text model (.fbx + textures) — Tier 0 (NONE)
```

---

## Design Principles

**No Prefabs.** The entire UI and 3D scene is constructed in C# code at runtime. There are no `.prefab` files. Every Canvas, panel, slider, button, text element, and 3D sphere is created via `new GameObject()` and `AddComponent<>()`. This was a deliberate choice — the project was built from a text editor without access to the Unity Editor's visual tools.

**No Comments.** The codebase contains zero comments. Method and variable names are intended to be self-documenting.

**JSON-Driven Data.** All scientific data (radiation levels, hardware specs, shielding factors, fidelity thresholds) and all scene data (planet positions, sizes, colors, moons) live in JSON files under `Assets/Resources/RadData/`. Nothing is hardcoded in C# that could change independently of code.

**Singleton Pattern.** `GameManager.Instance` and `SolarSystemBuilder.Instance` are set in `Awake()` for global access. No dependency injection — components reach each other through these statics.

**Event-Driven Updates.** When state changes in `GameManager`, C# `Action` events fire. Subscribers (`SystemMapUI`) update themselves. The flow is always: user action → GameManager setter → event → UI update.

**Lazy Initialization.** `RadiationCalculator.EnsureLoaded()` loads JSON on first call. `SubLocationDatabase.Data` builds its dictionary on first access. Data is only loaded once and cached.

**WebGL-Safe.** All file I/O uses `Resources.Load<TextAsset>()` which is synchronous and works in WebGL builds (unlike `File.ReadAllText` or `StreamReader`). `JsonUtility.FromJson` is Unity's built-in JSON parser.

---

## Data Files (JSON)

### solar_system.json

Defines the 3D scene layout. Two arrays: `planets` and `moons`.

**Planets array** — 8 entries (Mercury through Neptune):
- `name`: Must match `SpaceEnvironment` enum exactly (case-sensitive for `Enum.Parse`)
- `distance`: X-axis position in world units. Mercury=2, Neptune=42. Planets are spread horizontally.
- `radius`: Sphere radius. Converted to diameter via `localScale = Vector3.one * radius * 2f`
- `color`: RGB float array [0-1]. Applied to a Standard shader material.
- `spin_speed`: Degrees per second the planet rotates. Negative = retrograde (Venus at -8, Uranus at -18).

**Moons array** — 5 entries (Moon, ISS under Earth; Io, Europa, Ganymede under Jupiter):
- `name`: Display name and lookup key
- `parent`: Must match a planet name exactly
- `radius`: Same as planets
- `color`: Same as planets

Moons are positioned vertically below their parent planet. Spacing is 1.4 world units. First moon starts at `-(planetRadius + 3.0)` below the planet's Y position.

### dosimetry_table.json

15 locations with annual radiation dose data from the research paper (Table 3.3).

Each entry:
- `id`: Lookup key (e.g., `"mercury_orbit"`, `"europa"`, `"leo_iss"`)
- `name`: Human-readable name shown nowhere in UI (stored in FidelityResult but overridden by app names)
- `tid`: Total Ionizing Dose in krad/year. The central input to the algorithm. `-1` means unknown (Uranus).
- `tid_min`, `tid_max`: Confidence range. Not used in calculations — reference only.
- `dominant_source`: One of `"gcr"`, `"spe"`, `"trapped_electrons"`. Determines which shielding column to use.
- `confidence`: How reliable the data is. Not used in calculations.

Key values: LEO/ISS = 0.3 krad/yr, Moon = 0.012, Mars Orbit = 0.006, Jupiter = 1000, Io = 18000, Europa = 3600, Ganymede = 220. These span 6 orders of magnitude.

The app maps its location names to dosimetry IDs via `RadiationCalculator.locationIdMap`:
```
Mercury → mercury_orbit    Venus → venus_orbit       Earth → leo_iss
Mars → mars_orbit          Jupiter → jupiter_orbit    Saturn → saturn_orbit
Uranus → uranus_orbit      Neptune → neptune_orbit    Moon → lunar_surface
ISS → leo_iss              Io → io                    Europa → europa
Ganymede → ganymede
```

Note: Earth and ISS both map to `leo_iss`. GEO, Mars Surface, and Interplanetary exist in the JSON but have no corresponding app locations.

### shielding_table.json

3 shielding levels with reduction factors per radiation source type.

| Level | Areal Density | GCR   | SPE  | Trapped Electrons |
|-------|---------------|-------|------|-------------------|
| Low   | 5 g/cm²       | 1.0   | 1.0  | 1.0               |
| Medium| 20 g/cm²      | 0.9   | 0.3  | 0.5               |
| High  | 45 g/cm²      | 0.8   | 0.1  | 0.1               |

Low is the baseline — the TID values in the dosimetry table already assume ~5-10mm aluminum shielding. Medium and High represent additional shielding on top of that. The `dominant_source` field in dosimetry determines which column is used.

GCR (galactic cosmic rays) is barely affected by shielding — even High only reduces it to 0.8x. SPE (solar particle events) is highly shieldable — High reduces to 0.1x. Trapped electrons (Jupiter system) are also very shieldable at 0.1x with heavy vaulting (Europa Clipper class).

### hardware_classes.json

4 hardware classes representing real space-capable computers.

| Class | GFLOPS | TID Tolerance | Overhead | Examples |
|-------|--------|---------------|----------|----------|
| Legacy Rad-Hard | 0.001 | 1000 krad | 1.0x | RAD750 |
| Modern Rad-Hard | 3.7 | 100 krad | 1.0x | RAD5545, GR740, RC64 |
| FPGA | 529.5 | 100 krad | 1.15x | Virtex-5QV, NG-ULTRA |
| COTS | 1567 | 30 krad | 1.0x | Jetson Nano/Xavier/Orin |

COTS has 1.0x overhead because the model assumes no software fault tolerance — raw performance, it survives or it dies. Rad-hard chips have protection built into the silicon (RHBD). FPGA overhead comes from SECDED error correction.

The tradeoff: COTS has ~400x the compute of modern rad-hard, but only 30 krad tolerance vs 100-1000 krad. In benign environments (Moon, Mars), COTS dominates. In harsh environments (Jupiter moons), only rad-hard survives.

### fidelity_tiers.json

4 VR fidelity tiers based on effective GFLOPS thresholds.

| Tier | Name | Short | Min GFLOPS | Reference System |
|------|------|-------|------------|-----------------|
| 0 | Infeasible | NONE | -1 (catch-all) | — |
| 1 | Wireframe VR | LOW | 0.08 | Virtuality 1000 (1991) |
| 2 | Textured VR | MED | 0.2 | Virtuality 2000 (1993-94) |
| 3 | Modern VR | HIGH | 567 | Meta Quest 1 (2019) |

The algorithm iterates tiers from highest to lowest (3→0) and picks the first one where `effectiveGFLOPS >= gflops_min`. Tier 0 has `gflops_min: -1` so it always matches as the fallback.

### overhead_factors.json

Reference-only documentation of fault tolerance techniques from Table 3.1. Lists RHBD (1.0x), SECDED (1.125x), ABFT (1.15x), DWC (2.0x), TMR (3.0x). This file is NOT loaded by any code — it exists purely for documentation.

---

## C# Source Files

### SpaceLocation.cs (63 lines)

Defines the core data types that everything else uses.

**`SpaceEnvironment` enum** — The 8 planets: Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus, Neptune. No Sun (no dosimetry data). This enum is the primary key for location selection throughout the app.

**`SubLocation` class** — Simple data holder with `name` (string) and `parent` (SpaceEnvironment). Marked `[System.Serializable]`.

**`SubLocationDatabase` static class** — Lazy-initialized dictionary mapping `SpaceEnvironment` to `List<SubLocation>`. Only two entries:
- Earth → [Moon, ISS]
- Jupiter → [Io, Europa, Ganymede]

The `Data` property calls `BuildDatabase()` on first access. `GetSubLocations(env)` returns the list or an empty list if the planet has no moons.

**`SpaceLocation` MonoBehaviour** — Attached to planet GameObjects. Single field: `environment` (SpaceEnvironment). Used by `ClickDetector` to identify what was clicked.

**`SubLocationMarker` MonoBehaviour** — Attached to moon GameObjects. Fields: `subLocationName` (string), `parentEnvironment` (SpaceEnvironment). Also used by `ClickDetector`.

### GameManager.cs (80 lines)

Central state manager. Singleton via `Instance = this` in `Awake()`.

**State properties** (all have `{ get; private set; }`):
- `CurrentSelection` (`SpaceEnvironment?`) — nullable, null until first click
- `CurrentSubLocation` (`string`) — null when a planet is selected, set when a moon is selected
- `ShieldingLevel` (`int`) — 0, 1, or 2 (LOW/MED/HIGH)
- `HardwareClassIndex` (`int`) — 0-3, defaults to 3 (COTS)
- `MissionDuration` (`int`) — 1-20 years, defaults to 1
- `VRPreviewEnabled` (`bool`) — toggle state
- `CurrentResult` (`FidelityResult`) — last computed result

**Static data**: `ShieldingNames` string array: { "LOW", "MEDIUM", "HIGH" }

**Events** (all `Action<T>`):
- `OnLocationChanged` — fires when a planet is selected
- `OnSubLocationChanged` — fires when a moon is selected
- `OnShieldingChanged` — fires when shielding slider moves
- `OnHardwareChanged` — fires when hardware button is clicked
- `OnMissionDurationChanged` — fires when mission slider moves
- `OnVRPreviewChanged` — fires when VR toggle flips
- `OnFidelityChanged` — fires after every recalculation with the new FidelityResult

**Methods**:
- `SelectLocation(SpaceEnvironment env)` — Sets `CurrentSelection`, clears `CurrentSubLocation` to null, fires `OnLocationChanged`, calls `RecalculateFidelity()`.
- `SelectSubLocation(string name)` — Sets `CurrentSubLocation`, fires `OnSubLocationChanged`, calls `RecalculateFidelity()`.
- `SetShieldingLevel(int level)` — Clamps to 0-2, fires event, recalculates.
- `SetHardwareClass(int index)` — Clamps to 0-3, fires event, recalculates.
- `SetMissionDuration(int years)` — Clamps to 1-20, fires event, recalculates.
- `SetVRPreview(bool enabled)` — Sets state, fires event. Does NOT recalculate (preview is display-only).
- `RecalculateFidelity()` — Private. Guards on `CurrentSelection.HasValue`. Calls `RadiationCalculator.Calculate()` with all 5 parameters. Stores result in `CurrentResult`. Fires `OnFidelityChanged`.

Every setter that affects the calculation triggers `RecalculateFidelity()`. This is the core pattern — any input change immediately produces a new output.

### RadiationCalculator.cs (219 lines)

Static utility class. No MonoBehaviour, no instance. All methods are `public static`.

**Data structures** — Private `[System.Serializable]` classes matching the JSON structure:
- `DosimetryEntry` (id, name, tid, tid_min, tid_max, dominant_source, confidence)
- `DosimetryTable` (locations array wrapper)
- `ShieldingEntry` (id, name, label, areal_density_gcm2, gcr, spe, trapped_electrons)
- `ShieldingTable` (levels array wrapper)
- `HardwareEntry` (id, name, short_name, examples, gflops, tid_tolerance_krad, overhead_factor)
- `HardwareTable` (classes array wrapper)
- `FidelityEntry` (tier, name, short_name, gflops_min)
- `FidelityTable` (tiers array wrapper)

**Static cached data**:
- `dosimetryMap` — Dictionary<string, DosimetryEntry> keyed by ID
- `shieldingLevels` — ShieldingEntry[]
- `hardwareClasses` — HardwareEntry[]
- `fidelityTiers` — FidelityEntry[]
- `locationIdMap` — Dictionary<string, string> mapping app names to dosimetry IDs
- `loaded` — bool flag for lazy init

**Public constants**:
- `HardwareIds` = { "radhard_legacy", "radhard_modern", "fpga", "cots" }
- `HardwareNames` = { "Legacy RH", "Modern RH", "FPGA", "COTS" }

**`EnsureLoaded()`** — Called at the start of every public method. Loads 4 JSON files via `Resources.Load<TextAsset>()` and parses them with `JsonUtility.FromJson<T>()`. Builds the `dosimetryMap` dictionary. Hardcodes the `locationIdMap` (13 entries mapping app names to dosimetry IDs). Sets `loaded = true`. Only runs once.

**`GetDosimetryId(SpaceEnvironment env, string moonName)`** — Returns the dosimetry table key. If `moonName` is provided, uses that as the lookup key. Otherwise uses `env.ToString()`. Returns null if not found.

**`Calculate(SpaceEnvironment env, string moonName, int shieldingIndex, int hardwareIndex, int missionYears)`** — The core algorithm. Returns a `FidelityResult` struct. Full algorithm detailed in its own section below.

**`FormatLifespan(float years)`** — Formats lifespan for display:
- Negative → "N/A"
- >= half of float.MaxValue → "Indefinite"
- >= 1000 → "~1,234 yrs" (with commas)
- >= 1 → "~1.5 yrs" (one decimal)
- >= 1 day → "~45 days"
- Otherwise → "~12 hrs"

**`FidelityResult` struct** (defined at top of file, outside the class):
- `tierLevel` (int, 0-3)
- `tierName` (string, e.g., "Modern VR")
- `tierShortName` (string, e.g., "HIGH")
- `lifespanYears` (float, -1 if N/A)
- `effectiveTID` (float, krad/yr after shielding)
- `totalMissionDose` (float, krad over full mission)
- `missionDurationYears` (int)
- `effectiveGFLOPS` (float, after overhead)
- `hardwareSurvives` (bool)
- `hardwareName` (string)
- `locationName` (string)

### SolarSystemBuilder.cs (252 lines)

Builds the 3D solar system from `solar_system.json`. Singleton via `Instance = this` in `Awake()`.

**Private JSON classes**: `PlanetJson`, `MoonJson`, `SolarSystemJson` — match the JSON structure. Marked `[Serializable]`.

**Public state**:
- `PlanetObjects` — `Dictionary<SpaceEnvironment, GameObject>` mapping enum values to their planet spheres
- `HasMoons(env)` — returns true if the planet has a moon container
- `IsExpanded(env)` — returns true if the planet's moons are currently visible

**Private state**:
- `moonContainers` — Dictionary mapping each planet to its moon container GameObject
- `planetSpinSpeeds` — Dictionary of rotation speeds per planet
- `expandedPlanet` — Which planet's moons are currently showing (nullable)
- `animatingPlanet`, `animatingOpen`, `animProgress`, `animDuration` — Animation state

**`Start()` → `BuildSolarSystem()`**:
1. Loads `solar_system.json` via `Resources.Load<TextAsset>`
2. Parses with `JsonUtility.FromJson<SolarSystemJson>`
3. Creates a root `"SolarSystem_Generated"` GameObject
4. For each planet: creates a sphere via `CreateSphere()`, positions at `(distance, 0, 0)`, adds `SpaceLocation` component with the enum value, adds `SphereCollider`, stores in `PlanetObjects` dictionary
5. Groups moons by parent planet. For each group: creates a container positioned at the parent's X, creates moon spheres inside it. Each moon gets a `SubLocationMarker` component and `SphereCollider`. Moons are spaced vertically: first at `startY = -(planetRadius + 3.0)`, then each 1.4 units lower.
6. All moon containers start as `SetActive(false)` — hidden until expanded.

**`CreateSphere(name, radius, color)`** — Creates a `PrimitiveType.Sphere`, sets `localScale = Vector3.one * radius * 2f` (radius to diameter), creates a new Standard shader material, sets its color. Returns the GameObject.

**`ParseEnv(string name)`** — Converts planet name string to `SpaceEnvironment` enum via `Enum.Parse`. This is why JSON planet names must exactly match enum names.

**`ParseColor(float[] c)`** — Converts a 3-element float array to a Unity `Color`.

**`Update()` — Three things happen every frame**:
1. **Planet spin**: Each planet rotates around its local Y axis at its `spin_speed` degrees/second.
2. **Moon spin**: All visible moons rotate at 20 deg/sec around their local Y axis.
3. **Animation tick**: If an animation is in progress, advances `animProgress` by `Time.deltaTime / animDuration`. Calls `UpdateAnimation()` or `FinishAnimation()`.

**Dropdown Animation System** (detailed in its own section):
- `ToggleMoons(env)` — Entry point. If already expanded, closes. If another is expanded, immediately hides it (no animation) then opens the new one. Blocks while animating.
- `StartAnimation(env, opening)` — Sets up animation state, activates container if opening.
- `UpdateAnimation()` — Computes eased progress `t = 1 - (1 - progress)³` (ease-out cubic). Calls `SetMoonContainerScale`.
- `FinishAnimation()` — Snaps to final state, deactivates if closing, clears animation state.
- `SetMoonContainerScale(env, t)` — Sets container's Y-scale to `t` and sets each moon renderer's material alpha to `t`. This creates a "fold down from planet" effect.

### ClickDetector.cs (34 lines)

Attached to the Main Camera. Has an optional `[SerializeField]` reference to `SystemMapUI`.

**`Update()`**:
1. Checks `Input.GetMouseButtonDown(0)` (left click)
2. Guards against UI click-through: `EventSystem.current.IsPointerOverGameObject()` — if true, the click is on a UI element, so ignore it
3. Casts a ray from the camera through the mouse position: `Camera.main.ScreenPointToRay(Input.mousePosition)`
4. If the ray hits something:
   - First checks for `SubLocationMarker` (moon). If found: selects the parent planet via `GameManager.Instance.SelectLocation(moon.parentEnvironment)`, then the moon via `SelectSubLocation(moon.subLocationName)`. Returns.
   - Then checks for `SpaceLocation` (planet). If found: selects via `GameManager.Instance.SelectLocation`. Also calls `systemMapUI.ExpandPlanet()` to toggle moon dropdown.

Moon check comes first because moons are children of containers that overlap with planets spatially.

### SystemMapUI.cs (966 lines)

The largest file. Builds the entire UI programmatically — no prefabs, no editor setup.

**Color Palette** (static readonly):
- `PanelBg` — very dark blue-gray, 85% opacity: `(0.05, 0.05, 0.08, 0.85)`
- `GoldText` — warm gold: `(0.9, 0.75, 0.3)`
- `GoldHighlight` — brighter gold for emphasis: `(1.0, 0.85, 0.4)`
- `DimText` — muted gold for labels: `(0.5, 0.45, 0.3)`
- `ButtonNormal` — dark for button backgrounds: `(0.1, 0.1, 0.14, 0.9)`
- `ArrowBg` — slightly lighter for dropdown arrows: `(0.12, 0.12, 0.16, 0.95)`
- `ToggleOn` — gold for active state: `(0.8, 0.65, 0.2)`
- `ToggleOff` — gray for inactive: `(0.25, 0.25, 0.28)`
- `SliderFill` — gold: `(0.8, 0.65, 0.2)`
- `SliderBg` — dark: `(0.15, 0.15, 0.18)`

**Instance variables**:
- Canvas and its RectTransform
- Text references: `tierInfoText` (top bar right), `selectionNameText` (info panel header), `selectionDetailText` (info panel body)
- Shielding slider + value text, Mission slider + value text
- VR preview state: `vrToggleOn` bool, `previewCamera`, `previewRenderTex`, `tierPreviewObjects[4]`, `previewDisplay`, `previewLabel`, `previewCanvasGroup`
- Hardware button arrays: `hardwareButtonImages[4]`, `hardwareButtonTexts[4]`
- Dropdown arrows: `List<DropdownArrow>`, `arrowsBuilt` flag

**`Start()`**:
1. Calls `BuildUI()` — constructs the Canvas and all UI panels
2. Calls `SetupPreviewScene()` — creates the offscreen preview camera, light, and 4 tier models
3. Subscribes to `GameManager` events: `OnLocationChanged`, `OnSubLocationChanged`, `OnFidelityChanged`
4. If a selection already exists (unlikely at start), updates UI
5. Hides preview section (alpha = 0)
6. Highlights the default hardware button (index 3 = COTS)

**`BuildUI()`** — Creates:
1. A `Canvas` with `ScreenSpaceOverlay` render mode, sorting order 10
2. A `CanvasScaler` at 1920x1080 reference resolution, `matchWidthOrHeight = 0.5f`
3. A `GraphicRaycaster` for UI interaction
4. Calls `BuildTopBar()`, `BuildSettingsSidebar()`, `BuildSelectionInfo()`, `BuildBackButton()`

**`BuildTopBar(parent)`** — Full-width panel anchored to top. 60px tall.
- Left side: "SOL SYSTEM MAP" title, 24pt bold gold
- Right side: `tierInfoText` showing selection info or "CLICK A PLANET OR MOON" placeholder

**`BuildSelectionInfo(parent)`** — Panel anchored to bottom-right corner. 380x210px.
- Uses `VerticalLayoutGroup` for auto-stacking
- Header: `selectionNameText` — 22pt bold bright gold
- Separator line
- Body: `selectionDetailText` — 15pt dim gold, shows type/TID/dose/fidelity/lifespan

**`BuildSettingsSidebar(parent)`** — Left sidebar. 300px wide, anchored from 10% to 93% of screen height.
- `VerticalLayoutGroup` with 10px spacing and 18px padding
- Title: "PARAMETERS" in 22pt bold gold
- Then 4 parameter sections separated by gold separator lines:
  1. **SHIELDING LEVEL** — label, italic description, slider (0-2, whole numbers), value text showing "LOW"/"MED"/"HIGH"
  2. **MISSION DURATION** — label, italic description, slider (1-20, whole numbers), value text showing "X YR"
  3. **HARDWARE CLASS** — label, italic description, 4 radio buttons in a `HorizontalLayoutGroup` (LEGACY RH / MODERN RH / FPGA / COTS)
  4. **VR PREVIEW** — label, italic description, toggle button (ON/OFF) with preview section below

Each parameter has an italic 13pt description explaining what it does:
- Shielding: "Spacecraft hull thickness / reducing radiation exposure"
- Mission Duration: "Total years of operation / at the selected location"
- Hardware Class: "Onboard compute hardware / for VR rendering"
- VR Preview: "Shows max feasible VR tier / for the selected location"

**Preview Section** — Inside the sidebar, below VR toggle. Uses a `CanvasGroup` with alpha=0 (hidden until VR toggle is ON).
- `previewLabel`: "FIDELITY: --" text, updated to "FIDELITY: NONE/LOW/MEDIUM/HIGH"
- `previewDisplay`: `RawImage` showing the `RenderTexture` from the preview camera. 264x220px with a gold `Outline`.

**`BuildShieldingSlider(parent)`** — Constructs a Unity `Slider` entirely in code:
- Background bar (30%-70% vertical anchors for thin appearance)
- Fill area + fill image (gold)
- Handle slide area + handle (28x28px gold square)
- Links: `shieldingSlider.fillRect = fillRT`, `handleRect = handleRT`
- Value text below: shows "LOW" at 0, "MED" at 1, "HIGH" at 2
- `onValueChanged` listener: updates label, calls `GameManager.SetShieldingLevel()`

**`BuildMissionSlider(parent)`** — Same structure as shielding slider but range 1-20. Value text shows "X YR". Calls `GameManager.SetMissionDuration()`.

**`BuildHardwareSelector(parent)`** — 4 buttons in a `HorizontalLayoutGroup`:
- Each button: `Image` background (ToggleOff color) + centered `TMP_Text` label (11pt bold)
- Labels come from `RadiationCalculator.HardwareNames[i].ToUpper()`
- On click: calls `GameManager.SetHardwareClass(index)` and `UpdateHardwareButtons(index)`
- `UpdateHardwareButtons(selectedIndex)`: loops through all 4, sets selected to ToggleOn/GoldText, others to ToggleOff/DimText

**`BuildVRToggle(parent)`** — `HorizontalLayoutGroup` with:
- A 64x34px toggle box (`Image` that changes color)
- A "OFF"/"ON" status text
- On click: flips `vrToggleOn`, updates colors/text, calls `GameManager.SetVRPreview()`, shows/hides preview section via `CanvasGroup.alpha`, calls `RefreshVRPreview()` if turning on

**`BuildPreviewDisplay(parent)`** — Creates a `RawImage` (264x220px) with a gold `Outline` effect. The `RenderTexture` is assigned later in `SetupPreviewScene()`.

**`BuildBackButton(parent)`** — 130x48px button anchored to bottom-left. Shows "BACK" in gold. Has a `Button` component but no click handler is wired up (placeholder).

**`BuildDropdownArrows()`** — Called once from `LateUpdate()` when `SolarSystemBuilder` is ready. For each planet that has moons:
- Creates a 48x36px button with the ▼ arrow character
- Anchored at canvas center (0.5, 0.5) — position is updated every frame in `LateUpdate()`
- Stores a `DropdownArrow` struct with references to the planet object, button RectTransform, and arrow text
- On click: calls `SolarSystemBuilder.ToggleMoons(capturedEnv)`
- Uses variable capture: `SpaceEnvironment capturedEnv = env` to avoid closure-over-loop-variable bug

**`LateUpdate()` — Runs every frame after `Update()`**:
1. **Lazy arrow build**: If arrows haven't been built and `SolarSystemBuilder` is ready, builds them. This deferred approach avoids a race condition — `SystemMapUI.Start()` runs before `SolarSystemBuilder.Start()` finishes building planets.
2. **Arrow position tracking**: For each dropdown arrow, converts the planet's world position to canvas space:
   - Calculates world position: planet center + down * (radius + 0.5)
   - `Camera.main.WorldToScreenPoint(worldPos)` → screen pixels
   - `RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPos, null, out canvasPos)` → canvas local coordinates
   - Sets `arrow.buttonRT.anchoredPosition = canvasPos`
   - Hides arrow if behind camera (`screenPos.z < 0`)
   - Updates arrow text: ▲ if expanded, ▼ if collapsed
3. **Preview rotation**: If VR toggle is on, rotates the active preview model at 45 deg/sec around its local Y axis.

**`OnDestroy()`** — Unsubscribes from all GameManager events. Releases the `RenderTexture`.

**`ExpandPlanet(env)`** — Public method called by `ClickDetector`. Delegates to `SolarSystemBuilder.ToggleMoons()`.

**Event handlers**:

**`OnLocationChanged(env)`** — Sets `selectionNameText` to planet name (uppercase). Shows "Type: Planet" + sub-location count + "Calculating...".

**`OnSubLocationChanged(subName)`** — Sets `selectionNameText` to moon name (uppercase). Shows "Type: Moon" + "Calculating...".

**`OnFidelityChanged(result)`** — The main display update. Called after every recalculation.
- Determines location name (moon name if selected, else planet name)
- Formats lifespan via `RadiationCalculator.FormatLifespan()`
- Checks `willFail`: true if `lifespanYears > 0 && lifespanYears < missionDurationYears`
- **Top bar**: Shows `"LOCATION  |  lifespan"`. If failing, shows `"<color=#FF4444>FAILS AT YEAR X</color>"` in red using TMP rich text.
- **Info panel**: Shows Type, Eff. TID (3 decimal krad/yr), Mission Dose (2 decimal krad), Fidelity (name + short), Lifespan. If hardware fails, appends red warning.
- Calls `RefreshVRPreview()` to update the 3D preview.

**Utility methods**:
- `CreatePanel(parent, name, color)` — Creates a `GameObject` with `RectTransform` and `Image`. Returns the `RectTransform`.
- `CreateText(parent, name, text, fontSize, color)` — Creates a `GameObject` with `RectTransform`, `TextMeshProUGUI`, and `LayoutElement`. Sets text, fontSize, color, richText=true.
- `CreateSeparator(parent)` — 1px tall gold line at 30% opacity.
- `CreateSpacer(parent, height)` — Empty element with `LayoutElement.preferredHeight` for spacing.

---

## 3D Model Assets

Four model folders under `Assets/Resources/Models/`:

### ErrorText (Tier 0 — NONE)
- `ERRORText.fbx` — A 3D "ERROR" text mesh
- `ERRORText_typeBlinn_BaseColor.jpeg` — Color texture
- `ERRORText_typeBlinn_Emissive.jpeg` — Emissive glow texture
- Loaded by `LoadErrorTextModel()` with special handling: scales to fit 1.2 world units, centers on preview point, applies emissive material

### Mario (Tier 1 — LOW / Wireframe VR)
- `mario.obj` + `mario.mtl` — OBJ model
- Multiple textures: body, eyes, hair, mustache, overalls, shoes, etc.
- Loaded by `LoadPreviewModel()` with `bustFraction = 0.45` (shows nearly half the model)

### CounterStrike (Tier 2 — MED / Textured VR)
- `COUNTER-TERRORIST_GIGN.obj` + `.mtl` — CS 1.6 GIGN counter-terrorist
- `GIGN_DMBASE2.png` — Body texture
- `Backpack2.png` — Backpack texture
- Loaded with `bustFraction = 0.28` (tight bust — head and shoulders only)
- Texture mapping in `ApplyModelTextures()`: matches material names containing "GIGN_DMBASE2" or "Backpack2"

### DoomSlayer (Tier 3 — HIGH / Modern VR)
- `doommarine.obj` + `.mtl` — Doom 3 marine model
- 6 body part textures (arms, cowl, helmet, legs, torso, visor), each with `_c` (color), `_n` (normal), `_s` (specular), `_mask` variants
- Only `_c` (color) textures are used by the runtime texture applicator
- Loaded with `bustFraction = 0.28` (tight bust)
- Texture mapping: matches material names containing part names (e.g., "doommarine_arms" → "models_characters_doommarine_doommarine_arms_c")

---

## Application Flow

### Startup Sequence

1. **Unity loads the scene**. The scene has 3 GameObjects with scripts attached:
   - One with `GameManager` — `Awake()` sets `Instance`, defaults `HardwareClassIndex=3`, `MissionDuration=1`
   - One with `SystemMapUI` — `Start()` builds UI, sets up preview
   - Main Camera with `ClickDetector` — has `[SerializeField]` reference to `SystemMapUI`

2. **`SolarSystemBuilder.Awake()`** — Sets its singleton. `Start()` loads `solar_system.json`, creates 8 planet spheres and 5 moon spheres (in 2 containers, initially hidden).

3. **`SystemMapUI.Start()`** — Builds the Canvas, sidebar, info panel, top bar, back button. Creates the preview camera/models at position (1000, 1000, 1000). Subscribes to GameManager events.

4. **First `LateUpdate()`** — Detects that `SolarSystemBuilder` is ready, builds dropdown arrow buttons for Earth and Jupiter (the planets with moons).

### User Click Flow

1. User clicks on a planet sphere (e.g., Jupiter)
2. `ClickDetector.Update()` fires a raycast, hits the sphere's `SphereCollider`
3. Gets the `SpaceLocation` component → `location.environment` = `SpaceEnvironment.Jupiter`
4. Calls `GameManager.Instance.SelectLocation(SpaceEnvironment.Jupiter)`:
   - Sets `CurrentSelection = Jupiter`, clears `CurrentSubLocation = null`
   - Fires `OnLocationChanged(Jupiter)` → `SystemMapUI.OnLocationChanged()` updates text
   - Calls `RecalculateFidelity()`:
     - Calls `RadiationCalculator.Calculate(Jupiter, null, shieldingLevel, hwIndex, missionYears)`
     - `EnsureLoaded()` loads JSON on first call
     - Algorithm runs (see next section)
     - Result stored in `CurrentResult`
     - Fires `OnFidelityChanged(result)` → `SystemMapUI.OnFidelityChanged()` updates all display
5. `ClickDetector` also calls `systemMapUI.ExpandPlanet(Jupiter)` → `SolarSystemBuilder.ToggleMoons(Jupiter)` starts moon dropdown animation

### Parameter Change Flow

1. User drags mission duration slider to 10
2. `missionSlider.onValueChanged` fires
3. Updates label to "10 YR"
4. Calls `GameManager.Instance.SetMissionDuration(10)`:
   - Sets `MissionDuration = 10`, fires `OnMissionDurationChanged(10)`
   - Calls `RecalculateFidelity()` with new mission years
   - New `FidelityResult` produced (different `totalMissionDose`, possibly different `hardwareSurvives`)
   - Fires `OnFidelityChanged` → UI updates

---

## The Radiation Algorithm

`RadiationCalculator.Calculate()` step by step:

**Input**: SpaceEnvironment, moonName, shieldingIndex (0-2), hardwareIndex (0-3), missionYears (1-20)

**Step 1 — Initialize default result**:
```
tierLevel = 0, tierName = "Infeasible", tierShortName = "NONE"
lifespanYears = -1, effectiveTID = 0, totalMissionDose = 0
effectiveGFLOPS = 0, hardwareSurvives = false
hardwareName = HardwareNames[hardwareIndex]
```

**Step 2 — Look up dosimetry ID**:
- If moonName is set (e.g., "Europa"), uses moonName as the key
- If null, uses `env.ToString()` (e.g., "Jupiter")
- Looks up in `locationIdMap` → "europa" or "jupiter_orbit"
- If not found → returns early with "No Data"

**Step 3 — Get dosimetry entry**:
- Looks up the dosimetry ID in `dosimetryMap`
- If `tid < 0` (Uranus) → returns "Unknown Radiation"

**Step 4 — Apply shielding**:
- Clamps `shieldingIndex` to valid range
- Gets the shielding entry (Low/Medium/High)
- Reads the reduction factor for the location's `dominant_source`:
  - `"gcr"` → `shield.gcr`
  - `"spe"` → `shield.spe`
  - `"trapped_electrons"` → `shield.trapped_electrons`
- `effectiveTID = tid × reductionFactor`

**Step 5 — Compute mission dose**:
- `totalMissionDose = effectiveTID × missionYears`

**Step 6 — Get hardware**:
- Clamps `hardwareIndex` to valid range
- Gets the hardware entry

**Step 7 — Compute lifespan**:
- If `effectiveTID > 0`: `lifespanYears = tid_tolerance_krad / effectiveTID`
- If zero: `lifespanYears = float.MaxValue` (indefinite)

**Step 8 — Survivability check**:
- If `totalMissionDose > tid_tolerance_krad`: hardware fails. Returns "Hardware Fails" with `hardwareSurvives = false`.
- This check uses total dose over the full mission, not annual dose vs tolerance.

**Step 9 — Compute effective GFLOPS**:
- `effectiveGFLOPS = gflops / overhead_factor`
- For Legacy Rad-Hard: 0.001 / 1.0 = 0.001
- For Modern Rad-Hard: 3.7 / 1.0 = 3.7
- For FPGA: 529.5 / 1.15 = 460.4
- For COTS: 1567 / 1.0 = 1567

**Step 10 — Determine fidelity tier**:
- Iterates tiers from highest (3) to lowest (0)
- Picks the first tier where `effectiveGFLOPS >= gflops_min`
- Tier 3 (Modern VR) needs ≥567 GFLOPS → only COTS achieves this
- Tier 2 (Textured VR) needs ≥0.2 → FPGA and Modern Rad-Hard achieve this
- Tier 1 (Wireframe VR) needs ≥0.08 → Modern Rad-Hard achieves this (3.7 > 0.08)
- Tier 0 (Infeasible) has gflops_min = -1 → always matches as fallback
- Legacy Rad-Hard at 0.001 GFLOPS falls into Tier 0 (0.001 < 0.08)

**Example**: Jupiter + Low Shielding + COTS + 1 Year
- TID = 1000 krad/yr, dominant_source = trapped_electrons, shield factor = 1.0
- effectiveTID = 1000 × 1.0 = 1000 krad/yr
- totalMissionDose = 1000 × 1 = 1000 krad
- COTS tolerance = 30 krad → 1000 > 30 → HARDWARE FAILS

**Example**: Moon + High Shielding + COTS + 5 Years
- TID = 0.012 krad/yr, dominant_source = gcr, shield factor = 0.8
- effectiveTID = 0.012 × 0.8 = 0.0096 krad/yr
- totalMissionDose = 0.0096 × 5 = 0.048 krad
- COTS tolerance = 30 krad → 0.048 < 30 → SURVIVES
- lifespanYears = 30 / 0.0096 = 3125 years
- effectiveGFLOPS = 1567 → Tier 3 (Modern VR)

---

## UI System — How It All Gets Built

Every UI element is created programmatically in `SystemMapUI.cs`. There are zero prefabs.

### Canvas Setup
```csharp
canvas = canvasGO.AddComponent<Canvas>();
canvas.renderMode = RenderMode.ScreenSpaceOverlay;
```
ScreenSpaceOverlay means the UI renders on top of everything, not in world space. `sortingOrder = 10` ensures it's above other canvases.

The `CanvasScaler` at 1920x1080 reference with `matchWidthOrHeight = 0.5f` means the UI scales based on an average of width and height scaling, so it looks reasonable on different aspect ratios.

### Anchor System
Every panel is positioned using RectTransform anchors:
- **Top bar**: `anchorMin=(0,1), anchorMax=(1,1)` — stretches full width, pinned to top
- **Sidebar**: `anchorMin=(0,0.10), anchorMax=(0,0.93)` — pinned to left, 10%-93% height
- **Info panel**: `anchorMin=(1,0), anchorMax=(1,0)` — pinned to bottom-right corner
- **Back button**: `anchorMin=(0,0), anchorMax=(0,0)` — pinned to bottom-left corner

### Layout Groups
The sidebar and info panel use `VerticalLayoutGroup` to auto-stack children vertically. The hardware selector uses `HorizontalLayoutGroup` to arrange buttons side-by-side. `LayoutElement.preferredHeight` controls how much space each child takes.

### Slider Construction
Each slider is built from scratch:
1. Root GameObject with `Slider` component
2. Background `Image` (dark bar, anchored to 30%-70% vertical for thin look)
3. Fill area with `Fill` image (gold, stretches with slider value)
4. Handle slide area with `Handle` image (28x28 gold square)
5. Wire up: `slider.fillRect`, `slider.handleRect`, `slider.targetGraphic`
6. Add `onValueChanged` listener

This is more work than using a prefab but necessary without editor access.

### Text Creation
All text uses TextMeshPro (`TextMeshProUGUI`). The `CreateText()` helper:
1. Creates a `GameObject`
2. Adds `RectTransform` and `TextMeshProUGUI`
3. Sets text, fontSize, color
4. Enables `richText = true` (for `<color=...>` tags used in warnings)
5. Adds a `LayoutElement` if one doesn't exist

---

## The VR Preview System

A 3D model viewer embedded in the 2D UI sidebar, implemented via the RenderTexture trick.

### How It Works

1. **Offscreen camera** at position `(1000, 1000, 995)` looking forward (+Z). This is far from the main scene so nothing overlaps.
2. **RenderTexture** (512x512, 2x antialiasing) captures what this camera sees.
3. **RawImage** in the sidebar displays the RenderTexture.
4. **Directional light** at `(998, 1003, 993)` angled at (30, -30, 0) illuminates the models.
5. **4 model GameObjects** positioned at `(1000, 1000, 1000)` — one for each tier. Only one is active at a time.

### Model Loading

**Tier 0 (NONE)** — `LoadErrorTextModel(center)`:
- Loads `Models/ErrorText/ERRORText` FBX via `Resources.Load<GameObject>`
- If load fails or model has no renderers → returns null → fallback red sphere
- Applies base color and emissive textures
- Scales to fit 1.2 world units (measures bounds, computes scale factor)
- Centers on preview point

**Tiers 1-3** — `LoadPreviewModel(path, position, scale, bustFraction)`:
- Loads model from Resources
- If no renderers → returns null (triggers fallback primitive)
- Applies textures via `ApplyModelTextures()`
- **Bust framing**: Measures full model bounds, takes the top `bustFraction` portion (0.45 for Mario, 0.28 for CS and Doom), scales to fit `desiredSize = 0.9` units, then repositions so the bust center is at the preview point. This crops legs/lower body out of view.
- Removes all colliders (prevents raycast interference)

### Texture Application

`ApplyModelTextures(instance, resourcePath)`:
- Extracts the folder from the resource path
- For each Renderer's materials: if no `mainTexture`, tries to match material name to a known texture name
- Hardcoded mappings:
  - CS: "GIGN_DMBASE2" → GIGN_DMBASE2 texture, "Backpack2" → Backpack2 texture
  - Doom: 6 body parts (arms, cowl, helmet, legs, torso, visor) → their respective `_c` textures
  - Error text: "typeBlinn" or ErrorText path → ERRORText_typeBlinn_BaseColor + emissive
- Fallback: `Resources.LoadAll<Texture2D>(folder)` — grabs the first texture in the folder

### Preview Updates

- `RefreshVRPreview()` — If VR toggle is on, calls `ShowPreviewTier(tierLevel)`
- `ShowPreviewTier(vrTierLevel)` — Activates the matching tier object, deactivates all others. Updates label text.
- In `LateUpdate()` — Active preview objects rotate at 45 deg/sec around local Y (`Space.Self`)

### Camera Settings
- `fieldOfView = 20` — narrow FOV for a zoomed-in look
- `clearFlags = SolidColor` — dark background `(0.03, 0.03, 0.06)`
- `cullingMask = "Default"` layer — sees all default objects
- `depth = -10` — renders before the main camera (doesn't matter since it uses a RenderTexture)

---

## The Dropdown Animation System

When you click a planet or its dropdown arrow, moons appear/disappear with an animated fold-down effect.

### Mechanics

**Only one dropdown at a time.** If Jupiter's moons are showing and you click Earth, Jupiter's moons instantly hide (no animation) and Earth's moons animate open. This prevents visual clutter.

**Animation blocks input.** While `animatingPlanet.HasValue`, `ToggleMoons()` returns immediately. This prevents double-clicks from breaking state.

**Ease-out cubic curve**: `t = 1 - (1 - progress)³`
- At progress=0: t=0 (start)
- At progress=0.5: t=0.875 (already most of the way)
- At progress=1: t=1 (end)
- This means fast start, gentle landing.

**Duration**: 0.35 seconds (`animDuration`).

### Visual Effect

`SetMoonContainerScale(env, t)`:
- Sets the container's Y scale to `t`: `localScale = new Vector3(1, t, 1)`. Moons "unfold" vertically from the planet.
- Sets each moon renderer's material alpha to `t`. Moons fade in simultaneously.
- At t=0: container is flat and invisible. At t=1: container is full size and opaque.

### State Machine

```
IDLE (no animation, expandedPlanet may or may not be set)
  → ToggleMoons(env) called
  → If same planet: StartAnimation(env, closing=false) → ANIMATING_CLOSE
  → If different planet: instantly hide old, StartAnimation(env, opening=true) → ANIMATING_OPEN
  → If no current: StartAnimation(env, opening=true) → ANIMATING_OPEN

ANIMATING_OPEN
  → Each frame: advance progress, apply ease-out, scale up
  → At progress >= 1: FinishAnimation() → scale=1, expandedPlanet=env → IDLE

ANIMATING_CLOSE
  → Each frame: advance progress, apply ease-out, scale down
  → At progress >= 1: FinishAnimation() → scale=0, deactivate, expandedPlanet=null → IDLE
```

---

## Coordinate Conversion Trick

The dropdown arrows are UI elements (2D canvas) that need to track 3D planets as the camera moves. This requires converting between coordinate systems:

**World Space** → **Screen Space** → **Canvas Space**

```csharp
Vector3 worldPos = planetObj.transform.position + Vector3.down * (planetRadius + 0.5f);
Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
Vector2 canvasPos;
RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPos, null, out canvasPos);
arrow.buttonRT.anchoredPosition = canvasPos;
```

1. Calculate where below the planet the arrow should be (half a unit below the sphere's bottom)
2. `WorldToScreenPoint` converts 3D world coords to pixel coords on screen
3. `ScreenPointToLocalPointInRectangle` converts screen pixels to the canvas's local coordinate system (accounting for the CanvasScaler's reference resolution). The `null` camera parameter works because the canvas is ScreenSpaceOverlay.
4. Set the button's `anchoredPosition` (which is in canvas local space since anchors are at 0.5, 0.5)

If `screenPos.z < 0`, the planet is behind the camera — hide the arrow.

This runs every `LateUpdate()` so arrows track planets in real-time.

---

## Event-Driven Architecture

The app uses a publish-subscribe pattern via C# `Action` delegates.

### Event Flow Diagram

```
User Action
    │
    ├─ Click planet ──────────→ ClickDetector
    │                               │
    │                               ├→ GameManager.SelectLocation()
    │                               │       ├→ OnLocationChanged ──→ SystemMapUI.OnLocationChanged()
    │                               │       └→ RecalculateFidelity()
    │                               │               └→ OnFidelityChanged ──→ SystemMapUI.OnFidelityChanged()
    │                               │                                               └→ RefreshVRPreview()
    │                               └→ SystemMapUI.ExpandPlanet()
    │                                       └→ SolarSystemBuilder.ToggleMoons()
    │
    ├─ Click moon ────────────→ ClickDetector
    │                               ├→ GameManager.SelectLocation() (parent)
    │                               └→ GameManager.SelectSubLocation()
    │                                       ├→ OnSubLocationChanged ──→ SystemMapUI
    │                                       └→ RecalculateFidelity() → ...
    │
    ├─ Move shielding slider ─→ Slider.onValueChanged
    │                               └→ GameManager.SetShieldingLevel()
    │                                       ├→ OnShieldingChanged
    │                                       └→ RecalculateFidelity() → ...
    │
    ├─ Move mission slider ───→ Slider.onValueChanged
    │                               └→ GameManager.SetMissionDuration()
    │                                       └→ RecalculateFidelity() → ...
    │
    ├─ Click hardware button ─→ Button.onClick
    │                               ├→ GameManager.SetHardwareClass()
    │                               │       └→ RecalculateFidelity() → ...
    │                               └→ UpdateHardwareButtons() (visual)
    │
    └─ Toggle VR preview ─────→ Button.onClick
                                    ├→ GameManager.SetVRPreview()
                                    ├→ Toggle CanvasGroup alpha
                                    └→ RefreshVRPreview()
```

### Subscription Lifecycle
- Subscribed in `SystemMapUI.Start()` via `+=`
- Unsubscribed in `SystemMapUI.OnDestroy()` via `-=`
- This prevents memory leaks and null reference exceptions if the UI is destroyed before GameManager.

---

## Unity-Specific Tricks

### Programmatic Slider
Unity's `Slider` component requires specific child hierarchy (Background, FillArea/Fill, HandleSlideArea/Handle). The code builds this exact hierarchy and assigns `slider.fillRect` and `slider.handleRect` references. Without these assignments, the slider appears but doesn't function.

### CanvasGroup for Show/Hide
The VR preview section uses `CanvasGroup.alpha = 0/1` instead of `SetActive(false/true)`. This maintains the section's layout space in the `VerticalLayoutGroup` — if you deactivate it, all elements below shift up. CanvasGroup also has `blocksRaycasts = false` when hidden, preventing invisible UI from eating clicks.

### EventSystem Click-Through Guard
```csharp
if(EventSystem.current.IsPointerOverGameObject()) return;
```
Without this, clicking a UI button would also fire a physics raycast through the button into the 3D scene, selecting whatever planet/moon is behind the UI. This line prevents that.

### Capture-by-Value in Loops
```csharp
SpaceEnvironment capturedEnv = env;
btn.onClick.AddListener(() => { SolarSystemBuilder.Instance.ToggleMoons(capturedEnv); });
```
In C# closures, the loop variable `env` is captured by reference. By the time the button is clicked, the loop has finished and `env` is the last value. Creating `capturedEnv` captures the current iteration's value.

### Resources.Load for WebGL
`Resources.Load<TextAsset>("RadData/dosimetry_table")` loads a JSON file synchronously from the `Assets/Resources/` folder. This works in WebGL (compiled into the build) unlike `System.IO.File` which doesn't exist in the browser. The path has no extension — Unity strips it.

### RenderTexture Trick
Rendering a 3D object inside a 2D UI panel:
1. Place objects far away (1000, 1000, 1000) so they don't appear in the main camera
2. Point a secondary camera at them
3. Set `camera.targetTexture = renderTexture` so it renders to a texture instead of the screen
4. Display that texture in a `RawImage` UI element

### Material Color Alpha for Fade
When animating moon dropdown, material alpha is set directly:
```csharp
Color c = rend.material.color;
c.a = t;
rend.material.color = c;
```
This works with the Standard shader in Opaque mode technically — the alpha won't actually be visible unless the shader is set to Fade or Transparent mode. The visual effect comes primarily from the Y-scale squash, not the alpha.

---

## What's Good About This Codebase

**Clear separation of concerns.** GameManager holds state, RadiationCalculator computes, SystemMapUI displays, SolarSystemBuilder builds the scene, ClickDetector handles input. No file does two things.

**Data-driven design.** Adding a new planet, moon, hardware class, or shielding level means editing JSON — no C# changes needed (assuming the JSON structure stays the same).

**Event-driven updates.** Changing any parameter automatically recalculates and updates all displays. No manual "refresh all UI" calls scattered around.

**The algorithm is real physics.** The TID values, shielding factors, hardware specs, and fidelity thresholds are from actual research data. The results are meaningful, not arbitrary.

**WebGL-ready.** `Resources.Load` and `JsonUtility` both work in browser builds. No file I/O, no async loading complications.

**Defensive coding.** Null checks on GameManager.Instance, CurrentSelection.HasValue guards, Mathf.Clamp on all slider values, fallback primitives when model loading fails.

---

## What Could Be Improved

**Model texture loading is brittle.** Hardcoded material name → texture name mappings in `ApplyModelTextures()`. If models are swapped, the mappings break. A convention-based system (material name matches texture filename) would be more maintainable.

**No camera controls.** The camera is static. Users can't pan/zoom/orbit to see different parts of the solar system. A simple orbit camera or scroll-to-zoom would help.

**Material alpha doesn't actually fade.** The dropdown animation sets alpha on Standard shader materials, but they're in Opaque mode. The Y-scale squash creates the illusion of folding, but a proper fade would need Transparent/Fade rendering mode.

**Back button does nothing.** `BuildBackButton()` creates the button and text but no `onClick` handler. It's a placeholder.

**No loading indicator.** `EnsureLoaded()` loads 4 JSON files synchronously on first use. On slow devices this could cause a frame hitch. An async loading approach with a spinner would be better, though synchronous loading is simpler for WebGL.

**LocationPanelUI.cs still exists.** Legacy file from before the UI rewrite. Should be deleted.

**Singleton fragility.** If multiple GameManager or SolarSystemBuilder instances exist, `Instance` silently overwrites. `DontDestroyOnLoad` isn't called, so scene reloads create duplicates. For a single-scene app this is fine.

**Preview section layout.** The sidebar is getting tall. On shorter screens, the bottom of the sidebar (VR preview) may clip below the screen. A scrollview or collapsible sections would help.

**Hard-coded bust fractions.** Mario uses 0.45, CS and Doom use 0.28. If models change, these values need manual tuning. Auto-detecting "head" vs "body" isn't trivial.

---

## How to Modify Things

### Add a New Planet
1. Add the name to `SpaceEnvironment` enum in `SpaceLocation.cs`
2. Add an entry in `solar_system.json` with name, distance, radius, color, spin_speed
3. Add a dosimetry entry in `dosimetry_table.json` with a unique ID
4. Add the mapping in `RadiationCalculator.locationIdMap` (e.g., `{ "Pluto", "pluto_orbit" }`)

### Add a New Moon
1. Add an entry in the `moons` array of `solar_system.json` with name, parent, radius, color
2. Add a `SubLocation` entry in `SubLocationDatabase.BuildDatabase()` in `SpaceLocation.cs`
3. Add a dosimetry entry in `dosimetry_table.json`
4. Add the mapping in `RadiationCalculator.locationIdMap`

### Add a New Hardware Class
1. Add the entry in `hardware_classes.json`
2. Add entries in `RadiationCalculator.HardwareIds` and `HardwareNames` arrays
3. Update `GameManager.SetHardwareClass()` clamp max (currently 3)
4. Update `SystemMapUI.BuildHardwareSelector()` array sizes and loop bounds (currently 4)
5. Update `SystemMapUI.UpdateHardwareButtons()` loop bound

### Add a New Shielding Level
1. Add the entry in `shielding_table.json`
2. Update `GameManager.SetShieldingLevel()` clamp max (currently 2)
3. Update `SystemMapUI.ShieldingLabels` array
4. Update the shielding slider's `maxValue` in `BuildShieldingSlider()`

### Change Preview Models
1. Place model files (.obj or .fbx) + textures in `Assets/Resources/Models/YourModel/`
2. In `SetupPreviewScene()`, update the `LoadPreviewModel()` path and adjust `bustFraction`
3. If the model has special texture naming, add mappings in `ApplyModelTextures()`

### Change Color Scheme
All colors are `static readonly Color` at the top of `SystemMapUI.cs`. Change them there. The scheme is: dark backgrounds, gold/amber text, brighter gold for highlights, muted gold for secondary text.

---

## WebGL Deployment

The app is designed for WebGL from the start. Key compatibility points:

1. **`Resources.Load`** works in WebGL (compiled into the build data)
2. **`JsonUtility.FromJson`** is Unity's C# parser (no native dependencies)
3. **No `System.IO`** file operations anywhere
4. **No async/await** patterns that might not work in older Unity WebGL
5. **Standard shader** works in WebGL (though performance varies)
6. **TextMeshPro** works in WebGL (font assets are included in build)

To build:
1. File → Build Settings → WebGL platform
2. Switch Platform
3. Player Settings: set compression (Brotli recommended), template (Minimal or Default)
4. Build
5. Output is an `index.html` + `Build/` folder + `TemplateData/` — host on any web server

Potential WebGL issues:
- Initial load time (all Resources are packed into the build)
- Memory: WebGL runs in browser memory limits
- Shader compilation may cause hitches on first frame
- No `Application.Quit()` in browser
- Touch input works differently (no hover)
