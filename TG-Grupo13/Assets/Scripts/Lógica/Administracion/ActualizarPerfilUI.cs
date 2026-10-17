using UnityEngine;
using TMPro;

/*Gestión del formulario para editar el perfil del usuario existente desde el panel de administración.
 * Permite cambiar el correo, edad, rol y contraseña (si es administrador).*/
public class ActualizarPerfilUI : MonoBehaviour
{
    //Atributos de UI
    [SerializeField] private TMP_Text nombreLabel;
    [SerializeField] private TMP_InputField edadInput;
    [SerializeField] private TMP_InputField correoInput;
    [SerializeField] private TMP_Dropdown rolDropdown; 
    [SerializeField] private TMP_Dropdown manoDropdown; //0 = Diestro, 1 = Zurdo
    [SerializeField] private GameObject passwordInputObject;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_Text mensajeError;

    //Variables itnernas para conservar el estado anterior antes de guardar por si no se cambian datos
    private string _nombreOriginal;
    private string _passwordHashActual;
    private AdminListUI _listaPadre;

    //Carga los datos del usuario en el formulario para su edición
    public void CargarUsuario(UserProfile usuario, AdminListUI listaPadre)
    {
        _listaPadre = listaPadre;
        _nombreOriginal = usuario.Nombre;
        _passwordHashActual = usuario.PasswordHash;

        //Se asignan los campos del formulario con los datos del usuario
        nombreLabel.text = usuario.Nombre;
        edadInput.text = usuario.Edad;
        correoInput.text = usuario.Correo;
        //El dropdown tiene dos opciones: 0 = Normal, 1 = Administrador
        rolDropdown.value = usuario.EsAdministrador ? 1 : 0;
        if (manoDropdown != null) manoDropdown.value = usuario.EsZurdo ? 1 : 0;
        passwordInput.text = "";
        mensajeError.text = "";
        ActualizarVisibilidadPassword();
    }

    //Etiquetas de los campos, ya que el placeholder por defecto no indica qué dato se pide.
    //La contraseña se puede dejar vacía si el usuario ya era administrador (se conserva la anterior).
    void Awake()
    {
        EtiquetaCampoUI.Etiquetar(edadInput, "Edad", "Ej: 25");
        EtiquetaCampoUI.Etiquetar(correoInput, "Correo electrónico", "Ej: ana@correo.com");
        EtiquetaCampoUI.Etiquetar(rolDropdown, "Rol");
        EtiquetaCampoUI.Etiquetar(passwordInput, "Nueva contraseña", "Vacío = no cambiar");
        ManoDominanteUI.Configurar(manoDropdown);
    }

    //Escucha los cambios del dropdown para mostrar u ocultar el campo de contraseña según el rol seleccionado
    void Start()
    {
        rolDropdown.onValueChanged.AddListener(_ => ActualizarVisibilidadPassword());
    }

    //Actualiza la visibilidad del campo de contraseña según el rol seleccionado
    void ActualizarVisibilidadPassword()
    {
        passwordInputObject.SetActive(rolDropdown.value == 1);
    }

    //Maneja la lógica de actualización del perfil del usuario
    public void OnActualizarPresionado()
    {
        //Validación de campos obligatorios
        bool esAdmin = rolDropdown.value == 1;
        bool eraAdmin = !string.IsNullOrEmpty(_passwordHashActual);
        string nuevoHash = _passwordHashActual;

        if (esAdmin)
        {
            if (!eraAdmin)
            {
                //Si el usuario no era admin y ahora sí, se requiere una contraseña
                if (string.IsNullOrWhiteSpace(passwordInput.text))
                {
                    mensajeError.text = "Ingresa una contraseña para convertirlo en administrador";
                    return;
                }
                nuevoHash = UserDatabase.HashPassword(passwordInput.text);
            }
            else if (!string.IsNullOrWhiteSpace(passwordInput.text))
            {
                //Si era un admin y ahora sigue siéndolo, pero ingresó una nueva contraseña, se actualiza el hash
                nuevoHash = UserDatabase.HashPassword(passwordInput.text);
            }
            //Si ya era admin, y deja la casilla vacía, se conserva '_passwordHashActual'
        }
        else
        {
            //Pasa a Normal: ya no necesita contraseña
            nuevoHash = "";
        }

        //Se construye el nuevo perfil con los datos actualizados. El Id y las capacitaciones se copian del perfil
        //guardado, porque el formulario no los edita y si no se perderían al guardar.
        var original = UserDatabase.ObtenerUsuario(_nombreOriginal);
        var actualizado = new UserProfile
        {
            Id = original != null ? original.Id : 0,
            Nombre = _nombreOriginal,
            Correo = correoInput.text.Trim(),
            Edad = edadInput.text.Trim(),
            EsAdministrador = esAdmin,
            PasswordHash = nuevoHash,
            EsZurdo = manoDropdown != null ? ManoDominanteUI.EsZurdo(manoDropdown) : original != null && original.EsZurdo,
            Capacitaciones = original != null ? original.Capacitaciones : new System.Collections.Generic.List<Capacitacion>()
        };

        //Se guarda en la database y se refresca la lista de usuarios en el panel de administración para el JSON
        UserDatabase.ActualizarUsuario(_nombreOriginal, actualizado);

        //Se refresca la lista de usuarios en el panel de administración
        _listaPadre.RefrescarLista();
        gameObject.SetActive(false);
        _listaPadre.gameObject.SetActive(true);
    }

    //Permite volver al panel de administración sin guardar cambios
    public void OnVolverPresionado()
    {
        gameObject.SetActive(false);
        _listaPadre.gameObject.SetActive(true);
    }
}