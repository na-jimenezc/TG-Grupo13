using UnityEngine;
using UnityEngine.Audio;

public enum EstadoSlot { Vacio, Cavado, Sembrado }

public class PlantingSlot : MonoBehaviour
{
    [Header("Visuales por estado (arrastra objetos hijos o materiales)")]
    [SerializeField] private GameObject indicadorVacio;      //Marca de "Acá va un bulbo"
    [SerializeField] private GameObject indicadorCavado;     //Marca de que el terreno está cavado 
    [SerializeField] private GameObject bulboSembradoVisual; //Marca de que el bulbo ya fue sembrado

    public EstadoSlot Estado { get; private set; } = EstadoSlot.Vacio;

    [Header("Efectos de Sonido")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoCavar;
    [SerializeField] private AudioClip sonidoSembrar;

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        ActualizarVisual();
    }

    public bool Cavar()
    {
        if (Estado != EstadoSlot.Vacio) return false;

        Estado = EstadoSlot.Cavado;
        ActualizarVisual();
        ReproducirSonido(sonidoCavar);
        return true;
    }

    public bool Sembrar()
    {
        if (Estado != EstadoSlot.Cavado) return false;

        Estado = EstadoSlot.Sembrado;
        ActualizarVisual();
        ReproducirSonido(sonidoSembrar);
        return true;
    }

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