using AutoReparos.AuthLambda.Models;

namespace AutoReparos.AuthLambda.Services;

public enum StatusAutenticacaoCadastros
{
    Autenticado,
    NaoEncontrado,
    Inativo
}

public record ResultadoAutenticacaoCadastros(StatusAutenticacaoCadastros Status, ClienteAuthResponse? Resposta = null);

/// <summary>O Cadastros não respondeu de forma utilizável (rede, 5xx, chave interna recusada).</summary>
public sealed class CadastrosIndisponivelException(string mensagem, Exception? interna = null) : Exception(mensagem, interna);

/// <summary>
/// Endpoint interno do Cadastros: a Lambda não acessa o banco nem assina tokens; o Cadastros devolve o cliente e o JWT RS256.
/// </summary>
public interface ICadastrosClient
{
    /// <exception cref="CadastrosIndisponivelException">O Cadastros não respondeu de forma utilizável.</exception>
    Task<ResultadoAutenticacaoCadastros> AutenticarAsync(string cpf, string email, CancellationToken cancellationToken = default);
}
