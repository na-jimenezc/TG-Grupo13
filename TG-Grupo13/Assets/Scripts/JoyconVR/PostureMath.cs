// PostureMath.cs
//
// Matemática para pasar de "un cuaternión por Joy-Con" a ángulos articulares
// útiles para REBA. Ver README.md para la explicación completa.
//
// Convención del marco del cuerpo C (marco "postura neutra", fijado en la calibración):
//   X = derecha de la persona, Y = arriba, Z = adelante   (ejes de Unity, zurdo)
//
// Todas las funciones usan solo campos/aritmética de Quaternion/Vector3 (nada
// de Quaternion.Euler / eulerAngles), para poder controlar el orden de Euler.

using System;
using UnityEngine;

namespace PujJoycon
{
    public enum EulerOrder
    {
        // Orden intrínseco: XYZ significa R = Rx(a) * Ry(b) * Rz(c)
        // (primero se rota sobre X, luego sobre la Y ya rotada, luego sobre la Z ya rotada).
        XYZ, XZY, YXZ, YZX, ZXY, ZYX,
    }

    public static class PostureMath
    {
        // ------------------------------------------------------------------
        // 1) Del servidor a Unity
        // ------------------------------------------------------------------

        /// <summary>
        /// Convierte el cuaternión del servidor (marco diestro, Z arriba; ver
        /// common/ahrs_filter.h) a Unity (zurdo, Y arriba) intercambiando Y<->Z.
        /// Un cambio de mano es una reflexión, y para un cuaternión eso equivale a
        /// permutar el vector y negarlo: (x, y, z, w) -> (-x, -z, -y, w).
        ///
        /// VERIFICAR con JoyconDebugDriver: si un eje gira al revés, corregirlo
        /// aquí (o con el "mounting" del segmento) y no en cada script.
        /// </summary>
        public static Quaternion ToUnity(RawQuaternion q)
        {
            return new Quaternion(-q.X, -q.Z, -q.Y, q.W);
        }

        // ------------------------------------------------------------------
        // 2) Calibración sensor -> segmento (postura N)
        // ------------------------------------------------------------------

        /// <summary>
        /// Orientación del segmento corporal en el marco del cuerpo C.
        ///
        ///   seg(t) = M * inv(q0) * q(t) * inv(M)
        ///
        /// q(t)  : orientación actual del sensor (ya en ejes de Unity)
        /// q0    : orientación del sensor capturada en postura N (todos los segmentos en neutro)
        /// M     : "mounting" = cómo están los ejes del sensor expresados en ejes del segmento
        ///         (identidad si el Joy-Con se pegó con sus ejes alineados al segmento).
        ///
        /// Ventaja clave: el marco "mundo" de cada Joy-Con (que es distinto para cada
        /// uno porque cada filtro arranca en identidad y no hay magnetómetro) se
        /// cancela. Solo hace falta que la persona esté en postura N al calibrar y
        /// que la deriva desde entonces sea pequeña.
        /// </summary>
        public static Quaternion SegmentOrientation(Quaternion q, Quaternion q0, Quaternion mounting)
        {
            return Normalize(mounting * Inverse(q0) * q * Inverse(mounting));
        }

        /// <summary>
        /// Rotación del hijo relativa al padre, expresada en el marco del padre
        /// (ej.: brazo respecto al tronco, antebrazo respecto al brazo).
        /// Esto es lo que "depende de otros ángulos": la articulación es la diferencia
        /// entre dos segmentos, no la orientación absoluta de uno.
        /// </summary>
        public static Quaternion Relative(Quaternion parent, Quaternion child)
        {
            return Normalize(Inverse(parent) * child);
        }

        // ------------------------------------------------------------------
        // 3) Euler en cualquier orden
        // ------------------------------------------------------------------

        /// <summary>
        /// Ángulos de Euler (grados, en (-180, 180]) para el orden intrínseco dado.
        /// El resultado siempre se devuelve como (ángulo sobre X, sobre Y, sobre Z),
        /// sin importar el orden. Ojo: Unity's transform.eulerAngles usa ZXY y 0..360,
        /// por eso no sirve directamente para ángulos anatómicos.
        ///
        /// El segundo ángulo del orden queda limitado a [-90, 90]; cerca de ±90 hay
        /// gimbal lock y el primero/tercero dejan de ser únicos (se fija el tercero en 0).
        /// </summary>
        public static Vector3 ToEuler(Quaternion q, EulerOrder order)
        {
            GetAxes(order, out int i, out int j, out int k);
            // Paridad: +1 si (i,j,k) es permutación cíclica de (0,1,2), -1 si no.
            float s = ((j - i + 3) % 3 == 1) ? 1f : -1f;

            float[,] r = ToMatrix(q);
            float sinB = Mathf.Clamp(s * r[i, k], -1f, 1f);
            float a, b, c;
            b = Mathf.Asin(sinB);
            if (Mathf.Abs(sinB) < 0.9999f)
            {
                a = Mathf.Atan2(-s * r[j, k], r[k, k]);
                c = Mathf.Atan2(-s * r[i, j], r[i, i]);
            }
            else
            {
                a = Mathf.Atan2(s * r[k, j], r[j, j]);
                c = 0f;
            }

            var result = new float[3];
            result[i] = a * Mathf.Rad2Deg;
            result[j] = b * Mathf.Rad2Deg;
            result[k] = c * Mathf.Rad2Deg;
            return new Vector3(result[0], result[1], result[2]);
        }

        /// <summary>Inversa de ToEuler (útil para pruebas y para construir posturas).</summary>
        public static Quaternion FromEuler(Vector3 degrees, EulerOrder order)
        {
            GetAxes(order, out int i, out int j, out int k);
            float[] ang = { degrees.x, degrees.y, degrees.z };
            return Normalize(AxisRotation(i, ang[i]) * AxisRotation(j, ang[j]) * AxisRotation(k, ang[k]));
        }

        // ------------------------------------------------------------------
        // 4) Swing-twist: separar la rotación "alrededor del hueso" del resto
        // ------------------------------------------------------------------

        /// <summary>
        /// Descompone q = swing * twist, donde twist es la rotación alrededor de
        /// <paramref name="axis"/> (en el marco local del segmento). Sirve para
        /// torsión de tronco, rotación del cuello, rotación del húmero, etc.
        /// Sin gimbal lock, a diferencia de Euler.
        /// </summary>
        public static void SwingTwist(Quaternion q, Vector3 axis, out Quaternion swing, out Quaternion twist)
        {
            axis = axis.normalized;
            var v = new Vector3(q.x, q.y, q.z);
            Vector3 p = Vector3.Dot(v, axis) * axis;
            twist = new Quaternion(p.x, p.y, p.z, q.w);
            float n = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (n < 1e-6f)
            {
                // Rotación de 180° perpendicular al eje: el twist es indefinido -> identidad.
                twist = Quaternion.identity;
            }
            else
            {
                twist = new Quaternion(twist.x / n, twist.y / n, twist.z / n, twist.w / n);
            }
            swing = Normalize(q * Inverse(twist));
        }

        /// <summary>Ángulo con signo (grados, -180..180) del twist de q alrededor de axis.</summary>
        public static float TwistAngle(Quaternion q, Vector3 axis)
        {
            SwingTwist(q, axis, out _, out var twist);
            float angle = 2f * Mathf.Atan2(new Vector3(twist.x, twist.y, twist.z).magnitude, twist.w) * Mathf.Rad2Deg;
            if (Vector3.Dot(new Vector3(twist.x, twist.y, twist.z), axis) < 0f) angle = -angle;
            return WrapDegrees(angle);
        }

        // ------------------------------------------------------------------
        // 5) Ángulos "anatómicos" sin Euler
        // ------------------------------------------------------------------

        /// <summary>
        /// Inclinación de un segmento respecto a la vertical de su marco padre,
        /// separada en flexión (+ adelante / − atrás) y lateral (+ a la derecha),
        /// en grados. Independiente de hacia dónde esté mirando la persona: la
        /// dirección "adelante" se toma del propio segmento (su eje X horizontal),
        /// así que girar todo el cuerpo no se confunde con inclinarse de lado.
        ///
        /// <paramref name="segment"/> debe tener Y = eje largo hacia arriba en neutro
        /// (tronco, cuello/cabeza).
        /// </summary>
        public static void TiltAngles(Quaternion segment, out float flexion, out float lateral)
        {
            Vector3 up = segment * Vector3.up;
            Vector3 right = segment * Vector3.right;
            Vector3 rightH = new Vector3(right.x, 0f, right.z);
            if (rightH.sqrMagnitude < 1e-8f)
            {
                // Eje lateral vertical (inclinación lateral de ~90°): caso extremo.
                flexion = 0f;
                lateral = Mathf.Asin(Mathf.Clamp(up.x, -1f, 1f)) * Mathf.Rad2Deg;
                return;
            }
            rightH.Normalize();
            Vector3 forwardH = Vector3.Cross(rightH, Vector3.up); // (1,0,0) x (0,1,0) = (0,0,1)
            flexion = Mathf.Atan2(Vector3.Dot(up, forwardH), up.y) * Mathf.Rad2Deg;
            lateral = Mathf.Asin(Mathf.Clamp(Vector3.Dot(up, rightH), -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Elevación del brazo respecto al tronco. <paramref name="upperArmInTrunk"/> =
        /// Relative(tronco, brazo). En postura N el brazo cuelga: su eje largo es −Y.
        ///   flexion   : en el plano sagital, + adelante, − atrás (extensión)
        ///   abduction : en el plano frontal, + hacia afuera del cuerpo
        ///   elevation : ángulo total entre el brazo y "colgando" (0..180), sin signo
        /// </summary>
        public static void UpperArmAngles(Quaternion upperArmInTrunk, bool isLeft,
                                          out float flexion, out float abduction, out float elevation)
        {
            Vector3 d = upperArmInTrunk * Vector3.down;
            float outward = isLeft ? -d.x : d.x;
            flexion = Mathf.Atan2(d.z, -d.y) * Mathf.Rad2Deg;
            abduction = Mathf.Atan2(outward, -d.y) * Mathf.Rad2Deg;
            elevation = Vector3.Angle(d, Vector3.down);
        }

        /// <summary>
        /// Ángulo entre los ejes largos de dos segmentos consecutivos de una
        /// extremidad (0 = extendido). Codo: (brazo, antebrazo). Rodilla: (muslo, pierna).
        /// No depende del orden de Euler ni de la deriva en yaw de cada sensor.
        /// </summary>
        public static float HingeFlexion(Quaternion proximal, Quaternion distal)
        {
            return Vector3.Angle(proximal * Vector3.down, distal * Vector3.down);
        }

        /// <summary>
        /// Muñeca. <paramref name="handInForearm"/> = Relative(antebrazo, mano).
        /// En postura N (palmas hacia los muslos) la palma derecha mira a −X y la
        /// izquierda a +X; el pulgar apunta a +Z.
        ///   flexion   : + hacia la palma, − extensión
        ///   deviation : + radial (hacia el pulgar), − cubital
        /// </summary>
        public static void WristAngles(Quaternion handInForearm, bool isLeft, out float flexion, out float deviation)
        {
            Vector3 h = handInForearm * Vector3.down;
            float towardPalm = isLeft ? h.x : -h.x;
            flexion = Mathf.Atan2(towardPalm, -h.y) * Mathf.Rad2Deg;
            deviation = Mathf.Atan2(h.z, -h.y) * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------------
        // 6) Llevar los segmentos al avatar (JoyconAvatarDriver)
        // ------------------------------------------------------------------

        /// <summary>
        /// Solo el giro alrededor de la vertical (Y) de q, sin inclinación. Se toma
        /// del eje X (lateral) proyectado al plano horizontal, que no se mueve al
        /// inclinarse hacia adelante, así que agacharse no cambia el rumbo.
        /// </summary>
        public static Quaternion Heading(Quaternion q)
        {
            Vector3 right = q * Vector3.right;
            float x = right.x, z = right.z;
            if (x * x + z * z < 1e-8f)
            {
                // Eje lateral vertical (inclinación lateral de ~90°): usar el eje adelante.
                Vector3 fwd = q * Vector3.forward;
                return AxisRotation(1, Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg);
            }
            // Ry(ψ) lleva (1,0,0) a (cos ψ, 0, −sin ψ).
            return AxisRotation(1, Mathf.Atan2(-z, x) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Rumbo de q en grados (−180..180]: cuánto está girado alrededor de la vertical.
        /// A diferencia de los Euler XYZ, no "salta" cerca de 90° (sirve para comparar giros).
        /// </summary>
        public static float HeadingDegrees(Quaternion q)
        {
            Quaternion h = Heading(q);
            float angle = 2f * Mathf.Atan2(h.y, h.w) * Mathf.Rad2Deg; // h es un giro puro alrededor de Y
            return WrapDegrees(angle);
        }

        /// <summary>
        /// Rotación de mundo que debe tener un hueso del avatar para reflejar un segmento.
        ///
        ///   hueso(t) = cuerpoAhora · inv(rumboRef) · seg(t) · inv(cuerpo0) · hueso0
        ///
        /// seg       : orientación del segmento en el marco del cuerpo C (SegmentOrientation)
        /// rumboRef  : Heading() del segmento de referencia (pelvis/tronco) en C. Se le quita
        ///             a todos los segmentos para que girar todo el cuerpo o la deriva común
        ///             en yaw de los Joy-Cons no gire al avatar; el rumbo lo pone cuerpoAhora.
        /// cuerpo0   : marco del cuerpo C en el mundo de Unity al calibrar (hacia dónde miraba).
        /// cuerpoAhora: cuerpo0 girado lo mismo que giró el avatar según el seguimiento de Meta.
        /// hueso0    : rotación de mundo del hueso al calibrar (avatar en postura N).
        ///
        /// Al calibrar (seg = rumboRef = identidad, cuerpoAhora = cuerpo0) da hueso0.
        /// </summary>
        public static Quaternion BoneWorldRotation(Quaternion segment, Quaternion referenceHeading,
                                                   Quaternion bodyNow, Quaternion body0, Quaternion bone0)
        {
            return Normalize(bodyNow * Inverse(referenceHeading) * segment * Inverse(body0) * bone0);
        }

        // ------------------------------------------------------------------
        // 7) Calibración automática del montaje (postura N + sentadilla)
        // ------------------------------------------------------------------

        /// <summary>
        /// Calcula el "mounting" de un Joy-Con (cómo está puesto en el segmento) a partir de:
        ///   - la gravedad en postura N: ahí el eje largo del segmento (Y del cuerpo) es vertical;
        ///   - un movimiento conocido alrededor del eje lateral del cuerpo (X), por ejemplo la
        ///     sentadilla con brazos al frente, con el signo esperado para ese segmento
        ///     (+1 si su eje Y se va hacia adelante: tronco, pelvis, pierna; −1 si su eje −Y se va
        ///     hacia adelante: brazo, muslo).
        ///
        /// q0          : orientación del Joy-Con en postura N (ejes de Unity, ToUnity)
        /// qMoved      : orientación en la sentadilla
        /// gravityUpY  : dónde queda "arriba" en el mundo del filtro del servidor tras ToUnity: (0, gravityUpY, 0)
        /// minAngle    : giro mínimo (grados) para confiar en el eje
        ///
        /// Devuelve false (y un motivo) si el segmento casi no se movió o giró alrededor de la vertical.
        /// </summary>
        public static bool MountingFromMovement(Quaternion q0, Quaternion qMoved, float expectedSign, float gravityUpY,
                                                float minAngle, out Quaternion mounting, out string problem)
        {
            mounting = Quaternion.identity;
            problem = null;

            // Giro desde la postura N, en los ejes del propio Joy-Con.
            ToAngleAxis(Inverse(q0) * qMoved, out float angle, out Vector3 axis);
            if (angle < minAngle)
            {
                problem = $"se movió muy poco ({angle:F0}°, mínimo {minAngle:F0}°)";
                return false;
            }

            // Eje Y del cuerpo (vertical) y eje X (lateral) expresados en los ejes del Joy-Con.
            Vector3 up = (Inverse(q0) * new Vector3(0f, gravityUpY, 0f)).normalized;
            Vector3 lateral = axis * Mathf.Sign(expectedSign);
            lateral -= Vector3.Dot(lateral, up) * up;
            if (lateral.magnitude < 0.5f)
            {
                problem = "giró alrededor de la vertical en vez de inclinarse hacia adelante";
                return false;
            }
            lateral.Normalize();
            Vector3 forward = Vector3.Cross(lateral, up); // (1,0,0) x (0,1,0) = (0,0,1)

            // bodyInSensor lleva los ejes del cuerpo a sus coordenadas en el Joy-Con;
            // el mounting es lo contrario (de ejes del Joy-Con a ejes del segmento).
            mounting = Inverse(FromBasis(lateral, up, forward));
            return true;
        }

        /// <summary>
        /// Promedio de varias orientaciones parecidas (suavizado de la tembladera).
        /// q y −q son la misma rotación, así que cada una se alinea primero con
        /// <paramref name="reference"/> (la más reciente) antes de sumar.
        /// </summary>
        public static Quaternion Average(Quaternion[] samples, int count, Quaternion reference)
        {
            if (count <= 0) return reference;
            float x = 0f, y = 0f, z = 0f, w = 0f;
            for (int i = 0; i < count; i++)
            {
                Quaternion q = samples[i];
                float sign = (q.x * reference.x + q.y * reference.y + q.z * reference.z + q.w * reference.w) < 0f ? -1f : 1f;
                x += sign * q.x; y += sign * q.y; z += sign * q.z; w += sign * q.w;
            }
            return Normalize(new Quaternion(x, y, z, w));
        }

        /// <summary>Ángulo (0..180°) y eje unitario de la rotación q (el camino más corto).</summary>
        public static void ToAngleAxis(Quaternion q, out float angleDegrees, out Vector3 axis)
        {
            q = Normalize(q);
            if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            angleDegrees = 2f * Mathf.Acos(Mathf.Clamp(q.w, -1f, 1f)) * Mathf.Rad2Deg;
            float s = Mathf.Sqrt(Mathf.Max(0f, 1f - q.w * q.w));
            axis = s < 1e-6f ? Vector3.right : new Vector3(q.x / s, q.y / s, q.z / s);
        }

        /// <summary>Rotación que lleva los ejes X, Y, Z a x, y, z (base ortonormal derecha).</summary>
        public static Quaternion FromBasis(Vector3 x, Vector3 y, Vector3 z)
        {
            // Matriz con columnas x, y, z -> cuaternión.
            float m00 = x.x, m10 = x.y, m20 = x.z;
            float m01 = y.x, m11 = y.y, m21 = y.z;
            float m02 = z.x, m12 = z.y, m22 = z.z;
            float trace = m00 + m11 + m22;
            float qw, qx, qy, qz;
            if (trace > 0f)
            {
                float s = Mathf.Sqrt(trace + 1f) * 2f;
                qw = 0.25f * s; qx = (m21 - m12) / s; qy = (m02 - m20) / s; qz = (m10 - m01) / s;
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f;
                qw = (m21 - m12) / s; qx = 0.25f * s; qy = (m01 + m10) / s; qz = (m02 + m20) / s;
            }
            else if (m11 > m22)
            {
                float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f;
                qw = (m02 - m20) / s; qx = (m01 + m10) / s; qy = 0.25f * s; qz = (m12 + m21) / s;
            }
            else
            {
                float s = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f;
                qw = (m10 - m01) / s; qx = (m02 + m20) / s; qy = (m12 + m21) / s; qz = 0.25f * s;
            }
            return Normalize(new Quaternion(qx, qy, qz, qw));
        }

        // ------------------------------------------------------------------
        // Utilidades (todo administrado, sin llamadas nativas de Unity)
        // ------------------------------------------------------------------

        public static Quaternion Inverse(Quaternion q)
        {
            // Para cuaterniones unitarios la inversa es el conjugado.
            return new Quaternion(-q.x, -q.y, -q.z, q.w);
        }

        public static Quaternion Normalize(Quaternion q)
        {
            float n = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (n < 1e-8f) return Quaternion.identity;
            return new Quaternion(q.x / n, q.y / n, q.z / n, q.w / n);
        }

        public static Quaternion AxisRotation(int axis, float degrees)
        {
            float half = degrees * Mathf.Deg2Rad * 0.5f;
            float s = Mathf.Sin(half), c = Mathf.Cos(half);
            switch (axis)
            {
                case 0: return new Quaternion(s, 0f, 0f, c);
                case 1: return new Quaternion(0f, s, 0f, c);
                default: return new Quaternion(0f, 0f, s, c);
            }
        }

        public static float WrapDegrees(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            if (degrees <= -180f) degrees += 360f;
            return degrees;
        }

        private static float[,] ToMatrix(Quaternion q)
        {
            q = Normalize(q);
            float x = q.x, y = q.y, z = q.z, w = q.w;
            return new float[3, 3]
            {
                { 1f - 2f * (y * y + z * z), 2f * (x * y - z * w),      2f * (x * z + y * w) },
                { 2f * (x * y + z * w),      1f - 2f * (x * x + z * z), 2f * (y * z - x * w) },
                { 2f * (x * z - y * w),      2f * (y * z + x * w),      1f - 2f * (x * x + y * y) },
            };
        }

        private static void GetAxes(EulerOrder order, out int i, out int j, out int k)
        {
            switch (order)
            {
                case EulerOrder.XYZ: i = 0; j = 1; k = 2; return;
                case EulerOrder.XZY: i = 0; j = 2; k = 1; return;
                case EulerOrder.YXZ: i = 1; j = 0; k = 2; return;
                case EulerOrder.YZX: i = 1; j = 2; k = 0; return;
                case EulerOrder.ZXY: i = 2; j = 0; k = 1; return;
                case EulerOrder.ZYX: i = 2; j = 1; k = 0; return;
                default: throw new ArgumentOutOfRangeException(nameof(order));
            }
        }
    }
}
