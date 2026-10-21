// RebaSessionRecorder.cs
//
// Guarda REBA de cada sesión para el análisis de la tesis.
//
// Por sesión, en <persistentDataPath>/REBA/<fecha>_P<id del participante>/:
//   muestras.csv  5 filas por segundo mientras hay actividad o tutorial: subtarea, si la muestra
//                 cuenta para el análisis (y si no, por qué), ángulos clave, puntos de cada parte
//                 y REBA. Es la tabla para gráficas en el tiempo y análisis por muestra.
//   crudos.csv    la orientación de cada segmento (cuaternión) en las mismas filas, para poder
//                 recalcular todo después con otros criterios.
//   eventos.csv   calibraciones, cambios de fase, avisos del semáforo, sensores perdidos.
//   sesion.json   cómo se midió: umbrales, sensores, montajes, factores, criterios de "cuenta".
//
// Y para todos los participantes, en <persistentDataPath>/REBA/:
//   resumen_participantes.csv  una fila por participante x actividad x momento (pre/tutorial/post).
//   resumen_subtareas.csv      lo mismo, separado por subtarea (Cavar hueco, Cortar tallo...).
//
// Qué muestras "cuentan" (columna cuenta = 1). El resto se guarda igual, con su motivo:
//   calibrando         la persona está en postura N para la recalibración
//   sin tarea          en la actividad no está haciendo ninguna subtarea (camina hacia la
//                      siguiente planta, mira alrededor...). En el tutorial siempre hay un paso.
//   caminando          el visor se mueve en horizontal a más de umbralCaminando
//   mirando a un lado  la cabeza está girada más de umbralCabezaGirada respecto al tronco
//                      (p. ej. leyendo instrucciones a un lado)
// El resumen trae el REBA de todo el tiempo (_total) y el del tiempo que cuenta (el resto).
//
// El momento se etiqueta solo con GameFlowManager.Fase, por actividad:
//   "pre" la actividad antes del tutorial, "tutorial", "post" la actividad después del tutorial.
// Cada momento se graba desde que termina una recalibración del Game Master: la que se haga
// después de que empezó, o una hecha poco antes (calibracionReciente) que no se haya usado.
//
// Factores de REBA que no miden los sensores: los deciden las investigadoras, uno por actividad
// (iguales en pre, tutorial y post). Carga < 5 kg, agarre bueno y sin impactos son fijos.
//
// Al terminar la sesión guarda el resumen pre/tutorial/post en el historial del participante
// (Capacitacion -> ActividadRealizada -> RebaData en usuarios.json) y lo deja listo para la
// pantalla de resultados del visor.
//
// CSV para Excel en español: separador ';' y decimales con coma. El participante solo se
// identifica por su Id (no se guarda nombre ni correo en los CSV).
//
// Sin GameFlowManager (escena de prueba), o en Core mientras no hay una sesión en curso, se
// graba a mano con los botones en pantalla.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PujJoycon
{
    // Después de RebaPostureEstimator, que calcula REBA en su Update.
    [DefaultExecutionOrder(50)]
    public class RebaSessionRecorder : MonoBehaviour
    {
        // Lo que REBA pregunta y los sensores no miden: lo deciden las investigadoras por actividad.
        [Serializable]
        public class FactoresActividad
        {
            public ActivityType actividad;
            [Tooltip("Hombro elevado (encogido): +1 al brazo.")]
            public bool hombroElevado;
            [Tooltip("Brazo apoyado o postura a favor de la gravedad: −1 al brazo.")]
            public bool brazoApoyadoOAFavorDeGravedad;
            [Tooltip("Una o más partes del cuerpo estáticas (más de 1 minuto): +1.")]
            public bool partesEstaticas;
            [Tooltip("Movimientos repetitivos (más de 4 por minuto, en rango pequeño): +1.")]
            public bool movimientosRepetitivos;
            [Tooltip("Cambios posturales grandes y rápidos, o postura inestable: +1.")]
            public bool cambiosPosturalesOInestables;
        }

        private static FactoresActividad[] FactoresPorDefecto()
        {
            var list = new List<FactoresActividad>();
            foreach (ActivityType a in Enum.GetValues(typeof(ActivityType))) list.Add(new FactoresActividad { actividad = a });
            return list.ToArray();
        }

        public const string CarpetaRaiz = "REBA";
        private const string ArchivoParticipantes = "resumen_participantes.csv";
        private const string ArchivoSubtareas = "resumen_subtareas.csv";
        private const string SinTarea = "sin tarea";
        private const char Sep = ';';

        [SerializeField] private RebaPostureEstimator estimator;
        [SerializeField] private JoyconReceiver receiver;
        [SerializeField] private JoyconSessionCalibrator calibrator;

        [Header("Muestreo")]
        [SerializeField, Range(1f, 30f)] private float frecuenciaMuestreo = 5f;
        [SerializeField] private bool grabarTutorial = true;
        [Tooltip("Guardar crudos.csv (orientación de cada segmento) para poder recalcular después.")]
        [SerializeField] private bool guardarCrudos = true;

        [Header("Calibración antes de cada momento")]
        [Tooltip("Cada momento (pre, tutorial, post) se graba desde que termina una recalibración del Game Master.")]
        [SerializeField] private bool esperarCalibracionParaGrabar = true;
        [Tooltip("Una calibración terminada hace menos de estos segundos (y que no usó otro momento) vale para el " +
                 "momento que empieza: no hay que volver a calibrar si se acaba de hacer.")]
        [SerializeField] private float calibracionReciente = 90f;

        [Header("Qué muestras cuentan para el análisis")]
        [Tooltip("Velocidad horizontal del visor (m/s) desde la que se considera que la persona camina.")]
        [SerializeField] private float umbralCaminando = 0.4f;
        [Tooltip("Giro de la cabeza respecto al tronco (grados) desde el que se considera que mira a un lado.")]
        [SerializeField] private float umbralCabezaGirada = 45f;

        [Header("Factores de REBA que no miden los sensores (uno por actividad)")]
        [Tooltip("Iguales en pre, tutorial y post. Carga < 5 kg, agarre bueno y sin impactos son fijos.")]
        [SerializeField] private FactoresActividad[] factoresPorActividad = FactoresPorDefecto();

        [Header("Historial del participante (usuarios.json)")]
        [SerializeField] private bool guardarEnHistorial = true;

        [Header("Prueba sin GameFlowManager (escena PruebaJoycons)")]
        [SerializeField] private ActivityType actividadPrueba = ActivityType.Siembra;
        [SerializeField] private int participantePruebaId = 0;
        [SerializeField] private bool showOverlay = true;

        // Lo acumulado de un momento (o de una subtarea dentro de él).
        private sealed class Acumulado
        {
            public readonly RebaStats total = new RebaStats();   // todo menos la calibración
            public readonly RebaStats util = new RebaStats();    // solo lo que cuenta
            public readonly Exposicion exposicion = new Exposicion();
            public int acciones, alertas;

            public void Merge(Acumulado o)
            {
                total.Merge(o.total);
                util.Merge(o.util);
                exposicion.Merge(o.exposicion);
                acciones += o.acciones;
                alertas += o.alertas;
            }
        }

        // % del tiempo que cuenta con cada parte del cuerpo por encima de su umbral (exposición).
        private sealed class Exposicion
        {
            public double tiempo, troncoMas20, troncoMas60, troncoTorcido, cuelloMas20, brazoMas45, brazoMas90, rodillaMas60;

            public void Add(PostureAngles a, RebaInputs izq, RebaInputs der, double dt)
            {
                tiempo += dt;
                if (a.hasTrunk && a.trunkFlexion > 20f) troncoMas20 += dt;
                if (a.hasTrunk && a.trunkFlexion > 60f) troncoMas60 += dt;
                if (izq.trunkTwistedOrSideBent) troncoTorcido += dt;
                if (a.hasNeck && a.neckFlexion > 20f) cuelloMas20 += dt;
                float brazo = Mathf.Max(a.left.measured ? izq.upperArmFlexion : 0f, a.right.measured ? der.upperArmFlexion : 0f);
                if (brazo > 45f) brazoMas45 += dt;
                if (brazo > 90f) brazoMas90 += dt;
                if (a.hasKnee && Mathf.Max(a.kneeFlexionLeft, a.kneeFlexionRight) > 60f) rodillaMas60 += dt;
            }

            public void Merge(Exposicion o)
            {
                tiempo += o.tiempo; troncoMas20 += o.troncoMas20; troncoMas60 += o.troncoMas60;
                troncoTorcido += o.troncoTorcido; cuelloMas20 += o.cuelloMas20; brazoMas45 += o.brazoMas45;
                brazoMas90 += o.brazoMas90; rodillaMas60 += o.rodillaMas60;
            }

            public double Pct(double t) => tiempo > 0 ? 100.0 * t / tiempo : 0;
        }

        // Un tramo = una actividad en un momento en un intento.
        private sealed class Tramo
        {
            public int indice;
            public ActivityType actividad;
            public string condicion;
            public int intento;
            public readonly Acumulado todo = new Acumulado();
            public readonly Dictionary<string, Acumulado> porSubtarea = new Dictionary<string, Acumulado>();

            public Acumulado Subtarea(string nombre)
            {
                if (!porSubtarea.TryGetValue(nombre, out var a)) porSubtarea[nombre] = a = new Acumulado();
                return a;
            }
        }

        private bool _active, _manual, _finalized;
        private string _folder, _sessionId;
        private DateTime _sessionStart;
        private float _sessionT0;
        private int _participantId;
        private string _participantName, _age;
        private bool _leftHanded;
        private StreamWriter _samples, _raw, _events;
        private bool _samplesHeaderWritten, _rawHeaderWritten;
        private float _nextFlush;

        private FaseSesion _lastFase = FaseSesion.SinSesion;
        private int _lastIndex = int.MinValue;
        private Tramo _tramo;
        private float _tramoT0, _nextSampleT;
        private string _lastSubtarea = SinTarea;
        private int _lastAcciones;
        private readonly HashSet<int> _seenTutorial = new HashSet<int>();
        private readonly Dictionary<string, int> _attempts = new Dictionary<string, int>();
        private readonly List<Tramo> _tramos = new List<Tramo>();
        private bool _reachedResults;
        private bool[] _serverWasReceiving = new bool[0];
        private readonly CsvRow _row = new CsvRow();
        private readonly CsvRow _rawRow = new CsvRow();
        private RebaSubtareas _subtareas;

        // Velocidad del visor (para "caminando").
        private readonly Queue<(float t, Vector3 p)> _headTrail = new Queue<(float, Vector3)>();

        // Momento que ya empezó en el juego pero espera la calibración para grabarse.
        private bool _pending;
        private string _pendingCondicion;
        private ActivityType _pendingActividad;
        private int _pendingIndice;
        private int _pendingCalibraciones;
        private int _calibracionesUsadas = -1; // la última calibración que ya abrió un momento

        // Resultados de la última sesión terminada (para la pantalla de resultados del visor).
        private List<ActividadRealizada> _lastResults = new List<ActividadRealizada>();

        public bool Grabando => _tramo != null;
        public string CarpetaSesion => _folder;

        /// <summary>true cuando ya se calcularon los resultados de la sesión actual (al llegar a Resultados).</summary>
        public bool ResultadosListos => _finalized;

        /// <summary>Resumen REBA (pre / tutorial / post) de una actividad de la última sesión terminada, si se midió.</summary>
        public bool TryGetResultado(ActivityType actividad, out RebaData reba)
        {
            foreach (var r in _lastResults)
            {
                if (r.Tipo == actividad && r.Reba != null)
                {
                    reba = r.Reba;
                    return true;
                }
            }
            reba = null;
            return false;
        }

        /// <summary>Para que otros componentes (p. ej. el semáforo) dejen constancia en eventos.csv.</summary>
        public void RegistrarEvento(string tipo, string detalle)
        {
            if (!_active) return;
            LogEvent(tipo, detalle);
            if (tipo == "semaforo" && _tramo != null)
            {
                _tramo.todo.alertas++;
                _tramo.Subtarea(_lastSubtarea).alertas++;
            }
        }

        /// <summary>Hay un momento (pre/tutorial/post) esperando una calibración para empezar a grabarse.</summary>
        public bool EsperandoCalibracion => _active && _pending;
        public string MomentoPendiente => _pending ? $"{_pendingActividad} · {_pendingCondicion}" : "";

        private void Awake()
        {
            if (estimator == null) estimator = FindAnyObjectByType<RebaPostureEstimator>();
            if (receiver == null) receiver = FindAnyObjectByType<JoyconReceiver>();
            if (calibrator == null) calibrator = FindAnyObjectByType<JoyconSessionCalibrator>();

            // Se agregan solos (así no hay que tocar Core).
            if (GetComponent<RebaResultadosEnPantalla>() == null) gameObject.AddComponent<RebaResultadosEnPantalla>();
            if (GetComponent<RebaSemaforoPostura>() == null) gameObject.AddComponent<RebaSemaforoPostura>();
            _subtareas = GetComponent<RebaSubtareas>();
            if (_subtareas == null) _subtareas = gameObject.AddComponent<RebaSubtareas>();
        }

        private void OnEnable()
        {
            if (estimator != null) estimator.Calibrated += OnCalibrated;
        }

        private void OnDisable()
        {
            if (estimator != null) estimator.Calibrated -= OnCalibrated;
            if (_active) EndSession("app cerrada o componente desactivado");
        }

        private void OnApplicationQuit()
        {
            if (_active) EndSession("app cerrada");
        }

        // ------------------------------------------------------------------
        // Flujo de la sesión
        // ------------------------------------------------------------------

        private void Update()
        {
            TrackHead();

            var flow = GameFlowManager.Instance;
            // Una prueba manual en Core (sin sesión) se cierra sola si el Game Master inicia una sesión de verdad.
            if (flow != null && _manual && _active && flow.simulacionEnCurso)
            {
                EndSession("prueba terminada: empezó una sesión");
            }

            if (flow != null && !_manual)
            {
                bool running = flow.simulacionEnCurso && flow.usuarioActivo != null;
                if (running && !_active) StartSession(flow.usuarioActivo);
                if (_active)
                {
                    if (!flow.simulacionEnCurso)
                    {
                        EndSession(_reachedResults ? "completa" : "interrumpida");
                        return;
                    }
                    if (flow.Fase != _lastFase || flow.IndiceActividadActual != _lastIndex)
                    {
                        OnPhase(flow.Fase, flow.IndiceActividadActual, flow.ActividadActual);
                        _lastFase = flow.Fase;
                        _lastIndex = flow.IndiceActividadActual;
                    }
                    if (flow.Fase == FaseSesion.Resultados && !_reachedResults)
                    {
                        _reachedResults = true;
                        FinalizeData("completa");
                    }
                    // El momento pendiente empieza a grabarse cuando termina una calibración lanzada después.
                    if (_pending && calibrator != null && !calibrator.IsCalibrating &&
                        calibrator.CalibracionesTerminadas > _pendingCalibraciones)
                    {
                        StartPending(null);
                    }
                }
            }

            if (!_active) return;
            Sample();
            MonitorServers();
            if (Time.unscaledTime >= _nextFlush)
            {
                _samples?.Flush();
                _raw?.Flush();
                _events?.Flush();
                _nextFlush = Time.unscaledTime + 1f;
            }
        }

        private void StartSession(UserProfile user)
        {
            _active = true;
            _finalized = false;
            _reachedResults = false;
            _sessionStart = DateTime.Now;
            _sessionT0 = Time.unscaledTime;
            _participantId = user != null ? user.Id : participantePruebaId;
            _participantName = user?.Nombre;
            _age = user?.Edad ?? "";
            _leftHanded = user != null && user.EsZurdo;
            _sessionId = $"{_sessionStart:yyyy-MM-dd_HH-mm-ss}_P{_participantId:000}" + (_manual ? "_prueba" : "");
            _folder = Path.Combine(Application.persistentDataPath, CarpetaRaiz, _sessionId);
            Directory.CreateDirectory(_folder);

            var utf8Bom = new UTF8Encoding(true); // con BOM: Excel muestra bien las tildes
            _samples = new StreamWriter(Path.Combine(_folder, "muestras.csv"), false, utf8Bom);
            _raw = guardarCrudos ? new StreamWriter(Path.Combine(_folder, "crudos.csv"), false, utf8Bom) : null;
            _events = new StreamWriter(Path.Combine(_folder, "eventos.csv"), false, utf8Bom);
            _events.WriteLine(string.Join(Sep.ToString(), "fecha_hora", "t_sesion_s", "tipo", "detalle"));
            _samplesHeaderWritten = false;
            _rawHeaderWritten = false;

            _tramos.Clear();
            _attempts.Clear();
            _seenTutorial.Clear();
            _tramo = null;
            _pending = false;
            _lastFase = FaseSesion.SinSesion;
            _lastIndex = int.MinValue;
            _serverWasReceiving = new bool[receiver != null ? receiver.ServerCount : 0];

            WriteMeta("en curso");
            LogEvent("inicio_sesion", $"participante {_participantId}" + (_manual ? " (prueba manual)" : ""));
            Debug.Log($"[REBA] Grabando la sesión en {_folder}", this);
        }

        private void OnPhase(FaseSesion fase, int indice, ActivityType? actividad)
        {
            LogEvent("fase", $"{fase} | actividad {(actividad?.ToString() ?? "-")} | índice {indice}");

            string condicion = null;
            if (actividad != null && indice >= 0)
            {
                if (fase == FaseSesion.Tutorial)
                {
                    _seenTutorial.Add(indice);
                    if (grabarTutorial) condicion = "tutorial";
                }
                else if (fase == FaseSesion.Actividad)
                {
                    condicion = _seenTutorial.Contains(indice) ? "post" : "pre";
                }
            }

            if (_pending) LogEvent("aviso", $"{MomentoPendiente} no se grabó: se pasó a otra fase sin calibrar");
            _pending = false;

            if (condicion != null && esperarCalibracionParaGrabar && calibrator != null && !CalibracionRecienteSinUsar())
            {
                // Se cierra el momento anterior y el nuevo queda esperando la calibración.
                SetCondition(null, null, -1);
                _pending = true;
                _pendingCondicion = condicion;
                _pendingActividad = actividad.Value;
                _pendingIndice = indice;
                _pendingCalibraciones = calibrator.CalibracionesTerminadas;
                LogEvent("esperando_calibracion", $"{actividad} {condicion}: se graba cuando terminen la calibración");
                return;
            }
            SetCondition(condicion, actividad, indice);
        }

        // ¿Hay una calibración terminada hace poco que todavía no abrió ningún momento?
        private bool CalibracionRecienteSinUsar()
        {
            if (calibrator == null || calibrator.IsCalibrating) return false;
            if (calibrator.CalibracionesTerminadas <= _calibracionesUsadas || calibrator.CalibracionesTerminadas == 0) return false;
            return Time.unscaledTime - calibrator.UltimaCalibracionFin <= calibracionReciente;
        }

        /// <summary>Empieza a grabar el momento pendiente. motivoSinCalibrar != null: se empezó sin calibrar.</summary>
        private void StartPending(string motivoSinCalibrar)
        {
            if (!_pending) return;
            _pending = false;
            SetCondition(_pendingCondicion, _pendingActividad, _pendingIndice);
            if (motivoSinCalibrar != null) LogEvent("aviso", motivoSinCalibrar);
        }

        private void SetCondition(string condicion, ActivityType? actividad, int indice)
        {
            if (_tramo != null && condicion == _tramo.condicion && indice == _tramo.indice) return;

            if (_tramo != null)
            {
                LogEvent("fin_tramo", $"{_tramo.actividad} {_tramo.condicion} intento {_tramo.intento}: " +
                                      $"{F(_tramo.todo.total.DurationSeconds)} s, de los que cuentan {F(_tramo.todo.util.DurationSeconds)} s");
                _tramo = null;
            }

            if (condicion == null || actividad == null) return;
            ApplyFactors(actividad.Value);

            string key = $"{indice}|{condicion}";
            _attempts.TryGetValue(key, out int n);
            _attempts[key] = ++n;
            _tramo = new Tramo { indice = indice, actividad = actividad.Value, condicion = condicion, intento = n };
            _tramos.Add(_tramo);
            _tramoT0 = Time.unscaledTime;
            _nextSampleT = Time.unscaledTime;
            _lastSubtarea = SinTarea;
            _lastAcciones = _subtareas != null ? _subtareas.AccionesCompletadas(actividad) : 0;
            if (calibrator != null) _calibracionesUsadas = calibrator.CalibracionesTerminadas;
            LogEvent("inicio_tramo", $"{_tramo.actividad} {condicion} intento {n}");

            // La calibración completa la lanza el Game Master: si se olvidó, queda anotado para revisar después.
            if (!_manual && calibrator != null && !calibrator.CalibradoEnEstaSesion)
                LogEvent("aviso", "el tramo empezó SIN la calibración completa de este participante");
        }

        private FactoresActividad FindFactors(ActivityType actividad)
        {
            if (factoresPorActividad == null) return null;
            foreach (var f in factoresPorActividad) if (f != null && f.actividad == actividad) return f;
            return null;
        }

        private void ApplyFactors(ActivityType actividad)
        {
            if (estimator == null) return;
            var f = FindFactors(actividad) ?? new FactoresActividad { actividad = actividad };

            // Fijos en este estudio.
            estimator.LoadKg = 0f;          // < 5 kg
            estimator.Coupling = 0;         // agarre bueno
            estimator.ShockOrRapidForce = false;
            estimator.TrunkTwistOrSideBendSource = FuenteSiNo.Sensores;
            estimator.NeckTwistOrSideBendSource = FuenteSiNo.Sensores;
            estimator.ArmRotatedSource = FuenteSiNo.Sensores;
            estimator.WristDeviatedOrTwistedSource = FuenteSiNo.Sensores;

            // Los de la actividad.
            estimator.ShoulderRaised = f.hombroElevado;
            estimator.ArmSupported = f.brazoApoyadoOAFavorDeGravedad;
            estimator.StaticPosture = f.partesEstaticas;
            estimator.RepetitiveMovements = f.movimientosRepetitivos;
            estimator.RapidChangesOrUnstable = f.cambiosPosturalesOInestables;

            LogEvent("factores", $"{actividad}: hombro elevado {SiNo(f.hombroElevado)}, " +
                                 $"brazo apoyado o a favor de la gravedad {SiNo(f.brazoApoyadoOAFavorDeGravedad)}, " +
                                 $"partes estáticas {SiNo(f.partesEstaticas)}, repetitivos {SiNo(f.movimientosRepetitivos)}, " +
                                 $"cambios posturales o inestable {SiNo(f.cambiosPosturalesOInestables)}");
        }

        /// <summary>Deja una entrada por actividad (3), conservando los valores que ya existan.</summary>
        [ContextMenu("Restablecer factores (una entrada por actividad)")]
        public void RestablecerFactores()
        {
            var nuevos = FactoresPorDefecto();
            for (int i = 0; i < nuevos.Length; i++)
            {
                var existente = FindFactors(nuevos[i].actividad);
                if (existente != null) nuevos[i] = existente;
            }
            factoresPorActividad = nuevos;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void EndSession(string motivo)
        {
            if (!_active) return;
            if (_pending) LogEvent("aviso", $"{MomentoPendiente} no se grabó: nunca se calibró");
            _pending = false;
            SetCondition(null, null, -1);
            if (!_finalized) FinalizeData(motivo);
            LogEvent("fin_sesion", motivo);
            _samples?.Dispose();
            _raw?.Dispose();
            _events?.Dispose();
            _samples = _raw = _events = null;
            _active = false;
            _manual = false;
            Debug.Log($"[REBA] Sesión guardada ({motivo}) en {_folder}", this);
        }

        // ------------------------------------------------------------------
        // Muestras
        // ------------------------------------------------------------------

        private void TrackHead()
        {
            var cam = Camera.main;
            if (cam == null) return;
            float now = Time.unscaledTime;
            _headTrail.Enqueue((now, cam.transform.position));
            while (_headTrail.Count > 2 && now - _headTrail.Peek().t > 0.5f) _headTrail.Dequeue();
        }

        // Velocidad horizontal del visor en el último medio segundo (m/s).
        private float HeadSpeed()
        {
            if (_headTrail.Count < 2) return 0f;
            var first = _headTrail.Peek();
            (float t, Vector3 p) last = default;
            foreach (var e in _headTrail) last = e;
            float dt = last.t - first.t;
            if (dt < 0.05f) return 0f;
            Vector3 d = last.p - first.p;
            d.y = 0f;
            return d.magnitude / dt;
        }

        private void Sample()
        {
            if (_tramo == null || estimator == null || !estimator.IsCalibrated) return;
            float now = Time.unscaledTime;
            if (now < _nextSampleT) return;
            float period = 1f / Mathf.Max(1f, frecuenciaMuestreo);
            _nextSampleT += period;
            if (_nextSampleT < now) _nextSampleT = now + period; // tras una pausa no se "ponen al día" filas de golpe

            // Subtarea y acciones.
            var fase = _manual ? (_tramo.condicion == "tutorial" ? FaseSesion.Tutorial : FaseSesion.Actividad)
                               : (GameFlowManager.Instance != null ? GameFlowManager.Instance.Fase : FaseSesion.Actividad);
            string subtarea = _subtareas != null ? _subtareas.Subtarea(fase, _tramo.actividad) : "";
            if (string.IsNullOrEmpty(subtarea)) subtarea = SinTarea;
            int acciones = _subtareas != null ? _subtareas.AccionesCompletadas(_tramo.actividad) : 0;
            if (acciones > _lastAcciones)
            {
                // La acción se completa al terminar la subtarea: se le cuenta a la que venía.
                _tramo.todo.acciones += acciones - _lastAcciones;
                _tramo.Subtarea(_lastSubtarea == SinTarea ? subtarea : _lastSubtarea).acciones += acciones - _lastAcciones;
            }
            _lastAcciones = acciones;

            // ¿Cuenta? (en la escena de prueba no hay objetos de la actividad: ahí no se exige subtarea)
            var a = estimator.CurrentAngles;
            string motivo = "";
            if (calibrator != null && calibrator.IsCalibrating) motivo = "calibrando";
            else if (subtarea == SinTarea && !_manual) motivo = SinTarea;
            else if (HeadSpeed() > umbralCaminando) motivo = "caminando";
            else if (a.hasNeck && Mathf.Abs(a.neckTwist) > umbralCabezaGirada) motivo = "mirando a un lado";
            bool cuenta = motivo == "";

            WriteSampleRow(now, subtarea, acciones, cuenta, motivo);
            if (_raw != null) WriteRawRow(now);

            if (motivo != "calibrando")
            {
                var li = estimator.LeftInputs;
                var ri = estimator.RightInputs;
                foreach (var acc in new[] { _tramo.todo, _tramo.Subtarea(subtarea) })
                {
                    acc.total.Add(estimator.Left, estimator.Right, period);
                    if (!cuenta) continue;
                    acc.util.Add(estimator.Left, estimator.Right, period);
                    acc.exposicion.Add(a, li, ri, period);
                }
            }
            _lastSubtarea = subtarea;
        }

        private void WriteSampleRow(float now, string subtarea, int acciones, bool cuenta, string motivo)
        {
            var r = _row;
            var a = estimator.CurrentAngles;
            RebaResult L = estimator.Left, R = estimator.Right, worst = estimator.Worst;

            r.Begin();
            r.Add("fecha_hora", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            r.Add("t_sesion_s", now - _sessionT0);
            r.Add("participante_id", _participantId);
            r.Add("actividad", _tramo.actividad.ToString());
            r.Add("momento", _tramo.condicion);
            r.Add("intento", _tramo.intento);
            r.Add("t_momento_s", now - _tramoT0);
            r.Add("subtarea", subtarea);
            r.Add("acciones_completadas", acciones);
            r.Add("cuenta", cuenta);
            r.Add("motivo", motivo);

            // Ángulos (grados).
            r.Add("tronco_flexion", a.trunkFlexion, a.hasTrunk);
            r.Add("tronco_lateral", a.trunkLateral, a.hasTrunk);
            r.Add("tronco_torsion", a.trunkTwist, a.hasTrunkTwist);
            r.Add("cuello_flexion", a.neckFlexion, a.hasNeck);
            r.Add("cuello_lateral", a.neckLateral, a.hasNeck);
            r.Add("cuello_torsion", a.neckTwist, a.hasNeck);
            r.Add("rodilla_izq", a.kneeFlexionLeft, a.hasKnee);
            r.Add("rodilla_der", a.kneeFlexionRight, a.hasKnee);
            r.Add("brazo_izq_flexion", estimator.LeftInputs.upperArmFlexion, a.left.measured);
            r.Add("brazo_izq_abduccion", a.left.upperArmAbduction, a.left.measured);
            r.Add("brazo_der_flexion", estimator.RightInputs.upperArmFlexion, a.right.measured);
            r.Add("brazo_der_abduccion", a.right.upperArmAbduction, a.right.measured);
            r.Add("codo_izq", a.left.elbowFlexion, a.left.hasElbow);
            r.Add("codo_der", a.right.elbowFlexion, a.right.hasElbow);
            r.Add("muneca_izq", a.left.wristFlexion, a.left.hasWrist);
            r.Add("muneca_der", a.right.wristFlexion, a.right.hasWrist);

            // Puntos de cada parte (hoja REBA) y REBA.
            r.Add("p_tronco", L.trunk);
            r.Add("p_cuello", L.neck);
            r.Add("p_piernas", L.legs);
            r.Add("p_brazo_izq", L.upperArm);
            r.Add("p_brazo_der", R.upperArm);
            r.Add("p_antebrazo_izq", L.lowerArm);
            r.Add("p_antebrazo_der", R.lowerArm);
            r.Add("p_muneca_izq", L.wrist);
            r.Add("p_muneca_der", R.wrist);
            r.Add("p_actividad", L.activity);
            r.Add("reba_izq", L.final);
            r.Add("reba_der", R.final);
            r.Add("reba", worst.final);
            r.Add("riesgo", Reba.RiskName(worst.risk));

            if (!_samplesHeaderWritten)
            {
                _samples.WriteLine(r.Header(Sep));
                _samplesHeaderWritten = true;
            }
            _samples.WriteLine(r.Values(Sep));
        }

        // Orientación de cada segmento en el marco del cuerpo, para recalcular después.
        private void WriteRawRow(float now)
        {
            var r = _rawRow;
            r.Begin();
            r.Add("t_sesion_s", now - _sessionT0);
            r.Add("actividad", _tramo.actividad.ToString());
            r.Add("momento", _tramo.condicion);
            for (int s = 0; s < _serverWasReceiving.Length; s++)
            {
                r.Add($"pc{s + 1}_recibiendo", receiver != null && s < receiver.ServerCount && receiver.GetServerStatus(s).IsReceiving);
            }
            foreach (BodySegment seg in Enum.GetValues(typeof(BodySegment)))
            {
                string n = SegmentColumn(seg);
                bool ok = estimator.TryGetSegment(seg, out var q);
                r.Add($"{n}_ok", ok);
                r.Add($"{n}_qx", q.x, ok);
                r.Add($"{n}_qy", q.y, ok);
                r.Add($"{n}_qz", q.z, ok);
                r.Add($"{n}_qw", q.w, ok);
            }
            if (!_rawHeaderWritten)
            {
                _raw.WriteLine(r.Header(Sep));
                _rawHeaderWritten = true;
            }
            _raw.WriteLine(r.Values(Sep));
        }

        private static string SegmentColumn(BodySegment s)
        {
            switch (s)
            {
                case BodySegment.Pelvis: return "pelvis";
                case BodySegment.Trunk: return "tronco";
                case BodySegment.Head: return "cabeza";
                case BodySegment.UpperArmLeft: return "brazo_izq";
                case BodySegment.ForearmLeft: return "antebrazo_izq";
                case BodySegment.HandLeft: return "mano_izq";
                case BodySegment.UpperArmRight: return "brazo_der";
                case BodySegment.ForearmRight: return "antebrazo_der";
                case BodySegment.HandRight: return "mano_der";
                case BodySegment.ThighLeft: return "muslo_izq";
                case BodySegment.ShankLeft: return "pierna_izq";
                case BodySegment.ThighRight: return "muslo_der";
                case BodySegment.ShankRight: return "pierna_der";
                default: return s.ToString().ToLowerInvariant();
            }
        }

        private void OnCalibrated()
        {
            if (!_active || estimator == null) return;
            var sb = new StringBuilder();
            foreach (var b in estimator.Bindings)
            {
                if (b.trackedTransform == null && b.sensorIndex < 0) continue;
                string fuente = b.trackedTransform != null ? b.trackedTransform.name : $"sensor {b.sensorIndex}";
                Vector3 m = b.mountingEuler;
                sb.Append($"{b.segment}={fuente} montaje({F(m.x)} {F(m.y)} {F(m.z)}), ");
            }
            LogEvent("calibracion", sb.ToString().TrimEnd(',', ' '));
        }

        private void MonitorServers()
        {
            if (receiver == null) return;
            int n = Mathf.Min(_serverWasReceiving.Length, receiver.ServerCount);
            for (int s = 0; s < n; s++)
            {
                var st = receiver.GetServerStatus(s);
                if (st.IsReceiving == _serverWasReceiving[s]) continue;
                LogEvent(st.IsReceiving ? "servidor_recibiendo" : "servidor_sin_datos", $"PC {s + 1} {st.Endpoint}");
                _serverWasReceiving[s] = st.IsReceiving;
            }
        }

        private void LogEvent(string tipo, string detalle)
        {
            if (_events == null) return;
            _events.WriteLine(string.Join(Sep.ToString(),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                F(Time.unscaledTime - _sessionT0), tipo, CsvRow.Clean(detalle)));
        }

        // ------------------------------------------------------------------
        // Al terminar: resúmenes, sesion.json, historial y pantalla de resultados
        // ------------------------------------------------------------------

        private void FinalizeData(string estado)
        {
            _finalized = true;
            SetCondition(null, null, -1);
            try
            {
                WriteSummaries(estado);
                WriteMeta(estado);
                var results = BuildResults(estado);
                _lastResults = results;
                if (guardarEnHistorial && !_manual) SaveToHistory(results);
                LogEvent("resumen_guardado", estado);
            }
            catch (Exception e)
            {
                Debug.LogError($"[REBA] Error guardando el resumen: {e}", this);
                LogEvent("error", e.Message);
            }
            _events?.Flush();
            _samples?.Flush();
            _raw?.Flush();
        }

        private static readonly string[] SummaryColumns =
        {
            "intentos", "duracion_total_s", "duracion_util_s", "pct_util", "acciones_completadas", "alertas_semaforo",
            "reba_moda_total", "reba_promedio_total", "reba_p90_total",
            "reba_moda", "reba_mediana", "reba_promedio", "reba_p90", "reba_max",
            "pct_riesgo_insignificante", "pct_riesgo_bajo", "pct_riesgo_medio", "pct_riesgo_alto", "pct_riesgo_muy_alto",
            "pct_tronco_mas_20", "pct_tronco_mas_60", "pct_tronco_torcido_o_inclinado", "pct_cuello_mas_20",
            "pct_brazo_mas_45", "pct_brazo_mas_90", "pct_rodilla_mas_60",
            "prom_p_tronco", "prom_p_cuello", "prom_p_piernas", "prom_p_brazo", "prom_p_antebrazo", "prom_p_muneca",
            "estado_sesion",
        };

        private static string Header(bool conSubtarea)
        {
            var h = new List<string> { "sesion", "fecha", "participante_id", "edad", "mano_dominante", "actividad", "momento" };
            if (conSubtarea) h.Add("subtarea");
            h.AddRange(SummaryColumns);
            return string.Join(Sep.ToString(), h);
        }

        private void WriteSummaries(string estado)
        {
            var participantes = new List<string>();
            var subtareas = new List<string>();
            foreach (var g in GroupByCondition())
            {
                participantes.Add(SummaryLine(g, null, g.todo, estado));
                var nombres = new List<string>(g.porSubtarea.Keys);
                nombres.Sort(StringComparer.Ordinal);
                foreach (var s in nombres) subtareas.Add(SummaryLine(g, s, g.porSubtarea[s], estado));
            }
            Append(ArchivoParticipantes, Header(false), participantes);
            Append(ArchivoSubtareas, Header(true), subtareas);
        }

        private void Append(string archivo, string header, List<string> lines)
        {
            string path = Path.Combine(Application.persistentDataPath, CarpetaRaiz, archivo);
            bool isNew = !File.Exists(path);
            using (var w = new StreamWriter(path, true, new UTF8Encoding(true)))
            {
                if (isNew) w.WriteLine(header);
                foreach (var line in lines) w.WriteLine(line);
            }
        }

        private string SummaryLine(Tramo g, string subtarea, Acumulado acc, string estado)
        {
            RebaStats t = acc.total, u = acc.util;
            Exposicion e = acc.exposicion;
            var v = new List<string>
            {
                _sessionId, _sessionStart.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                _participantId.ToString(), CsvRow.Clean(_age), _leftHanded ? "izquierda" : "derecha",
                g.actividad.ToString(), g.condicion,
            };
            if (subtarea != null) v.Add(CsvRow.Clean(subtarea));
            v.AddRange(new[]
            {
                g.intento.ToString(), F(t.DurationSeconds), F(u.DurationSeconds),
                F(t.DurationSeconds > 0 ? 100.0 * u.DurationSeconds / t.DurationSeconds : 0),
                acc.acciones.ToString(), acc.alertas.ToString(),
                t.Count > 0 ? t.Mode.ToString() : "", N(t, t.Mean), N(t, t.Percentile(90)),
                u.Count > 0 ? u.Mode.ToString() : "", N(u, u.Median), N(u, u.Mean), N(u, u.Percentile(90)),
                u.Count > 0 ? u.Max.ToString() : "",
                N(u, u.RiskPercent(RebaRisk.Negligible)), N(u, u.RiskPercent(RebaRisk.Low)), N(u, u.RiskPercent(RebaRisk.Medium)),
                N(u, u.RiskPercent(RebaRisk.High)), N(u, u.RiskPercent(RebaRisk.VeryHigh)),
                N(u, e.Pct(e.troncoMas20)), N(u, e.Pct(e.troncoMas60)), N(u, e.Pct(e.troncoTorcido)), N(u, e.Pct(e.cuelloMas20)),
                N(u, e.Pct(e.brazoMas45)), N(u, e.Pct(e.brazoMas90)), N(u, e.Pct(e.rodillaMas60)),
                N(u, u.MeanTrunk), N(u, u.MeanNeck), N(u, u.MeanLegs), N(u, u.MeanUpperArm), N(u, u.MeanLowerArm), N(u, u.MeanWrist),
                estado,
            });
            return string.Join(Sep.ToString(), v);
        }

        // Vacío si no hay muestras (para que en Excel no aparezca un 0 que no es real).
        private static string N(RebaStats s, double v) => s.Count > 0 ? F(v) : "";

        // Junta los intentos de cada actividad x momento.
        private List<Tramo> GroupByCondition()
        {
            var groups = new List<Tramo>();
            foreach (var t in _tramos)
            {
                var g = groups.Find(x => x.indice == t.indice && x.condicion == t.condicion);
                if (g == null)
                {
                    g = new Tramo { indice = t.indice, actividad = t.actividad, condicion = t.condicion };
                    groups.Add(g);
                }
                g.intento++;
                g.todo.Merge(t.todo);
                foreach (var kv in t.porSubtarea) g.Subtarea(kv.Key).Merge(kv.Value);
            }
            return groups;
        }

        // Resumen por actividad (pre / tutorial / post), con el tiempo que cuenta.
        private List<ActividadRealizada> BuildResults(string estado)
        {
            var results = new List<ActividadRealizada>();
            var groups = GroupByCondition();
            var indices = new List<int>();
            foreach (var g in groups) if (!indices.Contains(g.indice)) indices.Add(g.indice);
            indices.Sort();

            foreach (int idx in indices)
            {
                Tramo pre = groups.Find(g => g.indice == idx && g.condicion == "pre");
                Tramo tut = groups.Find(g => g.indice == idx && g.condicion == "tutorial");
                Tramo post = groups.Find(g => g.indice == idx && g.condicion == "post");
                var any = pre ?? tut ?? post;

                double activeSeconds = (pre?.todo.total.DurationSeconds ?? 0) + (post?.todo.total.DurationSeconds ?? 0);
                string modaPre = pre != null && pre.todo.util.Count > 0 ? pre.todo.util.Mode.ToString() : "-";
                string modaPost = post != null && post.todo.util.Count > 0 ? post.todo.util.Mode.ToString() : "-";

                results.Add(new ActividadRealizada
                {
                    Tipo = any.actividad,
                    TiempoTomado = TimeSpan.FromSeconds(activeSeconds).ToString(@"hh\:mm\:ss"),
                    CalificacionGeneral = $"REBA más frecuente pre {modaPre} / post {modaPost}" + (estado == "completa" ? "" : $" ({estado})"),
                    Reba = new RebaData
                    {
                        PreTutorial = ToResumen(pre),
                        Tutorial = ToResumen(tut),
                        PostTutorial = ToResumen(post),
                        CarpetaDatos = Path.Combine(CarpetaRaiz, _sessionId),
                    },
                });
            }
            return results;
        }

        private void SaveToHistory(List<ActividadRealizada> results)
        {
            if (string.IsNullOrEmpty(_participantName)) return;
            var user = UserDatabase.ObtenerUsuario(_participantName);
            if (user == null || user.EsAdministrador) return;

            var cap = new Capacitacion { Fecha = _sessionStart.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) };
            cap.Actividades.AddRange(results);
            if (user.Capacitaciones == null) user.Capacitaciones = new List<Capacitacion>();
            user.Capacitaciones.Add(cap);
            UserDatabase.ActualizarUsuario(user.Nombre, user);
        }

        private static RebaResumen ToResumen(Tramo g)
        {
            var r = new RebaResumen();
            if (g == null || g.todo.util.Count == 0) return r;
            var s = g.todo.util;
            r.HayDatos = true;
            r.Intentos = g.intento;
            r.DuracionSegundos = (float)s.DurationSeconds;
            r.Muestras = s.Count;
            r.Promedio = (float)s.Mean;
            r.Mediana = (float)s.Median;
            r.Moda = s.Mode;
            r.Percentil90 = (float)s.Percentile(90);
            r.Maximo = s.Max;
            r.PorcentajeInsignificante = (float)s.RiskPercent(RebaRisk.Negligible);
            r.PorcentajeBajo = (float)s.RiskPercent(RebaRisk.Low);
            r.PorcentajeMedio = (float)s.RiskPercent(RebaRisk.Medium);
            r.PorcentajeAlto = (float)s.RiskPercent(RebaRisk.High);
            r.PorcentajeMuyAlto = (float)s.RiskPercent(RebaRisk.VeryHigh);
            return r;
        }

        [Serializable]
        private class SegmentMeta
        {
            public string segmento;
            public int sensor;
            public string rastreado;
            public Vector3 montajeEuler;
        }

        [Serializable]
        private class SessionMeta
        {
            public string sesion, fecha, estado, versionApp, versionUnity;
            public int participanteId;
            public string edad, manoDominante;
            public float frecuenciaMuestreo;
            public bool grabarTutorial;
            public float umbralCaminandoMs, umbralCabezaGiradaGrados;
            public RebaThresholds umbrales;
            public float gravedadArribaY;
            public int suavizadoMuestras;
            public string[] servidores;
            public SegmentMeta[] segmentos;
            public string factoresFijos;
            public FactoresActividad[] factoresPorActividad;
        }

        private void WriteMeta(string estado)
        {
            var meta = new SessionMeta
            {
                sesion = _sessionId,
                fecha = _sessionStart.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                estado = estado,
                versionApp = Application.version,
                versionUnity = Application.unityVersion,
                participanteId = _participantId,
                edad = _age,
                manoDominante = _leftHanded ? "izquierda" : "derecha",
                frecuenciaMuestreo = frecuenciaMuestreo,
                grabarTutorial = grabarTutorial,
                umbralCaminandoMs = umbralCaminando,
                umbralCabezaGiradaGrados = umbralCabezaGirada,
                umbrales = estimator != null ? estimator.Thresholds : null,
                gravedadArribaY = estimator != null ? estimator.GravityUpY : 0f,
                suavizadoMuestras = receiver != null ? receiver.SmoothingSamples : 0,
                factoresFijos = "carga < 5 kg (0), agarre bueno (0), sin impactos; torsiones y desviaciones medidas por los sensores",
                factoresPorActividad = factoresPorActividad,
            };

            var servers = new List<string>();
            if (receiver != null)
            {
                for (int s = 0; s < receiver.ServerCount; s++)
                {
                    var ep = receiver.GetServerStatus(s).Endpoint;
                    if (ep != null) servers.Add($"PC {s + 1}: {ep} (sensores desde el {ep.sensorOffset})");
                }
            }
            meta.servidores = servers.ToArray();

            var segs = new List<SegmentMeta>();
            if (estimator != null)
            {
                foreach (var b in estimator.Bindings)
                {
                    segs.Add(new SegmentMeta
                    {
                        segmento = b.segment.ToString(),
                        sensor = b.sensorIndex,
                        rastreado = b.trackedTransform != null ? b.trackedTransform.name : "",
                        montajeEuler = b.mountingEuler,
                    });
                }
            }
            meta.segmentos = segs.ToArray();
            File.WriteAllText(Path.Combine(_folder, "sesion.json"), JsonUtility.ToJson(meta, true), new UTF8Encoding(false));
        }

        // ------------------------------------------------------------------
        // Prueba sin GameFlowManager y overlay
        // ------------------------------------------------------------------

        // Se puede grabar a mano en la escena de prueba o en Core mientras no haya una sesión en curso
        // (así se prueba con el visor, con el espejo de la sala de espera, sin crear participantes).
        private bool PuedeGrabarPrueba =>
            _manual || GameFlowManager.Instance == null || (!GameFlowManager.Instance.simulacionEnCurso && !_active);

        /// <summary>Escena de prueba: empieza (si hace falta) y graba el momento dado de actividadPrueba.</summary>
        public void GrabarPrueba(string condicion)
        {
            if (!PuedeGrabarPrueba)
            {
                Debug.LogWarning("[REBA] Hay una sesión en curso: la grabación la maneja el flujo de la sesión.", this);
                return;
            }
            if (!_active)
            {
                _manual = true;
                StartSession(null);
            }
            if (condicion == "tutorial") _seenTutorial.Add(0);
            SetCondition(condicion, actividadPrueba, 0);
        }

        [ContextMenu("Prueba: detener y guardar")]
        public void DetenerPrueba()
        {
            if (_active && _manual) EndSession("prueba terminada");
        }

        [ContextMenu("Abrir carpeta de datos REBA")]
        public void AbrirCarpeta()
        {
            string path = Path.Combine(Application.persistentDataPath, CarpetaRaiz);
            Directory.CreateDirectory(path);
            Application.OpenURL("file:///" + path.Replace('\\', '/'));
        }

        private void OnGUI()
        {
            if (!showOverlay) return;
            var flow = GameFlowManager.Instance;
            bool inTutorial = flow != null && !_manual && _active && flow.Fase == FaseSesion.Tutorial && flow.ActividadActual != null;

            bool pending = EsperandoCalibracion;
            float w = 420, h = 90 + (inTutorial ? 60 : 0) + (pending ? 60 : 0);
            float x = Screen.width - w - 10, y = Screen.height - h - 10;
            GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
            if (_tramo != null)
            {
                GUILayout.Label($"● Grabando REBA: {_tramo.actividad} · {_tramo.condicion} · {_lastSubtarea} · " +
                                $"{_tramo.todo.util.DurationSeconds:0} s que cuentan");
            }
            else if (pending)
            {
                GUILayout.Label($"REBA en espera: {MomentoPendiente}. Se graba al terminar la recalibración rápida.");
            }
            else
            {
                GUILayout.Label(_active ? "REBA: sesión abierta, sin actividad en curso" : "REBA: no está grabando");
            }

            // Plan B si la persona no puede quedarse quieta para calibrar: graba igual, queda el aviso en eventos.csv.
            if (pending && !(calibrator != null && calibrator.IsCalibrating) &&
                GUILayout.Button("Grabar SIN recalibrar (queda anotado)", GUILayout.Height(30)))
            {
                StartPending($"{MomentoPendiente} se empezó a grabar SIN recalibrar (decisión del Game Master)");
            }

            // El tutorial lanza solo la actividad (post) al terminar. Si no se puede terminar, se lanza desde aquí
            // (o con "Saltar a la actividad" en el panel del Game Master).
            if (inTutorial)
            {
                GUILayout.Label("Tutorial: al terminar se lanza la actividad POST.");
                if (GUILayout.Button($"Terminar tutorial → {flow.ActividadActual} POST", GUILayout.Height(30)))
                {
                    LogEvent("tutorial_terminado_a_mano", $"{flow.ActividadActual}");
                    flow.LanzarActividadDirecta(flow.ActividadActual.Value);
                }
            }

            if (PuedeGrabarPrueba)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Grabar PRE")) GrabarPrueba("pre");
                if (GUILayout.Button("TUTORIAL")) GrabarPrueba("tutorial");
                if (GUILayout.Button("POST")) GrabarPrueba("post");
                if (_active && GUILayout.Button("Detener y guardar")) DetenerPrueba();
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Abrir carpeta de datos")) AbrirCarpeta();
            GUILayout.EndArea();
        }

        private static string F(double v) => CsvRow.Number(v);
        private static string SiNo(bool v) => v ? "sí" : "no";
    }
}
