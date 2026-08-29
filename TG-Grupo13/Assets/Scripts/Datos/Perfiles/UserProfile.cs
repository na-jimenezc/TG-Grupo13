using System;
using System.Collections.Generic;

[Serializable]
public class UserProfile
{
    public int Id;
    public string Nombre;
    public string Correo;
    public bool EsAdministrador;
    public string Edad;
    public string PasswordHash;
    public List<Capacitacion> Capacitaciones = new List<Capacitacion>();
}