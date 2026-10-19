using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*Agrega por código una etiqueta encima de un campo del formulario (InputField o Dropdown) y, opcionalmente,
 * cambia el texto de ayuda (placeholder) que viene por defecto ("Enter text..."). La etiqueta se crea como hija
 * del campo, así se mueve y se oculta junto con él (por ejemplo, el campo de contraseña que solo ve un Administrador).*/
public static class EtiquetaCampoUI
{
    private const string NombreEtiqueta = "EtiquetaCampo";
    private const float AltoEtiqueta = 28f;
    private const float SeparacionEtiqueta = 2f;
    private const float TamanoFuenteEtiqueta = 22f;

    //El texto dentro de los campos ocupa este porcentaje del alto del campo (antes venía en 14, muy pequeño).
    private const float ProporcionFuenteCampo = 0.6f;
    private const float FuenteCampoMinima = 18f;
    private const float FuenteCampoMaxima = 28f;
    private const float MargenHorizontalCampo = 8f;
    private const float MargenVerticalCampo = 2f;

    //Alto de cada opción de un desplegable abierto, en proporción al tamaño de la letra.
    private const float AltoOpcionPorTamano = 1.5f;
    private const float MargenContenidoOpciones = 8f;

    public static void Etiquetar(TMP_InputField campo, string etiqueta, string ayuda = null)
    {
        if (campo == null) return;

        CrearEtiqueta((RectTransform)campo.transform, etiqueta, campo.textComponent != null ? campo.textComponent.font : null);

        if (ayuda != null && campo.placeholder is TMP_Text placeholder)
            placeholder.text = ayuda;

        //Se reducen los márgenes internos que trae Unity por defecto, para que el texto grande quepa en campos bajitos.
        if (campo.textViewport != null)
            AjustarMargenes(campo.textViewport, MargenHorizontalCampo, MargenHorizontalCampo);

        float tamano = TamanoFuenteCampo((RectTransform)campo.transform);
        campo.pointSize = tamano;
        AjustarTextoCampo(campo.textComponent, tamano);
        AjustarTextoCampo(campo.placeholder as TMP_Text, tamano);
    }

    public static void Etiquetar(TMP_Dropdown campo, string etiqueta)
    {
        if (campo == null) return;
        CrearEtiqueta((RectTransform)campo.transform, etiqueta, campo.captionText != null ? campo.captionText.font : null);

        float tamano = TamanoFuenteCampo((RectTransform)campo.transform);
        if (campo.captionText != null)
        {
            //A la derecha se deja espacio para la flecha del desplegable.
            AjustarMargenes(campo.captionText.rectTransform, MargenHorizontalCampo, 30f);
            AjustarTextoCampo(campo.captionText, tamano);
        }
        if (campo.itemText != null)
        {
            campo.itemText.fontSize = tamano;
            campo.itemText.verticalAlignment = VerticalAlignmentOptions.Middle;
            AjustarAltoOpciones(campo, tamano);
        }
    }

    //Con la letra más grande, las opciones de la lista desplegada quedaban montadas unas sobre otras:
    //cada opción (Item de la plantilla) medía 20 de alto. Se agranda para que quepa el texto.
    private static void AjustarAltoOpciones(TMP_Dropdown campo, float tamano)
    {
        var opcion = campo.itemText.GetComponentInParent<Toggle>(true);
        if (opcion == null) return;

        float alto = Mathf.Ceil(tamano * AltoOpcionPorTamano);
        var rtOpcion = (RectTransform)opcion.transform;
        rtOpcion.sizeDelta = new Vector2(rtOpcion.sizeDelta.x, alto);
        //El contenido de la plantilla debe medir al menos una opción (TMP_Dropdown calcula el resto al abrirse).
        if (rtOpcion.parent is RectTransform contenido)
            contenido.sizeDelta = new Vector2(contenido.sizeDelta.x, alto + MargenContenidoOpciones);
    }

    private static float TamanoFuenteCampo(RectTransform campo)
    {
        return Mathf.Clamp(campo.rect.height * ProporcionFuenteCampo, FuenteCampoMinima, FuenteCampoMaxima);
    }

    private static void AjustarTextoCampo(TMP_Text texto, float tamano)
    {
        if (texto == null) return;
        texto.enableAutoSizing = false;
        texto.fontSize = tamano;
        texto.verticalAlignment = VerticalAlignmentOptions.Middle;
    }

    //Deja el elemento estirado sobre todo el campo, con márgenes pequeños.
    private static void AjustarMargenes(RectTransform rt, float izquierda, float derecha)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(izquierda, MargenVerticalCampo);
        rt.offsetMax = new Vector2(-derecha, -MargenVerticalCampo);
    }

    //Se llama desde Awake, pero se revisa si la etiqueta ya existe por si se agregó a mano en la escena.
    private static void CrearEtiqueta(RectTransform campo, string texto, TMP_FontAsset fuente)
    {
        Transform existente = campo.Find(NombreEtiqueta);
        TextMeshProUGUI etiqueta = existente != null ? existente.GetComponent<TextMeshProUGUI>() : null;

        if (etiqueta == null)
        {
            var go = new GameObject(NombreEtiqueta, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = campo.gameObject.layer;
            go.transform.SetParent(campo, false);
            etiqueta = go.GetComponent<TextMeshProUGUI>();
        }

        //Anclada al borde superior del campo, con el mismo ancho que él, para que quede justo encima.
        var rt = etiqueta.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(0f, SeparacionEtiqueta);
        rt.sizeDelta = new Vector2(0f, AltoEtiqueta);

        if (fuente != null) etiqueta.font = fuente;
        etiqueta.text = texto;
        etiqueta.fontSize = TamanoFuenteEtiqueta;
        etiqueta.fontStyle = FontStyles.Bold;
        etiqueta.color = Color.white;
        etiqueta.alignment = TextAlignmentOptions.BottomLeft;
        etiqueta.textWrappingMode = TextWrappingModes.NoWrap;
        etiqueta.overflowMode = TextOverflowModes.Overflow;
        //Que la etiqueta no le robe los clics al campo.
        etiqueta.raycastTarget = false;
    }
}
