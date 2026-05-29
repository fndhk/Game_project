using System.Collections;
using System.Collections.Generic;
using ArtNotes.UndergroundLaboratoryGenerator;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialSceneController : MonoBehaviour
{
    private const int RequiredTutorialComputerCount = 3;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public string mainMenuSceneName = "LobbyScene";

    [Header("Tutorial Timing")]
    [SerializeField] private float tutorialComputerHoldDuration = 6f;
    [SerializeField] private float tutorialSabotageDuration = 5f;
    [SerializeField] private float tutorialSabotagedRepairDuration = 4f;
    [SerializeField] private float blendInSeconds = 2f;
    [SerializeField] private float alibiSeconds = 2f;

    [Header("In-Game Prefabs")]
    [SerializeField] private Cell commonRoomPrefab;
    [SerializeField] private Cell citizenRoomPrefab;
    [SerializeField] private Cell doppelgangerRoomPrefab;
    [SerializeField] private Cell corridorPrefab;
    [SerializeField] private GameObject doorPrefab;
    [SerializeField] private GameObject cameraPickupPrefab;
    [SerializeField] private GameObject dotSourcePrefab;

    [Header("In-Game Audio")]
    [SerializeField] private AudioClip scanPulseClip;
    [SerializeField] private AudioClip computerStartClip;
    [SerializeField] private AudioClip computerProgressClip;
    [SerializeField] private AudioClip computerSuccessClip;
    [SerializeField] private AudioClip computerFakeClip;
    [SerializeField] private AudioClip ambientLoopClip;
    [SerializeField] private AudioClip[] footstepClips;

    [Header("In-Game Map Generation")]
    [SerializeField] private int tutorialRoomCount = 6;
    [SerializeField] private Vector2 tutorialMapSize = new Vector2(58f, 58f);
    [SerializeField] private Cell[] generatorCellPrefabs;
    [SerializeField] private Cell[] generatorStartRoomPrefabs;
    [SerializeField] private GameObject[] generatorDoorPrefabs;
    [SerializeField] private GameObject generatorConnectedDoorPrefab;
    [SerializeField] private GameObject generatorBlockDoorPrefab;
    [SerializeField] private GameObject generatorExitDoorPrefab;
    [SerializeField] private GameObject[] generatorItemPrefabs;

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
        RepairFirstComputer,
        RepairSecondComputer,
        RepairThirdComputer,
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
    private LaboratoryGenerator laboratoryGenerator;
    private EmergencyExitDoor tutorialExitDoor;

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
    private bool tutorialReady;
    private ObjectiveComputer[] generatedComputers = new ObjectiveComputer[0];
    private WorldItemPickup[] generatedPickups = new WorldItemPickup[0];
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
    private Canvas tutorialLoadingCanvas;
    private CanvasGroup tutorialLoadingGroup;
    private TMP_Text tutorialLoadingText;
    private Image tutorialLoadingFill;
    private Sprite whiteSprite;

    private Material floorMaterial;
    private Material wallMaterial;
    private Material metalMaterial;
    private Material itemMaterial;
    private Material gateMaterial;
    private Material markerMaterial;
    private Material dummyMaterial;

    private IEnumerator Start()
    {
        DestroySceneCamera();
        whiteSprite = CreateSolidSprite();

        UiEventSystemUtility.EnsureSingle(gameObject);
        ConfigureSceneLighting();
        PrepareMaterials();
        BuildTutorialLoadingUi();
        ShowTutorialLoading("튜토리얼 맵 생성 중...", 0.02f);
        yield return null;

        BuildOverlayUi();
        BuildManagers();
        BuildPlayer();
        ConfigureRoundFlow();
        LaboratoryGenerator.LoadingPhaseChanged += HandleTutorialLoadingPhaseChanged;
        yield return StartCoroutine(BuildWorld());
        LaboratoryGenerator.LoadingPhaseChanged -= HandleTutorialLoadingPhaseChanged;
        BuildTrainingObjects();
        if (!HasRequiredTutorialComputers())
        {
            tutorialReady = false;
            HideTutorialLoading();
            yield break;
        }

        ConfigureRoundFlow();
        EnterCommonStage();
        LockGameplayCursor();
        tutorialReady = true;
        HideTutorialLoading();
    }

    private void Update()
    {
        if (tutorialFinished)
        {
            return;
        }

        if (!tutorialReady)
        {
            return;
        }

        UpdateTutorialProgress();
        RefreshOverlayText();
    }

    private void OnDestroy()
    {
        LaboratoryGenerator.LoadingPhaseChanged -= HandleTutorialLoadingPhaseChanged;
        GameplayStartupGate.SetLoadingScreenBlocked(false);
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
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientIntensity = 0f;
        RenderSettings.reflectionIntensity = 0f;
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogColor = Color.black;
        RenderSettings.fogDensity = 0.035f;
    }

    private void PrepareMaterials()
    {
        floorMaterial = CreateMaterial("Tutorial_Floor", new Color(0.16f, 0.18f, 0.18f, 1f));
        wallMaterial = CreateMaterial("Tutorial_Wall", new Color(0.24f, 0.27f, 0.29f, 1f));
        metalMaterial = CreateMaterial("Tutorial_Metal", new Color(0.32f, 0.38f, 0.41f, 1f));
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

    private IEnumerator BuildWorld()
    {
        worldRoot = new GameObject("TutorialGeneratedLaboratory").transform;
        laboratoryGenerator = worldRoot.gameObject.AddComponent<LaboratoryGenerator>();
        ConfigureTutorialGenerator(laboratoryGenerator);

        int baseSeed = laboratoryGenerator.FixedGenerationSeed;
        int generationAttempts = 4;
        for (int attempt = 0; attempt < generationAttempts; attempt++)
        {
            laboratoryGenerator.FixedGenerationSeed = baseSeed + attempt * 97;
            yield return StartCoroutine(laboratoryGenerator.StartGeneration());
            CacheGeneratedWorldReferences();

            if (laboratoryGenerator.IsGenerationComplete && GetGeneratedComputerCount() >= RequiredTutorialComputerCount)
            {
                break;
            }

            Debug.LogWarning(
                "[TutorialSceneController] Generated map computer count is not enough. Regenerating tutorial map. Count: " +
                GetGeneratedComputerCount() + " / Required: " + RequiredTutorialComputerCount
            );
        }

        if (!laboratoryGenerator.IsGenerationComplete)
        {
            Debug.LogWarning("[TutorialSceneController] LaboratoryGenerator failed. Falling back to minimal tutorial geometry.");
            BuildFallbackWorld();
            CacheGeneratedWorldReferences();
        }
    }

    private int GetGeneratedComputerCount()
    {
        int count = 0;

        if (generatedComputers == null)
        {
            return count;
        }

        for (int i = 0; i < generatedComputers.Length; i++)
        {
            if (generatedComputers[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private void BuildFallbackWorld()
    {
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
    }

    private void ConfigureTutorialGenerator(LaboratoryGenerator generator)
    {
        if (generator == null)
        {
            return;
        }

        generator.ManualGenerationOnly = true;
        generator.GenerateOnStart = false;
        generator.UsePhotonRoomSeed = false;
        generator.UseFixedGenerationSeed = true;
        generator.FixedGenerationSeed = 240527;
        generator.AutoScaleByPlayerCount = false;
        generator.RoomCount = Mathf.Clamp(tutorialRoomCount, 4, 6);
        generator.PlayerCount = 1;
        generator.BalancePlayerCountOverride = 1;
        generator.UseMapBounds = true;
        generator.MapCenter = Vector3.zero;
        generator.MapSize = tutorialMapSize;
        generator.MapBoundsPadding = 0f;
        generator.DrawMapBoundsGizmo = false;
        generator.FloorHeight = 10f;
        generator.BlockBigRoomToBigRoom = true;
        generator.BlockCorridorToCorridor = true;
        generator.ForceCorridorBetweenRooms = false;
        generator.AllowSmallBigDirectConnection = true;
        generator.StartRoomMinDistance = 10f;
        generator.StartRoomsOnlyOnFirstFloor = false;
        generator.GenerateDedicatedStartRooms = false;

        generator.SpawnPlayersAfterGeneration = true;
        generator.ExistingPlayer = playerObject != null ? playerObject.transform : null;
        generator.ExistingPlayers = playerObject != null ? new[] { playerObject.transform } : new Transform[0];
        generator.PlayerSpawnCount = 1;
        generator.PlayerPrefab = null;
        generator.PlayerParent = null;
        generator.AutoFindExistingPlayerByTag = false;
        generator.PlayerSpawnPositionOffset = Vector3.zero;
        generator.AlignPlayerRotationToSpawnPoint = true;
        generator.RandomizePlayerSpawnYaw = false;

        generator.SpawnItemsAfterGeneration = true;
        generator.ItemPrefabs = ResolveGeneratorItemPrefabs();
        generator.ItemSpawnCount = 5;
        generator.AllowItemSpawnInPlayerRooms = true;
        generator.MinItemDistanceFromPlayerSpawn = 0f;
        generator.PreventDuplicateItemSpawnPointUse = true;
        generator.ItemParent = generator.transform;
        generator.ItemSpawnPositionOffset = Vector3.zero;
        generator.AlignItemRotationToSpawnPoint = true;
        generator.HideSpawnedItemRenderers = true;
        generator.MaxItemsPerRoom = 2;
        generator.SnapSpawnedItemsToSpawnPointGround = true;
        generator.ItemGroundOffset = 0.03f;

        generator.ExitMinDistanceFromStart = 12f;
        generator.ExitDoorPrefab = generatorExitDoorPrefab;
        generator.MaxPlacementAttempts = 360;
        generator.MaxFullGenerationAttempts = 12;
        generator.InsteadDoor = generatorBlockDoorPrefab != null ? generatorBlockDoorPrefab : generatorConnectedDoorPrefab;
        generator.DoorPrefabs = generatorDoorPrefabs;
        generator.CellPrefabs = ResolveGeneratorCellPrefabs();
        generator.StartRoomPrefabs = ResolveGeneratorStartRoomPrefabs();
        generator.ConnectedDoorPositionOffset = Vector3.zero;
        generator.ConnectedDoorRotationOffset = Vector3.zero;
        generator.BlockDoorPositionOffset = new Vector3(0f, 0f, -2f);
        generator.BlockDoorRotationOffset = Vector3.zero;
        generator.ExitDoorPositionOffset = Vector3.zero;
        generator.ExitDoorRotationOffset = Vector3.zero;
        generator.HideGeneratedVisualsInGame = true;
        generator.HideGeneratedVisualsOnlyInPlayMode = true;
        generator.UseMeshCollidersInsteadOfBoxColliders = true;
        generator.PreserveObjectiveComputersInGeneratedCells = true;
        generator.OptimizeGeneratedLightShadows = true;
        generator.DisableGeneratedLightShadows = true;
        generator.CreateFallbackBlockDoorWhenPrefabMissing = true;
        generator.BlockExtraExitObjectsByName = true;
        generator.ExtraExitObjectNameKeywords = new[] { "TempPortal", "DoorPoint" };
        generator.ClearPreviousGeneratedChildren = true;
        generator.UseBfsExpansion = true;
        generator.BfsDepthRandomSpread = 0;
        generator.MaxFailedAttemptsPerOpenExit = 2;
        generator.GeneratedCellsPerFrame = 4;

        int cellLayer = LayerMask.NameToLayer("Cell");
        generator.CellLayer = cellLayer >= 0 ? 1 << cellLayer : 64;
    }

    private Cell[] ResolveGeneratorCellPrefabs()
    {
        if (generatorCellPrefabs != null && generatorCellPrefabs.Length > 0)
        {
            return generatorCellPrefabs;
        }

        return new[] { commonRoomPrefab, citizenRoomPrefab, doppelgangerRoomPrefab, corridorPrefab };
    }

    private Cell[] ResolveGeneratorStartRoomPrefabs()
    {
        if (generatorStartRoomPrefabs != null && generatorStartRoomPrefabs.Length > 0)
        {
            return generatorStartRoomPrefabs;
        }

        return new[] { commonRoomPrefab, citizenRoomPrefab, doppelgangerRoomPrefab };
    }

    private GameObject[] ResolveGeneratorItemPrefabs()
    {
        if (generatorItemPrefabs != null && generatorItemPrefabs.Length > 0)
        {
            return generatorItemPrefabs;
        }

        return cameraPickupPrefab != null ? new[] { cameraPickupPrefab } : new GameObject[0];
    }

    private void CacheGeneratedWorldReferences()
    {
        Transform root = worldRoot != null ? worldRoot : transform;
        generatedComputers = root.GetComponentsInChildren<ObjectiveComputer>(true);
        generatedPickups = root.GetComponentsInChildren<WorldItemPickup>(true);
        tutorialExitDoor = root.GetComponentInChildren<EmergencyExitDoor>(true);

        if (tutorialExitDoor != null)
        {
            tutorialExitDoor.ResetDoorState(true);
            if (labObjectiveManager != null)
            {
                labObjectiveManager.RegisterExitDoor(tutorialExitDoor);
            }
        }

        Light[] generatedLights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < generatedLights.Length; i++)
        {
            if (generatedLights[i] != null)
            {
                generatedLights[i].enabled = false;
            }
        }

        Vector3 playerPosition = playerObject != null ? playerObject.transform.position : Vector3.zero;
        commonRoomCenter = playerPosition;
        citizenRoomCenter = playerPosition;
        doppelgangerRoomCenter = playerPosition;
    }

    private bool TryBuildInGamePrefabMap()
    {
        if (commonRoomPrefab == null || citizenRoomPrefab == null || doppelgangerRoomPrefab == null || corridorPrefab == null)
        {
            return false;
        }

        Cell commonRoom = SpawnCell(commonRoomPrefab, "Tutorial_CommonRoom", Vector3.zero, Quaternion.identity);
        Transform commonNorthExit = FindExitByLocalDirection(commonRoom, Vector3.forward);
        if (commonNorthExit == null)
        {
            return AbortInGamePrefabMap(commonRoom);
        }

        Cell firstCorridor = SpawnCell(corridorPrefab, "Tutorial_Corridor_CommonToCitizen", Vector3.zero, Quaternion.identity);
        Transform firstCorridorSouthExit = FindExitByLocalDirection(firstCorridor, Vector3.back);
        Transform firstCorridorNorthExit = FindExitByLocalDirection(firstCorridor, Vector3.forward);
        if (firstCorridorSouthExit == null || firstCorridorNorthExit == null)
        {
            return AbortInGamePrefabMap(commonRoom, firstCorridor);
        }

        AlignCellToExit(firstCorridor, firstCorridorSouthExit, commonNorthExit);

        Cell citizenRoom = SpawnCell(citizenRoomPrefab, "Tutorial_CitizenRoom", Vector3.zero, Quaternion.identity);
        Transform citizenSouthExit = FindExitByLocalDirection(citizenRoom, Vector3.back);
        Transform citizenNorthExit = FindExitByLocalDirection(citizenRoom, Vector3.forward);
        if (citizenSouthExit == null || citizenNorthExit == null)
        {
            return AbortInGamePrefabMap(commonRoom, firstCorridor, citizenRoom);
        }

        AlignCellToExit(citizenRoom, citizenSouthExit, firstCorridorNorthExit);

        Cell secondCorridor = SpawnCell(corridorPrefab, "Tutorial_Corridor_CitizenToDoppelganger", Vector3.zero, Quaternion.identity);
        Transform secondCorridorSouthExit = FindExitByLocalDirection(secondCorridor, Vector3.back);
        Transform secondCorridorNorthExit = FindExitByLocalDirection(secondCorridor, Vector3.forward);
        if (secondCorridorSouthExit == null || secondCorridorNorthExit == null)
        {
            return AbortInGamePrefabMap(commonRoom, firstCorridor, citizenRoom, secondCorridor);
        }

        AlignCellToExit(secondCorridor, secondCorridorSouthExit, citizenNorthExit);

        Cell doppelgangerRoom = SpawnCell(doppelgangerRoomPrefab, "Tutorial_DoppelgangerRoom", Vector3.zero, Quaternion.identity);
        Transform doppelgangerSouthExit = FindExitByLocalDirection(doppelgangerRoom, Vector3.back);
        if (doppelgangerSouthExit == null)
        {
            return AbortInGamePrefabMap(commonRoom, firstCorridor, citizenRoom, secondCorridor, doppelgangerRoom);
        }

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

    private bool AbortInGamePrefabMap(params Cell[] spawnedCells)
    {
        if (spawnedCells != null)
        {
            for (int i = 0; i < spawnedCells.Length; i++)
            {
                if (spawnedCells[i] != null)
                {
                    Destroy(spawnedCells[i].gameObject);
                }
            }
        }

        return false;
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

    private void BuildTutorialLoadingUi()
    {
        GameObject canvasObject = new GameObject("TutorialLoadingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        tutorialLoadingCanvas = canvasObject.GetComponent<Canvas>();
        tutorialLoadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tutorialLoadingCanvas.overrideSorting = true;
        tutorialLoadingCanvas.sortingOrder = 2000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        tutorialLoadingGroup = canvasObject.GetComponent<CanvasGroup>();
        tutorialLoadingGroup.alpha = 1f;
        tutorialLoadingGroup.blocksRaycasts = true;
        tutorialLoadingGroup.interactable = true;

        RectTransform root = CreateRect("Root", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        AddImage(root, Color.black);

        RectTransform panel = CreateRect("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        panel.sizeDelta = new Vector2(760f, 260f);
        AddImage(panel, new Color(0f, 0f, 0f, 0.72f));
        AddOutline(panel, new Color(0.62f, 0.86f, 0.92f, 0.38f), new Vector2(1.6f, -1.6f));

        TMP_Text title = CreateLabel("튜토리얼 준비 중", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), 34f, new Color(0.88f, 0.98f, 1f, 1f), TextAlignmentOptions.Center);
        title.rectTransform.sizeDelta = new Vector2(-80f, 56f);

        tutorialLoadingText = CreateLabel("튜토리얼 맵 생성 중...", panel, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), 22f, new Color(0.72f, 0.9f, 0.96f, 1f), TextAlignmentOptions.Center);
        tutorialLoadingText.rectTransform.sizeDelta = new Vector2(-100f, 44f);
        tutorialLoadingText.textWrappingMode = TextWrappingModes.Normal;

        RectTransform bar = CreateRect("ProgressBar", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f));
        bar.sizeDelta = new Vector2(520f, 12f);
        AddImage(bar, new Color(0.1f, 0.16f, 0.18f, 1f));

        RectTransform fill = CreateRect("ProgressFill", bar, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        tutorialLoadingFill = AddImage(fill, new Color(0.38f, 0.86f, 1f, 1f));

        canvasObject.SetActive(false);
    }

    private void BuildPromptUi()
    {
        GameObject canvasObject = new GameObject("TutorialPromptCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas promptCanvas = canvasObject.GetComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        promptCanvas.overrideSorting = true;
        promptCanvas.sortingOrder = 250;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = CreateRect("Root", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        promptText = CreateLabel("", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 148f), 24f, new Color(0.96f, 0.98f, 1f, 1f), TextAlignmentOptions.Center);
        promptText.rectTransform.sizeDelta = new Vector2(800f, 42f);
        promptText.gameObject.SetActive(false);
    }

    private void ShowTutorialLoading(string message, float progress)
    {
        if (tutorialLoadingCanvas != null)
        {
            tutorialLoadingCanvas.gameObject.SetActive(true);
        }

        if (tutorialLoadingGroup != null)
        {
            tutorialLoadingGroup.alpha = 1f;
            tutorialLoadingGroup.blocksRaycasts = true;
            tutorialLoadingGroup.interactable = true;
        }

        GameplayStartupGate.SetLoadingScreenBlocked(true);
        SetTutorialLoadingProgress(message, progress);
    }

    private void HideTutorialLoading()
    {
        GameplayStartupGate.SetLoadingScreenBlocked(false);

        if (tutorialLoadingCanvas != null)
        {
            tutorialLoadingCanvas.gameObject.SetActive(false);
        }
    }

    private void HandleTutorialLoadingPhaseChanged(string message, float progress)
    {
        SetTutorialLoadingProgress(GetTutorialLoadingMessage(message), progress);
    }

    private void SetTutorialLoadingProgress(string message, float progress)
    {
        if (tutorialLoadingText != null)
        {
            tutorialLoadingText.text = string.IsNullOrWhiteSpace(message) ? "튜토리얼 맵 생성 중..." : message;
        }

        if (tutorialLoadingFill != null)
        {
            RectTransform fillRect = tutorialLoadingFill.rectTransform;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }
    }

    private string GetTutorialLoadingMessage(string message)
    {
        switch (message)
        {
            case "SCANNING AREA...":
                return "튜토리얼 구역 스캔 중...";
            case "BUILDING PATHS...":
                return "작은 실험실 맵 생성 중...";
            case "CLOSING PATHS...":
                return "막힌 출구 정리 중...";
            case "CALIBRATING SCANNER...":
                return "스캐너 표면 보정 중...";
            case "SYNCING PLAYERS...":
                return "플레이어 위치 설정 중...";
            case "PLACING SIGNALS...":
                return "아이템과 탈출구 배치 중...";
            case "SCAN READY":
                return "튜토리얼 진입 중...";
            case "SCAN FAILED":
                return "튜토리얼 맵 생성 실패";
            default:
                return string.IsNullOrWhiteSpace(message) ? "튜토리얼 맵 생성 중..." : message;
        }
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

        TMP_Text finishBody = CreateLabel("이동, 스캔, 아이템 사용, 컴퓨터 복구, 탈출 흐름을 완료했습니다.", finishPanel, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), 24f, new Color(0.9f, 0.96f, 0.97f, 1f), TextAlignmentOptions.Center);
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
        labObjectiveManager.objectiveText = null;
        labObjectiveManager.promptText = promptText;
        labObjectiveManager.requiredComputerCount = 1;
    }

    private void BuildPlayer()
    {
        playerObject = new GameObject("TutorialPlayer");
        playerObject.transform.position = GetRoomPoint(commonRoomCenter, 0f, -1.8f, 0.12f);
        playerObject.transform.rotation = Quaternion.identity;
        int playerScanLayer = LayerMask.NameToLayer("PlayerScan");
        if (playerScanLayer >= 0)
        {
            playerObject.layer = playerScanLayer;
        }

        CharacterController controller = playerObject.AddComponent<CharacterController>();
        controller.radius = 0.3f;
        controller.height = 1.8f;
        controller.center = Vector3.zero;
        controller.stepOffset = 0.3f;
        controller.skinWidth = 0.05f;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.transform.SetParent(playerObject.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 2.05f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;
        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.fieldOfView = 75f;
        camera.nearClipPlane = 0.02f;
        camera.farClipPlane = 64f;
        cameraObject.AddComponent<AudioListener>();
        CreateAmbientAudio();

        dotRenderer = cameraObject.AddComponent<InstancedScanDotRenderer>();
        dotRenderer.SetSourceDotPrefab(dotSourcePrefab);

        AudioSource scanAudioSource = cameraObject.AddComponent<AudioSource>();
        ConfigureAudioSource(scanAudioSource, scanPulseClip, 0.08f, 2f, false, 0f, 1f, 500f);
        LidarSpotScanner scanner = cameraObject.AddComponent<LidarSpotScanner>();
        scanner.ApplyInGameDefaults();

        PlayerStats stats = playerObject.AddComponent<PlayerStats>();
        playerTarget = playerObject.AddComponent<PlayerCombatTarget>();
        playerTarget.SetRole(PlayerRole.Citizen);
        playerTarget.photonActorNumber = 1;

        playerInventory = playerObject.AddComponent<PlayerInventory>();

        PlayerObjectiveInteractor interactor = playerObject.AddComponent<PlayerObjectiveInteractor>();
        interactor.playerCamera = camera;
        interactor.promptText = promptText;
        interactor.interactDistance = 4.2f;
        interactor.lookAssistRadius = 0.65f;
        interactor.groundItemPickupRadius = 3.4f;

        PlayerItemUser itemUser = playerObject.AddComponent<PlayerItemUser>();
        itemUser.inventory = playerInventory;
        itemUser.playerCamera = camera;
        itemUser.dotRenderer = dotRenderer;
        itemUser.playerStats = stats;
        itemUser.selfTarget = playerTarget;
        itemUser.cameraDropPrefab = cameraPickupPrefab;
        itemUser.knifeDropPrefab = ResolveItemPrefab(ItemType.Knife);
        itemUser.medkitDropPrefab = ResolveItemPrefab(ItemType.Medkit);
        itemUser.cameraPointCount = 3500;
        itemUser.cameraMaxDistance = 100f;
        itemUser.cameraScreenHalfWidth = 0.48f;
        itemUser.cameraScreenHalfHeight = 0.36f;
        itemUser.cameraAttemptsPerPoint = 14;
        itemUser.cameraSurfaceOffset = 0.012f;
        itemUser.scanMask = GetInGameScanMask();
        itemUser.knifeDamage = 50f;
        itemUser.knifeAttackDistance = 1.55f;
        itemUser.knifeAttackRadius = 0.55f;
        itemUser.allowFriendlyFire = true;
        itemUser.consumeKnifeWhenMissed = false;
        itemUser.medkitHealAmount = 50f;
        itemUser.restoreStaminaWithMedkit = false;

        PlayerMotor motor = playerObject.AddComponent<PlayerMotor>();
        motor.playerCamera = cameraObject.transform;
        motor.playerStats = stats;
        motor.walkSpeed = 2f;
        motor.sprintSpeed = 3.5f;
        motor.crouchSpeed = 0.85f;
        motor.gravity = -24f;
        motor.groundedStickForce = -7f;
        motor.groundedStepOffset = 0.4f;
        motor.standingCameraHeight = 2.05f;
        motor.crouchingCameraHeight = 1.32f;
        motor.standingControllerHeight = 2.2f;
        motor.crouchingControllerHeight = 1.5f;
        CreateFootstepAudio(motor, controller);

        MouseLook mouseLook = playerObject.AddComponent<MouseLook>();
        mouseLook.playerCamera = cameraObject.transform;
        mouseLook.maxLookUpAngle = 60f;

        KillerAttack killerAttack = playerObject.AddComponent<KillerAttack>();
        killerAttack.playerCamera = cameraObject.transform;
        killerAttack.attackDistance = 1.6f;
        killerAttack.attackRadius = 0.6f;
        killerAttack.attackDelay = 0.08f;
        killerAttack.attackCooldown = 0.9f;
        killerAttack.canAttack = true;

        PlayerHUDController hud = playerObject.AddComponent<PlayerHUDController>();
        hud.targetStats = stats;
        hud.targetScanner = scanner;
        hud.targetInventory = playerInventory;
        hud.targetDotRenderer = dotRenderer;
        hud.targetCombatTarget = playerTarget;
        hud.cooldownReadyAlpha = 0.22f;
        hud.cooldownActiveAlpha = 0.9f;
        hud.buildRuntimeHud = true;
        hud.vitalSegmentCount = 12;
        hud.staminaSegmentCount = 14;
        hud.dotMeterSegmentCount = 28;
        hud.roleRevealDuration = 4f;
        itemUser.hudController = hud;
    }

    private void CreateAmbientAudio()
    {
        if (playerObject == null || ambientLoopClip == null)
        {
            return;
        }

        GameObject ambientObject = new GameObject("BGMPlayer");
        ambientObject.transform.SetParent(playerObject.transform, false);
        ambientObject.transform.localPosition = Vector3.zero;

        AudioSource source = ambientObject.AddComponent<AudioSource>();
        ConfigureAudioSource(source, ambientLoopClip, 0.1f, 1f, true, 0f, 1f, 500f);
        source.Play();
    }

    private void CreateFootstepAudio(PlayerMotor motor, CharacterController controller)
    {
        if (playerObject == null)
        {
            return;
        }

        GameObject footstepObject = new GameObject("FootstepAudio");
        footstepObject.transform.SetParent(playerObject.transform, false);
        footstepObject.transform.localPosition = new Vector3(0f, 0.25f, 0f);

        AudioSource source = footstepObject.AddComponent<AudioSource>();
        ConfigureAudioSource(source, null, 0.4f, 1f, false, 1f, 1.5f, 12f);
        source.dopplerLevel = 0f;

        PlayerFootstepAudio footsteps = footstepObject.AddComponent<PlayerFootstepAudio>();
        footsteps.playerRoot = playerObject.transform;
        footsteps.playerMotor = motor;
        footsteps.characterController = controller;
        footsteps.groundMask = GetInGameScanMask();
        footsteps.useGroundRaycastFallback = true;
        footsteps.commonClips = footstepClips != null ? footstepClips : new AudioClip[0];
        footsteps.walkStepDistance = 0.85f;
        footsteps.sprintStepDistance = 0.95f;
        footsteps.crouchStepDistance = 0.60f;
        footsteps.walkVolume = 0.90f;
        footsteps.sprintVolume = 1.00f;
        footsteps.crouchVolume = 0.55f;
        footsteps.broadcastFootstepsToNetwork = false;
    }

    private void ConfigureAudioSource(
        AudioSource source,
        AudioClip clip,
        float volume,
        float pitch,
        bool loop,
        float spatialBlend,
        float minDistance,
        float maxDistance)
    {
        if (source == null)
        {
            return;
        }

        source.clip = clip;
        source.playOnAwake = false;
        source.loop = loop;
        source.volume = volume;
        source.pitch = pitch;
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.dopplerLevel = spatialBlend > 0f ? 1f : 0f;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
    }

    private LayerMask GetInGameScanMask()
    {
        int cellsLayer = LayerMask.NameToLayer("cells");
        int revealLayer = LayerMask.NameToLayer("RevealSurface");
        int mask = 0;

        if (cellsLayer >= 0)
        {
            mask |= 1 << cellsLayer;
        }

        if (revealLayer >= 0)
        {
            mask |= 1 << revealLayer;
        }

        return mask != 0 ? mask : ~0;
    }

    private GameObject ResolveItemPrefab(ItemType itemType)
    {
        if (generatorItemPrefabs == null)
        {
            return null;
        }

        for (int i = 0; i < generatorItemPrefabs.Length; i++)
        {
            GameObject prefab = generatorItemPrefabs[i];
            if (prefab == null)
            {
                continue;
            }

            WorldItemPickup pickup = prefab.GetComponent<WorldItemPickup>();
            if (pickup == null)
            {
                pickup = prefab.GetComponentInChildren<WorldItemPickup>(true);
            }

            if (pickup != null && pickup.itemType == itemType)
            {
                return prefab;
            }
        }

        return null;
    }

    private void BuildTrainingObjects()
    {
        List<ObjectiveComputer> generatedMapComputers = CollectSortedGeneratedComputers();

        if (generatedMapComputers.Count < RequiredTutorialComputerCount)
        {
            Debug.LogError(
                "[TutorialSceneController] Tutorial needs generated room computer tables, but the map only has " +
                generatedMapComputers.Count + ". Check room prefabs that contain ObjectiveComputer."
            );
            return;
        }

        commonComputer = generatedMapComputers[0];
        citizenTargetA = generatedMapComputers[1];
        citizenTargetB = generatedMapComputers[2];
        citizenWrongComputer = generatedMapComputers.Count > 3 ? generatedMapComputers[3] : commonComputer;
        doppelgangerComputer = citizenTargetB != null ? citizenTargetB : commonComputer;

        ConfigureObjectiveComputer(commonComputer, false, 1);
        ConfigureObjectiveComputer(citizenTargetA, false, 2);
        ConfigureObjectiveComputer(citizenTargetB, false, 3);

        if (citizenWrongComputer != null)
        {
            ConfigureObjectiveComputer(citizenWrongComputer, false, 4);
        }

        cameraPickup = ResolveCameraPickup();
        MovePickupNearPlayer(cameraPickup);

        blendInStation = doppelgangerComputer != null
            ? doppelgangerComputer.transform
            : commonComputer != null
                ? commonComputer.transform
                : playerObject != null
                    ? playerObject.transform
                    : null;
        alibiStation = commonComputer != null ? commonComputer.transform : blendInStation;

        GameLoopManager.EnsureExists().RebuildComputerIndex();
    }

    private bool HasRequiredTutorialComputers()
    {
        return commonComputer != null &&
               citizenTargetA != null &&
               citizenTargetB != null;
    }

    private List<ObjectiveComputer> CollectSortedGeneratedComputers()
    {
        List<ObjectiveComputer> result = new List<ObjectiveComputer>();

        if (generatedComputers != null)
        {
            for (int i = 0; i < generatedComputers.Length; i++)
            {
                if (generatedComputers[i] != null && !result.Contains(generatedComputers[i]))
                {
                    ConfigureObjectiveComputer(generatedComputers[i], false, i + 1);
                    result.Add(generatedComputers[i]);
                }
            }
        }

        Vector3 origin = playerObject != null ? playerObject.transform.position : Vector3.zero;
        result.Sort((left, right) =>
        {
            float leftDistance = left != null ? Vector3.SqrMagnitude(left.transform.position - origin) : float.MaxValue;
            float rightDistance = right != null ? Vector3.SqrMagnitude(right.transform.position - origin) : float.MaxValue;
            return leftDistance.CompareTo(rightDistance);
        });

        return result;
    }

    private WorldItemPickup ResolveCameraPickup()
    {
        WorldItemPickup bestPickup = null;
        float bestDistance = float.MaxValue;
        Vector3 origin = playerObject != null ? playerObject.transform.position : Vector3.zero;

        if (generatedPickups != null)
        {
            for (int i = 0; i < generatedPickups.Length; i++)
            {
                WorldItemPickup pickup = generatedPickups[i];
                if (pickup == null || pickup.itemType != ItemType.Camera)
                {
                    continue;
                }

                float distance = Vector3.SqrMagnitude(pickup.transform.position - origin);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPickup = pickup;
                }
            }
        }

        if (bestPickup != null)
        {
            return bestPickup;
        }

        return CreatePickup(ItemType.Camera, GetPointNearPlayer(1.75f, -0.95f));
    }

    private void MovePickupNearPlayer(WorldItemPickup pickup)
    {
        if (pickup == null || playerObject == null)
        {
            return;
        }

        pickup.transform.position = GetPointNearPlayer(1.65f, -0.85f);
        HideRenderers(pickup.gameObject, true);
        PrepareTutorialObjectForScan(pickup.gameObject, ScanSurfaceType.Item);
    }

    private Vector3 GetPointNearPlayer(float forwardOffset, float rightOffset)
    {
        Transform playerTransform = playerObject != null ? playerObject.transform : null;
        if (playerTransform == null)
        {
            return new Vector3(rightOffset, 0.08f, forwardOffset);
        }

        Vector3 forward = playerTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 position = playerTransform.position + forward * forwardOffset + right * rightOffset;
        position.y += 0.08f;
        return position;
    }

    private Vector3 GetPointNearTransform(Transform target, float forwardOffset, float rightOffset)
    {
        if (target == null)
        {
            return GetPointNearPlayer(forwardOffset, rightOffset);
        }

        Vector3 forward = target.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 position = target.position + forward * forwardOffset + right * rightOffset;
        position.y = Mathf.Max(position.y, target.position.y) + 0.08f;
        return position;
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
        ResetTutorialExitDoor(true);
        commonComputer.SetSelectedObjective(true, true);
        citizenTargetA.SetSelectedObjective(true, true);
        citizenTargetB.SetSelectedObjective(true, true);
        labObjectiveManager.SetupComputerObjectives(new[] { commonComputer, citizenTargetA, citizenTargetB }, 3);
        if (commonGate != null)
        {
            commonGate.SetUnlocked(false);
        }

        if (citizenGate != null)
        {
            citizenGate.SetUnlocked(false);
        }

        SetCommonStep(CommonStep.Move);
    }

    private void EnterCitizenStage()
    {
        currentStage = TutorialStage.Citizen;
        citizenSabotageIssued = false;
        playerTarget.SetRole(PlayerRole.Citizen);
        ResetTutorialExitDoor(true);

        citizenTargetA.SetSelectedObjective(true, true);
        citizenTargetB.SetSelectedObjective(true, true);
        citizenWrongComputer.SetSelectedObjective(false, true);
        labObjectiveManager.SetupComputerObjectives(new[] { citizenTargetA, citizenTargetB }, 2);
        if (citizenGate != null)
        {
            citizenGate.SetUnlocked(false);
        }

        SetCitizenStep(CitizenStep.FindFirstTarget);
    }

    private void EnterDoppelgangerStage()
    {
        currentStage = TutorialStage.Doppelganger;
        playerTarget.SetRole(PlayerRole.Killer);
        doppelgangerComputer.SetSelectedObjective(true, true);
        doppelgangerComputer.ApplyRestoredFromNetwork();
        MovePlayerNearDoppelgangerPractice();
        blendTimer = 0f;
        alibiTimer = 0f;
        SetDoppelgangerStep(DoppelgangerStep.BlendIn);
    }

    private void ResetTutorialExitDoor(bool locked)
    {
        if (tutorialExitDoor != null)
        {
            tutorialExitDoor.ResetDoorState(locked);
        }
    }

    private bool IsTutorialExitOpen()
    {
        if (tutorialExitDoor != null)
        {
            return tutorialExitDoor.IsOpen;
        }

        if (commonGate != null && currentStage == TutorialStage.Common)
        {
            return commonGate.IsOpen;
        }

        if (citizenGate != null && currentStage == TutorialStage.Citizen)
        {
            return citizenGate.IsOpen;
        }

        return false;
    }

    private void MovePlayerNearDoppelgangerPractice()
    {
        if (playerObject == null || doppelgangerComputer == null)
        {
            return;
        }

        CharacterController controller = playerObject.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        playerObject.transform.position = GetPointNearTransform(doppelgangerComputer.transform, -2.1f, 0.2f);
        Vector3 lookDirection = doppelgangerComputer.transform.position - playerObject.transform.position;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            playerObject.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        if (controller != null)
        {
            controller.enabled = true;
        }
    }

    private void SetCommonStep(CommonStep step)
    {
        currentStep = (int)step;
        ResetStepTracking();

        if (step == CommonStep.OpenExit)
        {
            if (commonGate != null)
            {
                commonGate.SetUnlocked(true);
            }

            if (labObjectiveManager != null && !labObjectiveManager.ExitUnlocked)
            {
                labObjectiveManager.UnlockExit();
            }
        }
    }

    private void SetCitizenStep(CitizenStep step)
    {
        currentStep = (int)step;
        ResetStepTracking();

        if (step == CitizenStep.Escape)
        {
            if (citizenGate != null)
            {
                citizenGate.SetUnlocked(true);
            }

            if (labObjectiveManager != null && !labObjectiveManager.ExitUnlocked)
            {
                labObjectiveManager.UnlockExit();
            }
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
            SetCommonStep(CommonStep.RepairFirstComputer);
            return;
        }

        int restoredComputerCount = GetCommonRestoredComputerCount();

        if (step == CommonStep.RepairFirstComputer && restoredComputerCount >= 1)
        {
            SetCommonStep(CommonStep.RepairSecondComputer);
            return;
        }

        if (step == CommonStep.RepairSecondComputer && restoredComputerCount >= 2)
        {
            SetCommonStep(CommonStep.RepairThirdComputer);
            return;
        }

        if (step == CommonStep.RepairThirdComputer && restoredComputerCount >= 3)
        {
            SetCommonStep(CommonStep.OpenExit);
            return;
        }

        if (step == CommonStep.OpenExit && IsTutorialExitOpen())
        {
            EnterCitizenStage();
        }
    }

    private int GetCommonRestoredComputerCount()
    {
        int count = 0;

        if (commonComputer != null && commonComputer.IsRestored)
        {
            count++;
        }

        if (citizenTargetA != null && citizenTargetA.IsRestored)
        {
            count++;
        }

        if (citizenTargetB != null && citizenTargetB.IsRestored)
        {
            count++;
        }

        return count;
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
            return;
        }

        if (step == CitizenStep.Escape && IsTutorialExitOpen())
        {
            EnterDoppelgangerStage();
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
        if (labObjectiveManager != null)
        {
            labObjectiveManager.SetHudOverride("튜토리얼 완료", "이동, 스캔, 아이템 사용, 컴퓨터 복구, 탈출 흐름을 완료했습니다. ESC로 메뉴를 열 수 있습니다.", 1f);
        }

        if (promptText != null)
        {
            promptText.text = "튜토리얼 완료";
            promptText.gameObject.SetActive(true);
        }

        if (finishPanel != null)
        {
            finishPanel.gameObject.SetActive(true);
        }

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
        string title;
        string body;
        string step;
        string objective;

        GetCurrentTexts(out title, out body, out step, out objective);

        if (labObjectiveManager != null)
        {
            labObjectiveManager.SetHudOverride(objective, step, GetTutorialProgress01());
        }

        if (titleText != null)
        {
            titleText.text = title;
        }

        if (bodyText != null)
        {
            bodyText.text = body;
        }

        if (stepText != null)
        {
            stepText.text = step;
        }

        if (objectiveText != null)
        {
            objectiveText.text = objective;
        }

        if (roleText != null)
        {
            roleText.text = playerTarget != null && playerTarget.role == PlayerRole.Killer ? "역할: 도플갱어" : "역할: 시민";
        }
    }

    private float GetTutorialProgress01()
    {
        int completedSteps = 0;
        int totalSteps = 8;

        switch (currentStage)
        {
            case TutorialStage.Common:
                completedSteps = Mathf.Clamp(currentStep, 0, 8);
                break;

            case TutorialStage.Citizen:
                completedSteps = 8 + Mathf.Clamp(currentStep, 0, 5);
                break;

            case TutorialStage.Doppelganger:
                completedSteps = 13 + Mathf.Clamp(currentStep, 0, 4);
                break;

            case TutorialStage.Complete:
                completedSteps = totalSteps;
                break;
        }

        return totalSteps > 0 ? Mathf.Clamp01(completedSteps / (float)totalSteps) : 0f;
    }

    private void GetCurrentTexts(out string title, out string body, out string step, out string objective)
    {
        title = "";
        body = "";
        step = "";
        objective = "";

        if (currentStage == TutorialStage.Common)
        {
            title = "튜토리얼: 1인 실전 훈련";
            body = "labor 씬의 인게임 시스템 그대로 작은 맵에서 스폰, 스캔, 아이템, 컴퓨터 3개 복구, 탈출을 익힙니다.";
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
                    step = "스캔으로 드러난 카메라 아이템을 보고 " + GameInputBindings.FormatKey(GameInputBindings.Pickup) + "로 줍습니다.";
                    objective = "아이템 줍기";
                    break;
                case CommonStep.UseItem:
                    step = GameInputBindings.FormatKey(GameInputBindings.UseItem) + "으로 카메라 아이템을 사용하세요.";
                    objective = "아이템 사용";
                    break;
                case CommonStep.RepairFirstComputer:
                    step = "목표 컴퓨터를 찾아 " + GameInputBindings.FormatKey(GameInputBindings.Interact) + "를 길게 눌러 복구하세요.";
                    objective = "목표 컴퓨터 " + GetCommonRestoredComputerCount() + "/3";
                    break;
                case CommonStep.RepairSecondComputer:
                    step = "스캔으로 다른 목표 컴퓨터를 찾고 " + GameInputBindings.FormatKey(GameInputBindings.Interact) + "를 길게 눌러 복구하세요.";
                    objective = "목표 컴퓨터 " + GetCommonRestoredComputerCount() + "/3";
                    break;
                case CommonStep.RepairThirdComputer:
                    step = "마지막 목표 컴퓨터를 복구하면 탈출문이 열립니다.";
                    objective = "목표 컴퓨터 " + GetCommonRestoredComputerCount() + "/3";
                    break;
                case CommonStep.OpenExit:
                    step = "탈출문을 찾아 " + GameInputBindings.FormatKey(GameInputBindings.Interact) + "로 열면 시민 실전으로 이어집니다.";
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

    private void ConfigureObjectiveComputer(ObjectiveComputer computer, bool selected, int networkId)
    {
        if (computer == null)
        {
            return;
        }

        computer.restoreDuration = tutorialComputerHoldDuration;
        computer.sabotageDuration = tutorialSabotageDuration;
        computer.sabotagedRepairDuration = tutorialSabotagedRepairDuration;
        computer.maxInteractorMoveDistance = 0.45f;
        computer.requireCitizenRole = false;
        computer.preventKillerRestore = true;
        computer.allowKillerSabotage = true;
        computer.preventSabotageAfterExitUnlocked = false;
        computer.existingDotRecolorRadius = 1.5f;
        computer.existingDotRecolorCenter = computer.transform;
        computer.SetNetworkObjectiveId(networkId);
        computer.SetSelectedObjective(selected, true);
        ConfigureComputerAudio(computer);
    }

    private void ConfigureComputerAudio(ObjectiveComputer computer)
    {
        if (computer == null)
        {
            return;
        }

        Transform root = computer.transform;
        computer.startAudioSource = EnsureComputerAudioSource(root, "ComputerStartAudio", computer.startAudioSource, computerStartClip, 0.85f, 1f, false);
        computer.loopAudioSource = EnsureComputerAudioSource(root, "ComputerProgressAudio", computer.loopAudioSource, computerProgressClip, 0.55f, 1f, false);
        computer.completeAudioSource = EnsureComputerAudioSource(root, "ComputerSuccessAudio", computer.completeAudioSource, computerSuccessClip, 0.9f, 1f, false);
        computer.fakeCompleteAudioSource = EnsureComputerAudioSource(root, "ComputerFakeAudio", computer.fakeCompleteAudioSource, computerFakeClip, 0.85f, 1f, false);
        computer.sabotageAudioSource = EnsureComputerAudioSource(root, "ComputerSabotageAudio", computer.sabotageAudioSource, computerFakeClip, 0.85f, 0.82f, false);
        computer.loopProgressAudio = false;
        computer.restartProgressAudioOnBegin = true;
    }

    private AudioSource EnsureComputerAudioSource(
        Transform root,
        string objectName,
        AudioSource existingSource,
        AudioClip clip,
        float volume,
        float pitch,
        bool loop)
    {
        AudioSource source = existingSource;

        if (source == null)
        {
            GameObject audioObject = new GameObject(objectName);
            audioObject.transform.SetParent(root, false);
            audioObject.transform.localPosition = Vector3.zero;
            source = audioObject.AddComponent<AudioSource>();
        }

        ConfigureAudioSource(source, clip, volume, pitch, loop, 1f, 1.5f, 14f);
        return source;
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
        PrepareTutorialObjectForScan(itemObject, ScanSurfaceType.Item);
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
            ApplyRevealSurfaceLayer(body);
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
        ApplyRevealSurfaceLayer(marker);
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
        ApplyRevealSurfaceLayer(cube);
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
        ApplyRevealSurfaceLayer(cube);
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

    private void ApplyRevealSurfaceLayer(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        int revealLayer = LayerMask.NameToLayer("RevealSurface");
        if (revealLayer >= 0)
        {
            Transform[] children = target.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null)
                {
                    children[i].gameObject.layer = revealLayer;
                }
            }
        }
    }

    private void PrepareTutorialObjectForScan(GameObject root, ScanSurfaceType fallbackSurfaceType)
    {
        if (root == null)
        {
            return;
        }

        DisableSimpleRandomComponents(root);
        ApplyRevealSurfaceLayer(root);
        EnsureScanSurfaceInfo(root, fallbackSurfaceType);
        EnsureMeshColliders(root);
    }

    private void DisableSimpleRandomComponents(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        SimpleRandom[] randomizers = root.GetComponentsInChildren<SimpleRandom>(true);
        for (int i = 0; i < randomizers.Length; i++)
        {
            if (randomizers[i] != null)
            {
                randomizers[i].enabled = false;
            }
        }
    }

    private void EnsureMeshColliders(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];

            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            Renderer renderer = meshFilter.GetComponent<Renderer>();
            if (renderer == null)
            {
                continue;
            }

            if (!meshFilter.sharedMesh.isReadable)
            {
                continue;
            }

            MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
            }

            meshCollider.sharedMesh = meshFilter.sharedMesh;
            Rigidbody attachedRigidbody = meshFilter.GetComponentInParent<Rigidbody>();
            meshCollider.convex = attachedRigidbody != null && !attachedRigidbody.isKinematic;
            meshCollider.isTrigger = false;
            meshCollider.enabled = true;
        }
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

    private void HideRenderers(GameObject root, bool hide)
    {
        if (!hide || root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
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
