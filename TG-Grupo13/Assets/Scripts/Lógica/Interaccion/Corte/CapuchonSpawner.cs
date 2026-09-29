using UnityEngine;
using UnityEngine.SceneManagement;
using Oculus.Interaction;

/*Generador automático de capuchones plásticos en la mesa de trabajo.Garantiza que siempre haya uno disponible para el usuario.*/
public class CapuchonSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefabCapuchon;
    [SerializeField] private Transform puntoSpawn;

    private GameObject _actual;
    private Grabbable _grabbableActual;

    private void Start() => SpawnearSiHaceFalta();

    private void Update()
    {
        //Check de seguridad constante
        if (_actual == null)
            SpawnearSiHaceFalta();
    }

    //Spawnea un capuchón si no hay uno en la escena
    private void SpawnearSiHaceFalta()
    {
        if (_actual != null) return; //Si ya hay uno en la mesa, no hace nada

        _actual = Instantiate(prefabCapuchon, puntoSpawn.position, puntoSpawn.rotation);

        //Se fuerza que el objeto recién creado quede alojado en la MISMA escena aditiva que este spawner para que no se mande a otro lado
        SceneManager.MoveGameObjectToScene(_actual, gameObject.scene);

        //Suscribe el spawner para detectar cuándo el usuario toma este nuevo capuchón
        if (_actual.TryGetComponent<Grabbable>(out _grabbableActual))
            _grabbableActual.WhenPointerEventRaised += OnPointerEventCapuchonActual;
    }

    //Detecta cuando el usuario toma el capuchón actual y lo libera para que se pueda spawnear otro
    private void OnPointerEventCapuchonActual(PointerEvent evt)
    {
        //Detecta el momento exacto en que la mano agarra el capuchón de la mesa
        if (evt.Type != PointerEventType.Select) return;

        //Desuscribe el evento del objeto tomado, pues ya le pertenece al jugador
        if (_grabbableActual != null)
            _grabbableActual.WhenPointerEventRaised -= OnPointerEventCapuchonActual;

        //Libera las referencias internas y genera uno nuevo inmediatamente en la mesa
        _actual = null;
        _grabbableActual = null;
        SpawnearSiHaceFalta();
    }
}