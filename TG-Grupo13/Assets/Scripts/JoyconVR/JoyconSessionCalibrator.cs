// JoyconSessionCalibrator.cs
//
// Calibra los Joy-Cons (RebaPostureEstimator -> y con eso el avatar,
// JoyconAvatarDriver) al empezar cada sesión, con instrucciones en el visor:
//
//   1. Cuando GameFlowManager pasa a simulacionEnCurso = true (el Game Master
//      inició la sesión), espera unos segundos a que carguen las escenas.
//   2. Espera a que lleguen datos de los Joy-Cons.
//   3. Pide la postura N con cuenta regresiva y calibra (el "cero" de cada Joy-Con).
//   4. Pide la sentadilla con brazos al frente y calcula cómo está puesto cada
//      Joy-Con en el cuerpo (su montaje), para que los ejes salgan bien.
//
// También se puede lanzar a mano: Calibrar() desde un botón del Game Master, o
// el menú ⋮ del componente -> "Calibrar ahora".
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

        [Header("Cuándo")]
        [SerializeField] private bool calibrateOnSessionStart = true;
        [Tooltip("Botón 'Calibrar Joy-Cons' en la pantalla del PC (vista Game), abajo a la izquierda.")]
        [SerializeField] private bool showCalibrateButton = true;
        [Tooltip("Espera tras iniciar la sesión, para que carguen las escenas y la persona se ubique.")]
        [SerializeField] private float delayAfterSessionStart = 3f;

        [Header("Cómo")]
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

        private bool _sessionWasRunning;
        private Coroutine _routine;
        private GameObject _runtimeMessage;

        public bool IsCalibrating => _routine != null;

        private void Awake()
        {
            if (estimator == null) estimator = FindAnyObjectByType<RebaPostureEstimator>();
            if (receiver == null) receiver = FindAnyObjectByType<JoyconReceiver>();
        }

        private void Update()
        {
            if (!calibrateOnSessionStart) return;
            var flow = GameFlowManager.Instance;
            bool running = flow != null && flow.simulacionEnCurso;
            if (running && !_sessionWasRunning) StartCalibration(delayAfterSessionStart);
            _sessionWasRunning = running;
        }

        /// <summary>Para un botón del Game Master: calibra ya (con cuenta regresiva).</summary>
        [ContextMenu("Calibrar ahora")]
        public void Calibrar() => StartCalibration(0f);

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
                ShowMessage("No llegan datos de los Joy-Cons.\nAvise al encargado.");
                yield return new WaitForSeconds(4f);
                HideMessage();
                _routine = null;
                yield break;
            }

            for (float left = countdownSeconds; left > 0f; left -= Time.unscaledDeltaTime)
            {
                ShowMessage($"{poseInstructions}\n\nQuédese quieto: {Mathf.CeilToInt(left)}");
                yield return null;
            }

            estimator.Calibrate();
            if (!estimator.IsCalibrated)
            {
                ShowMessage("No se pudo calibrar.\nAvise al encargado.");
                yield return new WaitForSeconds(3f);
                HideMessage();
                _routine = null;
                yield break;
            }

            if (calibrateMountingWithSquat)
            {
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
                    ShowMessage("Algunos sensores no se calibraron:\n\n" + estimator.LastMountingReport +
                                "\n\nVuelva a pararse derecho y avise al encargado.");
                    yield return new WaitForSeconds(6f);
                    HideMessage();
                    _routine = null;
                    yield break;
                }
            }

            ShowMessage(calibrateMountingWithSquat ? "¡Listo! Vuelva a pararse derecho.\nYa puede moverse." : "¡Listo! Ya puede moverse.");
            yield return new WaitForSeconds(2.5f);
            HideMessage();
            _routine = null;
        }

        private void OnGUI()
        {
            if (!showCalibrateButton) return;
            var area = new Rect(10, Screen.height - 50, 220, 40);
            if (_routine != null)
            {
                GUI.Box(area, "Calibrando...");
            }
            else if (GUI.Button(area, "Calibrar Joy-Cons"))
            {
                Calibrar();
            }
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
