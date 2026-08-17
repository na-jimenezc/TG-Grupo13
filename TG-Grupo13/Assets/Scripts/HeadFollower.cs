using UnityEngine;

public class HeadFollower : MonoBehaviour
{
   public Transform cameraTransform;
    public Transform headBone;

    void LateUpdate()
    {
        headBone.position = cameraTransform.position;
        headBone.rotation = cameraTransform.rotation;
    }
}