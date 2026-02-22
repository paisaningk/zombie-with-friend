using FishNet.Object;
using Unity.Cinemachine;
using UnityEngine;

// This script will be a NetworkBehaviour so that we can use the OnStartClient override.
public class PlayerCamera : NetworkBehaviour
{
    [SerializeField] private CinemachineCamera _cinemachineCamera;

    // This method is called on the client after the object is spawned in.
    public override void OnStartClient()
    {
        // Simply enable our local cinemachine camera on the object if we are the owner.
        _cinemachineCamera.enabled = IsOwner;
    }
}
