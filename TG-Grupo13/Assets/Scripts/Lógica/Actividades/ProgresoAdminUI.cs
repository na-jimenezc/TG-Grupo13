using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*Muestra en el panel de control del Game Master qué está viendo el usuario en el visor y cuánto lleva:
 *  - El recorrido de la sesión (las actividades elegidas, marcando las terminadas, la actual y las pendientes).
 *  - La fase de la actividad actual (Menú, Tutorial o Actividad en curso).
 *  - El detalle: en el Tutorial, el paso y su instrucción; en la Actividad, los contadores (plantas, ortigas, ramos).
 *  - Una barra de progreso del tutorial o de la actividad.
 * La interfaz se construye por código (ver Crear), así no hay que armarla a mano en la escena Core.*/
public class ProgresoAdminUI : MonoBehaviour
{
    [Header("Colores")]
    [SerializeField] private Color colorCompletada = new Color(0.25f, 0.65f, 0.35f);
    [SerializeField] private Color colorActual = new Color(0.20f, 0.45f, 0.85f);
    [SerializeField] private Color colorPendiente = new Color(0.35f, 0.35f, 0.35f);
    [SerializeField] private Color colorFondoBarra = new Color(0.15f, 0.15f, 0.15f, 0.9f);

    private TMP_FontAsset _fuente;
    private RectTransform _recorrido;
    private TextMeshProUGUI _faseText;
    private TextMeshProUGUI _detalleText;
    private GameObject _barra;
    private RectTransform _rellenoBarra;
    private Image _rellenoImagen;
    private readonly List<(Image fondo, TextMeshProUGUI texto)> _chips = new List<(Image, TextMeshProUGUI)>();

    //Crea el bloque de progreso como hijo del panel indicado, en la franja entre el título y los botones.
    public static ProgresoAdminUI Crear(RectTransform panel, TMP_FontAsset fuente)
    {
        var go = new GameObject("ProgresoAdmin", typeof(RectTransform));
        //Se crea apagado para asignar la fuente antes de que corra Awake (que es quien arma la interfaz).
        go.SetActive(false);
        go.layer = panel.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(panel, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 165f);
        rt.sizeDelta = new Vector2(900f, 230f);

        var progreso = go.AddComponent<ProgresoAdminUI>();
        progreso._fuente = fuente;
        go.SetActive(true);
        return progreso;
    }

    private void Awake() => Construir();

    private void OnEnable()
    {
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.OnEstadoCambiado += Refrescar;
        TutorialManagerBase.OnPasoCambiado += Refrescar;
        ActivityManagerBase.OnProgresoCambiado += Refrescar;
        Refrescar();
    }

    private void OnDisable()
    {
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.OnEstadoCambiado -= Refrescar;
        TutorialManagerBase.OnPasoCambiado -= Refrescar;
        ActivityManagerBase.OnProgresoCambiado -= Refrescar;
    }

    private void Construir()
    {
        if (_fuente == null) _fuente = TMP_Settings.defaultFontAsset;

        var layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        //Fila con una "ficha" por actividad de la sesión.
        _recorrido = NuevoHijo("Recorrido", transform, 48f);
        var fila = _recorrido.gameObject.AddComponent<HorizontalLayoutGroup>();
        fila.childAlignment = TextAnchor.MiddleCenter;
        fila.spacing = 16f;
        fila.childControlWidth = true;
        fila.childControlHeight = true;
        fila.childForceExpandWidth = false;
        fila.childForceExpandHeight = true;

        _faseText = NuevoTexto("Fase", transform, 44f, 32f, FontStyles.Bold);
        _detalleText = NuevoTexto("Detalle", transform, 70f, 24f, FontStyles.Normal);
        _detalleText.enableAutoSizing = true;
        _detalleText.fontSizeMin = 16f;
        _detalleText.fontSizeMax = 24f;

        //Barra de progreso: fondo oscuro y un relleno que se estira según la fracción (no necesita sprite).
        var fondoBarra = NuevoHijo("Barra", transform, 22f);
        fondoBarra.gameObject.AddComponent<Image>().color = colorFondoBarra;
        _barra = fondoBarra.gameObject;

        _rellenoBarra = NuevoHijo("Relleno", fondoBarra, 0f);
        _rellenoBarra.anchorMin = Vector2.zero;
        _rellenoBarra.anchorMax = new Vector2(0f, 1f);
        _rellenoBarra.offsetMin = _rellenoBarra.offsetMax = Vector2.zero;
        _rellenoImagen = _rellenoBarra.gameObject.AddComponent<Image>();
        _rellenoImagen.raycastTarget = false;
    }

    private void Refrescar()
    {
        if (_faseText == null) return;

        GameFlowManager flujo = GameFlowManager.Instance;
        if (flujo == null || flujo.ActividadActual == null)
        {
            ActualizarRecorrido(null);
            _faseText.text = "";
            _detalleText.text = "";
            _barra.SetActive(false);
            return;
        }

        ActualizarRecorrido(flujo);

        string encabezado = $"Actividad {flujo.IndiceActividadActual + 1} de {flujo.ActividadesSesion.Count}: {flujo.ActividadActual}";

        switch (flujo.Fase)
        {
            case FaseSesion.Tutorial:
                _faseText.text = $"{encabezado}  |  Tutorial";
                MostrarTutorial(TutorialManagerBase.Actual);
                break;

            case FaseSesion.Actividad:
                bool post = flujo.TutorialVistoEnActividadActual;
                _faseText.text = $"{encabezado}  |  Actividad {(post ? "post (después del tutorial)" : "pre (antes del tutorial)")}";
                MostrarActividad(ActivityManagerBase.Current, post);
                break;

            default:
                _faseText.text = $"{encabezado}  |  Menú de la actividad";
                _detalleText.text = "El usuario está viendo el menú de la actividad.\nPresione \"Comenzar\" para la actividad pre (antes del tutorial).";
                _barra.SetActive(false);
                break;
        }
    }

    private void MostrarTutorial(TutorialManagerBase tutorial)
    {
        if (tutorial == null || tutorial.TotalPasos == 0)
        {
            _detalleText.text = "Cargando tutorial...";
            _barra.SetActive(false);
            return;
        }

        int paso = tutorial.PasoActual + 1;
        string contador = string.IsNullOrEmpty(tutorial.ContadorActual) ? "" : $"  ({tutorial.ContadorActual})";
        _detalleText.text = $"Paso {paso} de {tutorial.TotalPasos}{contador}\n{tutorial.InstruccionActual}";
        PonerBarra((float)paso / tutorial.TotalPasos, colorActual);
    }

    private void MostrarActividad(IActivityManager manager, bool post)
    {
        if (manager == null)
        {
            _detalleText.text = "Cargando actividad...";
            _barra.SetActive(false);
            return;
        }

        string siguientePaso = post ? "\"Continuar\"" : "\"Ir al tutorial\"";
        _detalleText.text = manager.ActividadCompletada
            ? $"{manager.ResumenProgreso}\nMeta cumplida: puede presionar {siguientePaso}."
            : manager.ResumenProgreso;
        PonerBarra(manager.FraccionProgreso, manager.ActividadCompletada ? colorCompletada : colorActual);
    }

    private void PonerBarra(float fraccion, Color color)
    {
        _barra.SetActive(true);
        _rellenoBarra.anchorMax = new Vector2(Mathf.Clamp01(fraccion), 1f);
        _rellenoImagen.color = color;
    }

    //Una ficha por actividad: verde las terminadas, azul la actual y gris las que faltan.
    private void ActualizarRecorrido(GameFlowManager flujo)
    {
        int total = flujo != null ? flujo.ActividadesSesion.Count : 0;

        while (_chips.Count < total)
        {
            var chip = NuevoHijo($"Ficha{_chips.Count + 1}", _recorrido, 48f);
            chip.GetComponent<LayoutElement>().preferredWidth = 260f;
            var fondo = chip.gameObject.AddComponent<Image>();
            fondo.raycastTarget = false;

            var texto = NuevoTexto("Texto", chip, 0f, 22f, FontStyles.Bold);
            var rtTexto = texto.rectTransform;
            rtTexto.anchorMin = Vector2.zero;
            rtTexto.anchorMax = Vector2.one;
            rtTexto.offsetMin = rtTexto.offsetMax = Vector2.zero;
            _chips.Add((fondo, texto));
        }

        for (int i = 0; i < _chips.Count; i++)
        {
            bool visible = i < total;
            _chips[i].fondo.gameObject.SetActive(visible);
            if (!visible) continue;

            int actual = flujo.IndiceActividadActual;
            _chips[i].fondo.color = i < actual ? colorCompletada : i == actual ? colorActual : colorPendiente;
            _chips[i].texto.text = $"{i + 1}. {flujo.ActividadesSesion[i]}";
        }
    }

    private RectTransform NuevoHijo(string nombre, Transform padre, float alto)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(LayoutElement));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        go.GetComponent<LayoutElement>().preferredHeight = alto;
        return rt;
    }

    private TextMeshProUGUI NuevoTexto(string nombre, Transform padre, float alto, float tamano, FontStyles estilo)
    {
        var rt = NuevoHijo(nombre, padre, alto);
        var texto = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_fuente != null) texto.font = _fuente;
        texto.fontSize = tamano;
        texto.fontStyle = estilo;
        texto.color = Color.white;
        texto.alignment = TextAlignmentOptions.Center;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.raycastTarget = false;
        return texto;
    }
}
