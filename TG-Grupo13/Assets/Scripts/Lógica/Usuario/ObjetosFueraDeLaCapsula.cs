using Oculus.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;

/*Evita que el jugador "vuele" o se hunda (mareo) al agacharse a recoger objetos.
 *
 * 1. Objetos fuera de la cápsula: el "cuerpo" físico del jugador en el rig de Meta (Locomotor -> CharacterController)
 *    es una cápsula que choca con Default, TransparentFX, Ignore Raycast y Water, y que se sube a lo que mida hasta
 *    30 cm (_maxStep). Los objetos de las actividades estaban en Default: la cápsula se subía a ellos y la cámara subía.
 *    Aquí lo que se puede agarrar pasa a la capa "Jugador", que choca con el piso, las manos y los demás objetos (la
 *    física y los agarres siguen igual), pero que la cápsula ignora.
 *
 * 2. Altura fija: los pisos de las actividades son planos, así que el rig nunca necesita subir ni bajar. Si algo lo
 *    mueve en vertical (lo que sea), se devuelve a la altura del piso antes de dibujar la imagen. La altura se toma
 *    al cargar cada escena (cuando el rig está quieto).
 *
 * 3. Diagnóstico: cada vez que hay que corregir la altura, se anota en la Console (y en eventos.csv de REBA) cuánto y
 *    qué objeto había debajo, para encontrar la causa.
 *
 * Se crea solo al iniciar el juego (no hay que ponerlo en ninguna escena) y no modifica escenas ni prefabs.*/
[DefaultExecutionOrder(32000)]
public class ObjetosFueraDeLaCapsula : MonoBehaviour
{
    private const string Capa = "Jugador";
    private const float SegundosEntreRevisiones = 1f;

    [Tooltip("Mantener el rig a la altura del piso (sin subir ni bajar).")]
    [SerializeField] private bool fijarAltura = true;
    [Tooltip("Segundos después de cargar una escena para tomar la altura del piso (el rig ya se acomodó).")]
    [SerializeField] private float esperaTomarAltura = 1.5f;
    [Tooltip("Correcciones más pequeñas que esto (m) no se anotan en el diagnóstico.")]
    [SerializeField] private float umbralDiagnostico = 0.03f;

    private static ObjetosFueraDeLaCapsula _instancia;
    private int _capa = -1;
    private float _proxima;

    private Transform _espacio;      // TrackingSpace: su altura es la del piso con "Floor Level"
    private bool _alturaTomada;
    private float _alturaPiso, _tomarAlturaEn;
    private float _proximoAviso;

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
        _tomarAlturaEn = Time.unscaledTime + esperaTomarAltura;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += AlCargarEscena;
        Application.onBeforeRender += AntesDeDibujar;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        Application.onBeforeRender -= AntesDeDibujar;
    }

    private void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        _proxima = 0f;
        // Cada escena puede tener su piso a otra altura: se vuelve a tomar cuando el rig se acomode.
        _alturaTomada = false;
        _tomarAlturaEn = Time.unscaledTime + esperaTomarAltura;
    }

    private void Update()
    {
        if (_capa < 0 || Time.unscaledTime < _proxima) return;
        _proxima = Time.unscaledTime + SegundosEntreRevisiones;

        // Solo lo que se puede agarrar (bulbos, canasta, pala, ortigas, bolsa, tijeras, capuchones, ramos...): es lo que
        // queda suelto en el piso. Los huecos y tallos plantados no se tocan (de eso ya se encarga la altura fija).
        foreach (var agarrable in FindObjectsByType<Grabbable>(FindObjectsSortMode.None)) Mover(agarrable.gameObject);
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

    //Después de que todo se movió (locomoción de Meta incluida) y antes de dibujar: el rig vuelve a la altura del piso.
    private void LateUpdate() => FijarAltura();
    private void AntesDeDibujar() => FijarAltura();

    private void FijarAltura()
    {
        if (!fijarAltura || !BuscarEspacio()) return;

        if (!_alturaTomada)
        {
            if (Time.unscaledTime < _tomarAlturaEn) return;
            _alturaPiso = _espacio.position.y;
            _alturaTomada = true;
            return;
        }

        float desvio = _espacio.position.y - _alturaPiso;
        if (Mathf.Abs(desvio) < 0.001f) return;

        if (Mathf.Abs(desvio) >= umbralDiagnostico) Diagnosticar(desvio);
        // Se mueve la raíz del jugador (sea cual sea el objeto que movió la locomoción, el TrackingSpace la sigue).
        _espacio.root.position -= Vector3.up * desvio;
    }

    private bool BuscarEspacio()
    {
        if (_espacio != null) return true;
        var camara = Camera.main;
        if (camara == null || camara.transform.parent == null) return false;
        _espacio = camara.transform.parent; // CenterEyeAnchor -> TrackingSpace
        return true;
    }

    // Qué hay debajo de la cabeza (sin contar los triggers): ahí es donde la cápsula se habría "subido".
    private void Diagnosticar(float desvio)
    {
        if (Time.unscaledTime < _proximoAviso) return;
        _proximoAviso = Time.unscaledTime + 0.5f;

        var camara = Camera.main;
        string debajo = "nada";
        if (camara != null && Physics.Raycast(camara.transform.position, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            var c = hit.collider;
            bool delJugador = c.transform.IsChildOf(_espacio.root);
            debajo = $"{c.name} (capa {LayerMask.LayerToName(c.gameObject.layer)}, de {c.transform.root.name}{(delJugador ? ", PARTE DEL JUGADOR" : "")}) a {hit.distance:0.00} m";
        }
        string texto = $"el rig se {(desvio > 0 ? "subió" : "bajó")} {Mathf.Abs(desvio) * 100f:0} cm (corregido). Debajo de la cabeza: {debajo}";
        Debug.LogWarning("[Física] " + texto);
        var grabador = FindAnyObjectByType<PujJoycon.RebaSessionRecorder>();
        if (grabador != null) grabador.RegistrarEvento("altura_rig", texto);
    }
}
