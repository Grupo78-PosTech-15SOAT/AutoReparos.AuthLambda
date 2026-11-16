using AutoReparos.AuthLambda.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AutoReparos.AuthLambda.Services;

public class CadastrosClient : ICadastrosClient
{
    private const string Caminho = "internal/clientes/autenticar";
    private const string CabecalhoChaveInterna = "X-Chave-Interna";

    private readonly HttpClient _http;
    private readonly string _chaveInterna;

    public CadastrosClient(HttpClient http, string baseUrl, string chaveInterna)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(chaveInterna);

        _http = http;
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _chaveInterna = chaveInterna;
    }

    public async Task<ResultadoAutenticacaoCadastros> AutenticarAsync(string cpf, string email, CancellationToken cancellationToken = default)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, Caminho)
        {
            Content = JsonContent.Create(new { cpf, email })
        };
        requisicao.Headers.TryAddWithoutValidation(CabecalhoChaveInterna, _chaveInterna);

        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.SendAsync(requisicao, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new CadastrosIndisponivelException("Falha de comunicação com o Cadastros.", ex);
        }

        using (resposta)
        {
            switch (resposta.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    return new ResultadoAutenticacaoCadastros(StatusAutenticacaoCadastros.NaoEncontrado);
                case HttpStatusCode.Forbidden:
                    return new ResultadoAutenticacaoCadastros(StatusAutenticacaoCadastros.Inativo);
                case HttpStatusCode.OK:
                    return new ResultadoAutenticacaoCadastros(StatusAutenticacaoCadastros.Autenticado, await LerRespostaAsync(resposta, cancellationToken));
                default:
                    // 401 aqui significa chave interna recusada: é configuração, não credencial do cliente
                    throw new CadastrosIndisponivelException($"O Cadastros respondeu {(int)resposta.StatusCode} a /{Caminho}.");
            }
        }
    }

    private static async Task<ClienteAuthResponse> LerRespostaAsync(HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        try
        {
            using var documento = await JsonDocument.ParseAsync(await resposta.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var raiz = documento.RootElement;
            var token = raiz.GetProperty("token").GetString()!;
            var cliente = raiz.GetProperty("cliente");

            var expiraEm = new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;
            var expiresIn = Math.Max(0, (int)(expiraEm - DateTime.UtcNow).TotalSeconds);

            return new ClienteAuthResponse(token, expiresIn, cliente.GetProperty("nome").GetString()!, cliente.GetProperty("email").GetString()!);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            throw new CadastrosIndisponivelException("Resposta do Cadastros em formato inesperado.", ex);
        }
    }
}
