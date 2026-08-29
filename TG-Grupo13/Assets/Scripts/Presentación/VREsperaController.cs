using UnityEngine;
using TMPro;

public class VREsperaController : MonoBehaviour
{
    [SerializeField] private GameObject panelEsperaVR;
    [SerializeField] private TMP_Text mensajeVRText;

    private void Start()
    {
        MostrarPantallaEspera("Por favor espera a un administrador");
    }

    private void Update()
    {
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.simulacionEnCurso)
        {
            if (panelEsperaVR.activeSelf)
            {
                OcultarPantallaEspera();
            }
        }
    }

    public void MostrarPantallaEspera(string mensaje)
    {
        if (panelEsperaVR != null) panelEsperaVR.SetActive(true);
        if (mensajeVRText != null) mensajeVRText.text = mensaje;
    }

    public void OcultarPantallaEspera()
    {
        if (panelEsperaVR != null) panelEsperaVR.SetActive(false);
    }
}