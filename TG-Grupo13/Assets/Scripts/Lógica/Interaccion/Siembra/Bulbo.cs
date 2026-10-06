using UnityEngine;
using Oculus.Interaction;
using System;
/*Elemento qu erepresenta al bulbo, se siembra al soltarlo sobre el terreno anteriormente cavado*/

[RequireComponent(typeof(Grabbable))]
public class Bulbo : MonoBehaviour
{
    private Grabbable _grabbable;
    private PlantingSlot _slotDebajo; //Referencia al terreno sobre el que está flotando

    //EVENTO DEL TUTORIAL
    public event Action OnAgarrada;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    //Cuando se destruye el bulbo, se desuscribe del evento
    private void OnDestroy() => _grabbable.WhenPointerEventRaised -= OnPointerEvent;

    //Cuando se suelta el bulbo, si hay un slot válido debajo, se siembra
    private void OnPointerEvent(PointerEvent evt)
    {

        //Al agarrarlo, solo notifica al tutorial, no se cambia la lógica real
        if (evt.Type == PointerEventType.Select)
        {
            OnAgarrada?.Invoke();
            return;
        }

        //Solo nos interesa el momento en que el usuario lo suelta de su mano
        if (evt.Type != PointerEventType.Unselect) return;

        //Si hay un slot debajo e intenta sembrar exitosamente (retorna true)
        if (_slotDebajo != null && _slotDebajo.Sembrar())
        {
            //Notifica el progreso al manager de la actividad
            SiembraActivityManager.Instance.RegistrarPlantaSembrada();

            //Destruye y oculta el bulbo, ya que fue sembrado
            gameObject.SetActive(false); 
            Destroy(gameObject, 0.1f);
        }
        //Si no había slot válido o ya estaba sembrado, simplemente cae/queda ahí
    }

    //Registra la presencia de un terreno debajo del bulbo
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