using UnityEngine;
using TMPro;
using System.Collections;

public class ControlActividadUI : MonoBehaviour
{
    [SerializeField] private TMP_Text tituloActividadText;

    //Botón principal del panel. Para comparar la postura antes y después del tutorial, cada actividad sigue siempre
    //el mismo orden y el botón cambia de texto en cada momento:
    //Menú ("Comenzar") -> Actividad pre ("Ir al tutorial") -> Tutorial ("Saltar a la actividad") -> Actividad post ("Continuar").
    [SerializeField] private TMP_Text comenzarButtonTexto;
    [SerializeField] private UnityEngine.UI.Button comenzarButton;
    private bool _avanzando = false;
    private IActivityManager _managerSuscrito;
    private Coroutine _esperaManager;

    private enum Etapa { Menu, ActividadPre, Tutorial, ActividadPost }

    [Header("Botón Tutorial (se oculta: el tutorial va siempre entre la actividad pre y la post)")]
    [SerializeField] private UnityEngine.UI.Button tutorialButton;

    [Header("Indicador de progreso (opcional)")]
    [SerializeField] private UnityEngine.UI.Image indicadorCondicion;
    [SerializeField] private Color colorCondicionCumplida = Color.green;
    [SerializeField] private Color colorCondicionPendiente = Color.gray;

    [Header("Confirmación si avanza sin cumplir la condición")]
    [SerializeField] private GameObject panelConfirmacionAvanceForzado;

    [Header("Progreso del usuario (se crea solo si se deja vacío)")]
    [SerializeField] private ProgresoAdminUI progresoAdmin;

    [Header("Terminar sesión (se crea copiando el botón Comenzar si se deja vacío)")]
    [SerializeField] private UnityEngine.UI.Button terminarSesionButton;
    [SerializeField] private Vector2 posicionBotonTerminar = new Vector2(0f, -260f);
    [SerializeField] private Color colorBotonTerminar = new Color(0.75f, 0.2f, 0.2f);

    //Confirmación en el mismo botón: el primer clic pide confirmar y solo un segundo clic (dentro del tiempo) termina
    private const float SEGUNDOS_PARA_CONFIRMAR = 3f;
    private const string TEXTO_TERMINAR = "Terminar sesión";
    private TMP_Text _terminarTexto;
    private bool _confirmandoTerminar = false;
    private Coroutine _restaurarTerminar;

    void Awake()
    {
        //El bloque que muestra en qué paso del tutorial o de la actividad va el usuario.
        if (progresoAdmin == null)
            progresoAdmin = GetComponentInChildren<ProgresoAdminUI>(true);
        if (progresoAdmin == null)
            progresoAdmin = ProgresoAdminUI.Crear((RectTransform)transform, tituloActividadText != null ? tituloActividadText.font : null);

        PrepararBotonTerminar();
        OcultarBotonTutorial();
    }

    //Con un botón aparte el Game Master podía abrir el tutorial desde el menú y saltarse la actividad pre.
    //Se busca por el método que tiene conectado, para no tener que modificar la escena Core.
    private void OcultarBotonTutorial()
    {
        if (tutorialButton == null)
        {
            foreach (var boton in GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                for (int i = 0; i < boton.onClick.GetPersistentEventCount(); i++)
                {
                    if (boton.onClick.GetPersistentMethodName(i) == nameof(OnTutorialPresionado))
                        tutorialButton = boton;
                }
            }
        }

        if (tutorialButton != null) tutorialButton.gameObject.SetActive(false);
    }

    //Crea (si no está asignado) el botón para cortar la sesión en cualquier momento, p. ej. si el participante se marea.
    //Se copia el botón Comenzar para que tenga el mismo estilo, sin tener que modificar la escena Core.
    private void PrepararBotonTerminar()
    {
        if (terminarSesionButton == null && comenzarButton != null)
        {
            var copia = Instantiate(comenzarButton.gameObject, comenzarButton.transform.parent);
            copia.name = "TerminarSesionButton";
            ((RectTransform)copia.transform).anchoredPosition = posicionBotonTerminar;

            terminarSesionButton = copia.GetComponent<UnityEngine.UI.Button>();
            //Se reemplaza el evento para no heredar el "Comenzar" que trae la copia
            terminarSesionButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            if (terminarSesionButton.image != null) terminarSesionButton.image.color = colorBotonTerminar;
        }

        if (terminarSesionButton == null) return;

        _terminarTexto = terminarSesionButton.GetComponentInChildren<TMP_Text>(true);
        if (_terminarTexto != null)
        {
            //El texto se reduce solo si no cabe en el botón, sin pasar del tamaño original
            _terminarTexto.fontSizeMax = _terminarTexto.fontSize;
            _terminarTexto.fontSizeMin = Mathf.Min(14f, _terminarTexto.fontSize);
            _terminarTexto.enableAutoSizing = true;
        }
        terminarSesionButton.onClick.AddListener(OnTerminarSesionPresionado);
        ReiniciarBotonTerminar();
    }

    private void ReiniciarBotonTerminar()
    {
        _confirmandoTerminar = false;
        if (_restaurarTerminar != null) StopCoroutine(_restaurarTerminar);
        _restaurarTerminar = null;
        if (_terminarTexto != null) _terminarTexto.text = TEXTO_TERMINAR;
        if (terminarSesionButton != null) terminarSesionButton.interactable = true;
    }

    public void OnTerminarSesionPresionado()
    {
        //Segundo clic dentro del tiempo: se termina la sesión
        if (_confirmandoTerminar)
        {
            _confirmandoTerminar = false;
            if (terminarSesionButton != null) terminarSesionButton.interactable = false;
            DesuscribirseDelManager();
            GameFlowManager.Instance?.TerminarSesionAnticipada();
            return;
        }

        _confirmandoTerminar = true;
        if (_terminarTexto != null) _terminarTexto.text = "¿Seguro?";
        if (_restaurarTerminar != null) StopCoroutine(_restaurarTerminar);
        _restaurarTerminar = StartCoroutine(RestaurarBotonTerminar());
    }

    private IEnumerator RestaurarBotonTerminar()
    {
        yield return new WaitForSeconds(SEGUNDOS_PARA_CONFIRMAR);
        _restaurarTerminar = null;
        ReiniciarBotonTerminar();
    }

    void OnEnable()
    {
        _avanzando = false;
        ReiniciarBotonTerminar();
        ActualizarTitulo();
        ActualizarTextoComenzar();
        ActualizarIndicador();

        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.OnEstadoCambiado += AlCambiarEstadoSesion;
    }
    void OnDisable()
    {
        if (_esperaManager != null)
            StopCoroutine(_esperaManager);
        _esperaManager = null;

        DesuscribirseDelManager();

        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.OnEstadoCambiado -= AlCambiarEstadoSesion;
    }

    //El botón se bloquea mientras carga la escena del siguiente momento: un doble clic en "Ir al tutorial"
    //lanzaba la actividad post antes de que el tutorial terminara de cargar.
    void Update()
    {
        if (comenzarButton != null)
            comenzarButton.interactable = !_avanzando && EtapaCargada();
    }

    private bool EtapaCargada()
    {
        switch (EtapaActual())
        {
            case Etapa.Tutorial: return TutorialManagerBase.Actual != null;
            case Etapa.ActividadPre:
            case Etapa.ActividadPost: return ActivityManagerBase.Current != null;
            default: return true;
        }
    }

    private Etapa EtapaActual()
    {
        GameFlowManager flujo = GameFlowManager.Instance;
        if (flujo == null) return Etapa.Menu;

        switch (flujo.Fase)
        {
            case FaseSesion.Tutorial: return Etapa.Tutorial;
            case FaseSesion.Actividad: return flujo.TutorialVistoEnActividadActual ? Etapa.ActividadPost : Etapa.ActividadPre;
            default: return Etapa.Menu;
        }
    }

    //Cada cambio de momento (también cuando el tutorial termina y lanza la actividad post por su cuenta)
    //pone al día el texto del botón y vuelve a seguir la condición de la actividad que se esté mostrando.
    private void AlCambiarEstadoSesion()
    {
        ActualizarTextoComenzar();

        if (panelConfirmacionAvanceForzado != null)
            panelConfirmacionAvanceForzado.SetActive(false);
        _accionAlConfirmar = null;
        _momentoAlConfirmar = null;

        if (_esperaManager != null) StopCoroutine(_esperaManager);
        _esperaManager = null;
        DesuscribirseDelManager();

        if (GameFlowManager.Instance.Fase == FaseSesion.Actividad)
            _esperaManager = StartCoroutine(EsperarManagerYSuscribir());
        ActualizarIndicador();
    }
    void ActualizarTitulo()
    {
        if (tituloActividadText != null && ActividadMenuUI.ActivoActual != null)
            tituloActividadText.text = $"Actividad: {ActividadMenuUI.ActivoActual.NombreActividad}";
    }

    void ActualizarTextoComenzar()
    {
        if (comenzarButtonTexto == null) return;

        comenzarButtonTexto.text = EtapaActual() switch
        {
            Etapa.ActividadPre => "Ir al tutorial",
            Etapa.Tutorial => "Saltar a la actividad",
            Etapa.ActividadPost => "Continuar",
            _ => "Comenzar"
        };
    }

    //Se le pide al GameFlowManager (que siempre está cargado) y no al Menú de la actividad, porque el Menú se descarga
    //al abrir el Tutorial; antes, presionar un botón durante el tutorial no hacía nada.
    //El botón Tutorial se oculta (el tutorial se abre con el botón principal), pero se deja por si se vuelve a conectar.
    public void OnTutorialPresionado()
    {
        GameFlowManager flujo = GameFlowManager.Instance;
        if (flujo != null && flujo.ActividadActual is ActivityType tipo)
            flujo.MostrarTutorialActividad(tipo);
    }

    //Un solo botón que lleva la actividad por sus momentos: lanza la actividad pre, pasa al tutorial,
    //lanza la actividad post (si el tutorial no se puede terminar) y, al final, avanza a la siguiente actividad.
    public void OnComenzarOContinuarPresionado()
    {
        GameFlowManager flujo = GameFlowManager.Instance;
        if (flujo == null || !(flujo.ActividadActual is ActivityType tipo)) return;

        switch (EtapaActual())
        {
            case Etapa.Menu:
            case Etapa.Tutorial:
                flujo.LanzarActividadDirecta(tipo);
                return;

            //Pre y post se comparan, así que piden la misma cantidad de trabajo (la meta de la actividad): el pre
            //también exige la meta antes de pasar al tutorial (antes se podía pasar en cualquier momento).
            case Etapa.ActividadPre:
                PedirMetaOAvanzar(() => flujo.MostrarTutorialActividad(tipo), "pre");
                return;
        }

        PedirMetaOAvanzar(Avanzar, "post");
    }

    //Si la meta no se cumplió, pide confirmación (mismo panel en pre y post). Si se avanza igual, queda en eventos.csv
    //de REBA para saber en el análisis qué momentos quedaron incompletos.
    private System.Action _accionAlConfirmar;
    private string _momentoAlConfirmar;

    private void PedirMetaOAvanzar(System.Action accion, string momento)
    {
        bool condicionCumplida = ActivityManagerBase.Current == null
            || ActivityManagerBase.Current.ActividadCompletada;

        if (!condicionCumplida && panelConfirmacionAvanceForzado != null)
        {
            _accionAlConfirmar = accion;
            _momentoAlConfirmar = momento;
            panelConfirmacionAvanceForzado.SetActive(true);
            return;
        }
        if (!condicionCumplida) RegistrarAvanceSinMeta(momento);
        accion();
    }

    private static void RegistrarAvanceSinMeta(string momento)
    {
        var grabador = FindAnyObjectByType<PujJoycon.RebaSessionRecorder>();
        string progreso = ActivityManagerBase.Current != null ? ActivityManagerBase.Current.ResumenProgreso : "";
        if (grabador != null) grabador.RegistrarEvento("avance_sin_meta", $"{momento}: el Game Master avanzó sin cumplir la meta ({progreso})");
    }

    public void OnConfirmarAvanceForzado()
    {
        if (panelConfirmacionAvanceForzado != null)
            panelConfirmacionAvanceForzado.SetActive(false);

        RegistrarAvanceSinMeta(_momentoAlConfirmar ?? "post");
        var accion = _accionAlConfirmar ?? Avanzar;
        _accionAlConfirmar = null;
        _momentoAlConfirmar = null;
        accion();
    }

    public void OnCancelarAvanceForzado()
    {
        if (panelConfirmacionAvanceForzado != null)
            panelConfirmacionAvanceForzado.SetActive(false);
        _accionAlConfirmar = null;
        _momentoAlConfirmar = null;
    }

    private void Avanzar()
    {
        //Mientras carga la siguiente actividad se bloquea el botón, para que un doble clic no salte una actividad.
        _avanzando = true;
        if (comenzarButton != null) comenzarButton.interactable = false;
        DesuscribirseDelManager();
        GameFlowManager.Instance?.ContinuarConSiguienteActividad();
    }

    private IEnumerator EsperarManagerYSuscribir()
    {
        //Espera a que la escena de la actividad termine de cargar y su manager haga Awake().
        while (ActivityManagerBase.Current == null)
            yield return null;
        _esperaManager = null;

        _managerSuscrito = ActivityManagerBase.Current;
        _managerSuscrito.OnActividadCompletada += ActualizarIndicador;
        ActualizarIndicador();
    }

    private void DesuscribirseDelManager()
    {
        if (_managerSuscrito != null)
        {
            _managerSuscrito.OnActividadCompletada -= ActualizarIndicador;
            _managerSuscrito = null;
        }
    }

    private void ActualizarIndicador()
    {
        if (indicadorCondicion == null) return;

        bool completada = ActivityManagerBase.Current != null && ActivityManagerBase.Current.ActividadCompletada;
        indicadorCondicion.color = completada ? colorCondicionCumplida : colorCondicionPendiente;
    }
}
