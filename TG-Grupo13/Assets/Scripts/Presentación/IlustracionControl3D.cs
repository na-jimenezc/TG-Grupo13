using System.Collections.Generic;
using UnityEngine;

/* Va en la raíz del prefab de cada ilustración 3D de controles (pose de Blender: mano + control de Quest).
// Reemplaza las imágenes de referencia: hace que el modelo oscile suavemente para que el usuario lo vea
// en 3D y hace "latir" los botones que debe presionar (grab, trigger, joystick...).
//
// No hace falta separar los botones en Blender: el control viene como una sola malla, pero cada botón
// está pegado a su propio hueso (right_b_thumbstick, right_b_trigger_grip, ...). Al iniciar, este script
// busca los triángulos de esos huesos, los pasa a un submesh aparte y les pone un material resaltado.
// Funciona igual con las poses de mano izquierda, que usan el mismo control espejado.
//
// IMPORTANTE: en el FBX (pestaña Model) debe estar activado "Read/Write", si no en las gafas no se puede leer la malla.*/
public class IlustracionControl3D : MonoBehaviour
{
    //Botones del control de Quest que se pueden resaltar, se pueden marcar varios a la vez
    [System.Flags]
    public enum BotonControl
    {
        Ninguno = 0,
        Grab = 1 << 0,
        Trigger = 1 << 1,
        Joystick = 1 << 2,
        BotonA = 1 << 3,
        BotonB = 1 << 4,
        BotonMenu = 1 << 5
    }

    //Final del nombre del hueso que exporta Blender para cada botón
    private static readonly Dictionary<BotonControl, string> HuesoPorBoton = new Dictionary<BotonControl, string>
    {
        { BotonControl.Grab, "b_trigger_grip" },
        { BotonControl.Trigger, "b_trigger_front" },
        { BotonControl.Joystick, "b_thumbstick" },
        { BotonControl.BotonA, "b_button_a" },
        { BotonControl.BotonB, "b_button_b" },
        { BotonControl.BotonMenu, "b_button_oculus" }
    };

    [Header("Movimiento")]
    [Tooltip("Grados máximos que el modelo gira a cada lado, siempre en vertical y sobre el centro del modelo (aunque esté inclinado)")]
    [SerializeField] private float anguloOscilacion = 25f;
    [SerializeField] private float velocidadOscilacion = 0.8f;

    [Header("Resaltado de botones")]
    [Tooltip("Botones que debe presionar el usuario en esta pose")]
    [SerializeField] private BotonControl botonesResaltados = BotonControl.Ninguno;
    [SerializeField] private Color colorResaltado = new Color(1f, 0.8f, 0f);
    [SerializeField] private float velocidadPulso = 3f;

    [Header("Mano dominante")]
    [Tooltip("El mismo control pero de la otra mano (p. ej. Ref_Grab_Izq para Ref_Grab_Der). El tutorial lo usa con perfiles zurdos")]
    [SerializeField] private GameObject prefabManoContraria;
    public GameObject PrefabManoContraria => prefabManoContraria;

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private Quaternion _rotacionInicial;
    private Vector3 _posicionInicial;
    private Vector3 _centroEnPadre;
    private readonly List<Material> _materialesResaltados = new List<Material>();
    private readonly List<Color> _coloresOriginales = new List<Color>();

    private void Awake()
    {
        if (botonesResaltados == BotonControl.Ninguno) return;

        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            SepararBotones(smr);

        if (_materialesResaltados.Count == 0)
            Debug.LogWarning($"[IlustracionControl3D] No se encontraron los huesos de {botonesResaltados} en {name}", this);
    }

    // Se calcula en Start para que ya esté ubicado en el punto de la escena donde se instanció
    private void Start()
    {
        _rotacionInicial = transform.localRotation;
        _posicionInicial = transform.localPosition;

        // Centro visual del modelo: el pivote del FBX puede quedar lejos de la mano, por eso se gira sobre el centro real
        var renderers = GetComponentsInChildren<Renderer>();
        Vector3 centro = transform.position;
        if (renderers.Length > 0)
        {
            var limites = renderers[0].bounds;
            foreach (var r in renderers) limites.Encapsulate(r.bounds);
            centro = limites.center;
        }
        _centroEnPadre = transform.parent != null ? transform.parent.InverseTransformPoint(centro) : centro;
    }

    private void Update()
    {
        // Giro sobre el eje vertical del punto donde está el modelo, pasando por su centro visual,
        // así una inclinación en X del modelo (p.ej. para ver el joystick desde arriba) no lo hace girar raro
        float angulo = Mathf.Sin(Time.time * velocidadOscilacion) * anguloOscilacion;
        var giro = Quaternion.Euler(0f, angulo, 0f);
        transform.localRotation = giro * _rotacionInicial;
        transform.localPosition = _centroEnPadre + giro * (_posicionInicial - _centroEnPadre);

        // Pulso entre 0 y 1 para que el botón se ilumine y se apague
        float intensidad = (Mathf.Sin(Time.time * velocidadPulso) + 1f) * 0.5f;
        for (int i = 0; i < _materialesResaltados.Count; i++)
        {
            var mat = _materialesResaltados[i];
            mat.SetColor(EmissionColor, colorResaltado * intensidad * 2f);
            // También se tiñe el color base por si el shader no tiene emisión
            if (TieneColorBase(mat)) mat.color = Color.Lerp(_coloresOriginales[i], colorResaltado, intensidad);
        }
    }

    //Mueve los triángulos de los botones a un submesh nuevo con su propio material
    private void SepararBotones(SkinnedMeshRenderer smr)
    {
        var bones = smr.bones;
        var indicesHuesos = new HashSet<int>();
        for (int b = 0; b < bones.Length; b++)
            if (bones[b] != null && EsHuesoResaltado(bones[b].name)) indicesHuesos.Add(b);
        if (indicesHuesos.Count == 0) return;

        if (!smr.sharedMesh.isReadable)
        {
            Debug.LogError($"[IlustracionControl3D] Activa Read/Write en el FBX de {smr.sharedMesh.name} para poder resaltar botones", this);
            return;
        }

        var mesh = Instantiate(smr.sharedMesh);
        // boneIndex0 es el hueso con más peso del vértice (Unity los ordena de mayor a menor)
        var pesos = mesh.boneWeights;
        var materiales = new List<Material>(smr.sharedMaterials);
        var trianglesBoton = new List<int>();

        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            var tris = mesh.GetTriangles(s);
            var restantes = new List<int>(tris.Length);
            for (int t = 0; t < tris.Length; t += 3)
            {
                bool esBoton = indicesHuesos.Contains(pesos[tris[t]].boneIndex0)
                            && indicesHuesos.Contains(pesos[tris[t + 1]].boneIndex0)
                            && indicesHuesos.Contains(pesos[tris[t + 2]].boneIndex0);
                var destino = esBoton ? trianglesBoton : restantes;
                destino.Add(tris[t]); destino.Add(tris[t + 1]); destino.Add(tris[t + 2]);
            }
            mesh.SetTriangles(restantes, s);
        }
        if (trianglesBoton.Count == 0) return;

        mesh.subMeshCount += 1;
        mesh.SetTriangles(trianglesBoton, mesh.subMeshCount - 1);
        smr.sharedMesh = mesh;

        // El material resaltado es una copia del material del control, con emisión activada
        var matResaltado = new Material(materiales[0]) { name = materiales[0].name + "_Resaltado" };
        matResaltado.EnableKeyword("_EMISSION");
        matResaltado.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        materiales.Add(matResaltado);
        smr.sharedMaterials = materiales.ToArray();

        _materialesResaltados.Add(matResaltado);
        _coloresOriginales.Add(TieneColorBase(matResaltado) ? matResaltado.color : Color.white);
    }

    private static bool TieneColorBase(Material mat) => mat.HasProperty("_BaseColor") || mat.HasProperty("_Color");

    private bool EsHuesoResaltado(string nombreHueso)
    {
        foreach (var par in HuesoPorBoton)
            if ((botonesResaltados & par.Key) != 0 && nombreHueso.EndsWith(par.Value)) return true;
        return false;
    }
}
