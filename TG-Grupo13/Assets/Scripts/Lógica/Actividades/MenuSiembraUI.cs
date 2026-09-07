using UnityEngine;

public class MenuSiembraUI : ActividadMenuUI
{
    public override string NombreActividad => "Siembra"; 

    public override void MostrarTutorial()
    {
        Debug.Log("[MenuSiembra] Mostrando tutorial de siembra...");
    }

    public override void LanzarActividad()
    {
        Debug.Log("[MenuSiembra] Lanzando actividad de siembra directamente...");
    }
}