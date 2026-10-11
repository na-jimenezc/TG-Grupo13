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

    protected virtual void Start() => MostrarPaso(0);

    protected void MostrarPaso(int index)
    {
        _pasoActual = index;
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
    }

    private void InstanciarPoses(GameObject[] prefabs)
    {
        if (prefabs == null) return;
        for (int i = 0; i < prefabs.Length && i < puntosAparicionMuñeco.Length; i++)
        {
            if (prefabs[i] == null) continue;
            _posesInstanciadas.Add(Instantiate(prefabs[i], puntosAparicionMuñeco[i].position, puntosAparicionMuñeco[i].rotation));
        }
    }

    // Los controles se crean como hijos del punto para que hereden su escala y sigan al panel si este se mueve
    private void InstanciarControles(GameObject[] prefabs)
    {
        if (prefabs == null || puntosControles == null) return;
        for (int i = 0; i < prefabs.Length && i < puntosControles.Length; i++)
        {
            if (prefabs[i] == null) continue;
            _posesInstanciadas.Add(Instantiate(prefabs[i], puntosControles[i], false));
        }
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