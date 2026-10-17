// RebaPostureEstimator.cs
//
// Une todo: Joy-Con (y opcionalmente el visor/manos del Quest) -> segmentos
// del cuerpo -> ángulos articulares -> REBA por lado (izquierdo / derecho).
//
// Flujo:
//   1. Asignar en el inspector qué sensor va en qué segmento (bindings).
//      Para Head conviene usar el CenterEyeAnchor del rig en vez de un Joy-Con.
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

    [Serializable]
    public class SegmentBinding
    {
        public BodySegment segment;
        [Tooltip("Índice GLOBAL del Joy-Con (−1 = no usar Joy-Con): el que imprime el servidor principal, " +
                 "o sensorOffset + el que imprime un servidor adicional. El overlay de JoyconReceiver los muestra.")]
        public int sensorIndex = -1;
        [Tooltip("Alternativa al Joy-Con: un Transform rastreado (ej. CenterEyeAnchor para Head).")]
        public Transform trackedTransform;
        [Tooltip("Rotación del sensor respecto al segmento (Euler XYZ, grados). " +
                 "0,0,0 si los ejes del sensor están alineados con el segmento: X derecha, Y hacia arriba por el hueso, Z adelante.")]
        public Vector3 mountingEuler;

        [NonSerialized] public Quaternion q0 = Quaternion.identity;
        [NonSerialized] public bool calibrated;

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
        [SerializeField] private SegmentBinding[] bindings =
        {
            // Propuesta para 8 Joy-Cons + visor del Quest para la cabeza (ver README).
            new SegmentBinding(BodySegment.Trunk, 0),
            new SegmentBinding(BodySegment.Pelvis, 1),
            new SegmentBinding(BodySegment.UpperArmLeft, 2),
            new SegmentBinding(BodySegment.ForearmLeft, 3),
            new SegmentBinding(BodySegment.UpperArmRight, 4),
            new SegmentBinding(BodySegment.ForearmRight, 5),
            new SegmentBinding(BodySegment.ThighRight, 6),
            new SegmentBinding(BodySegment.ShankRight, 7),
            new SegmentBinding(BodySegment.Head, -1),
        };

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
            int ok = 0, missing = 0;
            foreach (var b in bindings)
            {
                if (!HasSource(b)) continue;
                if (TryGetSourceRotation(b, out var q))
                {
                    b.q0 = q;
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
                var mount = PostureMath.FromEuler(b.mountingEuler, EulerOrder.XYZ);
                _segments[(int)b.segment] = PostureMath.SegmentOrientation(q, b.q0, mount);
                _hasSegment[(int)b.segment] = true;
            }

            currentAngles = ComputeAngles();
            left = Reba.Compute(BuildInputs(currentAngles, currentAngles.left, true), thresholds);
            right = Reba.Compute(BuildInputs(currentAngles, currentAngles.right, false), thresholds);
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

            return new RebaInputs
            {
                trunkFlexion = a.trunkFlexion,
                trunkTwistedOrSideBent = Mathf.Abs(a.trunkLateral) > tw || (a.hasTrunkTwist && Mathf.Abs(a.trunkTwist) > tw),
                neckFlexion = a.neckFlexion,
                neckTwistedOrSideBent = a.hasNeck && (Mathf.Abs(a.neckLateral) > tw || Mathf.Abs(a.neckTwist) > tw),
                legsUnilateralOrUnstable = legsUnilateralOrUnstable,
                kneeFlexion = Mathf.Max(a.kneeFlexionLeft, a.kneeFlexionRight),
                sitting = sitting,
                loadKg = loadKg,
                shockOrRapidForce = shockOrRapidForce,

                upperArmFlexion = arm.measured ? upperArm : 0f,
                upperArmAbductedOrRotated = arm.measured &&
                    (arm.upperArmAbduction > thresholds.armAbductionThreshold ||
                     Mathf.Abs(arm.upperArmRotation) > thresholds.armRotationThreshold),
                shoulderRaised = isLeft ? shoulderRaisedLeft : shoulderRaisedRight,
                armSupportedOrLeaning = isLeft ? armSupportedLeft : armSupportedRight,
                // Sin datos de codo se asume 80° (puntaje 1) en vez de 0° (que daría 2).
                elbowFlexion = arm.hasElbow ? arm.elbowFlexion : 80f,
                wristFlexion = arm.hasWrist ? arm.wristFlexion : 0f,
                wristDeviatedOrTwisted = arm.hasWrist && Mathf.Abs(arm.wristDeviation) > tw,
                coupling = coupling,
                activity = activity,
            };
        }

        // ------------------------------------------------------------------
        // Fuentes de orientación
        // ------------------------------------------------------------------

        private static bool HasSource(SegmentBinding b) => b.trackedTransform != null || b.sensorIndex >= 0;

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
