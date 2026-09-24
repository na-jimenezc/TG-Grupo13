using UnityEngine;

public class PuntoHolsterTijerasRef : MonoBehaviour
{
    public static Transform Instance { get; private set; }

    private void Awake() => Instance = transform;

    private void OnDestroy()
    {
        if (Instance == transform) Instance = null;
    }
}