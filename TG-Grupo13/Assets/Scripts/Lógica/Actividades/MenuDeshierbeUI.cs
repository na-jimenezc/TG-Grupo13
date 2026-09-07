using UnityEngine;

public class MenuDeshierbeUI : ActividadMenuUI
{
    public override string NombreActividad => "Deshierbe";
    public override void MostrarTutorial()
    {
        Debug.Log("[MenuDeshierbe] Mostrando tutorial de deshierbe...");
    }

    public override void LanzarActividad()
    {
        Debug.Log("[MenuDeshierbe] Lanzando actividad de deshierbe directamente...");
    }
}