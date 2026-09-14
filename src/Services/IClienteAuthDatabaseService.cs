using AutoReparos.AuthLambda.Models;

namespace AutoReparos.AuthLambda.Services;

public interface IClienteAuthDatabaseService
{
    /// <summary>
    /// Consulta o cliente na tabela "Clientes" pelo CPF normalizado e e-mail.
    /// Retorna null se não localizado.
    /// </summary>
    Task<ClienteDbRecord?> BuscarClientePorCpfEmailAsync(string cpfNormalizado, string email, CancellationToken cancellationToken = default);
}
