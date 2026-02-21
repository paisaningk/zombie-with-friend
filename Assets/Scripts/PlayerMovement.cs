using FishNet.Object;
using UnityEngine;

// Inherit from NetworkBehaviour instead of MonoBehaviour
public class PlayerMovement : NetworkBehaviour
{
    public float MoveSpeed = 5f;

    private void Update()
    {
        // Only run this code on the object the local client owns.
        // This prevents us from moving other players' objects.
        if (!IsOwner)
            return;

        var horizontal = Input.GetAxis("Horizontal");
        var vertical = Input.GetAxis("Vertical");

        var moveDirection = new Vector3(horizontal, 0f, vertical);
        if (moveDirection.magnitude > 1f)
            moveDirection.Normalize();

        transform.position += MoveSpeed * Time.deltaTime * moveDirection;
    }
}