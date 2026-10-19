// RebaSessionRecorder.cs
//
// Guarda REBA de cada sesión para el análisis de la tesis. Por sesión crea la carpeta
//   <persistentDataPath>/REBA/<fecha>_P<id del participante>/
// con:
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
//
// Al terminar la sesión también guarda el resumen pre/tutorial/post en el historial del
// participante (Capacitacion -> ActividadRealizada -> RebaData en usuarios.json).
//
// CSV para Excel en español: separador ';' y decimales con coma.
// Solo se identifica al participante por su Id (no se guarda nombre ni correo en los CSV).
//
// Sin GameFlowManager (escena de prueba) se graba a mano con los botones en pantalla.

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
        [Serializable]
        public class FactoresActividad
        {
            public ActivityType actividad;
            [Tooltip("Carga que se manipula en esta actividad (kg). REBA: <5 kg = 0, 5–10 = +1, >10 = +2.")]
            public float cargaKg;
            [Tooltip("Golpes o fuerza brusca (+1 a la carga).")]
            public bool impactoOFuerzaBrusca;
            [Tooltip("Agarre: 0 bueno, 1 regular, 2 malo, 3 inaceptable.")]
            [Range(0, 3)] public int agarre;
        }

        public const string CarpetaRaiz = "REBA";
        private const char Sep = ';';

        [SerializeField] private RebaPostureEstimator estimator;
        [SerializeField] private JoyconReceiver receiver;
        [SerializeField] private JoyconSessionCalibrator calibrator;

        [Header("Muestreo")]
        [SerializeField, Range(1f, 60f)] private float muestrasPorSegundo = 10f;
        [SerializeField] private bool grabarTutorial = true;

        [Header("Factores de REBA que no miden los sensores (por actividad)")]
        [SerializeField] private FactoresActividad[] factores =
        {
            new FactoresActividad { actividad = ActivityType.Siembra },
            new FactoresActividad { actividad = ActivityType.Deshierbe },
            new FactoresActividad { actividad = ActivityType.Corte },
        };

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
        private StreamWriter _samples, _events;
        private bool _samplesHeaderWritten;
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

        public bool Grabando => _tramo != null;
        public string CarpetaSesion => _folder;

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
                }
            }

            if (!_active) return;
            Sample();
            MonitorServers();
            if (Time.unscaledTime >= _nextFlush)
            {
                _samples?.Flush();
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
            _events = new StreamWriter(Path.Combine(_folder, "eventos.csv"), false, utf8Bom);
            _events.WriteLine(string.Join(Sep.ToString(), "fecha_hora", "t_sesion_s", "tipo", "detalle"));
            _samplesHeaderWritten = false;

            _tramos.Clear();
            _attempts.Clear();
            _seenTutorial.Clear();
            _tramo = null;
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
            SetCondition(condicion, actividad, indice);
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

            if (actividad != null && actividad != _lastActivity)
            {
                ApplyFactors(actividad.Value);
                _lastActivity = actividad;
            }
            if (condicion == null || actividad == null) return;

            string key = $"{indice}|{condicion}";
            _attempts.TryGetValue(key, out int n);
            _attempts[key] = ++n;
            _tramo = new Tramo { indice = indice, actividad = actividad.Value, condicion = condicion, intento = n };
            _tramos.Add(_tramo);
            _tramoT0 = Time.unscaledTime;
            _nextSampleT = Time.unscaledTime;
            LogEvent("inicio_tramo", $"{_tramo.actividad} {condicion} intento {n}");
        }

        private void ApplyFactors(ActivityType actividad)
        {
            if (estimator == null || factores == null) return;
            foreach (var f in factores)
            {
                if (f == null || f.actividad != actividad) continue;
                estimator.LoadKg = f.cargaKg;
                estimator.ShockOrRapidForce = f.impactoOFuerzaBrusca;
                estimator.Coupling = f.agarre;
                LogEvent("factores", $"{actividad}: carga {F(f.cargaKg)} kg, impacto {(f.impactoOFuerzaBrusca ? "sí" : "no")}, agarre {f.agarre}");
                return;
            }
        }

        private void EndSession(string motivo)
        {
            if (!_active) return;
            SetCondition(null, null, -1);
            if (!_finalized) FinalizeData(motivo);
            LogEvent("fin_sesion", motivo);
            _samples?.Dispose();
            _events?.Dispose();
            _samples = _events = null;
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
            r.Add("riesgo_final", worst.risk.ToString());
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
            r.Add($"{side}_riesgo", x.risk.ToString());
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
        public void GrabarPrueba(string condicion)
        {
            if (GameFlowManager.Instance != null && !_manual)
            {
                Debug.LogWarning("[REBA] Hay GameFlowManager: la grabación la maneja el flujo de la sesión.", this);
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
            float w = 420, x = Screen.width - w - 10, y = Screen.height - 100;
            GUILayout.BeginArea(new Rect(x, y, w, 90), GUI.skin.box);
            if (_tramo != null)
            {
                GUILayout.Label($"● Grabando REBA: {_tramo.actividad} · {_tramo.condicion} · intento {_tramo.intento} · {_tramo.stats.Count} muestras");
            }
            else
            {
                GUILayout.Label(_active ? "REBA: sesión abierta, sin actividad en curso" : "REBA: no está grabando");
            }

            if (GameFlowManager.Instance == null || _manual)
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
    }

}
