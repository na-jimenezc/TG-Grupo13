using UnityEngine;
using Oculus.Interaction;
/*Recipiente para depositar las malas hierbas que se recogen en el juego. 
 *Se puede agarrar y tiene un indicador visual opcional que se activa cuando se agarra la bolsa.*/

[RequireComponent(typeof(Grabbable))]
public class Bolsa : MonoBehaviour
{
    [Tooltip("Opcional: objeto que se resalta cuando agarras la bolsa (retroalimentación visual)")]
    [SerializeField] private GameObject indicadorDisponible;

    private Grabbable _grabbable;

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
        //Enciende el indicador visual (ej: brillo o aro en la boquilla) al momento de agarrar la bolsa
        if (evt.Type == PointerEventType.Select && indicadorDisponible != null)
            indicadorDisponible.SetActive(true);
    }
}