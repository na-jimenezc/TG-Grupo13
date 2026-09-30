using UnityEngine;

/*Tallo individual que ha sido cortado de la planta*/
public class TalloCortado : MonoBehaviour
{
    //Referencias a los modelos del tallo
    [SerializeField] private GameObject modeloConHojas;
    [SerializeField] private GameObject modeloDeshojado;
    [SerializeField] private Collider zonaDeshoje; //Collider que detecta la pasada de mano

    private bool _manoDentro = false;
    private bool _deshojado = false;

    [Header("Efectos de Sonido")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoDeshoje;

    private void Awake()
    {
        modeloConHojas.SetActive(true);
        modeloDeshojado.SetActive(false);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }


    //Detecta si la mano dominante con la que se limpia cruza el área requerida
    private void OnTriggerEnter(Collider other)
    {
        if (_deshojado) return;
        if (other.CompareTag("ManoDominante")) _manoDentro = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("ManoDominante")) _manoDentro = false;
    }

    //Detecta si la mano dominante con la que se limpia cruza el área requerida
    private void Update()
    {
        //Solo avanza si la mano cruzó el trigger
        if (_deshojado || !_manoDentro) return;

        //Se tiene que presionar el gatillo al pasar la mano para confirmar el gesto de deshojar
        bool trigger = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger)
                    || OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger);

        if (trigger)
            Deshojar();
    }

    //Deshoja el tallo, cambiando el modelo y registrando el evento
    private void Deshojar()
    {
        if (RamoEnProgreso.Instance == null) return;

        Debug.Log("[TalloCortado] Deshojar() ejecutado.");

        _deshojado = true;
        modeloConHojas.SetActive(false);
        modeloDeshojado.SetActive(true);

        if (audioSource != null && sonidoDeshoje != null)
            audioSource.PlayOneShot(sonidoDeshoje);

        //Transfiere este tallo deshojado al agrupador general y suma al progreso
        bool agregado = RamoEnProgreso.Instance.AgregarTallo(this);
        if (!agregado) return;

        if (agregado)
        {
            CorteActivityManager.Instance.RegistrarTalloDeshojado();
        }
    }
}