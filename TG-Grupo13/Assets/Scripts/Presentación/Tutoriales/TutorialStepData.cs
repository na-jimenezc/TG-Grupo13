using UnityEngine;

/* Corresponde a los datos de un paso del tutorial: qué se muestra, qué modelo(s) de pose se instancian,
// qué icono(s) de control se señalan y bajo qué condición se avanza al siguiente paso*/
[CreateAssetMenu(fileName = "TutorialStep", menuName = "Tutorial/Paso")]
public class TutorialStepData : ScriptableObject
{
    [TextArea] public string instruccion;

    [Tooltip("Prefabs de Maniquí")]
    public GameObject[] prefabsPose;

    [Tooltip("Íconos de Interacción")]
    public Sprite[] iconosBoton;

    [Tooltip("Modelos 3D de mano + control (prefabs con IlustracionControl3D) que muestran qué botón usar")]
    public GameObject[] prefabsControles;

    [Tooltip("Condición para Avanzar")]
    public TipoEsperaTutorial tipoEspera;

    [Tooltip("Solo usado si se Usa el Tiempo")]
    public float duracionMinima = 3f;
}