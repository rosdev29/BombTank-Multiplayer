using System.Collections.Generic;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    private static readonly List<SpawnPoint> spawnPoints = new List<SpawnPoint>();

    private void OnEnable()
    {
        if (!spawnPoints.Contains(this))
        {
            spawnPoints.Add(this);
        }
    }

    private void OnDisable()
    {
        spawnPoints.Remove(this);
    }

    public static Vector3 GetRandomSpawnPos()
    {
        if (spawnPoints.Count == 0)
        {
            return Vector3.zero;
        }

        return spawnPoints[Random.Range(0, spawnPoints.Count)].transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.position, 1f);
    }
}
