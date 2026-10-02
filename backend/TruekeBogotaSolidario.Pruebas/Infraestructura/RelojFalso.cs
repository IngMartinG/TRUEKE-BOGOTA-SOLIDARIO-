namespace TruekeBogotaSolidario.Pruebas.Infraestructura;

/// <summary>Reloj controlable para probar plazos (cierre automático, vencimientos) sin esperar días.</summary>
public sealed class RelojFalso : TimeProvider
{
    private DateTimeOffset _ahora = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _ahora;
    public void Avanzar(TimeSpan tiempo) => _ahora = _ahora.Add(tiempo);
}
