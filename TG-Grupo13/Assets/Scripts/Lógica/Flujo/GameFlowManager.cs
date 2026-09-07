using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

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

    [SerializeField] private GameMasterUI gameMasterUI;


    //Nombres de las escenas que se cargarán de forma aditiva para la simulación VR.
    //SE TIENE QUE PONER EL MISMO NOMBRE QUE TIENE EN UNITY Y HACERLAS RECONOCIBLES POR MEDIO DEL BUILD SETTINGS
    [Header("Nombres de Escenas Aditivas")]
    [SerializeField] private string escenaEsperaNombre = "PantallaEsperaInicial";
    [SerializeField] private string escenaSeleccionActividadesNombre = "MenuSeleccionActividades";
    [SerializeField] private string escenaMenuSiembraNombre = "MenuSiembra";
    [SerializeField] private string escenaMenuDeshierbeNombre = "MenuDeshierbe";
    [SerializeField] private string escenaMenuCorteNombre = "MenuCorte";

    //Cola para las actividades
    private Queue<ActivityType> _colaActividades = new Queue<ActivityType>();
    /*Bandera para indicar si una secuencia de actividades está en proceso, evitando que se inicie otra mientras la actual no ha terminado
    //Esta es la solución para evitar que se inicie otra secuencia mientras la actual no ha terminado, lo que podría causar errores de flujo
    //y confusión en la experiencia del usuario*/
    private bool _secuenciaEnProceso = false;
    private string _escenaActividadActual;


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
        Debug.Log("[GameFlowManager] Enviando al participante a Selección de Actividades...");

        if (waitScreenCanvas != null)
            waitScreenCanvas.SetActive(false);

        if (gameMasterUI != null)
            gameMasterUI.MostrarSeleccionActividades();

        StartCoroutine(RutinaIrASeleccionActividades());
    }

    //Rutina de selección de actividades
    private IEnumerator RutinaIrASeleccionActividades()
    {
        yield return DescargarEscenaSiEstaCargada(escenaEsperaNombre);
        yield return CargarEscenaAditiva(escenaSeleccionActividadesNombre);
    }

    //Llamado por SeleccionActividadesUI al presionar el botón de comenzar
    public void IniciarSecuenciaActividades(List<ActivityType> seleccionadas)
    {
        if (_secuenciaEnProceso)
        {
            Debug.Log("[GameFlowManager] Ya hay una secuencia en curso, se ignora el clic repetido.");
            return;
        }
        _secuenciaEnProceso = true;

        _colaActividades.Clear();
        ActivityType[] ordenFijo = { ActivityType.Siembra, ActivityType.Deshierbe, ActivityType.Corte };
        foreach (var actividad in ordenFijo)
        {
            if (seleccionadas.Contains(actividad))
                _colaActividades.Enqueue(actividad);
        }

        StartCoroutine(RutinaIniciarPrimeraActividad());
    }

    //Descarga de la escena de selección de actividades y carga de la primera actividad en la cola.
    private IEnumerator RutinaIniciarPrimeraActividad()
    {
        yield return DescargarEscenaSiEstaCargada(escenaSeleccionActividadesNombre);
        yield return AvanzarSiguienteActividad();
        _secuenciaEnProceso = false;
    }


    //PENDIENTE -> CONTECTAR A CADA ESCENA DE CADA ACTIVIDAD.
    //Al final de AvanzarSiguienteActividad, después de cargar la escena con éxito:
    public IEnumerator AvanzarSiguienteActividad()
    {
        if (_escenaActividadActual != null)
            yield return DescargarEscenaSiEstaCargada(_escenaActividadActual);

        if (_colaActividades.Count == 0)
        {
            Debug.Log("[GameFlowManager] Todas las actividades seleccionadas fueron completadas.");
            yield break;
        }

        ActivityType siguiente = _colaActividades.Dequeue();
        _escenaActividadActual = NombreEscenaParaActividad(siguiente);
        yield return CargarEscenaAditiva(_escenaActividadActual);

        // Avisa al Game Master que la actividad cambió, mostrando el panel de control (Tutorial/Lanzar)
        if (gameMasterUI != null)
            gameMasterUI.MostrarControlActividad(); 
    }

    //Devuelve el nombre de la escena correspondiente a la actividad seleccionada.
    private string NombreEscenaParaActividad(ActivityType tipo)
    {
        return tipo switch
        {
            ActivityType.Siembra => escenaMenuSiembraNombre,
            ActivityType.Deshierbe => escenaMenuDeshierbeNombre,
            ActivityType.Corte => escenaMenuCorteNombre,
            _ => null
        };
    }

    //Descarga la escena si está cargada, evitando errores al intentar descargar una escena que no está activa.
    private IEnumerator DescargarEscenaSiEstaCargada(string nombreEscena)
    {
        if (string.IsNullOrEmpty(nombreEscena)) yield break;

        Scene escena = SceneManager.GetSceneByName(nombreEscena);

        Debug.Log($"Intentando descargar '{nombreEscena}' — IsValid: {escena.IsValid()}, isLoaded: {escena.isLoaded}");

        if (escena.IsValid() && escena.isLoaded)
        {
            AsyncOperation op = SceneManager.UnloadSceneAsync(escena);
            if (op != null)
            {
                while (!op.isDone) yield return null;
            }
        }
    }
    //Carga la escena de forma aditiva y la establece como activa, asegurando que la transición entre escenas sea fluida.
    private IEnumerator CargarEscenaAditiva(string nombreEscena)
    {
        Scene escena = SceneManager.GetSceneByName(nombreEscena);
        if (!escena.isLoaded)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(nombreEscena, LoadSceneMode.Additive);
            while (!op.isDone) yield return null;
        }

        Scene nuevaEscena = SceneManager.GetSceneByName(nombreEscena);
        if (nuevaEscena.isLoaded)
            SceneManager.SetActiveScene(nuevaEscena);
    }

    //Cierra la sesión del usuario activo, reinicia el estado de la simulación y muestra la pantalla de espera.
    public void CerrarSesion()
    {
        usuarioActivo = null;
        simulacionEnCurso = false;

        if (waitScreenCanvas != null)
            waitScreenCanvas.SetActive(true);
    }
}