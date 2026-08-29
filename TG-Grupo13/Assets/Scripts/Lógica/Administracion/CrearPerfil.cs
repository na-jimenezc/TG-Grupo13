using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CrearPerfilUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField nombreInput;
    [SerializeField] private TMP_InputField correoInput;
    [SerializeField] private TMP_Dropdown rolDropdown; // 0 = Normal, 1 = Administrador
    [SerializeField] private GameObject passwordInputObject;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_InputField edadInput;
    [SerializeField] private TMP_Text mensajeError;

    void OnEnable()
    {
        // Cada vez que se muestra esta pantalla, empieza limpia
        nombreInput.text = "";
        correoInput.text = "";
        passwordInput.text = "";
        rolDropdown.value = 0;
        mensajeError.text = "";
        edadInput.text = "";
        ActualizarVisibilidadPassword();
    }

    void Start()
    {
        rolDropdown.onValueChanged.AddListener(_ => ActualizarVisibilidadPassword());
    }

    void ActualizarVisibilidadPassword()
    {
        bool esAdmin = rolDropdown.value == 1;
        passwordInputObject.SetActive(esAdmin);
    }

    public void OnCrearPerfilPresionado()
    {
        string nombre = nombreInput.text.Trim();

        if (string.IsNullOrWhiteSpace(nombre))
        {
            mensajeError.text = "El nombre es obligatorio";
            return;
        }

        if (UserDatabase.ExisteUsuario(nombre))
        {
            mensajeError.text = "Ya existe un usuario con ese nombre";
            return;
        }

        bool esAdmin = rolDropdown.value == 1;

        if (esAdmin && string.IsNullOrWhiteSpace(passwordInput.text))
        {
            mensajeError.text = "Los administradores necesitan contraseña";
            return;
        }

        var nuevoUsuario = UserDatabase.CrearUsuario(
            nombre,
            correoInput.text.Trim(),
            esAdmin,
            esAdmin ? passwordInput.text : null,
            edadInput.text.Trim()
        );

        GameFlowManager.Instance.IniciarSesionConUsuario(nuevoUsuario);
    }

    public void OnVolverPresionado()
    {
        GetComponentInParent<GameMasterUI>().MostrarInicio();
    }
}