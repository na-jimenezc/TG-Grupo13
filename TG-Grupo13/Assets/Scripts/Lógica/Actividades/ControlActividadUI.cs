using UnityEngine;
using TMPro;
using System.Collections;

public class ControlActividadUI : MonoBehaviour
{
    [SerializeField] private TMP_Text tituloActividadText;

    //Texto del botón "Comenzar", que se reutiliza como "Continuar" una vez la actividad ya fue lanzada.
    //Así el panel mantiene siempre solo dos botones (Tutorial y Comenzar/Continuar), como en la Figura 19 del documento.
    [SerializeField] private TMP_Text comenzarButtonTexto;
    [SerializeField] private UnityEngine.UI.Button comenzarButton;
    private bool _actividadLanzada = false;
    private IActivityManager _managerSuscrito;
    private Coroutine _esperaManager;

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
        _actividadLanzada = false;
        if (comenzarButton != null) comenzarButton.interactable = true;
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

    //Al terminar el tutorial, este lanza la actividad por su cuenta (sin pasar por el botón "Comenzar"),
    //así que el panel se pone al día: el botón pasa a "Continuar" y se empieza a seguir la condición de la actividad.
    private void AlCambiarEstadoSesion()
    {
        if (_actividadLanzada || GameFlowManager.Instance.Fase != FaseSesion.Actividad) return;

        _actividadLanzada = true;
        ActualizarTextoComenzar();
        if (_esperaManager == null)
            _esperaManager = StartCoroutine(EsperarManagerYSuscribir());
    }
    void ActualizarTitulo()
    {
        if (tituloActividadText != null && ActividadMenuUI.ActivoActual != null)
            tituloActividadText.text = $"Actividad: {ActividadMenuUI.ActivoActual.NombreActividad}";
    }

    void ActualizarTextoComenzar()
    {
        if (comenzarButtonTexto != null)
            comenzarButtonTexto.text = _actividadLanzada ? "Continuar" : "Comenzar";
    }

    //Se le pide al GameFlowManager (que siempre está cargado) y no al Menú de la actividad, porque el Menú se descarga
    //al abrir el Tutorial; antes, presionar un botón durante el tutorial no hacía nada.
    public void OnTutorialPresionado()
    {
        GameFlowManager flujo = GameFlowManager.Instance;
        if (flujo != null && flujo.ActividadActual is ActivityType tipo)
            flujo.MostrarTutorialActividad(tipo);
    }

    //Un solo botón para las dos funciones: la primera vez lanza la actividad real; una vez lanzada,
    //el mismo botón (ya renombrado a "Continuar") avanza a la siguiente actividad de la cola.
    public void OnComenzarOContinuarPresionado()
    {
        if (!_actividadLanzada)
        {
            GameFlowManager flujo = GameFlowManager.Instance;
            if (flujo == null || !(flujo.ActividadActual is ActivityType tipo)) return;

            //Se marca antes de lanzar, porque el cambio de fase a "Actividad" llega en este mismo clic
            //(AlCambiarEstadoSesion) y así no se suscribe dos veces al manager.
            _actividadLanzada = true;
            ActualizarTextoComenzar();
            flujo.LanzarActividadDirecta(tipo);

            if (_esperaManager == null)
                _esperaManager = StartCoroutine(EsperarManagerYSuscribir());
            return;
        }

        bool condicionCumplida = ActivityManagerBase.Current == null
            || ActivityManagerBase.Current.ActividadCompletada;

        if (condicionCumplida)
        {
            Avanzar();
        }
        else if (panelConfirmacionAvanceForzado != null)
        {
            panelConfirmacionAvanceForzado.SetActive(true);
        }
        else
        {
            Avanzar();
        }
    }

    public void OnConfirmarAvanceForzado()
    {
        if (panelConfirmacionAvanceForzado != null)
            panelConfirmacionAvanceForzado.SetActive(false);

        Avanzar();
    }

    public void OnCancelarAvanceForzado()
    {
        if (panelConfirmacionAvanceForzado != null)
            panelConfirmacionAvanceForzado.SetActive(false);
    }

    private void Avanzar()
    {
        //Mientras carga la siguiente actividad se bloquea el botón, para que un doble clic no salte una actividad.
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
