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

    private Grabbable _grabbable; //Referencia al componente Grabbable para detectar cuando el usuario la agarra o suelta

    private PlantingSlot _slotCercano; //Guarda temporalmente el slot sobre el que está la pala
    private bool _equipada = false; //Bandera para saber si el usuario la sostiene

    //EVENTO PARA EL TUTORIAL
    public event Action OnAgarrada;

    private void Awake()
    {
        //Se suscribe a los eventos de agarre del Oculus Interaction SDK
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
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
        //Solo puede cavar si la tiene en la mano y está sobre un slot de tierra
        if (!_equipada || _slotCercano == null) return;

        //Intenta cavar en el slot guardado al presionar el gatillo de la mano dominante
        if (Lateralidad.GatilloDominantePresionado)
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