using AutoReparos.AuthLambda.Models;

namespace AutoReparos.AuthLambda.Services;

public interface ITokenGenerationService
{
    /// <summary>
    /// Gera um token JWT efêmero (1 hora) com claims seguras (sub/ClienteId, cpf, email, role=Cliente).
    /// </summary>
    ClienteAuthResponse GerarTokenCliente(ClienteDbRecord cliente);
}
