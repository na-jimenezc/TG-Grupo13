using UnityEngine;
using Oculus.Interaction;

[RequireComponent(typeof(Grabbable))]
public class RamoTerminado : MonoBehaviour
{
    private Grabbable _grabbable;
    private ZonaDeposito _zonaDebajo;
    private bool _registradoEnMalla = false;
    private bool _apiladoEnHombro = false;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDestroy() => _grabbable.WhenPointerEventRaised -= OnPointerEvent;

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type != PointerEventType.Unselect || _apiladoEnHombro) return;

        // Fase de recolección: ya se completaron los ramos y este se suelta cerca de la mano izquierda
        if (CorteActivityManager.Instance.RamosListos
            && HombroAcumulador.Instance != null
            && HombroAcumulador.Instance.PuedeRecibir(transform.position))
        {
            _apiladoEnHombro = true;
            HombroAcumulador.Instance.Apilar(gameObject);
            return;
        }

        // Fase normal: primer depósito en la malla de entutorado
        if (!_registradoEnMalla && _zonaDebajo != null && _zonaDebajo.EsZonaDeRamos)
        {
            _registradoEnMalla = true;
            CorteActivityManager.Instance.RegistrarRamoCompletado();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<ZonaDeposito>(out var zona)) _zonaDebajo = zona;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<ZonaDeposito>(out var zona) && zona == _zonaDebajo) _zonaDebajo = null;
    }
}