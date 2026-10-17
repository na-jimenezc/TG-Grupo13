using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public abstract class TutorialManagerBase : MonoBehaviour
{
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
    public string InstruccionActual => pasos != null && _pasoActual < pasos.Length && pasos[_pasoActual] != null ? pasos[_pasoActual].instruccion : "";
    //Contador del paso actual (p. ej. "3/5" bulbos sembrados), vacío si el paso no lleva contador.
    public string ContadorActual { get; private set; } = "";

    protected virtual void Start()
    {
        Actual = this;
        MostrarPaso(0);
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
        InstanciarPoses(paso.prefabsPose);
        InstanciarControles(paso.prefabsControles);
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

    // El maniquí hereda la escala del punto de aparición, para igualarlo a escenas cuyo entorno está escalado
    private void InstanciarPoses(GameObject[] prefabs)
    {
        if (prefabs == null) return;
        for (int i = 0; i < prefabs.Length && i < puntosAparicionMuñeco.Length; i++)
        {
            if (prefabs[i] == null) continue;
            var punto = puntosAparicionMuñeco[i];
            var pose = Instantiate(prefabs[i], punto.position, punto.rotation);
            pose.transform.localScale = Vector3.Scale(prefabs[i].transform.localScale, punto.lossyScale);
            _posesInstanciadas.Add(pose);
        }
    }

    // Los controles se crean como hijos del punto para que hereden su escala y sigan al panel si este se mueve
    private void InstanciarControles(GameObject[] prefabs)
    {
        if (prefabs == null || puntosControles == null) return;
        for (int i = 0; i < prefabs.Length && i < puntosControles.Length; i++)
        {
            if (prefabs[i] == null || puntosControles[i] == null) continue;
            var control = Instantiate(prefabs[i], puntosControles[i], false);
            CentrarEnPunto(control.transform, puntosControles[i]);
            _posesInstanciadas.Add(control);
        }
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
        textoInstruccion.text = paso.instruccion;
        for (int i = 0; i < iconosBotonUI.Length; i++)
        {
            bool hay = paso.iconosBoton != null && i < paso.iconosBoton.Length;
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
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.LanzarActividadDirecta(tipoActividad);
        }
    }
}