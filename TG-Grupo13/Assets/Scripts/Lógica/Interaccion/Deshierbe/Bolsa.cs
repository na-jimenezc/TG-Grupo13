using UnityEngine;
using Oculus.Interaction;

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

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select && indicadorDisponible != null)
            indicadorDisponible.SetActive(true);
    }
}