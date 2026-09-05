using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SpawnEntry
{
    public string id;   
    public Transform point;   
}

public class SpawnPointCollection : MonoBehaviour
{
    [Tooltip("내부 → 마을 진입 시 사용할 스폰 포인트들을 등록하세요.")]
    public List<SpawnEntry> entries = new List<SpawnEntry>();

    private Dictionary<string, Transform> map;

    void Awake()
    {
        map = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (var e in entries)
        {
            if (!string.IsNullOrEmpty(e.id) && e.point != null && !map.ContainsKey(e.id))
                map.Add(e.id, e.point);
        }
    }

    public bool TryGet(string id, out Transform t)
    {
        if (map == null) { t = null; return false; }
        return map.TryGetValue(id, out t);
    }
}

