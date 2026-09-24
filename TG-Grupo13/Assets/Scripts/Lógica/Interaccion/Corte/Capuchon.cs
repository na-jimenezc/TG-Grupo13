using UnityEngine;
using Oculus.Interaction;

[RequireComponent(typeof(Grabbable))]
public class Capuchon : MonoBehaviour
{
    [SerializeField] private GameObject prefabRamoTerminado;
    [SerializeField] private float distanciaEnsamble = 0.15f;

    private Grabbable _grabbable;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDestroy() => _grabbable.WhenPointerEventRaised -= OnPointerEvent;

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type != PointerEventType.Unselect) return;

        var ramo = RamoEnProgreso.Instance;
        if (ramo == null || !ramo.PuedeArmarseConCapuchon) return;

        float distancia = Vector3.Distance(transform.position, ramo.transform.position);
        if (distancia > distanciaEnsamble) return;

        // Ensambla: reemplaza tallos + capuchón por el modelo del ramo terminado
        Vector3 posicion = ramo.transform.position;
        Instantiate(prefabRamoTerminado, posicion, Quaternion.identity);

        ramo.Reiniciar();
        gameObject.SetActive(false);
        Destroy(gameObject, 0.1f);
    }
}