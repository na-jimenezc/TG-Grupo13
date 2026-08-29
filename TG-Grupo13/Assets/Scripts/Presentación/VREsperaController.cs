using UnityEngine;
using TMPro;

/*Controla la interfaz visual que observa le usuario en las gafas de realida virtual 
 * mientras el GameMaster registra o inicia la sesión al usuario*/
public class VREsperaController : MonoBehaviour
{
    //Panel de espera que tiene el texto flotante para que espere el usuario
    [SerializeField] private GameObject panelEsperaVR;

    //Componente donde se le muestra al usuario el mensaje de espera
    [SerializeField] private TMP_Text mensajeVRText;

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
    }

    //Oculta el panel de espera y el mensaje que se le estaba mostrando al usuario una vez se detecta que inicio la simulación
    public void OcultarPantallaEspera()
    {
        if (panelEsperaVR != null) panelEsperaVR.SetActive(false);
    }
}