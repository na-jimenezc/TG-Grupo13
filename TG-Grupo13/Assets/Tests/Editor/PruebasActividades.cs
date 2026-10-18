using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/*Fase 3: pruebas de los managers de actividad (metas, conteo y progreso).
 * En EditMode Unity no ejecuta Awake/OnDestroy de los MonoBehaviour, así que se llaman por reflexión;
 * con eso se prueban sin entrar a PlayMode ni crear un .asmdef para los scripts del juego.*/
public abstract class PruebasManagerBase
{
    private GameObject _objeto;

    //Crea el componente, ajusta sus campos privados (metas) y ejecuta su Awake como lo haría Unity
    protected T Crear<T>(params (string campo, object valor)[] campos) where T : MonoBehaviour
    {
        _objeto = new GameObject("Prueba_" + typeof(T).Name);
        var componente = _objeto.AddComponent<T>();
        foreach (var (campo, valor) in campos)
            Campo(typeof(T), campo).SetValue(componente, valor);

        Metodo(typeof(T), "Awake")?.Invoke(componente, null);
        return componente;
    }

    [TearDown]
    public void Destruir()
    {
        if (_objeto == null) return;
        //OnDestroy limpia los Instance/Current estáticos para que no pasen a la siguiente prueba
        foreach (var componente in _objeto.GetComponents<MonoBehaviour>())
            Metodo(componente.GetType(), "OnDestroy")?.Invoke(componente, null);
        Object.DestroyImmediate(_objeto);
    }

    private static FieldInfo Campo(System.Type tipo, string nombre)
    {
        for (var t = tipo; t != null; t = t.BaseType)
        {
            var campo = t.GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (campo != null) return campo;
        }
        throw new System.ArgumentException($"{tipo.Name} no tiene el campo '{nombre}'");
    }

    private static MethodInfo Metodo(System.Type tipo, string nombre) =>
        tipo.GetMethod(nombre, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
}

public class PruebasSiembra : PruebasManagerBase
{
    [Test]
    public void S1_SeCompletaJustoAlLlegarALaMeta()
    {
        var manager = Crear<SiembraActivityManager>(("metaPlantas", 3));
        int eventos = 0;
        manager.OnActividadCompletada += () => eventos++;

        manager.RegistrarPlantaSembrada();
        manager.RegistrarPlantaSembrada();
        Assert.IsFalse(manager.ActividadCompletada);

        manager.RegistrarPlantaSembrada();
        Assert.IsTrue(manager.ActividadCompletada);
        Assert.AreEqual(1, eventos, "El evento de completada se dispara una sola vez");
    }

    [Test]
    public void S2_NoCuentaDeMasDespuesDeLaMeta()
    {
        var manager = Crear<SiembraActivityManager>(("metaPlantas", 2));
        for (int i = 0; i < 5; i++) manager.RegistrarPlantaSembrada();

        Assert.AreEqual(2, manager.PlantasSembradas);
    }

    [Test]
    public void S3_ProgresoProporcional()
    {
        var manager = Crear<SiembraActivityManager>(("metaPlantas", 4));
        manager.RegistrarPlantaSembrada();

        Assert.AreEqual(0.25f, manager.FraccionProgreso, 0.0001f);
        Assert.AreEqual("Plantas sembradas: 1/4", manager.ResumenProgreso);
    }

    [Test]
    public void S4_SeRegistraComoManagerActual()
    {
        var manager = Crear<SiembraActivityManager>();
        Assert.AreSame(manager, ActivityManagerBase.Current);
        Assert.AreSame(manager, SiembraActivityManager.Instance);
    }
}

public class PruebasDeshierbe : PruebasManagerBase
{
    [Test]
    public void H1_SeCompletaJustoAlLlegarALaMeta()
    {
        var manager = Crear<DeshierbeActivityManager>(("metaOrtigas", 3));
        int eventos = 0;
        manager.OnActividadCompletada += () => eventos++;

        manager.RegistrarOrtigaDepositada();
        manager.RegistrarOrtigaDepositada();
        Assert.IsFalse(manager.ActividadCompletada);

        manager.RegistrarOrtigaDepositada();
        Assert.IsTrue(manager.ActividadCompletada);
        Assert.AreEqual(1, eventos);
    }

    [Test]
    public void H2_NoCuentaDeMasDespuesDeLaMeta()
    {
        var manager = Crear<DeshierbeActivityManager>(("metaOrtigas", 2));
        for (int i = 0; i < 5; i++) manager.RegistrarOrtigaDepositada();

        Assert.AreEqual(2, manager.OrtigasRecolectadas);
        Assert.AreEqual(1f, manager.FraccionProgreso, 0.0001f);
    }
}

public class PruebasCorte : PruebasManagerBase
{
    private CorteActivityManager CrearCorte(int tallos = 3, int ramos = 2) =>
        Crear<CorteActivityManager>(("tallosParaRamo", tallos), ("ramosParaCompletar", ramos));

    [Test]
    public void C1_TallosDelRamoNoPasanDeLaMeta()
    {
        var manager = CrearCorte(tallos: 3);
        for (int i = 0; i < 5; i++) manager.RegistrarTalloDeshojado();

        Assert.AreEqual(3, manager.TallosEnRamoActual);
    }

    [Test]
    public void C2_RamoCompletado_ReiniciaTallosYNoPasaDeLaMeta()
    {
        var manager = CrearCorte(tallos: 3, ramos: 2);

        manager.RegistrarTalloDeshojado();
        manager.RegistrarRamoCompletado();
        Assert.AreEqual(1, manager.RamosCompletados);
        Assert.AreEqual(0, manager.TallosEnRamoActual);

        manager.RegistrarRamoCompletado();
        manager.RegistrarRamoCompletado();
        Assert.AreEqual(2, manager.RamosCompletados);
        Assert.IsTrue(manager.RamosListos);
    }

    [Test]
    public void C3_SoloSeCompletaConLaRecoleccionFinal()
    {
        var manager = CrearCorte(ramos: 1);
        int eventos = 0;
        manager.OnActividadCompletada += () => eventos++;

        manager.RegistrarRamoCompletado();
        Assert.IsTrue(manager.RamosListos);
        Assert.IsFalse(manager.ActividadCompletada, "Con los ramos listos falta depositarlos en el balde");
        Assert.AreEqual(0, eventos);

        manager.RegistrarRecoleccionFinal();
        Assert.IsTrue(manager.ActividadCompletada);
        Assert.AreEqual(1, eventos);
    }

    [Test]
    public void C4_ProgresoNoLlegaAl100AntesDeLaRecoleccionFinal()
    {
        var manager = CrearCorte(tallos: 1, ramos: 1);

        manager.RegistrarTalloDeshojado();
        manager.RegistrarRamoCompletado();
        Assert.LessOrEqual(manager.FraccionProgreso, 0.95f);

        manager.RegistrarRecoleccionFinal();
        Assert.AreEqual(1f, manager.FraccionProgreso, 0.0001f);
    }

    [Test]
    public void C5_MetasExpuestasDesdeUnaSolaFuente()
    {
        var manager = CrearCorte(tallos: 4, ramos: 6);

        Assert.AreEqual(4, manager.TallosPorRamo);
        Assert.AreEqual(6, manager.RamosParaCompletar);
    }
}

public class PruebasRamoEnProgreso : PruebasManagerBase
{
    [Test]
    public void R1_SinManager_UsaSuPropiaMeta()
    {
        var ramo = Crear<RamoEnProgreso>(("tallosParaNivelar", 4));
        Assert.AreEqual(4, ramo.TallosNecesarios);
    }
}

public class PruebasRamoConManager : PruebasManagerBase
{
    private GameObject _manager;

    [TearDown]
    public void DestruirManager()
    {
        if (_manager == null) return;
        var corte = _manager.GetComponent<CorteActivityManager>();
        typeof(CorteActivityManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(corte, null);
        Object.DestroyImmediate(_manager);
    }

    [Test]
    public void R2_ConManager_UsaLaMetaDelManager()
    {
        //El manager se crea aparte porque la clase base solo maneja un objeto por prueba
        _manager = new GameObject("Prueba_CorteActivityManager");
        var corte = _manager.AddComponent<CorteActivityManager>();
        typeof(CorteActivityManager).GetField("tallosParaRamo", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(corte, 3);
        typeof(CorteActivityManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(corte, null);

        var ramo = Crear<RamoEnProgreso>(("tallosParaNivelar", 7));

        Assert.AreEqual(3, ramo.TallosNecesarios, "En la actividad manda la meta de CorteActivityManager");
    }
}
