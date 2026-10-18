using UnityEngine;
using Oculus.Interaction;

/*Envoltorio plástico (funda) que agrupa el ramo finalizado.*/
[RequireComponent(typeof(Grabbable))]
public class Capuchon : MonoBehaviour
{
    [SerializeField] private GameObject prefabRamoTerminado; //Prefab del ramo finalizado que se instanciará al ensamblar correctamente
    [SerializeField] private float distanciaEnsamble = 0.15f; //Distancia máxima a la que se puede ensamblar el capuchón con el ramo

    private Grabbable _grabbable;
    private Rigidbody _rb;

    //EVENTO DEL TUTORIAL (estático porque los capuchones se instancian en runtime)
    public static event System.Action OnEnsamblado;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _rb = GetComponent<Rigidbody>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDestroy() => _grabbable.WhenPointerEventRaised -= OnPointerEvent;

    //Maneja los eventos de selección y deselección del objeto
    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
        {
            if (_rb != null) _rb.isKinematic = false;   //Se activan las físicas normales al agarrar el capuchón
            return;
        }

        //Solo evalúa la fusión al soltar el objeto
        if (evt.Type != PointerEventType.Unselect) return;

        //Se revisa si ya hay un ramo en progreso y si se puede ensamblar con el capuchón
        var ramo = RamoEnProgreso.Instance;
        if (ramo == null || !ramo.PuedeArmarseConCapuchon)
        {
            if (_rb != null) _rb.isKinematic = false; // si lo sueltas sin ensamblar, cae con física normal
            return;
        }

        //Validación de distancia entre el capuchón soltado y el ramo de la mano secundaria
        float distancia = Vector3.Distance(transform.position, ramo.transform.position);
        if (distancia > distanciaEnsamble)
        {
            if (_rb != null) _rb.isKinematic = false;
            return;
        }

        //Ensambla correctamente: instancia el ramo finalizado fusionado
        Vector3 posicion = ramo.transform.position;
        Instantiate(prefabRamoTerminado, posicion, Quaternion.identity);
        OnEnsamblado?.Invoke();

        //Se limpia el ramo y se destruye el capuchón
        ramo.Reiniciar();
        gameObject.SetActive(false);
        Destroy(gameObject, 0.1f);
    }
}