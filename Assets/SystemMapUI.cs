using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SystemMapUI : MonoBehaviour {

    // --- Color Palette ---
    private static readonly Color PanelBg       = new Color(0.05f, 0.05f, 0.08f, 0.85f);
    private static readonly Color GoldText       = new Color(0.9f, 0.75f, 0.3f, 1f);
    private static readonly Color GoldHighlight  = new Color(1f, 0.85f, 0.4f, 1f);
    private static readonly Color DimText        = new Color(0.5f, 0.45f, 0.3f, 1f);
    private static readonly Color ButtonNormal   = new Color(0.1f, 0.1f, 0.14f, 0.9f);
    private static readonly Color ArrowBg        = new Color(0.12f, 0.12f, 0.16f, 0.95f);
    private static readonly Color ToggleOn       = new Color(0.8f, 0.65f, 0.2f, 1f);
    private static readonly Color ToggleOff      = new Color(0.25f, 0.25f, 0.28f, 1f);
    private static readonly Color SliderFill     = new Color(0.8f, 0.65f, 0.2f, 1f);
    private static readonly Color SliderBg       = new Color(0.15f, 0.15f, 0.18f, 1f);

    private static readonly string[] ShieldingLabels = { "OFF", "LOW", "MED", "HIGH" };

    // --- Runtime references ---
    private Canvas canvas;
    private RectTransform canvasRT;
    private TMP_Text tierInfoText;
    private TMP_Text selectionNameText;
    private TMP_Text selectionDetailText;

    // Shielding slider
    private Slider shieldingSlider;
    private TMP_Text shieldingValueText;

    // VR Preview
    private bool vrToggleOn = false;
    private Camera previewCamera;
    private RenderTexture previewRenderTex;
    private GameObject[] tierPreviewObjects;
    private RawImage previewDisplay;
    private TMP_Text previewLabel;
    private CanvasGroup previewCanvasGroup;

    // Dropdown arrow buttons
    private class DropdownArrow {
        public SpaceEnvironment env;
        public GameObject planetObj;
        public RectTransform buttonRT;
        public TMP_Text arrowText;
    }
    private List<DropdownArrow> dropdownArrows = new List<DropdownArrow>();
    private bool arrowsBuilt = false;

    void Start() {
        BuildUI();
        SetupPreviewScene();

        GameManager.Instance.OnLocationChanged += OnLocationChanged;
        GameManager.Instance.OnSubLocationChanged += OnSubLocationChanged;

        if(GameManager.Instance.CurrentSelection.HasValue) {
            OnLocationChanged(GameManager.Instance.CurrentSelection.Value);
        }

        if(previewCanvasGroup != null) {
            previewCanvasGroup.alpha = 0f;
            previewCanvasGroup.blocksRaycasts = false;
        }
    }

    void LateUpdate() {
        if(!arrowsBuilt && SolarSystemBuilder.Instance != null
            && SolarSystemBuilder.Instance.PlanetObjects.Count > 0) {
            BuildDropdownArrows();
            arrowsBuilt = true;
        }

        if(Camera.main == null) return;
        foreach(DropdownArrow arrow in dropdownArrows) {
            if(arrow.planetObj == null) continue;

            float planetRadius = arrow.planetObj.transform.localScale.x * 0.5f;
            Vector3 worldPos = arrow.planetObj.transform.position + Vector3.down * (planetRadius + 0.5f);
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            if(screenPos.z < 0) {
                arrow.buttonRT.gameObject.SetActive(false);
                continue;
            }
            arrow.buttonRT.gameObject.SetActive(true);

            Vector2 canvasPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, screenPos, null, out canvasPos);
            arrow.buttonRT.anchoredPosition = canvasPos;

            bool isExpanded = SolarSystemBuilder.Instance.IsExpanded(arrow.env);
            arrow.arrowText.text = isExpanded ? "\u25B2" : "\u25BC";
        }

        // Spin active preview object
        if(vrToggleOn && tierPreviewObjects != null) {
            foreach(var obj in tierPreviewObjects) {
                if(obj != null && obj.activeSelf) {
                    obj.transform.Rotate(Vector3.up, 45f * Time.deltaTime, Space.Self);
                    obj.transform.Rotate(Vector3.right, 15f * Time.deltaTime, Space.Self);
                }
            }
        }
    }

    void OnDestroy() {
        if(GameManager.Instance != null) {
            GameManager.Instance.OnLocationChanged -= OnLocationChanged;
            GameManager.Instance.OnSubLocationChanged -= OnSubLocationChanged;
        }
        if(previewRenderTex != null) previewRenderTex.Release();
    }

    public void ExpandPlanet(SpaceEnvironment env) {
        if(SolarSystemBuilder.Instance != null) {
            SolarSystemBuilder.Instance.ToggleMoons(env);
        }
    }

    // ========== UI CONSTRUCTION ==========

    private void BuildUI() {
        GameObject canvasGO = new GameObject("SystemMapCanvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        canvasRT = canvasGO.GetComponent<RectTransform>();

        BuildTopBar(canvasRT);
        BuildSettingsSidebar(canvasRT);
        BuildSelectionInfo(canvasRT);
        BuildBackButton(canvasRT);
    }

    private void BuildDropdownArrows() {
        foreach(var kvp in SolarSystemBuilder.Instance.PlanetObjects) {
            SpaceEnvironment env = kvp.Key;
            GameObject planetObj = kvp.Value;

            if(!SolarSystemBuilder.Instance.HasMoons(env)) continue;

            DropdownArrow arrow = new DropdownArrow();
            arrow.env = env;
            arrow.planetObj = planetObj;

            GameObject btnGO = new GameObject("Arrow_" + env);
            btnGO.transform.SetParent(canvasRT, false);
            arrow.buttonRT = btnGO.AddComponent<RectTransform>();
            arrow.buttonRT.sizeDelta = new Vector2(48, 36); // bigger for WebGL clicking
            arrow.buttonRT.anchorMin = new Vector2(0.5f, 0.5f);
            arrow.buttonRT.anchorMax = new Vector2(0.5f, 0.5f);
            arrow.buttonRT.pivot = new Vector2(0.5f, 0.5f);

            Image btnImg = btnGO.AddComponent<Image>();
            btnImg.color = ArrowBg;

            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = cb;

            arrow.arrowText = CreateText(arrow.buttonRT, "ArrowIcon", "\u25BC", 20, GoldText);
            RectTransform txtRT = arrow.arrowText.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;
            arrow.arrowText.alignment = TextAlignmentOptions.Center;

            SpaceEnvironment capturedEnv = env;
            btn.onClick.AddListener(() => {
                SolarSystemBuilder.Instance.ToggleMoons(capturedEnv);
            });

            dropdownArrows.Add(arrow);
        }
    }

    // --- TOP BAR ---
    private void BuildTopBar(RectTransform parent) {
        RectTransform bar = CreatePanel(parent, "TopBar", PanelBg);
        bar.anchorMin = new Vector2(0, 1);
        bar.anchorMax = new Vector2(1, 1);
        bar.pivot = new Vector2(0.5f, 1);
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(0, 60);

        TMP_Text title = CreateText(bar, "TitleText", "SOL SYSTEM MAP", 24, GoldText);
        RectTransform titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0);
        titleRT.anchorMax = new Vector2(0, 1);
        titleRT.pivot = new Vector2(0, 0.5f);
        titleRT.anchoredPosition = new Vector2(25, 0);
        titleRT.sizeDelta = new Vector2(400, 0);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.fontStyle = FontStyles.Bold;

        tierInfoText = CreateText(bar, "TierInfo", "CLICK A PLANET OR MOON", 18, DimText);
        RectTransform tierRT = tierInfoText.GetComponent<RectTransform>();
        tierRT.anchorMin = new Vector2(1, 0);
        tierRT.anchorMax = new Vector2(1, 1);
        tierRT.pivot = new Vector2(1, 0.5f);
        tierRT.anchoredPosition = new Vector2(-25, 0);
        tierRT.sizeDelta = new Vector2(600, 0);
        tierInfoText.alignment = TextAlignmentOptions.MidlineRight;
    }

    // --- SELECTION INFO PANEL (bottom-right) ---
    private void BuildSelectionInfo(RectTransform parent) {
        RectTransform panel = CreatePanel(parent, "SelectionInfo", PanelBg);
        panel.anchorMin = new Vector2(1, 0);
        panel.anchorMax = new Vector2(1, 0);
        panel.pivot = new Vector2(1, 0);
        panel.anchoredPosition = new Vector2(-20, 20);
        panel.sizeDelta = new Vector2(340, 150);

        VerticalLayoutGroup vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(18, 18, 14, 14);
        vlg.spacing = 8;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        selectionNameText = CreateText(panel, "SelectionName", "NO SELECTION", 22, GoldHighlight);
        selectionNameText.alignment = TextAlignmentOptions.TopLeft;
        selectionNameText.fontStyle = FontStyles.Bold;
        selectionNameText.GetComponent<LayoutElement>().preferredHeight = 34;

        CreateSeparator(panel);

        selectionDetailText = CreateText(panel, "SelectionDetail", "Click on a planet or moon\nto view its details.", 16, DimText);
        selectionDetailText.alignment = TextAlignmentOptions.TopLeft;
        selectionDetailText.GetComponent<LayoutElement>().preferredHeight = 70;
    }

    // --- SETTINGS SIDEBAR ---
    private void BuildSettingsSidebar(RectTransform parent) {
        RectTransform sidebar = CreatePanel(parent, "SettingsSidebar", PanelBg);
        sidebar.anchorMin = new Vector2(0, 0.10f);
        sidebar.anchorMax = new Vector2(0, 0.93f);
        sidebar.pivot = new Vector2(0, 0.5f);
        sidebar.anchoredPosition = new Vector2(12, 0);
        sidebar.sizeDelta = new Vector2(300, 0);

        VerticalLayoutGroup vlg = sidebar.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(18, 18, 18, 18);
        vlg.spacing = 10;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        TMP_Text sidebarTitle = CreateText(sidebar, "SidebarTitle", "PARAMETERS", 22, GoldText);
        sidebarTitle.alignment = TextAlignmentOptions.TopLeft;
        sidebarTitle.fontStyle = FontStyles.Bold;
        sidebarTitle.GetComponent<LayoutElement>().preferredHeight = 34;

        CreateSeparator(sidebar);

        // --- SHIELDING LEVEL ---
        CreateText(sidebar, "ShieldLabel", "SHIELDING LEVEL", 16, DimText).GetComponent<LayoutElement>().preferredHeight = 24;
        BuildShieldingSlider(sidebar);

        CreateSpacer(sidebar, 6);
        CreateSeparator(sidebar);
        CreateSpacer(sidebar, 6);

        // --- VR PREVIEW ---
        CreateText(sidebar, "VRLabel", "VR PREVIEW", 16, DimText).GetComponent<LayoutElement>().preferredHeight = 24;
        TMP_Text vrDesc = CreateText(sidebar, "VRDesc", "Shows max feasible VR tier\nfor the selected location", 13, DimText);
        vrDesc.GetComponent<LayoutElement>().preferredHeight = 36;
        vrDesc.fontStyle = FontStyles.Italic;
        BuildVRToggle(sidebar);

        CreateSpacer(sidebar, 6);
        CreateSeparator(sidebar);
        CreateSpacer(sidebar, 6);

        // --- VR PREVIEW DISPLAY (always reserves space, hidden via alpha) ---
        GameObject previewSection = new GameObject("PreviewSection");
        previewSection.transform.SetParent(sidebar, false);
        previewSection.AddComponent<RectTransform>();
        LayoutElement previewSectionLE = previewSection.AddComponent<LayoutElement>();
        previewSectionLE.preferredHeight = 260;

        previewCanvasGroup = previewSection.AddComponent<CanvasGroup>();
        previewCanvasGroup.alpha = 0f;
        previewCanvasGroup.blocksRaycasts = false;

        VerticalLayoutGroup pvlg = previewSection.AddComponent<VerticalLayoutGroup>();
        pvlg.spacing = 8;
        pvlg.childForceExpandWidth = true;
        pvlg.childForceExpandHeight = false;
        pvlg.childControlWidth = true;
        pvlg.childControlHeight = true;

        RectTransform previewPanelRT = previewSection.GetComponent<RectTransform>();

        previewLabel = CreateText(previewPanelRT, "PreviewTierLabel", "MAX VR TIER: --", 17, GoldText);
        previewLabel.alignment = TextAlignmentOptions.Center;
        previewLabel.fontStyle = FontStyles.Bold;
        previewLabel.GetComponent<LayoutElement>().preferredHeight = 28;

        BuildPreviewDisplay(previewPanelRT);
    }

    private void BuildShieldingSlider(RectTransform parent) {
        GameObject sliderGO = new GameObject("ShieldingSlider");
        sliderGO.transform.SetParent(parent, false);
        RectTransform sliderRT = sliderGO.AddComponent<RectTransform>();
        LayoutElement le = sliderGO.AddComponent<LayoutElement>();
        le.preferredHeight = 40;

        shieldingSlider = sliderGO.AddComponent<Slider>();
        shieldingSlider.minValue = 0;
        shieldingSlider.maxValue = 3;
        shieldingSlider.wholeNumbers = true;
        shieldingSlider.value = 0;

        // Background track
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderRT, false);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.3f);
        bgRT.anchorMax = new Vector2(1, 0.7f);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = SliderBg;

        // Fill area
        GameObject fillAreaGO = new GameObject("FillArea");
        fillAreaGO.transform.SetParent(sliderRT, false);
        RectTransform fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0, 0.3f);
        fillAreaRT.anchorMax = new Vector2(1, 0.7f);
        fillAreaRT.offsetMin = Vector2.zero;
        fillAreaRT.offsetMax = Vector2.zero;

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaRT, false);
        RectTransform fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(1, 1);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.color = SliderFill;

        // Handle - bigger for easy clicking
        GameObject handleAreaGO = new GameObject("HandleSlideArea");
        handleAreaGO.transform.SetParent(sliderRT, false);
        RectTransform handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = Vector2.zero;
        handleAreaRT.offsetMax = Vector2.zero;

        GameObject handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaRT, false);
        RectTransform handleRT = handleGO.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(28, 28);
        Image handleImg = handleGO.AddComponent<Image>();
        handleImg.color = GoldHighlight;

        shieldingSlider.fillRect = fillRT;
        shieldingSlider.handleRect = handleRT;
        shieldingSlider.targetGraphic = handleImg;

        // Value label showing current level
        shieldingValueText = CreateText(parent, "ShieldValue", "OFF", 18, GoldHighlight);
        shieldingValueText.alignment = TextAlignmentOptions.Center;
        shieldingValueText.fontStyle = FontStyles.Bold;
        shieldingValueText.GetComponent<LayoutElement>().preferredHeight = 26;

        shieldingSlider.onValueChanged.AddListener((val) => {
            int level = Mathf.RoundToInt(val);
            shieldingValueText.text = ShieldingLabels[level];
            GameManager.Instance.SetShieldingLevel(level);
        });
    }

    private void BuildVRToggle(RectTransform parent) {
        GameObject toggleGO = new GameObject("VRToggle");
        toggleGO.transform.SetParent(parent, false);
        RectTransform toggleRT = toggleGO.AddComponent<RectTransform>();
        LayoutElement le = toggleGO.AddComponent<LayoutElement>();
        le.preferredHeight = 44;

        HorizontalLayoutGroup hlg = toggleGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        GameObject boxGO = new GameObject("ToggleBox");
        boxGO.transform.SetParent(toggleRT, false);
        Image boxImg = boxGO.AddComponent<Image>();
        boxImg.color = ToggleOff;
        LayoutElement boxLE = boxGO.AddComponent<LayoutElement>();
        boxLE.preferredWidth = 64;
        boxLE.preferredHeight = 34;

        TMP_Text statusText = CreateText(toggleRT, "StatusText", "OFF", 17, DimText);
        statusText.fontStyle = FontStyles.Bold;
        statusText.GetComponent<LayoutElement>().preferredWidth = 80;

        Button btn = boxGO.AddComponent<Button>();
        btn.onClick.AddListener(() => {
            vrToggleOn = !vrToggleOn;
            boxImg.color = vrToggleOn ? ToggleOn : ToggleOff;
            statusText.text = vrToggleOn ? "ON" : "OFF";
            statusText.color = vrToggleOn ? GoldText : DimText;
            GameManager.Instance.SetVRPreview(vrToggleOn);

            if(previewCanvasGroup != null) {
                previewCanvasGroup.alpha = vrToggleOn ? 1f : 0f;
                previewCanvasGroup.blocksRaycasts = vrToggleOn;
            }

            if(vrToggleOn) {
                RefreshVRPreview();
            }
        });
    }

    private void BuildPreviewDisplay(RectTransform parent) {
        GameObject displayGO = new GameObject("PreviewImage");
        displayGO.transform.SetParent(parent, false);
        displayGO.AddComponent<RectTransform>();
        previewDisplay = displayGO.AddComponent<RawImage>();
        previewDisplay.color = Color.white;

        LayoutElement le = displayGO.AddComponent<LayoutElement>();
        le.preferredHeight = 220;
        le.preferredWidth = 264;

        Outline outline = displayGO.AddComponent<Outline>();
        outline.effectColor = new Color(GoldText.r, GoldText.g, GoldText.b, 0.4f);
        outline.effectDistance = new Vector2(2, 2);
    }

    // --- BACK BUTTON ---
    private void BuildBackButton(RectTransform parent) {
        RectTransform btnRT = CreatePanel(parent, "BackButton", ButtonNormal);
        btnRT.anchorMin = new Vector2(0, 0);
        btnRT.anchorMax = new Vector2(0, 0);
        btnRT.pivot = new Vector2(0, 0);
        btnRT.anchoredPosition = new Vector2(20, 20);
        btnRT.sizeDelta = new Vector2(130, 48);

        TMP_Text txt = CreateText(btnRT, "BackText", "BACK", 20, GoldText);
        txt.fontStyle = FontStyles.Bold;
        RectTransform txtRT = txt.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;
        txt.alignment = TextAlignmentOptions.Center;

        Button btn = btnRT.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnRT.GetComponent<Image>();
    }

    // ========== 3D PREVIEW SCENE ==========

    private void SetupPreviewScene() {
        previewRenderTex = new RenderTexture(512, 512, 16);
        previewRenderTex.antiAliasing = 2;

        GameObject camGO = new GameObject("PreviewCamera");
        camGO.transform.position = new Vector3(1000, 1000, 995);
        camGO.transform.rotation = Quaternion.identity;
        previewCamera = camGO.AddComponent<Camera>();
        previewCamera.targetTexture = previewRenderTex;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.03f, 0.03f, 0.06f, 1f);
        previewCamera.fieldOfView = 30;
        previewCamera.cullingMask = LayerMask.GetMask("Default");
        previewCamera.depth = -10;

        GameObject lightGO = new GameObject("PreviewLight");
        lightGO.transform.position = new Vector3(998, 1003, 993);
        lightGO.transform.rotation = Quaternion.Euler(30, -30, 0);
        Light previewLight = lightGO.AddComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 1.2f;
        previewLight.color = new Color(1f, 0.95f, 0.85f);

        Vector3 center = new Vector3(1000, 1000, 1000);
        tierPreviewObjects = new GameObject[4];

        // TIER 0: NONE
        GameObject noneParent = new GameObject("Preview_None");
        noneParent.transform.position = center;
        GameObject noneSphere = CreatePreviewPrimitive(PrimitiveType.Sphere, center, 0.7f,
            new Color(0.2f, 0.1f, 0.1f), "NoneSphere", false);
        noneSphere.transform.SetParent(noneParent.transform, true);
        GameObject xBar1 = CreatePreviewPrimitive(PrimitiveType.Cube, center, 1f,
            new Color(0.7f, 0.15f, 0.1f), "XBar1", false);
        xBar1.transform.SetParent(noneParent.transform, true);
        xBar1.transform.localScale = new Vector3(0.08f, 0.08f, 2f);
        xBar1.transform.localRotation = Quaternion.Euler(0, 0, 45);
        GameObject xBar2 = CreatePreviewPrimitive(PrimitiveType.Cube, center, 1f,
            new Color(0.7f, 0.15f, 0.1f), "XBar2", false);
        xBar2.transform.SetParent(noneParent.transform, true);
        xBar2.transform.localScale = new Vector3(0.08f, 0.08f, 2f);
        xBar2.transform.localRotation = Quaternion.Euler(0, 0, -45);
        tierPreviewObjects[0] = noneParent;

        // TIER 1: LOW
        tierPreviewObjects[1] = CreatePreviewPrimitive(PrimitiveType.Sphere, center, 0.8f,
            new Color(0.3f, 0.5f, 0.3f), "Preview_Low", false);

        // TIER 2: MEDIUM
        tierPreviewObjects[2] = CreatePreviewPrimitive(PrimitiveType.Sphere, center, 0.8f,
            new Color(0.3f, 0.4f, 0.8f), "Preview_Medium", true);

        // TIER 3: HIGH
        GameObject highParent = new GameObject("Preview_High");
        highParent.transform.position = center;
        GameObject highSphere = CreatePreviewPrimitive(PrimitiveType.Sphere, center, 0.8f,
            new Color(0.9f, 0.75f, 0.3f), "HighSphere", true);
        highSphere.transform.SetParent(highParent.transform, true);
        GameObject ring = CreatePreviewPrimitive(PrimitiveType.Cylinder, center, 1f,
            new Color(1f, 0.85f, 0.4f, 0.7f), "HighRing", true);
        ring.transform.SetParent(highParent.transform, true);
        ring.transform.localScale = new Vector3(2.2f, 0.03f, 2.2f);
        tierPreviewObjects[3] = highParent;

        for(int i = 0; i < tierPreviewObjects.Length; i++) {
            tierPreviewObjects[i].SetActive(false);
        }

        if(previewDisplay != null) {
            previewDisplay.texture = previewRenderTex;
        }
    }

    private GameObject CreatePreviewPrimitive(PrimitiveType type, Vector3 position, float scale, Color color, string name, bool emissive) {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.position = position;
        obj.transform.localScale = Vector3.one * scale;

        Collider col = obj.GetComponent<Collider>();
        if(col != null) Destroy(col);

        Renderer rend = obj.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        if(emissive) {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 0.8f);
        }
        rend.material = mat;
        return obj;
    }

    private void ShowPreviewTier(int vrTierLevel) {
        if(tierPreviewObjects == null) return;
        for(int i = 0; i < tierPreviewObjects.Length; i++) {
            if(tierPreviewObjects[i] != null)
                tierPreviewObjects[i].SetActive(i == vrTierLevel);
        }

        string[] tierNames = { "NONE", "LOW", "MEDIUM", "HIGH" };
        if(previewLabel != null) {
            previewLabel.text = "MAX VR TIER: " + tierNames[vrTierLevel];
        }
    }

    private void RefreshVRPreview() {
        if(!vrToggleOn) return;

        GameManager gm = GameManager.Instance;
        int vrTier = 0;

        if(gm.CurrentSubLocation != null && gm.CurrentSelection.HasValue) {
            vrTier = gm.GetMoonVRTierLevel(gm.CurrentSubLocation, gm.CurrentSelection.Value);
        } else if(gm.CurrentSelection.HasValue) {
            vrTier = gm.GetVRTierLevel(gm.CurrentSelection.Value);
        }

        ShowPreviewTier(vrTier);
    }

    // ========== EVENT HANDLERS ==========

    private void OnLocationChanged(SpaceEnvironment env) {
        string tier = GameManager.Instance.GetTierName(env);
        int vrTier = GameManager.Instance.GetVRTierLevel(env);
        string[] vrNames = { "None", "Low", "Medium", "High" };

        tierInfoText.text = env.ToString().ToUpper() + "  |  TIER: " + tier.ToUpper() + "  |  VR: " + vrNames[vrTier].ToUpper();
        tierInfoText.color = GoldText;

        selectionNameText.text = env.ToString().ToUpper();

        int moonCount = SubLocationDatabase.GetSubLocations(env).Count;
        string moonsLine = moonCount > 0 ? "Moons: " + moonCount : "No known moons";
        selectionDetailText.text = "Type: Planet\nTier: " + tier + "\nMax VR: " + vrNames[vrTier] + "\n" + moonsLine;

        RefreshVRPreview();
    }

    private void OnSubLocationChanged(string subName) {
        if(GameManager.Instance.CurrentSelection.HasValue) {
            SpaceEnvironment env = GameManager.Instance.CurrentSelection.Value;
            string tier = GameManager.Instance.GetTierName(env);
            int vrTier = GameManager.Instance.GetMoonVRTierLevel(subName, env);
            string[] vrNames = { "None", "Low", "Medium", "High" };

            tierInfoText.text = env.ToString().ToUpper() + " > " + subName.ToUpper() + "  |  VR: " + vrNames[vrTier].ToUpper();

            selectionNameText.text = subName.ToUpper();
            selectionDetailText.text = "Type: Moon\nParent: " + env.ToString() + "\nTier: " + tier + "\nMax VR: " + vrNames[vrTier];

            RefreshVRPreview();
        }
    }

    // ========== UTILITY ==========

    private RectTransform CreatePanel(RectTransform parent, string name, Color color) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        Image img = go.AddComponent<Image>();
        img.color = color;
        return rt;
    }

    private TMP_Text CreateText(RectTransform parent, string name, string text, float fontSize, Color color) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Normal;

        if(go.GetComponent<LayoutElement>() == null) {
            go.AddComponent<LayoutElement>();
        }

        return tmp;
    }

    private void CreateSeparator(RectTransform parent) {
        GameObject go = new GameObject("Separator");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        Image img = go.AddComponent<Image>();
        img.color = new Color(GoldText.r, GoldText.g, GoldText.b, 0.3f);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 1;
    }

    private void CreateSpacer(RectTransform parent, float height) {
        GameObject go = new GameObject("Spacer");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
    }
}
