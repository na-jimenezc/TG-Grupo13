using UnityEngine;
using UnityEngine.SceneManagement;
using Oculus.Interaction;

public class CapuchonSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefabCapuchon;
    [SerializeField] private Transform puntoSpawn;

    private GameObject _actual;
    private Grabbable _grabbableActual;

    private void Start() => SpawnearSiHaceFalta();

    private void Update()
    {
        if (_actual == null)
            SpawnearSiHaceFalta();
    }

    private void SpawnearSiHaceFalta()
    {
        if (_actual != null) return;

        _actual = Instantiate(prefabCapuchon, puntoSpawn.position, puntoSpawn.rotation);

        // Fuerza que el objeto quede en la MISMA escena que este spawner,
        // sin importar cuál esté "activa" en el momento del Instantiate.
        SceneManager.MoveGameObjectToScene(_actual, gameObject.scene);

        if (_actual.TryGetComponent<Grabbable>(out _grabbableActual))
            _grabbableActual.WhenPointerEventRaised += OnPointerEventCapuchonActual;
    }

    private void OnPointerEventCapuchonActual(PointerEvent evt)
    {
        if (evt.Type != PointerEventType.Select) return;

        if (_grabbableActual != null)
            _grabbableActual.WhenPointerEventRaised -= OnPointerEventCapuchonActual;

        _actual = null;
        _grabbableActual = null;
        SpawnearSiHaceFalta();
    }
}