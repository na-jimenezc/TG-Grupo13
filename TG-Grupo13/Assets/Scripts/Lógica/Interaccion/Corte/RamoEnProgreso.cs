using System.Collections.Generic;
using UnityEngine;

/*Administra la agrupación de tallos cortados en un ramo en progreso, 
 * y la verificación de si el ramo está nivelado para poder ensamblar el capuchón.*/
public class RamoEnProgreso : MonoBehaviour
{
    public static RamoEnProgreso Instance { get; private set; }

    [SerializeField] private Transform puntoAgrupacion; //Punto donde se agrupan los tallos cortados (hijo del objeto que contiene este script)
    [SerializeField] private int tallosParaNivelar = 5; //Cantidad de tallos necesarios para que el ramo se considere nivelado y pueda ensamblarse con el capuchón

    private readonly List<TalloCortado> _tallos = new List<TalloCortado>();
    public bool Nivelado { get; private set; } = false;

    //Efectos de Sonido
    [Header("Efectos de Sonido")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoAgregarTallo;
    [SerializeField] private AudioClip sonidoNivelado;

    private void Awake()
    {
        Instance = this;
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        //Se ejecuta en Start (no en Awake) para garantizar que ManoSecundaria,
        //que vive en Core y se carga antes, ya haya corrido su propio Awake.
        AnclarseALaMano();
    }

    //Función que ancla el objeto RamoEnProgreso a la mano secundaria del usuario, para que se mueva con ella.
    private void AnclarseALaMano()
    {
        if (ManoSecundaria.Instance == null)
        {
            Debug.LogWarning("[RamoEnProgreso] ManoSecundaria.Instance es null — no se pudo anclar a la mano.");
            return;
        }

        //Fija el agrupador a la jerarquía de la mano izquierda
        transform.SetParent(ManoSecundaria.Instance.transform, worldPositionStays: false);
        transform.localPosition = new Vector3(0f, 0.05f, 0f); //REVISAR SI QUEDÓ EN LA PALMA
        transform.localRotation = Quaternion.identity;
    }

    //Recibe un nuevo tallo deshojado y lo integra visual y lógicamente al ramo.
    public bool AgregarTallo(TalloCortado tallo)
    {
        if (_tallos.Count >= tallosParaNivelar) return false;

        //Obliga a la mano a "soltar" su único objeto para que pase a formar parte del grupo
        ManoSecundaria.Instance.Soltar();
        tallo.transform.SetParent(puntoAgrupacion, worldPositionStays: false);

        //Se agrega un desplazamiento en el eje X por cada tallo añadido para simular volumen
        int index = _tallos.Count;
        tallo.transform.localPosition = new Vector3(index * 0.015f, 0, 0);
        AlineadorPivote.CorregirPivote(tallo.gameObject);

        ReproducirSonido(sonidoAgregarTallo);

        _tallos.Add(tallo);
        return true;
    }

    //Acción de las tijeras sobre el grupo. Iguala las puntas de todos los tallos.
    public void CortarParejo()
    {
        if (Nivelado) return;
        //No se nivelan los tallos si no hay suficientes para formar un ramo
        if (_tallos.Count < tallosParaNivelar) return;

        Nivelado = true;
        ReproducirSonido(sonidoNivelado);

        //Se alineala altura de todos los tallos para simular el corte parejo
        foreach (var tallo in _tallos)
        {
            if (tallo != null)
            {
                Vector3 pos = tallo.transform.localPosition;
                // Se iguala el eje Y a 0 (o la altura base que prefieras) para emparejarlos
                tallo.transform.localPosition = new Vector3(pos.x, 0f, pos.z);
            }
        }
    }

    //Bandera para el ensamble
    public bool PuedeArmarseConCapuchon => Nivelado;

    public List<TalloCortado> Tallos => _tallos;

    //Limpia el ramo y elimina las instancias una vez se ensambla el RamoTerminado.
    public void Reiniciar()
    {
        foreach (var t in _tallos)
            if (t != null) Destroy(t.gameObject);

        _tallos.Clear();
        Nivelado = false;
    }

    //Método privado para reproducir un clip de sonido usando el AudioSource asignado.
    private void ReproducirSonido(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}