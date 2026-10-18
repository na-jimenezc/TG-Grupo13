using TMPro;

/*Reglas de la edad de los perfiles, compartidas por Crear Perfil y Actualizar Datos.
 * La edad es obligatoria, solo números y dentro del rango permitido (participantes adultos que firman el consentimiento).
 * Se sigue guardando como texto en el JSON para no romper los perfiles ya guardados.*/
public static class ValidacionEdad
{
    public const int EDAD_MINIMA = 18;
    public const int EDAD_MAXIMA = 80;

    //Deja el campo aceptando solo dígitos y máximo 2 caracteres (se hace por código para no modificar la escena)
    public static void Configurar(TMP_InputField campo)
    {
        if (campo == null) return;
        campo.contentType = TMP_InputField.ContentType.IntegerNumber;
        campo.characterLimit = 2;
    }

    //Valida el texto ingresado. Si es válido devuelve true y la edad normalizada; si no, el mensaje de error para mostrar.
    public static bool Validar(string texto, out string edadNormalizada, out string error)
    {
        edadNormalizada = "";
        texto = texto != null ? texto.Trim() : "";

        if (string.IsNullOrEmpty(texto))
        {
            error = "La edad es obligatoria";
            return false;
        }

        if (!int.TryParse(texto, out int edad))
        {
            error = "La edad debe ser un número";
            return false;
        }

        if (edad < EDAD_MINIMA || edad > EDAD_MAXIMA)
        {
            error = $"La edad debe estar entre {EDAD_MINIMA} y {EDAD_MAXIMA} años";
            return false;
        }

        edadNormalizada = edad.ToString();
        error = "";
        return true;
    }
}
