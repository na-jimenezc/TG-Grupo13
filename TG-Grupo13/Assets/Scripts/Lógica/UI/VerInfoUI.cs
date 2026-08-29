using UnityEngine;
using TMPro;

public class VerInfoUI : MonoBehaviour
{
    [Header("Datos básicos")]
    [SerializeField] private TMP_Text nombreLabel;
    [SerializeField] private TMP_Text edadLabel;
    [SerializeField] private TMP_Text correoLabel;
    [SerializeField] private TMP_Text rolLabel;

    [Header("Lista de Capacitaciones")]
    [SerializeField] private Transform contenedorCapacitaciones;
    [SerializeField] private GameObject capacitacionCardPrefab;
    [SerializeField] private TMP_Text mensajeVacio;

    private AdminListUI _listaPadre;

    public void CargarUsuario(UserProfile usuario, AdminListUI listaPadre)
    {
        _listaPadre = listaPadre;

        nombreLabel.text = usuario.Nombre;
        edadLabel.text = $"Edad: {usuario.Edad}";
        correoLabel.text = $"Correo: {usuario.Correo}";
        rolLabel.text = usuario.EsAdministrador ? "Rol: Administrador" : "Rol: Normal";

        foreach (Transform hijo in contenedorCapacitaciones)
            Destroy(hijo.gameObject);

        bool tieneCapacitaciones = usuario.Capacitaciones != null && usuario.Capacitaciones.Count > 0;

        if (mensajeVacio != null) mensajeVacio.gameObject.SetActive(!tieneCapacitaciones);

        if (tieneCapacitaciones)
        {
            for (int i = 0; i < usuario.Capacitaciones.Count; i++)
            {
                GameObject card = Instantiate(capacitacionCardPrefab, contenedorCapacitaciones);
                card.GetComponent<CapacitacionCardUI>().Configurar(usuario.Capacitaciones[i], i + 1);
            }
        }
    }

    public void OnVolverPresionado()
    {
        gameObject.SetActive(false);
        _listaPadre.gameObject.SetActive(true);
    }
}