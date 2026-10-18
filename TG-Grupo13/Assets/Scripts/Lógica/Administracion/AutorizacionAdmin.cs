using UnityEngine;

/*Ventana de autorización para crear administradores. Cuando un administrador ingresa con su contraseña
 * (AdminGateUI), queda autorizado unos minutos para crear otros administradores desde Crear Perfil.
 * La autorización se pierde al vencer el tiempo o cuando un participante inicia sesión.
 * Si aún no existe ningún administrador, se permite crear el primero sin autorización.*/
public static class AutorizacionAdmin
{
    public const float MINUTOS_VIGENCIA = 5f;

    private static float _vigenteHasta = -1f;

    public static bool Vigente => Time.realtimeSinceStartup < _vigenteHasta;

    //Llamado al validar la contraseña de un administrador
    public static void Otorgar() => _vigenteHasta = Time.realtimeSinceStartup + MINUTOS_VIGENCIA * 60f;

    //Llamado cuando un participante inicia sesión, para que la autorización no quede abierta
    public static void Revocar() => _vigenteHasta = -1f;

    //Se puede crear un administrador si hay una autorización vigente o si todavía no existe ninguno
    public static bool PuedeCrearAdministrador() => Vigente || !UserDatabase.ExisteAlgunAdministrador();
}
