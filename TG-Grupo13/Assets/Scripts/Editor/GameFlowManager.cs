using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Estado Actual")]
    public UserProfile usuarioActivo;
    public bool simulacionEnCurso = false;

    [Header("Referencias de la Escena")]
    [SerializeField] private GameObject waitScreenCanvas;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (!simulacionEnCurso && waitScreenCanvas != null)
        {
            waitScreenCanvas.SetActive(true);
        }
    }

    public void IniciarSesionConUsuario(UserProfile usuario)
    {
        usuarioActivo = usuario;
        Debug.Log($"[GameFlowManager] Sesión iniciada: {usuario.Nombre} | Admin: {usuario.EsAdministrador}");

        if (!usuario.EsAdministrador)
        {
            LanzarSimulacionVR();
        }
    }

    public void LanzarSimulacionVR()
    {
        simulacionEnCurso = true;
        Debug.Log("[GameFlowManager] Iniciando simulación VR en el invernadero...");

        if (waitScreenCanvas != null)
        {
            waitScreenCanvas.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[GameFlowManager] No se asignó el WaitScreenCanvas en el Inspector.");
        }
    }

    public void CerrarSesion()
    {
        usuarioActivo = null;
        simulacionEnCurso = false;

        if (waitScreenCanvas != null)
        {
            waitScreenCanvas.SetActive(true);
        }
    }
}