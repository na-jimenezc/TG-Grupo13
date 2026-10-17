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

    void Awake()
    {
        //El bloque que muestra en qué paso del tutorial o de la actividad va el usuario.
        if (progresoAdmin == null)
            progresoAdmin = GetComponentInChildren<ProgresoAdminUI>(true);
        if (progresoAdmin == null)
            progresoAdmin = ProgresoAdminUI.Crear((RectTransform)transform, tituloActividadText != null ? tituloActividadText.font : null);
    }

    void OnEnable()
    {
        _actividadLanzada = false;
        if (comenzarButton != null) comenzarButton.interactable = true;
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
