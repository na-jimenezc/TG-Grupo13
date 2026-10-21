// RebaResultadosEnPantalla.cs
//
// Llena la pantalla de resultados del visor (escena PantallaResultados, que carga
// GameFlowManager al terminar las actividades) con el puntaje REBA de cada
// actividad antes y después del tutorial.
//
// No modifica la escena: cuando se carga, busca los textos que dejó el equipo
// (TextResultadosSiembra, TextResultadosDeshierbe y "TextResultadosDeshierbe (1)",
// que es el de Corte) y les cambia el contenido. Los datos salen de
// RebaSessionRecorder (los mismos que van al historial del participante).
//
// Se muestra la moda del REBA (el puntaje más repetido = la postura más frecuente),
// igual que en la tarjeta de "Revisión REBA" del Game Master.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PujJoycon
{
    public class RebaResultadosEnPantalla : MonoBehaviour
    {
        [System.Serializable]
        public class TextoActividad
        {
            public ActivityType actividad;
            [Tooltip("Nombre del objeto con el texto en la escena de resultados.")]
            public string objeto;
        }

        [SerializeField] private RebaSessionRecorder recorder;
        [SerializeField] private string escenaResultados = "PantallaResultados";
        [SerializeField] private TextoActividad[] textos =
        {
            new TextoActividad { actividad = ActivityType.Siembra, objeto = "TextResultadosSiembra" },
            new TextoActividad { actividad = ActivityType.Deshierbe, objeto = "TextResultadosDeshierbe" },
            new TextoActividad { actividad = ActivityType.Corte, objeto = "TextResultadosDeshierbe (1)" },
        };
        [Tooltip("Segundos máximos esperando a que el grabador termine de calcular los resultados.")]
        [SerializeField] private float esperaMaxima = 5f;

        private const string ColorMejoro = "#2E7D32";
        private const string ColorEmpeoro = "#C62828";

        private void Awake()
        {
            if (recorder == null) recorder = GetComponent<RebaSessionRecorder>();
            if (recorder == null) recorder = FindAnyObjectByType<RebaSessionRecorder>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += AlCargarEscena;
        private void OnDisable() => SceneManager.sceneLoaded -= AlCargarEscena;

        private void AlCargarEscena(Scene escena, LoadSceneMode modo)
        {
            if (escena.name == escenaResultados) StartCoroutine(Llenar(escena));
        }

        private IEnumerator Llenar(Scene escena)
        {
            // El grabador calcula los resultados al pasar a la fase Resultados; normalmente ya terminó.
            for (float t = 0f; recorder != null && !recorder.ResultadosListos && t < esperaMaxima; t += Time.unscaledDeltaTime)
                yield return null;

            var flujo = GameFlowManager.Instance;
            foreach (var item in textos)
            {
                TMP_Text texto = BuscarTexto(escena, item.objeto);
                if (texto == null)
                {
                    Debug.LogWarning($"[REBA] No encontré '{item.objeto}' en {escena.name} para mostrar {item.actividad}.", this);
                    continue;
                }

                bool hecha = flujo == null || flujo.ActividadesSesion == null || Contiene(flujo, item.actividad);
                RebaData reba = null;
                bool medida = recorder != null && recorder.ResultadosListos && recorder.TryGetResultado(item.actividad, out reba);
                texto.text = Texto(hecha, medida ? reba : null);

                // Que quepa en el recuadro que dejó el equipo, sin pasar del tamaño de letra que tenía.
                texto.fontSizeMax = texto.fontSize;
                texto.fontSizeMin = Mathf.Min(12f, texto.fontSize);
                texto.enableAutoSizing = true;
                texto.richText = true;
            }
        }

        private static bool Contiene(GameFlowManager flujo, ActivityType actividad)
        {
            foreach (var a in flujo.ActividadesSesion) if (a == actividad) return true;
            return false;
        }

        // Texto para el participante: puntaje y nivel de riesgo antes y después, y si mejoró.
        private static string Texto(bool hecha, RebaData reba)
        {
            if (!hecha) return "No realizada en esta sesión.";
            if (reba == null || !(reba.PreTutorial.HayDatos || reba.PostTutorial.HayDatos))
                return "No se midió la postura en esta actividad.";

            string pre = Linea("Antes del tutorial", reba.PreTutorial);
            string post = Linea("Después del tutorial", reba.PostTutorial);
            if (!reba.PreTutorial.HayDatos || !reba.PostTutorial.HayDatos) return $"{pre}\n{post}";

            int antes = Puntaje(reba.PreTutorial), despues = Puntaje(reba.PostTutorial);
            string veredicto = despues < antes
                ? $"<color={ColorMejoro}>¡Mejoró su postura! ({antes - despues} punto{(antes - despues == 1 ? "" : "s")} menos)</color>"
                : despues == antes
                    ? "Mantuvo el mismo nivel de postura."
                    : $"<color={ColorEmpeoro}>Puede mejorar: repase las recomendaciones del tutorial.</color>";
            return $"{pre}\n{post}\n{veredicto}";
        }

        private static string Linea(string momento, RebaResumen r) =>
            r.HayDatos ? $"{momento}: REBA más frecuente {Puntaje(r)} (riesgo {Riesgo(Puntaje(r))})" : $"{momento}: sin medición";

        // La moda; en registros viejos (antes de guardarla) se usa la mediana.
        private static int Puntaje(RebaResumen r) => r.Moda > 0 ? r.Moda : Mathf.RoundToInt(r.Mediana);

        private static string Riesgo(int puntaje) => Reba.RiskName(Reba.RiskOf(puntaje)).ToLowerInvariant();

        private static TMP_Text BuscarTexto(Scene escena, string nombre)
        {
            foreach (var raiz in escena.GetRootGameObjects())
            {
                foreach (var t in raiz.GetComponentsInChildren<TMP_Text>(true))
                    if (t.name == nombre) return t;
            }
            return null;
        }
    }
}
