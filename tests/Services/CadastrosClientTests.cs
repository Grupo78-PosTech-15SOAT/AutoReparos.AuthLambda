using AutoReparos.AuthLambda.Services;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace AutoReparos.AuthLambda.Tests.Services;

public class CadastrosClientTests
{
    private const string ChaveInterna = "chave-interna-de-teste";

    private sealed class ManipuladorFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Recebida { get; private set; }
        public string? CorpoRecebido { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Recebida = request;
            CorpoRecebido = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }

    private static string TokenQueExpiraEm(TimeSpan duracao)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            Expires = DateTime.UtcNow.Add(duracao),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(new byte[32]), SecurityAlgorithms.HmacSha256)
        }));
    }

    private static (CadastrosClient Cliente, ManipuladorFalso Manipulador) Criar(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var manipulador = new ManipuladorFalso(responder);
        return (new CadastrosClient(new HttpClient(manipulador), "http://cadastros.interno", ChaveInterna), manipulador);
    }

    [Fact]
    public async Task Autenticar_DeveChamarOEndpointInternoComACpfEEmailEAChaveInterna()
    {
        var (cliente, manipulador) = Criar(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await cliente.AutenticarAsync("97632180044", "maria@email.com");

        manipulador.Recebida!.Method.Should().Be(HttpMethod.Post);
        manipulador.Recebida.RequestUri!.ToString().Should().Be("http://cadastros.interno/internal/clientes/autenticar");
        manipulador.Recebida.Headers.GetValues("X-Chave-Interna").Should().ContainSingle().Which.Should().Be(ChaveInterna);
        var corpo = JsonDocument.Parse(manipulador.CorpoRecebido!).RootElement;
        corpo.GetProperty("cpf").GetString().Should().Be("97632180044");
        corpo.GetProperty("email").GetString().Should().Be("maria@email.com");
    }

    [Fact]
    public async Task Autenticar_DeveDevolverOTokenDoCadastrosEOTempoRestante()
    {
        var token = TokenQueExpiraEm(TimeSpan.FromHours(2));
        var (cliente, _) = Criar(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { cliente = new { id = Guid.NewGuid(), nome = "Maria Ativa", email = "maria@email.com" }, token })
        });

        var resultado = await cliente.AutenticarAsync("97632180044", "maria@email.com");

        resultado.Status.Should().Be(StatusAutenticacaoCadastros.Autenticado);
        resultado.Resposta!.Token.Should().Be(token);
        resultado.Resposta.Nome.Should().Be("Maria Ativa");
        resultado.Resposta.Role.Should().Be("Cliente");
        resultado.Resposta.ExpiresIn.Should().BeInRange(7100, 7200);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, StatusAutenticacaoCadastros.NaoEncontrado)]
    [InlineData(HttpStatusCode.Forbidden, StatusAutenticacaoCadastros.Inativo)]
    public async Task Autenticar_DeveMapearOsStatusDeNegocio(HttpStatusCode status, StatusAutenticacaoCadastros esperado)
    {
        var (cliente, _) = Criar(_ => new HttpResponseMessage(status));

        var resultado = await cliente.AutenticarAsync("97632180044", "maria@email.com");

        resultado.Status.Should().Be(esperado);
        resultado.Resposta.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task Autenticar_DeveLancarIndisponivel_QuandoOCadastrosRecusaOuFalha(HttpStatusCode status)
    {
        var (cliente, _) = Criar(_ => new HttpResponseMessage(status));

        var acao = () => cliente.AutenticarAsync("97632180044", "maria@email.com");

        await acao.Should().ThrowAsync<CadastrosIndisponivelException>();
    }

    [Fact]
    public async Task Autenticar_DeveLancarIndisponivel_QuandoARedeFalha()
    {
        var (cliente, _) = Criar(_ => throw new HttpRequestException("sem rota"));

        var acao = () => cliente.AutenticarAsync("97632180044", "maria@email.com");

        await acao.Should().ThrowAsync<CadastrosIndisponivelException>();
    }

    [Fact]
    public async Task Autenticar_DeveLancarIndisponivel_QuandoARespostaForMalFormada()
    {
        var (cliente, _) = Criar(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { qualquer = "coisa" }) });

        var acao = () => cliente.AutenticarAsync("97632180044", "maria@email.com");

        await acao.Should().ThrowAsync<CadastrosIndisponivelException>();
    }
}
