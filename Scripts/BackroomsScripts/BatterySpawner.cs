using System.Collections.Generic;
using UnityEngine;

public class BatterySpawner : MonoBehaviour
{
    [Header("Spawn")]
    public GameObject batteryPickupPrefab;

    [Min(0)] public int minSpawn = 2;
    [Min(0)] public int maxSpawn = 3;

    [Tooltip("If empty, uses all children of this object as spawn points.")]
    public Transform[] spawnPoints;

    [Tooltip("Optional: don't spawn twice at the same point.")]
    public bool uniquePoints = true;

    void Start()
    {
        if (!batteryPickupPrefab)
        {
            Debug.LogWarning("[BatterySpawner] No batteryPickupPrefab assigned.");
            return;
        }

        Transform[] points = (spawnPoints != null && spawnPoints.Length > 0) ? spawnPoints : GetChildPoints();
        if (points.Length == 0)
        {
            Debug.LogWarning("[BatterySpawner] No spawn points found.");
            return;
        }

        int count = Random.Range(minSpawn, maxSpawn + 1);
        count = Mathf.Clamp(count, 0, points.Length);

        if (uniquePoints)
        {
            List<int> indices = new List<int>(points.Length);
            for (int i = 0; i < points.Length; i++) indices.Add(i);

            for (int i = 0; i < count; i++)
            {
                int pick = Random.Range(0, indices.Count);
                int idx = indices[pick];
                indices.RemoveAt(pick);

                SpawnAt(points[idx]);
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
                SpawnAt(points[Random.Range(0, points.Length)]);
        }
    }

    Transform[] GetChildPoints()
    {
        List<Transform> pts = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
            pts.Add(transform.GetChild(i));
        return pts.ToArray();
    }

    void SpawnAt(Transform p)
    {
        Instantiate(batteryPickupPrefab, p.position, p.rotation, transform);
    }
}
