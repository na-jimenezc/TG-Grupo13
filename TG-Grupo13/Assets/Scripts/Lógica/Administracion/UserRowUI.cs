using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*Controla cada fila individual lesto de la lista de usuarios en el panel de administración
 * cada fila es un contenedor que está definido en unity y se le asignan las etiquetas*/
public class UserRowUI : MonoBehaviour
{
    //Etiquetas para mostrar
    [SerializeField] private TMP_Text nombreText;
    [SerializeField] private Button masInfoButton;
    [SerializeField] private Button actualizarButton;
    [SerializeField] private Button eliminarButton;

    //Referencias internas del usuario asignado a la fila y a su contenedor
    private UserProfile _usuario;
    private AdminListUI _listaPadre;

    //Asigna el usuario y el contenedor a la fila y configura los botones para que llamen a los métodos del contenedor
    public void Configurar(UserProfile usuario, AdminListUI listaPadre)
    {
        _usuario = usuario;
        _listaPadre = listaPadre;
        nombreText.text = usuario.Nombre;

        //Siempre se limpian los listener anteriores para evitar que se acumulen y luego agrega los nuevos listener para cada botón

        masInfoButton.onClick.RemoveAllListeners();
        //Se vincula el botón de más info para que llame al método MostrarInfo del contenedor, pasando el usuario actual
        masInfoButton.onClick.AddListener(() => _listaPadre.MostrarInfo(_usuario));

        actualizarButton.onClick.RemoveAllListeners();
        //Se vincula el botón de actualizar para que llame al método MostrarActualizar del contenedor, pasando el usuario actual
        actualizarButton.onClick.AddListener(() => _listaPadre.MostrarActualizar(_usuario));

        eliminarButton.onClick.RemoveAllListeners();
        //Se vincula el botón de eliminar para que llame al método EliminarUsuario del contenedor, pasando el usuario actual
        eliminarButton.onClick.AddListener(() => _listaPadre.EliminarUsuario(_usuario));
    }
}