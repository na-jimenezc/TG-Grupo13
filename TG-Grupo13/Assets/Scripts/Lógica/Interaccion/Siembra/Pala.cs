using UnityEngine;
using Oculus.Interaction;

[RequireComponent(typeof(Grabbable))]
public class Pala : MonoBehaviour
{
    [SerializeField] private Transform puntaPala; 

    private PlantingSlot _slotCercano;
    private bool _equipada = false;

    private void Awake()
    {
        GetComponent<Grabbable>().WhenPointerEventRaised += (evt) =>
        {
            if (evt.Type == PointerEventType.Select) _equipada = true;
            else if (evt.Type == PointerEventType.Unselect) _equipada = false;
        };
    }

    private void Update()
    {
        if (!_equipada || _slotCercano == null) return;

        bool trigger = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger)
                    || OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger);

        if (trigger)
            _slotCercano.Cavar();
    }

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