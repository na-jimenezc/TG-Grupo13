// JoyconReceiver.cs
//
// Componente de Unity que recibe las orientaciones de los Joy-Cons desde uno o
// varios joycon_vrpn_server. Poner UNO en la escena (ej. en un GameObject "Joycons").
//
//  - Servidor principal (serverHost/serverPort): sus Joy-Cons son los sensores
//    0, 1, 2... Si corre en el mismo PC que Unity (Play en el editor / Quest
//    Link) sirve 127.0.0.1.
//  - Servidores adicionales: otros PCs con más Joy-Cons. Cada uno con su IP
//    (la muestra iniciar_servidor_joycons.bat) y un sensorOffset: con offset 4,
//    su sensor 0 pasa a ser el sensor global 4, su 1 el 5, etc. Ese PC debe
//    permitir UDP entrante en el puerto 60123 en el firewall.
//  - Si la app corre DENTRO del Quest (build Android), ningún servidor es
//    127.0.0.1: poner la IP LAN de cada PC.
//  - Los datos se leen en hilos aparte (JoyconUdpClient, uno por servidor);
//    aquí solo se copian una vez por frame, antes que los demás scripts
//    (DefaultExecutionOrder).
//
// En el resto del código (JoyconDebugDriver, RebaPostureEstimator) el
// "sensorIndex" es siempre el índice GLOBAL.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PujJoycon
{
    [DefaultExecutionOrder(-100)]
    public class JoyconReceiver : MonoBehaviour
    {
        public static JoyconReceiver Instance { get; private set; }

        public const int MaxSensors = JoyconMultiClient.MaxTotalSensors;

        [Header("Servidor principal (sensores 0, 1, 2...)")]
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = JoyconProtocol.DefaultPort;

        [Header("Servidores adicionales (otros PCs)")]
        [Tooltip("Ej.: host = IP del otro PC, port = 60123, sensorOffset = 4 -> sus Joy-Cons son los sensores 4, 5, 6, 7.")]
        [SerializeField] private JoyconServerEndpoint[] additionalServers = new JoyconServerEndpoint[0];

        [Header("Estado")]
        [Tooltip("Un sensor sin actualizar por más de esto se considera perdido.")]
        [SerializeField] private float staleAfterSeconds = 0.5f;
        [SerializeField] private bool showDebugOverlay = true;

        private JoyconMultiClient _client;
        private readonly JoyconSensorSample[] _samples = new JoyconSensorSample[MaxSensors];
        private readonly int[] _sourceServer = new int[MaxSensors];
        private readonly Quaternion[] _rotations = new Quaternion[MaxSensors];
        private JoyconServerStatus[] _serverStatus = new JoyconServerStatus[0];
        private bool[] _wasReceiving = new bool[0];
        private string _lastOverlapWarning;

        /// <summary>True si al menos un servidor está mandando datos.</summary>
        public bool IsReceiving
        {
            get
            {
                foreach (var s in _serverStatus) if (s.IsReceiving) return true;
                return false;
            }
        }

        /// <summary>True si TODOS los servidores configurados están mandando datos.</summary>
        public bool AllServersReceiving
        {
            get
            {
                if (_serverStatus.Length == 0) return false;
                foreach (var s in _serverStatus) if (!s.IsReceiving) return false;
                return true;
            }
        }

        /// <summary>Suma de los Joy-Cons que están mandando todos los servidores activos.</summary>
        public int SensorCount
        {
            get
            {
                int n = 0;
                foreach (var s in _serverStatus) if (s.IsReceiving) n += s.Client.SensorCount;
                return n;
            }
        }

        public int ServerCount => _serverStatus.Length;
        public JoyconServerStatus GetServerStatus(int server) => _serverStatus[server];

        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Joycon] Hay más de un JoyconReceiver en la escena; se usa el último habilitado.", this);
            }
            Instance = this;
            for (int i = 0; i < _rotations.Length; i++) _rotations[i] = Quaternion.identity;

            var endpoints = new List<JoyconServerEndpoint> { new JoyconServerEndpoint(serverHost, serverPort, 0) };
            if (additionalServers != null)
            {
                foreach (var e in additionalServers)
                {
                    if (e == null || string.IsNullOrWhiteSpace(e.host))
                    {
                        Debug.LogWarning("[Joycon] Servidor adicional sin host: se ignora.", this);
                        continue;
                    }
                    endpoints.Add(e);
                }
            }

            try
            {
                _client = new JoyconMultiClient(endpoints, staleAfterSeconds);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Joycon] Configuración de servidores inválida: {e.Message}", this);
                _client = null;
                return;
            }
            if (_client.ConfigurationWarning != null) Debug.LogWarning($"[Joycon] {_client.ConfigurationWarning}", this);

            _serverStatus = new JoyconServerStatus[_client.ServerCount];
            _wasReceiving = new bool[_client.ServerCount];
            _client.Start();
            for (int s = 0; s < _client.ServerCount; s++)
            {
                var ep = _client.GetEndpoint(s);
                Debug.Log($"[Joycon] Registrándose con {ep} (sensores desde el {ep.sensorOffset}) ...", this);
            }
        }

        private void OnDisable()
        {
            // Importante en el editor: sin esto los hilos y sockets siguen vivos al salir de Play.
            _client?.Dispose();
            _client = null;
            _serverStatus = new JoyconServerStatus[0];
            if (Instance == this) Instance = null;
        }

        private void OnApplicationQuit()
        {
            _client?.Dispose();
        }

        private void Update()
        {
            if (_client == null) return;

            _client.Update(_samples, _sourceServer);
            for (int s = 0; s < _serverStatus.Length; s++) _serverStatus[s] = _client.GetServerStatus(s);

            for (int i = 0; i < _samples.Length; i++)
            {
                if (_samples[i].HasData) _rotations[i] = PostureMath.ToUnity(_samples[i].Orientation);
            }

            for (int s = 0; s < _serverStatus.Length; s++)
            {
                var st = _serverStatus[s];
                if (st.IsReceiving == _wasReceiving[s]) continue;
                if (st.IsReceiving)
                {
                    int first = st.FirstGlobalIndex, last = first + st.Client.SensorCount - 1;
                    Debug.Log($"[Joycon] {st.Endpoint}: recibiendo {st.Client.SensorCount} sensor(es) -> globales {first}-{last}.", this);
                }
                else
                {
                    Debug.LogWarning($"[Joycon] {st.Endpoint}: sin datos. {st.Client.LastError}", this);
                }
                _wasReceiving[s] = st.IsReceiving;
            }

            if (_client.OverlapWarning != _lastOverlapWarning)
            {
                if (_client.OverlapWarning != null) Debug.LogWarning($"[Joycon] {_client.OverlapWarning}", this);
                _lastOverlapWarning = _client.OverlapWarning;
            }
        }

        /// <summary>
        /// Orientación del sensor GLOBAL en ejes de Unity, SIN calibrar (cada Joy-Con
        /// tiene su propio "mundo", ver README). Devuelve false si el sensor no existe
        /// o lleva más de staleAfterSeconds sin actualizarse.
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

        /// <summary>De qué servidor viene el sensor global (−1 si de ninguno).</summary>
        public int GetSensorServer(int sensorIndex)
        {
            if (sensorIndex < 0 || sensorIndex >= _sourceServer.Length) return -1;
            return _sourceServer[sensorIndex];
        }

        private void OnGUI()
        {
            if (!showDebugOverlay) return;

            int sensorRows = 0;
            for (int i = 0; i < _samples.Length; i++) if (_sourceServer[i] >= 0) sensorRows++;
            float height = 30 + 22 * (_serverStatus.Length + sensorRows + (_lastOverlapWarning != null ? 2 : 0));
            GUILayout.BeginArea(new Rect(10, 10, 440, height), GUI.skin.box);

            if (_client == null) GUILayout.Label("Joy-Con: configuración inválida (ver Console)");
            for (int s = 0; s < _serverStatus.Length; s++)
            {
                var st = _serverStatus[s];
                string state = st.IsReceiving
                    ? $"OK, {st.Client.SensorCount} sensor(es) -> {st.FirstGlobalIndex}-{st.FirstGlobalIndex + st.Client.SensorCount - 1}"
                    : "SIN DATOS" + (string.IsNullOrEmpty(st.Client.LastError) ? "" : $" ({st.Client.LastError})");
                GUILayout.Label($"PC {s + 1} {st.Endpoint}: {state}");
            }
            if (_lastOverlapWarning != null) GUILayout.Label($"AVISO: {_lastOverlapWarning}");

            for (int i = 0; i < _samples.Length; i++)
            {
                if (_sourceServer[i] < 0) continue;
                Vector3 e = PostureMath.ToEuler(_rotations[i], EulerOrder.XYZ);
                GUILayout.Label($"  sensor {i,2} (PC {_sourceServer[i] + 1}): XYZ = ({e.x,7:F1}, {e.y,7:F1}, {e.z,7:F1})°  edad {_samples[i].AgeSeconds * 1000:F0} ms");
            }
            GUILayout.EndArea();
        }
    }
}
