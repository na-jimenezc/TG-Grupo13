using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

/*Apila los ramos terminados en la mano/hombro secundario (izquierdo para diestros, derecho para zurdos)
 *durante la fase de recolección final. El apilado se dispara con el gesto: trigger + grab de la mano
 *secundaria presionados a la vez, mientras el ramo está agarrado.*/
public class HombroAcumulador : MonoBehaviour
{
    public static HombroAcumulador Instance { get; private set; }

    [Tooltip("Punto cerca del hombro")]
    [SerializeField] private Transform puntoApilado;
    [SerializeField] private float separacionApilado = 0.06f;
    [Tooltip("Eje local de la pila")]
    [SerializeField] private Vector3 direccionApilado = Vector3.up;
    [Tooltip("Rotación local del ramo")]
    [SerializeField] private Vector3 rotacionLocalRamo = Vector3.zero;

    [Header("Input de la mano secundaria (ver Lateralidad)")]
    [SerializeField] private OVRInput.Button botonTrigger = OVRInput.Button.PrimaryIndexTrigger;
    [SerializeField] private OVRInput.Button botonGrab = OVRInput.Button.PrimaryHandTrigger;

    private readonly List<GameObject> _ramosApilados = new List<GameObject>();
    //Se quitan primero los ramos ya destruidos (p. ej. si la sesión se terminó a mitad de Corte),
    //para que la siguiente sesión no empiece contando ramos que ya no existen
    public int CantidadApilada
    {
        get
        {
            _ramosApilados.RemoveAll(r => r == null);
            return _ramosApilados.Count;
        }
    }

    //Gesto para apilar: trigger y grab de la mano secundaria presionados al mismo tiempo
    public bool GestoApilarPresionado =>
        OVRInput.Get(botonTrigger, Lateralidad.ControlSecundario) &&
        OVRInput.Get(botonGrab, Lateralidad.ControlSecundario);

    //Para descargar en el balde: al soltar trigger o grab de la mano secundaria
    public bool BotonSoltado =>
        OVRInput.GetUp(botonTrigger, Lateralidad.ControlSecundario) ||
        OVRInput.GetUp(botonGrab, Lateralidad.ControlSecundario);

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Apilar(GameObject ramo)
    {
        int index = CantidadApilada;
        _ramosApilados.Add(ramo);

        //El hombro vive en Core: se registra antes de apilarlo para que se destruya con la escena de Corte
        DestruirConEscenaOrigen.Registrar(ramo);
        StartCoroutine(ApilarRoutine(ramo, index));
    }

    //Coroutine que apila un ramo en el hombro, desactivando sus componentes interactivos y ajustando su posición y rotación.
    private IEnumerator ApilarRoutine(GameObject ramo, int index)
    {
        if (ramo == null) yield break;

        //Se apagan los interactables y el PhysicsGrabbable.
        //Y cuando se desactiva el PhysicsGrabbable, el SDK de Oculus libera el objeto automáticamente.
        foreach (var mb in ramo.GetComponentsInChildren<MonoBehaviour>())
            if (mb != null && (mb is IInteractableView || mb.GetType().Name == "PhysicsGrabbable")) mb.enabled = false;

        //Se espera un frame para que el SDK procese la liberación antes de tocar el Rigidbody
        yield return null;
        if (ramo == null) yield break;

        //Se parenta el tallo a la pila y se ajusta su posición y rotación
        if (ramo.TryGetComponent<Grabbable>(out var g)) g.enabled = false;
        foreach (var col in ramo.GetComponentsInChildren<Collider>()) col.enabled = false;

        if (ramo.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
        }

        //Se ajusta la posición y rotación del ramo en la pila
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