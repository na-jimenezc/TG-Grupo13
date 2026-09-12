using UnityEngine;
using TMPro;

public class ControlActividadUI : MonoBehaviour
{
    [SerializeField] private TMP_Text tituloActividadText;

    //Texto del botón "Comenzar", que se reutiliza como "Continuar" una vez la actividad ya fue lanzada.
    //Así el panel mantiene siempre solo dos botones (Tutorial y Comenzar/Continuar), como en la Figura 19 del documento.
    [SerializeField] private TMP_Text comenzarButtonTexto;

    private bool _actividadLanzada = false;

    void OnEnable()
    {
        _actividadLanzada = false;
        ActualizarTitulo();
        ActualizarTextoComenzar();
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
        }
        else
        {
            GameFlowManager.Instance?.ContinuarConSiguienteActividad();
        }
    }
}
