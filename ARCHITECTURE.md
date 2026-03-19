# Sol System Map - Architecture & Code Documentation

A comprehensive guide to every file, class, method, and concept in this Unity WebGL application.

---

## 1. Overview

This is a **Unity WebGL application** that displays an interactive solar system map. Users can:

- View all 9 celestial bodies (Sun, Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus, Neptune) as 3D spheres arranged horizontally
- Click on planets to select them and see their details
- Expand dropdown menus to reveal moons/stations below each planet
- Click on moons to select them and see their details
- Adjust a **Shielding Level** slider (Off / Low / Med / High)
- Toggle a **VR Preview** that shows a spinning 3D model representing the max feasible VR graphics tier for the selected location
- All rendered with a dark sci-fi aesthetic (gold text on semi-transparent dark panels)

The UI is inspired by the system map from Elite Dangerous.

---

## 2. Project Structure

```
Assets/
  SpaceLocation.cs      -- Data definitions (enums, moon database, component nametags)
  GameManager.cs         -- Central state manager (singleton, events, selection tracking)
  ClickDetector.cs       -- Mouse input handler (raycasting into 3D world)
  SolarSystemBuilder.cs  -- Creates all planet/moon 3D spheres at runtime
  SystemMapUI.cs         -- Builds all UI elements in code (sidebar, top bar, preview)
  Scenes/
    SampleScene.unity    -- The main Unity scene file
```

---

## 3. File-by-File Breakdown

---

### 3.1 SpaceLocation.cs

This file defines **what things exist** in the solar system. It contains 4 things:

#### SpaceEnvironment (enum)
```csharp
public enum SpaceEnvironment {
    Sun, Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus, Neptune
}
```
An **enum** is a fixed list of named options. A planet can only be one of these 9 values. Enums are used everywhere in the code to identify which planet we're talking about.

#### SubLocation (class)
```csharp
public class SubLocation {
    public string name;              // e.g. "Moon", "Phobos", "Titan"
    public SpaceEnvironment parent;  // e.g. SpaceEnvironment.Earth
}
```
Represents a moon or space station. Each one has a name and knows which planet it belongs to. The `[System.Serializable]` attribute means Unity can show it in the Inspector panel.

#### SubLocationDatabase (static class)
```csharp
public static class SubLocationDatabase { ... }
```
A **hardcoded lookup table** mapping each planet to its list of moons:

| Planet  | Moons/Stations                     |
|---------|-------------------------------------|
| Earth   | Moon, ISS                           |
| Mars    | Phobos, Deimos                      |
| Jupiter | Io, Europa, Ganymede, Callisto      |
| Saturn  | Titan, Enceladus                    |
| Uranus  | Titania, Miranda                    |
| Neptune | Triton                              |

The `static` keyword means there's only ONE copy of this database shared by all scripts. You never create an instance of it -- you just call `SubLocationDatabase.GetSubLocations(SpaceEnvironment.Earth)` and get back `[Moon, ISS]`.

The database uses **lazy initialization**: it only builds itself the first time someone asks for data. This is the `if(_data == null) BuildDatabase()` pattern.

#### SpaceLocation (MonoBehaviour)
```csharp
public class SpaceLocation : MonoBehaviour {
    public SpaceEnvironment environment;
}
```
A **component** (nametag) that gets attached to planet 3D spheres. When this is on a GameObject, it tells the code "this sphere IS Earth" or "this sphere IS Mars". MonoBehaviour means it can be attached to GameObjects in Unity.

#### SubLocationMarker (MonoBehaviour)
```csharp
public class SubLocationMarker : MonoBehaviour {
    public string subLocationName;
    public SpaceEnvironment parentEnvironment;
}
```
Same concept but for moons. Attached to moon spheres. Tells the code "this sphere is Phobos, and it belongs to Mars."

---

### 3.2 GameManager.cs

The **central brain** of the application. Everything talks to this.

#### Singleton Pattern
```csharp
public static GameManager Instance;

void Awake() {
    Instance = this;
}
```
There is only ONE GameManager. Any script anywhere can access it by writing `GameManager.Instance`. The `Awake()` method runs once when the object is created (before `Start()`), so by the time other scripts run, `Instance` is already set.

#### State Properties
```csharp
public SpaceEnvironment? CurrentSelection { get; private set; }
public string CurrentSubLocation { get; private set; }
public int ShieldingLevel { get; private set; }
public bool VRPreviewEnabled { get; private set; }
```
These store the current state of the app:
- `CurrentSelection` -- which planet is selected (the `?` means it can be null/nothing)
- `CurrentSubLocation` -- which moon is selected (null if none)
- `ShieldingLevel` -- 0 (Off), 1 (Low), 2 (Medium), or 3 (High)
- `VRPreviewEnabled` -- is the VR preview toggle on?

`private set` means only GameManager can change these values. Other scripts can read them but not write them directly -- they must call the public methods instead.

#### Events (Observer Pattern)
```csharp
public event Action<SpaceEnvironment> OnLocationChanged;
public event Action<string> OnSubLocationChanged;
public event Action<int> OnShieldingChanged;
public event Action<bool> OnVRPreviewChanged;
```
Events are like **mailing lists**. Other scripts subscribe to them:
```csharp
// In SystemMapUI.Start():
GameManager.Instance.OnLocationChanged += OnLocationChanged;
```
Now whenever a planet is selected, SystemMapUI's `OnLocationChanged` method gets called automatically. This is how the UI knows to update without GameManager needing to know anything about the UI.

The `?.Invoke()` syntax means "only fire this event if someone is actually subscribed" (prevents crashes).

#### SelectLocation Method
```csharp
public void SelectLocation(SpaceEnvironment env) {
    DisableAllTiers();
    // Enable the correct tier object based on planet
    CurrentSelection = env;
    CurrentSubLocation = null;  // clear moon selection
    OnLocationChanged?.Invoke(env);  // notify all subscribers
}
```
Called when a planet is clicked. Updates state and notifies all listeners.

#### Tier System
```csharp
public string GetTierName(SpaceEnvironment env) { ... }
```
Returns "Extreme", "High", "Medium", or "Low" based on planet. This represents the general danger/difficulty tier.

#### VR Tier System
```csharp
public int GetVRTierLevel(SpaceEnvironment env) { ... }
public int GetMoonVRTierLevel(string moonName, SpaceEnvironment parent) { ... }
```
Returns 0-3 representing the maximum feasible VR quality at each location:

| Level | Name   | Locations                                    |
|-------|--------|----------------------------------------------|
| 0     | None   | Sun, Jupiter, Io                             |
| 1     | Low    | Mercury, Venus, Saturn, Uranus, Neptune, most distant moons |
| 2     | Medium | Mars, Phobos, Deimos, Europa, Titan          |
| 3     | High   | Earth, Moon, ISS                             |

---

### 3.3 ClickDetector.cs

Handles **mouse input** -- detects when the user clicks on 3D objects.

#### Update Loop
```csharp
void Update() {
    if(Input.GetMouseButtonDown(0)) { ... }
}
```
`Update()` runs **every single frame** (60 times per second at 60fps). `GetMouseButtonDown(0)` returns true only on the exact frame the left mouse button is pressed.

#### UI Guard
```csharp
if(EventSystem.current.IsPointerOverGameObject())
    return;
```
**Critical check**: if the mouse is over a UI element (like the sidebar or a button), DON'T process the click as a 3D world click. Without this, clicking the shielding slider would also select whatever planet is behind it.

#### Raycasting
```csharp
Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
RaycastHit hit;
if(Physics.Raycast(ray, out hit)) { ... }
```
**Raycasting** = shooting an invisible laser beam from the camera through the mouse cursor into the 3D world. Think of it as pointing a laser pointer at the screen.

- `Camera.main` -- the main camera in the scene
- `ScreenPointToRay(Input.mousePosition)` -- creates a ray from the camera through where the mouse is on screen
- `Physics.Raycast(ray, out hit)` -- asks Unity's physics engine "does this ray hit anything with a Collider?" If yes, `hit` contains info about what was hit

#### Moon vs Planet Check
```csharp
SubLocationMarker moon = hit.collider.GetComponent<SubLocationMarker>();
if(moon != null) {
    GameManager.Instance.SelectLocation(moon.parentEnvironment);
    GameManager.Instance.SelectSubLocation(moon.subLocationName);
    return;  // stop here, don't also check for planet
}

SpaceLocation location = hit.collider.GetComponent<SpaceLocation>();
if(location != null) {
    GameManager.Instance.SelectLocation(location.environment);
}
```
First checks if the hit object is a moon (has SubLocationMarker). If yes, select both the parent planet AND the moon. The `return` stops execution so we don't double-process.

If it's not a moon, checks if it's a planet (has SpaceLocation). If yes, select the planet.

If it's neither (hit some random object), nothing happens.

---

### 3.4 SolarSystemBuilder.cs

Creates all the **3D planet and moon spheres** when you press Play. Also handles spinning and dropdown animation.

#### Data Structs
```csharp
private struct PlanetDef {
    public SpaceEnvironment env;   // which planet
    public float distance;          // X position (distance from Sun)
    public float radius;            // size of the sphere
    public Color color;             // RGB color
    public float spinSpeed;         // rotation speed (degrees/second)
}
```
A **struct** is a lightweight data container (like a recipe card). Each planet is defined by these 5 properties. There's a similar `MoonDef` for moons (without distance/spinSpeed).

#### Planet Definitions
```csharp
PlanetDef[] planets = new PlanetDef[] {
    new PlanetDef(SpaceEnvironment.Sun,    0f,   3.0f,  yellowColor,   2f),
    new PlanetDef(SpaceEnvironment.Mercury, 5f,  0.3f,  grayColor,    15f),
    // ... all 9 planets
};
```
All planets hardcoded with their properties. The Sun is at X=0, Mercury at X=5, Earth at X=12, Neptune at X=45. Sizes roughly reflect real relative sizes. Spin speeds vary -- Jupiter spins fastest (25 deg/s), Uranus spins in reverse (-18 deg/s, which is astronomically accurate!).

#### Creating Planets (BuildSolarSystem)
For each planet definition:

1. **Create a sphere**: `GameObject.CreatePrimitive(PrimitiveType.Sphere)` -- Unity makes a basic sphere mesh
2. **Scale it**: `transform.localScale = Vector3.one * radius * 2f` -- diameter = radius * 2
3. **Position it**: `transform.position = new Vector3(p.distance, 0, 0)` -- along the X axis
4. **Color it**: Create a new Material with the Standard shader, set its color
5. **Add nametag**: `AddComponent<SpaceLocation>()` -- attach the "I am Earth" component
6. **Make clickable**: `AddComponent<SphereCollider>()` -- without this, raycasts pass through

The Sun gets special treatment: `mat.EnableKeyword("_EMISSION")` makes it glow.

#### Creating Moons
Moons are grouped by parent planet using a Dictionary. For each planet's moons:

1. Create a **container** GameObject (empty parent) positioned at the planet's X
2. Create each moon sphere inside the container
3. Position moons in a **vertical column** below the planet: `localPosition = new Vector3(0, yPos, 0)`
4. Attach `SubLocationMarker` component (the moon nametag)
5. **Hide the container**: `container.SetActive(false)` -- moons start hidden

When we want to show/hide moons, we just toggle the container. All moons inside show/hide together.

#### Spinning (Update)
```csharp
void Update() {
    // Spin planets
    foreach(var kvp in PlanetObjects) {
        kvp.Value.transform.Rotate(Vector3.up, speed * Time.deltaTime, Space.Self);
    }
    // Spin visible moons
    foreach(var kvp in moonContainers) {
        if(kvp.Value.activeSelf) {
            foreach(Transform child in kvp.Value.transform) {
                child.Rotate(Vector3.up, 20f * Time.deltaTime, Space.Self);
            }
        }
    }
}
```
Every frame, rotate every planet around its Y axis. `Time.deltaTime` is the time since the last frame (~0.016s at 60fps). Multiplying by deltaTime makes rotation **framerate-independent** -- same visual speed whether you get 30fps or 144fps.

Moons only spin if their container is active (visible).

#### Dropdown Animation

The animation system uses a simple **0-to-1 progress** value:

```csharp
private float animProgress;     // goes from 0.0 to 1.0
private float animDuration = 0.35f;  // takes 0.35 seconds
```

Each frame during animation:
```csharp
animProgress += Time.deltaTime / animDuration;
```
This adds a fraction each frame. After 0.35 seconds total, it reaches 1.0.

**Ease-out curve** (smooth deceleration):
```csharp
float t = 1f - Mathf.Pow(1f - animProgress, 3f);
```
Instead of linear motion (constant speed), this math creates a curve that starts fast and slows down:
- At progress 0.0: t = 0.0 (start)
- At progress 0.5: t = 0.875 (already 87.5% there!)
- At progress 1.0: t = 1.0 (done)

The actual visual effect:
```csharp
container.transform.localScale = new Vector3(1f, t, 1f);
```
Scales the Y axis from 0 to 1. At t=0, moons are squished flat. At t=1, they're full size. This creates a smooth "sliding down from the planet" effect.

Moon alpha (transparency) also fades in:
```csharp
Color c = rend.material.color;
c.a = t;  // alpha goes 0 to 1
rend.material.color = c;
```

**One-at-a-time rule**: When opening a new dropdown, the previous one closes instantly first. The `animatingPlanet` flag prevents interrupting an in-progress animation.

---

### 3.5 SystemMapUI.cs

The **biggest file**. Builds the entire 2D user interface in code at runtime. No prefabs needed.

#### Color Palette
```csharp
private static readonly Color PanelBg      = new Color(0.05f, 0.05f, 0.08f, 0.85f);
private static readonly Color GoldText      = new Color(0.9f, 0.75f, 0.3f, 1f);
private static readonly Color GoldHighlight = new Color(1f, 0.85f, 0.4f, 1f);
private static readonly Color DimText       = new Color(0.5f, 0.45f, 0.3f, 1f);
// etc.
```
All colors defined in one place. `static readonly` means they're constants that never change. Colors are RGBA (Red, Green, Blue, Alpha) with values from 0 to 1. The panel background has 0.85 alpha = slightly transparent.

#### Canvas Setup
```csharp
canvas = canvasGO.AddComponent<Canvas>();
canvas.renderMode = RenderMode.ScreenSpaceOverlay;
```
A **Canvas** is Unity's container for all 2D UI elements. `ScreenSpaceOverlay` means it draws flat on top of the 3D world, like a HUD in a video game. Everything UI must be inside a Canvas.

```csharp
CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
scaler.referenceResolution = new Vector2(1920, 1080);
scaler.matchWidthOrHeight = 0.5f;
```
The **CanvasScaler** handles different screen sizes. "I designed this for 1920x1080. If the screen is smaller/bigger, scale everything proportionally." The 0.5 match means it balances between width and height scaling.

```csharp
canvasGO.AddComponent<GraphicRaycaster>();
```
Required for UI clicks to work. Without this, buttons wouldn't respond to mouse clicks.

#### UI Element Creation Pattern

Every UI element follows this pattern:

```csharp
// 1. Create a GameObject
GameObject go = new GameObject("MyPanel");

// 2. Parent it to the canvas (or another UI element)
go.transform.SetParent(canvasRT, false);

// 3. Add RectTransform (required for all UI elements)
RectTransform rt = go.AddComponent<RectTransform>();

// 4. Position it using anchors + offsets
rt.anchorMin = new Vector2(0, 1);    // anchor point(s) on parent
rt.anchorMax = new Vector2(1, 1);    // anchor stretches
rt.sizeDelta = new Vector2(0, 60);   // size adjustments
```

#### Understanding Anchors

Anchors are the most important concept for UI positioning. They define where an element "attaches" to its parent.

Each anchor is a Vector2 with values 0-1:
- (0,0) = bottom-left of parent
- (1,1) = top-right of parent
- (0.5, 0.5) = center of parent

**Pinned to a corner** (anchorMin == anchorMax):
```
anchorMin = (0, 0), anchorMax = (0, 0)  --> pinned to bottom-left
anchorMin = (1, 1), anchorMax = (1, 1)  --> pinned to top-right
```

**Stretched across an edge** (anchors span a range):
```
anchorMin = (0, 1), anchorMax = (1, 1)  --> stretches across the top
anchorMin = (0, 0), anchorMax = (0, 1)  --> stretches along the left side
```

`sizeDelta` = extra size beyond what the anchors define. If anchors stretch full width, sizeDelta.x = 0 means "exactly the parent's width". sizeDelta.y = 60 means "60 pixels tall".

`anchoredPosition` = offset from the anchor point.

#### Layout Groups (Auto-Positioning)

Instead of manually positioning every child element, **layout groups** auto-arrange children:

```csharp
VerticalLayoutGroup vlg = sidebar.AddComponent<VerticalLayoutGroup>();
vlg.spacing = 10;        // 10px gap between each child
vlg.padding = new RectOffset(18, 18, 18, 18);  // left, right, top, bottom padding
```

This says "stack all my children vertically with 10px gaps and 18px padding on all sides." Children are placed in order, top to bottom. Similar to CSS flexbox if you know web development.

`HorizontalLayoutGroup` does the same but left-to-right.

`LayoutElement` on a child controls how much space it takes:
```csharp
le.preferredHeight = 40;  // "I'd like to be 40px tall"
```

#### The Top Bar
```
+--[SOL SYSTEM MAP]--------[EARTH | TIER: HIGH | VR: HIGH]--+
```
- Anchored to top of screen, full width, 60px tall
- Title text on the left
- Selection info text on the right (updates when you click planets)

#### The Settings Sidebar
```
+--PARAMETERS---------+
|  SHIELDING LEVEL    |
|  [====o--------]    |
|  OFF                |
|  ----------------   |
|  VR PREVIEW         |
|  Shows max feasible |
|  [toggle] OFF       |
|  ----------------   |
|  MAX VR TIER: HIGH  |
|  [3D preview image] |
+---------------------+
```
- Anchored to left side, spans most of the height
- Contains: title, shielding slider, VR toggle, preview display
- 300px wide

#### The Shielding Slider

Unity's built-in `Slider` component needs child objects for its visual parts:

```
SliderGO (has Slider component)
  ├── Background (dark gray track)
  ├── FillArea
  │   └── Fill (gold colored, grows with value)
  └── HandleSlideArea
      └── Handle (gold circle you drag)
```

```csharp
shieldingSlider.wholeNumbers = true;  // snaps to integers
shieldingSlider.minValue = 0;
shieldingSlider.maxValue = 3;
```
`wholeNumbers = true` is the key -- it makes the slider snap to 0, 1, 2, 3 instead of sliding smoothly between them.

```csharp
shieldingSlider.onValueChanged.AddListener((val) => {
    int level = Mathf.RoundToInt(val);
    shieldingValueText.text = ShieldingLabels[level];  // "OFF", "LOW", etc.
    GameManager.Instance.SetShieldingLevel(level);
});
```
When the slider changes, update the text label and tell GameManager.

#### The VR Toggle

A simple ON/OFF button that:
1. Changes its own color (dark gray <-> gold)
2. Updates the "OFF"/"ON" text
3. Tells GameManager
4. Shows/hides the preview panel via CanvasGroup alpha

#### CanvasGroup (Hide Without Layout Shift)
```csharp
previewCanvasGroup = previewSection.AddComponent<CanvasGroup>();
previewCanvasGroup.alpha = 0f;           // invisible
previewCanvasGroup.blocksRaycasts = false; // clicks pass through
```
**Why not just SetActive(false)?** Because `SetActive(false)` removes the element from the layout entirely. The VerticalLayoutGroup would then rearrange everything above it -- the VR toggle would jump down to fill the gap.

CanvasGroup with alpha=0 makes it **invisible but still takes up space**. The toggle button stays exactly where it is.

#### The 3D Preview (RenderTexture Trick)

This is the coolest technique in the project:

1. **Create a RenderTexture** -- a texture that a camera can draw into
```csharp
previewRenderTex = new RenderTexture(512, 512, 16);
```

2. **Create a second camera** far away from the main scene (position 1000, 1000, 995)
```csharp
previewCamera.targetTexture = previewRenderTex;
```
This camera renders to the texture instead of the screen.

3. **Create preview objects** at position (1000, 1000, 1000) -- only this camera can see them
   - Tier 0 (None): Dark sphere with red X bars
   - Tier 1 (Low): Green sphere
   - Tier 2 (Medium): Blue glowing sphere
   - Tier 3 (High): Gold glowing sphere with a ring

4. **Display the texture** on a UI RawImage
```csharp
previewDisplay.texture = previewRenderTex;
```

Result: a little window in the sidebar showing a spinning 3D object, rendered by a separate camera that's invisible to the player.

Only one preview object is active at a time:
```csharp
tierPreviewObjects[i].SetActive(i == vrTierLevel);
```

#### Dropdown Arrows Following 3D Planets

Each planet with moons gets a small arrow button in the UI. These buttons need to visually sit just below their planet, even though the planets are in 3D space and the buttons are in 2D UI space.

Every frame in LateUpdate:
```csharp
// 1. Get the planet's world position (below the planet)
Vector3 worldPos = planetObj.transform.position + Vector3.down * (planetRadius + 0.5f);

// 2. Convert 3D world position to 2D screen position
Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

// 3. Convert screen position to canvas position
RectTransformUtility.ScreenPointToLocalPointInRectangle(
    canvasRT, screenPos, null, out canvasPos);

// 4. Move the button there
arrow.buttonRT.anchoredPosition = canvasPos;
```

This 3-step conversion (World -> Screen -> Canvas) is how you make UI elements "stick" to 3D objects.

#### Selection Info Panel
Bottom-right corner. Shows:
- Selected planet/moon name (big gold text)
- Type, Tier, Max VR level, moon count

Updated via event handlers:
```csharp
private void OnLocationChanged(SpaceEnvironment env) {
    selectionNameText.text = env.ToString().ToUpper();
    // ... update detail text
}
```

#### Back Button
Bottom-left, 130x48px. Currently just a visual placeholder -- no navigation logic yet.

---

## 4. Data Flow

### When the user clicks a planet in the 3D scene:
```
1. ClickDetector.Update() detects mouse click
2. Shoots a raycast from camera through mouse position
3. Ray hits a sphere with SpaceLocation component
4. Calls GameManager.Instance.SelectLocation(SpaceEnvironment.Earth)
5. GameManager updates CurrentSelection, fires OnLocationChanged event
6. SystemMapUI.OnLocationChanged() receives the event
7. Updates top bar text: "EARTH | TIER: HIGH | VR: HIGH"
8. Updates selection panel: name, type, tier, VR level, moon count
9. If VR Preview is ON, calls RefreshVRPreview()
10. RefreshVRPreview() gets VR tier (3 for Earth), shows gold sphere in preview
```

### When the user clicks a dropdown arrow:
```
1. UI Button click handler fires
2. Calls SolarSystemBuilder.Instance.ToggleMoons(SpaceEnvironment.Earth)
3. If another planet's moons are open, close them instantly
4. StartAnimation() begins -- sets animProgress = 0, activates moon container
5. Each frame in Update(): animProgress increases, ease-out curve applied
6. Moon container Y-scale goes from 0 to 1 over 0.35 seconds
7. Moon alpha fades from 0 to 1 simultaneously
8. After 0.35s: FinishAnimation() sets final scale to 1.0
```

### When the user clicks a moon sphere:
```
1. ClickDetector detects click, raycast hits moon with SubLocationMarker
2. Calls GameManager.SelectLocation(moon.parentEnvironment) -- selects parent planet
3. Calls GameManager.SelectSubLocation(moon.subLocationName) -- selects the moon
4. Both events fire, SystemMapUI updates both top bar and selection panel
5. VR Preview refreshes using GetMoonVRTierLevel() for moon-specific VR tier
```

### When the user moves the shielding slider:
```
1. Slider.onValueChanged fires with new value (0, 1, 2, or 3)
2. Updates text label to "OFF", "LOW", "MED", or "HIGH"
3. Calls GameManager.Instance.SetShieldingLevel(level)
4. GameManager stores value, fires OnShieldingChanged event
```

### When the user toggles VR Preview:
```
1. Button click handler fires
2. Toggles vrToggleOn boolean
3. Updates toggle box color (gray <-> gold)
4. Updates status text ("OFF" <-> "ON")
5. Sets CanvasGroup alpha (0 <-> 1) to show/hide preview panel
6. If turning ON: calls RefreshVRPreview() to show correct tier model
7. Tells GameManager via SetVRPreview()
```

---

## 5. Key Unity Concepts Used

### MonoBehaviour
The base class for all scripts that attach to GameObjects. Provides lifecycle methods:
- `Awake()` -- called once, before Start, when the object is created
- `Start()` -- called once, when the object first becomes active
- `Update()` -- called every frame (60fps = 60 times per second)
- `LateUpdate()` -- called every frame, but AFTER all Update() calls finish
- `OnDestroy()` -- called when the object is destroyed (cleanup)

### GameObject vs Component
- **GameObject** = a "thing" in the scene (can be invisible)
- **Component** = a "feature" attached to a GameObject
- A planet sphere is a GameObject. Its Renderer, Collider, SpaceLocation script are all Components.
- `AddComponent<T>()` = attach a new component
- `GetComponent<T>()` = find an existing component on the same object

### Transform
Every GameObject has a Transform (position, rotation, scale in 3D space):
- `transform.position` -- world position
- `transform.localPosition` -- position relative to parent
- `transform.localScale` -- size multiplier
- `transform.Rotate()` -- rotate by some amount
- `transform.SetParent()` -- make this a child of another object

### RectTransform
UI version of Transform. Uses anchors, pivot, and sizeDelta instead of just position:
- `anchorMin/anchorMax` -- where the element attaches to its parent
- `anchoredPosition` -- offset from anchors
- `sizeDelta` -- size beyond anchor-defined area
- `pivot` -- the "center point" for positioning and rotation

### Canvas
Required container for all UI. renderMode options:
- `ScreenSpaceOverlay` -- draws on top of everything (what we use)
- `ScreenSpaceCamera` -- drawn by a specific camera
- `WorldSpace` -- exists in 3D space (like a TV screen in-game)

### Physics.Raycast
Shoots a ray through the physics world:
```csharp
if(Physics.Raycast(ray, out hit)) {
    // hit.collider -- the Collider that was hit
    // hit.point -- the exact world position of the hit
    // hit.collider.GetComponent<T>() -- get scripts on the hit object
}
```
Only hits objects with Colliders (SphereCollider, BoxCollider, etc.).

### Events (Action)
C# events allow loose coupling -- scripts communicate without knowing about each other:
```csharp
// Declare event
public event Action<SpaceEnvironment> OnLocationChanged;

// Subscribe (in another script)
GameManager.Instance.OnLocationChanged += MyHandler;

// Fire event
OnLocationChanged?.Invoke(env);

// Unsubscribe (important for cleanup!)
GameManager.Instance.OnLocationChanged -= MyHandler;
```
Always unsubscribe in OnDestroy() to prevent memory leaks.

### RenderTexture
A texture that a Camera can render into. Used to create "picture-in-picture" effects:
```csharp
RenderTexture rt = new RenderTexture(512, 512, 16);
camera.targetTexture = rt;      // camera renders to texture
rawImage.texture = rt;          // UI element displays the texture
```

### CanvasGroup
Controls visibility/interactivity for a group of UI elements:
- `alpha` -- 0 = invisible, 1 = fully visible (children still take up space)
- `blocksRaycasts` -- whether clicks can hit elements in this group
- Unlike SetActive(false), doesn't remove the element from layout calculations

---

## 6. How to Modify

### Add a new planet
1. **SpaceLocation.cs**: Add to the `SpaceEnvironment` enum
2. **SolarSystemBuilder.cs**: Add a new `PlanetDef` entry in the `planets` array
3. **GameManager.cs**: Add a case in `GetTierName()` and `GetVRTierLevel()`
4. **SpaceLocation.cs**: Optionally add moons in `SubLocationDatabase.BuildDatabase()`

### Add a new moon
1. **SolarSystemBuilder.cs**: Add a new `MoonDef` entry in the `moons` array
2. **SpaceLocation.cs**: Add to the parent planet's list in `SubLocationDatabase`
3. **GameManager.cs**: Optionally add a case in `GetMoonVRTierLevel()` for custom VR tier

### Change colors
- **Planet/moon colors**: In `SolarSystemBuilder.cs`, modify the `Color` in planet/moon definitions
- **UI colors**: In `SystemMapUI.cs`, modify the color constants at the top of the class

### Change planet sizes/positions
- In `SolarSystemBuilder.cs`, modify `radius` and `distance` in the planet definitions

### Change spin speeds
- In `SolarSystemBuilder.cs`, modify `spinSpeed` in planet definitions (degrees per second, negative = reverse)

### Change dropdown animation speed
- In `SolarSystemBuilder.cs`, modify `animDuration` (currently 0.35 seconds)

### Change UI sizes (for different screen targets)
- In `SystemMapUI.cs`, modify font sizes, sizeDelta values, and preferredHeight values
- The CanvasScaler reference resolution (1920x1080) can be changed for different target screens

### Add a new sidebar control
1. In `SystemMapUI.BuildSettingsSidebar()`, add new elements after the existing ones
2. Create a label with `CreateText()`
3. Create your control (slider, toggle, button, etc.)
4. Add a state property and event in `GameManager.cs`
5. Wire up the control's callback to call the GameManager method
