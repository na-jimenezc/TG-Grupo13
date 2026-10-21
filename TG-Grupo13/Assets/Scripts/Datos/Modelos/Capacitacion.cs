using System;
using System.Collections.Generic;

//Resumen de REBA de una condición de una actividad (antes del tutorial, durante el tutorial o después).
//Lo llena RebaSessionRecorder (Scripts/JoyconVR) al terminar la sesión. El detalle completo (una fila
//cada 0,1 s con todos los ángulos y puntajes) queda en los CSV de la carpeta RebaData.CarpetaDatos.
[Serializable]
public class RebaResumen
{
    public bool HayDatos;              //false si esa condición no se hizo (ej. no se vio el tutorial)
    public int Intentos;               //veces que se lanzó la actividad en esa condición
    public float DuracionSegundos;
    public int Muestras;
    public float Promedio;             //REBA final promedio
    public float Mediana;
    public int Moda;                   //REBA más repetido (postura más frecuente); el que se muestra en pantalla
    public float Percentil90;          //el 10% del tiempo estuvo por encima de este valor
    public int Maximo;
    //% del tiempo en cada nivel de riesgo de REBA (1: insignificante, 2-3: bajo, 4-7: medio, 8-10: alto, 11+: muy alto)
    public float PorcentajeInsignificante;
    public float PorcentajeBajo;
    public float PorcentajeMedio;
    public float PorcentajeAlto;
    public float PorcentajeMuyAlto;
}

[Serializable]
public class RebaData
{
    public RebaResumen PreTutorial = new RebaResumen();
    public RebaResumen Tutorial = new RebaResumen();
    public RebaResumen PostTutorial = new RebaResumen();
    //Carpeta (dentro de Application.persistentDataPath) con muestras.csv, resumen.csv, eventos.csv y sesion.json
    public string CarpetaDatos;
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