using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[System.Serializable]
public class UserListWrapper
{
    public List<UserProfile> usuarios = new();
}

public static class UserDatabase
{
    private static string RutaArchivo => Path.Combine(Application.persistentDataPath, "usuarios.json");
    private static UserListWrapper _data;

    private static void CargarSiHaceFalta()
    {
        if (_data != null) return;

        if (File.Exists(RutaArchivo))
        {
            string json = File.ReadAllText(RutaArchivo);
            _data = JsonUtility.FromJson<UserListWrapper>(json);

            // Si por alguna razón la deserialización devuelve null, inicializamos la lista
            if (_data == null) _data = new UserListWrapper();
        }
        else
        {
            _data = new UserListWrapper();
        }
    }

    private static void Guardar()
    {
        if (_data == null) _data = new UserListWrapper();

        string json = JsonUtility.ToJson(_data, true);
        File.WriteAllText(RutaArchivo, json);

        Debug.Log($"[UserDatabase] JSON guardado con éxito en: {RutaArchivo}");
        Debug.Log($"[UserDatabase] Contenido guardado:\n{json}");

            // Abre la carpeta de AppData automáticamente en Windows (Solo dentro del Editor)
    #if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(RutaArchivo);
    #endif
    }

    public static bool ExisteUsuario(string nombre)
    {
        CargarSiHaceFalta();
        return _data.usuarios.Exists(u => u.Nombre.Equals(nombre, System.StringComparison.OrdinalIgnoreCase));
    }

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

    public static UserProfile ObtenerUsuario(string nombre)
    {
        CargarSiHaceFalta();
        return _data.usuarios.Find(u => u.Nombre.Equals(nombre, System.StringComparison.OrdinalIgnoreCase));
    }

    public static bool ValidarLoginAdmin(string nombre, string password)
    {
        var user = ObtenerUsuario(nombre);
        if (user == null || !user.EsAdministrador) return false;
        return user.PasswordHash == Hash(password);
    }

    private static string Hash(string texto)
    {
        using var sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(texto));
        var sb = new StringBuilder();
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public static List<UserProfile> ObtenerTodosLosUsuarios()
    {
        CargarSiHaceFalta();
        return _data.usuarios;
    }

    public static void EliminarUsuario(string nombre)
    {
        CargarSiHaceFalta();
        _data.usuarios.RemoveAll(u => u.Nombre.Equals(nombre, System.StringComparison.OrdinalIgnoreCase));
        Guardar();
    }

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