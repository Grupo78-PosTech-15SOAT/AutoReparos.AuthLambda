using AutoReparos.AuthLambda.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AutoReparos.AuthLambda.Services;

public class TokenGenerationService : ITokenGenerationService
{
    private readonly string _jwtSecret;
    private readonly int _expiryHours;

    public TokenGenerationService(string jwtSecret, int expiryHours = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jwtSecret);
        if (jwtSecret.Length < 32)
            throw new ArgumentException("O segredo JWT deve conter no mínimo 32 caracteres.", nameof(jwtSecret));

        _jwtSecret = jwtSecret;
        _expiryHours = expiryHours > 0 ? expiryHours : 1;
    }

    public ClienteAuthResponse GerarTokenCliente(ClienteDbRecord cliente)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        var key = Encoding.ASCII.GetBytes(_jwtSecret);
        var tokenHandler = new JwtSecurityTokenHandler();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, cliente.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, cliente.Id.ToString()),
            new Claim(ClaimTypes.Name, cliente.Nome),
            new Claim(ClaimTypes.Email, cliente.Email),
            new Claim("cpf", cliente.Documento),
            new Claim(ClaimTypes.Role, "Cliente")
        };

        var expiresInSeconds = _expiryHours * 3600;
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddSeconds(expiresInSeconds),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return new ClienteAuthResponse(
            Token: tokenString,
            ExpiresIn: expiresInSeconds,
            Nome: cliente.Nome,
            Email: cliente.Email,
            Role: "Cliente"
        );
    }
}
