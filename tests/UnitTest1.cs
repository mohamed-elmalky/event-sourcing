using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace tests;

public class UnitTest1 : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient _client;

    public UnitTest1(WebApplicationFactory<Program> fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task HealthCheck()
    {
        // Arrange
        var url = "/";
        // Act
        var response = await _client.GetAsync(url);
        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task POST_Creates_Participant_With_Name_Only()
    {
        // Arrange
        var url = "/participants";
        var participant = new Participant("some-id") { Name = "John Doe" };
        var json = JsonSerializer.Serialize(participant);
        var data = new StringContent(json, Encoding.UTF8, "application/json");
        // Act
        var response = await _client.PostAsync(url, data);
        // Assert
        response.EnsureSuccessStatusCode();

        // get the url of the created participant
        var location = response.Headers.Location;
        // get the participant
        var response2 = await _client.GetAsync(location);
        response2.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task POST_Existing_SSN_Should_Fail()
    {
        // Arrange
        var url = "/participants";
        var participant = new Participant("some-id") { Name = "John Doe", SSN = "123-45-6789" };
        var data = new StringContent(JsonSerializer.Serialize(participant), Encoding.UTF8, "application/json");
        // Act
        var response = await _client.PostAsync(url, data);
        // Assert
        response.EnsureSuccessStatusCode();

        // try to create another participant with the same SSN
        var response2 = await _client.PostAsync(url, data);
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }

    [Fact]
    public async Task POST_Existing_Name_And_Home_Phone_Should_Fail()
    {
        // Arrange
        var url = "/participants";
        var participant = new Participant("some-id") { Name = "John Doe", HomePhone = "123-45-6789" };
        var data = new StringContent(JsonSerializer.Serialize(participant), Encoding.UTF8, "application/json");
        // Act
        var response = await _client.PostAsync(url, data);
        // Assert
        response.EnsureSuccessStatusCode();

        // try to create another participant with the same name and home phone
        var response2 = await _client.PostAsync(url, data);
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }

    [Fact]
    public async Task POST_Existing_Name_And_Cell_Phone_Should_Fail()
    {
        // Arrange
        var url = "/participants";
        var participant = new Participant("some-id") { Name = "John Doe", MobilePhone = "123-45-6789" };
        var data = new StringContent(JsonSerializer.Serialize(participant), Encoding.UTF8, "application/json");
        // Act
        var response = await _client.PostAsync(url, data);
        // Assert
        response.EnsureSuccessStatusCode();

        // try to create another participant with the same name and cell phone
        var response2 = await _client.PostAsync(url, data);
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }

    [Fact]
    public async Task POST_Existing_Name_And_Address_Should_Fail()
    {
        // Arrange
        var url = "/participants";
        var participant = new Participant("some-id") { Name = "John Doe", Address = new Address("123 Main St", "Ste 101", "Anytown", "NY", "USA", "75001") };
        var data = new StringContent(JsonSerializer.Serialize(participant), Encoding.UTF8, "application/json");
        // Act
        var response = await _client.PostAsync(url, data);
        // Assert
        response.EnsureSuccessStatusCode();

        // try to create another participant with the same name and address
        var response2 = await _client.PostAsync(url, data);
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }
}
