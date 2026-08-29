using UnityEngine;
using TMPro;
using System.Text;

public class CapacitacionCardUI : MonoBehaviour
{
    [SerializeField] private TMP_Text tituloText;
    [SerializeField] private TMP_Text fechaText;
    [SerializeField] private TMP_Text actividadesText;

    public void Configurar(Capacitacion capacitacion, int numero)
    {
        tituloText.text = $"Capacitación {numero}";
        fechaText.text = $"Fecha: {capacitacion.Fecha}";

        var sb = new StringBuilder();
        foreach (var actividad in capacitacion.Actividades)
        {
            sb.AppendLine($"{actividad.Tipo} — Tiempo: {actividad.TiempoTomado} — Calificación: {actividad.CalificacionGeneral}");
        }
        actividadesText.text = sb.ToString();
    }
}