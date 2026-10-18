using UnityEngine;
using UnityEngine.SceneManagement;

/*Destruye el objeto cuando se descarga la escena de la que salió.
 * Al pegar un objeto a las manos o al hombro del jugador (que viven en Core, que nunca se descarga),
 * Unity lo muda a la escena Core y ya no se iría al descargar la actividad o el tutorial.
 * Se registra ANTES de cambiarlo de padre (ver Registrar), para recordar su escena original.*/
public class DestruirConEscenaOrigen : MonoBehaviour
{
    private Scene _escenaOrigen;

    //Agrega el componente al objeto recordando la escena en la que está ahora. Si ya lo tenía, conserva la primera.
    public static void Registrar(GameObject objeto)
    {
        if (objeto == null || objeto.GetComponent<DestruirConEscenaOrigen>() != null) return;

        var componente = objeto.AddComponent<DestruirConEscenaOrigen>();
        componente._escenaOrigen = objeto.scene;
    }

    //Se usa Awake/OnDestroy (y no OnEnable/OnDisable) para que también se limpie si el objeto está desactivado
    private void Awake() => SceneManager.sceneUnloaded += AlDescargarEscena;
    private void OnDestroy() => SceneManager.sceneUnloaded -= AlDescargarEscena;

    private void AlDescargarEscena(Scene escena)
    {
        if (escena == _escenaOrigen)
            Destroy(gameObject);
    }
}
