// JoyconReceiver.cs
//
// Componente de Unity que recibe las orientaciones de los Joy-Cons desde
// joycon_vrpn_server. Poner UNO en la escena (ej. en un GameObject "Joycons").
//
//  - serverHost: IP del PC donde corre el servidor. Si la app corre en el mismo
//    PC (Quest Link / Play en el editor) sirve 127.0.0.1; si corre DENTRO del
//    Quest (build Android), poner la IP LAN del PC (ipconfig) y permitir UDP
//    entrante en el puerto 60123 en el firewall de Windows.
//  - Los datos se leen en un hilo aparte (JoyconUdpClient); aquí solo se copian
//    una vez por frame, antes que los demás scripts (DefaultExecutionOrder).

using UnityEngine;

namespace PujJoycon
{
    [DefaultExecutionOrder(-100)]
    public class JoyconReceiver : MonoBehaviour
    {
        public static JoyconReceiver Instance { get; private set; }

        [Header("Servidor")]
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = JoyconProtocol.DefaultPort;

        [Header("Estado")]
        [Tooltip("Un sensor sin actualizar por más de esto se considera perdido.")]
        [SerializeField] private float staleAfterSeconds = 0.5f;
        [SerializeField] private bool showDebugOverlay = true;

        private JoyconUdpClient _client;
        private readonly JoyconSensorSample[] _samples = new JoyconSensorSample[JoyconProtocol.MaxSensors];
        private readonly Quaternion[] _rotations = new Quaternion[JoyconProtocol.MaxSensors];
        private JoyconClientStatus _status;
        private bool _wasReceiving;

        /// <summary>True si llegó un paquete hace menos de staleAfterSeconds.</summary>
        public bool IsReceiving => _status.SecondsSinceLastPacket < staleAfterSeconds;
        /// <summary>Cuántos Joy-Cons está enviando el servidor.</summary>
        public int SensorCount => _status.SensorCount;
        public JoyconClientStatus Status => _status;

        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Joycon] Hay más de un JoyconReceiver en la escena; se usa el último habilitado.", this);
            }
            Instance = this;
            for (int i = 0; i < _rotations.Length; i++) _rotations[i] = Quaternion.identity;

            _client = new JoyconUdpClient(serverHost, serverPort);
            _client.Start();
            Debug.Log($"[Joycon] Registrándose con el servidor {serverHost}:{serverPort} ...", this);
        }

        private void OnDisable()
        {
            // Importante en el editor: sin esto el hilo y el socket siguen vivos al salir de Play.
            _client?.Dispose();
            _client = null;
            if (Instance == this) Instance = null;
        }

        private void OnApplicationQuit()
        {
            _client?.Dispose();
        }

        private void Update()
        {
            if (_client == null) return;

            _client.CopySamples(_samples);
            _status = _client.GetStatus();

            for (int i = 0; i < _samples.Length; i++)
            {
                if (_samples[i].HasData) _rotations[i] = PostureMath.ToUnity(_samples[i].Orientation);
            }

            bool receiving = IsReceiving;
            if (receiving != _wasReceiving)
            {
                if (receiving) Debug.Log($"[Joycon] Recibiendo datos: {_status.SensorCount} sensor(es).", this);
                else Debug.LogWarning($"[Joycon] Sin datos del servidor. {_status.LastError}", this);
                _wasReceiving = receiving;
            }
        }

        /// <summary>
        /// Orientación del sensor en ejes de Unity, SIN calibrar (cada Joy-Con tiene
        /// su propio "mundo", ver README). Devuelve false si el sensor no existe o
        /// lleva más de staleAfterSeconds sin actualizarse.
        /// </summary>
        public bool TryGetRotation(int sensorIndex, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (sensorIndex < 0 || sensorIndex >= _rotations.Length) return false;
            if (!_samples[sensorIndex].HasData || _samples[sensorIndex].AgeSeconds > staleAfterSeconds) return false;
            rotation = _rotations[sensorIndex];
            return true;
        }

        /// <summary>Cuaternión crudo tal como lo manda el servidor (para registrar datos / depurar).</summary>
        public bool TryGetRaw(int sensorIndex, out RawQuaternion raw)
        {
            raw = RawQuaternion.Identity;
            if (sensorIndex < 0 || sensorIndex >= _samples.Length || !_samples[sensorIndex].HasData) return false;
            raw = _samples[sensorIndex].Orientation;
            return true;
        }

        private void OnGUI()
        {
            if (!showDebugOverlay) return;
            GUILayout.BeginArea(new Rect(10, 10, 420, 260), GUI.skin.box);
            GUILayout.Label($"Joy-Con server {serverHost}:{serverPort} — " +
                            (IsReceiving ? $"OK, {_status.SensorCount} sensor(es), {_status.PacketsReceived} paquetes"
                                         : "SIN DATOS"));
            if (!string.IsNullOrEmpty(_status.LastError)) GUILayout.Label($"Error: {_status.LastError}");
            for (int i = 0; i < _status.SensorCount && i < _samples.Length; i++)
            {
                Vector3 e = PostureMath.ToEuler(_rotations[i], EulerOrder.XYZ);
                GUILayout.Label($"  sensor {i}: XYZ = ({e.x,7:F1}, {e.y,7:F1}, {e.z,7:F1})°  edad {_samples[i].AgeSeconds * 1000:F0} ms");
            }
            GUILayout.EndArea();
        }
    }
}
