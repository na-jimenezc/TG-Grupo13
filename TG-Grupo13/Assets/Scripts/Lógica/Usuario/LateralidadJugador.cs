using UnityEngine;

/*Va en el Jugador (Core). Acomoda el rig según la mano dominante del perfil activo (ver Lateralidad):
 * - RolManoDominante (tag ManoDominante + collider para deshojar) va en la mano dominante.
 * - RolManoSecundaria (ManoSecundaria, ManoIzquierdaController, HombroAcumulador y sus puntos) va en la otra.
 * - El holster de las tijeras pasa a la cadera del lado dominante.
 * Los dos roles son hijos de los anchors de las manos, así que basta con cambiarlos de padre.
 * Se crean con el menú TG-Grupo13 > Configurar mano dominante en Core.*/
public class LateralidadJugador : MonoBehaviour
{
    public enum ModoPrueba { SegunPerfil, ForzarDiestro, ForzarZurdo }

    [Header("Manos del rig")]
    [SerializeField] private Transform manoIzquierda; //LeftHandAnchor
    [SerializeField] private Transform manoDerecha;   //RightHandAnchor

    [Header("Roles que se cambian de mano")]
    [SerializeField] private Transform rolManoDominante;
    [SerializeField] private Transform rolManoSecundaria;
    [SerializeField] private Transform puntoHolster;

    [Header("Solo editor")]
    [Tooltip("Para probar en el editor sin iniciar sesión con un perfil zurdo. En el build siempre se usa el perfil.")]
    [SerializeField] private ModoPrueba modoPrueba = ModoPrueba.SegunPerfil;

    private bool? _aplicado;

    private void Start()
    {
        ActualizarForzado();
        if (GameFlowManager.Instance != null) GameFlowManager.Instance.OnEstadoCambiado += Aplicar;
        Aplicar();
    }

    private void OnDestroy()
    {
        if (GameFlowManager.Instance != null) GameFlowManager.Instance.OnEstadoCambiado -= Aplicar;
    }

    //Permite cambiar el modo de prueba desde el Inspector con el juego corriendo
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        ActualizarForzado();
        Aplicar();
    }

    private void ActualizarForzado()
    {
#if UNITY_EDITOR
        Lateralidad.ForzadoEnEditor = modoPrueba == ModoPrueba.SegunPerfil ? (bool?)null : modoPrueba == ModoPrueba.ForzarZurdo;
#endif
    }

    //Se llama al iniciar y cada vez que cambia la fase de la sesión (p. ej. al iniciar sesión con otro usuario)
    private void Aplicar()
    {
        bool zurdo = Lateralidad.EsZurdo;
        if (_aplicado == zurdo) return;
        _aplicado = zurdo;

        Reubicar(rolManoDominante, zurdo ? manoIzquierda : manoDerecha);
        Reubicar(rolManoSecundaria, zurdo ? manoDerecha : manoIzquierda);

        //El holster está a la derecha (x positivo) para diestros
        if (puntoHolster != null)
        {
            var p = puntoHolster.localPosition;
            p.x = Mathf.Abs(p.x) * (zurdo ? -1f : 1f);
            puntoHolster.localPosition = p;
        }

        Debug.Log($"[LateralidadJugador] Rig configurado para {(zurdo ? "zurdo" : "diestro")}");
    }

    private static void Reubicar(Transform rol, Transform mano)
    {
        if (rol == null || mano == null) return;

        //Si el anchor de la mano está desactivado, el rol queda apagado con él: lo que se sostiene (tallos, ramo)
        //no se ve y sus colliders no detectan nada. Se activa para que ambas manos funcionen sin importar el perfil.
        if (!mano.gameObject.activeSelf)
        {
            Debug.LogWarning($"[LateralidadJugador] {mano.name} estaba desactivado; se activa para que {rol.name} funcione.");
            mano.gameObject.SetActive(true);
        }

        if (rol.parent == mano) return;
        rol.SetParent(mano, worldPositionStays: false);
        rol.localPosition = Vector3.zero;
        rol.localRotation = Quaternion.identity;
    }
}
