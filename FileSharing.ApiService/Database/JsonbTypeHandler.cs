using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace FileSharing.ApiService.Database;

public class JsonbTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        if (parameter is NpgsqlParameter npgsqlParameter)
        {
            npgsqlParameter.NpgsqlDbType = NpgsqlDbType.Jsonb;
        }
        parameter.Value = value is null 
            ? DBNull.Value 
            : JsonSerializer.Serialize(value, _options);
    }

    public override T? Parse(object? value)
    {
        return value switch
        {
            null or DBNull => default,
            string json => JsonSerializer.Deserialize<T>(json, _options),
            _ => JsonSerializer.Deserialize<T>(value.ToString()!, _options)
        };
    }
}