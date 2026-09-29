using Oculus.Interaction;
using System.Collections.Generic;
using UnityEngine;

/*Permite apilar los ramos en el hombro izquierdo del jugador y registrar el primer ramo en la malla de entutorado.*/
public class HombroAcumulador : MonoBehaviour
{
    public static HombroAcumulador Instance { get; private set; }

    [Tooltip("Punto en la mano izquierda (o zona del hombro) donde se apilan los ramos")]
    [SerializeField] private Transform puntoApilado;
    [SerializeField] private float radioDeteccionTransferencia = 0.15f; //Radio de detección para que un ramo pueda ser apilado en el hombro
    [SerializeField] private float separacionApilado = 0.06f; //Separación vertical entre ramos apilados

    private readonly List<GameObject> _ramosApilados = new List<GameObject>();
    public int CantidadApilada => _ramosApilados.Count;

    private void Awake() => Instance = this;

    //Comprueba si un ramo puede ser apilado en el hombro izquierdo del jugador
    public bool PuedeRecibir(Vector3 posicionRamo)
    {
        return Vector3.Distance(posicionRamo, puntoApilado.position) <= radioDeteccionTransferencia;
    }

    //Apila un ramo en el hombro izquierdo del jugador
    public void Apilar(GameObject ramo)
    {
        //El punto de apilado es hijo del rig del jugador (mano izquierda),
        //así que al parentarse el ramo sigue la mano automáticamente
        ramo.transform.SetParent(puntoApilado, worldPositionStays: false);

        //Posicionamiento con offset vertical basado en cuántos hay
        int index = _ramosApilados.Count;
        ramo.transform.localPosition = new Vector3(0f, index * separacionApilado, 0f);
        ramo.transform.localRotation = Quaternion.identity;

        //Deshabilita interacciones para evitar sacar un ramo por error mientras se transporta
        if (ramo.TryGetComponent<Grabbable>(out var g)) g.enabled = false;
        foreach (var col in ramo.GetComponentsInChildren<Collider>()) col.enabled = false;

        _ramosApilados.Add(ramo);
    }

    //Deposita todos los ramos apilados en el hombro izquierdo del jugador en el balde de recolección
    public void DepositarTodoEnBalde()
    {
        foreach (var ramo in _ramosApilados)
            if (ramo != null) Destroy(ramo);

        _ramosApilados.Clear();
    }
}