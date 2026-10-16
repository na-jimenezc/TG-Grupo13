using UnityEngine;
using UnityEngine.Audio;
using System;

//Máquina de estados para los espacios del terreno donde se puede sembrar un bulbo. Cada espacio puede estar en uno de los siguientes estados:
public enum EstadoSlot { Vacio, Cavado, Sembrado }

/*Representa un espacio enla tierra de la simulación.
 * gestiona la transición visual entre la máquina de estados*/
public class PlantingSlot : MonoBehaviour
{
    [Header("Visuales por estado (arrastra objetos hijos o materiales)")]
    [SerializeField] private GameObject indicadorVacio;      //Marca de "Acá va un bulbo"
    [SerializeField] private GameObject indicadorCavado;     //Marca de que el terreno está cavado 
    [SerializeField] private GameObject bulboSembradoVisual; //Marca de que el bulbo ya fue sembrado

    //Propiedad de solo lectura pública para consultar el estado del hueco
    public EstadoSlot Estado { get; private set; } = EstadoSlot.Vacio;

    [Header("Efectos de Sonido")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoCavar;
    [SerializeField] private AudioClip sonidoSembrar;

    //EVENTOS PARA EL TUTORIAL
    public event Action OnCavado;
    public event Action OnSembrado;

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        ActualizarVisual();//Asegura que inicie mostrando el indicador correcto
    }

    //Método para cavar el terreno. Cambia el estado a Cavado si estaba Vacio y actualiza la visualización.
    public bool Cavar()
    {
        if (Estado != EstadoSlot.Vacio) return false;

        Estado = EstadoSlot.Cavado;
        ActualizarVisual();
        ReproducirSonido(sonidoCavar);
        OnCavado?.Invoke(); //Dipara el evento del tutorial
        return true;
    }

    //Método para sembrar un bulbo. Cambia el estado a Sembrado si estaba Cavado y actualiza la visualización.
    public bool Sembrar()
    {
        if (Estado != EstadoSlot.Cavado) return false;

        Estado = EstadoSlot.Sembrado;
        ActualizarVisual();
        ReproducirSonido(sonidoSembrar);
        OnSembrado?.Invoke(); //Dispara el evento del tutorial
        return true;
    }

    //Método privado para actualizar la visualización de los indicadores según el estado actual del slot.
    private void ActualizarVisual()
    {
        if (indicadorVacio != null) indicadorVacio.SetActive(Estado == EstadoSlot.Vacio);
        if (indicadorCavado != null) indicadorCavado.SetActive(Estado == EstadoSlot.Cavado);
        if (bulboSembradoVisual != null) bulboSembradoVisual.SetActive(Estado == EstadoSlot.Sembrado);
    }

    private void ReproducirSonido(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}