namespace AutoReparos.AuthLambda.Services;

public class CpfValidationService : ICpfValidationService
{
    public string Normalizar(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return string.Empty;

        return new string(cpf.Where(char.IsDigit).ToArray());
    }

    public bool Validar(string? cpf)
    {
        var digits = Normalizar(cpf);

        if (digits.Length != 11)
            return false;

        // Rejeita sequências homogêneas com todos os dígitos iguais (ex: 000.000.000-00, 111.111.111-11, etc.)
        if (digits.Distinct().Count() == 1)
            return false;

        // Primeiro dígito verificador (módulo 11)
        var soma1 = 0;
        for (var i = 0; i < 9; i++)
        {
            soma1 += (digits[i] - '0') * (10 - i);
        }

        var resto1 = (soma1 * 10) % 11;
        var primeiroDigito = resto1 == 10 ? 0 : resto1;

        if (primeiroDigito != (digits[9] - '0'))
            return false;

        // Segundo dígito verificador (módulo 11)
        var soma2 = 0;
        for (var i = 0; i < 10; i++)
        {
            soma2 += (digits[i] - '0') * (11 - i);
        }

        var resto2 = (soma2 * 10) % 11;
        var segundoDigito = resto2 == 10 ? 0 : resto2;

        return segundoDigito == (digits[10] - '0');
    }
}
