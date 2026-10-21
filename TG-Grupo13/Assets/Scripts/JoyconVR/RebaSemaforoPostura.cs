// RebaSemaforoPostura.cs
//
// Semáforo de postura en el visor durante el TUTORIAL de cada actividad: un panel
// delante de la persona (un poco por debajo de la vista) con tres luces y una
// indicación de qué corregir, según la parte del cuerpo que esté peor.
//
// Reglas (puntos de la hoja REBA de cada parte, los mismos que se graban):
//   Tronco   rojo  : flexión > 60°                 "Espalda muy inclinada: doble las rodillas…"
//            amarillo: 20–60°, extensión > 20°, o torcido / inclinado de lado
//   Brazos   rojo  : brazo > 90° (por encima del hombro)
//            amarillo: 45–90°
//   Cuello   amarillo: flexión > 20°, extensión, o torcido / inclinado de lado
//   Muñecas  amarillo: flexión o extensión > 15°
//   Piernas: sin aviso (doblar las rodillas es justo lo que se recomienda).
//
// Para que no parpadee: una indicación solo aparece si se mantiene un rato
// (segundosParaAvisar) y queda en pantalla al menos segundosMinimosAviso.
// Cada indicación nueva queda en eventos.csv ("semaforo"). Con buena postura un
// rato largo, felicita con el panel de RetroalimentacionPositiva del equipo.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PujJoycon
{
    public class RebaSemaforoPostura : MonoBehaviour
    {
        [SerializeField] private RebaPostureEstimator estimator;
        [SerializeField] private JoyconSessionCalibrator calibrator;
        [SerializeField] private RebaSessionRecorder recorder;

        [Header("Cuándo se muestra")]
        [SerializeField] private bool mostrarEnTutorial = true;
        [Tooltip("También durante la actividad (pre y post). Apagado: el semáforo es parte de la capacitación (tutorial).")]
        [SerializeField] private bool mostrarEnActividad = false;
        [Tooltip("En escenas sin GameFlowManager (PruebaJoycons) se muestra siempre que haya calibración.")]
        [SerializeField] private bool mostrarSinFlujo = true;
        [Tooltip("Para probar en Core sin iniciar una sesión: marcarlo con Play activo.")]
        [SerializeField] private bool forzarVisible = false;

        [Header("Avisos")]
        [Tooltip("Segundos que una mala postura debe mantenerse antes de avisar.")]
        [SerializeField] private float segundosParaAvisar = 1f;
        [Tooltip("Segundos mínimos que una indicación queda en pantalla.")]
        [SerializeField] private float segundosMinimosAviso = 2.5f;
        [Tooltip("Segundos seguidos en verde para felicitar (0 = nunca).")]
        [SerializeField] private float felicitarTras = 12f;
        [SerializeField] private float segundosEntreFelicitaciones = 40f;

        [Header("Panel en el visor")]
        [SerializeField] private float distancia = 0.9f;
        [Tooltip("Metros por debajo de los ojos, para no tapar la tarea.")]
        [SerializeField] private float alturaBajoLaVista = 0.35f;
        [Tooltip("Qué tan rápido sigue a la cabeza (1/s). Bajo = se mueve suave y no marea.")]
        [SerializeField] private float seguimiento = 2.5f;

        private enum Nivel { Verde = 0, Amarillo = 1, Rojo = 2 }

        private struct Indicacion
        {
            public Nivel nivel;
            public string parte;
            public string mensaje;
            public int prioridad;   // menor = más importante a igual nivel
            public float espera;    // segundos que debe mantenerse antes de avisar
            public string Clave => parte + "|" + mensaje;
        }

        private static readonly Color[] ColoresLuz =
        {
            new Color(0.20f, 0.75f, 0.30f), // verde
            new Color(1.00f, 0.78f, 0.10f), // amarillo
            new Color(0.90f, 0.20f, 0.18f), // rojo
        };
        private const float LuzApagada = 0.18f;

        private GameObject _panel;
        private Image[] _luces;
        private TextMeshProUGUI _texto;
        private bool _panelUbicado;

        private readonly List<Indicacion> _candidatas = new List<Indicacion>();
        private string _candidataClave;
        private float _candidataDesde;
        private Indicacion _mostrada;
        private bool _hayMostrada;
        private float _mostradaDesde;
        private float _verdeDesde = -1f;
        private float _ultimaFelicitacion = -999f;

        private void Awake()
        {
            // (sin "??": con objetos de Unity no detecta los componentes destruidos)
            if (estimator == null) estimator = FindAnyObjectByType<RebaPostureEstimator>();
            if (calibrator == null) calibrator = FindAnyObjectByType<JoyconSessionCalibrator>();
            if (recorder == null) recorder = FindAnyObjectByType<RebaSessionRecorder>();
            ConstruirPanel();
        }

        private void OnDestroy()
        {
            if (_panel != null) Destroy(_panel);
        }

        private bool DebeMostrarse()
        {
            if (estimator == null || !estimator.IsCalibrated) return false;
            if (calibrator != null && calibrator.IsCalibrating) return false; // en la calibración manda su mensaje
            if (forzarVisible) return true;

            var flujo = GameFlowManager.Instance;
            if (flujo == null) return mostrarSinFlujo;
            if (!flujo.simulacionEnCurso) return false;
            return (mostrarEnTutorial && flujo.Fase == FaseSesion.Tutorial) ||
                   (mostrarEnActividad && flujo.Fase == FaseSesion.Actividad);
        }

        private void LateUpdate()
        {
            if (!DebeMostrarse())
            {
                if (_panel.activeSelf) _panel.SetActive(false);
                _panelUbicado = false;
                _hayMostrada = false;
                _candidataClave = null;
                _verdeDesde = -1f;
                return;
            }

            float ahora = Time.unscaledTime;
            Indicacion peor = PeorIndicacion();

            // La peor indicación debe mantenerse un rato antes de reemplazar a la que se muestra.
            if (peor.Clave != _candidataClave)
            {
                _candidataClave = peor.Clave;
                _candidataDesde = ahora;
            }
            bool sostenida = ahora - _candidataDesde >= (peor.nivel == Nivel.Verde ? segundosParaAvisar : peor.espera);
            bool puedeCambiar = !_hayMostrada || ahora - _mostradaDesde >= segundosMinimosAviso ||
                                (int)peor.nivel > (int)_mostrada.nivel; // algo peor entra antes
            if (sostenida && puedeCambiar && (!_hayMostrada || peor.Clave != _mostrada.Clave))
            {
                _mostrada = peor;
                _hayMostrada = true;
                _mostradaDesde = ahora;
                if (peor.nivel != Nivel.Verde && recorder != null)
                    recorder.RegistrarEvento("semaforo", $"{peor.nivel} {peor.parte}: {peor.mensaje}");
            }

            if (!_hayMostrada)
            {
                _mostrada = new Indicacion { nivel = Nivel.Verde, parte = "", mensaje = MensajeVerde };
                _hayMostrada = true;
                _mostradaDesde = ahora;
            }

            Felicitar(ahora);
            Dibujar(_mostrada);
            Ubicar();
        }

        private const string MensajeVerde = "Buena postura. ¡Siga así!";

        private void Felicitar(float ahora)
        {
            if (_mostrada.nivel != Nivel.Verde)
            {
                _verdeDesde = -1f;
                return;
            }
            if (_verdeDesde < 0f) _verdeDesde = ahora;
            if (felicitarTras <= 0f || ahora - _verdeDesde < felicitarTras) return;
            if (ahora - _ultimaFelicitacion < segundosEntreFelicitaciones) return;

            _ultimaFelicitacion = ahora;
            _verdeDesde = ahora;
            RetroalimentacionPositiva.MostrarMensaje("¡Excelente postura! Así cuida su espalda.");
        }

        // ------------------------------------------------------------------
        // Reglas por parte del cuerpo
        // ------------------------------------------------------------------

        private Indicacion PeorIndicacion()
        {
            _candidatas.Clear();
            var izq = estimator.Left;
            var der = estimator.Right;
            var angulos = estimator.CurrentAngles;

            // Tronco (igual en los dos lados).
            if (angulos.hasTrunk)
            {
                bool extension = angulos.trunkFlexion < 0f;
                if (izq.trunkBase >= 4)
                    Agregar(Nivel.Rojo, "Espalda", "Espalda muy inclinada: doble las rodillas y acérquese, en vez de agacharse con la espalda.", 0, segundosParaAvisar);
                else if (izq.trunkBase >= 3)
                    Agregar(Nivel.Amarillo, "Espalda", extension ? "No eche la espalda hacia atrás." : "Enderece un poco la espalda: doble más las rodillas.", 0, segundosParaAvisar);
                if (izq.trunkAdjust > 0)
                    Agregar(Nivel.Amarillo, "Espalda", "No tuerza ni ladee el tronco: gire con los pies.", 1, segundosParaAvisar);
            }

            // Brazos.
            ReglasBrazo(izq, angulos.left, "izquierdo");
            ReglasBrazo(der, angulos.right, "derecho");

            // Cuello (solo si se mide, con el visor).
            if (angulos.hasNeck)
            {
                if (izq.neckBase >= 2)
                    Agregar(Nivel.Amarillo, "Cuello", angulos.neckFlexion >= 0f
                        ? "Levante un poco la cabeza: mire hacia abajo con los ojos."
                        : "No eche la cabeza hacia atrás.", 3, segundosParaAvisar);
                if (izq.neckAdjust > 0)
                    Agregar(Nivel.Amarillo, "Cuello", "Mantenga la cabeza de frente, sin girarla ni ladearla.", 4, segundosParaAvisar);
            }

            // Muñecas: más espera, porque su medida es la más ruidosa.
            if (angulos.left.hasWrist && izq.wristBase >= 2)
                Agregar(Nivel.Amarillo, "Muñeca", "Mantenga la muñeca izquierda recta.", 5, segundosParaAvisar * 2f);
            if (angulos.right.hasWrist && der.wristBase >= 2)
                Agregar(Nivel.Amarillo, "Muñeca", "Mantenga la muñeca derecha recta.", 5, segundosParaAvisar * 2f);

            var peor = new Indicacion { nivel = Nivel.Verde, parte = "", mensaje = MensajeVerde, prioridad = 99, espera = segundosParaAvisar };
            foreach (var c in _candidatas)
            {
                if ((int)c.nivel > (int)peor.nivel || (c.nivel == peor.nivel && c.prioridad < peor.prioridad)) peor = c;
            }
            return peor;
        }

        private void ReglasBrazo(RebaResult r, ArmAngles brazo, string lado)
        {
            if (!brazo.measured) return;
            if (r.upperArmBase >= 4)
                Agregar(Nivel.Rojo, "Brazo", $"Baje el brazo {lado}: trabaje por debajo del hombro.", 2, segundosParaAvisar);
            else if (r.upperArmBase >= 3)
                Agregar(Nivel.Amarillo, "Brazo", $"Acerque el brazo {lado} al cuerpo.", 2, segundosParaAvisar);
        }

        private void Agregar(Nivel nivel, string parte, string mensaje, int prioridad, float espera)
        {
            _candidatas.Add(new Indicacion { nivel = nivel, parte = parte, mensaje = mensaje, prioridad = prioridad, espera = espera });
        }

        // ------------------------------------------------------------------
        // Panel
        // ------------------------------------------------------------------

        private void Dibujar(Indicacion i)
        {
            if (!_panel.activeSelf) _panel.SetActive(true);
            for (int k = 0; k < _luces.Length; k++)
            {
                // Las luces van rojo (arriba/izquierda), amarillo, verde, como un semáforo.
                Nivel nivelLuz = (Nivel)(2 - k);
                Color c = ColoresLuz[(int)nivelLuz];
                _luces[k].color = nivelLuz == i.nivel ? c : new Color(c.r * LuzApagada, c.g * LuzApagada, c.b * LuzApagada, 1f);
            }
            string titulo = i.nivel == Nivel.Verde ? "" : $"<b>{i.parte}</b>\n";
            _texto.text = titulo + i.mensaje;
        }

        private void Ubicar()
        {
            var camara = Camera.main;
            if (camara == null) return;

            Vector3 frente = Vector3.ProjectOnPlane(camara.transform.forward, Vector3.up);
            if (frente.sqrMagnitude < 0.001f) frente = Vector3.ProjectOnPlane(camara.transform.up, Vector3.up);
            frente.Normalize();
            Vector3 destino = camara.transform.position + frente * distancia + Vector3.down * alturaBajoLaVista;

            // Sigue a la cabeza con suavidad (pegado a la cabeza marea).
            Transform t = _panel.transform;
            t.position = _panelUbicado ? Vector3.Lerp(t.position, destino, 1f - Mathf.Exp(-seguimiento * Time.unscaledDeltaTime)) : destino;
            _panelUbicado = true;
            Vector3 mirada = t.position - camara.transform.position;
            if (mirada.sqrMagnitude > 1e-6f) t.rotation = Quaternion.LookRotation(mirada, Vector3.up);
        }

        // Panel en el mundo de 70 x 15 cm: fondo oscuro, tres luces a la izquierda y el texto.
        private void ConstruirPanel()
        {
            _panel = new GameObject("SemaforoPostura", typeof(RectTransform), typeof(Canvas));
            var canvas = _panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            var rt = (RectTransform)_panel.transform;
            rt.sizeDelta = new Vector2(700f, 150f);
            _panel.transform.localScale = Vector3.one * 0.001f;

            var fondo = Hijo("Fondo", _panel.transform).AddComponent<Image>();
            Estirar(fondo.rectTransform, 0f, 0f);
            fondo.color = new Color(0.08f, 0.08f, 0.1f, 0.82f);
            fondo.raycastTarget = false;

            Sprite circulo = CrearCirculo();
            _luces = new Image[3];
            for (int k = 0; k < 3; k++)
            {
                var luz = Hijo($"Luz{k}", _panel.transform).AddComponent<Image>();
                luz.sprite = circulo;
                luz.raycastTarget = false;
                var r = luz.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0f, 0.5f);
                r.sizeDelta = new Vector2(38f, 38f);
                r.anchoredPosition = new Vector2(16f + k * 44f, 0f);
                _luces[k] = luz;
            }

            var textoGo = Hijo("Texto", _panel.transform);
            _texto = textoGo.AddComponent<TextMeshProUGUI>();
            Estirar(_texto.rectTransform, 160f, 14f);
            if (TMP_Settings.defaultFontAsset != null) _texto.font = TMP_Settings.defaultFontAsset;
            _texto.alignment = TextAlignmentOptions.MidlineLeft;
            _texto.color = Color.white;
            _texto.richText = true;
            _texto.enableAutoSizing = true;
            _texto.fontSizeMin = 20f;
            _texto.fontSizeMax = 40f;
            _texto.raycastTarget = false;

            _panel.SetActive(false);
        }

        private static GameObject Hijo(string nombre, Transform padre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);
            return go;
        }

        private static void Estirar(RectTransform r, float izquierda, float margen)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(izquierda, margen);
            r.offsetMax = new Vector2(-margen, -margen);
        }

        // Círculo blanco dibujado por código (no depende de recursos internos de Unity).
        private static Sprite CrearCirculo()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float centro = (n - 1) / 2f, radio = n / 2f - 1f;
            var pix = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - centro) * (x - centro) + (y - centro) * (y - centro));
                byte a = (byte)(Mathf.Clamp01(radio - d + 0.5f) * 255f);
                pix[y * n + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pix);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
