// JoyconPruebaPosturaN.cs
//
// SOLO PARA PROBAR SIN VISOR (escena PruebaJoycons). Sin el seguimiento de Meta
// nadie pone al avatar en una postura: el X Bot queda en T (brazos estirados a
// los lados). Este script lo deja en postura N (brazos colgando, palmas hacia
// los muslos), que es la postura en la que se calibran los Joy-Cons, y lo
// vuelve a poner así en cada calibración para que JoyconAvatarDriver tome esa
// postura como "cero".
//
// En la escena real (Core) NO se usa: ahí la postura la pone Meta.

using System.Collections.Generic;
using UnityEngine;

namespace PujJoycon
{
    [DefaultExecutionOrder(-50)]
    public class JoyconPruebaPosturaN : MonoBehaviour
    {
        [SerializeField] private Animator avatar;
        [SerializeField] private RebaPostureEstimator estimator;
        [Tooltip("Separación de los brazos respecto al cuerpo, en grados (0 = pegados).")]
        [SerializeField, Range(0f, 30f)] private float armSpread = 8f;
        [Tooltip("Separación de las piernas, en grados (0 = verticales). El modelo viene con las piernas " +
                 "abiertas; si no se enderezan, la pierna parece empezar levantada hacia el lado.")]
        [SerializeField, Range(0f, 15f)] private float legSpread = 2f;

        private readonly List<(Transform bone, Quaternion local)> _poseN = new List<(Transform, Quaternion)>();

        private void Start()
        {
            if (avatar == null || !avatar.isHuman)
            {
                Debug.LogError("[Joycon] JoyconPruebaPosturaN necesita el Animator Humanoid del avatar.", this);
                enabled = false;
                return;
            }
            if (estimator == null) estimator = FindAnyObjectByType<RebaPostureEstimator>();

            PointDown(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, -1f, armSpread);
            PointDown(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 1f, armSpread);
            PointDown(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, -1f, legSpread);
            PointDown(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 1f, legSpread);

            // Se guarda la postura N completa para restaurarla en cada calibración.
            foreach (var t in avatar.GetComponentsInChildren<Transform>()) _poseN.Add((t, t.localRotation));

            if (estimator != null) estimator.Calibrated += RestorePoseN;
        }

        private void OnDestroy()
        {
            if (estimator != null) estimator.Calibrated -= RestorePoseN;
        }

        // JoyconAvatarDriver toma la postura en su LateUpdate, después de esto.
        private void RestorePoseN()
        {
            foreach (var (bone, local) in _poseN)
            {
                if (bone != null) bone.localRotation = local;
            }
        }

        // Deja una extremidad (brazo o pierna) recta hacia abajo, un poco separada del cuerpo
        // (side = +1 derecha, −1 izquierda del avatar).
        private void PointDown(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end, float side, float spread)
        {
            Transform u = avatar.GetBoneTransform(upper), l = avatar.GetBoneTransform(lower), e = avatar.GetBoneTransform(end);
            if (u == null || l == null) return;

            Vector3 outward = avatar.transform.right * side;
            Vector3 down = Vector3.Slerp(-avatar.transform.up, outward, spread / 90f).normalized;

            u.rotation = Quaternion.FromToRotation(l.position - u.position, down) * u.rotation;
            if (e != null) l.rotation = Quaternion.FromToRotation(e.position - l.position, down) * l.rotation;
        }
    }
}
