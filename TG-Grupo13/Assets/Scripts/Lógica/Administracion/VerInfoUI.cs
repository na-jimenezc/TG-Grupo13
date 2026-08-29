using UnityEngine;
using TMPro;

/*Gestiona la vista de la información del usuario registrado para el GameMaster desde la UI de administración. 
 * Permite ver los datos básicos y la lista de capacitaciones del usuario seleccionado.*/

public class VerInfoUI : MonoBehaviour
{
    //Campos de texto para desplegar la información del usuario
    [Header("Datos básicos")]
    [SerializeField] private TMP_Text nombreLabel;
    [SerializeField] private TMP_Text edadLabel;
    [SerializeField] private TMP_Text correoLabel;
    [SerializeField] private TMP_Text rolLabel;

    //Contenedor y prefab para mostrar la lista de capacitaciones del usuario
    [Header("Lista de Capacitaciones")]
    [SerializeField] private Transform contenedorCapacitaciones;
    [SerializeField] private GameObject capacitacionCardPrefab;
    [SerializeField] private TMP_Text mensajeVacio;

    //Guarda la referencia de la pantalla de la lista anterior para volver a ella al presionar el botón "Volver"
    private AdminListUI _listaPadre;

    //Carga la información del usuario en la UI y genera las tarjetas de capacitaciones si existen.
    public void CargarUsuario(UserProfile usuario, AdminListUI listaPadre)
    {
        _listaPadre = listaPadre;

        //Asignación de los datos del usuario a los campos de texto correspondientes
        nombreLabel.text = usuario.Nombre;
        edadLabel.text = $"Edad: {usuario.Edad}";
        correoLabel.text = $"Correo: {usuario.Correo}";
        rolLabel.text = usuario.EsAdministrador ? "Rol: Administrador" : "Rol: Normal";

        //Se limpia el contenedor para cargar nuevas tarjetas de capacitaciones
        foreach (Transform hijo in contenedorCapacitaciones)
            Destroy(hijo.gameObject);

        //Se muestra o no el mensaje dependiendo si se encuentran capacitaciones registradas
        bool tieneCapacitaciones = usuario.Capacitaciones != null && usuario.Capacitaciones.Count > 0;

        if (mensajeVacio != null) mensajeVacio.gameObject.SetActive(!tieneCapacitaciones);

        //Si hay capacitaciones se realiza un bucle para crear una tarjeta por cada capacitación y se configura con la información correspondiente
        if (tieneCapacitaciones)
        {
            for (int i = 0; i < usuario.Capacitaciones.Count; i++)
            {
                GameObject card = Instantiate(capacitacionCardPrefab, contenedorCapacitaciones);
                card.GetComponent<CapacitacionCardUI>().Configurar(usuario.Capacitaciones[i], i + 1);
            }
        }
    }

    // Al presionar el botón "Volver", se oculta la pantalla de información y se muestra nuevamente la lista de usuarios.
    public void OnVolverPresionado()
    {
        gameObject.SetActive(false);
        _listaPadre.gameObject.SetActive(true);
    }
}