using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using AutoReparos.AuthLambda.Models;
using AutoReparos.AuthLambda.Services;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AutoReparos.AuthLambda.Functions;

public class ClienteAuthFunction
{
    private readonly ICpfValidationService _cpfValidationService;
    private readonly ICadastrosClient _cadastros;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Dictionary<string, string> CorsHeaders = new()
    {
        { "Content-Type", "application/json" },
        { "Access-Control-Allow-Origin", "*" },
        { "Access-Control-Allow-Headers", "Content-Type,Authorization,X-Amz-Date,X-Api-Key,X-Amz-Security-Token" },
        { "Access-Control-Allow-Methods", "POST,OPTIONS" }
    };

    private static readonly HttpClient HttpCompartilhado = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>
    /// Construtor padrão utilizado no runtime da AWS Lambda (carrega configurações do ambiente).
    /// </summary>
    public ClienteAuthFunction()
    {
        var baseUrl = Environment.GetEnvironmentVariable("Cadastros__BaseUrl")
                      ?? Environment.GetEnvironmentVariable("CADASTROS_BASE_URL");

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Variável de ambiente 'Cadastros__BaseUrl' ou 'CADASTROS_BASE_URL' não foi configurada.");
        }

        var chaveInterna = Environment.GetEnvironmentVariable("Cadastros__ChaveInterna")
                           ?? Environment.GetEnvironmentVariable("CADASTROS_CHAVE_INTERNA");

        if (string.IsNullOrWhiteSpace(chaveInterna))
        {
            throw new InvalidOperationException("Variável de ambiente 'Cadastros__ChaveInterna' ou 'CADASTROS_CHAVE_INTERNA' não foi configurada.");
        }

        _cpfValidationService = new CpfValidationService();
        _cadastros = new CadastrosClient(HttpCompartilhado, baseUrl, chaveInterna);
    }

    /// <summary>
    /// Construtor com injeção explícita de dependências para testes unitários e de integração.
    /// </summary>
    public ClienteAuthFunction(ICpfValidationService cpfValidationService, ICadastrosClient cadastros)
    {
        _cpfValidationService = cpfValidationService ?? throw new ArgumentNullException(nameof(cpfValidationService));
        _cadastros = cadastros ?? throw new ArgumentNullException(nameof(cadastros));
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext? context = null)
    {
        // Tratamento de preflight CORS (OPTIONS)
        if (string.Equals(request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Headers = CorsHeaders,
                Body = string.Empty
            };
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return CriarResposta(400, new { erro = "Corpo da requisição não pode ser vazio." });
            }

            ClienteAuthRequest? authRequest;
            try
            {
                authRequest = JsonSerializer.Deserialize<ClienteAuthRequest>(request.Body, JsonOptions);
            }
            catch (JsonException)
            {
                return CriarResposta(400, new { erro = "Formato do JSON é inválido." });
            }

            if (authRequest == null ||
                string.IsNullOrWhiteSpace(authRequest.Cpf) ||
                string.IsNullOrWhiteSpace(authRequest.Email))
            {
                return CriarResposta(400, new { erro = "CPF e E-mail são obrigatórios." });
            }

            var emailSanitizado = authRequest.Email.Trim().ToLowerInvariant();
            if (!emailSanitizado.Contains('@') || !emailSanitizado.Contains('.'))
            {
                return CriarResposta(400, new { erro = "Formato de e-mail inválido." });
            }

            if (!_cpfValidationService.Validar(authRequest.Cpf))
            {
                return CriarResposta(400, new { erro = "CPF inválido." });
            }

            var cpfNormalizado = _cpfValidationService.Normalizar(authRequest.Cpf);

            // O Cadastros (dono dos clientes) autentica e emite o JWT; a Lambda não acessa o banco
            var resultado = await _cadastros.AutenticarAsync(cpfNormalizado, emailSanitizado);

            return resultado.Status switch
            {
                StatusAutenticacaoCadastros.Autenticado => CriarResposta(200, resultado.Resposta!),
                StatusAutenticacaoCadastros.Inativo => CriarResposta(403, new { erro = "Cadastro do cliente encontra-se inativo. Entre em contato com a recepção da oficina." }),
                _ => CriarResposta(401, new { erro = "Cliente não localizado ou dados divergentes." })
            };
        }
        catch (CadastrosIndisponivelException ex)
        {
            context?.Logger.LogError($"[ClienteAuthFunction] Cadastros indisponível: {ex.Message}");
            return CriarResposta(503, new { erro = "Serviço de cadastros indisponível. Tente novamente em instantes." });
        }
        catch (Exception ex)
        {
            context?.Logger.LogError($"[ClienteAuthFunction] Erro inesperado: {ex.Message}");
            return CriarResposta(500, new { erro = "Erro interno ao processar a autenticação." });
        }
    }

    private static APIGatewayProxyResponse CriarResposta(int statusCode, object body)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = statusCode,
            Headers = CorsHeaders,
            Body = JsonSerializer.Serialize(body, JsonOptions)
        };
    }
}
