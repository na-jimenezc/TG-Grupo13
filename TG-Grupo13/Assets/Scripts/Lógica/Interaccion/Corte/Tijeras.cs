using UnityEngine;
using Oculus.Interaction;

[RequireComponent(typeof(Grabbable))]
public class Tijeras : MonoBehaviour
{
    [Header("Punta (detección de corte)")]
    [SerializeField] private Transform puntaTijeras;

    [Header("Equipar en el cuerpo (holster)")]
    private Transform puntoHolster => PuntoHolsterTijerasRef.Instance;
    [SerializeField] private float radioHolster = 0.12f;

    [Header("Recuperación si se caen")]
    [SerializeField] private Transform puntoSpawnMesa;
    [SerializeField] private float alturaMinimaAntesDeRecuperar = 0.3f;
    [SerializeField] private float tiempoAntesDeRecuperar = 2f;

    private Grabbable _grabbable;
    private Rigidbody _rb;
    private TalloEnPlantacion _talloCercano;
    private RamoEnProgreso _ramoCercano;
    private bool _equipadas = false;
    private bool _enHolster = false;
    private float _tiempoCaida = 0f;

    private float _tiempoSeleccionado = 0f;
    private const float TIEMPO_MINIMO_PARA_DESENGANCHAR = 0.15f;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _rb = GetComponent<Rigidbody>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDestroy() => _grabbable.WhenPointerEventRaised -= OnPointerEvent;

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
        {
            _equipadas = true;
            _tiempoSeleccionado = 0f; 
        }
        else if (evt.Type == PointerEventType.Unselect)
        {
            _equipadas = false;

            if (puntoHolster != null && Vector3.Distance(transform.position, puntoHolster.position) <= radioHolster)
                EngancharAHolster();
        }
    }

    private void EngancharAHolster()
    {
        _enHolster = true;
        transform.SetParent(puntoHolster, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        if (_rb != null) _rb.isKinematic = true;
    }

    private void DesengancharDeHolster()
    {
        _enHolster = false;
        transform.SetParent(null, worldPositionStays: true);
        if (_rb != null) _rb.isKinematic = false;
    }

    private void Update()
    {
        if (_equipadas)
        {
            bool trigger = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger)
                        || OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger);
            if (trigger)
            {
                if (_talloCercano != null) _talloCercano.Cortar();
                else if (_ramoCercano != null) _ramoCercano.CortarParejo();
            }

            if (_equipadas && _enHolster)
            {
                _tiempoSeleccionado += Time.deltaTime;
                if (_tiempoSeleccionado >= TIEMPO_MINIMO_PARA_DESENGANCHAR)
                    DesengancharDeHolster();
            }
        }

        // Si quedaron "perdidas" (se cayeron de la mesa, rodaron lejos) por más de X segundos, vuelven solas
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
                _tiempoCaida = 0f;
            }
        }
    }

    private void RecuperarEnMesa()
    {
        _tiempoCaida = 0f;
        transform.SetParent(null);

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

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<TalloEnPlantacion>(out var tallo)) _talloCercano = tallo;
        if (other.TryGetComponent<RamoEnProgreso>(out var ramo)) _ramoCercano = ramo;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<TalloEnPlantacion>(out var tallo) && tallo == _talloCercano) _talloCercano = null;
        if (other.TryGetComponent<RamoEnProgreso>(out var ramo) && ramo == _ramoCercano) _ramoCercano = null;
    }

    
}