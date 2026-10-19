// RebaSessionRecorder.cs
//
// Guarda REBA de cada sesión para el análisis de la tesis. Por sesión crea la carpeta
//   <persistentDataPath>/REBA/<fecha>_P<id del participante>/
// con:
//   hoja_reba.csv los ítems de la hoja REBA en su orden, una fila por muestra: el valor medido
//                 (grados, sí/no) y los puntos que suma cada uno, hasta el REBA final y la actuación.
//   muestras.csv  una fila cada 1/muestrasPorSegundo s mientras hay actividad o tutorial:
//                 todos los ángulos, las entradas y puntajes de REBA de cada lado, qué
//                 segmentos tenían datos y la orientación de cada segmento (cuaternión)
//                 para poder recalcular todo después con otros umbrales.
//   resumen.csv   una fila por actividad x condición x intento (y el total de los intentos):
//                 promedio, mediana, percentil 90, máximo, % del tiempo en cada riesgo...
//   eventos.csv   calibraciones, cambios de fase, sensores perdidos, fin de la sesión.
//   sesion.json   cómo se midió: umbrales, suavizado, sensores, montajes, factores.
// y agrega el resumen a REBA/resumen_todas_las_sesiones.csv (una tabla para todos los participantes).
//
// La condición se etiqueta sola con GameFlowManager.Fase, por actividad:
//   "pre"      la actividad antes de ver el tutorial
//   "tutorial" durante el tutorial
//   "post"     la actividad después del tutorial
// Cada momento empieza a grabarse cuando el Game Master termina una recalibración
// (JoyconSessionCalibrator) después de que empezó; antes queda "en espera".
//
// Al terminar la sesión también guarda el resumen pre/tutorial/post en el historial del
// participante (Capacitacion -> ActividadRealizada -> RebaData en usuarios.json).
//
// CSV para Excel en español: separador ';' y decimales con coma.
// Solo se identifica al participante por su Id (no se guarda nombre ni correo en los CSV).
//
// Sin GameFlowManager (escena de prueba), o en Core mientras no hay una sesión en curso (prueba con
// el visor en la sala de espera), se graba a mano con los botones en pantalla.

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
        public enum Condicion { Pre, Tutorial, Post }

        // Lo que REBA pregunta y los sensores no miden (o no miden con confianza): lo deciden las
        // investigadoras por actividad y por condición (pre / tutorial / post).
        [Serializable]
        public class FactoresActividad
        {
            public ActivityType actividad;
            public Condicion condicion;
            [Header("Carga y agarre")]
            [Tooltip("Carga que se manipula (kg). REBA: <5 kg = 0, 5–10 = +1, >10 = +2. En este estudio siempre < 5 kg.")]
            public float cargaKg;
            [Tooltip("Golpes o fuerza brusca (+1 a la carga).")]
            public bool impactoOFuerzaBrusca;
            [Tooltip("Agarre: 0 bueno, 1 regular, 2 malo, 3 inaceptable. En este estudio siempre bueno (0).")]
            [Range(0, 3)] public int agarre;
            [Header("Brazos (no lo mide el Joy-Con)")]
            [Tooltip("Hombro elevado (encogido): +1 al brazo.")]
            public bool hombroElevado;
            [Tooltip("Brazo apoyado o postura a favor de la gravedad: −1 al brazo.")]
            public bool brazoApoyadoOAFavorDeGravedad;
            [Header("Actividad (+1 cada una)")]
            [Tooltip("Una o más partes del cuerpo estáticas (más de 1 minuto).")]
            public bool partesEstaticas;
            [Tooltip("Movimientos repetitivos (más de 4 por minuto, en rango pequeño).")]
            public bool movimientosRepetitivos;
            [Tooltip("Cambios posturales grandes y rápidos, o postura inestable.")]
            public bool cambiosPosturalesOInestables;

            [Header("Preguntas de sí/no: Sensores (con su umbral) o decisión de las investigadoras")]
            [Tooltip("Tronco con rotación o inclinación lateral (+1). Usar Si/No si el Joy-Con de la pelvis falla o no se confía en la torsión.")]
            public FuenteSiNo troncoRotacionOInclinacion = FuenteSiNo.Sensores;
            [Tooltip("Cuello con torsión o inclinación lateral (+1).")]
            public FuenteSiNo cuelloTorsionOInclinacion = FuenteSiNo.Sensores;
            [Tooltip("Brazo rotado (cuenta para el +1 de abducido o rotado; la abducción siempre la miden los sensores).")]
            public FuenteSiNo brazoRotado = FuenteSiNo.Sensores;
            [Tooltip("Muñeca con torsión o desviación radial/cubital (+1).")]
            public FuenteSiNo munecaTorsionODesviacion = FuenteSiNo.Sensores;
        }

        private static FactoresActividad[] FactoresPorDefecto()
        {
            var list = new List<FactoresActividad>();
            foreach (ActivityType a in Enum.GetValues(typeof(ActivityType)))
                foreach (Condicion c in Enum.GetValues(typeof(Condicion)))
                    list.Add(new FactoresActividad { actividad = a, condicion = c });
            return list.ToArray();
        }

        public const string CarpetaRaiz = "REBA";
        private const char Sep = ';';

        [SerializeField] private RebaPostureEstimator estimator;
        [SerializeField] private JoyconReceiver receiver;
        [SerializeField] private JoyconSessionCalibrator calibrator;

        [Header("Muestreo")]
        [SerializeField, Range(1f, 60f)] private float muestrasPorSegundo = 10f;
        [SerializeField] private bool grabarTutorial = true;
        [Tooltip("Cada momento (pre, tutorial, post) empieza a grabarse solo cuando el Game Master termina una " +
                 "calibración (rápida o completa) DESPUÉS de que empezó. Así cada tramo arranca con el 'cero' recién tomado.")]
        [SerializeField] private bool esperarCalibracionParaGrabar = true;

        [Header("Factores de REBA que no miden los sensores (por actividad y condición)")]
        [Tooltip("Una entrada por actividad x condición (pre / tutorial / post). Si falta una, se usa la de la misma " +
                 "actividad con otra condición. Menú ⋮ -> 'Restablecer factores' crea las 9.")]
        [SerializeField] private FactoresActividad[] factores = FactoresPorDefecto();

        [Header("Historial del participante (usuarios.json)")]
        [Tooltip("Al terminar la sesión crea una Capacitacion con una ActividadRealizada por actividad y su RebaData.")]
        [SerializeField] private bool guardarEnHistorial = true;

        [Header("Prueba sin GameFlowManager (escena PruebaJoycons)")]
        [SerializeField] private ActivityType actividadPrueba = ActivityType.Siembra;
        [SerializeField] private int participantePruebaId = 0;
        [SerializeField] private bool showOverlay = true;

        // Un tramo = una actividad en una condición en un intento.
        private sealed class Tramo
        {
            public int indice;
            public ActivityType actividad;
            public string condicion;
            public int intento;
            public readonly RebaStats stats = new RebaStats();
        }

        private bool _active, _manual, _finalized;
        private string _folder, _sessionId;
        private DateTime _sessionStart;
        private float _sessionT0;
        private int _participantId;
        private string _participantName, _age;
        private bool _leftHanded;
        private StreamWriter _samples, _events, _sheet;
        private bool _samplesHeaderWritten, _sheetHeaderWritten;
        private float _nextFlush;

        private FaseSesion _lastFase = FaseSesion.SinSesion;
        private int _lastIndex = int.MinValue;
        private ActivityType? _lastActivity;
        private Tramo _tramo;
        private float _tramoT0, _nextSampleT;
        private readonly HashSet<int> _seenTutorial = new HashSet<int>();
        private readonly Dictionary<string, int> _attempts = new Dictionary<string, int>();
        private readonly List<Tramo> _tramos = new List<Tramo>();
        private bool _reachedResults;
        private bool[] _serverWasReceiving = new bool[0];
        private readonly CsvRow _row = new CsvRow();

        // Momento que ya empezó en el juego pero espera la calibración para grabarse.
        private bool _pending;
        private string _pendingCondicion;
        private ActivityType _pendingActividad;
        private int _pendingIndice;
        private int _pendingCalibraciones;

        public bool Grabando => _tramo != null;
        public string CarpetaSesion => _folder;

        /// <summary>Hay un momento (pre/tutorial/post) esperando una calibración para empezar a grabarse.</summary>
        public bool EsperandoCalibracion => _active && _pending;
        public string MomentoPendiente => _pending ? $"{_pendingActividad} · {_pendingCondicion}" : "";

        private void Awake()
        {
            if (estimator == null) estimator = FindAnyObjectByType<RebaPostureEstimator>();
            if (receiver == null) receiver = FindAnyObjectByType<JoyconReceiver>();
            if (calibrator == null) calibrator = FindAnyObjectByType<JoyconSessionCalibrator>();
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
                _sheet?.Flush();
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
            _sheet = new StreamWriter(Path.Combine(_folder, "hoja_reba.csv"), false, utf8Bom);
            _events = new StreamWriter(Path.Combine(_folder, "eventos.csv"), false, utf8Bom);
            _events.WriteLine(string.Join(Sep.ToString(), "fecha_hora", "t_sesion_s", "tipo", "detalle"));
            _samplesHeaderWritten = false;
            _sheetHeaderWritten = false;

            _tramos.Clear();
            _attempts.Clear();
            _seenTutorial.Clear();
            _tramo = null;
            _pending = false;
            _lastFase = FaseSesion.SinSesion;
            _lastIndex = int.MinValue;
            _lastActivity = null;
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
            if (condicion != null && esperarCalibracionParaGrabar && calibrator != null)
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
                                      $"{_tramo.stats.Count} muestras, {F(_tramo.stats.DurationSeconds)} s");
                _tramo = null;
            }

            if (condicion == null || actividad == null) return;
            _lastActivity = actividad;
            ApplyFactors(actividad.Value, condicion); // cada tramo con los factores de su actividad y condición

            string key = $"{indice}|{condicion}";
            _attempts.TryGetValue(key, out int n);
            _attempts[key] = ++n;
            _tramo = new Tramo { indice = indice, actividad = actividad.Value, condicion = condicion, intento = n };
            _tramos.Add(_tramo);
            _tramoT0 = Time.unscaledTime;
            _nextSampleT = Time.unscaledTime;
            LogEvent("inicio_tramo", $"{_tramo.actividad} {condicion} intento {n}");

            // La calibración completa la lanza el Game Master: si se olvidó, queda anotado para revisar después.
            if (!_manual && calibrator != null && !calibrator.CalibradoEnEstaSesion)
                LogEvent("aviso", "el tramo empezó SIN la calibración completa de este participante");
        }

        private static Condicion ToCondicion(string condicion) =>
            condicion == "tutorial" ? Condicion.Tutorial : (condicion == "post" ? Condicion.Post : Condicion.Pre);

        // La entrada de esa actividad y condición; si no existe, la primera de la misma actividad.
        private FactoresActividad FindFactors(ActivityType actividad, Condicion condicion)
        {
            if (factores == null) return null;
            FactoresActividad sameActivity = null;
            foreach (var f in factores)
            {
                if (f == null || f.actividad != actividad) continue;
                if (f.condicion == condicion) return f;
                if (sameActivity == null) sameActivity = f;
            }
            return sameActivity;
        }

        private void ApplyFactors(ActivityType actividad, string condicionTexto)
        {
            if (estimator == null) return;
            Condicion condicion = ToCondicion(condicionTexto);
            var f = FindFactors(actividad, condicion);
            if (f == null)
            {
                LogEvent("aviso", $"no hay factores para {actividad} {condicionTexto}: se mantienen los anteriores");
                return;
            }
            estimator.LoadKg = f.cargaKg;
            estimator.ShockOrRapidForce = f.impactoOFuerzaBrusca;
            estimator.Coupling = f.agarre;
            estimator.ShoulderRaised = f.hombroElevado;
            estimator.ArmSupported = f.brazoApoyadoOAFavorDeGravedad;
            estimator.StaticPosture = f.partesEstaticas;
            estimator.RepetitiveMovements = f.movimientosRepetitivos;
            estimator.RapidChangesOrUnstable = f.cambiosPosturalesOInestables;
            estimator.TrunkTwistOrSideBendSource = f.troncoRotacionOInclinacion;
            estimator.NeckTwistOrSideBendSource = f.cuelloTorsionOInclinacion;
            estimator.ArmRotatedSource = f.brazoRotado;
            estimator.WristDeviatedOrTwistedSource = f.munecaTorsionODesviacion;

            string usada = f.condicion == condicion ? "" : $" (no había entrada para {condicionTexto}: se usó la de {f.condicion})";
            LogEvent("factores", $"{actividad} {condicionTexto}{usada}: carga {F(f.cargaKg)} kg, impacto {SiNo(f.impactoOFuerzaBrusca)}, " +
                                 $"agarre {f.agarre}, hombro elevado {SiNo(f.hombroElevado)}, " +
                                 $"brazo apoyado {SiNo(f.brazoApoyadoOAFavorDeGravedad)}, " +
                                 $"partes estáticas {SiNo(f.partesEstaticas)}, repetitivos {SiNo(f.movimientosRepetitivos)}, " +
                                 $"cambios posturales {SiNo(f.cambiosPosturalesOInestables)}, " +
                                 $"tronco rot/incl {Fuente(f.troncoRotacionOInclinacion)}, cuello tors/incl {Fuente(f.cuelloTorsionOInclinacion)}, " +
                                 $"brazo rotado {Fuente(f.brazoRotado)}, muñeca tors/desv {Fuente(f.munecaTorsionODesviacion)}");
        }

        private static string Fuente(FuenteSiNo s) =>
            s == FuenteSiNo.Sensores ? "sensores" : (s == FuenteSiNo.Si ? "investigadoras: sí" : "investigadoras: no");

        /// <summary>Deja una entrada por actividad x condición (9), conservando los valores que ya existan.</summary>
        [ContextMenu("Restablecer factores (3 actividades x pre/tutorial/post)")]
        public void RestablecerFactores()
        {
            var nuevos = FactoresPorDefecto();
            for (int i = 0; i < nuevos.Length; i++)
            {
                var existente = FindFactors(nuevos[i].actividad, nuevos[i].condicion);
                if (existente == null) continue;
                // Copia los valores (de la misma condición o, si no había, de la misma actividad).
                var copia = JsonUtility.FromJson<FactoresActividad>(JsonUtility.ToJson(existente));
                copia.condicion = nuevos[i].condicion;
                nuevos[i] = copia;
            }
            factores = nuevos;
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
            _sheet?.Dispose();
            _events?.Dispose();
            _samples = _events = _sheet = null;
            _active = false;
            _manual = false;
            Debug.Log($"[REBA] Sesión guardada ({motivo}) en {_folder}", this);
        }

        // ------------------------------------------------------------------
        // Muestras
        // ------------------------------------------------------------------

        private void Sample()
        {
            if (_tramo == null || estimator == null || !estimator.IsCalibrated) return;
            float now = Time.unscaledTime;
            if (now < _nextSampleT) return;
            float period = 1f / Mathf.Max(1f, muestrasPorSegundo);
            _nextSampleT += period;
            if (_nextSampleT < now) _nextSampleT = now + period; // tras una pausa no se "ponen al día" filas de golpe

            // Las recalibraciones (persona en postura N) se graban marcadas pero no entran al resumen.
            bool calibrando = calibrator != null && calibrator.IsCalibrating;
            WriteSampleRow(now, calibrando);
            WriteSheetRow(now, calibrando);
            if (!calibrando) _tramo.stats.Add(estimator.Left, estimator.Right, period);
        }

        private void WriteSampleRow(float now, bool calibrando)
        {
            var r = _row;
            r.Begin();
            r.Add("fecha_hora", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            r.Add("t_sesion_s", now - _sessionT0);
            r.Add("participante_id", _participantId);
            r.Add("actividad", _tramo.actividad.ToString());
            r.Add("indice_actividad", _tramo.indice);
            r.Add("condicion", _tramo.condicion);
            r.Add("intento", _tramo.intento);
            r.Add("t_tramo_s", now - _tramoT0);
            r.Add("calibrando", calibrando);
            for (int s = 0; s < _serverWasReceiving.Length; s++)
            {
                r.Add($"pc{s + 1}_recibiendo", receiver != null && s < receiver.ServerCount && receiver.GetServerStatus(s).IsReceiving);
            }

            // Ángulos (grados).
            var a = estimator.CurrentAngles;
            r.Add("tronco_flexion", a.trunkFlexion, a.hasTrunk);
            r.Add("tronco_lateral", a.trunkLateral, a.hasTrunk);
            r.Add("tronco_torsion", a.trunkTwist, a.hasTrunkTwist);
            r.Add("cuello_flexion", a.neckFlexion, a.hasNeck);
            r.Add("cuello_lateral", a.neckLateral, a.hasNeck);
            r.Add("cuello_torsion", a.neckTwist, a.hasNeck);
            r.Add("rodilla_izq_flexion", a.kneeFlexionLeft, a.hasKnee);
            r.Add("rodilla_der_flexion", a.kneeFlexionRight, a.hasKnee);
            AddArm("izq", a.left);
            AddArm("der", a.right);

            // Entradas de REBA (lo que REBA "ve", incluidos los factores manuales).
            var li = estimator.LeftInputs;
            var ri = estimator.RightInputs;
            r.Add("carga_kg", li.loadKg);
            r.Add("impacto_o_fuerza_brusca", li.shockOrRapidForce);
            r.Add("agarre", li.coupling);
            r.Add("actividad_reba", li.activity);
            r.Add("piernas_unilateral_o_inestable", li.legsUnilateralOrUnstable);
            r.Add("sentado", li.sitting);
            r.Add("tronco_torcido_o_inclinado", li.trunkTwistedOrSideBent);
            r.Add("cuello_torcido_o_inclinado", li.neckTwistedOrSideBent);
            r.Add("rodilla_reba", li.kneeFlexion);
            AddArmInputs("izq", li);
            AddArmInputs("der", ri);

            // Puntajes REBA de cada lado y el final.
            AddResult("izq", estimator.Left);
            AddResult("der", estimator.Right);
            var worst = estimator.Worst;
            r.Add("reba_final", worst.final);
            r.Add("riesgo_final", Reba.RiskName(worst.risk));
            r.Add("lado_peor", estimator.Left.final >= estimator.Right.final ? "izq" : "der");

            // Orientación de cada segmento en el marco del cuerpo (para recalcular después).
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

            if (!_samplesHeaderWritten)
            {
                _samples.WriteLine(r.Header(Sep));
                _samplesHeaderWritten = true;
            }
            _samples.WriteLine(r.Values(Sep));
        }

        private void AddArm(string side, ArmAngles arm)
        {
            var r = _row;
            r.Add($"brazo_{side}_flexion", arm.upperArmFlexion, arm.measured);
            r.Add($"brazo_{side}_abduccion", arm.upperArmAbduction, arm.measured);
            r.Add($"brazo_{side}_elevacion", arm.upperArmElevation, arm.measured);
            r.Add($"brazo_{side}_rotacion", arm.upperArmRotation, arm.measured);
            r.Add($"codo_{side}_flexion", arm.elbowFlexion, arm.hasElbow);
            r.Add($"muneca_{side}_flexion", arm.wristFlexion, arm.hasWrist);
            r.Add($"muneca_{side}_desviacion", arm.wristDeviation, arm.hasWrist);
        }

        private void AddArmInputs(string side, RebaInputs i)
        {
            var r = _row;
            r.Add($"brazo_{side}_reba", i.upperArmFlexion);
            r.Add($"brazo_{side}_abducido_o_rotado", i.upperArmAbductedOrRotated);
            r.Add($"hombro_{side}_elevado", i.shoulderRaised);
            r.Add($"brazo_{side}_apoyado", i.armSupportedOrLeaning);
            r.Add($"codo_{side}_reba", i.elbowFlexion);
            r.Add($"muneca_{side}_reba", i.wristFlexion);
            r.Add($"muneca_{side}_desviada_o_torcida", i.wristDeviatedOrTwisted);
        }

        private void AddResult(string side, RebaResult x)
        {
            var r = _row;
            r.Add($"{side}_p_tronco", x.trunk);
            r.Add($"{side}_p_cuello", x.neck);
            r.Add($"{side}_p_piernas", x.legs);
            r.Add($"{side}_p_carga", x.load);
            r.Add($"{side}_p_brazo", x.upperArm);
            r.Add($"{side}_p_antebrazo", x.lowerArm);
            r.Add($"{side}_p_muneca", x.wrist);
            r.Add($"{side}_p_agarre", x.coupling);
            r.Add($"{side}_tabla_a", x.tableA);
            r.Add($"{side}_puntaje_a", x.scoreA);
            r.Add($"{side}_tabla_b", x.tableB);
            r.Add($"{side}_puntaje_b", x.scoreB);
            r.Add($"{side}_tabla_c", x.tableC);
            r.Add($"{side}_p_actividad", x.activity);
            r.Add($"{side}_reba", x.final);
            r.Add($"{side}_riesgo", Reba.RiskName(x.risk));
        }

        // ------------------------------------------------------------------
        // hoja_reba.csv: los ítems de la hoja REBA, en su orden, con valor -> puntos
        // ------------------------------------------------------------------

        private readonly CsvRow _sheetRow = new CsvRow();

        private void WriteSheetRow(float now, bool calibrando)
        {
            var r = _sheetRow;
            var a = estimator.CurrentAngles;
            var li = estimator.LeftInputs;
            RebaResult L = estimator.Left, R = estimator.Right, worst = estimator.Worst;

            r.Begin();
            r.Add("fecha_hora", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            r.Add("t_sesion_s", now - _sessionT0);
            r.Add("participante_id", _participantId);
            r.Add("actividad", _tramo.actividad.ToString());
            r.Add("condicion", _tramo.condicion);
            r.Add("intento", _tramo.intento);
            r.Add("t_tramo_s", now - _tramoT0);
            r.Add("calibrando", SiNo(calibrando));

            // ---- Parte 1 (grupo A). Tronco, cuello y piernas son iguales para los dos lados. ----
            r.Add("tronco_flexion_extension_grados", a.trunkFlexion, a.hasTrunk);
            r.Add("tronco_flexion_extension_puntos", L.trunkBase);
            r.Add("tronco_inclinacion_lateral_grados", a.trunkLateral, a.hasTrunk);
            r.Add("tronco_rotacion_medida", SiNo(a.hasTrunkTwist)); // no: sin datos del Joy-Con de la pelvis
            r.Add("tronco_rotacion_grados", a.trunkTwist, a.hasTrunkTwist);
            r.Add("tronco_rotacion_o_inclinacion_lateral", SiNo(li.trunkTwistedOrSideBent));
            r.Add("tronco_rotacion_o_inclinacion_lateral_fuente", Fuente(estimator.TrunkTwistOrSideBendSource));
            r.Add("tronco_rotacion_o_inclinacion_lateral_puntos", L.trunkAdjust);
            r.Add("tronco_total", L.trunk);

            r.Add("cuello_medido", SiNo(a.hasNeck));
            r.Add("cuello_flexion_extension_grados", a.neckFlexion, a.hasNeck);
            r.Add("cuello_flexion_extension_puntos", L.neckBase);
            r.Add("cuello_inclinacion_lateral_grados", a.neckLateral, a.hasNeck);
            r.Add("cuello_torsion_grados", a.neckTwist, a.hasNeck);
            r.Add("cuello_torsion_o_inclinacion_lateral", SiNo(li.neckTwistedOrSideBent));
            r.Add("cuello_torsion_o_inclinacion_lateral_fuente", Fuente(estimator.NeckTwistOrSideBendSource));
            r.Add("cuello_torsion_o_inclinacion_lateral_puntos", L.neckAdjust);
            r.Add("cuello_total", L.neck);

            r.Add("piernas_soporte", li.legsUnilateralOrUnstable ? "unilateral" : "bilateral");
            r.Add("piernas_soporte_puntos", L.legsBase);
            r.Add("rodilla_izq_flexion_grados", a.kneeFlexionLeft, a.hasKnee);
            r.Add("rodilla_der_flexion_grados", a.kneeFlexionRight, a.hasKnee);
            r.Add("rodillas_flexion_grados_reba", li.kneeFlexion, a.hasKnee); // la más flexionada
            r.Add("rodillas_flexion_puntos", L.kneeAdjust);
            r.Add("piernas_total", L.legs);

            r.Add("tabla_a", L.tableA);
            r.Add("carga_kg", li.loadKg);
            r.Add("carga_puntos", L.loadBase);
            r.Add("impacto_o_fuerza_brusca", SiNo(li.shockOrRapidForce));
            r.Add("impacto_puntos", L.shockAdjust);
            r.Add("puntaje_a", L.scoreA);

            // ---- Parte 2 (grupo B), por lado ----
            AddSheetArm("izq", a.left, estimator.LeftInputs, L);
            AddSheetArm("der", a.right, estimator.RightInputs, R);

            // ---- Parte final ----
            r.Add("partes_del_cuerpo_estaticas", SiNo(li.staticPosture));
            r.Add("partes_del_cuerpo_estaticas_puntos", L.activityStatic);
            r.Add("movimientos_repetitivos", SiNo(li.repetitiveMovements));
            r.Add("movimientos_repetitivos_puntos", L.activityRepetitive);
            r.Add("cambios_posturales_o_postura_inestable", SiNo(li.rapidChangesOrUnstable));
            r.Add("cambios_posturales_o_postura_inestable_puntos", L.activityRapid);
            r.Add("actividad_puntos", L.activity);

            r.Add("reba_izq", L.final);
            r.Add("reba_der", R.final);
            r.Add("reba_final", worst.final);
            r.Add("lado_peor", L.final >= R.final ? "izq" : "der");
            r.Add("nivel_de_riesgo", Reba.RiskName(worst.risk));
            r.Add("actuacion", Reba.RiskAction(worst.risk));

            if (!_sheetHeaderWritten)
            {
                _sheet.WriteLine(r.Header(Sep));
                _sheetHeaderWritten = true;
            }
            _sheet.WriteLine(r.Values(Sep));
        }

        private void AddSheetArm(string side, ArmAngles arm, RebaInputs i, RebaResult x)
        {
            var r = _sheetRow;
            r.Add($"brazo_{side}_medido", SiNo(arm.measured));
            // El ángulo que puntúa REBA: la elevación del brazo (o la extensión si va hacia atrás).
            r.Add($"brazo_{side}_flexion_grados", i.upperArmFlexion, arm.measured);
            r.Add($"brazo_{side}_flexion_puntos", x.upperArmBase);
            r.Add($"hombro_{side}_abduccion_grados", arm.upperArmAbduction, arm.measured);
            r.Add($"hombro_{side}_abducido", SiNo(i.upperArmAbducted));
            r.Add($"brazo_{side}_rotacion_grados", arm.upperArmRotation, arm.measured);
            r.Add($"brazo_{side}_rotado", SiNo(i.upperArmRotated));
            r.Add($"brazo_{side}_rotado_fuente", Fuente(estimator.ArmRotatedSource));
            r.Add($"brazo_{side}_abducido_o_rotado_puntos", x.upperArmAbductedAdjust);
            r.Add($"hombro_{side}_elevado", SiNo(i.shoulderRaised));
            r.Add($"hombro_{side}_elevado_fuente", "investigadoras");
            r.Add($"hombro_{side}_elevado_puntos", x.shoulderRaisedAdjust);
            r.Add($"brazo_{side}_apoyo_o_a_favor_de_gravedad", SiNo(i.armSupportedOrLeaning));
            r.Add($"brazo_{side}_apoyo_o_a_favor_de_gravedad_fuente", "investigadoras");
            r.Add($"brazo_{side}_apoyo_o_a_favor_de_gravedad_puntos", x.armSupportedAdjust);
            r.Add($"brazo_{side}_total", x.upperArm);

            r.Add($"antebrazo_{side}_medido", SiNo(arm.hasElbow));
            r.Add($"antebrazo_{side}_flexion_grados", arm.elbowFlexion, arm.hasElbow);
            r.Add($"antebrazo_{side}_puntos", x.lowerArm);

            r.Add($"muneca_{side}_medida", SiNo(arm.hasWrist));
            r.Add($"muneca_{side}_flexion_extension_grados", arm.wristFlexion, arm.hasWrist);
            r.Add($"muneca_{side}_flexion_extension_puntos", x.wristBase);
            r.Add($"muneca_{side}_desviacion_grados", arm.wristDeviation, arm.hasWrist);
            r.Add($"muneca_{side}_torsion_o_desviacion", SiNo(i.wristDeviatedOrTwisted));
            r.Add($"muneca_{side}_torsion_o_desviacion_fuente", Fuente(estimator.WristDeviatedOrTwistedSource));
            r.Add($"muneca_{side}_torsion_o_desviacion_puntos", x.wristAdjust);
            r.Add($"muneca_{side}_total", x.wrist);

            r.Add($"tabla_b_{side}", x.tableB);
            r.Add($"agarre_{side}", CouplingName(i.coupling));
            r.Add($"agarre_{side}_puntos", x.coupling);
            r.Add($"puntaje_b_{side}", x.scoreB);
            r.Add($"tabla_c_{side}", x.tableC);
        }

        private static string CouplingName(int coupling)
        {
            switch (coupling)
            {
                case 0: return "bueno";
                case 1: return "regular";
                case 2: return "malo";
                default: return "inaceptable";
            }
        }

        private static string SegmentColumn(BodySegment s)
        {
            switch (s)
            {
                case BodySegment.Pelvis: return "seg_pelvis";
                case BodySegment.Trunk: return "seg_tronco";
                case BodySegment.Head: return "seg_cabeza";
                case BodySegment.UpperArmLeft: return "seg_brazo_izq";
                case BodySegment.ForearmLeft: return "seg_antebrazo_izq";
                case BodySegment.HandLeft: return "seg_mano_izq";
                case BodySegment.UpperArmRight: return "seg_brazo_der";
                case BodySegment.ForearmRight: return "seg_antebrazo_der";
                case BodySegment.HandRight: return "seg_mano_der";
                case BodySegment.ThighLeft: return "seg_muslo_izq";
                case BodySegment.ShankLeft: return "seg_pierna_izq";
                case BodySegment.ThighRight: return "seg_muslo_der";
                case BodySegment.ShankRight: return "seg_pierna_der";
                default: return "seg_" + s.ToString().ToLowerInvariant();
            }
        }

        // ------------------------------------------------------------------
        // Eventos
        // ------------------------------------------------------------------

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
        // Al terminar: resumen, sesion.json e historial
        // ------------------------------------------------------------------

        private void FinalizeData(string estado)
        {
            _finalized = true;
            SetCondition(null, null, -1);
            try
            {
                WriteSummaries(estado);
                WriteMeta(estado);
                if (guardarEnHistorial && !_manual) SaveToHistory(estado);
                LogEvent("resumen_guardado", estado);
            }
            catch (Exception e)
            {
                Debug.LogError($"[REBA] Error guardando el resumen: {e}", this);
                LogEvent("error", e.Message);
            }
            _events?.Flush();
            _samples?.Flush();
            _sheet?.Flush();
        }

        private static readonly string[] SummaryHeader =
        {
            "sesion", "fecha", "participante_id", "edad", "mano_dominante", "actividad", "indice_actividad",
            "condicion", "intento", "duracion_s", "muestras", "reba_promedio", "reba_mediana", "reba_p90",
            "reba_max", "t_max_s", "pct_insignificante", "pct_bajo", "pct_medio", "pct_alto", "pct_muy_alto",
            "prom_tronco", "prom_cuello", "prom_piernas", "prom_brazo_izq", "prom_brazo_der",
            "prom_antebrazo_izq", "prom_antebrazo_der", "prom_muneca_izq", "prom_muneca_der", "estado_sesion",
        };

        private void WriteSummaries(string estado)
        {
            var lines = new List<string>();
            // Primero cada intento; después el total de cada actividad x condición ("todos").
            foreach (var t in _tramos) lines.Add(SummaryLine(t.actividad, t.indice, t.condicion, t.intento.ToString(), t.stats, estado));
            foreach (var g in GroupByCondition())
            {
                lines.Add(SummaryLine(g.actividad, g.indice, g.condicion, "todos", g.stats, estado));
            }

            string header = string.Join(Sep.ToString(), SummaryHeader);
            var utf8Bom = new UTF8Encoding(true);
            File.WriteAllText(Path.Combine(_folder, "resumen.csv"), header + "\n" + string.Join("\n", lines) + "\n", utf8Bom);

            // Tabla común de todas las sesiones (todos los participantes), solo con los totales.
            string all = Path.Combine(Application.persistentDataPath, CarpetaRaiz, "resumen_todas_las_sesiones.csv");
            bool isNew = !File.Exists(all);
            using (var w = new StreamWriter(all, true, utf8Bom))
            {
                if (isNew) w.WriteLine(header);
                foreach (var line in lines)
                {
                    if (line.Contains($"{Sep}todos{Sep}")) w.WriteLine(line);
                }
            }
        }

        private string SummaryLine(ActivityType actividad, int indice, string condicion, string intento, RebaStats s, string estado)
        {
            var v = new List<string>
            {
                _sessionId, _sessionStart.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                _participantId.ToString(), CsvRow.Clean(_age), _leftHanded ? "izquierda" : "derecha",
                actividad.ToString(), indice.ToString(), condicion, intento,
                F(s.DurationSeconds), s.Count.ToString(), F(s.Mean), F(s.Median), F(s.Percentile(90)),
                s.Count > 0 ? s.Max.ToString() : "", F(s.TimeOfMaxSeconds),
                F(s.RiskPercent(RebaRisk.Negligible)), F(s.RiskPercent(RebaRisk.Low)), F(s.RiskPercent(RebaRisk.Medium)),
                F(s.RiskPercent(RebaRisk.High)), F(s.RiskPercent(RebaRisk.VeryHigh)),
                F(s.MeanTrunk), F(s.MeanNeck), F(s.MeanLegs), F(s.MeanUpperArmLeft), F(s.MeanUpperArmRight),
                F(s.MeanLowerArmLeft), F(s.MeanLowerArmRight), F(s.MeanWristLeft), F(s.MeanWristRight), estado,
            };
            return string.Join(Sep.ToString(), v);
        }

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
                g.stats.Merge(t.stats);
            }
            return groups;
        }

        private void SaveToHistory(string estado)
        {
            if (string.IsNullOrEmpty(_participantName)) return;
            var user = UserDatabase.ObtenerUsuario(_participantName);
            if (user == null || user.EsAdministrador) return;

            var cap = new Capacitacion { Fecha = _sessionStart.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) };
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

                double activeSeconds = (pre?.stats.DurationSeconds ?? 0) + (post?.stats.DurationSeconds ?? 0);
                string p90Pre = pre != null && pre.stats.Count > 0 ? F(pre.stats.Percentile(90)) : "-";
                string p90Post = post != null && post.stats.Count > 0 ? F(post.stats.Percentile(90)) : "-";

                cap.Actividades.Add(new ActividadRealizada
                {
                    Tipo = any.actividad,
                    TiempoTomado = TimeSpan.FromSeconds(activeSeconds).ToString(@"hh\:mm\:ss"),
                    CalificacionGeneral = $"REBA P90 pre {p90Pre} / post {p90Post}" + (estado == "completa" ? "" : $" ({estado})"),
                    Reba = new RebaData
                    {
                        PreTutorial = ToResumen(pre),
                        Tutorial = ToResumen(tut),
                        PostTutorial = ToResumen(post),
                        CarpetaDatos = Path.Combine(CarpetaRaiz, _sessionId),
                    },
                });
            }

            if (user.Capacitaciones == null) user.Capacitaciones = new List<Capacitacion>();
            user.Capacitaciones.Add(cap);
            UserDatabase.ActualizarUsuario(user.Nombre, user);
        }

        private static RebaResumen ToResumen(Tramo g)
        {
            var r = new RebaResumen();
            if (g == null || g.stats.Count == 0) return r;
            var s = g.stats;
            r.HayDatos = true;
            r.Intentos = g.intento;
            r.DuracionSegundos = (float)s.DurationSeconds;
            r.Muestras = s.Count;
            r.Promedio = (float)s.Mean;
            r.Mediana = (float)s.Median;
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
            public float muestrasPorSegundo;
            public bool grabarTutorial;
            public RebaThresholds umbrales;
            public float gravedadArribaY;
            public int suavizadoMuestras;
            public string[] servidores;
            public SegmentMeta[] segmentos;
            public FactoresActividad[] factores;
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
                muestrasPorSegundo = muestrasPorSegundo,
                grabarTutorial = grabarTutorial,
                umbrales = estimator != null ? estimator.Thresholds : null,
                gravedadArribaY = estimator != null ? estimator.GravityUpY : 0f,
                suavizadoMuestras = receiver != null ? receiver.SmoothingSamples : 0,
                factores = factores,
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

        /// <summary>Escena de prueba: empieza (si hace falta) y graba la condición dada de actividadPrueba.</summary>
        // Se puede grabar a mano en la escena de prueba o en Core mientras no haya una sesión en curso
        // (así se prueba con el visor, con el espejo de la sala de espera, sin crear participantes).
        private bool PuedeGrabarPrueba =>
            _manual || GameFlowManager.Instance == null || (!GameFlowManager.Instance.simulacionEnCurso && !_active);

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
                GUILayout.Label($"● Grabando REBA: {_tramo.actividad} · {_tramo.condicion} · intento {_tramo.intento} · {_tramo.stats.Count} muestras");
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

            // El tutorial lanza solo la actividad (post) al terminar. Si no se puede terminar, se lanza desde aquí.
            // Ojo: "Continuar" del panel del equipo pasa a la SIGUIENTE actividad y se pierde el post.
            if (inTutorial)
            {
                GUILayout.Label("Tutorial: NO presione 'Continuar' hasta grabar el post.");
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
