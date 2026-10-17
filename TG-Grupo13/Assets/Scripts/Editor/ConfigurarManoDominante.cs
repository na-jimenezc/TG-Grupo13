using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

/*Herramienta de editor que deja lista la escena Core para perfiles zurdos, sin tener que mover nada a mano:
 * 1. Rig: pasa lo que estaba fijo en las manos a dos objetos hijos que LateralidadJugador cambia de mano.
 *    - RolManoSecundaria (bajo LeftHandAnchor): ManoSecundaria, ManoIzquierdaController, su collider/Rigidbody
 *      y los hijos PuntoAnclaje, PuntoApilado y HombroAcumulador.
 *    - RolManoDominante (bajo RightHandAnchor): el tag ManoDominante y su collider/Rigidbody.
 *    Y agrega LateralidadJugador al Jugador con sus referencias.
 * 2. Formularios: agrega el desplegable "Mano dominante" en Crear perfil y Actualizar perfil (copiando el de Rol)
 *    y la etiqueta en Ver info.
 * Se puede correr varias veces: lo que ya está hecho se salta.*/
public static class ConfigurarManoDominante
{
    private const string NombreRolSecundaria = "RolManoSecundaria";
    private const string NombreRolDominante = "RolManoDominante";
    private const string TagDominante = "ManoDominante";

    [MenuItem("TG-Grupo13/Configurar mano dominante en Core")]
    public static void Configurar()
    {
        Scene core = BuscarEscenaCore();
        if (!core.IsValid())
        {
            EditorUtility.DisplayDialog("Core no está abierta", "Abre la escena Core.unity y vuelve a ejecutar este menú.", "OK");
            return;
        }

        var log = new StringBuilder();
        ConfigurarRig(core, log);
        ConfigurarFormularios(core, log);

        EditorSceneManager.MarkSceneDirty(core);
        EditorUtility.DisplayDialog("Mano dominante", log + "\nRevisa la posición de los desplegables nuevos y guarda la escena Core (Ctrl+S).", "OK");
    }

    // ---------- Rig ----------

    private static void ConfigurarRig(Scene core, StringBuilder log)
    {
        Transform izquierda = Buscar(core, "LeftHandAnchor");
        Transform derecha = Buscar(core, "RightHandAnchor");
        if (izquierda == null || derecha == null)
        {
            log.AppendLine("✗ No se encontraron LeftHandAnchor / RightHandAnchor.");
            return;
        }

        Transform rolSecundaria = BuscarHijo(izquierda, derecha, NombreRolSecundaria);
        if (rolSecundaria == null)
        {
            rolSecundaria = CrearRol(NombreRolSecundaria, izquierda);
            MoverFisica(izquierda.gameObject, rolSecundaria.gameObject);
            MoverComponente<ManoSecundaria>(izquierda.gameObject, rolSecundaria.gameObject);
            MoverComponente<ManoIzquierdaController>(izquierda.gameObject, rolSecundaria.gameObject);
            foreach (var nombre in new[] { "PuntoAnclaje", "PuntoApilado", "HombroAcumulador" })
            {
                Transform hijo = izquierda.Find(nombre);
                if (hijo != null) Undo.SetTransformParent(hijo, rolSecundaria, "Mover " + nombre);
            }
            log.AppendLine("✓ Creado RolManoSecundaria en LeftHandAnchor.");
        }
        else log.AppendLine("• RolManoSecundaria ya existía.");

        Transform rolDominante = BuscarHijo(izquierda, derecha, NombreRolDominante);
        if (rolDominante == null)
        {
            rolDominante = CrearRol(NombreRolDominante, derecha);
            MoverFisica(derecha.gameObject, rolDominante.gameObject);
            rolDominante.gameObject.tag = TagDominante;
            if (derecha.CompareTag(TagDominante))
            {
                Undo.RecordObject(derecha.gameObject, "Quitar tag");
                derecha.gameObject.tag = "Untagged";
            }
            log.AppendLine("✓ Creado RolManoDominante en RightHandAnchor (con el tag ManoDominante).");
        }
        else log.AppendLine("• RolManoDominante ya existía.");

        Transform jugador = Buscar(core, "Jugador");
        if (jugador == null)
        {
            log.AppendLine("✗ No se encontró el objeto Jugador para agregar LateralidadJugador.");
            return;
        }
        var lateralidad = jugador.GetComponent<LateralidadJugador>();
        if (lateralidad == null) lateralidad = Undo.AddComponent<LateralidadJugador>(jugador.gameObject);

        var holster = Object.FindObjectsByType<PuntoHolsterTijerasRef>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var so = new SerializedObject(lateralidad);
        so.FindProperty("manoIzquierda").objectReferenceValue = izquierda;
        so.FindProperty("manoDerecha").objectReferenceValue = derecha;
        so.FindProperty("rolManoDominante").objectReferenceValue = rolDominante;
        so.FindProperty("rolManoSecundaria").objectReferenceValue = rolSecundaria;
        if (holster.Length > 0) so.FindProperty("puntoHolster").objectReferenceValue = holster[0].transform;
        so.ApplyModifiedProperties();
        log.AppendLine("✓ LateralidadJugador conectado en Jugador" + (holster.Length > 0 ? "." : " (sin holster: no se encontró PuntoHolsterTijeras)."));
    }

    private static Transform CrearRol(string nombre, Transform mano)
    {
        var go = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        go.layer = mano.gameObject.layer;
        Undo.SetTransformParent(go.transform, mano, "Crear " + nombre);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    //El collider (trigger) y el Rigidbody kinemático de la mano son los que detectan baldes, tallos, etc.,
    //así que se mueven al rol para que viajen con él al cambiar de mano.
    private static void MoverFisica(GameObject origen, GameObject destino)
    {
        foreach (var col in origen.GetComponents<Collider>()) MoverComponente(col, destino);
        var rb = origen.GetComponent<Rigidbody>();
        if (rb != null) MoverComponente(rb, destino);
    }

    private static void MoverComponente<T>(GameObject origen, GameObject destino) where T : Component
    {
        var comp = origen.GetComponent<T>();
        if (comp != null) MoverComponente(comp, destino);
    }

    //Copia el componente con todos sus valores y referencias, y borra el original
    private static void MoverComponente(Component comp, GameObject destino)
    {
        ComponentUtility.CopyComponent(comp);
        ComponentUtility.PasteComponentAsNew(destino);
        Undo.DestroyObjectImmediate(comp);
    }

    // ---------- Formularios ----------

    private static void ConfigurarFormularios(Scene core, StringBuilder log)
    {
        var crear = BuscarComponente<CrearPerfilUI>(core);
        if (crear != null) AgregarDropdown(crear, log, "Crear perfil");
        else log.AppendLine("✗ No se encontró CrearPerfilUI.");

        var actualizar = BuscarComponente<ActualizarPerfilUI>(core);
        if (actualizar != null) AgregarDropdown(actualizar, log, "Actualizar perfil");
        else log.AppendLine("✗ No se encontró ActualizarPerfilUI.");

        var verInfo = BuscarComponente<VerInfoUI>(core);
        if (verInfo != null) AgregarEtiquetaVerInfo(verInfo, log);
        else log.AppendLine("✗ No se encontró VerInfoUI.");
    }

    //Copia el desplegable de Rol, lo deja a su lado (Rol se corre a la izquierda y Mano queda a la derecha)
    private static void AgregarDropdown(MonoBehaviour formulario, StringBuilder log, string nombreFormulario)
    {
        var so = new SerializedObject(formulario);
        var propMano = so.FindProperty("manoDropdown");
        if (propMano.objectReferenceValue != null)
        {
            log.AppendLine($"• {nombreFormulario}: ya tenía desplegable de mano.");
            return;
        }
        var rol = so.FindProperty("rolDropdown").objectReferenceValue as TMP_Dropdown;
        if (rol == null)
        {
            log.AppendLine($"✗ {nombreFormulario}: no tiene rolDropdown asignado para copiarlo.");
            return;
        }

        var copia = Object.Instantiate(rol.gameObject, rol.transform.parent);
        Undo.RegisterCreatedObjectUndo(copia, "Crear ManoDropdown");
        copia.name = "ManoDropdown";
        copia.transform.SetSiblingIndex(rol.transform.GetSiblingIndex() + 1);

        var mano = copia.GetComponent<TMP_Dropdown>();
        mano.onValueChanged = new TMP_Dropdown.DropdownEvent();
        mano.ClearOptions();
        mano.AddOptions(new List<string> { "Diestro", "Zurdo" });
        mano.SetValueWithoutNotify(0);

        var rtRol = (RectTransform)rol.transform;
        var rtMano = (RectTransform)copia.transform;
        float desplazamiento = rtRol.rect.width * 0.5f + 15f;
        Undo.RecordObject(rtRol, "Mover RolDropdown");
        Vector2 pos = rtRol.anchoredPosition;
        rtRol.anchoredPosition = pos + Vector2.left * desplazamiento;
        rtMano.anchoredPosition = pos + Vector2.right * desplazamiento;

        propMano.objectReferenceValue = mano;
        so.ApplyModifiedProperties();
        log.AppendLine($"✓ {nombreFormulario}: desplegable 'Mano dominante' agregado junto al de Rol.");
    }

    //En Ver info los datos están en una columna con VerticalLayoutGroup, así que basta con copiar la etiqueta de Rol
    private static void AgregarEtiquetaVerInfo(VerInfoUI verInfo, StringBuilder log)
    {
        var so = new SerializedObject(verInfo);
        var propMano = so.FindProperty("manoLabel");
        if (propMano.objectReferenceValue != null)
        {
            log.AppendLine("• Ver info: ya tenía etiqueta de mano.");
            return;
        }
        var rol = so.FindProperty("rolLabel").objectReferenceValue as TMP_Text;
        if (rol == null)
        {
            log.AppendLine("✗ Ver info: no tiene rolLabel asignado para copiarlo.");
            return;
        }

        var copia = Object.Instantiate(rol.gameObject, rol.transform.parent);
        Undo.RegisterCreatedObjectUndo(copia, "Crear ManoLabel");
        copia.name = "ManoLabel";
        copia.transform.SetSiblingIndex(rol.transform.GetSiblingIndex() + 1);
        var texto = copia.GetComponent<TMP_Text>();
        texto.text = "Mano dominante: Derecha (diestro)";

        propMano.objectReferenceValue = texto;
        so.ApplyModifiedProperties();
        log.AppendLine("✓ Ver info: etiqueta 'Mano dominante' agregada.");
    }

    // ---------- Búsqueda ----------

    private static Scene BuscarEscenaCore()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene escena = SceneManager.GetSceneAt(i);
            if (escena.isLoaded && escena.name == "Core") return escena;
        }
        return default;
    }

    private static Transform Buscar(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            Transform t = BuscarRecursivo(raiz.transform, nombre);
            if (t != null) return t;
        }
        return null;
    }

    private static Transform BuscarRecursivo(Transform actual, string nombre)
    {
        if (actual.name == nombre) return actual;
        foreach (Transform hijo in actual)
        {
            Transform r = BuscarRecursivo(hijo, nombre);
            if (r != null) return r;
        }
        return null;
    }

    //Un rol puede estar ya en cualquiera de las dos manos
    private static Transform BuscarHijo(Transform a, Transform b, string nombre) => a.Find(nombre) ?? b.Find(nombre);

    private static T BuscarComponente<T>(Scene escena) where T : Component
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            var c = raiz.GetComponentInChildren<T>(true);
            if (c != null) return c;
        }
        return null;
    }
}
