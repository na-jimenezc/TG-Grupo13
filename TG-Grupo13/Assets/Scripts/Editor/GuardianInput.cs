using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Oculus.Interaction;

public class GuardianInput : MonoBehaviour
{
    private void OnEnable() => SceneManager.sceneLoaded += Validar;

    private void Validar(Scene escena, LoadSceneMode modo)
    {
        var eventSystems = FindObjectsOfType<EventSystem>();
        var modules = FindObjectsOfType<PointableCanvasModule>();

        if (eventSystems.Length > 1)
            Debug.LogError($"[InputGuard] {eventSystems.Length} EventSystem detectados tras cargar '{escena.name}'. Debe haber solo 1.");

        if (modules.Length > 1)
            Debug.LogError($"[InputGuard] {modules.Length} PointableCanvasModule detectados tras cargar '{escena.name}'. Debe haber solo 1.");
    }
}