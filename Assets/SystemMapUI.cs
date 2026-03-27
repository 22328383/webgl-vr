using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SystemMapUI : MonoBehaviour {

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

    private static readonly string[] ShieldingLabels = { "LOW", "MED", "HIGH" };

    private Canvas canvas;
    private RectTransform canvasRT;
    private TMP_Text tierInfoText;
    private TMP_Text selectionNameText;
    private TMP_Text selectionDetailText;

    private Slider shieldingSlider;
    private TMP_Text shieldingValueText;

    private Slider missionSlider;
    private TMP_Text missionValueText;

    private bool vrToggleOn = false;
    private Camera previewCamera;
    private RenderTexture previewRenderTex;
    private GameObject[] tierPreviewObjects;
    private RawImage previewDisplay;
    private TMP_Text previewLabel;
    private CanvasGroup previewCanvasGroup;

    private Image[] hardwareButtonImages;
    private TMP_Text[] hardwareButtonTexts;

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
        GameManager.Instance.OnFidelityChanged += OnFidelityChanged;

        if(GameManager.Instance.CurrentSelection.HasValue) {
            OnLocationChanged(GameManager.Instance.CurrentSelection.Value);
        }

        if(previewCanvasGroup != null) {
            previewCanvasGroup.alpha = 0f;
            previewCanvasGroup.blocksRaycasts = false;
        }

        UpdateHardwareButtons(GameManager.Instance.HardwareClassIndex);
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

        if(vrToggleOn && tierPreviewObjects != null) {
            foreach(var obj in tierPreviewObjects) {
                if(obj != null && obj.activeSelf) {
                    obj.transform.Rotate(0, 45f * Time.deltaTime, 0, Space.Self);
                }
            }
        }
    }

    void OnDestroy() {
        if(GameManager.Instance != null) {
            GameManager.Instance.OnLocationChanged -= OnLocationChanged;
            GameManager.Instance.OnSubLocationChanged -= OnSubLocationChanged;
            GameManager.Instance.OnFidelityChanged -= OnFidelityChanged;
        }
        if(previewRenderTex != null) previewRenderTex.Release();
    }

    public void ExpandPlanet(SpaceEnvironment env) {
        if(SolarSystemBuilder.Instance != null) {
            SolarSystemBuilder.Instance.ToggleMoons(env);
        }
    }

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
            arrow.buttonRT.sizeDelta = new Vector2(48, 36);
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
        tierRT.sizeDelta = new Vector2(700, 0);
        tierInfoText.alignment = TextAlignmentOptions.MidlineRight;
    }

    private void BuildSelectionInfo(RectTransform parent) {
        RectTransform panel = CreatePanel(parent, "SelectionInfo", PanelBg);
        panel.anchorMin = new Vector2(1, 0);
        panel.anchorMax = new Vector2(1, 0);
        panel.pivot = new Vector2(1, 0);
        panel.anchoredPosition = new Vector2(-20, 20);
        panel.sizeDelta = new Vector2(380, 210);

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

        selectionDetailText = CreateText(panel, "SelectionDetail", "Click on a planet or moon\nto view its details.", 15, DimText);
        selectionDetailText.alignment = TextAlignmentOptions.TopLeft;
        selectionDetailText.GetComponent<LayoutElement>().preferredHeight = 130;
    }

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

        CreateText(sidebar, "ShieldLabel", "SHIELDING LEVEL", 16, DimText).GetComponent<LayoutElement>().preferredHeight = 24;
        TMP_Text shieldDesc = CreateText(sidebar, "ShieldDesc", "Spacecraft hull thickness\nreducing radiation exposure", 13, DimText);
        shieldDesc.GetComponent<LayoutElement>().preferredHeight = 36;
        shieldDesc.fontStyle = FontStyles.Italic;
        BuildShieldingSlider(sidebar);

        CreateSpacer(sidebar, 6);
        CreateSeparator(sidebar);
        CreateSpacer(sidebar, 6);

        CreateText(sidebar, "MissionLabel", "MISSION DURATION", 16, DimText).GetComponent<LayoutElement>().preferredHeight = 24;
        TMP_Text missionDesc = CreateText(sidebar, "MissionDesc", "Total years of operation\nat the selected location", 13, DimText);
        missionDesc.GetComponent<LayoutElement>().preferredHeight = 36;
        missionDesc.fontStyle = FontStyles.Italic;
        BuildMissionSlider(sidebar);

        CreateSpacer(sidebar, 6);
        CreateSeparator(sidebar);
        CreateSpacer(sidebar, 6);

        CreateText(sidebar, "HWLabel", "HARDWARE CLASS", 16, DimText).GetComponent<LayoutElement>().preferredHeight = 24;
        TMP_Text hwDesc = CreateText(sidebar, "HWDesc", "Onboard compute hardware\nfor VR rendering", 13, DimText);
        hwDesc.GetComponent<LayoutElement>().preferredHeight = 36;
        hwDesc.fontStyle = FontStyles.Italic;
        BuildHardwareSelector(sidebar);

        CreateSpacer(sidebar, 6);
        CreateSeparator(sidebar);
        CreateSpacer(sidebar, 6);

        CreateText(sidebar, "VRLabel", "VR PREVIEW", 16, DimText).GetComponent<LayoutElement>().preferredHeight = 24;
        TMP_Text vrDesc = CreateText(sidebar, "VRDesc", "Shows max feasible VR tier\nfor the selected location", 13, DimText);
        vrDesc.GetComponent<LayoutElement>().preferredHeight = 36;
        vrDesc.fontStyle = FontStyles.Italic;
        BuildVRToggle(sidebar);

        CreateSpacer(sidebar, 6);
        CreateSeparator(sidebar);
        CreateSpacer(sidebar, 6);

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

        previewLabel = CreateText(previewPanelRT, "PreviewTierLabel", "FIDELITY: --", 17, GoldText);
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
        shieldingSlider.maxValue = 2;
        shieldingSlider.wholeNumbers = true;
        shieldingSlider.value = 0;

        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderRT, false);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.3f);
        bgRT.anchorMax = new Vector2(1, 0.7f);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = SliderBg;

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

        shieldingValueText = CreateText(parent, "ShieldValue", "LOW", 18, GoldHighlight);
        shieldingValueText.alignment = TextAlignmentOptions.Center;
        shieldingValueText.fontStyle = FontStyles.Bold;
        shieldingValueText.GetComponent<LayoutElement>().preferredHeight = 26;

        shieldingSlider.onValueChanged.AddListener((val) => {
            int level = Mathf.RoundToInt(val);
            shieldingValueText.text = ShieldingLabels[level];
            GameManager.Instance.SetShieldingLevel(level);
        });
    }

    private void BuildMissionSlider(RectTransform parent) {
        GameObject sliderGO = new GameObject("MissionSlider");
        sliderGO.transform.SetParent(parent, false);
        RectTransform sliderRT = sliderGO.AddComponent<RectTransform>();
        LayoutElement le = sliderGO.AddComponent<LayoutElement>();
        le.preferredHeight = 40;

        missionSlider = sliderGO.AddComponent<Slider>();
        missionSlider.minValue = 1;
        missionSlider.maxValue = 20;
        missionSlider.wholeNumbers = true;
        missionSlider.value = 1;

        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderRT, false);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.3f);
        bgRT.anchorMax = new Vector2(1, 0.7f);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = SliderBg;

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

        missionSlider.fillRect = fillRT;
        missionSlider.handleRect = handleRT;
        missionSlider.targetGraphic = handleImg;

        missionValueText = CreateText(parent, "MissionValue", "1 YR", 18, GoldHighlight);
        missionValueText.alignment = TextAlignmentOptions.Center;
        missionValueText.fontStyle = FontStyles.Bold;
        missionValueText.GetComponent<LayoutElement>().preferredHeight = 26;

        missionSlider.onValueChanged.AddListener((val) => {
            int years = Mathf.RoundToInt(val);
            missionValueText.text = years + " YR";
            GameManager.Instance.SetMissionDuration(years);
        });
    }

    private void BuildHardwareSelector(RectTransform parent) {
        GameObject rowGO = new GameObject("HardwareRow");
        rowGO.transform.SetParent(parent, false);
        rowGO.AddComponent<RectTransform>();
        LayoutElement rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 40;

        HorizontalLayoutGroup hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        hardwareButtonImages = new Image[4];
        hardwareButtonTexts = new TMP_Text[4];

        for(int i = 0; i < 4; i++) {
            int index = i;
            GameObject btnGO = new GameObject("HW_" + RadiationCalculator.HardwareNames[i]);
            btnGO.transform.SetParent(rowGO.transform, false);
            btnGO.AddComponent<RectTransform>();

            Image img = btnGO.AddComponent<Image>();
            img.color = ToggleOff;
            hardwareButtonImages[i] = img;

            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = img;

            TMP_Text txt = CreateText(btnGO.GetComponent<RectTransform>(), "Label",
                RadiationCalculator.HardwareNames[i].ToUpper(), 11, DimText);
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontStyle = FontStyles.Bold;
            RectTransform txtRT = txt.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;
            hardwareButtonTexts[i] = txt;

            btn.onClick.AddListener(() => {
                GameManager.Instance.SetHardwareClass(index);
                UpdateHardwareButtons(index);
            });
        }
    }

    private void UpdateHardwareButtons(int selectedIndex) {
        if(hardwareButtonImages == null) return;
        for(int i = 0; i < 4; i++) {
            hardwareButtonImages[i].color = (i == selectedIndex) ? ToggleOn : ToggleOff;
            hardwareButtonTexts[i].color = (i == selectedIndex) ? GoldText : DimText;
        }
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
        previewCamera.fieldOfView = 20;
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

        tierPreviewObjects[0] = LoadErrorTextModel(center);
        if(tierPreviewObjects[0] == null) {
            GameObject noneParent = new GameObject("Preview_None");
            noneParent.transform.position = center;
            GameObject noneSphere = CreatePreviewPrimitive(PrimitiveType.Sphere, center, 0.7f,
                new Color(0.7f, 0.15f, 0.1f), "NoneSphere", true);
            noneSphere.transform.SetParent(noneParent.transform, true);
            tierPreviewObjects[0] = noneParent;
        }

        tierPreviewObjects[1] = LoadPreviewModel("Models/Mario/mario", center, 0.015f, 0.45f);
        if(tierPreviewObjects[1] == null)
            tierPreviewObjects[1] = CreatePreviewPrimitive(PrimitiveType.Sphere, center, 0.8f,
                new Color(0.3f, 0.5f, 0.3f), "Preview_Low", false);

        tierPreviewObjects[2] = LoadPreviewModel("Models/CounterStrike/COUNTER-TERRORIST_GIGN", center, 0.015f, 0.28f);
        if(tierPreviewObjects[2] == null)
            tierPreviewObjects[2] = CreatePreviewPrimitive(PrimitiveType.Sphere, center, 0.8f,
                new Color(0.3f, 0.4f, 0.8f), "Preview_Medium", true);

        tierPreviewObjects[3] = LoadPreviewModel("Models/DoomSlayer/doommarine", center, 0.008f, 0.28f);
        if(tierPreviewObjects[3] == null) {
            GameObject highParent = new GameObject("Preview_High");
            highParent.transform.position = center;
            GameObject highSphere = CreatePreviewPrimitive(PrimitiveType.Sphere, center, 0.8f,
                new Color(0.9f, 0.75f, 0.3f), "HighSphere", true);
            highSphere.transform.SetParent(highParent.transform, true);
            tierPreviewObjects[3] = highParent;
        }

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

    private GameObject LoadErrorTextModel(Vector3 center) {
        GameObject prefab = Resources.Load<GameObject>("Models/ErrorText/ERRORText");
        if(prefab == null) return null;

        GameObject instance = Instantiate(prefab);
        instance.name = "Preview_ErrorText";

        foreach(Collider col in instance.GetComponentsInChildren<Collider>())
            Destroy(col);

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if(renderers.Length == 0) {
            Destroy(instance);
            return null;
        }

        ApplyModelTextures(instance, "Models/ErrorText/ERRORText");

        instance.transform.position = center;
        instance.transform.localScale = Vector3.one;

        Bounds bounds = new Bounds(center, Vector3.zero);
        foreach(Renderer r in renderers)
            bounds.Encapsulate(r.bounds);

        float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if(maxDim > 0.001f) {
            float targetSize = 1.2f;
            float s = targetSize / maxDim;
            instance.transform.localScale = Vector3.one * s;
        }

        bounds = new Bounds(center, Vector3.zero);
        foreach(Renderer r in instance.GetComponentsInChildren<Renderer>())
            bounds.Encapsulate(r.bounds);
        Vector3 offset = center - bounds.center;
        instance.transform.position += offset;

        return instance;
    }

    private GameObject LoadPreviewModel(string resourcePath, Vector3 position, float scale, float bustFraction) {
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if(prefab == null) return null;

        GameObject instance = Instantiate(prefab);
        instance.name = "Preview_" + prefab.name;
        instance.transform.position = position;
        instance.transform.localScale = Vector3.one * scale;

        foreach(Collider col in instance.GetComponentsInChildren<Collider>())
            Destroy(col);

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if(renderers.Length == 0) {
            Destroy(instance);
            return null;
        }

        ApplyModelTextures(instance, resourcePath);

        Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
        foreach(Renderer r in renderers)
            bounds.Encapsulate(r.bounds);

        if(bounds.size.magnitude > 0.01f) {
            float fullHeight = bounds.size.y;
            float bustTop = bounds.max.y;
            float bustBottom = bustTop - fullHeight * bustFraction;
            float bustHeight = bustTop - bustBottom;
            float bustCenterY = bustBottom + bustHeight * 0.5f;

            float maxExtent = Mathf.Max(bounds.extents.x, bustHeight * 0.5f, bounds.extents.z);
            if(maxExtent > 0) {
                float desiredSize = 0.9f;
                float adjustedScale = (desiredSize / maxExtent) * instance.transform.localScale.x;
                instance.transform.localScale = Vector3.one * adjustedScale;
            }

            bounds = new Bounds(instance.transform.position, Vector3.zero);
            foreach(Renderer r in instance.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(r.bounds);

            fullHeight = bounds.size.y;
            bustTop = bounds.max.y;
            bustBottom = bustTop - fullHeight * bustFraction;
            bustCenterY = bustBottom + (bustTop - bustBottom) * 0.5f;

            Vector3 offset = position - new Vector3(bounds.center.x, bustCenterY, bounds.center.z);
            instance.transform.position += offset;
        }

        return instance;
    }

    private void ApplyModelTextures(GameObject instance, string resourcePath) {
        string folder = resourcePath.Substring(0, resourcePath.LastIndexOf('/') + 1);

        foreach(Renderer rend in instance.GetComponentsInChildren<Renderer>()) {
            foreach(Material mat in rend.materials) {
                if(mat.mainTexture != null) continue;

                string matName = mat.name.Replace(" (Instance)", "");

                string texName = null;
                if(matName.Contains("GIGN_DMBASE2")) texName = "GIGN_DMBASE2";
                else if(matName.Contains("Backpack2")) texName = "Backpack2";
                else if(matName.Contains("doommarine_arms")) texName = "models_characters_doommarine_doommarine_arms_c";
                else if(matName.Contains("doommarine_cowl")) texName = "models_characters_doommarine_doommarine_cowl_c";
                else if(matName.Contains("doommarine_helmet")) texName = "models_characters_doommarine_doommarine_helmet_c";
                else if(matName.Contains("doommarine_legs")) texName = "models_characters_doommarine_doommarine_legs_c";
                else if(matName.Contains("doommarine_torso")) texName = "models_characters_doommarine_doommarine_torso_c";
                else if(matName.Contains("doommarine_visor")) texName = "models_characters_doommarine_doommarine_visor_c";
                else if(matName.Contains("typeBlinn") || resourcePath.Contains("ErrorText")) texName = "ERRORText_typeBlinn_BaseColor";

                if(texName != null) {
                    Texture2D tex = Resources.Load<Texture2D>(folder + texName);
                    if(tex != null) {
                        mat.mainTexture = tex;
                        if(texName.Contains("ERRORText")) {
                            Texture2D emTex = Resources.Load<Texture2D>(folder + "ERRORText_typeBlinn_Emissive");
                            if(emTex != null) {
                                mat.EnableKeyword("_EMISSION");
                                mat.SetTexture("_EmissionMap", emTex);
                                mat.SetColor("_EmissionColor", Color.white);
                            }
                        }
                    }
                }

                if(mat.mainTexture == null) {
                    Texture2D[] allTex = Resources.LoadAll<Texture2D>(folder.TrimEnd('/'));
                    if(allTex.Length > 0) mat.mainTexture = allTex[0];
                }
            }
        }
    }

    private void ShowPreviewTier(int vrTierLevel) {
        if(tierPreviewObjects == null) return;
        for(int i = 0; i < tierPreviewObjects.Length; i++) {
            if(tierPreviewObjects[i] != null)
                tierPreviewObjects[i].SetActive(i == vrTierLevel);
        }

        string[] tierNames = { "NONE", "LOW", "MEDIUM", "HIGH" };
        if(previewLabel != null) {
            previewLabel.text = "FIDELITY: " + tierNames[vrTierLevel];
        }
    }

    private void RefreshVRPreview() {
        if(!vrToggleOn) return;

        GameManager gm = GameManager.Instance;
        ShowPreviewTier(gm.CurrentResult.tierLevel);
    }

    private void OnLocationChanged(SpaceEnvironment env) {
        selectionNameText.text = env.ToString().ToUpper();
        tierInfoText.color = GoldText;

        int moonCount = SubLocationDatabase.GetSubLocations(env).Count;
        string moonsLine = moonCount > 0 ? "Sub-locations: " + moonCount : "No sub-locations";
        selectionDetailText.text = "Type: Planet\n" + moonsLine + "\n\nCalculating...";
    }

    private void OnSubLocationChanged(string subName) {
        if(!GameManager.Instance.CurrentSelection.HasValue) return;
        selectionNameText.text = subName.ToUpper();
        selectionDetailText.text = "Type: Moon\n\nCalculating...";
    }

    private void OnFidelityChanged(FidelityResult result) {
        string locName = string.IsNullOrEmpty(GameManager.Instance.CurrentSubLocation)
            ? GameManager.Instance.CurrentSelection.Value.ToString().ToUpper()
            : GameManager.Instance.CurrentSubLocation.ToUpper();

        string lifespan = RadiationCalculator.FormatLifespan(result.lifespanYears);
        bool willFail = result.lifespanYears > 0 && result.lifespanYears < result.missionDurationYears;

        string topLifespan = lifespan;
        if(willFail) {
            topLifespan = "<color=#FF4444>FAILS AT YEAR " + Mathf.FloorToInt(result.lifespanYears) + "</color>";
        }

        tierInfoText.text = locName + "  |  " + topLifespan;
        tierInfoText.color = GoldText;

        selectionNameText.text = locName;

        bool isMoon = !string.IsNullOrEmpty(GameManager.Instance.CurrentSubLocation);
        string typeStr = isMoon ? "Moon" : "Planet";

        string lifespanLine = willFail
            ? "<color=#FF4444>HARDWARE FAILS AT YEAR " + Mathf.FloorToInt(result.lifespanYears) + "</color>"
            : lifespan;

        string detail = "Type: " + typeStr
            + "\nEff. TID: " + result.effectiveTID.ToString("F3") + " krad/yr"
            + "\nMission Dose: " + result.totalMissionDose.ToString("F2") + " krad"
            + "\nFidelity: " + result.tierName + " (" + result.tierShortName + ")"
            + "\nLifespan: " + lifespanLine;

        if(!result.hardwareSurvives) {
            detail += "\n<color=#FF4444>Total dose exceeds tolerance!</color>";
        }

        selectionDetailText.text = detail;

        RefreshVRPreview();
    }

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
        tmp.richText = true;

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
