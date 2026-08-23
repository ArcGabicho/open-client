var password = Environment.GetEnvironmentVariable("OPENCLIENT_ADMIN_PASSWORD");

if (string.IsNullOrWhiteSpace(password))
{
    Console.Error.WriteLine(
        "ERROR: OPENCLIENT_ADMIN_PASSWORD no está definido.");

    return 1;
}

var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

Console.Write(hash);

return 0;