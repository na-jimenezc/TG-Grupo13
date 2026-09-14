using UnityEngine;
using Oculus.Interaction;

[RequireComponent(typeof(Grabbable))]
public class Ortiga : MonoBehaviour
{
    [Tooltip("Distancia mínima en metros que hay que 'tirar' para considerarse extraída")]
    [SerializeField] private float distanciaExtraccion = 0.12f;

    private Grabbable _grabbable;
    private Vector3 _posicionOriginal;
    private bool _agarrada = false;
    private bool _extraida = false;
    private bool _dentroDeBolsa = false;
    private bool _depositada = false;

    public bool EstaExtraida => _extraida;

    [Header("Feedback Audio y Efectos")]
    [SerializeField] private AudioClip sonidoExtraccion;
    [SerializeField] private AudioClip sonidoDeposito;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _posicionOriginal = transform.position;
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDestroy()
    {
        _grabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Select:
                _agarrada = true;
                break;

            case PointerEventType.Unselect:
                _agarrada = false;

                //Caso A: Se soltó dentro del área de la bolsa estando extraída
                if (_extraida && _dentroDeBolsa && !_depositada)
                {
                    _depositada = true;
                    if (DeshierbeActivityManager.Instance != null)
                    {
                        DeshierbeActivityManager.Instance.RegistrarOrtigaDepositada();
                    }
                    Destroy(gameObject, 0.1f);
                }
                //Caso B: Se soltó sin haber superado la distancia de extracción
                else if (!_extraida)
                {
                    transform.position = _posicionOriginal;
                }
                break;
        }
    }

    private void Update()
    {
        if (_agarrada && !_extraida)
        {
            float distancia = Vector3.Distance(transform.position, _posicionOriginal);
            if (distancia >= distanciaExtraccion)
            {
                _extraida = true;

                //Audio 3D en la posición de la mano
                if (sonidoExtraccion != null)
                    AudioSource.PlayClipAtPoint(sonidoExtraccion, transform.position);
            }
        }
    }

    //Detecta la entrada al Trigger de la boca de la bolsa
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bolsa"))
        {
            _dentroDeBolsa = true;
        }
    }

    //Detecta si el usuario saca la maleza de la bolsa antes de soltarl
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Bolsa"))
        {
            _dentroDeBolsa = false;
        }
    }
}