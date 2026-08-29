using UnityEngine;
using TMPro;

public class AdminGateUI : MonoBehaviour
{
    [Header("UI Elements - Credenciales")]
    [SerializeField] private TMP_InputField nombreAdminInput;   // Campo de texto para el Usuario/Nombre
    [SerializeField] private TMP_InputField passwordInput;      // Campo de texto para la Contraseña
    [SerializeField] private TMP_Text mensajeError;

    [Header("Contenedores de Pantalla")]
    [SerializeField] private GameObject elementosLogin;        // Objeto que agrupa las entradas y el botón "Entrar"
    [SerializeField] private GameObject panelEnConstruccion;    // Objeto que dice "En Construcción"

    private void OnEnable()
    {
        // Limpiar inputs y mensajes al activar el panel
        if (passwordInput != null) passwordInput.text = "";
        if (mensajeError != null) mensajeError.text = "";

        // Asegurar la vista limpia de login
        if (elementosLogin != null) elementosLogin.SetActive(true);
        if (panelEnConstruccion != null) panelEnConstruccion.SetActive(false);
    }

    /// <summary>
    /// Permite pre-llenar el nombre si se viene desde otra pantalla (como IniciarSesionUI).
    /// </summary>
    public void PrepararLoginAdmin(string nombreAdmin)
    {
        if (nombreAdminInput != null)
        {
            nombreAdminInput.text = nombreAdmin;
        }
    }

    public void OnValidarPasswordPresionado()
    {
        // Obtener y limpiar los textos ingresados
        string nombre = nombreAdminInput != null ? nombreAdminInput.text.Trim() : "";
        string password = passwordInput != null ? passwordInput.text : "";

        // 1. Validar que no estén vacíos
        if (string.IsNullOrWhiteSpace(nombre))
        {
            mensajeError.color = Color.red;
            mensajeError.text = "Ingresa el nombre de administrador.";
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            mensajeError.color = Color.red;
            mensajeError.text = "Ingresa la contraseña.";
            return;
        }

        // 2. Verificar que el usuario exista
        UserProfile adminUser = UserDatabase.ObtenerUsuario(nombre);
        if (adminUser == null)
        {
            mensajeError.color = Color.red;
            mensajeError.text = "El usuario no existe.";
            return;
        }

        // 3. Validar si realmente tiene rol de administrador
        if (!adminUser.EsAdministrador)
        {
            mensajeError.color = Color.red;
            mensajeError.text = "El usuario ingresado no es un Administrador.";
            return;
        }

        // 4. Validar contraseña contra el Hash guardado
        if (UserDatabase.ValidarLoginAdmin(nombre, password))
        {
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.IniciarSesionConUsuario(adminUser);
            }

            GetComponentInParent<GameMasterUI>()?.MostrarPanelAdmin();

            if (mensajeError != null) mensajeError.text = "";
        }

        else
        {
            mensajeError.color = Color.red;
            mensajeError.text = "Contraseña incorrecta.";
        }
    }

    public void OnVolverPresionado()
    {
        // Regresar al menú de inicio principal de la PC
        GetComponentInParent<GameMasterUI>()?.MostrarInicio();
    }
}