// JoyconMultiClient.cs
//
// Recibe de VARIOS servidores joycon_vrpn_server a la vez (ej. 4 Joy-Cons en
// este PC y 4 en otro) y los junta en una sola numeración "global".
//
// Cada servidor numera sus Joy-Cons desde 0, así que a cada uno se le asigna un
// desplazamiento (sensorOffset):
//
//   servidor A (127.0.0.1,     offset 0): sensores locales 0..3 -> globales 0..3
//   servidor B (192.168.1.45,  offset 4): sensores locales 0..3 -> globales 4..7
//
// Si los rangos se pisan (ej. A tiene 5 Joy-Cons y B empieza en 4) se reporta
// en OverlapWarning y gana la muestra más reciente.
//
// No depende de UnityEngine.

using System;
using System.Collections.Generic;

namespace PujJoycon
{
    [Serializable]
    public class JoyconServerEndpoint
    {
        public string host = "127.0.0.1";
        public int port = JoyconProtocol.DefaultPort;
        /// <summary>Índice global del sensor 0 de este servidor.</summary>
        public int sensorOffset;

        public JoyconServerEndpoint() { }

        public JoyconServerEndpoint(string host, int port, int sensorOffset)
        {
            this.host = host;
            this.port = port;
            this.sensorOffset = sensorOffset;
        }

        public override string ToString() => $"{host}:{port}";
    }

    public struct JoyconServerStatus
    {
        public JoyconServerEndpoint Endpoint;
        public JoyconClientStatus Client;
        /// <summary>True si llegó un paquete de este servidor hace menos de activeWithinSeconds.</summary>
        public bool IsReceiving;
        /// <summary>Rango global que ocupa ahora: [FirstGlobalIndex, FirstGlobalIndex + Client.SensorCount).</summary>
        public int FirstGlobalIndex;
    }

    public sealed class JoyconMultiClient : IDisposable
    {
        /// <summary>Tamaño de la numeración global (2 servidores x 8 Joy-Cons).</summary>
        public const int MaxTotalSensors = 16;

        private readonly JoyconServerEndpoint[] _endpoints;
        private readonly JoyconUdpClient[] _clients;
        private readonly JoyconSensorSample[][] _perServer;
        private readonly double _activeWithinSeconds;

        public JoyconMultiClient(IList<JoyconServerEndpoint> endpoints, double activeWithinSeconds = 1.0)
        {
            if (endpoints == null || endpoints.Count == 0) throw new ArgumentException("Se necesita al menos un servidor.", nameof(endpoints));

            _activeWithinSeconds = activeWithinSeconds;
            _endpoints = new JoyconServerEndpoint[endpoints.Count];
            _clients = new JoyconUdpClient[endpoints.Count];
            _perServer = new JoyconSensorSample[endpoints.Count][];

            var warnings = new List<string>();
            for (int i = 0; i < endpoints.Count; i++)
            {
                var e = endpoints[i] ?? throw new ArgumentException($"Servidor {i} es null.", nameof(endpoints));
                if (e.sensorOffset < 0 || e.sensorOffset >= MaxTotalSensors)
                {
                    throw new ArgumentOutOfRangeException(nameof(endpoints),
                        $"sensorOffset de {e} debe estar entre 0 y {MaxTotalSensors - 1} (es {e.sensorOffset}).");
                }
                for (int j = 0; j < i; j++)
                {
                    if (SameEndpoint(_endpoints[j], e)) warnings.Add($"{e} está repetido en la lista de servidores.");
                    if (_endpoints[j].sensorOffset == e.sensorOffset)
                    {
                        warnings.Add($"{_endpoints[j]} y {e} tienen el mismo sensorOffset ({e.sensorOffset}): sus sensores se pisan.");
                    }
                }

                _endpoints[i] = new JoyconServerEndpoint(e.host?.Trim(), e.port, e.sensorOffset);
                _clients[i] = new JoyconUdpClient(_endpoints[i].host, _endpoints[i].port);
                _perServer[i] = new JoyconSensorSample[JoyconProtocol.MaxSensors];
            }
            ConfigurationWarning = warnings.Count > 0 ? string.Join(" ", warnings) : null;
        }

        public int ServerCount => _clients.Length;

        /// <summary>Problemas de configuración detectados al crear (null si no hay).</summary>
        public string ConfigurationWarning { get; }

        /// <summary>Rangos de sensores que se pisan, según lo último recibido (null si no hay). Se actualiza en Update.</summary>
        public string OverlapWarning { get; private set; }

        public JoyconServerEndpoint GetEndpoint(int server) => _endpoints[server];

        public void Start()
        {
            foreach (var c in _clients) c.Start();
        }

        public void Stop()
        {
            foreach (var c in _clients) c.Stop();
        }

        public void Dispose() => Stop();

        public JoyconServerStatus GetServerStatus(int server)
        {
            var st = _clients[server].GetStatus();
            return new JoyconServerStatus
            {
                Endpoint = _endpoints[server],
                Client = st,
                IsReceiving = st.SecondsSinceLastPacket < _activeWithinSeconds,
                FirstGlobalIndex = _endpoints[server].sensorOffset,
            };
        }

        /// <summary>
        /// Junta las muestras de todos los servidores en la numeración global.
        /// <paramref name="merged"/> y <paramref name="sourceServer"/> deben tener
        /// MaxTotalSensors elementos; sourceServer[g] queda en −1 si nadie manda el sensor g.
        /// Llamar una vez por frame.
        /// </summary>
        public void Update(JoyconSensorSample[] merged, int[] sourceServer)
        {
            if (merged == null || merged.Length < MaxTotalSensors) throw new ArgumentException("merged debe tener MaxTotalSensors elementos.", nameof(merged));
            if (sourceServer == null || sourceServer.Length < MaxTotalSensors) throw new ArgumentException("sourceServer debe tener MaxTotalSensors elementos.", nameof(sourceServer));

            for (int g = 0; g < MaxTotalSensors; g++)
            {
                merged[g] = default;
                merged[g].AgeSeconds = double.PositiveInfinity;
                sourceServer[g] = -1;
            }

            for (int s = 0; s < _clients.Length; s++)
            {
                var samples = _perServer[s];
                _clients[s].CopySamples(samples);
                int offset = _endpoints[s].sensorOffset;
                for (int i = 0; i < samples.Length; i++)
                {
                    int g = offset + i;
                    if (g >= MaxTotalSensors || !samples[i].HasData) continue;
                    // Si dos servidores mandan el mismo índice global, gana el más reciente.
                    if (sourceServer[g] < 0 || samples[i].AgeSeconds < merged[g].AgeSeconds)
                    {
                        merged[g] = samples[i];
                        sourceServer[g] = s;
                    }
                }
            }

            OverlapWarning = ComputeOverlapWarning();
        }

        private string ComputeOverlapWarning()
        {
            List<string> problems = null;
            for (int a = 0; a < _clients.Length; a++)
            {
                var sa = GetServerStatus(a);
                if (!sa.IsReceiving || sa.Client.SensorCount == 0) continue;
                int aStart = sa.FirstGlobalIndex, aEnd = aStart + sa.Client.SensorCount; // [aStart, aEnd)

                if (aEnd > MaxTotalSensors)
                {
                    (problems ??= new List<string>()).Add(
                        $"{sa.Endpoint} manda {sa.Client.SensorCount} sensores desde el {aStart}: los mayores a {MaxTotalSensors - 1} se ignoran.");
                }

                for (int b = a + 1; b < _clients.Length; b++)
                {
                    var sb = GetServerStatus(b);
                    if (!sb.IsReceiving || sb.Client.SensorCount == 0) continue;
                    int bStart = sb.FirstGlobalIndex, bEnd = bStart + sb.Client.SensorCount;
                    if (aStart < bEnd && bStart < aEnd)
                    {
                        (problems ??= new List<string>()).Add(
                            $"{sa.Endpoint} (sensores {aStart}-{aEnd - 1}) y {sb.Endpoint} (sensores {bStart}-{bEnd - 1}) se pisan: " +
                            "aumente el sensorOffset del segundo.");
                    }
                }
            }
            return problems == null ? null : string.Join(" ", problems);
        }

        private static bool SameEndpoint(JoyconServerEndpoint a, JoyconServerEndpoint b)
        {
            return a.port == b.port && string.Equals(a.host?.Trim(), b.host?.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
