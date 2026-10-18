using System;
using System.Collections.Generic;

[Serializable]
public class RebaData
{
    //ACÁ IRIAN LOS DATOS DE REBA MÁS ADELANTE, POR AHORA SE DEJAN VACIOS, MÁS ADELANTE SE PUEDE EXPANDIR A UNA NUEVA CLASE 
}

[Serializable]
public class ActividadRealizada
{
    public ActivityType Tipo; //Mismo enum que usa el flujo (ActivityType.cs); se guarda en el JSON como número
    public string TiempoTomado;        //Tiempo tomado de la actividad en formato "HH:MM:SS"
    public string CalificacionGeneral; //Bajo, medio o alto, por el momento son datos de prueba
    public RebaData Reba = new RebaData();
}

[Serializable]
public class Capacitacion
{
    public string Fecha;
    public List<ActividadRealizada> Actividades = new List<ActividadRealizada>();
}