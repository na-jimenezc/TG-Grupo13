// Reba.cs
//
// Puntuación REBA (Rapid Entire Body Assessment, Hignett & McAtamney, 2000) a
// partir de ángulos ya calculados. No depende de UnityEngine.
//
// Separado a propósito de la estimación de postura: aquí solo entran números
// (grados + factores que los Joy-Cons no pueden medir) y salen puntajes. Así se
// puede validar contra hojas REBA hechas a mano sin hardware.
//
// IMPORTANTE (tesis): verificar umbrales y tablas contra el artículo original /
// la hoja oficial que use su grupo; los umbrales "neutro" y "torcido" no los
// define REBA en grados y son decisiones metodológicas (ver RebaThresholds).

using System;

namespace PujJoycon
{
    [Serializable]
    public class RebaThresholds
    {
        /// <summary>|flexión de tronco| por debajo de esto = "erguido" (REBA no da un número).</summary>
        public float trunkUprightTolerance = 5f;
        /// <summary>Extensión de cuello por debajo de −esto cuenta como "en extensión".</summary>
        public float neckExtensionTolerance = 5f;
        /// <summary>Torsión o inclinación lateral por encima de esto suma +1 (tronco, cuello, muñeca).</summary>
        public float twistOrSideBendThreshold = 10f;
        /// <summary>Abducción o rotación de brazo por encima de esto suma +1.</summary>
        public float armAbductionThreshold = 20f;
        public float armRotationThreshold = 30f;
    }

    [Serializable]
    public struct RebaInputs
    {
        // --- Grupo A ---
        public float trunkFlexion;          // grados, + flexión, − extensión
        public bool trunkTwistedOrSideBent;
        public float neckFlexion;           // grados, + flexión, − extensión
        public bool neckTwistedOrSideBent;
        public bool legsUnilateralOrUnstable; // apoyo en una pierna / inestable (no medible con IMU)
        public float kneeFlexion;           // grados, la rodilla más flexionada
        public bool sitting;                // sentado: no se suma por flexión de rodilla
        public float loadKg;
        public bool shockOrRapidForce;

        // --- Grupo B (un lado) ---
        public float upperArmFlexion;       // grados, + flexión, − extensión (ver RebaPostureEstimator)
        public bool upperArmAbductedOrRotated;
        public bool shoulderRaised;         // no medible con un Joy-Con en el brazo
        public bool armSupportedOrLeaning;
        public float elbowFlexion;          // grados, 0 = brazo extendido
        public float wristFlexion;          // grados, + flexión, − extensión
        public bool wristDeviatedOrTwisted;
        public int coupling;                // 0 bueno, 1 regular, 2 malo, 3 inaceptable

        // --- Actividad ---
        public int activity;                // 0..3: estático >1 min, repetitivo >4/min, cambios rápidos/base inestable
    }

    public enum RebaRisk { Negligible, Low, Medium, High, VeryHigh }

    [Serializable]
    public struct RebaResult
    {
        public int trunk, neck, legs, load;
        public int upperArm, lowerArm, wrist, coupling;
        public int tableA, scoreA, tableB, scoreB, tableC, activity;
        public int final;
        public RebaRisk risk;

        public override string ToString() =>
            $"REBA {final} ({risk}) | A: tronco {trunk} cuello {neck} piernas {legs} -> {tableA}+{load}={scoreA}" +
            $" | B: brazo {upperArm} antebrazo {lowerArm} muñeca {wrist} -> {tableB}+{coupling}={scoreB}" +
            $" | C {tableC} + act {activity}";
    }

    public static class Reba
    {
        // Tabla A [tronco 1..5][cuello 1..3][piernas 1..4]
        private static readonly int[,,] TableA =
        {
            { { 1, 2, 3, 4 }, { 1, 2, 3, 4 }, { 3, 3, 5, 6 } },
            { { 2, 3, 4, 5 }, { 3, 4, 5, 6 }, { 4, 5, 6, 7 } },
            { { 2, 4, 5, 6 }, { 4, 5, 6, 7 }, { 5, 6, 7, 8 } },
            { { 3, 5, 6, 7 }, { 5, 6, 7, 8 }, { 6, 7, 8, 9 } },
            { { 4, 6, 7, 8 }, { 6, 7, 8, 9 }, { 7, 8, 9, 9 } },
        };

        // Tabla B [brazo 1..6][antebrazo 1..2][muñeca 1..3]
        private static readonly int[,,] TableB =
        {
            { { 1, 2, 2 }, { 1, 2, 3 } },
            { { 1, 2, 3 }, { 2, 3, 4 } },
            { { 3, 4, 5 }, { 4, 5, 5 } },
            { { 4, 5, 5 }, { 5, 6, 7 } },
            { { 6, 7, 8 }, { 7, 8, 8 } },
            { { 7, 8, 8 }, { 8, 9, 9 } },
        };

        // Tabla C [puntaje A 1..12][puntaje B 1..12]
        private static readonly int[,] TableC =
        {
            {  1,  1,  1,  2,  3,  3,  4,  5,  6,  7,  7,  7 },
            {  1,  2,  2,  3,  4,  4,  5,  6,  6,  7,  7,  8 },
            {  2,  3,  3,  3,  4,  5,  6,  7,  7,  8,  8,  8 },
            {  3,  4,  4,  4,  5,  6,  7,  8,  8,  9,  9,  9 },
            {  4,  4,  4,  5,  6,  7,  8,  8,  9,  9,  9,  9 },
            {  6,  6,  6,  7,  8,  8,  9,  9, 10, 10, 10, 10 },
            {  7,  7,  7,  8,  9,  9,  9, 10, 10, 11, 11, 11 },
            {  8,  8,  8,  9, 10, 10, 10, 10, 10, 11, 11, 11 },
            {  9,  9,  9, 10, 10, 10, 11, 11, 11, 12, 12, 12 },
            { 10, 10, 10, 11, 11, 11, 11, 12, 12, 12, 12, 12 },
            { 11, 11, 11, 11, 12, 12, 12, 12, 12, 12, 12, 12 },
            { 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12 },
        };

        public static RebaResult Compute(RebaInputs input, RebaThresholds t)
        {
            if (t == null) t = new RebaThresholds();
            var r = new RebaResult
            {
                trunk = ScoreTrunk(input.trunkFlexion, input.trunkTwistedOrSideBent, t),
                neck = ScoreNeck(input.neckFlexion, input.neckTwistedOrSideBent, t),
                legs = ScoreLegs(input.legsUnilateralOrUnstable, input.kneeFlexion, input.sitting),
                load = ScoreLoad(input.loadKg, input.shockOrRapidForce),
                upperArm = ScoreUpperArm(input.upperArmFlexion, input.upperArmAbductedOrRotated,
                                         input.shoulderRaised, input.armSupportedOrLeaning),
                lowerArm = ScoreLowerArm(input.elbowFlexion),
                wrist = ScoreWrist(input.wristFlexion, input.wristDeviatedOrTwisted),
                coupling = Clamp(input.coupling, 0, 3),
                activity = Clamp(input.activity, 0, 3),
            };

            r.tableA = TableA[r.trunk - 1, r.neck - 1, r.legs - 1];
            r.scoreA = Clamp(r.tableA + r.load, 1, 12);
            r.tableB = TableB[r.upperArm - 1, r.lowerArm - 1, r.wrist - 1];
            r.scoreB = Clamp(r.tableB + r.coupling, 1, 12);
            r.tableC = TableC[r.scoreA - 1, r.scoreB - 1];
            r.final = r.tableC + r.activity;
            r.risk = RiskOf(r.final);
            return r;
        }

        public static int ScoreTrunk(float flexion, bool twistedOrSideBent, RebaThresholds t)
        {
            int s;
            if (Math.Abs(flexion) <= t.trunkUprightTolerance) s = 1;
            else if (flexion < 0f) s = flexion >= -20f ? 2 : 3;   // extensión
            else if (flexion <= 20f) s = 2;
            else if (flexion <= 60f) s = 3;
            else s = 4;
            if (twistedOrSideBent) s++;
            return Clamp(s, 1, 5);
        }

        public static int ScoreNeck(float flexion, bool twistedOrSideBent, RebaThresholds t)
        {
            int s = (flexion > 20f || flexion < -t.neckExtensionTolerance) ? 2 : 1;
            if (twistedOrSideBent) s++;
            return Clamp(s, 1, 3);
        }

        public static int ScoreLegs(bool unilateralOrUnstable, float kneeFlexion, bool sitting)
        {
            int s = unilateralOrUnstable ? 2 : 1;
            if (!sitting)
            {
                if (kneeFlexion > 60f) s += 2;
                else if (kneeFlexion >= 30f) s += 1;
            }
            return Clamp(s, 1, 4);
        }

        public static int ScoreLoad(float kg, bool shock)
        {
            int s = kg < 5f ? 0 : (kg <= 10f ? 1 : 2);
            if (shock) s++;
            return Clamp(s, 0, 3);
        }

        public static int ScoreUpperArm(float flexion, bool abductedOrRotated, bool shoulderRaised, bool supported)
        {
            int s;
            if (flexion >= -20f && flexion <= 20f) s = 1;
            else if (flexion < -20f || flexion <= 45f) s = 2;
            else if (flexion <= 90f) s = 3;
            else s = 4;
            if (abductedOrRotated) s++;
            if (shoulderRaised) s++;
            if (supported) s--;
            return Clamp(s, 1, 6);
        }

        public static int ScoreLowerArm(float elbowFlexion)
        {
            return (elbowFlexion >= 60f && elbowFlexion <= 100f) ? 1 : 2;
        }

        public static int ScoreWrist(float flexion, bool deviatedOrTwisted)
        {
            int s = Math.Abs(flexion) <= 15f ? 1 : 2;
            if (deviatedOrTwisted) s++;
            return Clamp(s, 1, 3);
        }

        public static RebaRisk RiskOf(int finalScore)
        {
            if (finalScore <= 1) return RebaRisk.Negligible;
            if (finalScore <= 3) return RebaRisk.Low;
            if (finalScore <= 7) return RebaRisk.Medium;
            if (finalScore <= 10) return RebaRisk.High;
            return RebaRisk.VeryHigh;
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
