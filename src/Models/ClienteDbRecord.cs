namespace AutoReparos.AuthLambda.Models;

public record ClienteDbRecord(
    Guid Id,
    string Nome,
    string Documento,
    string Email,
    DateTime? InativoEm
)
{
    public bool Ativo => !InativoEm.HasValue;
}
