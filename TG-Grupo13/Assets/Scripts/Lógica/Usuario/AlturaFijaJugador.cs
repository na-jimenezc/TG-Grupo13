using UnityEngine;
using UnityEngine.SceneManagement;

/*Evita que el jugador "vuele" o se hunda al agacharse (mareo).
 * El "cuerpo" físico del jugador en el rig de Meta (Locomotor -> CharacterController) es una cápsula que, al agacharse
 * mucho (p. ej. a recoger un bulbo), no se encoge lo suficiente, choca con el piso y empuja todo el rig hacia arriba
 * hasta 30 cm (_maxStep); al levantarse, baja de golpe.
 * Los pisos de las actividades son planos, así que el rig nunca necesita subir ni bajar: si algo lo mueve en vertical,
 * se devuelve a la altura del piso antes de dibujar la imagen. La altura se toma al cargar cada escena, cuando el rig
 * ya se acomodó. Agacharse de verdad se sigue viendo (eso es el visor, no el rig).
 * Cada corrección grande queda en eventos.csv de REBA ("altura_rig").
 * Se crea solo al iniciar el juego (no hay que ponerlo en ninguna escena) y no modifica escenas ni prefabs.*/
[DefaultExecutionOrder(32000)]
public class AlturaFijaJugador : MonoBehaviour
{
    [Tooltip("Mantener el rig a la altura del piso (sin subir ni bajar).")]
    [SerializeField] private bool fijarAltura = true;
    [Tooltip("Segundos después de cargar una escena para tomar la altura del piso (el rig ya se acomodó).")]
    [SerializeField] private float esperaTomarAltura = 1.5f;
    [Tooltip("Correcciones más pequeñas que esto (m) no se anotan en eventos.csv.")]
    [SerializeField] private float umbralRegistro = 0.03f;

    private static AlturaFijaJugador _instancia;
    private Transform _espacio;      // TrackingSpace: con "Floor Level" su altura es la del piso
    private bool _alturaTomada;
    private float _alturaPiso, _tomarAlturaEn, _proximoRegistro;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        if (_instancia != null) return;
        var go = new GameObject("AlturaFijaJugador");
        DontDestroyOnLoad(go);
        _instancia = go.AddComponent<AlturaFijaJugador>();
    }

    private void Awake() => _tomarAlturaEn = Time.unscaledTime + esperaTomarAltura;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += AlCargarEscena;
        Application.onBeforeRender += FijarAltura;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        Application.onBeforeRender -= FijarAltura;
    }

    //Cada escena puede tener su piso a otra altura: se vuelve a tomar cuando el rig se acomode.
    private void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        _alturaTomada = false;
        _tomarAlturaEn = Time.unscaledTime + esperaTomarAltura;
    }

    //Después de que todo se movió (locomoción de Meta incluida) y otra vez justo antes de dibujar.
    private void LateUpdate() => FijarAltura();

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
        if (Mathf.Abs(desvio) >= umbralRegistro) Registrar(desvio);
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

    private void Registrar(float desvio)
    {
        if (Time.unscaledTime < _proximoRegistro) return;
        _proximoRegistro = Time.unscaledTime + 0.5f;
        var grabador = FindAnyObjectByType<PujJoycon.RebaSessionRecorder>();
        if (grabador != null)
            grabador.RegistrarEvento("altura_rig", $"el rig se {(desvio > 0 ? "subió" : "bajó")} {Mathf.Abs(desvio) * 100f:0} cm (corregido)");
    }
}
