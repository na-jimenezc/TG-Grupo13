using UnityEngine;
using UnityEngine.SceneManagement; 
using System.Collections;

/* El gameflow manager es el encargado de administrar las escenas y el flujo de la simulación VR, 
 * incluyendo la gestión de usuarios y la transición entre escenas. */
public class GameFlowManager : MonoBehaviour
{
    //Acceso global para el GameFlowManager, asegurando que solo exista una instancia en la escena.
    public static GameFlowManager Instance { get; private set; }


    //Mensaje de depuración para indicar que el GameFlowManager ha sido inicializado correctamente.
    [Header("Estado Actual")]
    public UserProfile usuarioActivo;
    public bool simulacionEnCurso = false;

    [Header("Referencias de la Escena")]
    [SerializeField] private GameObject waitScreenCanvas;


    //Nombres de las escenas que se cargarán de forma aditiva para la simulación VR.
    //SE TIENE QUE PONER EL MISMO NOMBRE QUE TIENE EN UNITY Y HACERLAS RECONOCIBLES POR MEDIO DEL BUILD SETTINGS
    [Header("Nombres de Escenas Aditivas")]
    [SerializeField] private string escenaEsperaNombre = "PantallaEsperaInicial";
    [SerializeField] private string escenaInvernaderoNombre = "InvernaderoEscenaInicial";


    //Mantener la instancia del GameFlowManager a lo largo de las escenas, evitando que se destruya al cargar nuevas escenas.
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //Mostrar la pantalla de espera si no hay simulación en curso al iniciar la aplicación.
    private void Start()
    {
        if (!simulacionEnCurso && waitScreenCanvas != null)
        {
            waitScreenCanvas.SetActive(true);
        }
    }

    //Inicia la sesión con el usuario proporcionado y lanza la simulación VR si no es administrador.
    public void IniciarSesionConUsuario(UserProfile usuario)
    {
        usuarioActivo = usuario;
        Debug.Log($"[GameFlowManager] Sesión iniciada: {usuario.Nombre} | Admin: {usuario.EsAdministrador}");

        //Acá se pasa de la escena de espera a la escena de selección de actividades si el usuario no es administrador.
        if (!usuario.EsAdministrador)
        {
            LanzarSimulacionVR();
        }
    }

    //Inicia la simulación VR cargando la escena del invernadero y ocultando la UI de espera.
    public void LanzarSimulacionVR()
    {
        simulacionEnCurso = true;
        Debug.Log("[GameFlowManager] Iniciando simulación VR en el invernadero...");

        //Ocultar la UI de espera
        if (waitScreenCanvas != null)
        {
            waitScreenCanvas.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[GameFlowManager] No se asignó el WaitScreenCanvas en el Inspector.");
        }

        //Iniciar el intercambio de escenas aditivas de fondo
        StartCoroutine(RutinaTransicionSimulacion());
    }


    //Corrutina que descarga la escena del entorno inicial y carga la escena del invernadero.
    private IEnumerator RutinaTransicionSimulacion()
    {
        //Descargar la escena de entorno de espera si está cargada
        Scene escenaEspera = SceneManager.GetSceneByName(escenaEsperaNombre);
        if (escenaEspera.isLoaded)
        {
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(escenaEsperaNombre);
            while (!unloadOp.isDone)
            {
                yield return null;
            }
        }

        //Cargar de forma aditiva la escena del Invernadero
        Scene escenaInvernadero = SceneManager.GetSceneByName(escenaInvernaderoNombre);
        if (!escenaInvernadero.isLoaded)
        {
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(escenaInvernaderoNombre, LoadSceneMode.Additive);
            while (!loadOp.isDone)
            {
                yield return null;
            }
        }

        //Definir la nueva escena como activa para instanciar objetos correctamente
        Scene nuevaEscena = SceneManager.GetSceneByName(escenaInvernaderoNombre);
        if (nuevaEscena.isLoaded)
        {
            SceneManager.SetActiveScene(nuevaEscena);
        }
    }

    //Cierra la sesión del usuario activo, reinicia el estado de la simulación.
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