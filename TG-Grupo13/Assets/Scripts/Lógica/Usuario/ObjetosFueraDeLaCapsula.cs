using Oculus.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;

/*El "cuerpo" físico del jugador en el rig de Meta (Locomotor -> CharacterController) es una cápsula que choca con las
 * capas Default, TransparentFX, Ignore Raycast y Water, y que puede subirse a cualquier cosa de hasta 30 cm (_maxStep).
 * Todos los objetos de las actividades estaban en Default: al agacharse a recoger un bulbo, la cápsula quedaba encima
 * de los bulbos del piso y "subía el escalón": todo el rig (la cámara) subía, como volando, y al alejarse bajaba de
 * golpe. Además, al chocar con ellos la cámara "entraba" en el objeto y Meta oscurecía la vista (pantalla gris).
 *
 * Aquí, al cargar cada escena (y cada segundo, porque aparecen bulbos nuevos al esparcir la canasta), los objetos que se
 * pueden agarrar y las plantas/huecos pasan a la capa "Jugador": esa capa choca con el piso, las manos y los demás
 * objetos (la física y los agarres siguen igual), pero la cápsula del jugador la ignora.
 * No modifica escenas ni prefabs.*/
public class ObjetosFueraDeLaCapsula : MonoBehaviour
{
    private const string Capa = "Jugador";
    private const float SegundosEntreRevisiones = 1f;

    private static ObjetosFueraDeLaCapsula _instancia;
    private int _capa = -1;
    private float _proxima;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        if (_instancia != null) return;
        var go = new GameObject("ObjetosFueraDeLaCapsula");
        DontDestroyOnLoad(go);
        _instancia = go.AddComponent<ObjetosFueraDeLaCapsula>();
    }

    private void Awake()
    {
        _capa = LayerMask.NameToLayer(Capa);
        if (_capa < 0) Debug.LogWarning($"[Física] No existe la capa \"{Capa}\": los objetos siguen en su capa y la cápsula del jugador puede subirse a ellos.");
    }

    private void OnEnable() => SceneManager.sceneLoaded += AlCargarEscena;
    private void OnDisable() => SceneManager.sceneLoaded -= AlCargarEscena;
    private void AlCargarEscena(Scene escena, LoadSceneMode modo) => _proxima = 0f;

    private void Update()
    {
        if (_capa < 0 || Time.unscaledTime < _proxima) return;
        _proxima = Time.unscaledTime + SegundosEntreRevisiones;

        // Todo lo que se puede agarrar (bulbos, canasta, pala, ortigas, bolsa, tijeras, capuchones, ramos...).
        foreach (var agarrable in FindObjectsByType<Grabbable>(FindObjectsSortMode.None)) Mover(agarrable.gameObject);
        // Y lo que está en el piso de la actividad: huecos de siembra y tallos plantados.
        foreach (var hueco in FindObjectsByType<PlantingSlot>(FindObjectsSortMode.None)) Mover(hueco.gameObject);
        foreach (var tallo in FindObjectsByType<TalloEnPlantacion>(FindObjectsSortMode.None)) Mover(tallo.gameObject);
    }

    //El objeto y sus hijos (los colliders suelen estar en los hijos). Lo que ya está en una capa propia no se toca.
    private void Mover(GameObject raiz)
    {
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
        {
            int capa = t.gameObject.layer;
            if (capa == _capa) continue;
            if (capa == 0 || capa == 1 || capa == 4) t.gameObject.layer = _capa; //Default, TransparentFX, Water
        }
    }
}
