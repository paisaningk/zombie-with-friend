using FishNet.Object;
using UnityEngine;

public class PlayerCubeCreator : NetworkBehaviour
{
    public NetworkObject CubePrefab;

    void Update()
    {
        // Only the local player object should perform these actions.
        if (!IsOwner)
            return;

        if (Input.GetButtonDown("Fire1"))
            SpawnCube();
    }

    // We are using a ServerRpc here because the Server needs to do all network object spawning.
    [ServerRpc]
    private void SpawnCube()
    {
        NetworkObject obj = Instantiate(CubePrefab, transform.position, Quaternion.identity);
        obj.GetComponent<SyncMaterialColor>().Color.Value = Random.ColorHSV();
        Spawn(obj); // NetworkBehaviour shortcut for ServerManager.Spawn(obj);
    }
}
