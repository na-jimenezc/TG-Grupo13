using UnityEngine;
using Oculus.Interaction;

/*Representa la pala que se usa para cavar en los slots de siembra. 
 *Cuando está equipada y se pulsa el gatillo, cava en el slot más cercano.*/
[RequireComponent(typeof(Grabbable))]
public class Pala : MonoBehaviour
{
    //Referencia a la punta de la pala, que es la parte que debe entrar en contacto con el slot para cavar
    [SerializeField] private Transform puntaPala; 

    private PlantingSlot _slotCercano; //Guarda temporalmente el slot sobre el que está la pala
    private bool _equipada = false; //Bandera para saber si el usuario la sostiene

    private void Awake()
    {
        //Se suscribe a los eventos de agarre del Oculus Interaction SDK
        GetComponent<Grabbable>().WhenPointerEventRaised += (evt) =>
        {
            if (evt.Type == PointerEventType.Select) _equipada = true;
            else if (evt.Type == PointerEventType.Unselect) _equipada = false;
        };
    }

    //Update se llama una vez por frame y verifica si la pala está equipada y si hay un slot cercano. Si se presiona el gatillo, se llama al método Cavar() del slot.
    private void Update()
    {
        //Solo puede cavar si la tiene en la mano y está sobre un slot de tierra
        if (!_equipada || _slotCercano == null) return;

        //Lee el gatillo índice de cualquiera de los dos controladores 
        bool trigger = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger)
                    || OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger);

        //Intenta cavar en el slot guardado
        if (trigger)
            _slotCercano.Cavar();
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