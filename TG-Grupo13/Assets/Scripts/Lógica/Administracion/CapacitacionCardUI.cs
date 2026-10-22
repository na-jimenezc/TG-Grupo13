using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

/*Controlador de la tarjeta visual de capacitación que se renderiza en VerInfoUI.
 * Tiene dos modos:
 *  - "Más Info": una línea por actividad con el tiempo y la calificación.
 *  - "Revisión REBA": por actividad, una tabla pre / tutorial / post con los puntajes REBA que guardó
 *    RebaSessionRecorder (Scripts/JoyconVR) y un botón para abrir la carpeta con los CSV de la sesión.
 * El prefab traía los textos en blanco (sobre el fondo blanco de la lista) y con un alto de 0, así que la tarjeta
 * acomoda sus textos por código: un fondo gris claro y los textos uno debajo del otro, en letra oscura.*/
public class CapacitacionCardUI : MonoBehaviour
{
    //Campos referenciando los elementos de texto en la UI para mostrar la información de la capacitación
    [SerializeField] private TMP_Text tituloText;
    [SerializeField] private TMP_Text fechaText;
    [SerializeField] private TMP_Text actividadesText;

    private const float Margen = 16f;
    private const float Separacion = 4f;
    private const float AltoBoton = 36f;
    private const float AnchoBoton = 260f;
    private static readonly Color ColorFondo = new Color(0.93f, 0.95f, 0.97f);
    private static readonly Color ColorTexto = new Color(0.13f, 0.13f, 0.13f);
    private static readonly Color ColorBoton = new Color(0.22f, 0.45f, 0.72f);
    private static readonly CultureInfo Espanol = new CultureInfo("es-ES");

    private bool _disenoListo;
    private Button _botonCarpeta;
    private TMP_Text _textoBoton;
    private string _carpeta;

    //Alto que ocupa la tarjeta con su contenido (lo usa VerInfoUI para apilarlas).
    public float Alto { get; private set; }

    //Llena la tarjeta visual con la información de la capacitación y el número correspondiente
    public void Configurar(Capacitacion capacitacion, int numero) => Configurar(capacitacion, numero, false, 0f);

    //ancho: ancho de la tarjeta (para calcular cuánto alto necesitan los textos)
    public void Configurar(Capacitacion capacitacion, int numero, bool revisionReba, float ancho)
    {
        PrepararDiseno();

        tituloText.text = $"Capacitación {numero}";
        fechaText.text = $"Fecha: {capacitacion.Fecha}";
        actividadesText.text = revisionReba ? TextoReba(capacitacion) : TextoResumen(capacitacion);

        float anchoTexto = Mathf.Max(100f, ancho - 2f * Margen);
        float y = Margen;
        y = Colocar(tituloText, y, anchoTexto);
        y = Colocar(fechaText, y, anchoTexto);
        y = Colocar(actividadesText, y + Separacion, anchoTexto);

        _carpeta = revisionReba ? CarpetaDatos(capacitacion) : null;
        _botonCarpeta.gameObject.SetActive(revisionReba);
        if (revisionReba)
        {
            bool existe = _carpeta != null && Directory.Exists(_carpeta);
            _botonCarpeta.interactable = existe;
            _textoBoton.text = existe ? "Abrir carpeta de datos (CSV)" : "Sin carpeta de datos";
            var rt = (RectTransform)_botonCarpeta.transform;
            rt.anchoredPosition = new Vector2(Margen, -(y + Separacion));
            y += Separacion + AltoBoton + Separacion;
        }

        Alto = y + Margen;
    }

    private static string TextoResumen(Capacitacion capacitacion)
    {
        var sb = new StringBuilder();
        foreach (var actividad in capacitacion.Actividades)
        {
            sb.AppendLine($"{actividad.Tipo} — Tiempo: {actividad.TiempoTomado} — Calificación: {actividad.CalificacionGeneral}");
        }
        return sb.ToString().TrimEnd();
    }

    //Tabla por actividad. Las columnas se alinean con <pos> de TextMeshPro (porcentaje del ancho del texto).
    private static string TextoReba(Capacitacion capacitacion)
    {
        var sb = new StringBuilder();
        foreach (var actividad in capacitacion.Actividades)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine($"<b>{actividad.Tipo}</b>");

            RebaData reba = actividad.Reba;
            if (reba == null || !(reba.PreTutorial.HayDatos || reba.Tutorial.HayDatos || reba.PostTutorial.HayDatos))
            {
                sb.AppendLine("Sin datos REBA (no se grabó con los Joy-Cons).");
                continue;
            }

            sb.AppendLine(Fila("", "<b>Pre</b>", "<b>Tutorial</b>", "<b>Post</b>"));
            sb.AppendLine(Fila("REBA más frecuente (moda)", Moda(reba.PreTutorial), Moda(reba.Tutorial), Moda(reba.PostTutorial)));
            sb.AppendLine(Fila("% tiempo riesgo alto o muy alto", PctAlto(reba.PreTutorial), PctAlto(reba.Tutorial), PctAlto(reba.PostTutorial)));
            sb.AppendLine(Fila("Duración que cuenta", Duracion(reba.PreTutorial), Duracion(reba.Tutorial), Duracion(reba.PostTutorial)));
        }
        sb.AppendLine();
        sb.Append("<size=80%>Moda: el puntaje REBA que más se repitió (la postura más frecuente) en el tiempo que cuenta " +
                  "(sin calibración, caminatas ni momentos sin tarea). " +
                  "Riesgo: 1 insignificante, 2-3 bajo, 4-7 medio, 8-10 alto, 11+ muy alto.</size>");
        return sb.ToString();
    }

    private static string Fila(string nombre, string pre, string tutorial, string post) =>
        $"{nombre}<pos=50%>{pre}<pos=67%>{tutorial}<pos=84%>{post}";

    //La moda; en registros viejos (antes de guardarla) se usa la mediana.
    private static string Moda(RebaResumen r)
    {
        if (!r.HayDatos) return "—";
        int moda = r.Moda > 0 ? r.Moda : Mathf.RoundToInt(r.Mediana);
        return $"{moda} ({Riesgo(moda)})";
    }

    private static string PctAlto(RebaResumen r) =>
        r.HayDatos ? (r.PorcentajeAlto + r.PorcentajeMuyAlto).ToString("0", Espanol) + " %" : "—";

    private static string Duracion(RebaResumen r)
    {
        if (!r.HayDatos) return "—";
        int s = Mathf.RoundToInt(r.DuracionSegundos);
        return s < 60 ? $"{s} s" : $"{s / 60} min {s % 60:00} s";
    }

    private static string Riesgo(int puntaje)
    {
        if (puntaje <= 1) return "insignificante";
        if (puntaje <= 3) return "bajo";
        if (puntaje <= 7) return "medio";
        if (puntaje <= 10) return "alto";
        return "muy alto";
    }

    //La carpeta de la sesión es la misma para todas las actividades de la capacitación.
    private static string CarpetaDatos(Capacitacion capacitacion)
    {
        foreach (var actividad in capacitacion.Actividades)
        {
            string carpeta = actividad.Reba?.CarpetaDatos;
            if (!string.IsNullOrEmpty(carpeta)) return Path.Combine(Application.persistentDataPath, carpeta);
        }
        return null;
    }

    private void AbrirCarpeta()
    {
        if (_carpeta != null && Directory.Exists(_carpeta))
            Application.OpenURL("file:///" + _carpeta.Replace('\\', '/'));
    }

    //Se hace una sola vez por tarjeta: deja los tres textos como hijos directos de la tarjeta, sin los
    //LayoutGroup del prefab (que los dejaban encimados), con fondo, y crea el botón de la carpeta.
    private void PrepararDiseno()
    {
        if (_disenoListo) return;
        _disenoListo = true;

        foreach (var grupo in GetComponentsInChildren<LayoutGroup>(true))
            grupo.enabled = false;

        var fondo = GetComponent<Image>();
        if (fondo == null) fondo = gameObject.AddComponent<Image>();
        fondo.color = ColorFondo;
        fondo.raycastTarget = false; //que se pueda arrastrar la lista (scroll) desde encima de la tarjeta

        PrepararTexto(tituloText, 26f, FontStyles.Bold);
        PrepararTexto(fechaText, 20f, FontStyles.Normal);
        PrepararTexto(actividadesText, 20f, FontStyles.Normal);

        _botonCarpeta = CrearBoton();
    }

    private void PrepararTexto(TMP_Text texto, float tamano, FontStyles estilo)
    {
        texto.rectTransform.SetParent(transform, false);
        texto.enableAutoSizing = false;
        texto.fontSize = tamano;
        texto.fontStyle = estilo;
        texto.color = ColorTexto;
        texto.alignment = TextAlignmentOptions.TopLeft;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.overflowMode = TextOverflowModes.Overflow;
        texto.richText = true;
        texto.raycastTarget = false;
    }

    //Pone el texto arriba (debajo del anterior), estirado a lo ancho, con el alto que necesita.
    private static float Colocar(TMP_Text texto, float y, float anchoTexto)
    {
        var rt = texto.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        float alto = Mathf.Ceil(texto.GetPreferredValues(texto.text, anchoTexto, 0f).y);
        rt.sizeDelta = new Vector2(-2f * Margen, alto);
        rt.anchoredPosition = new Vector2(0f, -y);
        return y + alto + Separacion;
    }

    private Button CrearBoton()
    {
        var go = new GameObject("AbrirCarpetaButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(AnchoBoton, AltoBoton);
        go.GetComponent<Image>().color = ColorBoton;

        var textoGo = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        textoGo.layer = gameObject.layer;
        var rtTexto = (RectTransform)textoGo.transform;
        rtTexto.SetParent(rt, false);
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.offsetMin = rtTexto.offsetMax = Vector2.zero;
        _textoBoton = textoGo.GetComponent<TextMeshProUGUI>();
        if (tituloText.font != null) _textoBoton.font = tituloText.font;
        _textoBoton.fontSize = 18f;
        _textoBoton.color = Color.white;
        _textoBoton.alignment = TextAlignmentOptions.Center;
        _textoBoton.raycastTarget = false;

        var boton = go.GetComponent<Button>();
        boton.onClick.AddListener(AbrirCarpeta);
        return boton;
    }
}
