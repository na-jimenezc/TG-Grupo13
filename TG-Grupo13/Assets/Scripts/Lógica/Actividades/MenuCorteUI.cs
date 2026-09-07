using UnityEngine;

public class MenuCorteUI : ActividadMenuUI
{

    public override string NombreActividad => "Corte";
    public override void MostrarTutorial()
    {
        Debug.Log("[MenuCorte] Mostrando tutorial de corte...");
    }

    public override void LanzarActividad()
    {
        Debug.Log("[MenuCorte] Lanzando actividad de corte directamente...");
    }
}