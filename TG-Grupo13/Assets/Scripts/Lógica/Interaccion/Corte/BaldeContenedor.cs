using UnityEngine;
using UnityEngine.SceneManagement;

/*Se coloca en el balde sirve como contenedor de los ramos que se van a descargar. Cuando la mano deposita todos los ramos, se llama al método MostrarLleno() 
 * para mostrar el prefab del balde lleno de lirios en vez del balde vacio*/
public class BaldeContenedor : MonoBehaviour
{
    [Tooltip("Prefab del balde ya lleno de lirios.")]
    [SerializeField] private GameObject prefabBaldeLleno;

    [Tooltip("Punto de aparicion del prefab")]
    [SerializeField] private Transform puntoAparicion;

    [Tooltip("Modelo del balde vacío")]
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

        //Se crea un objeto en la escena activa. Se mueve a la escena del balde
        //para que se descargue junto con la actividad y no quede huérfano en las siguientes.
        SceneManager.MoveGameObjectToScene(_instancia, gameObject.scene);

        //Oculta el balde vacío sin desactivar el GameObject (podría contener el trigger o este script)
        if (modeloBaldeVacio != null)
            foreach (var r in modeloBaldeVacio.GetComponentsInChildren<Renderer>())
                r.enabled = false;

        Debug.Log("[BaldeContenedor] Balde lleno mostrado.");
    }
}