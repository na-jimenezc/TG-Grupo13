using UnityEngine;
using TMPro;

public class IniciarSesionUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField nombreInput;
    [SerializeField] private TMP_Text mensajeError;

    private void OnEnable()
    {
        if (nombreInput != null) nombreInput.text = "";
        if (mensajeError != null) mensajeError.text = "";
    }

    public void OnIngresarPresionado()
    {
        string nombre = nombreInput.text.Trim();

        if (string.IsNullOrWhiteSpace(nombre))
        {
            mensajeError.text = "Ingresa un nombre de usuario.";
            return;
        }

        UserProfile usuario = UserDatabase.ObtenerUsuario(nombre);

        if (usuario == null)
        {
            mensajeError.text = "El usuario no existe.";
            return;
        }

        if (usuario.EsAdministrador)
        {
            AdminGateUI adminGate = FindFirstObjectByType<AdminGateUI>(FindObjectsInactive.Include);
            if (adminGate != null)
            {
                adminGate.PrepararLoginAdmin(usuario.Nombre);
                GetComponentInParent<GameMasterUI>().MostrarAdminGate();
            }
        }
        else
        {
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.IniciarSesionConUsuario(usuario);
                mensajeError.color = Color.green;
                mensajeError.text = "Iniciando simulación en visor...";

                // (Opcional) Puedes regresar la pantalla de la PC a inicio o mantener la UI de control
                // GetComponentInParent<GameMasterUI>()?.MostrarInicio();
            }
        }
    }

    public void OnVolverPresionado()
    {
        GetComponentInParent<GameMasterUI>().MostrarInicio();
    }
}