using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

/*Apila los ramos terminados en la mano/hombro izquierdo durante la fase de recolección final.
 *El apilado se dispara con el gesto: trigger + grab de la mano izquierda presionados a la vez,
 *mientras el ramo está agarrado. NO depende de la distancia al PuntoApilado.*/
public class HombroAcumulador : MonoBehaviour
{
    public static HombroAcumulador Instance { get; private set; }

    [Tooltip("Punto en la mano izquierda (o zona del hombro) donde se apilan los ramos")]
    [SerializeField] private Transform puntoApilado;
    [SerializeField] private float separacionApilado = 0.06f;
    [Tooltip("Eje LOCAL de PuntoApilado en el que crece la pila")]
    [SerializeField] private Vector3 direccionApilado = Vector3.up;
    [Tooltip("Rotación LOCAL que se le da a cada ramo al apilarlo")]
    [SerializeField] private Vector3 rotacionLocalRamo = Vector3.zero;

    [Header("Input de la mano izquierda (solo controles)")]
    [SerializeField] private OVRInput.Button botonTrigger = OVRInput.Button.PrimaryIndexTrigger;
    [SerializeField] private OVRInput.Button botonGrab = OVRInput.Button.PrimaryHandTrigger;

    private readonly List<GameObject> _ramosApilados = new List<GameObject>();
    public int CantidadApilada => _ramosApilados.Count;

    //Gesto para apilar: trigger y grab izquierdos presionados al mismo tiempo
    public bool GestoApilarPresionado =>
        OVRInput.Get(botonTrigger, OVRInput.Controller.LTouch) &&
        OVRInput.Get(botonGrab, OVRInput.Controller.LTouch);

    //Para descargar en el balde: al soltar trigger o grab izquierdo
    public bool BotonSoltado =>
        OVRInput.GetUp(botonTrigger, OVRInput.Controller.LTouch) ||
        OVRInput.GetUp(botonGrab, OVRInput.Controller.LTouch);

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Apilar(GameObject ramo)
    {
        int index = _ramosApilados.Count;
        _ramosApilados.Add(ramo);
        StartCoroutine(ApilarRoutine(ramo, index));
    }

    private IEnumerator ApilarRoutine(GameObject ramo, int index)
    {
        if (ramo == null) yield break;

        //FASE 1: apagar los interactables y el PhysicsGrabbable.
        //Al deshabilitar el interactable, el interactor que lo sostiene lo suelta solo.
        foreach (var mb in ramo.GetComponentsInChildren<MonoBehaviour>())
            if (mb != null && (mb is IInteractableView || mb.GetType().Name == "PhysicsGrabbable")) mb.enabled = false;

        //Se espera un frame para que el SDK procese la liberación antes de tocar el Rigidbody
        yield return null;
        if (ramo == null) yield break;

        //FASE 2: dejar el ramo inerte y parentarlo a la pila
        if (ramo.TryGetComponent<Grabbable>(out var g)) g.enabled = false;
        foreach (var col in ramo.GetComponentsInChildren<Collider>()) col.enabled = false;

        if (ramo.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
        }

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