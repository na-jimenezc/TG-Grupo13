// ConfigurarJoyconsEnEscena.cs
//
// Herramienta de editor: TG-Grupo13 -> Joy-Cons -> Configurar avatar y calibración.
// Con Core.unity abierta, busca el avatar con CharacterRetargeter (seguimiento de
// Meta) y la cámara del visor, y deja listo un GameObject "Joycons" con:
//   JoyconReceiver, RebaPostureEstimator, JoyconAvatarDriver y JoyconSessionCalibrator,
// con todas las referencias conectadas (la cabeza para REBA = CenterEyeAnchor).
// Se puede correr varias veces: reutiliza lo que ya exista.
//
// Segundo menú: TG-Grupo13 -> Joy-Cons -> Preparar prueba sin visor (escena abierta).
// Para PruebaJoycons: pone un X Bot frente a la cámara como espejo, en postura N,
// movido solo por los Joy-Cons (sin seguimiento de Meta), con calibración manual.

using System.Collections.Generic;
using PujJoycon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ConfigurarJoyconsEnEscena
{
    [MenuItem("TG-Grupo13/Joy-Cons/Configurar avatar y calibración")]
    public static void Configurar()
    {
        if (EstaEnPlay()) return;
        Animator avatar = BuscarAvatar();
        if (avatar == null)
        {
            EditorUtility.DisplayDialog("Avatar no encontrado",
                "No se encontró un avatar Humanoid con CharacterRetargeter en las escenas abiertas.\n\n" +
                "Abra Core.unity y vuelva a intentarlo.", "OK");
            return;
        }

        Scene escena = avatar.gameObject.scene;
        Transform cabeza = BuscarPorNombre(escena, "CenterEyeAnchor");
        if (cabeza == null)
        {
            EditorUtility.DisplayDialog("Cámara no encontrada",
                $"No se encontró 'CenterEyeAnchor' en {escena.name}. Sin ella no se puede medir el cuello " +
                "ni saber hacia dónde mira la persona al calibrar.", "OK");
            return;
        }

        GameObject joycons = BuscarOCrearJoycons(escena);
        var receiver = Asegurar<JoyconReceiver>(joycons);
        var estimator = Asegurar<RebaPostureEstimator>(joycons);
        var driver = Asegurar<JoyconAvatarDriver>(joycons);
        var calibrator = Asegurar<JoyconSessionCalibrator>(joycons);
        ConectarGrabador(joycons, estimator, receiver, calibrator);

        // Cabeza = visor; antebrazos y manos = huesos del avatar (Meta pone las manos en
        // los controles y JoyconAvatarDriver apunta cada antebrazo hacia su mano).
        var rastreados = new Dictionary<BodySegment, Transform>
        {
            { BodySegment.Head, cabeza },
            { BodySegment.ForearmLeft, avatar.GetBoneTransform(HumanBodyBones.LeftLowerArm) },
            { BodySegment.HandLeft, avatar.GetBoneTransform(HumanBodyBones.LeftHand) },
            { BodySegment.ForearmRight, avatar.GetBoneTransform(HumanBodyBones.RightLowerArm) },
            { BodySegment.HandRight, avatar.GetBoneTransform(HumanBodyBones.RightHand) },
        };

        var so = new SerializedObject(estimator);
        so.FindProperty("receiver").objectReferenceValue = receiver;
        so.FindProperty("bodyForwardReference").objectReferenceValue = cabeza;
        so.FindProperty("gravityUpY").floatValue = GravedadVerificada;
        EscribirDistribucionEstandar(so.FindProperty("bindings"), rastreados);
        so.ApplyModifiedProperties();

        so = new SerializedObject(driver);
        so.FindProperty("estimator").objectReferenceValue = estimator;
        so.FindProperty("avatar").objectReferenceValue = avatar;
        so.FindProperty("headReference").objectReferenceValue = cabeza;
        so.FindProperty("headingFromTracking").boolValue = true;
        so.FindProperty("keepHeadFromTracking").boolValue = true;
        so.FindProperty("keepHandsFromTracking").boolValue = true;
        so.FindProperty("aimForearmsAtTrackedHands").boolValue = true;
        so.ApplyModifiedProperties();

        so = new SerializedObject(calibrator);
        so.FindProperty("estimator").objectReferenceValue = estimator;
        so.FindProperty("receiver").objectReferenceValue = receiver;
        so.FindProperty("headReference").objectReferenceValue = cabeza;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(escena);
        Selection.activeGameObject = joycons;

        EditorUtility.DisplayDialog("Listo",
            $"Se configuró 'Joycons' en {escena.name}:\n" +
            $"  - avatar: {avatar.name}\n" +
            $"  - cabeza (visor): {cabeza.name}\n" +
            "  - antebrazos y manos: desde los controles (a través del avatar)\n" +
            "  - 8 Joy-Cons con la distribución estándar:\n" + DistribucionTexto + "\n\n" +
            "Falta:\n" +
            "  1. JoyconReceiver: IP del servidor 1 y, en Additional Servers, la del servidor 2 con Sensor Offset 4.\n" +
            $"  2. Guardar la escena {escena.name} (Ctrl+S).", "OK");
    }

    private const string RutaXBot = "Assets/Arte/Modelos/X Bot.fbx";

    [MenuItem("TG-Grupo13/Joy-Cons/Preparar prueba sin visor (escena abierta)")]
    public static void PrepararPruebaSinVisor()
    {
        if (EstaEnPlay()) return;
        Scene escena = SceneManager.GetActiveScene();
        if (escena.name == "Core")
        {
            EditorUtility.DisplayDialog("Escena equivocada",
                "Esta prueba es para una escena aparte (ej. PruebaJoycons), no para Core.", "OK");
            return;
        }

        // Avatar de prueba: un X Bot sin seguimiento de Meta, mirando a la cámara (como un espejo).
        Animator avatar = null;
        foreach (var pose in Object.FindObjectsByType<JoyconPruebaPosturaN>(FindObjectsInactive.Include))
        {
            if (pose.gameObject.scene == escena) avatar = pose.GetComponent<Animator>();
        }
        if (avatar == null)
        {
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaXBot);
            if (modelo == null)
            {
                EditorUtility.DisplayDialog("Modelo no encontrado", $"No se encontró {RutaXBot}.", "OK");
                return;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo, escena);
            go.name = "X Bot (prueba Joy-Cons)";
            go.transform.SetPositionAndRotation(new Vector3(-2.5f, -1f, 0f), Quaternion.Euler(0f, 180f, 0f));
            Undo.RegisterCreatedObjectUndo(go, "Crear avatar de prueba");
            avatar = go.GetComponent<Animator>();
            if (avatar == null || !avatar.isHuman)
            {
                EditorUtility.DisplayDialog("Avatar inválido", "El X Bot no tiene un Animator Humanoid.", "OK");
                return;
            }
        }

        GameObject joycons = BuscarOCrearJoycons(escena);
        var receiver = Asegurar<JoyconReceiver>(joycons);
        var estimator = Asegurar<RebaPostureEstimator>(joycons);
        var driver = Asegurar<JoyconAvatarDriver>(joycons);
        var calibrator = Asegurar<JoyconSessionCalibrator>(joycons);
        ConectarGrabador(joycons, estimator, receiver, calibrator);
        var postura = Asegurar<JoyconPruebaPosturaN>(avatar.gameObject);
        Transform camara = Camera.main != null ? Camera.main.transform : null;

        // Sin visor ni controles: solo los 8 Joy-Cons (cabeza, antebrazos y manos sin fuente).
        var so = new SerializedObject(estimator);
        so.FindProperty("receiver").objectReferenceValue = receiver;
        so.FindProperty("bodyForwardReference").objectReferenceValue = null;
        so.FindProperty("gravityUpY").floatValue = GravedadVerificada;
        EscribirDistribucionEstandar(so.FindProperty("bindings"), new Dictionary<BodySegment, Transform>());
        so.ApplyModifiedProperties();

        so = new SerializedObject(driver);
        so.FindProperty("estimator").objectReferenceValue = estimator;
        so.FindProperty("avatar").objectReferenceValue = avatar;
        so.FindProperty("headReference").objectReferenceValue = null; // "adelante" = hacia donde mira el avatar
        so.FindProperty("headingFromTracking").boolValue = false;      // no hay Meta: el rumbo lo dan los Joy-Cons
        so.FindProperty("keepHeadFromTracking").boolValue = false;
        so.FindProperty("keepHandsFromTracking").boolValue = false;
        so.FindProperty("aimForearmsAtTrackedHands").boolValue = false; // nadie mueve las manos
        so.ApplyModifiedProperties();

        so = new SerializedObject(calibrator);
        so.FindProperty("estimator").objectReferenceValue = estimator;
        so.FindProperty("receiver").objectReferenceValue = receiver;
        so.FindProperty("headReference").objectReferenceValue = camara;
        so.FindProperty("calibrateOnSessionStart").boolValue = false; // no hay GameFlowManager: se calibra a mano
        so.ApplyModifiedProperties();

        so = new SerializedObject(postura);
        so.FindProperty("avatar").objectReferenceValue = avatar;
        so.FindProperty("estimator").objectReferenceValue = estimator;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(escena);
        Selection.activeGameObject = joycons;

        EditorUtility.DisplayDialog("Prueba sin visor lista",
            $"En {escena.name}:\n" +
            $"  - avatar de prueba: {avatar.name} (en postura N al dar Play)\n" +
            "  - Joycons: receptor, estimador REBA, avatar y calibración manual\n" +
            "  - 8 Joy-Cons con la distribución estándar:\n" + DistribucionTexto + "\n\n" +
            "Antes de dar Play:\n" +
            "  1. JoyconReceiver: IP del servidor 1 y, en Additional Servers, la del servidor 2 con Sensor Offset 4.\n" +
            "  2. Guardar la escena (Ctrl+S).\n\n" +
            "En Play: menú ⋮ de JoyconSessionCalibrator -> 'Calibrar ahora', postura N 5 s, y moverse.", "OK");
    }

    private static Animator BuscarAvatar()
    {
        var animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include);
        foreach (var a in animators)
        {
            if (!a.isHuman) continue;
            // Por nombre, para no depender del ensamblado del Movement SDK.
            for (Transform t = a.transform; t != null; t = t.parent)
            {
                if (t.GetComponent("CharacterRetargeter") != null) return a;
            }
        }
        return null;
    }

    private static GameObject BuscarOCrearJoycons(Scene escena)
    {
        foreach (var r in Object.FindObjectsByType<JoyconReceiver>(FindObjectsInactive.Include))
        {
            if (r.gameObject.scene == escena) return r.gameObject;
            Debug.LogWarning($"[Joycon] Hay otro JoyconReceiver en la escena {r.gameObject.scene.name}; " +
                             "si ambas escenas se cargan juntas habrá dos clientes.", r);
        }
        var go = new GameObject("Joycons");
        SceneManager.MoveGameObjectToScene(go, escena);
        Undo.RegisterCreatedObjectUndo(go, "Crear Joycons");
        return go;
    }

    private static T Asegurar<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    // Grabador de REBA (CSV por sesión + resumen en el historial del participante).
    private static void ConectarGrabador(GameObject joycons, RebaPostureEstimator estimator,
                                         JoyconReceiver receiver, JoyconSessionCalibrator calibrator)
    {
        var recorder = Asegurar<RebaSessionRecorder>(joycons);
        var so = new SerializedObject(recorder);
        so.FindProperty("estimator").objectReferenceValue = estimator;
        so.FindProperty("receiver").objectReferenceValue = receiver;
        so.FindProperty("calibrator").objectReferenceValue = calibrator;
        so.ApplyModifiedProperties();
    }

    // En Play los cambios a la escena no se pueden guardar y se pierden al salir; además
    // los componentes nuevos arrancan (Awake) antes de que se les conecten las referencias.
    private static bool EstaEnPlay()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return false;
        EditorUtility.DisplayDialog("Salga de Play primero",
            "Esta herramienta modifica la escena y no funciona en Play: los cambios se perderían.\n\n" +
            "Detenga Play (botón ▶ arriba), vuelva a correr la herramienta y guarde con Ctrl+S.", "OK");
        return true;
    }

    // Signo de la gravedad en el mundo del servidor de Joy-Cons, verificado con el tronco
    // (con −1 la inclinación lateral y la torsión salían al revés).
    private const float GravedadVerificada = 1f;

    private const string DistribucionTexto =
        "      PC 1: 0 tronco, 1 brazo izq., 2 muslo izq., 3 pierna izq.\n" +
        "      PC 2: 4 pelvis, 5 brazo der., 6 muslo der., 7 pierna der.";

    // Reescribe la lista con RebaPostureEstimator.StandardLayout(), conservando el
    // mountingEuler que ya se hubiera ajustado para cada parte del cuerpo.
    private static void EscribirDistribucionEstandar(SerializedProperty bindings,
                                                     Dictionary<BodySegment, Transform> rastreados)
    {
        var mountings = new Dictionary<int, Vector3>();
        for (int i = 0; i < bindings.arraySize; i++)
        {
            var b = bindings.GetArrayElementAtIndex(i);
            mountings[b.FindPropertyRelative("segment").enumValueIndex] = b.FindPropertyRelative("mountingEuler").vector3Value;
        }

        var layout = RebaPostureEstimator.StandardLayout();
        bindings.arraySize = layout.Length;
        for (int i = 0; i < layout.Length; i++)
        {
            var b = bindings.GetArrayElementAtIndex(i);
            var segment = layout[i].segment;
            rastreados.TryGetValue(segment, out var t);
            b.FindPropertyRelative("segment").enumValueIndex = (int)segment;
            b.FindPropertyRelative("sensorIndex").intValue = layout[i].sensorIndex;
            b.FindPropertyRelative("trackedTransform").objectReferenceValue = t;
            b.FindPropertyRelative("mountingEuler").vector3Value =
                mountings.TryGetValue((int)segment, out var m) ? m : Vector3.zero;
        }
    }

    private static Transform BuscarPorNombre(Scene escena, string nombre)
    {
        var pendientes = new Stack<Transform>();
        foreach (var raiz in escena.GetRootGameObjects()) pendientes.Push(raiz.transform);
        while (pendientes.Count > 0)
        {
            var t = pendientes.Pop();
            if (t.name == nombre) return t;
            foreach (Transform hijo in t) pendientes.Push(hijo);
        }
        return null;
    }
}
