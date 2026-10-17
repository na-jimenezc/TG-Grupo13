using System.Collections;
using UnityEngine;
using TMPro;

/*Controla la interfaz visual que observa le usuario en las gafas de realida virtual
 * mientras el GameMaster registra o inicia la sesión al usuario*/
public class VREsperaController : MonoBehaviour
{
    //Referencia 3D (mano + control) que se le muestra al usuario mientras espera, con su explicación
    [System.Serializable]
    public class ReferenciaControl
    {
        [TextArea] public string descripcion;
        public GameObject prefabControl;
    }

    //Panel de espera que tiene el texto flotante para que espere el usuario
    [SerializeField] private GameObject panelEsperaVR;

    //Componente donde se le muestra al usuario el mensaje de espera
    [SerializeField] private TMP_Text mensajeVRText;

    [Header("Referencias 3D de controles")]
    //Orden sugerido: 1. Cómo se cogen los controles, 2. Grab, 3. Grab con ambos controles, 4. Grab + Trigger
    [SerializeField] private ReferenciaControl[] referenciasControles;

    //Punto (hijo del panel de espera) donde aparece el modelo 3D de la referencia actual
    [SerializeField] private Transform puntoReferencia;

    //Texto donde se explica la referencia que se está mostrando
    [SerializeField] private TMP_Text descripcionReferenciaText;

    [SerializeField] private float segundosPorReferencia = 6f;

    private GameObject _referenciaActual;
    private Coroutine _carrusel;

    private void Start()
    {
        MostrarPantallaEspera("Por favor espera a un administrador");
    }

    private void Update()
    {
        //Se monitorea constantemente si la simulación ya inició para ocultar el panel de espera
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.simulacionEnCurso)
        {
            if (panelEsperaVR.activeSelf)
            {
                OcultarPantallaEspera();
            }
        }
    }

    //Muestra el panel de espera y el mensaje que se le quiere mostrar al usuario
    public void MostrarPantallaEspera(string mensaje)
    {
        if (panelEsperaVR != null) panelEsperaVR.SetActive(true);
        if (mensajeVRText != null) mensajeVRText.text = mensaje;

        if (_carrusel == null && referenciasControles != null && referenciasControles.Length > 0 && puntoReferencia != null)
            _carrusel = StartCoroutine(RotarReferencias());
    }

    //Oculta el panel de espera y el mensaje que se le estaba mostrando al usuario una vez se detecta que inicio la simulación
    public void OcultarPantallaEspera()
    {
        if (panelEsperaVR != null) panelEsperaVR.SetActive(false);

        if (_carrusel != null)
        {
            StopCoroutine(_carrusel);
            _carrusel = null;
        }
        if (_referenciaActual != null) Destroy(_referenciaActual);
    }

    //Muestra una a una las referencias de controles en bucle mientras el usuario espera
    private IEnumerator RotarReferencias()
    {
        int i = 0;
        while (true)
        {
            var referencia = referenciasControles[i];
            if (_referenciaActual != null) Destroy(_referenciaActual);
            if (referencia.prefabControl != null)
                _referenciaActual = Instantiate(referencia.prefabControl, puntoReferencia, false);
            if (descripcionReferenciaText != null)
                descripcionReferenciaText.text = $"{i + 1}. {referencia.descripcion}";

            yield return new WaitForSeconds(segundosPorReferencia);
            i = (i + 1) % referenciasControles.Length;
        }
    }
}
