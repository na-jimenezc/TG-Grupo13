using UnityEngine;
using Oculus.Interaction;

[RequireComponent(typeof(Grabbable))]
public class Capuchon : MonoBehaviour
{
    [SerializeField] private GameObject prefabRamoTerminado;
    [SerializeField] private float distanciaEnsamble = 0.15f;

    private Grabbable _grabbable;
    private Rigidbody _rb;

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
            if (_rb != null) _rb.isKinematic = false;   // se activa la física real al agarrarlo
            return;
        }

        if (evt.Type != PointerEventType.Unselect) return;

        var ramo = RamoEnProgreso.Instance;
        if (ramo == null || !ramo.PuedeArmarseConCapuchon)
        {
            if (_rb != null) _rb.isKinematic = false; // si lo sueltas sin ensamblar, cae con física normal
            return;
        }

        float distancia = Vector3.Distance(transform.position, ramo.transform.position);
        if (distancia > distanciaEnsamble)
        {
            if (_rb != null) _rb.isKinematic = false;
            return;
        }

        // Ensambla correctamente
        Vector3 posicion = ramo.transform.position;
        Instantiate(prefabRamoTerminado, posicion, Quaternion.identity);

        ramo.Reiniciar();
        gameObject.SetActive(false);
        Destroy(gameObject, 0.1f);
    }
}