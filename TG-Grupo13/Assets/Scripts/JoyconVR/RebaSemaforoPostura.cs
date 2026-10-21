// RebaSemaforoPostura.cs
//
// Indicador discreto de postura en el visor durante el TUTORIAL: un punto de color
// fijo arriba a la derecha del campo de visión (verde, amarillo o rojo), sin texto
// por defecto, para no tapar la vista ni interrumpir.
//
// Solo mira espalda y brazos (lo que más pesa en REBA y mejor se mide). El cuello y
// las muñecas no cuentan: girar la cabeza para leer las instrucciones no debe
// encender el aviso.
//
// Poco sensible a propósito: el color cambia solo si la mala postura ocupa la mayor
// parte de los últimos segundos (fraccionParaAvisar de ventanaSegundos):
//   rojo     : espalda > 60° o brazo por encima del hombro (> 90°)
//   amarillo : espalda 20–60° o brazo 45–90°
// Cada cambio a amarillo/rojo queda en eventos.csv ("semaforo").

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
        [Tooltip("También durante la actividad (pre y post). Apagado: el indicador es parte de la capacitación (tutorial).")]
        [SerializeField] private bool mostrarEnActividad = false;
        [Tooltip("En escenas sin GameFlowManager (PruebaJoycons) se muestra siempre que haya calibración.")]
        [SerializeField] private bool mostrarSinFlujo = true;
        [Tooltip("Para probar en Core sin iniciar una sesión: marcarlo con Play activo.")]
        [SerializeField] private bool forzarVisible = false;

        [Header("Sensibilidad")]
        [Tooltip("Segundos que se miran hacia atrás.")]
        [SerializeField] private float ventanaSegundos = 4f;
        [Tooltip("Parte de esos segundos que debe estar en mala postura para cambiar de color (0.7 = 70 %).")]
        [SerializeField, Range(0.3f, 1f)] private float fraccionParaAvisar = 0.7f;

        [Header("Indicador")]
        [Tooltip("Posición respecto a los ojos, en metros: x derecha, y arriba, z adelante.")]
        [SerializeField] private Vector3 posicionEnLaVista = new Vector3(0.22f, 0.15f, 0.75f);
        [Tooltip("Diámetro del punto, en metros.")]
        [SerializeField] private float diametro = 0.03f;
        [SerializeField, Range(0.2f, 1f)] private float opacidad = 0.7f;
        [Tooltip("Mostrar al lado del punto la parte a corregir (\"Espalda\", \"Brazo derecho\").")]
        [SerializeField] private bool mostrarPalabra = false;
        [Tooltip("Felicitar con el panel de RetroalimentacionPositiva tras estos segundos seguidos en verde (0 = nunca).")]
        [SerializeField] private float felicitarTras = 0f;

        private enum Nivel { Verde = 0, Amarillo = 1, Rojo = 2 }

        private static readonly Color[] Colores =
        {
            new Color(0.20f, 0.75f, 0.30f), // verde
            new Color(1.00f, 0.78f, 0.10f), // amarillo
            new Color(0.90f, 0.20f, 0.18f), // rojo
        };

        // Historial de los últimos segundos: (tiempo, nivel de espalda, nivel de cada brazo).
        private struct Muestra { public float t; public Nivel espalda, brazoIzq, brazoDer; }
        private readonly Queue<Muestra> _historial = new Queue<Muestra>();

        private GameObject _indicador;
        private Image _punto;
        private TextMeshProUGUI _palabra;
        private Nivel _nivel = Nivel.Verde;
        private float _verdeDesde = -1f;

        private void Awake()
        {
            // (sin "??": con objetos de Unity no detecta los componentes destruidos)
            if (estimator == null) estimator = FindAnyObjectByType<RebaPostureEstimator>();
            if (calibrator == null) calibrator = FindAnyObjectByType<JoyconSessionCalibrator>();
            if (recorder == null) recorder = FindAnyObjectByType<RebaSessionRecorder>();
            ConstruirIndicador();
        }

        private void OnDestroy()
        {
            if (_indicador != null) Destroy(_indicador);
        }

        private bool DebeMostrarse()
        {
            if (estimator == null || !estimator.IsCalibrated) return false;
            if (calibrator != null && calibrator.IsCalibrating) return false;
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
                if (_indicador.activeSelf) _indicador.SetActive(false);
                _historial.Clear();
                _nivel = Nivel.Verde;
                _verdeDesde = -1f;
                return;
            }

            float ahora = Time.unscaledTime;
            AgregarMuestra(ahora);

            // Cada parte se evalúa con lo que ocupó la mayor parte de la ventana.
            Nivel espalda = Sostenido(m => m.espalda);
            Nivel izq = Sostenido(m => m.brazoIzq);
            Nivel der = Sostenido(m => m.brazoDer);
            Nivel nuevo = Max(espalda, Max(izq, der));
            string parte = nuevo == Nivel.Verde ? "" : espalda == nuevo ? "Espalda" : der == nuevo ? "Brazo derecho" : "Brazo izquierdo";

            if (nuevo != _nivel && nuevo != Nivel.Verde && recorder != null)
                recorder.RegistrarEvento("semaforo", $"{nuevo} {parte}");
            _nivel = nuevo;

            Felicitar(ahora);
            Dibujar(parte);
            Ubicar();
        }

        private void AgregarMuestra(float ahora)
        {
            var izq = estimator.Left;
            var der = estimator.Right;
            var a = estimator.CurrentAngles;
            _historial.Enqueue(new Muestra
            {
                t = ahora,
                espalda = !a.hasTrunk ? Nivel.Verde : izq.trunkBase >= 4 ? Nivel.Rojo : izq.trunkBase >= 3 ? Nivel.Amarillo : Nivel.Verde,
                brazoIzq = NivelBrazo(izq, a.left),
                brazoDer = NivelBrazo(der, a.right),
            });
            while (_historial.Count > 0 && ahora - _historial.Peek().t > ventanaSegundos) _historial.Dequeue();
        }

        private static Nivel NivelBrazo(RebaResult r, ArmAngles brazo)
        {
            if (!brazo.measured) return Nivel.Verde;
            return r.upperArmBase >= 4 ? Nivel.Rojo : r.upperArmBase >= 3 ? Nivel.Amarillo : Nivel.Verde;
        }

        // Rojo si el rojo ocupó la fracción pedida de la ventana; si no, amarillo si amarillo+rojo la ocuparon.
        private Nivel Sostenido(System.Func<Muestra, Nivel> nivelDe)
        {
            if (_historial.Count == 0) return Nivel.Verde;
            int rojo = 0, amarilloORojo = 0;
            foreach (var m in _historial)
            {
                Nivel n = nivelDe(m);
                if (n == Nivel.Rojo) rojo++;
                if (n != Nivel.Verde) amarilloORojo++;
            }
            float total = _historial.Count;
            if (rojo / total >= fraccionParaAvisar) return Nivel.Rojo;
            if (amarilloORojo / total >= fraccionParaAvisar) return Nivel.Amarillo;
            return Nivel.Verde;
        }

        private static Nivel Max(Nivel a, Nivel b) => (int)a >= (int)b ? a : b;

        private void Felicitar(float ahora)
        {
            if (_nivel != Nivel.Verde || felicitarTras <= 0f)
            {
                _verdeDesde = -1f;
                return;
            }
            if (_verdeDesde < 0f) _verdeDesde = ahora;
            if (ahora - _verdeDesde < felicitarTras) return;
            _verdeDesde = ahora;
            RetroalimentacionPositiva.MostrarMensaje("¡Excelente postura! Así cuida su espalda.");
        }

        private void Dibujar(string parte)
        {
            if (!_indicador.activeSelf) _indicador.SetActive(true);
            Color c = Colores[(int)_nivel];
            _punto.color = new Color(c.r, c.g, c.b, opacidad);
            if (_palabra != null)
            {
                _palabra.gameObject.SetActive(mostrarPalabra && _nivel != Nivel.Verde);
                _palabra.text = parte;
            }
        }

        // Fijo en la esquina superior derecha de la vista (se mueve con la cabeza, como un ícono del visor).
        private void Ubicar()
        {
            var camara = Camera.main;
            if (camara == null) return;
            Transform t = _indicador.transform;
            t.position = camara.transform.TransformPoint(posicionEnLaVista);
            t.rotation = camara.transform.rotation;
            t.localScale = Vector3.one * (diametro / 100f); // el punto mide 100 unidades del Canvas
        }

        private void ConstruirIndicador()
        {
            _indicador = new GameObject("SemaforoPostura", typeof(RectTransform), typeof(Canvas));
            var canvas = _indicador.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            ((RectTransform)_indicador.transform).sizeDelta = new Vector2(100f, 100f);

            var puntoGo = new GameObject("Punto", typeof(RectTransform));
            puntoGo.transform.SetParent(_indicador.transform, false);
            _punto = puntoGo.AddComponent<Image>();
            _punto.sprite = CrearCirculo();
            _punto.raycastTarget = false;
            var r = _punto.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;

            // Palabra opcional a la izquierda del punto.
            var palabraGo = new GameObject("Palabra", typeof(RectTransform));
            palabraGo.transform.SetParent(_indicador.transform, false);
            _palabra = palabraGo.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) _palabra.font = TMP_Settings.defaultFontAsset;
            _palabra.alignment = TextAlignmentOptions.MidlineRight;
            _palabra.fontSize = 40f;
            _palabra.color = new Color(1f, 1f, 1f, 0.85f);
            _palabra.raycastTarget = false;
            var rp = _palabra.rectTransform;
            rp.anchorMin = rp.anchorMax = new Vector2(0f, 0.5f);
            rp.pivot = new Vector2(1f, 0.5f);
            rp.sizeDelta = new Vector2(400f, 100f);
            rp.anchoredPosition = new Vector2(-15f, 0f);
            palabraGo.SetActive(false);

            _indicador.SetActive(false);
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
                pix[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(radio - d + 0.5f) * 255f));
            }
            tex.SetPixels32(pix);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
