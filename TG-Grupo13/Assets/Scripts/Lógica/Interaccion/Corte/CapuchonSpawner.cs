using UnityEngine;

public class CapuchonSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefabCapuchon;
    [SerializeField] private Transform puntoSpawn;
    [SerializeField] private float radioDeteccionVacio = 0.15f;

    private GameObject _actual;

    private void Start() => SpawnearSiHaceFalta();

    private void Update()
    {
        if (_actual == null)
        {
            SpawnearSiHaceFalta();
            return;
        }

        // Si se lo llevaron del punto, genera uno nuevo para que el spot nunca quede vacío
        float distancia = Vector3.Distance(_actual.transform.position, puntoSpawn.position);
        if (distancia > radioDeteccionVacio)
            SpawnearSiHaceFalta();
    }

    private void SpawnearSiHaceFalta()
    {
        if (_actual != null) return;
        _actual = Instantiate(prefabCapuchon, puntoSpawn.position, puntoSpawn.rotation);
    }
}