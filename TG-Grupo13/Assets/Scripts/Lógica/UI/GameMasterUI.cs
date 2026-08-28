using UnityEngine;

public class GameMasterUI : MonoBehaviour
{
    [SerializeField] private GameObject panelInicio;
    [SerializeField] private GameObject panelCrearPerfil;
    [SerializeField] private GameObject panelIniciarSesion;
    [SerializeField] private GameObject panelAdminGate;

    private void OcultarTodos()
    {
        if (panelInicio) panelInicio.SetActive(false);
        if (panelCrearPerfil) panelCrearPerfil.SetActive(false);
        if (panelIniciarSesion) panelIniciarSesion.SetActive(false);
        if (panelAdminGate) panelAdminGate.SetActive(false);
    }

    public void MostrarInicio()
    {
        OcultarTodos();
        if (panelInicio) panelInicio.SetActive(true);
    }

    public void MostrarCrearPerfil()
    {
        OcultarTodos();
        if (panelCrearPerfil) panelCrearPerfil.SetActive(true);
    }

    public void MostrarIniciarSesion()
    {
        OcultarTodos();
        if (panelIniciarSesion) panelIniciarSesion.SetActive(true);
    }

    public void MostrarAdminGate()
    {
        OcultarTodos();
        if (panelAdminGate) panelAdminGate.SetActive(true);
    }

    public void OnSalirPresionado()
    {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
                Application.Quit();
    #endif
    }
}

