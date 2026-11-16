using Amazon.Lambda.APIGatewayEvents;
using AutoReparos.AuthLambda.Functions;
using AutoReparos.AuthLambda.Models;
using AutoReparos.AuthLambda.Services;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Text.Json;

namespace AutoReparos.AuthLambda.Tests.Functions;

public class ClienteAuthFunctionTests
{
    private readonly ICpfValidationService _cpfValidationService = Substitute.For<ICpfValidationService>();
    private readonly ICadastrosClient _cadastros = Substitute.For<ICadastrosClient>();
    private readonly ClienteAuthFunction _function;

    public ClienteAuthFunctionTests()
    {
        _function = new ClienteAuthFunction(_cpfValidationService, _cadastros);
    }

    [Fact]
    public async Task FunctionHandler_DeveRetornar200_QuandoRequisicaoOptionsDeCors()
    {
        // Arrange
        var request = new APIGatewayProxyRequest { HttpMethod = "OPTIONS" };

        // Act
        var response = await _function.FunctionHandler(request);

        // Assert
        response.StatusCode.Should().Be(200);
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FunctionHandler_DeveRetornar400_QuandoBodyForVazio(string? body)
    {
        // Arrange
        var request = new APIGatewayProxyRequest { HttpMethod = "POST", Body = body };

        // Act
        var response = await _function.FunctionHandler(request);

        // Assert
        response.StatusCode.Should().Be(400);
        response.Body.Should().Contain("não pode ser vazio");
    }

    [Fact]
    public async Task FunctionHandler_DeveRetornar400_QuandoJsonForInvalido()
    {
        // Arrange
        var request = new APIGatewayProxyRequest { HttpMethod = "POST", Body = "{ json_invalido: " };

        // Act
        var response = await _function.FunctionHandler(request);

        // Assert
        response.StatusCode.Should().Be(400);
        response.Body.Should().Contain("inválido");
    }

    [Fact]
    public async Task FunctionHandler_DeveRetornar400_QuandoCpfOuEmailEstiveremAusentes()
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Body = JsonSerializer.Serialize(new { cpf = "", email = "" })
        };

        // Act
        var response = await _function.FunctionHandler(request);

        // Assert
        response.StatusCode.Should().Be(400);
        response.Body.Should().Contain("obrigatórios");
    }

    [Fact]
    public async Task FunctionHandler_DeveRetornar400_QuandoEmailForEmFormatoInvalido()
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Body = JsonSerializer.Serialize(new { cpf = "97632180044", email = "email_sem_arroba" })
        };

        // Act
        var response = await _function.FunctionHandler(request);

        // Assert
        response.StatusCode.Should().Be(400);
        response.Body.Should().Contain("Formato de e-mail inválido");
    }

    [Fact]
    public async Task FunctionHandler_DeveRetornar400_QuandoCpfForMatematicamenteInvalido()
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Body = JsonSerializer.Serialize(new { cpf = "11111111111", email = "cliente@email.com" })
        };
        _cpfValidationService.Validar("11111111111").Returns(false);

        // Act
        var response = await _function.FunctionHandler(request);

        // Assert
        response.StatusCode.Should().Be(400);
        response.Body.Should().Contain("CPF inválido");
    }

    private void ConfigurarCpfValido(string cpfEntrada = "97632180044")
    {
        _cpfValidationService.Validar(cpfEntrada).Returns(true);
        _cpfValidationService.Normalizar(cpfEntrada).Returns("97632180044");
    }

    private static APIGatewayProxyRequest Requisicao(string cpf, string email) => new()
    {
        HttpMethod = "POST",
        Body = JsonSerializer.Serialize(new { cpf, email })
    };

    [Fact]
    public async Task FunctionHandler_DeveRetornar401_QuandoCadastrosNaoLocalizarOCliente()
    {
        ConfigurarCpfValido();
        _cadastros.AutenticarAsync("97632180044", "cliente@email.com", Arg.Any<CancellationToken>())
            .Returns(new ResultadoAutenticacaoCadastros(StatusAutenticacaoCadastros.NaoEncontrado));

        var response = await _function.FunctionHandler(Requisicao("97632180044", "cliente@email.com"));

        response.StatusCode.Should().Be(401);
        response.Body.Should().Contain("não localizado ou dados divergentes");
    }

    [Fact]
    public async Task FunctionHandler_DeveRetornar403_QuandoClienteEstiverInativo()
    {
        ConfigurarCpfValido();
        _cadastros.AutenticarAsync("97632180044", "carlos@email.com", Arg.Any<CancellationToken>())
            .Returns(new ResultadoAutenticacaoCadastros(StatusAutenticacaoCadastros.Inativo));

        var response = await _function.FunctionHandler(Requisicao("97632180044", "carlos@email.com"));

        response.StatusCode.Should().Be(403);
        response.Body.Should().Contain("encontra-se inativo");
    }

    [Fact]
    public async Task FunctionHandler_DeveRetornar200ComOTokenDoCadastros_QuandoClienteForAtivoEValido()
    {
        ConfigurarCpfValido("976.321.800-44");
        _cadastros.AutenticarAsync("97632180044", "maria@email.com", Arg.Any<CancellationToken>())
            .Returns(new ResultadoAutenticacaoCadastros(
                StatusAutenticacaoCadastros.Autenticado,
                new ClienteAuthResponse("jwt_do_cadastros_123", 7200, "Maria Ativa", "maria@email.com")));

        var response = await _function.FunctionHandler(Requisicao("976.321.800-44", " Maria@email.com "));

        response.StatusCode.Should().Be(200);
        response.Body.Should().Contain("jwt_do_cadastros_123");
        response.Body.Should().Contain("Maria Ativa");
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
    }

    [Fact]
    public async Task FunctionHandler_DeveRetornar503_QuandoCadastrosEstiverIndisponivel()
    {
        ConfigurarCpfValido();
        _cadastros.AutenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new CadastrosIndisponivelException("O Cadastros respondeu 500."));

        var response = await _function.FunctionHandler(Requisicao("97632180044", "maria@email.com"));

        response.StatusCode.Should().Be(503);
        response.Body.Should().Contain("Serviço de cadastros indisponível");
    }

    [Fact]
    public async Task FunctionHandler_DeveRetornar500_QuandoOcorrerExcecaoInesperada()
    {
        ConfigurarCpfValido();
        _cadastros.AutenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("falha inesperada"));

        var response = await _function.FunctionHandler(Requisicao("97632180044", "maria@email.com"));

        response.StatusCode.Should().Be(500);
        response.Body.Should().Contain("Erro interno ao processar a autenticação");
    }

    [Fact]
    public void Lambda_NaoReferenciaDriverDeBancoNemAssinaTokens()
    {
        var referenciados = typeof(ClienteAuthFunction).Assembly.GetReferencedAssemblies().Select(a => a.Name);

        referenciados.Should().NotContain(nome => nome != null && nome.StartsWith("Npgsql", StringComparison.OrdinalIgnoreCase));
        typeof(ClienteAuthFunction).Assembly.GetTypes().Should().NotContain(t => t.Name.Contains("TokenGeneration") || t.Name.Contains("Database"));
    }
}
