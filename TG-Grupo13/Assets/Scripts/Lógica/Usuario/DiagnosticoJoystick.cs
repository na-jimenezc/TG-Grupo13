using UnityEngine;
using LocomotionCharacter = Oculus.Interaction.Locomotion.CharacterController;

/*Script de diagnóstico (se puede poner en el Jugador de Core y quitar después) para saber por qué el jugador
 * no avanza con el joystick izquierdo. Escribe en la consola cada medio segundo:
 * - Si el control izquierdo está conectado, si se detecta el dedo sobre el joystick y cuánto marca el joystick.
 *   Si hay dedo pero el joystick marca 0 al empujarlo, el problema es del control (físico).
 * - Si el joystick sí marca, hace el mismo barrido de cápsula que la locomoción de Meta y dice con qué collider
 *   choca. Si aparece un collider (tijeras, pala, tallo, el CharacterController del Camera Rig...), es de código/escena.*/
public class DiagnosticoJoystick : MonoBehaviour
{
    [SerializeField] private float intervaloLog = 0.5f;
    [SerializeField] private float distanciaBarrido = 0.3f;
    [SerializeField] private float zonaMuerta = 0.2f;

    private LocomotionCharacter _personaje;
    private Transform _cabeza;
    private float _siguienteLog;

    private void Start()
    {
        _personaje = FindFirstObjectByType<LocomotionCharacter>(FindObjectsInactive.Include);
        if (_personaje == null) Debug.LogWarning("[DiagnosticoJoystick] No se encontró el CharacterController de la locomoción de Meta.");
        if (Camera.main != null) _cabeza = Camera.main.transform;
    }

    private void Update()
    {
        if (Time.unscaledTime < _siguienteLog) return;

        bool conectado = OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);
        bool dedo = OVRInput.Get(OVRInput.Touch.PrimaryThumbstick, OVRInput.Controller.LTouch);
        Vector2 eje = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);

        //Solo se escribe cuando hay algo que revisar, para no llenar la consola
        if (!dedo && eje.magnitude < zonaMuerta) return;
        _siguienteLog = Time.unscaledTime + intervaloLog;

        string estado = $"[DiagnosticoJoystick] Control izq. conectado: {conectado} | dedo en joystick: {dedo} | eje: {eje.ToString("F2")}";

        if (eje.magnitude < zonaMuerta)
        {
            Debug.Log(estado + " → el joystick no marca movimiento (si lo estás empujando, revisa el control).");
            return;
        }

        if (_personaje == null || _cabeza == null)
        {
            Debug.Log(estado);
            return;
        }

        //Dirección del joystick relativa a hacia dónde mira la cabeza, en el plano del piso
        Vector3 adelante = Vector3.ProjectOnPlane(_cabeza.forward, Vector3.up).normalized;
        Vector3 derecha = Vector3.ProjectOnPlane(_cabeza.right, Vector3.up).normalized;
        Vector3 direccion = (adelante * eje.y + derecha * eje.x).normalized;

        //Mismo barrido que hace la locomoción de Meta (ver Oculus.Interaction.Locomotion.CharacterController)
        Vector3 centro = _personaje.Pose.position;
        Vector3 medio = Vector3.up * Mathf.Max(0f, _personaje.Height * 0.5f - _personaje.Radius);
        if (Physics.CapsuleCast(centro - medio, centro + medio, _personaje.Radius, direccion, out RaycastHit hit,
                distanciaBarrido, _personaje.LayerMask.value, QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning(estado + $" → la cápsula de locomoción CHOCA con '{RutaDe(hit.collider.transform)}' " +
                             $"(capa {LayerMask.LayerToName(hit.collider.gameObject.layer)}, a {hit.distance:F2} m).", hit.collider);
        }
        else
        {
            Debug.Log(estado + " → nada bloquea el movimiento.");
        }
    }

    private static string RutaDe(Transform t)
    {
        string ruta = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            ruta = t.name + "/" + ruta;
        }
        return ruta;
    }
}
