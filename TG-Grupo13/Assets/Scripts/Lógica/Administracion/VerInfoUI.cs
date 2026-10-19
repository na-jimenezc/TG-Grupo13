using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private TMP_Text manoLabel;

    //Contenedor y prefab para mostrar la lista de capacitaciones del usuario
    [Header("Lista de Capacitaciones")]
    [SerializeField] private Transform contenedorCapacitaciones;
    [SerializeField] private GameObject capacitacionCardPrefab;
    [SerializeField] private TMP_Text mensajeVacio;

    [Header("Título del panel (si se deja vacío se busca el hijo MasInfoText)")]
    [SerializeField] private TMP_Text tituloPanel;

    //Guarda la referencia de la pantalla de la lista anterior para volver a ella al presionar el botón "Volver"
    private AdminListUI _listaPadre;

    //Separación entre tarjetas y con los bordes de la lista
    private const float MargenTarjetas = 10f;

    //En la escena, la lista de capacitaciones (CapacitacionesScroll) medía 70 de alto y su Viewport 10, así que las
    //tarjetas quedaban recortadas. Se estira hacia abajo, conservando su borde superior, hasta esta altura
    //(unidades del Canvas de 1920x1080, medidas desde el centro; el botón Volver está en -428, a la izquierda).
    private const float BordeInferiorLista = -330f;
    private const float AnchoBarra = 20f;
    private bool _listaAjustada;

    //Carga la información del usuario en la UI y genera las tarjetas de capacitaciones si existen.
    public void CargarUsuario(UserProfile usuario, AdminListUI listaPadre) => CargarUsuario(usuario, listaPadre, false);

    //revisionReba = true: mismo panel, pero cada tarjeta muestra la tabla REBA pre / tutorial / post (botón "Revisión REBA").
    public void CargarUsuario(UserProfile usuario, AdminListUI listaPadre, bool revisionReba)
    {
        _listaPadre = listaPadre;
        AjustarAreaLista();

        if (tituloPanel == null)
        {
            Transform titulo = transform.Find("MasInfoText");
            if (titulo != null) tituloPanel = titulo.GetComponent<TMP_Text>();
        }
        if (tituloPanel != null) tituloPanel.text = revisionReba ? "Revisión REBA" : "Más Información";

        //Asignación de los datos del usuario a los campos de texto correspondientes
        nombreLabel.text = usuario.Nombre;
        edadLabel.text = $"Edad: {usuario.Edad}";
        correoLabel.text = $"Correo: {usuario.Correo}";
        rolLabel.text = usuario.EsAdministrador ? "Rol: Administrador" : "Rol: Normal";
        if (manoLabel != null) manoLabel.text = usuario.EsZurdo ? "Mano dominante: Izquierda (zurdo)" : "Mano dominante: Derecha (diestro)";

        //Se limpia el contenedor para cargar nuevas tarjetas de capacitaciones
        foreach (Transform hijo in contenedorCapacitaciones)
            Destroy(hijo.gameObject);

        //Se muestra o no el mensaje dependiendo si se encuentran capacitaciones registradas
        bool tieneCapacitaciones = usuario.Capacitaciones != null && usuario.Capacitaciones.Count > 0;

        if (mensajeVacio != null) mensajeVacio.gameObject.SetActive(!tieneCapacitaciones);

        //Si hay capacitaciones se realiza un bucle para crear una tarjeta por cada capacitación y se configura con la información correspondiente.
        //El contenedor no tiene LayoutGroup, así que las tarjetas se apilan aquí, una debajo de la otra.
        var contenido = (RectTransform)contenedorCapacitaciones;
        //Se descuenta la barra de desplazamiento, que al aparecer angosta la lista (y así el texto no se sale).
        float ancho = contenido.rect.width - 2f * MargenTarjetas - AnchoBarra;
        float y = MargenTarjetas;
        if (tieneCapacitaciones)
        {
            for (int i = 0; i < usuario.Capacitaciones.Count; i++)
            {
                GameObject card = Instantiate(capacitacionCardPrefab, contenedorCapacitaciones);
                var tarjeta = card.GetComponent<CapacitacionCardUI>();
                tarjeta.Configurar(usuario.Capacitaciones[i], i + 1, revisionReba, ancho);

                var rt = (RectTransform)card.transform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(-2f * MargenTarjetas, tarjeta.Alto);
                rt.anchoredPosition = new Vector2(0f, -y);
                y += tarjeta.Alto + MargenTarjetas;
            }
        }

        //El contenido mide lo que suman las tarjetas y vuelve arriba (antes quedaba corrido hacia arriba y no se veía).
        contenido.anchorMin = new Vector2(0f, 1f);
        contenido.anchorMax = new Vector2(1f, 1f);
        contenido.pivot = new Vector2(0.5f, 1f);
        contenido.sizeDelta = new Vector2(0f, y);
        contenido.anchoredPosition = Vector2.zero;

        //Que la lista quede mostrando la primera tarjeta desde su borde superior.
        var scroll = contenedorCapacitaciones.GetComponentInParent<ScrollRect>(true);
        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    //Agranda la lista de capacitaciones hacia abajo (una sola vez) y deja el Viewport ocupando toda la lista.
    private void AjustarAreaLista()
    {
        if (_listaAjustada) return;
        _listaAjustada = true;

        var scroll = contenedorCapacitaciones.GetComponentInParent<ScrollRect>(true);
        if (scroll == null) return;

        //La lista traía un ContentSizeFitter y un VerticalLayoutGroup propios que la encogían (y al Viewport con ella).
        //Las tarjetas las acomoda CargarUsuario, así que se apagan.
        var ajusteTamano = scroll.GetComponent<ContentSizeFitter>();
        if (ajusteTamano != null) ajusteTamano.enabled = false;
        var grupo = scroll.GetComponent<LayoutGroup>();
        if (grupo != null) grupo.enabled = false;

        var area = (RectTransform)scroll.transform;
        float bordeSuperior = area.anchoredPosition.y + (1f - area.pivot.y) * area.sizeDelta.y;
        float alto = bordeSuperior - BordeInferiorLista;
        if (alto > area.sizeDelta.y)
        {
            area.sizeDelta = new Vector2(area.sizeDelta.x, alto);
            area.anchoredPosition = new Vector2(area.anchoredPosition.x, bordeSuperior - (1f - area.pivot.y) * alto);
        }

        var viewport = scroll.viewport != null ? scroll.viewport : contenedorCapacitaciones.parent as RectTransform;
        if (viewport != null)
        {
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
        }
        //La barra de desplazamiento también la acomodaba el LayoutGroup: se deja pegada al borde derecho.
        if (scroll.verticalScrollbar != null)
        {
            var barra = (RectTransform)scroll.verticalScrollbar.transform;
            barra.anchorMin = new Vector2(1f, 0f);
            barra.anchorMax = Vector2.one;
            barra.pivot = Vector2.one;
            barra.anchoredPosition = Vector2.zero;
            barra.sizeDelta = new Vector2(AnchoBarra, 0f);
        }
        scroll.horizontal = false;
        scroll.vertical = true;
    }

    // Al presionar el botón "Volver", se oculta la pantalla de información y se muestra nuevamente la lista de usuarios.
    public void OnVolverPresionado()
    {
        gameObject.SetActive(false);
        _listaPadre.gameObject.SetActive(true);
    }
}