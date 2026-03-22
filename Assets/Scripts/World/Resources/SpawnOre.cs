using System;
using FishNet.Object;
using UnityEngine;

public class SpawnOre : NetworkBehaviour
{
    public NetworkBehaviour Ore;
    public Transform SpawnPoint;
    public GameObject InteractPrompt;
    public KeyCode InteractKey = KeyCode.E;
    public float SpawnInterval = 0.5f;

    [SerializeField] private bool AutoSpawnActive;
    [SerializeField] private bool LocalPlayerInRange;
    [SerializeField] private float SpawnTimer;

    public void Update()
    {
        if (!LocalPlayerInRange)
        {
            return;
        }

        if (Input.GetKeyDown(InteractKey))
        {
            AutoSpawnActive = true;
            SetActiveInteractPrompt(false);
        }

        if (!AutoSpawnActive)
        {
            return;
        }

        SpawnTimer += Time.deltaTime;

        if (SpawnTimer < SpawnInterval)
        {
            return;
        }

        SpawnTimer = 0;
        SpawnCube();
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void SpawnCube()
    {
        var obj = Instantiate(Ore, SpawnPoint.position, Quaternion.identity);
        Spawn(obj); // NetworkBehaviour shortcut for ServerManager.Spawn(obj);
    }

    private void SetActiveInteractPrompt(bool open)
    {
        if (InteractPrompt != null)
        {
            InteractPrompt.SetActive(open);
        }
    }
    

    public void OnTriggerEnter(Collider other)
    {
        if (!other.gameObject.CompareTag("Player"))
        {
            return;
        }

        if (!other.TryGetComponent(out NetworkObject networkObject) || !networkObject.IsOwner)
        {
            return;
        }

        LocalPlayerInRange = true;
        AutoSpawnActive = false;
        SpawnTimer = 0f;

        if (InteractPrompt != null)
        {
            InteractPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.gameObject.CompareTag("Player"))
        {
            return;
        }

        if (!other.TryGetComponent<NetworkObject>(out NetworkObject networkObject) || !networkObject.IsOwner)
        {
            return;
        }

        LocalPlayerInRange = false;
        AutoSpawnActive = false;
        SpawnTimer = 0f;

        if (InteractPrompt != null)
        {
            InteractPrompt.SetActive(false);
        }
    }

    private void OnDisable()
    {
        AutoSpawnActive = false;
        SpawnTimer = 0f;

        if (InteractPrompt != null)
        {
            InteractPrompt.SetActive(false);
        }
    }
}
