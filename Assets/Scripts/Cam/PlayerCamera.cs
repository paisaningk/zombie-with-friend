using System;
using FishNet.Component.Spawning;
using FishNet.Object;
using Unity.Cinemachine;
using UnityEngine;

// This script will be a NetworkBehaviour so that we can use the OnStartClient override.
public class PlayerCamera : NetworkBehaviour
{
    [SerializeField] private CinemachineCamera CinemachineCamera;

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        CinemachineCamera.enabled = IsOwner;
        
        if (IsOwner)
        { 
            CinemachineCamera.transform.SetParent(null, true);
        }
    }
}
