namespace AutoReparos.AuthLambda.Services;

public interface ICpfValidationService
{
    /// <summary>
    /// Valida matematicamente um CPF brasileiro (11 dígitos, cálculo dos dígitos verificadores, rejeição de repetidos).
    /// </summary>
    bool Validar(string? cpf);

    /// <summary>
    /// Extrai apenas os dígitos numéricos de uma string de CPF.
    /// </summary>
    string Normalizar(string? cpf);
}
