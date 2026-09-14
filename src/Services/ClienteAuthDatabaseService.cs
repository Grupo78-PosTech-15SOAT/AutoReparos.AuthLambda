using AutoReparos.AuthLambda.Models;
using Npgsql;
using System.Data;

namespace AutoReparos.AuthLambda.Services;

public class ClienteAuthDatabaseService : IClienteAuthDatabaseService, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly bool _ownsDataSource;

    public ClienteAuthDatabaseService(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _dataSource = builder.Build();
        _ownsDataSource = true;
    }

    public ClienteAuthDatabaseService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _ownsDataSource = false;
    }

    public async Task<ClienteDbRecord?> BuscarClientePorCpfEmailAsync(string cpfNormalizado, string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cpfNormalizado);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        const string sql = """
            SELECT "Id", "Nome", "Documento", "Email", "InativoEm"
            FROM "Clientes"
            WHERE "Documento" = @cpf AND LOWER("Email") = LOWER(@email)
            LIMIT 1;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@cpf", cpfNormalizado);
        cmd.Parameters.AddWithValue("@email", email.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetGuid(reader.GetOrdinal("Id"));
            var nome = reader.GetString(reader.GetOrdinal("Nome"));
            var documento = reader.GetString(reader.GetOrdinal("Documento"));
            var emailDb = reader.GetString(reader.GetOrdinal("Email"));
            var inativoOrdinal = reader.GetOrdinal("InativoEm");
            DateTime? inativoEm = reader.IsDBNull(inativoOrdinal) ? null : reader.GetDateTime(inativoOrdinal);

            return new ClienteDbRecord(id, nome, documento, emailDb, inativoEm);
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsDataSource)
        {
            await _dataSource.DisposeAsync();
        }
    }
}
