using UnityEngine;

public class VRBodyTracking : MonoBehaviour{
    public Transform headTarget;
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    public Transform vrHead;
    public Transform vrLeftHand;
    public Transform vrRightHand;

    void LateUpdate(){
        vrHead.position = headTarget.position;
        vrHead.rotation = headTarget.rotation;

        vrLeftHand.position = leftHandTarget.position;
        vrLeftHand.rotation = leftHandTarget.rotation;

        vrRightHand.position = rightHandTarget.position;
        vrRightHand.rotation = rightHandTarget.rotation;
    }
}