using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.UI;
using SwingingPaintBucket.Rendering;
using System.IO;

public class AutoSetupScene : EditorWindow
{
    [MenuItem("Tools/Setup Simulation Scene")]
    public static void SetupScene()
    {
        // 1. Create a new empty scene
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Create Materials directory if it doesn't exist
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        // Load or create materials
        Material canvasMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CanvasMaterial.mat");
        if (canvasMat == null)
        {
            Shader litShader = Shader.Find("SwingingPaintBucket/PaintSurface");
            if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            canvasMat = new Material(litShader);
            canvasMat.color = Color.white;
            AssetDatabase.CreateAsset(canvasMat, "Assets/Materials/CanvasMaterial.mat");
        }

        Material bucketMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BucketMaterial.mat");
        if (bucketMat == null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            bucketMat = new Material(litShader);
            AssetDatabase.CreateAsset(bucketMat, "Assets/Materials/BucketMaterial.mat");
        }
        // Make the bucket transparent so the paint is visible inside it
        MakeMaterialTransparent(bucketMat, new Color(0.85f, 0.9f, 0.95f), 0.35f);
        if (bucketMat.HasProperty("_Metallic")) bucketMat.SetFloat("_Metallic", 0.1f);
        if (bucketMat.HasProperty("_Smoothness")) bucketMat.SetFloat("_Smoothness", 0.9f);
        if (bucketMat.HasProperty("_Glossiness")) bucketMat.SetFloat("_Glossiness", 0.9f);
        EditorUtility.SetDirty(bucketMat);

        Material handleMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/HandleMaterial.mat");
        if (handleMat == null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            handleMat = new Material(litShader);
            AssetDatabase.CreateAsset(handleMat, "Assets/Materials/HandleMaterial.mat");
        }
        // Dark metallic steel wire properties
        handleMat.color = new Color(0.42f, 0.44f, 0.46f);
        if (handleMat.HasProperty("_Metallic")) handleMat.SetFloat("_Metallic", 0.9f);
        if (handleMat.HasProperty("_Smoothness")) handleMat.SetFloat("_Smoothness", 0.7f);
        if (handleMat.HasProperty("_Glossiness")) handleMat.SetFloat("_Glossiness", 0.7f);
        EditorUtility.SetDirty(handleMat);

        Material gripMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GripMaterial.mat");
        if (gripMat == null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            gripMat = new Material(litShader);
            AssetDatabase.CreateAsset(gripMat, "Assets/Materials/GripMaterial.mat");
        }
        // Wooden bucket handle grip properties
        gripMat.color = new Color(0.6f, 0.42f, 0.26f);
        if (gripMat.HasProperty("_Metallic")) gripMat.SetFloat("_Metallic", 0.0f);
        if (gripMat.HasProperty("_Smoothness")) gripMat.SetFloat("_Smoothness", 0.2f);
        if (gripMat.HasProperty("_Glossiness")) gripMat.SetFloat("_Glossiness", 0.2f);
        EditorUtility.SetDirty(gripMat);

        Material woodFrameMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WoodFrameMaterial.mat");
        if (woodFrameMat == null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            woodFrameMat = new Material(litShader);
            AssetDatabase.CreateAsset(woodFrameMat, "Assets/Materials/WoodFrameMaterial.mat");
        }
        // Wooden canvas frame borders properties
        woodFrameMat.color = new Color(0.32f, 0.22f, 0.12f);
        if (woodFrameMat.HasProperty("_Metallic")) woodFrameMat.SetFloat("_Metallic", 0.0f);
        if (woodFrameMat.HasProperty("_Smoothness")) woodFrameMat.SetFloat("_Smoothness", 0.1f);
        if (woodFrameMat.HasProperty("_Glossiness")) woodFrameMat.SetFloat("_Glossiness", 0.1f);
        EditorUtility.SetDirty(woodFrameMat);

        Material paintMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PaintMaterial.mat");
        if (paintMat == null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            paintMat = new Material(litShader);
            paintMat.color = Color.red;
            AssetDatabase.CreateAsset(paintMat, "Assets/Materials/PaintMaterial.mat");
        }

        Material particleMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ParticleMaterial.mat");
        if (particleMat == null)
        {
            Shader particleShader = Shader.Find("SwingingPaintBucket/ParticleInstanced") ?? Shader.Find("Standard");
            particleMat = new Material(particleShader);
            AssetDatabase.CreateAsset(particleMat, "Assets/Materials/ParticleMaterial.mat");
        }

        // 2. Create the 3D Canvas (Painting Board)
        GameObject canvas3D = GameObject.CreatePrimitive(PrimitiveType.Plane);
        canvas3D.name = "Canvas3D";
        canvas3D.transform.position = new Vector3(0, 0, 0);
        canvas3D.transform.localScale = new Vector3(1, 1, 1); // 10x10 meters
        MeshFilter canvasMeshFilter = canvas3D.GetComponent<MeshFilter>();
        MeshRenderer canvasMeshRenderer = canvas3D.GetComponent<MeshRenderer>();
        canvasMeshRenderer.material = canvasMat;

        // Add a beautiful wooden easel frame backing the canvas plane
        GameObject canvasFrameParent = new GameObject("CanvasEaselFrame");
        canvasFrameParent.transform.position = canvas3D.transform.position;
        canvasFrameParent.transform.rotation = canvas3D.transform.rotation;
        canvasFrameParent.transform.SetParent(canvas3D.transform, true);

        GameObject leftBorder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftBorder.name = "LeftBorder";
        leftBorder.transform.SetParent(canvasFrameParent.transform, false);
        leftBorder.transform.localPosition = new Vector3(-5.15f, -0.05f, 0f);
        leftBorder.transform.localScale = new Vector3(0.3f, 0.15f, 10.6f);
        leftBorder.GetComponent<MeshRenderer>().material = woodFrameMat;

        GameObject rightBorder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightBorder.name = "RightBorder";
        rightBorder.transform.SetParent(canvasFrameParent.transform, false);
        rightBorder.transform.localPosition = new Vector3(5.15f, -0.05f, 0f);
        rightBorder.transform.localScale = new Vector3(0.3f, 0.15f, 10.6f);
        rightBorder.GetComponent<MeshRenderer>().material = woodFrameMat;

        GameObject topBorder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        topBorder.name = "TopBorder";
        topBorder.transform.SetParent(canvasFrameParent.transform, false);
        topBorder.transform.localPosition = new Vector3(0f, -0.05f, 5.15f);
        topBorder.transform.localScale = new Vector3(10.6f, 0.15f, 0.3f);
        topBorder.GetComponent<MeshRenderer>().material = woodFrameMat;

        GameObject bottomBorder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bottomBorder.name = "BottomBorder";
        bottomBorder.transform.SetParent(canvasFrameParent.transform, false);
        bottomBorder.transform.localPosition = new Vector3(0f, -0.05f, -5.15f);
        bottomBorder.transform.localScale = new Vector3(10.6f, 0.15f, 0.3f);
        bottomBorder.GetComponent<MeshRenderer>().material = woodFrameMat;

        // 3. Create the 3D Bucket & Paint Indicator
        GameObject bucketParent = new GameObject("Bucket");
        bucketParent.transform.position = new Vector3(0, 3, 0);
        
        // Main bucket body cylinder
        GameObject bucketBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bucketBody.name = "BucketBody";
        bucketBody.transform.SetParent(bucketParent.transform, false);
        bucketBody.transform.localPosition = Vector3.zero;
        bucketBody.transform.localScale = new Vector3(1, 1, 1);
        bucketBody.GetComponent<MeshRenderer>().material = bucketMat;

        // Top Rim (slightly wider cylinder at top)
        GameObject topRim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        topRim.name = "TopRim";
        topRim.transform.SetParent(bucketParent.transform, false);
        topRim.transform.localPosition = new Vector3(0, 0.95f, 0);
        topRim.transform.localScale = new Vector3(1.06f, 0.05f, 1.06f);
        topRim.GetComponent<MeshRenderer>().material = bucketMat;

        // Bottom Rim (slightly narrower cylinder at bottom)
        GameObject bottomRim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bottomRim.name = "BottomRim";
        bottomRim.transform.SetParent(bucketParent.transform, false);
        bottomRim.transform.localPosition = new Vector3(0, -0.95f, 0);
        bottomRim.transform.localScale = new Vector3(0.92f, 0.05f, 0.92f);
        bottomRim.GetComponent<MeshRenderer>().material = bucketMat;

        // Side handle hinges/mounts
        GameObject leftEar = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leftEar.name = "LeftHandleMount";
        leftEar.transform.SetParent(bucketParent.transform, false);
        leftEar.transform.localPosition = new Vector3(-0.52f, 0.8f, 0);
        leftEar.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
        leftEar.GetComponent<MeshRenderer>().material = handleMat;

        GameObject rightEar = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rightEar.name = "RightHandleMount";
        rightEar.transform.SetParent(bucketParent.transform, false);
        rightEar.transform.localPosition = new Vector3(0.52f, 0.8f, 0);
        rightEar.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
        rightEar.GetComponent<MeshRenderer>().material = handleMat;

        // Wire handle pieces
        GameObject leftWire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leftWire.name = "LeftWireHandle";
        leftWire.transform.SetParent(bucketParent.transform, false);
        leftWire.transform.localPosition = new Vector3(-0.35f, 1.25f, 0);
        leftWire.transform.localRotation = Quaternion.Euler(0, 0, -35f);
        leftWire.transform.localScale = new Vector3(0.02f, 0.48f, 0.02f);
        leftWire.GetComponent<MeshRenderer>().material = handleMat;

        GameObject rightWire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rightWire.name = "RightWireHandle";
        rightWire.transform.SetParent(bucketParent.transform, false);
        rightWire.transform.localPosition = new Vector3(0.35f, 1.25f, 0);
        rightWire.transform.localRotation = Quaternion.Euler(0, 0, 35f);
        rightWire.transform.localScale = new Vector3(0.02f, 0.48f, 0.02f);
        rightWire.GetComponent<MeshRenderer>().material = handleMat;

        GameObject wireGripCenter = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wireGripCenter.name = "WireGripCenter";
        wireGripCenter.transform.SetParent(bucketParent.transform, false);
        wireGripCenter.transform.localPosition = new Vector3(0, 1.55f, 0);
        wireGripCenter.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        wireGripCenter.transform.localScale = new Vector3(0.02f, 0.4f, 0.02f);
        wireGripCenter.GetComponent<MeshRenderer>().material = handleMat;

        GameObject woodGrip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        woodGrip.name = "WoodGrip";
        woodGrip.transform.SetParent(bucketParent.transform, false);
        woodGrip.transform.localPosition = new Vector3(0, 1.55f, 0);
        woodGrip.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        woodGrip.transform.localScale = new Vector3(0.06f, 0.2f, 0.06f);
        woodGrip.GetComponent<MeshRenderer>().material = gripMat;

        // Paint indicator
        GameObject paintIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        paintIndicator.name = "PaintIndicator";
        paintIndicator.transform.SetParent(bucketParent.transform, false);
        paintIndicator.transform.localPosition = new Vector3(0, 0.05f, 0);
        paintIndicator.transform.localScale = new Vector3(0.95f, 0.9f, 0.95f);
        paintIndicator.GetComponent<MeshRenderer>().material = paintMat;

        // 4. Create the Simulation Manager GameObject
        GameObject simManagerObj = new GameObject("SimulationManager");
        SimulationManager simManager = simManagerObj.AddComponent<SimulationManager>();

        // 5. Create Renderers on the SimulationManager
        ParticleRenderer pRenderer = simManagerObj.AddComponent<ParticleRenderer>();
        BucketRenderer bRenderer = simManagerObj.AddComponent<BucketRenderer>();
        RopeRenderer rRenderer = simManagerObj.AddComponent<RopeRenderer>();
        CanvasPaintRenderer cpRenderer = canvas3D.AddComponent<CanvasPaintRenderer>();

        // Set references on Renderers
        SerializedObject soPRenderer = new SerializedObject(pRenderer);
        soPRenderer.FindProperty("_particleMaterial").objectReferenceValue = particleMat;
        soPRenderer.ApplyModifiedProperties();

        SerializedObject soBRenderer = new SerializedObject(bRenderer);
        soBRenderer.FindProperty("_bucketTransform").objectReferenceValue = bucketParent.transform;
        soBRenderer.FindProperty("_paintLevelIndicator").objectReferenceValue = paintIndicator.transform;
        soBRenderer.FindProperty("_paintRenderer").objectReferenceValue = paintIndicator.GetComponent<MeshRenderer>();
        soBRenderer.ApplyModifiedProperties();

        SerializedObject soCPRenderer = new SerializedObject(cpRenderer);
        soCPRenderer.FindProperty("_canvasMeshFilter").objectReferenceValue = canvasMeshFilter;
        soCPRenderer.FindProperty("_canvasMeshRenderer").objectReferenceValue = canvasMeshRenderer;
        soCPRenderer.ApplyModifiedProperties();

        // 6. Create UI Canvas & EventSystem
        GameObject canvasUIObj = new GameObject("CanvasUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvasUI = canvasUIObj.GetComponent<Canvas>();
        canvasUI.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasUIObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject eventSystemObj = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif

        // Create UI Manager GameObject
        GameObject uiManagerObj = new GameObject("UIManager");
        UIManager uiManager = uiManagerObj.AddComponent<UIManager>();

        // Build UI elements
        DefaultControls.Resources uiRes = new DefaultControls.Resources();

        // Palette definitions for UI styling
        Color panelBgColor = new Color(0.06f, 0.08f, 0.12f, 0.96f);
        Color sidebarBgColor = new Color(0.09f, 0.11f, 0.16f, 0.92f);
        Color controlBarBgColor = new Color(0.12f, 0.15f, 0.22f, 0.94f);
        Color primaryBtnColor = new Color(0.12f, 0.45f, 0.85f, 1f);
        Color successBtnColor = new Color(0.15f, 0.55f, 0.35f, 1f);
        Color warningBtnColor = new Color(0.85f, 0.5f, 0.1f, 1f);
        Color dangerBtnColor = new Color(0.75f, 0.22f, 0.22f, 1f);
        Color neutralBtnColor = new Color(0.2f, 0.23f, 0.28f, 1f);

        // Main Menu Panel
        GameObject mainMenuPanel = DefaultControls.CreatePanel(uiRes);
        mainMenuPanel.name = "MainMenuPanel";
        mainMenuPanel.transform.SetParent(canvasUIObj.transform, false);
        SetRectTransformFullStretch(mainMenuPanel.GetComponent<RectTransform>());
        mainMenuPanel.GetComponent<Image>().color = panelBgColor;

        GameObject menuTitle = DefaultControls.CreateText(uiRes);
        menuTitle.name = "Title";
        menuTitle.transform.SetParent(mainMenuPanel.transform, false);
        Text menuTitleText = menuTitle.GetComponent<Text>();
        menuTitleText.text = "Swinging Paint Bucket Simulation";
        menuTitleText.fontSize = 48;
        menuTitleText.fontStyle = FontStyle.Bold;
        menuTitleText.alignment = TextAnchor.MiddleCenter;
        menuTitleText.color = Color.white;
        SetRectTransformAnchor(menuTitle.GetComponent<RectTransform>(), 0.5f, 0.7f, 800, 100);

        GameObject startButtonObj = DefaultControls.CreateButton(uiRes);
        startButtonObj.name = "StartButton";
        startButtonObj.transform.SetParent(mainMenuPanel.transform, false);
        SetRectTransformAnchor(startButtonObj.GetComponent<RectTransform>(), 0.5f, 0.4f, 300, 70);
        startButtonObj.GetComponentInChildren<Text>().text = "Start Simulation";
        StyleButton(startButtonObj, primaryBtnColor, Color.white, 22);

        // Simulation Panel
        GameObject simulationPanel = DefaultControls.CreatePanel(uiRes);
        simulationPanel.name = "SimulationPanel";
        simulationPanel.transform.SetParent(canvasUIObj.transform, false);
        SetRectTransformFullStretch(simulationPanel.GetComponent<RectTransform>());
        simulationPanel.GetComponent<Image>().color = Color.clear;
        simulationPanel.SetActive(false);

        // Left sidebar (parameter editor panel)
        GameObject parameterPanelObj = DefaultControls.CreatePanel(uiRes);
        parameterPanelObj.name = "ParameterPanel";
        parameterPanelObj.transform.SetParent(simulationPanel.transform, false);
        RectTransform paramPanelRect = parameterPanelObj.GetComponent<RectTransform>();
        paramPanelRect.anchorMin = new Vector2(0, 0);
        paramPanelRect.anchorMax = new Vector2(0, 1);
        paramPanelRect.pivot = new Vector2(0, 0.5f);
        paramPanelRect.anchoredPosition = Vector2.zero;
        paramPanelRect.sizeDelta = new Vector2(380, 0);
        parameterPanelObj.GetComponent<Image>().color = sidebarBgColor;

        // Scrollview inside Parameter panel
        GameObject scrollView = DefaultControls.CreateScrollView(uiRes);
        scrollView.name = "ParameterScrollView";
        scrollView.transform.SetParent(parameterPanelObj.transform, false);
        SetRectTransformFullStretch(scrollView.GetComponent<RectTransform>());
        ScrollRect scrollRect = scrollView.GetComponent<ScrollRect>();
        Transform contentTrans = scrollRect.content;
        
        // Add Vertical Layout Group to scroll content
        VerticalLayoutGroup vlg = contentTrans.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(15, 15, 15, 15);
        vlg.spacing = 10;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        
        ContentSizeFitter csf = contentTrans.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Sliders helper creation inside content
        Slider bucketMassSlider, bucketRadiusSlider, orificeRadiusSlider, paintVolumeSlider;
        Text bucketMassLabel, bucketRadiusLabel, orificeRadiusLabel, paintVolumeLabel;
        CreateSliderItem(contentTrans, "Bucket Mass", 0.05f, 10f, out bucketMassSlider, out bucketMassLabel, uiRes);
        CreateSliderItem(contentTrans, "Bucket Radius", 0.02f, 0.5f, out bucketRadiusSlider, out bucketRadiusLabel, uiRes);
        CreateSliderItem(contentTrans, "Orifice Radius", 0.001f, 0.05f, out orificeRadiusSlider, out orificeRadiusLabel, uiRes);
        CreateSliderItem(contentTrans, "Paint Volume", 0.0001f, 0.01f, out paintVolumeSlider, out paintVolumeLabel, uiRes);

        Slider ropeLengthSlider, ropeDampingSlider;
        Text ropeLengthLabel;
        CreateSliderItem(contentTrans, "Rope Length", 0.1f, 10f, out ropeLengthSlider, out ropeLengthLabel, uiRes);
        
        GameObject toggleObj = DefaultControls.CreateToggle(uiRes);
        toggleObj.transform.SetParent(contentTrans, false);
        Toggle ropeElasticToggle = toggleObj.GetComponent<Toggle>();
        Text ropeToggleText = toggleObj.GetComponentInChildren<Text>();
        ropeToggleText.text = "Rope Elasticity";
        ropeToggleText.color = new Color(0.85f, 0.88f, 0.95f);
        ropeToggleText.fontStyle = FontStyle.Bold;
        
        CreateSliderItem(contentTrans, "Rope Damping", 0.0f, 100f, out ropeDampingSlider, out _, uiRes);

        Slider thetaInitSlider, phiInitSlider, initialVelocitySlider;
        Text thetaInitLabel, phiInitLabel;
        CreateSliderItem(contentTrans, "Initial Theta", 0f, 90f, out thetaInitSlider, out thetaInitLabel, uiRes);
        CreateSliderItem(contentTrans, "Initial Phi", 0f, 360f, out phiInitSlider, out phiInitLabel, uiRes);
        CreateSliderItem(contentTrans, "Initial Velocity", 0f, 10f, out initialVelocitySlider, out _, uiRes);

        Slider gravitySlider, humiditySlider, viscositySlider;
        Text gravityLabel, humidityLabel, viscosityLabel;
        CreateSliderItem(contentTrans, "Gravity", 0.1f, 30f, out gravitySlider, out gravityLabel, uiRes);
        CreateSliderItem(contentTrans, "Humidity", 0f, 1f, out humiditySlider, out humidityLabel, uiRes);
        CreateSliderItem(contentTrans, "Viscosity", 0.01f, 10f, out viscositySlider, out viscosityLabel, uiRes);

        GameObject windToggleObj = DefaultControls.CreateToggle(uiRes);
        windToggleObj.transform.SetParent(contentTrans, false);
        Toggle windToggle = windToggleObj.GetComponent<Toggle>();
        Text windToggleText = windToggleObj.GetComponentInChildren<Text>();
        windToggleText.text = "Wind Enabled";
        windToggleText.color = new Color(0.85f, 0.88f, 0.95f);
        windToggleText.fontStyle = FontStyle.Bold;

        Slider canvasWidthSlider, canvasHeightSlider, inclinationSlider;
        CreateSliderItem(contentTrans, "Canvas Width", 0.5f, 10f, out canvasWidthSlider, out _, uiRes);
        CreateSliderItem(contentTrans, "Canvas Height", 0.5f, 10f, out canvasHeightSlider, out _, uiRes);
        CreateSliderItem(contentTrans, "Canvas Inclination", 0f, 90f, out inclinationSlider, out _, uiRes);

        // Dropdown for surface type
        GameObject ddObj = DefaultControls.CreateDropdown(uiRes);
        ddObj.transform.SetParent(contentTrans, false);
        Dropdown surfaceTypeDropdown = ddObj.GetComponent<Dropdown>();
        surfaceTypeDropdown.options.Clear();
        surfaceTypeDropdown.options.Add(new Dropdown.OptionData("Fabric"));
        surfaceTypeDropdown.options.Add(new Dropdown.OptionData("Wood"));
        surfaceTypeDropdown.options.Add(new Dropdown.OptionData("Metal"));
        surfaceTypeDropdown.options.Add(new Dropdown.OptionData("Paper"));

        // Right sidebar (physics display panel)
        GameObject physicsDisplayPanelObj = DefaultControls.CreatePanel(uiRes);
        physicsDisplayPanelObj.name = "PhysicsDisplayPanel";
        physicsDisplayPanelObj.transform.SetParent(simulationPanel.transform, false);
        RectTransform physPanelRect = physicsDisplayPanelObj.GetComponent<RectTransform>();
        physPanelRect.anchorMin = new Vector2(1, 0);
        physPanelRect.anchorMax = new Vector2(1, 1);
        physPanelRect.pivot = new Vector2(1, 0.5f);
        physPanelRect.anchoredPosition = Vector2.zero;
        physPanelRect.sizeDelta = new Vector2(300, 0);
        physicsDisplayPanelObj.GetComponent<Image>().color = sidebarBgColor;

        // Vertical Layout Group for Physics Display
        GameObject physContainer = new GameObject("Container", typeof(RectTransform), typeof(VerticalLayoutGroup));
        physContainer.transform.SetParent(physicsDisplayPanelObj.transform, false);
        SetRectTransformFullStretch(physContainer.GetComponent<RectTransform>());
        physContainer.GetComponent<RectTransform>().offsetMin = new Vector2(15, 15);
        physContainer.GetComponent<RectTransform>().offsetMax = new Vector2(-15, -15);
        VerticalLayoutGroup vlgPhys = physContainer.GetComponent<VerticalLayoutGroup>();
        vlgPhys.spacing = 10;
        vlgPhys.childControlHeight = true;
        vlgPhys.childControlWidth = true;
        vlgPhys.childForceExpandHeight = false;
        vlgPhys.childForceExpandWidth = true;

        Text telemetryHeader = CreateTextItem(physContainer.transform, "TELEMETRY", uiRes);
        telemetryHeader.fontStyle = FontStyle.Bold;
        telemetryHeader.fontSize = 16;
        telemetryHeader.alignment = TextAnchor.MiddleCenter;
        telemetryHeader.color = new Color(0.2f, 0.65f, 1f, 1f);

        Text thetaDisplay = CreateTextItem(physContainer.transform, "θ: 0.00°", uiRes);
        Text phiDisplay = CreateTextItem(physContainer.transform, "ϕ: 0.00°", uiRes);
        Text velocityDisplay = CreateTextItem(physContainer.transform, "v: 0.000 m/s", uiRes);
        Text paintRemainingDisplay = CreateTextItem(physContainer.transform, "Paint: 0.0000 kg", uiRes);
        Text flowRateDisplay = CreateTextItem(physContainer.transform, "Flow: 0.00 mL/s", uiRes);
        Text particleCountDisplay = CreateTextItem(physContainer.transform, "Particles: 0", uiRes);
        Text fpsDisplay = CreateTextItem(physContainer.transform, "FPS: 0", uiRes);
        Text swingCountDisplay = CreateTextItem(physContainer.transform, "Swings: 0", uiRes);
        Text elapsedTimeDisplay = CreateTextItem(physContainer.transform, "Time: 0.00s", uiRes);

        // --- Camera Control Section ---
        Text camHeader = CreateTextItem(physContainer.transform, "\nCAMERA CONTROLS", uiRes);
        camHeader.fontStyle = FontStyle.Bold;
        camHeader.fontSize = 16;
        camHeader.alignment = TextAnchor.MiddleCenter;
        camHeader.color = new Color(0.2f, 0.65f, 1f, 1f);

        Text camGuideText = CreateTextItem(physContainer.transform, 
            "• Orbit: Right-Click + Drag\n" +
            "• Zoom: Scroll Mouse Wheel\n" +
            "• Pan: Middle-Click + Drag\n" +
            "• Focus Bucket: Press [F] Key", uiRes);
        camGuideText.fontSize = 12;
        camGuideText.lineSpacing = 1.2f;
        camGuideText.color = new Color(0.75f, 0.78f, 0.85f);

        // Grid for camera quick-preset buttons
        GameObject camButtonsGrid = new GameObject("CameraButtonsGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        camButtonsGrid.transform.SetParent(physContainer.transform, false);
        GridLayoutGroup grid = camButtonsGrid.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(120, 32);
        grid.spacing = new Vector2(10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.MiddleCenter;

        ContentSizeFitter gridFitter = camButtonsGrid.AddComponent<ContentSizeFitter>();
        gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject topViewBtn = DefaultControls.CreateButton(uiRes);
        topViewBtn.name = "TopViewButton";
        topViewBtn.transform.SetParent(camButtonsGrid.transform, false);
        topViewBtn.GetComponentInChildren<Text>().text = "Top (1)";
        StyleButton(topViewBtn, neutralBtnColor, Color.white, 12);

        GameObject sideViewBtn = DefaultControls.CreateButton(uiRes);
        sideViewBtn.name = "SideViewButton";
        sideViewBtn.transform.SetParent(camButtonsGrid.transform, false);
        sideViewBtn.GetComponentInChildren<Text>().text = "Side (2)";
        StyleButton(sideViewBtn, neutralBtnColor, Color.white, 12);

        GameObject frontViewBtn = DefaultControls.CreateButton(uiRes);
        frontViewBtn.name = "FrontViewButton";
        frontViewBtn.transform.SetParent(camButtonsGrid.transform, false);
        frontViewBtn.GetComponentInChildren<Text>().text = "Front (3)";
        StyleButton(frontViewBtn, neutralBtnColor, Color.white, 12);

        GameObject freeViewBtn = DefaultControls.CreateButton(uiRes);
        freeViewBtn.name = "FreeViewButton";
        freeViewBtn.transform.SetParent(camButtonsGrid.transform, false);
        freeViewBtn.GetComponentInChildren<Text>().text = "Free (4)";
        StyleButton(freeViewBtn, neutralBtnColor, Color.white, 12);

        GameObject focusBtn = DefaultControls.CreateButton(uiRes);
        focusBtn.name = "FocusButton";
        focusBtn.transform.SetParent(camButtonsGrid.transform, false);
        focusBtn.GetComponentInChildren<Text>().text = "Focus (F)";
        StyleButton(focusBtn, primaryBtnColor, Color.white, 12);

        // Bottom bar (controls)
        GameObject controlBarObj = DefaultControls.CreatePanel(uiRes);
        controlBarObj.name = "ControlBar";
        controlBarObj.transform.SetParent(simulationPanel.transform, false);
        RectTransform ctrlBarRect = controlBarObj.GetComponent<RectTransform>();
        ctrlBarRect.anchorMin = new Vector2(0.2f, 0);
        ctrlBarRect.anchorMax = new Vector2(0.8f, 0);
        ctrlBarRect.pivot = new Vector2(0.5f, 0);
        ctrlBarRect.anchoredPosition = new Vector2(0, 15);
        ctrlBarRect.sizeDelta = new Vector2(0, 80);
        controlBarObj.GetComponent<Image>().color = controlBarBgColor;

        // Horizontal Layout Group inside control bar
        HorizontalLayoutGroup hlg = controlBarObj.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(10, 10, 10, 10);
        hlg.spacing = 15;
        hlg.childControlHeight = true;
        hlg.childControlWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = true;

        GameObject pauseButtonObj = DefaultControls.CreateButton(uiRes);
        pauseButtonObj.name = "PauseButton";
        pauseButtonObj.transform.SetParent(controlBarObj.transform, false);
        pauseButtonObj.GetComponentInChildren<Text>().text = "Pause";
        StyleButton(pauseButtonObj, warningBtnColor, Color.white, 16);

        GameObject resumeButtonObj = DefaultControls.CreateButton(uiRes);
        resumeButtonObj.name = "ResumeButton";
        resumeButtonObj.transform.SetParent(controlBarObj.transform, false);
        resumeButtonObj.GetComponentInChildren<Text>().text = "Resume";
        StyleButton(resumeButtonObj, successBtnColor, Color.white, 16);
        resumeButtonObj.SetActive(false); // starts hidden

        GameObject resetButtonObj = DefaultControls.CreateButton(uiRes);
        resetButtonObj.name = "ResetButton";
        resetButtonObj.transform.SetParent(controlBarObj.transform, false);
        resetButtonObj.GetComponentInChildren<Text>().text = "Reset";
        StyleButton(resetButtonObj, dangerBtnColor, Color.white, 16);

        // Speed Slider item in control bar
        GameObject speedSliderContainer = new GameObject("SpeedSliderItem", typeof(RectTransform), typeof(VerticalLayoutGroup));
        speedSliderContainer.transform.SetParent(controlBarObj.transform, false);
        VerticalLayoutGroup vlgSpeed = speedSliderContainer.GetComponent<VerticalLayoutGroup>();
        vlgSpeed.childForceExpandWidth = true;
        vlgSpeed.childForceExpandHeight = false;

        Text speedLabel = CreateTextItem(speedSliderContainer.transform, "Speed: 1.0x", uiRes);
        speedLabel.alignment = TextAnchor.MiddleCenter;
        speedLabel.color = new Color(0.2f, 0.65f, 1f, 1f);
        speedLabel.fontStyle = FontStyle.Bold;

        GameObject speedSliderObj = DefaultControls.CreateSlider(uiRes);
        speedSliderObj.transform.SetParent(speedSliderContainer.transform, false);
        Slider speedSlider = speedSliderObj.GetComponent<Slider>();

        // Results Panel
        GameObject resultsPanel = DefaultControls.CreatePanel(uiRes);
        resultsPanel.name = "ResultsPanel";
        resultsPanel.transform.SetParent(canvasUIObj.transform, false);
        SetRectTransformFullStretch(resultsPanel.GetComponent<RectTransform>());
        resultsPanel.GetComponent<Image>().color = panelBgColor;
        resultsPanel.SetActive(false);

        GameObject rawImgObj = DefaultControls.CreateRawImage(uiRes);
        rawImgObj.name = "CanvasPreview";
        rawImgObj.transform.SetParent(resultsPanel.transform, false);
        RawImage canvasPreview = rawImgObj.GetComponent<RawImage>();
        SetRectTransformAnchor(rawImgObj.GetComponent<RectTransform>(), 0.5f, 0.5f, 500, 500);

        GameObject resultsTextObj = DefaultControls.CreateText(uiRes);
        resultsTextObj.name = "ResultsText";
        resultsTextObj.transform.SetParent(resultsPanel.transform, false);
        Text resultsText = resultsTextObj.GetComponent<Text>();
        resultsText.text = "Experiment results detailed here...";
        resultsText.fontSize = 20;
        resultsText.color = Color.white;
        resultsText.alignment = TextAnchor.UpperLeft;
        SetRectTransformAnchor(resultsTextObj.GetComponent<RectTransform>(), 0.15f, 0.5f, 400, 500);

        // Buttons for results
        GameObject resultsButtonContainer = new GameObject("ButtonContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        resultsButtonContainer.transform.SetParent(resultsPanel.transform, false);
        SetRectTransformAnchor(resultsButtonContainer.GetComponent<RectTransform>(), 0.5f, 0.15f, 800, 60);
        HorizontalLayoutGroup hlgResults = resultsButtonContainer.GetComponent<HorizontalLayoutGroup>();
        hlgResults.spacing = 15;
        hlgResults.childControlHeight = true;
        hlgResults.childControlWidth = true;
        hlgResults.childForceExpandHeight = true;
        hlgResults.childForceExpandWidth = true;

        GameObject saveImgBtnObj = DefaultControls.CreateButton(uiRes);
        saveImgBtnObj.name = "SaveImageButton";
        saveImgBtnObj.transform.SetParent(resultsButtonContainer.transform, false);
        saveImgBtnObj.GetComponentInChildren<Text>().text = "Save PNG Canvas";
        StyleButton(saveImgBtnObj, neutralBtnColor, Color.white, 14);

        GameObject exportCsvBtnObj = DefaultControls.CreateButton(uiRes);
        exportCsvBtnObj.name = "ExportCSVButton";
        exportCsvBtnObj.transform.SetParent(resultsButtonContainer.transform, false);
        exportCsvBtnObj.GetComponentInChildren<Text>().text = "Export CSV Data";
        StyleButton(exportCsvBtnObj, neutralBtnColor, Color.white, 14);

        GameObject exportJsonBtnObj = DefaultControls.CreateButton(uiRes);
        exportJsonBtnObj.name = "ExportJSONButton";
        exportJsonBtnObj.transform.SetParent(resultsButtonContainer.transform, false);
        exportJsonBtnObj.GetComponentInChildren<Text>().text = "Export JSON Data";
        StyleButton(exportJsonBtnObj, neutralBtnColor, Color.white, 14);

        GameObject newExpBtnObj = DefaultControls.CreateButton(uiRes);
        newExpBtnObj.name = "NewExperimentButton";
        newExpBtnObj.transform.SetParent(resultsButtonContainer.transform, false);
        newExpBtnObj.GetComponentInChildren<Text>().text = "New Experiment";
        StyleButton(newExpBtnObj, primaryBtnColor, Color.white, 14);

        // Comparison Panel (Minimal)
        GameObject comparisonPanel = DefaultControls.CreatePanel(uiRes);
        comparisonPanel.name = "ComparisonPanel";
        comparisonPanel.transform.SetParent(canvasUIObj.transform, false);
        SetRectTransformFullStretch(comparisonPanel.GetComponent<RectTransform>());
        comparisonPanel.GetComponent<Image>().color = panelBgColor;
        comparisonPanel.SetActive(false);

        // 7. Load Compute Shaders
        ComputeShader sphCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ComputeShaders/SPHCompute.compute");
        ComputeShader particleCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ComputeShaders/ParticleUpdate.compute");
        ComputeShader canvasCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ComputeShaders/CanvasBlend.compute");
        
        // 8. Create or Load Default SimulationConfig
        SimulationConfig config = AssetDatabase.LoadAssetAtPath<SimulationConfig>("Assets/ScriptableObjects/SimulationPresets/DefaultSimConfig.asset");
        if (config == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/SimulationPresets"))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "SimulationPresets");
                
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            AssetDatabase.CreateAsset(config, "Assets/ScriptableObjects/SimulationPresets/DefaultSimConfig.asset");
        }

        // 9. Assign references via SerializedObject
        SerializedObject soSim = new SerializedObject(simManager);
        soSim.FindProperty("_config").objectReferenceValue = config;
        
        if (sphCompute != null) soSim.FindProperty("_sphComputeShader").objectReferenceValue = sphCompute;
        if (particleCompute != null) soSim.FindProperty("_particleUpdateComputeShader").objectReferenceValue = particleCompute;
        if (canvasCompute != null) soSim.FindProperty("_canvasBlendComputeShader").objectReferenceValue = canvasCompute;
        
        soSim.FindProperty("_particleRenderer").objectReferenceValue = pRenderer;
        soSim.FindProperty("_bucketRenderer").objectReferenceValue = bRenderer;
        soSim.FindProperty("_ropeRenderer").objectReferenceValue = rRenderer;
        soSim.FindProperty("_canvasPaintRenderer").objectReferenceValue = cpRenderer;
        soSim.ApplyModifiedProperties();

        // UIManager settings
        SerializedObject soUI = new SerializedObject(uiManager);
        soUI.FindProperty("_simulationManager").objectReferenceValue = simManager;
        
        soUI.FindProperty("_mainMenuPanel").objectReferenceValue = mainMenuPanel;
        soUI.FindProperty("_simulationPanel").objectReferenceValue = simulationPanel;
        soUI.FindProperty("_parameterPanel").objectReferenceValue = parameterPanelObj;
        soUI.FindProperty("_physicsDisplayPanel").objectReferenceValue = physicsDisplayPanelObj;
        soUI.FindProperty("_resultsPanel").objectReferenceValue = resultsPanel;
        soUI.FindProperty("_comparisonPanel").objectReferenceValue = comparisonPanel;

        soUI.FindProperty("_startButton").objectReferenceValue = startButtonObj.GetComponent<Button>();
        soUI.FindProperty("_pauseButton").objectReferenceValue = pauseButtonObj.GetComponent<Button>();
        soUI.FindProperty("_resumeButton").objectReferenceValue = resumeButtonObj.GetComponent<Button>();
        soUI.FindProperty("_resetButton").objectReferenceValue = resetButtonObj.GetComponent<Button>();
        soUI.FindProperty("_speedSlider").objectReferenceValue = speedSlider;
        soUI.FindProperty("_speedLabel").objectReferenceValue = speedLabel;

        soUI.FindProperty("_thetaDisplay").objectReferenceValue = thetaDisplay;
        soUI.FindProperty("_phiDisplay").objectReferenceValue = phiDisplay;
        soUI.FindProperty("_velocityDisplay").objectReferenceValue = velocityDisplay;
        soUI.FindProperty("_paintRemainingDisplay").objectReferenceValue = paintRemainingDisplay;
        soUI.FindProperty("_flowRateDisplay").objectReferenceValue = flowRateDisplay;
        soUI.FindProperty("_particleCountDisplay").objectReferenceValue = particleCountDisplay;
        soUI.FindProperty("_fpsDisplay").objectReferenceValue = fpsDisplay;
        soUI.FindProperty("_swingCountDisplay").objectReferenceValue = swingCountDisplay;
        soUI.FindProperty("_elapsedTimeDisplay").objectReferenceValue = elapsedTimeDisplay;

        soUI.FindProperty("_bucketMassSlider").objectReferenceValue = bucketMassSlider;
        soUI.FindProperty("_bucketRadiusSlider").objectReferenceValue = bucketRadiusSlider;
        soUI.FindProperty("_orificeRadiusSlider").objectReferenceValue = orificeRadiusSlider;
        soUI.FindProperty("_paintVolumeSlider").objectReferenceValue = paintVolumeSlider;
        soUI.FindProperty("_bucketMassLabel").objectReferenceValue = bucketMassLabel;
        soUI.FindProperty("_bucketRadiusLabel").objectReferenceValue = bucketRadiusLabel;
        soUI.FindProperty("_orificeRadiusLabel").objectReferenceValue = orificeRadiusLabel;
        soUI.FindProperty("_paintVolumeLabel").objectReferenceValue = paintVolumeLabel;

        soUI.FindProperty("_ropeLengthSlider").objectReferenceValue = ropeLengthSlider;
        soUI.FindProperty("_ropeElasticToggle").objectReferenceValue = ropeElasticToggle;
        soUI.FindProperty("_ropeDampingSlider").objectReferenceValue = ropeDampingSlider;
        soUI.FindProperty("_ropeLengthLabel").objectReferenceValue = ropeLengthLabel;

        soUI.FindProperty("_thetaInitSlider").objectReferenceValue = thetaInitSlider;
        soUI.FindProperty("_phiInitSlider").objectReferenceValue = phiInitSlider;
        soUI.FindProperty("_initialVelocitySlider").objectReferenceValue = initialVelocitySlider;
        soUI.FindProperty("_thetaInitLabel").objectReferenceValue = thetaInitLabel;
        soUI.FindProperty("_phiInitLabel").objectReferenceValue = phiInitLabel;

        soUI.FindProperty("_gravitySlider").objectReferenceValue = gravitySlider;
        soUI.FindProperty("_humiditySlider").objectReferenceValue = humiditySlider;
        soUI.FindProperty("_windToggle").objectReferenceValue = windToggle;
        soUI.FindProperty("_gravityLabel").objectReferenceValue = gravityLabel;
        soUI.FindProperty("_humidityLabel").objectReferenceValue = humidityLabel;

        soUI.FindProperty("_viscositySlider").objectReferenceValue = viscositySlider;
        soUI.FindProperty("_viscosityLabel").objectReferenceValue = viscosityLabel;

        soUI.FindProperty("_canvasWidthSlider").objectReferenceValue = canvasWidthSlider;
        soUI.FindProperty("_canvasHeightSlider").objectReferenceValue = canvasHeightSlider;
        soUI.FindProperty("_surfaceTypeDropdown").objectReferenceValue = surfaceTypeDropdown;
        soUI.FindProperty("_inclinationSlider").objectReferenceValue = inclinationSlider;

        soUI.FindProperty("_resultsText").objectReferenceValue = resultsText;
        soUI.FindProperty("_canvasPreview").objectReferenceValue = canvasPreview;
        soUI.FindProperty("_saveImageButton").objectReferenceValue = saveImgBtnObj.GetComponent<Button>();
        soUI.FindProperty("_exportCSVButton").objectReferenceValue = exportCsvBtnObj.GetComponent<Button>();
        soUI.FindProperty("_exportJSONButton").objectReferenceValue = exportJsonBtnObj.GetComponent<Button>();
        soUI.FindProperty("_newExperimentButton").objectReferenceValue = newExpBtnObj.GetComponent<Button>();

        // Camera control button serialization
        soUI.FindProperty("_topViewButton").objectReferenceValue = topViewBtn.GetComponent<Button>();
        soUI.FindProperty("_sideViewButton").objectReferenceValue = sideViewBtn.GetComponent<Button>();
        soUI.FindProperty("_frontViewButton").objectReferenceValue = frontViewBtn.GetComponent<Button>();
        soUI.FindProperty("_freeViewButton").objectReferenceValue = freeViewBtn.GetComponent<Button>();
        soUI.FindProperty("_focusButton").objectReferenceValue = focusBtn.GetComponent<Button>();

        soUI.ApplyModifiedProperties();

        // Setup Main Camera Transform
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(0, 6, -8);
            mainCam.transform.rotation = Quaternion.Euler(30, 0, 0);
            
            // Add CameraController if missing
            if (mainCam.GetComponent<CameraController>() == null)
            {
                CameraController cc = mainCam.gameObject.AddComponent<CameraController>();
                SerializedObject soCam = new SerializedObject(cc);
                soCam.FindProperty("_target").objectReferenceValue = bucketParent.transform;
                soCam.ApplyModifiedProperties();
            }
        }

        // Setup Directional Light Transform
        Light dirLight = FindObjectOfType<Light>();
        if (dirLight != null && dirLight.type == LightType.Directional)
        {
            dirLight.transform.rotation = Quaternion.Euler(50, -30, 0);
            dirLight.color = new Color(1, 0.95f, 0.9f);
            dirLight.intensity = 1.2f;
        }

        // 10. Save the Scene
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
            
        string scenePath = "Assets/Scenes/MainScene.unity";
        EditorSceneManager.SaveScene(newScene, scenePath);
        
        Debug.Log($"[AutoSetup] Full 3D & UI Simulation Scene successfully generated and saved to {scenePath}.");
        EditorUtility.DisplayDialog("Setup Complete", "MainScene has been generated successfully with all 3D Objects & UI. Please open it from Assets/Scenes/MainScene.unity and press Play!", "OK");
    }

    private static void SetRectTransformFullStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    private static void SetRectTransformAnchor(RectTransform rt, float xAnchor, float yAnchor, float width, float height)
    {
        rt.anchorMin = new Vector2(xAnchor, yAnchor);
        rt.anchorMax = new Vector2(xAnchor, yAnchor);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(width, height);
    }

    private static void StyleButton(GameObject buttonObj, Color normalColor, Color textColor, int fontSize = 14)
    {
        Button btn = buttonObj.GetComponent<Button>();
        Image img = buttonObj.GetComponent<Image>();
        if (img != null)
        {
            img.color = normalColor;
        }
        
        if (btn != null)
        {
            ColorBlock cb = btn.colors;
            cb.normalColor = normalColor;
            cb.highlightedColor = normalColor * 1.15f;
            cb.pressedColor = normalColor * 0.85f;
            cb.selectedColor = normalColor;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
        }

        Text txt = buttonObj.GetComponentInChildren<Text>();
        if (txt != null)
        {
            txt.color = textColor;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
        }
    }

    private static void CreateSliderItem(Transform parent, string name, float min, float max, out Slider slider, out Text valLabel, DefaultControls.Resources uiRes)
    {
        GameObject container = new GameObject(name + "_Item", typeof(RectTransform), typeof(VerticalLayoutGroup));
        container.transform.SetParent(parent, false);
        VerticalLayoutGroup vlg = container.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 2;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Label layout
        GameObject row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(container.transform, false);
        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false;
        
        Text titleText = CreateTextItem(row.transform, name + ": ", uiRes);
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.85f, 0.88f, 0.95f);
        
        valLabel = CreateTextItem(row.transform, "0.0", uiRes);
        valLabel.color = new Color(0.2f, 0.65f, 1f, 1f); // Electric Cyan for values

        GameObject sliderObj = DefaultControls.CreateSlider(uiRes);
        sliderObj.transform.SetParent(container.transform, false);
        slider = sliderObj.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
    }

    private static Text CreateTextItem(Transform parent, string content, DefaultControls.Resources uiRes)
    {
        GameObject txtObj = DefaultControls.CreateText(uiRes);
        txtObj.transform.SetParent(parent, false);
        Text txt = txtObj.GetComponent<Text>();
        txt.text = content;
        txt.fontSize = 14;
        txt.color = Color.white;
        return txt;
    }

    private static void MakeMaterialTransparent(Material mat, Color baseColor, float alpha)
    {
        mat.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        
        // 1. For Universal Render Pipeline/Lit
        if (mat.shader.name.Contains("Universal Render Pipeline") || mat.shader.name.Contains("URP"))
        {
            mat.SetFloat("_Surface", 1); // 1 = Transparent
            mat.SetFloat("_Blend", 0); // 0 = Alpha blend
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
        else // 2. For Standard Shader
        {
            mat.SetFloat("_Mode", 3f); // 3 = Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
    }
}
