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
    private readonly IClienteAuthDatabaseService _databaseService = Substitute.For<IClienteAuthDatabaseService>();
    private readonly ITokenGenerationService _tokenGenerationService = Substitute.For<ITokenGenerationService>();
    private readonly ClienteAuthFunction _function;

    public ClienteAuthFunctionTests()
    {
        _function = new ClienteAuthFunction(_cpfValidationService, _databaseService, _tokenGenerationService);
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

    [Fact]
    public async Task FunctionHandler_DeveRetornar401_QuandoClienteNaoExistirNoBanco()
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Body = JsonSerializer.Serialize(new { cpf = "97632180044", email = "cliente@email.com" })
        };
        _cpfValidationService.Validar("97632180044").Returns(true);
        _cpfValidationService.Normalizar("97632180044").Returns("97632180044");
        _databaseService.BuscarClientePorCpfEmailAsync("97632180044", "cliente@email.com")
            .Returns((ClienteDbRecord?)null);

        // Act
        var response = await _function.FunctionHandler(request);

        // Assert
        response.StatusCode.Should().Be(401);
        response.Body.Should().Contain("não localizado ou dados divergentes");
    }

    [Fact]
    public async Task FunctionHandler_DeveRetornar403_QuandoClienteEstiverInativo()
    {
        // Arrange
        var clienteInativo = new ClienteDbRecord(
            Id: Guid.NewGuid(),
            Nome: "Carlos Inativo",
            Documento: "97632180044",
            Email: "carlos@email.com",
            InativoEm: DateTime.UtcNow.AddDays(-5)
        );

        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Body = JsonSerializer.Serialize(new { cpf = "97632180044", email = "carlos@email.com" })
        };
        _cpfValidationService.Validar("97632180044").Returns(true);
        _cpfValidationService.Normalizar("97632180044").Returns("97632180044");
        _databaseService.BuscarClientePorCpfEmailAsync("97632180044", "carlos@email.com")
            .Returns(clienteInativo);

        // Act
        var response = await _function.FunctionHandler(request);

        // Assert
        response.StatusCode.Should().Be(403);
        response.Body.Should().Contain("encontra-se inativo");
    }

    [Fact]
    public async Task FunctionHandler_DeveRetornar200ComToken_QuandoClienteForAtivoEValido()
    {
        // Arrange
        var clienteAtivo = new ClienteDbRecord(
            Id: Guid.NewGuid(),
            Nome: "Maria Ativa",
            Documento: "97632180044",
            Email: "maria@email.com",
            InativoEm: null
        );

        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Body = JsonSerializer.Serialize(new { cpf = "976.321.800-44", email = " Maria@email.com " })
        };
        _cpfValidationService.Validar("976.321.800-44").Returns(true);
        _cpfValidationService.Normalizar("976.321.800-44").Returns("97632180044");
        _databaseService.BuscarClientePorCpfEmailAsync("97632180044", "maria@email.com")
            .Returns(clienteAtivo);

        var responseEsperada = new ClienteAuthResponse(
            Token: "jwt_token_valido_123",
            ExpiresIn: 3600,
            Nome: "Maria Ativa",
            Email: "maria@email.com",
            Role: "Cliente"
        );
        _tokenGenerationService.GerarTokenCliente(clienteAtivo).Returns(responseEsperada);

        // Act
        var response = await _function.FunctionHandler(request);

        // Assert
        response.StatusCode.Should().Be(200);
        response.Body.Should().Contain("jwt_token_valido_123");
        response.Body.Should().Contain("Maria Ativa");
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
    }

    [Fact]
    public async Task FunctionHandler_DeveRetornar500_QuandoOcorrerExcecaoInesperada()
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Body = JsonSerializer.Serialize(new { cpf = "97632180044", email = "maria@email.com" })
        };
        _cpfValidationService.Validar("97632180044").Returns(true);
        _cpfValidationService.Normalizar("97632180044").Returns("97632180044");
        _databaseService.BuscarClientePorCpfEmailAsync(Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("Falha na conexão com Postgres"));

        // Act
        var response = await _function.FunctionHandler(request);

        // Assert
        response.StatusCode.Should().Be(500);
        response.Body.Should().Contain("Erro interno ao processar a autenticação");
    }
}
