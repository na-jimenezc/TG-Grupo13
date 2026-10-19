using System.Collections;
using Oculus.Interaction.Locomotion;
using UnityEngine;
using UnityEngine.SceneManagement;
using LocomotionCharacter = Oculus.Interaction.Locomotion.CharacterController;

/*Arregla que el joystick izquierdo no mueva al jugador.
 * El FirstPersonLocomotor de Meta busca piso debajo del jugador en su Start y, si no lo encuentra, desactiva el
 * movimiento con joystick para siempre (girar y teletransportarse siguen funcionando). Core no tiene piso: el piso
 * llega después con la escena de espera, que se carga de forma aditiva, así que el movimiento quedaba apagado desde
 * el inicio.
 * Este componente vuelve a activar el movimiento apenas hay piso debajo. Además, si al descargar una escena el
 * jugador se queda sin piso (p. ej. en Continuar), lo pausa para que no caiga y lo reactiva cuando llega el siguiente.
 * Se agrega solo al locomotor al iniciar el juego (no hay que ponerlo en ninguna escena).*/
public class LocomocionSegunPiso : MonoBehaviour
{
    private const float DistanciaMaximaPiso = 10f; //La misma que usa el FirstPersonLocomotor al iniciar

    private FirstPersonLocomotor _locomotor;
    private LocomotionCharacter _personaje;
    private Transform _raizJugador;
    private bool _pausadoPorFaltaDePiso;
    private readonly RaycastHit[] _impactos = new RaycastHit[16];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AgregarAlIniciar()
    {
        AgregarALosLocomotores();
        SceneManager.sceneLoaded += (_, _) => AgregarALosLocomotores();
    }

    private static void AgregarALosLocomotores()
    {
        foreach (var locomotor in FindObjectsByType<FirstPersonLocomotor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (locomotor.GetComponent<LocomocionSegunPiso>() == null)
                locomotor.gameObject.AddComponent<LocomocionSegunPiso>();
        }
    }

    private void Awake()
    {
        _locomotor = GetComponent<FirstPersonLocomotor>();
        _personaje = GetComponentInChildren<LocomotionCharacter>(true);
        if (_personaje == null) _personaje = FindFirstObjectByType<LocomotionCharacter>(FindObjectsInactive.Include);
        _raizJugador = transform.root;
    }

    private void OnEnable() => SceneManager.sceneUnloaded += AlDescargarEscena;
    private void OnDisable() => SceneManager.sceneUnloaded -= AlDescargarEscena;

    private IEnumerator Start()
    {
        //Se espera un frame para que el Start del locomotor ya haya revisado el piso
        yield return null;

        //Al iniciar no puede haber hotspot de teletransporte, así que si ignora la velocidad es porque no encontró piso
        if (_locomotor != null && _locomotor.IgnoringVelocity)
        {
            _pausadoPorFaltaDePiso = true;
            Debug.Log("[LocomocionSegunPiso] Sin piso al iniciar: el movimiento se activará cuando cargue el entorno.");
        }
    }

    private void AlDescargarEscena(Scene _)
    {
        if (_locomotor == null || _pausadoPorFaltaDePiso || _locomotor.IgnoringVelocity) return;
        if (HayPiso()) return;

        //Sin piso el jugador caería mientras carga la siguiente escena
        _locomotor.DisableMovement();
        _pausadoPorFaltaDePiso = true;
    }

    private void Update()
    {
        if (!_pausadoPorFaltaDePiso || _locomotor == null || !HayPiso()) return;

        _pausadoPorFaltaDePiso = false;
        _locomotor.EnableMovement();
        Debug.Log("[LocomocionSegunPiso] Piso encontrado: movimiento con joystick activado.");
    }

    //Busca piso debajo del personaje con las mismas capas que usa la locomoción, sin contar los colliders del propio jugador
    private bool HayPiso()
    {
        Vector3 origen = _personaje != null ? _personaje.Pose.position : transform.position;
        int capas = _personaje != null ? _personaje.LayerMask.value : Physics.DefaultRaycastLayers;
        float distancia = DistanciaMaximaPiso + (_personaje != null ? _personaje.Height : 2f);

        int cantidad = Physics.RaycastNonAlloc(origen, Vector3.down, _impactos, distancia, capas, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < cantidad; i++)
        {
            if (!_impactos[i].collider.transform.IsChildOf(_raizJugador)) return true;
        }
        return false;
    }
}
