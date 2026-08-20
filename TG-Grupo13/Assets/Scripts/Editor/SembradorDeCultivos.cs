using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SembradorDeCultivos : EditorWindow
{
    class EntradaPrefab
    {
        public GameObject prefab;
        public float peso = 1f;
    }

    enum ModoDistribucion
    {
        Cuadricula,
        Dispersion
    }

    const string PREFIJO_CONTENEDOR = "Siembra_";

    List<EntradaPrefab> _entradas = new List<EntradaPrefab> { new EntradaPrefab() };

    float _margenMetros = 0.04f;
    float _ajustePorPlanta = 0.6f;
    bool _usarRaycast = false;
    float _alturaRaycast = 3f;

    ModoDistribucion _modo = ModoDistribucion.Dispersion;
    int _filas = 2;
    int _columnas = 12;
    int _cantidadMin = 30;
    int _cantidadMax = 45;
    float _distanciaMinima = 0.12f;

    float _probabilidadOcupacion = 0.9f;
    int _racimoMin = 1;
    int _racimoMax = 2;
    float _radioRacimo = 0.07f;

    bool _apoyarBase = true;
    float _alturaExtra = 0f;

    float _variacionPosicion = 0.03f;
    float _variacionRotacion = 180f;
    float _variacionInclinacion = 5f;
    bool _inclinarHaciaAdentro = true;
    float _variacionEscala = 0.12f;

    bool _usarSemilla = false;
    int _semilla = 12345;

    Vector2 _scroll;

    Transform _superficie;
    Bounds _localBounds;
    float _localMargenX;
    float _localMargenZ;
    float _radioPlanta;
    Vector3 _centroMundo;

    [MenuItem("Herramientas/Invernadero/Sembrador")]
    static void Abrir()
    {
        GetWindow<SembradorDeCultivos>("Sembrador");
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.HelpBox(
            "1. Selecciona la cama en la jerarquia.\n" +
            "2. Agrega uno o varios prefabs y ajusta sus pesos.\n" +
            "3. Ajusta distribucion y densidad.\n" +
            "4. Presiona Sembrar.",
            MessageType.Info);

        DibujarDiagnostico();
        DibujarListaPrefabs();
        DibujarArea();
        DibujarDistribucion();
        DibujarDensidad();
        DibujarAltura();
        DibujarVariacion();
        DibujarSemilla();
        DibujarBotones();

        EditorGUILayout.EndScrollView();
    }

    void DibujarDiagnostico()
    {
        var sel = Selection.activeGameObject;
        if (sel == null) return;

        Bounds local;
        if (!ObtenerLocalBounds(sel, out local))
        {
            EditorGUILayout.HelpBox(
                "El objeto seleccionado no tiene MeshFilter. " +
                "No puedo calcular el area de siembra.",
                MessageType.Warning);
            return;
        }

        Vector3 escala = sel.transform.lossyScale;
        float anchoX = local.size.x * Mathf.Abs(escala.x);
        float largoZ = local.size.z * Mathf.Abs(escala.z);

        float radio = CalcularRadioPlanta();
        float insetTotal = _margenMetros + radio * _ajustePorPlanta;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            string.Format("Superficie: {0}   {1:0.00} m x {2:0.00} m", sel.name, anchoX, largoZ),
            EditorStyles.boldLabel);

        float utilX = Mathf.Max(0f, anchoX - insetTotal * 2f);
        float utilZ = Mathf.Max(0f, largoZ - insetTotal * 2f);

        EditorGUILayout.LabelField(
            string.Format("Radio de la planta: {0:0.00} m   Retiro del borde: {1:0.00} m",
                radio, insetTotal),
            EditorStyles.miniLabel);

        EditorGUILayout.LabelField(
            string.Format("Area util: {0:0.00} m x {1:0.00} m", utilX, utilZ),
            EditorStyles.miniLabel);

        if (utilX <= 0.01f || utilZ <= 0.01f)
        {
            EditorGUILayout.HelpBox(
                "El area util quedo en cero. Baja el margen o el ajuste por tamano de planta.",
                MessageType.Warning);
        }

        var box = sel.GetComponent<BoxCollider>();
        if (box != null)
        {
            bool difiere =
                !Mathf.Approximately(box.size.x, local.size.x) ||
                !Mathf.Approximately(box.size.y, local.size.y) ||
                !Mathf.Approximately(box.size.z, local.size.z);

            if (difiere)
            {
                EditorGUILayout.HelpBox(
                    string.Format(
                        "El Box Collider no coincide con el mesh.\nMesh: {0}   Collider: {1}",
                        local.size, box.size),
                    MessageType.Warning);
            }
        }
    }

    void DibujarListaPrefabs()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefabs de cultivo", EditorStyles.boldLabel);

        int aBorrar = -1;

        for (int i = 0; i < _entradas.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            _entradas[i].prefab = (GameObject)EditorGUILayout.ObjectField(
                _entradas[i].prefab, typeof(GameObject), false);

            EditorGUILayout.LabelField("Peso", GUILayout.Width(35));
            _entradas[i].peso = Mathf.Max(0f,
                EditorGUILayout.FloatField(_entradas[i].peso, GUILayout.Width(45)));

            GUI.enabled = _entradas.Count > 1;
            if (GUILayout.Button("X", GUILayout.Width(22)))
                aBorrar = i;
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        if (aBorrar >= 0)
            _entradas.RemoveAt(aBorrar);

        if (GUILayout.Button("+ Agregar variante"))
            _entradas.Add(new EntradaPrefab());

        MostrarProporciones();
    }

    void MostrarProporciones()
    {
        float total = 0f;
        int validos = 0;

        foreach (var e in _entradas)
        {
            if (e.prefab == null) continue;
            total += e.peso;
            validos++;
        }

        if (validos < 2 || total <= 0f) return;

        string texto = "Proporcion esperada:  ";
        foreach (var e in _entradas)
        {
            if (e.prefab == null) continue;
            texto += string.Format("{0} {1:0}%   ", e.prefab.name, (e.peso / total) * 100f);
        }

        EditorGUILayout.LabelField(texto, EditorStyles.miniLabel);
    }

    void DibujarArea()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Area de siembra", EditorStyles.boldLabel);

        _margenMetros = EditorGUILayout.Slider("Margen fijo (m)", _margenMetros, 0f, 0.5f);

        _ajustePorPlanta = EditorGUILayout.Slider(
            "Ajuste por tamano de planta", _ajustePorPlanta, 0f, 1f);
        EditorGUILayout.LabelField(
            "1 = ninguna hoja sobresale. 0 = solo cuenta la base del tallo.",
            EditorStyles.miniLabel);

        EditorGUILayout.Space(2);

        _usarRaycast = EditorGUILayout.Toggle("Apoyar con raycast", _usarRaycast);

        if (_usarRaycast)
            _alturaRaycast = EditorGUILayout.Slider("Altura del rayo (m)", _alturaRaycast, 0.5f, 10f);
    }

    void DibujarDistribucion()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Distribucion", EditorStyles.boldLabel);

        _modo = (ModoDistribucion)EditorGUILayout.EnumPopup("Modo", _modo);

        if (_modo == ModoDistribucion.Cuadricula)
        {
            _filas = Mathf.Max(1, EditorGUILayout.IntField("Filas (ancho)", _filas));
            _columnas = Mathf.Max(1, EditorGUILayout.IntField("Columnas (largo)", _columnas));
        }
        else
        {
            _cantidadMin = Mathf.Max(1, EditorGUILayout.IntField("Cantidad minima", _cantidadMin));
            _cantidadMax = Mathf.Max(_cantidadMin, EditorGUILayout.IntField("Cantidad maxima", _cantidadMax));
            _distanciaMinima = EditorGUILayout.Slider("Separacion minima (m)", _distanciaMinima, 0.01f, 0.5f);
        }
    }

    void DibujarDensidad()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Densidad", EditorStyles.boldLabel);

        _probabilidadOcupacion = EditorGUILayout.Slider("Ocupacion", _probabilidadOcupacion, 0.1f, 1f);

        EditorGUILayout.Space(2);

        _racimoMin = Mathf.Max(1, EditorGUILayout.IntField("Plantas por mata (min)", _racimoMin));
        _racimoMax = Mathf.Max(_racimoMin, EditorGUILayout.IntField("Plantas por mata (max)", _racimoMax));

        if (_racimoMax > 1)
            _radioRacimo = EditorGUILayout.Slider("Radio de la mata (m)", _radioRacimo, 0.02f, 0.3f);
    }

    void DibujarAltura()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Altura", EditorStyles.boldLabel);
        _apoyarBase = EditorGUILayout.Toggle("Apoyar base en superficie", _apoyarBase);
        _alturaExtra = EditorGUILayout.FloatField("Altura extra", _alturaExtra);
    }

    void DibujarVariacion()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Variacion natural", EditorStyles.boldLabel);
        _variacionPosicion = EditorGUILayout.Slider("Posicion (m)", _variacionPosicion, 0f, 0.3f);
        _variacionRotacion = EditorGUILayout.Slider("Rotacion Y (grados)", _variacionRotacion, 0f, 180f);
        _variacionInclinacion = EditorGUILayout.Slider("Inclinacion (grados)", _variacionInclinacion, 0f, 20f);

        _inclinarHaciaAdentro = EditorGUILayout.Toggle("Inclinar hacia el centro", _inclinarHaciaAdentro);
        EditorGUILayout.LabelField(
            "Las plantas del borde se ladean hacia adentro y no sobresalen.",
            EditorStyles.miniLabel);

        _variacionEscala = EditorGUILayout.Slider("Escala", _variacionEscala, 0f, 0.5f);
    }

    void DibujarSemilla()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Reproducibilidad", EditorStyles.boldLabel);

        _usarSemilla = EditorGUILayout.Toggle("Usar semilla fija", _usarSemilla);
        if (_usarSemilla)
            _semilla = EditorGUILayout.IntField("Semilla", _semilla);
    }

    void DibujarBotones()
    {
        EditorGUILayout.Space();

        var seleccion = Selection.activeGameObject;
        bool hayPrefab = false;
        foreach (var e in _entradas)
            if (e.prefab != null) { hayPrefab = true; break; }

        if (seleccion == null)
            EditorGUILayout.HelpBox("Selecciona la cama en la jerarquia.", MessageType.Warning);
        else if (!hayPrefab)
            EditorGUILayout.HelpBox("Asigna al menos un prefab.", MessageType.Warning);

        GUI.enabled = seleccion != null && hayPrefab;
        if (GUILayout.Button("Sembrar", GUILayout.Height(32)))
            Sembrar(seleccion);
        GUI.enabled = true;

        EditorGUILayout.Space();

        GUI.enabled = seleccion != null;
        if (GUILayout.Button("Borrar lo sembrado en el objeto seleccionado"))
            BorrarSiembra(seleccion);
        GUI.enabled = true;
    }

    float CalcularRadioPlanta()
    {
        float radio = 0f;

        foreach (var e in _entradas)
        {
            if (e.prefab == null) continue;

            Bounds b;
            if (!ObtenerBoundsPrefab(e.prefab, out b)) continue;

            radio = Mathf.Max(radio, Mathf.Max(b.extents.x, b.extents.z));
        }

        return radio;
    }

    void Sembrar(GameObject superficie)
    {
        Bounds local;
        if (!ObtenerLocalBounds(superficie, out local))
        {
            EditorUtility.DisplayDialog(
                "Sembrador",
                "El objeto seleccionado no tiene MeshFilter con mesh asignado.",
                "Entendido");
            return;
        }

        _superficie = superficie.transform;
        _localBounds = local;
        _radioPlanta = CalcularRadioPlanta();

        Vector3 escala = _superficie.lossyScale;
        float inset = _margenMetros + _radioPlanta * _ajustePorPlanta;

        _localMargenX = Mathf.Abs(escala.x) > 0.0001f ? inset / Mathf.Abs(escala.x) : 0f;
        _localMargenZ = Mathf.Abs(escala.z) > 0.0001f ? inset / Mathf.Abs(escala.z) : 0f;

        float utilX = local.size.x - _localMargenX * 2f;
        float utilZ = local.size.z - _localMargenZ * 2f;

        if (utilX <= 0f || utilZ <= 0f)
        {
            EditorUtility.DisplayDialog(
                "Sembrador",
                "El retiro del borde es mas grande que la cama.\n" +
                "Baja el margen fijo o el ajuste por tamano de planta.",
                "Entendido");
            _superficie = null;
            return;
        }

        _centroMundo = _superficie.TransformPoint(
            new Vector3(local.center.x, local.max.y, local.center.z));

        if (_usarSemilla)
            Random.InitState(_semilla);

        GameObject contenedor = new GameObject(PREFIJO_CONTENEDOR + superficie.name);
        Undo.RegisterCreatedObjectUndo(contenedor, "Sembrar cultivos");
        contenedor.transform.position = Vector3.zero;
        contenedor.transform.rotation = Quaternion.identity;
        contenedor.transform.localScale = Vector3.one;

        List<Vector3> puntosMundo = _modo == ModoDistribucion.Cuadricula
            ? GenerarCuadricula()
            : GenerarDispersion();

        int sembradas = 0;

        foreach (Vector3 punto in puntosMundo)
        {
            if (Random.value > _probabilidadOcupacion)
                continue;

            int enLaMata = Random.Range(_racimoMin, _racimoMax + 1);

            for (int k = 0; k < enLaMata; k++)
            {
                Vector3 pos = punto;

                if (k > 0)
                {
                    Vector2 d = Random.insideUnitCircle * _radioRacimo;
                    pos += new Vector3(d.x, 0f, d.y);
                }

                if (_variacionPosicion > 0f)
                {
                    Vector2 d = Random.insideUnitCircle * _variacionPosicion;
                    pos += new Vector3(d.x, 0f, d.y);
                }

                pos = ConfinarASuperficie(pos);

                float altura;
                ResolverAltura(pos, out altura);

                if (ColocarPlanta(new Vector3(pos.x, altura, pos.z), contenedor.transform))
                    sembradas++;
            }
        }

        Selection.activeGameObject = contenedor;

        Debug.Log(string.Format(
            "[Sembrador] {0} plantas en '{1}'. Area util {2:0.00} x {3:0.00} m, " +
            "radio de planta {4:0.00} m, retiro del borde {5:0.00} m.",
            sembradas,
            superficie.name,
            utilX * Mathf.Abs(escala.x),
            utilZ * Mathf.Abs(escala.z),
            _radioPlanta,
            inset));

        _superficie = null;
    }

    Vector3 ConfinarASuperficie(Vector3 puntoMundo)
    {
        Vector3 local = _superficie.InverseTransformPoint(puntoMundo);

        local.x = Mathf.Clamp(local.x,
            _localBounds.min.x + _localMargenX,
            _localBounds.max.x - _localMargenX);

        local.z = Mathf.Clamp(local.z,
            _localBounds.min.z + _localMargenZ,
            _localBounds.max.z - _localMargenZ);

        local.y = _localBounds.max.y;

        return _superficie.TransformPoint(local);
    }

    void ResolverAltura(Vector3 puntoMundo, out float altura)
    {
        altura = puntoMundo.y;

        if (!_usarRaycast) return;

        Vector3 origen = new Vector3(puntoMundo.x, puntoMundo.y + _alturaRaycast, puntoMundo.z);

        RaycastHit[] impactos = Physics.RaycastAll(
            origen, Vector3.down, _alturaRaycast * 2f, ~0, QueryTriggerInteraction.Ignore);

        bool encontrado = false;
        float mejorY = 0f;

        foreach (RaycastHit impacto in impactos)
        {
            if (impacto.transform != _superficie) continue;

            if (!encontrado || impacto.point.y > mejorY)
            {
                mejorY = impacto.point.y;
                encontrado = true;
            }
        }

        if (encontrado)
            altura = mejorY;
    }

    float CercaniaAlBorde(Vector3 posicionMundo)
    {
        Vector3 local = _superficie.InverseTransformPoint(posicionMundo);

        float utilX = _localBounds.extents.x - _localMargenX;
        float utilZ = _localBounds.extents.z - _localMargenZ;

        float tx = utilX > 0.0001f ? Mathf.Abs(local.x - _localBounds.center.x) / utilX : 0f;
        float tz = utilZ > 0.0001f ? Mathf.Abs(local.z - _localBounds.center.z) / utilZ : 0f;

        return Mathf.Clamp01(Mathf.Max(tx, tz));
    }

    Quaternion CalcularInclinacion(Vector3 posicionMundo)
    {
        if (_variacionInclinacion <= 0f)
            return Quaternion.identity;

        float angulo = Random.Range(_variacionInclinacion * 0.2f, _variacionInclinacion);

        Vector2 aleatorio = Random.insideUnitCircle.normalized;
        Vector3 direccion = new Vector3(aleatorio.x, 0f, aleatorio.y);

        if (_inclinarHaciaAdentro)
        {
            Vector3 haciaCentro = _centroMundo - posicionMundo;
            haciaCentro.y = 0f;

            if (haciaCentro.sqrMagnitude > 0.0001f)
            {
                float peso = CercaniaAlBorde(posicionMundo);
                direccion = Vector3.Slerp(direccion, haciaCentro.normalized, peso).normalized;
            }
        }

        Vector3 eje = Vector3.Cross(Vector3.up, direccion);
        if (eje.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        return Quaternion.AngleAxis(angulo, eje.normalized);
    }

    bool ColocarPlanta(Vector3 posicion, Transform contenedor)
    {
        GameObject prefab = ElegirPrefab();
        if (prefab == null) return false;

        GameObject instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab, contenedor);
        if (instancia == null) return false;

        Undo.RegisterCreatedObjectUndo(instancia, "Sembrar cultivos");

        Quaternion rotacionOriginal = prefab.transform.rotation;
        Quaternion giroY = Quaternion.Euler(0f, Random.Range(-_variacionRotacion, _variacionRotacion), 0f);
        Quaternion inclinacion = CalcularInclinacion(posicion);

        instancia.transform.rotation = inclinacion * giroY * rotacionOriginal;

        float factor = 1f + Random.Range(-_variacionEscala, _variacionEscala);
        instancia.transform.localScale = prefab.transform.localScale * factor;

        instancia.transform.position = posicion;

        if (_apoyarBase)
        {
            Bounds bp;
            if (ObtenerBoundsMundo(instancia, out bp))
                instancia.transform.position += new Vector3(0f, posicion.y - bp.min.y, 0f);
        }

        instancia.transform.position += new Vector3(0f, _alturaExtra, 0f);

        return true;
    }

    GameObject ElegirPrefab()
    {
        float total = 0f;
        foreach (var e in _entradas)
            if (e.prefab != null) total += e.peso;

        if (total <= 0f)
        {
            foreach (var e in _entradas)
                if (e.prefab != null) return e.prefab;
            return null;
        }

        float tirada = Random.value * total;
        float acumulado = 0f;

        foreach (var e in _entradas)
        {
            if (e.prefab == null) continue;
            acumulado += e.peso;
            if (tirada <= acumulado) return e.prefab;
        }

        foreach (var e in _entradas)
            if (e.prefab != null) return e.prefab;

        return null;
    }

    List<Vector3> GenerarCuadricula()
    {
        var lista = new List<Vector3>();

        float minX = _localBounds.min.x + _localMargenX;
        float maxX = _localBounds.max.x - _localMargenX;
        float minZ = _localBounds.min.z + _localMargenZ;
        float maxZ = _localBounds.max.z - _localMargenZ;
        float topY = _localBounds.max.y;

        for (int f = 0; f < _filas; f++)
        {
            float tx = _filas == 1 ? 0.5f : (float)f / (_filas - 1);

            for (int c = 0; c < _columnas; c++)
            {
                float tz = _columnas == 1 ? 0.5f : (float)c / (_columnas - 1);

                Vector3 local = new Vector3(
                    Mathf.Lerp(minX, maxX, tx),
                    topY,
                    Mathf.Lerp(minZ, maxZ, tz));

                lista.Add(_superficie.TransformPoint(local));
            }
        }

        return lista;
    }

    List<Vector3> GenerarDispersion()
    {
        var lista = new List<Vector3>();

        float minX = _localBounds.min.x + _localMargenX;
        float maxX = _localBounds.max.x - _localMargenX;
        float minZ = _localBounds.min.z + _localMargenZ;
        float maxZ = _localBounds.max.z - _localMargenZ;
        float topY = _localBounds.max.y;

        int objetivo = Random.Range(_cantidadMin, _cantidadMax + 1);
        float distCuadrada = _distanciaMinima * _distanciaMinima;

        int intentos = 0;
        int intentosMaximos = objetivo * 40;

        while (lista.Count < objetivo && intentos < intentosMaximos)
        {
            intentos++;

            Vector3 local = new Vector3(
                Random.Range(minX, maxX),
                topY,
                Random.Range(minZ, maxZ));

            Vector3 mundo = _superficie.TransformPoint(local);

            bool valido = true;
            foreach (Vector3 existente in lista)
            {
                float dx = existente.x - mundo.x;
                float dz = existente.z - mundo.z;
                if (dx * dx + dz * dz < distCuadrada)
                {
                    valido = false;
                    break;
                }
            }

            if (valido)
                lista.Add(mundo);
        }

        if (lista.Count < objetivo)
        {
            Debug.LogWarning(string.Format(
                "[Sembrador] Solo entraron {0} de {1} posiciones. " +
                "Baja la separacion minima o el retiro del borde.",
                lista.Count, objetivo));
        }

        return lista;
    }

    bool ObtenerLocalBounds(GameObject go, out Bounds resultado)
    {
        resultado = new Bounds();

        var mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            resultado = mf.sharedMesh.bounds;
            return true;
        }

        var smr = go.GetComponent<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null)
        {
            resultado = smr.sharedMesh.bounds;
            return true;
        }

        return false;
    }

    bool ObtenerBoundsPrefab(GameObject prefab, out Bounds resultado)
    {
        resultado = new Bounds();
        bool encontrado = false;

        var filtros = prefab.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in filtros)
        {
            if (mf.sharedMesh == null) continue;

            Bounds b = mf.sharedMesh.bounds;
            Vector3 escala = mf.transform.lossyScale;
            Vector3 tamano = new Vector3(
                b.size.x * Mathf.Abs(escala.x),
                b.size.y * Mathf.Abs(escala.y),
                b.size.z * Mathf.Abs(escala.z));

            Bounds mundial = new Bounds(mf.transform.position, tamano);

            if (!encontrado) { resultado = mundial; encontrado = true; }
            else resultado.Encapsulate(mundial);
        }

        return encontrado;
    }

    bool ObtenerBoundsMundo(GameObject go, out Bounds resultado)
    {
        resultado = new Bounds();
        bool encontrado = false;

        var renderers = go.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (!encontrado) { resultado = r.bounds; encontrado = true; }
            else resultado.Encapsulate(r.bounds);
        }

        return encontrado;
    }

    void BorrarSiembra(GameObject superficie)
    {
        string objetivo = PREFIJO_CONTENEDOR + superficie.name;
        var raices = superficie.scene.GetRootGameObjects();

        int borrados = 0;
        foreach (var raiz in raices)
        {
            if (raiz.name != objetivo) continue;
            Undo.DestroyObjectImmediate(raiz);
            borrados++;
        }

        Debug.Log(borrados == 0
            ? string.Format("[Sembrador] No se encontro ningun contenedor llamado '{0}'.", objetivo)
            : string.Format("[Sembrador] {0} contenedor(es) borrado(s).", borrados));
    }
}