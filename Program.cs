using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// Configuração do OpenAPI / Swagger
builder.Services.AddOpenApi();

// Configuração de CORS para permitir integração com o Frontend (Nuxt/Vue)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Registro do repositório em memória como Singleton (thread-safe)
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();

// -------------------------------------------------------------
// ENDPOINTS DE USUÁRIOS (4 VERBOS HTTP PRINCIPAIS)
// -------------------------------------------------------------
var usersGroup = app.MapGroup("/users")
    .WithTags("Users");

// 1. POST /users - Criar Usuário (com Username e Password)
usersGroup.MapPost("/", async (CreateUserRequest request, IUserRepository repository) =>
{
    if (string.IsNullOrWhiteSpace(request.Username))
    {
        return Results.BadRequest(new { error = "O nome de usuário (username) é obrigatório." });
    }

    if (string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "A senha (password) é obrigatória." });
    }

    var trimmedUsername = request.Username.Trim();

    if (await repository.ExistsAsync(trimmedUsername))
    {
        return Results.Conflict(new { error = $"O nome de usuário '{trimmedUsername}' já está em uso." });
    }

    var user = await repository.CreateAsync(trimmedUsername, request.Password);
    var response = new UserResponse(user.Username, user.CreatedAt);

    return Results.Created($"/users/{user.Username}", response);
})
.WithName("CreateUser");

// 2. GET /users - Listar todos os Usuários
usersGroup.MapGet("/", async (IUserRepository repository) =>
{
    var users = await repository.GetAllAsync();
    var response = users.Select(u => new UserResponse(u.Username, u.CreatedAt));
    return Results.Ok(response);
})
.WithName("GetAllUsers");

// 3. GET /users/{username} - Obter Usuário pelo Nome de Usuário
usersGroup.MapGet("/{username}", async (string username, IUserRepository repository) =>
{
    var user = await repository.GetByUsernameAsync(username.Trim());
    if (user is null)
    {
        return Results.NotFound(new { error = $"Usuário '{username}' não encontrado." });
    }

    return Results.Ok(new UserResponse(user.Username, user.CreatedAt));
})
.WithName("GetUserByUsername");

// 4. PUT /users/{username} - Atualizar Usuário (Nome de Usuário e/ou Senha)
usersGroup.MapPut("/{username}", async (string username, UpdateUserRequest request, IUserRepository repository) =>
{
    if (string.IsNullOrWhiteSpace(request.NewUsername) && string.IsNullOrWhiteSpace(request.NewPassword))
    {
        return Results.BadRequest(new { error = "Pelo menos um campo (newUsername ou newPassword) deve ser informado." });
    }

    var currentUsername = username.Trim();
    var existingUser = await repository.GetByUsernameAsync(currentUsername);
    if (existingUser is null)
    {
        return Results.NotFound(new { error = $"Usuário '{currentUsername}' não encontrado." });
    }

    string finalUsername = currentUsername;
    if (!string.IsNullOrWhiteSpace(request.NewUsername))
    {
        var trimmedNewUsername = request.NewUsername.Trim();
        if (!string.Equals(currentUsername, trimmedNewUsername, StringComparison.OrdinalIgnoreCase) &&
            await repository.ExistsAsync(trimmedNewUsername))
        {
            return Results.Conflict(new { error = $"O novo nome de usuário '{trimmedNewUsername}' já está em uso." });
        }
        finalUsername = trimmedNewUsername;
    }

    var updatedUser = await repository.UpdateAsync(currentUsername, finalUsername, request.NewPassword);
    return Results.Ok(new UserResponse(updatedUser!.Username, updatedUser.CreatedAt));
})
.WithName("UpdateUser");

// 5. DELETE /users/{username} - Deletar Usuário
usersGroup.MapDelete("/{username}", async (string username, IUserRepository repository) =>
{
    var removed = await repository.DeleteAsync(username.Trim());
    if (!removed)
    {
        return Results.NotFound(new { error = $"Usuário '{username}' não encontrado." });
    }

    return Results.NoContent();
})
.WithName("DeleteUser");

app.Run();

// -------------------------------------------------------------
// DOMÍNIO & DTOs
// Invariante de Segurança: O cliente nunca recebe o ID interno (GUID) nem a senha
// -------------------------------------------------------------
public sealed class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public record CreateUserRequest(string Username, string Password);
public record UpdateUserRequest(string? NewUsername, string? NewPassword = null);
public record UserResponse(string Username, DateTime CreatedAt);

// -------------------------------------------------------------
// REPOSITÓRIO (THREAD-SAFE IN-MEMORY STORE)
// -------------------------------------------------------------
public interface IUserRepository
{
    Task<bool> ExistsAsync(string username);
    Task<User?> GetByUsernameAsync(string username);
    Task<IEnumerable<User>> GetAllAsync();
    Task<User> CreateAsync(string username, string password);
    Task<User?> UpdateAsync(string currentUsername, string newUsername, string? newPassword);
    Task<bool> DeleteAsync(string username);
}

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _usersById = new();
    private readonly ConcurrentDictionary<string, Guid> _idByUsername = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> ExistsAsync(string username)
    {
        return Task.FromResult(_idByUsername.ContainsKey(username));
    }

    public Task<User?> GetByUsernameAsync(string username)
    {
        if (_idByUsername.TryGetValue(username, out var id) && _usersById.TryGetValue(id, out var user))
        {
            return Task.FromResult<User?>(user);
        }

        return Task.FromResult<User?>(null);
    }

    public Task<IEnumerable<User>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<User>>(_usersById.Values.OrderBy(u => u.CreatedAt).ToList());
    }

    public Task<User> CreateAsync(string username, string password)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Password = password,
            CreatedAt = DateTime.UtcNow
        };

        _usersById[user.Id] = user;
        _idByUsername[username] = user.Id;

        return Task.FromResult(user);
    }

    public Task<User?> UpdateAsync(string currentUsername, string newUsername, string? newPassword)
    {
        if (!_idByUsername.TryGetValue(currentUsername, out var id) || !_usersById.TryGetValue(id, out var user))
        {
            return Task.FromResult<User?>(null);
        }

        if (!string.Equals(currentUsername, newUsername, StringComparison.OrdinalIgnoreCase))
        {
            _idByUsername.TryRemove(currentUsername, out _);
            user.Username = newUsername;
            _idByUsername[newUsername] = id;
        }

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            user.Password = newPassword;
        }

        return Task.FromResult<User?>(user);
    }

    public Task<bool> DeleteAsync(string username)
    {
        if (_idByUsername.TryRemove(username, out var id) && _usersById.TryRemove(id, out _))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}

public partial class Program { }
