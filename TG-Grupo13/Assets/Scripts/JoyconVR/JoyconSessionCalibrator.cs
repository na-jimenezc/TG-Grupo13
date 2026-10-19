// JoyconSessionCalibrator.cs
//
// Calibra los Joy-Cons (RebaPostureEstimator -> y con eso el avatar,
// JoyconAvatarDriver) con instrucciones en el visor. NUNCA arranca sola: la
// lanza el Game Master con los botones de la pantalla del PC (abajo a la izquierda),
// cuando el participante está listo.
//
// Calibración completa (una vez por participante, al empezar la sesión):
//   1. Espera a que lleguen datos de los Joy-Cons.
//   2. Pide la postura N con cuenta regresiva y calibra (el "cero" de cada Joy-Con).
//   3. Pide la sentadilla con brazos al frente y calcula cómo está puesto cada
//      Joy-Con en el cuerpo (su montaje), para que los ejes salgan bien.
//   4. Pide volver a la postura N y toma el "cero" otra vez (lo que se corrieron
//      los Joy-Cons en la sentadilla no queda como si la persona estuviera torcida).
//
// Recalibración rápida (al empezar cada momento: pre, tutorial y post):
//   solo postura N, sin sentadilla (el montaje no cambia). RebaSessionRecorder
//   no graba el momento hasta que termina esta calibración.
//
// El panel avisa si falta la calibración completa del participante o si hay un
// momento esperando la recalibración rápida, pero no hace nada por su cuenta.
//
// El mensaje se muestra en el TMP_Text asignado; si no hay ninguno, se crea un
// texto 3D flotando delante de la cámara del visor.

using System.Collections;
using TMPro;
using UnityEngine;

namespace PujJoycon
{
    public class JoyconSessionCalibrator : MonoBehaviour
    {
        [SerializeField] private RebaPostureEstimator estimator;
        [SerializeField] private JoyconReceiver receiver;
        [Tooltip("Cámara del visor (CenterEyeAnchor), para poner el mensaje delante de los ojos.")]
        [SerializeField] private Transform headReference;
        [Tooltip("Opcional: texto propio donde mostrar las instrucciones.")]
        [SerializeField] private TMP_Text messageText;

        [Header("Panel del Game Master")]
        [Tooltip("Botones de calibración en la pantalla del PC (vista Game), abajo a la izquierda.")]
        [SerializeField] private bool showCalibrateButton = true;
        [Tooltip("En la simulación (hay GameFlowManager) se ocultan al iniciar los recuadros de diagnóstico de " +
                 "JoyconReceiver y RebaPostureEstimator, que tapan el panel del Game Master. Se ven con el botón 'Diagnóstico'.")]
        [SerializeField] private bool ocultarDiagnosticoEnSimulacion = true;

        [Header("Calibración completa")]
        [SerializeField] private float countdownSeconds = 5f;
        [SerializeField] private float waitForDataSeconds = 15f;
        [SerializeField, TextArea] private string poseInstructions =
            "Calibración de sensores\n\nPárese derecho, mirando al frente,\ncon los brazos relajados a los lados\ny las palmas hacia los muslos.";

        [Header("Paso 2: sentadilla (montaje de cada Joy-Con)")]
        [SerializeField] private bool calibrateMountingWithSquat = true;
        [Tooltip("Tiempo para bajar a la sentadilla.")]
        [SerializeField] private float squatPrepareSeconds = 4f;
        [Tooltip("Tiempo quieto en la sentadilla antes de medir.")]
        [SerializeField] private float squatHoldSeconds = 2f;
        [SerializeField, TextArea] private string squatInstructions =
            "Ahora la sentadilla:\n\nDoble las rodillas, incline un poco\nel tronco hacia adelante y estire\nlos brazos al frente.";

        [Header("Paso 3: volver a la postura N")]
        [Tooltip("Después de la sentadilla, vuelve a tomar el 'cero' en postura N. Absorbe lo que se hayan " +
                 "corrido los Joy-Cons sobre la piel en la sentadilla (si no, el avatar puede quedar con las " +
                 "piernas cruzadas o torcido).")]
        [SerializeField] private bool recaptureNPoseAfterSquat = true;
        [SerializeField] private float finalPoseSeconds = 4f;

        [Header("Recalibración rápida (al empezar cada actividad)")]
        [Tooltip("Vuelve a tomar solo el 'cero' en postura N (sin sentadilla: el montaje no cambia). Así lo que " +
                 "se corran los Joy-Cons sobre la piel no se acumula de una actividad a otra.")]
        [SerializeField] private float quickPoseSeconds = 4f;

        private Coroutine _routine;
        private GameObject _runtimeMessage;
        private string _step = "";

        // Seguimiento de la sesión, solo para los avisos del panel (nada se lanza solo).
        private bool _sessionWasRunning;
        private bool _fullDoneThisSession;
        private RebaSessionRecorder _recorder;

        public bool IsCalibrating => _routine != null;

        /// <summary>Ya se hizo la calibración completa (N + sentadilla) en la sesión actual.</summary>
        public bool CalibradoEnEstaSesion => _fullDoneThisSession;

        /// <summary>Cuántas calibraciones (rápidas o completas) terminaron bien. El grabador la usa para
        /// empezar cada momento (pre/tutorial/post) solo después de una calibración.</summary>
        public int CalibracionesTerminadas { get; private set; }

        private void Awake()
        {
            if (estimator == null) estimator = FindAnyObjectByType<RebaPostureEstimator>();
            if (receiver == null) receiver = FindAnyObjectByType<JoyconReceiver>();
            _recorder = FindAnyObjectByType<RebaSessionRecorder>();
        }

        private void Start()
        {
            if (ocultarDiagnosticoEnSimulacion && GameFlowManager.Instance != null) SetDiagnostico(false);
        }

        private bool DiagnosticoVisible => receiver != null && receiver.ShowOverlay;

        private void SetDiagnostico(bool visible)
        {
            if (receiver != null) receiver.ShowOverlay = visible;
            if (estimator != null) estimator.ShowOverlay = visible;
        }

        private void Update()
        {
            var flow = GameFlowManager.Instance;
            bool running = flow != null && flow.simulacionEnCurso;
            if (running != _sessionWasRunning) _fullDoneThisSession = false; // participante nuevo: falta su calibración
            _sessionWasRunning = running;
        }

        /// <summary>Botón del Game Master: calibración completa (postura N + sentadilla + postura N).</summary>
        [ContextMenu("Calibrar ahora")]
        public void Calibrar() => StartCalibration(0f);

        /// <summary>Detiene la calibración en curso (se queda la calibración anterior, si había).</summary>
        public void CancelarCalibracion()
        {
            if (_routine == null) return;
            StopCoroutine(_routine);
            _routine = null;
            _step = "";
            HideMessage();
            Debug.Log("[Joycon] Calibración cancelada por el Game Master.", this);
        }

        /// <summary>Solo vuelve a tomar el "cero" en postura N (sin sentadilla). Requiere una calibración completa antes.</summary>
        [ContextMenu("Recalibrar rápido (solo postura N)")]
        public void RecalibrarRapido()
        {
            if (estimator == null || !estimator.IsCalibrated)
            {
                Debug.LogWarning("[Joycon] Recalibración rápida: primero haga una calibración completa.", this);
                return;
            }
            StartQuickRecalibration(0f);
        }

        private void StartQuickRecalibration(float delay)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(QuickRoutine(delay));
        }

        private IEnumerator QuickRoutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            _step = "recalibración rápida: postura N";
            for (float left = quickPoseSeconds; left > 0f; left -= Time.unscaledDeltaTime)
            {
                ShowMessage($"Recalibración rápida\n\n{poseInstructions}\n\nQuédese quieto: {Mathf.CeilToInt(left)}");
                yield return null;
            }
            estimator.Calibrate(); // mismo montaje, nuevo "cero" (y nueva postura del avatar)
            if (estimator.IsCalibrated) CalibracionesTerminadas++;
            ShowMessage(estimator.IsCalibrated ? "¡Listo! Ya puede moverse." : "No se pudo recalibrar.\nAvise al encargado.");
            _step = "terminando";
            yield return new WaitForSeconds(1.5f);
            HideMessage();
            _step = "";
            _routine = null;
        }

        private void StartCalibration(float delay)
        {
            if (estimator == null)
            {
                Debug.LogError("[Joycon] JoyconSessionCalibrator no tiene RebaPostureEstimator.", this);
                return;
            }
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(CalibrationRoutine(delay));
        }

        private IEnumerator CalibrationRoutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            _step = "esperando datos";
            float waited = 0f;
            while (receiver != null && !receiver.IsReceiving && waited < waitForDataSeconds)
            {
                ShowMessage("Calibración de sensores\n\nEsperando los Joy-Cons...");
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (receiver != null && !receiver.IsReceiving)
            {
                Debug.LogError("[Joycon] No llegan datos de los Joy-Cons: no se pudo calibrar. Revise los servidores.", this);
                yield return Finish("No llegan datos de los Joy-Cons.\nAvise al encargado.", 4f);
                yield break;
            }

            _step = "1/3 postura N";
            for (float left = countdownSeconds; left > 0f; left -= Time.unscaledDeltaTime)
            {
                ShowMessage($"{poseInstructions}\n\nQuédese quieto: {Mathf.CeilToInt(left)}");
                yield return null;
            }

            estimator.Calibrate();
            if (!estimator.IsCalibrated)
            {
                yield return Finish("No se pudo calibrar.\nAvise al encargado.", 3f);
                yield break;
            }

            if (calibrateMountingWithSquat)
            {
                _step = "2/3 sentadilla";
                for (float left = squatPrepareSeconds; left > 0f; left -= Time.unscaledDeltaTime)
                {
                    ShowMessage($"{squatInstructions}\n\nBájese: {Mathf.CeilToInt(left)}");
                    yield return null;
                }
                for (float left = squatHoldSeconds; left > 0f; left -= Time.unscaledDeltaTime)
                {
                    ShowMessage($"{squatInstructions}\n\n¡Quieto así! {Mathf.CeilToInt(left)}");
                    yield return null;
                }

                if (!estimator.CalibrateMountingFromSquat())
                {
                    Debug.LogWarning("[Joycon] Sentadilla: algunos sensores no se calibraron. " + estimator.LastMountingReport, this);
                    yield return Finish("Algunos sensores no se calibraron:\n\n" + estimator.LastMountingReport +
                                        "\n\nVuelva a pararse derecho y avise al encargado.", 6f);
                    yield break;
                }

                if (recaptureNPoseAfterSquat)
                {
                    _step = "3/3 postura N";
                    for (float left = finalPoseSeconds; left > 0f; left -= Time.unscaledDeltaTime)
                    {
                        ShowMessage($"Último paso: vuelva a la postura inicial.\n\n{poseInstructions}\n\nQuédese quieto: {Mathf.CeilToInt(left)}");
                        yield return null;
                    }
                    // Nuevo "cero" con el montaje ya calculado (también vuelve a tomar la postura del avatar).
                    estimator.Calibrate();
                }
            }

            _fullDoneThisSession = true;
            CalibracionesTerminadas++; // también vale como recalibración del momento actual
            yield return Finish("¡Listo! Ya puede moverse.", 2.5f);
        }

        private IEnumerator Finish(string message, float seconds)
        {
            _step = "terminando";
            ShowMessage(message);
            yield return new WaitForSeconds(seconds);
            HideMessage();
            _step = "";
            _routine = null;
        }

        // ------------------------------------------------------------------
        // Panel del Game Master (pantalla del PC, abajo a la izquierda)
        // ------------------------------------------------------------------

        private GUIStyle _warnStyle, _okStyle;

        private void OnGUI()
        {
            if (!showCalibrateButton) return;
            if (_warnStyle == null)
            {
                _warnStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true };
                _warnStyle.normal.textColor = new Color(1f, 0.75f, 0.2f);
                _okStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
                _okStyle.normal.textColor = new Color(0.55f, 0.95f, 0.55f);
            }

            // Angosto, para no tapar los botones del panel del Game Master (p. ej. "Volver").
            const float w = 330f, h = 165f;
            GUILayout.BeginArea(new Rect(10, Screen.height - h - 10, w, h), GUI.skin.box);

            // Estado de los servidores en una línea (el detalle está en "Diagnóstico").
            GUILayout.BeginHorizontal();
            GUILayout.Label("Joy-Cons:");
            if (receiver != null)
            {
                for (int s = 0; s < receiver.ServerCount; s++)
                {
                    bool ok = receiver.GetServerStatus(s).IsReceiving;
                    GUILayout.Label(ok ? $"PC {s + 1} OK" : $"PC {s + 1} SIN DATOS", ok ? _okStyle : _warnStyle);
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(DiagnosticoVisible ? "Ocultar diagnóstico" : "Diagnóstico")) SetDiagnostico(!DiagnosticoVisible);
            GUILayout.EndHorizontal();

            if (_routine != null)
            {
                GUILayout.Label($"Calibrando… {_step}", _okStyle);
                if (GUILayout.Button("Cancelar calibración", GUILayout.Height(30))) CancelarCalibracion();
                GUILayout.EndArea();
                return;
            }

            var flow = GameFlowManager.Instance;
            bool running = flow != null && flow.simulacionEnCurso;
            if (running && !_fullDoneThisSession)
                GUILayout.Label("Falta la CALIBRACIÓN COMPLETA de este participante.", _warnStyle);
            else if (running && _recorder != null && _recorder.EsperandoCalibracion)
                GUILayout.Label($"{_recorder.MomentoPendiente}: haga la RECALIBRACIÓN RÁPIDA para empezar a grabar.", _warnStyle);
            else if (running)
                GUILayout.Label("Calibrado. Puede seguir.", _okStyle);
            else
                GUILayout.Label(estimator != null && estimator.IsCalibrated ? "Sin sesión (calibrado)." : "Sin sesión (sin calibrar).");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Calibración completa\n(N + sentadilla)", GUILayout.Height(40))) Calibrar();
            GUI.enabled = estimator != null && estimator.IsCalibrated;
            if (GUILayout.Button("Recalibración rápida\n(postura N)", GUILayout.Height(40))) RecalibrarRapido();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void ShowMessage(string text)
        {
            if (messageText != null)
            {
                messageText.gameObject.SetActive(true);
                messageText.text = text;
                return;
            }
            if (_runtimeMessage == null) _runtimeMessage = CreateRuntimeMessage();
            if (_runtimeMessage == null) return;
            _runtimeMessage.SetActive(true);
            _runtimeMessage.GetComponent<TextMeshPro>().text = text;
        }

        private void HideMessage()
        {
            if (messageText != null) messageText.gameObject.SetActive(false);
            if (_runtimeMessage != null) _runtimeMessage.SetActive(false);
        }

        private GameObject CreateRuntimeMessage()
        {
            Transform parent = headReference != null ? headReference : (Camera.main != null ? Camera.main.transform : null);
            if (parent == null) return null;

            var go = new GameObject("MensajeCalibracionJoycons");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, -0.05f, 1.2f);
            go.transform.localRotation = Quaternion.identity;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = new Vector2(1.0f, 0.55f);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = 2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            return go;
        }

        private void OnDestroy()
        {
            if (_runtimeMessage != null) Destroy(_runtimeMessage);
        }
    }
}
