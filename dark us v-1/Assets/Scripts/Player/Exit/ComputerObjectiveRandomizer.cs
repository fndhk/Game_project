using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

// 맵 안에 존재하는 ObjectiveComputer 중 무작위로 일부를 목표 컴퓨터로 선택한다.
// 이번 버전은 플레이어 시작 위치와 너무 가까운 컴퓨터가 성공 컴퓨터로 뽑히지 않도록 거리 조건을 적용한다.
public class ComputerObjectiveRandomizer : MonoBehaviour
{
    [Header("References")]
    // 컴퓨터를 찾을 부모 루트이다. 비워두면 이 오브젝트의 자식에서 찾는다.
    public Transform searchRoot;

    // 목표 진행도를 관리할 매니저이다. 비워두면 LabObjectiveManager.Instance를 사용한다.
    public LabObjectiveManager objectiveManager;

    [Header("Selection")]
    // 이번 판에 목표로 삼을 컴퓨터 수이다.
    public int requiredComputerCount = 4;

    // 비활성화된 컴퓨터 오브젝트도 후보에 포함할지 정한다.
    public bool includeInactiveComputers = true;

    // Start에서 자동으로 목표 컴퓨터를 고를지 정한다.
    public bool randomizeOnStart = true;

    // 맵 생성과 플레이어 스폰이 끝날 시간을 주기 위해 기다릴 프레임 수이다.
    public int startDelayFrames = 2;

    [Header("Distance Rules")]
    // 성공 컴퓨터 선택 시 거리 조건을 사용할지 정한다.
    public bool useDistanceRules = true;

    // 성공 컴퓨터가 플레이어 시작 위치에서 최소 이 거리 이상 떨어져야 한다.
    public float minDistanceFromPlayerStarts = 12f;

    // 성공 컴퓨터끼리 최소 이 거리 이상 떨어져야 한다.
    public float minDistanceBetweenSelectedComputers = 10f;

    // 거리 조건이 너무 빡세서 목표 개수를 못 채우면 조건을 조금씩 완화할지 정한다.
    public bool relaxDistancesIfNeeded = true;

    // 조건 완화 시 한 번에 줄일 거리이다.
    public float relaxDistanceStep = 2f;

    // 조건 완화가 이 거리보다 더 낮아지지 않게 하는 최저값이다.
    public float minimumRelaxedDistance = 4f;

    // 완화 후에도 부족하면 남은 후보 중 멀리 있는 컴퓨터부터 채울지 정한다.
    public bool fillRemainingWithFarthestCandidates = true;

    [Header("Player Start Detection")]
    // 플레이어 시작 위치로 사용할 Transform 목록이다. 비워두면 Player 태그를 자동 검색한다.
    public Transform[] playerStartReferences;

    // Player 태그를 가진 오브젝트를 자동으로 찾아 시작 위치로 사용할지 정한다.
    public bool autoFindPlayerStartsByTag = true;

    // 자동 검색에 사용할 플레이어 태그이다.
    public string playerTag = "Player";

    [Header("Fallback Search")]
    // searchRoot에서 못 찾으면 씬 전체를 한 번 더 검색할지 정한다.
    public bool searchWholeSceneIfNoCandidates = true;

    [Header("Optional Name Filter")]
    // 이름 키워드 필터를 사용할지 정한다.
    public bool useNameFilter = false;

    // 이름 필터를 사용할 때 후보로 인정할 키워드이다.
    public string[] nameKeywords = { "Computer", "PC", "Monitor" };

    // 시작 시 자동 선택을 실행한다.
    private IEnumerator Start()
    {
        if (!randomizeOnStart)
        {
            yield break;
        }

        int delay = Mathf.Max(0, startDelayFrames);

        for (int i = 0; i < delay; i++)
        {
            yield return null;
        }

        SelectObjectivesNow();
    }

    // ContextMenu나 다른 스크립트에서 즉시 목표 컴퓨터를 다시 선택할 수 있게 한다.
    [ContextMenu("Select Computer Objectives Now")]
    public void SelectObjectivesNow()
    {
        GameLoopManager.EnsureExists();

        if (objectiveManager == null)
        {
            objectiveManager = LabObjectiveManager.Instance;
        }

        if (objectiveManager == null)
        {
            Debug.LogWarning("[ComputerObjectiveRandomizer] LabObjectiveManager를 찾지 못함.");
            return;
        }

        List<ObjectiveComputer> candidates = CollectCandidates();
        SortCandidatesByStablePath(candidates);
        AssignNetworkObjectiveIds(candidates);

        if (candidates == null || candidates.Count <= 0)
        {
            Debug.LogWarning("[ComputerObjectiveRandomizer] ObjectiveComputer 후보를 찾지 못함. 컴퓨터 프리팹에 ObjectiveComputer 컴포넌트를 붙여야 함.");
            return;
        }

        int targetCount = Mathf.Min(Mathf.Max(1, requiredComputerCount), candidates.Count);

        // 먼저 모든 컴퓨터를 가짜 컴퓨터 상태로 초기화한다.
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] != null)
            {
                candidates[i].SetSelectedObjective(false, true);
            }
        }

        List<Vector3> playerStartPositions = CollectPlayerStartPositions();
        System.Random deterministicRandom = CreateDeterministicRandom();
        List<ObjectiveComputer> selectedComputers = SelectComputersWithRules(candidates, targetCount, playerStartPositions, deterministicRandom);

        for (int i = 0; i < selectedComputers.Count; i++)
        {
            if (selectedComputers[i] != null)
            {
                selectedComputers[i].SetSelectedObjective(true, true);
            }
        }

        objectiveManager.SetupComputerObjectives(selectedComputers.ToArray(), targetCount);
        GameLoopManager.EnsureExists().RebuildComputerIndex();

        Debug.Log(
            "[ComputerObjectiveRandomizer] Selected computers: " + selectedComputers.Count +
            " / Required: " + targetCount +
            " / Candidates: " + candidates.Count +
            " / PlayerStarts: " + playerStartPositions.Count
        );
    }

    // 거리 규칙을 적용해 성공 컴퓨터를 고른다.
    private List<ObjectiveComputer> SelectComputersWithRules(List<ObjectiveComputer> candidates, int targetCount, List<Vector3> playerStartPositions, System.Random random)
    {
        List<ObjectiveComputer> selected = new List<ObjectiveComputer>();

        if (candidates == null || candidates.Count <= 0 || targetCount <= 0)
        {
            return selected;
        }

        // 거리 규칙을 쓰지 않으면 기존처럼 완전 랜덤으로 고른다.
        if (!useDistanceRules)
        {
            List<ObjectiveComputer> randomList = new List<ObjectiveComputer>(candidates);
            Shuffle(randomList, random);

            for (int i = 0; i < randomList.Count && selected.Count < targetCount; i++)
            {
                if (randomList[i] != null)
                {
                    selected.Add(randomList[i]);
                }
            }

            return selected;
        }

        float currentPlayerDistance = Mathf.Max(0f, minDistanceFromPlayerStarts);
        float currentBetweenDistance = Mathf.Max(0f, minDistanceBetweenSelectedComputers);
        float minimumDistance = Mathf.Max(0f, minimumRelaxedDistance);
        float step = Mathf.Max(0.1f, relaxDistanceStep);

        while (true)
        {
            selected = TrySelectWithCurrentDistance(candidates, targetCount, playerStartPositions, currentPlayerDistance, currentBetweenDistance, random);

            if (selected.Count >= targetCount)
            {
                return selected;
            }

            if (!relaxDistancesIfNeeded)
            {
                break;
            }

            bool canRelaxPlayerDistance = currentPlayerDistance > minimumDistance;
            bool canRelaxBetweenDistance = currentBetweenDistance > minimumDistance;

            if (!canRelaxPlayerDistance && !canRelaxBetweenDistance)
            {
                break;
            }

            if (canRelaxPlayerDistance)
            {
                currentPlayerDistance = Mathf.Max(minimumDistance, currentPlayerDistance - step);
            }

            if (canRelaxBetweenDistance)
            {
                currentBetweenDistance = Mathf.Max(minimumDistance, currentBetweenDistance - step);
            }
        }

        // 그래도 목표 개수를 못 채웠다면, 남은 후보 중 최대한 멀리 있는 컴퓨터부터 채운다.
        if (selected.Count < targetCount && fillRemainingWithFarthestCandidates)
        {
            FillRemainingWithFarthestCandidates(candidates, selected, targetCount, playerStartPositions);
        }

        // 마지막 안전장치이다. 그래도 부족하면 랜덤으로 남은 후보를 채운다.
        if (selected.Count < targetCount)
        {
            FillRemainingRandomly(candidates, selected, targetCount, random);
        }

        return selected;
    }

    // 현재 거리 조건으로 목표 컴퓨터를 골라본다.
    private List<ObjectiveComputer> TrySelectWithCurrentDistance(
        List<ObjectiveComputer> candidates,
        int targetCount,
        List<Vector3> playerStartPositions,
        float playerDistance,
        float betweenDistance,
        System.Random random)
    {
        List<ObjectiveComputer> selected = new List<ObjectiveComputer>();
        List<ObjectiveComputer> randomList = new List<ObjectiveComputer>(candidates);
        Shuffle(randomList, random);

        for (int i = 0; i < randomList.Count && selected.Count < targetCount; i++)
        {
            ObjectiveComputer candidate = randomList[i];

            if (candidate == null)
            {
                continue;
            }

            if (!IsFarEnoughFromPlayerStarts(candidate.transform.position, playerStartPositions, playerDistance))
            {
                continue;
            }

            if (!IsFarEnoughFromSelectedComputers(candidate.transform.position, selected, betweenDistance))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    // 남은 후보 중 플레이어 시작 위치와 이미 선택된 컴퓨터에서 가장 먼 컴퓨터부터 채운다.
    private void FillRemainingWithFarthestCandidates(
        List<ObjectiveComputer> candidates,
        List<ObjectiveComputer> selected,
        int targetCount,
        List<Vector3> playerStartPositions)
    {
        while (selected.Count < targetCount)
        {
            ObjectiveComputer bestComputer = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < candidates.Count; i++)
            {
                ObjectiveComputer candidate = candidates[i];

                if (candidate == null || selected.Contains(candidate))
                {
                    continue;
                }

                float score = GetDistanceScore(candidate.transform.position, selected, playerStartPositions);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestComputer = candidate;
                }
            }

            if (bestComputer == null)
            {
                break;
            }

            selected.Add(bestComputer);
        }
    }

    // 부족한 개수를 랜덤으로 채운다.
    private void FillRemainingRandomly(List<ObjectiveComputer> candidates, List<ObjectiveComputer> selected, int targetCount, System.Random random)
    {
        List<ObjectiveComputer> randomList = new List<ObjectiveComputer>(candidates);
        Shuffle(randomList, random);

        for (int i = 0; i < randomList.Count && selected.Count < targetCount; i++)
        {
            ObjectiveComputer candidate = randomList[i];

            if (candidate == null || selected.Contains(candidate))
            {
                continue;
            }

            selected.Add(candidate);
        }
    }

    // 후보 컴퓨터를 수집한다.
    private List<ObjectiveComputer> CollectCandidates()
    {
        Transform root = searchRoot != null ? searchRoot : transform;
        ObjectiveComputer[] rootCandidates = root.GetComponentsInChildren<ObjectiveComputer>(includeInactiveComputers);
        List<ObjectiveComputer> filtered = FilterCandidates(rootCandidates);

        if (filtered.Count > 0 || !searchWholeSceneIfNoCandidates)
        {
            return filtered;
        }

        ObjectiveComputer[] sceneCandidates = FindObjectsOfType<ObjectiveComputer>(includeInactiveComputers);
        filtered = FilterCandidates(sceneCandidates);
        return filtered;
    }

    // 이름 필터와 null 검사를 적용한다.
    private List<ObjectiveComputer> FilterCandidates(ObjectiveComputer[] source)
    {
        List<ObjectiveComputer> result = new List<ObjectiveComputer>();

        if (source == null)
        {
            return result;
        }

        for (int i = 0; i < source.Length; i++)
        {
            ObjectiveComputer computer = source[i];

            if (computer == null)
            {
                continue;
            }

            if (useNameFilter && !IsNameMatched(computer.name))
            {
                continue;
            }

            if (!result.Contains(computer))
            {
                result.Add(computer);
            }
        }

        return result;
    }

    // 플레이어 시작 위치들을 수집한다.
    private List<Vector3> CollectPlayerStartPositions()
    {
        List<Vector3> result = new List<Vector3>();

        if (playerStartReferences != null)
        {
            for (int i = 0; i < playerStartReferences.Length; i++)
            {
                if (playerStartReferences[i] != null)
                {
                    result.Add(playerStartReferences[i].position);
                }
            }
        }

        if (autoFindPlayerStartsByTag && !string.IsNullOrEmpty(playerTag))
        {
            try
            {
                GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);

                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] == null)
                    {
                        continue;
                    }

                    Vector3 position = players[i].transform.position;

                    if (!ContainsNearPosition(result, position, 0.05f))
                    {
                        result.Add(position);
                    }
                }
            }
            catch
            {
                // Player 태그가 프로젝트에 없으면 Unity가 예외를 던질 수 있으므로 무시한다.
            }
        }

        return result;
    }

    // 이미 거의 같은 위치가 들어있는지 확인한다.
    private bool ContainsNearPosition(List<Vector3> positions, Vector3 targetPosition, float tolerance)
    {
        if (positions == null)
        {
            return false;
        }

        float sqrTolerance = tolerance * tolerance;

        for (int i = 0; i < positions.Count; i++)
        {
            if ((positions[i] - targetPosition).sqrMagnitude <= sqrTolerance)
            {
                return true;
            }
        }

        return false;
    }

    // 플레이어 시작 위치와 충분히 떨어져 있는지 확인한다.
    private bool IsFarEnoughFromPlayerStarts(Vector3 candidatePosition, List<Vector3> playerStartPositions, float requiredDistance)
    {
        if (requiredDistance <= 0f)
        {
            return true;
        }

        if (playerStartPositions == null || playerStartPositions.Count <= 0)
        {
            return true;
        }

        float requiredSqrDistance = requiredDistance * requiredDistance;

        for (int i = 0; i < playerStartPositions.Count; i++)
        {
            if ((candidatePosition - playerStartPositions[i]).sqrMagnitude < requiredSqrDistance)
            {
                return false;
            }
        }

        return true;
    }

    // 이미 선택된 성공 컴퓨터들과 충분히 떨어져 있는지 확인한다.
    private bool IsFarEnoughFromSelectedComputers(Vector3 candidatePosition, List<ObjectiveComputer> selected, float requiredDistance)
    {
        if (requiredDistance <= 0f)
        {
            return true;
        }

        if (selected == null || selected.Count <= 0)
        {
            return true;
        }

        float requiredSqrDistance = requiredDistance * requiredDistance;

        for (int i = 0; i < selected.Count; i++)
        {
            if (selected[i] == null)
            {
                continue;
            }

            if ((candidatePosition - selected[i].transform.position).sqrMagnitude < requiredSqrDistance)
            {
                return false;
            }
        }

        return true;
    }

    // 멀리 있는 후보를 고르기 위한 거리 점수를 계산한다.
    private float GetDistanceScore(Vector3 candidatePosition, List<ObjectiveComputer> selected, List<Vector3> playerStartPositions)
    {
        float playerScore = GetNearestPlayerStartDistance(candidatePosition, playerStartPositions);
        float selectedScore = GetNearestSelectedComputerDistance(candidatePosition, selected);

        if (playerStartPositions == null || playerStartPositions.Count <= 0)
        {
            playerScore = 0f;
        }

        if (selected == null || selected.Count <= 0)
        {
            selectedScore = 0f;
        }

        return playerScore + selectedScore;
    }

    // 가장 가까운 플레이어 시작 위치까지의 거리를 반환한다.
    private float GetNearestPlayerStartDistance(Vector3 candidatePosition, List<Vector3> playerStartPositions)
    {
        if (playerStartPositions == null || playerStartPositions.Count <= 0)
        {
            return 0f;
        }

        float nearest = float.PositiveInfinity;

        for (int i = 0; i < playerStartPositions.Count; i++)
        {
            float distance = Vector3.Distance(candidatePosition, playerStartPositions[i]);

            if (distance < nearest)
            {
                nearest = distance;
            }
        }

        return nearest;
    }

    // 가장 가까운 이미 선택된 컴퓨터까지의 거리를 반환한다.
    private float GetNearestSelectedComputerDistance(Vector3 candidatePosition, List<ObjectiveComputer> selected)
    {
        if (selected == null || selected.Count <= 0)
        {
            return 0f;
        }

        float nearest = float.PositiveInfinity;

        for (int i = 0; i < selected.Count; i++)
        {
            if (selected[i] == null)
            {
                continue;
            }

            float distance = Vector3.Distance(candidatePosition, selected[i].transform.position);

            if (distance < nearest)
            {
                nearest = distance;
            }
        }

        if (float.IsInfinity(nearest))
        {
            return 0f;
        }

        return nearest;
    }

    // 이름이 키워드에 해당하는지 확인한다.
    private bool IsNameMatched(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        if (nameKeywords == null || nameKeywords.Length <= 0)
        {
            return true;
        }

        for (int i = 0; i < nameKeywords.Length; i++)
        {
            string keyword = nameKeywords[i];

            if (string.IsNullOrEmpty(keyword))
            {
                continue;
            }

            if (objectName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    // 리스트를 무작위로 섞는다.
    private void SortCandidatesByStablePath(List<ObjectiveComputer> candidates)
    {
        if (candidates == null)
        {
            return;
        }

        candidates.Sort((left, right) => string.CompareOrdinal(GetStablePath(left != null ? left.transform : null), GetStablePath(right != null ? right.transform : null)));
    }

    private void AssignNetworkObjectiveIds(List<ObjectiveComputer> candidates)
    {
        if (candidates == null)
        {
            return;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] != null)
            {
                candidates[i].SetNetworkObjectiveId(i);
            }
        }
    }

    private string GetStablePath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private System.Random CreateDeterministicRandom()
    {
        int seed = 1337;

        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties != null)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("mapSeed", out object seedValue))
            {
                seed = ToInt(seedValue, seed);
            }
            else if (PhotonNetwork.IsMasterClient)
            {
                seed = Random.Range(1, int.MaxValue);
                PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
                {
                    { "mapSeed", seed }
                });
            }
        }

        return new System.Random(seed ^ requiredComputerCount ^ 0x4C4142);
    }

    private int ToInt(object value, int fallback)
    {
        if (value is int intValue)
        {
            return intValue;
        }

        if (value is short shortValue)
        {
            return shortValue;
        }

        if (value is byte byteValue)
        {
            return byteValue;
        }

        return fallback;
    }

    private void Shuffle(List<ObjectiveComputer> list, System.Random random)
    {
        if (list == null)
        {
            return;
        }

        if (random == null)
        {
            random = new System.Random();
        }

        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = random.Next(0, i + 1);
            ObjectiveComputer temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
