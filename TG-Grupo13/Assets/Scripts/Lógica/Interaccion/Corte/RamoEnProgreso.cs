using System.Collections.Generic;
using UnityEngine;

/*Administra la agrupación de tallos cortados en un ramo en progreso, 
 * y la verificación de si el ramo está nivelado para poder ensamblar el capuchón.*/
public class RamoEnProgreso : MonoBehaviour
{
    public static RamoEnProgreso Instance { get; private set; }

    [SerializeField] private Transform puntoAgrupacion; //Punto donde se agrupan los tallos cortados (hijo del objeto que contiene este script)
    [Tooltip("Tallos por ramo SOLO cuando no hay CorteActivityManager (tutorial). En la actividad se usa el valor del manager")]
    [SerializeField] private int tallosParaNivelar = 5;

    //Cantidad de tallos necesarios para poder nivelar el ramo y ensamblarlo con el capuchón
    public int TallosNecesarios => CorteActivityManager.Instance != null
        ? CorteActivityManager.Instance.TallosPorRamo
        : tallosParaNivelar;

    [Header("Corte parejo (visual)")]
    [Tooltip("Rango en metros del desfase con el que entra cada tallo al ramo, para que se vea disparejo antes del corte parejo")]
    [SerializeField] private float desfaseMinimo = 0.01f;
    [SerializeField] private float desfaseMaximo = 0.03f;
    [Tooltip("Eje local (del punto de agrupación) a lo largo del tallo. Si los tallos se mueven de lado en vez de subir/bajar, cambiarlo")]
    [SerializeField] private Vector3 ejeTallos = Vector3.up;

    private readonly List<TalloCortado> _tallos = new List<TalloCortado>();
    private readonly List<float> _desfases = new List<float>(); //Desfase aplicado a cada tallo, para deshacerlo al cortar parejo
    public bool Nivelado { get; private set; } = false;

    //EVENTO DEL TUTORIAL
    public static event System.Action OnNivelado;

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

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
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

        //Al pegarse a la mano (que vive en Core) pasaría a Core y no se iría con la escena de Corte;
        //se registra antes para destruirlo cuando se descargue su escena (tutorial o actividad)
        DestruirConEscenaOrigen.Registrar(gameObject);

        //Fija el agrupador a la jerarquía de la mano secundaria
        transform.SetParent(ManoSecundaria.Instance.transform, worldPositionStays: false);
        transform.localPosition = new Vector3(0f, 0.05f, 0f); //REVISAR SI QUEDÓ EN LA PALMA
        transform.localRotation = Quaternion.identity;
    }

    //Recibe un nuevo tallo deshojado y lo integra visual y lógicamente al ramo.
    public bool AgregarTallo(TalloCortado tallo)
    {
        if (_tallos.Count >= TallosNecesarios) return false;

        //Obliga a la mano a "soltar" su único objeto para que pase a formar parte del grupo
        ManoSecundaria.Instance.Soltar();
        tallo.transform.SetParent(puntoAgrupacion, worldPositionStays: false);

        //Se agrega un desplazamiento en el eje X por cada tallo añadido para simular volumen
        int index = _tallos.Count;
        tallo.transform.localPosition = new Vector3(index * 0.015f, 0, 0);
        AlineadorPivote.CorregirPivote(tallo.gameObject);

        //Cada tallo entra a una altura distinta (hacia arriba o abajo) para que el ramo se vea disparejo hasta el corte parejo.
        //Se aplica después de corregir el pivote, porque esa corrección vuelve a mover el tallo.
        float desfase = Random.Range(desfaseMinimo, desfaseMaximo) * (Random.value < 0.5f ? -1f : 1f);
        tallo.transform.localPosition += ejeTallos.normalized * desfase;

        ReproducirSonido(sonidoAgregarTallo);

        _tallos.Add(tallo);
        _desfases.Add(desfase);
        return true;
    }

    //Acción de las tijeras sobre el grupo. Iguala las puntas de todos los tallos.
    public void CortarParejo()
    {
        if (Nivelado) return;
        //No se nivelan los tallos si no hay suficientes para formar un ramo
        if (_tallos.Count < TallosNecesarios) return;

        Nivelado = true;
        ReproducirSonido(sonidoNivelado);
        OnNivelado?.Invoke();

        //Se quita el desfase con el que entró cada tallo, así todos quedan a la misma altura (corte parejo)
        for (int i = 0; i < _tallos.Count; i++)
        {
            if (_tallos[i] != null)
                _tallos[i].transform.localPosition -= ejeTallos.normalized * _desfases[i];
            _desfases[i] = 0f;
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
        _desfases.Clear();
        Nivelado = false;
    }

    //Método privado para reproducir un clip de sonido usando el AudioSource asignado.
    private void ReproducirSonido(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}