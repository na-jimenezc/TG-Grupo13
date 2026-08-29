using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UserRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nombreText;
    [SerializeField] private Button masInfoButton;
    [SerializeField] private Button actualizarButton;
    [SerializeField] private Button eliminarButton;

    private UserProfile _usuario;
    private AdminListUI _listaPadre;

    public void Configurar(UserProfile usuario, AdminListUI listaPadre)
    {
        _usuario = usuario;
        _listaPadre = listaPadre;
        nombreText.text = usuario.Nombre;

        masInfoButton.onClick.RemoveAllListeners();
        masInfoButton.onClick.AddListener(() => _listaPadre.MostrarInfo(_usuario));

        actualizarButton.onClick.RemoveAllListeners();
        actualizarButton.onClick.AddListener(() => _listaPadre.MostrarActualizar(_usuario));

        eliminarButton.onClick.RemoveAllListeners();
        eliminarButton.onClick.AddListener(() => _listaPadre.EliminarUsuario(_usuario));
    }
}