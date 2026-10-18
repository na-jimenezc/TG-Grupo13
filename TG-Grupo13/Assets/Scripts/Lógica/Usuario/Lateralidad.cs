/*Punto único para saber si el participante es zurdo o diestro (según su perfil) y qué mano/control
 * cumple cada papel. La mano dominante es la que usa las herramientas (tijeras, pala...) y la secundaria
 * la que sostiene lo que se va juntando (tallos, ramo, ramos en el hombro).
 *
 * Los textos de los tutoriales usan los marcadores {dominante} y {secundaria}, que se cambian por
 * "derecha" o "izquierda" con Formatear().*/
public static class Lateralidad
{
    public const string MarcadorDominante = "{dominante}";
    public const string MarcadorSecundaria = "{secundaria}";

    //Solo para probar en el editor sin iniciar sesión con un perfil zurdo (lo pone LateralidadJugador)
    public static bool? ForzadoEnEditor;

    public static bool EsZurdo
    {
        get
        {
            if (ForzadoEnEditor.HasValue) return ForzadoEnEditor.Value;
            var flujo = GameFlowManager.Instance;
            return flujo != null && flujo.usuarioActivo != null && flujo.usuarioActivo.EsZurdo;
        }
    }

    public static string NombreManoDominante => EsZurdo ? "izquierda" : "derecha";
    public static string NombreManoSecundaria => EsZurdo ? "derecha" : "izquierda";

    public static OVRInput.Controller ControlDominante => EsZurdo ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
    public static OVRInput.Controller ControlSecundario => EsZurdo ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;

    //Gatillo del control de la mano dominante (al pasar el control, "Primary" es el gatillo de ese control).
    //Las herramientas (pala, tijeras, deshoje) solo responden a este gatillo y no al de la mano secundaria.
    public static bool GatilloDominantePresionado => OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, ControlDominante);
    public static bool GatilloDominanteSostenido => OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, ControlDominante);

    public static string Formatear(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        return texto.Replace(MarcadorDominante, NombreManoDominante).Replace(MarcadorSecundaria, NombreManoSecundaria);
    }
}
