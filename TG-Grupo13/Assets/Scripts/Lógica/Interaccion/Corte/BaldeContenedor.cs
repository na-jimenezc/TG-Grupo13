using UnityEngine;
using UnityEngine.SceneManagement;

/*Se coloca en el balde (en el mismo GameObject que su ZonaDeposito con EsZonaDeRamos = false, o en un padre).
 *Al descargar todos los ramos, muestra el prefab "balde con lirios" en lugar del balde vacío.*/
public class BaldeContenedor : MonoBehaviour
{
    [Tooltip("Prefab del balde ya lleno de lirios (solo visual: sin scripts, Rigidbody ni Grabbable).")]
    [SerializeField] private GameObject prefabBaldeLleno;

    [Tooltip("Dónde aparece el prefab. Si se deja vacío, usa la posición y rotación de este objeto.")]
    [SerializeField] private Transform puntoAparicion;

    [Tooltip("Modelo del balde vacío (solo el visual). Se ocultan sus Renderers; el objeto NO se desactiva para no perder el trigger.")]
    [SerializeField] private GameObject modeloBaldeVacio;

    private bool _lleno = false;
    private GameObject _instancia;

    public bool EstaLleno => _lleno;

    public void MostrarLleno()
    {
        if (_lleno) return;

        if (prefabBaldeLleno == null)
        {
            Debug.LogWarning("[BaldeContenedor] Falta asignar Prefab Balde Lleno.", this);
            return;
        }

        _lleno = true;

        Transform punto = puntoAparicion != null ? puntoAparicion : transform;
        _instancia = Instantiate(prefabBaldeLleno, punto.position, punto.rotation);

        //Instantiate crea el objeto en la escena ACTIVA (que puede ser Core). Se mueve a la escena del balde
        //para que se descargue junto con la actividad y no quede huérfano en las siguientes.
        SceneManager.MoveGameObjectToScene(_instancia, gameObject.scene);

        //Oculta el balde vacío sin desactivar el GameObject (podría contener el trigger o este script)
        if (modeloBaldeVacio != null)
            foreach (var r in modeloBaldeVacio.GetComponentsInChildren<Renderer>())
                r.enabled = false;

        Debug.Log("[BaldeContenedor] Balde lleno mostrado.");
    }
}