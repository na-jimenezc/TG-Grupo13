using Oculus.Interaction;
using System.Collections.Generic;
using UnityEngine;

public class HombroAcumulador : MonoBehaviour
{
    public static HombroAcumulador Instance { get; private set; }

    [Tooltip("Punto en la mano izquierda (o zona del hombro) donde se apilan los ramos")]
    [SerializeField] private Transform puntoApilado;
    [SerializeField] private float radioDeteccionTransferencia = 0.15f;
    [SerializeField] private float separacionApilado = 0.06f;

    private readonly List<GameObject> _ramosApilados = new List<GameObject>();
    public int CantidadApilada => _ramosApilados.Count;

    private void Awake() => Instance = this;

    public bool PuedeRecibir(Vector3 posicionRamo)
    {
        return Vector3.Distance(posicionRamo, puntoApilado.position) <= radioDeteccionTransferencia;
    }

    public void Apilar(GameObject ramo)
    {
        // El punto de apilado es hijo del rig del jugador (mano izquierda),
        // así que al parentarse el ramo sigue la mano automáticamente, sin necesidad de un Grab extra.
        ramo.transform.SetParent(puntoApilado, worldPositionStays: false);
        int index = _ramosApilados.Count;
        ramo.transform.localPosition = new Vector3(0f, index * separacionApilado, 0f);
        ramo.transform.localRotation = Quaternion.identity;

        // Ya no se vuelve a agarrar individualmente mientras está apilado
        if (ramo.TryGetComponent<Grabbable>(out var g)) g.enabled = false;
        foreach (var col in ramo.GetComponentsInChildren<Collider>()) col.enabled = false;

        _ramosApilados.Add(ramo);
    }

    public void DepositarTodoEnBalde()
    {
        foreach (var ramo in _ramosApilados)
            if (ramo != null) Destroy(ramo);

        _ramosApilados.Clear();
    }
}