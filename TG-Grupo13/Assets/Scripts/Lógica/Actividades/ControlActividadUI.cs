using UnityEngine;
using TMPro;

public class ControlActividadUI : MonoBehaviour
{
    [SerializeField] private TMP_Text tituloActividadText;

    void OnEnable()
    {
        ActualizarTitulo();
    }

    void ActualizarTitulo()
    {
        if (tituloActividadText != null && ActividadMenuUI.ActivoActual != null)
            tituloActividadText.text = $"Actividad: {ActividadMenuUI.ActivoActual.NombreActividad}";
    }

    public void OnTutorialPresionado()
    {
        ActividadMenuUI.ActivoActual?.MostrarTutorial();
    }

    public void OnLanzarPresionado()
    {
        ActividadMenuUI.ActivoActual?.LanzarActividad();
    }
}