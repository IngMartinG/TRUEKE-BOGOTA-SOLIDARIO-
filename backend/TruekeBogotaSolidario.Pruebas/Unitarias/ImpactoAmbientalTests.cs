using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class ImpactoAmbientalTests
{
    [Fact]
    public void Sin_intercambios_todo_es_cero()
    {
        Assert.Equal((0, 0m, 0m), ImpactoAmbiental.Estimar(new Dictionary<int, int>()));
    }

    [Fact]
    public void Suma_los_factores_de_cada_categoria()
    {
        // 3 prendas (0,5 kg y 5 kg de CO₂ cada una) + 1 celular (1 kg y 40 kg)
        var (objetos, kg, co2) = ImpactoAmbiental.Estimar(new Dictionary<int, int> { [1] = 3, [5] = 1 });
        Assert.Equal(4, objetos);
        Assert.Equal(2.5m, kg);
        Assert.Equal(55m, co2);
    }

    [Fact]
    public void Una_categoria_sin_factor_usa_la_de_otros()
    {
        Assert.Equal(ImpactoAmbiental.PorCategoria[7], ImpactoAmbiental.Factor(99));
        var (objetos, kg, co2) = ImpactoAmbiental.Estimar(new Dictionary<int, int> { [99] = 2 });
        Assert.Equal((2, 2m, 4m), (objetos, kg, co2));
    }

    [Fact]
    public void Todas_las_categorias_de_la_semilla_tienen_factor_positivo()
    {
        foreach (var id in Enumerable.Range(1, 7))
        {
            var f = ImpactoAmbiental.PorCategoria[id];
            Assert.True(f.KgPorObjeto > 0 && f.KgCo2PorObjeto > 0, $"Categoría {id}");
        }
    }
}
