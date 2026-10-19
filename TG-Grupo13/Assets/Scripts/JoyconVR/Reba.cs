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
        /// <summary>Inclinación lateral (tronco, cuello) o desviación de muñeca por encima de esto suma +1.</summary>
        public float twistOrSideBendThreshold = 10f;
        /// <summary>
        /// Torsión (tronco respecto a la pelvis, cuello respecto al tronco) por encima de esto suma +1.
        /// Más alto que el de inclinación porque la torsión es lo que menos precisión tiene con
        /// IMU sin magnetómetro (se midieron ±5–9° de error tras mover las piernas).
        /// </summary>
        public float twistThreshold = 15f;
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
        public bool upperArmAbductedOrRotated; // lo que puntúa (+1): abducido O rotado
        public bool upperArmAbducted;       // solo informativo: cuál de los dos lo activó
        public bool upperArmRotated;
        public bool shoulderRaised;         // no medible con un Joy-Con en el brazo
        public bool armSupportedOrLeaning;
        public float elbowFlexion;          // grados, 0 = brazo extendido
        public float wristFlexion;          // grados, + flexión, − extensión
        public bool wristDeviatedOrTwisted;
        public int coupling;                // 0 bueno, 1 regular, 2 malo, 3 inaceptable

        // --- Actividad (cada sí suma +1, máximo 3) ---
        public bool staticPosture;          // una o más partes del cuerpo estáticas (> 1 min)
        public bool repetitiveMovements;    // movimientos repetitivos (> 4 por minuto, rango pequeño)
        public bool rapidChangesOrUnstable; // cambios posturales grandes y rápidos, o base inestable
        public int activity;                // puntos extra a mano (además de los tres de arriba)
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

        // Desglose como en la hoja REBA: puntos base de cada parte y cada ajuste (+1, +2, −1).
        // (Los totales de arriba son base + ajustes, limitados a su rango.)
        public int trunkBase, trunkAdjust;              // tronco: flexión/extensión; torcido o inclinado
        public int neckBase, neckAdjust;                // cuello: flexión/extensión; torcido o inclinado
        public int legsBase, kneeAdjust;                // piernas: soporte; flexión de rodillas
        public int loadBase, shockAdjust;               // carga; impacto o fuerza brusca
        public int upperArmBase, upperArmAbductedAdjust, shoulderRaisedAdjust, armSupportedAdjust;
        public int wristBase, wristAdjust;              // muñeca: flexión/extensión; desviada o torcida
        public int activityStatic, activityRepetitive, activityRapid;

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
                trunkBase = TrunkBase(input.trunkFlexion, t),
                trunkAdjust = input.trunkTwistedOrSideBent ? 1 : 0,
                neckBase = NeckBase(input.neckFlexion, t),
                neckAdjust = input.neckTwistedOrSideBent ? 1 : 0,
                legsBase = input.legsUnilateralOrUnstable ? 2 : 1,
                kneeAdjust = KneeAdjust(input.kneeFlexion, input.sitting),
                loadBase = LoadBase(input.loadKg),
                shockAdjust = input.shockOrRapidForce ? 1 : 0,
                upperArmBase = UpperArmBase(input.upperArmFlexion),
                upperArmAbductedAdjust = input.upperArmAbductedOrRotated ? 1 : 0,
                shoulderRaisedAdjust = input.shoulderRaised ? 1 : 0,
                armSupportedAdjust = input.armSupportedOrLeaning ? -1 : 0,
                lowerArm = ScoreLowerArm(input.elbowFlexion),
                wristBase = WristBase(input.wristFlexion),
                wristAdjust = input.wristDeviatedOrTwisted ? 1 : 0,
                coupling = Clamp(input.coupling, 0, 3),
                activityStatic = input.staticPosture ? 1 : 0,
                activityRepetitive = input.repetitiveMovements ? 1 : 0,
                activityRapid = input.rapidChangesOrUnstable ? 1 : 0,
            };
            r.trunk = Clamp(r.trunkBase + r.trunkAdjust, 1, 5);
            r.neck = Clamp(r.neckBase + r.neckAdjust, 1, 3);
            r.legs = Clamp(r.legsBase + r.kneeAdjust, 1, 4);
            r.load = Clamp(r.loadBase + r.shockAdjust, 0, 3);
            r.upperArm = Clamp(r.upperArmBase + r.upperArmAbductedAdjust + r.shoulderRaisedAdjust + r.armSupportedAdjust, 1, 6);
            r.wrist = Clamp(r.wristBase + r.wristAdjust, 1, 3);
            r.activity = Clamp(input.activity + r.activityStatic + r.activityRepetitive + r.activityRapid, 0, 3);

            r.tableA = TableA[r.trunk - 1, r.neck - 1, r.legs - 1];
            r.scoreA = Clamp(r.tableA + r.load, 1, 12);
            r.tableB = TableB[r.upperArm - 1, r.lowerArm - 1, r.wrist - 1];
            r.scoreB = Clamp(r.tableB + r.coupling, 1, 12);
            r.tableC = TableC[r.scoreA - 1, r.scoreB - 1];
            r.final = r.tableC + r.activity;
            r.risk = RiskOf(r.final);
            return r;
        }

        // --- Puntos base (sin ajustes) de cada parte ---

        public static int TrunkBase(float flexion, RebaThresholds t)
        {
            if (Math.Abs(flexion) <= t.trunkUprightTolerance) return 1;
            if (flexion < 0f) return flexion >= -20f ? 2 : 3;   // extensión
            if (flexion <= 20f) return 2;
            if (flexion <= 60f) return 3;
            return 4;
        }

        public static int NeckBase(float flexion, RebaThresholds t) =>
            (flexion > 20f || flexion < -t.neckExtensionTolerance) ? 2 : 1;

        public static int KneeAdjust(float kneeFlexion, bool sitting)
        {
            if (sitting) return 0;
            if (kneeFlexion > 60f) return 2;
            return kneeFlexion >= 30f ? 1 : 0;
        }

        public static int LoadBase(float kg) => kg < 5f ? 0 : (kg <= 10f ? 1 : 2);

        public static int UpperArmBase(float flexion)
        {
            if (flexion >= -20f && flexion <= 20f) return 1;
            if (flexion < -20f || flexion <= 45f) return 2;
            if (flexion <= 90f) return 3;
            return 4;
        }

        public static int WristBase(float flexion) => Math.Abs(flexion) <= 15f ? 1 : 2;

        // --- Puntaje de cada parte (base + ajustes) ---

        public static int ScoreTrunk(float flexion, bool twistedOrSideBent, RebaThresholds t) =>
            Clamp(TrunkBase(flexion, t) + (twistedOrSideBent ? 1 : 0), 1, 5);

        public static int ScoreNeck(float flexion, bool twistedOrSideBent, RebaThresholds t) =>
            Clamp(NeckBase(flexion, t) + (twistedOrSideBent ? 1 : 0), 1, 3);

        public static int ScoreLegs(bool unilateralOrUnstable, float kneeFlexion, bool sitting) =>
            Clamp((unilateralOrUnstable ? 2 : 1) + KneeAdjust(kneeFlexion, sitting), 1, 4);

        public static int ScoreLoad(float kg, bool shock) => Clamp(LoadBase(kg) + (shock ? 1 : 0), 0, 3);

        public static int ScoreUpperArm(float flexion, bool abductedOrRotated, bool shoulderRaised, bool supported) =>
            Clamp(UpperArmBase(flexion) + (abductedOrRotated ? 1 : 0) + (shoulderRaised ? 1 : 0) - (supported ? 1 : 0), 1, 6);

        public static int ScoreLowerArm(float elbowFlexion)
        {
            return (elbowFlexion >= 60f && elbowFlexion <= 100f) ? 1 : 2;
        }

        public static int ScoreWrist(float flexion, bool deviatedOrTwisted) =>
            Clamp(WristBase(flexion) + (deviatedOrTwisted ? 1 : 0), 1, 3);

        public static RebaRisk RiskOf(int finalScore)
        {
            if (finalScore <= 1) return RebaRisk.Negligible;
            if (finalScore <= 3) return RebaRisk.Low;
            if (finalScore <= 7) return RebaRisk.Medium;
            if (finalScore <= 10) return RebaRisk.High;
            return RebaRisk.VeryHigh;
        }

        /// <summary>Nivel de riesgo como en la hoja REBA en español.</summary>
        public static string RiskName(RebaRisk risk)
        {
            switch (risk)
            {
                case RebaRisk.Negligible: return "Insignificante";
                case RebaRisk.Low: return "Bajo";
                case RebaRisk.Medium: return "Medio";
                case RebaRisk.High: return "Alto";
                default: return "Muy alto";
            }
        }

        /// <summary>Actuación recomendada por REBA para el nivel de riesgo.</summary>
        public static string RiskAction(RebaRisk risk)
        {
            switch (risk)
            {
                case RebaRisk.Negligible: return "No es necesaria";
                case RebaRisk.Low: return "Puede ser necesaria";
                case RebaRisk.Medium: return "Es necesaria";
                case RebaRisk.High: return "Es necesaria pronto";
                default: return "Es necesaria de inmediato";
            }
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
