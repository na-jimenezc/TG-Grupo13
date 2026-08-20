using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlantaFlexible : MonoBehaviour
{
    public enum ModoPivote
    {
        UnionAutomatica,
        Propio,
        BaseDeLaPlanta
    }

    [System.Serializable]
    public class GrupoFlexible
    {
        public string prefijo = "Leaf";

        [Range(0f, 3f)]
        public float multiplicador = 1.6f;

        public float rigidez = 6f;
        public float amortiguacion = 1.8f;
    }

    [Header("Anclaje")]
    public bool detectarAnclaAutomatica = true;
    public Vector3 anclaLocal = Vector3.zero;

    [Header("Segmentos")]
    public ModoPivote modoPivote = ModoPivote.UnionAutomatica;

    public GrupoFlexible[] grupos =
    {
        new GrupoFlexible { prefijo = "Stem", multiplicador = 1f, rigidez = 20f, amortiguacion = 6f },
        new GrupoFlexible { prefijo = "Petal", multiplicador = 1.2f, rigidez = 10f, amortiguacion = 2.5f },
        new GrupoFlexible { prefijo = "Leaf", multiplicador = 1.7f, rigidez = 6f, amortiguacion = 1.8f }
    };

    [Header("Deteccion del jugador")]
    public float radioDeteccion = 0.5f;
    public LayerMask capasQueEmpujan;
    public float distanciaCulling = 6f;

    [Header("Vecinos")]
    public LayerMask capaPlantas;
    public float radioVecinos = 0.35f;

    [Range(0f, 1f)]
    public float presionEnReposo = 0.3f;

    [Range(0f, 1f)]
    public float propagacion = 0.5f;

    [Header("Doblado")]
    [Range(0f, 80f)]
    public float anguloMaximo = 30f;
    public float rigidez = 14f;
    public float amortiguacion = 3.5f;

    [Header("Viento ambiental")]
    public bool vientoActivo = true;

    [Range(0f, 10f)]
    public float amplitudViento = 2f;
    public float velocidadViento = 0.7f;

    public float NivelDoblado
    {
        get
        {
            if (anguloMaximo <= 0.001f) return 0f;
            return Mathf.Clamp01(_inclinacionActual.magnitude / anguloMaximo);
        }
    }

    public Vector3 AnclaMundo { get { return _anclaMundo; } }

    Quaternion _rotacionReposoRaiz;
    Vector3 _posicionReposoRaiz;
    Vector3 _anclaMundo;
    Vector3 _anclaResuelta;

    Transform[] _segmentos;
    Vector3[] _posicionesReposo;
    Quaternion[] _rotacionesReposo;
    Vector3[] _pivotesLocales;
    int[] _grupoDeSegmento;

    Vector2[] _inclinacionGrupo;
    Vector2[] _velocidadGrupo;

    PlantaFlexible[] _vecinos;
    Vector3[] _direccionVecino;
    float[] _intensidadVecino;

    Vector2 _inclinacionActual;
    Vector2 _velocidadInclinacion;
    Vector2 _objetivoCacheado;

    float _desfaseViento;

    static readonly Collider[] _buffer = new Collider[16];
    static Transform _camaraJugador;
    static int _contadorGlobal;

    int _miTurno;
    const int FRAMES_ENTRE_CHEQUEOS = 3;

    void Awake()
    {
        _rotacionReposoRaiz = transform.localRotation;
        _posicionReposoRaiz = transform.localPosition;

        _anclaResuelta = detectarAnclaAutomatica ? DetectarAncla() : anclaLocal;
        _anclaMundo = transform.TransformPoint(_anclaResuelta);

        RecolectarSegmentos();

        _desfaseViento = Random.Range(0f, 100f);
        _miTurno = _contadorGlobal++ % FRAMES_ENTRE_CHEQUEOS;
    }

    void Start()
    {
        RecolectarVecinos();
    }

    Vector3 DetectarAncla()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return Vector3.zero;

        Bounds total = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            total.Encapsulate(renderers[i].bounds);

        Vector3 baseMundo = new Vector3(total.center.x, total.min.y, total.center.z);
        return transform.InverseTransformPoint(baseMundo);
    }

    void RecolectarSegmentos()
    {
        var transforms = new List<Transform>();
        var indices = new List<int>();

        foreach (Transform hijo in transform)
        {
            int indice = BuscarGrupo(hijo.name);
            if (indice < 0) continue;

            transforms.Add(hijo);
            indices.Add(indice);
        }

        _segmentos = transforms.ToArray();
        _grupoDeSegmento = indices.ToArray();
        _posicionesReposo = new Vector3[_segmentos.Length];
        _rotacionesReposo = new Quaternion[_segmentos.Length];
        _pivotesLocales = new Vector3[_segmentos.Length];

        for (int i = 0; i < _segmentos.Length; i++)
        {
            _posicionesReposo[i] = _segmentos[i].localPosition;
            _rotacionesReposo[i] = _segmentos[i].localRotation;
            _pivotesLocales[i] = CalcularPivote(_segmentos[i]);
        }

        int cantidadGrupos = grupos != null ? grupos.Length : 0;
        _inclinacionGrupo = new Vector2[cantidadGrupos];
        _velocidadGrupo = new Vector2[cantidadGrupos];
    }

    Vector3 CalcularPivote(Transform segmento)
    {
        if (modoPivote == ModoPivote.Propio)
            return segmento.localPosition;

        if (modoPivote == ModoPivote.BaseDeLaPlanta)
            return _anclaResuelta;

        Bounds bounds;
        if (!ObtenerBoundsMundo(segmento, out bounds))
            return segmento.localPosition;

        Vector3 eje = transform.up;
        Vector3 desde = _anclaMundo;

        float altura = Vector3.Dot(bounds.center - desde, eje);
        Vector3 puntoEnEje = desde + eje * altura;

        Vector3 union = bounds.ClosestPoint(puntoEnEje);

        return transform.InverseTransformPoint(union);
    }

    bool ObtenerBoundsMundo(Transform objetivo, out Bounds resultado)
    {
        resultado = new Bounds();

        var renderers = objetivo.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return false;

        resultado = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            resultado.Encapsulate(renderers[i].bounds);

        return true;
    }

    int BuscarGrupo(string nombre)
    {
        if (grupos == null) return -1;

        for (int i = 0; i < grupos.Length; i++)
        {
            if (grupos[i] == null || string.IsNullOrEmpty(grupos[i].prefijo)) continue;

            if (nombre.StartsWith(grupos[i].prefijo, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    void RecolectarVecinos()
    {
        var plantas = new List<PlantaFlexible>();
        var direcciones = new List<Vector3>();
        var intensidades = new List<float>();

        if (capaPlantas.value != 0 && radioVecinos > 0.001f)
        {
            int cantidad = Physics.OverlapSphereNonAlloc(
                _anclaMundo, radioVecinos, _buffer, capaPlantas, QueryTriggerInteraction.Collide);

            for (int i = 0; i < cantidad; i++)
            {
                if (_buffer[i] == null) continue;

                PlantaFlexible otra = _buffer[i].GetComponentInParent<PlantaFlexible>();
                if (otra == null || otra == this || plantas.Contains(otra)) continue;

                Vector3 direccion = _anclaMundo - otra.AnclaMundo;
                direccion.y = 0f;

                float dist = direccion.magnitude;
                if (dist < 0.0001f) continue;

                float intensidad = 1f - Mathf.Clamp01(dist / radioVecinos);
                intensidad *= intensidad;

                plantas.Add(otra);
                direcciones.Add(direccion.normalized);
                intensidades.Add(intensidad);
            }
        }

        _vecinos = plantas.ToArray();
        _direccionVecino = direcciones.ToArray();
        _intensidadVecino = intensidades.ToArray();
    }

    void Update()
    {
        if (_camaraJugador == null)
        {
            if (Camera.main == null) return;
            _camaraJugador = Camera.main.transform;
        }

        float distancia = Vector3.Distance(_camaraJugador.position, _anclaMundo);
        bool lejos = distancia > distanciaCulling;

        if (lejos && _inclinacionActual.sqrMagnitude < 0.01f && !HayVecinoActivo())
            return;

        if ((Time.frameCount + _miTurno) % FRAMES_ENTRE_CHEQUEOS == 0)
            _objetivoCacheado = CalcularEmpujeJugador();

        Vector3 empuje = new Vector3(_objetivoCacheado.x, 0f, _objetivoCacheado.y);
        empuje += CalcularEmpujeVecinos();

        Vector2 objetivo = EmpujeAInclinacion(empuje);

        if (vientoActivo)
        {
            float t = Time.time * velocidadViento + _desfaseViento;
            objetivo.x += Mathf.Sin(t) * amplitudViento;
            objetivo.y += Mathf.Cos(t * 0.73f) * amplitudViento * 0.6f;
        }

        SimularResorte(objetivo);
        SimularGrupos();
        AplicarRotacion();
    }

    bool HayVecinoActivo()
    {
        if (_vecinos == null) return false;

        for (int i = 0; i < _vecinos.Length; i++)
        {
            if (_vecinos[i] != null && _vecinos[i].NivelDoblado > 0.02f)
                return true;
        }

        return false;
    }

    Vector2 CalcularEmpujeJugador()
    {
        int cantidad = Physics.OverlapSphereNonAlloc(
            _anclaMundo + Vector3.up * (radioDeteccion * 0.5f),
            radioDeteccion,
            _buffer,
            capasQueEmpujan,
            QueryTriggerInteraction.Collide);

        if (cantidad == 0)
            return Vector2.zero;

        Vector3 acumulado = Vector3.zero;

        for (int i = 0; i < cantidad; i++)
        {
            Collider col = _buffer[i];
            if (col == null) continue;

            Vector3 puntoCercano = col.ClosestPoint(_anclaMundo);

            Vector3 direccion = _anclaMundo - puntoCercano;
            direccion.y = 0f;

            float dist = direccion.magnitude;
            if (dist < 0.0001f) continue;

            float intensidad = 1f - Mathf.Clamp01(dist / radioDeteccion);
            intensidad *= intensidad;

            acumulado += direccion.normalized * intensidad;
        }

        return new Vector2(acumulado.x, acumulado.z);
    }

    Vector3 CalcularEmpujeVecinos()
    {
        if (_vecinos == null || _vecinos.Length == 0)
            return Vector3.zero;

        Vector3 acumulado = Vector3.zero;

        for (int i = 0; i < _vecinos.Length; i++)
        {
            if (_vecinos[i] == null) continue;

            float peso = presionEnReposo + propagacion * _vecinos[i].NivelDoblado;
            acumulado += _direccionVecino[i] * _intensidadVecino[i] * peso;
        }

        return acumulado;
    }

    Vector2 EmpujeAInclinacion(Vector3 empuje)
    {
        if (empuje.sqrMagnitude < 0.000001f)
            return Vector2.zero;

        float fuerza = Mathf.Clamp01(empuje.magnitude);
        Vector3 direccion = empuje.normalized;

        return new Vector2(
            -direccion.z * anguloMaximo * fuerza,
            direccion.x * anguloMaximo * fuerza);
    }

    void SimularResorte(Vector2 objetivo)
    {
        float dt = Time.deltaTime;

        Vector2 fuerza = (objetivo - _inclinacionActual) * rigidez
                       - _velocidadInclinacion * amortiguacion;

        _velocidadInclinacion += fuerza * dt;
        _inclinacionActual += _velocidadInclinacion * dt;

        _inclinacionActual.x = Mathf.Clamp(_inclinacionActual.x, -anguloMaximo, anguloMaximo);
        _inclinacionActual.y = Mathf.Clamp(_inclinacionActual.y, -anguloMaximo, anguloMaximo);
    }

    void SimularGrupos()
    {
        if (grupos == null || _inclinacionGrupo == null) return;

        float dt = Time.deltaTime;

        for (int i = 0; i < grupos.Length && i < _inclinacionGrupo.Length; i++)
        {
            if (grupos[i] == null) continue;

            Vector2 objetivo = _inclinacionActual * grupos[i].multiplicador;

            Vector2 fuerza = (objetivo - _inclinacionGrupo[i]) * grupos[i].rigidez
                           - _velocidadGrupo[i] * grupos[i].amortiguacion;

            _velocidadGrupo[i] += fuerza * dt;
            _inclinacionGrupo[i] += _velocidadGrupo[i] * dt;

            float limite = anguloMaximo * 3f;
            _inclinacionGrupo[i].x = Mathf.Clamp(_inclinacionGrupo[i].x, -limite, limite);
            _inclinacionGrupo[i].y = Mathf.Clamp(_inclinacionGrupo[i].y, -limite, limite);
        }
    }

    void AplicarRotacion()
    {
        Quaternion doblado = Quaternion.Euler(_inclinacionActual.x, 0f, _inclinacionActual.y);

        transform.localPosition = _posicionReposoRaiz;
        transform.localRotation = _rotacionReposoRaiz * doblado;

        Vector3 anclaAhora = transform.TransformPoint(_anclaResuelta);
        transform.position += _anclaMundo - anclaAhora;

        if (_segmentos == null) return;

        for (int i = 0; i < _segmentos.Length; i++)
        {
            if (_segmentos[i] == null) continue;

            int g = _grupoDeSegmento[i];
            if (g < 0 || g >= _inclinacionGrupo.Length) continue;

            Vector2 extra = _inclinacionGrupo[g] - _inclinacionActual;

            if (extra.sqrMagnitude < 0.0001f)
            {
                _segmentos[i].localPosition = _posicionesReposo[i];
                _segmentos[i].localRotation = _rotacionesReposo[i];
                continue;
            }

            Quaternion adicional = Quaternion.Euler(extra.x, 0f, extra.y);
            Vector3 pivote = _pivotesLocales[i];

            _segmentos[i].localPosition =
                pivote + adicional * (_posicionesReposo[i] - pivote);

            _segmentos[i].localRotation = adicional * _rotacionesReposo[i];
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 ancla = Application.isPlaying
            ? _anclaMundo
            : transform.TransformPoint(detectarAnclaAutomatica ? DetectarAncla() : anclaLocal);

        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.35f);
        Gizmos.DrawWireSphere(ancla + Vector3.up * (radioDeteccion * 0.5f), radioDeteccion);

        Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.35f);
        Gizmos.DrawWireSphere(ancla, radioVecinos);

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawSphere(ancla, 0.02f);

        if (_pivotesLocales == null) return;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        for (int i = 0; i < _pivotesLocales.Length; i++)
            Gizmos.DrawSphere(transform.TransformPoint(_pivotesLocales[i]), 0.008f);
    }
}