using UnityEngine;

//Identificador lógico simple para los colliders del entorno (malla o balde).
public class ZonaDeposito : MonoBehaviour
{
    [Tooltip("true = zona de la malla de entutorado (recibe ramos individuales). false = el balde (recibe la recolección final).")]
    public bool EsZonaDeRamos = true;
}