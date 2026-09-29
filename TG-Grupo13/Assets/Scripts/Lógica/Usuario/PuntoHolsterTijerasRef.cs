using UnityEngine;
/*Expone el punto de enganche en la cadera del avatar.
/Permite que las tijeras ubiquen dinámicamente este punto sin referencias estáticas complejas.*/
public class PuntoHolsterTijerasRef : MonoBehaviour
{

    public static Transform Instance { get; private set; }

    private void Awake() => Instance = transform;

    private void OnDestroy()
    {
        if (Instance == transform) Instance = null;
    }
}