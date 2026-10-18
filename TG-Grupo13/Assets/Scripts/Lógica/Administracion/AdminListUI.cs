using UnityEngine;
using System.Collections.Generic;



public class AdminListUI : MonoBehaviour
{
    //Objetos referenciados de la UI
    [SerializeField] private Transform contenedorLista; //el "Content" del Scroll View
    [SerializeField] private GameObject filaPrefab;      //el Prefab FilaUsuario
    [SerializeField] private GameObject panelActualizar;
    [SerializeField] private ActualizarPerfilUI actualizarPerfilUI;
    [SerializeField] private GameObject panelInfo;
    [SerializeField] private VerInfoUI verInfoUI;

    //Se refresca la lista de usuarios cada vez que se activa el panel
    void OnEnable()
    {
        RefrescarLista();
    }

    //Se destruye cada hijo del contenedor y se vuelven a obtener todos los usuarios
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

    //Se carga la información del usuario seleccionado en el panel de información y se oculta la lista
    public void MostrarInfo(UserProfile usuario)
    {
        gameObject.SetActive(false);
        panelInfo.SetActive(true);
        verInfoUI.CargarUsuario(usuario, this);
    }

    //Se carga la información del usuario seleccionado en el panel de actualización y se oculta la lista
    public void MostrarActualizar(UserProfile usuario)
    {
        gameObject.SetActive(false); //oculta la lista (Panel_Admin)
        panelActualizar.SetActive(true);
        actualizarPerfilUI.CargarUsuario(usuario, this);
    }

    //Revisa si el usuario se puede eliminar; si no, devuelve en 'motivo' un texto corto para mostrar en el botón
    public bool PuedeEliminar(UserProfile usuario, out string motivo)
    {
        var activo = GameFlowManager.Instance != null ? GameFlowManager.Instance.usuarioActivo : null;
        if (activo != null && activo.Nombre.Equals(usuario.Nombre, System.StringComparison.OrdinalIgnoreCase))
        {
            motivo = "Es tu sesión";
            return false;
        }

        if (UserDatabase.EsUltimoAdministrador(usuario.Nombre))
        {
            motivo = "Único admin";
            return false;
        }

        motivo = "";
        return true;
    }

    //Se elimina el usuario seleccionado de la base de datos y se refresca la lista
    public void EliminarUsuario(UserProfile usuario)
    {
        if (!PuedeEliminar(usuario, out _)) return;

        UserDatabase.EliminarUsuario(usuario.Nombre);
        RefrescarLista();
    }
}