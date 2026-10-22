using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*Controla el formulario para la creación e inscripción de un nuevo perfil de usuario. Permite ingresar nombre, correo, edad, rol y contraseña (si es administrador). 
 * Valida los datos antes de crear el perfil y manejar la sesión del usuario recién creado.*/
public class CrearPerfilUI : MonoBehaviour
{
    //Referencias a los elementos de la UI
    [SerializeField] private TMP_InputField nombreInput;
    [SerializeField] private TMP_InputField correoInput;
    [SerializeField] private TMP_Dropdown rolDropdown; //0 = Normal, 1 = Administrador
    [SerializeField] private GameObject passwordInputObject;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_InputField edadInput;
    [SerializeField] private TMP_Dropdown manoDropdown; //0 = Diestro, 1 = Zurdo
    [SerializeField] private TMP_Text mensajeError;
    [Tooltip("Segundos que se muestra el mensaje de éxito al crear un administrador antes de volver al inicio")]
    [SerializeField] private float segundosMensajeExito = 1.5f;

    //Evita crear el mismo perfil dos veces con un doble clic mientras se muestra el mensaje de éxito
    private bool _creando = false;

    //Se le pone a cada campo una etiqueta que indique qué dato va en él, ya que el placeholder por defecto no lo dice.
    void Awake()
    {
        EtiquetaCampoUI.Etiquetar(nombreInput, "Nombre *", "Ej: Ana Gómez");
        EtiquetaCampoUI.Etiquetar(correoInput, "Correo electrónico", "Ej: ana@correo.com");
        EtiquetaCampoUI.Etiquetar(edadInput, "Edad *", $"Entre {ValidacionEdad.EDAD_MINIMA} y {ValidacionEdad.EDAD_MAXIMA}");
        RolUI.Configurar(rolDropdown);
        EtiquetaCampoUI.Etiquetar(passwordInput, "Contraseña *", "Contraseña");
        ManoDominanteUI.Configurar(manoDropdown);
        ValidacionEdad.Configurar(edadInput);
    }

    //Cuando se activa esta pantalla, se limpia el formulario y se actualiza la visibilidad del campo de contraseña según el rol seleccionado.
    void OnEnable()
    {
        //Cada vez que se muestra esta pantalla, empieza limpia
        nombreInput.text = "";
        correoInput.text = "";
        passwordInput.text = "";
        rolDropdown.value = 0;
        mensajeError.text = "";
        mensajeError.color = Color.red;
        edadInput.text = "";
        _creando = false;
        if (manoDropdown != null) manoDropdown.value = 0;
        ActualizarVisibilidadPassword();
    }

    //Se suscribe al evento de cambio de valor del dropdown para actualizar la visibilidad del campo de contraseña.
    void Start()
    {
        rolDropdown.onValueChanged.AddListener(_ => ActualizarVisibilidadPassword());
    }

    //Actualiza la visibilidad del campo de contraseña según el rol seleccionado. Si es administrador, se muestra; si es usuario normal, se oculta.
    //La mano dominante solo importa para los participantes, así que a los administradores no se les pregunta.
    void ActualizarVisibilidadPassword()
    {
        bool esAdmin = rolDropdown.value == 1;
        passwordInputObject.SetActive(esAdmin);
        if (manoDropdown != null) manoDropdown.gameObject.SetActive(!esAdmin);
    }

    //Cuando se presiona el botón de crear perfil, se validan los datos ingresados y se crea el usuario si todo es correcto.
    public void OnCrearPerfilPresionado()
    {
        if (_creando) return;

        string nombre = nombreInput.text.Trim();

        //Se hace la validación de los campos requeridos y se muestra un mensaje de error si algo no es correcto.
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

        if (!ValidacionEdad.Validar(edadInput.text, out string edad, out string errorEdad))
        {
            mensajeError.text = errorEdad;
            return;
        }

        bool esAdmin = rolDropdown.value == 1;

        //Solo se crean administradores con un administrador autorizado (o si aún no existe ninguno)
        if (esAdmin && !AutorizacionAdmin.PuedeCrearAdministrador())
        {
            mensajeError.text = $"Para crear un administrador, primero ingresa al Panel de Administrador con un admin existente (vale {AutorizacionAdmin.MINUTOS_VIGENCIA:0} min)";
            return;
        }

        if (esAdmin && string.IsNullOrWhiteSpace(passwordInput.text))
        {
            mensajeError.text = "Los administradores necesitan contraseña";
            return;
        }

        //Se crea el usuario si todos los campos pasan el check
        var nuevoUsuario = UserDatabase.CrearUsuario(
            nombre,
            correoInput.text.Trim(),
            esAdmin,
            esAdmin ? passwordInput.text : null,
            edad,
            ManoDominanteUI.EsZurdo(manoDropdown)
        );

        //Un administrador no entra a la simulación ni queda con la sesión iniciada: debe ingresar
        //con su contraseña desde Iniciar Sesión. Solo se confirma la creación y se vuelve al inicio.
        if (esAdmin)
        {
            StartCoroutine(MostrarExitoYVolver(nombre));
            return;
        }

        GameFlowManager.Instance.IniciarSesionConUsuario(nuevoUsuario);
    }

    private IEnumerator MostrarExitoYVolver(string nombre)
    {
        _creando = true;
        mensajeError.color = Color.green;
        mensajeError.text = $"Administrador \"{nombre}\" creado. Ingresa desde Iniciar Sesión.";

        yield return new WaitForSeconds(segundosMensajeExito);

        GetComponentInParent<GameMasterUI>().MostrarInicio();
    }

    //Cuando se presiona el botón de volver, se regresa a la pantalla de inicio.
    public void OnVolverPresionado()
    {
        GetComponentInParent<GameMasterUI>().MostrarInicio();
    }
}