using UnityEditor;
using UnityEngine;


public class SembradorDeCultivos : EditorWindow
{
    GameObject _prefab;
    int _filas = 3;
    int _columnas = 10;
    float _margen = 0.15f;
    float _alturaExtra = 0f;
    float _variacionPosicion = 0.03f;
    float _variacionRotacion = 180f;
    float _variacionEscala = 0.1f;
    bool _alinearAlPadre = true;

    [MenuItem("Herramientas/Invernadero/Sembrador")]
    static void Abrir()
    {
        GetWindow<SembradorDeCultivos>("Sembrador");
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1. Selecciona en la jerarquia la cama o malla donde quieres sembrar.\n" +
            "2. Asigna el prefab del cultivo.\n" +
            "3. Ajusta filas y columnas.\n" +
            "4. Presiona Sembrar.",
            MessageType.Info);

        EditorGUILayout.Space();

        _prefab = (GameObject)EditorGUILayout.ObjectField(
            "Prefab del cultivo", _prefab, typeof(GameObject), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cuadricula", EditorStyles.boldLabel);
        _filas = Mathf.Max(1, EditorGUILayout.IntField("Filas (ancho)", _filas));
        _columnas = Mathf.Max(1, EditorGUILayout.IntField("Columnas (largo)", _columnas));
        _margen = EditorGUILayout.Slider("Margen del borde", _margen, 0f, 0.45f);
        _alturaExtra = EditorGUILayout.FloatField("Altura extra", _alturaExtra);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Variacion natural", EditorStyles.boldLabel);
        _variacionPosicion = EditorGUILayout.Slider("Posicion (m)", _variacionPosicion, 0f, 0.3f);
        _variacionRotacion = EditorGUILayout.Slider("Rotacion Y (grados)", _variacionRotacion, 0f, 180f);
        _variacionEscala = EditorGUILayout.Slider("Escala", _variacionEscala, 0f, 0.5f);

        EditorGUILayout.Space();
        _alinearAlPadre = EditorGUILayout.Toggle("Hijo del objeto", _alinearAlPadre);

        EditorGUILayout.Space();

        var seleccion = Selection.activeGameObject;
        bool listo = _prefab != null && seleccion != null;

        if (seleccion == null)
            EditorGUILayout.HelpBox("Selecciona un objeto en la jerarquia.", MessageType.Warning);
        else if (_prefab == null)
            EditorGUILayout.HelpBox("Asigna un prefab.", MessageType.Warning);
        else
            EditorGUILayout.LabelField("Destino:", seleccion.name);

        EditorGUI.BeginDisabledGroup(!listo);
        if (GUILayout.Button("Sembrar", GUILayout.Height(30)))
            Sembrar(seleccion);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        EditorGUI.BeginDisabledGroup(seleccion == null);
        if (GUILayout.Button("Borrar lo sembrado en el objeto seleccionado"))
            BorrarSembrado(seleccion);
        EditorGUI.EndDisabledGroup();
    }

    void Sembrar(GameObject destino)
    {
        Bounds limites;

        var col = destino.GetComponent<Collider>();
        var ren = destino.GetComponent<Renderer>();

        if (col != null) limites = col.bounds;
        else if (ren != null) limites = ren.bounds;
        else
        {
            EditorUtility.DisplayDialog("Sembrador",
                "El objeto seleccionado no tiene Collider ni Renderer, " +
                "asi que no puedo calcular su superficie.", "Entendido");
            return;
        }

        float alturaSuperficie = limites.max.y + _alturaExtra;

        float anchoUtil = limites.size.x * (1f - _margen * 2f);
        float largoUtil = limites.size.z * (1f - _margen * 2f);

        Vector3 esquina = new Vector3(
            limites.center.x - anchoUtil * 0.5f,
            alturaSuperficie,
            limites.center.z - largoUtil * 0.5f);


        float pasoX = _filas > 1 ? anchoUtil / (_filas - 1) : 0f;
        float pasoZ = _columnas > 1 ? largoUtil / (_columnas - 1) : 0f;

        var contenedor = new GameObject("Sembrado_" + _prefab.name);
        Undo.RegisterCreatedObjectUndo(contenedor, "Sembrar cultivos");
        if (_alinearAlPadre) contenedor.transform.SetParent(destino.transform, true);

        int total = 0;

        for (int f = 0; f < _filas; f++)
        {
            for (int c = 0; c < _columnas; c++)
            {
                Vector3 pos = esquina + new Vector3(
                    _filas > 1 ? f * pasoX : anchoUtil * 0.5f,
                    0f,
                    _columnas > 1 ? c * pasoZ : largoUtil * 0.5f);

        
                pos.x += Random.Range(-_variacionPosicion, _variacionPosicion);
                pos.z += Random.Range(-_variacionPosicion, _variacionPosicion);

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(_prefab);
                if (inst == null) continue;

                inst.transform.position = pos;
                inst.transform.rotation = Quaternion.Euler(
                    0f, Random.Range(-_variacionRotacion, _variacionRotacion), 0f);

                float esc = 1f + Random.Range(-_variacionEscala, _variacionEscala);
                inst.transform.localScale = _prefab.transform.localScale * esc;

                inst.transform.SetParent(contenedor.transform, true);
                Undo.RegisterCreatedObjectUndo(inst, "Sembrar cultivos");
                total++;
            }
        }

        Selection.activeGameObject = contenedor;
        Debug.Log($"[Sembrador] Se sembraron {total} instancias de {_prefab.name} sobre {destino.name}.");
    }

    void BorrarSembrado(GameObject destino)
    {
        int borrados = 0;
        for (int i = destino.transform.childCount - 1; i >= 0; i--)
        {
            var hijo = destino.transform.GetChild(i);
            if (hijo.name.StartsWith("Sembrado_"))
            {
                Undo.DestroyObjectImmediate(hijo.gameObject);
                borrados++;
            }
        }
        Debug.Log($"[Sembrador] Se borraron {borrados} grupos sembrados.");
    }
}
