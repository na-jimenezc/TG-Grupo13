using UnityEngine;
using UnityEngine.SceneManagement; // Importante para manejar escenas
using System.Collections;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Estado Actual")]
    public UserProfile usuarioActivo;
    public bool simulacionEnCurso = false;

    [Header("Referencias de la Escena")]
    [SerializeField] private GameObject waitScreenCanvas;

    [Header("Nombres de Escenas Aditivas")]
    [SerializeField] private string escenaEsperaNombre = "PantallaEsperaInicial";
    [SerializeField] private string escenaInvernaderoNombre = "InvernaderoEscenaInicial";

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

    private void Start()
    {
        if (!simulacionEnCurso && waitScreenCanvas != null)
        {
            waitScreenCanvas.SetActive(true);
        }
    }

    public void IniciarSesionConUsuario(UserProfile usuario)
    {
        usuarioActivo = usuario;
        Debug.Log($"[GameFlowManager] Sesión iniciada: {usuario.Nombre} | Admin: {usuario.EsAdministrador}");

        if (!usuario.EsAdministrador)
        {
            LanzarSimulacionVR();
        }
    }

    public void LanzarSimulacionVR()
    {
        simulacionEnCurso = true;
        Debug.Log("[GameFlowManager] Iniciando simulación VR en el invernadero...");

        // Ocultar la UI de espera
        if (waitScreenCanvas != null)
        {
            waitScreenCanvas.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[GameFlowManager] No se asignó el WaitScreenCanvas en el Inspector.");
        }

        // Iniciar el intercambio de escenas aditivas de fondo
        StartCoroutine(RutinaTransicionSimulacion());
    }

    /// <summary>
    /// Corrutina que descarga la escena del entorno inicial y carga la escena del invernadero.
    /// </summary>
    private IEnumerator RutinaTransicionSimulacion()
    {
        // 1. Descargar la escena de entorno de espera si está cargada
        Scene escenaEspera = SceneManager.GetSceneByName(escenaEsperaNombre);
        if (escenaEspera.isLoaded)
        {
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(escenaEsperaNombre);
            while (!unloadOp.isDone)
            {
                yield return null;
            }
        }

        // 2. Cargar de forma aditiva la escena del Invernadero
        Scene escenaInvernadero = SceneManager.GetSceneByName(escenaInvernaderoNombre);
        if (!escenaInvernadero.isLoaded)
        {
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(escenaInvernaderoNombre, LoadSceneMode.Additive);
            while (!loadOp.isDone)
            {
                yield return null;
            }
        }

        // 3. Definir la nueva escena como activa para instanciar objetos correctamente
        Scene nuevaEscena = SceneManager.GetSceneByName(escenaInvernaderoNombre);
        if (nuevaEscena.isLoaded)
        {
            SceneManager.SetActiveScene(nuevaEscena);
        }
    }

    public void CerrarSesion()
    {
        usuarioActivo = null;
        simulacionEnCurso = false;

        if (waitScreenCanvas != null)
        {
            waitScreenCanvas.SetActive(true);
        }

        // (Opcional) Puedes invocar otra corrutina aquí para devolver al usuario a PantallaEsperaInicial
    }
}