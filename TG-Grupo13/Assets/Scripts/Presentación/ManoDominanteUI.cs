using System.Collections.Generic;
using TMPro;

/*Configura el desplegable de "Mano dominante" de los formularios de perfil (crear y actualizar).
 * Las opciones se ponen por código para que siempre sean las mismas: 0 = Diestro, 1 = Zurdo.*/
public static class ManoDominanteUI
{
    private static readonly List<string> Opciones = new List<string> { "Diestro", "Zurdo" };

    public static void Configurar(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();
        dropdown.AddOptions(Opciones);
        EtiquetaCampoUI.Etiquetar(dropdown, "Mano dominante");
    }

    public static bool EsZurdo(TMP_Dropdown dropdown) => dropdown != null && dropdown.value == 1;
}
