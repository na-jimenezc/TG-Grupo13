using UnityEngine;

public class MenuSiembraUI : ActividadMenuUI
{
    public override string NombreActividad => "Siembra"; 

    public override void MostrarTutorial()
    {
        Debug.Log("[MenuSiembra] Mostrando tutorial de siembra...");
        GameFlowManager.Instance?.MostrarTutorialActividad(ActivityType.Siembra);
    }

    public override void LanzarActividad()
    {
        Debug.Log("[MenuSiembra] Lanzando actividad de siembra directamente...");
        GameFlowManager.Instance?.LanzarActividadDirecta(ActivityType.Siembra);
    }
}