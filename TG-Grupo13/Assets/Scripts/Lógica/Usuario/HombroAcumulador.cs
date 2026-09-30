using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

/*Permite apilar los ramos terminados en el hombro/mano izquierda del jugador durante la fase de recolección final.*/
public class HombroAcumulador : MonoBehaviour
{
    public static HombroAcumulador Instance { get; private set; }

    [Tooltip("Punto en la mano izquierda (o zona del hombro) donde se apilan los ramos")]
    [SerializeField] private Transform puntoApilado;
    [SerializeField] private float radioDeteccionTransferencia = 0.25f; //Antes 0.7: demasiado grande, capturaba ramos soltados en la malla
    [SerializeField] private float separacionApilado = 0.06f;
    [Tooltip("Eje LOCAL de PuntoApilado en el que crece la pila (ajústalo en Play para que los ramos queden uno sobre otro)")]
    [SerializeField] private Vector3 direccionApilado = Vector3.up;
    [Tooltip("Rotación LOCAL que se le da a cada ramo al apilarlo (ajústala en Play)")]
    [SerializeField] private Vector3 rotacionLocalRamo = Vector3.zero;

    [Header("Input de la mano izquierda (solo controles)")]
    [SerializeField] private bool requerirBotonAlApilar = true;
    [SerializeField] private OVRInput.Button botonMano = OVRInput.Button.PrimaryIndexTrigger;

    private readonly List<GameObject> _ramosApilados = new List<GameObject>();
    public int CantidadApilada => _ramosApilados.Count;

    public bool BotonPresionado => OVRInput.Get(botonMano, OVRInput.Controller.LTouch);
    public bool BotonSoltado => OVRInput.GetUp(botonMano, OVRInput.Controller.LTouch);

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    //Un ramo se puede apilar si se suelta cerca de la mano izquierda (y con el botón presionado, si está activado)
    public bool PuedeRecibir(Vector3 posicionRamo)
    {
        if (puntoApilado == null) return false;
        if (Vector3.Distance(posicionRamo, puntoApilado.position) > radioDeteccionTransferencia) return false;
        return !requerirBotonAlApilar || BotonPresionado;
    }

    public void Apilar(GameObject ramo)
    {
        //Se reserva el índice ya, pero la transformación se aplica un frame después (ver corrutina)
        int index = _ramosApilados.Count;
        _ramosApilados.Add(ramo);
        StartCoroutine(ApilarTrasFrame(ramo, index));
    }

    /*Se espera un frame porque Apilar() se llama DENTRO del evento Unselect: en ese mismo frame
     *PhysicsGrabbable/GrabInteractable restauran el Rigidbody a no-kinemático y aplican la velocidad de lanzamiento,
     *pisando cualquier cambio que hagamos aquí (por eso el ramo "no se quedaba").*/
    private IEnumerator ApilarTrasFrame(GameObject ramo, int index)
    {
        yield return null;
        if (ramo == null) yield break;

        //1. Apagar toda la interacción (interactables, physics grabbable, grabbable, colliders)
        //PhysicsGrabbable se busca por nombre: su namespace/assembly cambia entre versiones del Interaction SDK
        foreach (var mb in ramo.GetComponentsInChildren<MonoBehaviour>())
            if (mb != null && (mb is IInteractableView || mb.GetType().Name == "PhysicsGrabbable")) mb.enabled = false;
        if (ramo.TryGetComponent<Grabbable>(out var g)) g.enabled = false;
        foreach (var col in ramo.GetComponentsInChildren<Collider>()) col.enabled = false;

        //2. El Rigidbody debe quedar kinemático, si no la gravedad lo suelta de la mano
        if (ramo.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
        }

        //3. Parentar y posicionar en la pila
        ramo.transform.SetParent(puntoApilado, worldPositionStays: false);
        ramo.transform.localPosition = direccionApilado.normalized * (index * separacionApilado);
        ramo.transform.localRotation = Quaternion.Euler(rotacionLocalRamo);
    }

    //Deposita todos los ramos apilados en el balde. Devuelve cuántos se depositaron.
    public int DepositarTodoEnBalde()
    {
        int cantidad = _ramosApilados.Count;
        foreach (var ramo in _ramosApilados)
            if (ramo != null) Destroy(ramo);

        _ramosApilados.Clear();
        return cantidad;
    }
}