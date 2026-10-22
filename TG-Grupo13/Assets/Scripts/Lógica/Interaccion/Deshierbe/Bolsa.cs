using UnityEngine;
using Oculus.Interaction;
using System;


/*Recipiente para depositar las malas hierbas que se recogen en el juego. 
 *Se puede agarrar y tiene un indicador visual opcional que se activa cuando se agarra la bolsa.*/

[RequireComponent(typeof(Grabbable))]
public class Bolsa : MonoBehaviour
{
    [Tooltip("Retroalimentación Visual")]
    [SerializeField] private GameObject indicadorDisponible;

    //Referencia al punto donde se reubica la bolsa si se cae (si no se asigna, vuelve a donde estaba al iniciar la escena)
    [Header("Recuperación si se cae")]
    [SerializeField] private Transform puntoSpawnMesa; //Punto donde se reubica la bolsa si se cae
    [SerializeField] private float alturaMinimaAntesDeRecuperar = 0.3f; //Altura mínima antes de que se considere que la bolsa se cayó
    [SerializeField] private float tiempoAntesDeRecuperar = 2f; //Tiempo que debe permanecer caída antes de que se reubique

    private Grabbable _grabbable;
    private Rigidbody _rb;
    private bool _agarrada = false;
    private float _tiempoCaida = 0f;
    private Vector3 _posicionInicial;
    private Quaternion _rotacionInicial;

    //EVENTO DEL TUTORIAL
    public event Action OnAgarrada;

    private void Awake()
    {
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

    //Método que se llama cuando se produce un evento de puntero en el objeto grabbable
    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select) _agarrada = true;
        else if (evt.Type == PointerEventType.Unselect) _agarrada = false;

        //Encender el indicador visual
        if (evt.Type == PointerEventType.Select && indicadorDisponible != null)
            indicadorDisponible.SetActive(true);

        OnAgarrada?.Invoke();
    }

    //Si quedó "perdida" (se cayó, rodó lejos) por más de X segundos, vuelve sola
    private void Update()
    {
        if (_agarrada) return;

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

    //Reubica la bolsa en el punto de spawn, reiniciando su física y posición
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
}