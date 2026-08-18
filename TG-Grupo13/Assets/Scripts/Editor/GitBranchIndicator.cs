using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Muestra la rama de git actual en la esquina superior izquierda de la Scene View.
/// Lee .git/HEAD directamente: no necesita que git este instalado ni ejecuta procesos.
///
/// UBICACION: este archivo DEBE ir en una carpeta llamada "Editor"
/// (por ejemplo Assets/Scripts/Editor/GitBranchIndicator.cs), si no, no compila la build.
/// </summary>
[InitializeOnLoad]
public static class GitBranchIndicator
{
    // Ramas cuyo nombre contenga alguna de estas palabras se pintan en rojo,
    // como recordatorio de "ojo, estas tocando algo importante".
    static readonly string[] RamasProtegidas = { "main", "master" };

    const double IntervaloRefresco = 2.0; // segundos

    static string _rama = "...";
    static string _gitDir;
    static bool _gitDirBuscado;
    static double _proximoRefresco;

    static GitBranchIndicator()
    {
        Refrescar();
        SceneView.duringSceneGui += DibujarEnSceneView;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < _proximoRefresco) return;
        _proximoRefresco = EditorApplication.timeSinceStartup + IntervaloRefresco;
        Refrescar();
    }

    [MenuItem("Herramientas/Git/Refrescar rama")]
    static void RefrescarManual()
    {
        _gitDirBuscado = false; // fuerza volver a localizar el .git
        _gitDir = null;
        Refrescar();
        Debug.Log("[Git] Rama actual: " + _rama);
    }

    static void Refrescar()
    {
        string anterior = _rama;
        _rama = LeerRama();
        if (anterior != _rama) SceneView.RepaintAll();
    }

    /// <summary>
    /// Sube por el arbol de carpetas desde Assets/ hasta encontrar el .git.
    /// Necesario porque en este repo el proyecto Unity esta en una subcarpeta
    /// (TG-Grupo13/) y el .git vive un nivel mas arriba.
    /// </summary>
    static string BuscarGitDir()
    {
        var dir = new DirectoryInfo(Application.dataPath);

        while (dir != null)
        {
            string candidato = Path.Combine(dir.FullName, ".git");

            // Caso normal: .git es una carpeta.
            if (Directory.Exists(candidato)) return candidato;

            // Caso worktree o submodulo: .git es un archivo con "gitdir: <ruta>".
            if (File.Exists(candidato))
            {
                string contenido = File.ReadAllText(candidato).Trim();
                const string prefijo = "gitdir:";
                if (contenido.StartsWith(prefijo))
                {
                    string ruta = contenido.Substring(prefijo.Length).Trim();
                    if (!Path.IsPathRooted(ruta))
                        ruta = Path.GetFullPath(Path.Combine(dir.FullName, ruta));
                    if (Directory.Exists(ruta)) return ruta;
                }
            }

            dir = dir.Parent;
        }

        return null;
    }

    static string LeerRama()
    {
        try
        {
            if (!_gitDirBuscado)
            {
                _gitDir = BuscarGitDir();
                _gitDirBuscado = true;
            }

            if (_gitDir == null) return "sin repo git";

            string head = Path.Combine(_gitDir, "HEAD");
            if (!File.Exists(head)) return "sin repo git";

            string texto = File.ReadAllText(head).Trim();

            // Formato normal: "ref: refs/heads/limpieza/consolidacion"
            const string prefijoRef = "ref: refs/heads/";
            if (texto.StartsWith(prefijoRef))
                return texto.Substring(prefijoRef.Length); // conserva las barras del nombre

            // HEAD desprendido: el archivo contiene el hash del commit.
            if (texto.Length >= 7)
                return "HEAD suelto (" + texto.Substring(0, 7) + ")";

            return "?";
        }
        catch
        {
            // Puede fallar si git esta escribiendo el archivo justo en ese momento.
            return _rama;
        }
    }

    static void DibujarEnSceneView(SceneView vista)
    {
        Handles.BeginGUI();

        bool protegida = false;
        foreach (var nombre in RamasProtegidas)
        {
            if (_rama == nombre) { protegida = true; break; }
        }

        var estilo = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            padding = new RectOffset(8, 8, 4, 4)
        };
        estilo.normal.textColor = protegida
            ? new Color(1f, 0.45f, 0.4f)   // rojo: rama protegida
            : new Color(0.5f, 0.9f, 0.6f); // verde: rama de trabajo

        var contenido = new GUIContent("⑂ " + _rama);
        Vector2 tam = estilo.CalcSize(contenido);
        var rect = new Rect(10, 10, tam.x, tam.y);

        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.55f));
        GUI.Label(rect, contenido, estilo);

        Handles.EndGUI();
    }
}
