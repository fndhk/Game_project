using UnityEngine;

// 이 구조체는 스캔으로 생성된 점 하나의 데이터를 저장한다.
[System.Serializable]
public struct RevealDot
{
    // 각 점을 구분하기 위한 고유 번호이다.
    public int id;

    // 점이 놓일 월드 좌표이다.
    public Vector3 worldPos;

    // 점의 실제 크기이다.
    public float size;

    // 이 점이 생성된 시간이다.
    public float spawnTime;

    // 이 점이 얼마나 오래 유지될지 정하는 시간이다.
    public float lifetime;

    // 생성 시 필요한 값을 한 번에 넣기 위한 생성자이다.
    public RevealDot(int id, Vector3 worldPos, float size, float spawnTime, float lifetime)
    {
        // 고유 번호를 저장한다.
        this.id = id;

        // 점 위치를 저장한다.
        this.worldPos = worldPos;

        // 점 크기를 저장한다.
        this.size = size;

        // 생성 시간을 저장한다.
        this.spawnTime = spawnTime;

        // 유지 시간을 저장한다.
        this.lifetime = lifetime;
    }

    // 현재 시간이 주어졌을 때 이 점이 수명을 다했는지 반환한다.
    public bool IsExpired(float currentTime)
    {
        // lifetime이 음수면 영구 유지로 처리한다.
        if (lifetime < 0f)
        {
            return false;
        }

        // lifetime이 0이면 즉시 만료로 처리한다.
        if (lifetime == 0f)
        {
            return true;
        }

        // 현재 시간이 생성 시간과 유지 시간 합을 넘었는지 반환한다.
        return currentTime >= spawnTime + lifetime;
    }
}