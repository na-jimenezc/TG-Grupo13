using System;

[Serializable]
public class UserProfile
{
    public int Id;
    public string Nombre;
    public string Correo;
    public bool EsAdministrador;
    public string Edad;
    public string PasswordHash;
}