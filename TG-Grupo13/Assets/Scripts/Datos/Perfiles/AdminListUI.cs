using UnityEngine;
using System.Collections.Generic;

public class AdminListUI : MonoBehaviour
{
    [SerializeField] private Transform contenedorLista; // el "Content" del Scroll View
    [SerializeField] private GameObject filaPrefab;      // el Prefab FilaUsuario
    [SerializeField] private GameObject panelActualizar;
    [SerializeField] private ActualizarPerfilUI actualizarPerfilUI;

    void OnEnable()
    {
        RefrescarLista();
    }

    public void RefrescarLista()
    {
        foreach (Transform hijo in contenedorLista)
            Destroy(hijo.gameObject);

        List<UserProfile> usuarios = UserDatabase.ObtenerTodosLosUsuarios();
        foreach (var usuario in usuarios)
        {
            GameObject fila = Instantiate(filaPrefab, contenedorLista);
            fila.GetComponent<UserRowUI>().Configurar(usuario, this);
        }
    }

    public void MostrarInfo(UserProfile usuario)
    {
        Debug.Log($"Ver info de: {usuario.Nombre}"); // lo armamos completo en el próximo paso
    }

    public void MostrarActualizar(UserProfile usuario)
    {
        gameObject.SetActive(false); // oculta la lista (Panel_Admin)
        panelActualizar.SetActive(true);
        actualizarPerfilUI.CargarUsuario(usuario, this);
    }

    public void EliminarUsuario(UserProfile usuario)
    {
        UserDatabase.EliminarUsuario(usuario.Nombre);
        RefrescarLista();
    }

}