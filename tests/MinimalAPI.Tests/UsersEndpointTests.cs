using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MinimalAPI.Tests;

public class UsersEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public UsersEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateUser_ReturnsCreated_WhenUsernameIsValid()
    {
        // Arrange
        var username = $"valid_user_{Guid.NewGuid():N}";
        var request = new { username };

        // Act
        var response = await _client.PostAsJsonAsync("/users", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("username", out var propUsername));
        Assert.Equal(username, propUsername.GetString());

        // Invariante de Segurança: O ID interno NUNCA deve ser exposto ao cliente
        Assert.False(root.TryGetProperty("id", out _), "O campo 'id' não deve ser exposto na resposta HTTP.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateUser_ReturnsBadRequest_WhenUsernameIsBlank(string username)
    {
        // Arrange
        var request = new { username };

        // Act
        var response = await _client.PostAsJsonAsync("/users", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_ReturnsConflict_WhenUsernameAlreadyExists()
    {
        // Arrange
        var username = $"duplicate_{Guid.NewGuid():N}";
        var request = new { username };

        // Act 1: Cria primeira vez
        var firstResponse = await _client.PostAsJsonAsync("/users", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act 2: Tenta criar com o mesmo username
        var duplicateResponse = await _client.PostAsJsonAsync("/users", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task CreateUser_ReturnsConflict_WhenUsernameMatchesCaseInsensitively()
    {
        // Arrange
        var baseName = $"case_user_{Guid.NewGuid():N}";

        // Act 1: Cria com minúsculo
        var firstResponse = await _client.PostAsJsonAsync("/users", new { username = baseName.ToLowerInvariant() });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act 2: Tenta com maiúsculo
        var secondResponse = await _client.PostAsJsonAsync("/users", new { username = baseName.ToUpperInvariant() });

        // Assert: Deve barrar duplicidade case-insensitive
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task GetUserByUsername_ReturnsUser_WhenUserExists()
    {
        // Arrange
        var username = $"get_user_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/users", new { username });

        // Act
        var response = await _client.GetAsync($"/users/{username}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(username, doc.RootElement.GetProperty("username").GetString());
        Assert.False(doc.RootElement.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task GetUserByUsername_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        var nonExistent = $"ghost_{Guid.NewGuid():N}";

        // Act
        var response = await _client.GetAsync($"/users/{nonExistent}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAllUsers_ReturnsList_ContainingCreatedUsers()
    {
        // Arrange
        var username = $"list_user_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/users", new { username });

        // Act
        var response = await _client.GetAsync("/users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.NotNull(users);
        Assert.Contains(users, u => u.Username == username);
    }

    [Fact]
    public async Task UpdateUser_ReturnsOk_WhenNewUsernameIsAvailable()
    {
        // Arrange
        var oldUsername = $"old_{Guid.NewGuid():N}";
        var newUsername = $"new_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/users", new { username = oldUsername });

        // Act: Atualiza username
        var putResponse = await _client.PutAsJsonAsync($"/users/{oldUsername}", new { newUsername });

        // Assert
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        // O novo username agora deve ser encontrado
        var getNewResponse = await _client.GetAsync($"/users/{newUsername}");
        Assert.Equal(HttpStatusCode.OK, getNewResponse.StatusCode);

        // O username antigo agora deve retornar 404 Not Found
        var getOldResponse = await _client.GetAsync($"/users/{oldUsername}");
        Assert.Equal(HttpStatusCode.NotFound, getOldResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_ReturnsConflict_WhenNewUsernameAlreadyBelongsToAnotherUser()
    {
        // Arrange
        var userA = $"usera_{Guid.NewGuid():N}";
        var userB = $"userb_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/users", new { username = userA });
        await _client.PostAsJsonAsync("/users", new { username = userB });

        // Act: Tenta renomear userA para userB
        var putResponse = await _client.PutAsJsonAsync($"/users/{userA}", new { newUsername = userB });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, putResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_ReturnsNoContent_AndRemovesUserFromSystem()
    {
        // Arrange
        var username = $"delete_user_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/users", new { username });

        // Act: Deleta usuário
        var deleteResponse = await _client.DeleteAsync($"/users/{username}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Consulta posterior deve retornar 404 Not Found
        var getResponse = await _client.GetAsync($"/users/{username}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        var ghost = $"ghost_del_{Guid.NewGuid():N}";

        // Act
        var response = await _client.DeleteAsync($"/users/{ghost}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record UserDto(string Username, DateTime CreatedAt);
}
