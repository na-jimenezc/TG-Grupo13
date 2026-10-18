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
    //Mano dominante del participante. Los perfiles guardados antes de existir este campo quedan como diestros (false).
    public bool EsZurdo;
    public List<Capacitacion> Capacitaciones = new List<Capacitacion>();
}