using UnityEngine;
using TMPro;

/*Enlace entre la validación de seguirdad de administrador, obtene los usuarios con los permisos elevados
 * y les pide las contraseña antes de poder abrir el panel de administración.*/
public class AdminGateUI : MonoBehaviour
{
    [Header("UI Elements - Credenciales")]
    [SerializeField] private TMP_InputField nombreAdminInput;   //Campo de texto para el Usuario/Nombre
    [SerializeField] private TMP_InputField passwordInput;      //Campo de texto para la Contraseña
    [SerializeField] private TMP_Text mensajeError;

    [Header("Contenedores de Pantalla")]
    [SerializeField] private GameObject elementosLogin;        //Objeto que agrupa las entradas y el botón "Entrar"
    [SerializeField] private GameObject panelEnConstruccion;    //Objeto que dice "En Construcción"

    //Etiquetas de los campos, ya que el placeholder por defecto no indica qué dato se pide.
    private void Awake()
    {
        EtiquetaCampoUI.Etiquetar(nombreAdminInput, "Nombre del administrador", "Ej: Ana Gómez");
        EtiquetaCampoUI.Etiquetar(passwordInput, "Contraseña", "Contraseña");
    }

    //Se limpia por default al activar el panel, para evitar que queden datos de intentos anteriores.
    private void OnEnable()
    {
        //Limpiar inputs y mensajes al activar el panel
        if (passwordInput != null) passwordInput.text = "";
        if (mensajeError != null) mensajeError.text = "";

        //Asegurar la vista limpia de login
        if (elementosLogin != null) elementosLogin.SetActive(true);
        if (panelEnConstruccion != null) panelEnConstruccion.SetActive(false);
    }


    //Permite pre-llenar el nombre si se viene desde otra pantalla (como IniciarSesionUI).
    public void PrepararLoginAdmin(string nombreAdmin)
    {
        if (nombreAdminInput != null)
        {
            nombreAdminInput.text = nombreAdmin;
        }
    }

    //Se ejecuta al presionar el botón "Entrar" en la UI de validación de administrador., simpplemente valida que el usuario exista, que sea administrador y que la contraseña sea correcta.
    public void OnValidarPasswordPresionado()
    {
        //Obtener y limpiar los textos ingresados
        string nombre = nombreAdminInput != null ? nombreAdminInput.text.Trim() : "";
        string password = passwordInput != null ? passwordInput.text : "";

        //Se valida que los campos no estén vacios e ingresa los mensajes de error correspondientes.
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

        //Se verifica que el usuario si exista
        UserProfile adminUser = UserDatabase.ObtenerUsuario(nombre);
        if (adminUser == null)
        {
            mensajeError.color = Color.red;
            mensajeError.text = "El usuario no existe.";
            return;
        }

        //Se valida si el usuario es realmente un administrador
        if (!adminUser.EsAdministrador)
        {
            mensajeError.color = Color.red;
            mensajeError.text = "El usuario ingresado no es un Administrador.";
            return;
        }

        //Se valida la contraseña vs el hash guardado en el .json
        if (UserDatabase.ValidarLoginAdmin(nombre, password))
        {
            //Habilita por unos minutos la creación de otros administradores desde Crear Perfil
            AutorizacionAdmin.Otorgar();

            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.IniciarSesionConUsuario(adminUser);
            }

            GetComponentInParent<GameMasterUI>()?.MostrarPanelAdmin();

            if (mensajeError != null) mensajeError.text = "";
        }

        //Si la contraseña es incorrecta, se muestra un mensaje de error.
        else
        {
            mensajeError.color = Color.red;
            mensajeError.text = "Contraseña incorrecta.";
        }
    }

    public void OnVolverPresionado()
    {
        //Regresar al menú de inicio principal de la PC
        GetComponentInParent<GameMasterUI>()?.MostrarInicio();
    }
}