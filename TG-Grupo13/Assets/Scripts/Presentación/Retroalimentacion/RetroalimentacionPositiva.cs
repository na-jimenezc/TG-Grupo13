using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*Sistema de retroalimentación positiva durante las actividades, en tres niveles:
 *  1. Acción: cada acción correcta que suma al progreso (bulbo sembrado, ortiga depositada, tallo deshojado...)
 *     da una vibración corta en el control de la mano dominante (los sonidos y contadores ya existían).
 *  2. Avance: al llegar a la mitad de la meta y en cada ramo terminado, un mensaje breve.
 *  3. Tarea completada: al cumplir la meta, un mensaje de logro con un recordatorio postural en positivo,
 *     sonido de logro (si se asignó en el banco) y una vibración más larga en ambos controles.
 *
 * Pensado para un PC con gráfica modesta: un solo panel de texto que se reutiliza y se APAGA cuando no muestra nada,
 * sin partículas, sin animaciones y sin Update (solo reacciona al evento de progreso que ya existía).
 * Se crea solo al iniciar el juego (no hay que agregarlo a ninguna escena) y vive entre escenas.
 * Los textos salen de BancoMensajesPositivos. Otros scripts (p. ej. el de REBA) pueden usar MostrarMensaje().*/
public class RetroalimentacionPositiva : MonoBehaviour
{
    public static RetroalimentacionPositiva Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private float distanciaAlUsuario = 0.8f;   //Metros delante de la cabeza
    [SerializeField] private float alturaBajoLaVista = 0.2f;    //Metros por debajo de los ojos, para no tapar la tarea
    [SerializeField] private float duracionMensaje = 2.5f;
    [SerializeField] private float duracionLogro = 4f;
    [SerializeField] private Color colorFondo = new Color(0.13f, 0.5f, 0.25f, 0.85f);

    [Header("Vibración")]
    [SerializeField] private float amplitudAccion = 0.3f;
    [SerializeField] private float duracionVibracionAccion = 0.08f;
    [SerializeField] private float amplitudLogro = 0.6f;
    [SerializeField] private float duracionVibracionLogro = 0.3f;

    private BancoMensajesPositivos _banco;
    private GameObject _panel;
    private TextMeshProUGUI _texto;
    private AudioSource _audio;
    private Coroutine _ocultar;
    private Coroutine _vibracion;

    //Estado de la actividad que se está siguiendo, para detectar avances y no repetir mensajes
    private IActivityManager _manager;
    private float _ultimaFraccion;
    private int _ultimosRamos;
    private int _ultimosTallos;
    private bool _mitadMostrada;
    private bool _ramosListosMostrado;
    private bool _completadaMostrada;

    //Se crea solo al cargar la primera escena, así no hay que modificar Core
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CrearAutomaticamente()
    {
        if (Instance != null) return;
        var go = new GameObject("RetroalimentacionPositiva");
        DontDestroyOnLoad(go);
        go.AddComponent<RetroalimentacionPositiva>();
    }

    //Para mostrar un mensaje positivo desde otros scripts (p. ej. "¡Excelente postura!" desde el módulo de REBA)
    public static void MostrarMensaje(string mensaje)
    {
        if (Instance != null) Instance.Mostrar(mensaje, Instance.duracionMensaje);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _banco = BancoMensajesPositivos.Cargar();
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        ConstruirPanel();
    }

    private void OnEnable() => ActivityManagerBase.OnProgresoCambiado += AlCambiarProgreso;

    private void OnDisable()
    {
        ActivityManagerBase.OnProgresoCambiado -= AlCambiarProgreso;
        DetenerVibracion();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    //Se llama cada vez que un manager de actividad cambia sus contadores (y al crearse o destruirse)
    private void AlCambiarProgreso()
    {
        var manager = ActivityManagerBase.Current;

        //Cambió la actividad (empezó una nueva o se descargó): se toma su estado actual sin dar retroalimentación
        if (!ReferenceEquals(manager, _manager))
        {
            _manager = manager;
            ReiniciarEstado(manager);
            return;
        }
        if (manager == null) return;

        var corte = manager as CorteActivityManager;
        float fraccion = manager.FraccionProgreso;

        //En Corte la fracción no sirve para detectar avances: al terminar un ramo los tallos vuelven a 0 y la fracción
        //queda igual, y además está topada en 95 % hasta la recolección final. Ahí se usan los contadores.
        bool avanzo;
        if (corte != null)
        {
            avanzo = corte.RamosCompletados > _ultimosRamos || corte.TallosEnRamoActual > _ultimosTallos;
            _ultimosTallos = corte.TallosEnRamoActual;
        }
        else
        {
            avanzo = fraccion > _ultimaFraccion + 0.0001f;
        }
        _ultimaFraccion = fraccion;

        //Nivel 3: tarea completada (tiene prioridad sobre los demás)
        if (manager.ActividadCompletada && !_completadaMostrada)
        {
            _completadaMostrada = true;
            MostrarLogro(TipoDe(manager));
            return;
        }

        if (!avanzo) return;

        //Nivel 1: cada acción que suma al progreso
        Vibrar(amplitudAccion, duracionVibracionAccion, Lateralidad.ControlDominante);

        //Nivel 2: avances importantes
        var mensajes = _banco.Para(TipoDe(manager));
        if (corte != null)
        {
            if (corte.RamosCompletados <= _ultimosRamos) return;
            _ultimosRamos = corte.RamosCompletados;

            if (corte.RamosListos && !_ramosListosMostrado)
            {
                _ramosListosMostrado = true;
                Mostrar(_banco.ramosListos, duracionMensaje);
            }
            else if (!_mitadMostrada && corte.RamosCompletados * 2 >= corte.RamosParaCompletar)
            {
                _mitadMostrada = true;
                Mostrar(mensajes != null ? mensajes.mitad : null, duracionMensaje);
            }
            else
            {
                Mostrar(_banco.ramoListo
                    .Replace("{actual}", corte.RamosCompletados.ToString())
                    .Replace("{total}", corte.RamosParaCompletar.ToString()), duracionMensaje);
            }
        }
        else if (!_mitadMostrada && fraccion >= 0.5f)
        {
            _mitadMostrada = true;
            Mostrar(mensajes != null ? mensajes.mitad : null, duracionMensaje);
        }
    }

#if UNITY_EDITOR
    // SOLO EN EL EDITOR: en Play, seleccionar "RetroalimentacionPositiva" (en DontDestroyOnLoad) y en los tres puntos
    // del componente elegir una opción, para ver cómo se ven los mensajes sin gafas y sin completar la actividad.
    [ContextMenu("Probar mensaje de avance (nivel 2)")]
    private void ProbarAvance() => Mostrar(_banco.siembra.mitad, duracionMensaje);

    [ContextMenu("Probar mensaje de logro (nivel 3)")]
    private void ProbarLogro() => MostrarLogro(ActivityType.Siembra);
#endif

    private void ReiniciarEstado(IActivityManager manager)
    {
        Ocultar();
        _ultimaFraccion = manager != null ? manager.FraccionProgreso : 0f;
        _completadaMostrada = manager != null && manager.ActividadCompletada;
        _mitadMostrada = _ultimaFraccion >= 0.5f;

        var corte = manager as CorteActivityManager;
        _ultimosRamos = corte != null ? corte.RamosCompletados : 0;
        _ultimosTallos = corte != null ? corte.TallosEnRamoActual : 0;
        _ramosListosMostrado = corte != null && corte.RamosListos;
    }

    private static ActivityType? TipoDe(IActivityManager manager)
    {
        if (manager is SiembraActivityManager) return ActivityType.Siembra;
        if (manager is DeshierbeActivityManager) return ActivityType.Deshierbe;
        if (manager is CorteActivityManager) return ActivityType.Corte;
        return null;
    }

    private void MostrarLogro(ActivityType? tipo)
    {
        var mensajes = _banco.Para(tipo);
        string titulo = mensajes != null && !string.IsNullOrEmpty(mensajes.completada) ? mensajes.completada : "¡Actividad completada!";
        string recordatorio = mensajes != null ? mensajes.recordatorioPostural : "";

        Mostrar(string.IsNullOrEmpty(recordatorio) ? titulo : $"{titulo}\n<size=70%>{recordatorio}</size>", duracionLogro);

        if (_banco.sonidoLogro != null) _audio.PlayOneShot(_banco.sonidoLogro);
        Vibrar(amplitudLogro, duracionVibracionLogro, OVRInput.Controller.Touch); //Ambos controles
    }

    //Muestra el mensaje delante del usuario (fijo en el mundo, no pegado a la cabeza, para no marear) y lo oculta solo
    private void Mostrar(string mensaje, float duracion)
    {
        if (string.IsNullOrEmpty(mensaje) || _panel == null) return;

        _texto.text = Lateralidad.Formatear(mensaje);
        UbicarFrenteAlUsuario();
        _panel.SetActive(true);

        if (_ocultar != null) StopCoroutine(_ocultar);
        _ocultar = StartCoroutine(OcultarTras(duracion));
    }

    private IEnumerator OcultarTras(float segundos)
    {
        yield return new WaitForSecondsRealtime(segundos);
        _ocultar = null;
        Ocultar();
    }

    private void Ocultar()
    {
        if (_ocultar != null) StopCoroutine(_ocultar);
        _ocultar = null;
        if (_panel != null) _panel.SetActive(false);
    }

    private void UbicarFrenteAlUsuario()
    {
        var camara = Camera.main;
        if (camara == null) return;

        //Se ignora la inclinación de la cabeza para que el panel quede derecho y a la altura de la vista
        Vector3 frente = Vector3.ProjectOnPlane(camara.transform.forward, Vector3.up);
        if (frente.sqrMagnitude < 0.001f) frente = camara.transform.up;
        frente.Normalize();

        _panel.transform.position = camara.transform.position + frente * distanciaAlUsuario + Vector3.down * alturaBajoLaVista;
        _panel.transform.rotation = Quaternion.LookRotation(frente, Vector3.up);
    }

    private void Vibrar(float amplitud, float duracion, OVRInput.Controller control)
    {
        DetenerVibracion();
        _vibracion = StartCoroutine(VibrarRutina(amplitud, duracion, control));
    }

    private IEnumerator VibrarRutina(float amplitud, float duracion, OVRInput.Controller control)
    {
        OVRInput.SetControllerVibration(1f, amplitud, control);
        yield return new WaitForSecondsRealtime(duracion);
        OVRInput.SetControllerVibration(0f, 0f, control);
        _vibracion = null;
    }

    private void DetenerVibracion()
    {
        if (_vibracion != null) StopCoroutine(_vibracion);
        _vibracion = null;
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Touch);
    }

    //Panel en el mundo (World Space) de 80 x 20 cm: fondo verde y un texto que se ajusta solo
    private void ConstruirPanel()
    {
        _panel = new GameObject("PanelRetroalimentacion", typeof(RectTransform), typeof(Canvas));
        _panel.transform.SetParent(transform, false);
        var canvas = _panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rtPanel = (RectTransform)_panel.transform;
        rtPanel.sizeDelta = new Vector2(800f, 200f);
        _panel.transform.localScale = Vector3.one * 0.001f;

        var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        fondo.transform.SetParent(_panel.transform, false);
        Estirar((RectTransform)fondo.transform, 0f);
        fondo.GetComponent<Image>().color = colorFondo;

        var textoGo = new GameObject("Texto", typeof(RectTransform));
        textoGo.transform.SetParent(_panel.transform, false);
        Estirar((RectTransform)textoGo.transform, 20f);
        _texto = textoGo.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) _texto.font = TMP_Settings.defaultFontAsset;
        _texto.alignment = TextAlignmentOptions.Center;
        _texto.color = Color.white;
        _texto.richText = true;
        _texto.enableAutoSizing = true;
        _texto.fontSizeMin = 28f;
        _texto.fontSizeMax = 60f;

        _panel.SetActive(false);
    }

    private static void Estirar(RectTransform rt, float margen)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margen, margen);
        rt.offsetMax = new Vector2(-margen, -margen);
    }
}
