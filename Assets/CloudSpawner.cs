using UnityEngine;

public class CloudSpawner : MonoBehaviour
{
    [SerializeField] GameObject[] CloudPrefab;
    [SerializeField] float spawnInterval = 2f;
    [SerializeField] Vector3 spawnPosition;

    void Start()
    {

        InvokeRepeating("SpawnCloud", 0f, spawnInterval);
    }
    void FixedUpdate()
    {
    }
    private void SpawnCloud()
    {
        spawnInterval = Random.Range(1f*Time.deltaTime, 3f*Time.deltaTime);
        spawnPosition = new Vector3(-20, Random.Range(10f, 30f), Random.Range(1f, 35f));
        int randomIndex = Random.Range(0, CloudPrefab.Length);
        GameObject cloud = Instantiate(CloudPrefab[randomIndex], spawnPosition, Quaternion.identity);
        Destroy(cloud, 90f);
    }
}
