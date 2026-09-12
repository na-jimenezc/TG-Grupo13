using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/*Herramienta de editor para crear las escenas vacías (con un cubo blanco como referencia de posición del usuario)
 * que todavía faltan en el flujo de la simulación, y registrarlas en Build Settings junto con las que ya existían
 * pero no estaban habilitadas para poder cargarse por nombre desde el GameFlowManager.*/
public static class CrearEscenasFaltantes
{
    private static readonly string[] EscenasACrear =
    {
        "Assets/Escenas/Siembra/TutorialSiembra.unity",
        "Assets/Escenas/Siembra/ActividadSiembra.unity",
        "Assets/Escenas/Deshierbe/TutorialDeshierbe.unity",
        "Assets/Escenas/Corte/TutorialCorte.unity",
        "Assets/Escenas/FlujoGeneral/Continuar.unity",
        "Assets/Escenas/FlujoGeneral/PantallaResultados.unity",
        "Assets/Escenas/FlujoGeneral/SalirSimulacion.unity",
    };

    //Escenas que ya existían en el proyecto y solo necesitan quedar registradas/habilitadas en Build Settings.
    private static readonly string[] EscenasExistentesARegistrar =
    {
        "Assets/Escenas/Deshierbe/ActividadDeshierbe.unity",
        "Assets/Escenas/Corte/ActividadCorte.unity",
    };

    [MenuItem("TG-Grupo13/Crear Escenas Faltantes del Flujo")]
    public static void Ejecutar()
    {
        bool continuar = EditorUtility.DisplayDialog(
            "Crear Escenas Faltantes",
            "Esto creará " + EscenasACrear.Length + " escenas nuevas (cada una con un cubo blanco de referencia) " +
            "y las registrará junto con las escenas de Actividad existentes en Build Settings.\n\n" +
            "No se modifica ninguna escena que ya tengas abierta. ¿Continuar?",
            "Sí, crear",
            "Cancelar");

        if (!continuar) return;

        int creadas = 0;
        foreach (var ruta in EscenasACrear)
        {
            if (CrearEscenaConCuboBlanco(ruta))
                creadas++;
        }

        RegistrarEnBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Listo",
            $"Se crearon {creadas} escenas nuevas y se actualizó Build Settings con las {EscenasACrear.Length + EscenasExistentesARegistrar.Length} escenas del flujo de actividades.",
            "OK");

        Debug.Log("[CrearEscenasFaltantes] Proceso completado.");
    }

    private static bool CrearEscenaConCuboBlanco(string ruta)
    {
        if (File.Exists(ruta))
        {
            Debug.Log($"[CrearEscenasFaltantes] Ya existe, se omite: {ruta}");
            return false;
        }

        string carpeta = Path.GetDirectoryName(ruta)?.Replace("\\", "/");
        if (!string.IsNullOrEmpty(carpeta) && !Directory.Exists(carpeta))
            Directory.CreateDirectory(carpeta);

        //Se crea en modo aditivo para no cerrar ni afectar las escenas que el usuario tenga abiertas en el editor.
        Scene escenaNueva = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(escenaNueva);

        GameObject luz = new GameObject("Luz Direccional");
        Light luzComp = luz.AddComponent<Light>();
        luzComp.type = LightType.Directional;
        luz.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        GameObject cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubo.name = "CuboPlaceholder_PosicionUsuario";
        cubo.transform.position = new Vector3(0f, 0.5f, 0f);

        EditorSceneManager.SaveScene(escenaNueva, ruta);
        EditorSceneManager.CloseScene(escenaNueva, true);

        Debug.Log($"[CrearEscenasFaltantes] Escena creada: {ruta}");
        return true;
    }

    private static void RegistrarEnBuildSettings()
    {
        List<EditorBuildSettingsScene> actuales = EditorBuildSettings.scenes.ToList();

        void Asegurar(string ruta, bool habilitada)
        {
            int idx = actuales.FindIndex(s => s.path == ruta);
            if (idx >= 0)
                actuales[idx] = new EditorBuildSettingsScene(ruta, habilitada);
            else
                actuales.Add(new EditorBuildSettingsScene(ruta, habilitada));
        }

        foreach (var ruta in EscenasACrear)
            Asegurar(ruta, true);

        foreach (var ruta in EscenasExistentesARegistrar)
            Asegurar(ruta, true);

        EditorBuildSettings.scenes = actuales.ToArray();
    }
}
