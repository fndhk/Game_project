using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 이 스크립트는 우클릭 시 화면 맨 위에서 아래로 내려오는 초록색 스캔선을 표시하고,
// 스캔선이 지나간 구간만 순서대로 Raycast 해서 흰 점 데이터를 만든다.
// 핵심 목표는 "선이 먼저 내려오고, 그 뒤에 점이 찍히는 느낌"을 안정적으로 만드는 것이다.
public class PlayerRevealTrail : MonoBehaviour
{
    [Header("References")]
    // 스캔 기준이 되는 Transform이다.
    // 보통 플레이어 본체나 카메라 부모를 넣으면 된다.
    public Transform scanOrigin;

    // 화면 기준 Ray를 쏠 카메라이다.
    // 비어 있으면 자동으로 Main Camera를 찾는다.
    public Camera scanCamera;

    [Header("Input")]
    // 우클릭은 기본적으로 1번이다.
    public int scanMouseButton = 1;

    // 다음 스캔을 다시 쓸 수 있기까지의 쿨타임이다.
    public float scanCooldown = 1.2f;

    [Header("Dot Settings")]
    // 생성될 흰 점의 크기이다.
    public float dotSize = 0.03f;

    // 같은 칸 안에서는 점이 중복 생성되지 않게 막기 위한 셀 크기이다.
    public float cellSize = 0.06f;

    // 새 스캔을 시작할 때 이전 점을 지울지 정한다.
    public bool clearDotsBeforeScan = true;

    [Header("Sweep Scan")]
    // 스캔선이 화면 맨 위에서 아래까지 내려가는 전체 시간이다.
    public float sweepDuration = 1.1f;

    // 한 번의 프레임 스텝에서 세로 방향으로 몇 줄을 샘플링할지 정한다.
    // 너무 낮으면 점이 성기고, 너무 높으면 한꺼번에 너무 많이 찍힐 수 있다.
    public int rowsPerStep = 4;

    // 한 줄마다 좌우로 몇 번 Raycast를 쏠지 정한다.
    public int raysPerRow = 180;

    // 최대 스캔 거리이다.
    public float maxRevealDistance = 16f;

    // 화면 좌우 끝을 조금 비워 두기 위한 여백이다.
    [Range(0f, 0.2f)]
    public float horizontalViewportMargin = 0.03f;

    // 현재 스캔선이 차지하는 세로 두께를 viewport 비율로 정한다.
    // 값이 너무 크면 선보다 면처럼 보이고,
    // 너무 작으면 지나가는 느낌이 약해질 수 있다.
    [Range(0.002f, 0.12f)]
    public float scanBandThickness = 0.035f;

    [Header("Screen Scan Line")]
    // 실제 화면에 보일 초록색 선의 픽셀 높이이다.
    public float scanLineScreenHeight = 18f;

    // 선의 색상이다.
    public Color scanLineColor = new Color(0.05f, 1f, 0.2f, 1f);

    // 선의 투명도이다.
    [Range(0f, 1f)]
    public float scanLineAlpha = 0.95f;

    [Header("Layers")]
    // 점을 찍을 대상 레이어이다.
    public LayerMask revealSurfaceMask = ~0;

    [Header("State")]
    // DotVisionRenderer가 읽어 갈 점 데이터 리스트이다.
    public List<RevealDot> myDots = new List<RevealDot>();

    // 현재 스캔 중인지 확인하는 값이다.
    public bool IsScanning { get; private set; }

    // 다음 스캔 가능 시각이다.
    private float nextScanTime = 0f;

    // 중복 점 생성을 막기 위한 셀 기록이다.
    private readonly HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();

    // 런타임에 만들 화면용 스캔선 Canvas이다.
    private Canvas runtimeCanvas;

    // 런타임에 만들 화면용 스캔선 RectTransform이다.
    private RectTransform runtimeLineRect;

    // 런타임에 만들 화면용 스캔선 Image이다.
    private Image runtimeLineImage;

    // 시작 시 필요한 참조와 화면 스캔선을 준비한다.
    private void Awake()
    {
        // scanOrigin이 비어 있으면 자기 자신을 기준으로 사용한다.
        if (scanOrigin == null)
        {
            scanOrigin = transform;
        }

        // 스캔 카메라를 찾는다.
        ResolveCamera();

        // 초록색 스캔선을 화면 UI로 만든다.
        CreateRuntimeScreenLine();

        // 시작할 때는 보이지 않게 숨긴다.
        HideScanLine();
    }

    // 매 프레임 입력과 참조 상태를 확인한다.
    private void Update()
    {
        // 실행 도중 scanOrigin이 비면 다시 자기 자신을 넣는다.
        if (scanOrigin == null)
        {
            scanOrigin = transform;
        }

        // 카메라가 비면 다시 찾는다.
        if (scanCamera == null)
        {
            ResolveCamera();
        }

        // Inspector에서 색이나 높이를 바꿨을 때 즉시 반영되도록 갱신한다.
        RefreshLineStyle();

        // 우클릭을 눌렀고, 현재 스캔 중이 아니고, 쿨타임도 끝났다면 스캔을 시작한다.
        if (Input.GetMouseButtonDown(scanMouseButton) && !IsScanning && Time.time >= nextScanTime)
        {
            // 다음 사용 가능 시간을 갱신한다.
            nextScanTime = Time.time + scanCooldown;

            // 스캔 코루틴을 시작한다.
            StartCoroutine(SweepScanTopToBottom());
        }
    }

    // 사용할 카메라를 자동으로 찾는 함수이다.
    private void ResolveCamera()
    {
        // scanOrigin에 Camera가 붙어 있으면 그것을 우선 사용한다.
        if (scanOrigin != null)
        {
            Camera originCamera = scanOrigin.GetComponent<Camera>();
            if (originCamera != null)
            {
                scanCamera = originCamera;
            }
        }

        // 그래도 없으면 Main Camera를 사용한다.
        if (scanCamera == null)
        {
            scanCamera = Camera.main;
        }
    }

    // 화면 전체 너비를 가로지르는 초록색 선 UI를 만드는 함수이다.
    private void CreateRuntimeScreenLine()
    {
        // 이미 만들어져 있으면 다시 만들지 않는다.
        if (runtimeCanvas != null)
        {
            return;
        }

        // Canvas 오브젝트를 만든다.
        GameObject canvasObject = new GameObject("RuntimeScanLineCanvas");
        canvasObject.transform.SetParent(transform, false);

        // 화면 오버레이 Canvas를 추가한다.
        runtimeCanvas = canvasObject.AddComponent<Canvas>();
        runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeCanvas.sortingOrder = 5000;

        // 해상도 변화에도 선 두께가 너무 이상해지지 않도록 CanvasScaler를 붙인다.
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // 클릭을 막지 않도록 이미지 레이캐스트만 끄면 되지만,
        // 일반적인 UI 구성과 맞추기 위해 기본 GraphicRaycaster를 붙여 둔다.
        canvasObject.AddComponent<GraphicRaycaster>();

        // 실제 선 이미지를 만든다.
        GameObject lineObject = new GameObject("RuntimeScanLine");
        lineObject.transform.SetParent(canvasObject.transform, false);

        // 이미지 컴포넌트를 붙인다.
        runtimeLineImage = lineObject.AddComponent<Image>();
        runtimeLineImage.raycastTarget = false;

        // RectTransform을 가져온다.
        runtimeLineRect = lineObject.GetComponent<RectTransform>();

        // 좌우 전체를 쓰고, 세로만 움직이게 앵커를 설정한다.
        runtimeLineRect.anchorMin = new Vector2(0f, 0.5f);
        runtimeLineRect.anchorMax = new Vector2(1f, 0.5f);
        runtimeLineRect.pivot = new Vector2(0.5f, 0.5f);
        runtimeLineRect.sizeDelta = new Vector2(0f, scanLineScreenHeight);

        // 현재 Inspector 값으로 스타일을 맞춘다.
        RefreshLineStyle();
    }

    // 선의 색상과 높이를 현재 Inspector 값으로 갱신하는 함수이다.
    private void RefreshLineStyle()
    {
        // 선 UI가 아직 없으면 종료한다.
        if (runtimeLineImage == null || runtimeLineRect == null)
        {
            return;
        }

        // 알파를 포함한 최종 색을 만든다.
        Color finalColor = scanLineColor;
        finalColor.a = scanLineAlpha;

        // 이미지 색을 적용한다.
        runtimeLineImage.color = finalColor;

        // 선 높이를 적용한다.
        runtimeLineRect.sizeDelta = new Vector2(0f, scanLineScreenHeight);
    }

    // 실제 스캔을 위에서 아래로 진행하는 코루틴이다.
    private IEnumerator SweepScanTopToBottom()
    {
        // 카메라가 없으면 스캔할 수 없으므로 종료한다.
        if (scanCamera == null)
        {
            yield break;
        }

        // 스캔 시작 상태로 바꾼다.
        IsScanning = true;

        // 새 스캔 시작 시 이전 점을 지우도록 설정되어 있으면 데이터를 비운다.
        if (clearDotsBeforeScan)
        {
            ClearDotData();
        }

        // 선을 보이게 한다.
        ShowScanLine();

        // 경과 시간을 저장한다.
        float elapsed = 0f;

        // 첫 프레임 전의 선 중심 위치를 화면 위쪽 바깥으로 둔다.
        float previousCenterY = 1f + (scanBandThickness * 0.5f);

        // sweepDuration 동안 매 프레임 조금씩 내려오게 한다.
        while (elapsed < sweepDuration)
        {
            // 시간을 누적한다.
            elapsed += Time.deltaTime;

            // 0~1 진행도를 만든다.
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, sweepDuration));

            // 선 중심 y를 위에서 아래로 이동시킨다.
            float currentCenterY = Mathf.Lerp(
                1f + (scanBandThickness * 0.5f),
                0f - (scanBandThickness * 0.5f),
                progress
            );

            // 현재 선 위치를 화면에 반영한다.
            UpdateScanLineVisual(currentCenterY);

            // 이전 위치에서 현재 위치까지 지나간 밴드 구간만 스캔한다.
            ScanSweptBand(previousCenterY, currentCenterY);

            // 다음 프레임을 위해 이전 위치를 갱신한다.
            previousCenterY = currentCenterY;

            // 다음 프레임까지 기다린다.
            yield return null;
        }

        // 스캔 종료 시 선을 숨긴다.
        HideScanLine();

        // 상태를 종료로 바꾼다.
        IsScanning = false;
    }

    // 화면용 초록색 선의 세로 위치를 갱신하는 함수이다.
    private void UpdateScanLineVisual(float viewportCenterY)
    {
        // 필수 UI가 없으면 종료한다.
        if (runtimeLineRect == null || runtimeCanvas == null)
        {
            return;
        }

        // Canvas 루트의 RectTransform을 가져온다.
        RectTransform canvasRect = runtimeCanvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            return;
        }

        // 화면 높이를 가져온다.
        float canvasHeight = canvasRect.rect.height;

        // viewport 0.5를 화면 중앙으로 보고 anchoredPosition으로 변환한다.
        // 일부러 Clamp하지 않아서 시작과 끝에서 화면 밖으로 자연스럽게 들어왔다 나가게 만든다.
        float anchoredY = (viewportCenterY - 0.5f) * canvasHeight;

        // 계산한 위치를 적용한다.
        runtimeLineRect.anchoredPosition = new Vector2(0f, anchoredY);
    }

    // 선이 지나간 구간만 스캔하는 함수이다.
    private void ScanSweptBand(float previousCenterY, float currentCenterY)
    {
        // 카메라가 없으면 종료한다.
        if (scanCamera == null)
        {
            return;
        }

        // 이전 위치와 현재 위치를 합쳐 실제 지나간 밴드의 위/아래 경계를 계산한다.
        float top = Mathf.Max(previousCenterY, currentCenterY) + (scanBandThickness * 0.5f);
        float bottom = Mathf.Min(previousCenterY, currentCenterY) - (scanBandThickness * 0.5f);

        // 화면 범위 안으로만 제한한다.
        float clampedTop = Mathf.Clamp01(top);
        float clampedBottom = Mathf.Clamp01(bottom);

        // 혹시 순서가 뒤집히면 다시 맞춘다.
        if (clampedTop < clampedBottom)
        {
            float temp = clampedTop;
            clampedTop = clampedBottom;
            clampedBottom = temp;
        }

        // 한 프레임 안에서 밴드 두께 구간을 몇 줄로 나눠 스캔할지 정한다.
        int safeRows = Mathf.Max(1, rowsPerStep);

        // 밴드 위쪽에서 아래쪽으로 차례대로 스캔한다.
        for (int row = 0; row < safeRows; row++)
        {
            // 줄 위치를 계산한다.
            float t = safeRows == 1 ? 0.5f : (float)row / (safeRows - 1);
            float viewportY = Mathf.Lerp(clampedTop, clampedBottom, t);

            // 해당 줄을 좌우로 스캔한다.
            ScanCurrentRow(viewportY);
        }
    }

    // 현재 viewport y 한 줄을 좌우로 쭉 스캔하는 함수이다.
    private void ScanCurrentRow(float viewportY)
    {
        // 카메라가 없으면 종료한다.
        if (scanCamera == null)
        {
            return;
        }

        // 최소 1개 이상의 Ray를 쏘게 만든다.
        int rays = Mathf.Max(1, raysPerRow);

        // 좌우 끝 여백을 적용한 시작/끝 값이다.
        float minX = horizontalViewportMargin;
        float maxX = 1f - horizontalViewportMargin;

        // 한 줄 전체를 좌우로 촘촘하게 훑는다.
        for (int i = 0; i < rays; i++)
        {
            // 현재 위치 비율을 계산한다.
            float t = rays == 1 ? 0.5f : (float)i / (rays - 1);

            // 좌우 viewport x를 만든다.
            float viewportX = Mathf.Lerp(minX, maxX, t);

            // 너무 기계적으로 보이지 않도록 약간만 흔들어 준다.
            float jitterX = Random.Range(-0.0015f, 0.0015f);
            float jitterY = Random.Range(-scanBandThickness * 0.15f, scanBandThickness * 0.15f);

            // 최종 좌표를 화면 안으로 제한한다.
            float finalX = Mathf.Clamp01(viewportX + jitterX);
            float finalY = Mathf.Clamp01(viewportY + jitterY);

            // 해당 화면 좌표를 향한 Ray를 만든다.
            Ray ray = scanCamera.ViewportPointToRay(new Vector3(finalX, finalY, 0f));

            // Raycast 결과를 점 데이터로 반영한다.
            CastRevealRay(ray);
        }
    }

    // Ray 하나를 쏴서 맞은 위치에 점 데이터를 기록하는 함수이다.
    private bool CastRevealRay(Ray ray)
    {
        // 지정한 레이어에 맞았을 때만 점을 기록한다.
        if (Physics.Raycast(ray, out RaycastHit hit, maxRevealDistance, revealSurfaceMask, QueryTriggerInteraction.Ignore))
        {
            // 표면과 너무 겹치지 않도록 살짝 띄운다.
            Vector3 finalPos = hit.point + hit.normal * 0.02f;

            // 새 위치면 점 데이터를 추가한다.
            return AddDotIfNew(finalPos);
        }

        // 아무것도 맞지 않았으면 false를 반환한다.
        return false;
    }

    // 아직 없는 셀이라면 점 데이터를 추가하는 함수이다.
    private bool AddDotIfNew(Vector3 worldPos)
    {
        // 셀 크기가 0에 너무 가까우면 계산이 망가지므로 최소값을 강제한다.
        float safeCellSize = Mathf.Max(0.0001f, cellSize);

        // 월드 좌표를 셀 좌표로 바꿔 중복 검사를 한다.
        Vector3Int cell = new Vector3Int(
            Mathf.FloorToInt(worldPos.x / safeCellSize),
            Mathf.FloorToInt(worldPos.y / safeCellSize),
            Mathf.FloorToInt(worldPos.z / safeCellSize)
        );

        // 이미 같은 셀에 점이 있으면 추가하지 않는다.
        if (occupiedCells.Contains(cell))
        {
            return false;
        }

        // 새 셀을 기록한다.
        occupiedCells.Add(cell);

        // 점 데이터를 리스트에 추가한다.
        myDots.Add(new RevealDot(worldPos, dotSize));

        // 새 점이 실제로 추가되었음을 반환한다.
        return true;
    }

    // 현재 점 데이터와 셀 기록을 모두 지우는 함수이다.
    public void ClearDotData()
    {
        // 점 데이터 리스트를 비운다.
        myDots.Clear();

        // 셀 기록도 함께 비운다.
        occupiedCells.Clear();
    }

    // 외부에서 남은 쿨타임을 확인할 수 있게 해 주는 함수이다.
    public float GetRemainingCooldown()
    {
        // 현재 시간 기준으로 남은 시간을 구한다.
        float remain = nextScanTime - Time.time;

        // 음수가 되지 않게 0 이상으로 맞춘다.
        return Mathf.Max(0f, remain);
    }

    // 선을 보이게 하는 함수이다.
    private void ShowScanLine()
    {
        if (runtimeCanvas != null)
        {
            runtimeCanvas.enabled = true;
        }

        if (runtimeLineImage != null)
        {
            runtimeLineImage.enabled = true;
        }
    }

    // 선을 숨기는 함수이다.
    private void HideScanLine()
    {
        if (runtimeLineImage != null)
        {
            runtimeLineImage.enabled = false;
        }

        if (runtimeCanvas != null)
        {
            runtimeCanvas.enabled = false;
        }
    }

    // 오브젝트가 파괴될 때 런타임 UI를 정리한다.
    private void OnDestroy()
    {
        if (runtimeCanvas == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeCanvas.gameObject);
        }
        else
        {
            DestroyImmediate(runtimeCanvas.gameObject);
        }
    }
}
