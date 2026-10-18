using System.Collections;
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

    //Confirmación en el mismo botón: el primer clic pide confirmar y solo un segundo clic (dentro del tiempo) elimina
    private const float SEGUNDOS_PARA_CONFIRMAR = 3f;
    private TMP_Text _textoEliminar;
    private string _textoEliminarOriginal;
    private bool _esperandoConfirmacion = false;
    private Coroutine _restaurarTexto;

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
        //Se vincula el botón de eliminar, que primero pide confirmación antes de llamar al contenedor
        _textoEliminar = eliminarButton.GetComponentInChildren<TMP_Text>(true);
        if (_textoEliminar != null && _textoEliminarOriginal == null) _textoEliminarOriginal = _textoEliminar.text;
        _esperandoConfirmacion = false;
        eliminarButton.onClick.AddListener(OnEliminarPresionado);
    }

    private void OnEliminarPresionado()
    {
        //Segundo clic dentro del tiempo: se elimina
        if (_esperandoConfirmacion)
        {
            _esperandoConfirmacion = false;
            _listaPadre.EliminarUsuario(_usuario);
            return;
        }

        //Si no se puede eliminar (su propia sesión o el único administrador), se muestra el motivo en el botón
        if (!_listaPadre.PuedeEliminar(_usuario, out string motivo))
        {
            MostrarTextoTemporal(motivo);
            return;
        }

        _esperandoConfirmacion = true;
        MostrarTextoTemporal("¿Seguro?");
    }

    //Cambia el texto del botón y lo devuelve al original (cancelando la confirmación) pasado el tiempo
    private void MostrarTextoTemporal(string texto)
    {
        if (_textoEliminar == null) return;
        _textoEliminar.text = texto;

        if (_restaurarTexto != null) StopCoroutine(_restaurarTexto);
        _restaurarTexto = StartCoroutine(RestaurarTextoEliminar());
    }

    private IEnumerator RestaurarTextoEliminar()
    {
        yield return new WaitForSeconds(SEGUNDOS_PARA_CONFIRMAR);
        _esperandoConfirmacion = false;
        if (_textoEliminar != null) _textoEliminar.text = _textoEliminarOriginal;
        _restaurarTexto = null;
    }
}