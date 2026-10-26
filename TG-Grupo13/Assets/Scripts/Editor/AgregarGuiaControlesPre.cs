using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*Lleva la guía de controles del tutorial a la escena de actividad, para que se muestre en el pre.
 * Por cada actividad (Siembra, Deshierbe, Corte):
 *   - copia el panel de instrucciones del tutorial dentro del UsuarioCanvas de la actividad (Panel_GuiaControles),
 *     con el mismo tamaño físico que tiene en el tutorial;
 *   - crea los puntos de los controles 3D (PuntosGuiaControles) en la misma posición respecto al panel que en el tutorial;
 *   - crea el objeto GuiaControles con el mismo componente del tutorial en modo GuiaControles, con los mismos pasos.
 *     Los objetos de práctica (canasta, pala, ortigas, tijeras...) no se copian: la guía usa los de la actividad.
 * Se puede volver a ejecutar: borra la guía anterior y la crea de nuevo. No modifica las escenas de tutorial.
 * Después hay que revisar en la escena de actividad que el panel no tape el contador y quede a la vista.*/
public static class AgregarGuiaControlesPre
{
    private static readonly string[] Actividades = { "Siembra", "Deshierbe", "Corte" };
    private const string NombreGuia = "GuiaControles";
    private const string NombrePanel = "Panel_GuiaControles";
    private const string NombrePuntos = "PuntosGuiaControles";

    [MenuItem("TG-Grupo13/Agregar guía de controles al pre")]
    public static void Ejecutar()
    {
        bool seguir = EditorUtility.DisplayDialog(
            "Guía de controles en el pre",
            "Esto copia el panel de instrucciones y los controles 3D de cada tutorial a su escena de actividad " +
            "(ActividadSiembra, ActividadDeshierbe, ActividadCorte) y guarda esas tres escenas.\n\n" +
            "Las escenas de tutorial no se modifican. Si ya había una guía, se reemplaza. ¿Continuar?",
            "Sí, agregar", "Cancelar");
        if (!seguir || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string escenaAntes = SceneManager.GetActiveScene().path;
        var informe = new StringBuilder();
        foreach (var actividad in Actividades)
        {
            try { informe.AppendLine(Agregar(actividad)); }
            catch (System.Exception e)
            {
                informe.AppendLine($"{actividad}: ERROR {e.Message}");
                Debug.LogException(e);
            }
        }
        if (!string.IsNullOrEmpty(escenaAntes)) EditorSceneManager.OpenScene(escenaAntes, OpenSceneMode.Single);

        informe.AppendLine("\nAbra cada escena de actividad y revise que Panel_GuiaControles (dentro de UsuarioCanvas) " +
                           "quede a la vista y no tape el contador; si hace falta, muévalo. Los puntos de los controles " +
                           "(PuntosGuiaControles) quedaron junto al panel: muévalos con él.");
        Debug.Log("[Guía de controles]\n" + informe);
        EditorUtility.DisplayDialog("Guía de controles en el pre", informe.ToString(), "OK");
    }

    private static string Agregar(string actividad)
    {
        var escenaAct = EditorSceneManager.OpenScene($"Assets/Escenas/{actividad}/Actividad{actividad}.unity", OpenSceneMode.Single);
        var escenaTut = EditorSceneManager.OpenScene($"Assets/Escenas/{actividad}/Tutorial{actividad}.unity", OpenSceneMode.Additive);
        try
        {
            var tutorial = Buscar<TutorialManagerBase>(escenaTut);
            if (tutorial == null) return $"{actividad}: no se encontró el tutorial en la escena de tutorial.";
            var soTut = new SerializedObject(tutorial);

            var texto = soTut.FindProperty("textoInstruccion").objectReferenceValue as TMP_Text;
            if (texto == null) return $"{actividad}: el tutorial no tiene texto de instrucción asignado.";
            Transform canvasTut = texto.canvas != null ? texto.canvas.rootCanvas.transform : null;
            Transform panelTut = HijoDirectoDe(canvasTut, texto.transform);
            Transform canvasAct = BuscarPorNombre(escenaAct, "UsuarioCanvas");
            if (canvasTut == null || panelTut == null || canvasAct == null)
                return $"{actividad}: no se encontró el panel del tutorial o el UsuarioCanvas de la actividad.";

            //Si ya había una guía, se quita para crearla de nuevo.
            foreach (var raiz in escenaAct.GetRootGameObjects())
                if (raiz.name == NombreGuia || raiz.name == NombrePuntos) Object.DestroyImmediate(raiz);
            var panelViejo = canvasAct.Find(NombrePanel);
            if (panelViejo != null) Object.DestroyImmediate(panelViejo.gameObject);

            //Panel: mismos valores locales que en el tutorial, con el mismo tamaño físico (los canvas tienen escalas distintas).
            var panel = Object.Instantiate(panelTut.gameObject);
            SceneManager.MoveGameObjectToScene(panel, escenaAct);
            panel.name = NombrePanel;
            panel.transform.SetParent(canvasAct, false);
            CopiarLocal(panelTut, panel.transform);
            var contador = canvasAct.Find("Panel_Contador") as RectTransform;
            if (contador != null && panel.transform is RectTransform rp && canvasAct is RectTransform rc)
            {
                //Como en Corte (el único tutorial que ya tenía contador e instrucciones juntos): la guía justo debajo
                //del contador de la actividad, a su misma escala, con un margen de 40 unidades.
                rp.localScale = contador.localScale;
                rp.anchoredPosition = contador.anchoredPosition - new Vector2(0f, rc.rect.height * contador.localScale.y + 40f);
            }
            else
            {
                //Sin contador: el mismo tamaño físico que en el tutorial (los canvas pueden tener escalas distintas).
                float factor = panelTut.lossyScale.x / Mathf.Max(1e-6f, panel.transform.lossyScale.x);
                panel.transform.localScale *= factor;
            }
            panel.SetActive(true);

            //Puntos de los controles 3D: a los lados del panel de la guía (el primero, control izquierdo, a la izquierda;
            //el segundo, derecho, a la derecha), un poco hacia la persona. En los tutoriales los controles están cerca de
            //la persona y no del panel, así que copiar su posición los dejaba lejos.
            var raizPuntos = new GameObject(NombrePuntos);
            SceneManager.MoveGameObjectToScene(raizPuntos, escenaAct);
            var puntosTut = soTut.FindProperty("puntosControles");
            var puntosNuevos = new List<Transform>();
            var esquinas = new Vector3[4];
            ((RectTransform)panel.transform).GetWorldCorners(esquinas);
            Vector3 centro = (esquinas[0] + esquinas[2]) * 0.5f;
            float mitadAncho = Vector3.Distance(esquinas[0], esquinas[3]) * 0.5f;
            Vector3 derecha = (esquinas[3] - esquinas[0]).normalized;
            Vector3 haciaPersona = -panel.transform.forward;
            for (int i = 0; i < puntosTut.arraySize; i++)
            {
                var p = puntosTut.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (p == null) { puntosNuevos.Add(null); continue; }
                var copia = new GameObject(p.name).transform;
                copia.SetParent(raizPuntos.transform, false);
                float lado = puntosTut.arraySize == 1 ? 1f : (i == 0 ? -1f : 1f);
                copia.position = centro + derecha * lado * (mitadAncho + 0.12f) + haciaPersona * 0.08f;
                copia.rotation = panel.transform.rotation * (Quaternion.Inverse(panelTut.rotation) * p.rotation);
                copia.localScale = p.lossyScale;
                puntosNuevos.Add(copia);
            }

            //La guía: el mismo componente del tutorial, en modo GuiaControles.
            var guia = new GameObject(NombreGuia);
            SceneManager.MoveGameObjectToScene(guia, escenaAct);
            var m = (TutorialManagerBase)guia.AddComponent(tutorial.GetType());
            EditorUtility.CopySerialized(tutorial, m);
            var so = new SerializedObject(m);

            //Nada puede apuntar a la escena de tutorial (los objetos de práctica se buscan en la actividad al empezar).
            var it = so.GetIterator();
            while (it.Next(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && EsDeEscena(it.objectReferenceValue, escenaTut))
                    it.objectReferenceValue = null;
            foreach (var arreglo in new[] { "slotsPractica", "ortigasPractica" })
            {
                var prop = so.FindProperty(arreglo);
                if (prop != null) prop.arraySize = 0;
            }

            so.FindProperty("modo").enumValueIndex = (int)ModoTutorial.GuiaControles;
            so.FindProperty("textoInstruccion").objectReferenceValue = Equivalente(texto, panelTut, panel.transform, canvasTut, canvasAct);
            var contadorTexto = soTut.FindProperty("contadorText").objectReferenceValue as Component;
            so.FindProperty("contadorText").objectReferenceValue = Equivalente(contadorTexto, panelTut, panel.transform, canvasTut, canvasAct);

            var iconosTut = soTut.FindProperty("iconosBotonUI");
            var iconos = so.FindProperty("iconosBotonUI");
            iconos.arraySize = iconosTut.arraySize;
            for (int i = 0; i < iconosTut.arraySize; i++)
                iconos.GetArrayElementAtIndex(i).objectReferenceValue =
                    Equivalente(iconosTut.GetArrayElementAtIndex(i).objectReferenceValue as Image, panelTut, panel.transform, canvasTut, canvasAct);

            so.FindProperty("puntosAparicionMuñeco").arraySize = 0;
            var puntos = so.FindProperty("puntosControles");
            puntos.arraySize = puntosNuevos.Count;
            for (int i = 0; i < puntosNuevos.Count; i++) puntos.GetArrayElementAtIndex(i).objectReferenceValue = puntosNuevos[i];

            var ocultar = so.FindProperty("ocultarSinGuia");
            ocultar.arraySize = 2;
            ocultar.GetArrayElementAtIndex(0).objectReferenceValue = panel;
            ocultar.GetArrayElementAtIndex(1).objectReferenceValue = raizPuntos;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(escenaAct);
            EditorSceneManager.SaveScene(escenaAct);
            string captura = Capturar(actividad, m);
            return $"{actividad}: guía agregada ({puntosNuevos.Count} puntos de controles). Captura: {captura}";
        }
        finally
        {
            EditorSceneManager.CloseScene(escenaTut, true);
        }
    }

    //Revisar sin visor: abre cada escena de actividad y guarda una imagen del contador y de la guía (con los controles
    //del primer paso), tal como se verían de frente. Las imágenes quedan en la carpeta temporal de Windows.
    [MenuItem("TG-Grupo13/Capturar guía de controles (revisar sin visor)")]
    public static void CapturarGuias()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string escenaAntes = SceneManager.GetActiveScene().path;
        var informe = new StringBuilder();
        foreach (var actividad in Actividades)
        {
            var escena = EditorSceneManager.OpenScene($"Assets/Escenas/{actividad}/Actividad{actividad}.unity", OpenSceneMode.Single);
            var guia = Buscar<TutorialManagerBase>(escena);
            informe.AppendLine(guia == null ? $"{actividad}: no tiene guía (use Agregar guía de controles al pre)." : $"{actividad}: {Capturar(actividad, guia)}");
        }
        if (!string.IsNullOrEmpty(escenaAntes)) EditorSceneManager.OpenScene(escenaAntes, OpenSceneMode.Single);
        Debug.Log("[Guía de controles] Capturas\n" + informe);
        EditorUtility.DisplayDialog("Capturas de la guía", informe.ToString(), "OK");
    }

    private static string Capturar(string actividad, TutorialManagerBase guia)
    {
        var so = new SerializedObject(guia);
        var ocultar = so.FindProperty("ocultarSinGuia");
        if (ocultar == null || ocultar.arraySize < 2) return "sin panel para capturar";
        var panel = ocultar.GetArrayElementAtIndex(0).objectReferenceValue as GameObject;
        if (panel == null || !(panel.transform is RectTransform rPanel)) return "sin panel para capturar";

        //Lo que se pone solo para la foto se quita al final (la escena ya quedó guardada antes).
        var temporales = new List<Object>();
        var texto = so.FindProperty("textoInstruccion").objectReferenceValue as TMP_Text;
        string textoAntes = texto != null ? texto.text : null;
        var pasos = so.FindProperty("pasos");
        var paso = pasos != null && pasos.arraySize > 0 ? pasos.GetArrayElementAtIndex(0).objectReferenceValue as TutorialStepData : null;
        if (texto != null && paso != null) texto.text = paso.instruccion;

        var limites = Limites(rPanel);
        var contador = rPanel.parent != null ? rPanel.parent.Find("Panel_Contador") as RectTransform : null;
        if (contador != null) limites.Encapsulate(Limites(contador));
        var puntos = so.FindProperty("puntosControles");
        for (int i = 0; paso != null && paso.prefabsControles != null && i < paso.prefabsControles.Length && i < puntos.arraySize; i++)
        {
            var punto = puntos.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (punto == null || paso.prefabsControles[i] == null) continue;
            var control = Object.Instantiate(paso.prefabsControles[i], punto, false);
            temporales.Add(control);
            //Igual que en el juego (TutorialManagerBase.CentrarEnPunto): el centro visual del modelo sobre el punto.
            var rs = control.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                control.transform.position += punto.position - b.center;
            }
            foreach (var r in control.GetComponentsInChildren<Renderer>()) limites.Encapsulate(r.bounds);
        }

        var camGo = new GameObject("CamaraCaptura");
        temporales.Add(camGo);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;
        Vector3 frente = rPanel.forward;
        float tamano = Mathf.Max(limites.size.x, limites.size.y);
        cam.transform.rotation = Quaternion.LookRotation(frente, Vector3.up);
        cam.transform.position = limites.center - frente * (tamano * 0.65f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) + 0.2f);

        const int ancho = 1600, alto = 900;
        var rt = new RenderTexture(ancho, alto, 24);
        cam.targetTexture = rt;
        cam.Render();
        var anterior = RenderTexture.active;
        RenderTexture.active = rt;
        var imagen = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
        imagen.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
        imagen.Apply();
        RenderTexture.active = anterior;
        cam.targetTexture = null;

        string carpeta = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TG-Grupo13-capturas");
        System.IO.Directory.CreateDirectory(carpeta);
        string ruta = System.IO.Path.Combine(carpeta, $"Guia_{actividad}.png");
        System.IO.File.WriteAllBytes(ruta, imagen.EncodeToPNG());

        Object.DestroyImmediate(imagen);
        rt.Release();
        Object.DestroyImmediate(rt);
        foreach (var t in temporales) if (t != null) Object.DestroyImmediate(t);
        if (texto != null) texto.text = textoAntes;
        return ruta;
    }

    private static Bounds Limites(RectTransform r)
    {
        var esquinas = new Vector3[4];
        r.GetWorldCorners(esquinas);
        var b = new Bounds(esquinas[0], Vector3.zero);
        for (int i = 1; i < 4; i++) b.Encapsulate(esquinas[i]);
        return b;
    }

    private static T Buscar<T>(Scene escena) where T : Component
    {
        foreach (var raiz in escena.GetRootGameObjects())
        {
            var c = raiz.GetComponentInChildren<T>(true);
            if (c != null) return c;
        }
        return null;
    }

    private static Transform BuscarPorNombre(Scene escena, string nombre)
    {
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t;
        return null;
    }

    //El hijo directo de 'padre' que contiene a 'descendiente' (el panel del canvas que tiene el texto).
    private static Transform HijoDirectoDe(Transform padre, Transform descendiente)
    {
        if (padre == null) return null;
        var t = descendiente;
        while (t != null && t.parent != padre) t = t.parent;
        return t;
    }

    private static void CopiarLocal(Transform origen, Transform destino)
    {
        if (origen is RectTransform ro && destino is RectTransform rd)
        {
            rd.anchorMin = ro.anchorMin;
            rd.anchorMax = ro.anchorMax;
            rd.pivot = ro.pivot;
            rd.sizeDelta = ro.sizeDelta;
            rd.anchoredPosition3D = ro.anchoredPosition3D;
        }
        else destino.localPosition = origen.localPosition;
        destino.localRotation = origen.localRotation;
        destino.localScale = origen.localScale;
    }

    //El componente equivalente en la copia: si estaba dentro del panel copiado, el de la copia; si estaba en otra parte del
    //canvas del tutorial (p. ej. un contador aparte), el que tenga la misma ruta en el canvas de la actividad.
    private static Object Equivalente(Component original, Transform panelTut, Transform panelNuevo, Transform canvasTut, Transform canvasAct)
    {
        if (original == null) return null;
        Transform destino = null;
        if (original.transform == panelTut) destino = panelNuevo;
        else if (original.transform.IsChildOf(panelTut))
            destino = panelNuevo.Find(AnimationUtility.CalculateTransformPath(original.transform, panelTut));
        else if (original.transform.IsChildOf(canvasTut))
            destino = canvasAct.Find(AnimationUtility.CalculateTransformPath(original.transform, canvasTut));
        return destino != null ? destino.GetComponent(original.GetType()) : null;
    }

    private static bool EsDeEscena(Object obj, Scene escena)
    {
        if (obj is GameObject go) return go.scene == escena;
        if (obj is Component c) return c != null && c.gameObject.scene == escena;
        return false;
    }
}
