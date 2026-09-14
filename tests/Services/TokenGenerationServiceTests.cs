using AutoReparos.AuthLambda.Models;
using AutoReparos.AuthLambda.Services;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AutoReparos.AuthLambda.Tests.Services;

public class TokenGenerationServiceTests
{
    private const string TestSecret = "test_jwt_dummy_secret_key_with_32_chars_ok!";
    private readonly TokenGenerationService _service = new(TestSecret, expiryHours: 1);

    [Fact]
    public void GerarTokenCliente_DeveRetornarTokenComClaimsECorretas()
    {
        // Arrange
        var clienteId = Guid.NewGuid();
        var cliente = new ClienteDbRecord(
            Id: clienteId,
            Nome: "João da Silva",
            Documento: "97632180044",
            Email: "joao@email.com",
            InativoEm: null
        );

        // Act
        var response = _service.GerarTokenCliente(cliente);

        // Assert
        response.Should().NotBeNull();
        response.Token.Should().NotBeNullOrWhiteSpace();
        response.ExpiresIn.Should().Be(3600);
        response.Nome.Should().Be("João da Silva");
        response.Email.Should().Be("joao@email.com");
        response.Role.Should().Be("Cliente");

        // Valida criptograficamente o JWT gerado
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(TestSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };

        var principal = tokenHandler.ValidateToken(response.Token, validationParameters, out var validatedToken);
        validatedToken.Should().NotBeNull();

        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be(clienteId.ToString());
        principal.FindFirst(ClaimTypes.Role)?.Value.Should().Be("Cliente");
        principal.FindFirst(ClaimTypes.Name)?.Value.Should().Be("João da Silva");
        principal.FindFirst(ClaimTypes.Email)?.Value.Should().Be("joao@email.com");
        principal.FindFirst("cpf")?.Value.Should().Be("97632180044");
    }

    [Fact]
    public void Construtor_DeveLancarExcecao_QuandoSecretForMenorQue32Caracteres()
    {
        // Arrange & Act
        var action = () => new TokenGenerationService("segredo_curto");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*no mínimo 32 caracteres*");
    }
}
