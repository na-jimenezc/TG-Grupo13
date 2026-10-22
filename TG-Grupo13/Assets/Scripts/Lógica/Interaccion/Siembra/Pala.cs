using Oculus.Interaction;
using System;
using UnityEngine;
using UnityEngine.UIElements;

/*Representa la pala que se usa para cavar en los slots de siembra. 
 *Cuando está equipada y se pulsa el gatillo, cava en el slot más cercano.*/
[RequireComponent(typeof(Grabbable))]
public class Pala : MonoBehaviour
{
    //Referencia a la punta de la pala, que es la parte que debe entrar en contacto con el slot para cavar
    [SerializeField] private Transform puntaPala;

    //Referencia al punto donde se reubica la pala si se cae (si no se asigna, vuelve a donde estaba al iniciar la escena)
    [Header("Recuperación si se cae")]
    [SerializeField] private Transform puntoSpawnMesa; //Punto donde se reubica la pala si se cae de la mesa
    [SerializeField] private float alturaMinimaAntesDeRecuperar = 0.3f; //Altura mínima antes de que se considere que la pala se cayó
    [SerializeField] private float tiempoAntesDeRecuperar = 2f; //Tiempo que debe permanecer caída antes de que se reubique

    private Grabbable _grabbable; //Referencia al componente Grabbable para detectar cuando el usuario la agarra o suelta
    private Rigidbody _rb;

    private PlantingSlot _slotCercano; //Guarda temporalmente el slot sobre el que está la pala
    private bool _equipada = false; //Bandera para saber si el usuario la sostiene
    private float _tiempoCaida = 0f;
    private Vector3 _posicionInicial;
    private Quaternion _rotacionInicial;

    //EVENTO PARA EL TUTORIAL
    public event Action OnAgarrada;

    private void Awake()
    {
        //Se suscribe a los eventos de agarre del Oculus Interaction SDK
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
        _rb = GetComponent<Rigidbody>();
        _posicionInicial = transform.position;
        _rotacionInicial = transform.rotation;
    }

    private void OnDestroy()
    {
        _grabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

    //Cuando el usuario agarra o suelta la pala, se actualiza la bandera _equipada y se dispara el evento OnAgarrada si es que la pala fue agarrada.
    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
        {
            _equipada = true;
            OnAgarrada?.Invoke();
        }
        else if (evt.Type == PointerEventType.Unselect)
        {
            _equipada = false;
        }
    }

    //Update se llama una vez por frame y verifica si la pala está equipada y si hay un slot cercano. Si se presiona el gatillo, se llama al método Cavar() del slot.
    private void Update()
    {
        //Si quedó "perdida" (se cayó de la mesa, rodó lejos) por más de X segundos, vuelve sola
        if (!_equipada)
        {
            if (transform.position.y < alturaMinimaAntesDeRecuperar)
            {
                _tiempoCaida += Time.deltaTime;
                if (_tiempoCaida >= tiempoAntesDeRecuperar)
                    RecuperarEnMesa();
            }
            else
            {
                _tiempoCaida = 0f; //Reinicia si no está por debajo del umbral
            }
        }

        //Solo puede cavar si la tiene en la mano y está sobre un slot de tierra
        if (!_equipada || _slotCercano == null) return;

        //Intenta cavar en el slot guardado al presionar el gatillo de la mano dominante
        if (Lateralidad.GatilloDominantePresionado)
            _slotCercano.Cavar();
    }

    //Reubica la pala en el punto de spawn de la mesa, reiniciando su física y posición
    private void RecuperarEnMesa()
    {
        _tiempoCaida = 0f;
        transform.SetParent(null);

        Vector3 posicion = puntoSpawnMesa != null ? puntoSpawnMesa.position : _posicionInicial;
        Quaternion rotacion = puntoSpawnMesa != null ? puntoSpawnMesa.rotation : _rotacionInicial;

        //Detener movimientos e inercias y teletransporta el objeto
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = posicion;
            _rb.rotation = rotacion;
        }
        else
        {
            transform.position = posicion;
            transform.rotation = rotacion;
        }
    }

    //OnTriggerEnter y OnTriggerExit se llaman cuando la punta de la pala entra o sale del collider de un slot de siembra.
    //Se usa para actualizar la referencia al slot más cercano.
    private void OnTriggerEnter(Collider other)
    {
        var slot = other.GetComponent<PlantingSlot>();
        if (slot != null) _slotCercano = slot;
    }

    private void OnTriggerExit(Collider other)
    {
        var slot = other.GetComponent<PlantingSlot>();
        if (slot == _slotCercano) _slotCercano = null;
    }
}