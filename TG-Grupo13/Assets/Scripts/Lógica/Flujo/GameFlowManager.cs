using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.Rendering.DebugUI;

//Momento de la sesión en que se encuentra el usuario, para que el Game Master lo vea en su panel.
public enum FaseSesion { SinSesion, SeleccionActividades, MenuActividad, Tutorial, Actividad, Resultados }

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

    [Header("Nombres de Escenas de Tutorial")]
    [SerializeField] private string escenaTutorialSiembraNombre = "TutorialSiembra";
    [SerializeField] private string escenaTutorialDeshierbeNombre = "TutorialDeshierbe";
    [SerializeField] private string escenaTutorialCorteNombre = "TutorialCorte";

    [Header("Nombres de Escenas de Actividad")]
    [SerializeField] private string escenaActividadSiembraNombre = "ActividadSiembra";
    [SerializeField] private string escenaActividadDeshierbeNombre = "ActividadDeshierbe";
    [SerializeField] private string escenaActividadCorteNombre = "ActividadCorte";

    [Header("Escenas de Transición y Cierre")]
    [SerializeField] private string escenaContinuarNombre = "Continuar";
    [SerializeField] private string escenaResultadosNombre = "PantallaResultados";
    [SerializeField] private string escenaSalirNombre = "SalirSimulacion";
    [SerializeField] private float duracionPantallaSalida = 4f;

    //Cola para las actividades
    private Queue<ActivityType> _colaActividades = new Queue<ActivityType>();
    /*Bandera para indicar si una secuencia de actividades está en proceso, evitando que se inicie otra mientras la actual no ha terminado
    //Esta es la solución para evitar que se inicie otra secuencia mientras la actual no ha terminado, lo que podría causar errores de flujo
    //y confusión en la experiencia del usuario*/
    private bool _secuenciaEnProceso = false;
    private string _escenaActividadActual;

    //Actividad actualmente activa en la cola y escena de Tutorial/Actividad que está mostrándose sobre su Menú.
    private ActivityType? _actividadActual;
    private string _escenaContenidoActual;

    //Bandera para evitar que se ejecute la rutina de salida dos veces al mismo tiempo.
    private bool _saliendo = false;

    //Actividad de la cola que está siendo mostrada actualmente (Siembra, Deshierbe o Corte), null si no hay ninguna en curso.
    public ActivityType? ActividadActual => _actividadActual;

    //Todas las actividades elegidas para la sesión (en orden) y la posición de la actual, para mostrar el recorrido completo.
    private readonly List<ActivityType> _actividadesSesion = new List<ActivityType>();
    public IReadOnlyList<ActivityType> ActividadesSesion => _actividadesSesion;
    public int IndiceActividadActual { get; private set; } = -1;

    //Cada actividad se hace antes (pre) y después (post) de su tutorial, para comparar la postura.
    //true cuando ya se mostró el tutorial de la actividad actual: la siguiente vez que se lance es la "post".
    public bool TutorialVistoEnActividadActual { get; private set; }

    public FaseSesion Fase { get; private set; } = FaseSesion.SinSesion;
    //Se dispara cada vez que cambia la fase o la actividad actual (lo escucha el panel del Game Master).
    public event Action OnEstadoCambiado;

    //Se dispara cuando el Game Master termina la sesión antes de tiempo (p. ej. por mareo del participante),
    //ANTES de limpiar el estado: usuarioActivo, ActividadesSesion e IndiceActividadActual siguen disponibles
    //para guardar la capacitación marcada como interrumpida.
    public event Action OnSesionInterrumpida;

    private void CambiarFase(FaseSesion fase)
    {
        Fase = fase;
        OnEstadoCambiado?.Invoke();
    }


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
        if (Instance != this || simulacionEnCurso) return;

        if (waitScreenCanvas != null)
            waitScreenCanvas.SetActive(true);

        //Core no trae el entorno de espera: se carga de forma aditiva para que sea lo primero que ve el usuario
        //(antes solo aparecía si la escena estaba abierta a mano en el editor junto a Core).
        StartCoroutine(CargarEscenaAditiva(escenaEsperaNombre));
    }

    //Inicia la sesión con el usuario proporcionado y lanza la simulación VR si no es administrador.
    public void IniciarSesionConUsuario(UserProfile usuario)
    {
        usuarioActivo = usuario;
        Debug.Log($"[GameFlowManager] Sesión iniciada: {usuario.Nombre} | Admin: {usuario.EsAdministrador}");

        //Acá se pasa de la escena de espera a la escena de selección de actividades si el usuario no es administrador.
        if (!usuario.EsAdministrador)
        {
            //Al entrar un participante se cierra la autorización para crear administradores
            AutorizacionAdmin.Revocar();
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

        CambiarFase(FaseSesion.SeleccionActividades);
        StartCoroutine(RutinaIrASeleccionActividades());
    }

    //Rutina de selección de actividades
    private IEnumerator RutinaIrASeleccionActividades()
    {
        //Si el Game Master inicia otra sesión mientras el visor aún muestra la despedida, se espera a que termine.
        while (_saliendo) yield return null;

        yield return CargarEscenaAditiva(escenaSeleccionActividadesNombre);
        yield return DescargarEscenaSiEstaCargada(escenaEsperaNombre);
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
        _actividadesSesion.Clear();
        IndiceActividadActual = -1;
        ActivityType[] ordenFijo = { ActivityType.Siembra, ActivityType.Deshierbe, ActivityType.Corte };
        foreach (var actividad in ordenFijo)
        {
            if (seleccionadas.Contains(actividad))
            {
                _colaActividades.Enqueue(actividad);
                _actividadesSesion.Add(actividad);
            }
        }

        StartCoroutine(RutinaIniciarPrimeraActividad());
    }

    //Descarga de la escena de selección de actividades y carga de la primera actividad en la cola.
    private IEnumerator RutinaIniciarPrimeraActividad()
    {
        yield return AvanzarSiguienteActividad();
        yield return DescargarEscenaSiEstaCargada(escenaSeleccionActividadesNombre);
        _secuenciaEnProceso = false;
    }


    //Descarga el Menú de la actividad anterior (si había) y carga el Menú de la siguiente actividad en la cola.
    //Si ya no quedan actividades, se muestra la Pantalla de Resultados de la sesión.
    public IEnumerator AvanzarSiguienteActividad()
    {
        string escenaAnterior = _escenaActividadActual;

        if (_colaActividades.Count == 0)
        {
            Debug.Log("[GameFlowManager] Todas las actividades seleccionadas fueron completadas.");
            _actividadActual = null;
            IndiceActividadActual = _actividadesSesion.Count;
            CambiarFase(FaseSesion.Resultados);

            yield return CargarEscenaAditiva(escenaResultadosNombre);
            if (escenaAnterior != null)
                yield return DescargarEscenaSiEstaCargada(escenaAnterior);

            if (gameMasterUI != null)
                gameMasterUI.MostrarPanelResultados();
            yield break;
        }

        ActivityType siguiente = _colaActividades.Dequeue();
        _actividadActual = siguiente;
        _escenaActividadActual = NombreEscenaParaActividad(siguiente);
        IndiceActividadActual++;
        TutorialVistoEnActividadActual = false;
        CambiarFase(FaseSesion.MenuActividad);

        yield return CargarEscenaAditiva(_escenaActividadActual);
        if (escenaAnterior != null)
            yield return DescargarEscenaSiEstaCargada(escenaAnterior);

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

    //Devuelve el nombre de la escena de Tutorial correspondiente a la actividad indicada.
    private string NombreEscenaTutorial(ActivityType tipo)
    {
        return tipo switch
        {
            ActivityType.Siembra => escenaTutorialSiembraNombre,
            ActivityType.Deshierbe => escenaTutorialDeshierbeNombre,
            ActivityType.Corte => escenaTutorialCorteNombre,
            _ => null
        };
    }

    //Devuelve el nombre de la escena de la Actividad (práctica real) correspondiente a la actividad indicada.
    private string NombreEscenaActividad(ActivityType tipo)
    {
        return tipo switch
        {
            ActivityType.Siembra => escenaActividadSiembraNombre,
            ActivityType.Deshierbe => escenaActividadDeshierbeNombre,
            ActivityType.Corte => escenaActividadCorteNombre,
            _ => null
        };
    }

    //Llamado desde el panel de control del Game Master ("Ir al tutorial") al terminar la actividad pre.
    //Descarga la actividad pre y muestra el Tutorial; al terminarlo, este lanza la actividad post.
    public void MostrarTutorialActividad(ActivityType tipo)
    {
        //Se marca antes del cambio de fase, para que quienes escuchan OnEstadoCambiado ya vean que la próxima actividad es la post.
        TutorialVistoEnActividadActual = true;
        StartCoroutine(RutinaMostrarContenidoActividad(NombreEscenaTutorial(tipo), FaseSesion.Tutorial));
    }

    //Llamado por el Menú de cada actividad al presionar "Lanzar" en el panel de control del Game Master.
    //Descarga el Menú de la actividad (o su Tutorial, si estaba activo) y carga la Actividad real.
    public void LanzarActividadDirecta(ActivityType tipo)
    {
        StartCoroutine(RutinaMostrarContenidoActividad(NombreEscenaActividad(tipo), FaseSesion.Actividad));
    }

    //Descarga el Menú de la actividad (la primera vez) y el Tutorial/Actividad mostrado anteriormente (si aplica),
    //y carga la escena de contenido solicitada (Tutorial o Actividad) de forma aditiva.
    private IEnumerator RutinaMostrarContenidoActividad(string nombreEscena, FaseSesion fase)
    {
        if (string.IsNullOrEmpty(nombreEscena)) yield break;

        //Si ya se está mostrando esa misma escena (p. ej. se presionó "Tutorial" dos veces), no se hace nada;
        //de lo contrario se descargaría a sí misma al final de esta rutina y el visor quedaría vacío.
        if (nombreEscena == _escenaContenidoActual) yield break;

        //La fase se cambia apenas se pide (y no al terminar de cargar), para que el panel del Game Master deje de
        //mostrar los pasos del tutorial en cuanto se presiona "Comenzar" y pase a mostrar la actividad y su progreso.
        CambiarFase(fase);

        yield return CargarEscenaAditiva(nombreEscena);

        if (_escenaActividadActual != null)
            yield return DescargarEscenaSiEstaCargada(_escenaActividadActual);

        if (_escenaContenidoActual != null)
            yield return DescargarEscenaSiEstaCargada(_escenaContenidoActual);

        _escenaContenidoActual = nombreEscena;
    }

    //Llamado desde el botón "Continuar" del panel de control del Game Master para pasar a la siguiente
    //actividad de la cola (o a la Pantalla de Resultados si ya no quedan actividades).
    public void ContinuarConSiguienteActividad()
    {
        StartCoroutine(RutinaContinuarConSiguienteActividad());
    }

    private IEnumerator RutinaContinuarConSiguienteActividad()
    {
        if (!string.IsNullOrEmpty(escenaContinuarNombre))
            yield return CargarEscenaAditiva(escenaContinuarNombre);

        //Primero se carga la siguiente escena (Menú o Resultados) y después se descarga la actividad: la escena
        //Continuar no tiene piso, así que si la actividad se descargaba antes el jugador caía mientras cargaba la siguiente.
        yield return AvanzarSiguienteActividad();

        if (_escenaContenidoActual != null)
        {
            yield return DescargarEscenaSiEstaCargada(_escenaContenidoActual);
            _escenaContenidoActual = null;
        }

        if (!string.IsNullOrEmpty(escenaContinuarNombre))
            yield return DescargarEscenaSiEstaCargada(escenaContinuarNombre);
    }


    //Llamado al finalizar la consulta de resultados, para despedir al usuario y reiniciar el estado de la simulación.
    public void SalirDeSimulacion()
    {
        //Evita que un segundo clic (desde el visor o desde el panel del Game Master) lance otra salida en paralelo.
        if (_saliendo) return;
        _saliendo = true;
        StartCoroutine(RutinaSalirDeSimulacion());
    }

    private IEnumerator RutinaSalirDeSimulacion()
    {
        yield return CargarEscenaAditiva(escenaSalirNombre);

        //Se vuelve a cargar el entorno de la Pantalla de Espera ANTES de descargar Resultados, para que el visor
        //nunca quede en una escena vacía (sin suelo ni paredes de referencia), lo que desorienta y marea al usuario.
        //Se carga de último para que quede como escena activa y no cambie la iluminación al descargar las demás.
        yield return CargarEscenaAditiva(escenaEsperaNombre);
        yield return DescargarEscenaSiEstaCargada(escenaResultadosNombre);

        //La sesión se cierra y el Game Master vuelve al inicio de una vez, sin esperar la despedida del visor;
        //antes el panel de Resultados se quedaba congelado esos segundos y parecía que el botón no hacía nada.
        CerrarSesion();

        if (gameMasterUI != null)
            gameMasterUI.MostrarInicio();

        //Deja la pantalla de despedida visible unos segundos en el visor antes de quitarla.
        yield return new WaitForSeconds(duracionPantallaSalida);

        yield return DescargarEscenaSiEstaCargada(escenaSalirNombre);
        _saliendo = false;
    }

    //Llamado desde el botón "Terminar sesión" del panel de control del Game Master. Corta la sesión en cualquier
    //momento (menú, tutorial o actividad), muestra la despedida en el visor y vuelve al inicio.
    public void TerminarSesionAnticipada()
    {
        if (_saliendo || !simulacionEnCurso) return;
        _saliendo = true;

        //Se detienen las transiciones en curso (p. ej. si se estaba cargando la siguiente actividad)
        StopAllCoroutines();

        OnSesionInterrumpida?.Invoke();
        StartCoroutine(RutinaTerminarSesionAnticipada());
    }

    private IEnumerator RutinaTerminarSesionAnticipada()
    {
        yield return CargarEscenaAditiva(escenaSalirNombre);

        //Igual que en la salida normal: el entorno de espera se carga antes de descargar lo demás,
        //para que el visor nunca quede en una escena vacía.
        yield return CargarEscenaAditiva(escenaEsperaNombre);

        foreach (string escena in EscenasDeSesion())
            yield return DescargarEscenaSiEstaCargada(escena);

        CerrarSesion();

        if (gameMasterUI != null)
            gameMasterUI.MostrarInicio();

        yield return new WaitForSeconds(duracionPantallaSalida);

        yield return DescargarEscenaSiEstaCargada(escenaSalirNombre);
        _saliendo = false;
    }

    //Todas las escenas que pueden estar cargadas durante una sesión (se descargan solo las que lo estén)
    private IEnumerable<string> EscenasDeSesion()
    {
        return new[]
        {
            escenaSeleccionActividadesNombre,
            escenaMenuSiembraNombre, escenaMenuDeshierbeNombre, escenaMenuCorteNombre,
            escenaTutorialSiembraNombre, escenaTutorialDeshierbeNombre, escenaTutorialCorteNombre,
            escenaActividadSiembraNombre, escenaActividadDeshierbeNombre, escenaActividadCorteNombre,
            escenaContinuarNombre, escenaResultadosNombre
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

        //Se limpia el estado de la secuencia anterior para que la siguiente sesión empiece desde cero.
        _colaActividades.Clear();
        _secuenciaEnProceso = false;
        _actividadActual = null;
        _escenaActividadActual = null;
        _escenaContenidoActual = null;
        _actividadesSesion.Clear();
        IndiceActividadActual = -1;
        TutorialVistoEnActividadActual = false;
        CambiarFase(FaseSesion.SinSesion);

        if (waitScreenCanvas != null)
            waitScreenCanvas.SetActive(true);
    }
}