// JoyconDebugDriver.cs
//
// Equivalente al cubo de client/client_main.cpp: rota este Transform según un
// Joy-Con. Sirve para la primera prueba ("¿llegan datos?, ¿giran bien los ejes?").
//
// Uso: poner en un Cube, elegir sensorIndex, dar Play, dejar el Joy-Con quieto
// en la mano en la orientación que corresponda a la del cubo y usar
// "Calibrar" (menú ⋮ del componente) o esperar calibrateAfterSeconds.
// Luego girar el Joy-Con sobre cada eje y ver que el cubo gire igual; si un eje
// sale invertido, corregir en PostureMath.ToUnity.
//
// No usa teclado a propósito: el proyecto usa el Input System nuevo y
// UnityEngine.Input lanza excepción si el viejo está desactivado.

using UnityEngine;

namespace PujJoycon
{
    public class JoyconDebugDriver : MonoBehaviour
    {
        [SerializeField] private int sensorIndex = 0;
        [Tooltip("Calibra automáticamente tras estos segundos de datos (0 = solo manual).")]
        [SerializeField] private float calibrateAfterSeconds = 5f; // deja converger el filtro del servidor

        private Quaternion _restRotation;
        private Quaternion _q0 = Quaternion.identity;
        private bool _calibrated;
        private float _receivingSince = -1f;

        private void Start() => _restRotation = transform.rotation;

        [ContextMenu("Calibrar")]
        public void Calibrate()
        {
            var rx = JoyconReceiver.Instance;
            if (rx != null && rx.TryGetRotation(sensorIndex, out var q))
            {
                _q0 = q;
                _calibrated = true;
                Debug.Log($"[Joycon] Sensor {sensorIndex} calibrado.", this);
            }
            else
            {
                Debug.LogWarning($"[Joycon] No se pudo calibrar: no hay datos del sensor {sensorIndex}.", this);
            }
        }

        private void Update()
        {
            var rx = JoyconReceiver.Instance;
            if (rx == null || !rx.TryGetRotation(sensorIndex, out var q))
            {
                _receivingSince = -1f;
                return;
            }

            if (!_calibrated && calibrateAfterSeconds > 0f)
            {
                if (_receivingSince < 0f) _receivingSince = Time.time;
                if (Time.time - _receivingSince >= calibrateAfterSeconds) Calibrate();
            }

            // rest * inv(q0) * q : la rotación del Joy-Con desde la calibración,
            // aplicada en los ejes locales del cubo.
            transform.rotation = _restRotation * PostureMath.Inverse(_q0) * q;
        }
    }
}
