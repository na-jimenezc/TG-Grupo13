using UnityEngine;
using Oculus.Interaction;

/*Script que controla el comportamiento de las tijeras
 * para realizar la interacción de corte y emparejado de tallos*/
[RequireComponent(typeof(Grabbable))]
public class Tijeras : MonoBehaviour
{
    //Referencia a la punta de las tijeras, que es donde se detecta el corte
    [Header("Punta (detección de corte)")]
    [SerializeField] private Transform puntaTijeras;

    //Referencia al punto donde se engancha la tijera cuando no está en uso
    [Header("Equipar en el cuerpo (holster)")]
    private Transform puntoHolster => PuntoHolsterTijerasRef.Instance;
    [SerializeField] private float radioHolster = 0.12f; //Radio de detección para enganchar en el holster

    //Referencia al punto donde se reubican las tijeras si se caen de la mesa
    [Header("Recuperación si se caen")]
    [SerializeField] private Transform puntoSpawnMesa; //Punto donde se reubican las tijeras si se caen de la mesa
    [SerializeField] private float alturaMinimaAntesDeRecuperar = 0.3f; //Altura mínima antes de que se considere que las tijeras cayeron de la mesa
    [SerializeField] private float tiempoAntesDeRecuperar = 2f; //Tiempo que deben permanecer caídas antes de que se reubiquen en la mesa

    private Grabbable _grabbable;
    private Rigidbody _rb;
    private TalloEnPlantacion _talloCercano;
    private RamoEnProgreso _ramoCercano;
    private bool _equipadas = false;
    private bool _enHolster = false;
    private float _tiempoCaida = 0f;

    //Control de tiempo para evitar desenganchar inmediatamente al soltar las tijeras, evitando que se enganchen al holster accidentalmente
    private float _tiempoSeleccionado = 0f;
    private const float TIEMPO_MINIMO_PARA_DESENGANCHAR = 0.15f;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _rb = GetComponent<Rigidbody>();
        //Suscribirse al evento de selección y deselección del objeto grabbable
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDestroy() => _grabbable.WhenPointerEventRaised -= OnPointerEvent;

    private void OnPointerEvent(PointerEvent evt)
    {
        //Revisar si el usuario ya agarró las tijeras o las soltó
        if (evt.Type == PointerEventType.Select)
        {
            _equipadas = true;
            _tiempoSeleccionado = 0f; 
        }

        //Si el usuario soltó las tijeras, revisar si están cerca del holster para engancharlas
        else if (evt.Type == PointerEventType.Unselect)
        {
            _equipadas = false;

            if (puntoHolster != null && Vector3.Distance(transform.position, puntoHolster.position) <= radioHolster)
                EngancharAHolster();
        }
    }

    //Fija las tijeras al cuerpo del jugador y desactiva sus físicas
    private void EngancharAHolster()
    {
        _enHolster = true;
        transform.SetParent(puntoHolster, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        if (_rb != null) _rb.isKinematic = true; //Pausa la simulación física
    }

    //Desengancha las tijeras del cuerpo del jugador y reactiva sus físicas
    private void DesengancharDeHolster()
    {
        _enHolster = false;
        transform.SetParent(null, worldPositionStays: true);
        if (_rb != null) _rb.isKinematic = false;
    }

    //Actualiza cada frame el estado de las tijeras, revisando si se deben cortar tallos o ramas, y si se deben recuperar en la mesa
    private void Update()
    {
        //Lógica de uso mientras están agarradas
        if (_equipadas)
        {
            //Detectar pulsación de gatillos derecho o izquierdo para cortar tallos o ramas
            bool trigger = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger)
                        || OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger);
            if (trigger)
            {
                //Se prioriza el corte del ramo, y si no, intenta nivelar el ramo
                if (_talloCercano != null) _talloCercano.Cortar();
                else if (_ramoCercano != null) _ramoCercano.CortarParejo();
            }

            //Manejo de desenganche del holster con un pequeño retraso
            if (_equipadas && _enHolster)
            {
                _tiempoSeleccionado += Time.deltaTime;
                if (_tiempoSeleccionado >= TIEMPO_MINIMO_PARA_DESENGANCHAR)
                    DesengancharDeHolster();
            }
        }

        //Si quedaron "perdidas" (se cayeron de la mesa, rodaron lejos) por más de X segundos, vuelven solas
        if (!_equipadas && !_enHolster && puntoSpawnMesa != null)
        {
            if (transform.position.y < alturaMinimaAntesDeRecuperar)
            {
                _tiempoCaida += Time.deltaTime;
                if (_tiempoCaida >= tiempoAntesDeRecuperar)
                    RecuperarEnMesa();
            }
            else
            {
                _tiempoCaida = 0f; //Reinicia si no están por debajo del umbral
            }
        }
    }

    //Reubica las tijeras en el punto de spawn de la mesa, reiniciando su física y posición
    private void RecuperarEnMesa()
    {
        _tiempoCaida = 0f;
        transform.SetParent(null);

        //Detener movimientos e inercias y teletransporta el objeto
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = puntoSpawnMesa.position;   
            _rb.rotation = puntoSpawnMesa.rotation;  
        }
        else
        {
            transform.position = puntoSpawnMesa.position;
            transform.rotation = puntoSpawnMesa.rotation;
        }
    }

    //Registra objetos cortables al entrar en contacto con la tijera
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<TalloEnPlantacion>(out var tallo)) _talloCercano = tallo;
        if (other.TryGetComponent<RamoEnProgreso>(out var ramo)) _ramoCercano = ramo;
    }

    //Limpia las referencias al alejar la tijera de los objetos cortables
    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<TalloEnPlantacion>(out var tallo) && tallo == _talloCercano) _talloCercano = null;
        if (other.TryGetComponent<RamoEnProgreso>(out var ramo) && ramo == _ramoCercano) _ramoCercano = null;
    }

    
}