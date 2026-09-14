namespace AutoReparos.AuthLambda.Models;

public record ClienteAuthResponse(
    string Token,
    int ExpiresIn,
    string Nome,
    string Email,
    string Role = "Cliente"
);
