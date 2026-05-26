using ArtNotes.UndergroundLaboratoryGenerator;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialSceneController : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public string mainMenuSceneName = "LobbyScene";

    [Header("Tutorial Timing")]
    [SerializeField] private float tutorialComputerHoldDuration = 1.35f;
    [SerializeField] private float blendInSeconds = 2f;
    [SerializeField] private float alibiSeconds = 2f;

    [Header("In-Game Prefabs")]
    [SerializeField] private Cell commonRoomPrefab;
    [SerializeField] private Cell citizenRoomPrefab;
    [SerializeField] private Cell doppelgangerRoomPrefab;
    [SerializeField] private Cell corridorPrefab;
    [SerializeField] private GameObject doorPrefab;
    [SerializeField] private GameObject computerPrefab;
    [SerializeField] private GameObject tablePrefab;
    [SerializeField] private GameObject cameraPickupPrefab;
    [SerializeField] private GameObject dotSourcePrefab;

    private enum TutorialStage
    {
        Common,
        Citizen,
        Doppelganger,
        Complete
    }

    private enum CommonStep
    {
        Move,
        Scan,
        PickUpItem,
        UseItem,
        CheckComputer,
        OpenExit
    }

    private enum CitizenStep
    {
        FindFirstTarget,
        CheckWrongComputer,
        RepairSabotagedComputer,
        FindSecondTarget,
        Escape
    }

    private enum DoppelgangerStep
    {
        BlendIn,
        KillDuringKillTime,
        SabotageComputer,
        AvoidSuspicion
    }

    private Transform worldRoot;
    private GameObject playerObject;
    private PlayerCombatTarget playerTarget;
    private PlayerInventory playerInventory;
    private InstancedScanDotRenderer dotRenderer;
    private LabObjectiveManager labObjectiveManager;

    private ObjectiveComputer commonComputer;
    private ObjectiveComputer citizenTargetA;
    private ObjectiveComputer citizenTargetB;
    private ObjectiveComputer citizenWrongComputer;
    private ObjectiveComputer doppelgangerComputer;
    private TutorialGate commonGate;
    private TutorialGate citizenGate;
    private WorldItemPickup cameraPickup;
    private Transform blendInStation;
    private Transform alibiStation;
    private PlayerCombatTarget dummyCitizen;

    private TutorialStage currentStage;
    private int currentStep;
    private Vector3 stepStartPosition;
    private int stepStartDotCount;
    private int stepStartCameraAmount;
    private bool citizenSabotageIssued;
    private float blendTimer;
    private float alibiTimer;
    private bool tutorialFinished;
    private Vector3 commonRoomCenter = new Vector3(0f, 0f, 0f);
    private Vector3 citizenRoomCenter = new Vector3(0f, 0f, 16f);
    private Vector3 doppelgangerRoomCenter = new Vector3(0f, 0f, 32f);

    private Canvas overlayCanvas;
    private RectTransform finishPanel;
    private TMP_Text titleText;
    private TMP_Text bodyText;
    private TMP_Text stepText;
    private TMP_Text roleText;
    private TMP_Text objectiveText;
    private TMP_Text promptText;
    private Sprite whiteSprite;

    private Material floorMaterial;
    private Material wallMaterial;
    private Material metalMaterial;
    private Material computerMaterial;
    private Material itemMaterial;
    private Material gateMaterial;
    private Material markerMaterial;
    private Material dummyMaterial;

    private void Start()
    {
        DestroySceneCamera();
        whiteSprite = CreateSolidSprite();

        UiEventSystemUtility.EnsureSingle(gameObject);
        ConfigureSceneLighting();
        PrepareMaterials();
        BuildWorld();
        BuildOverlayUi();
        BuildManagers();
        BuildPlayer();
        BuildTrainingObjects();
        ConfigureRoundFlow();
        EnterCommonStage();
        LockGameplayCursor();
    }

    private void Update()
    {
        if (Input.GetKeyDown(GameInputBindings.Pause))
        {
            OnClickBackToMenu();
            return;
        }

        if (tutorialFinished)
        {
            return;
        }

        UpdateTutorialProgress();
        RefreshOverlayText();
    }

    public void OnClickBackToMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
        }
    }

    public void NotifyGateOpened(TutorialGate gate)
    {
        if (gate == commonGate && currentStage == TutorialStage.Common && currentStep == (int)CommonStep.OpenExit)
        {
            EnterCitizenStage();
            return;
        }

        if (gate == citizenGate && currentStage == TutorialStage.Citizen && currentStep == (int)CitizenStep.Escape)
        {
            EnterDoppelgangerStage();
        }
    }

    private void DestroySceneCamera()
    {
        Camera existingCamera = Camera.main;
        if (existingCamera != null)
        {
            Destroy(existingCamera.gameObject);
        }
    }

    private void ConfigureSceneLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.035f, 0.042f, 0.048f, 1f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.01f, 0.012f, 0.014f, 1f);
        RenderSettings.fogDensity = 0.018f;
    }

    private void PrepareMaterials()
    {
        floorMaterial = CreateMaterial("Tutorial_Floor", new Color(0.16f, 0.18f, 0.18f, 1f));
        wallMaterial = CreateMaterial("Tutorial_Wall", new Color(0.24f, 0.27f, 0.29f, 1f));
        metalMaterial = CreateMaterial("Tutorial_Metal", new Color(0.32f, 0.38f, 0.41f, 1f));
        computerMaterial = CreateMaterial("Tutorial_Computer", new Color(0.18f, 0.30f, 0.34f, 1f));
        itemMaterial = CreateMaterial("Tutorial_Item", new Color(0.56f, 0.57f, 0.54f, 1f));
        gateMaterial = CreateMaterial("Tutorial_Gate", new Color(0.72f, 0.48f, 0.16f, 1f));
        markerMaterial = CreateMaterial("Tutorial_Marker", new Color(0.16f, 0.65f, 0.75f, 1f));
        dummyMaterial = CreateMaterial("Tutorial_DummyCitizen", new Color(0.55f, 0.78f, 0.88f, 1f));
    }

    private Material CreateMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.name = materialName;
        SetMaterialColor(material, color);
        return material;
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, color);
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, color);
        }
    }

    private void BuildWorld()
    {
        worldRoot = new GameObject("TutorialHandmadeMap").transform;

        if (TryBuildInGamePrefabMap())
        {
            CreateRoomLight("CommonTrainingLight", commonRoomCenter + new Vector3(0f, 2.7f, 0f));
            CreateRoomLight("CitizenPracticeLight", citizenRoomCenter + new Vector3(0f, 2.7f, 0f));
            CreateRoomLight("DoppelgangerPracticeLight", doppelgangerRoomCenter + new Vector3(0f, 2.7f, 0f));
            return;
        }

        CreateCube("MainFloor", new Vector3(0f, -0.05f, 16f), new Vector3(14f, 0.1f, 48f), floorMaterial, ScanSurfaceType.Floor);
        CreateCube("WestWall", new Vector3(-7f, 1.5f, 16f), new Vector3(0.35f, 3f, 48f), wallMaterial, ScanSurfaceType.Wall);
        CreateCube("EastWall", new Vector3(7f, 1.5f, 16f), new Vector3(0.35f, 3f, 48f), wallMaterial, ScanSurfaceType.Wall);
        CreateCube("SouthWall", new Vector3(0f, 1.5f, -8f), new Vector3(14f, 3f, 0.35f), wallMaterial, ScanSurfaceType.Wall);
        CreateCube("NorthWall", new Vector3(0f, 1.5f, 40f), new Vector3(14f, 3f, 0.35f), wallMaterial, ScanSurfaceType.Wall);

        CreateDividerWall(8f);
        CreateDividerWall(24f);

        commonGate = CreateGate("CommonToCitizenGate", new Vector3(0f, 1.5f, 8f), "common");
        citizenGate = CreateGate("CitizenToDoppelgangerGate", new Vector3(0f, 1.5f, 24f), "citizen");

        commonRoomCenter = new Vector3(0f, 0f, 0f);
        citizenRoomCenter = new Vector3(0f, 0f, 16f);
        doppelgangerRoomCenter = new Vector3(0f, 0f, 32f);
        CreateRoomLight("CommonTrainingLight", new Vector3(0f, 2.7f, -1f));
        CreateRoomLight("CitizenPracticeLight", new Vector3(0f, 2.7f, 16f));
        CreateRoomLight("DoppelgangerPracticeLight", new Vector3(0f, 2.7f, 32f));
    }

    private bool TryBuildInGamePrefabMap()
    {
        if (commonRoomPrefab == null || citizenRoomPrefab == null || doppelgangerRoomPrefab == null || corridorPrefab == null)
        {
            return false;
        }

        Cell commonRoom = SpawnCell(commonRoomPrefab, "Tutorial_CommonRoom", Vector3.zero, Quaternion.identity);
        Transform commonNorthExit = FindExitByLocalDirection(commonRoom, Vector3.forward);

        Cell firstCorridor = SpawnCell(corridorPrefab, "Tutorial_Corridor_CommonToCitizen", Vector3.zero, Quaternion.identity);
        Transform firstCorridorSouthExit = FindExitByLocalDirection(firstCorridor, Vector3.back);
        AlignCellToExit(firstCorridor, firstCorridorSouthExit, commonNorthExit);
        Transform firstCorridorNorthExit = FindExitByLocalDirection(firstCorridor, Vector3.forward);

        Cell citizenRoom = SpawnCell(citizenRoomPrefab, "Tutorial_CitizenRoom", Vector3.zero, Quaternion.identity);
        Transform citizenSouthExit = FindExitByLocalDirection(citizenRoom, Vector3.back);
        AlignCellToExit(citizenRoom, citizenSouthExit, firstCorridorNorthExit);
        Transform citizenNorthExit = FindExitByLocalDirection(citizenRoom, Vector3.forward);

        Cell secondCorridor = SpawnCell(corridorPrefab, "Tutorial_Corridor_CitizenToDoppelganger", Vector3.zero, Quaternion.identity);
        Transform secondCorridorSouthExit = FindExitByLocalDirection(secondCorridor, Vector3.back);
        AlignCellToExit(secondCorridor, secondCorridorSouthExit, citizenNorthExit);
        Transform secondCorridorNorthExit = FindExitByLocalDirection(secondCorridor, Vector3.forward);

        Cell doppelgangerRoom = SpawnCell(doppelgangerRoomPrefab, "Tutorial_DoppelgangerRoom", Vector3.zero, Quaternion.identity);
        Transform doppelgangerSouthExit = FindExitByLocalDirection(doppelgangerRoom, Vector3.back);
        AlignCellToExit(doppelgangerRoom, doppelgangerSouthExit, secondCorridorNorthExit);

        commonGate = CreateGateAtExit("CommonToCitizenGate", commonNorthExit, "common");
        citizenGate = CreateGateAtExit("CitizenToDoppelgangerGate", citizenNorthExit, "citizen");

        Transform[] connectedExits =
        {
            commonNorthExit,
            firstCorridorSouthExit,
            firstCorridorNorthExit,
            citizenSouthExit,
            citizenNorthExit,
            secondCorridorSouthExit,
            secondCorridorNorthExit,
            doppelgangerSouthExit
        };

        BlockUnusedExits(commonRoom, connectedExits);
        BlockUnusedExits(firstCorridor, connectedExits);
        BlockUnusedExits(citizenRoom, connectedExits);
        BlockUnusedExits(secondCorridor, connectedExits);
        BlockUnusedExits(doppelgangerRoom, connectedExits);

        commonRoomCenter = GetCellCenter(commonRoom);
        citizenRoomCenter = GetCellCenter(citizenRoom);
        doppelgangerRoomCenter = GetCellCenter(doppelgangerRoom);
        return true;
    }

    private Cell SpawnCell(Cell prefab, string name, Vector3 position, Quaternion rotation)
    {
        Cell cell = Instantiate(prefab, position, rotation, worldRoot);
        cell.name = name;
        cell.CacheTriggerBox();
        HideCellHelperObjects(cell);
        return cell;
    }

    private Transform FindExitByLocalDirection(Cell cell, Vector3 localDirection)
    {
        if (cell == null || cell.Exits == null || cell.Exits.Length <= 0)
        {
            return null;
        }

        Transform bestExit = null;
        float bestScore = float.NegativeInfinity;
        Vector3 safeDirection = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.forward;

        for (int i = 0; i < cell.Exits.Length; i++)
        {
            GameObject exitObject = cell.Exits[i];
            if (exitObject == null)
            {
                continue;
            }

            Vector3 localPosition = cell.transform.InverseTransformPoint(exitObject.transform.position);
            float score = Vector3.Dot(localPosition.normalized, safeDirection);

            if (score > bestScore)
            {
                bestScore = score;
                bestExit = exitObject.transform;
            }
        }

        return bestExit;
    }

    private void AlignCellToExit(Cell cell, Transform selectedExit, Transform targetExit)
    {
        if (cell == null || selectedExit == null || targetExit == null)
        {
            return;
        }

        cell.transform.position = Vector3.zero;
        cell.transform.rotation = Quaternion.identity;

        float shiftAngle = targetExit.eulerAngles.y + 180f - selectedExit.eulerAngles.y;
        cell.transform.Rotate(new Vector3(0f, shiftAngle, 0f), Space.World);

        Vector3 shiftPosition = targetExit.position - selectedExit.position;
        cell.transform.position += shiftPosition;
    }

    private TutorialGate CreateGateAtExit(string name, Transform exit, string gateId)
    {
        if (exit == null)
        {
            return CreateGate(name, Vector3.zero, gateId);
        }

        GameObject gateObject = CreatePrefabObject(doorPrefab, name, exit.position, exit.rotation, worldRoot);

        if (gateObject == null)
        {
            gateObject = CreateCube(name, exit.position + Vector3.up * 1.5f, new Vector3(3.7f, 3f, 0.3f), gateMaterial, ScanSurfaceType.EmergencyExit);
            gateObject.transform.rotation = exit.rotation;
        }

        EnsureScanSurfaceInfo(gateObject, ScanSurfaceType.EmergencyExit);
        TutorialGate gate = gateObject.GetComponent<TutorialGate>();
        if (gate == null)
        {
            gate = gateObject.AddComponent<TutorialGate>();
        }

        gate.owner = this;
        gate.gateId = gateId;
        gate.lockedPrompt = "아직 잠겨 있습니다.";
        gate.openPrompt = "[E] 문 열기";
        gate.SetUnlocked(false);
        return gate;
    }

    private void BlockUnusedExits(Cell cell, Transform[] connectedExits)
    {
        if (cell == null || cell.Exits == null || doorPrefab == null)
        {
            return;
        }

        for (int i = 0; i < cell.Exits.Length; i++)
        {
            GameObject exitObject = cell.Exits[i];
            if (exitObject == null || IsConnectedExit(exitObject.transform, connectedExits))
            {
                continue;
            }

            GameObject blocker = CreatePrefabObject(doorPrefab, cell.name + "_BlockedExit_" + i, exitObject.transform.position, exitObject.transform.rotation, worldRoot);
            EnsureScanSurfaceInfo(blocker, ScanSurfaceType.Wall);
        }
    }

    private bool IsConnectedExit(Transform exit, Transform[] connectedExits)
    {
        if (exit == null || connectedExits == null)
        {
            return false;
        }

        for (int i = 0; i < connectedExits.Length; i++)
        {
            if (connectedExits[i] == exit)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetCellCenter(Cell cell)
    {
        if (TryGetObjectBounds(cell != null ? cell.gameObject : null, out Bounds bounds))
        {
            return new Vector3(bounds.center.x, 0f, bounds.center.z);
        }

        return cell != null ? cell.transform.position : Vector3.zero;
    }

    private void HideCellHelperObjects(Cell cell)
    {
        if (cell == null)
        {
            return;
        }

        Transform[] children = cell.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == cell.transform)
            {
                continue;
            }

            if (!IsGeneratedHelperName(child.name))
            {
                continue;
            }

            Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] != null)
                {
                    renderers[r].enabled = false;
                }
            }

            Collider[] colliders = child.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < colliders.Length; c++)
            {
                if (colliders[c] != null)
                {
                    colliders[c].enabled = false;
                }
            }
        }
    }

    private bool IsGeneratedHelperName(string objectName)
    {
        return ContainsIgnoreCase(objectName, "DoorPoint") ||
               ContainsIgnoreCase(objectName, "TempPortal") ||
               ContainsIgnoreCase(objectName, "PlayerSpawnPoint") ||
               ContainsIgnoreCase(objectName, "ItemSpawnPoint");
    }

    private bool ContainsIgnoreCase(string source, string value)
    {
        return !string.IsNullOrEmpty(source) &&
               source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void CreateDividerWall(float z)
    {
        CreateCube("DividerWall_Left_" + z, new Vector3(-4.45f, 1.5f, z), new Vector3(5.1f, 3f, 0.35f), wallMaterial, ScanSurfaceType.Wall);
        CreateCube("DividerWall_Right_" + z, new Vector3(4.45f, 1.5f, z), new Vector3(5.1f, 3f, 0.35f), wallMaterial, ScanSurfaceType.Wall);
    }

    private TutorialGate CreateGate(string name, Vector3 position, string gateId)
    {
        GameObject gateObject = CreateCube(name, position, new Vector3(3.7f, 3f, 0.3f), gateMaterial, ScanSurfaceType.EmergencyExit);
        TutorialGate gate = gateObject.AddComponent<TutorialGate>();
        gate.owner = this;
        gate.gateId = gateId;
        gate.lockedPrompt = "아직 잠겨 있습니다.";
        gate.openPrompt = "[E] 문 열기";
        gate.SetUnlocked(false);
        return gate;
    }

    private void CreateRoomLight(string name, Vector3 position)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(worldRoot, false);
        lightObject.transform.position = position;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.68f, 0.86f, 0.92f, 1f);
        light.range = 9f;
        light.intensity = 2.1f;

        GameObject strip = CreateCube(name + "_Strip", position + Vector3.down * 0.16f, new Vector3(3.2f, 0.08f, 0.18f), metalMaterial, ScanSurfaceType.Metal);
        strip.transform.SetParent(lightObject.transform, true);
    }

    private void BuildOverlayUi()
    {
        GameObject canvasObject = new GameObject("TutorialOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 300;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = CreateRect("Root", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        RectTransform guidePanel = CreateRect("GuidePanel", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -36f));
        guidePanel.sizeDelta = new Vector2(650f, 218f);
        AddImage(guidePanel, new Color(0f, 0f, 0f, 0.72f));
        AddOutline(guidePanel, new Color(0.62f, 0.86f, 0.92f, 0.32f), new Vector2(1.5f, -1.5f));

        titleText = CreateLabel("", guidePanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), 28f, new Color(1f, 0.78f, 0.34f, 1f), TextAlignmentOptions.Left);
        titleText.rectTransform.sizeDelta = new Vector2(-48f, 40f);

        bodyText = CreateLabel("", guidePanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(24f, -60f), 20f, new Color(0.86f, 0.94f, 0.95f, 1f), TextAlignmentOptions.Left);
        bodyText.rectTransform.sizeDelta = new Vector2(-48f, 68f);
        bodyText.textWrappingMode = TextWrappingModes.Normal;

        stepText = CreateLabel("", guidePanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), 18f, new Color(0.64f, 0.91f, 1f, 1f), TextAlignmentOptions.Left);
        stepText.rectTransform.sizeDelta = new Vector2(-48f, 58f);
        stepText.textWrappingMode = TextWrappingModes.Normal;

        RectTransform statusPanel = CreateRect("StatusPanel", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -36f));
        statusPanel.sizeDelta = new Vector2(440f, 116f);
        AddImage(statusPanel, new Color(0f, 0f, 0f, 0.68f));
        AddOutline(statusPanel, new Color(0.62f, 0.86f, 0.92f, 0.26f), new Vector2(1.5f, -1.5f));

        roleText = CreateLabel("", statusPanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -18f), 20f, new Color(0.94f, 0.97f, 0.98f, 1f), TextAlignmentOptions.Right);
        roleText.rectTransform.sizeDelta = new Vector2(-40f, 34f);

        objectiveText = CreateLabel("", statusPanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 20f), 18f, new Color(0.74f, 0.88f, 0.9f, 1f), TextAlignmentOptions.Right);
        objectiveText.rectTransform.sizeDelta = new Vector2(-40f, 48f);
        objectiveText.textWrappingMode = TextWrappingModes.Normal;

        promptText = CreateLabel("", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 148f), 24f, new Color(0.96f, 0.98f, 1f, 1f), TextAlignmentOptions.Center);
        promptText.rectTransform.sizeDelta = new Vector2(800f, 42f);
        promptText.gameObject.SetActive(false);

        BuildCrosshair(root);
        BuildFinishPanel(root);

        Button backButton = CreateButton(root, "나가기", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -170f), new Vector2(132f, 44f));
        backButton.onClick.AddListener(OnClickBackToMenu);
    }

    private void BuildCrosshair(RectTransform root)
    {
        RectTransform crosshairRoot = CreateRect("Crosshair", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        crosshairRoot.sizeDelta = new Vector2(54f, 54f);
        CreateCrosshairLine(crosshairRoot, new Vector2(-10f, 0f), new Vector2(7f, 1.4f));
        CreateCrosshairLine(crosshairRoot, new Vector2(10f, 0f), new Vector2(7f, 1.4f));
        CreateCrosshairLine(crosshairRoot, new Vector2(0f, -10f), new Vector2(1.4f, 7f));
        CreateCrosshairLine(crosshairRoot, new Vector2(0f, 10f), new Vector2(1.4f, 7f));
    }

    private void CreateCrosshairLine(Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform line = CreateRect("Line", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition);
        line.sizeDelta = size;
        AddImage(line, new Color(0.86f, 0.92f, 0.91f, 0.72f));
    }

    private void BuildFinishPanel(RectTransform root)
    {
        finishPanel = CreateRect("FinishPanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        finishPanel.sizeDelta = new Vector2(720f, 300f);
        AddImage(finishPanel, new Color(0f, 0f, 0f, 0.84f));
        AddOutline(finishPanel, new Color(1f, 0.78f, 0.34f, 0.45f), new Vector2(2f, -2f));

        TMP_Text finishTitle = CreateLabel("튜토리얼 완료", finishPanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), 38f, new Color(1f, 0.78f, 0.34f, 1f), TextAlignmentOptions.Center);
        finishTitle.rectTransform.sizeDelta = new Vector2(-80f, 58f);

        TMP_Text finishBody = CreateLabel("공통 조작, 시민 목표 진행, 도플갱어 방해 흐름을 모두 완료했습니다.", finishPanel, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), 24f, new Color(0.9f, 0.96f, 0.97f, 1f), TextAlignmentOptions.Center);
        finishBody.rectTransform.sizeDelta = new Vector2(-90f, 80f);
        finishBody.textWrappingMode = TextWrappingModes.Normal;

        Button backButton = CreateButton(finishPanel, "메인으로", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(210f, 58f));
        backButton.onClick.AddListener(OnClickBackToMenu);
        finishPanel.gameObject.SetActive(false);
    }

    private void BuildManagers()
    {
        GameObject managerObject = new GameObject("TutorialLabObjectiveManager");
        labObjectiveManager = managerObject.AddComponent<LabObjectiveManager>();
        labObjectiveManager.objectiveText = objectiveText;
        labObjectiveManager.promptText = promptText;
        labObjectiveManager.requiredComputerCount = 1;
    }

    private void BuildPlayer()
    {
        playerObject = new GameObject("TutorialPlayer");
        playerObject.transform.position = GetRoomPoint(commonRoomCenter, 0f, -1.8f, 0.12f);
        playerObject.transform.rotation = Quaternion.identity;

        CharacterController controller = playerObject.AddComponent<CharacterController>();
        controller.radius = 0.34f;
        controller.height = 1.8f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.35f;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.transform.SetParent(playerObject.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;
        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 75f;
        camera.nearClipPlane = 0.02f;
        camera.farClipPlane = 64f;
        cameraObject.AddComponent<AudioListener>();

        dotRenderer = cameraObject.AddComponent<InstancedScanDotRenderer>();
        dotRenderer.SetSourceDotPrefab(dotSourcePrefab);
        cameraObject.AddComponent<AudioSource>();
        LidarSpotScanner scanner = cameraObject.AddComponent<LidarSpotScanner>();

        PlayerStats stats = playerObject.AddComponent<PlayerStats>();
        playerTarget = playerObject.AddComponent<PlayerCombatTarget>();
        playerTarget.SetRole(PlayerRole.Citizen);
        playerTarget.photonActorNumber = 1;

        playerInventory = playerObject.AddComponent<PlayerInventory>();

        PlayerObjectiveInteractor interactor = playerObject.AddComponent<PlayerObjectiveInteractor>();
        interactor.playerCamera = camera;
        interactor.promptText = promptText;
        interactor.interactDistance = 2.9f;
        interactor.groundItemPickupRadius = 3.2f;

        PlayerItemUser itemUser = playerObject.AddComponent<PlayerItemUser>();
        itemUser.inventory = playerInventory;
        itemUser.playerCamera = camera;
        itemUser.dotRenderer = dotRenderer;
        itemUser.playerStats = stats;
        itemUser.selfTarget = playerTarget;
        itemUser.cameraPointCount = 560;
        itemUser.cameraMaxDistance = 16f;
        itemUser.cameraAttemptsPerPoint = 5;

        PlayerMotor motor = playerObject.AddComponent<PlayerMotor>();
        motor.playerCamera = cameraObject.transform;
        motor.playerStats = stats;

        MouseLook mouseLook = playerObject.AddComponent<MouseLook>();
        mouseLook.playerCamera = cameraObject.transform;
    }

    private void BuildTrainingObjects()
    {
        Vector3 itemTablePosition = GetRoomPoint(commonRoomCenter, -2.25f, -1.25f, 0f);
        CreateTable("CommonItemTable", itemTablePosition);
        cameraPickup = CreatePickup(ItemType.Camera, itemTablePosition + new Vector3(0f, 1.03f, 0f));
        commonComputer = CreateComputer("CommonTrainingComputer", GetRoomPoint(commonRoomCenter, 2.25f, -0.95f, 0f), Quaternion.Euler(0f, -90f, 0f), true, 1);

        citizenTargetA = CreateComputer("CitizenTargetComputer_A", GetRoomPoint(citizenRoomCenter, -2.35f, -1.55f, 0f), Quaternion.Euler(0f, 90f, 0f), true, 2);
        citizenWrongComputer = CreateComputer("CitizenWrongComputer", GetRoomPoint(citizenRoomCenter, 2.35f, -0.1f, 0f), Quaternion.Euler(0f, -90f, 0f), false, 3);
        citizenTargetB = CreateComputer("CitizenTargetComputer_B", GetRoomPoint(citizenRoomCenter, -0.6f, 2.0f, 0f), Quaternion.Euler(0f, 180f, 0f), true, 4);

        blendInStation = CreateMarker("BlendInStation", GetRoomPoint(doppelgangerRoomCenter, -2.2f, -1.7f, 0.03f), 1.1f);
        CreateComputer("DoppelFakeWorkstation", GetRoomPoint(doppelgangerRoomCenter, -2.85f, -1.7f, 0f), Quaternion.Euler(0f, 90f, 0f), false, 5);
        dummyCitizen = CreateDummyCitizen(GetRoomPoint(doppelgangerRoomCenter, 2.15f, -0.2f, 0f));
        doppelgangerComputer = CreateComputer("DoppelgangerRestoredTargetComputer", GetRoomPoint(doppelgangerRoomCenter, 0f, 2.25f, 0f), Quaternion.Euler(0f, 180f, 0f), true, 6);
        doppelgangerComputer.preventSabotageAfterExitUnlocked = false;
        alibiStation = CreateMarker("AlibiStation", GetRoomPoint(doppelgangerRoomCenter, -2.15f, 2.1f, 0.03f), 1.1f);

        GameLoopManager.EnsureExists().RebuildComputerIndex();
    }

    private Vector3 GetRoomPoint(Vector3 roomCenter, float xOffset, float zOffset, float y)
    {
        return new Vector3(roomCenter.x + xOffset, y, roomCenter.z + zOffset);
    }

    private void ConfigureRoundFlow()
    {
        GameLoopManager gameLoop = GameLoopManager.EnsureExists();
        gameLoop.autoReturnToRoomLobby = false;
        gameLoop.showInGameResultOverlay = false;
        gameLoop.killerWinsOnTimerExpired = false;
        gameLoop.killerWinsWhenNoCitizenCanEscape = false;
        gameLoop.requiredEscapedCitizens = 99;
        RoundTimer.ResetTimer();
    }

    private void EnterCommonStage()
    {
        currentStage = TutorialStage.Common;
        playerTarget.SetRole(PlayerRole.Citizen);
        commonComputer.SetSelectedObjective(true, true);
        labObjectiveManager.SetupComputerObjectives(new[] { commonComputer }, 1);
        commonGate.SetUnlocked(false);
        citizenGate.SetUnlocked(false);
        SetCommonStep(CommonStep.Move);
    }

    private void EnterCitizenStage()
    {
        currentStage = TutorialStage.Citizen;
        citizenSabotageIssued = false;
        playerTarget.SetRole(PlayerRole.Citizen);

        citizenTargetA.SetSelectedObjective(true, true);
        citizenTargetB.SetSelectedObjective(true, true);
        citizenWrongComputer.SetSelectedObjective(false, true);
        labObjectiveManager.SetupComputerObjectives(new[] { citizenTargetA, citizenTargetB }, 2);
        citizenGate.SetUnlocked(false);
        SetCitizenStep(CitizenStep.FindFirstTarget);
    }

    private void EnterDoppelgangerStage()
    {
        currentStage = TutorialStage.Doppelganger;
        playerTarget.SetRole(PlayerRole.Killer);
        doppelgangerComputer.SetSelectedObjective(true, true);
        doppelgangerComputer.ApplyRestoredFromNetwork();
        blendTimer = 0f;
        alibiTimer = 0f;
        SetDoppelgangerStep(DoppelgangerStep.BlendIn);
    }

    private void SetCommonStep(CommonStep step)
    {
        currentStep = (int)step;
        ResetStepTracking();

        if (step == CommonStep.OpenExit)
        {
            commonGate.SetUnlocked(true);
        }
    }

    private void SetCitizenStep(CitizenStep step)
    {
        currentStep = (int)step;
        ResetStepTracking();

        if (step == CitizenStep.Escape)
        {
            citizenGate.SetUnlocked(true);
        }
    }

    private void SetDoppelgangerStep(DoppelgangerStep step)
    {
        currentStep = (int)step;
        ResetStepTracking();
    }

    private void ResetStepTracking()
    {
        stepStartPosition = playerObject != null ? playerObject.transform.position : Vector3.zero;
        stepStartDotCount = dotRenderer != null ? dotRenderer.GetActiveDotCount() : 0;
        stepStartCameraAmount = playerInventory != null ? playerInventory.GetItemAmount(ItemType.Camera) : 0;
        blendTimer = 0f;
        alibiTimer = 0f;
    }

    private void UpdateTutorialProgress()
    {
        switch (currentStage)
        {
            case TutorialStage.Common:
                UpdateCommonProgress();
                break;

            case TutorialStage.Citizen:
                UpdateCitizenProgress();
                break;

            case TutorialStage.Doppelganger:
                UpdateDoppelgangerProgress();
                break;
        }
    }

    private void UpdateCommonProgress()
    {
        CommonStep step = (CommonStep)currentStep;

        if (step == CommonStep.Move && Vector3.Distance(playerObject.transform.position, stepStartPosition) >= 2f)
        {
            SetCommonStep(CommonStep.Scan);
            return;
        }

        if (step == CommonStep.Scan && dotRenderer != null && dotRenderer.GetActiveDotCount() >= stepStartDotCount + 18)
        {
            SetCommonStep(CommonStep.PickUpItem);
            return;
        }

        if (step == CommonStep.PickUpItem && playerInventory.GetItemAmount(ItemType.Camera) > 0)
        {
            SetCommonStep(CommonStep.UseItem);
            return;
        }

        if (step == CommonStep.UseItem &&
            (playerInventory.GetItemAmount(ItemType.Camera) < stepStartCameraAmount ||
             (dotRenderer != null && dotRenderer.GetActiveDotCount() >= stepStartDotCount + 24)))
        {
            SetCommonStep(CommonStep.CheckComputer);
            return;
        }

        if (step == CommonStep.CheckComputer && commonComputer.IsRestored)
        {
            SetCommonStep(CommonStep.OpenExit);
        }
    }

    private void UpdateCitizenProgress()
    {
        CitizenStep step = (CitizenStep)currentStep;

        if (step == CitizenStep.FindFirstTarget && citizenTargetA.IsRestored)
        {
            SetCitizenStep(CitizenStep.CheckWrongComputer);
            return;
        }

        if (step == CitizenStep.CheckWrongComputer && citizenWrongComputer.IsRestored)
        {
            IssueCitizenSabotage();
            SetCitizenStep(CitizenStep.RepairSabotagedComputer);
            return;
        }

        if (step == CitizenStep.RepairSabotagedComputer && citizenTargetA.IsRestored && citizenTargetA.HasBeenSabotaged)
        {
            SetCitizenStep(CitizenStep.FindSecondTarget);
            return;
        }

        if (step == CitizenStep.FindSecondTarget && citizenTargetB.IsRestored)
        {
            SetCitizenStep(CitizenStep.Escape);
        }
    }

    private void IssueCitizenSabotage()
    {
        if (citizenSabotageIssued)
        {
            return;
        }

        citizenSabotageIssued = true;
        citizenTargetA.ApplySabotagedFromNetwork();
    }

    private void UpdateDoppelgangerProgress()
    {
        DoppelgangerStep step = (DoppelgangerStep)currentStep;

        if (step == DoppelgangerStep.BlendIn)
        {
            blendTimer = UpdateStationTimer(blendInStation, blendTimer, blendInSeconds);
            if (blendTimer >= blendInSeconds)
            {
                SetDoppelgangerStep(DoppelgangerStep.KillDuringKillTime);
            }
            return;
        }

        if (step == DoppelgangerStep.KillDuringKillTime)
        {
            TryHandleTutorialKill();
            if (dummyCitizen != null && dummyCitizen.isDead)
            {
                SetDoppelgangerStep(DoppelgangerStep.SabotageComputer);
            }
            return;
        }

        if (step == DoppelgangerStep.SabotageComputer && doppelgangerComputer.IsSabotaged)
        {
            SetDoppelgangerStep(DoppelgangerStep.AvoidSuspicion);
            return;
        }

        if (step == DoppelgangerStep.AvoidSuspicion)
        {
            alibiTimer = UpdateStationTimer(alibiStation, alibiTimer, alibiSeconds);
            if (alibiTimer >= alibiSeconds)
            {
                CompleteTutorial();
            }
        }
    }

    private float UpdateStationTimer(Transform station, float timer, float requiredSeconds)
    {
        if (station == null || playerObject == null)
        {
            return 0f;
        }

        float distance = Vector3.Distance(playerObject.transform.position, station.position);
        if (distance <= 1.85f)
        {
            return Mathf.Min(requiredSeconds, timer + Time.deltaTime);
        }

        return 0f;
    }

    private void TryHandleTutorialKill()
    {
        if (!Input.GetKeyDown(GameInputBindings.Kill) || dummyCitizen == null || dummyCitizen.isDead)
        {
            return;
        }

        float distance = Vector3.Distance(playerObject.transform.position, dummyCitizen.transform.position);
        if (distance > 2.6f)
        {
            return;
        }

        dummyCitizen.Die();
    }

    private void CompleteTutorial()
    {
        tutorialFinished = true;
        currentStage = TutorialStage.Complete;
        finishPanel.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        MouseLook mouseLook = playerObject != null ? playerObject.GetComponent<MouseLook>() : null;
        if (mouseLook != null)
        {
            mouseLook.enabled = false;
        }
    }

    private void RefreshOverlayText()
    {
        if (titleText == null)
        {
            return;
        }

        string title;
        string body;
        string step;
        string objective;

        GetCurrentTexts(out title, out body, out step, out objective);
        titleText.text = title;
        bodyText.text = body;
        stepText.text = step;
        objectiveText.text = objective;
        roleText.text = playerTarget != null && playerTarget.role == PlayerRole.Killer ? "역할: 도플갱어" : "역할: 시민";
    }

    private void GetCurrentTexts(out string title, out string body, out string step, out string objective)
    {
        title = "";
        body = "";
        step = "";
        objective = "";

        if (currentStage == TutorialStage.Common)
        {
            title = "튜토리얼 1: 공통 훈련장";
            body = "이동, 스캔, 아이템, 컴퓨터 확인, 탈출구 열기를 한 방에서 익힙니다.";
            CommonStep commonStep = (CommonStep)currentStep;

            switch (commonStep)
            {
                case CommonStep.Move:
                    step = "WASD로 앞으로 이동하세요.";
                    objective = "이동";
                    break;
                case CommonStep.Scan:
                    step = GameInputBindings.FormatKey(GameInputBindings.Scan) + "을 눌러 주변을 스캔하세요.";
                    objective = "스캔";
                    break;
                case CommonStep.PickUpItem:
                    step = "테이블 위 카메라를 보고 " + GameInputBindings.FormatKey(GameInputBindings.Pickup) + "로 줍습니다.";
                    objective = "아이템 줍기";
                    break;
                case CommonStep.UseItem:
                    step = GameInputBindings.FormatKey(GameInputBindings.UseItem) + "으로 카메라 아이템을 사용하세요.";
                    objective = "아이템 사용";
                    break;
                case CommonStep.CheckComputer:
                    step = "컴퓨터를 보고 " + GameInputBindings.FormatKey(GameInputBindings.Interact) + "를 길게 눌러 확인하세요.";
                    objective = "컴퓨터 확인";
                    break;
                case CommonStep.OpenExit:
                    step = "앞의 훈련문을 보고 " + GameInputBindings.FormatKey(GameInputBindings.Interact) + "로 열면 시민 실전으로 이어집니다.";
                    objective = "탈출구 열기";
                    break;
            }

            return;
        }

        if (currentStage == TutorialStage.Citizen)
        {
            title = "튜토리얼 2: 시민 실전";
            body = "목표 컴퓨터 2개를 찾고, 오답과 도플갱어 방해를 처리한 뒤 탈출합니다.";
            CitizenStep citizenStep = (CitizenStep)currentStep;

            switch (citizenStep)
            {
                case CitizenStep.FindFirstTarget:
                    step = "왼쪽 목표 컴퓨터를 확인하세요. 복구 전에는 목표와 오답이 겉으로 구분되지 않습니다.";
                    objective = "목표 컴퓨터 1/2";
                    break;
                case CitizenStep.CheckWrongComputer:
                    step = "오른쪽 컴퓨터를 확인해서 오답 컴퓨터가 어떻게 표시되는지 확인하세요.";
                    objective = "오답 컴퓨터 1개 확인";
                    break;
                case CitizenStep.RepairSabotagedComputer:
                    step = "도플갱어가 첫 목표 컴퓨터를 다시 망가뜨렸습니다. 돌아가서 재수리하세요.";
                    objective = "망가진 컴퓨터 재수리";
                    break;
                case CitizenStep.FindSecondTarget:
                    step = "방 안쪽의 두 번째 목표 컴퓨터를 확인하세요.";
                    objective = "목표 컴퓨터 2/2";
                    break;
                case CitizenStep.Escape:
                    step = "열린 문을 열고 다음 방으로 이동하세요.";
                    objective = "탈출";
                    break;
            }

            return;
        }

        if (currentStage == TutorialStage.Doppelganger)
        {
            title = "튜토리얼 3: 도플갱어 실전";
            body = "시민처럼 행동하다가 킬타임에 공격하고, 복구된 목표 컴퓨터를 다시 망가뜨립니다.";
            DoppelgangerStep doppelStep = (DoppelgangerStep)currentStep;

            switch (doppelStep)
            {
                case DoppelgangerStep.BlendIn:
                    step = "바닥 표시 위에서 " + Mathf.CeilToInt(Mathf.Max(0f, blendInSeconds - blendTimer)) + "초 동안 시민처럼 머무르세요.";
                    objective = "시민처럼 행동하기";
                    break;
                case DoppelgangerStep.KillDuringKillTime:
                    step = "훈련 킬타임입니다. 더미 시민 근처에서 " + GameInputBindings.FormatKey(GameInputBindings.Kill) + "를 눌러 공격하세요.";
                    objective = "킬타임에 공격";
                    break;
                case DoppelgangerStep.SabotageComputer:
                    step = "복구된 목표 컴퓨터를 보고 " + GameInputBindings.FormatKey(GameInputBindings.Interact) + "를 길게 눌러 다시 망가뜨리세요.";
                    objective = "복구된 목표 컴퓨터 망가뜨리기";
                    break;
                case DoppelgangerStep.AvoidSuspicion:
                    step = "알리바이 표시 위에서 " + Mathf.CeilToInt(Mathf.Max(0f, alibiSeconds - alibiTimer)) + "초 동안 머무르세요.";
                    objective = "의심 피하기";
                    break;
            }
        }
    }

    private ObjectiveComputer CreateComputer(string name, Vector3 position, Quaternion rotation, bool selected, int networkId)
    {
        GameObject root = CreatePrefabObject(computerPrefab, name, position, rotation, worldRoot);

        if (root == null)
        {
            root = new GameObject(name);
            root.transform.SetParent(worldRoot, false);
            root.transform.position = position;
            root.transform.rotation = rotation;

            CreateChildCube("Base", root.transform, new Vector3(0f, 0.36f, 0f), new Vector3(1.25f, 0.72f, 0.52f), computerMaterial, ScanSurfaceType.SecurityTerminal);
            CreateChildCube("Screen", root.transform, new Vector3(0f, 0.96f, -0.18f), new Vector3(1.05f, 0.55f, 0.12f), metalMaterial, ScanSurfaceType.SecurityTerminal);
            CreateChildCube("Keyboard", root.transform, new Vector3(0f, 0.74f, 0.27f), new Vector3(0.98f, 0.12f, 0.34f), metalMaterial, ScanSurfaceType.SecurityTerminal);
        }

        ObjectiveComputer computer = root.GetComponent<ObjectiveComputer>();
        if (computer == null)
        {
            computer = root.GetComponentInChildren<ObjectiveComputer>(true);
        }

        if (computer == null)
        {
            computer = root.AddComponent<ObjectiveComputer>();
        }

        computer.restoreDuration = tutorialComputerHoldDuration;
        computer.sabotageDuration = tutorialComputerHoldDuration;
        computer.sabotagedRepairDuration = tutorialComputerHoldDuration;
        computer.maxInteractorMoveDistance = 0.8f;
        computer.requireCitizenRole = false;
        computer.preventKillerRestore = true;
        computer.allowKillerSabotage = true;
        computer.existingDotRecolorRadius = 1.5f;
        computer.existingDotRecolorCenter = computer.transform;
        computer.SetNetworkObjectiveId(networkId);
        computer.SetSelectedObjective(selected, true);
        EnsureScanSurfaceInfo(root, ScanSurfaceType.SecurityTerminal);
        return computer;
    }

    private void CreateTable(string name, Vector3 position)
    {
        GameObject prefabTable = CreatePrefabObject(tablePrefab, name, position, Quaternion.identity, worldRoot);
        if (prefabTable != null)
        {
            EnsureScanSurfaceInfo(prefabTable, ScanSurfaceType.Metal);
            return;
        }

        GameObject table = new GameObject(name);
        table.transform.SetParent(worldRoot, false);
        table.transform.position = position;
        CreateChildCube("Top", table.transform, new Vector3(0f, 0.58f, 0f), new Vector3(1.6f, 0.12f, 0.9f), metalMaterial, ScanSurfaceType.Metal);
        CreateChildCube("LegA", table.transform, new Vector3(-0.62f, 0.28f, -0.32f), new Vector3(0.12f, 0.56f, 0.12f), metalMaterial, ScanSurfaceType.Metal);
        CreateChildCube("LegB", table.transform, new Vector3(0.62f, 0.28f, -0.32f), new Vector3(0.12f, 0.56f, 0.12f), metalMaterial, ScanSurfaceType.Metal);
        CreateChildCube("LegC", table.transform, new Vector3(-0.62f, 0.28f, 0.32f), new Vector3(0.12f, 0.56f, 0.12f), metalMaterial, ScanSurfaceType.Metal);
        CreateChildCube("LegD", table.transform, new Vector3(0.62f, 0.28f, 0.32f), new Vector3(0.12f, 0.56f, 0.12f), metalMaterial, ScanSurfaceType.Metal);
    }

    private WorldItemPickup CreatePickup(ItemType itemType, Vector3 position)
    {
        GameObject prefab = itemType == ItemType.Camera ? cameraPickupPrefab : null;
        GameObject itemObject = CreatePrefabObject(prefab, "Tutorial" + itemType + "Pickup", position, Quaternion.identity, worldRoot);

        if (itemObject == null)
        {
            itemObject = CreateCube("Tutorial" + itemType + "Pickup", position, new Vector3(0.46f, 0.24f, 0.32f), itemMaterial, ScanSurfaceType.Item);
        }

        WorldItemPickup pickup = itemObject.GetComponent<WorldItemPickup>();
        if (pickup == null)
        {
            pickup = itemObject.GetComponentInChildren<WorldItemPickup>(true);
        }

        if (pickup == null)
        {
            pickup = itemObject.AddComponent<WorldItemPickup>();
        }

        pickup.itemType = itemType;
        pickup.hideAfterPickup = true;
        pickup.destroyAfterPickup = false;
        pickup.onlyRemoveItemColorDots = true;
        EnsureScanSurfaceInfo(itemObject, ScanSurfaceType.Item);
        return pickup;
    }

    private PlayerCombatTarget CreateDummyCitizen(Vector3 position)
    {
        GameObject root = new GameObject("TutorialDummyCitizen");
        root.transform.SetParent(worldRoot, false);
        root.transform.position = position;

        PlayerVisibleAvatar avatar = root.AddComponent<PlayerVisibleAvatar>();
        avatar.hideWhenLocalScannerOwner = false;
        avatar.hideRenderers = false;
        avatar.addScanColliders = true;
        avatar.RebuildAvatar();

        PlayerCombatTarget target = root.AddComponent<PlayerCombatTarget>();
        target.role = PlayerRole.Citizen;
        target.isRemoteProxy = true;
        target.photonActorNumber = 101;
        target.bodyVisualRoot = root;
        target.collidersToDisable = root.GetComponentsInChildren<Collider>(true);

        if (target.collidersToDisable == null || target.collidersToDisable.Length == 0)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "FallbackBody";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.92f, 0f);
            body.transform.localScale = new Vector3(0.48f, 0.92f, 0.48f);
            body.GetComponent<Renderer>().sharedMaterial = dummyMaterial;
            ScanSurfaceInfo surfaceInfo = body.AddComponent<ScanSurfaceInfo>();
            surfaceInfo.surfaceType = ScanSurfaceType.PlayerBody;
            target.collidersToDisable = body.GetComponents<Collider>();
        }

        root.AddComponent<PlayerStats>();
        return target;
    }

    private Transform CreateMarker(string name, Vector3 position, float radius)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = name;
        marker.transform.SetParent(worldRoot, false);
        marker.transform.position = position;
        marker.transform.localScale = new Vector3(radius, 0.025f, radius);
        marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;

        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            markerCollider.isTrigger = true;
        }

        ScanSurfaceInfo surfaceInfo = marker.AddComponent<ScanSurfaceInfo>();
        surfaceInfo.surfaceType = ScanSurfaceType.Metal;
        return marker.transform;
    }

    private GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material, ScanSurfaceType surfaceType)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(worldRoot, false);
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;

        ScanSurfaceInfo surfaceInfo = cube.AddComponent<ScanSurfaceInfo>();
        surfaceInfo.surfaceType = surfaceType;
        return cube;
    }

    private GameObject CreateChildCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, ScanSurfaceType surfaceType)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = localScale;
        cube.GetComponent<Renderer>().sharedMaterial = material;

        ScanSurfaceInfo surfaceInfo = cube.AddComponent<ScanSurfaceInfo>();
        surfaceInfo.surfaceType = surfaceType;
        return cube;
    }

    private GameObject CreatePrefabObject(GameObject prefab, string objectName, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = Instantiate(prefab, position, rotation, parent);
        instance.name = objectName;
        return instance;
    }

    private void EnsureScanSurfaceInfo(GameObject root, ScanSurfaceType fallbackSurfaceType)
    {
        if (root == null)
        {
            return;
        }

        ScanSurfaceInfo[] existingInfos = root.GetComponentsInChildren<ScanSurfaceInfo>(true);
        if (existingInfos != null && existingInfos.Length > 0)
        {
            return;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
        {
            ScanSurfaceInfo rootInfo = root.GetComponent<ScanSurfaceInfo>();
            if (rootInfo == null)
            {
                rootInfo = root.AddComponent<ScanSurfaceInfo>();
            }

            rootInfo.surfaceType = fallbackSurfaceType;
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            ScanSurfaceInfo info = colliders[i].GetComponent<ScanSurfaceInfo>();
            if (info == null)
            {
                info = colliders[i].gameObject.AddComponent<ScanSurfaceInfo>();
            }

            info.surfaceType = fallbackSurfaceType;
        }
    }

    private bool TryGetObjectBounds(GameObject root, out Bounds resultBounds)
    {
        resultBounds = new Bounds(root != null ? root.transform.position : Vector3.zero, Vector3.zero);

        if (root == null)
        {
            return false;
        }

        bool hasBounds = false;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                resultBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                resultBounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                resultBounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                resultBounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private void LockGameplayCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private Button CreateButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform rect = CreateRect("Button_" + label, parent, anchorMin, anchorMax, pivot, anchoredPosition);
        rect.sizeDelta = size;
        Image image = AddImage(rect, new Color(0f, 0f, 0f, 0.72f));
        AddOutline(rect, new Color(0.78f, 0.88f, 0.84f, 0.32f), new Vector2(1.2f, -1.2f));
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text buttonText = CreateLabel(label, rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, 20f, new Color(0.94f, 0.98f, 1f, 1f), TextAlignmentOptions.Center);
        buttonText.rectTransform.sizeDelta = Vector2.zero;
        return button;
    }

    private TMP_Text CreateLabel(string text, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect("Text", parent, anchorMin, anchorMax, pivot, anchoredPosition);
        TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.enableAutoSizing = true;
        label.fontSizeMax = fontSize;
        label.fontSizeMin = Mathf.Max(11f, fontSize - 8f);
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        LocalizedTmpFontProvider.Apply(label);

        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.86f);
        shadow.effectDistance = new Vector2(1.4f, -1.4f);
        return label;
    }

    private RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        return rect;
    }

    private Image AddImage(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = whiteSprite;
        image.color = color;
        return image;
    }

    private void AddOutline(RectTransform rect, Color color, Vector2 distance)
    {
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private Sprite CreateSolidSprite()
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                texture.SetPixel(x, y, Color.white);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
    }
}

public class TutorialGate : MonoBehaviour, IPlayerInteractable
{
    public TutorialSceneController owner;
    public string gateId;
    public string lockedPrompt = "Locked";
    public string openPrompt = "[E] Open";

    private bool unlocked;
    private bool opened;
    private Vector3 closedLocalPosition;
    private Collider[] colliders;

    public bool IsOpen => opened;

    private void Awake()
    {
        closedLocalPosition = transform.localPosition;
        colliders = GetComponentsInChildren<Collider>(true);
    }

    public void SetUnlocked(bool value)
    {
        unlocked = value;
    }

    public string GetPrompt(PlayerObjectiveInteractor interactor)
    {
        if (opened)
        {
            return string.Empty;
        }

        return unlocked ? openPrompt : lockedPrompt;
    }

    public bool CanInteract(PlayerObjectiveInteractor interactor)
    {
        return !opened;
    }

    public void Interact(PlayerObjectiveInteractor interactor)
    {
        if (!unlocked || opened)
        {
            return;
        }

        Open();
    }

    private void Open()
    {
        opened = true;
        transform.localPosition = closedLocalPosition + new Vector3(0f, 3.25f, 0f);

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        if (owner != null)
        {
            owner.NotifyGateOpened(this);
        }
    }
}
