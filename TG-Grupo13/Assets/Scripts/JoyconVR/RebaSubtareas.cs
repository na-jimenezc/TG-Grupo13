// RebaSubtareas.cs
//
// Dice en qué subtarea está la persona en cada momento, para etiquetar cada muestra de REBA
// (columna "subtarea") y comparar pre / tutorial / post por subtarea. Usa LA MISMA REGLA en los
// tres momentos, así "Cavar hueco" significa lo mismo antes, durante y después del tutorial:
//
//   1. Lo que tiene en la mano (Grabbable de Meta) dice qué está haciendo:
//        Siembra   bulbo -> Sembrar bulbo · canasta -> Esparcir bulbos
//                  pala junto a un hueco sin cavar -> Cavar hueco (llevar la pala no es cavar);
//                  pala en una mano y la otra cerca de un bulbo -> Tomar bulbo
//        Deshierbe ortiga aún en la tierra -> Arrancar ortiga · ortiga ya arrancada -> Dejar ortiga
//        Corte     tijeras junto a un tallo plantado -> Cortar tallo; luego, según los avisos de los objetos,
//                  Deshojar tallo / Cortar parejo · capuchón -> Armar ramo · ramo terminado -> Depositar ramo
//   2. Si no tiene nada de eso pero una mano está cerca (distanciaTomar) de un objeto que se puede agarrar:
//        Tomar canasta / Tomar pala / Tomar bulbo / Tomar bolsa / Tomar ortiga / Tomar tijeras
//   3. Si no: "" (sin tarea: camina hacia la siguiente planta, mira alrededor...).
//
// Los nombres son los mismos de los pasos del tutorial. Aparte, en el tutorial se guarda el paso
// que muestra la pantalla (PasoTutorial), como referencia.
//
// No modifica los scripts del equipo: solo lee su estado y escucha sus eventos.

using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace PujJoycon
{
    public class RebaSubtareas : MonoBehaviour
    {
        // Pasos de cada tutorial, en el orden de los TutorialStepData de su escena.
        private static readonly string[] PasosSiembra =
            { "Tomar canasta", "Esparcir bulbos", "Tomar pala", "Tomar bulbo", "Cavar hueco", "Sembrar bulbo" };
        private static readonly string[] PasosDeshierbe =
            { "Tomar bolsa", "Tomar ortiga", "Arrancar ortiga", "Dejar ortiga" };
        private static readonly string[] PasosCorte =
            { "Tomar tijeras", "Cortar tallo", "Deshojar tallo", "Cortar parejo", "Armar ramo", "Depositar ramo" };

        private const string CortarTallo = "Cortar tallo", DeshojarTallo = "Deshojar tallo", CortarParejo = "Cortar parejo",
                             ArmarRamo = "Armar ramo", DepositarRamo = "Depositar ramo";

        [Tooltip("Distancia (m) de una mano a un objeto para considerar que lo está tomando.")]
        [SerializeField] private float distanciaTomar = 0.5f;
        [Tooltip("Distancia (m) de la herramienta (pala, tijeras) a un hueco o tallo para considerar que lo está trabajando.")]
        [SerializeField] private float distanciaHerramienta = 0.35f;
        [Tooltip("Cada cuánto se vuelven a buscar los objetos de la actividad (aparecen bulbos nuevos al esparcir).")]
        [SerializeField] private float segundosEntreBusquedas = 1f;

        private readonly List<(Component objeto, Grabbable agarre)> _objetos = new List<(Component, Grabbable)>();
        private readonly List<PlantingSlot> _huecos = new List<PlantingSlot>();
        private readonly List<TalloEnPlantacion> _tallos = new List<TalloEnPlantacion>();
        private float _proximaBusqueda;
        private string _faseCorte = CortarTallo;
        private Transform _manoIzq, _manoDer;

        private void OnEnable()
        {
            TalloEnPlantacion.OnCortado += AlCortarTallo;
            TalloCortado.OnDeshojado += AlDeshojar;
            RamoEnProgreso.OnNivelado += AlNivelar;
            Capuchon.OnEnsamblado += AlEnsamblar;
            RamoTerminado.OnDepositadoEnMalla += AlDepositarRamo;
        }

        private void OnDisable()
        {
            TalloEnPlantacion.OnCortado -= AlCortarTallo;
            TalloCortado.OnDeshojado -= AlDeshojar;
            RamoEnProgreso.OnNivelado -= AlNivelar;
            Capuchon.OnEnsamblado -= AlEnsamblar;
            RamoTerminado.OnDepositadoEnMalla -= AlDepositarRamo;
        }

        private void AlCortarTallo() => _faseCorte = DeshojarTallo;
        private void AlDeshojar() => _faseCorte = RamoLleno() ? CortarParejo : CortarTallo;
        private void AlNivelar() => _faseCorte = ArmarRamo;
        private void AlEnsamblar() => _faseCorte = DepositarRamo;
        private void AlDepositarRamo() => _faseCorte = CortarTallo;

        private static bool RamoLleno()
        {
            var corte = CorteActivityManager.Instance;
            return corte != null && corte.TallosEnRamoActual >= corte.TallosPorRamo;
        }

        /// <summary>Se llama al empezar cada momento (pre, tutorial, post): cada escena empieza de cero.</summary>
        public void Reiniciar()
        {
            _faseCorte = CortarTallo;
            _proximaBusqueda = 0f;
            _objetos.Clear();
        }

        /// <summary>Subtarea actual ("" = sin tarea). Misma regla en pre, tutorial y post.</summary>
        public string Subtarea(ActivityType? actividad)
        {
            if (actividad == null) return "";
            ActualizarObjetos();
            switch (actividad.Value)
            {
                case ActivityType.Siembra: return SubtareaSiembra();
                case ActivityType.Deshierbe: return SubtareaDeshierbe();
                case ActivityType.Corte: return SubtareaCorte();
                default: return "";
            }
        }

        /// <summary>Paso que muestra el tutorial ("" fuera del tutorial).</summary>
        public string PasoTutorial(FaseSesion fase, ActivityType? actividad)
        {
            if (fase != FaseSesion.Tutorial || actividad == null) return "";
            var tutorial = TutorialManagerBase.Actual;
            if (tutorial == null) return "";
            string[] pasos = actividad == ActivityType.Siembra ? PasosSiembra
                           : actividad == ActivityType.Deshierbe ? PasosDeshierbe : PasosCorte;
            int i = tutorial.PasoActual;
            return i >= 0 && i < pasos.Length ? pasos[i] : $"Paso {i + 1}";
        }

        /// <summary>Cuántas unidades lleva: bulbos sembrados, ortigas depositadas o tallos procesados.</summary>
        public int AccionesCompletadas(ActivityType? actividad)
        {
            switch (actividad)
            {
                case ActivityType.Siembra:
                    return SiembraActivityManager.Instance != null ? SiembraActivityManager.Instance.PlantasSembradas : 0;
                case ActivityType.Deshierbe:
                    return DeshierbeActivityManager.Instance != null ? DeshierbeActivityManager.Instance.OrtigasRecolectadas : 0;
                case ActivityType.Corte:
                    var c = CorteActivityManager.Instance;
                    return c != null ? c.RamosCompletados * c.TallosPorRamo + c.TallosEnRamoActual : 0;
                default:
                    return 0;
            }
        }

        private string SubtareaSiembra()
        {
            if (Agarrado<Bulbo>(out _)) return "Sembrar bulbo";
            if (Agarrado<Canasta>(out _)) return "Esparcir bulbos";
            string tomar = Cercano(("Tomar bulbo", typeof(Bulbo)), ("Tomar pala", typeof(Pala)), ("Tomar canasta", typeof(Canasta)));
            if (Agarrado<Pala>(out var pala))
            {
                // Tener la pala no es cavar: solo cuenta como "Cavar hueco" junto a un hueco sin cavar.
                // Con la pala en una mano y la otra cerca de un bulbo, está recogiendo el bulbo.
                if (tomar == "Tomar bulbo") return tomar;
                return CercaDeHuecoSinCavar(pala) ? "Cavar hueco" : "";
            }
            return tomar;
        }

        private bool CercaDeHuecoSinCavar(Component pala)
        {
            foreach (var hueco in _huecos)
            {
                if (hueco != null && hueco.Estado == EstadoSlot.Vacio && Distancia(pala, hueco.transform.position) <= distanciaHerramienta)
                    return true;
            }
            return false;
        }

        private string SubtareaDeshierbe()
        {
            if (Agarrado<Ortiga>(out var ortiga)) return ortiga.EstaExtraida ? "Dejar ortiga" : "Arrancar ortiga";
            return Cercano(("Tomar ortiga", typeof(Ortiga)), ("Tomar bolsa", typeof(Bolsa)));
        }

        private string SubtareaCorte()
        {
            if (Agarrado<RamoTerminado>(out _)) return DepositarRamo;
            if (Agarrado<Capuchon>(out _)) return ArmarRamo;
            if (Agarrado<Tijeras>(out var tijeras))
            {
                // Tener las tijeras no es cortar: "Cortar tallo" solo junto a un tallo plantado.
                if (_faseCorte != CortarTallo) return _faseCorte;
                return CercaDeTallo(tijeras) ? CortarTallo : "";
            }
            return Cercano(("Tomar tijeras", typeof(Tijeras)), (DepositarRamo, typeof(RamoTerminado)), (ArmarRamo, typeof(Capuchon)));
        }

        private bool CercaDeTallo(Component tijeras)
        {
            Vector3 centro = Centro(tijeras);
            foreach (var tallo in _tallos)
            {
                if (tallo != null && tallo.isActiveAndEnabled && Distancia(tallo, centro) <= distanciaHerramienta) return true;
            }
            return false;
        }

        // Distancia de un punto al objeto (a sus colliders si tiene; si no, a su posición).
        private static float Distancia(Component objeto, Vector3 punto)
        {
            float mejor = float.MaxValue;
            foreach (var c in objeto.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled) continue;
                mejor = Mathf.Min(mejor, Vector3.Distance(c.bounds.ClosestPoint(punto), punto));
            }
            return mejor < float.MaxValue ? mejor : Vector3.Distance(objeto.transform.position, punto);
        }

        private static Vector3 Centro(Component objeto)
        {
            foreach (var c in objeto.GetComponentsInChildren<Collider>())
                if (c.enabled) return c.bounds.center;
            return objeto.transform.position;
        }

        private bool Agarrado<T>(out T objeto) where T : Component
        {
            foreach (var (o, agarre) in _objetos)
            {
                if (o is T t && o != null && agarre != null && agarre.SelectingPointsCount > 0)
                {
                    objeto = t;
                    return true;
                }
            }
            objeto = null;
            return false;
        }

        // El objeto que se puede agarrar más cercano a una de las manos (sin contar los que ya tiene en la mano
        // ni los que no se pueden agarrar, como los bulbos todavía dentro de la canasta).
        private string Cercano(params (string nombre, System.Type tipo)[] candidatos)
        {
            BuscarManos();
            if (_manoIzq == null && _manoDer == null) return "";

            string mejor = "";
            float mejorDistancia = distanciaTomar;
            foreach (var (o, agarre) in _objetos)
            {
                if (o == null || agarre == null || !agarre.isActiveAndEnabled || agarre.SelectingPointsCount > 0) continue;
                foreach (var (nombre, tipo) in candidatos)
                {
                    if (!tipo.IsInstanceOfType(o)) continue;
                    Vector3 p = o.transform.position;
                    float d = Mathf.Min(_manoIzq != null ? Vector3.Distance(p, _manoIzq.position) : float.MaxValue,
                                        _manoDer != null ? Vector3.Distance(p, _manoDer.position) : float.MaxValue);
                    if (d < mejorDistancia)
                    {
                        mejorDistancia = d;
                        mejor = nombre;
                    }
                }
            }
            return mejor;
        }

        // Los controles del visor (LeftHandAnchor / RightHandAnchor del Camera Rig).
        private void BuscarManos()
        {
            if (_manoIzq != null && _manoDer != null) return;
            var camara = Camera.main;
            if (camara == null) return;
            foreach (var t in camara.transform.root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "LeftHandAnchor") _manoIzq = t;
                else if (t.name == "RightHandAnchor") _manoDer = t;
            }
        }

        // Los objetos cambian (bulbos que aparecen, ortigas que se destruyen, escenas que se cargan):
        // se vuelven a buscar cada segundo.
        private void ActualizarObjetos()
        {
            if (Time.unscaledTime < _proximaBusqueda) return;
            _proximaBusqueda = Time.unscaledTime + segundosEntreBusquedas;

            _objetos.Clear();
            Buscar<Bulbo>();
            Buscar<Pala>();
            Buscar<Canasta>();
            Buscar<Ortiga>();
            Buscar<Bolsa>();
            Buscar<Tijeras>();
            Buscar<Capuchon>();
            Buscar<RamoTerminado>();

            _huecos.Clear();
            _huecos.AddRange(FindObjectsByType<PlantingSlot>(FindObjectsSortMode.None));
            _tallos.Clear();
            _tallos.AddRange(FindObjectsByType<TalloEnPlantacion>(FindObjectsSortMode.None));
        }

        private void Buscar<T>() where T : Component
        {
            foreach (var o in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                var agarre = o.GetComponent<Grabbable>();
                if (agarre != null) _objetos.Add((o, agarre));
            }
        }
    }
}
