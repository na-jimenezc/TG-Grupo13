using System.Collections.Generic;
using TMPro;

/*Configura el desplegable de "Rol" de los formularios de perfil (crear y actualizar).
 * Las opciones se ponen por código para que siempre sean las mismas: 0 = Normal, 1 = Administrador
 * (en la escena el de Crear Perfil traía además un "Option C" que sobraba).*/
public static class RolUI
{
    private static readonly List<string> Opciones = new List<string> { "Normal", "Administrador" };

    public static void Configurar(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();
        dropdown.AddOptions(Opciones);
        EtiquetaCampoUI.Etiquetar(dropdown, "Rol");
    }
}
