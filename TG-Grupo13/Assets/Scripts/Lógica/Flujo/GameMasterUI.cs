using UnityEngine;

/*Encargado de ser el controlador de la UI principal del juego para 
 * el GameMasterrr, manejando la visibilidad de los distintos paneles y la navegación entre ellos.*/
public class GameMasterUI : MonoBehaviour
{
    //Todas las pantallas que se manejan hasta el momenot
    [SerializeField] private GameObject panelInicio;
    [SerializeField] private GameObject panelCrearPerfil;
    [SerializeField] private GameObject panelIniciarSesion;
    [SerializeField] private GameObject panelAdminGate;

    [SerializeField] private GameObject panelAdmin;

    [SerializeField] private GameObject panelInfo;

    [SerializeField] private GameObject panelSeleccionActividades;

    [SerializeField] private GameObject panelControlActividad;

    [SerializeField] private GameObject panelResultados;

    //Función para ocultar los objetos de la UI, para que solo se muestre el que se desea
    private void OcultarTodos()
    {
        if (panelInicio) panelInicio.SetActive(false);
        if (panelCrearPerfil) panelCrearPerfil.SetActive(false);
        if (panelIniciarSesion) panelIniciarSesion.SetActive(false);
        if (panelAdminGate) panelAdminGate.SetActive(false);
        if (panelAdmin) panelAdmin.SetActive(false);
        if (panelInfo) panelInfo.SetActive(false);
        if (panelSeleccionActividades) panelSeleccionActividades.SetActive(false);
        if (panelControlActividad) panelControlActividad.SetActive(false);
        if (panelResultados) panelResultados.SetActive(false);
    }

    //De aquí para adelante, se decide cual se quiere mostrar y se ocultan los demás
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

    public void MostrarPanelAdmin()
    {
        OcultarTodos();
        if (panelAdmin) panelAdmin.SetActive(true);
    }

    public void MostrarPanelInfo()
    {
        OcultarTodos();
        if (panelInfo) panelInfo.SetActive(true);
    }

    public void MostrarSeleccionActividades()
    {
        OcultarTodos();
        if (panelSeleccionActividades) panelSeleccionActividades.SetActive(true);
    }

    public void MostrarControlActividad()
    {
        OcultarTodos();
        if (panelControlActividad) panelControlActividad.SetActive(true);
    }

    //Se muestra cuando todas las actividades seleccionadas fueron completadas y el usuario ve su Pantalla de Resultados.
    public void MostrarPanelResultados()
    {
        OcultarTodos();
        if (panelResultados) panelResultados.SetActive(true);
    }

    //Finaliza la sesión del usuario actual desde la Pantalla de Resultados (botón a conectar en dicho panel).
    public void OnFinalizarSesionPresionado()
    {
        GameFlowManager.Instance?.SalirDeSimulacion();
    }

    //Función para salir del juego, si se está en el editor de Unity, se detiene la ejecución, si se está en un build, se cierra la aplicación
    public void OnSalirPresionado()
    {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
                Application.Quit();
    #endif
    }
}

