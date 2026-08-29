using UnityEngine;
using TMPro;
using System.Text;

/*Controlador de la tarjeta visual de capacitación que se renderiza en VerInfoUI*/
public class CapacitacionCardUI : MonoBehaviour
{
    //Campos referenciando los elementos de texto en la UI para mostrar la información de la capacitación
    [SerializeField] private TMP_Text tituloText;
    [SerializeField] private TMP_Text fechaText;
    [SerializeField] private TMP_Text actividadesText;

    //Llena la tarjeta visual con la información de la capacitación y el número correspondiente
    //SE TIENE QUE ACTUALIZAR CUANDO METAMOS LA INFORMACIÓN DE LA CAPACITACIÓN EN EL SCRIPTABLE OBJECT DE REBA
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