// RebaPostureEstimator.cs
//
// Une todo: Joy-Con (y opcionalmente el visor/manos del Quest) -> segmentos
// del cuerpo -> ángulos articulares -> REBA por lado (izquierdo / derecho).
//
// Distribución estándar (StandardLayout): 8 Joy-Cons en brazos, piernas, tronco
// y pelvis; la cabeza sale del visor y antebrazos/manos de los controles (a
// través del avatar, que apunta cada antebrazo hacia su control).
//
// Flujo:
//   1. Asignar en el inspector qué sensor va en qué segmento (bindings).
//      Las fuentes rastreadas (visor, controles, huesos del avatar) se calibran
//      solas; los Joy-Cons necesitan mountingEuler si no están alineados.
//   2. Con la persona en postura N (de pie, mirando al frente, brazos colgando,
//      palmas hacia los muslos, cabeza neutra) llamar a Calibrate() o
//      "Calibrar en 5 s" (menú ⋮ del componente / un botón de UI).
//   3. Cada frame se actualizan CurrentAngles, Left, Right y Worst.
//
// Lo que los IMU NO pueden medir (carga, agarre, actividad, apoyo en una
// pierna, hombro elevado, brazo apoyado) se pone a mano en "Factores manuales"
// o desde otro script según la actividad (siembra, corte, deshierbe...).

using System;
using System.Collections;
using UnityEngine;

namespace PujJoycon
{
    public enum BodySegment
    {
        Pelvis, Trunk, Head,
        UpperArmLeft, ForearmLeft, HandLeft,
        UpperArmRight, ForearmRight, HandRight,
        ThighLeft, ShankLeft, ThighRight, ShankRight,
    }

    /// <summary>
    /// De dónde sale una pregunta de sí/no de REBA: de los sensores (con su umbral) o de la
    /// decisión de las investigadoras (siempre sí / siempre no).
    /// </summary>
    public enum FuenteSiNo { Sensores, Si, No }

    [Serializable]
    public class SegmentBinding
    {
        public BodySegment segment;
        [Tooltip("Índice GLOBAL del Joy-Con (−1 = no usar Joy-Con): el que imprime el servidor principal, " +
                 "o sensorOffset + el que imprime un servidor adicional. El overlay de JoyconReceiver los muestra.")]
        public int sensorIndex = -1;
        [Tooltip("Alternativa al Joy-Con: un Transform rastreado (CenterEyeAnchor para Head, huesos del avatar " +
                 "para antebrazos y manos). Se calibra solo: no usa mountingEuler.")]
        public Transform trackedTransform;
        [Tooltip("Solo Joy-Cons. Rotación del sensor respecto al segmento (Euler XYZ, grados). " +
                 "0,0,0 si los ejes del sensor están alineados con el segmento: X derecha, Y hacia arriba por el hueso, Z adelante.")]
        public Vector3 mountingEuler;

        [NonSerialized] public Quaternion q0 = Quaternion.identity;
        [NonSerialized] public bool calibrated;
        // Para Transforms rastreados: como comparten el mundo de Unity, su "mounting"
        // se calcula al calibrar (dónde estaban sus ejes respecto al cuerpo en postura N).
        [NonSerialized] public Quaternion trackedMount = Quaternion.identity;

        public SegmentBinding() { }
        public SegmentBinding(BodySegment segment, int sensorIndex)
        {
            this.segment = segment;
            this.sensorIndex = sensorIndex;
        }
    }

    [Serializable]
    public struct ArmAngles
    {
        public bool measured;          // hay brazo (y tronco o se asume tronco vertical)
        public float upperArmFlexion;  // sagital, + adelante
        public float upperArmAbduction;
        public float upperArmElevation;
        public float upperArmRotation; // rotación del húmero (twist)
        public float elbowFlexion;     // 0 = extendido
        public float wristFlexion;
        public float wristDeviation;
        public bool hasElbow, hasWrist;
    }

    [Serializable]
    public struct PostureAngles
    {
        public bool hasTrunk, hasNeck, hasTrunkTwist, hasKnee;
        public float trunkFlexion, trunkLateral, trunkTwist;
        public float neckFlexion, neckLateral, neckTwist;
        public float kneeFlexionLeft, kneeFlexionRight;
        public ArmAngles left, right;
    }

    public class RebaPostureEstimator : MonoBehaviour
    {
        [SerializeField] private JoyconReceiver receiver;

        [Header("Asignación sensor -> segmento")]
        [SerializeField] private SegmentBinding[] bindings = StandardLayout();

        [Tooltip("Cámara del visor (CenterEyeAnchor): hacia dónde mira la persona al calibrar. Si está vacío " +
                 "se usa el Transform del segmento Head. Lo necesitan las fuentes rastreadas (visor, controles).")]
        [SerializeField] private Transform bodyForwardReference;

        /// <summary>
        /// Distribución de 8 Joy-Cons en 2 PCs (el segundo con sensorOffset 4):
        ///   PC 1: 0 tronco, 1 brazo izq., 2 muslo izq., 3 pierna izq.
        ///   PC 2: 4 pelvis, 5 brazo der., 6 muslo der., 7 pierna der.
        /// Cabeza, antebrazos y manos van sin Joy-Con (los asigna la herramienta de editor
        /// a la cámara del visor y a los huesos del avatar, que siguen a los controles).
        /// </summary>
        public static SegmentBinding[] StandardLayout() => new[]
        {
            new SegmentBinding(BodySegment.Trunk, 0),
            new SegmentBinding(BodySegment.UpperArmLeft, 1),
            new SegmentBinding(BodySegment.ThighLeft, 2),
            new SegmentBinding(BodySegment.ShankLeft, 3),
            new SegmentBinding(BodySegment.Pelvis, 4),
            new SegmentBinding(BodySegment.UpperArmRight, 5),
            new SegmentBinding(BodySegment.ThighRight, 6),
            new SegmentBinding(BodySegment.ShankRight, 7),
            new SegmentBinding(BodySegment.Head, -1),
            new SegmentBinding(BodySegment.ForearmLeft, -1),
            new SegmentBinding(BodySegment.HandLeft, -1),
            new SegmentBinding(BodySegment.ForearmRight, -1),
            new SegmentBinding(BodySegment.HandRight, -1),
        };

        [Header("Calibración automática del montaje (sentadilla)")]
        [Tooltip("Dónde queda 'arriba' en el mundo de los Joy-Cons: (0, este valor, 0). Verificado con el " +
                 "servidor de Joy-Cons: +1. Si después de la sentadilla la flexión sale bien pero la torsión y " +
                 "la inclinación lateral salen AL REVÉS, cambie el signo.")]
        [SerializeField] private float gravityUpY = 1f;
        [Tooltip("Giro mínimo (grados) de cada segmento en la sentadilla para confiar en él.")]
        [SerializeField] private float minMovementAngle = 15f;

        [Header("Umbrales REBA")]
        [SerializeField] private RebaThresholds thresholds = new RebaThresholds();

        [Header("Factores manuales (no medibles con IMU)")]
        [SerializeField] private float loadKg;
        [SerializeField] private bool shockOrRapidForce;
        [SerializeField, Range(0, 3)] private int coupling;
        [SerializeField, Range(0, 3)] private int activity;
        [SerializeField] private bool legsUnilateralOrUnstable;
        [SerializeField] private bool sitting;
        [SerializeField] private bool shoulderRaisedLeft, shoulderRaisedRight;
        [SerializeField] private bool armSupportedLeft, armSupportedRight;
        [Tooltip("Actividad REBA (+1 cada una): una o más partes del cuerpo estáticas más de 1 minuto.")]
        [SerializeField] private bool staticPosture;
        [Tooltip("Actividad REBA: movimientos repetitivos (más de 4 por minuto, en rango pequeño).")]
        [SerializeField] private bool repetitiveMovements;
        [Tooltip("Actividad REBA: cambios posturales grandes y rápidos, o postura/base inestable.")]
        [SerializeField] private bool rapidChangesOrUnstable;

        [Header("Salida (solo lectura, visible en Play)")]
        [SerializeField] private PostureAngles currentAngles;
        [SerializeField] private RebaResult left;
        [SerializeField] private RebaResult right;
        [SerializeField] private int worstScoreSoFar;
        [SerializeField] private bool showOverlay = true;

        private readonly Quaternion[] _segments = new Quaternion[Enum.GetValues(typeof(BodySegment)).Length];
        private readonly bool[] _hasSegment = new bool[Enum.GetValues(typeof(BodySegment)).Length];
        private bool _calibrated;
        private float _calibrationCountdown;

        public bool IsCalibrated => _calibrated;
        public PostureAngles CurrentAngles => currentAngles;
        public RebaResult Left => left;
        public RebaResult Right => right;
        public RebaResult Worst => left.final >= right.final ? left : right;
        public int WorstScoreSoFar => worstScoreSoFar;

        // Permite que la lógica de cada actividad ajuste lo no medible.
        public float LoadKg { get => loadKg; set => loadKg = value; }
        public int Coupling { get => coupling; set => coupling = Mathf.Clamp(value, 0, 3); }
        public int Activity { get => activity; set => activity = Mathf.Clamp(value, 0, 3); }
        public bool ShockOrRapidForce { get => shockOrRapidForce; set => shockOrRapidForce = value; }
        /// <summary>Hombro elevado (encogido): no lo mide el Joy-Con; se aplica a los dos brazos.</summary>
        public bool ShoulderRaised { set { shoulderRaisedLeft = value; shoulderRaisedRight = value; } }
        /// <summary>Brazo apoyado o postura a favor de la gravedad (−1): se aplica a los dos brazos.</summary>
        public bool ArmSupported { set { armSupportedLeft = value; armSupportedRight = value; } }
        // Preguntas de sí/no que se pueden decidir a mano en vez de medir (RebaSessionRecorder las fija
        // por actividad y condición). Por defecto las miden los sensores.
        public FuenteSiNo TrunkTwistOrSideBendSource { get; set; } = FuenteSiNo.Sensores;
        public FuenteSiNo NeckTwistOrSideBendSource { get; set; } = FuenteSiNo.Sensores;
        public FuenteSiNo ArmRotatedSource { get; set; } = FuenteSiNo.Sensores;
        public FuenteSiNo WristDeviatedOrTwistedSource { get; set; } = FuenteSiNo.Sensores;

        private static bool Resolve(FuenteSiNo source, bool measured) =>
            source == FuenteSiNo.Si || (source == FuenteSiNo.Sensores && measured);

        public bool StaticPosture { get => staticPosture; set => staticPosture = value; }
        public bool RepetitiveMovements { get => repetitiveMovements; set => repetitiveMovements = value; }
        public bool RapidChangesOrUnstable { get => rapidChangesOrUnstable; set => rapidChangesOrUnstable = value; }

        /// <summary>Entradas de REBA del último frame (ángulos ya convertidos + factores manuales), por lado.</summary>
        public RebaInputs LeftInputs => _leftInputs;
        public RebaInputs RightInputs => _rightInputs;
        public RebaThresholds Thresholds => thresholds;
        public float GravityUpY => gravityUpY;
        /// <summary>Asignación sensor -> segmento (con el mountingEuler de la última calibración). Solo lectura.</summary>
        public System.Collections.Generic.IReadOnlyList<SegmentBinding> Bindings => bindings;

        private RebaInputs _leftInputs, _rightInputs;

        private void Awake()
        {
            if (receiver == null) receiver = FindAnyObjectByType<JoyconReceiver>();
        }

        // ------------------------------------------------------------------
        // Calibración
        // ------------------------------------------------------------------

        [ContextMenu("Calibrar ahora (postura N)")]
        public void Calibrate()
        {
            Quaternion body0 = BodyFrameNow();
            int ok = 0, missing = 0;
            foreach (var b in bindings)
            {
                if (!HasSource(b)) continue;
                if (TryGetSourceRotation(b, out var q))
                {
                    b.q0 = q;
                    b.trackedMount = PostureMath.Inverse(body0) * q;
                    b.calibrated = true;
                    ok++;
                }
                else
                {
                    b.calibrated = false;
                    missing++;
                    Debug.LogWarning($"[REBA] Sin datos para {b.segment} al calibrar.", this);
                }
            }
            _calibrated = ok > 0;
            worstScoreSoFar = 0;
            Debug.Log($"[REBA] Calibración: {ok} segmento(s) OK, {missing} sin datos.", this);
            if (_calibrated) Calibrated?.Invoke();
        }

        /// <summary>Se dispara al terminar una calibración exitosa (JoyconAvatarDriver la usa para tomar la postura N del avatar).</summary>
        public event Action Calibrated;

        /// <summary>Resultado de la última calibración del montaje, una línea por Joy-Con.</summary>
        public string LastMountingReport { get; private set; }

        /// <summary>
        /// Paso 2 de la calibración, con la persona en la sentadilla (rodillas dobladas, tronco
        /// un poco adelante, brazos estirados al frente). Debe llamarse después de Calibrate()
        /// en postura N. Calcula el mountingEuler de cada Joy-Con a partir de la gravedad
        /// (postura N) y del giro alrededor del eje lateral (sentadilla).
        /// Devuelve true si todos los Joy-Cons quedaron calibrados.
        /// </summary>
        [ContextMenu("Calibrar montaje con la sentadilla (ahora)")]
        public bool CalibrateMountingFromSquat()
        {
            var report = new System.Text.StringBuilder();
            bool allOk = true;
            foreach (var b in bindings)
            {
                if (b.trackedTransform != null || b.sensorIndex < 0 || !b.calibrated) continue;
                float sign = ExpectedSquatSign(b.segment);
                if (sign == 0f) continue;

                string problem;
                if (!TryGetSourceRotation(b, out var q))
                {
                    problem = "sin datos";
                }
                else if (PostureMath.MountingFromMovement(b.q0, q, sign, gravityUpY, minMovementAngle,
                                                          out var mounting, out problem))
                {
                    b.mountingEuler = PostureMath.ToEuler(mounting, EulerOrder.XYZ);
                    report.AppendLine($"{b.segment}: OK");
                    continue;
                }
                allOk = false;
                report.AppendLine($"{b.segment}: {problem}");
            }
            LastMountingReport = report.ToString().TrimEnd();
            if (allOk) Debug.Log($"[REBA] Montaje calibrado con la sentadilla:\n{LastMountingReport}", this);
            else Debug.LogWarning($"[REBA] Montaje: algunos Joy-Cons no se calibraron:\n{LastMountingReport}", this);
            return allOk;
        }

        /// <summary>
        /// Sentido en que gira cada segmento alrededor del eje lateral (X) en la sentadilla:
        /// +1 si su eje Y (hacia arriba) se va hacia adelante (tronco, pelvis, pierna: la rodilla
        /// avanza), −1 si su eje −Y (hacia abajo) se va hacia adelante (brazos al frente, muslos).
        /// </summary>
        public static float ExpectedSquatSign(BodySegment segment)
        {
            switch (segment)
            {
                case BodySegment.Trunk:
                case BodySegment.Pelvis:
                case BodySegment.Head:
                case BodySegment.ShankLeft:
                case BodySegment.ShankRight:
                    return 1f;
                case BodySegment.UpperArmLeft:
                case BodySegment.UpperArmRight:
                case BodySegment.ForearmLeft:
                case BodySegment.ForearmRight:
                case BodySegment.HandLeft:
                case BodySegment.HandRight:
                case BodySegment.ThighLeft:
                case BodySegment.ThighRight:
                    return -1f;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Orientación actual del segmento en el marco del cuerpo C (identidad en postura N).
        /// False si el segmento no tiene fuente, no se calibró o su sensor no está mandando datos.
        /// </summary>
        public bool TryGetSegment(BodySegment segment, out Quaternion orientation)
        {
            orientation = _segments[(int)segment];
            return _calibrated && _hasSegment[(int)segment];
        }

        /// <summary>True si el segmento tiene un Joy-Con asignado (no un Transform rastreado).</summary>
        public bool IsJoyconSegment(BodySegment segment)
        {
            foreach (var b in bindings)
            {
                if (b.segment == segment) return b.trackedTransform == null && b.sensorIndex >= 0;
            }
            return false;
        }

        [ContextMenu("Calibrar en 5 s")]
        public void CalibrateIn5Seconds() => CalibrateAfterDelay(5f);

        public void CalibrateAfterDelay(float seconds)
        {
            StopAllCoroutines();
            StartCoroutine(CalibrateRoutine(seconds));
        }

        private IEnumerator CalibrateRoutine(float seconds)
        {
            _calibrationCountdown = seconds;
            while (_calibrationCountdown > 0f)
            {
                yield return null;
                _calibrationCountdown -= Time.unscaledDeltaTime;
            }
            _calibrationCountdown = 0f;
            Calibrate();
        }

        // ------------------------------------------------------------------
        // Por frame
        // ------------------------------------------------------------------

        private void Update()
        {
            if (!_calibrated) return;

            Array.Clear(_hasSegment, 0, _hasSegment.Length);
            foreach (var b in bindings)
            {
                if (!b.calibrated || !TryGetSourceRotation(b, out var q)) continue;
                // Joy-Con: mounting del inspector (se lee cada frame para poder ajustarlo en Play).
                var mount = b.trackedTransform != null
                    ? b.trackedMount
                    : PostureMath.FromEuler(b.mountingEuler, EulerOrder.XYZ);
                _segments[(int)b.segment] = PostureMath.SegmentOrientation(q, b.q0, mount);
                _hasSegment[(int)b.segment] = true;
            }

            currentAngles = ComputeAngles();
            _leftInputs = BuildInputs(currentAngles, currentAngles.left, true);
            _rightInputs = BuildInputs(currentAngles, currentAngles.right, false);
            left = Reba.Compute(_leftInputs, thresholds);
            right = Reba.Compute(_rightInputs, thresholds);
            worstScoreSoFar = Mathf.Max(worstScoreSoFar, Worst.final);
        }

        private PostureAngles ComputeAngles()
        {
            var a = new PostureAngles();
            bool hasTrunk = Has(BodySegment.Trunk);
            // Sin sensor de tronco se asume tronco vertical para medir los brazos.
            Quaternion trunk = hasTrunk ? Seg(BodySegment.Trunk) : Quaternion.identity;

            if (hasTrunk)
            {
                a.hasTrunk = true;
                PostureMath.TiltAngles(trunk, out a.trunkFlexion, out a.trunkLateral);
                if (Has(BodySegment.Pelvis))
                {
                    a.hasTrunkTwist = true;
                    a.trunkTwist = PostureMath.TwistAngle(PostureMath.Relative(Seg(BodySegment.Pelvis), trunk), Vector3.up);
                }
            }

            if (Has(BodySegment.Head))
            {
                a.hasNeck = true;
                Quaternion neck = PostureMath.Relative(trunk, Seg(BodySegment.Head));
                PostureMath.TiltAngles(neck, out a.neckFlexion, out a.neckLateral);
                a.neckTwist = PostureMath.TwistAngle(neck, Vector3.up);
            }

            a.left = ComputeArm(trunk, BodySegment.UpperArmLeft, BodySegment.ForearmLeft, BodySegment.HandLeft, true);
            a.right = ComputeArm(trunk, BodySegment.UpperArmRight, BodySegment.ForearmRight, BodySegment.HandRight, false);

            if (Has(BodySegment.ThighLeft) && Has(BodySegment.ShankLeft))
            {
                a.hasKnee = true;
                a.kneeFlexionLeft = PostureMath.HingeFlexion(Seg(BodySegment.ThighLeft), Seg(BodySegment.ShankLeft));
            }
            if (Has(BodySegment.ThighRight) && Has(BodySegment.ShankRight))
            {
                a.hasKnee = true;
                a.kneeFlexionRight = PostureMath.HingeFlexion(Seg(BodySegment.ThighRight), Seg(BodySegment.ShankRight));
            }
            return a;
        }

        private ArmAngles ComputeArm(Quaternion trunk, BodySegment upper, BodySegment fore, BodySegment hand, bool isLeft)
        {
            var arm = new ArmAngles();
            if (Has(upper))
            {
                arm.measured = true;
                Quaternion rel = PostureMath.Relative(trunk, Seg(upper));
                PostureMath.UpperArmAngles(rel, isLeft, out arm.upperArmFlexion, out arm.upperArmAbduction, out arm.upperArmElevation);
                arm.upperArmRotation = PostureMath.TwistAngle(rel, Vector3.down);
            }
            if (Has(upper) && Has(fore))
            {
                arm.hasElbow = true;
                arm.elbowFlexion = PostureMath.HingeFlexion(Seg(upper), Seg(fore));
            }
            if (Has(fore) && Has(hand))
            {
                arm.hasWrist = true;
                PostureMath.WristAngles(PostureMath.Relative(Seg(fore), Seg(hand)), isLeft,
                                        out arm.wristFlexion, out arm.wristDeviation);
            }
            return arm;
        }

        private RebaInputs BuildInputs(PostureAngles a, ArmAngles arm, bool isLeft)
        {
            float tw = thresholds.twistOrSideBendThreshold;

            // Brazo: REBA puntúa la elevación del brazo. Si va hacia atrás (más
            // extensión que abducción) se usa el ángulo sagital negativo; si no,
            // la elevación total, para no subestimar brazos levantados de lado.
            float upperArm = arm.upperArmElevation;
            if (arm.upperArmFlexion < 0f && Mathf.Abs(arm.upperArmAbduction) < Mathf.Abs(arm.upperArmFlexion))
            {
                upperArm = arm.upperArmFlexion;
            }

            bool abducted = arm.measured && arm.upperArmAbduction > thresholds.armAbductionThreshold;
            bool rotated = Resolve(ArmRotatedSource,
                                   arm.measured && Mathf.Abs(arm.upperArmRotation) > thresholds.armRotationThreshold);

            return new RebaInputs
            {
                trunkFlexion = a.trunkFlexion,
                trunkTwistedOrSideBent = Resolve(TrunkTwistOrSideBendSource,
                    Mathf.Abs(a.trunkLateral) > tw || (a.hasTrunkTwist && Mathf.Abs(a.trunkTwist) > thresholds.twistThreshold)),
                neckFlexion = a.neckFlexion,
                neckTwistedOrSideBent = Resolve(NeckTwistOrSideBendSource,
                    a.hasNeck && (Mathf.Abs(a.neckLateral) > tw || Mathf.Abs(a.neckTwist) > thresholds.twistThreshold)),
                legsUnilateralOrUnstable = legsUnilateralOrUnstable,
                kneeFlexion = Mathf.Max(a.kneeFlexionLeft, a.kneeFlexionRight),
                sitting = sitting,
                loadKg = loadKg,
                shockOrRapidForce = shockOrRapidForce,

                upperArmFlexion = arm.measured ? upperArm : 0f,
                upperArmAbductedOrRotated = abducted || rotated,
                upperArmAbducted = abducted,
                upperArmRotated = rotated,
                shoulderRaised = isLeft ? shoulderRaisedLeft : shoulderRaisedRight,
                armSupportedOrLeaning = isLeft ? armSupportedLeft : armSupportedRight,
                // Sin datos de codo se asume 80° (puntaje 1) en vez de 0° (que daría 2).
                elbowFlexion = arm.hasElbow ? arm.elbowFlexion : 80f,
                wristFlexion = arm.hasWrist ? arm.wristFlexion : 0f,
                wristDeviatedOrTwisted = Resolve(WristDeviatedOrTwistedSource, arm.hasWrist && Mathf.Abs(arm.wristDeviation) > tw),
                coupling = coupling,
                staticPosture = staticPosture,
                repetitiveMovements = repetitiveMovements,
                rapidChangesOrUnstable = rapidChangesOrUnstable,
                activity = activity,
            };
        }

        // ------------------------------------------------------------------
        // Fuentes de orientación
        // ------------------------------------------------------------------

        private static bool HasSource(SegmentBinding b) => b.trackedTransform != null || b.sensorIndex >= 0;

        // Marco del cuerpo C en el mundo de Unity: Y arriba, Z hacia donde mira la persona.
        private Quaternion BodyFrameNow()
        {
            Transform reference = bodyForwardReference;
            if (reference == null)
            {
                foreach (var b in bindings)
                {
                    if (b.segment == BodySegment.Head && b.trackedTransform != null) reference = b.trackedTransform;
                }
            }
            if (reference == null) return Quaternion.identity;
            Vector3 forward = reference.forward;
            forward.y = 0f;
            return forward.sqrMagnitude < 1e-6f
                ? Quaternion.identity
                : Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private bool TryGetSourceRotation(SegmentBinding b, out Quaternion q)
        {
            if (b.trackedTransform != null)
            {
                q = b.trackedTransform.rotation;
                return true;
            }
            q = Quaternion.identity;
            return b.sensorIndex >= 0 && receiver != null && receiver.TryGetRotation(b.sensorIndex, out q);
        }

        private bool Has(BodySegment s) => _hasSegment[(int)s];
        private Quaternion Seg(BodySegment s) => _segments[(int)s];

        /// <summary>Muestra u oculta el recuadro de ángulos y puntajes REBA en la vista Game.</summary>
        public bool ShowOverlay
        {
            get => showOverlay;
            set => showOverlay = value;
        }

        private void OnGUI()
        {
            if (!showOverlay) return;
            // A la derecha del overlay de JoyconReceiver, que crece con el número de sensores.
            GUILayout.BeginArea(new Rect(460, 10, 520, 230), GUI.skin.box);
            if (_calibrationCountdown > 0f)
            {
                GUILayout.Label($"Calibrando en {_calibrationCountdown:F1} s — quédese en postura N");
            }
            else if (!_calibrated)
            {
                GUILayout.Label("REBA: sin calibrar (menú ⋮ del componente → Calibrar en 5 s)");
            }
            else
            {
                var a = currentAngles;
                GUILayout.Label($"Tronco  flex {a.trunkFlexion,6:F1}°  lat {a.trunkLateral,6:F1}°  torsión {(a.hasTrunkTwist ? a.trunkTwist.ToString("F1") + "°" : "n/d")}");
                GUILayout.Label(a.hasNeck ? $"Cuello  flex {a.neckFlexion,6:F1}°  lat {a.neckLateral,6:F1}°  rot {a.neckTwist,6:F1}°" : "Cuello  n/d");
                GUILayout.Label($"Brazo I flex {a.left.upperArmFlexion,6:F1}° abd {a.left.upperArmAbduction,6:F1}° codo {(a.left.hasElbow ? a.left.elbowFlexion.ToString("F0") + "°" : "n/d")}");
                GUILayout.Label($"Brazo D flex {a.right.upperArmFlexion,6:F1}° abd {a.right.upperArmAbduction,6:F1}° codo {(a.right.hasElbow ? a.right.elbowFlexion.ToString("F0") + "°" : "n/d")}");
                GUILayout.Label(a.hasKnee ? $"Rodillas I {a.kneeFlexionLeft:F0}°  D {a.kneeFlexionRight:F0}°" : "Rodillas n/d");
                GUILayout.Label($"Izq: {left}");
                GUILayout.Label($"Der: {right}");
                GUILayout.Label($"Peor REBA en la sesión: {worstScoreSoFar}");
            }
            GUILayout.EndArea();
        }
    }
}
