using UnityEngine;

//Este es el encargado de manejar el flujo del juego, incluyendo la gestión de usuarios y la simulación VR.}

/*Actualmente se tienen funciones de mantener la información del usuario logueado, iniciar la simulación VR y cerrar sesión.*/
public class GameFlowManager : MonoBehaviour
{
    //Instancia pública y estática para acceder desde cualquier script
    public static GameFlowManager Instance { get; private set; }

    [Header("Estado Actual")]
    //Para almacenar la información del usuario activo y si hay una simulación en curso
    public UserProfile usuarioActivo;

    //Cambia si la simulación de VR ya inició o si está en espera a que un admin de el inicio de la simulación
    public bool simulacionEnCurso = false;

    //Referencia al canvas de espera que se muestra mientras la simulación no ha iniciado
    [Header("Referencias de la Escena")]
    [SerializeField] private GameObject waitScreenCanvas;

    //Método Awake para asegurar que solo haya una instancia del GameFlowManager
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //Persistir este objeto entre escenas
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //Método Start para mostrar la pantalla de espera si no hay simulación en curso
    private void Start()
    {
        if (!simulacionEnCurso && waitScreenCanvas != null)
        {
            waitScreenCanvas.SetActive(true);
        }
    }

    //Método para iniciar sesión con un usuario específico según el rol que se obtuvo desde la base de datos.
    //Si es administrador, no se inicia la simulación automáticamente.
    public void IniciarSesionConUsuario(UserProfile usuario)
    {
        usuarioActivo = usuario;
        Debug.Log($"[GameFlowManager] Sesión iniciada: {usuario.Nombre} | Admin: {usuario.EsAdministrador}");

        if (!usuario.EsAdministrador)
        {
            LanzarSimulacionVR();
        }
    }

    //Método para iniciar la simulación VR, ocultando la pantalla de espera para el jugador y mostrando un mensaje en la consola.
    public void LanzarSimulacionVR()
    {
        simulacionEnCurso = true;
        Debug.Log("[GameFlowManager] Iniciando simulación VR en el invernadero...");

        if (waitScreenCanvas != null)
        {
            waitScreenCanvas.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[GameFlowManager] No se asignó el WaitScreenCanvas en el Inspector.");
        }
    }

    //Método para cerrar sesión, limpiando la información del usuario activo y mostrando la pantalla de espera nuevamente.
    public void CerrarSesion()
    {
        usuarioActivo = null;
        simulacionEnCurso = false;

        if (waitScreenCanvas != null)
        {
            waitScreenCanvas.SetActive(true);
        }
    }
}