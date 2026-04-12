using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 이 스크립트는 우클릭 1회마다 화면 전체를 스캔해서
// 작은 점들을 "퍼지듯" 생성하는 역할을 한다.
public class PointCloudReveal : MonoBehaviour
{
    [Header("기준 카메라")]
    // 레이를 쏠 카메라이다.
    // 비워두면 자동으로 Camera.main을 사용한다.
    public Camera playerCamera;

    [Header("입력 / 쿨타임")]
    // 우클릭 1번 사용 후 다시 사용할 수 있기까지의 대기 시간이다.
    public float clickCooldown = 1.5f;

    // 현재 쿨타임이 끝났는지 여부를 저장한다.
    private bool canReveal = true;

    [Header("레이캐스트 설정")]
    // 점이 찍힐 수 있는 레이어를 제한할 때 사용한다.
    public LayerMask revealMask = ~0;

    // 레이를 쏠 최대 거리이다.
    public float maxDistance = 60f;

    [Header("퍼지는 연출")]
    // 한 번 클릭했을 때 전체 스캔에 사용할 총 레이 수이다.
    public int totalRaysPerClick = 500;

    // 한 번 클릭했을 때 점이 퍼지는 총 시간이다.
    // 값이 클수록 천천히 퍼지는 느낌이 난다.
    public float revealDuration = 0.25f;

    // 한 프레임 또는 한 스텝마다 처리할 레이 수이다.
    // 값이 작을수록 더 부드럽게 퍼지고, 너무 크면 다시 버벅일 수 있다.
    public int raysPerStep = 25;

    [Header("점 생성 설정")]
    // 레이 하나가 맞았을 때 최소 몇 개의 점을 만들지 정한다.
    public int minPointsPerHit = 1;

    // 레이 하나가 맞았을 때 최대 몇 개의 점을 만들지 정한다.
    public int maxPointsPerHit = 2;

    // 맞은 위치 주변에 점이 퍼지는 반경이다.
    public float hitSpreadRadius = 0.03f;

    // 표면과 점이 겹쳐 깜빡이지 않도록 아주 살짝 띄우는 값이다.
    public float surfaceOffset = 0.002f;

    [Header("점 크기 / 색상")]
    // 점의 최소 크기이다.
    public float minPointSize = 0.012f;

    // 점의 최대 크기이다.
    public float maxPointSize = 0.012f;

    // 점 색상이다.
    public Color pointColor = Color.white;

    // 직접 만든 머티리얼이 있으면 여기에 넣는다.
    // 비워두면 코드가 기본 머티리얼을 자동 생성한다.
    public Material pointMaterial;

    [Header("성능 제한")]
    // 장면에 남길 최대 점 개수이다.
    // 이 수를 넘으면 가장 오래된 점부터 삭제한다.
    public int maxPointCount = 9000;

    [Header("정리용 부모")]
    // 생성된 점들을 한 부모 아래에 모아두기 위한 변수이다.
    // 비워두면 자동으로 만든다.
    public Transform pointParent;

    // 이미 생성된 점들을 순서대로 저장해서 오래된 점부터 지우기 위해 사용한다.
    private Queue<GameObject> pointQueue = new Queue<GameObject>();

    // 점 생성에 사용할 런타임 머티리얼이다.
    private Material runtimePointMaterial;

    // 현재 퍼지는 연출이 실행 중인지 저장한다.
    private bool isRevealing = false;

    // 게임 시작 시 필요한 초기 설정을 한다.
    private void Start()
    {
        // 카메라가 비어 있으면 메인 카메라를 자동으로 넣는다.
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // 점들을 담을 부모 오브젝트가 없으면 새로 만든다.
        if (pointParent == null)
        {
            GameObject parentObject = new GameObject("PointCloudContainer");
            pointParent = parentObject.transform;
        }

        // 점 렌더링에 사용할 머티리얼을 준비한다.
        runtimePointMaterial = BuildRuntimeMaterial();
    }

    // 매 프레임 입력을 확인해서 우클릭 시 reveal을 시작한다.
    private void Update()
    {
        // 카메라가 없으면 아무것도 하지 않는다.
        if (playerCamera == null)
        {
            return;
        }

        // 우클릭을 눌렀고, 현재 사용 가능 상태이며, 이미 발동 중이 아닐 때만 실행한다.
        if (Input.GetMouseButtonDown(1) && canReveal && !isRevealing)
        {
            // 퍼지는 연출과 쿨타임을 함께 시작한다.
            StartCoroutine(RevealWithCooldown());
        }
    }

    // 우클릭 1회 발동 + 퍼지는 연출 + 쿨타임을 한 번에 관리하는 코루틴이다.
    private IEnumerator RevealWithCooldown()
    {
        // 지금부터는 사용할 수 없게 막는다.
        canReveal = false;

        // 현재 reveal이 실행 중임을 표시한다.
        isRevealing = true;

        // 점이 퍼지는 연출을 실행한다.
        yield return StartCoroutine(FireRevealBurstOverTime());

        // reveal이 끝났음을 표시한다.
        isRevealing = false;

        // 남은 쿨타임 동안 기다린다.
        yield return new WaitForSeconds(clickCooldown);

        // 다시 사용할 수 있게 푼다.
        canReveal = true;
    }

    // 한 번 클릭했을 때 전체 레이를 여러 스텝으로 나눠 퍼지듯 생성하는 코루틴이다.
    private IEnumerator FireRevealBurstOverTime()
    {
        // 잘못된 값으로 나누기 오류가 나지 않도록 최소 1 이상으로 보정한다.
        int safeRaysPerStep = Mathf.Max(1, raysPerStep);

        // 총 몇 번의 스텝으로 나눌지 계산한다.
        int totalSteps = Mathf.CeilToInt((float)totalRaysPerClick / safeRaysPerStep);

        // 총 duration을 스텝 수로 나눠 스텝 사이 간격을 만든다.
        float stepDelay = revealDuration / Mathf.Max(1, totalSteps);

        // 이미 처리한 레이 수를 저장한다.
        int processedRays = 0;

        // 모든 레이를 다 처리할 때까지 반복한다.
        while (processedRays < totalRaysPerClick)
        {
            // 이번 스텝에서 몇 개의 레이를 처리할지 계산한다.
            int currentStepCount = Mathf.Min(safeRaysPerStep, totalRaysPerClick - processedRays);

            // currentStepCount만큼 화면의 랜덤 위치로 레이를 쏜다.
            for (int i = 0; i < currentStepCount; i++)
            {
                // 화면 전체 중 랜덤한 viewport 좌표를 만든다.
                float vx = Random.value;
                float vy = Random.value;

                // 해당 화면 좌표 기준으로 레이를 만든다.
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(vx, vy, 0f));

                // 레이가 물체를 맞췄을 때만 점을 만든다.
                if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, revealMask, QueryTriggerInteraction.Ignore))
                {
                    // 한 번 맞았을 때 만들 점 개수를 랜덤으로 정한다.
                    int pointsToCreate = Random.Range(minPointsPerHit, maxPointsPerHit + 1);

                    // 맞은 지점 주변에 점을 생성한다.
                    for (int p = 0; p < pointsToCreate; p++)
                    {
                        CreatePointNearHit(hit);
                    }
                }
            }

            // 처리한 레이 수를 누적한다.
            processedRays += currentStepCount;

            // 다음 스텝까지 잠깐 기다려서 "퍼지는 느낌"을 만든다.
            yield return new WaitForSeconds(stepDelay);
        }
    }

    // 하나의 hit 위치 주변에 실제 점 하나를 생성하는 함수이다.
    private void CreatePointNearHit(RaycastHit hit)
    {
        // 표면 법선을 정규화해서 기준 방향으로 사용한다.
        Vector3 normal = hit.normal.normalized;

        // 표면 위 평면 방향을 만들기 위한 기준 축이다.
        Vector3 referenceAxis = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.98f
            ? Vector3.right
            : Vector3.up;

        // 표면 위 가로 방향 축을 만든다.
        Vector3 tangent = Vector3.Cross(normal, referenceAxis).normalized;

        // 표면 위 세로 방향 축을 만든다.
        Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

        // 원형으로 퍼지는 랜덤 각도를 만든다.
        float angle = Random.Range(0f, Mathf.PI * 2f);

        // 중심에서 바깥으로 자연스럽게 퍼지는 랜덤 거리를 만든다.
        float distance = Mathf.Sqrt(Random.value) * hitSpreadRadius;

        // 표면 평면 위 오프셋을 계산한다.
        Vector3 planarOffset =
            tangent * Mathf.Cos(angle) * distance +
            bitangent * Mathf.Sin(angle) * distance;

        // 최종 점 위치를 계산한다.
        Vector3 pointPosition = hit.point + planarOffset + normal * surfaceOffset;

        // 실제 점 오브젝트를 Sphere로 만든다.
        GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        // Hierarchy에서 보기 쉽도록 이름을 설정한다.
        point.name = "RevealPoint";

        // 생성된 점을 부모 오브젝트 아래로 정리한다.
        if (pointParent != null)
        {
            point.transform.SetParent(pointParent);
        }

        // 점의 위치를 적용한다.
        point.transform.position = pointPosition;

        // 점의 크기를 아주 작게 랜덤으로 설정한다.
        float pointSize = Random.Range(minPointSize, maxPointSize);

        // 점이 등장할 때 너무 딱딱하지 않게 약간 작은 크기에서 시작한다.
        point.transform.localScale = Vector3.one * (pointSize * Random.Range(0.35f, 0.6f));

        // 기본 Collider는 필요 없으므로 제거한다.
        Collider pointCollider = point.GetComponent<Collider>();
        if (pointCollider != null)
        {
            Destroy(pointCollider);
        }

        // 렌더러를 가져와 점 전용 설정을 한다.
        MeshRenderer renderer = point.GetComponent<MeshRenderer>();

        // 그림자 관련 옵션을 꺼서 더 깔끔하게 보이게 한다.
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        // 준비한 머티리얼을 sharedMaterial로 적용한다.
        if (runtimePointMaterial != null)
        {
            renderer.sharedMaterial = runtimePointMaterial;
        }

        // 점이 살짝 커지며 나타나도록 컴포넌트를 붙인다.
        PointGrow pointGrow = point.AddComponent<PointGrow>();
        pointGrow.targetScale = Vector3.one * pointSize;
        pointGrow.growDuration = Random.Range(0.05f, 0.12f);

        // 큐에 넣어서 오래된 점부터 지울 수 있게 관리한다.
        pointQueue.Enqueue(point);

        // 최대 점 개수를 넘으면 가장 오래된 점부터 삭제한다.
        while (pointQueue.Count > maxPointCount)
        {
            GameObject oldestPoint = pointQueue.Dequeue();

            // null이 아닐 때만 삭제한다.
            if (oldestPoint != null)
            {
                Destroy(oldestPoint);
            }
        }
    }

    // 점에 사용할 기본 머티리얼을 만드는 함수이다.
    private Material BuildRuntimeMaterial()
    {
        // 사용자가 직접 넣은 머티리얼이 있으면 그것을 복사해서 쓴다.
        if (pointMaterial != null)
        {
            return new Material(pointMaterial);
        }

        // URP 환경이면 URP Unlit 셰이더를 먼저 찾는다.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        // URP 셰이더가 없으면 일반 Unlit/Color를 찾는다.
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        // 그것도 없으면 Sprites/Default를 찾는다.
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        // 마지막 예비용으로 Standard를 찾는다.
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        // 찾은 셰이더로 머티리얼을 만든다.
        Material mat = new Material(shader);

        // 셰이더가 _BaseColor를 쓰는 경우 색상을 적용한다.
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", pointColor);
        }

        // 셰이더가 _Color를 쓰는 경우 색상을 적용한다.
        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", pointColor);
        }

        // 발광을 지원하면 약간 밝게 보이도록 emission도 넣는다.
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", pointColor * 1.1f);
        }

        // 완성된 머티리얼을 반환한다.
        return mat;
    }
}

// 이 스크립트는 점이 생성될 때 살짝 커지며 나타나는 효과를 담당한다.
public class PointGrow : MonoBehaviour
{
    // 점이 최종적으로 도달할 크기이다.
    public Vector3 targetScale = Vector3.one;

    // 점이 커지는 데 걸리는 시간이다.
    public float growDuration = 0.08f;

    // 시작 크기를 저장한다.
    private Vector3 startScale;

    // 경과 시간을 저장한다.
    private float elapsed = 0f;

    // 시작 시 현재 크기를 기록한다.
    private void Start()
    {
        // 현재 생성된 순간의 크기를 시작 크기로 저장한다.
        startScale = transform.localScale;
    }

    // 매 프레임 점의 크기를 조금씩 키운다.
    private void Update()
    {
        // growDuration이 0 이하이면 바로 목표 크기로 맞추고 종료한다.
        if (growDuration <= 0f)
        {
            transform.localScale = targetScale;
            enabled = false;
            return;
        }

        // 경과 시간을 누적한다.
        elapsed += Time.deltaTime;

        // 0~1 사이의 보간값을 만든다.
        float t = Mathf.Clamp01(elapsed / growDuration);

        // 시작 크기에서 목표 크기로 부드럽게 보간한다.
        transform.localScale = Vector3.Lerp(startScale, targetScale, t);

        // 목표 크기에 도달하면 스크립트를 끈다.
        if (t >= 1f)
        {
            transform.localScale = targetScale;
            enabled = false;
        }
    }
}