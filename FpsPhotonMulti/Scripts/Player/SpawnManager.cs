using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager instance;

    private void Awake()
    {
        instance = this;
    }

    public Transform[] spawnPoints;

    private void Start()
    {
        foreach(Transform spawn in spawnPoints)
        {
            spawn.gameObject.SetActive(false);
        }

    }

    public Transform GetSpawnPoint()
    {
        return spawnPoints[Random.Range(0, spawnPoints.Length )];
    }


}
