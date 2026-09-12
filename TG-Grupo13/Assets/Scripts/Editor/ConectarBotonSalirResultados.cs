using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;

/*Herramienta de editor para conectar por código el botón "SalirButton" de la Pantalla de Resultados
 * con BotonSalirResultadosUI.OnSalirPresionado() (que vive en el propio botón y llama a
 * GameFlowManager.Instance por código), sin depender de arrastrar objetos en el Inspector ni de
 * referencias entre escenas distintas.*/
public static class ConectarBotonSalirResultados
{
    [MenuItem("TG-Grupo13/Conectar Boton Salir en Resultados")]
    public static void Conectar()
    {
        Button boton = BuscarEnEscenasCargadas<Button>("SalirButton");

        if (boton == null)
        {
            EditorUtility.DisplayDialog("No encontrado",
                "No se encontró un GameObject llamado 'SalirButton' en ninguna escena cargada.\n\nAsegúrate de tener abierta PantallaResultados.unity.",
                "OK");
            return;
        }

        BotonSalirResultadosUI script = boton.GetComponent<BotonSalirResultadosUI>();
        if (script == null)
            script = Undo.AddComponent<BotonSalirResultadosUI>(boton.gameObject);

        UnityEventTools.AddPersistentListener(boton.onClick, script.OnSalirPresionado);
        EditorUtility.SetDirty(boton);
        EditorSceneManager.MarkSceneDirty(boton.gameObject.scene);

        EditorUtility.DisplayDialog("Listo",
            "Se agregó el script BotonSalirResultadosUI a SalirButton y se conectó su On Click().\n\nGuarda la escena PantallaResultados con clic derecho sobre su nombre en la Hierarchy -> Save Scene.",
            "OK");
    }

    private static T BuscarEnEscenasCargadas<T>(string nombreObjeto) where T : Component
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;

            foreach (GameObject raiz in escena.GetRootGameObjects())
            {
                Transform encontrado = BuscarPorNombreRecursivo(raiz.transform, nombreObjeto);
                if (encontrado != null)
                {
                    T componente = encontrado.GetComponent<T>();
                    if (componente != null) return componente;
                }
            }
        }
        return null;
    }

    private static Transform BuscarPorNombreRecursivo(Transform actual, string nombre)
    {
        if (actual.name == nombre) return actual;
        foreach (Transform hijo in actual)
        {
            Transform resultado = BuscarPorNombreRecursivo(hijo, nombre);
            if (resultado != null) return resultado;
        }
        return null;
    }
}
