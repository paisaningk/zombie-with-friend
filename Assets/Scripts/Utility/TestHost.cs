using FishNet;
using Sirenix.OdinInspector;
using UnityEngine;
namespace Utility
{
    public class TestHost : MonoBehaviour
    {
        [Button]
        public void IsHost()
        {
            Debug.Log($"IsHostStarted = {InstanceFinder.IsHostStarted}");
        }
    }
}
