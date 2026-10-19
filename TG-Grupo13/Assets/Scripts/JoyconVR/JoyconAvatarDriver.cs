// JoyconAvatarDriver.cs
//
// Mueve el avatar (X Bot, Humanoid) con los Joy-Cons, ENCIMA del seguimiento
// corporal de Meta (MetaSourceDataProvider + CharacterRetargeter):
//
//   - Meta sigue mandando: posición del avatar, rumbo (hacia dónde mira el cuerpo),
//     cabeza (visor) y manos (controles). Esas partes están bien rastreadas y no derivan.
//   - Los Joy-Cons mandan en los huesos que tengan sensor: pelvis, tronco (repartido
//     entre las 3 vértebras), brazos, antebrazos, muslos y piernas.
//   - Si un brazo tiene Joy-Con y su antebrazo no (distribución estándar), se dobla
//     el codo (IK de dos huesos) para que la mano quede en el control; el Joy-Con
//     del brazo decide hacia qué lado sale el codo.
//   - Pies en el piso: sube o baja el avatar para que el pie más bajo toque el piso.
//
// Funciona porque CharacterRetargeter escribe los huesos en su LateUpdate y este
// componente corre DESPUÉS (DefaultExecutionOrder alto) y los sobrescribe. No se
// modifica nada del paquete de Meta.
//
// La calibración es la misma de RebaPostureEstimator (evento Calibrated): en ese
// momento se toma la postura del avatar como "postura N" de cada hueso.

using UnityEngine;

namespace PujJoycon
{
    [DefaultExecutionOrder(10000)]
    public class JoyconAvatarDriver : MonoBehaviour
    {
        [SerializeField] private RebaPostureEstimator estimator;
        [Tooltip("Animator Humanoid del avatar (el X Bot con CharacterRetargeter).")]
        [SerializeField] private Animator avatar;
        [Tooltip("Cámara del visor (CenterEyeAnchor): define hacia dónde mira la persona al calibrar.")]
        [SerializeField] private Transform headReference;

        [Header("Mezcla Meta / Joy-Cons")]
        [Tooltip("0 = solo seguimiento de Meta, 1 = solo Joy-Cons, en los huesos que tienen sensor.")]
        [SerializeField, Range(0f, 1f)] private float weight = 1f;
        [Tooltip("El rumbo del cuerpo lo da Meta (visor). Si se apaga, el rumbo viene de los Joy-Cons y deriva.")]
        [SerializeField] private bool headingFromTracking = true;
        [Tooltip("Mantener la cabeza orientada como el visor aunque el tronco se mueva con el Joy-Con.")]
        [SerializeField] private bool keepHeadFromTracking = true;
        [Tooltip("Mantener las manos orientadas como los controles aunque los antebrazos se muevan con Joy-Cons.")]
        [SerializeField] private bool keepHandsFromTracking = true;
        [Tooltip("Si el brazo tiene Joy-Con y el antebrazo no, doblar el codo para que la mano llegue a donde " +
                 "Meta puso la mano (el control). El Joy-Con del brazo decide hacia dónde apunta el codo. " +
                 "Apagar en pruebas sin visor: ahí nadie mueve las manos.")]
        [SerializeField] private bool aimForearmsAtTrackedHands = true;
        [Tooltip("Mover pelvis y tronco con sus Joy-Cons. Apagar para descartar que el giro raro venga de ahí.")]
        [SerializeField] private bool driveTorso = true;

        [Header("Pies en el piso")]
        [Tooltip("Sube o baja todo el avatar para que el pie más bajo quede en el piso (la altura de los pies al " +
                 "calibrar). Sin esto, al doblar las rodillas con los Joy-Cons el avatar queda flotando o se hunde, " +
                 "porque la cadera la pone Meta (o queda fija en las pruebas sin visor).")]
        [SerializeField] private bool groundFeet = true;
        [Tooltip("Máximo que se puede mover el avatar hacia arriba o abajo (metros).")]
        [SerializeField, Range(0f, 1f)] private float maxGroundCorrection = 0.25f;
        [Tooltip("Qué tan rápido sigue al piso (1/s). Más alto = más pegado pero puede temblar.")]
        [SerializeField, Range(1f, 30f)] private float groundSmoothing = 12f;

        // Hueso del avatar que mueve cada segmento con Joy-Con.
        private static readonly (BodySegment segment, HumanBodyBones bone)[] Limbs =
        {
            (BodySegment.UpperArmLeft, HumanBodyBones.LeftUpperArm),
            (BodySegment.ForearmLeft, HumanBodyBones.LeftLowerArm),
            (BodySegment.UpperArmRight, HumanBodyBones.RightUpperArm),
            (BodySegment.ForearmRight, HumanBodyBones.RightLowerArm),
            (BodySegment.ThighLeft, HumanBodyBones.LeftUpperLeg),
            (BodySegment.ShankLeft, HumanBodyBones.LeftLowerLeg),
            (BodySegment.ThighRight, HumanBodyBones.RightUpperLeg),
            (BodySegment.ShankRight, HumanBodyBones.RightLowerLeg),
        };

        private Transform _hips, _head, _leftHand, _rightHand, _leftFoot, _rightFoot;

        // Pies en el piso: altura de los pies al calibrar y desplazamiento aplicado a la cadera.
        private float _floorFootY;
        private float _groundOffset;
        private Vector3 _lastHipsPosition;
        private bool _groundApplied;

        // Rotación de la cadera que dejamos al final del frame anterior, y la última que puso Meta. Si al empezar el
        // frame la cadera sigue como la dejamos, nadie la reescribió: usar la nuestra como "lo que dice Meta" haría
        // que el rumbo se realimente y el cuerpo gire sin parar.
        private Quaternion _hipsWritten, _hipsFromTracking;
        private bool _hipsWrittenValid, _hipsFromTrackingValid;
        private Transform[] _spine;          // Spine, Chest, UpperChest (los que existan)
        private Transform[] _limbBones;

        private bool _captureRequested;
        private bool _calibrated;
        private Quaternion _body0;           // marco del cuerpo C en el mundo al calibrar
        private Quaternion _hipsTracked0;    // cadera según Meta al calibrar
        private Quaternion _hipsBone0, _spineTop0;
        private Quaternion[] _limbBone0;

        public bool IsCalibrated => _calibrated;

        public float Weight
        {
            get => weight;
            set => weight = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            if (estimator == null) estimator = FindAnyObjectByType<RebaPostureEstimator>();
            CacheBones();
        }

        private void OnEnable()
        {
            if (estimator != null) estimator.Calibrated += OnEstimatorCalibrated;
        }

        private void OnDisable()
        {
            if (estimator != null) estimator.Calibrated -= OnEstimatorCalibrated;
        }

        private void OnEstimatorCalibrated()
        {
            // La postura se toma en LateUpdate, justo después de que Meta aplicó la suya
            // y ANTES de sobrescribirla (si se tomara aquí, en una recalibración se
            // copiaría la postura de Joy-Cons del frame anterior, con su deriva).
            _captureRequested = true;
        }

        private void CacheBones()
        {
            if (avatar == null || !avatar.isHuman)
            {
                Debug.LogError("[Joycon] JoyconAvatarDriver necesita el Animator Humanoid del avatar.", this);
                enabled = false;
                return;
            }

            _hips = avatar.GetBoneTransform(HumanBodyBones.Hips);
            _head = avatar.GetBoneTransform(HumanBodyBones.Head);
            _leftHand = avatar.GetBoneTransform(HumanBodyBones.LeftHand);
            _rightHand = avatar.GetBoneTransform(HumanBodyBones.RightHand);
            _leftFoot = avatar.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = avatar.GetBoneTransform(HumanBodyBones.RightFoot);

            var spine = new System.Collections.Generic.List<Transform>();
            foreach (var b in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest })
            {
                var t = avatar.GetBoneTransform(b);
                if (t != null) spine.Add(t);
            }
            _spine = spine.ToArray();

            _limbBones = new Transform[Limbs.Length];
            _limbBone0 = new Quaternion[Limbs.Length];
            for (int i = 0; i < Limbs.Length; i++) _limbBones[i] = avatar.GetBoneTransform(Limbs[i].bone);
        }

        private void Capture()
        {
            Vector3 fwd = headReference != null ? headReference.forward : avatar.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            _body0 = Quaternion.LookRotation(fwd.normalized, Vector3.up);

            // La persona está de pie en postura N: el pie más bajo marca el piso. Si había un desplazamiento
            // aplicado (recalibración en las pruebas sin visor), se mide sin él.
            float previousOffset = _groundApplied && _hips.position == _lastHipsPosition ? _groundOffset : 0f;
            _floorFootY = LowestFootY() - previousOffset;
            _groundOffset = previousOffset;

            _hipsTracked0 = TrackedHipsRotation();
            _hipsBone0 = _hipsTracked0;
            if (_spine.Length > 0) _spineTop0 = _spine[_spine.Length - 1].rotation;
            for (int i = 0; i < Limbs.Length; i++)
            {
                if (_limbBones[i] != null) _limbBone0[i] = _limbBones[i].rotation;
            }
            _calibrated = true;
            Debug.Log("[Joycon] Avatar calibrado: postura actual = postura N.", this);
        }

        private void LateUpdate()
        {
            if (estimator == null || _hips == null) return;

            if (_captureRequested)
            {
                _captureRequested = false;
                Capture();
            }
            if (!_calibrated || !estimator.IsCalibrated || weight <= 0f)
            {
                _hipsWrittenValid = false;
                return;
            }

            // Lo que Meta puso este frame, antes de tocar nada.
            Quaternion hipsTracked = TrackedHipsRotation();
            Quaternion headTracked = _head != null ? _head.rotation : Quaternion.identity;
            Quaternion leftHandTracked = _leftHand != null ? _leftHand.rotation : Quaternion.identity;
            Quaternion rightHandTracked = _rightHand != null ? _rightHand.rotation : Quaternion.identity;
            Vector3 leftHandTrackedPos = _leftHand != null ? _leftHand.position : Vector3.zero;
            Vector3 rightHandTrackedPos = _rightHand != null ? _rightHand.position : Vector3.zero;

            // Rumbo: lo que ha girado el cuerpo según Meta, y lo que ha girado la referencia
            // (pelvis o tronco) según los Joy-Cons, que se descuenta de todos los segmentos.
            Quaternion bodyNow = _body0;
            Quaternion referenceHeading = Quaternion.identity;
            if (headingFromTracking)
            {
                bodyNow = PostureMath.Heading(hipsTracked * PostureMath.Inverse(_hipsTracked0)) * _body0;
                if (estimator.TryGetSegment(BodySegment.Pelvis, out var refSeg) ||
                    estimator.TryGetSegment(BodySegment.Trunk, out refSeg))
                {
                    referenceHeading = PostureMath.Heading(refSeg);
                }
            }

            // Padres antes que hijos: al fijar la rotación de mundo de un hueso se mueven sus hijos.
            if (driveTorso && Drives(BodySegment.Pelvis, out var pelvis))
            {
                Apply(_hips, PostureMath.BoneWorldRotation(pelvis, referenceHeading, bodyNow, _body0, _hipsBone0));
            }

            if (driveTorso && _spine.Length > 0 && Drives(BodySegment.Trunk, out var trunk))
            {
                // El esternón mueve la vértebra superior; la corrección se reparte entre
                // las vértebras para que la columna se curve en vez de doblarse en un punto.
                Quaternion target = PostureMath.BoneWorldRotation(trunk, referenceHeading, bodyNow, _body0, _spineTop0);
                Transform top = _spine[_spine.Length - 1];
                Quaternion fullCorrection = target * PostureMath.Inverse(top.rotation);
                Quaternion step = Quaternion.Slerp(Quaternion.identity, fullCorrection, weight / _spine.Length);
                foreach (var vertebra in _spine) vertebra.rotation = step * vertebra.rotation;
            }

            for (int i = 0; i < Limbs.Length; i++)
            {
                if (_limbBones[i] == null || !Drives(Limbs[i].segment, out var seg)) continue;
                Apply(_limbBones[i], PostureMath.BoneWorldRotation(seg, referenceHeading, bodyNow, _body0, _limbBone0[i]));
            }

            // Antes de apuntar los antebrazos: mover la cadera mueve los codos.
            if (groundFeet) GroundFeet();
            else _groundApplied = false;

            if (aimForearmsAtTrackedHands)
            {
                ReachHand(BodySegment.UpperArmLeft, BodySegment.ForearmLeft, HumanBodyBones.LeftUpperArm,
                          HumanBodyBones.LeftLowerArm, _leftHand, leftHandTrackedPos);
                ReachHand(BodySegment.UpperArmRight, BodySegment.ForearmRight, HumanBodyBones.RightUpperArm,
                          HumanBodyBones.RightLowerArm, _rightHand, rightHandTrackedPos);
            }

            if (keepHeadFromTracking && _head != null) _head.rotation = headTracked;
            if (keepHandsFromTracking)
            {
                if (_leftHand != null) _leftHand.rotation = leftHandTracked;
                if (_rightHand != null) _rightHand.rotation = rightHandTracked;
            }

            _hipsWritten = _hips.rotation;
            _hipsWrittenValid = true;
        }

        // La cadera según Meta. Si sigue exactamente como la dejamos el frame anterior, Meta no la reescribió
        // (o no está mandando datos): se usa la última que sí vino de Meta en vez de la nuestra.
        private Quaternion TrackedHipsRotation()
        {
            Quaternion now = _hips.rotation;
            bool untouched = _hipsWrittenValid && Quaternion.Angle(now, _hipsWritten) < 0.01f;
            if (!untouched || !_hipsFromTrackingValid)
            {
                _hipsFromTracking = now;
                _hipsFromTrackingValid = true;
            }
            return _hipsFromTracking;
        }

        // Solo los segmentos con Joy-Con mueven huesos (cabeza y manos ya los pone Meta).
        private bool Drives(BodySegment segment, out Quaternion orientation)
        {
            orientation = Quaternion.identity;
            return estimator.IsJoyconSegment(segment) && estimator.TryGetSegment(segment, out orientation);
        }

        // El brazo (Joy-Con) dejó el codo en otro lugar, y solo girando el antebrazo la mano no llega al control.
        // IK de dos huesos: se dobla el codo para que la mano quede donde Meta la puso (el control), y el Joy-Con
        // del brazo decide hacia qué lado sale el codo (el plano del brazo). Si el control queda más lejos que el
        // largo del brazo, el brazo se estira apuntando hacia él.
        private void ReachHand(BodySegment upperArm, BodySegment forearm, HumanBodyBones upperBone,
                               HumanBodyBones foreBone, Transform hand, Vector3 target)
        {
            if (hand == null || !Drives(upperArm, out _) || estimator.IsJoyconSegment(forearm)) return;
            Transform upper = avatar.GetBoneTransform(upperBone);
            Transform fore = avatar.GetBoneTransform(foreBone);
            if (upper == null || fore == null) return;

            Vector3 shoulder = upper.position, elbow = fore.position, wrist = hand.position;
            float a = (elbow - shoulder).magnitude, b = (wrist - elbow).magnitude;
            Vector3 toTarget = target - shoulder;
            float distance = toTarget.magnitude;
            if (a < 1e-4f || b < 1e-4f || distance < 1e-4f) return;
            Vector3 axis = toTarget / distance;
            distance = Mathf.Clamp(distance, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);

            // Hacia dónde sale el codo: donde lo dejó el Joy-Con, sin la parte que va hacia el control.
            Vector3 pole = Vector3.ProjectOnPlane(elbow - shoulder, axis);
            if (pole.sqrMagnitude < 1e-8f) pole = Vector3.ProjectOnPlane(Vector3.down, axis);
            if (pole.sqrMagnitude < 1e-8f) return;
            pole.Normalize();

            float cosShoulder = Mathf.Clamp((a * a + distance * distance - b * b) / (2f * a * distance), -1f, 1f);
            Vector3 wantedElbow = shoulder + a * (cosShoulder * axis + Mathf.Sqrt(1f - cosShoulder * cosShoulder) * pole);

            Quaternion upperFix = Quaternion.FromToRotation(elbow - shoulder, wantedElbow - shoulder);
            upper.rotation = Quaternion.Slerp(Quaternion.identity, upperFix, weight) * upper.rotation;

            Vector3 foreNow = hand.position - fore.position, foreWanted = target - fore.position;
            if (foreNow.sqrMagnitude < 1e-8f || foreWanted.sqrMagnitude < 1e-8f) return;
            Quaternion foreFix = Quaternion.FromToRotation(foreNow, foreWanted);
            fore.rotation = Quaternion.Slerp(Quaternion.identity, foreFix, weight) * fore.rotation;
        }

        private float LowestFootY()
        {
            if (_leftFoot != null && _rightFoot != null) return Mathf.Min(_leftFoot.position.y, _rightFoot.position.y);
            if (_leftFoot != null) return _leftFoot.position.y;
            return _rightFoot != null ? _rightFoot.position.y : _hips.position.y;
        }

        // Desplaza la cadera (y con ella todo el cuerpo) en vertical para que el pie más bajo quede en el piso.
        // Con Meta, CharacterRetargeter vuelve a poner la cadera cada frame; sin Meta (pruebas sin visor) la cadera
        // se queda donde la dejamos. Por eso se parte de la cadera "sin corregir": si nadie la movió desde el frame
        // anterior, se le quita el desplazamiento que le habíamos puesto.
        private void GroundFeet()
        {
            if (_leftFoot == null && _rightFoot == null) return;

            Vector3 basePosition = _hips.position;
            if (_groundApplied && basePosition == _lastHipsPosition) basePosition -= Vector3.up * _groundOffset;
            _hips.position = basePosition;

            float wanted = Mathf.Clamp(_floorFootY - LowestFootY(), -maxGroundCorrection, maxGroundCorrection) * weight;
            _groundOffset = Mathf.Lerp(_groundOffset, wanted, 1f - Mathf.Exp(-groundSmoothing * Time.deltaTime));

            _hips.position = basePosition + Vector3.up * _groundOffset;
            _lastHipsPosition = _hips.position;
            _groundApplied = true;
        }

        private void Apply(Transform bone, Quaternion target)
        {
            bone.rotation = weight >= 1f ? target : Quaternion.Slerp(bone.rotation, target, weight);
        }
    }
}
