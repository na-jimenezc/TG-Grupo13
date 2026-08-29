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
    private static UserListWrapper _data;

    // Método privado que carga los datos desde el archivo JSON si aún no se han cargado
    private static void CargarSiHaceFalta()
    {
        if (_data != null) return;

        if (File.Exists(RutaArchivo))
        {
            string json = File.ReadAllText(RutaArchivo);
            _data = JsonUtility.FromJson<UserListWrapper>(json);

            //Si por alguna razón la deserialización devuelve null, inicializamos la lista
            if (_data == null) _data = new UserListWrapper();
        }
        else
        {
            _data = new UserListWrapper();
        }
    }

    //Método privado que guarda los datos en el archivo JSON con logs de depuración y abre la carpeta de AppData automáticamente en Windows (Solo dentro del Editor)
    private static void Guardar()
    {
        if (_data == null) _data = new UserListWrapper();

        string json = JsonUtility.ToJson(_data, true);
        File.WriteAllText(RutaArchivo, json);

        Debug.Log($"[UserDatabase] JSON guardado con éxito en: {RutaArchivo}");
        Debug.Log($"[UserDatabase] Contenido guardado:\n{json}");

            //Abre la carpeta de AppData automáticamente en Windows (Solo dentro del Editor)
    #if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(RutaArchivo);
    #endif
    }

    //Método público que verifica si un usuario con el nombre dado ya existe en la base de datos
    public static bool ExisteUsuario(string nombre)
    {
        CargarSiHaceFalta();
        return _data.usuarios.Exists(u => u.Nombre.Equals(nombre, System.StringComparison.OrdinalIgnoreCase));
    }

    //Método público que crea un nuevo usuario y lo agrega a la base de datos, generando un ID único y guardando la información en el archivo JSON
    public static UserProfile CrearUsuario(string nombre, string correo, bool esAdmin, string password, string edad)
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
            PasswordHash = esAdmin ? Hash(password) : ""
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

    //Método público que elimina un usuario por su nombre, actualizando la base de datos y guardando los cambios en el archivo JSON
    public static void EliminarUsuario(string nombre)
    {
        CargarSiHaceFalta();
        _data.usuarios.RemoveAll(u => u.Nombre.Equals(nombre, System.StringComparison.OrdinalIgnoreCase));
        Guardar();
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