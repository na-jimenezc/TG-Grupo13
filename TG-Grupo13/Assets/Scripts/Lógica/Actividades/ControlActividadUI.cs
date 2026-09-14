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

    void OnEnable()
    {
        _actividadLanzada = false;
        ActualizarTitulo();
        ActualizarTextoComenzar();
        ActualizarIndicador();
    }
    void OnDisable()
    {
        if (_esperaManager != null)
            StopCoroutine(_esperaManager);

        DesuscribirseDelManager();
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

    public void OnTutorialPresionado()
    {
        ActividadMenuUI.ActivoActual?.MostrarTutorial();
    }

    //Un solo botón para las dos funciones: la primera vez lanza la actividad real; una vez lanzada,
    //el mismo botón (ya renombrado a "Continuar") avanza a la siguiente actividad de la cola.
    public void OnComenzarOContinuarPresionado()
    {
        if (!_actividadLanzada)
        {
            ActividadMenuUI.ActivoActual?.LanzarActividad();
            _actividadLanzada = true;
            ActualizarTextoComenzar();

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
        DesuscribirseDelManager();
        GameFlowManager.Instance?.ContinuarConSiguienteActividad();
    }

    private IEnumerator EsperarManagerYSuscribir()
    {
        //Espera a que la escena de la actividad termine de cargar y su manager haga Awake().
        while (ActivityManagerBase.Current == null)
            yield return null;

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
