using AutoReparos.AuthLambda.Services;
using FluentAssertions;

namespace AutoReparos.AuthLambda.Tests.Services;

public class CpfValidationServiceTests
{
    private readonly CpfValidationService _service = new();

    [Theory]
    [InlineData("97632180044")]
    [InlineData("976.321.800-44")]
    [InlineData("52998224725")]
    [InlineData("529.982.247-25")]
    [InlineData(" 529.982.247-25 ")]
    public void Validar_DeveRetornarTrue_QuandoCpfForMatematicamenteValido(string cpf)
    {
        // Act
        var result = _service.Validar(cpf);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("22222222222")]
    [InlineData("33333333333")]
    [InlineData("44444444444")]
    [InlineData("55555555555")]
    [InlineData("66666666666")]
    [InlineData("77777777777")]
    [InlineData("88888888888")]
    [InlineData("99999999999")]
    public void Validar_DeveRetornarFalse_QuandoCpfConterTodosOsDigitosIguais(string cpf)
    {
        // Act
        var result = _service.Validar(cpf);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("52998224726")] // Primeiro dígito verificador incorreto
    [InlineData("52998224720")] // Ambos os dígitos incorretos
    [InlineData("12345678901")] // Sequência aleatória inválida
    public void Validar_DeveRetornarFalse_QuandoDigitosVerificadoresForemInvalidos(string cpf)
    {
        // Act
        var result = _service.Validar(cpf);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("1234567890")] // 10 dígitos
    [InlineData("123456789012")] // 12 dígitos
    [InlineData("abcdefghijk")]
    public void Validar_DeveRetornarFalse_QuandoTamanhoOuFormatoForInvalido(string? cpf)
    {
        // Act
        var result = _service.Validar(cpf);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Normalizar_DeveExtrairApenasDigitos()
    {
        // Act
        var result = _service.Normalizar(" 976.321.800-44 ");

        // Assert
        result.Should().Be("97632180044");
    }
}
