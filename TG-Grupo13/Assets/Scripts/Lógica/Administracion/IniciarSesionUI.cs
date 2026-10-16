using UnityEngine;
using TMPro;

/*Gestiona el formulario de Inicio de Sesión, si el rol es normal lanza la simulación de una, de lo contrari
 * lanza la pantalla de validación de contraseña para poder acceder al panel de administración.
 */
public class IniciarSesionUI : MonoBehaviour
{
    //Campo de texto para el nombre de usuario y mensaje de error
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField nombreInput;
    [SerializeField] private TMP_Text mensajeError;

    //Evento de Unity que se llama al presionar el botón de ingresar
    private void OnEnable()
    {
        if (nombreInput != null) nombreInput.text = "";
        if (mensajeError != null)
        {
            mensajeError.text = "";
            mensajeError.color = Color.red;
        }
    }

    //Evento de Unity que se llama al presionar el botón de ingresar
    public void OnIngresarPresionado()
    {
        string nombre = nombreInput.text.Trim();

        //Control de errores: si el campo está vacío, se muestra un mensaje de error
        mensajeError.color = Color.red;
        if (string.IsNullOrWhiteSpace(nombre))
        {
            mensajeError.text = "Ingresa un nombre de usuario.";
            return;
        }

        //Se obtiene el perfil del usuario desde la base de datos
        UserProfile usuario = UserDatabase.ObtenerUsuario(nombre);

        //Revisión si el usuario existe, si no, se muestra un mensaje de error
        if (usuario == null)
        {
            mensajeError.text = "El usuario no existe.";
            return;
        }

        //Si el usuario es administrador, se prepara la pantalla de validación de contraseña
        if (usuario.EsAdministrador)
        {
            AdminGateUI adminGate = FindFirstObjectByType<AdminGateUI>(FindObjectsInactive.Include);
            if (adminGate != null)
            {
                adminGate.PrepararLoginAdmin(usuario.Nombre);
                GetComponentInParent<GameMasterUI>().MostrarAdminGate();
            }
        }

        //Si el usuario es normal, se inicia la sesión y se muestra un mensaje de éxito
        else
        {
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.IniciarSesionConUsuario(usuario);
                mensajeError.color = Color.green;
                mensajeError.text = "Iniciando simulación en visor...";;
            }
        }
    }

    //Evento de Unity que se llama al presionar el botón de volver
    public void OnVolverPresionado()
    {
        GetComponentInParent<GameMasterUI>().MostrarInicio();
    }
}