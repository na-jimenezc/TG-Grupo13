using UnityEngine;

//Asigna la cámara principal al Canvas si no tiene ninguna asignada, para que el Canvas se renderice correctamente en VR
public class AsignarCamaraAlCanvas : MonoBehaviour
{
    private void Start()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera == null)
        {
            //CenterEyeAnchor tiene el tag "MainCamera", así que Camera.main lo encuentra
            //sin importar en qué escena esté, siempre que ya esté cargada.
            Camera camaraPrincipal = Camera.main;
            if (camaraPrincipal != null)
                canvas.worldCamera = camaraPrincipal;
            else
                Debug.LogWarning("[AsignarCamaraAlCanvas] No se encontró ninguna cámara con tag MainCamera.");
        }
    }
}

