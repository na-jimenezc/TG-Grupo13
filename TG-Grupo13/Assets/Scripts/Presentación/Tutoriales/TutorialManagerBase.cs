using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*Qué parte del tutorial se muestra:
 * Postural: escena de tutorial. Solo maniquíes con la postura correcta (sin modelos de controles ni íconos de botones).
 * GuiaControles: escena de actividad, en el pre. Solo los controles (texto y modelos 3D de mano + control, sin maniquíes),
 * para que la persona aprenda a usar el simulador sin que se le enseñe la postura. No aparece en el post.*/
public enum ModoTutorial { Postural, GuiaControles }

public abstract class TutorialManagerBase : MonoBehaviour
{
    [Header("Modo")]
    [Tooltip("Postural: escena de tutorial (solo maniquíes). GuiaControles: guía del pre en la escena de actividad " +
             "(solo controles; se oculta en el post). La crea el menú TG-Grupo13 > Agregar guía de controles al pre.")]
    [SerializeField] protected ModoTutorial modo = ModoTutorial.Postural;

    [Tooltip("Solo en la guía de controles: cuántas veces se pide el gesto en los pasos con contador (aprender el control, no hacer la tarea completa)")]
    [SerializeField] protected int metaEnGuia = 1;

    [Tooltip("Solo en la guía de controles: lo que se oculta en el post y al terminar la guía (panel de la guía y puntos de los controles)")]
    [SerializeField] protected GameObject[] ocultarSinGuia;

    [Header("Secuencia de pasos")]
    [SerializeField] protected TutorialStepData[] pasos;

    [Header("UI común")]
    [SerializeField] protected TMP_Text textoInstruccion;
    [SerializeField] protected Image[] iconosBotonUI;
    [SerializeField] protected TMP_Text contadorText;

    [Header("Maniquí")]
    [SerializeField] protected Transform[] puntosAparicionMuñeco;

    [Header("Controles 3D")]
    [Tooltip("Puntos (junto al panel de instrucciones) donde aparecen los modelos 3D de mano + control de cada paso")]
    [SerializeField] protected Transform[] puntosControles;

    [Header("Actividad")]
    [Tooltip("A qué actividad real pertenece este tutorial (Deshierbe, Siembra, Corte). Se usa para pedirle al GameFlowManager que lance la actividad correcta al terminar.")]
    [SerializeField] protected ActivityType tipoActividad;

    protected int _pasoActual = 0;
    private readonly List<GameObject> _posesInstanciadas = new List<GameObject>();
    private Coroutine _esperaTiempo;

    //Tutorial que se está mostrando en el visor (null si no hay ninguno), para que el panel del Game Master
    //pueda mostrar en qué paso va el usuario. Es estático porque el panel vive en Core y el tutorial en otra escena.
    public static TutorialManagerBase Actual { get; private set; }
    public static event Action OnPasoCambiado;

    public int PasoActual => _pasoActual;
    public int TotalPasos => pasos != null ? pasos.Length : 0;
    public string InstruccionActual => pasos != null && _pasoActual < pasos.Length && pasos[_pasoActual] != null ? Lateralidad.Formatear(pasos[_pasoActual].instruccion) : "";
    //Contador del paso actual (p. ej. "3/5" bulbos sembrados), vacío si el paso no lleva contador.
    public string ContadorActual { get; private set; } = "";

    protected bool EsGuia => modo == ModoTutorial.GuiaControles;

    //En la guía de controles basta con hacer el gesto unas pocas veces (metaEnGuia); en el tutorial postural se pide la meta completa.
    protected int Meta(int metaTutorial) => EsGuia ? Mathf.Max(1, metaEnGuia) : metaTutorial;

    protected virtual void Start()
    {
        if (EsGuia)
        {
            //La guía solo va en el pre: en el post (ya se vio el tutorial) la escena de actividad queda como siempre.
            //Sin GameFlowManager (escena abierta a mano para probar) se muestra.
            var flujo = GameFlowManager.Instance;
            if (flujo != null && flujo.TutorialVistoEnActividadActual)
            {
                OcultarGuia();
                return;
            }
            //No se registra como tutorial Actual: el panel del Game Master y REBA siguen viendo una actividad pre.
            BuscarObjetosEnEscena();
            MostrarPaso(0);
            return;
        }

        Actual = this;
        MostrarPaso(0);
    }

    //En la guía los objetos de práctica son los de la escena de actividad (algunos aparecen al empezar, como los bulbos):
    //cada subclase los busca si no están asignados.
    protected virtual void BuscarObjetosEnEscena() { }

    private void OcultarGuia()
    {
        LimpiarPosesAnteriores();
        if (ocultarSinGuia != null)
            foreach (var go in ocultarSinGuia) if (go != null) go.SetActive(false);
        enabled = false;
    }

    protected virtual void OnDisable()
    {
        if (Actual == this)
        {
            Actual = null;
            OnPasoCambiado?.Invoke();
        }
    }

#if UNITY_EDITOR
    // SOLO EN EL EDITOR (no llega a las gafas ni al build): permite revisar textos, poses y controles
    // de cada paso sin hacer los gestos. N = siguiente paso, B = paso anterior.
    // No avanza desde el último paso para no lanzar la actividad real.
    private void Update()
    {
        var teclado = UnityEngine.InputSystem.Keyboard.current;
        if (teclado == null) return;

        if (teclado.nKey.wasPressedThisFrame && _pasoActual < pasos.Length - 1)
            AvanzarPaso();
        else if (teclado.bKey.wasPressedThisFrame && _pasoActual > 0)
        {
            if (_esperaTiempo != null)
            {
                StopCoroutine(_esperaTiempo);
                _esperaTiempo = null;
            }
            MostrarPaso(_pasoActual - 1);
        }
    }
#endif

    protected void MostrarPaso(int index)
    {
        _pasoActual = index;
        ContadorActual = "";
        var paso = pasos[index];

        LimpiarPosesAnteriores();
        //Tutorial postural: solo maniquíes. Guía del pre: solo controles.
        if (EsGuia) InstanciarControles(paso.prefabsControles);
        else InstanciarPoses(paso.prefabsPose);
        ActualizarTextoEIconos(paso);
        DesuscribirEventosDePractica(); // cada subclase implementa qué desuscribir

        // El avance NUNCA es manual: o lo dispara el evento real del objeto de práctica
        // (SuscribirEventoDelPaso), o -solo para pasos de Tiempo- se dispara solo tras esperar.
        if (paso.tipoEspera == TipoEsperaTutorial.Tiempo)
            _esperaTiempo = StartCoroutine(AvanzarTrasEspera(paso.duracionMinima));
        else
            SuscribirEventoDelPaso(paso.tipoEspera); // cada subclase implementa el switch propio

        OnPasoCambiado?.Invoke();
    }

    protected abstract void SuscribirEventoDelPaso(TipoEsperaTutorial tipo);
    protected abstract void DesuscribirEventosDePractica();

    protected void AvanzarPaso()
    {
        // Si veníamos de un paso de Tiempo que aún no terminaba de esperar, se cancela
        // para que no dispare un segundo avance más adelante.
        if (_esperaTiempo != null)
        {
            StopCoroutine(_esperaTiempo);
            _esperaTiempo = null;
        }

        DesuscribirEventosDePractica();
        int siguiente = _pasoActual + 1;
        if (siguiente >= pasos.Length) FinalizarTutorial();
        else MostrarPaso(siguiente);
    }

    protected void ActualizarContador(int actual, int objetivo)
    {
        if (contadorText != null) contadorText.text = $"{actual}/{objetivo}";
        ContadorActual = $"{actual}/{objetivo}";
        OnPasoCambiado?.Invoke();
    }

    // El maniquí hereda la escala del punto de aparición, para igualarlo a escenas cuyo entorno está escalado.
    // Las poses están hechas para diestros: con un perfil zurdo se reflejan en X (el maniquí es simétrico).
    private void InstanciarPoses(GameObject[] prefabs)
    {
        if (prefabs == null) return;
        bool zurdo = Lateralidad.EsZurdo;
        for (int i = 0; i < prefabs.Length && i < puntosAparicionMuñeco.Length; i++)
        {
            if (prefabs[i] == null) continue;
            var punto = puntosAparicionMuñeco[i];
            var pose = Instantiate(prefabs[i], punto.position, punto.rotation);
            var escala = Vector3.Scale(prefabs[i].transform.localScale, punto.lossyScale);
            if (zurdo) escala.x = -escala.x;
            pose.transform.localScale = escala;
            AsentarEnPiso(pose.transform, punto);
            _posesInstanciadas.Add(pose);
        }
    }

    // Cada FBX de pose trae su propio desfase desde Blender y los puntos no están todos a la misma altura,
    // así que algunos maniquíes quedaban hundidos (pies, o rodillas y pantorrillas si están agachados).
    // Se sube o baja la pose para que su punto más bajo quede justo sobre el piso que hay debajo del punto.
    private static void AsentarEnPiso(Transform pose, Transform punto)
    {
        float? puntoMasBajo = PuntoMasBajo(pose);
        if (!puntoMasBajo.HasValue) return;

        float piso = punto.position.y;
        if (Physics.Raycast(punto.position + Vector3.up * 0.5f, Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore))
            piso = hit.point.y;

        pose.position += Vector3.up * (piso - puntoMasBajo.Value);
    }

    // Se recorren los vértices reales (las mallas con huesos se hornean en su pose) porque los bounds de un
    // SkinnedMeshRenderer no siguen la pose y no sirven para saber dónde quedan los pies.
    private static float? PuntoMasBajo(Transform pose)
    {
        float? minimo = null;
        var horneada = new Mesh();

        foreach (var r in pose.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (r.sharedMesh == null) continue;
            r.BakeMesh(horneada, true);
            minimo = MinimoY(horneada.vertices, r.transform.localToWorldMatrix, minimo);
        }
        foreach (var f in pose.GetComponentsInChildren<MeshFilter>())
        {
            if (f.sharedMesh == null || !f.sharedMesh.isReadable) continue;
            minimo = MinimoY(f.sharedMesh.vertices, f.transform.localToWorldMatrix, minimo);
        }

        Destroy(horneada);
        return minimo;
    }

    private static float? MinimoY(Vector3[] vertices, Matrix4x4 aMundo, float? minimo)
    {
        foreach (var v in vertices)
        {
            float y = aMundo.MultiplyPoint3x4(v).y;
            if (!minimo.HasValue || y < minimo.Value) minimo = y;
        }
        return minimo;
    }

    // Los controles se crean como hijos del punto para que hereden su escala y sigan al panel si este se mueve.
    // Los pasos están hechos para diestros (elemento 0 = control izquierdo, 1 = derecho): con un perfil zurdo
    // cada control se cambia por el de la otra mano y se muestra en el punto del otro lado.
    private void InstanciarControles(GameObject[] prefabs)
    {
        if (prefabs == null || puntosControles == null) return;
        bool zurdo = Lateralidad.EsZurdo;
        for (int i = 0; i < prefabs.Length && i < puntosControles.Length; i++)
        {
            var prefab = zurdo ? ControlManoContraria(prefabs[i]) : prefabs[i];
            var punto = puntosControles[zurdo ? puntosControles.Length - 1 - i : i];
            if (prefab == null || punto == null) continue;
            var control = Instantiate(prefab, punto, false);
            CentrarEnPunto(control.transform, punto);
            _posesInstanciadas.Add(control);
        }
    }

    private static GameObject ControlManoContraria(GameObject prefab)
    {
        if (prefab == null) return null;
        var ilustracion = prefab.GetComponentInChildren<IlustracionControl3D>(true);
        if (ilustracion != null && ilustracion.PrefabManoContraria != null) return ilustracion.PrefabManoContraria;
        Debug.LogWarning($"[TutorialManagerBase] {prefab.name} no tiene asignado su control de la mano contraria, se muestra el mismo.");
        return prefab;
    }

    // El pivote de los FBX de mano + control queda lejos de la mano (viene de la pose del cuerpo en Blender),
    // así que se corre el modelo para que su centro visual quede justo sobre el punto. Se hace antes del Start
    // de IlustracionControl3D, que toma esta posición como base para oscilar.
    private static void CentrarEnPunto(Transform control, Transform punto)
    {
        var renderers = control.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        var limites = renderers[0].bounds;
        foreach (var r in renderers) limites.Encapsulate(r.bounds);
        control.position += punto.position - limites.center;
    }

    private void LimpiarPosesAnteriores()
    {
        foreach (var p in _posesInstanciadas) if (p != null) Destroy(p);
        _posesInstanciadas.Clear();
    }

    private void ActualizarTextoEIconos(TutorialStepData paso)
    {
        string texto = !EsGuia && !string.IsNullOrWhiteSpace(paso.instruccionPostura) ? paso.instruccionPostura : paso.instruccion;
        if (textoInstruccion != null) textoInstruccion.text = Lateralidad.Formatear(texto);
        if (iconosBotonUI == null) return;
        for (int i = 0; i < iconosBotonUI.Length; i++)
        {
            if (iconosBotonUI[i] == null) continue;
            //Los íconos de botones son de controles: solo en la guía del pre.
            bool hay = EsGuia && paso.iconosBoton != null && i < paso.iconosBoton.Length;
            iconosBotonUI[i].gameObject.SetActive(hay);
            if (hay) iconosBotonUI[i].sprite = paso.iconosBoton[i];
        }
    }

    // Único caso en que el avance no depende de un objeto real: el paso de Tiempo
    // avanza solo al cumplirse la espera, sin que el usuario tenga que presionar nada.
    private IEnumerator AvanzarTrasEspera(float segundos)
    {
        yield return new WaitForSeconds(segundos);
        _esperaTiempo = null;
        AvanzarPaso();
    }

    protected virtual void FinalizarTutorial()
    {
        //La guía del pre termina y se quita; la persona sigue con la actividad pre (no se lanza nada).
        if (EsGuia)
        {
            DesuscribirEventosDePractica();
            OcultarGuia();
            OnPasoCambiado?.Invoke();
            return;
        }

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.LanzarActividadDirecta(tipoActividad);
        }
    }
}