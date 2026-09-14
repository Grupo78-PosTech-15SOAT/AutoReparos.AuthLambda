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
    private readonly IClienteAuthDatabaseService _databaseService;
    private readonly ITokenGenerationService _tokenGenerationService;

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

    /// <summary>
    /// Construtor padrão utilizado no runtime da AWS Lambda (carrega configurações do ambiente).
    /// </summary>
    public ClienteAuthFunction()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DbConnection")
                               ?? Environment.GetEnvironmentVariable("DB_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Variável de ambiente 'ConnectionStrings__DbConnection' ou 'DB_CONNECTION' não foi configurada.");
        }

        var jwtSecret = Environment.GetEnvironmentVariable("Jwt__Secret")
                        ?? Environment.GetEnvironmentVariable("JWT_SECRET");

        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            throw new InvalidOperationException("Variável de ambiente 'Jwt__Secret' ou 'JWT_SECRET' não foi configurada.");
        }

        _cpfValidationService = new CpfValidationService();
        _databaseService = new ClienteAuthDatabaseService(connectionString);
        _tokenGenerationService = new TokenGenerationService(jwtSecret, expiryHours: 1);
    }

    /// <summary>
    /// Construtor com injeção explícita de dependências para testes unitários e de integração.
    /// </summary>
    public ClienteAuthFunction(
        ICpfValidationService cpfValidationService,
        IClienteAuthDatabaseService databaseService,
        ITokenGenerationService tokenGenerationService)
    {
        _cpfValidationService = cpfValidationService ?? throw new ArgumentNullException(nameof(cpfValidationService));
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _tokenGenerationService = tokenGenerationService ?? throw new ArgumentNullException(nameof(tokenGenerationService));
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

            // Consulta ao banco PostgreSQL
            var cliente = await _databaseService.BuscarClientePorCpfEmailAsync(cpfNormalizado, emailSanitizado);

            if (cliente == null)
            {
                return CriarResposta(401, new { erro = "Cliente não localizado ou dados divergentes." });
            }

            if (!cliente.Ativo)
            {
                return CriarResposta(403, new { erro = "Cadastro do cliente encontra-se inativo. Entre em contato com a recepção da oficina." });
            }

            var responseDto = _tokenGenerationService.GerarTokenCliente(cliente);
            return CriarResposta(200, responseDto);
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
