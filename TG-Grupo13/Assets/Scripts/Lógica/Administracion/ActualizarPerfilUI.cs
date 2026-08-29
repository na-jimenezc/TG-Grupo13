using UnityEngine;
using TMPro;

public class ActualizarPerfilUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nombreLabel;
    [SerializeField] private TMP_InputField edadInput;
    [SerializeField] private TMP_InputField correoInput;
    [SerializeField] private TMP_Dropdown rolDropdown; 
    [SerializeField] private GameObject passwordInputObject;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_Text mensajeError;

    private string _nombreOriginal;
    private string _passwordHashActual;
    private AdminListUI _listaPadre;

    public void CargarUsuario(UserProfile usuario, AdminListUI listaPadre)
    {
        _listaPadre = listaPadre;
        _nombreOriginal = usuario.Nombre;
        _passwordHashActual = usuario.PasswordHash;

        nombreLabel.text = usuario.Nombre;
        edadInput.text = usuario.Edad;
        correoInput.text = usuario.Correo;
        rolDropdown.value = usuario.EsAdministrador ? 1 : 0;
        passwordInput.text = "";
        mensajeError.text = "";
        ActualizarVisibilidadPassword();
    }

    void Start()
    {
        rolDropdown.onValueChanged.AddListener(_ => ActualizarVisibilidadPassword());
    }

    void ActualizarVisibilidadPassword()
    {
        passwordInputObject.SetActive(rolDropdown.value == 1);
    }

    public void OnActualizarPresionado()
    {
        bool esAdmin = rolDropdown.value == 1;
        bool eraAdmin = !string.IsNullOrEmpty(_passwordHashActual);
        string nuevoHash = _passwordHashActual;

        if (esAdmin)
        {
            if (!eraAdmin)
            {
                // Pasa de Normal a Administrador: contraseña obligatoria
                if (string.IsNullOrWhiteSpace(passwordInput.text))
                {
                    mensajeError.text = "Ingresa una contraseña para convertirlo en administrador";
                    return;
                }
                nuevoHash = UserDatabase.HashPassword(passwordInput.text);
            }
            else if (!string.IsNullOrWhiteSpace(passwordInput.text))
            {
                // Ya era admin, pero decidió cambiar la contraseña
                nuevoHash = UserDatabase.HashPassword(passwordInput.text);
            }
            // si ya era admin y dejó el campo vacío, se mantiene la contraseña anterior
        }
        else
        {
            // Pasa a Normal: ya no necesita contraseña
            nuevoHash = "";
        }

        var actualizado = new UserProfile
        {
            Nombre = _nombreOriginal,
            Correo = correoInput.text.Trim(),
            Edad = edadInput.text.Trim(),
            EsAdministrador = esAdmin,
            PasswordHash = nuevoHash
        };

        UserDatabase.ActualizarUsuario(_nombreOriginal, actualizado);

        _listaPadre.RefrescarLista();
        gameObject.SetActive(false);
        _listaPadre.gameObject.SetActive(true);
    }

    public void OnVolverPresionado()
    {
        gameObject.SetActive(false);
        _listaPadre.gameObject.SetActive(true);
    }
}