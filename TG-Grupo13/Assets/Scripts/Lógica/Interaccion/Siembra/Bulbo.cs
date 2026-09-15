using UnityEngine;
using Oculus.Interaction;

[RequireComponent(typeof(Grabbable))]
public class Bulbo : MonoBehaviour
{
    private Grabbable _grabbable;
    private PlantingSlot _slotDebajo;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDestroy() => _grabbable.WhenPointerEventRaised -= OnPointerEvent;

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type != PointerEventType.Unselect) return;

        if (_slotDebajo != null && _slotDebajo.Sembrar())
        {
            SiembraActivityManager.Instance.RegistrarPlantaSembrada();
            Destroy(gameObject, 0.1f);
        }
        //Si no había slot válido o ya estaba sembrado, simplemente cae/queda ahí
    }

    private void OnTriggerEnter(Collider other)
    {
        var slot = other.GetComponent<PlantingSlot>();
        if (slot != null) _slotDebajo = slot;
    }

    private void OnTriggerExit(Collider other)
    {
        var slot = other.GetComponent<PlantingSlot>();
        if (slot == _slotDebajo) _slotDebajo = null;
    }
}