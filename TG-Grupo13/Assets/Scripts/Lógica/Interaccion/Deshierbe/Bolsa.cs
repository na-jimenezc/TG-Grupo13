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

    private Grabbable _grabbable;

    //EVENTO DEL TUTORIAL
    public event Action OnAgarrada;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDestroy()
    {
        _grabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

    //Método que se llama cuando se produce un evento de puntero en el objeto grabbable
    private void OnPointerEvent(PointerEvent evt)
    {
        //Encender el indicador visual
        if (evt.Type == PointerEventType.Select && indicadorDisponible != null)
            indicadorDisponible.SetActive(true);

        OnAgarrada?.Invoke();
    }
}