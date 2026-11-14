using System.Collections.Generic;
using UnityEngine;

// WorldManager에서 분리: 카메라 섹터 기준 구독 영역 계산 담당 (MonoBehaviour 아님, 네트워크 호출 없음)
public class SectorGrid
{
    private readonly HashSet<Vector3Int> activeSectors = new HashSet<Vector3Int>();

    // 새 중심 섹터 기준으로 새로 구독할 섹터(3x3x3 코어)와 해제할 섹터(5x5x5 버퍼 이탈)를 계산하고 내부 상태를 갱신
    public (List<Vector3Int> toSubscribe, List<Vector3Int> toUnsubscribe) Recenter(Vector3Int newSector)
    {
        HashSet<Vector3Int> newCoreSectors = new HashSet<Vector3Int>();   // 3x3x3 구독(요청) 구역
        HashSet<Vector3Int> newBufferSectors = new HashSet<Vector3Int>(); // 5x5x5 유지(버퍼) 구역

        for (int x = -2; x <= 2; x++)
        {
            for (int y = -2; y <= 2; y++)
            {
                for (int z = -2; z <= 2; z++)
                {
                    Vector3Int sec = new Vector3Int(newSector.x + x, newSector.y + y, newSector.z + z);

                    // 5x5x5 버퍼 영역
                    newBufferSectors.Add(sec);

                    // 3x3x3 코어 영역
                    if (Mathf.Abs(x) <= 1 && Mathf.Abs(y) <= 1 && Mathf.Abs(z) <= 1)
                    {
                        newCoreSectors.Add(sec);
                    }
                }
            }
        }

        List<Vector3Int> sectorsToSubscribe = new List<Vector3Int>();
        List<Vector3Int> sectorsToUnsubscribe = new List<Vector3Int>();

        // 1. 구독 해제: 기존 활성화된 섹터 중 5x5x5 버퍼를 완전히 벗어난 섹터
        foreach (var sector in activeSectors)
        {
            if (!newBufferSectors.Contains(sector))
            {
                sectorsToUnsubscribe.Add(sector);
            }
        }

        // 2. 새로 구독: 3x3x3 코어 영역에 새로 들어온 섹터
        foreach (var sector in newCoreSectors)
        {
            if (!activeSectors.Contains(sector))
            {
                sectorsToSubscribe.Add(sector);
            }
        }

        // activeSectors 갱신
        foreach (var sector in sectorsToUnsubscribe) activeSectors.Remove(sector);
        foreach (var sector in sectorsToSubscribe) activeSectors.Add(sector);

        return (sectorsToSubscribe, sectorsToUnsubscribe);
    }
}
