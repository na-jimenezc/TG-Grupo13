// JoyconAvatarDriver.cs
//
// Mueve el avatar (X Bot, Humanoid) con los Joy-Cons, ENCIMA del seguimiento
// corporal de Meta (MetaSourceDataProvider + CharacterRetargeter):
//
//   - Meta sigue mandando: posición del avatar, rumbo (hacia dónde mira el cuerpo),
//     cabeza (visor) y manos (controles). Esas partes están bien rastreadas y no derivan.
//   - Los Joy-Cons mandan en los huesos que tengan sensor: pelvis, tronco (repartido
//     entre las 3 vértebras), brazos, antebrazos, muslos y piernas.
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

        private Transform _hips, _head, _leftHand, _rightHand;
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

            _hipsTracked0 = _hips.rotation;
            _hipsBone0 = _hips.rotation;
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
            if (!_calibrated || !estimator.IsCalibrated || weight <= 0f) return;

            // Lo que Meta puso este frame, antes de tocar nada.
            Quaternion hipsTracked = _hips.rotation;
            Quaternion headTracked = _head != null ? _head.rotation : Quaternion.identity;
            Quaternion leftHandTracked = _leftHand != null ? _leftHand.rotation : Quaternion.identity;
            Quaternion rightHandTracked = _rightHand != null ? _rightHand.rotation : Quaternion.identity;

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
            if (Drives(BodySegment.Pelvis, out var pelvis))
            {
                Apply(_hips, PostureMath.BoneWorldRotation(pelvis, referenceHeading, bodyNow, _body0, _hipsBone0));
            }

            if (_spine.Length > 0 && Drives(BodySegment.Trunk, out var trunk))
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

            if (keepHeadFromTracking && _head != null) _head.rotation = headTracked;
            if (keepHandsFromTracking)
            {
                if (_leftHand != null) _leftHand.rotation = leftHandTracked;
                if (_rightHand != null) _rightHand.rotation = rightHandTracked;
            }
        }

        // Solo los segmentos con Joy-Con mueven huesos (cabeza y manos ya los pone Meta).
        private bool Drives(BodySegment segment, out Quaternion orientation)
        {
            orientation = Quaternion.identity;
            return estimator.IsJoyconSegment(segment) && estimator.TryGetSegment(segment, out orientation);
        }

        private void Apply(Transform bone, Quaternion target)
        {
            bone.rotation = weight >= 1f ? target : Quaternion.Slerp(bone.rotation, target, weight);
        }
    }
}
