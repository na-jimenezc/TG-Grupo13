using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[System.Serializable]


//Contenedor que ayuda a JSON Utility a serializar y deserializar la lista de usuarios
public class UserListWrapper
{
    public List<UserProfile> usuarios = new();
}

//Clase de manejo de la base de datos de usuarios, que se encarga de la persistencia en un archivo JSON
public static class UserDatabase
{
    //Para la ruta donde se están guardando los datos, usamos Application.persistentDataPath, que es una ruta segura para guardar datos persistentes en Unity
    private static string RutaArchivo => Path.Combine(Application.persistentDataPath, "usuarios.json");
    //Archivo intermedio donde se escribe primero, para no dejar el original a medias si algo falla
    private static string RutaTemporal => RutaArchivo + ".tmp";
    //Versión anterior del archivo, que se usa si el original llega a estar dañado
    private static string RutaRespaldo => RutaArchivo + ".bak";
    //Carpeta de respaldos diarios (uno por día, se conservan los últimos DIAS_RESPALDO) y manuales
    private static string CarpetaRespaldos => Path.Combine(Application.persistentDataPath, "Respaldos");
    private const int DIAS_RESPALDO = 14;
    private static UserListWrapper _data;

    // Método privado que carga los datos desde el archivo JSON si aún no se han cargado.
    // Si el archivo está dañado, se aparta (sin borrarlo) y se carga el respaldo.
    private static void CargarSiHaceFalta()
    {
        if (_data != null) return;

        if (IntentarLeer(RutaArchivo, out _data)) return;

        //El archivo principal está dañado: se aparta ANTES de volver a guardar, porque al guardar
        //el original pasa a ser el respaldo y se perdería el único respaldo bueno.
        if (File.Exists(RutaArchivo))
            ApartarArchivoDanado();

        if (IntentarLeer(RutaRespaldo, out _data))
        {
            Debug.LogWarning("[UserDatabase] usuarios.json estaba dañado; se cargó el respaldo (.bak).");
            return;
        }

        if (File.Exists(RutaRespaldo))
            Debug.LogError("[UserDatabase] usuarios.json y su respaldo están dañados; se inicia con la lista vacía. Los archivos dañados quedan en la carpeta para recuperarlos a mano.");

        _data = new UserListWrapper();
    }

    //Intenta leer y deserializar un archivo; devuelve false si no existe, está vacío o no es un JSON válido
    private static bool IntentarLeer(string ruta, out UserListWrapper datos)
    {
        datos = null;
        if (!File.Exists(ruta)) return false;

        try
        {
            string json = File.ReadAllText(ruta);
            if (string.IsNullOrWhiteSpace(json)) return false;

            datos = JsonUtility.FromJson<UserListWrapper>(json);
            if (datos != null && datos.usuarios == null) datos.usuarios = new List<UserProfile>();
            return datos != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UserDatabase] No se pudo leer '{Path.GetFileName(ruta)}': {e.Message}");
            datos = null;
            return false;
        }
    }

    //Renombra el archivo dañado con la fecha para conservarlo y que no estorbe al siguiente guardado
    private static void ApartarArchivoDanado()
    {
        string destino = Path.Combine(Application.persistentDataPath, $"usuarios.corrupto-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        try
        {
            File.Move(RutaArchivo, destino);
            Debug.LogWarning($"[UserDatabase] Archivo dañado apartado en: {destino}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UserDatabase] No se pudo apartar el archivo dañado: {e.Message}");
        }
    }

    //Método privado que guarda los datos en el archivo JSON de forma atómica: se escribe completo en un .tmp
    //y luego reemplaza al original de una sola vez, dejando la versión anterior como .bak.
    //No se registra el contenido en el log porque incluye datos personales y los hashes de las contraseñas.
    private static void Guardar()
    {
        if (_data == null) _data = new UserListWrapper();

        string json = JsonUtility.ToJson(_data, true);

        RespaldoDiarioSiHaceFalta();

        try
        {
            //Se fuerza la escritura al disco antes del reemplazo, para que el .tmp esté completo
            using (var flujo = new FileStream(RutaTemporal, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var escritor = new StreamWriter(flujo, new UTF8Encoding(false)))
            {
                escritor.Write(json);
                escritor.Flush();
                flujo.Flush(true);
            }

            if (File.Exists(RutaArchivo))
                File.Replace(RutaTemporal, RutaArchivo, RutaRespaldo, true);
            else
                File.Move(RutaTemporal, RutaArchivo);

            Debug.Log($"[UserDatabase] JSON guardado con éxito en: {RutaArchivo}");
        }
        catch (Exception e)
        {
            //Los datos siguen en memoria; el archivo anterior queda intacto
            Debug.LogError($"[UserDatabase] No se pudo guardar el archivo de usuarios: {e.Message}");
        }
    }

    //En el primer guardado de cada día copia el archivo tal como estaba ANTES de los cambios de ese día
    //(Respaldos/usuarios-AAAAMMDD.json) y borra los respaldos diarios más viejos que los últimos DIAS_RESPALDO.
    private static void RespaldoDiarioSiHaceFalta()
    {
        if (!File.Exists(RutaArchivo)) return;

        try
        {
            Directory.CreateDirectory(CarpetaRespaldos);
            string respaldoHoy = Path.Combine(CarpetaRespaldos, $"usuarios-{DateTime.Now:yyyyMMdd}.json");
            if (File.Exists(respaldoHoy)) return;

            File.Copy(RutaArchivo, respaldoHoy);

            //El nombre lleva la fecha, así que ordenar por nombre es ordenar por fecha
            var viejos = Directory.GetFiles(CarpetaRespaldos, "usuarios-*.json")
                .OrderByDescending(r => Path.GetFileName(r))
                .Skip(DIAS_RESPALDO);
            foreach (var ruta in viejos)
                File.Delete(ruta);
        }
        catch (Exception e)
        {
            //Un fallo del respaldo no debe impedir el guardado normal
            Debug.LogWarning($"[UserDatabase] No se pudo crear el respaldo diario: {e.Message}");
        }
    }

    //Método público para un respaldo manual (RF 13), p. ej. desde un botón del panel de administración.
    //Los manuales no se borran solos. Devuelve la ruta del respaldo o null si no se pudo crear.
    public static string CrearRespaldoManual()
    {
        CargarSiHaceFalta();
        Guardar(); //Asegura que el archivo tenga lo último que hay en memoria

        try
        {
            Directory.CreateDirectory(CarpetaRespaldos);
            string destino = Path.Combine(CarpetaRespaldos, $"manual-usuarios-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(RutaArchivo, destino);
            return destino;
        }
        catch (Exception e)
        {
            Debug.LogError($"[UserDatabase] No se pudo crear el respaldo manual: {e.Message}");
            return null;
        }
    }

    //Método público que verifica si un usuario con el nombre dado ya existe en la base de datos
    public static bool ExisteUsuario(string nombre)
    {
        CargarSiHaceFalta();
        return _data.usuarios.Exists(u => u.Nombre.Equals(nombre, System.StringComparison.OrdinalIgnoreCase));
    }

    //Método público que crea un nuevo usuario y lo agrega a la base de datos, generando un ID único y guardando la información en el archivo JSON
    public static UserProfile CrearUsuario(string nombre, string correo, bool esAdmin, string password, string edad, bool esZurdo = false)
    {
        CargarSiHaceFalta();

        int nuevoId = _data.usuarios.Count > 0 ? _data.usuarios.Max(u => u.Id) + 1 : 1;

        var nuevo = new UserProfile
        {
            Id = nuevoId,
            Nombre = nombre,
            Correo = correo,
            EsAdministrador = esAdmin,
            Edad = edad,
            PasswordHash = esAdmin ? Hash(password) : "",
            EsZurdo = esZurdo
        };

        _data.usuarios.Add(nuevo);
        Guardar();
        return nuevo;
    }

    //Método público que obtiene un usuario por su nombre, devolviendo null si no se encuentra
    public static UserProfile ObtenerUsuario(string nombre)
    {
        CargarSiHaceFalta();
        return _data.usuarios.Find(u => u.Nombre.Equals(nombre, System.StringComparison.OrdinalIgnoreCase));
    }


    //Método público que valida el login de un usuario administrador, devolviendo true si el nombre y la contraseña coinciden con un usuario administrador existente
    public static bool ValidarLoginAdmin(string nombre, string password)
    {
        var user = ObtenerUsuario(nombre);
        if (user == null || !user.EsAdministrador) return false;
        return user.PasswordHash == Hash(password);
    }

    //Método privado que genera un hash SHA256 de un texto dado, utilizado para almacenar contraseñas de manera segura
    private static string Hash(string texto)
    {
        using var sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(texto));
        var sb = new StringBuilder();
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    //Método público que devuelve la lista completa de usuarios en la base de datos
    public static List<UserProfile> ObtenerTodosLosUsuarios()
    {
        CargarSiHaceFalta();
        return _data.usuarios;
    }

    //Método público que indica si hay al menos un administrador registrado
    public static bool ExisteAlgunAdministrador()
    {
        CargarSiHaceFalta();
        return _data.usuarios.Exists(u => u.EsAdministrador);
    }

    //Método público que indica si el usuario dado es el único administrador registrado.
    //Se usa para no dejar el sistema sin nadie que pueda entrar al panel de administración.
    public static bool EsUltimoAdministrador(string nombre)
    {
        var usuario = ObtenerUsuario(nombre);
        return usuario != null && usuario.EsAdministrador && _data.usuarios.Count(u => u.EsAdministrador) == 1;
    }

    //Método público que elimina un usuario por su nombre, actualizando la base de datos y guardando los cambios en el archivo JSON.
    //Devuelve false (y no elimina) si es el último administrador.
    public static bool EliminarUsuario(string nombre)
    {
        CargarSiHaceFalta();
        if (EsUltimoAdministrador(nombre)) return false;

        _data.usuarios.RemoveAll(u => u.Nombre.Equals(nombre, System.StringComparison.OrdinalIgnoreCase));
        Guardar();
        return true;
    }

    //Método público que actualiza la información de un usuario existente, buscando por el nombre original y reemplazando con los datos actualizados
    //luego guarda los cambios en el archivo JSON
    public static void ActualizarUsuario(string nombreOriginal, UserProfile actualizado)
    {
        CargarSiHaceFalta();
        int index = _data.usuarios.FindIndex(u => u.Nombre.Equals(nombreOriginal, System.StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _data.usuarios[index] = actualizado;
            Guardar();
        }
    }

    //Exponemos el hash como público para poder usarlo también desde ActualizarPerfilUI
    public static string HashPassword(string texto) => Hash(texto);
}