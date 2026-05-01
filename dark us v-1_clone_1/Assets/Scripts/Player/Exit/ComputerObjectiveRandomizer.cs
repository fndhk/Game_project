using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 맵 안에 존재하는 ObjectiveComputer 중 무작위로 일부를 목표 컴퓨터로 선택한다.
// LaboratoryGenerator가 맵을 만든 뒤 한 프레임 이상 기다렸다가 실행하는 것을 기본으로 한다.
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

    // 맵 생성이 끝날 시간을 주기 위해 기다릴 프레임 수이다.
    public int startDelayFrames = 2;

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
        if (objectiveManager == null)
        {
            objectiveManager = LabObjectiveManager.Instance;
        }

        if (objectiveManager == null)
        {
            Debug.LogWarning("[ComputerObjectiveRandomizer] LabObjectiveManager를 찾지 못함.");
            return;
        }

        ObjectiveComputer[] candidates = CollectCandidates();

        if (candidates == null || candidates.Length <= 0)
        {
            Debug.LogWarning("[ComputerObjectiveRandomizer] ObjectiveComputer 후보를 찾지 못함. 컴퓨터 프리팹에 ObjectiveComputer 컴포넌트를 붙여야 함.");
            return;
        }

        Shuffle(candidates);

        int targetCount = Mathf.Min(Mathf.Max(1, requiredComputerCount), candidates.Length);
        ObjectiveComputer[] selectedComputers = new ObjectiveComputer[targetCount];

        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] == null)
            {
                continue;
            }

            candidates[i].SetSelectedObjective(false, true);
        }

        for (int i = 0; i < targetCount; i++)
        {
            selectedComputers[i] = candidates[i];

            if (selectedComputers[i] != null)
            {
                selectedComputers[i].SetSelectedObjective(true, true);
            }
        }

        objectiveManager.SetupComputerObjectives(selectedComputers, targetCount);

        Debug.Log("[ComputerObjectiveRandomizer] Selected computers: " + targetCount + " / Candidates: " + candidates.Length);
    }

    // 후보 컴퓨터를 수집한다.
    private ObjectiveComputer[] CollectCandidates()
    {
        Transform root = searchRoot != null ? searchRoot : transform;
        ObjectiveComputer[] rootCandidates = root.GetComponentsInChildren<ObjectiveComputer>(includeInactiveComputers);
        List<ObjectiveComputer> filtered = FilterCandidates(rootCandidates);

        if (filtered.Count > 0 || !searchWholeSceneIfNoCandidates)
        {
            return filtered.ToArray();
        }

        ObjectiveComputer[] sceneCandidates = FindObjectsOfType<ObjectiveComputer>(includeInactiveComputers);
        filtered = FilterCandidates(sceneCandidates);
        return filtered.ToArray();
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

    // 배열을 무작위로 섞는다.
    private void Shuffle(ObjectiveComputer[] array)
    {
        if (array == null)
        {
            return;
        }

        for (int i = array.Length - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            ObjectiveComputer temp = array[i];
            array[i] = array[randomIndex];
            array[randomIndex] = temp;
        }
    }
}
